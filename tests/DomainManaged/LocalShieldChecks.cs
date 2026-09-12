using Earthward.Domain;
using System;
using System.Linq;

internal static class LocalShieldChecks
{
    internal static void Run(Action<bool, string> check, DataMap golden)
    {
        void Check(bool value, string label) => check(value, "local shields: " + label);
        void Near(double a, double b, string label) => Check(Math.Abs(a - b) < 1e-8, label + $" {a} / {b}");
        var game = new DefenseState();
        Check(game.Shield == 0 && game.Buildings.L("shield") == 0 && !game.BuildingUnlocked("shield"), "new run has no global shield or free tower");
        Check(!DefenseState.ResourceFacilityKinds.Contains("lab") && game.Buildings.L("lab") == 0 && !game.BuildingUnlocked("lab"), "surface laboratory is removed from new runs");
        Near(game.Rates().N("science"), 0, "science stays offline until the first satellite reaches orbit");
        Check(game.BuildingMaxCount("satellite_launcher") == 1 && game.CanBuild("satellite_launcher"), "one-time satellite launcher is available in a new run");
        Check(game.Build("satellite_launcher", 2) && game.HasSatelliteLauncher && !game.CanBuild("satellite_launcher"), "satellite launcher can only be built once");
        Check(game.BeginResearchSatelliteLaunch() && game.ResearchSatelliteLaunchInProgress && game.Rates().N("science") == 0, "research satellite launch starts with science offline");
        Check(game.CompleteResearchSatelliteLaunch() && game.ResearchSatelliteDeployed && game.Rates().N("science") > 0, "completed satellite launch activates conservative science income");
        var opening = new DefenseState();
        Check(opening.PurchaseGroup("D_N4") && opening.ResearchLevel("D_N4") == 1, "local shield is a free single-level medium technology available at opening");
        Check(opening.LocalShieldBuildLimit == 2, "opening shield technology grants two field slots");
        Check(opening.BuildingCost("shield").Count == 0, "local shield generator has no construction cost");
        Check(opening.Build("shield", 40) && opening.Wave == 0, "first local shield tower can be placed without resources");
        Near(opening.Minerals, 320, "free shield leaves opening minerals"); Near(opening.Energy, 180, "free shield leaves opening energy"); Near(opening.Science, 0, "free local shield research does not consume science"); Near(opening.ResourceCores, 0, "free local shield construction does not consume resource cores");
        Check(opening.LocalShieldBuildReadyWave == 3 && opening.LocalShieldBuildCooldownRemaining == 3, "shield construction starts a three-wave cooldown");
        opening.Wave = 2;
        Check(!opening.CanBuild("shield") && opening.LocalShieldBuildCooldownRemaining == 1,
            "second opening slot cannot bypass the three-wave construction cooldown");
        opening.Wave = 3;
        Check(opening.Build("shield", 42) && opening.Buildings.L("shield") == 2 && opening.LocalShieldBuildReadyWave == 6,
            "second opening slot is actually buildable after the cooldown and starts another three-wave cooldown");
        opening.Wave = 6;
        Check(opening.LocalShieldBuildCooldownRemaining == 0 && !opening.CanBuild("shield") && !opening.Build("shield", 43),
            "two-generator opening cap blocks a third tower even after its cooldown expires");
        opening.TakeDamage(10); opening.Tick(60);
        Near(opening.EarthHp, 90, "installed generators cannot repair Earth without the optional repair research");
        Near(opening.LocalShieldEarthRepairRate(), 0, "unresearched repair contributes no passive rate");
        var earthRepair = new DefenseState { Science = 6 };
        Check(earthRepair.PurchaseGroup("D_N4") && earthRepair.PurchaseGroup("D_S41"), "six science buys the repair technology after free local shield engineering");
        Near(earthRepair.Science, 0, "repair research charges exactly six science");
        earthRepair.TakeDamage(10); earthRepair.Tick(10);
        Near(earthRepair.EarthHp, 90, "repair research alone cannot heal Earth before a generator is built");
        Near(earthRepair.LocalShieldEarthRepairRate(), 0, "researched repair without generators has zero rate");
        Check(earthRepair.Build("shield", 41), "Earth repair fixture can build a free local shield generator");
        earthRepair.Tick(10);
        Near(earthRepair.EarthHp, 90.5, "one local shield repairs half an Earth health point in ten seconds");
        Near(earthRepair.LocalShieldEarthRepairRate(), .05, "Earth repair rate is exposed per installed generator");
        earthRepair.Wave = 3;
        Check(earthRepair.Build("shield", 42), "second generator follows the ordinary three-wave construction cooldown");
        Near(earthRepair.LocalShieldEarthRepairRate(), .1, "two generators contribute their combined repair rate");
        earthRepair.Tick(10); Near(earthRepair.EarthHp, 91.5, "two generators restore one Earth health point in ten seconds");
        earthRepair.Tick(1000); Near(earthRepair.EarthHp, 100, "passive repair cannot exceed the hundred-point Earth health cap");
        earthRepair.TakeDamage(100); earthRepair.Tick(1000);
        Near(earthRepair.EarthHp, 0, "researched generators cannot revive a defeated Earth");
        var stats = game.ShieldFacilityStats();
        Near(stats.N("capacity"), 200, "per-facility doubled base capacity"); Near(stats.N("regeneration"), 1.6, "per-facility doubled base regeneration");
        Near(stats.N("surface_radius"), 6.5, "wider world-space ground coverage"); Near(stats.N("altitude"), Earthward.WorldScale.ShieldAltitude, "shared shell altitude");
        Near(stats.N("break_recovery_fraction"), 0, "rebuild requires technology");
        game.Shield = 77; game.Tick(1); Near(game.Shield, 77, "legacy compatibility balance does not regenerate");
        game.TakeDamage(10); Near(game.EarthHp, 90, "no tower means direct Earth damage"); Near(game.Shield, 77, "old global balance cannot absorb damage");
        game.Minerals = game.Energy = game.Science = 10000; game.Wave = 7; game.CompletedWaves = 6;
        Check(!game.CanBuild("shield"), "funds alone do not unlock tower");
        foreach (string id in new[] { "D_S01", "D_S21", "D_S02", "D_S22", "D_N1", "D_N4" }) Check(game.PurchaseGroup(id), "reachable shield research " + id);
        Check(game.BuildingUnlocked("shield") && game.BuildingLockReason("shield") == "" && game.Buildings.L("shield") == 0, "research permits construction without gifting a tower");
        var cost = game.BuildingCost("shield"); Check(cost.Count == 0, "shield generator remains free after research");
        Check(game.Build("shield", 50) && game.Buildings.L("shield") == 1 && game.ResourceCores == 0, "tower is an explicit ordinary free build");
        var saved = game.Serialize(); var copy = new DefenseState(); Check(copy.Restore(saved) && copy.Buildings.L("shield") == 1 && copy.LocalShieldBuildReadyWave == game.LocalShieldBuildReadyWave, "tower ownership and build cooldown persist");
        var malformed = saved.DeepClone(); malformed.Map("research_state").Map("nodes").Remove("D_N4"); Check(!copy.Restore(malformed), "cannot restore a tower without its unlock");
        var poor = new DefenseState(); Check(poor.Restore(saved), "unlocked building test fixture"); poor.Minerals = poor.Energy = 0;
        Check(poor.BuildingUnlocked("shield") && !poor.CanBuild("shield"), "UI technology lock is distinct from insufficient funds");
        // The legacy scalar shield balance never regenerates and never absorbs damage.
        var legacy = new DefenseState { Shield = 500 };
        legacy.Shield = 500; legacy.Tick(1); Near(legacy.Shield, 500, "unused old balance is preserved but inert");
        Near(legacy.ShieldFacilityStats().N("capacity"), legacy.ShieldMax(), "all shield investment feeds one facility");
        var full = new DefenseState { Science = 1e9, AlienPoints = 1000000 };
        Check(full.UnlockAllTechnologyCheat(), "advanced shield technology owned");
        Check(full.LocalShieldBuildLimit == 9, "defense technology expands the local shield field cap to nine");
        Check(full.Build("shield", 70), "free shield generator can be built without a wallet cost");
        full.Wave = 2;
        Check(!full.CanBuild("shield") && full.LocalShieldBuildCooldownRemaining == 1, "second shield remains locked during the three-wave cooldown");
        full.Wave = 3;
        Check(full.CanBuild("shield") && full.Build("shield", 71), "shield cooldown unlocks on the third following wave");
        Check(full.LocalShieldBuildReadyWave == 6, "each new shield restarts the three-wave cooldown");
        var fullStats = full.ShieldFacilityStats(); Near(fullStats.N("break_recovery_fraction"), .25, "D_G2 rebuild fraction"); Near(fullStats.N("break_hold_seconds"), 2, "D_G2 local protection duration"); Near(fullStats.N("break_cooldown"), 30, "D_G2 local cooldown");
        full.Shield = 1000; full.EarthHp = 100; full.TakeDamage(30); Near(full.EarthHp, 70, "advanced shield research does not silently create global armor");
        full.EarthHp = 100; full.RechargeLocalShields = null; Near(full.ApplySacrificeRecovery(10000), 0, "no tower receives no sacrifice benefit");
        double acceptedTotal = 0, perSecond = full.ShieldMax() * .02;
        full.RechargeLocalShields = budget => { double accepted = Math.Min(budget, .5); acceptedTotal += accepted; return accepted; };
        Near(full.ApplySacrificeRecovery(10000), .5, "sacrifice accounts actual accepted amount");
        full.RechargeLocalShields = budget => { acceptedTotal += budget; return budget; };
        Near(full.ApplySacrificeRecovery(10000), perSecond - .5, "remaining budget is shared across all shields");
        Near(full.ApplySacrificeRecovery(10000), 0, "same-second cap cannot be multiplied by tower count"); Near(acceptedTotal, perSecond, "fixed total per-second recharge");
        var budgetSave = full.Serialize(); var restored = new DefenseState(); Check(restored.Restore(budgetSave), "sacrifice budget persists"); restored.RechargeLocalShields = amount => amount;
        Near(restored.ApplySacrificeRecovery(10000), 0, "reload cannot repeat sacrifice budget"); restored.Tick(1.01); Near(restored.ApplySacrificeRecovery(10000), perSecond, "next second restores budget");
        var cheated = new DefenseState(); var beforeCheat = cheated.Serialize();
        Check(!cheated.UnlockAllTechnologyCheat(() => false) && DataMap.Equivalent(beforeCheat, cheated.Serialize()), "failed all-tech persistence restores flags and ownership");
        Check(cheated.UnlockAllTechnologyCheat(), "all-tech action completes only fixed-tree nodes");
        Check(cheated.Wave == 0 && cheated.CompletedWaves == 0 && cheated.PurchasedResearchCount == DeepTechnology.Nodes.Count && cheated.BuildingUnlocked("shield"), "cheat grants research without inventing waves or towers");
        var cheatedSave = cheated.Serialize(); var cheatReload = new DefenseState(); Check(cheatReload.Restore(cheatedSave), "explicit cheat research can save at wave0");
        Check(cheatReload.PurchasedResearchCount == cheated.PurchasedResearchCount && DataMap.Equivalent(cheatReload.TechEffects(), cheated.TechEffects()), "cheat reload does not double benefits");
        Check(cheatReload.Reset() && !cheatReload.Serialize().Map("research_flags").B("unrestricted_research"), "new run resets cheat gate exception");
    }
}
