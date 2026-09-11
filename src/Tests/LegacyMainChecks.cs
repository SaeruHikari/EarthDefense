using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Earthward.Domain;
using Earthward.Combat;
using ManagedMain = Earthward.Application.Main;
namespace Earthward.Tests;

/// <summary>Real C# application load/tick/save/load, with a physically isolated user profile.</summary>
public partial class LegacyMainChecks : Node
{
    private const string Original = "res://saves/backups/20260910-003354-827-pre-expedition-design/profile-01/capture-01/earthward_checkpoint.json";
    private int _checks;
    private readonly List<string> _failures = new();
    private ManagedMain? _app;
    private void Check(bool valid, string label)
    {
        _checks++;
        if (!valid)
        {
            _failures.Add(label);
            GD.PrintErr("MANAGED_LEGACY_FAIL: " + label);
        }
    }
    private void Near(double actual, double expected, string label, double tolerance = .00001) => Check(Math.Abs(actual - expected) <= Math.Max(tolerance, Math.Abs(expected) * .0000001), $"{label}: {actual} / {expected}");
    private static void WriteText(string path, string text)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
            throw new IOException("Unable to open isolated test file: " + path);
        file.StoreString(text);
        file.Flush();
    }
    public override async void _Ready()
    {
        try
        {
            if (!OS.GetUserDataDir().Replace('\\', '/').Contains("/.runtime-tests/", StringComparison.Ordinal))
                throw new InvalidOperationException("LegacyMainChecks requires a .runtime-tests isolated profile");
            GetWindow().Size = new Vector2I(1440, 900);
            GetWindow().Position = new Vector2I(-1700, 100);
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            string raw = Godot.FileAccess.GetFileAsString(Original), hash = FactoryPerks.Hash(raw);
            var original = DataMap.Parse(raw);
            Check(original.Map("game").L("wave") == 125 && original.List("destroyed_fronts").Count == 8, "protected fixture identity is completed125");
            WriteText(ManagedMain.SavePath, raw);
            // The protected completion predates perks; this separately identified old
            // profile proves a run checkpoint cannot erase existing permanent investment.
            var profile = new FactoryPerks().Snapshot();
            profile["version"] = 2L;
            profile["energy_cores"] = 17L;
            profile.Remove("known_intel");
            foreach (var row in PerkCatalog.Entries.Skip(27))
                profile.Map("levels").Remove(row.S("id"));
            profile.Map("levels")["armor"] = 3L;
            profile.Map("levels")["targeting"] = 2L;
            profile.Map("levels")["a_kinetic_core"] = 4L;
            profile.Map("templates")["interceptor"] = new List<object?> { "armor", "targeting" };
            profile.Map("aircraft_templates")["interceptor"] = new List<object?> { "a_kinetic_core", "" };
            WriteText("user://earthward_factory_perks.json", profile.ToJson());
            _app = new ManagedMain();
            AddChild(_app);
            _app.SetProcess(false);
            _app.Planet.SetProcess(false);
            if (_app.Sounds != null)
                _app.Sounds.Muted = true;
            Check(NoLegacyScripts(_app), "live Main/planet/UI tree contains no GDScript instance");
            Check(_app.ValidateCheckpoint(original), "real application validates protected old envelope");
            Check(_app.LoadCheckpoint(), "real C# Main loads original125");
            if (_app.Game.Wave != 125)
                throw new InvalidOperationException("Load did not commit protected wave125; stopping dependent checks");
            foreach (var (key, value) in original.Map("game").Map("tech"))
                Near(_app.Game.Tech.N(key), DataMap.Number(value), "old paid technology retained " + key);
            foreach (var (key, value) in original.Map("game").Map("buildings"))
                Near(_app.Game.Buildings.N(key), DataMap.Number(value), "old factory count retained " + key);
            foreach (string key in new[] { "minerals", "energy", "science", "earth_hp", "shield", "alien_points", "resource_cores" })
                Near(_app.Game.Serialize().N(key), original.Map("game").N(key), "initial wallet retained " + key);
            Check(DataMap.Equivalent(_app.Planet.GetSlots(), original.List("slots")), "all surface site identities retained");
            var directions = _app.Planet.GetSiteDirections();
            Check(directions.Count == original.List("site_directions").Count, "all direction records retained");
            for (int i = 0; i < directions.Count; i++)
            {
                var a = new DataMap { ["v"] = directions[i] }.Vector3("v");
                var b = new DataMap { ["v"] = original.List("site_directions")[i] }.Vector3("v");
                Check(a.DistanceTo(b) < .000005f, "site direction retained " + i);
            }
            Check(_app.Battle.GetDestroyedFronts().SequenceEqual(original.List("destroyed_fronts").Cast<string>()), "eight destroyed mothers remain defeated");
            if (original.ContainsKey("invasion_anchor"))
                Check(_app.Battle.GetInvasionAnchor().DistanceTo(original.Vector3("invasion_anchor")) < .00001, "original invasion coordinate frame retained");
            var celestial = _app.Planet.CaptureCelestialState();
            foreach (var (key, value) in original.Map("celestial"))
                Near(celestial.N(key), DataMap.Number(value), "celestial clock/rotation retained " + key);
            Check(_app.Game.FactoryPerks.GetLevel("armor") == 3 && _app.Game.FactoryPerks.GetLevel("targeting") == 2 && _app.Game.FactoryPerks.GetLevel("a_kinetic_core") == 4, "permanent levels survive realMain restore");
            Check(_app.Game.FactoryPerks.EnergyCores == 17 && _app.Game.FactoryPerks.SlotsFor("interceptor").SequenceEqual(new[] { "armor", "targeting" }), "permanent currency/templates survive");
            Check(_app.Game.HasResearch("C_G1") && _app.Game.HasResearch("L_N1"), "paid legacy research rights visible in independenttree");
            Check(_app.Game.DefenseReachStage == 1 && _app.Campaign.OwnsEarthSchedule(), "cleared old defense enters outercampaign director");
            Near(_app.Campaign.GetStatus().N("ceasefire_duration"), 225, "five45second cycles prepare outerdefense");
            double beforeTime = _app.Game.DefenseTime, beforeRemaining = _app.Campaign.GetStatus().N("ceasefire_remaining");
            var beforeResources = _app.Game.Serialize();
            var rates = _app.Game.Rates();
            _app.Modal = "";
            _app.UserPaused = false;
            _app.Speed = 1;
            for (int frame = 0; frame < 120; frame++)
            {
                _app.Planet._Process(1d / 30);
                _app._Process(1d / 30);
            }
            Near(_app.Game.DefenseTime, beforeTime + 4, "realMain advances gamefour seconds");
            Near(_app.Campaign.GetStatus().N("ceasefire_remaining"), beforeRemaining - 4, "director counts down instead of freezing completedcampaign");
            foreach (string key in new[] { "minerals", "energy", "science" })
                Near(_app.Game.Serialize().N(key), beforeResources.N(key) + rates.N(key) * 4, "realMain running income " + key);
            Check(_app.Battle.Drones.Count > 0 && _app.Battle.Active, "real managed factory fleet runs");
            Check(_app.Battle.Enemies.Count == 0 && !_app.Battle.WaveRunning, "preparation keeps old attackwave stopped");
            var savedCelestial = _app.Planet.CaptureCelestialState();
            double remaining = _app.Campaign.GetStatus().N("ceasefire_remaining"), clock = _app.Battle.Clock;
            var ids = _app.Battle.Drones.Select(row => row.L("uid")).Order().ToList();
            Check(_app.SaveCheckpoint(), "real C# Main atomic save succeeds");
            var saved = DataMap.Parse(Godot.FileAccess.GetFileAsString(ManagedMain.SavePath));
            foreach (string key in new[] { "game", "slots", "site_directions", "invasion_anchor", "destroyed_fronts", "expedition_battle", "celestial" })
                Check(saved.ContainsKey(key), "checkpoint contains required component " + key);
            Check(_app.ValidateCheckpoint(saved), "new C# checkpoint validates");
            foreach (string field in new[] { "destroyed_fronts", "site_directions", "invasion_anchor", "expedition_battle", "celestial", "combat_snapshot" })
            {
                var broken = original.DeepClone();
                broken[field] = "incorrect component type";
                Check(!_app.ValidateCheckpoint(broken), "optional legacy component cannot silently fallback: " + field);
            }
            var brokenV3 = saved.DeepClone();
            brokenV3["destroyed_fronts"] = "wrong ledger type";
            Check(!_app.ValidateCheckpoint(brokenV3), "version3 front ledger type must not silently become empty");
            Check(_app.LoadCheckpoint(), "real C# Main reloads continued save");
            Near(_app.Campaign.GetStatus().N("ceasefire_remaining"), remaining, "reload preserves remaining preparationtime");
            Near(_app.Battle.Clock, clock, "reload preserves battlefield clock");
            foreach (var (key, value) in savedCelestial)
                Near(_app.Planet.CaptureCelestialState().N(key), DataMap.Number(value), "continued celestial state remains exact " + key);
            Check(_app.Battle.Drones.Select(row => row.L("uid")).Order().SequenceEqual(ids), "reload preserves live fighter identities");
            var stableGame = _app.Game.Serialize();
            string stableFile = Godot.FileAccess.GetFileAsString(ManagedMain.SavePath);
            var bad = saved.DeepClone();
            bad["destroyed_fronts"] = new List<object?> { "front_invalid" };
            Check(!_app.ValidateCheckpoint(bad), "bad front ledger cannot enter load path");
            WriteText(ManagedMain.SavePath, bad.ToJson());
            Check(!_app.LoadCheckpoint(), "invalid checkpoint explicitly refused");
            Check(DataMap.Equivalent(_app.Game.Serialize(), stableGame), "invalid load leaves current campaign intact");
            Check(DataMap.Equivalent(DataMap.Parse(Godot.FileAccess.GetFileAsString(ManagedMain.SavePath)), bad), "invalid input not silently replaced by fresh state");
            WriteText(ManagedMain.SavePath, stableFile);
            Check(FactoryPerks.Hash(Godot.FileAccess.GetFileAsString(Original)) == hash, "protected original bytes unchanged");
            var report = new DataMap { ["checks"] = _checks, ["failures"] = _failures.Cast<object?>().ToList(), ["wave"] = _app.Game.Wave, ["stage"] = _app.Game.DefenseReachStage, ["active_aircraft"] = _app.Battle.Drones.Count, ["ceasefire_remaining"] = _app.Campaign.GetStatus().N("ceasefire_remaining"), ["original_sha256"] = hash, ["profile"] = OS.GetUserDataDir(), ["no_gdscript_instances"] = NoLegacyScripts(_app), ["permanent_test_fixture"] = "explicit oldschema2 fixture; original125predatesperks" };
            WriteText("res://artifacts/managed-legacy-main-result.json", report.ToJson(true));
            GD.Print($"MANAGED_LEGACY_RESULT {_checks} checks {_failures.Count} failures");
            _app.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Quit(_failures.Count == 0 ? 0 : 1);
        }
        catch (Exception exception) { GD.PrintErr("MANAGED_LEGACY_EXCEPTION: " + exception); _app?.QueueFree(); GetTree().Quit(1); }
    }
    private static bool NoLegacyScripts(Node node)
    {
        if (node.GetScript().VariantType == Variant.Type.Object && node.GetScript().AsGodotObject() is GDScript)
            return false;
        foreach (var child in node.GetChildren())
            if (!NoLegacyScripts(child))
                return false;
        return true;
    }
}
