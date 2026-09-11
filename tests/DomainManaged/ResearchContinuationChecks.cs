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
        foreach (string invalid in new[] { "K_R00000", "K_R10001", "K_R1", "K_R000001", "X_R00001", "K_R-0001" })
            Check(!DeepTechnology.Has(invalid), "strict successor ID " + invalid);
        var definition = DeepTechnology.Definition("K_R00001");
        Check(definition.I("max") == 1 && definition.Map("values").Count == 1 && definition.S("size") == "medium" && definition.B("alien"), "one property and one purchase with correct paid category");
        Check(definition.List("requires").Single() as string == "K_G2" && DeepTechnology.Definition("K_R00002").List("requires").Single() as string == "K_R00001", "independent sequential prerequisites");
        Near(definition.Map("cost").N("science"), 1200, "first cost science");
        Near(DeepTechnology.Definition("K_R00002").Map("cost").N("science"), 1464, "second cost science");
        Near(DeepTechnology.Definition("K_R00002").Map("cost").N("alien_points"), 17, "second cost alien");
        var maximum = DeepTechnology.Definition("K_R10000").Map("cost");
        Check(maximum.N("science") == DefenseState.ResourceLimit && maximum.L("alien_points") == DefenseState.MaxExactInteger, "extreme-rank cost ceilings retained");
        var fresh = new DefenseState { Science = 1e9, AlienPoints = 1000000 };
        Check(!fresh.PurchaseGroup("K_R00001"), "tail base is a real prerequisite");
        var funded = new DefenseState { Science = 1e12, AlienPoints = 1000000 };
        Check(funded.UnlockAllTechnologyCheat(), "fixed tree fully owned for continuation access");
        var before = funded.Serialize();
        Check(funded.PurchaseGroup("K_R00001"), "first successor purchases once");
        Near(funded.Science, before.N("science") - 1200, "one science payment");
        Near(funded.AlienPoints, before.N("alien_points") - 12, "one alien payment");
        var paid = funded.Serialize(); Check(!funded.PurchaseGroup("K_R00001") && DataMap.Equivalent(paid, funded.Serialize()), "duplicate is a no-op with no charge or refund");
        Check(funded.PurchaseGroup("K_R00002") && funded.ResearchLevel("K_R00001") == 1 && funded.ResearchLevel("K_R00002") == 1, "second research is a distinct node");
        Check(!funded.GetGroupStatus("K_G2").ContainsKey("can_refine") && !funded.GetGroupStatus("K_G2").ContainsKey("refinement_level"), "repeat-purchase status removed");
        Check(typeof(DefenseState).GetMethod("RefineResearch") == null && !funded.Research("damage"), "retired repeat purchase APIs cannot bypass single nodes");
        var graph = funded.GraphNodes(); Check(graph.Count < DeepTechnology.Nodes.Count + 40, "continuation graph stays bounded");
        var history = funded.GraphNodes("K_R00001");
        Check(history.Any(node => node.S("id") == "K_R00001"), "purchased history can be focused");
        var rows = history.Where(node => node.B("is_successor") && node.S("chain_branch") == "K").ToList();
        Check(rows.Count <= 6 && rows.All(node => node.ContainsKey("prev_page") && node.ContainsKey("next_page") && node.ContainsKey("chain_latest_id")), "history exposes navigation metadata");
        var saved = funded.Serialize(); var state = saved.Map("research_state");
        Check(state.I("version") == 3 && !state.ContainsKey("refinements") && !state.ContainsKey("credited") && !state.ContainsKey("migration"), "canonical version3 research state");
        var again = new DefenseState(); Check(again.Restore(saved), "canonical successor save restores");
        Check(DataMap.Equivalent(saved, again.Serialize()), "canonical reload is idempotent");
        foreach (string variant in new[] { "fractional", "too_many", "unknown_branch", "duplicate_node", "missing_tail", "both_formats" })
        {
            var stable = funded.Serialize(); var bad = stable.DeepClone(); var st = bad.Map("research_state");
            switch (variant)
            {
                case "fractional": st.Map("successor_levels")["K"] = 1.5; break;
                case "too_many": st.Map("successor_levels")["K"] = 10001L; break;
                case "unknown_branch": st.Map("successor_levels")["X"] = 1L; break;
                case "duplicate_node": st.Map("nodes")["K_R00001"] = 1L; break;
                case "missing_tail": st.Map("nodes").Remove("K_G2"); break;
                case "both_formats": st["refinements"] = new DataMap(); break;
            }
            Check(!funded.Restore(bad) && DataMap.Equivalent(stable, funded.Serialize()), "bad compressed archive refuses atomically " + variant);
        }
    }
}
