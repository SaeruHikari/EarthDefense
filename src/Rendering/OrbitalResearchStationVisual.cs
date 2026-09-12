using Godot;

namespace Earthward.Rendering;

/// <summary>
/// A single permanent research station in low Earth orbit.  It is intentionally
/// procedural: the project has no compatible satellite asset, and these shared
/// PBR meshes keep the station cheap while still giving it a readable silhouette
/// (bus, solar wings, radiator spine and a glowing science antenna).
/// </summary>
public sealed class OrbitalResearchStationVisual
{
    // Keep a clear, readable gap above the atmosphere and shield ring.  The
    // launcher path targets this same radius, so the payload always inserts
    // into the exact orbital plane selected by the launch site.
    public const float OrbitRadius = WorldScale.EarthRadius + 4.25f;
    public const float OrbitRate = .035f;
    public const float OrbitRingThickness = .045f;
    // Hidden initialization basis. Construction replaces it with the site's
    // surface normal before revealing the orbit.
    public static readonly Vector3 DefaultOrbitAnchor = new Vector3(0, .82f, -.57f).Normalized();

    public Node3D Root { get; }
    public Node3D Model { get; }
    public float OrbitPhase { get; private set; }
    public bool IsDeployed { get; private set; }
    public Vector3 OrbitAnchor { get; private set; } = DefaultOrbitAnchor;

    private readonly Node3D _orbit;
    private readonly Node3D _leftWing;
    private readonly Node3D _rightWing;
    private readonly Node3D _antenna;
    private readonly ShaderMaterial _orbitRingMaterial;
    private readonly ShaderMaterial _orbitHaloMaterial;
    private readonly float _phaseOffset;
    private Basis _orbitBasis = Basis.Identity;

    private static readonly Dictionary<string, Mesh> MeshCache = new(StringComparer.Ordinal);
    private static StandardMaterial3D? _alloy;
    private static StandardMaterial3D? _dark;
    private static StandardMaterial3D? _solar;
    private static StandardMaterial3D? _science;
    private static Shader? _orbitRingShader;

    private const string OrbitRingShaderCode = """
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec4 ring_tint : source_color = vec4(0.25, 0.9, 0.95, 0.42);
uniform float ring_alpha = 0.42;
uniform float pulse_phase = 0.0;
varying float ring_angle;

void vertex() {
    ring_angle = atan(VERTEX.z, VERTEX.x);
}

void fragment() {
    float sweep = 0.5 + 0.5 * cos(ring_angle - pulse_phase);
    float glint = pow(max(sweep, 0.0), 7.0);
    float grain = 0.95 + 0.05 * sin(ring_angle * 9.0 + pulse_phase * 1.7);
    // A continuous bright floor keeps the whole visible arc readable; the
    // moving highlight adds detail without making the rest disappear.
    float alpha = ring_alpha * (0.68 + 0.20 * sweep + 0.12 * glint) * grain;
    ALBEDO = ring_tint.rgb;
    EMISSION = ring_tint.rgb * (0.85 + 0.60 * sweep + 1.15 * glint);
    ALPHA = alpha;
}
""";

