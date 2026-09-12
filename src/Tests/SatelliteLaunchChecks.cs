using Godot;
using Earthward.Domain;
using Earthward.Rendering;

namespace Earthward.Tests;

public partial class SatelliteLaunchChecks : Node
{
    private int _checks, _failures;

    private void Check(bool result, string label)
    {
        _checks++;
        if (!result)
        {
            _failures++;
            GD.PrintErr("SATELLITE_LAUNCH_FAIL: " + label);
        }
    }

    public override async void _Ready()
    {
        try
        {
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new(1440, 900);
            GetWindow().Position = new(-1700, 100);
            var game = new DefenseState();
            Check(!game.ResearchSatelliteDeployed && !game.ResearchSatelliteLaunchInProgress, "new run starts without a deployed science satellite");
            Check(game.Rates().N("science") == 0, "science remains offline before launch");
            Check(game.BuildingMaxCount("satellite_launcher") == 1 && game.CanBuild("satellite_launcher"), "launcher is available and one-time in a new run");
            var opening = (game.Minerals, game.Energy, game.Science, game.ResourceCores);

            var planet = new PlanetView { Game = game, ViewSize = new(1440, 900) };
            AddChild(planet);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            planet.SetProcess(false);
            Check(!planet.HasOrbitalResearchOrbit && !planet.HasOrbitalResearchStation, "new run shows neither orbit nor satellite before construction");
            Check(planet.SpaceRoot.FindChild("ResearchStationOrbitRing", true, false) is MeshInstance3D, "orbit ring mesh is present");
            Check(planet.SpaceRoot.FindChild("ResearchStationOrbitHalo", true, false) is MeshInstance3D, "orbit halo mesh is present");

            Check(game.Build("satellite_launcher", 2), "launcher domain build succeeds");
            Check(planet.SetSlot(2, "satellite_launcher"), "launcher is placed on an empty surface hex");
            Check(planet.HasOrbitalResearchOrbit, "constructing the launch center reveals its orbit");
            Check(planet.Globe.FindChild("SatelliteLauncher", true, false) is Node3D, "launcher has a managed PBR model");
            Check(planet.GetStructureFootprint(2).Length == 7, "launcher uses a seven-cell honeycomb footprint");
            Check(!game.CanBuild("satellite_launcher"), "second launcher is rejected by the table max_count");
            Check(opening == (game.Minerals, game.Energy, game.Science, game.ResourceCores), "launcher consumes no opening resources or cores");
            var launcher = planet.Globe.GetNode<Node3D>("Site_2/SatelliteLauncher");
            Check(launcher.FindChildren("HoneycombAnnex_*", "Node3D", true, false).Count == 6, "each of the six outer hexes has a real annex model");
            var decks = launcher.GetNode<MeshInstance3D>("HoneycombDecks").Mesh.GetAabb();
            Check(decks.Size.X > .6f && decks.Size.Z > .6f, "physical decks fill the enlarged footprint");
            planet.RotatePlanet(.34f);
            planet._Process(0);
            Check(planet.ResearchSatelliteOrbitPoint().Normalized().Dot(planet.GetSiteWorldNormal(2)) > .99999f, "waiting orbit follows the rotating launcher location");
            Check(Math.Abs(planet.ResearchSatelliteOrbitPoint().Length() - 20.25f) < .001f, "orbital radius is enlarged to 20.25 world units");
            var cameraPose = planet.Camera.GlobalTransform;
            var pad = planet.GetSurfaceSiteTransform(2);
            planet.Camera.GlobalPosition = pad.Origin + pad.Basis * new Vector3(1.7f, 1.9f, 2.2f);
            planet.Camera.LookAt(pad.Origin + pad.Basis.Y * .42f, pad.Basis.Y);
            await Capture("satellite-launch-center.png");
            planet.Camera.GlobalTransform = cameraPose;

            bool completed = false;
            planet.ResearchSatelliteLaunchCompleted += () =>
            {
                completed = game.CompleteResearchSatelliteLaunch();
            };
            Check(game.BeginResearchSatelliteLaunch(), "domain launch state begins");
            Check(planet.StartResearchSatelliteLaunch(2), "world-space launch sequence begins from launcher pad");
            Check(planet.IsSatelliteLaunchActive && planet.SatelliteLaunchProgress == 0, "launch starts at zero progress");
            planet.Paused = true;
            planet._Process(.25);
            Check(planet.SatelliteLaunchProgress == 0, "pause freezes launch progress");
            planet.Paused = false;
            void Advance(double seconds)
            {
                int steps = (int)Math.Ceiling(seconds / .1);
                for (int i = 0; i < steps; i++)
                    planet._Process(Math.Min(.1, seconds - i * .1));
            }
            Advance(2.0);
            planet.Camera.GlobalPosition = pad.Origin + pad.Basis * new Vector3(2.4f, 2.9f, 3.4f);
            planet.Camera.LookAt(pad.Origin + pad.Basis.Y * 1.5f, pad.Basis.Y);
            await Capture("satellite-launch-ascent.png");
            planet.Camera.GlobalTransform = cameraPose;
            Advance(.9);
            Check(planet.SatelliteLaunchProgress > .3f && planet.SpaceRoot.FindChild("StageOne", true, false) is Node3D, "first stage rises before separation");
            Advance(2.4);
            Check(planet.SatelliteLaunchProgress > .6f, "second stage reaches orbital transfer arc");
            Advance(1.5);
            Check(planet.SatelliteLaunchProgress > .8f, "payload fairing separates near orbit");
            await Capture("satellite-launch.png");
            Advance(1.19);
            var payload = planet.SpaceRoot.FindChild("ResearchSatellitePayload", true, false) as Node3D;
            Check(payload != null && payload.GlobalPosition.DistanceTo(planet.ResearchSatelliteOrbitPoint()) < .035f, "payload reaches the orbit before switching to the deployed station");
            Advance(2.0);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(completed && game.ResearchSatelliteDeployed && !game.ResearchSatelliteLaunchInProgress, "payload insertion completes the science satellite state");
            Check(planet.HasOrbitalResearchStation && planet.IsResearchSatelliteDeployed, "deployed satellite is visible on the orbital ring");
            Check(!planet.IsSatelliteLaunchActive, "launch sequence cleans its transient rocket nodes");
            Check(game.Rates().N("science") > 0, "science income activates after insertion");
            Check(planet.Globe.GetNode<Node3D>("Site_2/SatelliteLauncher") != null, "launcher remains on the surface after launch");
            planet.ResetPlanet();
            Check(!planet.HasOrbitalResearchOrbit && !planet.HasOrbitalResearchStation, "new campaign clears the orbit and satellite");
            planet.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        catch (Exception error)
        {
            Check(false, error.ToString());
        }
        GD.Print($"SATELLITE_LAUNCH_CHECKS: {_checks} checks, {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private async Task Capture(string file)
    {
        if (DisplayServer.GetName() == "headless") return;
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng("res://artifacts/" + file);
    }
}
