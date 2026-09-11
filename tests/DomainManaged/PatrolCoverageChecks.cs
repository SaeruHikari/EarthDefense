using Earthward.Domain;
using System;
using System.Linq;
using System.Collections.Generic;

internal static class PatrolCoverageChecks
{
    private const string Key = "patrol_coverage_multiplier";
    internal static void Run(Action<bool, string> check, DataMap golden)
    {
        void Check(bool value, string label) => check(value, "coverage: " + label);
        void Near(double actual, double expected, string label) => Check(Math.Abs(actual - expected) < Math.Max(1e-9, Math.Abs(expected) * 1e-9), label + $" {actual} / {expected}");
        var game = new DefenseState();
        Near(game.CombatSettings.N(Key), 2, "new run defaults to double coverage");
        var bounds = game.CombatSettingBounds(Key); Check(bounds.X == .25f && bounds.Y == 8, "editing bounds");
        Check(!DefenseState.IntegerCombatSettings.Contains(Key), "fractional settings supported");
        foreach (var (kind, radius, outer, speed) in new[] { ("interceptor", 3.75, 2.25, .95), ("laser", 4.8, 2.7, .78), ("missile", 5.7, 3.0, .68) })
        {
            var patrol = game.PatrolStats(kind);
            Near(patrol.N("patrol_radius"), radius, kind + " default base radius");
            Near(patrol.N("patrol_outer_range"), outer, kind + " default base outer range");
            Near(patrol.N("patrol_speed"), speed, kind + " unchanged speed");
            Near(patrol.N("action_radius"), 0, kind + " no free expedition range");
        }
        foreach (double value in new[] { .25, 1, 2, 3.5, 8 })
        {
            Check(game.SetCombatSetting(Key, value), "valid multiplier " + value);
            var settingSave = game.SerializeCombatPreferences();
            Near(game.ReadCombatPreferences(settingSave)!.N(Key), value, "custom preferences preserve value");
            var copy = new DefenseState(); Check(copy.Restore(game.Serialize()), "custom complete checkpoint restores");
            Near(copy.CombatSettings.N(Key), value, "custom checkpoint preserves value");
            Near(copy.PatrolStats("interceptor").N("patrol_radius"), 1.875 * value, "base radius follows fractional multiplier");
        }
        foreach (double invalid in new[] { -.1, 0, .249, 8.001, double.NaN, double.PositiveInfinity })
        {
            var before = game.Serialize(); long revision = game.FactoryStatsRevision;
            Check(!game.SetCombatSetting(Key, invalid), "reject out of range multiplier " + invalid);
            Check(revision == game.FactoryStatsRevision && DataMap.Equivalent(before, game.Serialize()), "bad option preserves active game and cache revision");
        }
        foreach (var sample in golden.Map("samples").Values.OfType<DataMap>())
        {
            var old = sample.Map("save"); string source = old.ToJson();
            var migrated = new DefenseState(); Check(migrated.Restore(old), "historical checkpoint without option restores");
            Near(migrated.CombatSettings.N(Key), 2, "historical checkpoint receives new default");
            Check(source == old.ToJson(), "migration leaves historical input unchanged");
            var prefs = old.Map("combat_settings").DeepClone();
            Near(migrated.ReadCombatPreferences(prefs)!.N(Key), 2, "flat historical preferences receive default");
        }
        foreach (int version in new[] { 1, 2, 3 })
        {
            var settings = DefenseState.DefaultCombatSettings; settings.Remove(Key);
            double delta = Earthward.WorldScale.EarthRadius - DefenseState.RadiusForWorldScaleVersion(version);
            foreach (string radius in new[] { "frontier_radius_1", "frontier_radius_2", "frontier_radius_3" }) settings[radius] = settings.N(radius) - delta;
            var prefs = new DataMap { ["world_scale_version"] = version, ["settings"] = settings };
            Near(game.ReadCombatPreferences(prefs)!.N(Key), 2, "versioned historical preferences inherit2 at world version" + version);
        }
        // Compare the nine physical hulls, including warm caches and primed factory stats.
        foreach (var frame in AirframeCatalog.Definitions)
        {
            string kind = frame.S("kind"), id = frame.S("id");
            game.SetCombatSetting(Key, 1);
            var primed = new DataMap(); foreach (string factoryKind in PerkCatalog.Kinds) primed[factoryKind] = game.PatrolStats(factoryKind);
            game.PrimeFactoryBaseStats(game.DroneStats(), primed);
            var oldPatrol = game.AircraftPatrolStats(kind, 18, 0, id);
            var oldWeapon = game.AircraftDroneStats(kind, 18, 0, id).DeepClone();
            long revision = game.FactoryStatsRevision; int changes = 0; Action onChange = () => changes++; game.Changed += onChange;
            Check(game.SetCombatSetting(Key, 2), "live edit accepted for physical " + id);
            game.Changed -= onChange;
            Check(game.FactoryStatsRevision > revision && changes == 1, "live edit signals and invalidates factory revision " + id);
            var current = game.AircraftPatrolStats(kind, 18, 0, id);
            Check(!ReferenceEquals(oldPatrol, current), "warm physical cache replaced " + id);
            Near(current.N("patrol_radius"), oldPatrol.N("patrol_radius") * 2, "primed radius refreshes " + id);
            Near(current.N("patrol_outer_range"), oldPatrol.N("patrol_outer_range") * 2, "primed outer range refreshes " + id);
            foreach (string field in new[] { "patrol_speed", "action_radius", "combat_turn_rate", "return_speed_multiplier", "health", "factory_interval_multiplier" })
                Near(current.N(field), oldPatrol.N(field), "unrelated flight field unchanged " + id + "/" + field);
            Check(DataMap.Equivalent(oldWeapon, game.AircraftDroneStats(kind, 18, 0, id)), "all damage/range/projectile/physical weapon fields unchanged " + id);
        }
        // Tech and navigation Perk still compose after the configurable base.
        var equipped = new DefenseState();
        Check(equipped.PurchaseGroup("C_S01") && equipped.PurchaseGroup("C_S02"), "real coverage research purchases");
        var meta = equipped.FactoryPerks.Snapshot(); meta.Map("levels")["navigation"] = 3L;
        meta.Map("templates")["interceptor"] = new List<object?> { "navigation", "" };
        Check(equipped.FactoryPerks.ImportSnapshot(meta), "equip valid navigation template");
        double perk = PerkCatalog.Effects("navigation", 3, PerkCatalog.DefaultSettings).N("patrol_multiplier");
        var effects = equipped.TechEffects(); var prepared = equipped.FactoryPatrolStats("interceptor", 3);
        Near(prepared.N("patrol_radius"), 3.75 * (1 + effects.N("patrol_radius_bonus")) * perk, "new base then research then navigation Perk");
        Near(prepared.N("patrol_outer_range"), 2.25 * (1 + effects.N("patrol_outer_bonus")) * perk, "new outer base then research then Perk");
        var endgame = new DefenseState(); Check(endgame.Restore(golden.Map("samples").Map("full").Map("save")), "real endgame research fixture");
        endgame.SetCombatSetting(Key, 1); var endgameWeapons = endgame.DroneStats(); double action = endgame.PatrolStats("interceptor").N("action_radius");
        endgame.SetCombatSetting(Key, 8);
        Near(endgame.PatrolStats("interceptor").N("action_radius"), action, "fixed endgame action radius does not scale");
        Check(DataMap.Equivalent(endgameWeapons, endgame.DroneStats()), "endgame weapons and expedition fields unchanged");
        var legacy = new DefenseState(); Check(legacy.Restore(golden.Map("legacy_raw")), "actual old125 investment fixture");
        foreach (int stage in new[] { 0, 1, 2, 3 })
        {
            // Exercise the retained legacy additive terms independently of their purchase UI.
            for (int index = 1; index <= 3; index++) legacy.Tech["frontier_range_" + index] = stage >= index ? 1L : 0L;
            legacy.SetCombatSetting(Key, 1); var first = legacy.PatrolStats("interceptor"); var fx = legacy.TechEffects();
            legacy.SetCombatSetting(Key, 2); var second = legacy.PatrolStats("interceptor");
            double fixedRadius = 2 * (1 + fx.N("patrol_radius_bonus"));
            double fixedOuter = (6 + new[] { 0, 24, 48, 80 }[stage]) * (1 + fx.N("patrol_outer_bonus"));
            Near(second.N("patrol_radius"), first.N("patrol_radius") * 2 - fixedRadius, "assault additive preserved at stage" + stage);
            Near(second.N("patrol_outer_range"), first.N("patrol_outer_range") * 2 - fixedOuter, "frontier additive preserved at stage" + stage);
            Near(second.N("action_radius"), first.N("action_radius"), "legacy fixed action radius unchanged at stage" + stage);
        }
    }
}
