using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>Native ring reservation/publication and segmented overflow. Queue capacity is not a message-dropping limit.</summary>
public unsafe class MessageSubscription : IDisposable
{
    private readonly object _lifetime = new(), _consumeGate = new(), _overflowGate = new();
    private readonly IDisposable _queryLease;
    private readonly long[] _published;
    private readonly byte[] _valid;
    private readonly List<OverflowBlock> _overflow = new();
    private nint _values, _targets;
    private long _head, _reservedTail, _overflowMessages, _overflowBlocks, _dropped;
    private int _users;
    private volatile bool _disposed;
    private bool _consuming;

    internal MessageSubscription(MessageBus bus, Query query, MessageDescriptor descriptor, int capacity)
    {
        Bus = bus; Query = query; Descriptor = descriptor; Capacity = capacity;
        _queryLease = query.AcquireUsage();
        try
        {
            _values = MessageMemory.Allocate(descriptor.Layout.PayloadBytes(capacity), descriptor.Layout.Alignment);
            _targets = MessageMemory.Allocate(MessageLayout.EntityBytes(capacity), sizeof(Entity));
            _published = new long[capacity]; Array.Fill(_published, -1);
            _valid = new byte[capacity];
        }
        catch { MessageMemory.Free(_values); MessageMemory.Free(_targets); _queryLease.Dispose(); throw; }
    }
    public MessageBus Bus { get; }
    public Query Query { get; }
    public MessageDescriptor Descriptor { get; }
    public int Capacity { get; }
    public bool IsDisposed => _disposed;
    public long OverflowMessageCount => Interlocked.Read(ref _overflowMessages);
    public long OverflowBlockCount => Interlocked.Read(ref _overflowBlocks);
    public long DroppedCount => Interlocked.Read(ref _dropped);
    public int PublishedCount
    {
        get
        {
            if (_disposed) return 0;
            long head = Volatile.Read(ref _head);
            int window = PublishedWindow(head), count = 0;
            for (int i = 0; i < window; i++) if (_valid[(int)((head + i) % Capacity)] != 0) count++;
            return count;
        }
    }
    public int Count => _disposed ? 0 : checked(PublishedCount + (int)OverflowMessageCount);
    public int PendingCount => Count;
    public bool HasPending => Count != 0;

    internal bool TryUse()
    { lock (_lifetime) { if (_disposed) return false; _users++; return true; } }
    internal void EndUse()
    { lock (_lifetime) { if (--_users == 0) Monitor.PulseAll(_lifetime); } }

    private int PublishedWindow(long head)
    {
        int count = 0;
        while (count < Capacity)
        {
            long ticket = head + count;
            if (Volatile.Read(ref _published[(int)(ticket % Capacity)]) != ticket) break;
            count++;
        }
        return count;
    }

    private bool TryReserve(out long ticket)
    {
        while (true)
        {
            long head = Volatile.Read(ref _head), tail = Volatile.Read(ref _reservedTail);
            if (tail - head >= Capacity) { ticket = 0; return false; }
            if (Interlocked.CompareExchange(ref _reservedTail, tail + 1, tail) == tail) { ticket = tail; return true; }
        }
    }

