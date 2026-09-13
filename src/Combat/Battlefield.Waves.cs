using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private void EnsureInvasionAnchor()
    {
        if (_invasion.AnchorConfigured)
            return;
        var first = Factories.Values.FirstOrDefault(f => C.S(f, "kind") == "interceptor");
        _invasion.ConfigureAnchor(first == null ? Vector3.Back : SurfaceToSpace(C.V(C.M(first, "site"), "normal"), 0).Normalized());
    }
    public Vector3 GetInvasionAnchor()
    {
        EnsureInvasionAnchor();
        return _invasion.Anchor;
    }
    public void RestoreInvasionAnchor(Vector3 normal)
    {
        if (!normal.IsFinite() || normal.LengthSquared() < .0001)
            return;
        _invasion = new();
        _invasion.ConfigureAnchor(normal);
        Motherships.Clear();
        _motherWave = -1;
        InvasionWon = false;
    }
    public List<string> GetDestroyedFronts() => _invasion.GetDestroyedFronts();
    public bool RestoreDestroyedFronts(IEnumerable<string> values)
    {
        var list = values.Cast<object?>().ToList();
        if (!_invasion.RestoreDestroyedFronts(list))
            return false;
        Motherships.Clear();
        _motherWave = -1;
        InvasionWon = list.Count == 8;
        SyncMotherships();
        return true;
    }
    public List<DataMap> GetInvasionFronts()
    {
        EnsureInvasionAnchor();
        var fronts = _invasion.FrontsForWave(Game.Wave);
        foreach (var f in fronts)
            if (Motherships.TryGetValue(C.S(f, "id"), out var mother))
            {
                f["hp"] = mother["hp"];
                f["max_hp"] = mother["max_hp"];
            }
        return fronts;
    }
    public DataMap GetNextInvasionFront() => _invasion.NextFrontForWave(Game.Wave);
    private void SyncMotherships()
    {
        if (_postDefense)
        {
            Motherships.Clear();
            RebuildTargets();
            return;
        }
        EnsureInvasionAnchor();
        var fronts = _invasion.FrontsForWave(Game.Wave);
        var visible = new HashSet<string>();
        foreach (var f in fronts)
        {
            string id = C.S(f, "id");
            visible.Add(id);
            if (Motherships.ContainsKey(id))
                continue;
            var mother = new DataMap { ["uid"] = -100000L - C.L(f, "ordinal"), ["front_id"] = id, ["kind"] = "mothership", ["space_position"] = f["world_position"], ["velocity"] = Vector3.Zero, ["tangent"] = -C.V(f, "direction"), ["hit_radius"] = .65, ["size"] = 48d, ["phase"] = "carrier", ["hit"] = 0d };
            EnemyCatalog.Apply(mother, new()
            {
                ["wave"] = Game.Wave,
                ["stage"] = 0
            }, Game.CombatSettings);
            Motherships[id] = mother;
        }
        foreach (var id in Motherships.Keys.ToArray())
            if (!visible.Contains(id))
                Motherships.Remove(id);
        _motherWave = Game.Wave;
        RebuildTargets();
    }
    public void StartWave()
    {
        if (Game == null || Dead || _fixedRunning)
            return;
        RefreshConfiguration();
        SyncFleet();
        long next = _postDefense ? Game.Wave + 1 : _invasion.NextAvailableWave(Game.Wave + 1);
        if (next < 0)
        {
            DeclareVictory();
            return;
        }
        OpenWave(next);
    }
    private void OpenWave(long wave)
    {
        Game.Wave = wave;
        Active = true;
        WaveRunning = true;
        _fixedRunning = true;
        _cycleElapsed = 0;
        SyncMotherships();
        _wavePlan = MakeWavePlan(wave);
        WaveTotal = C.L(_wavePlan, "planned_count");
        WaveRemaining = WaveTotal;
        _spawnDuration = C.N(_wavePlan, "duration");
        _spawnElapsed = 0;
        _spawned = 0;
        _initialCount = WaveTotal;
        _spawnCancelled = false;
        _segmentStart = 0;
        _segmentCount = WaveTotal;
        _segmentSpawned = 0;
        _spawnClock = _spawnDuration / Math.Max(1, WaveTotal);
        SyncFleet();
        WaveStarted?.Invoke(wave, GetWaveSpawnPlan());
        EventNotice?.Invoke($"第{wave:00}波 · {_spawnDuration:0}秒部署窗口，{C.N(_wavePlan, "cycle_duration"):0}秒防御周期");
    }
    private DataMap MakeWavePlan(long wave)
    {
        var settings = Game.CombatSettings;
        double duration = settings.N("enemy_spawn_duration", 30), cycle = Math.Max(duration, settings.N("enemy_wave_duration", 45)), multiplier = settings.N("enemy_count_multiplier", 2);
        long basis = settings.L("enemy_wave_base_count", 10), growth = settings.L("enemy_wave_growth", 2), count = _invasion.WaveBudget(wave, basis, growth, multiplier);
        if (wave == 1)
            count = Math.Max(1, (long)Math.Ceiling(count * Math.Clamp(settings.N("first_wave_enemy_count_multiplier", 1), .25, 1)));
        int carriers = 0, stage = 0;
        if (_postDefense)
        {
            stage = C.I(_postPlan, "defense_stage", 1);
            carriers = Math.Max(0, C.I(_postPlan, "carrier_count") - _postSpawned);
            int live = FrontierCohortStatus().I("alive") + carriers;
            count = (long)Math.Ceiling((basis + growth * (double)Math.Max(0, wave - 1)) * multiplier * live / Math.Max(1, C.I(_postPlan, "carrier_count", 1)));
        }
        var plan = DefenseWavePlan.Build(wave, count, duration, cycle, stage, carriers);
        plan["base_count"] = basis;
        plan["growth"] = growth;
        plan["count_multiplier"] = multiplier;
        return plan;
    }
    public DataMap PreviewNextWaveSpawnPlan()
    {
        EnsureInvasionAnchor();
        return MakeWavePlan(_postDefense ? Game.Wave + 1 : _invasion.NextAvailableWave(Game.Wave + 1));
    }
    public DataMap GetWaveSpawnPlan()
    {
        var plan = C.Shallow(_wavePlan);
        plan["wave"] = C.L(plan, "wave", Game.Wave);
        plan["duration"] = _spawnDuration;
        plan["planned_count"] = WaveTotal;
        plan["initial_planned_count"] = _initialCount;
        plan["spawned"] = _spawned;
        plan["remaining"] = WaveRemaining;
        plan["elapsed"] = _spawnElapsed;
        plan["spawn_finished"] = _spawnDuration <= 0 || _spawnCancelled || InvasionWon || _spawnElapsed >= _spawnDuration;
        plan["cancelled"] = _spawnCancelled;
        plan["cycle_duration"] = C.N(_wavePlan, "cycle_duration", 45);
        plan["cycle_elapsed"] = _cycleElapsed;
        return plan;
    }
    public void AdvanceFixedSchedule(double delta)
    {
        double remaining = Math.Max(0, delta);
        while (_fixedRunning && remaining > 1e-7 && !Dead)
        {
            double step = Math.Min(remaining, Math.Max(.000001, C.N(_wavePlan, "cycle_duration", 45) - _cycleElapsed));
            UpdateSpawning(step);
            _cycleElapsed += step;
            remaining -= step;
            CheckWaveEnd();
        }
    }
    private void CancelSpawnWindow()
    {
        _spawnCancelled = true;
        WaveRemaining = 0;
        WaveTotal = _spawned;
        _segmentCount = _segmentSpawned;
        _spawnClock = 0;
    }
    public void UpdateSpawning(double dt)
    {
        if (_spawnDuration <= 0 || _spawnCancelled || InvasionWon)
            return;
        _spawnElapsed = Math.Min(_spawnDuration, _spawnElapsed + Math.Max(0, dt));
        if (_spawnElapsed >= _spawnDuration - 1e-7)
            _spawnElapsed = _spawnDuration;
        if (WaveRemaining <= 0)
        {
            _spawnClock = 0;
            return;
        }
        if (!_postDefense && _invasion.FrontsForWave(Game.Wave).Count == 0)
        {
            CancelSpawnWindow();
            return;
        }
        double duration = Math.Max(1e-7, _spawnDuration - _segmentStart), progress = C.Clamp((_spawnElapsed - _segmentStart) / duration, 0, 1);
        long due = Math.Min(_segmentCount, (long)Math.Floor(progress * _segmentCount + 1e-7));
        while (_segmentSpawned < due && WaveRemaining > 0)
        {
            var entry = DefenseWavePlan.Entry(_wavePlan, _spawned);
            if (!SpawnPlannedEntry(entry))
            {
                CancelSpawnWindow();
                break;
            }
            _segmentSpawned++;
            _spawned++;
            WaveRemaining--;
        }
        _spawnClock = WaveRemaining > 0 ? Math.Max(0, _segmentStart + duration * (_segmentSpawned + 1) / Math.Max(1, _segmentCount) - _spawnElapsed) : 0;
    }
    private void CheckWaveEnd()
    {
        if (!_fixedRunning || Dead || _cycleElapsed + .000001 < C.N(_wavePlan, "cycle_duration", 45))
            return;
        _fixedRunning = false;
        Game.RewardWave();
        WaveCompleted?.Invoke();
        if (_postDefense && _cohortComplete)
        {
            WaveRunning = false;
            PostDefenseWaveCompleted?.Invoke(C.S(_postPlan, "id"));
            return;
        }
        if (!InvasionWon)
            OpenWave(Game.Wave + 1);
    }
    public DataMap? SpawnEnemy(string kind, DataMap? planned = null, Vector3 originOverride = default)
    {
        if (kind is not ("scout" or "carrier")) throw new ArgumentException("Unknown enemy kind: " + kind, nameof(kind));
        EnsureInvasionAnchor();
        var front = originOverride.LengthSquared() > .01 ? new DataMap { ["position"] = originOverride, ["front_id"] = "outer", ["front_name"] = "" } : _invasion.SpawnPoint(Math.Max(1, Game.Wave), Random);
        if (front.Count == 0)
        {
            WaveRemaining = 0;
            return null;
        }
        var position = C.V(front, "position");
        var target = FacilitySpaceTarget(position);
        var kindData = CombatCatalog.Current.Kind(kind);
        double size = kindData.Size;
        double baseSpeed = (CombatCatalog.Current.Values.LegacySpawnSpeedBase + Game.Wave * CombatCatalog.Current.Values.LegacySpawnSpeedWaveGrowth) * kindData.LegacySpeed;
        double speed = baseSpeed * GetEnemySpeedMultiplier();
        var direction = -position.Normalized();
        double health = EnemyMaxHealth(kind);
        var enemy = new DataMap { ["uid"] = NewUid(), ["space_position"] = position, ["velocity"] = C.Scale(direction, speed), ["tangent"] = direction, ["target_space"] = target, ["kind"] = kind, ["speed"] = speed, ["base_speed"] = baseSpeed, ["phase"] = "approach", ["bombard_time"] = 0d, ["bombard_duration"] = kindData.BombardSeconds, ["hp"] = health, ["max_hp"] = health, ["base_size"] = size, ["size"] = size * GetEnemyScale(), ["hit_radius"] = size * 2 / CombatScale.PlanetPixelRadius * GetEnemyScale(), ["wave"] = Game.Wave, ["hit"] = 0d, ["age"] = 0d, ["fire"] = .45 };
        var metadata = planned ?? new()
        {
            ["wave"] = Game.Wave,
            ["stage"] = 0,
            ["role"] = kind == "scout" ? "claw" : ""
        };
        EnemyCatalog.Apply(enemy, metadata, Game.CombatSettings);
        enemy["front_id"] = front["front_id"];
        enemy["front_name"] = front["front_name"];
        enemy["spawn_radius"] = (double)position.Length();
        enemy["combat_entry_radius"] = DefenseEntryRadius(position);
        Enemies.Add(enemy);
        _enemyById[C.L(enemy, "uid")] = enemy;
        _targets.Add(enemy);
        _sectorsDirty = true;
        _assignmentClock = 0;
        return enemy;
    }
    private bool SpawnPlannedEntry(DataMap entry)
    {
        string kind = C.S(entry, "kind");
        if (_postDefense && kind == "carrier")
        {
            var mother = SpawnPostCarrier();
            EnemyCatalog.Apply(mother, entry, Game.CombatSettings);
            mother["wave"] = C.L(entry, "wave");
            mother["frontier_radius"] = C.N(_postPlan, "frontier_radius");
            _postSpawned++;
            return true;
        }
        var origin = Vector3.Zero;
        if (_postDefense)
        {
            var mothers = Enemies.Where(e => C.B(e, "post_carrier") && C.N(e, "hp") > 0).ToList();
            if (mothers.Count == 0)
                return false;
            var owner = mothers[(int)(C.L(entry, "index") % mothers.Count)];
            origin = _invasion.FrontierAircraftSpawn(C.V(owner, "space_position"), Random);
            owner["hangar_open"] = .7;
        }
        var enemy = SpawnEnemy(kind, entry, origin);
        if (enemy == null)
            return false;
        enemy["wave"] = C.L(entry, "wave");
        return true;
    }
    private Vector3 FacilitySpaceTarget(Vector3 origin)
    {
        var target = C.Scale(origin.Normalized(), CombatScale.EarthRadius - .1);
        double best = double.NegativeInfinity;
        if (Surface != null)
            foreach (var normal in Surface.GetOccupiedSurfaceNormals())
            {
                var site = SurfaceToSpace(normal, .018);
                double facing = origin.Normalized().Dot(site.Normalized());
                if (facing > best)
                {
                    best = facing;
                    target = site;
                }
            }
        return target;
    }
    private double DefenseEntryRadius(Vector3 direction)
    {
        var radial = direction.Normalized();
        double radius = CombatScale.DefenseEntryRadius;
        foreach (var f in Factories.Values)
        {
            var profile = _factoryProfiles.GetValueOrDefault(C.L(f, "site_id"));
            if (profile == null)
                continue;
            if (SurfaceToSpace(C.V(C.M(f, "site"), "normal"), 0).Normalized().Dot(radial) >= profile.Cosine)
                radius = Math.Max(radius, CombatScale.EarthRadius + CombatScale.DroneAltitude + C.N(profile.Patrol, "patrol_outer_range"));
        }
        return Math.Min(direction.Length() - 1, radius);
    }
    public double EnemyMaxHealth(string kind, long waveValue = -1)
    {
        var prototype = new DataMap { ["kind"] = kind };
        EnemyCatalog.Apply(prototype, new()
        {
            ["wave"] = Math.Max(1, waveValue < 0 ? Game.Wave : waveValue),
            ["role"] = "claw",
            ["stage"] = 0
        }, Game.CombatSettings);
        return C.N(prototype, "max_hp");
    }
    public double GetEnemySpeedMultiplier() => Math.Max(.01, C.N(Game.CombatSettings, "enemy_speed_multiplier", 1.5));
    public void RefreshEnemyHealth()
    {
        foreach (var enemy in Enemies.Concat(Motherships.Values))
        {
            double previous = Math.Max(.001, C.N(enemy, "max_hp")), fraction = C.Clamp(C.N(enemy, "hp") / previous, 0, 1);
            var proto = new DataMap { ["kind"] = C.S(enemy, "kind") };
            EnemyCatalog.Apply(proto, new()
            {
                ["wave"] = C.L(enemy, "wave", Game.Wave),
                ["stage"] = C.I(enemy, "defense_stage"),
                ["role"] = "claw"
            }, Game.CombatSettings);
            enemy["max_hp"] = proto["max_hp"];
            enemy["hp"] = C.N(proto, "max_hp") * fraction;
            foreach (var key in new[] { "attack_cooldown", "attack_damage", "ground_damage", "tactical_speed", "speed", "base_speed" })
                enemy[key] = proto[key];
            if (C.S(enemy, "kind") != "mothership")
            {
                double size = C.N(enemy, "base_size", C.N(enemy, "size"));
                enemy["base_size"] = size;
                enemy["size"] = size * GetEnemyScale();
                enemy["hit_radius"] = size * 2 / CombatScale.PlanetPixelRadius * GetEnemyScale();
            }
        }
        RefreshEnemyMovement();
    }
    public void RefreshEnemyMovement()
    {
        foreach (var enemy in Enemies)
        {
            enemy["speed"] = C.N(enemy, "base_speed", C.N(enemy, "speed") / GetEnemySpeedMultiplier()) * GetEnemySpeedMultiplier();
            enemy["tactical_speed"] = enemy["speed"];
        }
    }
    public bool IsPostDefenseActive() => _postDefense;
    public bool FixedCycleRunning => _fixedRunning;
    public DataMap PostPlan => _postPlan;
    public void EnablePostDefense()
    {
        _postDefense = true;
        InvasionWon = false;
        Active = true;
    }
    public bool StartPostDefenseWave(DataMap plan)
    {
        if (Game == null || Dead || _fixedRunning || C.S(plan, "id") == "" || C.N(plan, "frontier_radius") < CombatScale.EarthRadius + 6 || C.N(plan, "frontier_radius") > 1000 + CombatScale.EarthRadiusDelta || C.I(plan, "carrier_count") < 1 || C.I(plan, "carrier_count") > 512)
            return false;
        EnablePostDefense();
        _postPlan = plan.DeepClone();
        _postSpawned = 0;
        _cohortComplete = false;
        _postPlan.TryAdd("sortie_elapsed", 0d);
        _postPlan.TryAdd("sortie_started", true);
        _postPlan.TryAdd("sortie_round", 0);
        _postPlan.TryAdd("completion_handled", false);
        OpenWave(Game.Wave + 1);
        return true;
    }
    private DataMap SpawnPostCarrier()
    {
        var normal = C.Vec(Random.Range(-1d, 1d), Random.Range(-.55, .55), Random.Range(-1d, 1d)).Normalized();
        if (normal.LengthSquared() < .5)
            normal = Vector3.Back;
        var position = C.Scale(normal, Random.Range(CombatScale.EarthRadius + 4.2, CombatScale.EarthRadius + 5.1));
        position = C.V(_invasion.FrontierSpawnPoint(_postSpawned, C.I(_postPlan, "carrier_count"), C.N(_postPlan, "frontier_radius"), Random), "position");
        var heading = -position.Normalized();
        double health = C.N(_postPlan, "carrier_health", 600);
        var e = new DataMap { ["uid"] = NewUid(), ["kind"] = "carrier", ["space_position"] = position, ["velocity"] = Vector3.Zero, ["tangent"] = heading, ["world_up"] = position.Normalized(), ["target_space"] = FacilitySpaceTarget(position), ["speed"] = .31 * GetEnemySpeedMultiplier(), ["base_speed"] = .31, ["phase"] = "approach", ["bombard_time"] = 0d, ["bombard_duration"] = 16d, ["hp"] = health, ["max_hp"] = health, ["base_size"] = 48d, ["size"] = 30d, ["hit_radius"] = .46, ["wave"] = Game.Wave, ["hit"] = 0d, ["age"] = 0d, ["fire"] = 1d, ["post_carrier"] = true, ["post_wave_id"] = C.S(_postPlan, "id"), ["post_damage_multiplier"] = C.N(_postPlan, "damage_multiplier", 1), ["locked_volley_count"] = C.I(_postPlan, "volley_count", 1), ["hangar_remaining"] = 0, ["hangar_clock"] = .5, ["hangar_open"] = 0d, ["front_id"] = "post_defense", ["front_name"] = "", ["frontier_radius"] = (double)position.Length() };
        Enemies.Add(e);
        _targets.Add(e);
        _enemyById[C.L(e, "uid")] = e;
        _assignmentClock = 0;
        _sectorsDirty = true;
        return e;
    }
    public DataMap FrontierCohortStatus() => new() { ["stage"] = C.I(_postPlan, "defense_stage"), ["radius"] = C.N(_postPlan, "frontier_radius"), ["alive"] = Enemies.Count(e => C.B(e, "post_carrier") && C.N(e, "hp") > 0), ["pending"] = Math.Max(0, C.I(_postPlan, "carrier_count") - _postSpawned), ["sortie"] = C.I(_postPlan, "sortie_round"), ["cohort_id"] = C.S(_postPlan, "id") };
    public void ClearPostDefenseAttack()
    {
        _epoch++;
        Enemies.Clear();
        HostileShots.Clear();
        Shots.Clear();
        Beams.Clear();
        Bursts.Clear();
        DamageNumbers.Clear();
        Motherships.Clear();
        _targets.Clear();
        _enemyById.Clear();
        _enemySectors.Clear();
        _sectorKeys.Clear();
        _sectorsDirty = true;
        _postPlan = new();
        _postSpawned = 0;
        WaveRunning = false;
        _fixedRunning = false;
        _cycleElapsed = 0;
        _cohortComplete = false;
        WaveRemaining = WaveTotal = 0;
        _wavePlan = new();
        _spawnDuration = _spawnElapsed = 0;
        _spawned = _initialCount = 0;
        _spawnCancelled = true;
        _segmentStart = 0;
        _segmentCount = _segmentSpawned = 0;
        _spawnClock = 0;
        foreach (var d in Drones)
        {
            d["target_uid"] = -1L;
            d["aim_target_uid"] = -1L;
            if (C.S(d, "state") is "engaging" or "suiciding")
                d["state"] = "patrol";
        }
        EnablePostDefense();
    }
    private void DestroyMothership(DataMap vessel)
    {
        string id = C.S(vessel, "front_id");
        if (!_invasion.DestroyFront(id))
            return;
        SpawnEnemyLoot(vessel);
        AddBurst(C.V(vessel, "space_position"), CombatScale.Coral, 125);
        Motherships.Remove(id);
        SyncMotherships();
        EventNotice?.Invoke("外星母舰已击毁 · 该方向停止增援 · 点击物资回收");
        if (WaveRunning)
        {
            int alive = _invasion.FrontsForWave(Game.Wave).Count;
            if (alive <= 0)
                CancelSpawnWindow();
            else
            {
                WaveRemaining = (long)Math.Floor(WaveRemaining * (double)alive / (alive + 1));
                WaveTotal = _spawned + WaveRemaining;
                _segmentStart = _spawnElapsed;
                _segmentCount = WaveRemaining;
                _segmentSpawned = 0;
                _spawnClock = (_spawnDuration - _spawnElapsed) / Math.Max(1, WaveRemaining);
            }
        }
        if (_invasion.NextAvailableWave(Game.Wave) < 0)
            DeclareVictory();
    }
    private void DeclareVictory()
    {
        if (InvasionWon)
            return;
        _epoch++;
        InvasionWon = true;
        _fixedRunning = false;
        CancelSpawnWindow();
        WaveRunning = false;
        Enemies.Clear();
        Shots.Clear();
        HostileShots.Clear();
        _targets.Clear();
        _enemyById.Clear();
        _assignmentClock = 0;
        foreach (var d in Drones)
        {
            d["target_uid"] = -1L;
            d["aim_target_uid"] = -1L;
        }
        InvasionDefeated?.Invoke();
    }
}
