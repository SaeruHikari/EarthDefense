using System.Globalization;
using Earthward.Domain;
using Earthward.Combat;
using Godot;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
string root=Path.GetFullPath("data/domain");
CatalogData.Configure(root);
var state=new DefenseState();
CombatCatalog.Validate();
if(args.Contains("--validate-only"))
{
    Console.WriteLine($"BALANCE_VALID: {DeepTechnology.Nodes.Count} technology nodes, {AirframeCatalog.Definitions.Count} aircraft, {PerkCatalog.Entries.Count} perks, {CombatCatalog.Current.Fronts.Length} fronts; all live CSV parsed and validated.");
    return;
}
int checks=0,failures=0;
void Check(bool ok,string text){checks++;if(!ok){failures++;Console.Error.WriteLine("BALANCE_FAIL: "+text);}}
foreach(long wave in new long[]{1,2,3,4,5,6,7,8,9,10,11,15,16,19,20,21,23,24,25,26,27,28,29,30,40,60,100,195,500})
{
    foreach(int stage in Enumerable.Range(0,4))
    foreach(string role in DefenseWavePlan.RoleOrder)
    foreach(string kind in new[]{"scout","cruiser","small_boss","boss","carrier","mothership"})
    foreach(int index in new[]{0,12,13,25})
    {
        var entry=new DataMap{["wave"]=wave,["stage"]=stage,["role"]=role,["index"]=index};
        var actual=new DataMap{["kind"]=kind};
        EnemyCatalog.Apply(actual,entry,state.CombatSettings);
        Check(actual.N("max_hp")>0&&actual.N("tactical_speed")>0,$"enemy compiled {wave}/{stage}/{role}/{kind}/{index}");
    }
    foreach(long total in new long[]{1,4,30,128,1000})
    foreach(bool medium in new[]{false,true})
    {
        var current=DefenseWavePlan.Build(wave,total,30,45,medium,2,3);
        Check(current.L("planned_count")>=0,"full wave plan "+wave+"/"+total+"/"+medium);
        for(long i=0;i<current.L("planned_count");i++)
            Check(DefenseWavePlan.Entry(current,i).Count>0,"spawn order "+wave+"/"+i);
    }
}
var director=new InvasionDirector();
director.ConfigureAnchor(new Vector3(.2f,.3f,.8f));
foreach(long wave in Enumerable.Range(1,65).Select(i=>(long)i))
    Check(director.FrontsForWave(wave).Count>0,"front positions "+wave);
var defaults=CombatCatalog.Current;
double before=defaults.Roles["claw"].Health;
string enemiesText=File.ReadAllText(Path.Combine(root,"combat_enemies.csv"));
string edited=enemiesText.Replace("light,1,1.15,1.1,1,0.7,0","light,1,3.45,1.1,1,0.7,0");
Check(edited!=enemiesText,"fixture found editable health cell");
CatalogData.Configure(file=>file=="combat_enemies.csv"?edited:File.ReadAllText(Path.Combine(root,file)));
Check(Math.Abs(CombatCatalog.Current.Roles["claw"].Health-before*3)<1e-10,"editing CSV changes compiled enemy health and invalidates cache");
int reads=0;CatalogData.Configure(file=>{reads++;return File.ReadAllText(Path.Combine(root,file));});
CombatCatalog.Validate();int once=reads;
for(int i=0;i<10000;i++)_ = CombatCatalog.Current.Roles["claw"].Health;
Check(reads==once,"combat hot path never rereads CSV");
foreach((string file,string broken) in new[]{("combat_enemies.csv",enemiesText.Replace("claw,scout,claw", "claw,scout,unknown")),("combat_wave_composition.csv",File.ReadAllText(Path.Combine(root,"combat_wave_composition.csv")).Replace("4,6,", "5,6,")),("combat_fronts.csv",File.ReadAllText(Path.Combine(root,"combat_fronts.csv")).Replace("front_01", "front_missing")),("combat_tuning.csv",File.ReadAllText(Path.Combine(root,"combat_tuning.csv")).Replace("EliteInterval,13,", "EliteInterval,0,"))})
{
    CatalogData.Configure(name=>name==file?broken:File.ReadAllText(Path.Combine(root,name)));
    try{CombatCatalog.Validate();Check(false,"reject invalid "+file);}catch(InvalidDataException error){Check(error.Message.Contains(file),"actionable diagnostic "+file);}
}
CatalogData.Configure(root);
Console.WriteLine($"BALANCE_RESULT {checks} checks / {failures} failures");
System.Environment.ExitCode=failures==0?0:1;
