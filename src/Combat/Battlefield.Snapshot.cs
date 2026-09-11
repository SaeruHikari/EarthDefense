using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private static readonly string[] SnapshotFields = "active wave_running enemies _drones _shots _hostile_shots _beams _bursts _damage_numbers _factories _factory_activity _motherships _invasion_won _dead _next_uid _clock _assignment_clock _destroyed_drones _number_sequence wave_remaining wave_total _wave_spawn_snapshot _wave_spawn_duration _wave_spawn_elapsed _wave_spawned _wave_initial_count _wave_spawn_cancelled _wave_segment_start _wave_segment_count _wave_segment_spawned _spawn_clock _boss_spawned _small_boss_spawned _post_defense _post_plan _post_spawned _fixed_cycle_elapsed _fixed_cycle_running _fixed_cohort_complete _local_shields".Split(' ');
    public DataMap SerializeCombatSnapshot()
    {
        SyncLocalShields();
        var data = new DataMap { ["active"] = Active, ["wave_running"] = WaveRunning, ["enemies"] = Enemies, ["_drones"] = Drones, ["_shots"] = Shots, ["_hostile_shots"] = HostileShots, ["_beams"] = Beams, ["_bursts"] = Bursts, ["_damage_numbers"] = DamageNumbers, ["_factories"] = Factories, ["_factory_activity"] = FactoryActivity, ["_motherships"] = Motherships, ["_invasion_won"] = InvasionWon, ["_dead"] = Dead, ["_next_uid"] = _nextUid, ["_clock"] = Clock, ["_assignment_clock"] = _assignmentClock, ["_destroyed_drones"] = DestroyedDrones, ["_number_sequence"] = NumberSequence, ["wave_remaining"] = WaveRemaining, ["wave_total"] = WaveTotal, ["_wave_spawn_snapshot"] = _wavePlan, ["_wave_spawn_duration"] = _spawnDuration, ["_wave_spawn_elapsed"] = _spawnElapsed, ["_wave_spawned"] = _spawned, ["_wave_initial_count"] = _initialCount, ["_wave_spawn_cancelled"] = _spawnCancelled, ["_wave_segment_start"] = _segmentStart, ["_wave_segment_count"] = _segmentCount, ["_wave_segment_spawned"] = _segmentSpawned, ["_spawn_clock"] = _spawnClock, ["_boss_spawned"] = _bossSpawned, ["_small_boss_spawned"] = _smallBossSpawned, ["_post_defense"] = _postDefense, ["_post_plan"] = _postPlan, ["_post_spawned"] = _postSpawned, ["_fixed_cycle_elapsed"] = _cycleElapsed, ["_fixed_cycle_running"] = _fixedRunning, ["_fixed_cohort_complete"] = _cohortComplete, ["_local_shields"] = _localShields };
        return new()
        {
            ["version"] = 4,
            ["earth_radius"] = CombatScale.EarthRadius,
            ["rng_seed"] = unchecked((long)Random.Seed).ToString(CultureInfo.InvariantCulture),
            ["rng_state"] = unchecked((long)Random.State).ToString(CultureInfo.InvariantCulture),
            ["anchor"] = CombatSnapshotCodec.Encode(GetInvasionAnchor()),
            ["destroyed_fronts"] = GetDestroyedFronts().Cast<object?>().ToList(),
            ["payload"] = CombatSnapshotCodec.Encode(data)
        };
    }
    private static bool Nonnegative(object? value, double maximum = 10000, bool integer = false) => CombatSnapshotCodec.IsNumber(value) && DataMap.Number(value) >= 0 && DataMap.Number(value) <= maximum && (!integer || DataMap.Number(value) == Math.Floor(DataMap.Number(value)));
    public static bool ValidateCombatSnapshot(DataMap snapshot)
    {
        if (!CombatSnapshotCodec.HasFields(snapshot, "version:int rng_seed:text rng_state:text") || snapshot.I("version") is not (1 or 2 or 3 or 4) || !long.TryParse(snapshot.S("rng_seed"), out _) || !long.TryParse(snapshot.S("rng_state"), out _) || !InvasionDirector.ValidateDestroyedFronts(snapshot.Value("destroyed_fronts") as IEnumerable<object?>))
            return false;
        if (snapshot.I("version") >= 3 && (!CombatSnapshotCodec.HasFields(snapshot, "earth_radius:number") || (snapshot.N("earth_radius") != CombatScale.EarthRadius && snapshot.N("earth_radius") != CombatScale.PreviousEarthRadius && snapshot.N("earth_radius") != CombatScale.LegacyEarthRadius)))
            return false;
        if (!CombatSnapshotCodec.TryDecode(snapshot.Value("payload"), out var decoded) || decoded is not DataMap d || !CombatSnapshotCodec.TryDecode(snapshot.Value("anchor"), out var anchor) || anchor is not Vector3)
            return false;
        bool legacy = snapshot.I("version") == 1, oldShields = snapshot.I("version") < 4;
        if (d.Count != SnapshotFields.Length - (legacy ? 3 : 0) - (oldShields ? 1 : 0) || SnapshotFields.Any(k => !d.ContainsKey(k) && !(legacy && k.StartsWith("_fixed_", StringComparison.Ordinal)) && !(oldShields && k == "_local_shields")))
            return false;
        if (!oldShields && (d.Value("_local_shields") is not DataMap towers || !ValidateLocalShieldSnapshot(towers, StoredEarthRadius(snapshot))))
            return false;
        if (!CombatSnapshotCodec.HasFields(d, "active:bool wave_running:bool _invasion_won:bool _dead:bool _post_defense:bool _wave_spawn_cancelled:bool _boss_spawned:bool _small_boss_spawned:bool _factories:map _motherships:map _post_plan:map _wave_spawn_snapshot:map enemies:array _drones:array _shots:array _hostile_shots:array _beams:array _bursts:array _damage_numbers:array _factory_activity:array"))
            return false;
        foreach (var k in "_next_uid _clock _assignment_clock _destroyed_drones _number_sequence wave_remaining wave_total _wave_spawn_duration _wave_spawn_elapsed _wave_spawned _wave_initial_count _wave_segment_start _wave_segment_count _wave_segment_spawned _spawn_clock _post_spawned".Split(' '))
            if (!Nonnegative(d.Value(k), double.MaxValue))
                return false;
        if (!legacy)
        {
            if (!CombatSnapshotCodec.HasFields(d, "_fixed_cycle_elapsed:number _fixed_cycle_running:bool _fixed_cohort_complete:bool") || d.N("_fixed_cycle_elapsed") < 0)
                return false;
            if (d.B("_fixed_cycle_running"))
            {
                var p = d.Map("_wave_spawn_snapshot");
                if (!CombatSnapshotCodec.HasFields(p, "cycle_duration:number duration:number composition:map planned_count:int regular_count:int carrier_count:int medium:bool stride:int wave:int stage:int") || p.N("cycle_duration") < p.N("duration") || p.N("duration") <= 0 || d.N("_fixed_cycle_elapsed") > p.N("cycle_duration") + .00001)
                    return false;
                foreach (var (k, v) in p.Map("composition"))
                    if (!DefenseWavePlan.RoleOrder.Contains(k) || !Nonnegative(v, 1e9, true))
                        return false;
            }
        }
        return ValidateActors(d, StoredEarthRadius(snapshot));
    }
    private static bool ValidatePerkActor(DataMap a)
    {
        if (a.ContainsKey("local_shield_checked") && a["local_shield_checked"] is not bool || a.ContainsKey("earth_impact_damage") && !Nonnegative(a.Value("earth_impact_damage"), 1e300))
            return false;
        foreach (var k in new[] { "flight_age", "first_fire_at" })
            if (a.ContainsKey(k) && !Nonnegative(a[k], 1e12))
                return false;
        if (a.ContainsKey("perk_slow_remaining") && (!Nonnegative(a.Value("perk_slow_remaining"), 10) || !Nonnegative(a.Value("perk_slow_fraction"), .6)))
            return false;
        if (a.ContainsKey("perk_erosion_remaining") && (!Nonnegative(a.Value("perk_erosion_remaining"), 10) || !Nonnegative(a.Value("perk_erosion_stacks"), 10, true) || !Nonnegative(a.Value("perk_erosion_per_stack"), .1)))
            return false;
        foreach (var k in new[] { "armor_type", "body_armor" })
            if (a.ContainsKey(k) && !new[] { "legacy", "light", "heavy" }.Contains(a.S(k)))
                return false;
        if (a.ContainsKey("energy_hp") && (!Nonnegative(a.Value("energy_hp"), 1e300) || !Nonnegative(a.Value("energy_max_hp"), 1e300) || a.N("energy_hp") > a.N("energy_max_hp") || a.B("shield_broken") && a.N("energy_hp") > 0))
            return false;
        foreach (var k in new[] { "shield_broken", "energy_granted", "shield_unloaded", "shield_field_used" })
            if (a.ContainsKey(k) && a[k] is not bool)
                return false;
        if (a.ContainsKey("enemy_role_id") && a.S("enemy_role_id") != "" && !DefenseWavePlan.RoleOrder.Contains(a.S("enemy_role_id")))
            return false;
        if (a.ContainsKey("boss_variant_id") && !new[] { "brood", "forge", "prism" }.Contains(a.S("boss_variant_id")))
            return false;
        foreach (var k in "spawn_radius combat_entry_radius energy_recovery_budget lock_progress lock_tick fire_charge charge_total charge_remaining telegraph saturation_ready chain_ready intercept_boost_until intercept_boost_next dense_until pulse_next burst_until burst_ready first_energy_ready shield_field_until energy_recovery_blocked_until stagger_until target_lock_until".Split(' '))
            if (a.ContainsKey(k) && !Nonnegative(a[k], 1e300))
                return false;
        return true;
    }
    private static readonly string[] HistoricalProjectileAbilityIds = "K_N1 K_N2 K_N3 K_A1 K_A2 K_A3 K_G1 K_G2 M_N1 M_N2 M_N3 M_A1 M_A2 M_A3 M_G1 M_G2 L_N1 L_N2 L_N3 L_A1 L_A2 L_A3 L_G1 L_G2 I_N1 I_N2 I_N3 I_A1 I_A2 I_A3 I_G1 I_G2 D_N1 D_N2 D_N3 D_A1 D_A2 D_A3 D_G1 D_G2 C_N1 C_N2 C_N3 C_A1 C_A2 C_A3 C_G1 C_G2 M_N4 D_N4".Split(' ');
    private static long _projectileAbilityRevision = long.MinValue;
    private static readonly HashSet<string> _projectileAbilityIds = new(StringComparer.Ordinal);
    private static bool ValidProjectileAbilities(DataMap abilities)
    {
        if (_projectileAbilityRevision != CatalogData.Revision)
        {
            _projectileAbilityIds.Clear();
            foreach (string id in HistoricalProjectileAbilityIds) _projectileAbilityIds.Add(id);
            foreach (var row in DeepTechnology.Nodes) if (row.S("size") != "small") _projectileAbilityIds.Add(row.S("id"));
            _projectileAbilityRevision = CatalogData.Revision;
        }
        foreach (var (id, value) in abilities) if (!_projectileAbilityIds.Contains(id) || value is not bool) return false;
        return true;
    }
    private static bool ValidatePerkShot(DataMap s)
    {
        if (s.ContainsKey("local_shield_checked") && s["local_shield_checked"] is not bool)
            return false;
        if (s.ContainsKey("source"))
        {
            if (s["source"] is not DataMap source || source.ContainsKey("effects") && source["effects"] is not DataMap)
                return false;
            if (source.ContainsKey("missile_blast_damage_multiplier") && !Nonnegative(source["missile_blast_damage_multiplier"], 1e300)) return false;
            if (source.ContainsKey("actor_uid") && !CombatSnapshotCodec.IsInteger(source["actor_uid"]))
                return false;
            if (source.ContainsKey("damage_type") && !new[] { "kinetic", "explosive", "beam" }.Contains(source.S("damage_type")))
                return false;
            if (source.ContainsKey("primary") && source["primary"] is not bool)
                return false;
            if (source.ContainsKey("tech_abilities") && (source["tech_abilities"] is not DataMap tech || !ValidProjectileAbilities(tech)))
                return false;
            if (source.ContainsKey("ability_values"))
            {
                if (source["ability_values"] is not DataMap values || values.Any(p => !TechPacketKeys.Contains(p.Key) || !Nonnegative(p.Value, 1e300)))
                    return false;
            }
            if (source.ContainsKey("factory_id") && (!CombatSnapshotCodec.HasFields(source, "factory_id:int berth:int kind:text") || source.I("berth") < 0 || !new[] { "interceptor", "laser", "missile" }.Contains(source.S("kind"))))
                return false;
            if (source.Map("effects").Any(p => !PerkEffectKeys.Contains(p.Key) || !Nonnegative(p.Value)))
                return false;
        }
        foreach (var k in new[] { "pierce_left", "ricochet_left" })
            if (s.ContainsKey(k) && !Nonnegative(s[k], k == "pierce_left" ? 3 : 2, true))
                return false;
        if (s.ContainsKey("delayed_effect") && (!new[] { "blast", "shield_field" }.Contains(s.S("delayed_effect")) || !Nonnegative(s.Value("blast_radius"), 100)))
            return false;
        if (s.ContainsKey("interceptable") && (s["interceptable"] is not bool || !Nonnegative(s.Value("hp"), 1e9) || !Nonnegative(s.Value("max_hp"), 1e9)))
            return false;
        if (s.ContainsKey("secondary") && s["secondary"] is not bool)
            return false;
        if (s.ContainsKey("proc_kind") && !new[] { "ricochet", "cluster" }.Contains(s.S("proc_kind")))
            return false;
        if (s.ContainsKey("hit_ids"))
        {
            if (s["hit_ids"] is not List<object?> ids || ids.Count > 16 || ids.Any(v => !CombatSnapshotCodec.IsInteger(v)) || ids.Select(v => DataMap.Integer(v)).Distinct().Count() != ids.Count)
                return false;
        }
        return true;
    }
    private static bool ValidateActors(DataMap d, double storedEarthRadius)
    {
        foreach (var k in "_next_uid _destroyed_drones _number_sequence wave_remaining wave_total _wave_spawned _wave_initial_count _wave_segment_count _wave_segment_spawned _post_spawned".Split(' '))
            if (!CombatSnapshotCodec.IsInteger(d.Value(k)))
                return false;
        if (d.L("_next_uid") < 1 || d.L("wave_remaining") > d.L("wave_total"))
            return false;
        var used = new HashSet<long>();
        long maximum = 0;
        foreach (var obj in d.List("_drones").Concat(d.List("enemies")).Concat(d.Map("_motherships").Values))
        {
            if (obj is not DataMap a || !ValidatePerkActor(a) || !CombatSnapshotCodec.HasFields(a, "uid:int kind:text hp:number max_hp:number hit_radius:number hit:number tangent:v3 velocity:v3") || a.N("hp") <= 0 || a.N("hp") > a.N("max_hp") || a.N("hit_radius") <= 0 || !used.Add(a.L("uid")))
                return false;
            maximum = Math.Max(maximum, a.L("uid"));
        }
        var points = new HashSet<string>();
        foreach (var a in d.List("_drones").Cast<DataMap>())
        {
            if (a.ContainsKey("airframe_id"))
            {
                string id = a.S("airframe_id");
                var definition = AirframeCatalog.Definition(id);
                if (definition.Count == 0 || definition.S("kind") != a.S("kind") || a.I("capacity_cost") != definition.I("capacity_cost"))
                    return false;
            }
            for (int p = a.I("patrol_slot"); p < a.I("patrol_slot") + a.I("capacity_cost", 1); p++)
                if (!points.Add(a.L("factory_site_id") + ":" + p))
                    return false;
            if (!CombatSnapshotCodec.HasFields(a, "factory_site_id:int patrol_slot:int state:text normal:v3 axis:v3 altitude:number angular_speed:number fire:number flash:number launch_age:number spawn_space:v3 launch_direction:v3 world_up:v3 target_uid:int aim_target_uid:int aim_direction:v3 aim_up:v3") || !new[] { "interceptor", "laser", "missile" }.Contains(a.S("kind")) || !new[] { "launching", "patrol", "engaging", "returning", "landing", "repairing", "suiciding" }.Contains(a.S("state")) || !d.Map("_factories").ContainsKey(a.L("factory_site_id").ToString(CultureInfo.InvariantCulture)) || a.ContainsKey("space_position") && a["space_position"] is not Vector3)
                return false;
        }
        foreach (var a in d.List("enemies").Cast<DataMap>())
        {
            if (a.ContainsKey("stationary_bombard") && a["stationary_bombard"] is not bool)
                return false;
            if (a.B("stationary_bombard") && (!UsesStationaryBombardment(a) || a.S("phase") != "ground_attack" || !CombatSnapshotCodec.HasFields(a, "aim_direction:v3 aim_up:v3") || C.V(a, "velocity").LengthSquared() > .000001))
                return false;
            if (!CombatSnapshotCodec.HasFields(a, "space_position:v3 target_space:v3 speed:number base_speed:number phase:text bombard_time:number bombard_duration:number base_size:number size:number wave:int age:number fire:number") || !new[] { "scout", "small_boss", "cruiser", "boss", "carrier" }.Contains(a.S("kind")) || !new[] { "approach", "ground_attack", "retreat" }.Contains(a.S("phase")))
                return false;
            if (a.ContainsKey("post_wave_id") && !CombatSnapshotCodec.HasFields(a, "post_wave_id:text post_carrier:bool post_damage_multiplier:number locked_volley_count:int hangar_remaining:int hangar_clock:number hangar_open:number"))
                return false;
        }
        foreach (var a in d.Map("_motherships").Values.Cast<DataMap>())
            if (!CombatSnapshotCodec.HasFields(a, "front_id:text space_position:v3 size:number phase:text") || a.S("kind") != "mothership")
                return false;
        foreach (var (key, value) in d.Map("_factories"))
        {
            if (!CombatSnapshotCodec.IsNumericKey(d.Map("_factories"), key) || value is not DataMap f || !CombatSnapshotCodec.HasFields(f, "site_id:int kind:text site:map timer:number door:number capacity:int active:int") || f.L("site_id").ToString(CultureInfo.InvariantCulture) != key || f.I("capacity") < 1 || !CombatSnapshotCodec.HasFields(f.Map("site"), "site_id:int kind:text normal:v3 launch_position:v3 launch_direction:v3"))
                return false;
        }
        foreach (string key in new[] { "_shots", "_hostile_shots" })
            foreach (var obj in d.List(key))
            {
                if (obj is not DataMap s || !ValidatePerkShot(s) || !CombatSnapshotCodec.HasFields(s, "uid:int kind:text space_position:v3 velocity:v3 tangent:v3 life:number damage:number") || s.L("uid") < 1 || !used.Add(s.L("uid")))
                    return false;
                if (key == "_shots" && !CombatSnapshotCodec.HasFields(s, "missile:bool") || key == "_hostile_shots" && (!CombatSnapshotCodec.HasFields(s, "target_kind:text blast_radius:number") || !new[] { "earth", "drone" }.Contains(s.S("target_kind"))))
                    return false;
                maximum = Math.Max(maximum, s.L("uid"));
            }
        foreach (var obj in d.List("_beams"))
            if (obj is not DataMap b || !CombatSnapshotCodec.HasFields(b, "from_space:v3 to_space:v3 life:number max_life:number color:color") || b.ContainsKey("style") && (b["style"] is not string style || style != "laser"))
                return false;
        foreach (var obj in d.List("_bursts"))
        {
            if (obj is not DataMap b || !CombatSnapshotCodec.HasFields(b, "uid:int space_position:v3 color:color radius:number radius_world:number age:number life:number") || !used.Add(b.L("uid")))
                return false;
            maximum = Math.Max(maximum, b.L("uid"));
        }
        foreach (var obj in d.List("_damage_numbers"))
            if (obj is not DataMap n || !CombatSnapshotCodec.HasFields(n, "uid:int space_position:v3 amount:number color:color age:number life:number offset:number"))
                return false;
        foreach (var obj in d.List("_factory_activity"))
            if (obj is not DataMap a || !CombatSnapshotCodec.HasFields(a, "site_id:int phase:number progress:number active:int capacity:int"))
                return false;
        var plan = d.Map("_post_plan");
        if (d.B("_post_defense") && d.B("wave_running") && !CombatSnapshotCodec.HasFields(plan, "id:text wave:int carrier_count:int duration:number unit_health:number carrier_health:number damage_multiplier:number volley_count:int aircraft_per_carrier:int hangar_interval:number"))
            return false;
        if (plan.ContainsKey("completion_handled") && plan["completion_handled"] is not bool)
            return false;
        if (plan.ContainsKey("frontier_radius") && (!CombatSnapshotCodec.HasFields(plan, "frontier_radius:number defense_stage:int sortie_elapsed:number sortie_round:int sortie_started:bool") || plan.N("frontier_radius") < storedEarthRadius + 6 || plan.N("frontier_radius") > 1000 + storedEarthRadius - CombatScale.LegacyEarthRadius || plan.I("defense_stage") < 1 || plan.I("defense_stage") > 3 || plan.N("sortie_elapsed") < 0 || plan.I("sortie_round") < 0))
            return false;
        return d.L("_next_uid") > maximum;
    }
    public bool RestoreCombatSnapshot(DataMap snapshot)
    {
        if (!ValidateCombatSnapshot(snapshot))
            return false;
        CombatSnapshotCodec.TryDecode(snapshot["payload"], out var decoded);
        var d = (DataMap)decoded!;
        MigrateSnapshotSpace(d, CombatScale.EarthRadius - StoredEarthRadius(snapshot));
        CombatSnapshotCodec.TryDecode(snapshot["anchor"], out var anchor);
        _epoch++;
        _invasion = new();
        _invasion.ConfigureAnchor((Vector3)anchor!);
        _invasion.RestoreDestroyedFronts(snapshot.List("destroyed_fronts"));
        Active = d.B("active");
        WaveRunning = d.B("wave_running");
        InvasionWon = d.B("_invasion_won");
        Dead = d.B("_dead");
        Clock = d.N("_clock");
        DestroyedDrones = d.L("_destroyed_drones");
        NumberSequence = d.L("_number_sequence");
        _nextUid = d.L("_next_uid");
        _assignmentClock = d.N("_assignment_clock");
        WaveRemaining = d.L("wave_remaining");
        WaveTotal = d.L("wave_total");
        _wavePlan = d.Map("_wave_spawn_snapshot");
        _spawnDuration = d.N("_wave_spawn_duration");
        _spawnElapsed = d.N("_wave_spawn_elapsed");
        _spawned = d.L("_wave_spawned");
        _initialCount = d.L("_wave_initial_count");
        _spawnCancelled = d.B("_wave_spawn_cancelled");
        _segmentStart = d.N("_wave_segment_start");
        _segmentCount = d.L("_wave_segment_count");
        _segmentSpawned = d.L("_wave_segment_spawned");
        _spawnClock = d.N("_spawn_clock");
        _bossSpawned = d.B("_boss_spawned");
        _smallBossSpawned = d.B("_small_boss_spawned");
        _postDefense = d.B("_post_defense");
        _postPlan = d.Map("_post_plan");
        _postSpawned = d.I("_post_spawned");
        _cycleElapsed = d.N("_fixed_cycle_elapsed", Math.Min(44, _spawnElapsed));
        _fixedRunning = d.B("_fixed_cycle_running", WaveRunning);
        _cohortComplete = d.B("_fixed_cohort_complete");
        if (snapshot.I("version") == 1 && _fixedRunning)
        {
            var migrated = DefenseWavePlan.Build(Game.Wave, Math.Max(_initialCount, WaveTotal), _spawnDuration, Math.Max(45, _spawnDuration), _postDefense || ShouldSpawnMediumBoss(Game.Wave), _postPlan.I("defense_stage"), 0);
            foreach (var p in migrated)
                _wavePlan[p.Key] = p.Value;
        }
        foreach (var (key, list) in new[] { ("enemies", Enemies), ("_drones", Drones), ("_shots", Shots), ("_hostile_shots", HostileShots), ("_beams", Beams), ("_bursts", Bursts), ("_damage_numbers", DamageNumbers), ("_factory_activity", FactoryActivity) })
        {
            list.Clear();
            list.AddRange(d.List(key).Cast<DataMap>());
        }
        _localShields.Clear();
        _localShieldView.Clear();
        foreach (var (key, value) in d.Map("_local_shields"))
            _localShields[long.Parse(key, CultureInfo.InvariantCulture)] = (DataMap)value!;
        _localShieldRevision = -1;
        Factories.Clear();
        foreach (var (key, value) in d.Map("_factories"))
            Factories[long.Parse(key, CultureInfo.InvariantCulture)] = (DataMap)value!;
        Motherships.Clear();
        foreach (var (key, value) in d.Map("_motherships"))
            Motherships[key] = (DataMap)value!;
        foreach (var drone in Drones)
            if (!drone.ContainsKey("airframe_id"))
            {
                drone["airframe_id"] = BaseFrame(drone.S("kind"));
                drone["capacity_cost"] = 1;
            }
        Random.Seed = unchecked((ulong)long.Parse(snapshot.S("rng_seed"), CultureInfo.InvariantCulture));
        Random.State = unchecked((ulong)long.Parse(snapshot.S("rng_state"), CultureInfo.InvariantCulture));
        _revision = -1;
        _layoutDirty = false;
        _motherWave = Game.Wave;
        _defendersValid = false;
        WorldCache.Clear();
        RefreshConfiguration();
        SyncLocalShields();
        RebuildActorIndex();
        _sectorsDirty = true;
        return true;
    }
}


