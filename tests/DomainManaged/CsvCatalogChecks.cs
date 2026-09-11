using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Earthward.Domain;

internal static class CsvCatalogChecks
{
    internal static void Run(Action<bool,string> check)
    {
        void Check(bool ok,string label)=>check(ok,"CSV: "+label);
        string root=Path.GetFullPath("data/domain");
        var parsed=CsvTable.Parse("\uFEFFid,note,n,enabled\r\na,\"quoted, value\",1.25,true\r\nb,\"two\r\nlines and \"\"quote\"\"\",-2,false\r\nc,,0,true\r\n","quoted.csv");
        Check(parsed.Rows.Count==3&&parsed.Rows[0].String("note")=="quoted, value"&&parsed.Rows[1].String("note")=="two\r\nlines and \"quote\""&&parsed.Rows[2].Line==5,"RFC4180 commas, escaped quotes, BOM, physical multiline numbers");
        Check(parsed.Rows[0].Number("n")==1.25&&parsed.Rows[1].Integer("n")==-2&&!parsed.Rows[1].Boolean("enabled")&&parsed.Rows[2].OptionalString("note","fallback")=="fallback","invariant typed cells and blank optional values");
        Check(CsvTable.Parse("a,b\nx,", "final.csv").Rows[0].String("b")=="","terminal empty cell preserved without newline");
        foreach(string bad in new[]{"","a,a\n1,2","a,b\n1","a\n\"unfinished","a\nnot\"quoted","a\n\"closed\"oops"})
        {bool failed=false;try{CsvTable.Parse(bad,"invalid.csv");}catch(InvalidDataException ex){failed=ex.Message.Contains("invalid.csv");}Check(failed,"strict parse refusal with source "+bad.Replace('\n','/'));}
        foreach(string bad in new[]{"NaN","Infinity","1,2",""})
        {bool failed=false;try{CsvTable.Parse("value\n\""+bad+"\"","number.csv").Rows[0].Number("value");}catch(InvalidDataException ex){failed=ex.Message.Contains("number.csv:2");}Check(failed,"finite invariant numeric validation "+bad);}
        var logical=new[]{"deep-technology.json","airframes.json","perks.json","economy.json","expedition.json","legacy-technology.json"};
        CatalogData.Configure(root);
        foreach(string name in logical)
        {
            var reference=DataMap.Parse(File.ReadAllText(Path.Combine("tests/DomainManaged/Fixtures/pre-csv",name)));
            // The subsequent user-requested C_N1 change broadens target eligibility without changing its1.2 value.
            if(name=="deep-technology.json")
            {
                foreach(var node in reference.List("nodes").Cast<DataMap>())
                {
                    string key=node.Map("values").Keys.FirstOrDefault(k=>k is "patrol_radius_bonus" or "patrol_outer_bonus")??"";
                    if(key.Length==0)continue;
                    node.Map("values")[key]=Math.Round(node.Map("values").N(key)*1.5,12);
                    node.Map("effect_definition")["value"]=Math.Round(node.Map("effect_definition").N("value")*1.5,12);
                    node["effects"]=node.List("effects").Cast<string>().Select(text=>(object?)System.Text.RegularExpressions.Regex.Replace(text,@"[+]([0-9.]+)%",match=>"+"+(double.Parse(match.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)*1.5).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"%")).ToList();
                }
                reference.List("nodes").Cast<DataMap>().Single(n=>n.S("id")=="C_G1").List("effects")[0]="母舰成为可攻击目标；动能无法穿透能量层，激光破盾后可接力";
                var ranging=reference.List("nodes").Cast<DataMap>().Single(n=>n.S("id")=="C_N1");
                ranging.Map("values")["all_target_range_multiplier"]=ranging.Map("values").Value("large_target_range_multiplier");ranging.Map("values").Remove("large_target_range_multiplier");
                ranging["effects"]=new List<object?>{"\u6240\u6709\u76ee\u6807\u7684\u6b66\u5668\u5c04\u7a0b +20%"};
            }
            var actual=CatalogData.Load(name).DeepClone();
            if(name=="deep-technology.json")
            {
                // User-approved adjacent-DAG reflow changes only these presentation fields.
                // Technology127Checks independently verifies their true depth, geometry and separation.
                // Costs, values, all requires, IDs and every other field still use exact old-fixture equality.
                foreach(var catalog in new[]{actual,reference})
                    foreach(var node in catalog.List("nodes").Cast<DataMap>())
                        foreach(string field in new[]{"layout_depth","layout_lane","layout_row","draw_position"})
                            node.Remove(field);
                if(!DataMap.Equivalent(actual,reference))
                {
                    var actualNodes=actual.List("nodes").Cast<DataMap>().ToDictionary(n=>n.S("id"));
                    foreach(var expectedNode in reference.List("nodes").Cast<DataMap>())foreach(var(key,val)in expectedNode)
                        if(!DataMap.Equivalent(actualNodes[expectedNode.S("id")].Value(key),val))
                            Console.Error.WriteLine("CSV_DIFF "+expectedNode.S("id")+"/"+key+" "+new DataMap{["expected"]=val,["actual"]=actualNodes[expectedNode.S("id")].Value(key)}.ToJson());
                }
            }
            Check(DataMap.Equivalent(actual,reference),"whole catalog field-for-field equality excluding independently audited layout "+name);
        }
        var fixtureStats=new DefenseState();fixtureStats.UnlockAllTechnologyCheat();var stats=fixtureStats.DroneStats().ToJson();var meta=fixtureStats.FactoryPerks.Snapshot();
        var reads=new Dictionary<string,int>();
        CatalogData.Configure(file=>{reads[file]=reads.GetValueOrDefault(file)+1;if(logical.Contains(file))throw new InvalidDataException("JSON runtime fallback forbidden");return File.ReadAllText(Path.Combine(root,file));});
        var active=new DefenseState();active.UnlockAllTechnologyCheat();var twice=new DefenseState();
        Check(active.DroneStats().ToJson()==stats,"CSV-only runtime exact compiled stats hash input");Check(reads.Values.All(n=>n==1)&&!reads.Keys.Intersect(logical).Any(),"one-time load and no runtime JSON reads");
        long revision=CatalogData.Revision;var normal=PerkCatalog.Effects("f_kinetic_quality",1,PerkCatalog.DefaultSettings).N("damage_multiplier");
        CatalogData.Configure(file=>{var text=File.ReadAllText(Path.Combine(root,file));return file=="perk_effect_rules.csv"?text.Replace("f_kinetic_quality,damage_multiplier,linear,1,0.16,","f_kinetic_quality,damage_multiplier,linear,1,0.32,"):text;});
        Check(CatalogData.Revision>revision&&PerkCatalog.Effects("f_kinetic_quality",1,PerkCatalog.DefaultSettings).N("damage_multiplier")==1.32&&normal==1.16,"editing actual perk CSV invalidates cache and changes formula");
        foreach(string mutation in new[]{"missing_table","unknown_attribute","invalid_reference","cycle","missing_balance","invalid_perk","negative_airframe","invalid_reward_reference","invalid_legacy_reference","invalid_successor_reference"})
        {
            CatalogData.Configure(file=>
            {
                string text=File.ReadAllText(Path.Combine(root,file));
                if(mutation=="missing_table"&&file=="deep_technology_nodes.csv")throw new FileNotFoundException("intentionally missing CSV");
                if(mutation=="unknown_attribute"&&file=="deep_technology_nodes_values.csv")return text.Replace("kinetic_damage_bonus","unimplemented_fake_bonus");
                if(mutation=="invalid_reference"&&file=="deep_technology_nodes_requires.csv")return text.Replace(",string,K_S01",",string,UNKNOWN_NODE");
                if(mutation=="cycle"&&file=="deep_technology_nodes_requires.csv")return text.Replace("K_S21,0,string,K_S01","K_S21,0,string,K_S21");
                if(mutation=="missing_balance"&&file=="domain_balance.csv")return text.Replace("kinetic_fire_rate_base,","missing_original_parameter,");
                if(mutation=="invalid_perk"&&file=="perk_effect_rules.csv")return text.Replace("f_kinetic_quality,damage_multiplier,linear","f_kinetic_quality,damage_multiplier,execute_script");
                if(mutation=="invalid_reward_reference"&&file=="kill_rewards.csv")return text.Replace("resource_core_drop_count","missing_setting");
                if(mutation=="invalid_legacy_reference"&&file=="legacy_effect_coefficients.csv")return text.Replace(",mining,",",missing_technology,");
                if(mutation=="invalid_successor_reference"&&file=="successor_research.csv")return text.Replace("kinetic_damage_bonus","missing_attribute");
                if(mutation=="negative_airframe"&&file=="airframes_definitions.csv")
                {
                    var lines=text.Split('\n');var heads=lines[0].TrimEnd('\r').Split(',');int col=Array.IndexOf(heads,"damage_multiplier");var cells=lines[1].TrimEnd('\r').Split(',');cells[col]="-1";lines[1]=string.Join(',',cells);return string.Join('\n',lines);
                }
                return text;
            });
            bool failed=false;try{_ =new DefenseState();}catch(Exception ex)when(ex is InvalidDataException or FileNotFoundException){failed=true;}
            Check(failed,"startup refuses malformed table with no JSON fallback "+mutation);
        }
        CatalogData.Configure(root);Check(new DefenseState().DroneStats().N("damage")>0,"valid configuration reload recovers cleanly after rejected tables");
        // Fixture mode is explicit; it cannot be triggered by missing CSV at runtime.
        CatalogData.ConfigureLegacyFixture(Path.GetFullPath("tests/DomainManaged/Fixtures/pre-csv"));
        Check(DataMap.Equivalent(CatalogData.Load("deep-technology.json"),DataMap.Parse(File.ReadAllText("tests/DomainManaged/Fixtures/pre-csv/deep-technology.json"))),"explicit frozen fixture JSON is still readable");
        CatalogData.Configure(root);
    }
}
