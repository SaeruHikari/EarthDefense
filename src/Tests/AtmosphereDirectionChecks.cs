using Godot;
using Earthward.Domain;
using Earthward.Rendering;
using System.Reflection;

namespace Earthward.Tests;

/// <summary>Actual native shader, impact coordinates and image contrast checks.</summary>
public partial class AtmosphereDirectionChecks : Node
{
    private int _checks, _failures;
    private static readonly MethodInfo UpdateHit = typeof(PlanetView).GetMethod("UpdateAtmosphereHit", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private void Check(bool condition, string text)
    {
        _checks++;
        if (!condition) { _failures++; GD.PrintErr("ATMOSPHERE_DIRECTION_FAIL: " + text); }
    }
    private async Task<Image> Capture(string name)
    {
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "save " + name);
        return image;
    }
    private static Color Average(Image image, Vector2 at)
    {
        Color result = new(0, 0, 0, 0); int count = 0;
        for (int y = -4; y <= 4; y++) for (int x = -4; x <= 4; x++)
        {
            int ix = Math.Clamp(Mathf.RoundToInt(at.X) + x, 0, image.GetWidth() - 1);
            int iy = Math.Clamp(Mathf.RoundToInt(at.Y) + y, 0, image.GetHeight() - 1);
            result += image.GetPixel(ix, iy); count++;
        }
        return result / count;
    }
    private static void AdvancePulse(PlanetView planet)
    {
        planet.Paused = false;
        for (int i = 0; i < 3; i++) UpdateHit.Invoke(planet, new object[] { .04d });
        planet.Paused = true;
    }
    public override async void _Ready()
    {
        PlanetView? planet = null;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Native rendering is required.");
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use an isolated runtime-tests profile.");
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new(1440, 900);
            planet = new PlanetView { Game = new DefenseState(), ViewSize = new(1440, 900) };
            AddChild(planet); planet.SetProcess(false); planet.Paused = true;
            var material = (ShaderMaterial)typeof(PlanetView).GetField("_atmosphereMaterial", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(planet)!;
            var shell = (MeshInstance3D)typeof(PlanetView).GetField("_atmosphere", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(planet)!;
            Vector3 towardCamera = planet.Camera.GlobalPosition.Normalized();
            Vector3 right = planet.Camera.GlobalBasis.X.Normalized(), up = planet.Camera.GlobalBasis.Y.Normalized();
            Vector3 impactDirection = (towardCamera * .93f - right * .36f + up * .1f).Normalized();
            Vector3 distantDirection = (towardCamera * .34f + right * .94f).Normalized();
            Vector3 worldImpact = impactDirection * WorldScale.EarthRadius;
            Vector2 impactPixel = planet.GetSpaceScreenPosition(worldImpact);
            Vector2 distantPixel = planet.GetSpaceScreenPosition(distantDirection * WorldScale.EarthRadius);

            using var idle = await Capture("atmosphere-direction-idle");
            planet.PulseAtmosphereHit(16);
            double legacyDrive = planet.GetAtmosphereHitState().N("drive");
            AdvancePulse(planet);
            double legacyValue = planet.GetAtmosphereHitState().N("value");
            Check(material.GetShaderParameter("hit_direction_local").AsVector3() == Vector3.Zero, "directionless call retains global pulse");
            using var global = await Capture("atmosphere-direction-global");

            planet.ClearAtmosphereHit();
            planet.PulseAtmosphereHit(16, worldImpact);
            Check(Math.Abs(planet.GetAtmosphereHitState().N("drive") - legacyDrive) < .000001, "same damage produces exactly the old drive");
            AdvancePulse(planet);
            Check(Math.Abs(planet.GetAtmosphereHitState().N("value") - legacyValue) < .000001, "same damage and lerp retain old pulse envelope");
            Vector3 local = planet.GetAtmosphereHitState().Vector3("direction_local");
            Check(local.DistanceTo(planet.Globe.ToLocal(worldImpact).Normalized()) < .00001f, "impact is stored as geographic local direction");
            Vector3 shaderLocal = material.GetShaderParameter("hit_direction_local").AsVector3();
            Check((shell.GlobalBasis * shaderLocal).Normalized().DistanceTo(impactDirection) < .00001f, "shader model basis reconstructs actual world impact");
            using var directed = await Capture("atmosphere-direction-hit");
            Color centerDelta = Average(directed, impactPixel) - Average(global, impactPixel);
            Color farDelta = Average(directed, distantPixel) - Average(global, distantPixel);
            Check(centerDelta.R > .002f, "actual impact becomes visibly brighter than global pulse");
            Check(centerDelta.R > farDelta.R + .001f, "directional gain falls towards the distant hemisphere");
            Check(centerDelta.R > centerDelta.G && centerDelta.R > centerDelta.B, "impact crest remains red instead of white");
            Check(Math.Abs(farDelta.R) < .025f, "distant existing breathing glow stays visually consistent");

            planet.RotatePlanet(.45f);
            Vector3 rotated = (planet.Globe.GlobalBasis * local).Normalized();
            Check(planet.GetAtmosphereHitState().Vector3("direction_local") == local, "geographic anchor does not drift during Earth rotation");
            Check((shell.GlobalBasis * material.GetShaderParameter("hit_direction_local").AsVector3()).Normalized().DistanceTo(rotated) < .00001f, "shader highlight follows rotating Earth automatically");
            Check(rotated.DistanceTo(impactDirection) > .1f, "highlight is not frozen in screen or inertial coordinates");
            using var moved = await Capture("atmosphere-direction-rotated");

            planet.PulseAtmosphereHit(1);
            Check(material.GetShaderParameter("hit_direction_local").AsVector3() == Vector3.Zero, "later legacy call clears stale directional target");
            planet.PulseAtmosphereHit(1, new Vector3(float.NaN, 0, 0));
            Check(planet.GetAtmosphereHitState().Vector3("direction_local") == Vector3.Zero, "invalid position safely retains global path");
            double beforeInvalid = planet.GetAtmosphereHitState().N("drive");
            planet.PulseAtmosphereHit(double.NaN, worldImpact);
            Check(planet.GetAtmosphereHitState().N("drive") == beforeInvalid, "invalid damage is ignored");
            planet.ClearAtmosphereHit();
            Check(planet.GetAtmosphereHitState().N("value") == 0 && planet.GetAtmosphereHitState().N("drive") == 0, "clear removes pulse envelope");
            Check(material.GetShaderParameter("hit_direction_local").AsVector3() == Vector3.Zero && material.GetShaderParameter("hit_pulse").AsDouble() == 0, "clear resets native shader state");
            Check(material.Shader.Code.Contains("sample_index < 12") && !material.Shader.Code.Contains("sample_index < 13"), "atmosphere ray sample count is unchanged");
            GD.Print($"ATMOSPHERE_DIRECTION_PIXELS center={centerDelta} distant={farDelta}");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        finally
        {
            if (planet != null) { planet.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        }
        GD.Print($"ATMOSPHERE_DIRECTION_CHECKS: {_checks} checks, {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
