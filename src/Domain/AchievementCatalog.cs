using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Earthward.Domain;

/// <summary>Permanent achievement definitions and rewards, separate from run research.</summary>
public static class AchievementCatalog
{
    public const string FirstDefeatId = "first_defeat";
    public const string ShieldCapacityAttribute = "shield_building_limit_add";
    private static long _revision = -1;
    private static IReadOnlyList<DataMap> _entries = Array.Empty<DataMap>();
    private static Dictionary<string, DataMap> _definitions = new(StringComparer.Ordinal);

    public static IReadOnlyList<DataMap> Entries { get { Validate(); return _entries; } }
    public static DataMap Definition(string id)
    {
        Validate();
        return _definitions.TryGetValue(id, out var row) ? row.DeepClone() : new();
    }

    public static void Validate()
    {
        if (_revision == CatalogData.Revision) return;
        var table = CatalogData.ReadCsv("achievements.csv");
        table.RequireHeaders("id", "name", "description", "condition", "event", "reward_attribute", "reward_per_research", "icon");
        var definitions = new Dictionary<string, DataMap>(StringComparer.Ordinal);
        foreach (var row in table.Rows)
        {
            string id = row.String("id");
            long bonus = row.Integer("reward_per_research");
            if (id != FirstDefeatId || row.String("event") != "earth_defeat" || row.String("reward_attribute") != ShieldCapacityAttribute)
                throw row.Error("id", "unsupported achievement event or reward");
            if (bonus is < 1 or > 1024 || new[] { "name", "description", "condition", "icon" }.Any(k => string.IsNullOrWhiteSpace(row.String(k))))
                throw row.Error("reward_per_research", "positive bounded reward and display text are required");
            var definition = new DataMap();
            foreach (string column in table.Headers) definition[column] = row.String(column);
            definition["reward_per_research"] = bonus;
            if (!definitions.TryAdd(id, definition)) throw row.Error("id", "duplicate achievement");
        }
        if (definitions.Count != 1) throw new InvalidDataException("achievements.csv: exactly one first-defeat achievement is required");
        _definitions = definitions;
        _entries = definitions.Values.ToList().AsReadOnly();
        _revision = CatalogData.Revision;
    }
}
