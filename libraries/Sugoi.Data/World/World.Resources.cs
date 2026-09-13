namespace Sugoi.Data;

public sealed partial class World
{
    /// <summary>
    /// Visits resource GUIDs held by live component payloads, including disabled entities, native buffers,
    /// and each chunk singleton once. Finish conflicting component writers before scanning.
    /// </summary>
    public void ScanResourceReferences(ResourceVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        using var usage = AcquireUsage();
        foreach (var group in Groups)
        {
            if (group.IsDead) continue;
            foreach (var chunk in group.Chunks)
                for (int slot = 0; slot < group.Archetype.Columns.Length; slot++)
                {
                    var descriptor = group.Archetype.Columns[slot];
                    if (!descriptor.Ops.HasResources) continue;
                    descriptor.Ops.ScanResources(chunk.Address(slot, 0), descriptor.Type.IsChunk ? 1 : chunk.Count, visitor);
                }
        }
    }
}
