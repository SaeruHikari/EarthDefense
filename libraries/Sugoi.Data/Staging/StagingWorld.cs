using System.Runtime.CompilerServices;

namespace Sugoi.Data;

/// <summary>
/// Concurrent structural staging using per-type native payload rows and coalesced entity records.
/// This is not a second entity World or a chronological command log. Apply is an explicit safe-phase operation.
/// </summary>
public sealed partial class StagingWorld : IDisposable
{
    private readonly ReaderWriterLockSlim _phase = new(LockRecursionPolicy.SupportsRecursion);
    private readonly object _lookupGate = new(), _rowsGate = new(), _queriesGate = new(), _destroyMetaGate = new();
    private StagingRecord?[] _existingLookup, _spawnLookup;
    private readonly List<StagingRecord> _records;
    private readonly List<StagingRow?> _rows = [];
    private readonly Dictionary<Query, (StagingChange Change, IDisposable Lease)> _queries = [];
    private readonly List<Entity> _destroyMeta = [];
    private int _recordCount, _spawningCount;
    public EcsRuntime Runtime { get; }
    public bool IsDisposed { get; private set; }
    public bool IsFaulted { get; private set; }
    public int RecordCount => Volatile.Read(ref _recordCount);
    public int TransientCount => Volatile.Read(ref _spawningCount);
    public long AllocatedPayloadBytes { get { lock (_rowsGate) return _rows.Sum(r => r?.AllocatedBytes ?? 0); } }