    public OrbitalResearchStationVisual(Node3D parent)
    {
        _alloy ??= new StandardMaterial3D
        {
            AlbedoColor = new("b8d4d9"), Metallic = .88f, Roughness = .24f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon
        };
        _dark ??= new StandardMaterial3D { AlbedoColor = new("182c38"), Metallic = .9f, Roughness = .31f };
        _solar ??= new StandardMaterial3D
        {
            AlbedoColor = new("214f72"), Metallic = .48f, Roughness = .28f,
            EmissionEnabled = true, Emission = new("0c3655"), EmissionEnergyMultiplier = .18f
        };
        _science ??= new StandardMaterial3D
        {
            AlbedoColor = new("74f4e2"), Metallic = .25f, Roughness = .18f,
            EmissionEnabled = true, Emission = new("42e7dd"), EmissionEnergyMultiplier = 1.65f
        };
        _orbitRingShader ??= new Shader { Code = OrbitRingShaderCode };

        _orbitRingMaterial = new ShaderMaterial { Shader = _orbitRingShader };
        _orbitRingMaterial.SetShaderParameter("ring_tint", new Color("65e9e9", .74f));
        _orbitRingMaterial.SetShaderParameter("ring_alpha", .72f);
        _orbitHaloMaterial = new ShaderMaterial { Shader = _orbitRingShader };
        _orbitHaloMaterial.SetShaderParameter("ring_tint", new Color("3ca8df", .52f));
        _orbitHaloMaterial.SetShaderParameter("ring_alpha", .22f);

        Root = new Node3D { Name = "OrbitalResearchStationOrbit" };
        parent.AddChild(Root);
        _orbit = new Node3D { Name = "ResearchStationOrbitPivot" };
        Root.AddChild(_orbit);
        SetOrbitAnchor(DefaultOrbitAnchor);
        Part(_orbit, "ResearchStationOrbitHalo", Torus("research_orbit_halo", OrbitRadius - .04f, .13f, 192, 8), _orbitHaloMaterial, Vector3.Zero);
        Part(_orbit, "ResearchStationOrbitRing", Torus("research_orbit_ring", OrbitRadius, OrbitRingThickness, 128, 8), _orbitRingMaterial, Vector3.Zero);
        Model = new Node3D { Name = "OrbitalResearchStation", Position = new(0, 0, OrbitRadius) };
        _orbit.AddChild(Model);
        _phaseOffset = Mathf.PosMod(Root.GlobalPosition.Dot(new(3.71f, 8.23f, 5.17f)), Mathf.Tau);

        Part(Model, "CoreBus", Box("bus", new(.52f, .18f, .34f)), _dark, Vector3.Zero);
        Part(Model, "PressureModule", Cylinder("pressure", .14f, .12f, .30f, 20), _alloy, new(0, .02f, .02f), new(Mathf.Pi * .5f, 0, 0));
        Part(Model, "RadiatorSpine", Box("spine", new(.08f, .08f, .72f)), _alloy, new(0, .08f, 0));

        _leftWing = Part(Model, "SolarWingLeft", Box("solar_wing", new(.62f, .018f, .27f)), _solar, new(-.62f, 0, 0));
        _rightWing = Part(Model, "SolarWingRight", Box("solar_wing", new(.62f, .018f, .27f)), _solar, new(.62f, 0, 0));
        for (int side = -1; side <= 1; side += 2)
        {
            var boom = Part(Model, "WingBoom" + (side < 0 ? "Left" : "Right"), Box("boom", new(.08f, .035f, .05f)), _alloy, new(side * .34f, .01f, 0));
            boom.Rotation = new(0, 0, side * .035f);
        }

        Part(Model, "ForwardSensor", Sphere("sensor", .08f), _science, new(0, .02f, .24f));
        _antenna = new Node3D { Name = "ScienceAntenna", Position = new(0, .17f, -.04f) };
        Model.AddChild(_antenna);
        Part(_antenna, "AntennaMast", Cylinder("mast", .018f, .014f, .34f, 12), _alloy, Vector3.Zero);
        Part(_antenna, "AntennaDish", Torus("dish", .085f, .012f), _science, new(0, .16f, 0), new(Mathf.Pi * .5f, 0, 0));
        Part(Model, "NavigationBeacon", Sphere("beacon", .035f), _science, new(0, -.12f, -.19f));
        SetDeployed(false);
        SetOrbitAvailable(false);
    }

    public static Vector3 OrbitPoint(float phase)
        => OrbitPoint(DefaultOrbitAnchor, phase);

    public static Vector3 OrbitPoint(Vector3 anchor, float phase)
    {
        Basis basis = BuildOrbitBasis(anchor);
        return basis * new Basis(new Quaternion(Vector3.Up, phase)) * new Vector3(0, 0, OrbitRadius);
    }

    public Vector3 GetOrbitPoint(float phase)
        => _orbitBasis * new Basis(new Quaternion(Vector3.Up, phase)) * new Vector3(0, 0, OrbitRadius);

