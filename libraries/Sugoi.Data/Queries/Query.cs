namespace Sugoi.Data;

public sealed partial class Query : IDisposable
{
    private readonly object _cacheGate = new();
    private Group[] _groups = [];
    private ulong _structure = ulong.MaxValue;
    private long _queryRevision = -1;
    private int _users;
    private sealed record MetaConditions(Entity[] All, Entity[] None);
    private MetaConditions _meta;
    internal readonly QueryDescription Description;
    private readonly ComponentType[] _all, _none, _changed;
    public World World { get; }
    public bool IsDisposed { get; private set; }

    internal Query(World world, QueryDescription description)
    {
        World = world; Description = description;
        _meta = new(description.MetaAll.ToArray(), description.MetaNone.ToArray());
        _all = EntityType.SortedDistinct<ComponentType>(description.All.ToArray());
        _none = EntityType.SortedDistinct<ComponentType>(description.None.ToArray());
        _changed = EntityType.SortedDistinct<ComponentType>(description.Changed.ToArray());
        foreach (var type in _all) _ = world.Types.Get(type);
        foreach (var type in _none) _ = world.Types.Get(type);
    }

    public int Count
    {
        get { int result = 0; foreach (var view in this) result = checked(result + view.Count); return result; }
    }
    internal int ActiveUses => Math.Max(0, Volatile.Read(ref _users));
    public IDisposable AcquireUsage() => World.AcquireQueryUsage(this);
    internal IDisposable AcquireUsageCore()
    {
        while (true)
        {
            int count = Volatile.Read(ref _users);
            if (count < 0 || IsDisposed) throw new ObjectDisposedException(nameof(Query));
            if (count == int.MaxValue) throw new InvalidOperationException("Too many query users.");
            if (Interlocked.CompareExchange(ref _users, count + 1, count) == count) return new Usage(this);
        }
    }
    private sealed class Usage(Query query) : IDisposable
    {
        private Query? _query = query;
        public void Dispose() { var q = Interlocked.Exchange(ref _query, null); if (q is not null) Interlocked.Decrement(ref q._users); }
    }

    internal Group[] MatchingGroups()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        lock (_cacheGate)
        {
            if (_structure == World.StructureVersion && _queryRevision == World.QueryRevision) return _groups;
            var found = new List<Group>();
            foreach (var group in World.Groups)
                if (MatchesGroupCore(group)) found.Add(group);
            _groups = found.ToArray(); _structure = World.StructureVersion; _queryRevision = World.QueryRevision;
            return _groups;
        }
    }

    internal bool MatchesGroupCore(Group group)
    {
        if (group.IsDead && !Description.IncludeDeadValue || group.IsDisabled && !Description.IncludeDisabledValue) return false;
        foreach (var type in _all) if (!group.Has(type)) return false;
        foreach (var type in Description.Owned) if (!group.Owns(type)) return false;
        foreach (var type in _none)
            if (group.Has(type) && (!Description.MatchEnabled || group.MaskSlot < 0 || !group.Owns(type))) return false;
        foreach (var type in Description.SharedAll) if (!group.Shares(type)) return false;
        foreach (var type in Description.SharedNone) if (group.Shares(type)) return false;
        var metaConditions = Volatile.Read(ref _meta);
        foreach (var meta in metaConditions.All) if (Array.BinarySearch(group.Type.MetaArray, meta) < 0) return false;
        foreach (var meta in metaConditions.None) if (Array.BinarySearch(group.Type.MetaArray, meta) >= 0) return false;
        // Query overloads: a more specific writer of the same (component, phase) owns its matching group.
        if (Description.Writers.Count != 0)
        {
            foreach (var other in World.QuerySnapshot())
            {
                if (ReferenceEquals(this, other) || other.IsDisposed) continue;
                bool competing = false;
                foreach (var writer in Description.Writers)
                    if (other.Description.Writers.Contains(writer)) { competing = true; break; }
                if (!competing) continue;
                bool hasExtra = false, matchesExtra = true;
                foreach (var type in other._all)
                {
                    if (Array.BinarySearch(_all, type) >= 0) continue;
                    hasExtra = true;
                    if (!group.Has(type)) { matchesExtra = false; break; }
                }
                if (hasExtra && matchesExtra) return false;
            }
        }
        return true;
    }

    public bool MatchesGroup(long groupId)
    {
        using var use = World.AcquireUsage();
        using var queryUse = AcquireUsage();
        return MatchesGroupUnderUsage(groupId);
    }
    internal bool MatchesGroupUnderUsage(long groupId) => World.TryGetGroup(groupId, out var group) && MatchesGroupCore(group);

    public unsafe bool Matches(Entity entity)
    {
        using var use = World.AcquireUsage();
        using var queryUse = AcquireUsage();
        return MatchesUnderUsage(entity);
    }

    internal unsafe bool MatchesUnderUsage(Entity entity)
    {
        if (!World.Registry.TryGet(entity, out var entry) || !MatchesGroupCore(entry.Chunk!.Group)) return false;
        var group = entry.Chunk.Group;
        if (Description.MatchEnabled && group.MaskSlot >= 0)
        {
            uint mask = *(uint*)entry.Chunk.Address(group.MaskSlot, entry.Row);
            uint all = group.ComponentMask(_all), none = group.ComponentMask(_none);
            if ((mask & all) != all || (mask & none) != 0) return false;
        }
        if (!MatchesChanged(entry.Chunk)) return false;
        if (Description.Predicate is not null)
        {
            var view = World.GetView(entity);
            if (!Description.Predicate(in view)) return false;
        }
        return true;
    }

    internal unsafe bool MatchesChanged(Chunk chunk)
    {
        if (_changed.Length == 0) return true;
        foreach (var type in _changed)
        {
            int slot = chunk.Group.Archetype.Layout.IndexOf(type);
            if (slot >= 0 && chunk.Versions[slot] > Description.Since) return true;
        }
        return false;
    }

    internal uint AllMask(Group group) => group.ComponentMask(_all);
    internal uint NoneMask(Group group) => group.ComponentMask(_none);
    public QueryEnumerator GetEnumerator() => new(this);
    public QueryRange[] GetWorkRanges()
    {
        var result = new List<QueryRange>();
        using var iterator = GetEnumerator();
        while (iterator.MoveNext()) result.Add(iterator.Range);
        return result.ToArray();
    }

    public void SetMetaFilter(ReadOnlySpan<Entity> all, ReadOnlySpan<Entity> none)
    {
        using var worldUse = World.AcquireUsage();
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (Interlocked.CompareExchange(ref _users, -1, 0) != 0) throw new InvalidOperationException("Cannot alter an in-use query.");
        try
        {
            SetTaskMeta(all, none);
        }
        finally { Volatile.Write(ref _users, 0); }
    }

    // Reused task queries follow the original set_query_meta path. Publish immutable conditions
    // so concurrent message matching never enumerates a mutating List.
    internal void SetTaskMeta(ReadOnlySpan<Entity> all, ReadOnlySpan<Entity> none)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var current = Volatile.Read(ref _meta);
        if (all.SequenceEqual(current.All) && none.SequenceEqual(current.None)) return;
        var replacement = new MetaConditions(all.ToArray(), none.ToArray());
        lock (_cacheGate)
        {
            Volatile.Write(ref _meta, replacement);
            _structure = ulong.MaxValue;
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        if (Interlocked.CompareExchange(ref _users, -1, 0) != 0) throw new InvalidOperationException("Finish query users before releasing it.");
        IsDisposed = true; _groups = []; World.ReleaseQuery(this);
    }
}

