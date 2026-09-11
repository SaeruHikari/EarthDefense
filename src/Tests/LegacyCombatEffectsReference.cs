using Godot;
using Earthward.Domain;
namespace Earthward.Tests;

internal sealed class LegacyCombatEffectsReference
{
    private sealed class Burst
    {
        public required Node3D Root; public required MeshInstance3D Core, Shell; public required MultiMeshInstance3D Sparks; public readonly Vector3[] Directions = new Vector3[8]; public readonly Basis[] Bases = new Basis[8];
    }
    private readonly Node3D _space, _surface;
    private readonly Camera3D _camera;
    private readonly ImmediateMesh _spaceBeams = new(), _surfaceBeams = new();
    private readonly ShaderMaterial _beamMaterial = new() { Shader = new Shader { Code = "shader_type spatial;render_mode blend_add,cull_disabled,depth_draw_never,ambient_light_disabled,specular_disabled;void fragment(){ALBEDO=vec3(0.0);EMISSION=COLOR.rgb*5.5;ALPHA=COLOR.a;}" } };
    private readonly SphereMesh _coreMesh = new() { Radius = 1, Height = 2, RadialSegments = 16, Rings = 8 }, _shellMesh = new() { Radius = 1, Height = 2, RadialSegments = 32, Rings = 16 };
    private readonly BoxMesh _sparkMesh = new() { Size = new(.012f, .012f, .05f) };
    private readonly ShaderMaterial _coreMaterial = new() { Shader = GD.Load<Shader>("res://shaders/combat_energy.gdshader") }, _shellMaterial = new() { Shader = GD.Load<Shader>("res://shaders/combat_energy.gdshader") };
    private readonly Dictionary<long, Burst> _bursts = new(); private readonly Stack<Burst> _spares = new();
    public LegacyCombatEffectsReference(Node3D space, Node3D surface, Camera3D camera)
    {
        _space = space;
        _surface = surface;
        _camera = camera;
        space.AddChild(new MeshInstance3D { Mesh = _spaceBeams, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        surface.AddChild(new MeshInstance3D { Mesh = _surfaceBeams, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _coreMaterial.SetShaderParameter("energy", 10);
        _shellMaterial.SetShaderParameter("energy", 5.5);
        _shellMaterial.SetShaderParameter("shell", 1);
    }
    public void Sync(IReadOnlyList<DataMap> beams, IReadOnlyList<DataMap> bursts)
    {
        _spaceBeams.ClearSurfaces();
        _surfaceBeams.ClearSurfaces();
        bool spaceStarted = false, surfaceStarted = false;
        foreach (var beam in beams)
        {
            var color = beam.Get<Color>("color", new("a1ffe5"));
            color.A *= Mathf.Clamp((float)beam.N("life", .2) * 7, .12f, 1);
            if (beam.ContainsKey("from_space") && beam.ContainsKey("to_space"))
            {
                var a = beam.Vector3("from_space");
                var b = beam.Vector3("to_space");
                if (!a.IsFinite() || !b.IsFinite() || a.DistanceSquaredTo(b) < .000001f)
                    continue;
                if (!spaceStarted)
                {
                    _spaceBeams.SurfaceBegin(Mesh.PrimitiveType.Triangles, _beamMaterial);
                    spaceStarted = true;
                }
                var width = (b - a).Normalized().Cross(_camera.GlobalBasis.Z);
                if (width.LengthSquared() < .0001f)
                    width = _camera.GlobalBasis.X;
                Quad(_spaceBeams, a, b, width.Normalized() * .009f, color);
            }
            else
            {
                var a = beam.Vector3("from").Normalized();
                var b = beam.Vector3("to").Normalized();
                if (a.LengthSquared() < .001f || b.LengthSquared() < .001f)
                    continue;
                float angle = Mathf.Acos(Mathf.Clamp(a.Dot(b), -1, 1));
                if (angle < .0001f)
                    continue;
                if (!surfaceStarted)
                {
                    _surfaceBeams.SurfaceBegin(Mesh.PrimitiveType.Triangles, _beamMaterial);
                    surfaceStarted = true;
                }
                var axis = a.Cross(b);
                if (axis.LengthSquared() < .000001f)
                    axis = a.Cross(Math.Abs(a.Y) < .98 ? Vector3.Up : Vector3.Right);
                axis = axis.Normalized();
                int segments = Math.Max(2, Mathf.CeilToInt(angle / .025f));
                for (int i = 0; i < segments; i++)
                    Quad(_surfaceBeams, a.Rotated(axis, angle * i / segments) * (WorldScale.EarthRadius + .18f), a.Rotated(axis, angle * (i + 1) / segments) * (WorldScale.EarthRadius + .18f), axis * .009f, color);
            }
        }
        if (spaceStarted)
            _spaceBeams.SurfaceEnd();
        if (surfaceStarted)
            _surfaceBeams.SurfaceEnd();
        var seen = new HashSet<long>();
        foreach (var data in bursts)
        {
            long uid = data.L("uid");
            seen.Add(uid);
            if (!_bursts.TryGetValue(uid, out var burst))
            {
                burst = _spares.Count > 0 ? _spares.Pop() : CreateBurst();
                _bursts[uid] = burst;
                Prepare(burst, uid, data.Get<Color>("color", new("a1ffe5")));
            }
            burst.Root.Visible = true;
            burst.Root.Position = data.Vector3("space_position");
            float t = Mathf.Clamp((float)(data.N("age") / Math.Max(.001, data.N("life", 1))), 0, 1), radius = (float)data.N("radius_world", data.N("radius", 24) / 104);
            burst.Core.Scale = Vector3.One * Math.Max(.001f, radius * .28f * Mathf.Pow(1 - t, 2));
            burst.Core.SetInstanceShaderParameter("visibility", Mathf.Pow(1 - t, 3));
            burst.Shell.Scale = Vector3.One * Math.Max(.001f, radius * (.15f + .85f * Mathf.Sqrt(t)));
            burst.Shell.SetInstanceShaderParameter("visibility", Mathf.Pow(1 - t, 1.7f) * .6f);
            burst.Sparks.SetInstanceShaderParameter("visibility", Mathf.Pow(1 - t, 1.4f));
            for (int i = 0; i < 8; i++)
                burst.Sparks.Multimesh.SetInstanceTransform(i, new(burst.Bases[i].Scaled(Vector3.One * (1 - t * .6f)), burst.Directions[i] * radius * t * (.8f + i % 3 * .22f)));
        }
        foreach (long uid in _bursts.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            var burst = _bursts[uid];
            burst.Root.Visible = false;
            _spares.Push(burst);
            _bursts.Remove(uid);
        }
    }
    private static void Quad(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 width, Color color)
    {
        foreach (var p in new[] { a - width, a + width, b - width, b - width, a + width, b + width })
        {
            mesh.SurfaceSetColor(color);
            mesh.SurfaceAddVertex(p);
        }
    }
    private Burst CreateBurst()
    {
        var root = new Node3D();
        _space.AddChild(root);
        var core = new MeshInstance3D { Mesh = _coreMesh, MaterialOverride = _coreMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        var shell = new MeshInstance3D { Mesh = _shellMesh, MaterialOverride = _shellMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        var sparks = new MultiMeshInstance3D { Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = _sparkMesh, InstanceCount = 8 }, MaterialOverride = _coreMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(core);
        root.AddChild(shell);
        root.AddChild(sparks);
        return new()
        {
            Root = root,
            Core = core,
            Shell = shell,
            Sparks = sparks
        };
    }
    private static void Prepare(Burst burst, long uid, Color color)
    {
        burst.Core.SetInstanceShaderParameter("energy_color", color.Lerp(Colors.White, .38f));
        burst.Shell.SetInstanceShaderParameter("energy_color", color);
        burst.Sparks.SetInstanceShaderParameter("energy_color", color);
        for (int i = 0; i < 8; i++)
        {
            float y = 1 - 2 * (i + .5f) / 8, angle = i * 2.39996323f + uid % 19 * .31f, ring = Mathf.Sqrt(1 - y * y);
            var direction = new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
            burst.Directions[i] = direction;
            burst.Bases[i] = Basis.LookingAt(direction, Vector3.Up);
        }
    }
    public void Clear()
    {
        _spaceBeams.ClearSurfaces();
        _surfaceBeams.ClearSurfaces();
        foreach (var burst in _bursts.Values)
        {
            burst.Root.Visible = false;
            _spares.Push(burst);
        }
        _bursts.Clear();
    }
}
