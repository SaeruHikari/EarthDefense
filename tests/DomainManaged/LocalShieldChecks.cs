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
        var opening = new DefenseState();
        Check(opening.PurchaseGroup("D_S01") && opening.PurchaseGroup("D_S21") && opening.PurchaseGroup("D_N4"), "new player can research local shield before wave1");
        Check(opening.Build("shield", 40) && opening.Wave == 0, "new player can construct first shield before attacks");
        Near(opening.Minerals, 200, "opening shield leaves factory minerals"); Near(opening.Energy, 80, "opening shield leaves factory energy"); Near(opening.Science, 48, "opening shield research spends only32 science");
        var stats = game.ShieldFacilityStats();
        Near(stats.N("capacity"), 100, "per-facility base capacity"); Near(stats.N("regeneration"), .8, "per-facility base regeneration");
        Near(stats.N("surface_radius"), 5, "world-space ground coverage"); Near(stats.N("altitude"), Earthward.WorldScale.ShieldAltitude, "shared shell altitude");
        Near(stats.N("break_recovery_fraction"), 0, "rebuild requires technology");
        game.Shield = 77; game.Tick(1); Near(game.Shield, 77, "legacy compatibility balance does not regenerate");
        game.TakeDamage(10); Near(game.EarthHp, 90, "no tower means direct Earth damage"); Near(game.Shield, 77, "old global balance cannot absorb damage");
        game.Minerals = game.Energy = game.Science = 10000; game.Wave = 7; game.CompletedWaves = 6;
        Check(!game.CanBuild("shield"), "funds alone do not unlock tower");
        foreach (string id in new[] { "D_S01", "D_S21", "D_S02", "D_S22", "D_N1", "D_N4" }) Check(game.PurchaseGroup(id), "reachable shield research " + id);
        Check(game.BuildingUnlocked("shield") && game.BuildingLockReason("shield") == "" && game.Buildings.L("shield") == 0, "research permits construction without gifting a tower");
        var cost = game.BuildingCost("shield"); Near(cost.N("minerals"), 120, "first tower mineral cost"); Near(cost.N("energy"), 100, "first tower energy cost");
        long cores = game.ResourceCores; Check(game.Build("shield", 50) && game.Buildings.L("shield") == 1 && game.ResourceCores == cores, "tower is an explicit ordinary build");
        var saved = game.Serialize(); var copy = new DefenseState(); Check(copy.Restore(saved) && copy.Buildings.L("shield") == 1, "tower ownership persists");
        var malformed = saved.DeepClone(); malformed.Map("research_state").Map("nodes").Remove("D_N4"); Check(!copy.Restore(malformed), "cannot restore a tower without its unlock");
        var poor = new DefenseState(); Check(poor.Restore(saved), "unlocked building test fixture"); poor.Minerals = poor.Energy = 0;
        Check(poor.BuildingUnlocked("shield") && !poor.CanBuild("shield"), "UI technology lock is distinct from insufficient funds");
        foreach (var row in golden.Map("samples").Values.OfType<DataMap>())
        {
            var source = row.Map("save"); var old = new DefenseState(); Check(old.Restore(source), "old save without shield building restores");
            Check(old.Buildings.L("shield") == 0, "old progress does not create tower");
            Near(old.Shield, source.N("shield"), "unused old balance is preserved for compatibility");
            Near(old.ShieldFacilityStats().N("capacity"), old.ShieldMax(), "all old shield investment feeds one facility");
        }
        var full = new DefenseState(); Check(full.Restore(golden.Map("samples").Map("full").Map("save")), "advanced shield technology fixture");
        var fullStats = full.ShieldFacilityStats(); Near(fullStats.N("break_recovery_fraction"), .25, "D_G2 rebuild fraction"); Near(fullStats.N("break_hold_seconds"), 2, "D_G2 local protection duration"); Near(fullStats.N("break_cooldown"), 30, "D_G2 local cooldown");
        full.Shield = 1000; full.EarthHp = 100; full.TakeDamage(30); Near(full.EarthHp, 70, "advanced shield research does not silently create global armor");
        var repair = new DefenseState { Minerals = 10000, Energy = 10000, Shield = 40 };
        Check(!repair.CanRepair() && !repair.Repair(), "no tower and undamaged Earth offers no shield-only repair");
        double missing = 100; int calls = 0; double requested = 0;
        repair.HasDamagedLocalShields = () => missing > 0;
        repair.RechargeLocalShields = budget => { calls++; requested += budget; double accepted = Math.Min(budget, missing); missing -= accepted; return accepted; };
        Check(repair.Repair(), "local damaged shield allows repair"); Near(requested, 40, "repair sends one fixed budget"); Near(missing, 60, "actual accepted local recharge"); Near(repair.Shield, 40, "local repair does not update legacy balance");
        repair.Wave = 1; repair.RewardWave(1); Near(requested, 60, "wave reward sends only20 total"); int previousCalls = calls; repair.RewardWave(1); Check(calls == previousCalls, "duplicate wave reward does not recharge twice");
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