    internal bool SendNative(Entity target, nint source, bool move)
    {
        if (!move && !Descriptor.IsCopyable) return false;
        if (TryReserve(out long ticket))
        {
            int slot = (int)(ticket % Capacity);
            nint destination = _values + checked(slot * Descriptor.Layout.Size);
            _valid[slot] = 0;
            try
            {
                if (move) Descriptor.Operations.Move(source, destination); else Descriptor.Operations.Copy(source, destination);
                ((Entity*)_targets)[slot] = target;
                _valid[slot] = 1;
            }
            catch { Interlocked.Increment(ref _dropped); throw; }
            finally
            {
                // A violated no-throw hook publishes a skipped slot rather than permanently blocking later tickets.
                Volatile.Write(ref _published[slot], ticket);
            }
            return true;
        }
        lock (_overflowGate)
        {
            OverflowBlock block;
            if (_overflow.Count == 0 || _overflow[^1].Count == Capacity)
            {
                try { block = new OverflowBlock(Descriptor, Capacity); }
                catch (OutOfMemoryException) { Interlocked.Increment(ref _dropped); return false; }
                _overflow.Add(block); Interlocked.Increment(ref _overflowBlocks);
            }
            else block = _overflow[^1];
            nint destination = block.Values + checked(block.Count * Descriptor.Layout.Size);
            if (move) Descriptor.Operations.Move(source, destination); else Descriptor.Operations.Copy(source, destination);
            ((Entity*)block.Targets)[block.Count++] = target;
            Interlocked.Increment(ref _overflowMessages);
            return true;
        }
    }

    /// <summary>Consumes a borrowed contiguous native view; no payload allocation for an unwrapped ring-only batch.</summary>
    public void Visit(RawMessageConsumer consumer)
    {
        using var use = Bus.World.AcquireUsage();
        VisitUnderUsage(consumer);
    }
    internal void VisitUnderUsage(RawMessageConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (!TryUse()) throw new ObjectDisposedException(nameof(MessageSubscription));
        try
        {
            lock (_consumeGate)
            {
                if (_consuming) throw new InvalidOperationException("A message consumer cannot recursively consume its own queue.");
                _consuming = true;
                try { ConsumeCore(consumer); }
                finally { _consuming = false; }
            }
        }
        finally { EndUse(); }
    }

    public MessageBatch Consume(bool validateTargets = true)
    {
        using var use = Bus.World.AcquireUsage();
        return ConsumeOwned(validateTargets);
    }
    internal MessageBatch ConsumeForDispatch() => ConsumeOwned(false);

    private MessageBatch ConsumeOwned(bool validateTargets)
    {
        MessageBatch? owner = null;
        try
        {
            VisitUnderUsage((in MessageBatchView batch) =>
            {
                owner = new MessageBatch(Descriptor, batch.Count);
                if (!validateTargets) owner.AppendMoved(in batch);
                else
                    for (int i = 0; i < batch.Count; i++)
                        if (Query.MatchesUnderUsage(batch.Targets[i])) owner.AppendMoved(batch.Targets[i], batch.ValueAddress(i));
            });
            return owner ?? new MessageBatch(Descriptor, 0);
        }
        catch { owner?.Dispose(); throw; }
    }

