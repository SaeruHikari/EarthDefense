using Earthward.Domain;
using System.Globalization;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
CatalogData.Configure(Path.GetFullPath("data/domain"));
var golden=DataMap.Parse(File.ReadAllText("data/domain/golden-reference.json"));
int checks=0,failures=0;
void Check(bool ok,string label){checks++;if(!ok){failures++;Console.Error.WriteLine("DOMAIN_FAIL "+label);}}
void Compare(object? actual,object? expected,string path)
{
 if(expected is DataMap map){Check(actual is DataMap,path+" map");if(actual is DataMap found)foreach(var(k,v)in map)Compare(found.Value(k),v,path+"."+k);return;}
 if(expected is List<object?> list){Check(actual is List<object?>||actual is System.Collections.IEnumerable,path+" array");if(actual is System.Collections.IEnumerable found){var items=found.Cast<object?>().ToList();Check(items.Count==list.Count,path+" count");for(int i=0;i<Math.Min(items.Count,list.Count);i++)Compare(items[i],list[i],path+"["+i+"]");}return;}
 if(DataMap.ValidNumber(expected,-double.MaxValue,double.MaxValue)){double a=DataMap.Number(actual,double.NaN),e=DataMap.Number(expected);Check(double.IsFinite(a)&&Math.Abs(a-e)<=Math.Max(1e-8,Math.Abs(e)*1e-7),path+" "+a+" / "+e);return;}
 Check(Equals(actual,expected),path+" actual="+actual+" expected="+expected);
}
Check(DeepTechnology.Nodes.Count==248,"248 retained technology nodes");Check(PerkCatalog.Entries.Count==39,"39 perks");
Check(DeepTechnology.Nodes.Count(node=>node.S("size")=="small")==195,"195 independent small technologies remain");
Check(DeepTechnology.Nodes.Count(node=>node.S("size")=="medium")==41,"all 41 medium technologies remain");
Check(DeepTechnology.Nodes.Count(node=>node.S("size")=="large")==12,"all 12 large technologies remain");
Check(DeepTechnology.Nodes.Count(node=>node.B("is_pair_second"))==98,"98 marked adjacent research pairs including shield repair");
var removedResearch = new[]
{
 "K_S03","K_S23","K_S08","K_S28","K_S13","K_S33","K_S18","K_S38",
 "L_S04","L_S24","L_S09","L_S29","L_S14","L_S34","L_S19","L_S39",
 "L_S05","L_S25","L_S10","L_S30","L_S15","L_S35","L_S20","L_S40",
 "I_S04","I_S24","I_S09","I_S29","I_S14","I_S34","I_S19","I_S39",
 "C_S04","C_S24","C_S09","C_S29","C_S14","C_S34","C_S19","C_S39",
 "C_S05","C_S25","C_S10","C_S30","C_S15","C_S35","C_S20","C_S40",
 "M_S08","M_S31","M_S10","M_S33"
}.ToHashSet();
var freshResearch = new DefenseState();
var freshResearchIds = freshResearch.GraphNodes().Select(node=>node.S("id")).ToHashSet();
Check(removedResearch.Count==52,"exact redundant low-value research removal set");
foreach(string id in removedResearch)
 Check(!DeepTechnology.Has(id)&&!freshResearch.CanResearch(id)&&!freshResearchIds.Contains(id),"removed research has no definition, purchase, or graph node: "+id);
