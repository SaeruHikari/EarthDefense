using System;
using System.Linq;
using Earthward.Domain;
namespace Earthward.Combat;
/// <summary>The live Earth continuation from the retired expedition director; remote campaign records stay archived.</summary>
public sealed class DefenseCampaignDirector
{
    public DefenseState Game { get; private set; } = null!; public Battlefield Battle { get; private set; } = null!;
    public bool Paused
    {
        get; set;
    }
    public double SpeedScale { get; set; } = 1;
    private bool _enabled, _ceasefireInitialized; private double _clock, _earthTimer = 20, _ceasefireDuration, _roundSeconds = 30, _baselineHealth = 120; private long _earthWave, _baselineWave = 1, _nextUid = 2000001; private string _phase = "standby", _notice = ""; private DataMap _bases = new();
    public event Action? StateChanged, CheckpointRequested; public event Action<string>? EventNotice;
    public DefenseCampaignDirector()
    {
    }
    public DefenseCampaignDirector(DefenseState game, Battlefield battle)
    {
        Setup(game, battle);
    }
    public void Setup(DefenseState game, Battlefield battle)
    {
        if (Battle != null)
            Battle.PostDefenseWaveCompleted -= WaveCompleted;
        Game = game;
        Battle = battle;
        Battle.PostDefenseWaveCompleted += WaveCompleted;
        SyncCampaign();
    }
    public void ResetRuntime()
    {
        _enabled = false;
        _clock = 0;
        _earthWave = 0;
        _earthTimer = 20;
        _phase = "standby";
        _ceasefireInitialized = false;
        _ceasefireDuration = 0;
        _roundSeconds = 30;
        _baselineHealth = 120;
        _baselineWave = 1;
        _nextUid = 2000001;
        _bases = new();
        _notice = "";
    }
    public void Reset()
    {
        ResetRuntime();
        SyncCampaign();
    }
    public bool OwnsEarthSchedule() => _enabled;
    public void SyncCampaign()
    {
        if (Game == null || Battle == null)
            return;
        if (Battle.GetDestroyedFronts().Count == 8)
            Game.Expedition.SetEarthLiberated(true);
        if (!Game.Expedition.EarthLiberated)
            return;
        if (Game.GetDefenseReachStage() == 0)
            Game.SetDefenseReachStage(1);
        if (!_enabled)
        {
            _enabled = true;
            _baselineWave = Math.Max(1, Game.Wave);
            _baselineHealth = Battle.EnemyMaxHealth("scout", _baselineWave);
            Battle.EnablePostDefense();
        }
        if (!_ceasefireInitialized)
        {
            _ceasefireInitialized = true;
            _roundSeconds = Game.CombatSettings.N("enemy_wave_duration", 45);
            _ceasefireDuration = Game.Expedition.Settings.N("earth_ceasefire_rounds", 5) * _roundSeconds;
            _earthTimer = _ceasefireDuration;
            _phase = "ceasefire";
            Battle.ClearPostDefenseAttack();
            CheckpointRequested?.Invoke();
        }
    }
    public void Step(double delta)
    {
        if (Game == null)
            return;
        SyncCampaign();
        if (Paused || !_enabled || Game.EarthHp <= 0)
            return;
        double remaining = Math.Max(0, delta) * Math.Max(0, SpeedScale);
        while (remaining > .0000001)
        {
            double dt = Math.Min(remaining, 1d / 30);
            _clock += dt;
            UpdateEarthSchedule(dt);
            remaining -= dt;
        }
    }
    private void UpdateEarthSchedule(double dt)
    {
        if (Battle.WaveRunning)
            return;
        _earthTimer = Math.Max(0, _earthTimer - dt);
        if (_earthTimer > .000001)
            return;
        var settings = Game.Expedition.Settings;
        long next = _earthWave + 1;
        int count = (int)Math.Min(512, settings.L("earth_carrier_base_count", 2) + settings.L("earth_carrier_wave_growth", 1) * (next - 1));
        int stage = Math.Clamp(Game.GetDefenseReachStage(), 1, 3);
        var plan = new DataMap { ["id"] = $"continuing:{next}", ["wave"] = next, ["carrier_count"] = count, ["aircraft_per_carrier"] = settings.I("earth_escort_count", 12), ["duration"] = Game.CombatSettings.N("enemy_wave_duration", 45), ["unit_health"] = _baselineHealth * (1 + settings.N("earth_health_growth", .1) * (next - 1)), ["damage_multiplier"] = 1 + settings.N("earth_damage_growth", .06) * (next - 1), ["volley_count"] = (int)CombatCatalog.Current.Values.ContinuingVolleyCount, ["hangar_interval"] = CombatCatalog.Current.Values.ContinuingHangarInterval, ["defense_stage"] = stage, ["frontier_radius"] = Game.CombatSettings.N("frontier_radius_" + stage, CombatScale.DefaultFrontierRadius(stage)) };
        plan["carrier_health"] = plan.N("unit_health") * CombatCatalog.Current.Values.ContinuingCarrierHealthMultiplier;
        if (Battle.StartPostDefenseWave(plan))
        {
            _earthWave = next;
            _phase = "continuing";
        }
        else
            _earthTimer = 1;
    }
    private void WaveCompleted(string id)
    {
        var plan = Battle.PostPlan;
        if (id != plan.S("id") || Battle.FixedCycleRunning || plan.B("completion_handled"))
            return;
        plan["completion_handled"] = true;
        bool relocating = false;
        if (plan.ContainsKey("defense_stage"))
        {
            int previous = plan.I("defense_stage"), next = Math.Min(3, previous + 1);
            relocating = next > previous;
            Game.SetDefenseReachStage(next);
            Tell(relocating ? "母舰已全部清除 · 一波后敌军转移至更远阵地，请准备新的航程" : "深空母舰已全部清除 · 防御持续");
        }
        // Only a change of frontier gets a one-cycle preparation window.
        // Repeated reinforcements at the final frontier keep their original cadence.
        _earthTimer = relocating ? Game.CombatSettings.N("enemy_wave_duration", 45) : 0;
        _phase = "resupply";
        CheckpointRequested?.Invoke();
    }
    private void Tell(string text)
    {
        _notice = text;
        EventNotice?.Invoke(text);
        StateChanged?.Invoke();
    }
    public DataMap GetFrontierWarning()
    {
        int stage = Math.Clamp(Game.GetDefenseReachStage(), 1, 3);
        bool moving = _phase == "ceasefire" || _phase == "resupply" && stage > Battle.PostPlan.I("defense_stage", stage);
        bool pending = _enabled && moving && !Battle.WaveRunning && _earthTimer > .000001 && Game.EarthHp > 0;
        return new()
        {
            ["pending"] = pending,
            ["next_stage"] = stage,
            ["target_radius"] = Game.CombatSettings.N("frontier_radius_" + stage, CombatScale.DefaultFrontierRadius(stage)),
            ["remaining"] = pending ? _earthTimer : 0,
            ["window_duration"] = Game.CombatSettings.N("enemy_wave_duration", 45),
            ["transition_id"] = $"frontier:{stage}:{_earthWave + 1}"
        };
    }
    public DataMap GetStatus()
    {
        double remaining = _phase == "ceasefire" ? _earthTimer : 0;
        return new()
        {
            ["enabled"] = _enabled,
            ["research_unlocked"] = false,
            ["satellite_deployed"] = false,
            ["telescope_satellite"] = new DataMap { ["deployed"] = false },
            ["telescope_researched"] = false,
            ["earth_phase"] = _phase,
            ["earth_wave"] = _earthWave,
            ["earth_remaining"] = _earthTimer,
            ["earth_next_carriers"] = Math.Min(512, Game.Expedition.Settings.L("earth_carrier_base_count", 2) + Game.Expedition.Settings.L("earth_carrier_wave_growth", 1) * _earthWave),
            ["ceasefire_remaining"] = remaining,
            ["ceasefire_duration"] = _ceasefireDuration,
            ["ceasefire_rounds_remaining"] = (long)Math.Ceiling(remaining / Math.Max(1, _roundSeconds)),
            ["sectors"] = new System.Collections.Generic.List<object?>(),
            ["notice"] = _notice,
            ["frontier"] = Battle.FrontierCohortStatus(),
            ["frontier_warning"] = GetFrontierWarning(),
            ["defense_reach_stage"] = Game.GetDefenseReachStage()
        };
    }
    public DataMap Serialize() => new() { ["version"] = 4, ["earth"] = Battle.SerializeCombatSnapshot(), ["payload"] = CombatSnapshotCodec.Encode(new DataMap { ["_enabled"] = _enabled, ["_clock"] = _clock, ["_earth_wave"] = _earthWave, ["_earth_timer"] = _earthTimer, ["_earth_phase"] = _phase, ["_baseline_health"] = _baselineHealth, ["_baseline_wave"] = _baselineWave, ["_next_uid"] = _nextUid, ["_bases"] = _bases, ["_ceasefire_initialized"] = _ceasefireInitialized, ["_ceasefire_duration"] = _ceasefireDuration, ["_ceasefire_round_seconds"] = _roundSeconds }) };
    /// <summary>Read-only capture at Battlefield.WaveStarted, before the scheduling call has returned.</summary>
    public DataMap SerializeWaveStart()
    {
        var snapshot = Serialize();
        if (_enabled && Battle.IsPostDefenseActive() && Battle.FixedCycleRunning
            && CombatSnapshotCodec.TryDecode(snapshot.Value("payload"), out var decoded) && decoded is DataMap payload)
        {
            // StartPostDefenseWave opens the battle before UpdateEarthSchedule commits these two fields.
            payload["_earth_wave"] = Math.Max(_earthWave, Battle.PostPlan.L("wave", _earthWave));
            payload["_earth_phase"] = "continuing";
            payload["_earth_timer"] = 0d;
            snapshot["payload"] = CombatSnapshotCodec.Encode(payload);
        }
        return snapshot;
    }

