using Godot;
using Earthward.Domain;
using Earthward.Rendering;

namespace Earthward.Tests;

public partial class RenderCacheChecks : Node
{
    private int _checks, _failures;
    private void Check(bool ok, string text) { _checks++; if (!ok) { _failures++; GD.PrintErr("RENDER_CACHE_FAIL: " + text); } }
    public override async void _Ready()
    {
        try
        {
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new Vector2I(1440, 900);
            var planet = new PlanetView { Game = new DefenseState(), ViewSize = new(1440, 900) };
            AddChild(planet); planet.SetProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var rng = new Random(127);
            for (int setup = 0; setup < 9; setup++)
            {
                planet.ResetCamera();
                if (setup == 1) planet.OrbitCamera(.8f, -.3f);
                if (setup == 2) planet.ZoomCamera(-5);
                if (setup == 3) planet.FocusBody("moon", true);
                if (setup == 4) planet.FocusSystem(true);
                if (setup == 5) planet.SetRenderSize(new(1920, 1200));
                if (setup == 6) planet.SetViewSize(new(1280, 720), new(2560, 1440));
                if (setup == 7) { planet.BeginAircraftView(77); planet.ApplyAircraftCamera(new Transform3D(Basis.FromEuler(new(.12f, .8f, 0)), new(0, 0, 18))); }
                if (setup == 8) { var pose = planet.CaptureCameraState(); planet.OrbitCamera(.3f); planet.RestoreCameraState(pose); }
                planet.RotatePlanet(.137f);
                var frame = planet.Globe.GlobalTransform;
                Check(planet.TryGetCoordinateFrame(out var cached) && cached == frame, "worker frame equals native transform " + setup);
                for (int i = 0; i < 256; i++)
                {
                    var normal = new Vector3((float)(rng.NextDouble()*2-1), (float)(rng.NextDouble()*2-1), (float)(rng.NextDouble()*2-1)).Normalized();
                    double altitude = rng.NextDouble() * 3;
                    var world = planet.Globe.ToGlobal(normal * (WorldScale.EarthRadius + (float)altitude));
                    Check(planet.SurfaceToSpace(normal, altitude).DistanceTo(world) < .00001f, "surface conversion equals native " + setup);
                    Check(planet.SpaceToSurface(world).DistanceTo(planet.Globe.ToLocal(world).Normalized()) < .000001f, "inverse conversion equals native " + setup);
                    var p = planet.Camera.GlobalTransform * new Vector3((float)(rng.NextDouble()*20-10), (float)(rng.NextDouble()*20-10), (float)(-2-rng.NextDouble()*70));
                    // Native C++ and managed float matrix inversion differ by a few ULPs at system distances; require less than 1/32 pixel, including offscreen points.
                    var projected = planet.RenderToLogical(planet.Camera.UnprojectPosition(p));
                    Check(projected.DistanceTo(planet.GetSpaceScreenPosition(p)) < .03125f, "projection equals native under camera/viewport changes " + setup + " delta=" + projected.DistanceTo(planet.GetSpaceScreenPosition(p)) + " native=" + projected + " cached=" + planet.GetSpaceScreenPosition(p));
                    Check(planet.Camera.ToLocal(p).DistanceTo(planet.GetCameraRelativePosition(p)) < .0001f, "camera relative value equals native " + setup);
                }
            }
            planet.ResetCamera();
            var space = new Node3D { Name = "PoseTestSpace" }; var surface = new Node3D { Name = "PoseTestSurface", Rotation = new(0,.31f,0) };
            var oldSpace = new Node3D { Name = "PoseReferenceSpace" }; var oldSurface = new Node3D { Name = "PoseReferenceSurface", Rotation = surface.Rotation };
            foreach (var node in new[]{space,surface,oldSpace,oldSurface}) planet.SpaceRoot.AddChild(node);
            var parallel = new FleetRenderer(space,surface);
            var serial = new LegacyFleetRenderer127(oldSpace,oldSurface);
            var actors = new List<DataMap>(); var frames = AirframeCatalog.Definitions.Select(d=>d.S("id")).ToArray();
            for (int i=0;i<5200;i++)
            {
                string frame = frames[i%frames.Length], kind=frame[0]=='M'?"missile":frame[0]=='L'?"laser":"interceptor";
                var n=new Vector3((float)(rng.NextDouble()-.5),(float)(rng.NextDouble()-.5),1).Normalized();
                var d=new DataMap{["uid"]=(long)i+1,["kind"]=kind,["airframe_id"]=frame,["normal"]=n,["altitude"]=.7d,["aim_direction"]=n.Cross(Vector3.Up).Normalized(),["aim_up"]=n,["hp"]=i%4==0?20d:100d,["max_hp"]=100d,["energy_hp"]=(double)(i%100),["energy_max_hp"]=100d,["weapon_charge"]=(i%71)/71d};
                if(i%3!=0)d["space_position"]=n*16.7f;
                actors.Add(d);
            }
            foreach(double time in new[]{0d,.36,1.8})
            {
                parallel.Sync("drones",actors,.5f,time,17);
                serial.Sync("drones",actors,.5f,time,17);
                foreach(var roots in new[]{(space,oldSpace),(surface,oldSurface)})
                {
                    var expected=roots.Item2.GetChildren().OfType<MultiMeshInstance3D>().ToDictionary(n=>n.Name.ToString());
                    Check(roots.Item1.GetChildCount()==expected.Count,"same model/material groups");
                    foreach(var node in roots.Item1.GetChildren().OfType<MultiMeshInstance3D>())
                    {
                        Check(expected.TryGetValue(node.Name.ToString(),out var reference),"same deterministic bucket ordering");
                        if(reference==null)continue;
                        Check(node.Multimesh.Buffer.SequenceEqual(reference.Multimesh.Buffer),"5200 parallel transforms and instance colors match original bit-for-bit");
                        Check(node.Multimesh.VisibleInstanceCount==reference.Multimesh.VisibleInstanceCount,"same visible aircraft including excluded spectator");
                        Check(node.Multimesh.CustomAabb==reference.Multimesh.CustomAabb,"same culling bounds");
                    }
                }
            }
            parallel.Clear();serial.Clear();planet.QueueFree();
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        catch(Exception e){Check(false,e.ToString());}
        GD.Print($"RENDER_CACHE_CHECKS: {_checks} checks / {_failures} failures");GetTree().Quit(_failures==0?0:1);
    }
}