Check(DeepTechnology.Nodes.All(node=>node.List("requires").Cast<string>().All(parent=>!removedResearch.Contains(parent))),"no dangling prerequisite references deleted research");
foreach(var (id, parents) in new[]
{
 ("I_N4",new[]{"I_S25"}), ("I_N5",new[]{"I_N4","I_S30"}), ("I_N6",new[]{"I_N5","I_S35"}),
 ("M_S13",new[]{"M_S44"}), ("M_S15",new[]{"M_S46"})
}) Check(DeepTechnology.Definition(id).List("requires").Cast<string>().SequenceEqual(parents),"rewired research keeps exact surviving prerequisites: "+id);
foreach(string branch in new[]{"K","M","L","I","D","C"})
{
 for(int rank=1;rank<=3;rank++)
 {
  Check(DeepTechnology.Definition(branch+"_N"+rank).S("size")=="medium","regular medium retained: "+branch+"_N"+rank);
  var alien=DeepTechnology.Definition(branch+"_A"+rank);
  Check(alien.S("size")=="medium"&&alien.B("alien"),"alien medium retained: "+branch+"_A"+rank);
 }
 for(int rank=1;rank<=2;rank++) Check(DeepTechnology.Definition(branch+"_G"+rank).S("size")=="large","large milestone retained: "+branch+"_G"+rank);
}
foreach(var (id, attribute, value) in new[]
{
 ("K_S01","kinetic_damage_bonus",.04), ("K_S36","kinetic_damage_bonus",.12),
 ("L_S02","laser_fire_rate_bonus",.02), ("L_S37","laser_fire_rate_bonus",.06),
 ("L_S03","laser_range_bonus",.02), ("L_S38","laser_range_bonus",.06),
 ("I_S05","production_speed_bonus",.025), ("I_S40","production_speed_bonus",.075),
 ("C_S02","patrol_outer_bonus",.06), ("C_S37","patrol_outer_bonus",.18),
 ("C_S01","patrol_radius_bonus",.06), ("C_S36","patrol_radius_bonus",.18),
 ("M_S21","missile_blast_radius_bonus",.075), ("M_S44","missile_blast_radius_bonus",.075),
 ("M_S23","missile_speed_bonus",.1), ("M_S46","missile_speed_bonus",.1)
}) Compare(DeepTechnology.Definition(id).Map("values").Value(attribute),value,"stronger surviving research unchanged: "+id);
// Keep the historical reference immutable. The chip economy halves numerical benefits,
// while control durations, required hits, penalties, and integer baseline targets stay intact.
var retainedPerkControls = new HashSet<string>
{
 "launch_damage_seconds", "kill_spawn_reduction_cooldown", "erosion_max_stacks", "erosion_duration",
 "cluster_fragments", "slow_duration", "slow_boss_multiplier", "refraction_targets", "k2_dense_hits_required",
 "k2_dense_duration", "k2_reload_cooldown", "k3_peak_damage_multiplier", "m2_delayed_seconds",
 "m2_shock_required", "m2_shock_cooldown", "m3_escort_radius", "l3_damage_multiplier"
};
foreach(var row in golden.List("perk_effects").OfType<DataMap>())
{
 int level=row.I("level");var expected=row.Map("effects").DeepClone();
 foreach(string field in expected.Keys.ToArray())
 {
  double previous=expected.N(field);
  expected[field]=field switch
  {
   "capacity_add" => 1+(level-1)/5,
   "pierce_extra_targets" => Math.Min(3,1+(level-1)/5),
   "ricochet_targets" => level>=8?2:1,
   "spawn_multiplier" or "m3_escort_lock_multiplier" => 1/(1+(1/previous-1)*.5),
   _ when retainedPerkControls.Contains(field) => previous,
   _ when field.EndsWith("_multiplier") => 1+(previous-1)*.5,
   _ => previous*.5
  };
 }
 Compare(PerkCatalog.Effects(row.S("id"),level,PerkCatalog.DefaultSettings),expected,"rebalanced "+row.S("id")+" level"+level);
}
Compare(PerkCatalog.NeutralModifiers(),golden.Map("perk_neutral"),"neutral");
// Real managed purchase, persistent transactions, inheritance and failure tests.
var funded=new DefenseState{Minerals=1e12,Energy=1e12,Science=1e12,AlienPoints=100000,ResourceCores=100,Wave=120,CompletedWaves=120};funded.SetDefenseReachStage(3);funded.RewardKill("boss",120,3);
var pending=DeepTechnology.Nodes.Select(row=>row.S("id")).ToHashSet();
for(int pass=0;pass<DeepTechnology.Nodes.Count&&pending.Count>0;pass++){int before=pending.Count;foreach(string id in pending.ToList())if(funded.GetGroupStatus(id).B("can_purchase")){Check(funded.PurchaseGroup(id),"actual managed purchase "+id);pending.Remove(id);}if(before==pending.Count)break;}
Check(pending.Count==0,"entire248 actual managed DAG purchase reachable: "+string.Join(",",pending));
Check(funded.PurchaseGroup("K_R00001"),"actual managed independent continuation purchase");
Check(funded.SetAirframe("interceptor",18,-1,"K2"),"select heavy wing");var plan=funded.FactoryAirframePlan("interceptor",18);for(int i=1;i<plan.Count;i++)Check(plan[i].I("berth")==plan[i-1].I("berth")+2,"stable heavy occupancy");
Check(!funded.SetAirframe("interceptor",18,1,"K1"),"cannot select second occupied capacity point");
var fullSnapshot=funded.Serialize();var reopen=new DefenseState();Check(reopen.Restore(DataMap.Parse(fullSnapshot.ToJson())),"full purchasedmanaged save restore");Compare(reopen.DroneStats(),funded.DroneStats(),"purchasedfull restore exact");
var small=new DefenseState{Science=80};double opening=small.Science;Check(small.PurchaseGroup("K_S01")&&small.Science==opening-4&&small.Minerals==320&&small.Energy==180,"small node onlyscience and one node");Check(small.DeepResearch.Count==1&&!small.HasResearch("K_S02"),"no bundled sibling purchase");Check(small.PurchaseGroup("K_S21")&&small.PurchaseGroup("M_N1"),"missile8 independent of alienpoint randomloot");small.Science=10000;Check(small.PurchaseGroup("I_S03")&&small.PurchaseGroup("I_S23"),"laser prerequisite science node");Check(!small.PurchaseGroup("L_N1"),"laser wave10 gate");small.Wave=10;small.RewardWave(10);Check(small.PurchaseGroup("L_N1"),"laser afterwave10");Check(small.Reset()&&!small.HasResearch("M_N1")&&small.FactoryPerks.KnowsIntel("L1"),"newrun remembersintel but resets paidnodes");small.Science=10000;small.PurchaseGroup("K_S01");small.PurchaseGroup("K_S21");small.PurchaseGroup("M_N1");small.PurchaseGroup("I_S03");small.PurchaseGroup("I_S23");Check(!small.PurchaseGroup("L_N1"),"permanentintel never skips newrun wave10 gate");
var assault=new DefenseState{Science=100000,AlienPoints=100,Wave=24,CompletedWaves=23};assault.RewardKill("boss",3);foreach(string id in new[]{"C_S01","C_S21","C_S02","C_S22","C_S06","C_S26","C_N1","C_A1"})Check(assault.PurchaseGroup(id),"assault prerequisite "+id);Check(!assault.PurchaseGroup("C_G1")&&assault.GetGroupStatus("C_G1").S("lock_reason").Contains("24"),"assault explicit24gate");assault.RewardWave(24);Check(assault.PurchaseGroup("C_G1")&&assault.DroneStats().B("mothership_assault_unlocked"),"wave25 actual assault authorization");
funded.SetCombatSetting("resource_core_upgrade_percent",20);Check(funded.EffectiveResourceCorePercent()==22,"core configurablebase20 plus2points");double mineBase=funded.ResourceFacilityBaseOutputs().N("mine");Check(funded.Build("mine",33),"buildboostedresource");Compare(funded.ResourceFacilityOutput("mine",33),mineBase*2,"boostdoubleactualsiteoutput");Check(funded.UpgradeResourceFacility(33,"mine"),"coreupgradeboostedsite");Compare(funded.ResourceFacilityOutput("mine",33),mineBase*2*1.22,"coreplusboostcomposeonce");funded.Tick(10);var timerCopy=new DefenseState();Check(timerCopy.Restore(DataMap.Parse(funded.Serialize().ToJson())),"boost10seconds snapshot");timerCopy.Tick(10);Compare(timerCopy.ResourceFacilityOutput("mine",33),mineBase*1.22,"boostremaining10 expires");
var carrier=new DefenseState{Wave=40};var unit=new DataMap{["kind"]="carrier",["uid"]=100L,["wave"]=30L,["defense_stage"]=1L};var wallet=carrier.Serialize();Check(carrier.RewardEnemy(unit),"carrierfirstreward");Check(carrier.Minerals==wallet.N("minerals")+22&&carrier.Energy==wallet.N("energy")+9&&carrier.Science==wallet.N("science")&&carrier.AlienPoints==0,"carrier22/9 resources with no passive science or alien reward");Check(!carrier.RewardEnemy(unit),"carrierduplicateidempotent");var carrier2=new DefenseState();Check(carrier2.Restore(DataMap.Parse(carrier.Serialize().ToJson()))&&!carrier2.RewardEnemy(unit),"carrierledgerpersists");carrier.RewardEnemy(new(){["kind"]="boss",["uid"]=101L,["wave"]=3L,["spawn_wave"]=3L});Check(carrier.AlienPoints==3,"overlappingbossusesgeneratedwave");
string folder=Path.GetFullPath(Path.Combine(".runtime-tests","domain-permanent-"+Guid.NewGuid().ToString("N")));Directory.CreateDirectory(folder);string profile=Path.Combine(folder,"perks.json");
var perks=new FactoryPerks();Check(perks.ProfilePath.Length==0&&!File.Exists(profile),"constructor noIO");Check(perks.LoadProfile(profile),"bind explicit isolated profile");
var drop=perks.ClaimAlienChip("original","enemy:1");Check(drop.B("claimed")&&drop.L("alien_chips")==1&&perks.AlienChips==1&&!perks.IsUnlocked("a_kinetic_core"),"aircraft chip drop adds one currency without randomly unlocking perks");
Check(perks.CreditAlienChips(4)&&perks.Purchase("a_kinetic_core")&&perks.GetLevel("a_kinetic_core")==1&&perks.AlienChips==2,"three chips purchase chosen level-one aircraft perk");
Check(perks.Upgrade("a_kinetic_core")&&perks.GetLevel("a_kinetic_core")==2&&perks.AlienChips==0,"two additional chips fund first permanent upgrade");
Check(perks.Equip("interceptor",-1,0,"a_kinetic_core","aircraft"),"equip real aircraft template");
var fromDisk=new FactoryPerks();Check(fromDisk.LoadProfile(profile)&&fromDisk.GetLevel("a_kinetic_core")==2,"permanent load retains chip investment");
Check(!fromDisk.ClaimAlienChip("original","enemy:1").B("claimed"),"reload same historical enemy cannot duplicate chip");
Check(fromDisk.ResetRunSites("fresh")&&fromDisk.GetLevel("a_kinetic_core")==2&&fromDisk.SlotsFor("interceptor",-1,"aircraft")[0]=="a_kinetic_core","new run keeps levels and templates");
for(int i=0;i<30;i++)fromDisk.ClaimAlienChip("fresh","early"+i);
Check(!fromDisk.IsUnlocked("a_kinetic_pierce")&&!fromDisk.IsUnlocked("a_laser_crystal"),"chip drops never auto-unlock unpurchased perks");
var stale=new FactoryPerks();Check(stale.LoadProfile(profile),"second writer opens same version");Check(fromDisk.ClaimAlienChip("fresh","newwrite").B("claimed"),"first writer commits chip");
var staleBefore=stale.Snapshot();Check(!stale.ClaimAlienChip("fresh","stalewrite").B("claimed")&&DataMap.Equivalent(stale.Snapshot(),staleBefore),"stale disk writer atomically rejects chip");
var blocked=new FactoryPerks();Check(blocked.LoadProfile(profile),"readfailurefixturebind");File.WriteAllText(profile,"corrupt");File.WriteAllText(profile+".bak","also corrupt");var blockedBefore=blocked.Snapshot();Check(!blocked.LoadProfile(profile)&&!blocked.Upgrade("a_kinetic_core")&&!blocked.ResetRunSites("badreset")&&DataMap.Equivalent(blocked.Snapshot(),blockedBefore),"bothcorruptprofilesreadonlyretainmemory");Check(File.ReadAllText(profile)=="corrupt"&&File.ReadAllText(profile+".bak")=="also corrupt","corruptsourcepreservednotreplacedbyblank");
var allPerks=new FactoryPerks();var allMeta=allPerks.Snapshot();allMeta["advanced_unlocked"]=true;allMeta["alien_chips"]=100000L;foreach(string id in allMeta.Map("levels").Keys.ToList())allMeta.Map("levels")[id]=1L;Check(allPerks.ImportSnapshot(allMeta),"all39fixture");Check(allPerks.ResetRunSites("berth"),"bindstableslots");foreach(var frame in AirframeCatalog.Definitions){string kind=frame.S("kind"),id=frame.S("id");Check(allPerks.Definitions("aircraft",kind,id).Count==4,"4applicableperks "+id);foreach(var row in allPerks.Definitions("aircraft",kind,id)){Check(allPerks.Equip(kind,-1,0,row.S("id"),"aircraft",-1,id),"equiptype "+row.S("id"));Check(!DataMap.Equivalent(allPerks.Modifiers(kind,-1,"aircraft",-1,id),PerkCatalog.NeutralModifiers()),"actualexclusiveeffect "+row.S("id"));}allPerks.Equip(kind,-1,0,"","aircraft",-1,id);}
Check(allPerks.Equip("interceptor",-1,0,"a_kinetic_core","aircraft")&&allPerks.Equip("interceptor",18,0,"a_kinetic_pierce","aircraft")&&allPerks.Equip("interceptor",18,0,"a_kinetic_ricochet","aircraft",2),"threelevelinheritance");Check(allPerks.SlotsFor("interceptor",19,"aircraft",2)[0]=="a_kinetic_core"&&allPerks.SlotsFor("interceptor",18,"aircraft",1)[0]=="a_kinetic_pierce"&&allPerks.SlotsFor("interceptor",18,"aircraft",2)[0]=="a_kinetic_ricochet","berthstableandfactoryisolated");
foreach(string mutation in new[]{"unknown_node","fractional_schema","invalid_frame","bad_refund","invalid_flag","unknown_setting","wrong_kind"}){var before=reopen.Serialize();var bad=before.DeepClone();switch(mutation){case "unknown_node":bad.Map("research_state").Map("nodes")["fake"]=1L;break;case "fractional_schema":bad.Map("research_state")["version"]=1.5;break;case "invalid_frame":bad.Map("airframe_selection").Map("templates")["interceptor"]="M3";break;case "bad_refund":bad.Map("research_runtime").List("science_refunds").Add(new DataMap{["amount"]=-1d,["due"]=10d});break;case "invalid_flag":bad.Map("research_flags")["laser_intel"]=1L;break;case "unknown_setting":bad.Map("combat_settings")["fake"]=1L;break;case "wrong_kind":bad.Map("resource_core_upgrades")["5"]=new DataMap{["kind"]="interceptor",["level"]=1L};break;}Check(!reopen.Restore(bad)&&DataMap.Equivalent(reopen.Serialize(),before),"invalidstateatomic "+mutation);}
var vectorMap=new DataMap{["v"]=new Godot.Vector3(1,2,3)};var vectorRound=DataMap.Parse(vectorMap.ToJson());Check(vectorRound.List("v").Count==3&&vectorRound.Vector3("v")==new Godot.Vector3(1,2,3),"GodotvaluearrayJSONboundary");

