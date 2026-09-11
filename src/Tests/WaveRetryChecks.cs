using Godot;
using System.Security.Cryptography;
using Earthward.Application;
using Earthward.Domain;
using Earthward.Combat;

namespace Earthward.Tests;

public partial class WaveRetryChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool value, string label) { _checks++; if (!value) { _failures++; GD.PrintErr("WAVE_RETRY_FAIL: " + label); } }
    private static string PathFor(string name) => ProjectSettings.GlobalizePath("user://" + name);
    private string Read(string name) { _app.FlushCheckpointWrites(); return System.IO.File.ReadAllText(PathFor(name)); }
    private string Digest(string name) { _app.FlushCheckpointWrites(); return Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(PathFor(name)))); }
    private async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task NewMain()
    {
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout); }
        _app = new Main(); AddChild(_app); _app.SetProcess(false); _app.Planet.SetProcess(false); _app.UserPaused = true; _app.Sounds.Muted = true;
        await Frames(4);
    }
    private static DataMap BattleState(DataMap checkpoint)
    {
        CombatSnapshotCodec.TryDecode(checkpoint.Map("expedition_battle").Map("earth").Value("payload"), out var data);
        return (DataMap)data!;
    }
    private async Task DefeatAndClickRetry()
    {
        _app.Defeated = true; _app.Modal = "defeat"; _app.Game.EarthHp = 0; _app.QueueRedraw(); await Frames();
        var button = _app.Buttons.First(row => row.S("action") == "modal:retry_wave").Get<Rect2>("rect");
        Vector2 point = button.GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
    }
    private void AssertWaveStart(DataMap expected, string phase)
    {
        var state = _app.Game.Serialize();
        foreach (string key in new[] { "wave", "completed_waves", "minerals", "energy", "science", "resource_cores", "alien_points", "earth_hp", "kills", "score", "defense_time" })
            Check(state.N(key) == expected.Map("game").N(key), phase + " restores wave-start " + key);
        foreach (string key in new[] { "buildings", "resource_core_upgrades", "research_state", "airframe_selection" })
            Check(DataMap.Equivalent(state.Value(key), expected.Map("game").Value(key)), phase + " restores wave-start " + key);
        Check(DataMap.Equivalent(_app.Planet.GetSlots(), expected.List("slots")) && DataMap.Equivalent(_app.Planet.GetSiteDirections(), expected.List("site_directions")), phase + " restores actual surface factory layout");
        var plan = _app.Battle.GetWaveSpawnPlan();
        Check(plan.N("cycle_elapsed") == 0 && plan.N("elapsed") == 0 && plan.L("spawned") == 0, phase + " restarts at deployment zero, not in the middle");
        Check(_app.UserPaused && _app.Battle.Paused && _app.Campaign.Paused && _app.Planet.Paused && !_app.Defeated && _app.Modal == "", phase + " pauses in normal editable command mode");
        var battle = _app.Battle.SerializeCombatSnapshot();
        var oldBattle = expected.Map("expedition_battle").Map("earth");
        Check(battle.S("rng_seed") == oldBattle.S("rng_seed") && battle.S("rng_state") == oldBattle.S("rng_state"), phase + " restores exact random sequence");
        CombatSnapshotCodec.TryDecode(battle.Value("payload"), out var decoded);
        var actual = (DataMap)decoded!; var original = BattleState(expected);
        foreach (string key in new[] { "_next_uid", "_clock", "emp_cooldown", "_destroyed_drones" }) Check(actual.N(key) == original.N(key), phase + " restores battle " + key);
        foreach (string key in new[] { "_drones", "enemies", "_shots", "_hostile_shots", "_local_shields" })
        {
            bool equal = DataMap.Equivalent(actual.Value(key), original.Value(key));
            if (!equal)
            {
                var before = original.List(key); var after = actual.List(key);
                GD.PrintErr($"WAVE_RETRY_DIFF: {phase} {key} count {before.Count} -> {after.Count}");
                for (int i = 0; i < Math.Min(before.Count, after.Count); i++)
                {
                    if (before[i] is not DataMap a || after[i] is not DataMap b || DataMap.Equivalent(a, b)) continue;
                    foreach (string field in a.Keys.Union(b.Keys))
                        if (!DataMap.Equivalent(a.Value(field), b.Value(field)))
                            GD.PrintErr($"WAVE_RETRY_DIFF: {key}[{i}] uid={a.L("uid")} {field}: {new DataMap { ["before"] = a.Value(field), ["after"] = b.Value(field) }.ToJson()}");
                    if (i >= 5) break;
                }
            }
            Check(equal, phase + " restores authoritative roster " + key);
        }
    }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Retry tests require an isolated runtime-tests profile");
            GetWindow().Size = new Vector2I(1440, 900);
            await NewMain();
            _app.Game.SetCombatSetting("medium_boss_first_wave", 3);
            _app.Game.AddResourcesCheat("minerals", 500); _app.Game.AddResourcesCheat("energy", 500); _app.Game.AddResourcesCheat("science", 100); _app.Game.AddResourcesCheat("resource_cores", 10);
            _app.Battle.Active = true; _app.Battle.Paused = false;
            for (int i = 0; i < 600; i++) _app.Battle.Step(1d / 60);
            _app.StartWave();
            Check(_app.HasWaveStartRetry && _app.RetryWaveNumber == 1, "wave-start event immediately captures the complete in-memory retry point");
            _app.FlushCheckpointWrites();
            Check(_app.SaveExists, "queued opening is durably persisted after flush");
            var opening = _app.CurrentWaveStartCheckpoint!;
            Check(_app.ValidateWaveStartRecord(DataMap.Parse(Read("earthward_wave_start.json"))), "persisted opening record passes complete strict validation");
            string sidecarHash = Digest("earthward_wave_start.json");
            _app.Battle.Paused = false; _app.Campaign.Paused = false;
            for (int i = 0; i < 180; i++) { _app.Game.Tick(1d / 60); _app.Battle.Step(1d / 60); _app.Campaign.Step(1d / 60); }
            Check(_app.Battle.GetWaveSpawnPlan().N("cycle_elapsed") > 2 && _app.Battle.GetWaveSpawnPlan().L("spawned") > 0, "fixture genuinely advances into the wave");
            Check(_app.Game.PurchaseGroup("K_S01"), "fixture researches during wave");
            var slots = _app.Planet.GetSlots();
            int site = Enumerable.Range(0, slots.Count).First(i => (string?)slots[i] == "" && _app.Planet.CanPlaceStructure(i, "mine"));
            Check(_app.Game.Build("mine", site) && _app.Planet.SetSlot(site, "mine"), "fixture builds during wave");
            _app.Game.EarthHp = 42;
            Check(_app.SaveCheckpoint(), "ordinary autosave records actual mid-wave state");
            string midWave = Read("earthward_checkpoint.json");
            double middle = _app.Battle.GetWaveSpawnPlan().N("cycle_elapsed");
            Check(Digest("earthward_wave_start.json") == sidecarHash, "mid-wave save never overwrites opening record");
            var reward = _app.Game.ClaimFactoryBossReward("earth:wave:1:medium:0", 1, 0);
            Check(reward.B("claimed"), "fixture earns a permanent Boss reward after opening");
            long permanent = _app.Game.FactoryPerks.EnergyCores;
            string profileHash = Digest("earthward_factory_perks.json");
            await DefeatAndClickRetry();
            AssertWaveStart(opening, "native defeat retry");
            Check(_app.Game.FactoryPerks.EnergyCores == permanent && Digest("earthward_factory_perks.json") == profileHash, "retry never writes back an older permanent perk profile");
            Check(_app.Game.ClaimFactoryBossReward("earth:wave:1:medium:0", 1, 0).B("duplicate") && _app.Game.FactoryPerks.EnergyCores == permanent, "replaying same Boss cannot duplicate permanent reward");
            _app.ChooseBuild("mine");
            Check(_app.SelectedBuild == "mine" && _app.UserPaused, "player can redeploy while wave-start retry is paused");
            double pausedClock = _app.Battle.Clock; _app.Battle.Step(1);
            Check(_app.Battle.Clock == pausedClock, "retry remains paused until player resumes");

            _app.FlushCheckpointWrites(); // Complete the asynchronous writer before replacing its file with a fixture.
            System.IO.File.WriteAllText(PathFor("earthward_checkpoint.json"), midWave);
            Check(_app.LoadCheckpoint() && _app.Battle.GetWaveSpawnPlan().N("cycle_elapsed") == middle && _app.Game.HasResearch("K_S01"), "ordinary load still restores exact mid-wave checkpoint");
            _app.Battle.Paused = true;
            _app.Battle.AdvanceFixedSchedule(_app.Battle.GetWaveSpawnPlan().N("cycle_duration") - middle + .02);
            Check(_app.Game.Wave == 2 && _app.RetryWaveNumber == 2, "next wave writes a distinct new opening");
            await Frames(3); // Drain the old completion autosave before restoring the intentionally earlier record.
            _app.FlushCheckpointWrites(); // Complete the asynchronous writer before replacing its file with a fixture.
            System.IO.File.WriteAllText(PathFor("earthward_checkpoint.json"), midWave);
            Check(_app.LoadCheckpoint() && _app.Game.Wave == 1 && _app.RetryWaveNumber == 1, "loading earlier exact save matches earlier opening rather than future wave");

            // Simulate a genuine pre-feature checkpoint: remove only its matching opening in this isolated directory.
            var legacy = DataMap.Parse(midWave); legacy.Map("game")["earth_hp"] = 0d;
            _app.FlushCheckpointWrites(); // Complete the asynchronous writer before replacing its file with a fixture.
            System.IO.File.WriteAllText(PathFor("earthward_checkpoint.json"), legacy.ToJson());
            if (System.IO.File.Exists(PathFor("earthward_wave_start.json.previous"))) System.IO.File.Delete(PathFor("earthward_wave_start.json.previous"));
            await NewMain();
            Check(_app.LoadCheckpoint() && !_app.HasWaveStartRetry, "legacy mid-wave save does not inherit a later wave opening");
            double legacyMoney = _app.Game.Minerals;
            await DefeatAndClickRetry();
            Check(_app.Game.Wave == 1 && _app.Game.EarthHp == 100 && _app.Game.Minerals == legacyMoney && _app.Game.HasResearch("K_S01"), "first legacy recovery retains progress and reorganizes healthy Earth at same wave");
            Check(_app.HasWaveStartRetry && _app.UserPaused && _app.Battle.GetWaveSpawnPlan().N("cycle_elapsed") == 0 && _app.Battle.GetWaveSpawnPlan().L("spawned") == 0, "legacy recovery establishes a genuine reusable zero-time opening");
            var reorganized = _app.CurrentWaveStartCheckpoint!;
            _app.Game.Minerals = 1; _app.Game.EarthHp = 5;
            Check(_app.RetryWaveStart(), "subsequent legacy retries use captured opening");
            AssertWaveStart(reorganized, "subsequent legacy retry");

            string validOpening = Digest("earthward_wave_start.json");
            string blockedTemporary = PathFor("earthward_wave_start.json.tmp");
            System.IO.Directory.CreateDirectory(blockedTemporary);
            _app.Battle.AdvanceFixedSchedule(_app.Battle.GetWaveSpawnPlan().N("cycle_duration") + .01);
            Check(_app.Game.Wave == 2 && _app.HasWaveStartRetry && Digest("earthward_wave_start.json") == validOpening, "sidecar write failure preserves valid old file and current in-memory opening");
            System.IO.Directory.Delete(blockedTemporary);

            Check(_app.Restart(), "fixture starts independent continuation campaign"); _app.SetProcess(false); _app.Planet.SetProcess(false);
            Check(_app.Battle.RestoreDestroyedFronts(Enumerable.Range(1, 8).Select(i => $"front_{i:00}")), "continuation fixture clears all real near-Earth mothers");
            _app.Started = true; _app.CampaignWon = true;
            _app.Campaign.SyncCampaign(); _app.Campaign.Paused = false;
            _app.Campaign.Step(_app.Campaign.GetStatus().N("ceasefire_remaining") + .01);
            Check(_app.HasWaveStartRetry && _app.Battle.IsPostDefenseActive(), "continuing mothership wave also captures an opening");
            var continuing = _app.CurrentWaveStartCheckpoint!;
            CombatSnapshotCodec.TryDecode(continuing.Map("expedition_battle").Value("payload"), out var directorState);
            var director = (DataMap)directorState!;
            Check(director.S("_earth_phase") == "continuing" && director.L("_earth_wave") == _app.Battle.PostPlan.L("wave"), "synchronous opening snapshot contains committed director cohort metadata");
            _app.Game.EarthHp = 3;
            Check(_app.RetryWaveStart(), "continuing mothership wave retries successfully");
            Check(_app.Campaign.OwnsEarthSchedule() && _app.Campaign.GetStatus().S("earth_phase") == "continuing" && _app.Campaign.GetStatus().L("earth_wave") == director.L("_earth_wave") && _app.Battle.GetWaveSpawnPlan().N("cycle_elapsed") == 0, "continuing retry restores correct cohort and start without duplicated schedule");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout); }
        GD.Print($"WAVE_RETRY_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
