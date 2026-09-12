using Godot;

namespace Earthward.Rendering;

/// <summary>
/// A short, readable launch cinematic for the first research satellite.  The
/// vehicle is intentionally small enough to keep the Earth visible: three
/// PBR stages rise along a curved world-space path, separate with lateral drift,
/// and leave the payload on the same orbital ring used by the station visual.
/// </summary>
public sealed class SatelliteLaunchVehicleVisual
{
    public const float Duration = 8f;
    public const float StageOneSeparation = .34f;
    public const float StageTwoSeparation = .62f;
    public const float PayloadFairingSeparation = .72f;

    public Node3D Root { get; }
    public float Progress => Mathf.Clamp(_elapsed / Duration, 0, 1);
    public bool Finished => _elapsed >= Duration;

    private readonly Node3D _vehicle;
    private readonly Node3D _stageOne;
    private readonly Node3D _stageTwo;
    private readonly Node3D _payload;
    private readonly Node3D _fairingLeft;
    private readonly Node3D _fairingRight;
    private readonly Node3D _stageOneFlame;
    private readonly Node3D _stageTwoFlame;
    private readonly Vector3 _origin;
    private readonly Vector3 _target;
    private readonly Vector3 _side;
    private readonly Transform3D _insertionPose;
    private readonly Dictionary<Node3D, (Transform3D Pose, float Time, Vector3 Velocity)> _debris = new();
    private readonly Node3D _leftWing, _rightWing;
    private float _elapsed;

    private static readonly Dictionary<string, Mesh> Meshes = new(StringComparer.Ordinal);
    private static StandardMaterial3D? _stageOneMaterial, _stageTwoMaterial, _stageThreeMaterial, _darkMaterial, _flameMaterial;

    public SatelliteLaunchVehicleVisual(Node3D parent, Vector3 origin, Node3D payloadTemplate)
    {
        _origin = origin;
        _insertionPose = payloadTemplate.GlobalTransform;
        _target = _insertionPose.Origin;
        Vector3 outward = origin.Normalized();
        Vector3 reference = Math.Abs(outward.Dot(Vector3.Right)) > .94f ? Vector3.Up : Vector3.Right;
        _side = (reference - outward * reference.Dot(outward)).Normalized();

        _stageOneMaterial ??= new StandardMaterial3D { AlbedoColor = new("d5e1e0"), Metallic = .91f, Roughness = .22f };
        _stageTwoMaterial ??= new StandardMaterial3D { AlbedoColor = new("9eb9c2"), Metallic = .88f, Roughness = .25f };
        _stageThreeMaterial ??= new StandardMaterial3D { AlbedoColor = new("edf7f4"), Metallic = .78f, Roughness = .2f };
        _darkMaterial ??= new StandardMaterial3D { AlbedoColor = new("172d39"), Metallic = .86f, Roughness = .29f };
        _flameMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new("8feaf4"), Metallic = .08f, Roughness = .15f,
            EmissionEnabled = true, Emission = new("56dbe7"), EmissionEnergyMultiplier = 4.2f
        };

        Root = new Node3D { Name = "SatelliteLaunchSequence" };
        parent.AddChild(Root);
        _vehicle = new Node3D { Name = "LaunchVehicle", Position = origin };
        Root.AddChild(_vehicle);

        _stageOne = new Node3D { Name = "StageOne", Position = new(0, .34f, 0) };
        _vehicle.AddChild(_stageOne);
        Part(_stageOne, "StageOneTank", Cylinder("stage_one", .12f, .105f, .68f, 20), _stageOneMaterial, Vector3.Zero);
        Part(_stageOne, "StageOneInterstage", Torus("interstage_one", .106f, .014f, 24, 8), _darkMaterial, new(0, .34f, 0));
        AddFins(_stageOne, "StageOneFin", .12f, -.18f, _stageOneMaterial);
        _stageOneFlame = Flame(_stageOne, "StageOneFlame", .28f, -.47f, .075f);

        _stageTwo = new Node3D { Name = "StageTwo", Position = new(0, .86f, 0) };
        _vehicle.AddChild(_stageTwo);
        Part(_stageTwo, "StageTwoTank", Cylinder("stage_two", .092f, .075f, .47f, 20), _stageTwoMaterial, Vector3.Zero);
        Part(_stageTwo, "StageTwoInterstage", Torus("interstage_two", .077f, .011f, 24, 8), _darkMaterial, new(0, .235f, 0));
        AddFins(_stageTwo, "StageTwoFin", .09f, -.12f, _stageTwoMaterial);
        _stageTwoFlame = Flame(_stageTwo, "StageTwoFlame", .21f, -.34f, .055f);

        _payload = (Node3D)payloadTemplate.Duplicate();
        _payload.Name = "ResearchSatellitePayload";
        _payload.Transform = new Transform3D(Basis.Identity, new(0, 1.19f, 0));
        _vehicle.AddChild(_payload);
        _leftWing = _payload.GetNode<Node3D>("SolarWingLeft");
        _rightWing = _payload.GetNode<Node3D>("SolarWingRight");

