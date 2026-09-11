using Earthward.Domain;
using System;
using System.Linq;
using System.Collections.Generic;

internal static class MissileResearchChecks
{
    internal static void Run(Action<bool, string> check, DataMap golden)
    {
        void Check(bool value, string label) => check(value, "missile research: " + label);
        void Near(double a, double b, string label) => Check(Math.Abs(a - b) < 1e-9, label + $" {a} / {b}");
        DefenseState Ready()
        {
            var game = new DefenseState { Minerals = 10000, Energy = 10000, Science = 10000, Wave = 9, CompletedWaves = 8 };
            Check(game.PurchaseGroup("K_S01") && game.PurchaseGroup("K_S21") && game.PurchaseGroup("M_N1"), "normal missile unlock path");
            return game;
        }
        var ids = new[] { "M_N4", "M_S21", "M_S22", "M_S23" };
        Check(DeepTechnology.Nodes.Count == 299, "299 distinct static nodes");
        Check(DeepTechnology.Nodes.Select(row => row.S("id")).Distinct().Count() == 299, "all node IDs unique");
        foreach (var row in DeepTechnology.Nodes.Where(row => row.S("size") == "small"))
            Check(row.Map("cost").Keys.All(key => key == "science") && row.Map("values").Count == 1 && row.I("max") == 1, "small node single property and science-only " + row.S("id"));
        foreach (string id in ids)
        {
            var row = DeepTechnology.Definition(id);
            Check(row.I("max") == 1 && row.Map("values").Count == 1 && !row.B("alien") && row.Map("cost").L("alien_points") == 0, "one-time regular node " + id);
            Check(row.List("requires").Count > 0 && row.List("requires").Cast<string>().All(DeepTechnology.Has), "reachable prerequisite IDs " + id);
            Check(row.List("draw_position").Count == 2, "explicit roomy branch placement " + id);
            var initial = new DefenseState { Minerals = 10000, Energy = 10000, Science = 10000, Wave = 9, CompletedWaves = 8 };
            Check(!initial.PurchaseGroup(id), "prerequisites cannot be bypassed " + id);
        }
        var extra = Ready(); Check(extra.PurchaseGroup("M_S02") && extra.PurchaseGroup("M_S25"), "salvo prerequisite");
        extra.CompletedWaves = 7; Check(!extra.PurchaseGroup("M_N4"), "salvo progression gate before completed8"); extra.CompletedWaves = 8;
        var before = extra.Serialize(); var beforeStats = extra.DroneStats();
        Check(extra.PurchaseGroup("M_N4"), "normal mid research adds one missile");
        var stats = extra.DroneStats();
        Near(stats.N("missile_salvo"), beforeStats.N("missile_salvo"), "existing salvo rights untouched");
        Near(stats.N("missile_extra_projectiles"), 1, "new explicit bonus reaches live stats");
        Near(stats.N("missile_salvo") + stats.N("missile_extra_projectiles"), 2, "initial single launcher becomes double");
        Check(stats.Map("tech_abilities").B("M_N4"), "new medium ability included in immutable source flags");
        Check(extra.Minerals == before.N("minerals") - 80 && extra.Energy == before.N("energy") - 60 && extra.Science == before.N("science") - 140 && extra.AlienPoints == 0, "exact mid research cost without alien points");
        var paid = extra.Serialize(); Check(!extra.PurchaseGroup("M_N4") && DataMap.Equivalent(paid, extra.Serialize()), "second purchase cannot spend again");
        foreach (string frame in new[] { "M1", "M2", "M3" }) Near(extra.AircraftDroneStats("missile", 3, 0, frame).N("missile_extra_projectiles"), 1, "all physical missile hulls receive launch bonus " + frame);
        foreach (var (id, parent, effect, field, increase) in new[] {
            ("M_S21", "M_S03", "missile_blast_radius_bonus", "missile_blast_radius", .15),
            ("M_S22", "M_N1", "missile_range_bonus", "missile_range_multiplier", .15),
            ("M_S23", "M_S05", "missile_speed_bonus", "missile_speed_multiplier", .20) })
        {
            var game = Ready(); if (!game.HasResearch(parent)) Check(game.PurchaseGroup(parent), "small prerequisite " + parent); if(parent.Contains("_S")) Check(game.PurchaseGroup(CatalogData.Load("technology-migration-127.json").Map("pairs").S(parent)), "prerequisite second half");
            game.CompletedWaves = 5; Check(!game.PurchaseGroup(id), "small branch gate before completed6"); game.CompletedWaves = 6;
            var previous = game.DroneStats(); double half = increase / 2; double bonus = game.TechEffects().N(effect), science = game.Science, minerals = game.Minerals, energy = game.Energy; int nodeCount = game.DeepResearch.Count;
            Check(game.PurchaseGroup(id), "new small single purchase " + id);
            Near(game.TechEffects().N(effect) - bonus, half, "specific additive technology bonus " + id);
            Near(game.DroneStats().N(field), previous.N(field) * (1 + bonus + half) / (1 + bonus), "real compiled weapon property " + id);
            Check(game.DeepResearch.Count == nodeCount + 1 && game.Science == science - DeepTechnology.Definition(id).Map("cost").N("science") && game.Minerals == minerals && game.Energy == energy, "one node science-only transaction " + id);
            foreach (string untouched in new[] { "missile_damage", "missile_fire_rate", "missile_turn_rate", "missile_extra_projectiles", "damage", "laser_damage" }) Near(game.DroneStats().N(untouched), previous.N(untouched), "unrelated weapon field unchanged " + id + "/" + untouched);
            var once = game.Serialize(); Check(!game.PurchaseGroup(id) && DataMap.Equivalent(once, game.Serialize()), "small cannot be bought twice " + id);
            Check(game.PurchaseGroup(CatalogData.Load("technology-migration-127.json").Map("pairs").S(id)), "second half is independently purchased"); Near(game.TechEffects().N(effect)-bonus,increase,"two nodes preserve old total");
        }
        foreach (var row in golden.Map("samples").Values.OfType<DataMap>())
        {
            var legacy = new DefenseState(); Check(legacy.Restore(row.Map("save")), "previous168 state accepted");
            Check(ids.All(id => !legacy.HasResearch(id)), "old rights do not grant new nodes for free");
            Near(legacy.DroneStats().N("missile_extra_projectiles"), 0, "old missile salvo unchanged without research");
        }
    }
}
