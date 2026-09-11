using System;
using System.Collections.Generic;
using System.Linq;
namespace Earthward.Domain;

public static partial class DeepTechnology
{
    public static IReadOnlyList<DataMap> Nodes => CatalogData.Rows("deep-technology.json", "nodes");
    public static IReadOnlyList<DataMap> Branches => CatalogData.Rows("deep-technology.json", "branches");
    public static DataMap Definition(string id) => TryParseSuccessor(id, out string branch, out int rank) ? SuccessorDefinition(branch, rank) : CatalogData.Definition("deep-technology.json", "nodes", id);
    public static bool Has(string id) => Definition(id).Count > 0;
    private static readonly string[] SuccessorEffectKeys = { "kinetic_damage_bonus", "missile_damage_bonus", "laser_damage_bonus", "science_output_bonus", "health_bonus", "patrol_speed_bonus" };
    public static DataMap Effects(DataMap levels, ISet<string>? credited = null, DataMap? successorLevels = null)
    {
        var result = new DataMap { ["tech_abilities"] = new DataMap(), ["production_lanes"] = 1L, ["action_radius"] = 0d, ["strategic_enabled"] = false, ["strategic_speed_multiplier"] = 1d };
        foreach (var row in Nodes)
        {
            string id = row.S("id");
            if (row.S("size") != "small")
                result.Map("tech_abilities")[id] = levels.L(id) > 0;
            if (levels.L(id) <= 0 || credited?.Contains(id) == true)
                continue;
            foreach (var (key, value) in row.Map("values"))
            {
                if (key.EndsWith("_bonus") || key.EndsWith("_add"))
                    result[key] = result.N(key) + DataMap.Number(value);
                else if (value is bool)
                    result[key] = value;
                else if (key.EndsWith("_multiplier"))
                    result[key] = result.N(key, 1) * DataMap.Number(value);
                else
                    result[key] = Math.Max(result.N(key), DataMap.Number(value));
            }
        }
        if (successorLevels != null)
            foreach (var (branch, value) in successorLevels)
            {
                int index = Array.IndexOf(ChainBranches, branch);
                if (index >= 0)
                    result[SuccessorEffectKeys[index]] = result.N(SuccessorEffectKeys[index]) + .03 * DataMap.Integer(value);
            }
        return result;
    }
    public static DataMap? Validate(object? value)
    {
        if (value is not DataMap data || data.Count != 4 || !DataMap.ValidNumber(data.Value("version"), 1, 2, true) || data.Value("nodes") is not DataMap levels || data.Value("credited") is not List<object?> list || levels.Count > Nodes.Count || list.Count > Nodes.Count)
            return null;
        string field = data.I("version") == 1 ? "refinements" : "successor_levels";
        if (data.Value(field) is not DataMap tails || tails.Count > ChainBranches.Length)
            return null;
        // Continuation ownership has one canonical compressed representation; never also store R IDs here.
        if (levels.Any(pair => CatalogData.Definition("deep-technology.json", "nodes", pair.Key).Count == 0 || !DataMap.ValidNumber(pair.Value, 1, 1, true)))
            return null;
        var credited = new HashSet<string>();
        foreach (var item in list)
            if (item is not string id || !levels.ContainsKey(id) || !credited.Add(id))
                return null;
        foreach (string id in levels.Keys)
            if (!credited.Contains(id) && Definition(id).List("requires").Cast<string>().Any(parent => !levels.ContainsKey(parent)))
                return null;
        var chain = new DataMap();
        foreach (var (key, count) in tails)
        {
            string branch = data.I("version") == 1 && key.EndsWith("_G2", StringComparison.Ordinal) ? key[..^3] : key;
            if (!ChainBranches.Contains(branch) || (data.I("version") == 1 && key != branch + "_G2") || !levels.ContainsKey(branch + "_G2") || !DataMap.ValidNumber(count, 1, SuccessorMaxRank, true))
                return null;
            chain[branch] = DataMap.Integer(count);
        }
        return new() { ["version"] = 2L, ["nodes"] = levels.DeepClone(), ["credited"] = DataMap.Clone(list), ["successor_levels"] = chain };
    }

}

public static class AirframeCatalog
{
    public static IReadOnlyList<DataMap> Definitions => CatalogData.Rows("airframes.json", "definitions");
    public static DataMap Base => CatalogData.Load("airframes.json").Map("base").DeepClone();
    public static DataMap Definition(string id) => CatalogData.Definition("airframes.json", "definitions", id);
    public static string BaseFor(string kind) => Base.S(kind, "K1");
    public static bool Compatible(string id, string kind) => Definition(id).S("kind") == kind && Definition(id).Count > 0;
    public static DataMap EmptySelection() => new() { ["templates"] = Base, ["sites"] = new DataMap(), ["berths"] = new DataMap() };
    public static DataMap? Validate(object? value)
    {
        if (value is not DataMap map || map.Count != 3 || new[] { "templates", "sites", "berths" }.Any(key => map.Value(key) is not DataMap))
            return null;
        if (map.Map("templates").Count != 3 || map.Map("sites").Count > FactoryPerks.MaxSites || map.Map("berths").Count > 65536)
            return null;
        foreach (string kind in Base.Keys)
            if (map.Map("templates").Value(kind) is not string id || !Compatible(id, kind))
                return null;
        var kinds = new Dictionary<string, string>();
        foreach (string field in new[] { "sites", "berths" })
            foreach (var (key, data) in map.Map(field))
            {
                var parts = key.Split(':');
                if (parts.Length != (field == "berths" ? 2 : 1) || parts.Any(part => !long.TryParse(part, out long number) || number < 0 || number > 9007199254740991 || number.ToString(System.Globalization.CultureInfo.InvariantCulture) != part))
                    return null;
                if (field == "berths" && long.Parse(parts[1]) > 32767)
                    return null;
                if (data is not DataMap row || row.Count != 2 || !Compatible(row.S("airframe"), row.S("kind")))
                    return null;
                if (kinds.TryGetValue(parts[0], out string? previous) && previous != row.S("kind"))
                    return null;
                kinds[parts[0]] = row.S("kind");
            }
        return map.DeepClone();
    }
}
