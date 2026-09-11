using Earthward;
using Earthward.Domain;
using Earthward.Combat;
using Godot;
using System.Reflection;

internal static class AllDirectionsCombat
{
    private static Action<bool,string> Check = null!;
    private static Action<double,double,string,double> Near = null!;
    private static object? Call(Battlefield b,string name,params object?[] args) => typeof(Battlefield).GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(b,args);
    private sealed class RotatingSurface : ICombatSurface
    {
        private readonly List<DataMap> _sites = new(){new(){["site_id"]=3L,["kind"]="interceptor",["normal"]=Vector3.Back}};
        public Basis Rotation = Basis.Identity;
        public IReadOnlyList<DataMap> GetFactorySites()
        {
            _sites[0]["launch_position"]=SurfaceToSpace(Vector3.Back,.108);
            _sites[0]["launch_direction"]=Rotation*Vector3.Right;
            return _sites;
        }
        public Vector3 SurfaceToSpace(Vector3 normal,double altitude)=>Rotation*normal.Normalized()*(float)(WorldScale.EarthRadius+altitude);
        public Vector3 SpaceToSurface(Vector3 position)=>Rotation.Inverse()*position.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>new[]{Vector3.Back};
    }
    public static void Run(Action<bool,string> check,Action<double,double,string,double> near)
    {
        Check=check;Near=near;FixedWorldFronts();AllDirectionDamage();FixedWaveCounts();
    }
    private static void FixedWorldFronts()
    {
        var director=new InvasionDirector();director.ConfigureAnchor(Vector3.Back);var rng=new CombatRandom(125);
        Check(director.FrontsForWave(29).Any(f=>f.Vector3("direction").Dot(Vector3.Back)<-.9),"original eight fronts cover both hemispheres");
        for(int i=0;i<1000;i++)
        {
            var spawn=director.SpawnPoint(29,rng);var p=spawn.Vector3("position");var n=spawn.Vector3("direction");
            double width=(CombatScale.LegacyEarthRadius+5.2)*Math.Sin(director.SpawnConeRadians(29));
            Check(p.Cross(n).Length()<=width+.00001,"scatter retains original world width on radius16");
            Check(p.Length()>=CombatScale.SpawnMin-.00001&&p.Length()<=CombatScale.SpawnMax+.00001,"unrestricted spawn altitude unchanged");
        }
        var game=new DefenseState();var surface=new RotatingSurface();var b=new Battlefield(game,surface);b.StartWave();b.Active=false;
        var mother=b.Motherships["front_01"];var initial=mother.Vector3("space_position");var anchor=b.GetInvasionAnchor();long uid=mother.L("uid");double hp=mother.N("hp");
        for(int i=0;i<300;i++)
        {
            surface.Rotation=surface.Rotation.Rotated(Vector3.Up,.005f);b.Step(1d/30);
            Near(mother.Vector3("space_position").DistanceTo(initial),0,"mother remains fixed in world space during Earth rotation",.000001);
        }
        Near(b.GetInvasionAnchor().DistanceTo(anchor),0,"invasion anchor never follows the rotating surface",.000001);
        Check(mother.L("uid")==uid&&mother.N("hp")==hp,"world-stationary mother keeps identity and health");
    }
    private static void AllDirectionDamage()
    {
        var game=new DefenseState{Wave=1};var b=new Battlefield(game,new RotatingSurface());var farSide=Vector3.Forward*(float)CombatScale.CloseAssault;
        var enemy=b.SpawnEnemy("scout",new(){["wave"]=1L,["role"]="claw"},farSide)!;enemy["phase"]="ground_attack";
        Check((bool)Call(b,"CanBombardEarth",enemy)!,"wave1 far-side enemy has ordinary planetary attack access");double shield=game.EarthHp;
        Call(b,"FireHostile",enemy,2d,false);Check(b.HostileShots.Count>0,"far-side planetary shot is actually fired");b.UpdateShots(1.0);Near(game.EarthHp,shield-2,"actual far-side shot damages Earth normally",.000001);
        b.DetonateHostile(Vector3.Forward*(float)CombatScale.EarthCollisionRadius,new(){["damage"]=10d,["target_kind"]="earth",["blast_radius"]=0d},null,true);Near(game.EarthHp,shield-12,"all-direction impact has no geographic damage immunity",.000001);
    }
    private static void FixedWaveCounts()
    {
        foreach(long wave in new long[]{1,10,20,30,40})
        {
            var game=new DefenseState{Wave=wave-1};var b=new Battlefield(game,new RotatingSurface());b.Random.Seed=125;b.StartWave();
            long count=(game.CombatSettings.L("enemy_wave_base_count")+game.CombatSettings.L("enemy_wave_growth")*(wave-1))*game.CombatSettings.L("enemy_count_multiplier");
            var plan=b.GetWaveSpawnPlan();Near(plan.N("duration"),30,"30 second deployment window retained",.000001);Near(plan.N("cycle_duration"),45,"45 second wave cycle retained",.000001);
            Check(plan.L("planned_count")==count,"larger Earth never multiplies the wave budget");b.UpdateSpawning(30);
            Check(b.GetWaveSpawnPlan().L("spawned")==count&&b.WaveRemaining==0,"every planned enemy consumes one budget entry");
            long actual=b.Enemies.Count+b.Enemies.Sum(e=>e.List("cargo").Count);Check(actual==count,"hatcher reserve is within the same total budget");
        }
    }
}
