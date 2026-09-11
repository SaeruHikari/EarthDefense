using Godot;
using Earthward.Domain;
namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private readonly Dictionary<string, StandardMaterial3D> _frontLights = new();
    public void SetInvasionFronts(IReadOnlyList<DataMap> fronts)
    {
        foreach (var vessel in _frontModels.Values)
            vessel.Visible = false;
        foreach (var front in fronts)
        {
            string id = front.S("id");
            var position = front.Vector3("world_position");
            if (id == "" || front.B("destroyed") || !position.IsFinite() || position.LengthSquared() < 1)
                continue;
            if (!_frontModels.TryGetValue(id, out var vessel))
            {
                vessel = new Node3D { Name = "InvasionCarrier_" + id };
                _frontRoot.AddChild(vessel);
                vessel.AddChild(RenderAssets.Model("carrier"));
                _frontModels[id] = vessel;
                var color = front.Get<Color>("color", new("ff9477"));
                var material = new StandardMaterial3D { AlbedoColor = color * .45f, Metallic = .55f, Roughness = .28f, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 3.4f };
                _frontLights[id] = material;
                int count = Math.Clamp(front.I("ordinal", 1), 1, 8);
                var markers = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = Vector3.One }, InstanceCount = 4 + count };
                SetMarker(markers, 0, new(0, .118f, -.785f), new(.25f, .014f, .025f));
                SetMarker(markers, 1, new(0, -.018f, -.785f), new(.25f, .014f, .025f));
                SetMarker(markers, 2, new(-.125f, .05f, -.785f), new(.014f, .136f, .025f));
                SetMarker(markers, 3, new(.125f, .05f, -.785f), new(.014f, .136f, .025f));
                for (int i = 0; i < count; i++)
                    SetMarker(markers, 4 + i, new((i - (count - 1) * .5f) * .048f, .485f, .30f), new(.024f, .017f, .065f));
                vessel.AddChild(new MultiMeshInstance3D { Multimesh = markers, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            }
            var outward = position.Normalized();
            var reference = Math.Abs(outward.Dot(Vector3.Up)) > .94f ? Vector3.Right : Vector3.Up;
            var right = reference.Cross(outward).Normalized();
            vessel.GlobalTransform = new(new Basis(right, outward.Cross(right).Normalized(), outward), position);
            vessel.Visible = front.B("active", true) || front.B("preview");
            _frontLights[id].EmissionEnergyMultiplier = front.B("active", true) ? 3.4f : 1.1f;
        }
    }
    private static void SetMarker(MultiMesh mesh, int index, Vector3 at, Vector3 size) => mesh.SetInstanceTransform(index, new Transform3D(Basis.FromScale(size), at));
}
