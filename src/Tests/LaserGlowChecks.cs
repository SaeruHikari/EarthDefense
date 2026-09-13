using Godot;
using Earthward.Domain;
using Earthward.Combat;
using Earthward.Rendering;
using System.Reflection;

namespace Earthward.Tests;

public partial class LaserGlowChecks : Node
{
    private int _checks, _failures;
    private void Check(bool condition, string label) { _checks++; if (!condition) { _failures++; GD.PrintErr("LASER_GLOW_FAIL: " + label); } }
    private async Task<Image> Capture(string name)
    {
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "save " + name);
        return image;
    }
    private static double Gain(Image image, Image baseline, Vector2 point)
    {
        double gain = 0;
        for (int y = -5; y <= 5; y++) for (int x = -5; x <= 5; x++)
        {
            int px = Math.Clamp(Mathf.RoundToInt(point.X) + x, 0, image.GetWidth() - 1), py = Math.Clamp(Mathf.RoundToInt(point.Y) + y, 0, image.GetHeight() - 1);
            Color a = image.GetPixel(px, py), b = baseline.GetPixel(px, py);
            gain += Math.Max(0, a.R - b.R) + Math.Max(0, a.G - b.G) + Math.Max(0, a.B - b.B);
        }
        return gain / 121;
    }
    public override async void _Ready()
    {
        PlanetView? planet = null;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Native rendering required.");
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use an isolated runtime-tests profile.");
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new(1440, 900);
            var game = new DefenseState(); planet = new PlanetView { Game = game, ViewSize = new(1440, 900) };
            AddChild(planet); planet.SetProcess(false); planet.Paused = true; planet.ZoomCamera(-15);
            var effects = (CombatEffectsView)typeof(PlanetView).GetField("_effects", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(planet)!;
            var environment = (Godot.Environment)typeof(PlanetView).GetField("_environment", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(planet)!;
            float exposure = environment.TonemapExposure;
            Vector3 a = new(-1.45f, .03f, WorldScale.EarthRadius + 1.5f), b = new(1.45f, .03f, WorldScale.EarthRadius + 1.5f);
            var friendly = new DataMap { ["uid"] = 50L, ["kind"] = "laser", ["airframe_id"] = "L2", ["space_position"] = a - Vector3.Right * .15f, ["aim_direction"] = Vector3.Right, ["aim_up"] = Vector3.Up, ["hp"] = 100d, ["max_hp"] = 100d };
            var enemy = new DataMap { ["uid"] = 51L, ["kind"] = "scout", ["enemy_role_id"] = "claw", ["space_position"] = b + Vector3.Right * .12f, ["tangent"] = Vector3.Left, ["hp"] = 10000d, ["max_hp"] = 10000d };
            planet.SyncCombatUnits(new[] { friendly }, new[] { enemy }, [], []);
            using var baseline = await Capture("laser-glow-baseline");
            var battle = new Battlefield(game) { Surface = planet };
            double healthBefore = enemy.N("hp");
            battle.FireLaser(a, enemy, 1);
            Check(battle.Beams.Count == 1 && battle.Beams[0].S("style") == "laser" && enemy.N("hp") < healthBefore, "actual laser hit publishes explicit visual style and still deals damage");
            var laser = battle.Beams[0]; laser["to_space"] = b;
            var old = laser.DeepClone(); old.Remove("style");
            effects.Sync(new[] { old }, []);
            Check(effects.WorldBeamSegmentCount == 1 && effects.LaserRibbonCount == 0, "untagged same-color beam keeps original generic geometry");
            using var oldImage = await Capture("laser-glow-old");
            effects.Sync(new[] { laser }, []);
            Check(effects.LaserRibbonCount == 1 && effects.LaserFlareCount == 2 && effects.WorldBeamSegmentCount == 0, "one true laser creates one ribbon and two endpoint flares");
            Check(effects.BatchCount == 3, "laser uses only two additional native batches");
            var ribbon = effects.GetBatchNode("laser_ribbons")!.Multimesh;
            var flares = effects.GetBatchNode("laser_flares")!.Multimesh;
            Transform3D ribbonPose = ribbon.GetInstanceTransform(0);
            Check(ribbonPose.Origin.IsEqualApprox(a) && (ribbonPose.Origin + ribbonPose.Basis.Y).IsEqualApprox(b), "laser endpoints match real shot coordinates exactly");
            Check(Math.Abs(ribbonPose.Basis.X.Length() - .026f) < .00001 && Math.Abs(ribbonPose.Basis.X.Dot(planet.Camera.GlobalBasis.Z)) < .00001, "thin halo width stays camera-facing");
            Check(flares.GetInstanceTransform(0).Origin == a && flares.GetInstanceTransform(1).Origin == b, "muzzle and impact flares occupy the actual endpoints");
            Check(flares.GetInstanceTransform(0).Basis.X.Normalized().Dot(planet.Camera.GlobalBasis.X) > .9999, "endpoint billboard faces current camera");
            long uploads = effects.BufferUploads; effects.Sync(new[] { laser }, []);
            Check(effects.BufferUploads == uploads, "stable beam state reuses existing native buffers");
            using var bright = await Capture("laser-glow-new");
            Vector2 midpoint = planet.GetSpaceScreenPosition((a + b) * .5f);
            double fullGain = Gain(bright, baseline, midpoint), muzzleGain = Gain(bright, baseline, planet.GetSpaceScreenPosition(a));
            Check(fullGain > .002 && muzzleGain > .002, "native HDR core and muzzle bloom visibly add light");
            float initialAlpha = ribbon.GetInstanceCustomData(0).A;
            laser["life"] = .0425d; effects.Sync(new[] { laser }, []);
            Check(ribbon.GetInstanceCustomData(0).A < initialAlpha * .3f && ribbon.GetInstanceTransform(0) == ribbonPose, "fade decreases light without random width or position flicker");
            using var faded = await Capture("laser-glow-fade");
            Check(Gain(faded, baseline, midpoint) < fullGain * .9, "native image contribution fades with existing beam lifetime");
            planet.OrbitCamera(.08f, .025f); effects.Sync(new[] { laser }, []);
            Check(Math.Abs(ribbon.GetInstanceTransform(0).Basis.X.Dot(planet.Camera.GlobalBasis.Z)) < .00001, "camera rotation updates ribbon facing without shifting endpoints");
            laser["life"] = 0d; effects.Sync(new[] { laser }, []);
            Check(effects.LaserRibbonCount == 0 && effects.LaserFlareCount == 0, "expired visual leaves no flare or ribbon");
            Check(environment.TonemapExposure == exposure && environment.GlowEnabled, "effect uses existing bloom without changing scene exposure");
            GD.Print($"LASER_GLOW_PIXELS core={fullGain:F6} muzzle={muzzleGain:F6} old={Gain(oldImage, baseline, midpoint):F6}");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        finally { planet?.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        GD.Print($"LASER_GLOW_CHECKS: {_checks} checks, {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
