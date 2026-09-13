using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Earthward.Domain;

public static class DomainBalance
{
    private static long _revision=-1,_referencesRevision=-1;
    private static DataMap _values=new();
    private static Dictionary<string,DataMap> _patrol=new(),_rewards=new(),_successors=new();
    private static Dictionary<int,DataMap> _frontiers=new();
    public static void Validate()
    {
        if(_revision==CatalogData.Revision)return;
        var values=new DataMap();var patrol=new Dictionary<string,DataMap>();var rewards=new Dictionary<string,DataMap>();
        var table=CatalogData.ReadCsv("domain_balance.csv");table.RequireHeaders("key","value","minimum","maximum","unit","description");
        foreach(var r in table.Rows){string key=r.String("key");double n=r.Number("value"),min=r.Number("minimum"),max=r.Number("maximum");if(key.Length==0||min>max||n<min||n>max||!values.TryAdd(key,n))throw r.Error("value","invalid bounds, duplicate key or out-of-range value");}
        table=CatalogData.ReadCsv("patrol_bases.csv");table.RequireHeaders("kind","patrol_radius","patrol_outer_range","patrol_speed","health");
        foreach(var r in table.Rows){var row=new DataMap();foreach(string key in table.Headers.Where(k=>k!="kind")){double n=r.Number(key);if(n<=0)throw r.Error(key,"must be positive");row[key]=n;}if(!patrol.TryAdd(r.String("kind"),row))throw r.Error("kind","duplicate kind");}
        if(!patrol.Keys.ToHashSet().SetEquals(new[]{"interceptor","laser","missile"}))throw new InvalidDataException("patrol_bases.csv: all three factory kinds are required");
        table=CatalogData.ReadCsv("kill_rewards.csv");table.RequireHeaders("kind","minerals","energy","science","score");
        foreach(var r in table.Rows)
        {
            var row=new DataMap();foreach(string key in new[]{"minerals","energy","science"}){double n=r.Number(key);if(n<0)throw r.Error(key,"reward cannot be negative");row[key]=n;}
            long score=r.Integer("score");if(score<0)throw r.Error("score","reward cannot be negative");row["score"]=score;
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
        foreach(string key in new[]{"initial_minerals","initial_energy","initial_science","earth_max_health","death_blast_damage","death_blast_radius","mineral_output_base","energy_output_base","science_output_base","shield_capacity_base","shield_regeneration_base","building_cost_per_existing","building_cost_late_factor","building_cost_late_start","building_cost_late_power","wave_minerals_base","wave_minerals_increment","wave_energy_base","wave_energy_increment","wave_science_base","wave_science_increment","wave_score_base","wave_score_increment","wave_shield_budget","resource_core_drop_chance","alien_point_drop_chance","laser_intel_wave","kinetic_fire_rate_base","laser_fire_rate_base","missile_fire_rate_base","kinetic_projectile_speed_base","missile_blast_radius_base","missile_turn_rate_base","drone_armor_cap","repair_threshold_base","repair_threshold_cap","repair_rate_base","tactical_turn_rate_base","factory_spawn_interval_min","resource_core_reference_fraction"})if(!values.ContainsKey(key))throw new InvalidDataException("domain_balance.csv: missing required parameter "+key);
        foreach(string key in new[]{"resource_core_drop_chance","alien_point_drop_chance"})if(!DataMap.ValidNumber(values.Value(key),0,1))throw new InvalidDataException("domain_balance.csv: drop chance must be between zero and one: "+key);
        if(!rewards.Keys.ToHashSet().SetEquals(new[]{"scout","carrier","mothership"}))throw new InvalidDataException("kill_rewards.csv: scout, carrier and mothership rewards are required");
        _values=values;_patrol=patrol;_rewards=rewards;_successors=successors;_frontiers=frontiers;_revision=CatalogData.Revision;
    }
    public static void ValidateReferences()
    {
        if(_referencesRevision==CatalogData.Revision)return;Validate();
        var attributes=CatalogData.Rows("deep-technology.json","nodes").SelectMany(r=>r.Map("values").Keys).ToHashSet();
        foreach(var row in CatalogData.ReadCsv("successor_research.csv").Rows)if(!attributes.Contains(row.String("attribute")))throw row.Error("attribute","unknown technology attribute");
        _referencesRevision=CatalogData.Revision;
    }
    public static double Value(string key){Validate();return _values.ContainsKey(key)?_values.N(key):throw new InvalidDataException("domain_balance.csv: required parameter missing: "+key);}
    public static DataMap Patrol(string kind){Validate();return _patrol.GetValueOrDefault(kind,_patrol["interceptor"]).DeepClone();}
    public static DataMap Successor(string branch){Validate();return _successors[branch];}
    public static DataMap Frontier(int stage){Validate();return _frontiers[stage];}
    public static DataMap KillReward(string kind){Validate();return _rewards.TryGetValue(kind,out var row)?row.DeepClone():new();}
}
