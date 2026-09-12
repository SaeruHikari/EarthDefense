using Godot;

namespace Earthward.Rendering;

/// <summary>
/// Seven-cell launch complex. The central gantry is shared geometry; the seven
/// foundation plates follow the exact spherical grid and terrain at each site.
/// </summary>
public static class SatelliteLauncherModel
{
    private static StandardMaterial3D? _alloy, _dark, _fuel, _signal, _deck, _foundation, _solar;
    private static readonly Dictionary<string, Mesh> Meshes = new(StringComparer.Ordinal);

    public static Node3D Create()
    {
        _alloy ??= new StandardMaterial3D { AlbedoColor = new("b8d1d6"), Metallic = .9f, Roughness = .23f };
        _dark ??= new StandardMaterial3D { AlbedoColor = new("132936"), Metallic = .86f, Roughness = .29f };
        _fuel ??= new StandardMaterial3D { AlbedoColor = new("45636d"), Metallic = .72f, Roughness = .2f };
        _signal ??= new StandardMaterial3D
        {
            AlbedoColor = new("56dfe8"), Metallic = .18f, Roughness = .18f,
            EmissionEnabled = true, Emission = new("2acbd6"), EmissionEnergyMultiplier = 1.35f
        };
        _deck ??= new StandardMaterial3D { AlbedoColor = new("506773"), Metallic = .86f, Roughness = .32f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _foundation ??= new StandardMaterial3D { AlbedoColor = new("132936"), Metallic = .76f, Roughness = .39f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _solar ??= new StandardMaterial3D { AlbedoColor = new("224f70"), Metallic = .62f, Roughness = .27f };

        var root = new Node3D { Name = "SatelliteLauncher" };
        Part(root, "ArmorDeck", Cylinder("deck", .154f, .144f, .025f, 12), _alloy, new(0, .059f, 0));
        Part(root, "PadGlowRing", Torus("pad_ring", .134f, .008f, 32, 8), _signal, new(0, .075f, 0));
        Part(root, "PadCore", Cylinder("pad_core", .129f, .122f, .014f, 24), _dark, new(0, .075f, 0));

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * .17f;
            Part(root, side < 0 ? "LaunchTowerLeft" : "LaunchTowerRight", Box("tower", new(.038f, .78f, .055f)), _alloy, new(x, .45f, -.035f));
            Part(root, side < 0 ? "LaunchClampLeft" : "LaunchClampRight", Box("clamp", new(.08f, .028f, .048f)), _fuel, new(side * .125f, .70f, .018f));
            Part(root, side < 0 ? "GantryBackLeft" : "GantryBackRight", Box("back_tower", new(.026f, .78f, .032f)), _dark, new(x, .45f, -.16f));
            Part(root, side < 0 ? "GuidanceLightLeft" : "GuidanceLightRight", Sphere("light", .014f), _signal, new(x, .857f, -.035f));
            for (int rung = 0; rung < 5; rung++)
                Part(root, $"TowerBrace_{side}_{rung}", Box("tower_brace", new(.026f, .022f, .16f)), _fuel, new(x, .17f + rung * .14f, -.09f), new(.30f, 0, 0));
        }

        Part(root, "GantryCrossbeam", Box("crossbeam", new(.38f, .034f, .035f)), _alloy, new(0, .825f, -.16f));
        Part(root, "TelemetryMast", Cylinder("mast", .012f, .009f, .15f, 12), _alloy, new(0, .905f, -.16f));
        Part(root, "TelemetryDish", Torus("dish", .035f, .006f, 20, 8), _signal, new(0, .984f, -.16f), new(Mathf.Pi * .5f, 0, 0));
        Part(root, "LaunchBeacon", Sphere("beacon", .017f), _signal, new(0, 1.018f, -.16f));
        return root;
    }

    public static void AddHoneycombFoundation(Node3D root, SphericalGrid grid, int anchorCell,
        Transform3D globeToSite, Func<Vector3, float> surfaceRadius)
    {
        int[] cells = grid.GetClusterCells(anchorCell);
        if (cells.Length != 7)
            return;
        var topVertices = new List<Vector3>();
        var topNormals = new List<Vector3>();
        var sideVertices = new List<Vector3>();
        var sideNormals = new List<Vector3>();
        for (int plate = 0; plate < cells.Length; plate++)
        {
            int cell = cells[plate];
            Vector3 center = grid.GetCellCenter(cell);
            Vector3[] corners = grid.GetCellPolygonDirections(cell);
            Vector3[] inset = corners.Select(corner => corner.Slerp(center, .045f).Normalized()).ToArray();
            float deckRadius = inset.Append(center).Max(surfaceRadius) + .048f;
            Vector3 topCenter = globeToSite * (center * deckRadius);
            Vector3 up = (globeToSite.Basis * center).Normalized();
            for (int edge = 0; edge < inset.Length; edge++)
            {
                Vector3 a = inset[edge], b = inset[(edge + 1) % inset.Length];
                Vector3 topA = globeToSite * (a * deckRadius), topB = globeToSite * (b * deckRadius);
                Vector3 lowA = globeToSite * (a * (surfaceRadius(a) + .002f));
                Vector3 lowB = globeToSite * (b * (surfaceRadius(b) + .002f));
                Triangle(topVertices, topNormals, topCenter, topA, topB, up);
                Vector3 sideNormal = ((topA + topB) * .5f - topCenter).Normalized();
                Triangle(sideVertices, sideNormals, topA, lowA, lowB, sideNormal);
                Triangle(sideVertices, sideNormals, topA, lowB, topB, sideNormal);
            }

            if (plate == 0)
                continue;
            // Each annex is aligned to its own surface normal, so all seven
            // cells remain visibly grounded even on a curved or raised site.
            var annex = new Node3D
            {
                Name = "HoneycombAnnex_" + plate,
                Transform = globeToSite * new Transform3D(new Basis(new Quaternion(Vector3.Up, center)), center * (deckRadius + .006f))
            };
            root.AddChild(annex);
            AddAnnex(annex, plate);
        }
        Part(root, "HoneycombDecks", Surface(topVertices, topNormals), _deck!, Vector3.Zero);
        Part(root, "HoneycombFoundations", Surface(sideVertices, sideNormals), _foundation!, Vector3.Zero);
    }

    private static void AddAnnex(Node3D parent, int plate)
    {
        if (plate is 1 or 4)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Part(parent, "PropellantTank_" + side, Cylinder("annex_tank", .036f, .031f, .18f, 16), _fuel!, new(side * .045f, .095f, 0));
                Part(parent, "TankCap_" + side, Sphere("annex_tank_cap", .034f), _alloy!, new(side * .045f, .185f, 0));
                Part(parent, "TankFeed_" + side, Box("annex_tank_feed", new(.018f, .02f, .08f)), _signal!, new(side * .045f, .024f, .06f));
            }
        }
        else if (plate is 2 or 5)
        {
            Part(parent, "PowerCabinet", Box("power_cabinet", new(.14f, .065f, .13f)), _dark!, new(0, .037f, 0));
            Part(parent, "PhotovoltaicPanel", Box("annex_solar", new(.19f, .009f, .14f)), _solar!, new(0, .089f, 0), new(-.16f, 0, 0));
            for (int bar = -2; bar <= 2; bar++)
                Part(parent, "PanelDivider_" + bar, Box("solar_divider", new(.003f, .011f, .14f)), _alloy!, new(bar * .034f, .095f, 0), new(-.16f, 0, 0));
        }
        else
        {
            Part(parent, "ControlModule", Box("control_module", new(.16f, .083f, .13f)), _alloy!, new(0, .045f, 0));
            Part(parent, "ControlWindow", Box("control_window", new(.12f, .022f, .007f)), _signal!, new(0, .063f, .068f));
            Part(parent, "TelemetryBase", Cylinder("annex_mast", .009f, .006f, .12f, 10), _dark!, new(0, .14f, -.02f));
            Part(parent, "TelemetryArray", Cylinder("annex_dish", .053f, .03f, .018f, 18), _alloy!, new(0, .198f, -.02f), new(.48f, 0, 0));
        }
    }

    private static void Triangle(List<Vector3> vertices, List<Vector3> normals, Vector3 a, Vector3 b, Vector3 c, Vector3 direction)
    {
        Vector3 normal = (b - a).Cross(c - a).Normalized();
        if (normal.Dot(direction) < 0)
            normal = -normal;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        normals.Add(normal); normals.Add(normal); normals.Add(normal);
    }

    private static ArrayMesh Surface(List<Vector3> vertices, List<Vector3> normals)
    {
        var data = new Godot.Collections.Array();
        data.Resize((int)Mesh.ArrayType.Max);
        data[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        data[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, data);
        return mesh;
    }

    private static MeshInstance3D Part(Node3D parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 rotation = default)
    {
        var node = new MeshInstance3D
        {
            Name = name, Mesh = mesh, MaterialOverride = material, Position = position, Rotation = rotation,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(node);
        return node;
    }

    private static Mesh Box(string key, Vector3 size)
        => Meshes.TryGetValue(key, out var mesh) ? mesh : Meshes[key] = new BoxMesh { Size = size };

    private static Mesh Cylinder(string key, float bottom, float top, float height, int sides)
        => Meshes.TryGetValue(key, out var mesh) ? mesh : Meshes[key] = new CylinderMesh { BottomRadius = bottom, TopRadius = top, Height = height, RadialSegments = sides, Rings = 1 };

    private static Mesh Sphere(string key, float radius)
        => Meshes.TryGetValue(key, out var mesh) ? mesh : Meshes[key] = new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 16, Rings = 8 };

    private static Mesh Torus(string key, float inner, float outer, int rings, int segments)
        => Meshes.TryGetValue(key, out var mesh) ? mesh : Meshes[key] = new TorusMesh { InnerRadius = inner, OuterRadius = inner + outer, Rings = rings, RingSegments = segments };
}
