using Godot;

namespace Earthward.Rendering;

/// <summary>Shared PBR projector components, sized for one surface hex.</summary>
public static class ShieldProjectorModel
{
    private static StandardMaterial3D? _alloy, _dark, _core;
    private static readonly Dictionary<string, Mesh> Meshes = new();
    public static Node3D Create()
    {
        _alloy ??= new() { AlbedoColor = new("b5c7cc"), Metallic = .88f, Roughness = .27f };
        _dark ??= new() { AlbedoColor = new("172f3c"), Metallic = .82f, Roughness = .34f };
        _core ??= new() { AlbedoColor = new("75f3df"), Metallic = .25f, Roughness = .22f, EmissionEnabled = true, Emission = new("55d9cf"), EmissionEnergyMultiplier = 1.2f };
        var root = new Node3D { Name = "ShieldProjector" };
        Part(root, "Foundation", Cylinder("base", .146f, .134f, .036f, 6), _dark, new(0, .021f, 0));
        Part(root, "ArmorDeck", Cylinder("deck", .123f, .111f, .024f, 6), _alloy, new(0, .048f, 0));
        Part(root, "ContainmentColumn", Cylinder("column", .053f, .042f, .195f, 24), _dark, new(0, .15f, 0));
        Part(root, "FluxChamber", Cylinder("chamber", .028f, .028f, .15f, 24), _core, new(0, .198f, 0));
        Part(root, "EmitterCollar", Cylinder("collar", .077f, .068f, .025f, 24), _alloy, new(0, .259f, 0));
        for (int i = 0; i < 3; i++)
        {
            float angle = i * Mathf.Tau / 3;
            var vane = Part(root, "Conductor_" + i, Box("vane", new(.025f, .207f, .05f)), _alloy, new(Mathf.Sin(angle) * .084f, .155f, Mathf.Cos(angle) * .084f));
            vane.Rotation = new(0, angle, 0);
            var foot = Part(root, "PowerChannel_" + i, Box("channel", new(.012f, .008f, .073f)), _core, new(Mathf.Sin(angle) * .087f, .064f, Mathf.Cos(angle) * .087f));
            foot.Rotation = new(0, angle, 0);
        }
        if (!Meshes.TryGetValue("ring", out var ring))
            Meshes["ring"] = ring = new TorusMesh { InnerRadius = .068f, OuterRadius = .083f, Rings = 32, RingSegments = 12 };
        Part(root, "ShieldEmitterRing", ring, _alloy, new(0, .306f, 0));
        if (!Meshes.TryGetValue("lens", out var lens))
            Meshes["lens"] = lens = new SphereMesh { Radius = .031f, Height = .062f, RadialSegments = 20, Rings = 12 };
        Part(root, "ProjectionLens", lens, _core, new(0, .316f, 0));
        return root;
    }
    private static Mesh Cylinder(string key, float bottom, float top, float height, int sides)
    {
        if (!Meshes.TryGetValue(key, out var mesh))
            Meshes[key] = mesh = new CylinderMesh { BottomRadius = bottom, TopRadius = top, Height = height, RadialSegments = sides, Rings = 1 };
        return mesh;
    }
    private static Mesh Box(string key, Vector3 size)
    {
        if (!Meshes.TryGetValue(key, out var mesh)) Meshes[key] = mesh = new BoxMesh { Size = size };
        return mesh;
    }
    private static MeshInstance3D Part(Node3D parent, string name, Mesh mesh, Material material, Vector3 position)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material, Position = position };
        parent.AddChild(node);
        return node;
    }
}
