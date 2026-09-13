namespace Sugoi.Data;

public sealed partial class StagingWorld
{
    /// <summary>Stages source ordinary columns while retaining extra target columns and caller-preserved types.</summary>
    public bool UpdateFrom(World sourceWorld, Entity sourceEntity, World targetWorld, Entity targetEntity,
        ReadOnlySpan<ComponentType> preservedTypes = default, IReadOnlyDictionary<Entity, Entity>? entityRemap = null) =>
        CopyFrom(sourceWorld, sourceEntity, targetWorld, targetEntity, preservedTypes, entityRemap, replace: false);

    /// <summary>Additionally removes target ordinary columns missing from the source, except preserved types.</summary>
    public bool ReplaceFrom(World sourceWorld, Entity sourceEntity, World targetWorld, Entity targetEntity,
        ReadOnlySpan<ComponentType> preservedTypes = default, IReadOnlyDictionary<Entity, Entity>? entityRemap = null) =>
        CopyFrom(sourceWorld, sourceEntity, targetWorld, targetEntity, preservedTypes, entityRemap, replace: true);

    private bool CopyFrom(World sourceWorld, Entity sourceEntity, World targetWorld, Entity targetEntity,
        ReadOnlySpan<ComponentType> preservedTypes, IReadOnlyDictionary<Entity, Entity>? entityRemap, bool replace)
    {
        using var scope = EnterProducer();
        ArgumentNullException.ThrowIfNull(sourceWorld); ArgumentNullException.ThrowIfNull(targetWorld);
        if (sourceWorld.Runtime != Runtime || targetWorld.Runtime != Runtime) throw new ArgumentException("Worlds must use the staging runtime.");
        if (sourceEntity.IsNull || sourceEntity.IsTransient || targetEntity.IsNull) return false;
        if (targetEntity.IsTransient && targetEntity.Index >= Volatile.Read(ref _spawningCount)) return false;
        using var sourceUse = sourceWorld.AcquireUsage();
        using var targetUse = ReferenceEquals(sourceWorld, targetWorld) ? null : targetWorld.AcquireUsage();
        if (!sourceWorld.Registry.TryGet(sourceEntity, out var sourceEntry)) return false;
        if (!targetEntity.IsTransient && !targetWorld.Registry.Exists(targetEntity)) return false;
        // Validate before making removals; the C++ helper checks this after staging removals.
        if (entityRemap is not null && (!entityRemap.TryGetValue(sourceEntity, out var root) || root != targetEntity)) return false;
        Entity Map(Entity entity) => entityRemap is null ? (entity == sourceEntity ? targetEntity : entity) :
            entityRemap.TryGetValue(entity, out var destination) ? destination : entity;
        var preserved = new HashSet<ComponentType>(preservedTypes.ToArray());
        var sourceChunk = sourceEntry.Chunk!;
        var sourceColumns = sourceChunk.Group.Archetype.Columns;
        int ordinaryCount = sourceChunk.Group.Archetype.Layout.FirstChunkComponent;
        var sourceTypes = new HashSet<ComponentType>();
        for (int i = 0; i < ordinaryCount; i++)
        {
            sourceTypes.Add(sourceColumns[i].Type);
            if (!preserved.Contains(sourceColumns[i].Type) && !sourceColumns[i].Ops.CanCopy)
                throw new InvalidOperationException($"Component {sourceColumns[i].Name} requires a Copy hook for staging.");
        }
        var record = Record(targetEntity);
        lock (record.Gate)
        {
            if (replace && !targetEntity.IsTransient)
            {
                ref var targetEntry = ref targetWorld.Registry.ValidEntry(targetEntity);
                var targetArchetype = targetEntry.Chunk!.Group.Archetype;
                for (int i = 0; i < targetArchetype.Layout.FirstChunkComponent; i++)
                {
                    var type = targetArchetype.Columns[i].Type;
                    if (preserved.Contains(type) || sourceTypes.Contains(type)) continue;
                    if (record.Change.Remove(type)) Row(type).Destroy(record);
                }
            }
            try
            {
                for (int slot = 0; slot < ordinaryCount; slot++)
                {
                    var descriptor = sourceColumns[slot];
                    if (preserved.Contains(descriptor.Type)) continue;
                    var row = Row(descriptor.Type);
                    nint destination = row.Ensure(record.Index);
                    record.Change.Add(descriptor.Type); row.Destroy(record);
                    var sourceContext = new ComponentContext(sourceWorld, sourceEntity);
                    var targetContext = new ComponentContext(null, targetEntity);
                    if (row.Ops.Buffer is { } buffer)
                        buffer.CopyPreservingCapacity(sourceContext, sourceChunk.Address(slot, sourceEntry.Row), targetContext, destination);
                    else row.Ops.Copy(sourceContext, sourceChunk.Address(slot, sourceEntry.Row), targetContext, destination);
                    row.Mark(record.Index, true);
                    row.Ops.Remap(destination, Map);
                }
            }
            catch { IsFaulted = true; throw; }
        }
        return true;
    }
}
