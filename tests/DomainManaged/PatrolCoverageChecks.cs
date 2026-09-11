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
        var endgame = new DefenseState { Science = 1e12, AlienPoints = 1000000 };
        Check(endgame.UnlockAllTechnologyCheat(), "real endgame research fixture");
        endgame.SetCombatSetting(Key, 1); var endgameWeapons = endgame.DroneStats(); double action = endgame.PatrolStats("interceptor").N("action_radius");
        endgame.SetCombatSetting(Key, 8);
        Near(endgame.PatrolStats("interceptor").N("action_radius"), action, "fixed endgame action radius does not scale");
        Check(DataMap.Equivalent(endgameWeapons, endgame.DroneStats()), "endgame weapons and expedition fields unchanged");
        var preparedOuter = prepared.N("patrol_outer_range");
        Check(DataMap.Equivalent(equipped.FactoryPatrolStats("interceptor", 3), prepared), "repeated prepared patrol reads are stable");
        Near(preparedOuter, 2.25 * (1 + effects.N("patrol_outer_bonus")) * perk, "prepared outer range remains exact");
    }
}