    public static bool ValidateSnapshot(DataMap value)
    {
        if (!CombatSnapshotCodec.IsInteger(value.Value("version")) || value.I("version") != 4 || value.Count != 3 || value.Value("earth") is not DataMap earth || !Battlefield.ValidateCombatSnapshot(earth) || !CombatSnapshotCodec.TryDecode(value.Value("payload"), out var decoded) || decoded is not DataMap d)
            return false;
        if (d.Count != 12)
            return false;
        if (!CombatSnapshotCodec.HasFields(d, "_enabled:bool _clock:number _earth_wave:int _earth_timer:number _earth_phase:text _baseline_health:number _baseline_wave:int _next_uid:int _bases:map") || d.N("_clock") < 0 || d.L("_earth_wave") < 0 || d.L("_baseline_wave") < 1 || d.N("_baseline_health") <= 0 || d.L("_next_uid") < 2000001)
            return false;
        if (!CombatSnapshotCodec.HasFields(d, "_ceasefire_round_seconds:number") || d.N("_ceasefire_round_seconds") <= 0)
            return false;
        if (!new[] { "standby", "ceasefire", "continuing", "resupply" }.Contains(d.S("_earth_phase")) || !CombatSnapshotCodec.HasFields(d, "_ceasefire_initialized:bool _ceasefire_duration:number") || d.N("_ceasefire_duration") < 0 || d.N("_earth_timer") < 0 || d.S("_earth_phase") == "ceasefire" && (!d.B("_ceasefire_initialized") || d.N("_earth_timer") > d.N("_ceasefire_duration") + .000001) || d.B("_enabled") && !d.B("_ceasefire_initialized"))
            return false;
        var seen = new System.Collections.Generic.HashSet<long>();
        foreach (var b in d.Map("_bases").Values)
        {
            if (b is not DataMap row || !CombatSnapshotCodec.HasFields(row, "uid:int kind:text visual_kind:text sector_id:text local_normal:v3 space_position:v3 velocity:v3 basis:basis tangent:v3 world_up:v3 hp:number max_hp:number hit_radius:number destroyed:bool production_clock:number phase:number hit:number") || !new[] { "hive", "barracks" }.Contains(row.S("kind")) || !new[] { "moon", "mercury", "venus", "mars", "europa", "titan" }.Contains(row.S("sector_id")) || row.L("uid") < 2000001 || row.L("uid") >= d.L("_next_uid") || !seen.Add(row.L("uid")) || row.N("hp") < 0 || row.N("max_hp") <= 0 || row.N("hp") > row.N("max_hp"))
                return false;
        }
        return true;
    }
    public bool Restore(DataMap value)
    {
        if (!ValidateSnapshot(value))
            return false;
        CombatSnapshotCodec.TryDecode(value.Value("payload"), out var decoded);
        var d = (DataMap)decoded!;
        if (!Battle.RestoreCombatSnapshot(value.Map("earth")))
            return false;
        _enabled = d.B("_enabled");
        _clock = d.N("_clock");
        _earthWave = d.L("_earth_wave");
        _earthTimer = d.N("_earth_timer");
        _phase = d.S("_earth_phase");
        _baselineHealth = d.N("_baseline_health");
        _baselineWave = d.L("_baseline_wave");
        _nextUid = d.L("_next_uid");
        _bases = d.Map("_bases");
        _ceasefireInitialized = d.B("_ceasefire_initialized");
        _ceasefireDuration = d.N("_ceasefire_duration");
        _roundSeconds = d.N("_ceasefire_round_seconds");
        SyncCampaign();
        return true;
    }
}
