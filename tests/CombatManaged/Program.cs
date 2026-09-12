using Earthward.Domain;using Earthward.Combat;using Godot;using System.Globalization;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;CatalogData.Configure(Path.GetFullPath("data/domain"));if(args.Contains("--natural-opening")){NaturalOpening.Run();return;}if(args.Contains("--world-layout")){WorldStressLayoutChecks.Run();return;}if(args.Contains("--parallel-flight")){ParallelFlightChecks.Run();return;}if(args.Contains("--fleet-capacity")){FleetCapacityChecks.Run();return;}if(args.Contains("--explosion-tech")){ExplosionTechChecks.Run();return;}if(args.Contains("--spatial-index")){SpatialIndexChecks.Run();return;}if(args.Contains("--stress")){FleetLoadBench.Run(args);return;}if(args.Contains("--local-shields")){LocalShieldChecks.RunStandalone();return;}if(args.Contains("--bombardment")){StationaryBombardmentChecks.RunStandalone();return;}if(args.Contains("--coverage")){FactoryCoverageChecks.RunStandalone();return;}if(args.Contains("--bench")){CombatBench.Run();return;}int checks=0,failed=0;
void Check(bool ok,string label){checks++;if(!ok){failed++;Console.WriteLine("FAIL "+label);}}
// Self-consistent runtime checks over the live CSV catalogs (no frozen legacy baselines).
foreach(long wave in new long[]{1,5,10,20,28,60,120})
foreach(int stage in Enumerable.Range(0,4))
foreach(string role in DefenseWavePlan.RoleOrder)
foreach(string kind in new[]{"scout","cruiser","small_boss","boss","carrier"})
{
    var actual=new DataMap{["kind"]=kind};
    EnemyCatalog.Apply(actual,new DataMap{["wave"]=wave,["stage"]=stage,["role"]=role,["index"]=0},new DefenseState().CombatSettings);
    Check(actual.N("max_hp")>0&&actual.N("tactical_speed")>0,$"enemy compiled {wave}/{stage}/{role}/{kind}");
}
foreach(long wave in new long[]{1,5,10,20,28,60,120})
{
    var plan=DefenseWavePlan.Build(wave,36,30,45,wave==3||wave%5==0,wave>=31?1:0,wave>=31?2:0);
    Check(plan.L("planned_count")>=0&&plan.L("planned_count")<=36,"wave plan budget "+wave);
    for(long i=0;i<plan.L("planned_count");i++)Check(DefenseWavePlan.Entry(plan,i).Count>0,"spawn order "+wave+"/"+i);
}
var invasion=new InvasionDirector();invasion.ConfigureAnchor(new Vector3(.2f,.3f,1).Normalized());
foreach(long wave in Enumerable.Range(1,65).Select(i=>(long)i)){Check(invasion.FrontsForWave(wave).Count>0,"fronts "+wave);Check(invasion.WaveBudget(wave,10,2,2)>=0,"budget "+wave);}
var state=new DefenseState();var battle=new Battlefield(state);battle.StartWave();for(int i=0;i<600;i++){state.Tick(1d/60);battle.Step(1d/60);}var snapshot=DataMap.Parse(battle.SerializeCombatSnapshot().ToJson());Check(Battlefield.ValidateCombatSnapshot(snapshot),"live ten second snapshot");var copy=new Battlefield(state);Check(copy.RestoreCombatSnapshot(snapshot),"live snapshot restore");Check(copy.Drones.Count==battle.Drones.Count&&copy.Enemies.Count==battle.Enemies.Count,"actors persist");Check(copy.Random.State==battle.Random.State,"random persists");
CombatContracts.Run(Check);
Console.WriteLine($"COMBAT_MANAGED checks={checks} failures={failed}");System.Environment.ExitCode=failed==0?0:1;
