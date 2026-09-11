using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Earthward.Domain;

public static class DomainBalance
{
    private static long _revision=-1,_referencesRevision=-1;
    private static DataMap _values=new(),_legacy=new();
    private static Dictionary<string,DataMap> _patrol=new(),_rewards=new(),_successors=new();
    private static Dictionary<int,DataMap> _frontiers=new();
    public static void Validate()
    {
        if(_revision==CatalogData.Revision)return;
        var values=new DataMap();var legacy=new DataMap();var patrol=new Dictionary<string,DataMap>();var rewards=new Dictionary<string,DataMap>();
        var table=CatalogData.ReadCsv("domain_balance.csv");table.RequireHeaders("key","value","minimum","maximum","unit","description");
        foreach(var r in table.Rows){string key=r.String("key");double n=r.Number("value"),min=r.Number("minimum"),max=r.Number("maximum");if(key.Length==0||min>max||n<min||n>max||!values.TryAdd(key,n))throw r.Error("value","invalid bounds, duplicate key or out-of-range value");}
        table=CatalogData.ReadCsv("legacy_effect_coefficients.csv");table.RequireHeaders("key","technology_id","value","source");
        foreach(var r in table.Rows)if(!legacy.TryAdd(r.String("key"),r.Number("value")))throw r.Error("key","duplicate coefficient");
        table=CatalogData.ReadCsv("patrol_bases.csv");table.RequireHeaders("kind","patrol_radius","patrol_outer_range","patrol_speed","health");
        foreach(var r in table.Rows){var row=new DataMap();foreach(string key in table.Headers.Where(k=>k!="kind")){double n=r.Number(key);if(n<=0)throw r.Error(key,"must be positive");row[key]=n;}if(!patrol.TryAdd(r.String("kind"),row))throw r.Error("kind","duplicate kind");}
        if(!patrol.Keys.ToHashSet().SetEquals(new[]{"interceptor","laser","missile"}))throw new InvalidDataException("patrol_bases.csv: all three factory kinds are required");
        table=CatalogData.ReadCsv("kill_rewards.csv");table.RequireHeaders("kind","minerals","energy","science","score","alien_points","wave_alien_reward","resource_core_setting","first_medium_boss");
        foreach(var r in table.Rows)
        {
            var row=new DataMap();foreach(string key in new[]{"minerals","energy","science"}){double n=r.Number(key);if(n<0)throw r.Error(key,"reward cannot be negative");row[key]=n;}
            foreach(string key in new[]{"score","alien_points"}){long n=r.Integer(key);if(n<0)throw r.Error(key,"reward cannot be negative");row[key]=n;}
            row["wave_alien_reward"]=r.Boolean("wave_alien_reward");row["first_medium_boss"]=r.Boolean("first_medium_boss");row["resource_core_setting"]=r.String("resource_core_setting");
            if(!rewards.TryAdd(r.String("kind"),row))throw r.Error("kind","duplicate reward kind");
        }
        var successors=new Dictionary<string,DataMap>();table=CatalogData.ReadCsv("successor_research.csv");table.RequireHeaders("branch","attribute","display_name","increment","science_base","science_growth","alien_base","alien_growth");
        foreach(var r in table.Rows)
        {
            var value=new DataMap{["attribute"]=r.String("attribute"),["display_name"]=r.String("display_name")};
            foreach(string key in new[]{"increment","science_base","science_growth","alien_base","alien_growth"}){double n=r.Number(key);if(n<=0||key.EndsWith("growth")&&n<1)throw r.Error(key,"must be positive; growth at least1");value[key]=n;}
            if(!successors.TryAdd(r.String("branch"),value))throw r.Error("branch","duplicate continuation branch");
        }
        if(!successors.Keys.ToHashSet().SetEquals(new[]{"K","M","L","I","D","C"}))throw new InvalidDataException("successor_research.csv: all six branches required");
        var frontiers=new Dictionary<int,DataMap>();table=CatalogData.ReadCsv("legacy_frontiers.csv");table.RequireHeaders("stage","action_radius_base","weapon_range_bonus","patrol_outer_bonus");
        foreach(var r in table.Rows){var value=new DataMap();foreach(string key in table.Headers.Where(k=>k!="stage")){double n=r.Number(key);if(n<0)throw r.Error(key,"cannot be negative");value[key]=n;}int stage=(int)r.Integer("stage");if(stage<0||stage>3||!frontiers.TryAdd(stage,value))throw r.Error("stage","invalid or duplicate stage");}
        if(frontiers.Count!=4)throw new InvalidDataException("legacy_frontiers.csv: all four legacy stages required");
        foreach(string key in new[]{"initial_minerals","initial_energy","initial_science","earth_max_health","death_blast_damage","death_blast_radius","mineral_output_base","energy_output_base","science_output_base","legacy_industry_growth","shield_capacity_base","shield_regeneration_base","building_cost_per_existing","building_cost_late_factor","building_cost_late_start","building_cost_late_power","repair_minerals","repair_energy","repair_earth_health","repair_shield_budget","sacrifice_shield_budget_fraction","wave_minerals_base","wave_minerals_increment","wave_energy_base","wave_energy_increment","wave_science_base","wave_science_increment","wave_score_base","wave_score_increment","wave_shield_budget","alien_reward_base","alien_reward_wave_interval","alien_reward_stage_increment","laser_intel_wave","kinetic_fire_rate_base","laser_fire_rate_base","missile_fire_rate_base","kinetic_projectile_speed_base","missile_blast_radius_base","missile_turn_rate_base","advanced_weapon_rank_damage","spread_bullet_count","spread_secondary_damage","drone_armor_cap","repair_threshold_base","repair_threshold_cap","repair_rate_base","tactical_turn_rate_base","factory_spawn_interval_min","legacy_interceptor_assault_range_add","legacy_laser_assault_range_factor","legacy_missile_assault_range_factor","legacy_patrol_assault_radius_add","legacy_patrol_assault_outer_add","legacy_drive_speed_per_level","legacy_basic_research_cost_growth","legacy_advanced_research_cost_growth","resource_core_reference_fraction"})if(!values.ContainsKey(key))throw new InvalidDataException("domain_balance.csv: missing required parameter "+key);
        foreach(string key in new[]{"mining_1","industrial_synergy_1","mineral_processing_1","energy_grid_1","laser_capacitors_1","photonic_mastery_1","research_methods_1","shield_1","defense_network_1","shield_2","shield_regen_1","defense_network_2","industrial_synergy_2","repair_protocol_1","salvage_1","salvage_2","industrial_mastery_1","damage_1","factory_automation_1","combat_logistics_1","kinetic_coils_1","kinetic_mastery_1","rapid_1","fire_control_1","ammo_packing_1","laser_focus_1","laser_capacitors_2","photonic_mastery_2","warhead_1","missile_mastery_1","laser_cycling_1","laser_cooling_1","photonic_mastery_3","missile_feed_1","missile_mastery_2","projectile_drive_1","ammo_packing_2","missile_engines_1","missile_blast_1","missile_guidance_1","alien_kinetic_range_1","laser_range_1","alien_laser_range_1","alien_missile_range_1","death_blast_1","drone_armor_1","patrol_radius_1","orbit_navigation_1","patrol_command_1","alien_navigation_1","patrol_outer_1","orbit_navigation_2","patrol_command_2","alien_navigation_2","patrol_speed_1","patrol_command_3","alien_propulsion_1","drone_armor_2","defense_network_3","alien_field_support_1","repair_protocol_2","field_repair_1","alien_field_support_2"})if(!legacy.ContainsKey(key))throw new InvalidDataException("legacy_effect_coefficients.csv: missing required coefficient "+key);
        _values=values;_legacy=legacy;_patrol=patrol;_rewards=rewards;_successors=successors;_frontiers=frontiers;_revision=CatalogData.Revision;
    }
    public static void ValidateReferences()
    {
        if(_referencesRevision==CatalogData.Revision)return;Validate();
        var legacyIds=CatalogData.Rows("legacy-technology.json","definitions").Select(r=>r.S("id")).ToHashSet();
        foreach(var row in CatalogData.ReadCsv("legacy_effect_coefficients.csv").Rows)if(!legacyIds.Contains(row.String("technology_id")))throw row.Error("technology_id","unknown legacy technology");
        var settings=CatalogData.Load("economy.json").Map("settings");
        foreach(var row in CatalogData.ReadCsv("kill_rewards.csv").Rows)if(row.String("resource_core_setting").Length>0&&!settings.ContainsKey(row.String("resource_core_setting")))throw row.Error("resource_core_setting","unknown setting");
        var attributes=CatalogData.Rows("deep-technology.json","nodes").SelectMany(r=>r.Map("values").Keys).ToHashSet();
        foreach(var row in CatalogData.ReadCsv("successor_research.csv").Rows)if(!attributes.Contains(row.String("attribute")))throw row.Error("attribute","unknown technology attribute");
        _referencesRevision=CatalogData.Revision;
    }
    public static double Value(string key){Validate();return _values.ContainsKey(key)?_values.N(key):throw new InvalidDataException("domain_balance.csv: required parameter missing: "+key);}
    public static double Legacy(string key){Validate();return _legacy.ContainsKey(key)?_legacy.N(key):throw new InvalidDataException("legacy_effect_coefficients.csv: required coefficient missing: "+key);}
    public static DataMap Patrol(string kind){Validate();return _patrol.GetValueOrDefault(kind,_patrol["interceptor"]).DeepClone();}
    public static DataMap Successor(string branch){Validate();return _successors[branch];}
    public static DataMap Frontier(int stage){Validate();return _frontiers[stage];}
    public static DataMap KillReward(string kind){Validate();return _rewards.TryGetValue(kind,out var row)?row.DeepClone():new();}
}
