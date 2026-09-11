using Earthward.Combat;
using Earthward.Domain;
using Godot;
using System.Reflection;

internal static class FactoryCoverageChecks
{
    private static Action<bool,string> Check = null!;
    private static Action<double,double,string,double> Near = null!;
    private static object? Call(Battlefield b,string name,params object?[] values) => typeof(Battlefield).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(b,values);
    private sealed class Surface : ICombatSurface
    {
        public List<DataMap> Sites { get; }=new();
        public Basis Rotation=Basis.Identity;
        public void Add(long site,string kind,Vector3 normal)
        {
            Sites.Add(new(){["site_id"]=site,["kind"]=kind,["normal"]=normal,["launch_position"]=normal*(float)(CombatScale.EarthRadius+.108),["launch_direction"]=Vector3.Right});
        }
        public IReadOnlyList<DataMap> GetFactorySites()=>Sites;
        public Vector3 SurfaceToSpace(Vector3 n,double altitude)=>Rotation*n.Normalized()*(float)(CombatScale.EarthRadius+altitude);
        public Vector3 SpaceToSurface(Vector3 p)=>Rotation.Inverse()*p.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>Sites.Select(s=>s.Vector3("normal")).ToList();
    }
    public static void RunStandalone()
    {
        int checks=0,failures=0;
        void Assert(bool ok,string label){checks++;if(!ok){failures++;Console.WriteLine("COVERAGE_FAIL "+label);}}
        void Almost(double value,double expected,string label,double tolerance)=>Assert(double.IsFinite(value)&&Math.Abs(value-expected)<=tolerance,$"{label} ({value}/{expected})");
        Run(Assert,Almost);Console.WriteLine($"FACTORY_COVERAGE {checks} checks; {failures} failures");System.Environment.ExitCode=failures==0?0:1;
    }
    public static void Run(Action<bool,string> check,Action<double,double,string,double> near)
    {
        Check=check;Near=near;
        foreach(string kind in new[]{"interceptor","missile","laser"})Boundaries(kind);
        PlannedAndPhysicalFrames();PerkOwnershipAndDeduplication();EmptyAndInvalid();
    }
    private static (DefenseState Game,Surface Surface,Battlefield Battle) Create(string kind="interceptor")
    {
        var game=new DefenseState();var surface=new Surface();surface.Add(18,kind,Vector3.Back);surface.Add(19,kind,Vector3.Right);
        return (game,surface,new Battlefield(game,surface));
    }
    private static void SetPosition(Battlefield b,DataMap d,Vector3 position)
    {
        d["space_position"]=position;d["normal"]=b.SpaceToSurface(position);d["state"]="engaging";d["launch_age"]=2d;b.WorldCache[d.L("uid")]=position;
    }
    private static DataMap Target(Vector3 p,bool large=false)=>new(){["uid"]=99999L,["kind"]=large?"boss":"scout",["phase"]="approach",["armor_type"]="light",["hp"]=1e6,["space_position"]=p};
    private static void Boundaries(string kind)
    {
        var (g,s,b)=Create(kind);var preview=b.GetFactoryCoverage(18)!;
        Check(preview.Preview&&preview.AircraftCount==0&&preview.Bands.Count==1,"empty factory previews actual production profile "+kind);
        Check(ReferenceEquals(preview,b.GetFactoryCoverage(18)),"unchanged empty production plan reuses its preview "+kind);
        var planned=preview.Bands[0];var patrol=g.FactoryPatrolStats(kind,18);
        Near(planned.PatrolRadius,patrol.N("patrol_radius"),"preview uses compiled patrol radius "+kind,1e-9);
        Near(planned.OuterRange,patrol.N("patrol_outer_range"),"preview uses compiled outer range "+kind,1e-9);
        var d=b.SpawnFactoryDrone(b.Factories[18]);var active=b.GetFactoryCoverage(18)!;var band=active.Bands.Single();
        Check(!active.Preview&&active.AircraftCount==1,"living launching aircraft switches preview to actual life "+kind);
        Check(band==planned,"newly produced physical aircraft matches production coverage "+kind);
        Near(band.AngleRadians,Math.Min(Math.PI,(band.PatrolRadius+band.OuterRange)/CombatScale.EarthRadius),"angle excludes weapon reach "+kind,1e-12);
        Near(band.PursuitRadius,Math.Max(CombatScale.EarthRadius+CombatScale.DroneAltitude+band.OuterRange,b.DronePatrolStats(d).N("action_radius")),"pursuit uses actual radial clamp "+kind,1e-12);
        Near(band.AttackRadius,band.PursuitRadius+band.WeaponRange,"ordinary attack radial envelope "+kind,1e-12);
        Near(band.MaximumPursuitAltitude,band.PursuitRadius-CombatScale.EarthRadius,"pursuit altitude has Earth radius removed "+kind,1e-12);
        Near(band.MaximumCombatAltitude,band.AttackRadius-CombatScale.EarthRadius,"ordinary target altitude has Earth radius removed "+kind,1e-12);
        Near(band.LargeTargetAttackRadius,band.AttackRadius,"no unresearched capital range bonus "+kind,1e-12);
        SetPosition(b,d,Vector3.Back*(float)band.PursuitRadius);
        var inside=Target(Vector3.Back*(float)(band.AttackRadius-.005));var outside=Target(Vector3.Back*(float)(band.AttackRadius+.005));
        Check(b.AssignmentValid(d,inside)&&!b.AssignmentValid(d,outside),"actual assignment matches radial envelope on both sides "+kind);
        Check(b.CanDroneEngagePosition(d,inside.Vector3("space_position"))&&!b.CanDroneEngagePosition(d,outside.Vector3("space_position")),"actual fire distance matches ordinary radial envelope "+kind);
        foreach(bool within in new[]{true,false})
        {
            var n=Vector3.Back.Rotated(Vector3.Up,(float)(band.AngleRadians+(within?-.002:.002)));SetPosition(b,d,n*(float)band.PursuitRadius);var enemy=Target(n*(float)(band.PursuitRadius+.05));
            Check(b.AssignmentValid(d,enemy)==within,"actual assignment matches angle edge without gun inflation "+kind+within);
            Check(b.CanDroneEngagePosition(d,enemy.Vector3("space_position"))==within,"actual gun uses the same angular boundary "+kind+within);
        }
        SetPosition(b,d,Vector3.Back*(float)band.PursuitRadius);b.MoveDroneSafe(d,Vector3.Back.Rotated(Vector3.Up,(float)(band.AngleRadians+.4))*(float)band.PursuitRadius,1000);
        Near(b.GetDroneWorldPosition(d).Normalized().AngleTo(Vector3.Back),band.AngleRadians,"physical movement clamps to displayed angle "+kind,.00001);
        double action=b.DronePatrolStats(d).N("action_radius"),speed=b.DronePatrolStats(d).N("patrol_speed"),weapon=band.WeaponRange;
        Check(g.SetCombatSetting("patrol_coverage_multiplier",1),"editable patrol multiplier accepted "+kind);var reduced=b.GetFactoryCoverage(18)!;var smaller=reduced.Bands.Single();
        Near(smaller.PatrolRadius,band.PatrolRadius*.5,"existing physical craft picks up changed patrol setting "+kind,1e-9);Near(smaller.OuterRange,band.OuterRange*.5,"existing physical craft picks up changed outer setting "+kind,1e-9);
        Near(b.DronePatrolStats(d).N("action_radius"),action,"action radius is not multiplied "+kind,1e-12);Near(b.DronePatrolStats(d).N("patrol_speed"),speed,"patrol speed unchanged by coverage control "+kind,1e-12);Near(smaller.WeaponRange,weapon,"weapons not multiplied with coverage "+kind,1e-12);
        Check(!ReferenceEquals(active,reduced)&&active.Bands[0]==band,"new configuration never mutates earlier snapshot "+kind);
        g.DeepResearch["C_N1"]=1L;g.InvalidateFactoryStats();var capital=b.GetFactoryCoverage(18)!.Bands.Single();
        Near(capital.WeaponRange,weapon*1.2,"C_N1 increases ordinary weapon reach exactly once "+kind,1e-12);Near(capital.AngleRadians,smaller.AngleRadians,"capital tech does not inflate angular coverage "+kind,1e-12);
        Check(capital.LargeTargetWeaponRange==capital.WeaponRange&&capital.LargeTargetAttackRadius==capital.AttackRadius,"C_N1 applies equally to ordinary and capital targets "+kind);
        SetPosition(b,d,Vector3.Back*(float)capital.PursuitRadius);
        var largeInside=Target(Vector3.Back*(float)(capital.LargeTargetAttackRadius-.005),true);var largeOutside=Target(Vector3.Back*(float)(capital.LargeTargetAttackRadius+.005),true);
        Check(b.AssignmentValid(d,largeInside)&&!b.AssignmentValid(d,largeOutside),"actual large target assignment matches separate coverage "+kind);
        Check(b.AssignmentValid(d,Target(largeInside.Vector3("space_position"))),"ordinary enemy receives the same C_N1 reach "+kind);
        Near(capital.MaximumLargeTargetCombatAltitude,capital.LargeTargetAttackRadius-CombatScale.EarthRadius,"capital target altitude is explicit "+kind,1e-12);
        string gameBefore=g.Serialize().ToJson();int alive=b.Drones.Count;double timer=b.Factories[18].N("timer");var a=b.GetFactoryCoverage(18);var again=b.GetFactoryCoverage(18);
        Check(ReferenceEquals(a,again),"stable queries reuse immutable snapshot "+kind);Check(alive==b.Drones.Count&&timer==b.Factories[18].N("timer")&&gameBefore==g.Serialize().ToJson(),"stable query creates no aircraft, resources or simulation time "+kind);
        var snapshot=b.SerializeCombatSnapshot();var reopened=new Battlefield(g,s);Check(reopened.RestoreCombatSnapshot(snapshot),"coverage fixture combat save accepted "+kind);Check(reopened.GetFactoryCoverage(18)!.Bands.SequenceEqual(again!.Bands),"coverage reconstructs identically from physical saved life "+kind);
    }
    private static void PlannedAndPhysicalFrames()
    {
        var (g,s,b)=Create();g.DeepResearch["K_N2"]=1L;g.InvalidateFactoryStats();Check(g.SetCombatSetting("interceptor_factory_capacity",6),"capacity supports mixed one/two-point physical frames");b.GetFactoryCoverage(18);
        var first=b.SpawnFactoryDrone(b.Factories[18],0);var original=b.GetFactoryCoverage(18)!.Bands.Single();
        Check(g.SetAirframe("interceptor",18,-1,"K3"),"next factory production chooses real K3 frame");var live=b.GetFactoryCoverage(18)!;
        Check(first.S("airframe_id")=="K1"&&live.Bands.Single()==original,"changing future plan does not replace living K1 range with K3");
        var second=b.SpawnFactoryDrone(b.Factories[18],2);var mixed=b.GetFactoryCoverage(18)!;
        Check(second.S("airframe_id")=="K3"&&mixed.AircraftCount==2&&mixed.Bands.Count==2,"mixed physical K1 and K3 expose distinct real bands");
        Near(mixed.Bands.Max(x=>x.WeaponRange),original.WeaponRange*AirframeCatalog.Definition("K3").N("range_multiplier"),"K3 physical range includes actual frame multiplier",1e-12);
        Check(g.SetAirframe("interceptor",18,4,"K1"),"specific planned berth can use base K1");b.ApplyDroneDamage(first,1e9);b.ApplyDroneDamage(second,1e9);var planned=b.GetFactoryCoverage(18)!;
        Check(planned.Preview&&planned.AircraftCount==0&&planned.Bands.Count==2,"no survivors previews all enabled berth-specific production frames");
        var replacement=b.SpawnFactoryDrone(b.Factories[18],0);var replacementView=b.GetFactoryCoverage(18)!;
        Check(!replacementView.Preview&&replacementView.AircraftCount==1&&replacementView.Bands.Count==1&&replacement.S("airframe_id")=="K3","replacement switches to actual physical frame and removes unbuilt preview bands");
        var mutable=new List<FactoryCoverageBand>{original};var immutable=new FactoryCoverageSnapshot(18,"interceptor",false,1,mutable);mutable.Clear();Check(immutable.Bands.Count==1,"snapshot constructor defensively copies caller collection");
        bool blocked=false;try{((IList<FactoryCoverageBand>)immutable.Bands)[0]=original with{WeaponRange=999};}catch(NotSupportedException){blocked=true;}Check(blocked&&immutable.Bands[0].WeaponRange==original.WeaponRange,"bands cannot be edited through an IList cast");
    }
    private static void PerkOwnershipAndDeduplication()
    {
        var (g,s,b)=Create();Check(g.FactoryPerks.ResetRunSites(g.RunId),"perk ownership bound to the actual run");var settings=g.FactoryPerks.Snapshot();settings.Map("levels")["navigation"]=5L;settings.Map("levels")["a_kinetic_core"]=3L;settings["energy_cores"]=1000L;Check(g.FactoryPerks.ImportSnapshot(settings),"real coverage-affecting perk unlocked in memory");
        var first=b.SpawnFactoryDrone(b.Factories[18],0);var second=b.SpawnFactoryDrone(b.Factories[18],1);var neighbor=b.SpawnFactoryDrone(b.Factories[19],0);var before=b.GetFactoryCoverage(18)!;var other=b.GetFactoryCoverage(19)!;
        Check(before.AircraftCount==2&&before.Bands.Count==1,"identical living profiles collapse into one band");Check(g.FactoryPerks.Equip("interceptor",18,0,"navigation"),"factory navigation perk equipped for one real site");var boosted=b.GetFactoryCoverage(18)!;
        Check(boosted.Bands.Single().PatrolRadius>before.Bands.Single().PatrolRadius&&boosted.Bands.Single().OuterRange>before.Bands.Single().OuterRange,"coverage includes real factory navigation perk");Check(b.GetFactoryCoverage(19)!.Bands.SequenceEqual(other.Bands),"same family different factory retains its own coverage");
        Check(g.FactoryPerks.Equip("interceptor",18,0,"a_kinetic_core","aircraft",1,"K1"),"individual berth gets real different weapon stats");b.GetFactoryCoverage(18);Check(b.DroneWeaponStats(second).N("damage")>b.DroneWeaponStats(first).N("damage"),"fixture actually has unequal berth weapon stats");Check(b.GetFactoryCoverage(18)!.Bands.Count==1,"different damage-only berth profiles still deduplicate equal geometric coverage");
        b.ApplyDroneDamage(first,1e9);Check(b.GetFactoryCoverage(18)!.AircraftCount==1&&!b.GetFactoryCoverage(18)!.Preview,"death refreshes roster count without stale cache");
        s.Rotation=new Basis(Vector3.Up,1.2f);b.Step(0);var worldBand=b.GetFactoryCoverage(18)!.Bands.Single();var home=s.SurfaceToSpace(Vector3.Back,0).Normalized();SetPosition(b,second,home*(float)worldBand.PursuitRadius);Check(b.AssignmentValid(second,Target(home*(float)(worldBand.AttackRadius-.005))),"same angular coverage follows real rotated factory home in combat");Check(b.GetFactoryCoverage(19)!.AircraftCount==1&&neighbor.L("factory_site_id")==19,"rotation/query never changes factory affiliation");
    }
    private static void EmptyAndInvalid()
    {
        var (g,s,b)=Create();Check(b.GetFactoryCoverage(-1)==null&&b.GetFactoryCoverage(12345)==null,"invalid and absent IDs return null");Check(new Battlefield().GetFactoryCoverage(18)==null,"uninitialized battle returns no coverage");
        s.Add(20,"mine",Vector3.Down);Check(b.GetFactoryCoverage(20)==null,"resource facility never produces combat coverage");s.Add(21,"interceptor",Vector3.Up);Check(b.GetFactoryCoverage(21) is {Preview:true},"newly built factory immediately exposes a planned envelope");s.Sites.RemoveAll(x=>x.L("site_id")==21);Check(b.GetFactoryCoverage(21)==null,"removed factory discards cached coverage");
        g.DeepResearch["K_N2"]=1L;g.InvalidateFactoryStats();Check(g.SetCombatSetting("interceptor_factory_capacity",1),"minimum capacity configuration accepted");Check(g.SetAirframe("interceptor",18,-1,"K3"),"two-point frame selected for production plan");var empty=b.GetFactoryCoverage(18)!;Check(empty.Preview&&empty.AircraftCount==0&&empty.Bands.Count==0,"factory with no enabled production berth has a truthful empty plan");
    }
}

