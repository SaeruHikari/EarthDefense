using Earthward;
using Earthward.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class WorldScaleChecks
{
    internal static void Run(Action<bool, string> check, DataMap golden, DataMap currentRetired)
    {
        void Check(bool value, string message) => check(value, "scale: " + message);
        void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < .00001, message + $" {actual} / {expected}");
        var model = new DefenseState();
        Check(WorldScale.EarthRadius == 16 && WorldScale.EarthRadiusDelta == 12, "one shared radius definition");
        foreach (var (key, value) in new[] { ("frontier_radius_1", 56d), ("frontier_radius_2", 88d), ("frontier_radius_3", 128d) })
            Near(DefenseState.DefaultCombatSettings.N(key), value, "current default " + key);
        var savedPrefs = model.SerializeCombatPreferences();
        Check(savedPrefs.I("world_scale_version") == 3, "preferences carry scale version");
        for (int i = 0; i < 3; i++)
        {
            var round = model.ReadCombatPreferences(DataMap.Parse(savedPrefs.ToJson()));
            Check(round != null && DataMap.Equivalent(round, model.CombatSettings), "preferences repeated reads do not drift");
        }
        foreach (object invalid in new object[] { 0, 4, 1.5, true, "2" })
        {
            var corrupt = savedPrefs.DeepClone(); corrupt["world_scale_version"] = invalid;
            Check(model.ReadCombatPreferences(corrupt) == null, "invalid preferences scale marker rejected " + invalid);
            var state = model.Serialize(); state["world_scale_version"] = invalid; var before = model.Serialize();
            Check(!model.Restore(state) && DataMap.Equivalent(before, model.Serialize()), "invalid game marker is atomic " + invalid);
        }
        foreach (var (id, radius) in new[] { ("C_G1", 24d), ("C_A2", 62d), ("C_A3", 96d), ("C_G2", 140d) })
            Near(DeepTechnology.Definition(id).Map("values").N("action_radius"), radius, "independent research action radius " + id);
        Near(new DefenseState().TechEffects().N("action_radius"), 0, "unresearched radius remains disabled");
        var archive = new ExpeditionData(); Check(archive.Restore(currentRetired), "current fleet restores");
        var exact = archive.Serialize(); Check(archive.Restore(exact) && DataMap.Equivalent(exact, archive.Serialize()), "fleet version4 roundtrip is idempotent");
        Check(archive.Fleet.I("version") == 4, "fleet version normalized to4");
        var largeSelection = AirframeCatalog.EmptySelection();
        var largeMeta = new FactoryPerks().Snapshot(); largeMeta["active_run"] = "earth-eight-fixture";
        var largeState = new DefenseState().Serialize(); largeState.Map("buildings")["mine"] = (long)WorldScale.DefaultGridCellCount;
        for (int i = 0; i < WorldScale.DefaultGridCellCount; i++)
        {
            string site = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            largeSelection.Map("sites")[site] = new DataMap { ["kind"] = "interceptor", ["airframe"] = "K1" };
            largeMeta.Map("sites")[site] = new DataMap { ["kind"] = "interceptor", ["slots"] = new List<object?> { "", "" } };
            largeState.Map("research_runtime").List("resource_boosts").Add(new DataMap { ["kind"] = "mine", ["site"] = (long)i, ["expires"] = 20d });
        }
        Check(AirframeCatalog.Validate(largeSelection) != null, "all40962 cells can retain site airframe selections");
        var largeProfile = new FactoryPerks(); Check(largeProfile.ImportSnapshot(largeMeta), "all40962 cells can retain site perk overrides");
        Check(new DefenseState().Restore(largeState), "all40962 valid resource boost records can restore");
        Check(FactoryPerks.MaxSites == 65536, "bounded archive budget accommodates enlarged grid");
        var runtime = new DataMap { ["version"] = 4, ["earth"] = new DataMap(), ["payload"] = new DataMap() };
        Check(archive.SetRuntimeSnapshot(runtime), "canonical active snapshot shape accepted");
        var withRuntime = archive.Serialize(); Check(archive.Restore(withRuntime), "canonical active snapshot can reload");
        Check(DataMap.Equivalent(archive.RuntimeSnapshot, runtime), "runtime snapshot remains an owned copy");
        var invalidRuntime = runtime.DeepClone(); invalidRuntime["version"] = 3L;
        var beforeRuntime = archive.Serialize();
        Check(!archive.SetRuntimeSnapshot(invalidRuntime) && DataMap.Equivalent(beforeRuntime, archive.Serialize()), "obsolete runtime schema is rejected atomically");
    }
}
