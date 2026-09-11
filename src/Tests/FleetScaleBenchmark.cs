using Godot;
using System.Diagnostics;
using Earthward.Domain;
using ManagedMain = Earthward.Application.Main;

namespace Earthward.Tests;

/// <summary>Real main-loop, native-render frame pressure capture with a fixed population.</summary>
public partial class FleetScaleBenchmark : Node
{
    private ManagedMain _app = null!;
    private FleetStressFixture _fixture = null!;
    private readonly Dictionary<string, List<double>> _samples = new();
    private string _tag = "capture", _mode = "combat";
    private FleetStressLayout _layout;
    private int _tick, _warmup = 60, _sampleFrames = 180, _enemyCount = 1000;
    private bool _ready, _finished, _detailedProfiler;
    private int _minFriendly = int.MaxValue, _maxShots;
    private long _firstKills, _firstLosses, _firstDamage, _lastFrameStamp, _firstAllocated;
    private double _simulationStart;
    private int[] _firstCollections = new int[3];
    private readonly Earthward.Combat.CombatPerformanceCounters _combatTimings = new();
    private static string Option(string name, string fallback) => OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--" + name + "="))?.Split('=', 2)[1] ?? fallback;
    private void Record(string key, double value)
    {
        if (!_samples.TryGetValue(key, out var list)) _samples[key] = list = new(_sampleFrames);
        list.Add(value);
    }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Stress capture requires an isolated runtime-tests profile");
            _tag = Option("bench-tag", "capture"); _mode = Option("bench-mode", "combat");
            if (!Enum.TryParse<FleetStressLayout>(Option("layout", Option("bench-layout", "cluster")), true, out _layout)) throw new ArgumentException("layout must be cluster or world");
            _warmup = int.Parse(Option("warmup", "60")); _sampleFrames = int.Parse(Option("samples", "180")); _enemyCount = int.Parse(Option("enemies", "1000"));
            GetWindow().Size = new(1440, 900);
            _app = new ManagedMain(); AddChild(_app); _app.SetProcess(false);
            _app.Sounds.Muted = true; _app.PreserveCheckpoint = true; _app.NextWave = -1; _app.Modal = ""; _app.UserPaused = false; _app.DockOpen = true;
            FleetStressFixture.ConfigureCatalog(Option("fixture-root", ProjectSettings.GlobalizePath("res://")));
            _fixture = new FleetStressFixture(_app.Game, _app.Battle, 5000, _enemyCount, _layout);
            _fixture.Setup(_mode == "patrol" ? FleetStressMode.Patrol : FleetStressMode.Combat);
            _app.Planet.RestoreSites(new object?[] { "" }.Concat(_fixture.Sites.Select(site => (object?)site.S("kind"))).ToArray(), new object?[] { Vector3.Forward }.Concat(_fixture.Sites.Select(site => (object?)site.Vector3("normal"))).ToArray());
            _app.Battle.Surface = _app.Planet;
            _app.Battle.InvalidateFactoryLayout();
            _fixture.MaintainPopulation();
            _app.Started = _mode != "patrol";
            _app.CaptureFrameTimings = true;
            _detailedProfiler = Option("profile-stages", "false") == "true";
            _app.Battle.Performance = _detailedProfiler ? _combatTimings : null;
            _app.Planet.MeasureRenderTime(true);
            RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
            _app.SyncRender();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            _app.SetProcess(true);
            _ready = true;
            GD.Print($"FLEET_NATIVE_START tag={_tag} mode={_mode} friendly={_app.Battle.Drones.Count} enemies={_app.Battle.Enemies.Count} parts={_app.Planet.RenderedFleetPartCount} batches={_app.Planet.RenderedFleetBatchCount}");
        }
        catch (Exception error) { GD.PrintErr("FLEET_NATIVE_FAIL: " + error); GetTree().Quit(1); }
    }
    public override void _Process(double delta)
    {
        if (!_ready || _finished) return;
        long frameStamp = Stopwatch.GetTimestamp();
        double wallMs = _lastFrameStamp == 0 ? delta * 1000 : Stopwatch.GetElapsedTime(_lastFrameStamp, frameStamp).TotalMilliseconds;
        _lastFrameStamp = frameStamp;
        _tick++;
        if (_tick == _warmup)
        {
            _simulationStart = _app.Battle.Clock; _firstAllocated = GC.GetTotalAllocatedBytes(true);
            for (int g = 0; g < 3; g++) _firstCollections[g] = GC.CollectionCount(g);
            _firstKills = _app.Game.Kills; _firstLosses = _app.Battle.DestroyedDrones; _firstDamage = _app.Battle.NumberSequence;
        }
        if (_tick > _warmup)
        {
            Record("frame_ms", wallMs);
            Record("engine_delta_ms", delta * 1000);
            Record("main_ms", _app.LastMainProcessMs);
            Record("main_hud_ms", _app.LastHudDrawMs);
            Record("combat_hud_ms", _app.BattleHud.LastDrawMs);
            Record("planet_process_ms", _app.Planet.LastPlanetProcessMs);
            Record("render_drones_ms", _app.Planet.LastDroneSyncMs);
            Record("render_enemies_ms", _app.Planet.LastEnemySyncMs);
            Record("render_projectiles_ms", _app.Planet.LastProjectileSyncMs);
            Record("render_effects_ms", _app.Planet.LastEffectsSyncMs);
            foreach (var stage in Enum.GetValues<Earthward.Combat.CombatStage>()) Record("combat_" + stage.ToString(), _combatTimings.Milliseconds(stage));
            Record("combat_ms", _app.LastCombatStepMs);
            Record("sync_ms", _app.LastRenderSyncMs);
            Record("world_gpu_ms", _app.Planet.LastViewportGpuMs);
            Record("world_render_cpu_ms", _app.Planet.LastViewportCpuMs);
            Record("native_frame_setup_ms", RenderingServer.GetFrameSetupTimeCpu());
            Record("root_gpu_ms", RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid()));
            _maxShots = Math.Max(_maxShots, _app.Battle.Shots.Count + _app.Battle.HostileShots.Count);
        }
        long start = Stopwatch.GetTimestamp();
        _fixture.MaintainPopulation();
        if (_tick > _warmup) Record("maintenance_ms", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        _minFriendly = Math.Min(_minFriendly, _app.Battle.Drones.Count);
        if (_tick >= _warmup + _sampleFrames)
        {
            _finished = true;
            Callable.From(Finish).CallDeferred();
        }
    }
    private async void Finish()
    {
        try
        {
            _app.SetProcess(false);
            var metrics = new DataMap();
            foreach (var (key, values) in _samples)
            {
                values.Sort(); metrics[key] = new DataMap { ["mean"] = values.Average(), ["p50"] = values[values.Count / 2], ["p95"] = values[Math.Min(values.Count - 1, (int)(values.Count * .95))], ["worst"] = values[^1] };
            }
            var report = new DataMap
            {
                ["tag"] = _tag, ["mode"] = _mode, ["layout"] = _layout.ToString().ToLowerInvariant(),
                ["aircraft_with_targets_current"] = _fixture.AircraftWithTargets, ["aircraft_with_targets_peak"] = _fixture.PeakAircraftWithTargets,
                ["aircraft_lives_observed_firing"] = _fixture.ObservedFiringLives, ["factories_observed_engaged"] = _fixture.ObservedEngagedFactories,
                ["frozen_compiled_profiles"] = _fixture.FrozenProfilesLoaded, ["stats_hash"] = _fixture.StatsHash, ["seed"] = (long)FleetStressFixture.Seed, ["friendly_target"] = 5000, ["enemy_target"] = _enemyCount,
                ["minimum_friendly_after_maintenance"] = _minFriendly, ["frames"] = _sampleFrames, ["warmup_frames"] = _warmup,
                ["resolution"] = "1440x900", ["earth_radius"] = Earthward.WorldScale.EarthRadius,
                ["enemy_kills"] = _app.Game.Kills - _firstKills, ["friendly_losses"] = _app.Battle.DestroyedDrones - _firstLosses,
                ["damage_events"] = _app.Battle.NumberSequence - _firstDamage, ["peak_projectiles"] = _maxShots,
                ["native_batches"] = _app.Planet.RenderedFleetBatchCount, ["prototype_parts"] = _app.Planet.RenderedFleetPartCount,
                ["timings"] = metrics,
                ["detailed_profiler"] = _detailedProfiler,
                ["earthward_jit_disabled"] = typeof(Earthward.Combat.Battlefield).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false).OfType<DebuggableAttribute>().Any(a => a.IsJITOptimizerDisabled),
                ["godotsharp_jit_disabled"] = typeof(Vector3).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false).OfType<DebuggableAttribute>().Any(a => a.IsJITOptimizerDisabled),
                ["godotsharp_path"] = typeof(Vector3).Assembly.Location,
                ["simulated_seconds"] = _app.Battle.Clock - _simulationStart,
                ["allocated_bytes_all_threads"] = GC.GetTotalAllocatedBytes(true) - _firstAllocated,
                ["gc_server"] = System.Runtime.GCSettings.IsServerGC,
                ["gc_latency"] = System.Runtime.GCSettings.LatencyMode.ToString(),
                ["gc_collections"] = Enumerable.Range(0,3).Select(g => (object?)(GC.CollectionCount(g) - _firstCollections[g])).ToList(),
                ["render_size"] = _app.Planet.GetRenderSize().ToString(),
                ["notes"] = "Actual Main._Process and Forward+ rendering. Native frame time includes benchmark population maintenance; combat_ms excludes maintenance. Full simulation population remains active."
            };
            string layoutSuffix = _layout == FleetStressLayout.World ? "-world" : "";
            string basePath = ProjectSettings.GlobalizePath($"res://artifacts/fleet127-native-{_tag}{layoutSuffix}-{_mode}-{_enemyCount}");
            System.IO.File.WriteAllText(basePath + ".json", report.ToJson());
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var capture = GetViewport().GetTexture().GetImage(); capture.SavePng(basePath + ".png");
            GD.Print("FLEET_NATIVE_RESULT " + report.ToJson());
            _app.QueueFree();
            for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
            GetTree().Quit(_minFriendly == 5000 ? 0 : 1);
        }
        catch (Exception error) { GD.PrintErr("FLEET_NATIVE_FAIL: " + error); GetTree().Quit(1); }
    }
}
