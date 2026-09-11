using System;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public Func<double, double>? RechargeLocalShields { get; set; }
    public Func<bool>? HasDamagedLocalShields { get; set; }
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
