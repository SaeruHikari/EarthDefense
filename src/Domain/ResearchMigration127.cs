using System;
using System.Collections.Generic;
using System.Linq;
namespace Earthward.Domain;

public static partial class DeepTechnology
{
    public static IReadOnlyList<DataMap> Migration127SourceNodes => CatalogData.Rows("technology-migration-127.json", "source_nodes");
    public static DataMap Migration127SourceDefinition(string id) => CatalogData.Definition("technology-migration-127.json", "source_nodes", id);
    private static DataMap Migration127Data => CatalogData.Load("technology-migration-127.json");
    // These credited legacy nodes now grant replacement attributes; their former ledger never supplied those attributes.
    public static bool IsConvertedLegacyEffect(string id)
    {
        var data = Migration127Data;
        foreach (string source in data.List("retired_small_ids").Cast<string>())
            if (id == source || id == data.Map("pairs").S(source)) return true;
        return false;
    }
    public static DataMap Migrate127(DataMap oldLevels, ISet<string> oldCredited, DataMap chains, DataMap legacy, int sourceVersion)
    {
        var data = Migration127Data; var pairs = data.Map("pairs");
        var nodes = oldLevels.DeepClone(); var credited = oldCredited.ToHashSet();
        int split = 0, converted = 0;
        foreach (var (id, value) in pairs)
        {
            if (!oldLevels.ContainsKey(id)) continue;
            string companion = (string)value!; nodes[companion] = 1L; split++;
            if (oldCredited.Contains(id)) credited.Add(companion);
            if (data.List("retired_small_ids").Contains(id)) converted++;
        }
        long legacyCapacity = Math.Max(0, legacy.L("hangar_capacity"));
        long paidCapacity = data.Map("capacity_small_values").Where(p => oldLevels.ContainsKey(p.Key) && !oldCredited.Contains(p.Key)).Sum(p => DataMap.Integer(p.Value));
        int target = (int)Math.Min(3, legacyCapacity + paidCapacity), coveredByLedger = (int)Math.Min(3, legacyCapacity);
        for (int i = 0; i < target; i++)
        {
            string id = data.List("capacity_nodes")[i] as string ?? ""; nodes[id] = 1L;
            if (i < coveredByLedger) credited.Add(id);
        }
        var refund = new DataMap { ["minerals"] = 0d, ["energy"] = 0d, ["science"] = 0d };
        var legacyDefinition = CatalogData.Definition("legacy-technology.json", "definitions", "hangar_capacity");
        for (int rank = 3; rank < legacyCapacity; rank++)
            foreach (string currency in refund.Keys.ToList())
                refund[currency] = refund.N(currency) + Math.Ceiling(legacyDefinition.Map("cost").N(currency) * Math.Pow(1.32, rank));
        int remaining = 3 - coveredByLedger;
        foreach (var (id, value) in data.Map("capacity_small_values"))
        {
            if (!oldLevels.ContainsKey(id) || oldCredited.Contains(id)) continue;
            int points = (int)DataMap.Integer(value), accepted = Math.Min(remaining, points); remaining -= accepted;
            refund["science"] = refund.N("science") + Migration127SourceDefinition(id).Map("cost").N("science") * (points - accepted) / points;
        }
        var summary = new DataMap
        {
            ["source_version"] = (long)sourceVersion, ["converted_count"] = (long)converted, ["split_count"] = (long)split,
            ["previous_capacity"] = legacyCapacity + paidCapacity, ["research_capacity"] = (long)target, ["refund"] = refund,
            ["capacity_note"] = legacyCapacity + paidCapacity > 3 ? "\u7814\u7a76\u7f16\u5236\u4e0a\u9650\u8c03\u6574\u4e3a+3\uff0c\u8d85\u51fa\u90e8\u5206\u6309\u539f\u6295\u8d44\u8fd4\u8fd8\u8d44\u6e90" : ""
        };
        return new() { ["version"] = 3L, ["nodes"] = nodes, ["credited"] = credited.OrderBy(s => s, StringComparer.Ordinal).Cast<object?>().ToList(), ["successor_levels"] = chains.DeepClone(), ["migration"] = summary };
    }
    public static bool IsMigratedCapacityGrant(string id, DataMap summary)
    {
        int index = Array.IndexOf(new[] { "I_N4", "I_N5", "I_N6" }, id);
        return index >= 0 && ValidMigration127Summary(summary) && summary.I("research_capacity") > index;
    }
    public static bool ValidMigration127Summary(DataMap data)
    {
        if (data.Count == 0) return true;
        if (data.Count != 7 || !DataMap.ValidNumber(data.Value("source_version"), 0, 2, true)
            || !DataMap.ValidNumber(data.Value("converted_count"), 0, 28, true) || !DataMap.ValidNumber(data.Value("split_count"), 0, 123, true)
            || !DataMap.ValidNumber(data.Value("previous_capacity"), 0, 14, true) || !DataMap.ValidNumber(data.Value("research_capacity"), 0, 3, true)
            || data.I("research_capacity") != Math.Min(3, data.I("previous_capacity")) || data.Value("capacity_note") is not string note || note.Length > 256
            || data.Value("refund") is not DataMap refund || refund.Count != 3) return false;
        return new[] { "minerals", "energy", "science" }.All(k => DataMap.ValidNumber(refund.Value(k), 0, 1e100));
    }
}

public sealed partial class DefenseState
{
    public int ResearchCapacityBonus => (int)Math.Clamp(Tech.L("hangar_capacity") + TechEffects().L("capacity_add"), 0, 3);
    public DataMap LastResearchMigration => _researchMigration.DeepClone();
    public DataMap LastResearchRefund => _researchRefundApplied.DeepClone();
}
