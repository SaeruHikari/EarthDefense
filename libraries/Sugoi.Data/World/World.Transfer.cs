namespace Sugoi.Data;

public sealed partial class World
{
    /// <summary>Reclaims native metadata for signatures with no chunks; existing query definitions remain reusable.</summary>
    public int TrimEmptyGroups()
    {
        using var mutation = BeginMutation();
        return TrimEmptyGroupsCore();
    }

    private int TrimEmptyGroupsCore()
    {
        Group[] empty = _groups.Values.Where(g => g.Count == 0 && g.Chunks.Count == 0).ToArray();
        foreach (Group group in empty)
        {
            _groups.Remove(group.Type);
            _groupsById.Remove(group.Id);
            group.ReleaseMetadata();
        }
        if (empty.Length != 0)
        {
            var retained = _groups.Values.Select(g => g.Archetype.PhysicalType).ToHashSet();
            foreach (EntityType key in _archetypes.Keys.ToArray())
                if (!retained.Contains(key)) _archetypes.Remove(key);
        }
        return empty.Length;
    }

    private sealed class DenseEntityMap(int length)
    {
        internal readonly Entity[] Source = new Entity[length];
        internal readonly Entity[] Destination = new Entity[length];
        internal Entity Map(Entity entity) => !entity.IsNull && entity.Index < Source.Length && Source[entity.Index] == entity
            ? Destination[entity.Index] : Entity.Null;
        internal void Add(Entity source, Entity destination) { Source[source.Index] = source; Destination[source.Index] = destination; }
    }

