using System;
using System.Collections.Generic;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public DataMap DroneStats()
    {
        double damageMultiplier = CombatSettings.N("drone_base_damage_multiplier", .65);
        var result = new DataMap
        {
            ["damage"] = CombatSettings.N("drone_damage") * damageMultiplier,
            ["fire_rate"] = DomainBalance.Value("kinetic_fire_rate_base"),
            ["spread"] = 0d,
            ["bullet_count"] = 1L,
            ["bullet_damage_weights"] = new List<object?> { 1d },
            ["laser"] = Buildings.L("laser") * FactoryCapacity("laser"),
            ["missile"] = Buildings.L("missile") * FactoryCapacity("missile"),
            ["interceptor"] = Buildings.L("interceptor") * FactoryCapacity("interceptor"),
            ["laser_damage"] = CombatSettings.N("laser_damage") * damageMultiplier,
            ["missile_damage"] = CombatSettings.N("missile_damage") * damageMultiplier,
            ["laser_fire_rate"] = DomainBalance.Value("laser_fire_rate_base"),
            ["missile_fire_rate"] = DomainBalance.Value("missile_fire_rate_base"),
            ["laser_beam_count"] = 1L,
            ["missile_salvo"] = 1L,
            ["missile_extra_projectiles"] = 0L,
            ["projectile_speed"] = DomainBalance.Value("kinetic_projectile_speed_base"),
            ["projectile_speed_multiplier"] = 1.0,
            ["missile_speed_multiplier"] = 1.0,
            ["missile_blast_radius"] = DomainBalance.Value("missile_blast_radius_base"),
            ["missile_turn_rate"] = DomainBalance.Value("missile_turn_rate_base"),
            ["interceptor_range_multiplier"] = 1.0,
            ["laser_range_multiplier"] = 1.0,
            ["missile_range_multiplier"] = 1.0,
            ["mothership_assault_unlocked"] = false,
            ["weapon_range_bonus"] = 0d,
            ["death_blast_damage"] = DeathBlastBaseDamage,
            ["death_blast_radius"] = DeathBlastRadius,
            ["drone_armor"] = 0d,
            ["factory_interval_multiplier"] = 1.0,
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
        var result = new DataMap
        {
            ["patrol_radius"] = basis.N("patrol_radius"),
            ["patrol_outer_range"] = basis.N("patrol_outer_range"),
            ["patrol_speed"] = basis.N("patrol_speed"),
            ["health"] = basis.N("health") * CombatSettings.N("drone_base_health_multiplier", 0.65),
            ["repair_threshold"] = Math.Min(DomainBalance.Value("repair_threshold_cap"), DomainBalance.Value("repair_threshold_base")),
            ["repair_rate"] = DomainBalance.Value("repair_rate_base"),
            ["factory_interval_multiplier"] = 1.0,
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
                if (key.EndsWith("_multiplier"))
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