    public void SetOrbitAnchor(Vector3 normal)
    {
        if (!normal.IsFinite() || normal.LengthSquared() < .0001f)
            normal = DefaultOrbitAnchor;
        OrbitAnchor = normal.Normalized();
        _orbitBasis = BuildOrbitBasis(OrbitAnchor);
        ApplyOrbitTransform();
    }

    public void ResetOrbitAnchor()
    {
        SetOrbitAnchor(DefaultOrbitAnchor);
        SetOrbitAvailable(false);
    }

    public void SetOrbitAvailable(bool available) => Root.Visible = available;

    public void SetOrbitPhase(float phase)
    {
        OrbitPhase = Mathf.PosMod(phase, Mathf.Tau);
        ApplyOrbitTransform();
        Model.Position = new(0, 0, OrbitRadius);
    }

    public void SetDeployed(bool deployed)
    {
        IsDeployed = deployed;
        Model.Visible = deployed;
    }

    private void ApplyOrbitTransform()
    {
        _orbit.Basis = _orbitBasis * new Basis(new Quaternion(Vector3.Up, OrbitPhase));
        _orbit.Position = Vector3.Zero;
    }

    private static Basis BuildOrbitBasis(Vector3 anchor)
    {
        Vector3 z = anchor.Normalized();
        // Use a world-right tangent as the stable in-plane reference.  Using
        // world-up here makes the derived orbit plane contain the default
        // camera's view axis, so the transparent torus collapses into a
        // distracting vertical line.  Falling back near ±X keeps the basis
        // numerically stable for a launcher placed on that meridian.
        Vector3 reference = Math.Abs(z.Dot(Vector3.Right)) > .94f ? Vector3.Up : Vector3.Right;
        Vector3 y = z.Cross(reference).Normalized();
        if (y.LengthSquared() < .0001f)
            y = Vector3.Right;
        Vector3 x = y.Cross(z).Normalized();
        return new Basis(x, y, z);
    }

    public void Update(double delta, bool paused)
    {
        if (!paused && double.IsFinite(delta) && delta > 0)
            OrbitPhase = Mathf.PosMod(OrbitPhase + (float)Math.Min(delta, .25) * OrbitRate, Mathf.Tau);
        ApplyOrbitTransform();
        float t = OrbitPhase + _phaseOffset;
        _orbitRingMaterial.SetShaderParameter("pulse_phase", t * .75f);
        _orbitHaloMaterial.SetShaderParameter("pulse_phase", -t * .42f);
        _leftWing.Rotation = new(0, .08f * Mathf.Sin(t * .37f), .04f * Mathf.Sin(t * .21f));
        _rightWing.Rotation = new(0, .08f * Mathf.Sin(t * .37f), -.04f * Mathf.Sin(t * .21f));
        _antenna.Rotation = new(.04f * Mathf.Sin(t * .31f), Mathf.Sin(t * .17f) * .12f, 0);
    }

    private static MeshInstance3D Part(Node3D parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 rotation = default)
    {
        var part = new MeshInstance3D
        {
            Name = name, Mesh = mesh, MaterialOverride = material, Position = position, Rotation = rotation,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(part);
        return part;
    }

    private static Mesh Box(string key, Vector3 size)
        => MeshCache.TryGetValue(key, out var mesh) ? mesh : MeshCache[key] = new BoxMesh { Size = size };

    private static Mesh Cylinder(string key, float bottom, float top, float height, int sides)
        => MeshCache.TryGetValue(key, out var mesh) ? mesh : MeshCache[key] = new CylinderMesh { BottomRadius = bottom, TopRadius = top, Height = height, RadialSegments = sides, Rings = 1 };

    private static Mesh Sphere(string key, float radius)
        => MeshCache.TryGetValue(key, out var mesh) ? mesh : MeshCache[key] = new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 16, Rings = 8 };

    private static Mesh Torus(string key, float inner, float outer, int rings = 28, int segments = 10)
        => MeshCache.TryGetValue(key, out var mesh) ? mesh : MeshCache[key] = new TorusMesh { InnerRadius = inner, OuterRadius = inner + outer, Rings = rings, RingSegments = segments };
}
