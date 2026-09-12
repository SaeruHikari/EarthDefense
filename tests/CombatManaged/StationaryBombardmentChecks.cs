using Earthward.Combat;
using Earthward.Domain;
using Godot;
using System.Reflection;

internal static class StationaryBombardmentChecks
{
    private static Action<bool,string> Check = null!;
    private static Action<double,double,string,double> Near = null!;
    private static object? Call(Battlefield b,string name,params object?[] values) => typeof(Battlefield).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(b,values);
    private sealed class Surface : ICombatSurface
    {
        public List<DataMap> Sites {get;}=new();
        public IReadOnlyList<DataMap> GetFactorySites()=>Sites;
        public Vector3 SurfaceToSpace(Vector3 n,double altitude)=>n.Normalized()*(float)(CombatScale.EarthRadius+altitude);
        public Vector3 SpaceToSurface(Vector3 p)=>p.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>Sites.Select(s=>s.Vector3("normal")).ToList();
    }
    public static void RunStandalone()
    {
        int count=0,failed=0;
        void Assert(bool ok,string label){count++;if(!ok){failed++;Console.WriteLine("BOMBARD_FAIL "+label);}}
        void Almost(double actual,double expected,string label,double tolerance)=>Assert(double.IsFinite(actual)&&Math.Abs(actual-expected)<=tolerance,$"{label} ({actual}/{expected})");
        Run(Assert,Almost);Console.WriteLine($"STATIONARY_BOMBARDMENT {count} checks; {failed} failures");System.Environment.ExitCode=failed==0?0:1;
    }
    public static void Run(Action<bool,string> check,Action<double,double,string,double> near)
    {
        Check=check;Near=near;
        foreach(string role in DefenseWavePlan.RoleOrder)TravelHoldFireRetreat(role);
        ApproachCounterfire();SavedHoldAndReapproach();LargeHullAndBossRoutes();
    }
    private static (DefenseState Game,Battlefield Battle,DataMap Enemy) Scene(string role="claw",double altitude=2,double scale=.5)
    {
        string kind=role is "claw" or "needle" or "prism" or "jammer"?"scout":"cruiser";
        var g=new DefenseState{Wave=1};g.SetCombatSetting("enemy_aircraft_scale",scale);var b=new Battlefield(g,new Surface()){Active=true};b.Random.Seed=126;
        return(g,b,b.SpawnEnemy(kind,new(){["wave"]=1L,["role"]=role},Vector3.Back*(float)(CombatScale.EarthRadius+altitude))!);
    }
    private static void TravelHoldFireRetreat(string role)
    {
        var (g,b,e)=Scene(role);var start=e.Vector3("space_position");var held=Vector3.Zero;var phases=new List<string>{"approach"};var shots=new HashSet<long>();var fireTimes=new List<double>();int heldFrames=0;double heldAt=-1;
        for(int tick=0;tick<3600;tick++)
        {
            b.Step(1d/60);
            string phase=e.S("phase");if(phases[^1]!=phase)phases.Add(phase);
            foreach(var shot in b.HostileShots)
                if(shot.S("target_kind")=="earth"&&shots.Add(shot.L("uid")))
                {
                    fireTimes.Add(b.Clock);Check(shot.Vector3("tangent").Dot(-shot.Vector3("space_position").Normalized())>.99,"planetary shots point toward Earth "+role);
                }
            if(e.B("stationary_bombard"))
            {
                if(heldAt<0){held=e.Vector3("space_position");heldAt=b.Clock;}
                heldFrames++;
                Near(e.Vector3("space_position").DistanceTo(held),0,"bombarding ship never drifts along surface "+role,1e-8);
                Near(e.Vector3("velocity").LengthSquared(),0,"bombarding ship stops actual velocity "+role,1e-12);
                Check(e.Vector3("tangent").Dot(-held.Normalized())>.9999&&e.Vector3("aim_direction").Dot(-held.Normalized())>.9999,"rendered nose and aim face Earth "+role);
                Check(e.Vector3("space_position").Length()>CombatScale.EarthCollisionRadius+e.N("hit_radius")&&e.Vector3("space_position").Length()>=CombatScale.ShieldShellRadius-.0001,"stationary hull never descends below the shield shell "+role);
                Check(e.S("phase")=="ground_attack","stationary status retains compatible attack phase "+role);
            }
            if(!b.Enemies.Contains(e))break;
        }
        Check(start.Length()>held.Length()&&heldAt>0,"approach physically flies down before parking "+role);
        Check(phases.SequenceEqual(new[]{"approach","ground_attack","retreat"}),"one-way approach/hold/retreat transitions without jitter "+role);
        Check(heldFrames>300&&shots.Count>=2&&g.EarthHp<100,"stationary ship repeatedly shoots and causes real planetary damage "+role);
        Near(e.N("bombard_time"),e.N("bombard_duration"),"original finite bombardment time is preserved "+role,1d/60+.000001);
        Check(!e.B("stationary_bombard")&&!e.ContainsKey("aim_direction")&&!b.Enemies.Contains(e),"finite bombardment ends in ordinary outward retreat "+role);
        if(role=="claw")for(int i=1;i<fireTimes.Count;i++)Near(fireTimes[i]-fireTimes[i-1],e.N("attack_cooldown"),"ordinary attack frequency retained",1d/60+.000001);
    }
    private static void ApproachCounterfire()
    {
        var g=new DefenseState{Wave=1};var surface=new Surface();surface.Sites.Add(new(){["site_id"]=18L,["kind"]="interceptor",["normal"]=Vector3.Back,["launch_position"]=Vector3.Back*(float)(CombatScale.EarthRadius+.108),["launch_direction"]=Vector3.Right});var b=new Battlefield(g,surface);
        var d=b.SpawnFactoryDrone(b.Factories[18]);d["state"]="engaging";d["launch_age"]=2d;d["space_position"]=Vector3.Back*(float)(CombatScale.EarthRadius+.7);b.WorldCache[d.L("uid")]=d.Vector3("space_position");
        var e=b.SpawnEnemy("scout",new(){["wave"]=1L,["role"]="claw"},Vector3.Back*(float)(CombatScale.EarthRadius+3))!;e["fire"]=0d;
        Call(b,"UpdateEnemies",1d/60);Check(b.HostileShots.Count==0,"approaching enemy cannot fire at friendly outside return-fire range");
        e["space_position"]=Vector3.Back*(float)(CombatScale.CloseAssault+.6);e["fire"]=0d;Call(b,"UpdateEnemies",1d/60);
        Check(e.S("phase")=="approach"&&!e.B("stationary_bombard")&&b.HostileShots.Any(s=>s.S("target_kind")=="drone"),"approaching enemy retains legal anti-air attack before parking");
        b.HostileShots.Clear();e["space_position"]=Vector3.Back*(float)CombatScale.CloseAssault;e["fire"]=0d;Call(b,"UpdateEnemies",1d/60);
        Check(e.B("stationary_bombard")&&b.HostileShots.Any(s=>s.S("target_kind")=="earth")&&b.HostileShots.All(s=>s.S("target_kind")!="drone"),"parked ship prioritizes Earth while friendly is nearby");
    }
    private static void SavedHoldAndReapproach()
    {
        var(g,b,e)=Scene("claw",2);for(int i=0;i<60;i++)b.Step(1d/60);var p=e.Vector3("space_position");
        var save=b.SerializeCombatSnapshot();Check(Battlefield.ValidateCombatSnapshot(save),"stationary combat snapshot validates");var g2=new DefenseState();Check(g2.Restore(g.Serialize()),"stationary companion game restores");var copy=new Battlefield(g2,new Surface());Check(copy.RestoreCombatSnapshot(DataMap.Parse(save.ToJson())),"stationary world position and flag survive JSON restore");var e2=copy.Enemies.Single(x=>x.L("uid")==e.L("uid"));
        for(int i=0;i<60;i++){b.Step(1d/60);copy.Step(1d/60);Near(e2.Vector3("space_position").DistanceTo(p),0,"saved stationary ship never jumps or resumes orbit",1e-8);Near(e.N("bombard_time"),e2.N("bombard_time"),"saved hold timer continues identically",1e-12);Near(e.N("fire"),e2.N("fire"),"saved attack cadence continues identically",1e-12);}
        Check(b.HostileShots.Select(x=>x.L("uid")).SequenceEqual(copy.HostileShots.Select(x=>x.L("uid"))),"saved actual fire events remain deterministic");
        b.HostileShots.Clear();double previousTime=e.N("bombard_time");e["space_position"]=p.Normalized()*(p.Length()+.4f);var displaced=e.Vector3("space_position");b.Step(1d/60);
        Check(!e.B("stationary_bombard")&&e.S("phase")=="approach"&&!e.ContainsKey("aim_direction"),"invalid parking height releases aim lock and resumes approach");Check(e.Vector3("space_position").Length()<displaced.Length()&&e.Vector3("space_position").DistanceTo(displaced)<.05,"invalid stop repositions continuously rather than teleporting");Near(e.N("bombard_time"),previousTime,"reapproach does not reset or accumulate bombardment time",1e-12);Check(b.HostileShots.Count==0,"outside legal bombardment height cannot continue firing at Earth");
        for(int i=0;i<360&&!e.B("stationary_bombard");i++)b.Step(1d/60);Check(e.B("stationary_bombard")&&e.N("bombard_time")>=previousTime,"reapproach returns once to legal stationary bombardment");
        var invalid=save.DeepClone();CombatSnapshotCodec.TryDecode(invalid.Value("payload"),out var decoded);var fields=(DataMap)decoded!;var row=fields.List("enemies").OfType<DataMap>().Single();row["stationary_bombard"]="yes";invalid["payload"]=CombatSnapshotCodec.Encode(fields);Check(!Battlefield.ValidateCombatSnapshot(invalid),"malformed stationary state rejected");row["stationary_bombard"]=true;row["phase"]="approach";invalid["payload"]=CombatSnapshotCodec.Encode(fields);Check(!Battlefield.ValidateCombatSnapshot(invalid),"contradictory moving stationary phase rejected");
    }
    private static void LargeHullAndBossRoutes()
    {
        var(g,b,e)=Scene("rock",2,2);for(int i=0;i<600&&!e.B("stationary_bombard");i++)b.Step(1d/60);
        Check(e.B("stationary_bombard")&&e.Vector3("space_position").Length()>=CombatScale.EarthCollisionRadius+e.N("hit_radius")+.0249,"largest editable cruiser hull parks outside ground");Check(e.Vector3("space_position").Length()>=CombatScale.ShieldShellRadius,"large cruiser parks outside the shield shell instead of dipping below it");
        foreach(var scenario in new[]{("small_boss",3L),("boss",3L),("boss",10L),("boss",20L)})
        {
            var game=new DefenseState{Wave=scenario.Item2};var other=new Battlefield(game,new Surface()){Active=true};var actor=other.SpawnEnemy(scenario.Item1,new(){["wave"]=scenario.Item2},Vector3.Back*(float)(CombatScale.EarthRadius+1))!;
            var phases=new List<string>{"approach"};var shots=new HashSet<long>();var heldPosition=Vector3.Zero;int heldFrames=0;bool charged=false,exposed=false;
            for(int tick=0;tick<3600;tick++)
            {
                other.Step(1d/60);if(phases[^1]!=actor.S("phase"))phases.Add(actor.S("phase"));charged|=actor.S("skill_phase")=="charging"&&actor.N("telegraph")>0;exposed|=actor.N("armor_exposed_until")>other.Clock;
                foreach(var shot in other.HostileShots)if(shot.S("target_kind")=="earth")shots.Add(shot.L("uid"));
                if(actor.B("stationary_bombard"))
                {
                    if(heldFrames++==0)heldPosition=actor.Vector3("space_position");
                    Near(actor.Vector3("space_position").DistanceTo(heldPosition),0,"boss stays fixed throughout near-Earth bombardment "+scenario,1e-8);
                    Check(actor.Vector3("velocity")==Vector3.Zero&&actor.Vector3("tangent").Dot(-heldPosition.Normalized())>.9999,"boss nose remains toward Earth while skills charge "+scenario);
                }
                if(!other.Enemies.Contains(actor))break;
            }
            Check(heldFrames>300&&shots.Count>=2&&game.EarthHp<100,"boss parks and repeatedly causes actual Earth damage "+scenario);
            Check(phases.SequenceEqual(new[]{"approach","ground_attack","retreat"}),"boss makes stable approach/hold/retreat transitions "+scenario);
            Near(actor.N("bombard_time"),actor.N("bombard_duration"),"boss retains original finite bombardment window "+scenario,1d/60+.000001);
            Check(!actor.B("stationary_bombard")&&!other.Enemies.Contains(actor),"boss still retreats after its attack window "+scenario);
            if(scenario.Item1=="boss")Check(charged,"boss charge telegraph continues while stationary "+scenario);
            if(actor.S("boss_variant_id")=="forge")Check(exposed,"forge armor-exposure skill continues while stationary");
        }
        var carrier=b.SpawnEnemy("carrier",new(){["wave"]=30L},Vector3.Back*56)!;carrier["post_carrier"]=true;carrier["frontier_radius"]=56d;var before=carrier.Vector3("space_position");Call(b,"AdvancePostCarrier",carrier,.1);Check(!carrier.B("stationary_bombard")&&carrier.Vector3("space_position")==before,"outer mothership keeps its separate fixed-space deployment behavior");
    }
}
