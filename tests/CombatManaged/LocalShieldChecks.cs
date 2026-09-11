using Earthward.Combat;
using Earthward.Domain;
using Godot;
using System.Reflection;

internal static class LocalShieldChecks
{
    private static Action<bool,string> Check=null!;
    private static Action<double,double,string,double> Near=null!;
    private static object? Call(Battlefield b,string name,params object?[] values)=>typeof(Battlefield).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(b,values);
    private sealed class Surface : ICombatSurface
    {
        public List<DataMap> Shields {get;}=new();
        public Basis Rotation=Basis.Identity;
        public bool CacheWorld;
        public long Revision;
        public int TransformReads;
        public long SpatialRevision=>CacheWorld?Revision:-1;
        public IReadOnlyList<DataMap> GetFactorySites()=>Array.Empty<DataMap>();
        public IReadOnlyList<DataMap> GetShieldSites()=>Shields;
        public Vector3 SurfaceToSpace(Vector3 p,double altitude){TransformReads++;return Rotation*p.Normalized()*(float)(CombatScale.EarthRadius+altitude);}
        public Vector3 SpaceToSurface(Vector3 p)=>Rotation.Inverse()*p.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>Shields.Select(s=>s.Vector3("normal")).ToArray();
    }
    private static (DefenseState Game,Surface Surface,Battlefield Battle) Scene(int towers=1)
    {
        var g=new DefenseState{Wave=1,Shield=100};var s=new Surface();
        for(int i=0;i<towers;i++)s.Shields.Add(new(){["site_id"]=18L+i,["kind"]="shield",["normal"]=Vector3.Back});
        g.Buildings["shield"]=towers;g.Science=10000;
        bool BuyPath(string id){if(g.HasResearch(id))return true;foreach(string parent in DeepTechnology.Definition(id).List("requires").Cast<string>())if(!BuyPath(parent))return false;return g.Research(id);}
        Check(BuyPath("D_N4"),"shield unlock bought through actual CSV prerequisites and ordinary research API");g.InvalidateFactoryStats();var b=new Battlefield(g,s);b.GetLocalShieldState();return(g,s,b);
    }
    private static DataMap Tower(Battlefield b,long site=18)=>b.GetLocalShieldState().Single(s=>s.L("site_id")==site);
    private static void Hit(Battlefield b,Vector3 normal,double damage)=>b.DetonateHostile(normal.Normalized()*(float)CombatScale.EarthCollisionRadius,new(){["target_kind"]="earth",["damage"]=damage,["blast_radius"]=0d},null,true);
    private static DataMap Shot(Battlefield b,Vector3 position,Vector3 velocity,double damage)
    {
        var shot=new DataMap{["uid"]=(long)Call(b,"NewUid")!,["kind"]="hostile",["space_position"]=position,["velocity"]=velocity,["tangent"]=velocity.Normalized(),["damage"]=damage,["blast_radius"]=0d,["target_kind"]="earth",["life"]=10d};b.HostileShots.Add(shot);return shot;
    }
    public static void RunStandalone()
    {
        int count=0,failed=0;void Assert(bool ok,string label){count++;if(!ok){failed++;Console.WriteLine("LOCAL_SHIELD_FAIL "+label);}}
        void Almost(double value,double expected,string label,double eps)=>Assert(double.IsFinite(value)&&Math.Abs(value-expected)<=eps,$"{label} ({value}/{expected})");
        Run(Assert,Almost);Console.WriteLine($"LOCAL_SHIELDS {count} checks; {failed} failures");System.Environment.ExitCode=failed==0?0:1;
    }
    public static void Run(Action<bool,string> check,Action<double,double,string,double> near)
    {
        Check=check;Near=near;CoverageAndOverlap();ProjectilesAndMeteors();RepairRegenerationAndResearch();RotationAndPersistence();LegacyAndStationaryAttack();RepeatedQueryCache();
    }
    private static void CoverageAndOverlap()
    {
        var(no,ns,nb)=Scene(0);Hit(nb,Vector3.Back,20);Near(no.EarthHp,80,"without tower an old global shield balance does not absorb",1e-9);Near(no.Shield,100,"old shield balance remains archival only",1e-9);Check(nb.GetLocalShieldSummary().N("capacity")==0&&nb.GetLocalShieldSummary().I("count")==0,"empty local shield HUD totals are zero");
        var(g,s,b)=Scene();var t=Tower(b);double maximum=t.N("max_hp");Hit(b,Vector3.Back,30);Near(t.N("hp"),maximum-30,"covered impact consumes one local tower",1e-9);Near(g.EarthHp,100,"covered impact protects Earth",1e-9);Hit(b,Vector3.Forward,10);Near(g.EarthHp,90,"far hemisphere is not protected",1e-9);Near(t.N("hp"),maximum-30,"far impact leaves local tower untouched",1e-9);
        double angle=t.N("surface_radius")/CombatScale.EarthRadius;Hit(b,Vector3.Back.Rotated(Vector3.Up,(float)(angle-.002)),1);Hit(b,Vector3.Back.Rotated(Vector3.Up,(float)(angle+.002)),1);Near(t.N("hp"),maximum-31,"inside angular boundary is protected",1e-9);Near(g.EarthHp,89,"outside angular boundary directly hits Earth",1e-9);
        s.Shields.Add(new(){["site_id"]=19L,["kind"]="shield",["normal"]=Vector3.Back});g.Buildings["shield"]=2;g.InvalidateFactoryStats();var second=Tower(b,19);t["hp"]=5d;double secondBefore=second.N("hp");Hit(b,Vector3.Back,20);Near(t.N("hp"),0,"closest/tie-first tower depletes once",1e-9);Near(second.N("hp"),secondBefore,"overlap does not debit a second tower for the same impact",1e-9);Near(g.EarthHp,74,"one tower overflow goes to Earth exactly once",1e-9);Hit(b,Vector3.Back,2);Near(second.N("hp"),secondBefore-2,"later independent hit can select another active tower",1e-9);
        Check(b.GetLocalShieldSummary().I("count")==2&&b.GetLocalShieldSummary().I("active")==1,"summary reports real active and total tower counts");Near(b.GetLocalShieldSummary().N("capacity"),maximum*2,"HUD totals are independent of archival global shield",1e-9);Near(g.Shield,100,"multi-tower totals never overwrite old Game.Shield",1e-9);
    }
    private static void ProjectilesAndMeteors()
    {
        var(g,s,b)=Scene();var t=Tower(b);double radius=t.N("shield_radius"),maximum=t.N("max_hp");var shot=Shot(b,Vector3.Back*(float)(radius+.3),Vector3.Forward*5,20);b.UpdateShots(.1);Check(!b.HostileShots.Contains(shot),"incoming world projectile is intercepted on actual outer shield shell");Near(t.N("hp"),maximum-20,"projectile consumes shield at its shell contact",1e-9);Near(g.EarthHp,100,"shell interception stops projectile before planet",1e-9);Check(b.Bursts.Any(x=>Math.Abs(x.Vector3("space_position").Length()-radius)<.001),"shield impact effect is on the visible shell");
        t["hp"]=5d;var partial=Shot(b,Vector3.Back*(float)(radius+.3),Vector3.Forward*5,20);b.UpdateShots(.1);Check(b.HostileShots.Contains(partial)&&partial.B("local_shield_checked"),"overflow projectile retains one-hit interception marker");Near(partial.N("damage"),15,"only unabsorbed damage continues through shell",1e-9);
        s.Shields.Add(new(){["site_id"]=19L,["kind"]="shield",["normal"]=Vector3.Back});g.Buildings["shield"]=2;g.InvalidateFactoryStats();var second=Tower(b,19);var snapshot=b.SerializeCombatSnapshot();Check(snapshot.I("version")==4&&Battlefield.ValidateCombatSnapshot(snapshot),"partial shot and local tower state validate in snapshot4");var g2=new DefenseState();Check(g2.Restore(g.Serialize()),"companion tower game state restores");var copy=new Battlefield(g2,s);Check(copy.RestoreCombatSnapshot(DataMap.Parse(snapshot.ToJson())),"partial shield hit restores across JSON");b.UpdateShots(1);copy.UpdateShots(1);Near(g.EarthHp,85,"overflow reaches Earth exactly once",1e-9);Near(g2.EarthHp,85,"restored overflow also bypasses repeat shield absorption",1e-9);Near(second.N("hp"),second.N("max_hp"),"new overlapping shield is not double-charged by the same projectile",1e-9);Near(Tower(copy,19).N("hp"),second.N("hp"),"one-hit marker remains deterministic after reload",1e-9);
        var(mg,ms,mb)=Scene();var mt=Tower(mb);mb.Active=true;double hp=mt.N("hp");Check(mb.GetLocalShieldSummary().I("count")==1,"covered scene exposes one local tower");Near(mt.N("hp"),hp,"idle covered tower is stable",1e-9);
    }
    private static void RepairRegenerationAndResearch()
    {
        var(g,s,b)=Scene(2);var a=Tower(b);var other=Tower(b,19);a["hp"]=10d;other["hp"]=20d;Near(g.RechargeLocalShields!(30),30,"recharge callback returns actual amount restored",1e-9);Near(b.GetLocalShieldSummary().N("hp"),60,"fixed recharge budget is not multiplied by tower count",1e-9);double before=b.GetLocalShieldSummary().N("hp");Check(g.CanRepair()&&g.Repair(),"normal repair UX recognizes damaged local shields even at full Earth HP");Near(b.GetLocalShieldSummary().N("hp")-before,40,"repair shares its original forty shield points",1e-9);
        before=b.GetLocalShieldSummary().N("hp");g.RewardWave(1);Near(b.GetLocalShieldSummary().N("hp")-before,20,"wave reward shares its original twenty shield points",1e-9);g.DeepResearch["D_A3"]=1L;g.InvalidateFactoryStats();before=b.GetLocalShieldSummary().N("hp");double restored=g.ApplySacrificeRecovery(1000);Near(restored,g.ShieldMax()*.02,"sacrifice keeps original per-second recovery budget",1e-9);Near(b.GetLocalShieldSummary().N("hp")-before,restored,"sacrifice returns only real tower recovery",1e-9);Near(g.ApplySacrificeRecovery(1000),0,"same-second sacrifice cannot duplicate budget across towers",1e-9);
        a=Tower(b);a["hp"]=0d;b.Active=true;double regen=a.N("regeneration");b.Step(.05);Near(a.N("hp"),regen*.05,"tower independently regenerates using old shield upgrade formula",1e-9);Near(g.Shield,100,"tower regeneration does not regenerate global archival pool",1e-9);
        a["hp"]=a.N("max_hp")*.4;double fraction=.4;g.DeepResearch["D_S07"]=1L;g.InvalidateFactoryStats();a=Tower(b);Near(a.N("max_hp"),g.ShieldMax(),"research shield investment increases each tower capacity",1e-9);Near(a.N("hp")/a.N("max_hp"),fraction,"capacity changes preserve the tower damage fraction",1e-9);Near(a.N("regeneration"),g.ShieldRegeneration(),"current regeneration investment remains active",1e-9);
        var(rg,rs,rb)=Scene();rg.DeepResearch["D_G2"]=1L;rg.InvalidateFactoryStats();var rt=Tower(rb);rt["hp"]=10d;Hit(rb,Vector3.Back,20);Near(rt.N("hp"),rt.N("max_hp")*.25,"existing emergency shield tech rebuilds this tower",1e-9);Near(rg.EarthHp,90,"emergency rebuild cannot absorb overflow a second time",1e-9);double held=rt.N("hp");Hit(rb,Vector3.Back,5);Near(rt.N("hp"),held,"existing shield hold duration operates on the damaged tower",1e-9);Check(rt.N("break_ready_at")>rb.Clock+29&&rt.N("hold_until")>rb.Clock+1.9,"per-tower emergency cooldown and hold timer are tracked");rb.Active=true;for(int i=0;i<45;i++)rb.Step(.05);Hit(rb,Vector3.Back,rt.N("hp")+1);Near(rt.N("hp"),0,"same tower cannot trigger emergency rebuild again during cooldown",1e-9);
    }
    private static void RotationAndPersistence()
    {
        var(g,s,b)=Scene();var t=Tower(b);Hit(b,Vector3.Back,17);double hp=t.N("hp");s.Rotation=new Basis(Vector3.Up,1.2f);b.Step(0);var current=s.SurfaceToSpace(Vector3.Back,0).Normalized();Hit(b,current,4);Near(t.N("hp"),hp-4,"local shield coverage rotates with its surface facility",1e-9);double earth=g.EarthHp;Hit(b,Vector3.Back,2);Near(g.EarthHp,earth-2,"old world heading loses protection after Earth rotation",1e-9);
        var saved=b.SerializeCombatSnapshot();var raw=saved.ToJson();Check(Battlefield.ValidateCombatSnapshot(saved),"damaged rotated tower snapshot validates");var copy=new Battlefield(g,s);Check(copy.RestoreCombatSnapshot(DataMap.Parse(raw)),"damaged rotated tower restores");Near(Tower(copy).N("hp"),t.N("hp"),"tower damage survives JSON save/load",1e-9);Near(Tower(copy).Vector3("world_normal").DistanceTo(current),0,"restored shield uses current rotated facility normal",.000001);
        var normalized=Battlefield.NormalizeCombatSnapshot(saved);Check(normalized!=null&&DataMap.Equivalent(normalized,Battlefield.NormalizeCombatSnapshot(normalized))&&raw==saved.ToJson(),"snapshot4 normalization is idempotent and read-only");DefenseCampaignDirector.RegisterSnapshotMigration();var director=new DefenseCampaignDirector(g,b);var nested=director.Serialize();Check(DefenseCampaignDirector.ValidateSnapshot(nested)&&g.Expedition.SetRuntimeSnapshot(nested),"director and domain runtime accept nested battle snapshot4");
        var oldRadius=saved.DeepClone();CombatSnapshotCodec.TryDecode(oldRadius.Value("payload"),out var decoded);var fields=(DataMap)decoded!;fields.List("_bursts").Clear();fields.List("_damage_numbers").Clear();foreach(var row in fields.Map("_local_shields").Values.OfType<DataMap>()){row["shield_radius"]=row.N("shield_radius")-8;row["angle_radians"]=row.N("surface_radius")/8;}oldRadius["earth_radius"]=8d;oldRadius["payload"]=CombatSnapshotCodec.Encode(fields);var migrated=Battlefield.NormalizeCombatSnapshot(oldRadius);Check(migrated!=null,"local shield snapshot supports explicit prior Earth radius");if(migrated!=null){CombatSnapshotCodec.TryDecode(migrated.Value("payload"),out var loaded);var tower=((DataMap)loaded!).Map("_local_shields").Values.OfType<DataMap>().Single();Near(tower.N("shield_radius"),CombatScale.EarthRadius+t.N("altitude"),"shield shell radius migrates once with Earth",1e-9);Near(tower.N("angle_radians"),t.N("surface_radius")/CombatScale.EarthRadius,"shield coverage retains its actual surface arc length",1e-9);Near(tower.N("hp"),t.N("hp"),"scale migration never refills tower health",1e-9);}
        var invalid=saved.DeepClone();CombatSnapshotCodec.TryDecode(invalid.Value("payload"),out var bad);((DataMap)bad!).Map("_local_shields").Values.OfType<DataMap>().First()["hp"]=-1d;invalid["payload"]=CombatSnapshotCodec.Encode(bad);var before=copy.SerializeCombatSnapshot().ToJson();Check(!copy.RestoreCombatSnapshot(invalid)&&copy.SerializeCombatSnapshot().ToJson()==before,"invalid tower health rejected atomically");
        s.Shields[0]["kind"]="mine";g.Buildings["shield"]=0;g.InvalidateFactoryStats();Check(b.GetLocalShieldSummary().I("count")==0,"replacing shield building removes coverage and tower state");Hit(b,current,1);Near(g.EarthHp,earth-3,"removed tower cannot absorb later impacts",1e-9);s.Shields.Clear();s.Shields.Add(new(){["site_id"]=18L,["kind"]="shield",["normal"]=Vector3.Back});g.Buildings["shield"]=1;g.InvalidateFactoryStats();Near(Tower(b).N("hp"),Tower(b).N("max_hp"),"newly rebuilt physical tower starts with fresh capacity",1e-9);
    }
    private static void RepeatedQueryCache()
    {
        var(g,s,b)=Scene();s.CacheWorld=true;s.Revision=1;s.TransformReads=0;b.GetLocalShieldState();int first=s.TransformReads;b.GetLocalShieldSummary();b.GetLocalShieldState();b.UpdateShots(0);Check(first==1&&s.TransformReads==first,"render/summary/projectile requests share one spatial shield synchronization");
        s.Rotation=new Basis(Vector3.Up,.5f);s.Revision++;b.GetLocalShieldState();Check(s.TransformReads==first+1,"real spatial revision refreshes world normals immediately");
        g.DeepResearch["D_S07"]=1L;g.InvalidateFactoryStats();b.GetLocalShieldSummary();Check(s.TransformReads==first+2,"stat revision invalidates same-tick shield cache");
        double hp=Tower(b).N("hp");Hit(b,s.Rotation*Vector3.Back,3);Near(b.GetLocalShieldSummary().N("hp"),hp-3,"cached geometry does not cache stale shield health",1e-9);
    }
    private static void LegacyAndStationaryAttack()
    {
        var legacy=new DefenseState{Wave=1,Shield=100};var lb=new Battlefield(legacy,new Surface());double hp=legacy.EarthHp;Hit(lb,Vector3.Back,1);Near(legacy.EarthHp,hp-1,"archival global balance never protects without a tower",1e-9);Near(legacy.Shield,100,"archival balance is retained but inert",1e-9);
        var(ng,ns,nb)=Scene();var tower=Tower(nb);var enemy=nb.SpawnEnemy("scout",new(){["wave"]=1L,["role"]="claw"},Vector3.Back*(float)CombatScale.CloseAssault)!;nb.Active=true;double initial=tower.N("hp");for(int i=0;i<90;i++)nb.Step(1d/60);Check(enemy.B("stationary_bombard")&&tower.N("hp")<initial&&ng.EarthHp==100,"parked enemy inside outer dome still hits defended ground and consumes the local shield");
    }
}