    /// <summary>
    /// Destructively imports all constructed source entities, including disabled and cleanup entities.
    /// Source-local references are remapped by complete identity; stale or unmapped references become Null.
    /// Both Worlds must use the same runtime and have no live users. Output is published only on success.
    /// </summary>
    public int MergeFrom(World source, Span<EntityMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(source, this)) throw new ArgumentException("Cannot merge a World into itself.", nameof(source));
        if (!ReferenceEquals(Runtime, source.Runtime)) throw new ArgumentException("World import requires the same runtime/type registry.", nameof(source));
        using var targetMutation = BeginMutation();
        using var sourceMutation = source.BeginMutation();
        Entity[] originals = source.Registry.Snapshot(); // Registry order is source index ascending.
        if (mappings.Length < originals.Length) throw new ArgumentException("Mapping output cannot hold every source entity.", nameof(mappings));
        if (originals.Length == 0) return 0;
        Registry.EnsureAvailable(originals.Length);
        Registry.PrepareRelease(originals.Length);
        source.Registry.PrepareRelease(originals.Length);
        var map = new DenseEntityMap(source.Registry.AllocatedCount);
        var destinations = new Entity[originals.Length];
        var transfers = new List<(Group Source, Group Destination, Chunk[] Chunks)>();
        int reserved = 0;
        bool remappingStarted = false;
        try
        {
            for (; reserved < originals.Length; reserved++)
            {
                destinations[reserved] = Registry.Allocate();
                map.Add(originals[reserved], destinations[reserved]);
            }
            var plans = new List<(Group Source, EntityType Type, Chunk[] Chunks)>();
            var newTypes = new HashSet<EntityType>();
            foreach (var group in source._groups.Values)
            {
                if (group.Count == 0) continue;
                EntityType targetType = RemappedType(group.Type, map.Map, dropPinned: false, dropDead: false);
                plans.Add((group, targetType, group.Chunks.ToArray()));
                if (!_groups.ContainsKey(targetType)) newTypes.Add(targetType);
            }
            if (GroupPool.LeasedCount + newTypes.Count > NativeFixedBlockPool.BlocksPerSlab)
                throw new InvalidOperationException("Imported signatures exceed the target's fixed group pool.");
            foreach (var plan in plans)
            {
                Group targetGroup = GetGroup(plan.Type, validateMeta: false);
                targetGroup.Chunks.EnsureCapacity(checked(targetGroup.Chunks.Count + plan.Chunks.Length));
                transfers.Add((plan.Source, targetGroup, plan.Chunks));
            }
            // Distinct source groups can collapse when stale meta references are removed.
            foreach (var grouping in transfers.GroupBy(t => t.Destination))
                grouping.Key.Chunks.EnsureCapacity(checked(grouping.Key.Chunks.Count + grouping.Sum(t => t.Chunks.Length)));
            EntityRemapper remapper = map.Map;
            remappingStarted = true;
            foreach (var transfer in transfers)
                foreach (Chunk chunk in transfer.Chunks)
                    RemapChunkReferences(chunk, remapper);
            // No callbacks or allocation remain between ownership removal and target attachment.
            foreach (var transfer in transfers)
                foreach (Chunk chunk in transfer.Chunks)
                {
                    transfer.Source.Remove(chunk);
                    for (int row = 0; row < chunk.Count; row++)
                    {
                        Entity old = chunk.Entities[row], next = map.Map(old);
                        source.Registry.Release(old);
                        Registry.Attach(next, chunk, row);
                        chunk.EntityCapacity[row] = next;
                    }
                    transfer.Destination.Add(chunk); // Rebinds Group, Archetype consumer, and per-pool layout.
                    for (int slot = 0; slot < transfer.Destination.Archetype.Columns.Length; slot++) chunk.MarkChanged(slot);
                }
            for (int i = 0; i < originals.Length; i++) mappings[i] = new(originals[i], destinations[i]);
            return originals.Length;
        }
        catch
        {
            for (int i = 0; i < reserved; i++)
                if (!Registry.Exists(destinations[i])) Registry.Release(destinations[i]);
            if (remappingStarted) { IsFaulted = true; source.IsFaulted = true; }
            throw;
        }
    }

    private static EntityType RemappedType(EntityType source, EntityRemapper map, bool dropPinned, bool dropDead, ComponentType dead = default)
    {
        var components = new List<ComponentType>(source.Components.Length);
        foreach (var type in source.Components)
            if ((!dropPinned || !type.IsPinned) && (!dropDead || type != dead)) components.Add(type);
        var meta = new List<Entity>(source.MetaEntities.Length);
        foreach (var entity in source.MetaEntities)
        {
            Entity mapped = map(entity);
            if (!mapped.IsNull) meta.Add(mapped);
        }
        return new EntityType(components.ToArray(), meta.ToArray());
    }

    private static void RemapChunkReferences(Chunk chunk, EntityRemapper remapper)
    {
        var columns = chunk.Group.Archetype.Columns;
        for (int slot = 0; slot < columns.Length; slot++)
        {
            var descriptor = columns[slot];
            if (!descriptor.Ops.HasReferences) continue;
            descriptor.Ops.Remap(chunk.Address(slot, 0), descriptor.Type.IsChunk ? 1 : chunk.Count, remapper);
            chunk.MarkChanged(slot);
        }
    }

    public Entity Instantiate(Entity prefab) => Instantiate(this, prefab);
    public Entity Instantiate(World source, Entity prefab)
    {
        Span<Entity> destination = stackalloc Entity[1];
        Instantiate(source, prefab, destination);
        return destination[0];
    }

    /// <summary>Copies one prefab repeatedly. Same-World ordinary references retain the reference's single-prefab semantics.</summary>
    public void Instantiate(Entity prefab, Span<Entity> destination) => Instantiate(this, prefab, destination);
    public void Instantiate(World source, Entity prefab, Span<Entity> destination) =>
        InstantiateCore(source, [prefab], destination.Length, destination, remapInternal: false, delta: null);

    public void InstantiateWithDelta(Entity prefab, TypeDelta delta, Span<Entity> destination) =>
        InstantiateCore(this, [prefab], destination.Length, destination, remapInternal: false, delta);

    public void InstantiateWithDelta(World source, Entity prefab, TypeDelta delta, Span<Entity> destination) =>
        InstantiateCore(source, [prefab], destination.Length, destination, remapInternal: false, delta);

    public void InstantiateSet(ReadOnlySpan<Entity> prefabs, int instanceCount, Span<Entity> destination) =>
        InstantiateSet(this, prefabs, instanceCount, destination);
    public void InstantiateSet(ReadOnlySpan<Entity> prefabs, Span<Entity> destination) => InstantiateSet(this, prefabs, 1, destination);
    public void InstantiateSet(World source, ReadOnlySpan<Entity> prefabs, Span<Entity> destination) => InstantiateSet(source, prefabs, 1, destination);

    /// <summary>Output is [set0 entity0..N, set1 entity0..N]. Each replica gets its own internal reference mapping.</summary>
    public void InstantiateSet(World source, ReadOnlySpan<Entity> prefabs, int instanceCount, Span<Entity> destination) =>
        InstantiateCore(source, prefabs, instanceCount, destination, remapInternal: true, delta: null);

    private void InstantiateCore(World source, ReadOnlySpan<Entity> prefabs, int instanceCount, Span<Entity> destination,
        bool remapInternal, TypeDelta? delta)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!ReferenceEquals(Runtime, source.Runtime)) throw new ArgumentException("Instantiation requires the same runtime/type registry.", nameof(source));
        if (instanceCount < 0) throw new ArgumentOutOfRangeException(nameof(instanceCount));
        int count = checked(prefabs.Length * instanceCount);
        if (destination.Length < count) throw new ArgumentException("Destination cannot hold every replica.", nameof(destination));
        using var mutation = BeginMutation();
        // A copy reads arbitrary component types and cannot race a newly admitted writer.
        using Mutation? sourceUse = ReferenceEquals(source, this) ? null : source.BeginMutation();
        var originals = prefabs.ToArray();
        var sourceIndices = new Dictionary<Entity, int>();
        var sourceLocations = new (Chunk Chunk, int Row)[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            if (!sourceIndices.TryAdd(originals[i], i)) throw new ArgumentException("Prefab sets cannot contain duplicate identities.", nameof(prefabs));
            ref var entry = ref source.Registry.ValidEntry(originals[i]);
            // sugoi archetype.cpp:287 sets group.cloned to null for a dead group.
            if (entry.Chunk!.Group.IsDead) throw new InvalidOperationException("Cleanup entities cannot be used as prefabs.");
            sourceLocations[i] = (entry.Chunk!, entry.Row);
            foreach (var descriptor in entry.Chunk!.Group.Archetype.Columns)
                if (!descriptor.Type.IsPinned && !descriptor.Ops.CanCopy &&
                    !(delta is { } d && d.Removed.Contains(descriptor.Type) && !d.Added.Contains(descriptor.Type)))
                    throw new InvalidOperationException($"Component {descriptor.Name} has no Copy operation.");
        }
        if (count == 0) return;
        Registry.EnsureAvailable(count);
        Registry.PrepareRelease(count);
        var output = new Entity[count];
        var groups = new Group[count];
        int reserved = 0;
        bool lifetimesStarted = false;
        try
        {
            for (; reserved < count; reserved++) output[reserved] = Registry.Allocate();
            for (int replica = 0; replica < instanceCount; replica++)
            {
                Entity Map(Entity entity)
                {
                    if (remapInternal && sourceIndices.TryGetValue(entity, out int index)) return output[replica * originals.Length + index];
                    return ReferenceEquals(source, this) && source.Registry.Exists(entity) ? entity : Entity.Null;
                }
                for (int i = 0; i < originals.Length; i++)
                {
                    EntityType type = RemappedType(sourceLocations[i].Chunk.Group.Type, Map, dropPinned: true, dropDead: true, Runtime.DeadType);
                    if (delta is { } change) type = change.Apply(type);
                    groups[replica * originals.Length + i] = GetGroup(type, validateMeta: false);
                }
            }
            lifetimesStarted = true;
            // Repeated single-prefab ordinary columns use the doubling duplicate fast path.
            if (originals.Length == 1 && !remapInternal && groups.All(g => ReferenceEquals(g, groups[0])) &&
                groups[0].Archetype.Layout.FirstChunkComponent == groups[0].Archetype.Columns.Length)
                DuplicateIntoGroup(source, originals[0], sourceLocations[0].Chunk, sourceLocations[0].Row, groups[0], output);
            else
                for (int i = 0; i < output.Length; i++)
                {
                    int original = i % originals.Length;
                    CopyOne(source, originals[original], sourceLocations[original].Chunk, sourceLocations[original].Row, groups[i], output[i]);
                }
            // All identities and data exist before one reference-fixup pass. Source data is never edited.
            if (!remapInternal && !ReferenceEquals(source, this))
            {
                RemapCreatedRanges(output, static _ => Entity.Null);
            }
            else if (remapInternal) for (int replica = 0; replica < instanceCount; replica++)
            {
                Entity Map(Entity entity)
                {
                    if (remapInternal && sourceIndices.TryGetValue(entity, out int index)) return output[replica * originals.Length + index];
                    return ReferenceEquals(source, this) && source.Registry.Exists(entity) ? entity : Entity.Null;
                }
                var singletonVisited = new HashSet<Chunk>();
                for (int i = 0; i < originals.Length; i++)
                {
                    ref var entry = ref Registry.ValidEntry(output[replica * originals.Length + i]);
                    var chunk = entry.Chunk!;
                    var columns = chunk.Group.Archetype.Columns;
                    for (int slot = 0; slot < columns.Length; slot++)
                    {
                        if (!columns[slot].Ops.HasReferences) continue;
                        if (columns[slot].Type.IsChunk && singletonVisited.Contains(chunk)) continue;
                        columns[slot].Ops.Remap(chunk.Address(slot, entry.Row), Map);
                    }
                    singletonVisited.Add(chunk);
                }
            }
            output.AsSpan().CopyTo(destination);
        }
        catch
        {
            for (int i = 0; i < reserved; i++) if (!Registry.Exists(output[i])) Registry.Release(output[i]);
            if (lifetimesStarted) IsFaulted = true;
            throw;
        }
    }

    private void RemapCreatedRanges(ReadOnlySpan<Entity> entities, EntityRemapper remapper)
    {
        var seenSingletons = new HashSet<Chunk>();
        int cursor = 0;
        while (cursor < entities.Length)
        {
            ref var entry = ref Registry.ValidEntry(entities[cursor]);
            Chunk chunk = entry.Chunk!;
            int row = entry.Row, count = 1;
            while (cursor + count < entities.Length)
            {
                ref var next = ref Registry.ValidEntry(entities[cursor + count]);
                if (!ReferenceEquals(chunk, next.Chunk) || next.Row != row + count) break;
                count++;
            }
            var columns = chunk.Group.Archetype.Columns;
            for (int slot = 0; slot < columns.Length; slot++)
            {
                var descriptor = columns[slot];
                if (!descriptor.Ops.HasReferences || descriptor.Type.IsChunk && seenSingletons.Contains(chunk)) continue;
                descriptor.Ops.Remap(chunk.Address(slot, row), descriptor.Type.IsChunk ? 1 : count, remapper);
            }
            seenSingletons.Add(chunk);
            cursor += count;
        }
    }

    private void DuplicateIntoGroup(World sourceWorld, Entity prefab, Chunk sourceChunk, int sourceRow, Group group, ReadOnlySpan<Entity> output)
    {
        int cursor = 0;
        while (cursor < output.Length)
        {
            Chunk chunk = group.AcquireChunk(output.Length - cursor);
            int start = chunk.Count, count = Math.Min(chunk.Capacity - start, output.Length - cursor);
            var entities = output.Slice(cursor, count);
            entities.CopyTo(chunk.EntityCapacity.Slice(start, count));
            group.Resize(chunk, start + count);
            for (int i = 0; i < count; i++) Registry.Attach(entities[i], chunk, start + i);
            for (int slot = 0; slot < group.Archetype.Columns.Length; slot++)
            {
                var descriptor = group.Archetype.Columns[slot];
                int sourceSlot = descriptor.Type.IsPinned ? -1 : sourceChunk.Group.Archetype.Layout.IndexOf(descriptor.Type);
                if (sourceSlot >= 0) descriptor.Ops.Duplicate(new(sourceWorld, prefab), sourceChunk.Address(sourceSlot, sourceRow), this, entities, chunk.Address(slot, start));
                else descriptor.Ops.Construct(this, entities, chunk.Address(slot, start));
                chunk.MarkChanged(slot);
            }
            for (int i = 0; i < count; i++)
            {
                WriteRemappedMask(chunk, group.MaskSlot, start + i, ReadMask(sourceChunk, sourceChunk.Group.MaskSlot, sourceRow), sourceChunk.Group.Type, group.Type);
                WriteRemappedMask(chunk, group.DirtySlot, start + i, ReadMask(sourceChunk, sourceChunk.Group.DirtySlot, sourceRow), sourceChunk.Group.Type, group.Type);
            }
            cursor += count;
        }
    }

    private void CopyOne(World sourceWorld, Entity original, Chunk sourceChunk, int sourceRow, Group group, Entity output)
    {
        bool hasSingletons = group.Archetype.Layout.FirstChunkComponent < group.Archetype.Columns.Length;
        Chunk chunk;
        if (hasSingletons)
        {
            // Never overwrite a shared singleton belonging to already existing entities.
            PoolKind kind = group.Archetype.Layout.Small.Capacity > 0 ? PoolKind.Small : PoolKind.Large;
            chunk = new Chunk(group, kind); group.Add(chunk);
        }
        else chunk = group.AcquireChunk(1);
        int row = chunk.Count;
        chunk.EntityCapacity[row] = output;
        group.Resize(chunk, row + 1);
        Registry.Attach(output, chunk, row);
        var sourceContext = new ComponentContext(sourceWorld, original);
        var targetContext = new ComponentContext(this, output);
        for (int slot = 0; slot < group.Archetype.Columns.Length; slot++)
        {
            var descriptor = group.Archetype.Columns[slot];
            int sourceSlot = descriptor.Type.IsPinned ? -1 : sourceChunk.Group.Archetype.Layout.IndexOf(descriptor.Type);
            nint targetAddress = chunk.Address(slot, row);
            if (descriptor.Type.IsChunk)
            {
                if (sourceSlot >= 0)
                {
                    descriptor.Ops.Destroy(new(this, Entity.Null), targetAddress);
                    descriptor.Ops.Copy(new(sourceWorld, Entity.Null), sourceChunk.Address(sourceSlot, sourceRow), new(this, Entity.Null), targetAddress);
                }
            }
            else if (sourceSlot >= 0) descriptor.Ops.Copy(sourceContext, sourceChunk.Address(sourceSlot, sourceRow), targetContext, targetAddress);
            else descriptor.Ops.Construct(targetContext, targetAddress);
            chunk.MarkChanged(slot);
        }
        WriteRemappedMask(chunk, group.MaskSlot, row, ReadMask(sourceChunk, sourceChunk.Group.MaskSlot, sourceRow), sourceChunk.Group.Type, group.Type);
        WriteRemappedMask(chunk, group.DirtySlot, row, ReadMask(sourceChunk, sourceChunk.Group.DirtySlot, sourceRow), sourceChunk.Group.Type, group.Type);
    }

    /// <summary>Immediately repairs owned, buffer, and chunk component references; entity IDs and group meta are unchanged.</summary>
    public void RedirectReferences(EntityRemapper remapper)
    {
        ArgumentNullException.ThrowIfNull(remapper);
        using var mutation = BeginMutation();
        try
        {
            foreach (Group group in _groups.Values)
                foreach (Chunk chunk in group.Chunks) RemapChunkReferences(chunk, remapper);
        }
        catch { IsFaulted = true; throw; }
    }

    /// <summary>Rebalances ordinary columns across the three pools; groups with chunk singletons retain their chunk boundaries.</summary>
    public int Defragment()
    {
        using var mutation = BeginMutation();
        int moved = 0;
        foreach (Group group in _groups.Values)
        {
            if (group.Chunks.Count < 2 || group.Archetype.Layout.FirstChunkComponent < group.Archetype.Columns.Length) continue;
            var kinds = DefragmentPools(group.Archetype.Layout, group.Count);
            var existing = group.Chunks.OrderByDescending(c => c.PoolKind).ThenByDescending(c => c.Count).ToList();
            var selected = new List<Chunk>(kinds.Count);
            var created = new List<Chunk>();
            group.Chunks.EnsureCapacity(checked(group.Chunks.Count + kinds.Count));
            try
            {
                foreach (var kind in kinds)
                {
                    int index = existing.FindIndex(c => c.PoolKind == kind);
                    if (index >= 0) { selected.Add(existing[index]); existing.RemoveAt(index); }
                    else
                    {
                        var chunk = new Chunk(group, kind); group.Add(chunk); created.Add(chunk); selected.Add(chunk);
                    }
                }
            }
            catch
            {
                foreach (var chunk in created) { group.Remove(chunk); chunk.Release(); }
                throw;
            }
            var donors = existing.Concat(selected.AsEnumerable().Reverse()).ToArray();
            var completed = new HashSet<Chunk>();
            try
            {
                foreach (Chunk target in selected)
                {
                    foreach (Chunk donor in donors)
                    {
                        if (target.Count == target.Capacity) break;
                        if (ReferenceEquals(target, donor) || completed.Contains(donor) || donor.Count == 0) continue;
                        int count = Math.Min(donor.Count, target.Capacity - target.Count);
                        int sourceStart = donor.Count - count, targetStart = target.Count;
                        var sourceEntities = donor.Entities.Slice(sourceStart, count);
                        var targetEntities = target.EntityCapacity.Slice(targetStart, count);
                        sourceEntities.CopyTo(targetEntities);
                        for (int slot = 0; slot < group.Archetype.Columns.Length; slot++)
                        {
                            // Each endpoint resolves through its actual pool layout.
                            group.Archetype.Columns[slot].Ops.Move(this, sourceEntities, donor.Address(slot, sourceStart), this, targetEntities, target.Address(slot, targetStart));
                            target.MarkChanged(slot);
                        }
                        for (int row = 0; row < count; row++) Registry.Relocate(targetEntities[row], target, targetStart + row);
                        donor.EntityCapacity.Slice(sourceStart, count).Clear();
                        group.Resize(donor, sourceStart); group.Resize(target, targetStart + count);
                        moved += count;
                    }
                    completed.Add(target);
                }
                foreach (Chunk chunk in group.Chunks.ToArray())
                    if (chunk.Count == 0) { group.Remove(chunk); chunk.Release(); }
            }
            catch { IsFaulted = true; throw; }
        }
        return moved;
    }

    private static List<PoolKind> DefragmentPools(ArchetypeLayout layout, int total)
    {
        var result = new List<PoolKind>();
        if (layout.Normal.Capacity == 0)
        {
            while (total > 0) { result.Add(PoolKind.Large); total -= layout.Large.Capacity; }
            return result;
        }
        while (total > layout.Large.Capacity) { result.Add(PoolKind.Large); total -= layout.Large.Capacity; }
        while (total > layout.Normal.Capacity) { result.Add(PoolKind.Normal); total -= layout.Normal.Capacity; }
        if (result.Count == 0 && layout.Small.Capacity > 0)
            while (total > 0) { result.Add(PoolKind.Small); total -= layout.Small.Capacity; }
        else if (total > 0) result.Add(PoolKind.Normal);
        return result;
    }
}
