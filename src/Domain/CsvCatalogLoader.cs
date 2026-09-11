using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
namespace Earthward.Domain;

internal static class CsvCatalogLoader
{
    private sealed record TableSpec(string File,string Catalog,string Parent,string Field,string Shape,string Id,string Key,string Presence);
    internal static Dictionary<string,DataMap> Load(Func<string,CsvTable> read)
    {
        var manifest=read("catalog_tables.csv");manifest.RequireHeaders("table","catalog","parent_table","parent_field","shape","id_column","key_column","presence_column");
        var columns=read("catalog_columns.csv");columns.RequireHeaders("table","column","type","required");
        var specs=new List<TableSpec>();var names=new HashSet<string>();
        foreach(var row in manifest.Rows)
        {
            var s=new TableSpec(row.String("table"),row.String("catalog"),row.String("parent_table"),row.String("parent_field"),row.String("shape"),row.String("id_column"),row.String("key_column"),row.String("presence_column"));
            if(!names.Add(s.File)||s.Shape is not("object" or "object_list" or "map" or "map_list" or "list"))throw row.Error("table","duplicate table or unsupported shape");
            if(s.Parent.Length>0&&!names.Contains(s.Parent))throw row.Error("parent_table","parent must be declared before child");specs.Add(s);
        }
        var fields=columns.Rows.GroupBy(r=>r.String("table")).ToDictionary(g=>g.Key,g=>g.ToList());
        foreach(var row in columns.Rows)
            if(!names.Contains(row.String("table"))||row.String("type") is not("string" or "number" or "integer" or "bool"))throw row.Error("table","unknown table or scalar type");
        var catalogs=new Dictionary<string,DataMap>(StringComparer.Ordinal);var records=new Dictionary<string,Dictionary<string,DataMap>>(StringComparer.Ordinal);
        foreach(var spec in specs)
        {
            var table=read(spec.File);table.RequireHeaders("parent_id","position");
            var childSpecs=specs.Where(s=>s.Parent==spec.File).ToList();var scalars=fields.GetValueOrDefault(spec.File,new());
            var allowed=new HashSet<string>{"parent_id","position"};
            if(spec.Shape is "object" or "object_list")
            {
                table.RequireHeaders(spec.Id);allowed.Add(spec.Id);
                foreach(var field in scalars){table.RequireHeaders(field.String("column"));if(!allowed.Add(field.String("column"))&&field.String("column")!=spec.Id)throw field.Error("column","duplicate scalar column");}
                foreach(var child in childSpecs){table.RequireHeaders(child.Presence);allowed.Add(child.Presence);}
            }
            else{table.RequireHeaders("type","value");allowed.Add("type");allowed.Add("value");if(spec.Key.Length>0){table.RequireHeaders(spec.Key);allowed.Add(spec.Key);}}
            if(table.Headers.Any(h=>!allowed.Contains(h)))throw new InvalidDataException(spec.File+":1: unknown column "+table.Headers.First(h=>!allowed.Contains(h)));
            var owned=new Dictionary<string,DataMap>(StringComparer.Ordinal);records[spec.File]=owned;
            var positions=new Dictionary<string,HashSet<long>>();
            foreach(var row in table.Rows.OrderBy(r=>r.Integer("position")))
            {
                string parentId=row.String("parent_id"),key=spec.Key.Length>0?row.String(spec.Key):"";long position=row.Integer("position");
                string group=parentId+(spec.Shape=="map_list"?"/"+key:"");
                if(position<0||!positions.TryGetValue(group,out var seen)&&!(positions.TryAdd(group,seen=new()))||!seen!.Add(position))throw row.Error("position","negative or duplicate row order");
                DataMap? parent=null;
                if(spec.Parent.Length>0&&(!records[spec.Parent].TryGetValue(parentId,out parent)||!parent.ContainsKey(spec.Field)))throw row.Error("parent_id","unknown owner or disabled relation "+parentId);
                if(spec.Shape is "object" or "object_list")
                {
                    string id=row.String(spec.Id);if(id.Length==0||owned.ContainsKey(id))throw row.Error(spec.Id,"empty or duplicate record ID");var value=new DataMap();
                    foreach(var field in scalars)
                    {
                        string name=field.String("column");if(!field.Boolean("required")&&row.String(name).Length==0)continue;
                        value[name]=ReadScalar(row,name,field.String("type"));
                    }
                    foreach(var child in childSpecs)if(row.Boolean(child.Presence))value[child.Field]=child.Shape is "object_list" or "list"?new List<object?>():new DataMap();
                    owned[id]=value;
                    if(parent==null){if(spec.Parent.Length>0||parentId.Length>0||position!=0||!catalogs.TryAdd(spec.Catalog,value))throw row.Error(spec.Id,"duplicate or malformed catalog root");}
                    else if(spec.Shape=="object_list")parent.List(spec.Field).Add(value);
                    else parent[spec.Field]=value;
                }
                else
                {
                    if(parent==null)throw row.Error("parent_id","relation has no owner");var value=ReadScalar(row,"value",row.String("type"));
                    if(spec.Shape=="list")parent.List(spec.Field).Add(value);
                    else if(spec.Shape=="map") { if(key.Length==0||!parent.Map(spec.Field).TryAdd(key,value))throw row.Error(spec.Key,"empty or duplicate attribute"); }
                    else{var map=parent.Map(spec.Field);if(!map.ContainsKey(key))map[key]=new List<object?>();map.List(key).Add(value);}
                }
            }
            foreach(var group in positions)if(group.Value.Count>0&&(group.Value.Min()!=0||group.Value.Max()!=group.Value.Count-1))throw new InvalidDataException(spec.File+": non-contiguous position for "+group.Key);
        }
        Validate(catalogs,read);return catalogs;
    }
    private static object ReadScalar(CsvRow row,string column,string type)=>type switch
    {
        "string"=>row.String(column),"bool"=>row.Boolean(column),"integer"=>row.Integer(column),
        "number"=>long.TryParse(row.String(column),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out long n)?n:row.Number(column),
        _=>throw row.Error(column,"unknown scalar type "+type)
    };
    private static void Validate(Dictionary<string,DataMap> all,Func<string,CsvTable> read)
    {
        foreach(string name in new[]{"deep-technology.json","airframes.json","perks.json","economy.json","expedition.json","legacy-technology.json"})if(!all.ContainsKey(name))throw new InvalidDataException("catalog_tables.csv: missing required catalog "+name);
        var nodes=all["deep-technology.json"].List("nodes").Cast<DataMap>().ToDictionary(n=>n.S("id"));
        var branches=all["deep-technology.json"].List("branches").Cast<DataMap>().Select(n=>n.S("id")).ToHashSet();
        var attributeTable=read("technology_attribute_schema.csv");attributeTable.RequireHeaders("attribute","type");var attributes=new Dictionary<string,string>();
        foreach(var row in attributeTable.Rows)if(!attributes.TryAdd(row.String("attribute"),row.String("type")))throw row.Error("attribute","duplicate attribute");
        var path=new HashSet<string>();var done=new HashSet<string>();
        void Visit(string id)
        {
            if(!nodes.TryGetValue(id,out var node))throw new InvalidDataException("deep_technology_nodes_requires.csv: unknown prerequisite "+id);
            if(done.Contains(id))return;if(!path.Add(id))throw new InvalidDataException("deep_technology_nodes_requires.csv: cycle at "+id);
            foreach(var v in node.List("requires")){if(v is not string parent)throw new InvalidDataException("deep_technology_nodes_requires.csv: prerequisite must be ID");Visit(parent);}path.Remove(id);done.Add(id);
        }
        foreach(var (id,node)in nodes)
        {
            if(!branches.Contains(node.S("branch"))||node.S("size") is not("small" or "medium" or "large")||node.I("max")!=1)throw new InvalidDataException("deep_technology_nodes.csv: invalid branch/size/max for "+id);
            if(node.Map("values").Count==0||node.S("size")=="small"&&(node.Map("values").Count>2||node.Map("cost").Keys.Any(k=>k!="science")))throw new InvalidDataException("deep_technology_nodes.csv: invalid small-node contract "+id);
            foreach(var(k,v)in node.Map("values"))if(!attributes.TryGetValue(k,out string? type)||type=="bool"&&v is not bool||type=="number"&&!DataMap.ValidNumber(v,-1e100,1e100))throw new InvalidDataException("deep_technology_nodes_values.csv: unknown or invalid attribute "+id+"/"+k);
            foreach(var(k,v)in node.Map("cost"))if(k is not("minerals" or "energy" or "science" or "alien_points")||!DataMap.ValidNumber(v,0,1e100,k=="alien_points"))throw new InvalidDataException("deep_technology_nodes_cost.csv: invalid currency "+id+"/"+k);
            Visit(id);
        }
        var frames=all["airframes.json"].List("definitions").Cast<DataMap>().ToDictionary(n=>n.S("id"));
        var kinds=new HashSet<string>{"interceptor","laser","missile"};
        foreach(var(id,frame)in frames)
            if(frame.Any(p=>p.Key.EndsWith("_multiplier")&&!DataMap.ValidNumber(p.Value,0.000001,1e12))||!kinds.Contains(frame.S("kind"))||frame.I("capacity_cost") is <1 or >2||frame.S("unlock_node").Length>0&&!nodes.ContainsKey(frame.S("unlock_node")))throw new InvalidDataException("airframes_definitions.csv: invalid kind, capacity or research reference "+id);
        foreach(var(kind,id)in all["airframes.json"].Map("base"))if(id is not string frame||!frames.ContainsKey(frame)||frames[frame].S("kind")!=kind)throw new InvalidDataException("airframes_base.csv: invalid base airframe "+kind);
        var perks=all["perks.json"].List("definitions").Cast<DataMap>().ToDictionary(n=>n.S("id"));
        foreach(var(id,perk)in perks)if(perk.S("layer") is not("factory" or "aircraft")||perk.List("kinds").Any(k=>k is not string kind||!kinds.Contains(kind))||perk.List("airframes").Any(f=>f is not string frame||!frames.ContainsKey(frame)))throw new InvalidDataException("perks_definitions.csv: invalid layer/kind/airframe "+id);
        var economy=all["economy.json"];
        foreach(var(key,value)in economy.Map("settings"))
        {
            var limits=economy.Map("ranges").List(key);if(limits.Count!=2||DataMap.Number(limits[0])>DataMap.Number(limits[1])||!DataMap.ValidNumber(value,DataMap.Number(limits[0]),DataMap.Number(limits[1]),economy.List("integer_settings").Contains(key)))throw new InvalidDataException("economy_settings.csv: default outside valid range "+key);
        }
        foreach(var building in economy.List("buildings").Cast<DataMap>())if(building.S("unlock_research").Length>0&&!nodes.ContainsKey(building.S("unlock_research")))throw new InvalidDataException("economy_buildings.csv: unknown research "+building.S("id"));
        var legacy=all["legacy-technology.json"].List("definitions").Cast<DataMap>().Select(n=>n.S("id")).ToHashSet();
        foreach(var node in all["legacy-technology.json"].List("definitions").Cast<DataMap>())foreach(string field in new[]{"requires","legacy_requires","purchase_requires"})foreach(var (key,value)in node.Map(field))if(!legacy.Contains(key)||!DataMap.ValidNumber(value,0,10000,true))throw new InvalidDataException("legacy_technology_definitions_"+field+".csv: invalid legacy reference "+key);
    }
}
