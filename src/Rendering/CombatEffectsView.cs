using Godot;
using Earthward.Domain;
namespace Earthward.Rendering;

/// <summary>Five reusable native groups: world/surface beams and explosion core/shell/sparks.</summary>
public sealed partial class CombatEffectsView
{
    private sealed class Burst
    {
        public readonly Vector3[] Directions = new Vector3[8];
        public readonly Basis[] Bases = new Basis[8];
        public Color CoreColor, Color;
        public Vector3 Position;
        public float Time, Radius;
        public long Seen;
        public int Index;
    }

    private sealed class BeamGeometry
    {
        public Vector3 From, To, Minimum, Maximum;
        public Basis CameraBasis;
        public bool World, Initialized;
        public Transform3D[] Segments = [];
        public int Count;
    }

    // Beam geometry is stable during its fade. Cache by submission position; endpoint comparisons also
    // handle removed/reordered beams without relying on optional UIDs.
    private readonly List<BeamGeometry> _beamGeometry = new();

    private readonly Node3D _space, _surface;
    private readonly Camera3D _camera;
    private readonly Dictionary<long, Burst> _bursts = new();
    private readonly Stack<Burst> _spares = new();
    private readonly List<long> _retiredIds = new();
    private EffectInstanceBatch? _spaceBeams, _surfaceBeams, _cores, _shells, _sparks;
    private ArrayMesh? _beamMesh;
    private ShaderMaterial? _beamMaterial, _coreMaterial, _shellMaterial;
    private long _epoch;
    public int BurstCount => _bursts.Count;
    public int SpareBurstCount => _spares.Count;
    public int BatchCount => (_spaceBeams == null ? 0 : 1) + (_surfaceBeams == null ? 0 : 1) + (_cores == null ? 0 : 1) + (_shells == null ? 0 : 1) + (_sparks == null ? 0 : 1) + LaserBatchCount;
    public int SparkCount => _sparks?.Count ?? 0;
    public int WorldBeamSegmentCount => _spaceBeams?.Count ?? 0;
    public int SurfaceBeamSegmentCount => _surfaceBeams?.Count ?? 0;
    public long BufferUploads => (_spaceBeams?.BufferUploads ?? 0) + (_surfaceBeams?.BufferUploads ?? 0) + (_cores?.BufferUploads ?? 0) + (_shells?.BufferUploads ?? 0) + (_sparks?.BufferUploads ?? 0) + LaserBufferUploads;
    public long NativeWrites => (_spaceBeams?.NativeWrites ?? 0) + (_surfaceBeams?.NativeWrites ?? 0) + (_cores?.NativeWrites ?? 0) + (_shells?.NativeWrites ?? 0) + (_sparks?.NativeWrites ?? 0) + LaserNativeWrites;
    public int BurstInstanceIndex(long uid) => _bursts.TryGetValue(uid, out var burst) ? burst.Index : -1;
    public MultiMeshInstance3D? GetBatchNode(string group) => group switch
    {
        "world_beams" => _spaceBeams?.Node, "surface_beams" => _surfaceBeams?.Node,
        "cores" => _cores?.Node, "shells" => _shells?.Node, "sparks" => _sparks?.Node, "laser_ribbons" => _laserRibbons?.Node, "laser_flares" => _laserFlares?.Node, _ => null
    };

    public CombatEffectsView(Node3D space, Node3D surface, Camera3D camera)
    {
        _space = space;
        _surface = surface;
        _camera = camera;
    }

    public void Sync(IReadOnlyList<DataMap> beams, IReadOnlyList<DataMap> bursts)
    {
        SyncBeams(beams);
        SyncBursts(bursts);
    }

