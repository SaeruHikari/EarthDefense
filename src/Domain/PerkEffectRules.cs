using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Earthward.Domain;

internal static class PerkEffectRules
{
    private sealed record Rule(string Field,string Operation,double Base,double PerLevel,double Offset,double? Maximum,string Setting,bool Integer,double Step,double Threshold,double High);
    private static long _revision=-1;
    private static Dictionary<string,List<Rule>> _rules=new(StringComparer.Ordinal);
    private static Dictionary<int,DataMap> _tiers=new();
    private static Dictionary<string,DataMap> _bounds=new();
    private static void Ensure()
    {
        if(_revision==CatalogData.Revision)return;
        var definitions=PerkCatalog.Entries.Select(d=>d.S("id")).ToHashSet();var neutral=PerkCatalog.NeutralModifiers();var settings=PerkCatalog.DefaultSettings;
        var table=CatalogData.ReadCsv("perk_effect_rules.csv");table.RequireHeaders("perk_id","attribute","operation","base","per_level","level_offset","maximum","coefficient_setting","value_type","step","threshold","high");
        var rules=new Dictionary<string,List<Rule>>(StringComparer.Ordinal);var seen=new HashSet<string>();
        foreach(var row in table.Rows)
        {
            string id=row.String("perk_id"),field=row.String("attribute"),op=row.String("operation"),setting=row.String("coefficient_setting");
            if(!definitions.Contains(id)||!neutral.ContainsKey(field)||!seen.Add(id+":"+field))throw row.Error("perk_id","unknown perk, unknown modifier or duplicate effect");
            if(op is not("linear" or "reciprocal" or "floor_step" or "threshold")||row.String("value_type") is not("integer" or "number")||setting.Length>0&&!settings.ContainsKey(setting))throw row.Error("operation","invalid operation, result type or coefficient setting");
            var rule=new Rule(field,op,row.Number("base"),row.Number("per_level"),row.Number("level_offset"),row.String("maximum").Length==0?null:row.Number("maximum"),setting,row.String("value_type")=="integer",row.Number("step"),row.Number("threshold"),row.Number("high"));
            if(rule.Step<=0||rule.Offset<0||rule.Maximum<0)throw row.Error("step","invalid step/offset/cap");
            if(!rules.ContainsKey(id))rules[id]=new();rules[id].Add(rule);
        }
        if(!definitions.SetEquals(rules.Keys))throw new InvalidDataException("perk_effect_rules.csv: every perk must have an implemented effect rule");
        var tierTable=CatalogData.ReadCsv("perk_tiers.csv");tierTable.RequireHeaders("stage","max_level","upgrade_base_cost","upgrade_growth");var tiers=new Dictionary<int,DataMap>();
        foreach(var row in tierTable.Rows)
        {
            int stage=(int)row.Integer("stage");long max=row.Integer("max_level"),basis=row.Integer("upgrade_base_cost");double growth=row.Number("upgrade_growth");
            if(stage is <0 or >1||max is <1 or >10000||basis<1||growth<1||!tiers.TryAdd(stage,new(){["max_level"]=max,["upgrade_base_cost"]=basis,["upgrade_growth"]=growth}))throw row.Error("stage","invalid or duplicate tier");
        }
        if(tiers.Count!=2)throw new InvalidDataException("perk_tiers.csv: both normal and advanced tiers are required");
        var boundsTable=CatalogData.ReadCsv("perk_setting_bounds.csv");boundsTable.RequireHeaders("key","minimum","maximum","integer");var bounds=new Dictionary<string,DataMap>();
        foreach(var row in boundsTable.Rows){string key=row.String("key");double min=row.Number("minimum"),max=row.Number("maximum");bool whole=row.Boolean("integer");if(!settings.ContainsKey(key)||!DataMap.ValidNumber(settings.Value(key),min,max,whole)||!bounds.TryAdd(key,new(){["minimum"]=min,["maximum"]=max,["integer"]=whole}))throw row.Error("key","unknown, duplicate or invalid default/bounds");}
        if(bounds.Count!=settings.Count)throw new InvalidDataException("perk_setting_bounds.csv: every setting requires bounds");
        _rules=rules;_tiers=tiers;_bounds=bounds;_revision=CatalogData.Revision;
    }
    public static void Validate()=>Ensure();
    public static DataMap Bounds(string key){Ensure();return _bounds[key];}
    public static DataMap Tier(int stage){Ensure();return _tiers[stage];}
    public static DataMap Evaluate(string id,int level,DataMap settings)
    {
        if(level<=0)return new();Ensure();if(!_rules.TryGetValue(id,out var rules))return new();var result=new DataMap();
        foreach(var rule in rules)
        {
            double coefficient=rule.Setting.Length==0?rule.PerLevel:settings.N(rule.Setting),rank=level-rule.Offset;
            double value=rule.Operation switch
            {
                "reciprocal"=>1d/(rule.Base+coefficient*rank),
                "floor_step"=>rule.Base+coefficient*Math.Floor(rank/rule.Step),
                "threshold"=>level>=rule.Threshold?rule.High:rule.Base,
                _=>rule.Base+coefficient*rank
            };
            if(rule.Maximum is double maximum)value=Math.Min(maximum,value);
            if(!double.IsFinite(value))throw new InvalidDataException("perk_effect_rules.csv: nonfinite effect "+id+"/"+rule.Field);
            result[rule.Field]=rule.Integer?(object)(long)value:value;
        }
        return result;
    }
}
