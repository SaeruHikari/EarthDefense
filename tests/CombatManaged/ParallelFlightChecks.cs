using Earthward.Combat;
using Earthward.Domain;
using Earthward.Tests;
using Godot;

internal static class ParallelFlightChecks
{
    private sealed class RotatingSurface : ICombatSurface
    {
        private readonly IReadOnlyList<DataMap> _sites;
        private readonly Vector3[] _launches,_directions;
        public Transform3D Frame=Transform3D.Identity;public long SpatialRevision{get;private set;}
        public RotatingSurface(IReadOnlyList<DataMap> sites){_sites=sites;_launches=sites.Select(s=>s.Vector3("launch_position")).ToArray();_directions=sites.Select(s=>s.Vector3("launch_direction")).ToArray();}
        public void Rotate(float angle){Frame=new(new Basis(Vector3.Up,angle),Vector3.Zero);SpatialRevision++;for(int i=0;i<_sites.Count;i++){_sites[i]["launch_position"]=Frame*_launches[i];_sites[i]["launch_direction"]=Frame.Basis*_directions[i];}}
        public IReadOnlyList<DataMap> GetFactorySites()=>_sites;
        public Vector3 SurfaceToSpace(Vector3 normal,double altitude)=>Frame*(normal.Normalized()*(float)(CombatScale.EarthRadius+altitude));
        public Vector3 SpaceToSurface(Vector3 position)=>(Frame.AffineInverse()*position).Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()=>Array.Empty<Vector3>();
        public bool TryGetCoordinateFrame(out Transform3D frame){frame=Frame;return true;}
    }
    public static void Run()
    {
        FleetStressFixture.ConfigureCatalog();var ga=new DefenseState();var gb=new DefenseState();var a=new Battlefield(ga){ForceSerialFlight=true};var b=new Battlefield(gb){Performance=new CombatPerformanceCounters()};
        var fa=new FleetStressFixture(ga,a,1200,200){FreezeCompiledProfilesEnabled=false};var fb=new FleetStressFixture(gb,b,1200,200){FreezeCompiledProfilesEnabled=false};fa.Setup(FleetStressMode.Combat);fb.Setup(FleetStressMode.Combat);
        var sa=new RotatingSurface(fa.Sites);var sb=new RotatingSurface(fb.Sites);a.Surface=sa;b.Surface=sb;a.InvalidateFactoryLayout();b.InvalidateFactoryLayout();int checks=0,failed=0;
        void Check(bool ok,string label){checks++;if(!ok){failed++;Console.WriteLine("FAIL parallel flight "+label);}}
        for(int tick=0;tick<120;tick++)
        {
            sa.Rotate(tick*.001f);sb.Rotate(tick*.001f);fa.MaintainPopulation();fb.MaintainPopulation();
            if(tick==15)for(int i=41;i<a.Drones.Count;i+=31){a.Drones[i]["hp"]=a.Drones[i].N("max_hp")*.25;b.Drones[i]["hp"]=b.Drones[i].N("max_hp")*.25;}
            if(tick==35){ga.SetCombatSetting("patrol_coverage_multiplier",1.5);gb.SetCombatSetting("patrol_coverage_multiplier",1.5);}
            a.Step(1d/60);b.Step(1d/60);ga.Tick(1d/60);gb.Tick(1d/60);
            Check(b.FlightEligibilityChecks<=b.Drones.Count*2+8,"fragmented healthy segments scan each actor at most twice tick "+tick);
            if(tick%10==9)
            {
                CombatSnapshotCodec.TryDecode(a.SerializeCombatSnapshot().Value("payload"),out var left);CombatSnapshotCodec.TryDecode(b.SerializeCombatSnapshot().Value("payload"),out var right);
                Check(DataMap.Equivalent(left,right),"all actor fields / rotated home / runtime configuration tick "+tick);Check(a.Random.State==b.Random.State,"RNG tick "+tick);
                if(failed>0){File.WriteAllText("artifacts/flight-serial-mismatch.json",((DataMap)left!).ToJson());File.WriteAllText("artifacts/flight-parallel-mismatch.json",((DataMap)right!).ToJson());break;}
            }
            if(tick==60){var snapshot=DataMap.Parse(b.SerializeCombatSnapshot().ToJson());bool valid=Battlefield.ValidateCombatSnapshot(snapshot);Check(valid,"snapshot validates while planes / projectiles active");if(!valid){File.WriteAllText("artifacts/fleet127-invalid-live-snapshot.json",snapshot.ToJson());CombatSnapshotCodec.TryDecode(snapshot.Value("payload"),out var decoded);var payload=(DataMap)decoded!;foreach(string key in new[]{"_shots","_hostile_shots","_drones","enemies"})foreach(var actor in payload.List(key).OfType<DataMap>()){string method=key.Contains("shots")?"ValidatePerkShot":"ValidatePerkActor";bool accepted=(bool)typeof(Battlefield).GetMethod(method,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,new object[]{actor})!;if(!accepted)Console.WriteLine("INVALID "+key+" "+actor.ToJson());}break;}Check(b.RestoreCombatSnapshot(snapshot),"save and reload while planes / projectiles active");}
        }
        Console.WriteLine($"PARALLEL_FLIGHT checks={checks} failed={failed}");if(failed>0)System.Environment.ExitCode=1;
    }
}
