namespace Sugoi.Data;

internal sealed class EntityRegistry
{
    internal struct Entry
    {
        internal Chunk? Chunk;
        internal int Row;
        internal uint Generation;
    }

    private Entry[] _entries = new Entry[64];
    private readonly Stack<uint> _free = new();
    private int _allocated;
    internal int Count { get; private set; }
    internal int AllocatedCount => _allocated;
    internal int MaximumCount { get; }
    internal EntityRegistry(int maximumCount) => MaximumCount = maximumCount > 0 ? maximumCount : throw new ArgumentOutOfRangeException(nameof(maximumCount));

    internal Entity Allocate()
    {
        uint index;
        if (_free.TryPop(out index)) { }
        else
        {
            if (_allocated == MaximumCount) throw new InvalidOperationException("World entity capacity exhausted.");
            if (_allocated == _entries.Length)
                Array.Resize(ref _entries, (int)Math.Min(MaximumCount, Math.Max((long)_allocated + 1, (long)_entries.Length * 2)));
            index = (uint)_allocated++;
            _entries[index].Generation = 1;
        }
        ref var entry = ref _entries[index];
        entry.Row = -1;
        return Entity.Create(index, entry.Generation);
    }

    internal void Attach(Entity entity, Chunk chunk, int row)
    {
        ref var entry = ref ValidEntry(entity, allowReserved: true);
        if (entry.Chunk is null) Count++;
        entry.Chunk = chunk; entry.Row = row;
    }

    internal void Relocate(Entity entity, Chunk chunk, int row)
    {
        ref var entry = ref ValidEntry(entity);
        entry.Chunk = chunk; entry.Row = row;
    }

    internal bool Exists(Entity entity) => TryGet(entity, out _);
    internal bool TryGet(Entity entity, out Entry entry)
    {
        if (!entity.IsNull && entity.Index < _allocated)
        {
            entry = _entries[entity.Index];
            if (entry.Generation == entity.Generation && entry.Chunk is not null) return true;
        }
        entry = default;
        return false;
    }

    internal ref Entry ValidEntry(Entity entity, bool allowReserved = false)
    {
        if (entity.IsNull || entity.Index >= _allocated) throw new ArgumentException("Entity does not exist in this world.", nameof(entity));
        ref var entry = ref _entries[entity.Index];
        if (entry.Generation != entity.Generation || (entry.Chunk is null && !(allowReserved && entry.Row == -1)))
            throw new ArgumentException("Entity is stale, reserved, or belongs to another world.", nameof(entity));
        return ref entry;
    }

    internal void Release(Entity entity)
    {
        ref var entry = ref ValidEntry(entity, allowReserved: true);
        if (entry.Chunk is not null) Count--;
        entry.Chunk = null; entry.Row = -2;
        entry.Generation = Entity.NextGeneration(entry.Generation);
        _free.Push(entity.Index);
    }

    internal Entity[] Snapshot(bool includeReserved = false)
    {
        var result = new List<Entity>(Count);
        for (uint i = 0; i < _allocated; i++)
            if (_entries[i].Chunk is not null || includeReserved && _entries[i].Row == -1)
                result.Add(Entity.Create(i, _entries[i].Generation));
        return result.ToArray();
    }

    internal void EnsureAvailable(int count)
    {
        if (count < 0 || (long)_allocated - _free.Count + count > MaximumCount)
            throw new InvalidOperationException("World entity capacity exhausted.");
    }

    internal void PrepareRelease(int count) => _free.EnsureCapacity(checked(_free.Count + count));

    /// <summary>All allocation and generation selection precede the destructive identity switch.</summary>
    internal PackingPlan PreparePacking()
    {
        for (int i = 0; i < _allocated; i++)
            if (_entries[i].Chunk is null && _entries[i].Row == -1)
                throw new InvalidOperationException("Resolve entity reservations before compacting identities.");
        var before = Snapshot();
        // Keep generation history for vacated high indices. Shrinking to Count and later restarting them
        // at generation one would resurrect stale Entity64 values when the world grows again.
        var entries = (Entry[])_entries.Clone();
        var mapping = new EntityMapping[before.Length];
        _free.EnsureCapacity(_allocated - before.Length);
        for (int i = 0; i < _allocated; i++)
        {
            if (entries[i].Chunk is not null) entries[i].Generation = Entity.NextGeneration(entries[i].Generation);
            entries[i].Chunk = null; entries[i].Row = -2;
        }
        for (int i = 0; i < before.Length; i++)
        {
            uint generation = entries[i].Generation;
            entries[i] = _entries[before[i].Index]; entries[i].Generation = generation;
            mapping[i] = new(before[i], Entity.Create((uint)i, generation));
        }
        return new PackingPlan(this, _entries, entries, mapping, _allocated, Count);
    }

    internal void CommitPacking(PackingPlan plan)
    {
        if (plan.Owner != this || plan.Committed || !ReferenceEquals(plan.Before, _entries) ||
            plan.Allocated != _allocated || plan.Count != Count)
            throw new InvalidOperationException("The identity packing plan is stale or belongs to another registry.");
        plan.Committed = true;
        _entries = plan.After;
        _free.Clear();
        for (int i = _allocated - 1; i >= plan.Count; i--) _free.Push((uint)i);
    }

    internal sealed class PackingPlan(EntityRegistry owner, Entry[] before, Entry[] after,
        EntityMapping[] mappings, int allocated, int count)
    {
        internal readonly EntityRegistry Owner = owner;
        internal readonly Entry[] Before = before, After = after;
        internal readonly EntityMapping[] Mappings = mappings;
        internal readonly int Allocated = allocated, Count = count;
        internal bool Committed;
    }
}
