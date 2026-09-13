using System.Runtime.CompilerServices;

namespace Sugoi.Data;

public sealed partial class World
{
    public void Destroy(Entity entity)
    {
        using var mutation = BeginMutation();
        if (Registry.Exists(entity)) DestroyCore(entity);
    }

    public void Destroy(ReadOnlySpan<Entity> entities)
    {
        using var mutation = BeginMutation();
        var targets = new List<(Entity Entity, long ChunkId, int Row)>();
        var seen = new HashSet<Entity>();
        foreach (var entity in entities)
            if (seen.Add(entity) && Registry.TryGet(entity, out var entry)) targets.Add((entity, entry.Chunk!.Id, entry.Row));
        targets.Sort(static (a, b) => a.ChunkId == b.ChunkId ? b.Row.CompareTo(a.Row) : a.ChunkId.CompareTo(b.ChunkId));
        foreach (var target in targets) if (Registry.Exists(target.Entity)) DestroyCore(target.Entity);
    }

    public void Destroy(Query query)
    {
        if (query.World != this) throw new ArgumentException("Query belongs to another World.");
        var entities = new List<Entity>();
        foreach (var view in query) foreach (var entity in view.Entities) entities.Add(entity);
        Destroy(entities.ToArray());
    }

    private void DestroyCore(Entity entity)
    {
        ref var entry = ref Registry.ValidEntry(entity);
        var current = entry.Chunk!.Group.Type;
        bool pinned = false;
        foreach (var type in current.Components) if (type.IsPinned) { pinned = true; break; }
        if (pinned)
        {
            // sugoi archetype.cpp:265-302: the dead signature retains every component,
            // adds Dead, and drops meta. Ordinary values survive until final PIN cleanup.
            ChangeTypeCore(entity, new EntityType(current.Components, ReadOnlySpan<Entity>.Empty).With(Runtime.DeadType));
            return;
        }
        RemoveRow(entry.Chunk, entry.Row, destroy: true, releaseIdentity: true);
    }

    public void Remove<T>(Entity entity) where T : unmanaged => Remove(entity, Types.Get<T>());
    public void Remove(Entity entity, ComponentType type)
    {
        using var mutation = BeginMutation();
        var current = GetEntityType(entity);
        if (!current.Contains(type)) return;
        ChangeTypeCore(entity, current.Without(type));
    }
    public void Add(Entity entity, ComponentType type)
    {
        using var mutation = BeginMutation();
        var current = GetEntityType(entity);
        if (current.Contains(type)) return;
        ChangeTypeCore(entity, current.With(type));
    }
    public void Add<T>(Entity entity, in T value) where T : unmanaged
    {
        using var mutation = BeginMutation();
        var type = Types.Get<T>();
        var current = GetEntityType(entity);
        if (current.Contains(type)) throw new InvalidOperationException("Component exists; use Set to replace its value.");
        ChangeTypeCore(entity, current.With(type));
        if (!type.IsTag) Set(entity, in value);
    }
    public void AddBuffer<T>(Entity entity) where T : unmanaged => Add(entity, Types.GetBuffer<T>());
    public void RemoveBuffer<T>(Entity entity) where T : unmanaged => Remove(entity, Types.GetBuffer<T>());

    public void ChangeType(Entity entity, EntityType target)
    {
        using var mutation = BeginMutation();
        ChangeTypeCore(entity, target);
    }
    public void ApplyTypeDelta(Entity entity, TypeDelta delta) => ChangeType(entity, delta.Apply(GetEntityType(entity)));
    public void AddMeta(Entity entity, Entity meta) => ChangeType(entity, GetEntityType(entity).WithMeta(meta));
    public void RemoveMeta(Entity entity, Entity meta) => ChangeType(entity, GetEntityType(entity).WithoutMeta(meta));

    public void ApplyTypeDelta(Query query, TypeDelta delta)
    {
        if (query.World != this) throw new ArgumentException("Query belongs to another World.");
        using var mutation = BeginMutation();
        foreach (var group in _groups.Values.ToArray())
        {
            if (!query.MatchesGroupCore(group) || group.Count == 0) continue;
            var target = delta.Apply(group.Type);
            var destination = GetGroup(target);
            if (ReferenceEquals(group.Archetype, destination.Archetype))
            {
                foreach (var chunk in group.Chunks.ToArray()) { RemapMasks(chunk, group.Type, target); group.Remove(chunk); destination.Add(chunk); }
            }
            else
            {
                var entities = new List<Entity>(group.Count);
                foreach (var chunk in group.Chunks) foreach (var entity in chunk.Entities) entities.Add(entity);
                foreach (var entity in entities) ChangeTypeCore(entity, target);
            }
        }
    }