    private void ConsumeCore(RawMessageConsumer consumer)
    {
        long head = Volatile.Read(ref _head);
        int ringCount = PublishedWindow(head);
        OverflowBlock[] overflow;
        lock (_overflowGate)
        {
            overflow = _overflow.Count == 0 ? Array.Empty<OverflowBlock>() : _overflow.ToArray();
            _overflow.Clear(); Interlocked.Exchange(ref _overflowMessages, 0);
        }
        int overflowCount = 0;
        foreach (var block in overflow) overflowCount = checked(overflowCount + block.Count);
        if (ringCount == 0 && overflowCount == 0) return;
        int read = (int)(head % Capacity);
        bool contiguous = read + ringCount <= Capacity && overflowCount == 0;
        if (contiguous) for (int i = 0; i < ringCount; i++) if (_valid[read + i] == 0) { contiguous = false; break; }
        MessageBatch? linear = null;
        try
        {
            if (contiguous)
            {
                var view = new MessageBatchView(Descriptor, _values + checked(read * Descriptor.Layout.Size), _targets + read * sizeof(Entity), ringCount);
                consumer(in view);
            }
            else
            {
                linear = new MessageBatch(Descriptor, checked(ringCount + overflowCount));
                for (int i = 0; i < ringCount; i++)
                {
                    int slot = (read + i) % Capacity;
                    if (_valid[slot] != 0) linear.AppendMoved(((Entity*)_targets)[slot], _values + checked(slot * Descriptor.Layout.Size));
                }
                foreach (var block in overflow)
                {
                    var view = new MessageBatchView(Descriptor, block.Values, block.Targets, block.Count);
                    linear.AppendMoved(in view);
                }
                if (linear.Count != 0) { var view = linear.View; consumer(in view); }
            }
        }
        finally
        {
            try { linear?.Dispose(); }
            finally
            {
                try
                {
                    for (int i = 0; i < ringCount; i++)
                    {
                        int slot = (read + i) % Capacity;
                        if (_valid[slot] != 0) { Descriptor.Operations.Destroy(_values + checked(slot * Descriptor.Layout.Size)); _valid[slot] = 0; }
                    }
                    foreach (var block in overflow) block.DestroyValues(Descriptor);
                }
                finally
                {
                    foreach (var block in overflow) block.Dispose();
                    Volatile.Write(ref _head, head + ringCount);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lifetime)
        {
            if (_disposed) return;
            _disposed = true;
            while (_users != 0) Monitor.Wait(_lifetime);
        }
        Bus.Unsubscribe(this);
        try { lock (_consumeGate) DisposeQueuedValues(); }
        finally
        {
            MessageMemory.Free(_values); MessageMemory.Free(_targets); _values = _targets = 0;
            _queryLease.Dispose();
        }
    }

    private void DisposeQueuedValues()
    {
        long head = Volatile.Read(ref _head);
        int count = PublishedWindow(head);
        OverflowBlock[] overflow;
        lock (_overflowGate)
        {
            overflow = _overflow.ToArray(); _overflow.Clear(); Interlocked.Exchange(ref _overflowMessages, 0);
        }
        try
        {
            for (int i = 0; i < count; i++)
            {
                int slot = (int)((head + i) % Capacity);
                if (_valid[slot] != 0) { Descriptor.Operations.Destroy(_values + checked(slot * Descriptor.Layout.Size)); _valid[slot] = 0; }
            }
            foreach (var block in overflow) block.DestroyValues(Descriptor);
        }
        finally
        {
            foreach (var block in overflow) block.Dispose();
            Volatile.Write(ref _head, head + count);
        }
    }

    private sealed class OverflowBlock : IDisposable
    {
        internal readonly nint Values, Targets;
        internal int Count;
        internal OverflowBlock(MessageDescriptor descriptor, int capacity)
        {
            Values = MessageMemory.Allocate(descriptor.Layout.PayloadBytes(capacity), descriptor.Layout.Alignment);
            try { Targets = MessageMemory.Allocate(MessageLayout.EntityBytes(capacity), sizeof(Entity)); }
            catch { MessageMemory.Free(Values); throw; }
        }
        internal void DestroyValues(MessageDescriptor descriptor)
        { for (int i = 0; i < Count; i++) descriptor.Operations.Destroy(Values + checked(i * descriptor.Layout.Size)); }
        public void Dispose() { MessageMemory.Free(Values); MessageMemory.Free(Targets); }
    }
}

public sealed class MessageSubscription<T> : MessageSubscription where T : unmanaged
{
    internal MessageSubscription(MessageBus bus, Query query, MessageDescriptor descriptor, int capacity) : base(bus, query, descriptor, capacity) { }
    public new MessageBatch<T> Consume(bool validateTargets = true) => new(base.Consume(validateTargets));
    internal new MessageBatch<T> ConsumeForDispatch() => new(base.ConsumeForDispatch());
    public void Visit(MessageVisitor<T> consumer, bool validateTargets = true)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        using var use = Bus.World.AcquireUsage();
        VisitUnderUsage((in MessageBatchView batch) =>
        {
            var values = batch.Values<T>();
            for (int i = 0; i < batch.Count; i++)
                if (!validateTargets || Query.MatchesUnderUsage(batch.Targets[i])) consumer(batch.Targets[i], ref values[i]);
        });
    }
}
