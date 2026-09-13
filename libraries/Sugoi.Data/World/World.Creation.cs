namespace Sugoi.Data;

/// <summary>A synchronous creation callback. The view is valid only during this call; structure reentry is rejected.</summary>
public interface IEntityInitializer
{
    void Initialize(in ChunkView view, int entityStartIndex);
}

public sealed partial class World
{
    /// <summary>Constructs and initializes each new contiguous view before allocating the next view.</summary>
    public void Create<TInitializer>(EntityType type, Span<Entity> destination, ref TInitializer initializer)
        where TInitializer : IEntityInitializer
    {
        if (initializer is null) throw new ArgumentNullException(nameof(initializer));
        using var mutation = BeginMutation();
        Group group = GetGroup(type);
        Registry.EnsureAvailable(destination.Length); Registry.PrepareRelease(destination.Length);
        int reserved = 0;
        try
        {
            for (; reserved < destination.Length; reserved++) destination[reserved] = Registry.Allocate();
            CreateInitializedCore(group, destination, ref initializer);
        }
        catch
        {
            for (int i = 0; i < reserved; i++)
            {
                try { if (Registry.ValidEntry(destination[i], allowReserved: true).Chunk is null) Registry.Release(destination[i]); }
                catch (ArgumentException) { } // A construction rollback already retired this identity.
            }
            IsFaulted = true;
            throw;
        }
    }

    public void CreateReserved<TInitializer>(EntityType type, ReadOnlySpan<Entity> entities, ref TInitializer initializer)
        where TInitializer : IEntityInitializer
    {
        if (initializer is null) throw new ArgumentNullException(nameof(initializer));
        using var mutation = BeginMutation();
        var seen = new HashSet<Entity>();
        foreach (Entity entity in entities)
            if (!seen.Add(entity) || Registry.ValidEntry(entity, allowReserved: true).Chunk is not null)
                throw new ArgumentException("Creation requires unique, unmaterialized reserved identities.", nameof(entities));
        Group group = GetGroup(type);
        Registry.PrepareRelease(entities.Length);
        try { CreateInitializedCore(group, entities, ref initializer); }
        catch { IsFaulted = true; throw; }
    }

    private void CreateInitializedCore<TInitializer>(Group group, ReadOnlySpan<Entity> entities, ref TInitializer initializer)
        where TInitializer : IEntityInitializer
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
            int slot = 0, completedRows = 0;
            try
            {
                for (; slot < group.Archetype.Layout.FirstChunkComponent; slot++)
                {
                    var descriptor = group.Archetype.Columns[slot];
                    completedRows = 0;
                    if (descriptor.Ops.CanBulkConstruct)
                    {
                        descriptor.Ops.Construct(this, values, chunk.Address(slot, start));
                        completedRows = count;
                    }
                    else
                        for (; completedRows < count; completedRows++)
                            descriptor.Ops.Construct(new(this, values[completedRows]), chunk.Address(slot, start + completedRows));
                    chunk.MarkChanged(slot);
                }
            }
            catch
            {
                // Only completed constructor calls own values. Never later dispose uninitialized columns.
                if (slot < group.Archetype.Layout.FirstChunkComponent)
                    for (int row = completedRows - 1; row >= 0; row--)
                        group.Archetype.Columns[slot].Ops.Destroy(new(this, values[row]), chunk.Address(slot, start + row));
                for (int column = slot - 1; column >= 0; column--)
                    group.Archetype.Columns[column].Ops.Destroy(this, values, chunk.Address(column, start));
                foreach (Entity entity in values) Registry.Release(entity);
                chunk.EntityCapacity.Slice(start, count).Clear();
                group.Resize(chunk, start);
                if (start == 0) { group.Remove(chunk); chunk.Release(); }
                throw;
            }
            // ref TInitializer is the caller's actual struct/class, not a boxed copy per view.
            var view = new ChunkView(new QueryRange(this, chunk, start, count));
            initializer.Initialize(in view, cursor);
            cursor += count;
        }
    }
}