    private void ChangeTypeCore(Entity entity, EntityType target)
    {
        ref var entry = ref Registry.ValidEntry(entity);
        var source = entry.Chunk!;
        var sourceGroup = source.Group;
        int sourceRow = entry.Row;
        if (sourceGroup.Type.Equals(target)) return;
        if (sourceGroup.IsDead)
        {
            bool retainsPinned = false;
            foreach (var type in target.Components) if (type.IsPinned) { retainsPinned = true; break; }
            if (!retainsPinned) { RemoveRow(source, sourceRow, true, true); return; }
        }
        var destinationGroup = GetGroup(target);
        if (ReferenceEquals(sourceGroup.Archetype, destinationGroup.Archetype) && source.Count == 1)
        {
            RemapMasks(source, sourceGroup.Type, target);
            sourceGroup.Remove(source); destinationGroup.Add(source);
            return;
        }
        bool hasDestinationSingletons = destinationGroup.Archetype.Layout.FirstChunkComponent < destinationGroup.Archetype.Columns.Length;
        if (hasDestinationSingletons && source.Count > 1)
            foreach (var descriptor in destinationGroup.Archetype.Columns)
                if (descriptor.Type.IsChunk && sourceGroup.Archetype.Layout.IndexOf(descriptor.Type) >= 0 && !descriptor.Ops.CanCopy)
                    throw new InvalidOperationException($"Splitting chunk component {descriptor.Name} requires a Copy hook.");
        if (hasDestinationSingletons && source.Count == 1) source.PrepareSingletonTransfers();
        Chunk destination;
        if (hasDestinationSingletons)
        {
            PoolKind kind = destinationGroup.Archetype.Layout.Small.Capacity > 0 ? PoolKind.Small : PoolKind.Large;
            destinationGroup.Chunks.EnsureCapacity(checked(destinationGroup.Chunks.Count + 1));
            destination = new Chunk(destinationGroup, kind);
            destinationGroup.Add(destination);
        }
        else destination = destinationGroup.AcquireChunk(1);
        int destinationRow = destination.Count;
        destination.EntityCapacity[destinationRow] = entity;
        destinationGroup.Resize(destination, destinationRow + 1);
        try
        {
            var context = new ComponentContext(this, entity);
            var srcColumns = sourceGroup.Archetype.Columns;
            var dstColumns = destinationGroup.Archetype.Columns;
            uint? enabled = ReadMask(source, sourceGroup.MaskSlot, sourceRow);
            uint? dirty = ReadMask(source, sourceGroup.DirtySlot, sourceRow);
            int s = 0, d = 0;
            // Per-chunk values belong to all their entities. A partial migration copies them into
            // an isolated destination chunk; transferring the final entity moves ownership once.
            for (int slot = destinationGroup.Archetype.Layout.FirstChunkComponent; slot < dstColumns.Length; slot++)
            {
                var descriptor = dstColumns[slot];
                int sourceSlot = sourceGroup.Archetype.Layout.IndexOf(descriptor.Type);
                if (sourceSlot < 0) continue;
                var chunkContext = new ComponentContext(this, Entity.Null);
                descriptor.Ops.Destroy(chunkContext, destination.Address(slot, 0));
                if (source.Count == 1)
                {
                    descriptor.Ops.Move(chunkContext, source.Address(sourceSlot, 0), chunkContext, destination.Address(slot, 0));
                    source.EndSingletonLifetime(sourceSlot);
                }
                else descriptor.Ops.Copy(chunkContext, source.Address(sourceSlot, 0), chunkContext, destination.Address(slot, 0));
                destination.MarkChanged(slot);
            }
            // Sorted columns preserve sugoi's column-first merge rather than per-type dictionaries.
            while (s < sourceGroup.Archetype.Layout.FirstChunkComponent || d < destinationGroup.Archetype.Layout.FirstChunkComponent)
            {
                uint st = s < sourceGroup.Archetype.Layout.FirstChunkComponent ? srcColumns[s].Type.Value : uint.MaxValue;
                uint dt = d < destinationGroup.Archetype.Layout.FirstChunkComponent ? dstColumns[d].Type.Value : uint.MaxValue;
                if (st == dt)
                {
                    dstColumns[d].Ops.Move(context, source.Address(s, sourceRow), context, destination.Address(d, destinationRow));
                    destination.MarkChanged(d); s++; d++;
                }
                else if (st < dt) { srcColumns[s].Ops.Destroy(context, source.Address(s, sourceRow)); s++; }
                else { dstColumns[d].Ops.Construct(context, destination.Address(d, destinationRow)); destination.MarkChanged(d); d++; }
            }
            WriteRemappedMask(destination, destinationGroup.MaskSlot, destinationRow, enabled, sourceGroup.Type, target);
            WriteRemappedMask(destination, destinationGroup.DirtySlot, destinationRow, dirty, sourceGroup.Type, target);
            Registry.Relocate(entity, destination, destinationRow);
            RemoveRow(source, sourceRow, destroy: false, releaseIdentity: false);
        }
        catch { IsFaulted = true; throw; }
    }

