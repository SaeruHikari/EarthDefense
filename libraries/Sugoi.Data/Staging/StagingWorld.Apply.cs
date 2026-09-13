namespace Sugoi.Data;

public sealed partial class StagingWorld
{
    /// <summary>
    /// Query changes → reserve spawn identities → patch references → spawn → existing deltas →
    /// recursive/explicit destruction → staged lifetime cleanup. Caller first completes all relevant jobs.
    /// </summary>
    public void Apply(World world, IStructuralChangeSink? sink = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Runtime != Runtime) throw new ArgumentException("Target World belongs to another runtime.", nameof(world));
        using var exclusive = EnterExclusive();
        var changedWorlds = new List<World> { world };
        foreach (var query in _queries.Keys) if (!changedWorlds.Contains(query.World)) changedWorlds.Add(query.World);
        var boundaries = new List<IDisposable>(changedWorlds.Count);
        var spawns = _records.Take(_recordCount).Where(r => r.Entity.IsTransient).ToArray();
        var reserved = new Entity[spawns.Length];
        bool reservedAll = false, changed = false;
        try
        {
            foreach (World target in changedWorlds) boundaries.Add(target.BeginStagingMutation());
            foreach (var record in _records.Take(_recordCount))
                if (!record.Entity.IsTransient && !world.Exists(record.Entity))
                    throw new ArgumentException("A staged record targets a stale or missing entity.");
            world.Registry.EnsureAvailable(spawns.Length);
            // The reference applies query-scoped changes first; they do not see this epoch's spawns.
            changed = true;
            foreach (var (query, change) in _queries) query.World.StagingQueryDelta(query, change.Change.Delta());
            world.StagingReserve(reserved); reservedAll = true;
            var mapped = new Entity[_spawningCount];
            for (int i = 0; i < spawns.Length; i++) mapped[spawns[i].Entity.Index] = reserved[i];
            Entity Map(Entity entity)
            {
                if (entity.IsNull || !entity.IsTransient) return entity;
                if (entity.Index >= mapped.Length || mapped[entity.Index].IsNull)
                    throw new InvalidOperationException("Unknown or unrecorded transient reference in staged payload/meta.");
                return mapped[entity.Index];
            }
            for (int i = 0; i < _recordCount; i++)
            {
                var record = _records[i];
                RemapMeta(record.Change.AddedMeta, Map); RemapMeta(record.Change.RemovedMeta, Map);
                foreach (var type in record.Change.Added)
                {
                    if (type.IsTag) continue;
                    var row = _rows[(int)type.Index]!;
                    if (row.ConstructedExclusive(i)) row.Ops.Remap(row.PointerExclusive(i), Map);
                }
            }
            int cursor = 0;
            while (cursor < spawns.Length)
            {
                int end = cursor + 1;
                while (end < spawns.Length && spawns[end].Change.SameAdded(spawns[cursor].Change)) end++;
                world.StagingCreate(spawns[cursor].Change.EntityType(), reserved.AsSpan(cursor, end - cursor));
                for (int i = cursor; i < end; i++) MovePayloads(world, spawns[i], reserved[i], sink);
                cursor = end;
            }
            for (int i = 0; i < _recordCount; i++)
            {
                var record = _records[i];
                if (record.Entity.IsTransient || record.Destroy || record.Change.IsEmpty) continue;
                foreach (var type in record.Change.Removed)
                    sink?.ComponentRemoved(world, record.Entity, type, world.StagingAddress(record.Entity, type));
                world.StagingChange(record.Entity, record.Change.Delta());
                // Removing the final PIN may have ended this entity's lifetime.
                if (world.Exists(record.Entity)) MovePayloads(world, record, record.Entity, sink);
            }
            var destroying = new HashSet<Entity>(world.StagingOwnedTargets(_destroyMeta.ToArray()));
            for (int i = 0; i < _recordCount; i++) if (_records[i].Destroy) destroying.Add(_records[i].Entity);
            Entity[] ordered = destroying.OrderBy(e => e.Value).ToArray();
            foreach (Entity entity in ordered)
            {
                if (!world.Exists(entity)) throw new InvalidOperationException("Staged destruction targets a missing entity.");
                sink?.EntityDestroyed(world, entity);
            }
            world.StagingDestroy(ordered);
            ClearCore();
        }
        catch
        {
            if (reservedAll)
                foreach (Entity entity in reserved) if (!world.Exists(entity))
                {
                    try { world.Registry.Release(entity); } catch (ArgumentException) { }
                }
            if (changed)
            {
                IsFaulted = true;
                foreach (World target in changedWorlds) target.StagingFault();
            }
            throw;
        }
        finally { for (int i = boundaries.Count - 1; i >= 0; i--) boundaries[i].Dispose(); }
    }

    private void MovePayloads(World world, StagingRecord record, Entity entity, IStructuralChangeSink? sink)
    {
        foreach (ComponentType type in record.Change.Added)
        {
            // Source sink only reports non-tag staged payloads. Query changes do not emit these events.
            if (type.IsTag) continue;
            var row = _rows[(int)type.Index]!;
            if (!row.ConstructedExclusive(record.Index)) continue;
            world.StagingMove(entity, type, row.Ops, record.Entity, row.PointerExclusive(record.Index));
            row.MarkExclusive(record.Index, false);
            sink?.ComponentAdded(world, entity, type, world.StagingAddress(entity, type));
        }
    }
    private static void RemapMeta(List<Entity> entities, EntityRemapper map)
    {
        for (int i = 0; i < entities.Count; i++) entities[i] = map(entities[i]);
        entities.Sort();
        for (int i = entities.Count - 1; i > 0; i--) if (entities[i] == entities[i - 1]) entities.RemoveAt(i);
    }
}
