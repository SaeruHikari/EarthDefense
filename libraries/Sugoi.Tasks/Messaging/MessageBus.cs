using System.Runtime.CompilerServices;
using System.Buffers;
using Sugoi.Data;

namespace Sugoi.Tasks;

public readonly record struct EntityMessage<T>(Entity Target, T Value) where T : unmanaged;

/// <summary>World-local message bindings and one native MPSC queue for each (message GUID, Query) pair.</summary>
public sealed unsafe class MessageBus : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, MessageSubscription[]> _subscriptions = new();
    private bool _disposed;
    private long _moveOnlyFanouts;
    [InlineArray(8)]
    private struct InlineSubscriptions { private MessageSubscription? _first; }
    public MessageBus(World world, MessageRegistry? registry = null)
    { World = world ?? throw new ArgumentNullException(nameof(world)); Registry = registry ?? new MessageRegistry(); }
    public World World { get; }
    public MessageRegistry Registry { get; }
    public long MoveOnlyFanoutCount => Interlocked.Read(ref _moveOnlyFanouts);

    public MessageSubscription<T> Subscribe<T>(Query query, int capacity = 8192) where T : unmanaged =>
        (MessageSubscription<T>)Subscribe(Registry.Get<T>().Id, query, capacity);
    /// <summary>Repeated registration returns the same queue; capacity belongs to its first registration.</summary>
    public MessageSubscription Subscribe(Guid messageType, Query query, int capacity = 8192)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!ReferenceEquals(query.World, World)) throw new ArgumentException("Subscription query belongs to another World.", nameof(query));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        var descriptor = Registry.Get(messageType);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_subscriptions.TryGetValue(messageType, out var existing)) existing = Array.Empty<MessageSubscription>();
            foreach (var queue in existing) if (ReferenceEquals(queue.Query, query)) return queue;
            var subscription = descriptor.Operations.CreateSubscription(this, query, descriptor, capacity);
            var next = new MessageSubscription[existing.Length + 1];
            existing.CopyTo(next, 0); next[^1] = subscription;
            _subscriptions[messageType] = next;
            return subscription;
        }
    }
    public MessageSubscription<T>? GetSubscription<T>(Query query) where T : unmanaged => (MessageSubscription<T>?)GetSubscription(Registry.Get<T>().Id, query);
    public MessageSubscription? GetSubscription(Guid type, Query query)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_subscriptions.TryGetValue(type, out var entries))
                foreach (var entry in entries) if (ReferenceEquals(entry.Query, query)) return entry;
            return null;
        }
    }
    public bool HasPending<T>(Query? query = null) where T : unmanaged => HasPending(Registry.Get<T>().Id, query);
    public bool HasPending(Guid type, Query? query = null)
    {
        foreach (var entry in Snapshot(type)) if ((query is null || ReferenceEquals(entry.Query, query)) && entry.HasPending) return true;
        return false;
    }

    public int Send<T>(Entity target, in T value) where T : unmanaged
    { using var use = World.AcquireUsage(); return SendUnderUsage(target, in value); }
    public int Send<T>(ReadOnlySpan<EntityMessage<T>> messages) where T : unmanaged
    {
        using var use = World.AcquireUsage();
        int accepted = 0;
        foreach (ref readonly var message in messages) { var value = message.Value; accepted = checked(accepted + SendUnderUsage(message.Target, in value)); }
        return accepted;
    }
    public int SendMove<T>(Entity target, ref T value) where T : unmanaged
    { using var use = World.AcquireUsage(); return SendMoveUnderUsage(target, ref value); }
    internal int SendUnderUsage<T>(Entity target, in T value) where T : unmanaged
    {
        var descriptor = Registry.Get<T>();
        if (!descriptor.IsCopyable) throw new InvalidOperationException("Use SendMove(ref value) for a move-only message.");
        fixed (T* source = &value) return SendTyped(target, descriptor, (nint)source, false);
    }
    internal int SendMoveUnderUsage<T>(Entity target, ref T value) where T : unmanaged
    {
        var descriptor = Registry.Get<T>();
        fixed (T* source = &value) return SendTyped(target, descriptor, (nint)source, true);
    }

    private int SendTyped(Entity target, MessageDescriptor descriptor, nint source, bool move)
    {
        var candidates = Snapshot(descriptor.Id);
        if (candidates.Length == 0) return 0;
        if (candidates.Length == 1)
        {
            var subscription = candidates[0];
            if (!subscription.TryUse()) return 0;
            try { return subscription.Query.MatchesUnderUsage(target) && subscription.SendNative(target, source, move) ? 1 : 0; }
            finally { subscription.EndUse(); }
        }
        InlineSubscriptions local = default;
        MessageSubscription?[]? rented = null;
        Span<MessageSubscription?> matched = candidates.Length <= 8 ? local : (rented = ArrayPool<MessageSubscription?>.Shared.Rent(candidates.Length));
        int matchedCount = 0;
        try
        {
            foreach (var subscription in candidates)
            {
                if (!subscription.TryUse()) continue;
                bool added = false;
                try { if (subscription.Query.MatchesUnderUsage(target)) { matched[matchedCount++] = subscription; added = true; } }
                finally { if (!added) subscription.EndUse(); }
            }
            if (matchedCount == 0) return 0;
            if (matchedCount == 1) return matched[0]!.SendNative(target, source, move) ? 1 : 0;
            if (!descriptor.IsCopyable)
            {
                Interlocked.Increment(ref _moveOnlyFanouts);
                return matched[0]!.SendNative(target, source, true) ? 1 : 0;
            }
            nint stable = MessageMemory.Allocate(descriptor.Layout.Size, descriptor.Layout.Alignment);
            bool constructed = false;
            try
            {
                if (move) descriptor.Operations.Move(source, stable); else descriptor.Operations.Copy(source, stable);
                constructed = true;
                int accepted = 0;
                for (int i = 0; i < matchedCount; i++) if (matched[i]!.SendNative(target, stable, false)) accepted++;
                return accepted;
            }
            finally
            {
                try { if (constructed) descriptor.Operations.Destroy(stable); }
                finally { MessageMemory.Free(stable); }
            }
        }
        finally
        {
            for (int i = 0; i < matchedCount; i++) matched[i]!.EndUse();
            if (rented is not null) ArrayPool<MessageSubscription?>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <summary>GUID-directed copy dispatch. Registered types with no subscribers succeed without taking ownership.</summary>
    public bool SendRaw(Entity target, Guid messageType, nint message)
    {
        using var use = World.AcquireUsage();
        if (message == 0 || !Registry.TryGet(messageType, out var descriptor)) return false;
        bool sent = true;
        foreach (var subscription in Snapshot(messageType))
        {
            if (!subscription.TryUse()) continue;
            try
            {
                if (subscription.Query.MatchesUnderUsage(target))
                    sent = descriptor!.IsCopyable && subscription.SendNative(target, message, false) && sent;
            }
            finally { subscription.EndUse(); }
        }
        return sent;
    }
    public bool SendRaw(Guid messageType, Entity target, nint message) => SendRaw(target, messageType, message);

    private MessageSubscription[] Snapshot(Guid type)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _subscriptions.TryGetValue(type, out var subscriptions) ? subscriptions : Array.Empty<MessageSubscription>();
        }
    }
    internal void Unsubscribe(MessageSubscription subscription)
    {
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(subscription.Descriptor.Id, out var current)) return;
            int index = Array.IndexOf(current, subscription);
            if (index < 0) return;
            if (current.Length == 1) _subscriptions.Remove(subscription.Descriptor.Id);
            else
            {
                var next = new MessageSubscription[current.Length - 1];
                Array.Copy(current, 0, next, 0, index); Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                _subscriptions[subscription.Descriptor.Id] = next;
            }
        }
    }
    public void Unsubscribe(Guid type, Query query) => GetSubscription(type, query)?.Dispose();
    public void Unsubscribe<T>(Query query) where T : unmanaged => Unsubscribe(Registry.Get<T>().Id, query);
    public void Unsubscribe(Query query)
    {
        MessageSubscription[] subscriptions;
        lock (_gate) subscriptions = _subscriptions.Values.SelectMany(s => s).Where(s => ReferenceEquals(s.Query, query)).ToArray();
        foreach (var subscription in subscriptions) subscription.Dispose();
    }
    public void Dispose()
    {
        MessageSubscription[] subscriptions;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            subscriptions = _subscriptions.Values.SelectMany(s => s).ToArray();
            _subscriptions.Clear();
        }
        List<Exception>? failures = null;
        foreach (var subscription in subscriptions)
            try { subscription.Dispose(); } catch (Exception exception) { (failures ??= new()).Add(exception); }
        if (failures is not null) throw new AggregateException(failures);
    }
}

/// <summary>Bound task sender; the surrounding task owns its World's stable lifetime.</summary>
public readonly struct MessageSender<T>(MessageBus bus) where T : unmanaged
{
    public int Send(Entity target, in T message) => bus.SendUnderUsage(target, in message);
    public int SendMove(Entity target, ref T message) => bus.SendMoveUnderUsage(target, ref message);
}

public readonly ref struct MessageConsumer<T> where T : unmanaged
{
    private readonly Span<T> _values;
    public MessageConsumer(Span<T> values) => _values = values;
    public Span<T> Values => _values;
    public int Count => _values.Length;
    public bool IsEmpty => _values.IsEmpty;
    public ref T this[int index] => ref _values[index];
}