    private void RemoveRow(Chunk chunk, int row, bool destroy, bool releaseIdentity)
    {
        try { RemoveRowCore(chunk, row, destroy, releaseIdentity); }
        catch { IsFaulted = true; throw; }
    }

    private void RemoveRowCore(Chunk chunk, int row, bool destroy, bool releaseIdentity)
    {
        Entity removed = chunk.Entities[row];
        int tail = chunk.Count - 1;
        var archetype = chunk.Group.Archetype;
        if (destroy)
            for (int slot = 0; slot < archetype.Layout.FirstChunkComponent; slot++)
                archetype.Columns[slot].Ops.Destroy(new ComponentContext(this, removed), chunk.Address(slot, row));
        if (row != tail)
        {
            Entity moved = chunk.Entities[tail];
            var context = new ComponentContext(this, moved);
            for (int slot = 0; slot < archetype.Layout.FirstChunkComponent; slot++)
            {
                archetype.Columns[slot].Ops.Move(context, chunk.Address(slot, tail), context, chunk.Address(slot, row));
                chunk.MarkChanged(slot);
            }
            chunk.EntityCapacity[row] = moved;
            Registry.Relocate(moved, chunk, row);
        }
        chunk.EntityCapacity[tail] = Entity.Null;
        if (releaseIdentity) Registry.Release(removed);
        var group = chunk.Group;
        group.Resize(chunk, tail);
        if (tail == 0) { group.Remove(chunk); chunk.Release(); }
    }

    private static unsafe uint? ReadMask(Chunk chunk, int slot, int row) => slot < 0 ? null : *(uint*)chunk.Address(slot, row);
    private static unsafe void WriteRemappedMask(Chunk chunk, int slot, int row, uint? old, EntityType source, EntityType destination)
    {
        if (slot < 0) return;
        uint bits = 0;
        for (int i = 0; i < destination.Components.Length; i++)
        {
            int previous = Array.BinarySearch(source.ComponentArray, destination.Components[i]);
            if (previous < 0 || old is null || (old.Value & (1u << previous)) != 0) bits |= 1u << i;
        }
        *(uint*)chunk.Address(slot, row) = bits;
        chunk.MarkChanged(slot);
    }
    private static void RemapMasks(Chunk chunk, EntityType source, EntityType destination)
    {
        for (int row = 0; row < chunk.Count; row++)
        {
            WriteRemappedMask(chunk, chunk.Group.MaskSlot, row, ReadMask(chunk, chunk.Group.MaskSlot, row), source, destination);
            WriteRemappedMask(chunk, chunk.Group.DirtySlot, row, ReadMask(chunk, chunk.Group.DirtySlot, row), source, destination);
        }
    }

    public unsafe void Enable(Entity entity, params ComponentType[] types) => ChangeEnabled(entity, types, true);
    public unsafe void Disable(Entity entity, params ComponentType[] types) => ChangeEnabled(entity, types, false);
    private unsafe void ChangeEnabled(Entity entity, ReadOnlySpan<ComponentType> types, bool enabled)
    {
        ref var entry = ref Registry.ValidEntry(entity);
        var chunk = entry.Chunk!;
        if (chunk.Group.MaskSlot < 0) throw new InvalidOperationException("Add the runtime EnabledMaskType when creating this entity to use component enable bits.");
        uint bits = chunk.Group.ComponentMask(types);
        ref uint mask = ref Unsafe.AsRef<uint>((void*)chunk.Address(chunk.Group.MaskSlot, entry.Row));
        if (enabled) ComponentMask.Enable(ref mask, bits); else ComponentMask.Disable(ref mask, bits);
        chunk.MarkChanged(chunk.Group.MaskSlot);
    }
    public unsafe bool ComponentsEnabled(Entity entity, ReadOnlySpan<ComponentType> types)
    {
        ref var entry = ref Registry.ValidEntry(entity);
        var chunk = entry.Chunk!;
        foreach (var type in types) if (!chunk.Group.Has(type)) return false;
        if (chunk.Group.MaskSlot < 0) return true;
        uint bits = chunk.Group.ComponentMask(types);
        return (*(uint*)chunk.Address(chunk.Group.MaskSlot, entry.Row) & bits) == bits;
    }

    public void ValidateMeta()
    {
        using var mutation = BeginMutation();
        foreach (var group in _groups.Values.ToArray())
        {
            var valid = new List<Entity>();
            foreach (var entity in group.Type.MetaEntities) if (Registry.Exists(entity)) valid.Add(entity);
            if (valid.Count == group.Type.MetaEntities.Length) continue;
            var target = GetGroup(new EntityType(group.Type.Components, valid.ToArray()));
            foreach (var chunk in group.Chunks.ToArray()) { group.Remove(chunk); target.Add(chunk); }
        }
    }
}
