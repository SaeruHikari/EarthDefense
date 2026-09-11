using System;
using System.Collections.Generic;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public DataMap DroneStats()
    {
        double damageMultiplier = (1 + Level("damage") * .3) * CombatSettings.N("drone_base_damage_multiplier", .65);
        double factoryMultiplier = 1 / (1 + Level("factory_automation") * .12 + Level("combat_logistics") * .08);
        bool assault = Level("mothership_assault") > 0;
        var result = new DataMap
        {
            ["damage"] = CombatSettings.N("drone_damage") * damageMultiplier * (1.0 + Level("kinetic_coils") * 0.16) * (1.0 + Level("kinetic_mastery") * 0.12),
            ["fire_rate"] = 2.0 * (1.0 + Level("rapid") * 0.22) * (1.0 + Level("fire_control") * 0.12) * (1.0 + Level("ammo_packing") * 0.08),
            ["spread"] = Math.Min(1, Tech.L("spread")),
            ["bullet_count"] = Level("spread") > 0 ? 3L : 1L,
            ["bullet_damage_weights"] = Level("spread") > 0 ? new List<object?> { 1d, .5d, .5d } : new List<object?> { 1d },
            ["laser"] = Buildings.L("laser") * FactoryCapacity("laser"),
            ["missile"] = Buildings.L("missile") * FactoryCapacity("missile"),
            ["interceptor"] = Buildings.L("interceptor") * FactoryCapacity("interceptor"),
            ["laser_damage"] = CombatSettings.N("laser_damage") * damageMultiplier * (1.0 + Math.Max(0.0, Level("laser") - 1.0) * 0.35) * (1.0 + Level("laser_focus") * 0.18) * (1.0 + Level("laser_capacitors") * 0.10) * (1.0 + Level("photonic_mastery") * 0.12),
            ["missile_damage"] = CombatSettings.N("missile_damage") * damageMultiplier * (1.0 + Math.Max(0.0, Level("missile") - 1.0) * 0.35) * (1.0 + Level("warhead") * 0.18) * (1.0 + Level("missile_mastery") * 0.12),
            ["laser_fire_rate"] = 1.25 * (1.0 + Level("laser_cycling") * 0.14) * (1.0 + Level("laser_cooling") * 0.09) * (1.0 + Level("photonic_mastery") * 0.06),
            ["missile_fire_rate"] = 0.6 * (1.0 + Level("missile_feed") * 0.12) * (1.0 + Level("missile_mastery") * 0.06),
            ["laser_beam_count"] = 1 + Tech.L("laser_arrays"),
            ["missile_salvo"] = 1 + Tech.L("missile_salvo"),
            ["missile_extra_projectiles"] = 0L,
            ["projectile_speed"] = 630.0 * (1.0 + Level("projectile_drive") * 0.12 + Level("ammo_packing") * 0.03),
            ["projectile_speed_multiplier"] = 1.0 + Level("projectile_drive") * 0.12 + Level("ammo_packing") * 0.03,
            ["missile_speed_multiplier"] = 1.0 + Level("missile_engines") * 0.12,
            ["missile_blast_radius"] = 0.22 + Level("missile_blast") * 0.045,
            ["missile_turn_rate"] = 3.5 + Level("missile_guidance") * 0.35,
            ["interceptor_range_multiplier"] = 1.0 + Level("alien_kinetic_range") * 0.18 + (assault ? 4.0 : 0.0),
            ["laser_range_multiplier"] = (1.0 + Level("laser_range") * 0.08) * (1.0 + Level("alien_laser_range") * 0.12) * (assault ? 2.0 : 1.0),
            ["missile_range_multiplier"] = (1.0 + Level("alien_missile_range") * 0.18) * (assault ? 2.2 : 1.0),
            ["mothership_assault_unlocked"] = assault,
            ["weapon_range_bonus"] = new double[] { 0, 10, 20, 32 }[LegacyFrontierLevel()],
            ["death_blast_damage"] = DeathBlastBaseDamage * (1.0 + Level("death_blast") * 0.40),
            ["death_blast_radius"] = DeathBlastRadius,
            ["drone_armor"] = Math.Min(0.75, Level("drone_armor") * 0.015),
            ["factory_interval_multiplier"] = factoryMultiplier,
            ["shield_max"] = ShieldMax(),
        };
        var fx = TechEffects();
        foreach (var (key, bonus) in new[] { ("damage", "kinetic_damage_bonus"), ("fire_rate", "kinetic_fire_rate_bonus"), ("laser_damage", "laser_damage_bonus"), ("laser_fire_rate", "laser_fire_rate_bonus"), ("missile_damage", "missile_damage_bonus"), ("missile_fire_rate", "missile_fire_rate_bonus"), ("projectile_speed", "kinetic_projectile_speed_bonus"), ("projectile_speed_multiplier", "kinetic_projectile_speed_bonus"), ("missile_speed_multiplier", "missile_speed_bonus"), ("missile_blast_radius", "missile_blast_radius_bonus"), ("missile_turn_rate", "missile_turn_bonus"), ("interceptor_range_multiplier", "kinetic_range_bonus"), ("laser_range_multiplier", "laser_range_bonus"), ("missile_range_multiplier", "missile_range_bonus"), ("death_blast_damage", "death_damage_bonus"), ("death_blast_radius", "death_radius_bonus") })
            result[key] = result.N(key) * (1 + fx.N(bonus));
        result["factory_interval_multiplier"] = result.N("factory_interval_multiplier") / (1 + fx.N("production_speed_bonus"));
        result["mothership_assault_unlocked"] = result.B("mothership_assault_unlocked") || HasResearch("C_G1");
        foreach (var (key, value) in fx)
            result[key] = value;
        return result;
    }
    public DataMap PatrolStats(string kind)
    {
        double[] values = kind switch
        {
            "laser" => new[] { 1.60, .90, .78, 60 },
            "missile" => new[] { 1.90, 1.0, .68, 70 },
            _ => new[] { 1.25, .75, .95, 45 }
        };
        double coverage = CombatSettings.N("patrol_coverage_multiplier", 2);
        var basis = new DataMap { ["patrol_radius"] = values[0] * coverage, ["patrol_outer_range"] = values[1] * coverage, ["patrol_speed"] = values[2], ["health"] = values[3] };
        string driveId = PerkCatalog.Kinds.Contains(kind) ? kind + "_drive" : "interceptor_drive";
        bool assault = Level("mothership_assault") > 0;
        var result = new DataMap
        {
            ["patrol_radius"] = basis.N("patrol_radius") * (1.0 + Level("patrol_radius") * 0.10 + Level("orbit_navigation") * 0.04 + Level("patrol_command") * 0.06) * (1.0 + Level("alien_navigation") * 0.12) + (assault ? 2.0 : 0.0),
            ["patrol_outer_range"] = basis.N("patrol_outer_range") * (1.0 + Level("patrol_outer") * 0.12 + Level("orbit_navigation") * 0.05 + Level("patrol_command") * 0.08) * (1.0 + Level("alien_navigation") * 0.20) + (assault ? 6.0 : 0.0) + new double[] { 0, 24, 48, 80 }[LegacyFrontierLevel()],
            ["patrol_speed"] = basis.N("patrol_speed") * (1.0 + Level("patrol_speed") * 0.10 + Level(driveId) * 0.065 + Level("patrol_command") * 0.04) * (1.0 + Level("alien_propulsion") * 0.10),
            ["health"] = basis.N("health") * CombatSettings.N("drone_base_health_multiplier", 0.65) * (1.0 + Level("drone_armor") * 0.12) * (1.0 + Level("defense_network") * 0.05) * (1.0 + Level("alien_field_support") * 0.08),
            ["repair_threshold"] = Math.Min(0.75, 0.35 + Level("repair_protocol") * 0.02),
            ["repair_rate"] = 0.12 * (1.0 + Level("field_repair") * 0.18 + Level("combat_logistics") * 0.08) * (1.0 + Level("alien_field_support") * 0.12),
            ["factory_interval_multiplier"] = 1.0 / (1.0 + Level("factory_automation") * 0.12 + Level("combat_logistics") * 0.08),
        };
        var fx = TechEffects();
        foreach (var (key, bonus) in new[] { ("patrol_radius", "patrol_radius_bonus"), ("patrol_outer_range", "patrol_outer_bonus"), ("patrol_speed", "patrol_speed_bonus"), ("health", "health_bonus"), ("repair_rate", "repair_speed_bonus") })
            result[key] = result.N(key) * (1 + fx.N(bonus));
        result["factory_interval_multiplier"] = result.N("factory_interval_multiplier") / (1 + fx.N("production_speed_bonus"));
        result["action_radius"] = fx.N("action_radius");
        result["strategic_enabled"] = fx.B("strategic_enabled");
        result["strategic_speed_multiplier"] = fx.N("strategic_speed_multiplier", 1);
        result["combat_turn_rate"] = 4.2 * (1 + fx.N("combat_turn_bonus") + (kind == "laser" ? fx.N("laser_turn_bonus") : 0));
        result["return_speed_multiplier"] = 1 + fx.N("return_speed_bonus");
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
        return Math.Max(1, (int)Math.Floor(CombatSettings.N(kind + "_factory_capacity", kind == "interceptor" ? 3 : 2) * CombatSettings.N("factory_capacity_multiplier", 2) * CombatSettings.N("factory_starting_capacity_factor", .5))) + Tech.I("hangar_capacity") + TechEffects().I("capacity_add");
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
    public double FactorySpawnIntervalForSite(string kind, long site = -1, string airframeId = "") => Math.Max(.5, CombatSettings.N("factory_spawn_interval") * FactoryPatrolStats(kind, site).N("factory_interval_multiplier") * AirframeCatalog.Definition(airframeId.Length > 0 ? airframeId : FactoryAirframe(kind, site)).N("spawn_multiplier", 1));
    public void PrimeFactoryBaseStats(DataMap weapons, DataMap? patrol = null)
    {
        _primedWeapons = weapons.DeepClone();
        _primedPatrol = patrol?.DeepClone();
        _weaponCache.Clear();
        _patrolCache.Clear();
    }
}
