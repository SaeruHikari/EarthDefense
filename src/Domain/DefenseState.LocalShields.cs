using System;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    private long _shieldBuildReadyWave;

    public Func<double, double>? RechargeLocalShields { get; set; }
    public Func<bool>? HasDamagedLocalShields { get; set; }

    /// <summary>
    /// Number of local shield generators currently permitted by the researched
    /// defense branch.  The value is deliberately data driven so the cap can be
    /// expanded by later shield technologies without changing build logic.
    /// </summary>
    public int LocalShieldBuildLimit => Math.Clamp(TechEffects().I("shield_building_limit_add"), 0, 1024);

    public int LocalShieldBuildCooldownWaves
        => Math.Clamp(CatalogData.Load("economy.json").Map("local_shield").I("build_cooldown_waves", 3), 0, 1024);

    public long LocalShieldBuildReadyWave => _shieldBuildReadyWave;

    public long LocalShieldBuildCooldownRemaining
        => Math.Max(0, _shieldBuildReadyWave - Wave);

    /// <summary>Returns the actionable reason a researched shield cannot be built yet.</summary>
    public string ShieldBuildLockReason()
    {
        if (!HasResearch("D_N4"))
            return BuildingLockReason("shield");
        int limit = LocalShieldBuildLimit;
        if (limit <= 0)
            return "区域护盾工程尚未提供可用建造额度";
        if (Buildings.L("shield") >= limit)
            return $"局部护盾发生器已达科技上限 {limit} 座";
        long remaining = LocalShieldBuildCooldownRemaining;
        return remaining > 0 ? $"局部护盾建造冷却中，还需 {remaining} 波" : "";
    }

    private double RequestLocalShieldRecharge(double budget)
    {
        if (!double.IsFinite(budget) || budget <= 0 || RechargeLocalShields == null) return 0;
        double actual = RechargeLocalShields(budget);
        return double.IsFinite(actual) ? Math.Clamp(actual, 0, budget) : 0;
    }
    public DataMap ShieldFacilityStats()
    {
        var values = TechEffects();
        bool rebuild = HasResearch("D_G2");
        return new()
        {
            ["capacity"] = ShieldMax(), ["regeneration"] = ShieldRegeneration(),
            ["surface_radius"] = CatalogData.Load("economy.json").Map("local_shield").N("surface_radius", 5),
            ["altitude"] = (double)WorldScale.ShieldAltitude,
            ["build_limit"] = LocalShieldBuildLimit,
            ["build_cooldown_waves"] = LocalShieldBuildCooldownWaves,
            ["build_ready_wave"] = LocalShieldBuildReadyWave,
            ["build_cooldown_remaining"] = LocalShieldBuildCooldownRemaining,
            ["break_recovery_fraction"] = rebuild ? values.N("zero_shield_fraction", .25) : 0,
            ["break_hold_seconds"] = rebuild ? values.N("zero_shield_duration", 2) : 0,
            ["break_cooldown"] = rebuild ? values.N("zero_shield_cooldown", 30) : 0
        };
    }
    public bool BuildingUnlocked(string id) => BuildingLockReason(id).Length == 0;
    public string BuildingLockReason(string id)
    {
        var definition = Find(BuildingDefinitions, id);
        if (definition.Count == 0) return "\u672a\u77e5\u5efa\u7b51";
        if (id == "starship_silo" && (!ExpeditionEnabled || !Expedition.Research.B("telescope"))) return "\u661f\u8230\u7cfb\u7edf\u5c1a\u672a\u5f00\u653e";
        if (id is "laser" or "missile" && !WeaponUnlocked(id)) return "\u9700\u8981\u7814\u7a76\u5bf9\u5e94\u6b66\u5668\u79d1\u6280";
        string research = definition.S("unlock_research");
        return research.Length > 0 && !HasResearch(research) ? "\u9700\u8981\u7814\u7a76\u300c" + DeepTechnology.Definition(research).S("name", research) + "\u300d" : "";
    }
}
