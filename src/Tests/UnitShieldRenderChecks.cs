using Godot;
using Earthward.Domain;
using Earthward.Rendering;
using System.Diagnostics;

namespace Earthward.Tests;

public partial class UnitShieldRenderChecks : Node
{
    private int _checks, _failures;
    private void Check(bool value, string label) { _checks++; if (!value) { _failures++; GD.PrintErr("UNIT_SHIELD_FAIL: " + label); } }
    private async Task<Image> Capture(string name)
    {
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "save " + name);
        return image;
    }
    private static double LightGain(Image active, Image baseline, Vector2 point, int radius)
    {
        double sum = 0; int count = 0;
        for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
        {
            int px = Math.Clamp(Mathf.RoundToInt(point.X) + x, 0, active.GetWidth() - 1);
            int py = Math.Clamp(Mathf.RoundToInt(point.Y) + y, 0, active.GetHeight() - 1);
            var a = active.GetPixel(px, py); var b = baseline.GetPixel(px, py);
            sum += Math.Max(0, a.R - b.R) + Math.Max(0, a.G - b.G) + Math.Max(0, a.B - b.B); count++;
        }
        return sum / count;
    }
    private static DataMap Actor(long uid, string kind, Vector3 at, double energy, string frame = "") => new()
    {
        ["uid"] = uid, ["kind"] = kind, ["airframe_id"] = frame,
        ["space_position"] = at, ["tangent"] = Vector3.Forward,
        ["aim_direction"] = Vector3.Forward, ["aim_up"] = Vector3.Up,
        ["hp"] = 100d, ["max_hp"] = 100d,
        ["energy_hp"] = energy, ["energy_max_hp"] = 100d
    };
    public override async void _Ready()
    {
        Node3D? root = null; PlanetView? planet = null;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Native renderer is required.");
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use an isolated runtime-tests profile.");
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new(1440, 900);
            root = new Node3D(); AddChild(root);
            var space = new Node3D { Name = "ShieldTestWorld" }; var surface = new Node3D { Name = "ShieldTestSurface" };
            root.AddChild(space); root.AddChild(surface);
            var camera = new Camera3D { Position = new(0, 0, 8), Current = true, Fov = 48 }; root.AddChild(camera);
            root.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new(.005f, .008f, .018f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .8f, TonemapMode = Godot.Environment.ToneMapper.Filmic } });
            var hulls = new FleetRenderer(space, surface); var shields = new UnitShieldRenderer(space, surface); hulls.UnitShields = shields;
            var ratios = new[] { 100d, 50d, 10d, 0d };
            var drones = ratios.Select((ratio, i) => Actor(i + 1, "interceptor", new Vector3((i - 1.5f) * 1.65f, .65f, 0), ratio, "K3")).ToList();
            hulls.Sync("drones", drones, 3f, 0);
            Check(shields.InstanceCount == 3 && shields.BatchCount == 1, "1/.5/.1 create instances, zero does not");
            var row = shields.Diagnostics().Single(); var instances = row.List("instances").OfType<DataMap>().ToArray();
            Check(instances.Select(v => v.N("ratio")).SequenceEqual(new[] { 1d, .5d, (double).1f }), "GPU custom data uses actual remaining energy fraction");
            Aabb k3Bounds = hulls.GetPrototypeBounds("airframe/K3");
            Check(k3Bounds.Size.Length() > .01f && k3Bounds.Size.Z > k3Bounds.Size.Y * 2, "bounds preserve elongated K3 hull shape");
            Transform3D k3Shell = instances[0].Get<Transform3D>("transform");
            Check(k3Shell.Basis.Z.Length() > k3Shell.Basis.Y.Length() * 1.5f, "shield is a fitted ellipsoid, not uniform sphere");
            using var fullImage = await Capture("unit-shields-ratios");
            shields.Clear();
            using var baseline = await Capture("unit-shields-baseline");
            var gains = drones.Select(d => LightGain(fullImage, baseline, camera.UnprojectPosition(d.Vector3("space_position")), 58)).ToArray();
            Check(gains[0] > gains[1] * 1.1 && gains[1] > gains[2] * 1.5 && gains[2] > .0002, "native pixel contribution decreases at 1/.5/.1 integrity");
            Check(gains[3] < Math.Max(.002, gains[2] * .35), "zero integrity has no shield contribution");
            GD.Print("UNIT_SHIELD_PIXEL_GAINS " + string.Join(",", gains.Select(x => x.ToString("F6"))));

            drones[0]["energy_hp"] = 0d; hulls.Sync("drones", drones, 3f, 0);
            Check(shields.InstanceCount == 2, "breaking a shield removes the live instance");
            drones[0]["energy_hp"] = 100d; drones[0]["aim_direction"] = Vector3.Right;
            hulls.Sync("drones", drones, 1.5f, 0);
            var turned = shields.Diagnostics().Single().List("instances").OfType<DataMap>().First().Get<Transform3D>("transform");
            Check((-turned.Basis.Z.Normalized()).Dot(Vector3.Right) > .999, "shield nose rotates with actual cached ship aim");
            Check(Math.Abs(turned.Basis.Z.Length() / k3Shell.Basis.Z.Length() - .5) < .0001, "changing aircraft render scale changes shield by the same factor");

            var enemyRows = new List<DataMap>();
            for (int i = 0; i < 4; i++)
            {
                var actor = Actor(100 + i, "scout", new((i - 1.5f) * 1.4f, -1, 0), (i + 1) * 25);
                actor["enemy_role_id"] = "claw";
                Check(FleetRenderer.VisualKey(actor, true, false) == "enemy/claw", "ordinary shielded aircraft retains the unified hull");
                enemyRows.Add(actor);
            }
            enemyRows.Add(Actor(300, "carrier", new(0, 2, 0), 100));
            hulls.Sync("enemies", enemyRows, .7f, 0);
            Check(shields.InstanceCount == 8 && shields.BatchCount == 2, "four ordinary aircraft integrity levels and a moving carrier share one enemy shield batch");
            using var models = await Capture("unit-shields-model-family");
            var surfaceActor = new DataMap { ["uid"] = 700L, ["kind"] = "laser", ["airframe_id"] = "L1", ["normal"] = Vector3.Back, ["altitude"] = .7d, ["tangent"] = Vector3.Right, ["hp"] = 100d, ["energy_hp"] = 50d, ["energy_max_hp"] = 100d };
            hulls.Sync("drones", new[] { surfaceActor }, .5f, 0);
            var surfaceRow = shields.Diagnostics().Single(v => v.S("category") == "drones" && !v.B("in_space"));
            var surfacePose = surfaceRow.List("instances").OfType<DataMap>().Single().Get<Transform3D>("transform");
            var shieldNode = surface.GetNode<MultiMeshInstance3D>("UnitShields_drones_surface");
            var before = shieldNode.GlobalTransform * surfacePose;
            surface.RotateY(.5f);
            var after = shieldNode.GlobalTransform * surfacePose;
            Check(before.Origin.DistanceTo(after.Origin) > 1 && surfacePose == surfaceRow.List("instances").OfType<DataMap>().Single().Get<Transform3D>("transform"), "surface shield rotates with globe without rebuilding its local pose");

            // Optional load check uses real cached poses and uploads, without hidden actors.
            if (OS.GetCmdlineUserArgs().Contains("--shield-stress"))
            {
                var fleet = Enumerable.Range(0, 5000).Select(i => Actor(1000 + i, "interceptor", new((i % 100 - 50) * .025f, (i / 100 - 25) * .025f, 0), 50, new[] { "K1", "K2", "K3", "M1", "M2", "M3", "L1", "L2", "L3" }[i % 9])).ToArray();
                hulls.Sync("enemies", Array.Empty<DataMap>(), .5f, 0);
                for (int i = 0; i < 4; i++) hulls.Sync("drones", fleet, .5f, 0);
                var timings = new List<double>(); long allocations = GC.GetTotalAllocatedBytes(false);
                for (int i = 0; i < 30; i++) { long start = Stopwatch.GetTimestamp(); hulls.Sync("drones", fleet, .5f, 0); timings.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
                timings.Sort();
                Check(shields.InstanceCount == 5000 && shields.BatchCount <= 3, "5000 shielded aircraft retain bounded batches");
                GD.Print($"UNIT_SHIELD_5000_SYNC p50={timings[15]:F3}ms p95={timings[28]:F3}ms allocated={GC.GetTotalAllocatedBytes(false) - allocations} includes_hull_pose_and_upload=true");
            }
            hulls.Clear(); root.QueueFree(); root = null;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            planet = new PlanetView { Game = new DefenseState(), ViewSize = new(1440, 900) }; AddChild(planet); planet.SetProcess(false); planet.Paused = true;
            var front = new DataMap { ["id"] = "shield-check", ["world_position"] = new Vector3(7, 1, WorldScale.EarthRadius + 2), ["ordinal"] = 1, ["active"] = true };
            planet.SetInvasionFronts(new[] { front });
            var mother = Actor(-9000, "mothership", front.Vector3("world_position"), 50); mother["front_id"] = "shield-check";
            planet.SyncCombatUnits([], [], [], []); planet.SyncUnitShields(new[] { mother });
            Check(planet.RenderedUnitShieldCount == 1, "actual fixed mothership energy drives its shield");
            var motherRow = planet.GetUnitShieldDiagnostics().Single().List("instances").OfType<DataMap>().Single();
            Check(motherRow.N("ratio") == .5, "fixed mothership fraction is actual HP/maxHP");
            using var motherImage = await Capture("unit-shields-fixed-mothership");
            mother["energy_hp"] = 0d; planet.SyncUnitShields(new[] { mother });
            Check(planet.RenderedUnitShieldCount == 0, "fixed mothership broken shield disappears");
            planet.SyncUnitShields(Array.Empty<DataMap>());
            Check(planet.RenderedUnitShieldCount == 0, "removed fixed mothership leaves no stale shield");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        finally
        {
            root?.QueueFree(); planet?.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print($"UNIT_SHIELD_RENDER_CHECKS: {_checks} checks, {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
