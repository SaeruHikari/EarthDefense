using Godot;
using Earthward.Application;

namespace Earthward.Tests;

/// <summary>Exercises the one-time launcher through the real construction and HUD paths.</summary>
public partial class SatelliteLauncherUiChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;

    private void Check(bool result, string label)
    {
        _checks++;
        if (result)
            return;
        _failures++;
        GD.PrintErr("SATELLITE_UI_FAIL: " + label);
    }

    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Satellite launcher UI checks require an isolated runtime-tests profile");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main();
            AddChild(_app);
            _app.PreserveCheckpoint = true;
            await Frames(6);

            Check(_app.SatelliteGuidePending && !_app.Game.HasSatelliteLauncher, "new run exposes the first-launch guide");
            Check(!_app.Planet.HasOrbitalResearchOrbit, "opening HUD and world contain no prebuilt orbit");
            Check(_app.Buttons.Any(button => button.S("action") == "build:satellite_launcher"), "build rail contains the satellite launcher action");
            Check(_app.Buttons.First(button => button.S("action").StartsWith("build:")).S("action") == "build:satellite_launcher", "launcher is the first building in the command rail");
            _app.Action("build:satellite_launcher");
            Check(_app.SelectedBuild == "satellite_launcher" && _app.Planet.PlacingBuilding, "launcher card enters one-time placement mode");
            _app.PlaceBuilding(2);
            await Frames(3);
            Check(_app.Game.HasSatelliteLauncher && _app.Game.Buildings.L("satellite_launcher") == 1, "launcher build is committed once");
            Check(_app.Planet.HasOrbitalResearchOrbit, "construction reveals the launcher-aligned orbit");
            Check(_app.SelectedBuild == "" && !_app.Planet.PlacingBuilding, "one-time launcher exits continuous build mode after placement");
            Check(_app.SelectedSatelliteLauncherSiteId == 2, "placement selects the launcher for the next guided action");
            Check(_app.SatelliteLauncherHudRect.Size.X > 250, "launcher floating panel is visible");
            Check(_app.Buttons.Any(button => button.S("action") == "satellite:launch:research"), "launcher panel exposes free research-satellite launch");
            Check(_app.Notice.Contains("发射", StringComparison.Ordinal), "guide advances from construction to launch");
            _app.Planet.RotatePlanet(.41f);
            Check(_app.SaveCheckpoint(), "seven-cell launcher and satellite state save successfully");
            Check(_app.LoadCheckpoint(), "launcher checkpoint restores through the actual application path");
            Check(_app.Planet.GetSiteWorldNormal(2).DistanceTo(_app.Planet.ResearchSatelliteOrbitPoint().Normalized()) < .0001f, "restored orbit uses the saved Earth rotation and launcher site");
            _app.PreserveCheckpoint = true;
            _app.SelectSatelliteLauncher(2);

            _app.Action("satellite:launch:research");
            await Frames(3);
            Check(_app.Game.ResearchSatelliteLaunchInProgress && _app.Planet.IsSatelliteLaunchActive, "launch button starts domain and world-space sequence");
            Check(_app.Game.Rates().N("science") == 0, "science stays offline while rocket is ascending");

            _app.SetProcess(false);
            _app.Planet.SetProcess(false);
            for (int i = 0; i < 80; i++)
                _app.Planet._Process(.1);
            await Frames(2);
            Check(_app.Game.ResearchSatelliteDeployed && !_app.Game.ResearchSatelliteLaunchInProgress, "rocket completion activates the satellite exactly once");
            Check(!_app.Planet.IsSatelliteLaunchActive && _app.Planet.HasOrbitalResearchStation, "transient launch vehicle is cleaned and satellite appears in orbit");
            Check(_app.Game.Rates().N("science") > 0, "science income starts after orbital insertion");
            Check(_app.SelectedSatelliteLauncherSiteId == -1, "completion clears the temporary launcher panel selection");
            Check(_app.SaveCheckpoint() && _app.LoadCheckpoint(), "deployed satellite saves and reloads through the same checkpoint contract");
            _app.PreserveCheckpoint = true;
            Check(_app.Game.ResearchSatelliteDeployed && !_app.Game.CanLaunchResearchSatellite(), "resume keeps science online and rejects duplicate satellite launches");
        }
        catch (Exception error)
        {
            Check(false, error.ToString());
        }
        if (IsInstanceValid(_app))
        {
            _app.QueueFree();
            await Frames(3);
        }
        GD.Print($"SATELLITE_LAUNCHER_UI_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
