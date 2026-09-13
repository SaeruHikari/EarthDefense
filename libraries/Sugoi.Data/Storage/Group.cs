namespace Sugoi.Data;

internal sealed class Group
{
    internal readonly long Id;
    internal readonly World World;
    internal readonly EntityType Type;
    internal readonly Archetype Archetype;
    internal readonly List<Chunk> Chunks = [];
    internal int FirstFree;
    internal int Count;
    internal readonly bool IsDead;
    internal readonly bool IsDisabled;
    internal readonly int MaskSlot;
    internal readonly int DirtySlot;
    private nint _metadata;
    private readonly MetadataLayout _metadataLayout;
    internal ReadOnlySpan<ComponentType> Components => MetadataPacking.Types(_metadata, _metadataLayout);
    internal ReadOnlySpan<Entity> MetaEntities => MetadataPacking.Meta(_metadata, _metadataLayout);
    private ulong _sharedVersion = ulong.MaxValue;
    private readonly Dictionary<ComponentType, Entity> _sharedOwners = [];

    internal Group(World world, long id, EntityType type, Archetype archetype)
    {
        World = world; Id = id; Type = type; Archetype = archetype;
        IsDead = type.Contains(world.Runtime.DeadType); IsDisabled = type.Contains(world.Runtime.DisabledType);
        MaskSlot = archetype.Layout.IndexOf(world.Runtime.EnabledMaskType);
        DirtySlot = archetype.Layout.IndexOf(world.Runtime.DirtyMaskType);
        if ((MaskSlot >= 0 || DirtySlot >= 0) && type.Components.Length > 32)
            throw new ArgumentException("Enabled/dirty masks support at most 32 local component slots.");
        if (MetadataPacking.Size(type.Components.Length, type.MetaEntities.Length) > NativeFixedBlockPool.BlockBytes)
            throw new ArgumentException("Group signature exceeds its 1024-byte metadata block.");
        _metadata = world.GroupPool.Rent();
        _metadataLayout = MetadataPacking.Write(_metadata, NativeFixedBlockPool.BlockBytes, type.Components, type.MetaEntities);
    }

    internal bool Owns(ComponentType type) => Components.BinarySearch(type) >= 0;
    internal bool Has(ComponentType type) => Owns(type) || Shares(type);
    internal bool Shares(ComponentType type) { RefreshShared(); return _sharedOwners.ContainsKey(type); }
    internal Entity SharedOwner(ComponentType type)
    {
        RefreshShared();
        return _sharedOwners.TryGetValue(type, out var entity) ? entity : Entity.Null;
    }

    private void RefreshShared()
    {
        lock (_sharedOwners)
        {
            if (_sharedVersion == World.StructureVersion) return;
            _sharedOwners.Clear();
            var visited = new HashSet<Entity>();
            foreach (var meta in MetaEntities) Collect(meta, visited);
            _sharedVersion = World.StructureVersion;
        }
    }

    private void Collect(Entity meta, HashSet<Entity> visited)
    {
        if (!visited.Add(meta) || !World.Registry.TryGet(meta, out var entry)) return;
        var group = entry.Chunk!.Group;
        foreach (var type in group.Components) _sharedOwners.TryAdd(type, meta);
        foreach (var parent in group.MetaEntities) Collect(parent, visited);
    }

    internal uint ComponentMask(ReadOnlySpan<ComponentType> types)
    {
        uint result = 0;
        foreach (var type in types)
        {
            int slot = Array.BinarySearch(Type.ComponentArray, type);
            if (slot >= 0)
            {
                if (slot >= 32) throw new InvalidOperationException("Mask local slot exceeds 31.");
                result |= 1u << slot;
            }
        }
        return result;
    }

    internal Chunk AcquireChunk(int hint)
    {
        if (FirstFree < Chunks.Count) return Chunks[FirstFree];
        var layout = Archetype.Layout;
        PoolKind kind = Chunks.Count == 0 && hint < layout.Small.Capacity ? PoolKind.Small :
            hint > (long)layout.Normal.Capacity * 8 ? PoolKind.Large : PoolKind.Normal;
        if (layout.For(kind).Capacity == 0) kind = PoolKind.Large;
        var chunk = new Chunk(this, kind);
        Add(chunk);
        return chunk;
    }

    internal void Add(Chunk chunk)
    {
        chunk.Group = this; chunk.Pool = Archetype.Layout.For(chunk.PoolKind);
        chunk.GroupIndex = Chunks.Count;
        Chunks.Add(chunk); Count += chunk.Count;
        if (chunk.Count == chunk.Capacity) MarkFull(chunk);
    }

    internal void Resize(Chunk chunk, int count)
    {
        bool wasFull = chunk.Count == chunk.Capacity;
        Count += count - chunk.Count; chunk.Count = count;
        if (!wasFull && count == chunk.Capacity) MarkFull(chunk);
        else if (wasFull && count < chunk.Capacity) MarkFree(chunk);
    }

    private void Swap(int a, int b)
    {
        if (a == b) return;
        (Chunks[a], Chunks[b]) = (Chunks[b], Chunks[a]);
        Chunks[a].GroupIndex = a; Chunks[b].GroupIndex = b;
    }
    private void MarkFull(Chunk chunk) { Swap(chunk.GroupIndex, FirstFree); FirstFree++; }
    private void MarkFree(Chunk chunk) { FirstFree--; Swap(chunk.GroupIndex, FirstFree); }

    internal void Remove(Chunk chunk)
    {
        int index = chunk.GroupIndex;
        if (index < FirstFree) FirstFree--;
        Count -= chunk.Count;
        Chunks.RemoveAt(index);
        for (int i = index; i < Chunks.Count; i++) Chunks[i].GroupIndex = i;
    }

    internal void ReleaseMetadata()
    {
        if (_metadata == 0) return;
        World.GroupPool.Return(_metadata); _metadata = 0;
    }
}
