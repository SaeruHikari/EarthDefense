using Earthward.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class ResearchContinuationChecks
{
    internal static void Run(Action<bool, string> check, DataMap golden)
    {
        void Check(bool value, string label) => check(value, "single research: " + label);
        void Near(double a, double b, string label) => Check(Math.Abs(a - b) < Math.Max(1e-9, Math.Abs(b) * 1e-10), label + $" {a} / {b}");
        DataMap Original(long count)
        {
            var save = golden.Map("samples").Map("full").Map("save").DeepClone();
            var research = save.Map("research_state"); research["version"] = 1L; research.Remove("successor_levels");
            var ranks = new DataMap(); if (count > 0) foreach (string branch in DeepTechnology.ChainBranches) ranks[branch + "_G2"] = count;
            research["refinements"] = ranks; return save;
        }
        var baseline = new DefenseState(); Check(baseline.Restore(Original(0)), "base research fixture");
        var baseEffects = baseline.TechEffects();
        string[] effects = { "kinetic_damage_bonus", "missile_damage_bonus", "laser_damage_bonus", "science_output_bonus", "health_bonus", "patrol_speed_bonus" };
        foreach (long rank in new[] { 1L, 17L, 10000L })
        {
            var source = Original(rank); string immutable = source.ToJson(); var game = new DefenseState();
            Check(game.Restore(source), "old purchased rank migrates " + rank);
            for (int i = 0; i < DeepTechnology.ChainBranches.Length; i++)
            {
                string branch = DeepTechnology.ChainBranches[i], id = DeepTechnology.SuccessorId(branch, (int)rank);
                Check(game.HasResearch(branch + "_R00001") && game.HasResearch(id) && game.ResearchLevel(id) == 1, "all previously purchased successors individually owned " + id);
                Check(rank == DeepTechnology.SuccessorMaxRank || !game.HasResearch(DeepTechnology.SuccessorId(branch, (int)rank + 1)), "no free next node " + branch);
                Near(game.TechEffects().N(effects[i]), baseEffects.N(effects[i]) + rank * .03, "legacy benefit counted exactly once " + branch);
                Check(!game.PurchaseGroup(id), "owned historical node cannot charge again " + id);
            }
            Check(game.PurchasedResearchCount == game.DeepResearch.Count + rank * 6, "compressed ownership has full logical count");
            var saved = game.Serialize(); var state = saved.Map("research_state");
            Check(state.I("version") == 3 && !state.ContainsKey("refinements") && state.Map("successor_levels").Count == 6, "canonical version3 continuous ownership");
            Check(saved.ToJson().Length < 250000 && state.Map("nodes").Keys.All(id => !id.Contains("_R")), "no60000 explicit archive entries");
            var again = new DefenseState(); Check(again.Restore(saved), "canonical successor save restores");
            Check(DataMap.Equivalent(saved, again.Serialize()), "canonical reload is idempotent");
            Check(source.ToJson() == immutable, "legacy input is read-only");
            var graph = game.GraphNodes(); Check(graph.Count <= DeepTechnology.Nodes.Count + 36, "high-rank graph stays bounded");
            var history = game.GraphNodes("K_R00005");
            Check(history.Any(node => node.S("id") == "K_R00005") || rank < 2, "any purchased history can be focused");
            var rows = history.Where(node => node.B("is_successor") && node.S("chain_branch") == "K").ToList();
            Check(rows.Count <= 6 && rows.All(node => node.ContainsKey("prev_page") && node.ContainsKey("next_page") && node.ContainsKey("chain_latest_id")), "history exposes navigation metadata");
            Check(graph.Where(node => node.B("is_successor")).All(node => node.I("chain_rank") <= Math.Min(rank + 3, 10000)), "default graph shows at most three future levels");
        }
        foreach (string invalid in new[] { "K_R00000", "K_R10001", "K_R1", "K_R000001", "X_R00001", "K_R-0001" })
            Check(!DeepTechnology.Has(invalid), "strict successor ID " + invalid);
        var definition = DeepTechnology.Definition("K_R00001");
        Check(definition.I("max") == 1 && definition.Map("values").Count == 1 && definition.S("size") == "medium" && definition.B("alien"), "one property and one purchase with correct paid category");
        Check(definition.List("requires").Single() as string == "K_G2" && DeepTechnology.Definition("K_R00002").List("requires").Single() as string == "K_R00001", "independent sequential prerequisites");
        Near(definition.Map("cost").N("science"), 1200, "old first cost science retained");
        Near(DeepTechnology.Definition("K_R00002").Map("cost").N("science"), 1464, "old second cost science retained");
        Near(DeepTechnology.Definition("K_R00002").Map("cost").N("alien_points"), 17, "old second cost alien retained");
        var maximum = DeepTechnology.Definition("K_R10000").Map("cost");
        Check(maximum.N("science") == DefenseState.ResourceLimit && maximum.L("alien_points") == DefenseState.MaxExactInteger, "original extreme-rank cost ceilings retained");
        var fresh = new DefenseState { Minerals = 1e9, Energy = 1e9, Science = 1e9, AlienPoints = 1000000 };
        Check(!fresh.PurchaseGroup("K_R00001"), "tail base is a real prerequisite");
        var funded = new DefenseState(); Check(funded.Restore(Original(0)), "ready chain fixture"); funded.Science = 1e12; funded.AlienPoints = 1000000;
        Check(!funded.PurchaseGroup("K_R00002"), "cannot skip a successor");
        var before = funded.Serialize();
        Check(funded.PurchaseGroup("K_R00001"), "first successor purchases once");
        Near(funded.Science, before.N("science") - 1200, "one science payment");
        Near(funded.AlienPoints, before.N("alien_points") - 12, "one alien payment");
        var paid = funded.Serialize(); Check(!funded.PurchaseGroup("K_R00001") && DataMap.Equivalent(paid, funded.Serialize()), "duplicate is a no-op with no charge or refund");
        Check(funded.PurchaseGroup("K_R00002") && funded.ResearchLevel("K_R00001") == 1 && funded.ResearchLevel("K_R00002") == 1, "second research is a distinct node");
        Check(!funded.GetGroupStatus("K_G2").ContainsKey("can_refine") && !funded.GetGroupStatus("K_G2").ContainsKey("refinement_level"), "repeat-purchase status removed");
        Check(typeof(DefenseState).GetMethod("RefineResearch") == null && !funded.Research("damage"), "retired repeat purchase APIs cannot bypass single nodes");
        foreach (string variant in new[] { "fractional", "too_many", "unknown_branch", "duplicate_node", "missing_tail", "both_formats" })
        {
            var stable = funded.Serialize(); var bad = stable.DeepClone(); var state = bad.Map("research_state");
            switch (variant)
            {
                case "fractional": state.Map("successor_levels")["K"] = 1.5; break;
                case "too_many": state.Map("successor_levels")["K"] = 10001L; break;
                case "unknown_branch": state.Map("successor_levels")["X"] = 1L; break;
                case "duplicate_node": state.Map("nodes")["K_R00001"] = 1L; break;
                case "missing_tail": state.Map("nodes").Remove("K_G2"); break;
                case "both_formats": state["refinements"] = new DataMap(); break;
            }
            Check(!funded.Restore(bad) && DataMap.Equivalent(stable, funded.Serialize()), "bad compressed archive refuses atomically " + variant);
        }
    }
}
