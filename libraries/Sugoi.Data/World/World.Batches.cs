namespace Sugoi.Data;

public delegate void EntityBatchVisitor(in ChunkView view);

public sealed partial class World
{
    /// <summary>Preserves input order; only adjacent entries in the same physical range are coalesced.</summary>
    public QueryRange[] GetEntityRanges(ReadOnlySpan<Entity> entities)
    {
        using var use = AcquireUsage();
        var result = new List<QueryRange>();
        int cursor = 0;
        while (cursor < entities.Length)
        {
            if (!Registry.TryGet(entities[cursor], out var first)) { cursor++; continue; }
            int count = 1;
            while (cursor + count < entities.Length && Registry.TryGet(entities[cursor + count], out var next) &&
                   ReferenceEquals(next.Chunk, first.Chunk) && next.Row == first.Row + count) count++;
            result.Add(new QueryRange(this, first.Chunk!, first.Row, count));
            cursor += count;
        }
        return result.ToArray();
    }

    /// <summary>Allows immediate structural operations in the callback. Identities are re-located between callbacks.</summary>
    public void VisitEntities(ReadOnlySpan<Entity> entities, EntityBatchVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        EnsureAlive();
        // The caller may supply the very native Entity column its callback will move or free.
        Entity[] input = entities.ToArray();
        int cursor = 0;
        while (cursor < input.Length)
        {
            if (!Registry.TryGet(input[cursor], out var first)) { cursor++; continue; }
            int count = 1;
            while (cursor + count < input.Length && Registry.TryGet(input[cursor + count], out var next) &&
                   ReferenceEquals(next.Chunk, first.Chunk) && next.Row == first.Row + count) count++;
            var view = new QueryRange(this, first.Chunk!, first.Row, count).View;
            visitor(in view);
            cursor += count;
        }
    }

    public WorldDiagnostics GetDiagnostics()
    {
        using var use = AcquireUsage();
        int chunks = 0, capacity = 0;
        long usedNative = 0;
        foreach (var group in Groups)
            foreach (var chunk in group.Chunks) { chunks++; capacity += chunk.Capacity; usedNative += chunk.Pool.TotalBytes; }
        return new(EntityCount, _groups.Count, _archetypes.Count, chunks, capacity, usedNative, GroupPool.AllocatedBytes, StructureVersion);
    }
}

public readonly record struct WorldDiagnostics(int Entities, int Groups, int Archetypes, int Chunks,
    int Capacity, long ChunkBytes, long MetadataBytes, ulong StructureVersion);