        _fairingLeft = Fairing("PayloadFairingLeft", -.041f);
        _fairingRight = Fairing("PayloadFairingRight", .041f);
        _vehicle.AddChild(_fairingLeft);
        _vehicle.AddChild(_fairingRight);
        Apply(0);
    }

    public bool Update(double delta, bool paused)
    {
        if (!paused && double.IsFinite(delta) && delta > 0)
            _elapsed = Math.Min(Duration, _elapsed + (float)Math.Min(delta, .25));
        Apply(Progress);
        return Finished;
    }

    private void Apply(float progress)
    {
        float eased = Smooth(progress);
        Vector3 position = Path(eased);
        Vector3 look = (Path(Mathf.Min(1, eased + .006f)) - position).Normalized();
        if (look.LengthSquared() < .0001f)
            look = (_target - _origin).Normalized();
        float deploy = Mathf.SmoothStep(PayloadFairingSeparation, 1, progress);
        // The payload itself (not the bottom of the spent rocket) meets the
        // orbit. Preserve its exact final pose when handing over to the station.
        _vehicle.Position = position - look * (1.19f * deploy);
        _vehicle.Quaternion = new Quaternion(Vector3.Up, look);
        DropStage(_stageOne, progress, StageOneSeparation, .36f);
        DropStage(_stageTwo, progress, StageTwoSeparation, -.30f);
        DropStage(_fairingLeft, progress, PayloadFairingSeparation, -.50f);
        DropStage(_fairingRight, progress, PayloadFairingSeparation, .50f);
        _stageOneFlame.Visible = progress < StageOneSeparation;
        _stageTwoFlame.Visible = progress >= StageOneSeparation && progress < StageTwoSeparation;
        _payload.Visible = progress >= PayloadFairingSeparation;
        _payload.GlobalBasis = new Basis(_vehicle.GlobalBasis.GetRotationQuaternion().Slerp(_insertionPose.Basis.GetRotationQuaternion(), deploy));
        _payload.Scale *= Mathf.Lerp(.30f, 1, deploy);
        _leftWing.Rotation = new(0, 0, (1 - deploy) * Mathf.Pi * .47f);
        _rightWing.Rotation = new(0, 0, -(1 - deploy) * Mathf.Pi * .47f);
    }

    private void DropStage(Node3D part, float progress, float separation, float sideSpeed)
    {
        if (progress < separation) return;
        if (!_debris.TryGetValue(part, out var dropped))
        {
            part.Reparent(Root, true);
            dropped = (part.GlobalTransform, _elapsed, _origin.Normalized() * .24f + _side * sideSpeed);
            _debris.Add(part, dropped);
        }
        float seconds = _elapsed - dropped.Time;
        part.GlobalPosition = dropped.Pose.Origin + dropped.Velocity * seconds - _origin.Normalized() * (.23f * seconds * seconds);
        part.GlobalBasis = new Basis(new Quaternion(_side, seconds * .65f)) * dropped.Pose.Basis;
        part.Scale *= 1 - Mathf.SmoothStep(1.3f, 2.4f, seconds);
        part.Visible = seconds < 2.4f;
    }

    private Vector3 Path(float p)
    {
        Vector3 outward = _origin.Normalized();
        Vector3 lift = outward * 1.25f;
        Vector3 lateral = _side * MathF.Min(1.3f, _origin.DistanceTo(_target) * .12f);
        Vector3 a = _origin;
        Vector3 b = _origin + lift + lateral * .24f;
        Vector3 c = _target - outward * .72f - lateral * .18f;
        Vector3 d = _target;
        float inv = 1 - p;
        return inv * inv * inv * a + 3 * inv * inv * p * b + 3 * inv * p * p * c + p * p * p * d;
    }

    private static float Smooth(float value) => value * value * (3 - 2 * value);

    private Node3D Fairing(string name, float x)
    {
        var node = new Node3D { Name = name, Position = new(x, 1.19f, 0) };
        Part(node, name + "Shell", Box("fairing", new(.052f, .26f, .11f)), _stageThreeMaterial!, Vector3.Zero);
        return node;
    }

    private static Node3D Flame(Node3D parent, string name, float height, float y, float radius)
    {
        var node = new Node3D { Name = name, Position = new(0, y, 0) };
        parent.AddChild(node);
        Part(node, "Core", Cylinder(name + "Core", radius, .008f, height, 12), _flameMaterial!, Vector3.Zero);
        Part(node, "Halo", Cylinder(name + "Halo", radius * 1.55f, .01f, height * .72f, 12), _flameMaterial!, new(0, -.02f, 0));
        return node;
    }

    private static void AddFins(Node3D parent, string name, float width, float y, Material material)
    {
        for (int side = -1; side <= 1; side += 2)
            Part(parent, name + (side < 0 ? "Left" : "Right"), Box(name + "Mesh", new(.035f, .16f, width)), material, new(side * width * .78f, y, 0), new(0, 0, side * .18f));
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

    private static Mesh Torus(string key, float inner, float outer, int rings, int segments)
        => Meshes.TryGetValue(key, out var mesh) ? mesh : Meshes[key] = new TorusMesh { InnerRadius = inner, OuterRadius = inner + outer, Rings = rings, RingSegments = segments };
}