    private void EnsureBeamResources()
    {
        if (_beamMesh != null) return;
        _beamMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = "shader_type spatial;render_mode blend_add,cull_disabled,depth_draw_never,ambient_light_disabled,specular_disabled;varying vec4 effect_state;void vertex(){effect_state=INSTANCE_CUSTOM;}void fragment(){ALBEDO=vec3(0.0);EMISSION=effect_state.rgb*5.5;ALPHA=effect_state.a;}"
            }
        };
        _beamMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        // Matches the old ImmediateMesh's six vertices exactly after the instance transform.
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { new(-1, 0, 0), new(1, 0, 0), new(-1, 1, 0), new(-1, 1, 0), new(1, 0, 0), new(1, 1, 0) };
        _beamMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    private EffectInstanceBatch BeamBatch(bool world)
    {
        EnsureBeamResources();
        if (world) return _spaceBeams ??= new EffectInstanceBatch(_space, "WorldBeamBatch", _beamMesh!, _beamMaterial!);
        return _surfaceBeams ??= new EffectInstanceBatch(_surface, "SurfaceBeamBatch", _beamMesh!, _beamMaterial!);
    }

    private static Transform3D BeamTransform(Vector3 a, Vector3 b, Vector3 width)
    {
        Vector3 along = b - a;
        return new Transform3D(new Basis(width, along, width.Cross(along).Normalized()), a);
    }

    private static void RebuildBeam(BeamGeometry cache, Vector3 a, Vector3 b, bool world, Basis cameraBasis)
    {
        cache.Initialized = true;
        cache.From = a;
        cache.To = b;
        cache.World = world;
        cache.CameraBasis = cameraBasis;
        cache.Count = 0;
        if (!a.IsFinite() || !b.IsFinite()) return;
        if (world)
        {
            if (a.DistanceSquaredTo(b) < .000001f) return;
            Vector3 width = (b - a).Normalized().Cross(cameraBasis.Z);
            if (width.LengthSquared() < .0001f) width = cameraBasis.X;
            width = width.Normalized() * .009f;
            if (cache.Segments.Length < 1) cache.Segments = new Transform3D[1];
            cache.Segments[0] = BeamTransform(a, b, width);
            cache.Count = 1;
            cache.Minimum = a.Min(b) - width.Abs();
            cache.Maximum = a.Max(b) + width.Abs();
            return;
        }
        a = a.Normalized(); b = b.Normalized();
        if (a.LengthSquared() < .001f || b.LengthSquared() < .001f) return;
        float angle = Mathf.Acos(Mathf.Clamp(a.Dot(b), -1, 1));
        if (!float.IsFinite(angle) || angle < .0001f) return;
        Vector3 axis = a.Cross(b);
        if (axis.LengthSquared() < .000001f) axis = a.Cross(Math.Abs(a.Y) < .98 ? Vector3.Up : Vector3.Right);
        axis = axis.Normalized();
        int segments = Math.Max(2, Mathf.CeilToInt(angle / .025f));
        if (cache.Segments.Length < segments) cache.Segments = new Transform3D[segments];
        cache.Minimum = Vector3.One * float.PositiveInfinity;
        cache.Maximum = Vector3.One * float.NegativeInfinity;
        Vector3 extent = (axis * .009f).Abs();
        for (int i = 0; i < segments; i++)
        {
            Vector3 from = a.Rotated(axis, angle * i / segments) * (WorldScale.EarthRadius + .18f);
            Vector3 to = a.Rotated(axis, angle * (i + 1) / segments) * (WorldScale.EarthRadius + .18f);
            cache.Segments[i] = BeamTransform(from, to, axis * .009f);
            cache.Minimum = cache.Minimum.Min(from.Min(to) - extent);
            cache.Maximum = cache.Maximum.Max(from.Max(to) + extent);
        }
        cache.Count = segments;
    }

    private void SyncBeams(IReadOnlyList<DataMap> beams)
    {
        _spaceBeams?.Begin();
        _surfaceBeams?.Begin();
        BeginLaserBeams();
        bool hasCamera = false;
        Basis cameraBasis = default;
        for (int index = 0; index < beams.Count; index++)
        {
            var beam = beams[index];
            bool world = beam.ContainsKey("from_space") && beam.ContainsKey("to_space");
            Vector3 a = beam.Vector3(world ? "from_space" : "from"), b = beam.Vector3(world ? "to_space" : "to");
            if (world && !hasCamera) { cameraBasis = _camera.GlobalBasis; hasCamera = true; }
            if (index == _beamGeometry.Count) _beamGeometry.Add(new BeamGeometry());
            var cache = _beamGeometry[index];
            if (world && beam.S("style") == "laser")
            {
                AddLaserVisual(beam, a, b, cameraBasis);
                continue;
            }
            if (!cache.Initialized || cache.World != world || cache.From != a || cache.To != b || world && cache.CameraBasis != cameraBasis)
                RebuildBeam(cache, a, b, world, cameraBasis);
            if (cache.Count == 0) continue;
            Color color = beam.Get<Color>("color", new("a1ffe5"));
            color.A *= Mathf.Clamp((float)beam.N("life", .2) * 7, .12f, 1);
            var batch = BeamBatch(world);
            batch.IncludeBounds(cache.Minimum, cache.Maximum);
            for (int i = 0; i < cache.Count; i++) batch.Add(cache.Segments[i], color);
        }
        _spaceBeams?.Commit();
        _surfaceBeams?.Commit();
        CommitLaserBeams();
    }

    private void EnsureBurstResources()
    {
        if (_cores != null) return;
        var shader = GD.Load<Shader>("res://shaders/combat_energy.gdshader");
        _coreMaterial = new ShaderMaterial { Shader = shader };
        _shellMaterial = new ShaderMaterial { Shader = shader };
        _coreMaterial.SetShaderParameter("energy", 10);
        _coreMaterial.SetShaderParameter("effects_batched", true);
        _shellMaterial.SetShaderParameter("energy", 5.5);
        _shellMaterial.SetShaderParameter("shell", 1);
        _shellMaterial.SetShaderParameter("effects_batched", true);
        _cores = new EffectInstanceBatch(_space, "ExplosionCoreBatch", new SphereMesh { Radius = 1, Height = 2, RadialSegments = 16, Rings = 8 }, _coreMaterial);
        _shells = new EffectInstanceBatch(_space, "ExplosionShellBatch", new SphereMesh { Radius = 1, Height = 2, RadialSegments = 32, Rings = 16 }, _shellMaterial);
        _sparks = new EffectInstanceBatch(_space, "ExplosionSparkBatch", new BoxMesh { Size = new Vector3(.012f, .012f, .05f) }, _coreMaterial);
    }

    private void SyncBursts(IReadOnlyList<DataMap> data)
    {
        _cores?.Begin(); _shells?.Begin(); _sparks?.Begin();
        _epoch++;
        foreach (var item in data)
        {
            Vector3 position = item.Vector3("space_position");
            float time = Mathf.Clamp((float)(item.N("age") / Math.Max(.001, item.N("life", 1))), 0, 1);
            float radius = (float)item.N("radius_world", item.N("radius", 24) / 104);
            if (!position.IsFinite() || !float.IsFinite(time) || !float.IsFinite(radius)) continue;
            long uid = item.L("uid");
            if (!_bursts.TryGetValue(uid, out var burst))
            {
                burst = _spares.Count > 0 ? _spares.Pop() : new Burst();
                _bursts[uid] = burst;
                Prepare(burst, uid, item.Get<Color>("color", new("a1ffe5")));
            }
            burst.Seen = _epoch;
            burst.Position = position;
            burst.Time = time;
            burst.Radius = radius;
        }
        _retiredIds.Clear();
        foreach (var (uid, burst) in _bursts)
        {
            if (burst.Seen != _epoch) { _retiredIds.Add(uid); continue; }
            EnsureBurstResources();
            burst.Index = _cores!.Count;
            float t = burst.Time, radius = burst.Radius;
            float coreScale = Math.Max(.001f, radius * .28f * Mathf.Pow(1 - t, 2));
            float shellScale = Math.Max(.001f, radius * (.15f + .85f * Mathf.Sqrt(t)));
            Color core = burst.CoreColor, shell = burst.Color, sparks = burst.Color;
            core.A = Mathf.Pow(1 - t, 3);
            shell.A = Mathf.Pow(1 - t, 1.7f) * .6f;
            sparks.A = Mathf.Pow(1 - t, 1.4f);
            _cores.IncludeBounds(burst.Position - Vector3.One * coreScale, burst.Position + Vector3.One * coreScale);
            _shells!.IncludeBounds(burst.Position - Vector3.One * shellScale, burst.Position + Vector3.One * shellScale);
            // The eight oriented spark boxes fit inside this conservative sphere. One bound per burst
            // replaces eight generic box transforms; their actual geometry and positions are unchanged.
            float sparkExtent = Math.Abs(radius) * t * 1.24f + .026401f * (1 - t * .6f);
            _sparks!.IncludeBounds(burst.Position - Vector3.One * sparkExtent, burst.Position + Vector3.One * sparkExtent);
            _cores.Add(new Transform3D(Basis.Identity.Scaled(Vector3.One * coreScale), burst.Position), core);
            _shells!.Add(new Transform3D(Basis.Identity.Scaled(Vector3.One * shellScale), burst.Position), shell);
            for (int i = 0; i < 8; i++)
                _sparks!.Add(new Transform3D(burst.Bases[i].Scaled(Vector3.One * (1 - t * .6f)), burst.Position + burst.Directions[i] * radius * t * (.8f + i % 3 * .22f)), sparks);
        }
        foreach (long uid in _retiredIds)
        {
            _spares.Push(_bursts[uid]);
            _bursts.Remove(uid);
        }
        _cores?.Commit(); _shells?.Commit(); _sparks?.Commit();
    }

    private static void Prepare(Burst burst, long uid, Color color)
    {
        // Godot RD converts instance Color uniforms to linear RGB; custom data must match that explicitly.
        burst.CoreColor = color.Lerp(Colors.White, .38f).SrgbToLinear();
        burst.Color = color.SrgbToLinear();
        for (int i = 0; i < 8; i++)
        {
            float y = 1 - 2 * (i + .5f) / 8, angle = i * 2.39996323f + uid % 19 * .31f, ring = Mathf.Sqrt(1 - y * y);
            Vector3 direction = new(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
            burst.Directions[i] = direction;
            burst.Bases[i] = Basis.LookingAt(direction, Vector3.Up);
        }
    }

    public void Clear()
    {
        _spaceBeams?.Clear(); _surfaceBeams?.Clear(); _cores?.Clear(); _shells?.Clear(); _sparks?.Clear();
        ClearLaserBeams();
        foreach (var burst in _bursts.Values) _spares.Push(burst);
        _bursts.Clear();
        _retiredIds.Clear();
    }
}