    /// <param name="maxStagingCount">Initial capacity hint, not a hard maximum (matching ECSStagingWorld.initialize).</param>
    public StagingWorld(EcsRuntime runtime, int entryCapacity = 1024, int maxStagingCount = 1024)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (entryCapacity < 0 || maxStagingCount < 0) throw new ArgumentOutOfRangeException();
        if (runtime.IsDisposed) throw new ObjectDisposedException(nameof(runtime));
        Runtime = runtime;
        _existingLookup = new StagingRecord?[entryCapacity];
        _spawnLookup = new StagingRecord?[maxStagingCount];
        _records = new(maxStagingCount);
    }

    private PhaseScope EnterProducer()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (IsFaulted) throw new InvalidOperationException("Faulted staging must be cleared before reuse.");
        if (_phase.IsWriteLockHeld || !_phase.TryEnterReadLock(0))
            throw new InvalidOperationException("Cannot stage while Apply, Clear, or Dispose is active.");
        if (IsDisposed) { _phase.ExitReadLock(); throw new ObjectDisposedException(nameof(StagingWorld)); }
        return new(_phase, false);
    }
    private PhaseScope EnterExclusive(bool allowFault = false)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!allowFault && IsFaulted) throw new InvalidOperationException("Faulted staging must be cleared before reuse.");
        if (_phase.IsReadLockHeld || _phase.IsWriteLockHeld || !_phase.TryEnterWriteLock(0))
            throw new InvalidOperationException("Finish staging producers before Apply, Clear, or Dispose; reentrancy is not supported.");
        return new(_phase, true);
    }
    private readonly struct PhaseScope(ReaderWriterLockSlim gate, bool write) : IDisposable
    {
        public void Dispose() { if (write) gate.ExitWriteLock(); else gate.ExitReadLock(); }
    }

    public Entity NewEntity()
    {
        using var scope = EnterProducer();
        int index;
        do
        {
            index = Volatile.Read(ref _spawningCount);
            if (index == int.MaxValue) throw new InvalidOperationException("Staging transient index capacity exhausted.");
        } while (Interlocked.CompareExchange(ref _spawningCount, index + 1, index) != index);
        // Merely issuing an identity does not create a spawn record.
        return Entity.CreateTransient((uint)index);
    }

    private StagingRecord Record(Entity entity)
    {
        if (entity.IsNull || entity.Index > int.MaxValue) throw new ArgumentException("Invalid staging entity.", nameof(entity));
        if (entity.IsTransient && entity.Index >= Volatile.Read(ref _spawningCount))
            throw new ArgumentException("Transient was not issued in this staging epoch.", nameof(entity));
        lock (_lookupGate)
        {
            ref StagingRecord?[] lookup = ref (entity.IsTransient ? ref _spawnLookup : ref _existingLookup);
            int index = (int)entity.Index;
            if (lookup.Length <= index) Array.Resize(ref lookup, checked(Math.Max(index + 1, Math.Max(4, lookup.Length * 2))));
            if (lookup[index] is { } found)
            {
                if (found.Entity != entity) throw new InvalidOperationException("Two generations of the same entity were staged in one epoch.");
                return found;
            }
            int recordIndex = _recordCount;
            if (_records.Count == recordIndex) _records.Add(new());
            var record = _records[recordIndex]; record.Reset(entity, recordIndex);
            lookup[index] = record;
            Volatile.Write(ref _recordCount, recordIndex + 1);
            return record;
        }
    }

    private StagingRow Row(ComponentType type)
    {
        ComponentDescriptor descriptor = Runtime.Types.Get(type);
        lock (_rowsGate)
        {
            while (_rows.Count <= type.Index) _rows.Add(null);
            return _rows[(int)type.Index] ??= new(descriptor);
        }
    }

    public unsafe void Add<T>(Entity entity, in T component) where T : unmanaged
    {
        if (!Runtime.Types.TryGet<T>(out var type)) { Append(entity, component); return; }
        T copy = component;
        AddRaw(entity, type, type.IsTag ? 0 : (nint)Unsafe.AsPointer(ref copy));
    }

    public void AddRaw(Entity entity, ComponentType type, nint payload)
    {
        using var scope = EnterProducer();
        var row = Row(type);
        if (!type.IsTag && payload == 0) throw new ArgumentNullException(nameof(payload));
        if (!type.IsTag && !row.Ops.CanCopy) throw new InvalidOperationException("Staged value requires a Copy hook.");
        var record = Record(entity);
        lock (record.Gate)
        {
            try
            {
                record.Change.Add(type);
                if (type.IsTag) return;
                nint destination = row.Ensure(record.Index);
                row.Destroy(record);
                row.Ops.Copy(new(null, entity), payload, new(null, entity), destination);
                row.Mark(record.Index, true);
            }
            catch { IsFaulted = true; throw; }
        }
    }

    /// <summary>Transfers an owning native value into staging, replacing its previous value and ending the caller's value lifetime.</summary>
    public unsafe void AddMove<T>(Entity entity, ref T component) where T : unmanaged
    {
        using var scope = EnterProducer();
        if (!Runtime.Types.TryGet<T>(out var type)) { AppendMove(entity, ref component); return; }
        var row = Row(type); var record = Record(entity);
        lock (record.Gate)
        {
            try
            {
                record.Change.Add(type);
                if (type.IsTag) { component = default; return; }
                nint destination = row.Ensure(record.Index);
                row.Destroy(record);
                row.Ops.Move(new(null, entity), (nint)Unsafe.AsPointer(ref component), new(null, entity), destination);
                row.Mark(record.Index, true);
            }
            catch { IsFaulted = true; throw; }
        }
    }

    public void AppendMove<T>(Entity entity, ref T element) where T : unmanaged
    {
        using var scope = EnterProducer();
        var type = Runtime.Types.GetBuffer<T>();
        var row = Row(type); var record = Record(entity);
        lock (record.Gate)
        {
            try
            {
                record.Change.Add(type);
                nint destination = row.Ensure(record.Index);
                if (!row.Constructed(record.Index)) { row.Ops.Construct(new(null, entity), destination); row.Mark(record.Index, true); }
                row.Ops.Buffer!.AppendMove(new(null, entity), destination, ref element);
            }
            catch { IsFaulted = true; throw; }
        }
    }

    public void Append<T>(Entity entity, in T element) where T : unmanaged
    {
        using var scope = EnterProducer();
        var type = Runtime.Types.GetBuffer<T>();
        var row = Row(type);
        if (!row.Ops.CanCopy) throw new InvalidOperationException("Buffer elements require a Copy hook; use AppendMove to transfer ownership.");
        var record = Record(entity);
        lock (record.Gate)
        {
            try
            {
                record.Change.Add(type);
                nint destination = row.Ensure(record.Index);
                if (!row.Constructed(record.Index)) { row.Ops.Construct(new(null, entity), destination); row.Mark(record.Index, true); }
                row.Ops.Buffer!.Append(new(null, entity), destination, element);
            }
            catch { IsFaulted = true; throw; }
        }
    }

    public void SetBuffer<T>(Entity entity, ReadOnlySpan<T> elements) where T : unmanaged
    {
        using var scope = EnterProducer();
        var type = Runtime.Types.GetBuffer<T>();
        var row = Row(type);
        if (!row.Ops.CanCopy) throw new InvalidOperationException("Buffer elements require a Copy hook for SetBuffer.");
        var record = Record(entity);
        lock (record.Gate)
        {
            try
            {
                record.Change.Add(type);
                nint destination = row.Ensure(record.Index);
                if (!row.Constructed(record.Index)) { row.Ops.Construct(new(null, entity), destination); row.Mark(record.Index, true); }
                row.Ops.Buffer!.Set(new(null, entity), destination, elements);
            }
            catch { IsFaulted = true; throw; }
        }
    }

    public void Remove<T>(Entity entity) where T : unmanaged => Remove(entity,
        Runtime.Types.TryGet<T>(out var type) ? type : Runtime.Types.GetBuffer<T>());
    public void RemoveBuffer<T>(Entity entity) where T : unmanaged => Remove(entity, Runtime.Types.GetBuffer<T>());
    public void Remove(Entity entity, ComponentType type)
    {
        using var scope = EnterProducer();
        _ = Runtime.Types.Get(type);
        var record = Record(entity);
        lock (record.Gate)
        {
            try { if (record.Change.Remove(type) && !type.IsTag) Row(type).Destroy(record); }
            catch { IsFaulted = true; throw; }
        }
    }
    public void Destroy(Entity entity)
    {
        using var scope = EnterProducer();
        if (entity.IsTransient) throw new ArgumentException("The source staging API does not destroy transient identities.", nameof(entity));
        var record = Record(entity);
        lock (record.Gate) record.Destroy = true;
    }
    public void DestroyOwned(Entity meta)
    {
        using var scope = EnterProducer();
        if (meta.IsNull || meta.IsTransient) throw new ArgumentException("Meta-scoped destruction requires an existing identity.", nameof(meta));
        lock (_destroyMetaGate) if (!_destroyMeta.Contains(meta)) _destroyMeta.Add(meta);
    }
    public void AddMeta(Entity entity, Entity meta)
    {
        using var scope = EnterProducer();
        if (meta.IsNull) throw new ArgumentException("Meta cannot be Null.", nameof(meta));
        var record = Record(entity); lock (record.Gate) record.Change.AddMeta(meta);
    }
    public void RemoveMeta(Entity entity, Entity meta)
    {
        using var scope = EnterProducer();
        if (meta.IsNull) throw new ArgumentException("Meta cannot be Null.", nameof(meta));
        var record = Record(entity); lock (record.Gate) record.Change.RemoveMeta(meta);
    }

    public void AddTag<T>(Query query) where T : unmanaged => AddTag(query, Runtime.Types.Get<T>());
    public void AddTag(Query query, ComponentType type)
    {
        if (!type.IsTag) throw new ArgumentException("Query-scoped Add accepts tags only.", nameof(type));
        StageQuery(query, type, true);
    }
    public void Remove<T>(Query query) where T : unmanaged => Remove(query,
        Runtime.Types.TryGet<T>(out var type) ? type : Runtime.Types.GetBuffer<T>());
    public void Remove(Query query, ComponentType type) => StageQuery(query, type, false);
    private void StageQuery(Query query, ComponentType type, bool add)
    {
        using var scope = EnterProducer();
        ArgumentNullException.ThrowIfNull(query);
        if (query.World.Runtime != Runtime) throw new ArgumentException("Query belongs to another runtime.");
        _ = Runtime.Types.Get(type);
        lock (_queriesGate)
        {
            if (!_queries.TryGetValue(query, out var staged))
            {
                staged = (new(), query.AcquireUsage()); _queries.Add(query, staged);
            }
            if (add) staged.Change.Add(type); else staged.Change.Remove(type);
        }
    }

    public void Clear()
    {
        using var scope = EnterExclusive(allowFault: true);
        ClearCore(); IsFaulted = false;
    }
    public void ScanResourceReferences(ResourceVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        using var scope = EnterExclusive();
        foreach (var row in _rows)
        {
            if (row is null || !row.Ops.HasResources) continue;
            for (int i = 0; i < _recordCount; i++)
                if (row.ConstructedExclusive(i)) row.Ops.ScanResources(row.PointerExclusive(i), visitor);
        }
    }
    private void ClearCore()
    {
        foreach (var row in _rows)
            if (row is not null)
                for (int i = 0; i < _recordCount; i++) row.DestroyExclusive(_records[i]);
        for (int i = 0; i < _recordCount; i++)
        {
            var record = _records[i];
            if (record.Entity.IsTransient) _spawnLookup[record.Entity.Index] = null;
            else _existingLookup[record.Entity.Index] = null;
            record.Reset(Entity.Null, i);
        }
        foreach (var entry in _queries.Values) entry.Lease.Dispose();
        _queries.Clear(); _destroyMeta.Clear();
        Volatile.Write(ref _recordCount, 0); Volatile.Write(ref _spawningCount, 0);
    }
    public void Dispose()
    {
        if (IsDisposed) return;
        using (var scope = EnterExclusive(allowFault: true))
        {
            ClearCore(); foreach (var row in _rows) row?.Dispose(); _rows.Clear();
            IsDisposed = true;
        }
        // Do not dispose the gate while a rejected racing producer may still be inspecting it.
        GC.SuppressFinalize(this);
    }
}
