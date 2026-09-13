namespace Sugoi.Data;

public sealed partial class World
{
    internal IDisposable BeginStagingMutation() => BeginMutation();
    internal void StagingFault() => IsFaulted = true;
    internal void StagingReserve(Span<Entity> entities)
    {
        Registry.EnsureAvailable(entities.Length); Registry.PrepareRelease(entities.Length);
        for (int i = 0; i < entities.Length; i++) entities[i] = Registry.Allocate();
    }
    internal void StagingCreate(EntityType type, ReadOnlySpan<Entity> entities)
    {
        CreateReservedCore(GetGroup(type, validateMeta: false), entities, construct: true);
        if (!entities.IsEmpty) StructureVersion++;
    }
    internal void StagingChange(Entity entity, TypeDelta delta)
    {
        var previous = GetEntityType(entity);
        var target = delta.Apply(previous);
        if (target.Equals(previous)) return;
        ChangeTypeCore(entity, target);
        StructureVersion++;
    }
    internal nint StagingAddress(Entity entity, ComponentType type)
    {
        if (type.IsTag || !Registry.TryGet(entity, out var entry)) return 0;
        int slot = entry.Chunk!.Group.Archetype.Layout.IndexOf(type);
        return slot < 0 ? 0 : entry.Chunk.Address(slot, entry.Row);
    }
    internal void StagingMove(Entity entity, ComponentType type, ComponentOps ops, Entity staged, nint source)
    {
        ref var entry = ref Registry.ValidEntry(entity);
        var chunk = entry.Chunk!;
        int slot = chunk.Group.Archetype.Layout.IndexOf(type);
        if (slot < 0) throw new InvalidOperationException("Staged payload has no target column after structural migration.");
        nint destination = chunk.Address(slot, entry.Row);
        var targetContext = new ComponentContext(this, type.IsChunk ? Entity.Null : entity);
        // Ops.Move is construction into uninitialized storage. End the old/default value first,
        // including an existing owning buffer, instead of leaking it under raw header overwrite.
        ops.Destroy(targetContext, destination);
        ops.Move(new(null, staged), source, targetContext, destination);
        chunk.MarkChanged(slot);
    }
    internal void StagingQueryDelta(Query query, TypeDelta delta)
    {
        if (!ReferenceEquals(query.World, this)) throw new ArgumentException("Query belongs to a different World.");
        // The source query selects its group set before applying this query's delta.
        var matching = _groups.Values.Where(group => group.Count != 0 && query.MatchesGroupCore(group)).ToArray();
        bool changed = false;
        foreach (Group group in matching)
        {
            if (group.Count == 0) continue;
            EntityType type = delta.Apply(group.Type);
            if (type.Equals(group.Type)) continue;
            changed = true;
            // A Dead signature with no PIN is actual cleanup, not an empty dead group.
            bool finalCleanup = group.IsDead && !type.Components.ToArray().Any(t => t.IsPinned);
            Group? destination = finalCleanup ? null : GetGroup(type);
            if (destination is not null && ReferenceEquals(group.Archetype, destination.Archetype))
            {
                destination.Chunks.EnsureCapacity(checked(destination.Chunks.Count + group.Chunks.Count));
                foreach (Chunk chunk in group.Chunks.ToArray())
                {
                    RemapMasks(chunk, group.Type, type); group.Remove(chunk); destination.Add(chunk);
                }
            }
            else
            {
                Entity[] targets = group.Chunks.SelectMany(c => c.Entities.ToArray()).ToArray();
                foreach (Entity entity in targets) ChangeTypeCore(entity, type);
            }
        }
        if (changed) StructureVersion++;
    }
    internal Entity[] StagingOwnedTargets(ReadOnlySpan<Entity> roots)
    {
        var pending = new Queue<Entity>(roots.ToArray());
        var visited = new HashSet<Entity>(); var found = new HashSet<Entity>();
        while (pending.TryDequeue(out var owner))
        {
            if (!visited.Add(owner)) continue;
            foreach (var group in _groups.Values)
            {
                if (group.IsDead || group.IsDisabled || Array.BinarySearch(group.Type.MetaArray, owner) < 0) continue;
                foreach (var chunk in group.Chunks)
                    foreach (var entity in chunk.Entities) if (found.Add(entity)) pending.Enqueue(entity);
            }
        }
        return found.ToArray();
    }
    internal void StagingDestroy(ReadOnlySpan<Entity> entities)
    {
        Registry.PrepareRelease(entities.Length);
        var ordered = new List<(Entity Entity, long Chunk, int Row)>(entities.Length);
        foreach (Entity entity in entities)
        {
            ref var entry = ref Registry.ValidEntry(entity);
            ordered.Add((entity, entry.Chunk!.Id, entry.Row));
        }
        ordered.Sort(static (a, b) => a.Chunk == b.Chunk ? b.Row.CompareTo(a.Row) : a.Chunk.CompareTo(b.Chunk));
        foreach (var item in ordered) if (Registry.Exists(item.Entity)) DestroyCore(item.Entity);
        if (ordered.Count != 0) StructureVersion++;
    }
}
