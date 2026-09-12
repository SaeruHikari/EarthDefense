using Godot;
using Earthward.Domain;
using Earthward.Rendering;
namespace Earthward.Tests;

public partial class RenderingChecks : Node
{
    private int _checks; private readonly List<string> _failures = new();
    private void Check(bool valid, string text)
    {
        _checks++;
        if (!valid)
        {
            _failures.Add(text);
            GD.PrintErr("MANAGED_RENDER_FAIL: " + text);
        }
    }
    public override async void _Ready()
    {
        try
        {
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new(1440, 900);
            GetWindow().Position = new(-1700, 100);
            var state = new DefenseState();
            var planet = new PlanetView { Game = state, ViewSize = new(1440, 900) };
            AddChild(planet);
            planet.Paused = true;
            planet.SetProcess(false);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(planet.Grid.CellCount == WorldScale.DefaultGridCellCount, "Expanded 40962-cell topology");
            Check(WorldScale.EarthRadius == 16f && planet.SurfaceToSpace(Vector3.Back, 0).Length() == 16f, "Earth actual world radius doubled");
            Check(planet.Globe.Scale == Vector3.One, "Buildings and aircraft are not enlarged by a scaled world parent");
            Check(Math.Abs(planet.GetCameraDistance() - WorldScale.DefaultCameraDistance) < .001f, "Default framing fits the larger Earth");
            var legacyGrid = RenderAssets.ReadMap("res://assets/managed/world/grid.json").List("centers").Cast<Vector3>().ToArray();
            Check(legacyGrid.Length == 2562 && legacyGrid.Select((normal, id) => normal.DistanceTo(planet.Grid.Centers[id])).Max() < .000002f, "All legacy hex anchors remain vertices of refined grid");
            for (int site = 0; site < planet.GetSlots().Count; site++)
            {
                float radius = planet.GetSurfaceSiteTransform(site).Origin.Length();
                Check(radius >= WorldScale.EarthRadius && radius <= WorldScale.EarthRadius + WorldScale.MaxElevation + .004f, "Facility sits on expanded terrain " + site);
                Check(planet.GetSurfaceSiteTransform(site).Basis.Scale.IsEqualApprox(Vector3.One), "Facility model size preserved " + site);
            }
            Check(planet.GetSlots().Count == 16, "Initial site slots preserved");
            Check(planet.GetFactorySites().Count == 1, "Initial real factory");
            var slots = planet.GetSlots();
            Check((string)slots[0]! == "mine" && (string)slots[1]! == "solar" && (string)slots[2]! == "" && (string)slots[3]! == "interceptor", "Initial geography uses extraction, power and defense only");
            Check(!planet.HasOrbitalResearchStation && !planet.HasOrbitalResearchOrbit, "No orbit is visible before the launch center is built");
            var orbitRing = planet.SpaceRoot.FindChild("ResearchStationOrbitRing", true, false);
            var orbitHalo = planet.SpaceRoot.FindChild("ResearchStationOrbitHalo", true, false);
            Check(orbitRing is MeshInstance3D && orbitHalo is MeshInstance3D, "Research station orbit has a layered transparent ring and halo");
            planet.SyncResearchSatellite(false, false);
            Check(!planet.Grid.ConstructionOverlayVisible, "Construction hex grid is clear outside build mode");
            planet.PlacingBuilding = true;
            Check(planet.Grid.ConstructionOverlayVisible, "Construction hex grid appears while placing a building");
            planet.PlacingBuilding = false;
            Check(!planet.Grid.ConstructionOverlayVisible, "Construction hex grid clears when placement ends");
            Check(state.Wave < 10, "Free camera checks run before any former vision milestone");
            planet.ZoomCamera(100000);
            Check(Math.Abs(planet.GetCameraDistance() - WorldScale.MaxCameraDistance) < .001f, "Opening wave can zoom out to the global limit");
            planet.OrbitCamera(Mathf.Pi, 0);
            Check(planet.Camera.GlobalPosition.Z < 0, "Opening wave can rotate freely to the far hemisphere");
            planet.ResetCamera();
            Check(Math.Abs(planet.GetCameraDistance() - WorldScale.DefaultCameraDistance) < .001f, "Reset returns to free global framing");
            var camera = planet.Camera.GlobalTransform;
            var size = planet.GetRenderSize();
            planet.SetViewSize(new(1440, 900));
            Check(planet.Camera.GlobalTransform == camera && planet.GetRenderSize() == size, "Viewport layout does not move camera");
            var golden = RenderAssets.ReadMap("res://artifacts/csharp-render-golden.json");
            foreach (var row in golden.List("rays").OfType<DataMap>())
            {
                var pointValues = row.List("point");
                var point = new Vector2((float)DataMap.Number(pointValues[0]), (float)DataMap.Number(pointValues[1]));
                var normal = planet.ScreenToSurface(point);
                Check(normal.LengthSquared() > .99f && normal.DistanceTo(row.Vector3("normal")) < .006f, "Same screen position retains geographic framing");
                Check(planet.GetSurfaceScreenPosition(normal, WorldScale.MaxElevation + .02f).DistanceTo(point) < .01f, "World ray and HUD meet on the enlarged picking shell");
                int cell = planet.Grid.FindNearestCell(normal);
                Check(cell >= 0 && planet.GetSurfaceScreenPosition(planet.Grid.GetCellCenter(cell), WorldScale.MaxElevation + .02f).DistanceTo(point) < 10f, "Pointer selects a nearby refined hexagon");
            }
            var oldDirection = planet.GetSiteNormal(3);
            var before = planet.GetFactorySites()[0].Vector3("launch_position");
            planet.RotatePlanet(.3f);
            var after = planet.GetFactorySites()[0].Vector3("launch_position");
            Check(before.DistanceTo(after) > .1f, "Factory launch position follows Earth rotation");
            Check(planet.GetSiteNormal(3) == oldDirection, "Local factory site remains stable");
            planet.RestoreSites(slots, planet.GetSiteDirections());
            Check(planet.GetSlots().SequenceEqual(slots), "Facility layout restore");
            Check(planet.FocusBody("moon", true), "Moon focus");
            Check(planet.GetFocusId() == "moon", "Focus identity");
            planet.FocusSystem(true);
            Check(planet.GetFocusId() == "system" && planet.GetCameraDistance() > 100, "Full solar-system framing");
            planet.ResetCamera();
            var pose = planet.CaptureCameraState();
            planet.BeginAircraftView(4_294_967_300L);
            planet.ApplyAircraftCamera(new Transform3D(Basis.Identity, new Vector3(0, 0, WorldScale.EarthRadius + 1)));
            Check(planet.GetSpectatorDroneUid() == 4_294_967_300L, "Long aircraft UID preserved in camera");
            planet.RestoreCameraState(pose);
            Check(planet.Camera.GlobalTransform == camera, "Spectator restores exact camera transform");
            Check(planet.GetCelestialBodies().Count == 8, "Earth + 7 celestial bodies retained");
            var actors = new List<DataMap>();
            for (int i = 0; i < 120; i++)
                actors.Add(new()
                {
                    ["uid"] = (long)i + 1,
                    ["kind"] = "interceptor",
                    ["airframe_id"] = "K1",
                    ["space_position"] = new Vector3((i % 12 - 5.5f) * .18f, (i / 12 - 4.5f) * .16f, WorldScale.EarthRadius + .9f),
                    ["aim_direction"] = Vector3.Right,
                    ["aim_up"] = Vector3.Back,
                    ["hp"] = 100d,
                    ["max_hp"] = 100d
                });
            planet.SyncCombatUnits(actors, [], [], []);
            Check(planet.SpaceRoot.FindChildren("ManagedFleet_*", "MultiMeshInstance3D", true, false).Count >= 2, "Native batch receives hull and stable exhaust");
            Check(NoLegacyScripts(planet), "Managed view tree contains no GDScript instances");
            planet.ClearCombatUnits();
            planet.SetViewSize(new(1440, 900));
            planet._Process(0);
            planet.MeasureRenderTime(true);
            await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            var gpu = new List<double>();
            for (int sample = 0; sample < 45; sample++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                gpu.Add(planet.LastViewportGpuMs);
            }
            gpu.Sort();
            GD.Print($"WORLD_GPU_1440_900: median {gpu[gpu.Count / 2]:F3} ms, p95 {gpu[(int)(gpu.Count * .95)]:F3} ms");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://artifacts/managed-world.png");
            }
            GD.Print($"MANAGED_RENDER_RESULT {_checks} checks {_failures.Count} failures");
            planet.QueueFree();
            GetTree().Quit(_failures.Count == 0 ? 0 : 1);
        }
        catch (Exception e) { GD.PrintErr(e.ToString()); GetTree().Quit(1); }
    }
    private static bool NoLegacyScripts(Node node)
    {
        if (node.GetScript().VariantType == Variant.Type.Object && node.GetScript().AsGodotObject() is GDScript)
            return false;
        foreach (var child in node.GetChildren())
            if (!NoLegacyScripts(child))
                return false;
        return true;
    }
}
