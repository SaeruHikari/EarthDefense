using System.Runtime.CompilerServices;

namespace Sugoi.Data;

/// <summary>Owns entity state and performs structural operations immediately at an exclusive boundary.</summary>
public sealed partial class World : IDisposable
{
    private readonly Dictionary<EntityType, Group> _groups = [];
    private readonly Dictionary<EntityType, Archetype> _archetypes = [];
    private readonly Dictionary<long, Group> _groupsById = [];
    private readonly HashSet<Query> _queries = [];
    private readonly Dictionary<string, (ComponentType Type, int Phase)> _aliases = new(StringComparer.Ordinal);
    private long _nextGroup;
    private int _nextPhase;
    private long _queryRevision;
    private int _users; // -1 is a structural operation; positive values are live task/host uses. No component locks.
    internal readonly EntityRegistry Registry;
    internal readonly NativeFixedBlockPool GroupPool = new();
    public EcsRuntime Runtime { get; }
    public TypeRegistry Types => Runtime.Types;
    public ulong StructureVersion { get; private set; } = 1;
    public uint ChangeVersion { get; private set; } = 1;
    public int EntityCount => Registry.Count;
    public bool IsDisposed { get; private set; }
    public bool IsFaulted { get; private set; }
    public int ActiveUsers => Math.Max(0, Volatile.Read(ref _users));
    internal IEnumerable<Group> Groups => _groups.Values;
    internal long QueryRevision => Volatile.Read(ref _queryRevision);

    internal World(EcsRuntime runtime, int maximumEntities) { Runtime = runtime; Registry = new(maximumEntities); }

    /// <summary>Holds storage addresses stable for a complete asynchronous use, including admission waits.</summary>
    public IDisposable AcquireUsage()
    {
        EnsureAlive();
        while (true)
        {
            EnsureAlive();
            int count = Volatile.Read(ref _users);
            if (count < 0) throw new InvalidOperationException("World is being structurally modified. Submit after the structural phase.");
            if (count == int.MaxValue) throw new InvalidOperationException("Too many world users.");
            if (Interlocked.CompareExchange(ref _users, count + 1, count) == count) return new Usage(this);
        }
    }

    private sealed class Usage(World world) : IDisposable
    {
        private World? _world = world;
        public void Dispose() { var owner = Interlocked.Exchange(ref _world, null); if (owner is not null) Interlocked.Decrement(ref owner._users); }
    }

    private Mutation BeginMutation()
    {
        EnsureAlive();
        if (Interlocked.CompareExchange(ref _users, -1, 0) != 0)
            throw new InvalidOperationException("Structural operations require all world users to finish; lifecycle hooks cannot reenter structural operations.");
        ChangeVersion = ChangeVersion == uint.MaxValue ? 1 : ChangeVersion + 1;
        return new Mutation(this);
    }

    private readonly struct Mutation(World world) : IDisposable
    {
        public void Dispose() { world.StructureVersion++; Volatile.Write(ref world._users, 0); }
    }

