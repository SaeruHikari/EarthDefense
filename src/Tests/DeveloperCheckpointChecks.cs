using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Earthward.Domain;
using Earthward.Combat;
using ManagedMain = Earthward.Application.Main;
namespace Earthward.Tests;

/// <summary>Unmodified real developer checkpoint and a separately identified native radius-eight save.</summary>
public partial class DeveloperCheckpointChecks : Node
{
    private int _checks;
    private readonly List<string> _failures = new();
    private readonly List<object?> _reports = new();
    private ManagedMain? _app;
    private string _case = "";
    private DataMap _expectedRefund = new();
    private void Check(bool condition, string label)
    {
        _checks++;
        if (!condition) { string text = _case + ": " + label; _failures.Add(text); GD.PrintErr("DEVELOPER_CHECKPOINT_FAIL: " + text); }
    }
    private void Near(double actual, double expected, string label, double tolerance = .0001) => Check(Math.Abs(actual - expected) <= Math.Max(tolerance, Math.Abs(expected) * .0000001), label + $" {actual} / {expected}");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static DataMap Read(string path) => DataMap.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
    private static string UserPath(string name) => ProjectSettings.GlobalizePath("user://" + name);
    private static string ProfileName => "EARTHWARD \u00b7 \u5730\u7403\u5b88\u671b";
    private static DataMap DecodedBattle(DataMap snapshot)
    {
        if (!CombatSnapshotCodec.TryDecode(snapshot.Value("payload"), out var data) || data is not DataMap result)
            throw new InvalidDataException("Checkpoint battle payload cannot decode");
        return result;
    }
    private static List<DataMap> Rows(DataMap fields, string key) => fields.List(key).OfType<DataMap>().ToList();
    private static Dictionary<long, DataMap> Actors(DataMap fields, string key) => Rows(fields, key).ToDictionary(row => row.L("uid"));
    private void CheckPositions(DataMap original, DataMap actual, double delta, string phase)
    {
        foreach (string key in new[] { "_drones", "enemies", "_shots", "_hostile_shots" })
        {
            var before = Actors(original, key); var after = Actors(actual, key);
            Check(before.Keys.Order().SequenceEqual(after.Keys.Order()), phase + " stable actor UIDs " + key);
            foreach (var (id, prior) in before)
            {
                if (!after.TryGetValue(id, out var next) || prior.Value("space_position") is not Vector3 a || next.Value("space_position") is not Vector3 b)
                    continue;
                double expected = a.Length() <= .000001f ? 0 : a.Length() + delta;
                Near(b.Length(), expected, phase + " radial altitude " + key + "/" + id, .0002);
                if (a.Length() > .000001f) Check(a.Normalized().DistanceTo(b.Normalized()) < .00001f, phase + " direction " + key + "/" + id);
                foreach (string field in new[] { "factory_site_id", "patrol_slot" })
                    if (prior.ContainsKey(field)) Check(next.L(field) == prior.L(field), phase + " stable aircraft ownership " + field);
            }
        }
        Check(original.Map("_factories").Keys.Order().SequenceEqual(actual.Map("_factories").Keys.Order()), phase + " factory IDs remain stable");
        foreach (var (id, entry) in original.Map("_factories"))
        {
            if (entry is not DataMap factory || !actual.Map("_factories").ContainsKey(id)) continue;
            var prior = factory.Map("site"); var next = actual.Map("_factories").Map(id).Map("site");
            if (prior.Value("launch_position") is Vector3 a && next.Value("launch_position") is Vector3 b)
                Near(b.Length(), a.Length() <= .000001f ? 0 : a.Length() + delta, phase + " launch altitude " + id, .0002);
        }
    }
    private void CheckInvestment(DataMap originalGame, DataMap originalMeta, string phase)
    {
        var game = _app!.Game; var now = game.Serialize();
        foreach (string key in new[] { "wave", "completed_waves", "minerals", "energy", "science", "earth_hp", "shield", "alien_points", "resource_cores", "kills", "score" })
            Near(now.N(key), originalGame.N(key) + _expectedRefund.N(key), phase + " original + one-time migration refund " + key);
        foreach (var (id, level) in originalGame.Map("tech")) Near(game.Tech.N(id), DataMap.Number(level), phase + " paid legacy research " + id);
        foreach (var (id, level) in originalGame.Map("research_state").Map("nodes")) Near(game.DeepResearch.N(id), DataMap.Number(level), phase + " independent research " + id);
        foreach (var (kind, count) in originalGame.Map("buildings")) Near(game.Buildings.N(kind), DataMap.Number(count), phase + " actual facility count " + kind);
        if (!originalGame.Map("buildings").ContainsKey("shield")) Near(game.Buildings.N("shield"), 0, phase + " legacy checkpoint never receives a free shield tower");
        var meta = game.FactoryPerks.Snapshot();
        Check(DataMap.Equivalent(meta.Map("levels"), originalMeta.Map("levels")), phase + " all permanent perk ownership and levels");
        Near(meta.N("energy_cores"), originalMeta.N("energy_cores"), phase + " permanent currency");
        foreach (string field in new[] { "templates", "aircraft_templates" }) Check(DataMap.Equivalent(meta.Map(field), originalMeta.Map(field)), phase + " permanent templates " + field);
        Check(meta.List("claims").Cast<string>().Order().SequenceEqual(originalMeta.List("claims").Cast<string>().Order()), phase + " reward claim ledger");
    }
    private async System.Threading.Tasks.Task RunCase(string name, string sourcePath, double? requiredRadius = null)
    {
        _case = name;
        string projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string source = Path.GetFullPath(sourcePath);
        if (!source.StartsWith(Path.Combine(projectRoot, "saves", "backups") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !source.StartsWith(Path.Combine(projectRoot, ".runtime-tests") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only backups or previously isolated native fixtures may be read");
        string sourceMeta = Path.Combine(Path.GetDirectoryName(source)!, "earthward_factory_perks.json");
        string sourcePrefs = Path.Combine(Path.GetDirectoryName(source)!, "earthward_combat_settings.json");
        string sourceHash = Hash(source), metaHash = Hash(sourceMeta);
        string? prefsHash = File.Exists(sourcePrefs) ? Hash(sourcePrefs) : null;
        var original = Read(source); var originalGame = original.Map("game"); var sourceSnapshot = original.Map("expedition_battle").Map("earth");
        double sourceRadius = sourceSnapshot.I("version") >= 3 ? sourceSnapshot.N("earth_radius") : WorldScale.LegacyEarthRadius;
        if (requiredRadius.HasValue && sourceRadius != requiredRadius.Value) throw new InvalidDataException("The separately identified radius-eight fixture lost its explicit radius marker");
        var permanent = new FactoryPerks();
        if (!permanent.ImportSnapshot(Read(sourceMeta))) throw new InvalidDataException("Source permanent profile is invalid; refusing an empty replacement");
        var originalMeta = permanent.Snapshot();
        File.Copy(source, UserPath("earthward_checkpoint.json"), true);
        File.Copy(sourceMeta, UserPath("earthward_factory_perks.json"), true);
        if (File.Exists(sourcePrefs)) File.Copy(sourcePrefs, UserPath("earthward_combat_settings.json"), true);
        _app = new ManagedMain(); AddChild(_app); _app.SetProcess(false); _app.Planet.SetProcess(false); _app.Sounds.Muted = true;
        Check(_app.ValidateCheckpoint(original), "real Main validates unmodified source checkpoint");
        bool loaded = _app.LoadCheckpoint(); Check(loaded, "real Main initial load");
        if (!loaded) throw new InvalidOperationException("Initial load refused; stopping dependent checks");
        if (!originalGame.Map("buildings").ContainsKey("shield") && original.B("started")) Check(_app.UserPaused, "old live game resumes paused for local shield transition");
        _app.UserPaused = true;
        _expectedRefund = _app.Game.LastResearchRefund;
        CheckInvestment(originalGame, originalMeta, "initial");
        Check(_app.Game.ResearchCapacityBonus <= 3, "new research capacity cap applies while factory/perk investment survives");
        Check(DataMap.Equivalent(_app.Planet.GetSlots(), original.List("slots")), "surface logical slot identities unchanged");
        var directions = _app.Planet.GetSiteDirections(); Check(directions.Count == original.List("site_directions").Count, "all site directions retained");
        for (int i = 0; i < directions.Count; i++)
            Check(new DataMap { ["v"] = directions[i] }.Vector3("v").DistanceTo(new DataMap { ["v"] = original.List("site_directions")[i] }.Vector3("v")) < .000005f, "site direction identity " + i);
        Check(_app.Battle.GetDestroyedFronts().Order().SequenceEqual(original.List("destroyed_fronts").Cast<string>().Order()), "defeated mother records retained");
        var firstSnapshot = _app.Battle.SerializeCombatSnapshot(); var firstFields = DecodedBattle(firstSnapshot);
        Near(firstSnapshot.N("earth_radius"), WorldScale.EarthRadius, "battle now declares current radius");
        Check(_app.Game.Serialize().I("world_scale_version") == DefenseState.WorldScaleVersion, "game now declares current scale schema");
        CheckPositions(DecodedBattle(sourceSnapshot), firstFields, WorldScale.EarthRadius - sourceRadius, "first load");
        var firstGame = _app.Game.Serialize(); var celestial = _app.Planet.CaptureCelestialState(); double clock = _app.Battle.Clock;
        Check(_app.SaveCheckpoint(), "actual Main atomic save");
        var saved = Read(UserPath("earthward_checkpoint.json")); Check(_app.ValidateCheckpoint(saved), "new saved envelope validates");
        Check(_app.LoadCheckpoint(), "actual Main reload saved checkpoint"); _app.UserPaused = true;
        CheckInvestment(originalGame, originalMeta, "reload");
        Check(_app.Game.LastResearchRefund.Count == 0, "version3 checkpoint never repeats capacity refunds");
        CheckPositions(firstFields, DecodedBattle(_app.Battle.SerializeCombatSnapshot()), 0, "second load");
        Check(DataMap.Equivalent(firstGame, _app.Game.Serialize()), "game roundtrip has no repeated migration or reward changes");
        Near(_app.Battle.Clock, clock, "reload does not advance battle time");
        Check(DataMap.Equivalent(celestial, _app.Planet.CaptureCelestialState()), "reload retains celestial clock");
        Check(Hash(source) == sourceHash, "source checkpoint bytes unchanged"); Check(Hash(sourceMeta) == metaHash, "source permanent bytes unchanged");
        if (prefsHash != null) Check(Hash(sourcePrefs) == prefsHash, "source preferences bytes unchanged");
        _reports.Add(new DataMap { ["source"] = source, ["source_sha256"] = sourceHash, ["source_radius"] = sourceRadius, ["source_wave"] = originalGame.L("wave"), ["earth_hp"] = originalGame.N("earth_hp"), ["factory_sites"] = _app.Battle.Factories.Count, ["aircraft"] = _app.Battle.Drones.Count, ["enemies"] = _app.Battle.Enemies.Count, ["case"] = name });
        File.Copy(UserPath("earthward_checkpoint.json"), UserPath(name + "-migrated-checkpoint.json"), true);
        _app.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); _app = null;
    }
    public override async void _Ready()
    {
        try
        {
            string user = Path.GetFullPath(OS.GetUserDataDir()).Replace('\\', '/');
            if (!user.Contains("/.runtime-tests/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Native checkpoint tests require an isolated .runtime-tests profile");
            GetWindow().Size = new Vector2I(1440, 900); GetWindow().Position = new Vector2I(-1700, 100);
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            string root = ProjectSettings.GlobalizePath("res://");
            string backup = Read(Path.Combine(root, "artifacts", "vision125-backup.json")).S("backup");
            string realDeveloper = Path.Combine(backup, ".runtime", "debug-cleared", "AppData", "Godot", "app_userdata", ProfileName, "earthward_checkpoint.json");
            string nativeRadiusEight = Path.Combine(root, ".runtime-tests", "earth-radius8-legacy", "AppData", "Godot", "app_userdata", ProfileName, "earthward_checkpoint.json");
            string targetProfile = Path.GetFullPath(ProjectSettings.GlobalizePath("user://")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string source in new[] { realDeveloper, nativeRadiusEight })
                if (Path.GetFullPath(Path.GetDirectoryName(source)!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Equals(targetProfile, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The test profile must differ from every read-only source profile");
            await RunCase("actual-developer", realDeveloper);
            await RunCase("native-radius8", nativeRadiusEight, 8);
            var report = new DataMap { ["checks"] = _checks, ["failures"] = _failures.Cast<object?>().ToList(), ["cases"] = _reports, ["profile"] = OS.GetUserDataDir() };
            File.WriteAllText(UserPath("developer-checkpoint-result.json"), report.ToJson(true));
            GD.Print($"DEVELOPER_CHECKPOINT_RESULT {_checks} checks {_failures.Count} failures");
            GetTree().Quit(_failures.Count == 0 ? 0 : 1);
        }
        catch (Exception error) { GD.PrintErr("DEVELOPER_CHECKPOINT_EXCEPTION: " + error); _app?.QueueFree(); GetTree().Quit(1); }
    }
}
