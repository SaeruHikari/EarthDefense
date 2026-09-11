using System;
using System.Collections.Generic;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public DataMap DroneStats()
    {
        double damageMultiplier = (1 + Level("damage") * DomainBalance.Legacy("damage_1")) * CombatSettings.N("drone_base_damage_multiplier", .65);
        double factoryMultiplier = 1 / (1 + Level("factory_automation") * DomainBalance.Legacy("factory_automation_1") + Level("combat_logistics") * DomainBalance.Legacy("combat_logistics_1"));
        bool assault = Level("mothership_assault") > 0;
        var result = new DataMap
        {
            ["damage"] = CombatSettings.N("drone_damage") * damageMultiplier * (1.0 + Level("kinetic_coils") * DomainBalance.Legacy("kinetic_coils_1")) * (1.0 + Level("kinetic_mastery") * DomainBalance.Legacy("kinetic_mastery_1")),
            ["fire_rate"] = DomainBalance.Value("kinetic_fire_rate_base") * (1.0 + Level("rapid") * DomainBalance.Legacy("rapid_1")) * (1.0 + Level("fire_control") * DomainBalance.Legacy("fire_control_1")) * (1.0 + Level("ammo_packing") * DomainBalance.Legacy("ammo_packing_1")),
            ["spread"] = Math.Min(1, Tech.L("spread")),
            ["bullet_count"] = Level("spread") > 0 ? (long)DomainBalance.Value("spread_bullet_count") : 1L,
            ["bullet_damage_weights"] = Level("spread") > 0 ? new List<object?> { 1d, DomainBalance.Value("spread_secondary_damage"), DomainBalance.Value("spread_secondary_damage") } : new List<object?> { 1d },
            ["laser"] = Buildings.L("laser") * FactoryCapacity("laser"),
            ["missile"] = Buildings.L("missile") * FactoryCapacity("missile"),
            ["interceptor"] = Buildings.L("interceptor") * FactoryCapacity("interceptor"),
            ["laser_damage"] = CombatSettings.N("laser_damage") * damageMultiplier * (1.0 + Math.Max(0.0, Level("laser") - 1.0) * DomainBalance.Value("advanced_weapon_rank_damage")) * (1.0 + Level("laser_focus") * DomainBalance.Legacy("laser_focus_1")) * (1.0 + Level("laser_capacitors") * DomainBalance.Legacy("laser_capacitors_2")) * (1.0 + Level("photonic_mastery") * DomainBalance.Legacy("photonic_mastery_2")),
            ["missile_damage"] = CombatSettings.N("missile_damage") * damageMultiplier * (1.0 + Math.Max(0.0, Level("missile") - 1.0) * DomainBalance.Value("advanced_weapon_rank_damage")) * (1.0 + Level("warhead") * DomainBalance.Legacy("warhead_1")) * (1.0 + Level("missile_mastery") * DomainBalance.Legacy("missile_mastery_1")),
            ["laser_fire_rate"] = DomainBalance.Value("laser_fire_rate_base") * (1.0 + Level("laser_cycling") * DomainBalance.Legacy("laser_cycling_1")) * (1.0 + Level("laser_cooling") * DomainBalance.Legacy("laser_cooling_1")) * (1.0 + Level("photonic_mastery") * DomainBalance.Legacy("photonic_mastery_3")),
            ["missile_fire_rate"] = DomainBalance.Value("missile_fire_rate_base") * (1.0 + Level("missile_feed") * DomainBalance.Legacy("missile_feed_1")) * (1.0 + Level("missile_mastery") * DomainBalance.Legacy("missile_mastery_2")),
            ["laser_beam_count"] = 1 + Tech.L("laser_arrays"),
            ["missile_salvo"] = 1 + Tech.L("missile_salvo"),
            ["missile_extra_projectiles"] = 0L,
            ["projectile_speed"] = DomainBalance.Value("kinetic_projectile_speed_base") * (1.0 + Level("projectile_drive") * DomainBalance.Legacy("projectile_drive_1") + Level("ammo_packing") * DomainBalance.Legacy("ammo_packing_2")),
            ["projectile_speed_multiplier"] = 1.0 + Level("projectile_drive") * DomainBalance.Legacy("projectile_drive_1") + Level("ammo_packing") * DomainBalance.Legacy("ammo_packing_2"),
            ["missile_speed_multiplier"] = 1.0 + Level("missile_engines") * DomainBalance.Legacy("missile_engines_1"),
            ["missile_blast_radius"] = DomainBalance.Value("missile_blast_radius_base") + Level("missile_blast") * DomainBalance.Legacy("missile_blast_1"),
            ["missile_turn_rate"] = DomainBalance.Value("missile_turn_rate_base") + Level("missile_guidance") * DomainBalance.Legacy("missile_guidance_1"),
            ["interceptor_range_multiplier"] = 1.0 + Level("alien_kinetic_range") * DomainBalance.Legacy("alien_kinetic_range_1") + (assault ? DomainBalance.Value("legacy_interceptor_assault_range_add") : 0.0),
            ["laser_range_multiplier"] = (1.0 + Level("laser_range") * DomainBalance.Legacy("laser_range_1")) * (1.0 + Level("alien_laser_range") * DomainBalance.Legacy("alien_laser_range_1")) * (assault ? DomainBalance.Value("legacy_laser_assault_range_factor") : 1.0),
            ["missile_range_multiplier"] = (1.0 + Level("alien_missile_range") * DomainBalance.Legacy("alien_missile_range_1")) * (assault ? DomainBalance.Value("legacy_missile_assault_range_factor") : 1.0),
            ["mothership_assault_unlocked"] = assault,
            ["weapon_range_bonus"] = DomainBalance.Frontier(LegacyFrontierLevel()).N("weapon_range_bonus"),
            ["death_blast_damage"] = DeathBlastBaseDamage * (1.0 + Level("death_blast") * DomainBalance.Legacy("death_blast_1")),
            ["death_blast_radius"] = DeathBlastRadius,
            ["drone_armor"] = Math.Min(DomainBalance.Value("drone_armor_cap"), Level("drone_armor") * DomainBalance.Legacy("drone_armor_1")),
            ["factory_interval_multiplier"] = factoryMultiplier,
            ["shield_max"] = ShieldMax(),
        };
        var fx = TechEffects();
        foreach (var (key, bonus) in new[] { ("damage", "kinetic_damage_bonus"), ("fire_rate", "kinetic_fire_rate_bonus"), ("laser_damage", "laser_damage_bonus"), ("laser_fire_rate", "laser_fire_rate_bonus"), ("missile_damage", "missile_damage_bonus"), ("missile_fire_rate", "missile_fire_rate_bonus"), ("missile_speed_multiplier", "missile_speed_bonus"), ("missile_blast_radius", "missile_blast_radius_bonus"), ("interceptor_range_multiplier", "kinetic_range_bonus"), ("laser_range_multiplier", "laser_range_bonus"), ("missile_range_multiplier", "missile_range_bonus"), ("death_blast_damage", "death_damage_bonus"), ("death_blast_radius", "death_radius_bonus") })
            result[key] = result.N(key) * (1 + fx.N(bonus));
        result["missile_blast_damage_multiplier"] = 1 + fx.N("missile_blast_damage_bonus");
        result["all_target_range_multiplier"] = fx.N("all_target_range_multiplier", 1);
        result["factory_interval_multiplier"] = result.N("factory_interval_multiplier") / (1 + fx.N("production_speed_bonus"));
        result["mothership_assault_unlocked"] = result.B("mothership_assault_unlocked") || HasResearch("C_G1");
        foreach (var (key, value) in fx)
            result[key] = value;
        return result;
    }
    public DataMap PatrolStats(string kind)
    {
        var basis = DomainBalance.Patrol(kind);
        double coverage=CombatSettings.N("patrol_coverage_multiplier",2);
        basis["patrol_radius"]=basis.N("patrol_radius")*coverage;basis["patrol_outer_range"]=basis.N("patrol_outer_range")*coverage;
        string driveId = PerkCatalog.Kinds.Contains(kind) ? kind + "_drive" : "interceptor_drive";
        bool assault = Level("mothership_assault") > 0;
        var result = new DataMap
        {
            ["patrol_radius"] = basis.N("patrol_radius") * (1.0 + Level("patrol_radius") * DomainBalance.Legacy("patrol_radius_1") + Level("orbit_navigation") * DomainBalance.Legacy("orbit_navigation_1") + Level("patrol_command") * DomainBalance.Legacy("patrol_command_1")) * (1.0 + Level("alien_navigation") * DomainBalance.Legacy("alien_navigation_1")) + (assault ? DomainBalance.Value("legacy_patrol_assault_radius_add") : 0.0),
            ["patrol_outer_range"] = basis.N("patrol_outer_range") * (1.0 + Level("patrol_outer") * DomainBalance.Legacy("patrol_outer_1") + Level("orbit_navigation") * DomainBalance.Legacy("orbit_navigation_2") + Level("patrol_command") * DomainBalance.Legacy("patrol_command_2")) * (1.0 + Level("alien_navigation") * DomainBalance.Legacy("alien_navigation_2")) + (assault ? DomainBalance.Value("legacy_patrol_assault_outer_add") : 0.0) + DomainBalance.Frontier(LegacyFrontierLevel()).N("patrol_outer_bonus"),
            ["patrol_speed"] = basis.N("patrol_speed") * (1.0 + Level("patrol_speed") * DomainBalance.Legacy("patrol_speed_1") + Level(driveId) * DomainBalance.Value("legacy_drive_speed_per_level") + Level("patrol_command") * DomainBalance.Legacy("patrol_command_3")) * (1.0 + Level("alien_propulsion") * DomainBalance.Legacy("alien_propulsion_1")),
            ["health"] = basis.N("health") * CombatSettings.N("drone_base_health_multiplier", 0.65) * (1.0 + Level("drone_armor") * DomainBalance.Legacy("drone_armor_2")) * (1.0 + Level("defense_network") * DomainBalance.Legacy("defense_network_3")) * (1.0 + Level("alien_field_support") * DomainBalance.Legacy("alien_field_support_1")),
            ["repair_threshold"] = Math.Min(DomainBalance.Value("repair_threshold_cap"), DomainBalance.Value("repair_threshold_base") + Level("repair_protocol") * DomainBalance.Legacy("repair_protocol_2")),
            ["repair_rate"] = DomainBalance.Value("repair_rate_base") * (1.0 + Level("field_repair") * DomainBalance.Legacy("field_repair_1") + Level("combat_logistics") * DomainBalance.Legacy("combat_logistics_1")) * (1.0 + Level("alien_field_support") * DomainBalance.Legacy("alien_field_support_2")),
            ["factory_interval_multiplier"] = 1.0 / (1.0 + Level("factory_automation") * DomainBalance.Legacy("factory_automation_1") + Level("combat_logistics") * DomainBalance.Legacy("combat_logistics_1")),
        };
        var fx = TechEffects();
        foreach (var (key, bonus) in new[] { ("patrol_radius", "patrol_radius_bonus"), ("patrol_outer_range", "patrol_outer_bonus"), ("patrol_speed", "patrol_speed_bonus"), ("health", "health_bonus"), ("repair_rate", "repair_speed_bonus") })
            result[key] = result.N(key) * (1 + fx.N(bonus));
        result["factory_interval_multiplier"] = result.N("factory_interval_multiplier") / (1 + fx.N("production_speed_bonus"));
        result["action_radius"] = fx.N("action_radius");
        result["strategic_enabled"] = fx.B("strategic_enabled");
        result["strategic_speed_multiplier"] = fx.N("strategic_speed_multiplier", 1);
        result["combat_turn_rate"] = DomainBalance.Value("tactical_turn_rate_base");
        result["return_speed_multiplier"] = 1d;
        result["tech_abilities"] = fx.Map("tech_abilities");
        foreach (var (key, value) in fx)
            if (!result.ContainsKey(key))
                result[key] = value;
        return result;
    }
    public int FactoryCapacity(string kind)
    {
        if (!PerkCatalog.Kinds.Contains(kind))
            return 0;
        return Math.Max(1, (int)Math.Floor(CombatSettings.N(kind + "_factory_capacity", kind == "interceptor" ? 3 : 2) * CombatSettings.N("factory_capacity_multiplier", 2) * CombatSettings.N("factory_starting_capacity_factor", .5))) + ResearchCapacityBonus;
    }
    private DataMap FactoryModifiers(string kind, long site) => FactoryPerks.Modifiers(kind, FactoryPerks.ActiveRunId == RunId ? site : -1);
    public int FactoryCapacityForSite(string kind, long site = -1) => FactoryCapacity(kind) + FactoryModifiers(kind, site).I("capacity_add");
    public DataMap FactoryDroneStats(string kind, long site = -1) => AircraftDroneStats(kind, site, -1);
    public bool AircraftHasOverride(string kind, long site, int berth) => berth >= 0 && site >= 0 && FactoryPerks.ActiveRunId == RunId && FactoryPerks.HasSiteOverride(kind, site, "aircraft", berth);
    public DataMap AircraftDroneStats(string kind, long site, int berth, string physicalFrame = "")
    {
        string frame = AirframeCatalog.Compatible(physicalFrame, kind) ? physicalFrame : FactoryAirframe(kind, site, berth);
        if (!AircraftHasOverride(kind, site, berth) && frame == FactoryAirframe(kind, site))
            berth = -1;
        string key = $"{kind}:{site}:{berth}:{frame}";
        if (!_weaponCache.TryGetValue(key, out var result))
        {
            result = CompilePerkWeapons(kind, site, berth, frame);
            _weaponCache[key] = result;
        }
        return result;
    }
    private DataMap CompilePerkWeapons(string kind, long site, int berth, string frameId)
    {
        var stats = _primedWeapons?.DeepClone() ?? DroneStats();
        var frame = AirframeCatalog.Definition(frameId);
        long active = FactoryPerks.ActiveRunId == RunId ? site : -1;
        foreach (var effects in new[] { FactoryModifiers(kind, site), FactoryPerks.Modifiers(kind, active, "aircraft", active >= 0 ? berth : -1, frameId) })
        {
            foreach (string key in new[] { "damage", "laser_damage", "missile_damage" })
                stats[key] = stats.N(key) * effects.N("damage_multiplier", 1);
            foreach (string key in new[] { "fire_rate", "laser_fire_rate", "missile_fire_rate" })
                stats[key] = stats.N(key) * effects.N("fire_rate_multiplier", 1);
            foreach (string key in new[] { "projectile_speed", "projectile_speed_multiplier", "missile_speed_multiplier" })
                stats[key] = stats.N(key) * effects.N("projectile_speed_multiplier", 1);
            stats["missile_blast_radius"] = stats.N("missile_blast_radius") * effects.N("blast_radius_multiplier", 1);
            stats["death_blast_damage"] = stats.N("death_blast_damage") * effects.N("death_blast_multiplier", 1);
            foreach (var (key, value) in effects)
            {
                if (new[] { "damage_multiplier", "fire_rate_multiplier", "projectile_speed_multiplier", "blast_radius_multiplier", "death_blast_multiplier", "health_multiplier", "patrol_multiplier", "speed_multiplier", "spawn_multiplier", "capacity_add" }.Contains(key))
                    continue;
                if (key.EndsWith("_multiplier") && key != "slow_boss_multiplier")
                    stats[key] = stats.N(key, 1) * DataMap.Number(value);
                else if (value is int or long)
                    stats[key] = Math.Max(stats.L(key), DataMap.Integer(value));
                else
                    stats[key] = Math.Max(stats.N(key), DataMap.Number(value));
            }
        }
        foreach (string key in new[] { "damage", "laser_damage", "missile_damage" })
            stats[key] = stats.N(key) * frame.N("damage_multiplier", 1);
        foreach (string key in new[] { "fire_rate", "laser_fire_rate", "missile_fire_rate" })
            stats[key] = stats.N(key) * frame.N("fire_rate_multiplier", 1);
        stats["airframe_id"] = frameId;
        stats["capacity_cost"] = frame.I("capacity_cost", 1);
        stats["role"] = frame.S("role");
        stats["frame_range_multiplier"] = frame.N("range_multiplier", 1);
        stats["missile_blast_radius"] = stats.N("missile_blast_radius") * frame.N("blast_radius_multiplier", 1);
        foreach (string key in new[] { "burst_target_count", "aoe_budget", "warmup_seconds", "suppression_radius", "suppression_delay", "suppression_cooldown", "warmup_damage_multiplier", "turn_multiplier", "scale_multiplier" })
            stats[key] = frame.Value(key, new[] { "warmup_seconds", "suppression_radius", "suppression_delay", "suppression_cooldown" }.Contains(key) ? 0d : 1d);
        stats["frame_large_target_multiplier"] = frame.N("large_target_damage_multiplier", 1);
        return stats;
    }
    public DataMap FactoryPatrolStats(string kind, long site = -1)
    {
        string key = $"{kind}:{site}";
        if (_patrolCache.TryGetValue(key, out var found))
            return found;
        var primed = _primedPatrol?.Map(kind);
        var stats = primed is { Count: > 0 } ? primed.DeepClone() : PatrolStats(kind);
        var fx = FactoryModifiers(kind, site);
        foreach (var (field, multiplier) in new[] { ("health", "health_multiplier"), ("patrol_radius", "patrol_multiplier"), ("patrol_outer_range", "patrol_multiplier"), ("patrol_speed", "speed_multiplier"), ("factory_interval_multiplier", "spawn_multiplier") })
            stats[field] = stats.N(field) * fx.N(multiplier, 1);
        _patrolCache[key] = stats;
        return stats;
    }
    public DataMap AircraftPatrolStats(string kind, long site, int berth, string physicalFrame = "")
    {
        string id = AirframeCatalog.Compatible(physicalFrame, kind) ? physicalFrame : FactoryAirframe(kind, site, berth), key = $"frame:{kind}:{site}:{berth}:{id}";
        if (_patrolCache.TryGetValue(key, out var found))
            return found;
        var stats = FactoryPatrolStats(kind, site).DeepClone();
        var frame = AirframeCatalog.Definition(id);
        stats["health"] = stats.N("health") * frame.N("health_multiplier", 1);
        stats["patrol_speed"] = stats.N("patrol_speed") * frame.N("speed_multiplier", 1);
        stats["combat_turn_rate"] = stats.N("combat_turn_rate") * frame.N("turn_multiplier", 1);
        stats["airframe_id"] = id;
        stats["capacity_cost"] = frame.I("capacity_cost", 1);
        stats["factory_interval_multiplier"] = stats.N("factory_interval_multiplier") * frame.N("spawn_multiplier", 1);
        _patrolCache[key] = stats;
        return stats;
    }
    public double FactorySpawnIntervalForSite(string kind, long site = -1, string airframeId = "") => Math.Max(DomainBalance.Value("factory_spawn_interval_min"), CombatSettings.N("factory_spawn_interval") * FactoryPatrolStats(kind, site).N("factory_interval_multiplier") * AirframeCatalog.Definition(airframeId.Length > 0 ? airframeId : FactoryAirframe(kind, site)).N("spawn_multiplier", 1));
    public void PrimeFactoryBaseStats(DataMap weapons, DataMap? patrol = null)
    {
        _primedWeapons = weapons.DeepClone();
        _primedPatrol = patrol?.DeepClone();
        _weaponCache.Clear();
        _patrolCache.Clear();
    }
}
