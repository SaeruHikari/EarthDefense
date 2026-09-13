namespace Sugoi.Data;

/// <summary>Build-time query declaration. World.CreateQuery takes an owned copy.</summary>
public sealed class QueryDescription
{
    internal readonly List<ComponentType> All = [];
    internal readonly List<ComponentType> Owned = [];
    internal readonly List<ComponentType> None = [];
    internal readonly List<ComponentType> SharedAll = [];
    internal readonly List<ComponentType> SharedNone = [];
    internal readonly List<Entity> MetaAll = [];
    internal readonly List<Entity> MetaNone = [];
    internal readonly List<ComponentType> Changed = [];
    internal readonly List<(ComponentType Type, int Phase)> Writers = [];
    internal uint Since;
    internal bool IncludeDisabledValue;
    internal bool IncludeDeadValue;
    internal bool MatchEnabled = true;
    internal QueryPredicate? Predicate;

    public static QueryDescription Create() => new();
    public QueryDescription WithAll(params ComponentType[] types) { All.AddRange(types); return this; }
    /// <summary>Requires physical ownership as well as presence; shared/meta inheritance cannot satisfy this constraint.</summary>
    public QueryDescription WithOwned(params ComponentType[] types) { All.AddRange(types); Owned.AddRange(types); return this; }
    public QueryDescription Without(params ComponentType[] types) { None.AddRange(types); return this; }
    public QueryDescription WithShared(params ComponentType[] types) { SharedAll.AddRange(types); return this; }
    public QueryDescription WithoutShared(params ComponentType[] types) { SharedNone.AddRange(types); return this; }
    public QueryDescription WithMeta(params Entity[] entities) { MetaAll.AddRange(entities); return this; }
    public QueryDescription WithoutMeta(params Entity[] entities) { MetaNone.AddRange(entities); return this; }
    public QueryDescription ChangedSince(uint version, params ComponentType[] types) { Since = version; Changed.AddRange(types); return this; }
    public QueryDescription IncludeDisabled(bool include = true) { IncludeDisabledValue = include; return this; }
    public QueryDescription IncludeDead(bool include = true) { IncludeDeadValue = include; return this; }
    public QueryDescription IgnoreEnabledMask(bool ignore = true) { MatchEnabled = !ignore; return this; }
    public QueryDescription Where(QueryPredicate predicate) { Predicate = predicate; return this; }
    public QueryDescription WritePhase(ComponentType type, int phase) { Writers.Add((type, phase)); return this; }

    internal QueryDescription Copy()
    {
        var copy = new QueryDescription { Since = Since, IncludeDisabledValue = IncludeDisabledValue,
            IncludeDeadValue = IncludeDeadValue, MatchEnabled = MatchEnabled, Predicate = Predicate };
        copy.All.AddRange(All); copy.Owned.AddRange(Owned); copy.None.AddRange(None); copy.SharedAll.AddRange(SharedAll); copy.SharedNone.AddRange(SharedNone);
        copy.MetaAll.AddRange(MetaAll); copy.MetaNone.AddRange(MetaNone); copy.Changed.AddRange(Changed); copy.Writers.AddRange(Writers);
        return copy;
    }
}

public delegate bool QueryPredicate(in ChunkView view);