public unsafe ref struct QueryEnumerator
{
    private readonly Query _query;
    private readonly Group[] _groups;
    private readonly IDisposable _worldUse, _queryUse;
    private int _groupIndex, _chunkIndex;
    private Chunk? _chunk;
    private MaskScanner.RangeEnumerator _masks;
    private bool _masked;
    public QueryRange Range { get; private set; }
    public ChunkView Current => Range.View;

    internal QueryEnumerator(Query query)
    {
        _query = query; _worldUse = query.World.AcquireUsage();
        try { _queryUse = query.AcquireUsage(); }
        catch { _worldUse.Dispose(); throw; }
        try { _groups = query.MatchingGroups(); }
        catch { _worldUse.Dispose(); _queryUse.Dispose(); throw; }
        _groupIndex = 0; _chunkIndex = 0; _chunk = null; _masked = false; _masks = default; Range = default;
    }

    public bool MoveNext()
    {
        while (true)
        {
            if (_masked && _masks.MoveNext())
            {
                var maskRange = _masks.Current;
                var range = new QueryRange(_query.World, _chunk!, maskRange.Start, maskRange.Count);
                var view = range.View;
                if (_query.Description.Predicate is not null && !_query.Description.Predicate(in view)) continue;
                Range = range; return true;
            }
            _masked = false;
            while (_groupIndex < _groups.Length && _chunkIndex == _groups[_groupIndex].Chunks.Count) { _groupIndex++; _chunkIndex = 0; }
            if (_groupIndex == _groups.Length) return false;
            var group = _groups[_groupIndex];
            _chunk = group.Chunks[_chunkIndex++];
            if (_chunk.Count == 0 || !_query.MatchesChanged(_chunk)) continue;
            if (_query.Description.MatchEnabled && group.MaskSlot >= 0)
            {
                _masks = MaskScanner.Scan(new ReadOnlySpan<uint>((void*)_chunk.Address(group.MaskSlot, 0), _chunk.Count), _query.AllMask(group), _query.NoneMask(group));
                _masked = true; continue;
            }
            var whole = new QueryRange(_query.World, _chunk, 0, _chunk.Count);
            var wholeView = whole.View;
            if (_query.Description.Predicate is not null && !_query.Description.Predicate(in wholeView)) continue;
            Range = whole; return true;
        }
    }

    public void Dispose() { _queryUse.Dispose(); _worldUse.Dispose(); }
}
