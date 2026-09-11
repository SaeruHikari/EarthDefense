using System;
using System.Collections.Generic;
using Earthward.Domain;
namespace Earthward.Combat;

public static class DefenseWavePlan
{
    public static string[] RoleOrder => CombatCatalog.Current.RoleOrder;
    public static DataMap Composition(long wave)
    {
        var rows = CombatCatalog.Current.Composition;
        foreach (var row in rows)
            if (row.Through == 0 || wave <= row.Through) return row.Weights.DeepClone();
        throw new InvalidOperationException("Validated wave composition has no final range");
    }
    public static DataMap Build(long wave, long count, double spawnSeconds, double cycleSeconds, bool medium, int stage = 0, int carriers = 0)
    {
        long reserve = 1 + (medium ? 1 : 0) + Math.Max(0, carriers), total = Math.Max(reserve, count), regular = total - reserve, used = 0;
        var weights = Composition(wave);
        double sum = 0;
        foreach (var pair in weights)
            sum += Convert.ToDouble(pair.Value);
        var counts = new DataMap();
        foreach (var role in RoleOrder)
        {
            long quantity = (long)Math.Floor(regular * C.N(weights, role) / Math.Max(sum, .001));
            counts[role] = quantity;
            used += quantity;
        }
        counts["claw"] = C.L(counts, "claw") + regular - used;
        return new()
        {
            ["wave"] = wave,
            ["planned_count"] = total,
            ["duration"] = spawnSeconds,
            ["cycle_duration"] = Math.Max(spawnSeconds, cycleSeconds),
            ["stage"] = stage,
            ["medium"] = medium,
            ["carrier_count"] = carriers,
            ["composition"] = counts,
            ["regular_count"] = regular,
            ["stride"] = Math.Max(1, regular - 1)
        };
    }
    public static DataMap Entry(DataMap plan, long index)
    {
        long carriers = C.L(plan, "carrier_count"), total = C.L(plan, "planned_count"), smallAt = carriers + (long)Math.Floor((total - carriers) * CombatCatalog.Current.Values.SmallBossSpawnFraction), mediumAt = Math.Min(total - 1, Math.Max(smallAt + 1, carriers + (long)Math.Floor((total - carriers) * CombatCatalog.Current.Values.MediumBossSpawnFraction)));
        bool medium = C.B(plan, "medium");
        string kind = index < carriers ? "carrier" : index == smallAt ? "small_boss" : medium && index == mediumAt ? "boss" : "";
        var entry = new DataMap { ["kind"] = kind, ["index"] = index, ["wave"] = C.L(plan, "wave"), ["stage"] = C.I(plan, "stage"), ["planned_uid"] = $"wave:{C.L(plan, "wave")}:unit:{index}" };
        if (kind != "")
            return entry;
        long ordinal = index - carriers - (index > smallAt ? 1 : 0) - (medium && index > mediumAt ? 1 : 0), shuffled = C.Posmod(unchecked(ordinal * C.L(plan, "stride") + C.L(plan, "wave") * 7), Math.Max(1, C.L(plan, "regular_count"))), cumulative = 0;
        foreach (var role in RoleOrder)
        {
            cumulative += C.L(C.M(plan, "composition"), role);
            if (shuffled >= cumulative)
                continue;
            entry["kind"] = CombatCatalog.Current.Roles[role].Kind;
            entry["role"] = role;
            return entry;
        }
        entry["kind"] = "scout";
        entry["role"] = "claw";
        return entry;
    }
}
