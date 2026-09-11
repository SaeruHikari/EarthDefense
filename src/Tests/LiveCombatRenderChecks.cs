using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Earthward.Domain;
using Earthward.Combat;
using ManagedMain = Earthward.Application.Main;

namespace Earthward.Tests;

/// <summary>Real new-game combat packets must survive the native renderer, not only managed simulation tests.</summary>
public partial class LiveCombatRenderChecks : Node
{
    private ManagedMain? _app;
    private int _checks;
    private readonly List<string> _failures = new();
    private readonly HashSet<long> _friendlyUids = new(), _hostileUids = new();
    private int _friendlySubmissions, _hostileSubmissions, _renderSubmissions;
    private bool _friendlyNativeBatch, _hostileNativeBatch, _postDrawObserved;
    private double _simulatedSeconds;

    private void Check(bool valid, string label)
    {
        _checks++;
        if (valid)
            return;
        _failures.Add(label);
        GD.PrintErr("MANAGED_LIVE_COMBAT_FAIL: " + label);
    }

    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static string PrepareIsolatedNewGame()
    {
        string workspace = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string allowed = Path.GetFullPath(Path.Combine(workspace, ".runtime-tests")) + Path.DirectorySeparatorChar;
        string profile = Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        if (!profile.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LiveCombatRenderChecks requires a user profile under this project's .runtime-tests directory.");
        Directory.CreateDirectory(profile);
        // Only this isolated fixture's known files are reset; never enumerate or remove player directories.
        foreach (string name in new[] { "earthward_checkpoint.json", "earthward_factory_perks.json", "earthward_combat_settings.json" })
            foreach (string suffix in new[] { "", ".previous", ".bak", ".tmp" })
            {
                string path = Path.GetFullPath(Path.Combine(profile, name + suffix));
                if (!path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Test file escaped the isolated profile.");
                if (File.Exists(path))
                    File.Delete(path);
            }
        return profile;
    }

    private static bool HasVisibleProjectileBatch(ManagedMain app, string visual)
    {
        string prefix = "ManagedFleet_projectiles_projectile_" + visual + "_";
        foreach (Node child in app.Planet.SpaceRoot.FindChildren("ManagedFleet_*", "MultiMeshInstance3D", true, false))
        {
            if (child is not MultiMeshInstance3D instance || !instance.Name.ToString().StartsWith(prefix, StringComparison.Ordinal) || !instance.Visible)
                continue;
            var batch = instance.Multimesh;
            if (batch != null && batch.VisibleInstanceCount > 0 && batch.Mesh != null && batch.Mesh.GetSurfaceCount() > 0)
                return true;
        }
        return false;
    }

    private void SubmitCurrentPackets(ManagedMain app, bool friendly, bool hostile)
    {
        // This is the application integration path that previously dereferenced the missing friendly model.
        app.SyncRender();
        _renderSubmissions++;
        if (friendly)
        {
            _friendlySubmissions++;
            foreach (var shot in app.Battle.Shots.Where(row => row.S("kind") == "friendly"))
                _friendlyUids.Add(shot.L("uid"));
            _friendlyNativeBatch |= HasVisibleProjectileBatch(app, "interceptor");
        }
        if (hostile)
        {
            _hostileSubmissions++;
            foreach (var shot in app.Battle.HostileShots.Where(row => row.S("kind") == "hostile"))
                _hostileUids.Add(shot.L("uid"));
            _hostileNativeBatch |= HasVisibleProjectileBatch(app, "hostile");
        }
    }

    public override async void _Ready()
    {
        string profile = "";
        try
        {
            profile = PrepareIsolatedNewGame();
            GetWindow().Size = new Vector2I(1440, 900);
            var app = new ManagedMain();
            _app = app;
            AddChild(app);
            app.SetProcess(false);
            app.Planet.SetProcess(false);
            app.Sounds.Muted = true;
            app.Modal = "";
            app.UserPaused = false;
            app.Speed = 1;
            app.Battle.Random.Seed = 11;
            Check(app.Game.Wave == 0 && app.Game.DeepResearch.Count == 0, "actual Main starts a fresh unresearched run");
            Check(app.Game.Minerals == 320 && app.Game.Energy == 180 && app.Game.Science == 80, "default starting resources unchanged");
            Check(app.Game.FactoryPerks.Snapshot().Map("levels").Values.All(level => DataMap.Integer(level) == 0), "no permanent upgrades injected");
            Check(DataMap.Equivalent(app.Game.CombatSettings, DefenseState.DefaultCombatSettings), "production default combat parameters unchanged");
            await Frames(3);
            app.StartWave();
            Check(app.Started && app.Battle.Active && app.Battle.WaveRunning && app.Game.Wave == 1, "real Main.StartWave starts the production scheduler");
            const double dt = 1d / 60;
            for (int tick = 0; tick < 3600; tick++)
            {
                // Normal simulation order; automatic Main/Planet processing is disabled so no double tick occurs.
                if (app.Defeated)
                    break;
                app.Game.Tick(dt);
                app.Planet._Process(dt);
                app.Battle.Step(dt);
                app.Campaign.Step(dt);
                _simulatedSeconds += dt;
                bool friendly = app.Battle.Shots.Any(row => row.S("kind") == "friendly");
                bool hostile = app.Battle.HostileShots.Any(row => row.S("kind") == "hostile");
                if (friendly || hostile || tick % 6 == 0)
                    SubmitCurrentPackets(app, friendly, hostile);
                if (!_postDrawObserved && _friendlyNativeBatch && _hostileNativeBatch && (friendly || hostile) && DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    _postDrawObserved = true;
                }
                if (tick % 12 == 0)
                    await Frames(1);
            }
            app.SyncRender();
            await Frames(3);
            Check(_friendlyUids.Count > 0, "normal interceptor gun generated kind=friendly packets");
            Check(_friendlySubmissions > 0 && _friendlyNativeBatch, "real friendly packets reached a visible native mesh batch through Main.SyncRender");
            Check(_hostileUids.Count > 0, "normal enemies generated hostile projectiles");
            Check(_hostileSubmissions > 0 && _hostileNativeBatch, "real hostile packets reached a visible native mesh batch through Main.SyncRender");
            Check(app.Battle.NumberSequence > 0, "actual damage occurred while packets were submitted");
            Check(_simulatedSeconds >= 59.9 || app.Defeated && app.Game.EarthHp <= 0 && app.Battle.Dead && !app.Battle.WaveRunning,
                "unmodified combat reaches sixty seconds or terminates at a valid real defeat");
            Check(app.Defeated ? app.Modal == "defeat" && app.WorldIsPaused() : app.Game.Wave >= 2,
                "defeat presents paused recovery UI; a surviving run continues its fixed wave cycle");
            // Scheduling is a separate subsystem fixture. It advances only the real deployment clock,
            // without combat damage, resource gifts, or a claim that an unplayed defense must survive.
            var scheduleGame = new DefenseState();
            var schedule = new Battlefield(scheduleGame, app.Planet);
            schedule.Random.Seed = 11; schedule.StartWave();
            double cycle = scheduleGame.CombatSettings.N("enemy_wave_duration");
            schedule.AdvanceFixedSchedule(cycle - .01);
            Check(scheduleGame.Wave == 1 && schedule.GetWaveSpawnPlan().N("cycle_elapsed") < cycle,
                "independent default scheduler does not advance before its configured boundary");
            Check(schedule.Enemies.Count > 0, "scheduler fixture deploys genuine enemies before boundary");
            var previousEnemyIds = schedule.Enemies.Select(enemy => enemy.L("uid")).ToHashSet();
            schedule.AdvanceFixedSchedule(.02);
            Check(scheduleGame.Wave == 2 && schedule.GetWaveSpawnPlan().N("cycle_elapsed") < .02,
                "independent scheduler opens wave two at the fixed boundary");
            Check(schedule.Enemies.Any(enemy => previousEnemyIds.Contains(enemy.L("uid"))),
                "uncleared enemies remain alive across the fixed wave transition");
            if (DisplayServer.GetName() != "headless")
                Check(_postDrawObserved, "native renderer completed a draw with live projectile geometry");
        }
        catch (Exception exception)
        {
            _failures.Add(exception.ToString());
            GD.PrintErr("MANAGED_LIVE_COMBAT_EXCEPTION: " + exception);
        }

        var report = new DataMap
        {
            ["checks"] = _checks,
            ["failures"] = _failures.Cast<object?>().ToList(),
            ["profile"] = profile,
            ["simulated_seconds"] = _simulatedSeconds,
            ["wave"] = _app?.Game?.Wave ?? 0,
            ["earth_hp"] = _app?.Game?.EarthHp ?? 0,
            ["defeated"] = _app?.Defeated ?? false,
            ["friendly_projectile_uids"] = _friendlyUids.Count,
            ["hostile_projectile_uids"] = _hostileUids.Count,
            ["render_submissions"] = _renderSubmissions,
            ["friendly_submissions"] = _friendlySubmissions,
            ["hostile_submissions"] = _hostileSubmissions,
            ["friendly_native_batch"] = _friendlyNativeBatch,
            ["hostile_native_batch"] = _hostileNativeBatch,
            ["frame_post_draw_observed"] = _postDrawObserved,
            ["damage_events"] = _app?.Battle?.NumberSequence ?? 0,
            ["fixture"] = "default new Main, ordinary production and wave spawning, no actor injection or numerical boosts"
        };
        File.WriteAllText(ProjectSettings.GlobalizePath("res://artifacts/managed-live-combat-result.json"), report.ToJson(true));
        if (IsInstanceValid(_app))
        {
            _app!.QueueFree();
            await Frames(4);
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
        }
        GD.Print($"MANAGED_LIVE_COMBAT: {_checks} checks / {_failures.Count} failures");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}

