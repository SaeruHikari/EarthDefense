namespace Sugoi.Data;

public sealed partial class World
{
    /// <summary>
    /// Renumbers identities densely and remaps stored components, buffers, chunk values and group meta.
    /// All old identities and borrows become invalid. External identities (including explicit query meta filters)
    /// must be rebound from the returned mapping by their owner.
    /// </summary>
    public int CompactEntityIds(Span<EntityMapping> mappings)
    {
        if (mappings.Length < EntityCount) throw new ArgumentException("Mapping destination is too small.", nameof(mappings));
        using var mutation = BeginMutation();
        var originalGroups = Groups.ToArray();
        var chunks = originalGroups.SelectMany(group => group.Chunks).ToArray();
        var plan = Registry.PreparePacking();
        var packed = plan.Mappings;
        var destination = new Entity[Registry.AllocatedCount];
        var generation = new uint[destination.Length];
        foreach (var pair in packed) { destination[pair.Source.Index] = pair.Destination; generation[pair.Source.Index] = pair.Source.Generation; }
        Entity Map(Entity entity) => !entity.IsNull && entity.Index < destination.Length && generation[entity.Index] == entity.Generation
            ? destination[entity.Index] : Entity.Null;
        EntityRemapper remapper = Map;
        var transfers = new List<(Group Source, Group Destination, Chunk[] Chunks)>();
        var incoming = new Dictionary<Group, int>();
        bool committed = false;
        try
        {
            // Prepare target signatures and list capacity before changing any entity identity.
            foreach (var source in originalGroups)
            {
                if (source.Type.MetaEntities.IsEmpty || source.Count == 0) continue;
                var meta = new List<Entity>();
                foreach (var entity in source.Type.MetaEntities) { var target = Map(entity); if (!target.IsNull) meta.Add(target); }
                var targetGroup = GetGroup(new EntityType(source.Type.Components, meta.ToArray()), validateMeta: false);
                if (ReferenceEquals(source, targetGroup)) continue;
                var moving = source.Chunks.ToArray();
                transfers.Add((source, targetGroup, moving));
                incoming.TryGetValue(targetGroup, out int previous);
                incoming[targetGroup] = checked(previous + moving.Length);
            }
            foreach (var pair in incoming) pair.Key.Chunks.EnsureCapacity(checked(pair.Key.Chunks.Count + pair.Value));
            Registry.CommitPacking(plan);
            committed = true;
            // Complete the identity column pass before invoking user remappers. Even a forbidden
            // throwing remapper then leaves ownership coherent enough for deterministic disposal.
            foreach (var chunk in chunks)
                for (int row = 0; row < chunk.Count; row++) chunk.EntityCapacity[row] = Map(chunk.EntityCapacity[row]);
            foreach (var chunk in chunks) RemapChunkReferences(chunk, remapper);
            foreach (var transfer in transfers)
                foreach (var chunk in transfer.Chunks) { transfer.Source.Remove(chunk); transfer.Destination.Add(chunk); }
            TrimEmptyGroupsCore();
            packed.CopyTo(mappings);
            return packed.Length;
        }
        catch
        {
            if (committed) IsFaulted = true;
            throw;
        }
    }

    public void RedirectReferences(ReadOnlySpan<Entity> source, ReadOnlySpan<Entity> destination)
    {
        if (source.Length != destination.Length) throw new ArgumentException("Reference map lengths differ.");
        var map = new Dictionary<Entity, Entity>(source.Length);
        for (int i = 0; i < source.Length; i++) map.Add(source[i], destination[i]);
        RedirectReferences(entity => map.TryGetValue(entity, out var next) ? next : entity);
    }

    public void DestroyOwned(Entity meta)
    {
        using var mutation = BeginMutation();
        var pending = new Queue<Entity>(); pending.Enqueue(meta);
        var visited = new HashSet<Entity>();
        var destroying = new List<Entity>();
        while (pending.TryDequeue(out var owner))
        {
            if (!visited.Add(owner)) continue;
            foreach (var group in Groups)
            {
                if (Array.BinarySearch(group.Type.MetaArray, owner) < 0) continue;
                foreach (var chunk in group.Chunks)
                    foreach (var entity in chunk.Entities) { destroying.Add(entity); pending.Enqueue(entity); }
            }
        }
        foreach (var entity in destroying) if (Registry.Exists(entity)) DestroyCore(entity);
    }
}
