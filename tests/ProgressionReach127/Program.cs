using Earthward;
using Earthward.Domain;
using Earthward.Combat;
using Godot;
using System.Reflection;
using System.Globalization;

internal static class Program
{
    private static int _checks, _failures;
    private static readonly MethodInfo Firing = typeof(Battlefield).GetMethod("UpdateFiring", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly PropertyInfo ClockProperty = typeof(Battlefield).GetProperty("Clock")!;
    private static void Check(bool ok, string text) { _checks++; if (!ok) { _failures++; Console.WriteLine("FIRSTCLEAR_REACH_FAIL " + text); } }
    private static DefenseState NormalProgress(int stage)
    {
        // Funds are fixture inputs. All technology is bought through the actual DAG; no free unlock flag or injected nodes.
        var game = new DefenseState { Minerals = 1e8, Energy = 1e8, Science = 1e8, AlienPoints = 10000, CompletedWaves = 29, Wave = 28 };
        game.RewardKill("boss", 3, 0);
        if (stage > 0) game.SetDefenseReachStage(stage);
        int bought;
        do { bought = 0; foreach (var node in DeepTechnology.Nodes) if (game.GetGroupStatus(node.S("id")).B("can_purchase") && game.PurchaseGroup(node.S("id"))) bought++; } while (bought > 0);
        Check(game.HasResearch("C_G1") && !game.Serialize().Map("research_flags").B("unrestricted_research"), "normal DAG unlocks assault without unrestricted research stage="+stage);
        Check(game.DefenseReachStage == stage, "buying the tree cannot silently clear motherships stage="+stage);
        if (stage == 0) Check(!game.HasResearch("C_A2") && !game.HasResearch("C_A3") && !game.HasResearch("C_G2"), "first-clear research has no dependency on any frontier unlock");
        return game;
    }
    private static void TickFlight(Battlefield battle)
    {
        ClockProperty.SetValue(battle,battle.Clock+1d/30);
        battle.UpdateDrones(1d/30);
        Firing.Invoke(battle,new object[]{1d/30});
        battle.UpdateShots(1d/30);
    }
    private static bool FlyAndHit(Battlefield battle, DataMap craft, DataMap target, DataMap? support, out double seconds)
    {
        bool shieldBroken = target.N("energy_hp") <= 0, fired = false, engage = false;
        double hp = target.N("hp"), begin = battle.Clock;
        for (int tick = 0; tick < 7200; tick++)
        {
            TickFlight(battle);
            if (!shieldBroken && target.N("energy_hp") <= 0)
            {
                shieldBroken = true;
                if (support != null) { battle.Drones.Remove(support); support = null; hp = target.N("hp"); }
            }
            fired |= craft.L("total_weapon_shots") > 0;
            engage |= battle.CanDroneEngagePosition(craft, target.Vector3("space_position"));
            if (shieldBroken && fired && target.N("hp") < hp)
            {
                seconds = battle.Clock-begin;
                Check(engage, "actual flight reaches CanDroneEngagePosition before hull damage");
                return true;
            }
        }
        seconds = battle.Clock-begin;
        Console.WriteLine($"REACH_TRACE kind={craft.S("kind")} phase={craft.S("state")} target={craft.L("target_uid")} aim={craft.L("aim_target_uid")} dist={battle.GetDroneWorldPosition(craft).DistanceTo(target.Vector3("space_position")):0.000} hp={target.N("hp")} energy={target.N("energy_hp")} shots={craft.L("total_weapon_shots")} position={battle.GetDroneWorldPosition(craft)}");
        return false;
    }
    private static void CheckFramesAndProgression()
    {
        for (int stage = 0; stage <= 3; stage++)
        {
            var game = NormalProgress(stage);
            foreach (var frame in AirframeCatalog.Definitions)
            {
                string id = frame.S("id"), kind = frame.S("kind");
                bool available = game.AvailableAirframes(kind).Single(row => row.S("id") == id).B("unlocked");
                if (!available) { Console.WriteLine($"RANGE_MATRIX stage={stage} frame={id} locked={frame.S("unlock_node")}"); continue; }
                Check(game.SetAirframe(kind,-1,-1,id), "normal unlocked frame can be selected " + id);
                var b = new Battlefield(game,new Surface(kind,Vector3.Back));
                var craft = b.SpawnFactoryDrone(b.Factories[18]);
                var band = b.GetFactoryCoverage(18)!.Bands[0];
                double radius = stage == 0 ? InvasionDirector.MothershipRadius : game.CombatSettings.N("frontier_radius_"+stage);
                Check(band.AttackRadius >= radius && band.PursuitRadius >= radius-1.12, "pursuit and weapon range reach current mothership " + id + ":" + stage);
                if (stage < 3) Check(band.AttackRadius < game.CombatSettings.N("frontier_radius_"+(stage+1)), "next frontier still requires its own research " + id + ":" + stage);
                Console.WriteLine($"RANGE_MATRIX stage={stage} frame={id} patrol={band.PatrolRadius:0.000} outer={band.OuterRange:0.000} angle={band.AngleRadians*180/Math.PI:0.00} pursuit={band.PursuitRadius:0.000} weapon={band.WeaponRange:0.000} attack={band.AttackRadius:0.000} mother={radius:0.000}");
            }
        }
    }
    private static void CheckEightFronts()
    {
        var directions = new InvasionDirector(); directions.ConfigureAnchor(Vector3.Back);
        foreach (var front in directions.FrontsForWave(29))
            foreach (string kind in new[]{"interceptor","missile","laser"})
            {
                var game = NormalProgress(0);
                var patrol = game.PatrolStats(kind);
                double angle = (patrol.N("patrol_radius")+patrol.N("patrol_outer_range"))/WorldScale.EarthRadius;
                Vector3 direction = front.Vector3("direction");
                Vector3 home = direction.Rotated(CombatGeometry.AxisBetween(direction,Vector3.Up),(float)(angle*.9)).Normalized();
                var surface = new Surface(kind,home,kind=="interceptor");
                var b = new Battlefield(game,surface) { Active=true };
                b.RestoreInvasionAnchor(Vector3.Back);
                b.RestoreDestroyedFronts(directions.FrontsForWave(29).Where(row=>row.S("id")!=front.S("id")).Select(row=>row.S("id")));
                b.Random.Seed=127;
                b.StartWave();
                var target = b.Motherships[front.S("id")];
                Check(target.Vector3("space_position").DistanceTo(front.Vector3("world_position"))<.0001, "real fixed front position remains unchanged " + front.S("id"));
                var craft=b.SpawnFactoryDrone(b.Factories[18]);
                var band=b.GetFactoryCoverage(18)!.Bands[0];
                Check(home.AngleTo(direction)<band.AngleRadians && home.AngleTo(direction)>band.AngleRadians/1.5, "near-front factory tests newly opened angular region " + front.S("id")+":"+kind);
                DataMap? support=null;
                if(kind=="interceptor")
                {
                    Check(!b.AssignmentValid(craft,target) && EnemyArmor.Multiplier(target,"kinetic")==0, "energy immunity retained before laser support");
                    support=b.SpawnFactoryDrone(b.Factories[19]);
                }
                else Check(b.AssignmentValid(craft,target), "mother inside true angular and radial assignment envelope " + front.S("id")+":"+kind);
                bool hit=FlyAndHit(b,craft,target,support,out double seconds);
                Check(hit, "launch, approach, aim, fire and damage real mother " + front.S("id")+":"+kind);
                Check(b.AssignmentValid(craft,target), "actual mother remains inside reported attack envelope after shield break");
                var outside=target.DeepClone();
                outside["space_position"]=home.Rotated(CombatGeometry.AxisBetween(home,direction),(float)(band.AngleRadians+.01)).Normalized()*(float)InvasionDirector.MothershipRadius;
                Check(!b.AssignmentValid(craft,outside), "target beyond displayed angular boundary remains unreachable");
                if(kind=="interceptor") Check(target.N("energy_hp")<=0 && craft.L("total_weapon_shots")>0, "laser breaks shield then kinetic takes over real hull damage");
                Console.WriteLine($"FRONT_FLIGHT front={front.S("id")} kind={kind} hit={hit} seconds={seconds:0.00} from_angle={home.AngleTo(direction)*180/Math.PI:0.0} radius={b.GetDroneWorldPosition(craft).Length():0.00}");
            }
    }
    private static void CheckInitialFactory()
    {
        foreach(string kind in new[]{"interceptor","missile","laser"})
        {
            var game=NormalProgress(0);
            var b=new Battlefield(game,new Surface(kind,Vector3.Back,kind=="interceptor")){Active=true};
            b.Random.Seed=127; b.StartWave();
            Check(b.Motherships.Count==8,"initial home test retains all eight actual near-Earth mothers");
            var craft=b.SpawnFactoryDrone(b.Factories[18]);
            var support=kind=="interceptor"?b.SpawnFactoryDrone(b.Factories[19]):null;
            bool hit=FlyAndHit(b,craft,b.Motherships["front_01"],support,out double seconds);
            Check(hit,"original home factory physically attacks first mother "+kind);
            Console.WriteLine($"HOME_FLIGHT kind={kind} hit={hit} seconds={seconds:0.00}");
        }
    }
    private static void CheckFrontierFlights()
    {
        for(int stage=1;stage<=3;stage++) foreach(string kind in new[]{"interceptor","missile","laser"})
        {
            var game=NormalProgress(stage);
            var stats=game.PatrolStats(kind);
            float offset=(float)((stats.N("patrol_radius")+stats.N("patrol_outer_range"))/WorldScale.EarthRadius*.85);
            var home=Vector3.Back.Rotated(Vector3.Up,offset);
            var b=new Battlefield(game,new Surface(kind,home,kind!="laser")){Active=true};
            b.Random.Seed=127;
            var craft=b.SpawnFactoryDrone(b.Factories[18]);
            double radius=game.CombatSettings.N("frontier_radius_"+stage);
            var target=b.SpawnEnemy("carrier",new(){["wave"]=30L,["stage"]=stage},Vector3.Back*(float)radius)!;
            var coverage=b.GetFactoryCoverage(18)!.Bands[0];
            DataMap? support=null;
            if(kind!="laser" && target.N("energy_hp")>0) support=b.SpawnFactoryDrone(b.Factories[19]);
            Check(coverage.AttackRadius>=radius,"actual frontier target inside reported overlay radius");
            bool hit=FlyAndHit(b,craft,target,support,out double seconds);
            Check(hit,"normal DAG frontier flight, aim and hit "+kind+":"+stage);
            Check(b.GetDroneWorldPosition(craft).Length()<=coverage.PursuitRadius+.001,"frontier flight obeys actual radial cap");
            Console.WriteLine($"FRONTIER_FLIGHT stage={stage} kind={kind} hit={hit} seconds={seconds:0.00} mother={radius:0.0} from_angle={offset*180/Math.PI:0.0}");
        }
    }
    static void Main()
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        CatalogData.Configure(Path.GetFullPath("data/domain"));
        CheckFramesAndProgression();
        CheckInitialFactory();
        CheckEightFronts();
        CheckFrontierFlights();
        Console.WriteLine($"FIRSTCLEAR_REACH_CHECKS {_checks} checks / {_failures} failures");
        System.Environment.ExitCode=_failures==0?0:1;
    }
    private sealed class Surface : ICombatSurface
    {
        private readonly List<DataMap> _sites = new();
        public Surface(string kind,Vector3 normal,bool support=false)
        {
            Add(18,kind,normal);
            if(support)Add(19,"laser",normal);
        }
        private void Add(long id,string kind,Vector3 normal) => _sites.Add(new(){["site_id"]=id,["kind"]=kind,["normal"]=normal,["launch_position"]=normal*(WorldScale.EarthRadius+.108f),["launch_direction"]=CombatGeometry.AxisBetween(normal,Vector3.Up).Cross(normal).Normalized()});
        public IReadOnlyList<DataMap> GetFactorySites()=>_sites;
        public Vector3 SurfaceToSpace(Vector3 normal,double altitude)=>normal.Normalized()*(float)(WorldScale.EarthRadius+altitude);
        public Vector3 SpaceToSurface(Vector3 position)=>position.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>_sites.Select(row=>row.Vector3("normal")).ToArray();
    }
}