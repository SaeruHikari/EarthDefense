using Godot;
using Earthward.Domain;
using Earthward.Rendering;

namespace Earthward.Tests;

public partial class LocalShieldRenderChecks : Node
{
    private int _checks, _failures;
    private void Check(bool result, string label) { _checks++; if (!result) { _failures++; GD.PrintErr("SHIELD_RENDER_FAIL: " + label); } }
    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, name);
    }
    public override async void _Ready()
    {
        try
        {
            CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
            GetWindow().Size = new(1440, 900);
            var planet = new PlanetView { Game = new DefenseState(), ViewSize = new(1440, 900) };
            AddChild(planet); planet.Paused = true;
            Vector3 normal = new Vector3(.25f, .18f, 1).Normalized();
            planet.RestoreSites(new object?[] { "shield", "shield" }, new object?[] { normal, -normal });
            var sites = planet.GetShieldSites();
            var states = sites.Select(site => new DataMap
            {
                ["site_id"] = site.L("site_id"), ["normal"] = site.Vector3("normal"),
                ["hp"] = 100d, ["max_hp"] = 100d, ["surface_radius"] = 5d,
                ["shield_radius"] = WorldScale.EarthRadius + WorldScale.ShieldAltitude,
                ["angle_radians"] = 5d / WorldScale.EarthRadius, ["hit"] = 0d
            }).ToArray();
            planet.SyncLocalShields(states);
            Check(sites.Count == 2 && planet.GetFactorySites().Count == 0, "shield towers are not aircraft factories");
            Check(WorldScale.ShieldAltitude > WorldScale.AtmosphereRadius - WorldScale.EarthRadius && WorldScale.AtmosphereRadius - WorldScale.EarthRadius > EarthVisual.CirrusAltitude && EarthVisual.CirrusAltitude > EarthVisual.CloudBaseAltitude + EarthVisual.CloudHeightRange, "separated ground/cloud/cirrus/atmosphere/shield heights");
            var front = planet.Globe.GetNode<MeshInstance3D>("LocalShield_0");
            var back = planet.Globe.GetNode<MeshInstance3D>("LocalShield_1");
            Check(ReferenceEquals(front.Mesh, back.Mesh), "matching towers share cap geometry");
            Check(front.Mesh.GetFaces().Length < 5000, "each shield draws a local cap instead of a full planet sphere");
            Check(front.GetScript().VariantType == Variant.Type.Nil, "shield presentation has no script bridge");
            var structure = planet.Globe.GetNode<Node3D>("Site_0/ShieldProjector");
            var lens = structure.GetNode<MeshInstance3D>("ProjectionLens");
            Check(lens.MaterialOverride is StandardMaterial3D pbr && pbr.Metallic > .2f, "new projector uses PBR lens and metal geometry");

            await Capture("local-shield-global");
            var before = front.GlobalTransform; planet.RotatePlanet(.36f);
            Check(front.GlobalTransform != before, "shield cap follows its rotating surface tower");
            planet.ZoomCamera(-6);
            await Capture("local-shield-close");
            states[0]["hit"] = .22d;
            planet.SyncLocalShields(states);
            Check(((ShaderMaterial)front.MaterialOverride).GetShaderParameter("impact").AsDouble() > .99, "shield hit drives visual flash");
            await Capture("local-shield-hit");
            states[0]["hp"] = 0d; states[0]["hit"] = 0d; planet.SyncLocalShields(states);
            Check(((ShaderMaterial)front.MaterialOverride).GetShaderParameter("integrity").AsDouble() == 0, "broken shield becomes inactive outline");
            planet.SyncLocalShields(Array.Empty<DataMap>());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!planet.Globe.HasNode("LocalShield_0"), "removed tower removes its shield visual");
            planet.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        catch (Exception error) { Check(false, error.ToString()); }
        GD.Print($"LOCAL_SHIELD_RENDER_CHECKS: {_checks} checks, {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