// Dormant expedition/fleet records must survive the language migration intact.
var retired=new ExpeditionData().Serialize();retired["earth_liberated"]=true;retired.Map("research")["telescope"]=true;retired.Map("telescope_satellite")["deployed"]=true;var moon=retired.Map("sectors").Map("moon");moon["observation_paid"]=true;moon["revealed"]=true;moon["status"]="revealed";moon.List("rewarded_targets").Add("barracks");
var fleet=retired.Map("fleet");fleet["next_id"]=3000003L;fleet.List("orders").Add(new DataMap{["id"]=3000001L,["kind"]="destroyer",["silo_id"]=55L,["duration"]=30d,["elapsed"]=4d,["launch_seconds"]=6d,["transit_seconds"]=18d,["cost"]=new DataMap{["minerals"]=12000d,["energy"]=8000d,["science"]=2000d}});
var ship=new DataMap{["uid"]=3000002L,["kind"]="carrier",["state"]="parked",["silo_id"]=56L,["slot"]=0L,["age"]=25d,["launch_seconds"]=6d,["transit_seconds"]=18d,["clearance"]=24d,["hull_length"]=5d,["launch_origin"]=new List<object?>{0d,0d,24d},["launch_end"]=new List<object?>{0d,0d,26d},["launch_normal"]=new List<object?>{0d,0d,1d},["launch_up"]=new List<object?>{0d,1d,0d},["target"]=new List<object?>{-15.3d,4.275d,11.4d},["position"]=new List<object?>{-15.3d,4.275d,11.4d},["forward"]=new List<object?>{0d,0d,1d},["up"]=new List<object?>{0d,1d,0d}};
fleet.List("ships").Add(ship);
var expedition=new ExpeditionData();Check(expedition.Restore(retired),"dormantfleetwithshipandpaidorderrestores");Check(DataMap.Equivalent(expedition.Serialize(),retired),"retiredfleetresearchsectorssettingspreservedexactly");var invalidRetired=retired.DeepClone();invalidRetired.Map("fleet").List("ships").OfType<DataMap>().Single()["age"]=-1;Check(!expedition.Restore(invalidRetired),"invalidretiredshiprejected");
// A failed permanent write must abort an entire new run / checkpoint switch.
string stateFolder=Path.Combine(folder,"state");Directory.CreateDirectory(stateFolder);var atomic=new DefenseState();Check(atomic.FactoryPerks.LoadProfile(Path.Combine(stateFolder,"perks.json")),"atomicstatescopebind");Check(atomic.FactoryPerks.ResetRunSites(atomic.RunId),"atomiccurrentrunbind");var stateBefore=atomic.Serialize();var metaBefore=atomic.FactoryPerks.Snapshot();Directory.CreateDirectory(atomic.FactoryPerks.ProfilePath+".tmp");var different=stateBefore.DeepClone();different["run_id"]="different-run";different["minerals"]=12345d;Check(!atomic.Restore(different)&&DataMap.Equivalent(atomic.Serialize(),stateBefore)&&DataMap.Equivalent(atomic.FactoryPerks.Snapshot(),metaBefore),"failedmetawriteabortsrunrestoreatomically");Check(!atomic.Reset()&&DataMap.Equivalent(atomic.Serialize(),stateBefore),"failedmetawriteabortsnewrunatomically");Directory.Delete(atomic.FactoryPerks.ProfilePath+".tmp");Check(atomic.Reset(),"atomicnewrunretryaftersafestoragefix");
var maxResource=new DefenseState{ResourceCores=10};Check(maxResource.UpgradeResourceFacility(1,"mine"),"firstrealresourcefacilitycoreupgrade");Check(!maxResource.UpgradeResourceFacility(2,"mine"),"cannotinventsecondfacilitycoreincomefromonebuilding");

WorldScaleChecks.Run(Check, golden, retired);
PatrolCoverageChecks.Run(Check, golden);
ResearchContinuationChecks.Run(Check, golden);
LocalShieldChecks.Run(Check, golden);
CsvCatalogChecks.Run(Check);
AchievementChecks.Run(Check);
AlienChipChecks.Run(Check);
OpeningResearchChecks.Run(Check);
Console.WriteLine($"DOMAIN_RESULT {checks-failures} PASS / {failures} FAIL");
return failures==0?0:1;
