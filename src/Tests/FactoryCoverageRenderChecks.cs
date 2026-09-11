using Godot;
using Earthward.Domain;
using Earthward.Combat;
using Earthward.Rendering;

namespace Earthward.Tests;

/// <summary>Native shader and world-anchor checks in an isolated render-only fixture.</summary>
public partial class FactoryCoverageRenderChecks : Node
{
    private int _checks, _failures;
    private void Check(bool result, string label)
    {
        _checks++;
        if (!result) { _failures++; GD.PrintErr("COVERAGE_RENDER_FAIL: " + label); }
    }
    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "capture " + name);
    }
    public override async void _Ready()
    {
        try
        {
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new(1440, 900);
            var state = new DefenseState();
            var planet = new PlanetView { Game = state, ViewSize = new(1440, 900) };
            AddChild(planet);
            planet.Paused = true;
            planet.RestoreSites(new object?[] { "interceptor" }, new object?[] { Vector3.Back });
            var battle = new Battlefield(state, planet);
            var camera = planet.Camera.GlobalTransform;
            var snapshot = battle.GetFactoryCoverage(0)!;
            planet.SetFactoryCoverage(snapshot);
            Check(snapshot.Preview && planet.CoverageSiteId == 0 && planet.IsFactoryCoverageVisible, "empty factory has planned range");
            var overlay = planet.Globe.GetNode<MeshInstance3D>("SelectedFactoryCoverage");
            var mesh = overlay.Mesh;
            var material = (ShaderMaterial)overlay.MaterialOverride;
            Check(Math.Abs(snapshot.Bands.Max(b => b.PatrolRadius) - 3.75) < .00001, "default CSV patrol radius is 3.75");
            Check(Math.Abs(material.GetShaderParameter("patrol_angle").AsDouble() - snapshot.Bands.Max(b => b.PatrolRadius) / WorldScale.EarthRadius) < .00001, "patrol shader uses true surface arc");
            Check(Math.Abs(snapshot.Bands.Max(b => b.AngleRadians) - 6.0 / WorldScale.EarthRadius) < .00001, "default CSV angular reach is six surface units");
            Check(Math.Abs(material.GetShaderParameter("attack_angle").AsDouble() - snapshot.Bands.Max(b => b.AngleRadians)) < .00001, "weapon range does not inflate angular sector");
            Check(planet.Camera.GlobalTransform == camera, "selection does not alter camera");
            await Capture("factory-coverage-render-global");
            var before = overlay.GlobalTransform;
            planet.RotatePlanet(.42f);
            Check(overlay.GlobalTransform != before && overlay.Transform == Transform3D.Identity, "coverage inherits rotating Earth");
            Check(ReferenceEquals(mesh, overlay.Mesh), "rotation reuses mesh");
            state.SetCombatSetting("patrol_coverage_multiplier", 3);
            var adjustedSnapshot = battle.GetFactoryCoverage(0)!;
            planet.SetFactoryCoverage(adjustedSnapshot);
            Check(ReferenceEquals(mesh, overlay.Mesh) && ReferenceEquals(material, overlay.MaterialOverride), "settings reuse mesh and shader material");
            Check(Math.Abs(adjustedSnapshot.Bands.Max(b => b.PatrolRadius) - 5.625) < .00001, "coverage multiplier three applies to the new CSV base");
            Check(Math.Abs(material.GetShaderParameter("patrol_angle").AsDouble() - adjustedSnapshot.Bands.Max(b => b.PatrolRadius) / WorldScale.EarthRadius) < .00001, "live settings reach shader");
            planet.RotatePlanet(-.42f);
            planet.ZoomCamera(-24);
            await Capture("factory-coverage-render-close");
            planet.RotatePlanet(Mathf.Pi);
            await Capture("factory-coverage-render-far-side");
            planet.SetFactoryCoverage(null);
            Check(!planet.IsFactoryCoverageVisible && planet.CoverageSiteId == -1, "deselect removes range immediately");
            planet.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        catch (Exception error) { Check(false, error.ToString()); }
        GD.Print($"FACTORY_COVERAGE_RENDER_CHECKS: {_checks} checks, {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
