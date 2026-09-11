using Earthward;
using Earthward.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class WorldScaleChecks
{
    private static void OffsetShip(DataMap ship, double delta)
    {
        foreach (string key in new[] { "launch_origin", "launch_end", "position", "target" })
        {
            var old = ship.List(key).Select(value => DataMap.Number(value)).ToArray();
            double radius = Math.Sqrt(old.Sum(value => value * value));
            if (radius > 0) ship[key] = old.Select(value => (object?)(value * (1 + delta / radius))).ToList();
        }
        ship["clearance"] = ship.N("clearance") + delta;
    }
    internal static void Run(Action<bool, string> check, DataMap golden, DataMap currentRetired)
    {
        void Check(bool value, string message) => check(value, "scale: " + message);
        void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < .00001, message + $" {actual} / {expected}");
        var model = new DefenseState();
        Check(WorldScale.EarthRadius == 16 && WorldScale.EarthRadiusDelta == 12, "one shared radius definition");
        var old = golden.Map("samples").Values.OfType<DataMap>().First().Map("save").Map("combat_settings").DeepClone();
        old["frontier_radius_1"] = 44d; old["frontier_radius_2"] = 76d; old["frontier_radius_3"] = 116d;
        string sourceJson = old.ToJson();
        var shifted = model.ReadCombatPreferences(old);
        Check(shifted != null, "flat legacy preferences migrate");
        foreach (var (key, value) in new[] { ("frontier_radius_1", 56d), ("frontier_radius_2", 88d), ("frontier_radius_3", 128d) })
        {
            Near(DefenseState.DefaultCombatSettings.N(key), value, "current default " + key);
            Near(shifted!.N(key), value, "legacy offset " + key);
        }
        Check(sourceJson == old.ToJson(), "reading does not mutate source preferences");
        Check(model.ApplyCombatSettings(shifted!), "apply migrated options");
        var savedPrefs = model.SerializeCombatPreferences();
        Check(savedPrefs.I("world_scale_version") == 3, "preferences carry scale version");
        for (int i = 0; i < 3; i++)
        {
            var round = model.ReadCombatPreferences(DataMap.Parse(savedPrefs.ToJson()));
            Check(round != null && DataMap.Equivalent(round, shifted), "preferences repeated reads do not drift");
        }
        foreach (var (key, value) in old)
            if (!key.StartsWith("frontier_radius_", StringComparison.Ordinal))
                Near(shifted!.N(key), DataMap.Number(value), "unchanged user option " + key);
        var custom = old.DeepClone(); custom["frontier_radius_1"] = 25d; custom["frontier_radius_2"] = 95d; custom["frontier_radius_3"] = 220d;
        var customResult = model.ReadCombatPreferences(custom)!;
        Near(customResult.N("frontier_radius_1"), 37, "custom first shell preserves height");
        Near(customResult.N("frontier_radius_2"), 107, "custom second shell preserves height");
        Near(customResult.N("frontier_radius_3"), 232, "custom third shell preserves height");
        var partial = old.DeepClone(); foreach (string key in new[] { "frontier_radius_1", "frontier_radius_2", "frontier_radius_3" }) partial.Remove(key);
        var inherited = model.ReadCombatPreferences(partial)!;
        Near(inherited.N("frontier_radius_1"), 56, "absent legacy option uses new default once");
        foreach (double invalid in new[] { -1d, 19d, 301d, double.NaN, double.PositiveInfinity })
        {
            var corrupt = old.DeepClone(); corrupt["frontier_radius_1"] = invalid;
            Check(model.ReadCombatPreferences(corrupt) == null, "invalid old frontier rejected " + invalid);
        }
        var high = old.DeepClone(); high["frontier_radius_1"] = 300d; high["frontier_radius_2"] = 400d; high["frontier_radius_3"] = 500d;
        var highest = model.ReadCombatPreferences(high)!;
        Near(highest.N("frontier_radius_1"), 312, "old maximum maps to new maximum");
        foreach (object invalid in new object[] { 0, 4, 1.5, true, "2" })
        {
            var corrupt = savedPrefs.DeepClone(); corrupt["world_scale_version"] = invalid;
            Check(model.ReadCombatPreferences(corrupt) == null, "invalid preferences scale marker rejected " + invalid);
            var state = model.Serialize(); state["world_scale_version"] = invalid; var before = model.Serialize();
            Check(!model.Restore(state) && DataMap.Equivalent(before, model.Serialize()), "invalid game marker is atomic " + invalid);
        }
        foreach (var row in golden.Map("samples").Values.OfType<DataMap>())
        {
            var original = row.Map("save"); string raw = original.ToJson(); var migrated = new DefenseState();
            Check(migrated.Restore(original), "real historical state restores");
            Near(migrated.CombatSettings.N("frontier_radius_1"), original.Map("combat_settings").N("frontier_radius_1", 44) + 12, "old state frontier carries height");
            var now = migrated.Serialize(); Check(now.I("world_scale_version") == 3, "state marks current scale");
            var reloaded = new DefenseState(); Check(reloaded.Restore(now), "new state restores");
            Check(DataMap.Equivalent(now, reloaded.Serialize()), "state roundtrip no repeated scale or rights migration");
            Check(raw == original.ToJson(), "protected data tree not changed by migration");
        }
        foreach (var (id, radius) in new[] { ("C_G1", 24d), ("C_A2", 62d), ("C_A3", 96d), ("C_G2", 140d) })
            Near(DeepTechnology.Definition(id).Map("values").N("action_radius"), radius, "independent research action radius " + id);
        Near(new DefenseState().TechEffects().N("action_radius"), 0, "unresearched radius remains disabled");
        var oldRetired = currentRetired.DeepClone(); oldRetired.Map("fleet")["version"] = 2L;
        var oldShip = oldRetired.Map("fleet").List("ships").OfType<DataMap>().Single();
        OffsetShip(oldShip, -WorldScale.EarthRadiusDelta);
        var archive = new ExpeditionData(); Check(archive.Restore(oldRetired), "legacy fleet restores");
        var newShip = archive.Fleet.List("ships").OfType<DataMap>().Single();
        foreach (string key in new[] { "launch_origin", "launch_end", "position", "target" })
        {
            var a = oldShip.Vector3(key); var b = newShip.Vector3(key);
            Near(b.Length() - a.Length(), 12, "dormant fleet position keeps altitude " + key);
            Check(a.Normalized().DistanceTo(b.Normalized()) < .000001f, "dormant fleet direction retained " + key);
        }
        foreach (string key in new[] { "launch_normal", "launch_up", "forward", "up", "hull_length", "launch_seconds", "transit_seconds", "uid", "silo_id", "slot", "age" })
            Check(DataMap.Equivalent(newShip.Value(key), oldShip.Value(key)), "fleet physical scalar/identity unchanged " + key);
        Near(newShip.N("clearance"), oldShip.N("clearance") + 12, "fleet geocentric clearance lifted");
        Check(DataMap.Equivalent(archive.Fleet.List("orders"), oldRetired.Map("fleet").List("orders")), "paid fleet orders retained");
        var exact = archive.Serialize(); Check(archive.Restore(exact) && DataMap.Equivalent(exact, archive.Serialize()), "retired fleet version4 no double migration");
        var legacyV1 = oldRetired.DeepClone(); legacyV1.Map("fleet")["version"] = 1L;
        Check(archive.Restore(legacyV1), "historical fleet version1 runs both migrations");
        Check(archive.Fleet.I("version") == 4, "all fleet versions normalize to4");
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
        foreach (var (version, radius) in new[] { (1, 4d), (2, 8d), (3, 16d) })
        {
            Near(DefenseState.RadiusForWorldScaleVersion(version), radius, "historical world source radius");
            double delta = WorldScale.EarthRadius - radius;
            var settings = model.CombatSettings.DeepClone();
            foreach (string key in new[] { "frontier_radius_1", "frontier_radius_2", "frontier_radius_3" }) settings[key] = settings.N(key) - delta;
            var envelope = new DataMap { ["world_scale_version"] = version, ["settings"] = settings };
            Check(DataMap.Equivalent(model.ReadCombatPreferences(envelope), model.CombatSettings), "R" + radius + " preference migration reaches identical current distances");
            var gameSave = model.Serialize(); gameSave["world_scale_version"] = version; gameSave["combat_settings"] = settings;
            var restored = new DefenseState(); Check(restored.Restore(gameSave), "R" + radius + " complete save restores");
            Check(DataMap.Equivalent(restored.CombatSettings, model.CombatSettings), "complete save uses source-specific offset");
            Check(restored.Serialize().I("world_scale_version") == 3, "all complete saves normalize scale3");
            var fleetSave = currentRetired.DeepClone(); fleetSave.Map("fleet")["version"] = version + 1;
            var sourceShip = fleetSave.Map("fleet").List("ships").OfType<DataMap>().Single(); OffsetShip(sourceShip, -delta);
            Check(archive.Restore(fleetSave), "R" + radius + " retired fleet migration");
            var destination = archive.Fleet.List("ships").OfType<DataMap>().Single();
            Near(destination.N("clearance"), sourceShip.N("clearance") + delta, "fleet source-specific clearance offset");
            foreach (string key in new[] { "launch_origin", "launch_end", "position", "target" })
                Near(destination.Vector3(key).Length() - sourceShip.Vector3(key).Length(), delta, "fleet source-specific position offset " + key);
            var once = archive.Serialize(); Check(archive.Restore(once) && DataMap.Equivalent(once, archive.Serialize()), "R" + radius + " migration remains idempotent");
        }
        // Domain passes battle snapshots to the sole battle-owned normalizer; it never transforms their fields.
        var previousValidator = ExpeditionData.RuntimeSnapshotValidator; var previousMigrator = ExpeditionData.RuntimeSnapshotMigrator;
        try
        {
            ExpeditionData.RuntimeSnapshotValidator = value => value.I("version") == 4 && value.Count == 3;
            int calls = 0;
            ExpeditionData.RuntimeSnapshotMigrator = value => { calls++; if (value.Map("payload").I("scale", 1) == 1) { value.Map("payload")["radius"] = value.Map("payload").N("radius") + 4; value.Map("payload")["scale"] = 2; } return value; };
            var runtime = new DataMap { ["version"] = 4, ["earth"] = new DataMap(), ["payload"] = new DataMap { ["radius"] = 4d } };
            Check(archive.SetRuntimeSnapshot(runtime), "nested active snapshot delegates normalization");
            Near(archive.RuntimeSnapshot.Map("payload").N("radius"), 8, "only delegated active radius changed");
            var withRuntime = archive.Serialize(); Check(archive.Restore(withRuntime), "nested active snapshot can reload");
            Near(archive.RuntimeSnapshot.Map("payload").N("radius"), 8, "active normalization remains idempotent");
            Check(calls == 2 && !runtime.Map("payload").ContainsKey("scale"), "normalizer owns copy, source remains unchanged");
            var before = archive.Serialize(); ExpeditionData.RuntimeSnapshotMigrator = _ => null;
            Check(!archive.Restore(before) && DataMap.Equivalent(before, archive.Serialize()), "failed active migration aborts archive restore");
            Check(!archive.SetRuntimeSnapshot(runtime) && DataMap.Equivalent(before, archive.Serialize()), "failed active migration aborts setter");
        }
        finally { ExpeditionData.RuntimeSnapshotValidator = previousValidator; ExpeditionData.RuntimeSnapshotMigrator = previousMigrator; }
    }
}