    internal void EnsureAlive()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (IsFaulted) throw new InvalidOperationException("World is faulted after a failed lifecycle operation.");
    }

    public uint AdvanceChangeVersion()
    {
        EnsureAlive();
        if (Interlocked.CompareExchange(ref _users, -1, 0) != 0) throw new InvalidOperationException("Advance versions between task phases.");
        try { return ChangeVersion = ChangeVersion == uint.MaxValue ? 1 : ChangeVersion + 1; }
        finally { Volatile.Write(ref _users, 0); }
    }

    public bool Exists(Entity entity) => !IsDisposed && Registry.Exists(entity);
    public bool IsAlive(Entity entity) => !IsDisposed && Registry.TryGet(entity, out var entry) && !entry.Chunk!.Group.IsDead;
    public bool Has(Entity entity, ComponentType type) => Registry.TryGet(entity, out var entry) && entry.Chunk!.Group.Has(type);
    public bool Has<T>(Entity entity) where T : unmanaged => Has(entity, Types.Get<T>());
    public EntityType GetEntityType(Entity entity) => Registry.ValidEntry(entity).Chunk!.Group.Type;

    public Entity Create(EntityType type)
    {
        Span<Entity> result = stackalloc Entity[1];
        Create(type, result);
        return result[0];
    }

    public void Create(EntityType type, Span<Entity> destination)
    {
        using var mutation = BeginMutation();
        var group = GetGroup(type);
        Registry.EnsureAvailable(destination.Length);
        int reserved = 0;
        try
        {
            for (; reserved < destination.Length; reserved++) destination[reserved] = Registry.Allocate();
            CreateReservedCore(group, destination, construct: true);
        }
        catch
        {
            for (int i = 0; i < reserved; i++)
                if (!Registry.Exists(destination[i])) Registry.Release(destination[i]);
            IsFaulted = true;
            throw;
        }
    }

    public void ReserveEntities(Span<Entity> destination)
    {
        using var mutation = BeginMutation();
        Registry.EnsureAvailable(destination.Length);
        for (int i = 0; i < destination.Length; i++) destination[i] = Registry.Allocate();
    }

    public void CancelReservation(ReadOnlySpan<Entity> entities)
    {
        using var mutation = BeginMutation();
        ValidateReservations(entities);
        foreach (var entity in entities) Registry.Release(entity);
    }

    public void CreateReserved(EntityType type, ReadOnlySpan<Entity> entities)
    {
        using var mutation = BeginMutation();
        ValidateReservations(entities);
        var group = GetGroup(type);
        try { CreateReservedCore(group, entities, construct: true); }
        catch { IsFaulted = true; throw; }
    }

    private void ValidateReservations(ReadOnlySpan<Entity> entities)
    {
        var seen = new HashSet<Entity>();
        foreach (var entity in entities)
        {
            if (!seen.Add(entity)) throw new ArgumentException("Reserved identities must be unique.", nameof(entities));
            if (Registry.ValidEntry(entity, true).Chunk is not null) throw new ArgumentException("Entity is already created.", nameof(entities));
        }
    }

    private void CreateReservedCore(Group group, ReadOnlySpan<Entity> entities, bool construct)
    {
        int cursor = 0;
        while (cursor < entities.Length)
        {
            Chunk chunk = group.AcquireChunk(entities.Length - cursor);
            int start = chunk.Count, count = Math.Min(chunk.Capacity - start, entities.Length - cursor);
            ReadOnlySpan<Entity> values = entities.Slice(cursor, count);
            values.CopyTo(chunk.EntityCapacity.Slice(start, count));
            for (int i = 0; i < count; i++) Registry.Attach(values[i], chunk, start + i);
            group.Resize(chunk, start + count);
            if (construct)
                for (int slot = 0; slot < group.Archetype.Layout.FirstChunkComponent; slot++)
                {
                    group.Archetype.Columns[slot].Ops.Construct(this, values, chunk.Address(slot, start));
                    chunk.MarkChanged(slot);
                }
            cursor += count;
        }
    }

    internal Group GetGroup(EntityType type, bool validateMeta = true)
    {
        if (validateMeta)
            foreach (var meta in type.MetaEntities)
                if (!Registry.Exists(meta)) throw new ArgumentException("Meta entity must exist in the same World.");
        if (_groups.TryGetValue(type, out var found)) return found;
        foreach (var component in type.Components) _ = Types.Get(component);
        var physical = new List<ComponentType>(type.Components.Length);
        foreach (var component in type.Components) if (!component.IsTag) physical.Add(component);
        var key = new EntityType(physical.ToArray());
        if (!_archetypes.TryGetValue(key, out var archetype))
        {
            archetype = new Archetype(key, Types);
            _archetypes.Add(key, archetype);
        }
        var group = new Group(this, ++_nextGroup, type, archetype);
        _groups.Add(type, group); _groupsById.Add(group.Id, group);
        return group;
    }

    internal bool TryGetGroup(long id, out Group group) => _groupsById.TryGetValue(id, out group!);

    public ChunkView GetView(Entity entity)
    {
        EnsureAlive();
        ref var entry = ref Registry.ValidEntry(entity);
        return new ChunkView(new QueryRange(this, entry.Chunk!, entry.Row, 1));
    }

    public bool TryGetView(Entity entity, out ChunkView view)
    {
        if (!IsDisposed && Registry.TryGet(entity, out var entry)) { view = new(new QueryRange(this, entry.Chunk!, entry.Row, 1)); return true; }
        view = default; return false;
    }

    public ref T Get<T>(Entity entity) where T : unmanaged
    {
        var view = GetView(entity);
        if (Types.Get<T>().IsChunk) return ref view.WriteChunk<T>();
        return ref view.WriteOwned<T>()[0];
    }
    public ref readonly T Read<T>(Entity entity) where T : unmanaged
    {
        var view = GetView(entity);
        var type = Types.Get<T>();
        if (!view.HasOwned(type)) return ref view.ReadShared<T>();
        if (type.IsChunk) return ref view.ReadChunk<T>();
        return ref view.ReadOwned<T>()[0];
    }
    public void Set<T>(Entity entity, in T value) where T : unmanaged
    {
        var descriptor = Types.Describe<T>();
        ref var entry = ref Registry.ValidEntry(entity);
        var chunk = entry.Chunk!;
        int slot = chunk.Group.Archetype.Layout.IndexOf(descriptor.Type);
        if (slot < 0 || descriptor.Type.IsBuffer || descriptor.Type.IsTag) throw new ArgumentException("Entity does not own a value column of that type.");
        if (descriptor.Ops.IsTrivial) { Get<T>(entity) = value; return; }
        SetCore(entity, chunk, slot, in value);
    }

    private unsafe void SetCore<T>(Entity entity, Chunk chunk, int slot, in T value) where T : unmanaged
    {
        var descriptor = chunk.Group.Archetype.Columns[slot];
        var context = new ComponentContext(this, entity);
        T local = value;
        nint pointer = chunk.Address(slot, Registry.ValidEntry(entity).Row);
        // Copy before destroying the old value: callers may pass a borrowed reference to it.
        int alignment = Math.Max(IntPtr.Size, descriptor.Layout.Alignment);
        nint temporary = (nint)System.Runtime.InteropServices.NativeMemory.AlignedAlloc((nuint)LayoutMath.AlignUp(descriptor.Layout.Size, alignment), (nuint)alignment);
        if (temporary == 0) throw new OutOfMemoryException();
        try
        {
            descriptor.Ops.Copy(context, (nint)Unsafe.AsPointer(ref local), context, temporary);
            descriptor.Ops.Destroy(context, pointer);
            descriptor.Ops.Move(context, temporary, context, pointer);
            chunk.MarkChanged(slot);
        }
        finally { System.Runtime.InteropServices.NativeMemory.AlignedFree((void*)temporary); }
    }

    public Query CreateQuery(QueryDescription description)
    {
        using var use = AcquireUsage();
        var query = new Query(this, description.Copy());
        lock (_queries) { _queries.Add(query); Interlocked.Increment(ref _queryRevision); }
        return query;
    }
    internal void ReleaseQuery(Query query) { lock (_queries) { _queries.Remove(query); Interlocked.Increment(ref _queryRevision); } }
    internal Query[] QuerySnapshot() { lock (_queries) return _queries.ToArray(); }
    internal IDisposable AcquireQueryUsage(Query query)
    {
        lock (_queries)
        {
            EnsureAlive();
            if (Volatile.Read(ref _users) < 0) throw new InvalidOperationException("World is in an exclusive structural phase.");
            return query.AcquireUsageCore();
        }
    }

    public void MakeAlias(string name, ComponentType type)
    {
        using var mutation = BeginMutation();
        _ = Types.Get(type);
        _aliases.Add(name, (type, ++_nextPhase));
    }
    internal bool TryAlias(string name, out (ComponentType Type, int Phase) alias) => _aliases.TryGetValue(name, out alias);
    public ComponentAccessor<T> GetAccessor<T>() where T : unmanaged => new(this);
    public BufferColumn<T> GetBuffer<T>(Entity entity) where T : unmanaged => GetView(entity).WriteBuffer<T>(0);

    public void Dispose()
    {
        if (IsDisposed) return;
        if (Interlocked.CompareExchange(ref _users, -1, 0) != 0) throw new InvalidOperationException("Finish all world uses before disposal.");
        try
        {
            lock (_queries)
            {
                foreach (var query in _queries)
                    if (query.ActiveUses != 0) throw new InvalidOperationException("Release all query uses before disposing the world.");
                foreach (var query in _queries.ToArray()) query.Dispose();
            }
            ResetCore();
            foreach (var group in _groups.Values) group.ReleaseMetadata();
            _groups.Clear(); _groupsById.Clear(); _archetypes.Clear(); GroupPool.Dispose();
            IsDisposed = true;
            Runtime.Released(this);
        }
        finally { Volatile.Write(ref _users, IsDisposed ? -1 : 0); }
    }

    public void Reset()
    {
        using var mutation = BeginMutation();
        ResetCore();
    }

    private void ResetCore()
    {
        foreach (var group in _groups.Values)
        {
            foreach (var chunk in group.Chunks.ToArray())
            {
                for (int slot = 0; slot < group.Archetype.Layout.FirstChunkComponent; slot++)
                    group.Archetype.Columns[slot].Ops.Destroy(this, chunk.Entities, chunk.Address(slot, 0));
                foreach (var entity in chunk.Entities) Registry.Release(entity);
                group.Remove(chunk); chunk.Count = 0; chunk.Release();
            }
        }
        foreach (var reserved in Registry.Snapshot(includeReserved: true)) Registry.Release(reserved);
        TrimEmptyGroupsCore();
    }
}
