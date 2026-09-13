using System;
using Earthward.Domain;
namespace Earthward.Combat;

/// <summary>Uniform aircraft deployments; carriers only establish continuing invasion fronts.</summary>
public static class DefenseWavePlan
{
    public static string[] RoleOrder => CombatCatalog.Current.RoleOrder;
    public static DataMap Composition(long wave) => new() { ["claw"] = 1d };

    public static DataMap Build(long wave, long count, double spawnSeconds, double cycleSeconds, int stage = 0, int carriers = 0)
    {
        carriers = Math.Max(0, carriers);
        long total = Math.Max(carriers, count), regular = total - carriers;
        return new()
        {
            ["wave"] = wave,
            ["planned_count"] = total,
            ["duration"] = spawnSeconds,
            ["cycle_duration"] = Math.Max(spawnSeconds, cycleSeconds),
            ["stage"] = stage,
            ["carrier_count"] = carriers,
            ["composition"] = new DataMap { ["claw"] = regular },
            ["regular_count"] = regular
        };
    }

    public static DataMap Entry(DataMap plan, long index)
    {
        if (index < 0 || index >= plan.L("planned_count"))
            throw new ArgumentOutOfRangeException(nameof(index));
        bool carrier = index < plan.L("carrier_count");
        return new()
        {
            ["kind"] = carrier ? "carrier" : "scout",
            ["role"] = carrier ? "" : "claw",
            ["index"] = index,
            ["wave"] = plan.L("wave"),
            ["stage"] = plan.I("stage"),
            ["planned_uid"] = $"wave:{plan.L("wave")}:unit:{index}"
        };
    }
}
