using Godot;
using Earthward.Rendering;
using Earthward.Domain;
namespace Earthward.Tests;

/// <summary>One managed transform buffer per mesh/material group, uploaded once.</summary>
public sealed class LegacyFleetRenderer127
{
    private sealed record Part(Mesh Mesh, Material? Override, Transform3D Local, bool State);
    private sealed class Bucket
    {
        public required string Key, Category; public required Node3D Parent;
        public required Part[] Parts; public required MultiMeshInstance3D[] Nodes;
        public readonly List<Transform3D> Transforms = new(); public readonly List<Color> States = new();
        public int Capacity; public float[] Buffer12 = [], Buffer16 = []; public float Radius;
        public Vector3 Min, Max;
        public void Reset()
        {
            Transforms.Clear();
            States.Clear();
            Min = Vector3.One * float.PositiveInfinity;
            Max = Vector3.One * float.NegativeInfinity;
        }
    }
    private readonly Node3D _space, _surface;
    private readonly Dictionary<string, Bucket> _buckets = new();
    private readonly Dictionary<(string Category, string Visual, bool Space, bool Critical), Bucket> _bucketLookup = new();
    private readonly Dictionary<string, Part[]> _prototypes = new();
    private readonly ShaderMaterial _warning = new() { Shader = GD.Load<Shader>("res://shaders/aircraft_critical.gdshader") };
    private static readonly HashSet<string> Frames = new() { "K1", "K2", "K3", "M1", "M2", "M3", "L1", "L2", "L3" };
    private static readonly HashSet<string> Roles = new() { "claw", "needle", "rock", "siege", "prism", "weaver", "hatcher", "jammer" };
    private static readonly Dictionary<string, float> FrameScales = AirframeCatalog.Definitions.ToDictionary(row => row.S("id"), row => (float)row.N("scale_multiplier", 1));
    private static readonly Dictionary<string, string> FrameVisuals = Frames.ToDictionary(id => id, id => "airframe/" + id);
    private static readonly Dictionary<string, string> RoleVisuals = Roles.ToDictionary(id => id, id => "enemy/" + id);
    public int BatchCount => _buckets.Values.Sum(bucket => bucket.Nodes.Length);
    public int PrototypePartCount => _prototypes.Values.Sum(parts => parts.Length);
    public LegacyFleetRenderer127(Node3D space, Node3D surface)
    {
        _space = space;
        _surface = surface;
    }
    public static string VisualKey(DataMap data, bool enemy, bool projectile)
    {
        string kind = data.S("kind", "interceptor");
        if (projectile)
        {
            // Packet kind can denote allegiance ("friendly"), not a resource name.
            // Resolve the visual weapon from explicit flags and source metadata.
            if (kind == "hostile" || data.S("target_kind") is "earth" or "drone")
                return "projectile/hostile";
            var source = data.Value("source") as DataMap;
            string family = data.S("weapon_kind", source?.S("kind", "") ?? "");
            string damageType = source?.S("damage_type", "") ?? "";
            if (data.B("missile") || kind == "missile" || family == "missile" || damageType == "explosive")
                return "projectile/missile";
            if (kind == "laser" || family == "laser" || damageType == "beam")
                return "projectile/laser";
            return "projectile/interceptor";
        }
        if (kind is "meteor" or "carrier" or "mothership")
            return kind;
        if (enemy)
        {
            string boss = data.S("boss_variant_id");
            if (boss is "brood" or "forge" or "prism")
                return boss switch { "brood" => "boss/brood", "forge" => "boss/forge", _ => "boss/prism" };
            string role = data.S("enemy_role_id");
            if (Roles.Contains(role))
                return RoleVisuals[role];
            return kind is "scout" or "cruiser" or "small_boss" or "boss" ? kind : "enemy/" + (role.Length > 0 ? role : kind);
        }
        string frame = data.S("airframe_id", kind == "laser" ? "L1" : kind == "missile" ? "M1" : "K1");
        string baseFrame = kind == "laser" ? "L1" : kind == "missile" ? "M1" : "K1";
        return FrameVisuals.GetValueOrDefault(frame, FrameVisuals[baseFrame]);
    }
    public void Sync(string category, IReadOnlyList<DataMap> actors, float scale, double time, long excludedUid = -1)
    {
        foreach (var bucket in _buckets.Values)
            if (bucket.Category == category)
                bucket.Reset();
        bool enemy = category == "enemies", projectile = category == "projectiles";
        var inverse = _surface.GlobalBasis.Inverse();
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i];
            long uid = actor.L("uid", i);
            if (category == "drones" && uid == excludedUid)
                continue;
            bool inSpace = actor.ContainsKey("space_position");
            var position = actor.Vector3("space_position");
            var normal = actor.Vector3("normal", Vector3.Forward);
            if (!position.IsFinite() || (!inSpace && (!normal.IsFinite() || normal.LengthSquared() < .0001f)))
                continue;
            if (!inSpace)
            {
                normal = normal.Normalized();
                position = normal * (WorldScale.EarthRadius + (float)actor.N("altitude", projectile ? .12 : .14));
            }
            var aim = actor.Vector3("aim_direction");
            bool hasAim = category == "drones" && aim.IsFinite() && aim.LengthSquared() > .0001f;
            var tangent = hasAim ? aim : actor.Vector3("tangent", Vector3.Right);
            if (!tangent.IsFinite())
                tangent = Vector3.Right;
            if (hasAim)
            {
                normal = actor.Vector3("aim_up", position.Normalized());
                if (!inSpace)
                {
                    tangent = inverse * tangent;
                    normal = inverse * normal;
                }
                tangent = tangent.Normalized();
                normal = ValidUp(tangent, normal, .999f);
            }
            else if (inSpace)
            {
                if (tangent.LengthSquared() < .0001f)
                    tangent = Vector3.Left;
                tangent = tangent.Normalized();
                normal = category == "drones" ? actor.Vector3("world_up", position.Normalized()) : Vector3.Back;
                normal = ValidUp(tangent, normal, .98f);
            }
            else
            {
                tangent -= normal * tangent.Dot(normal);
                if (tangent.LengthSquared() < .0001f)
                {
                    tangent = normal.Cross(Vector3.Up);
                    if (tangent.LengthSquared() < .0001f)
                        tangent = normal.Cross(Vector3.Right);
                }
            }
            tangent = tangent.Normalized();
            var right = tangent.Cross(normal).Normalized();
            float unitScale = scale * (category == "drones" ? FrameScales.GetValueOrDefault(actor.S("airframe_id"), 1) : 1);
            var transform = new Transform3D(new Basis(right, normal, -tangent).Scaled(Vector3.One * unitScale), position);
            if (actor.S("kind") == "meteor")
                transform = transform.RotatedLocal(Vector3.Up, (float)time * .43f + uid * .71f);
            double hp = actor.N("hp", 1), max = actor.N("max_hp", 1);
            bool critical = !projectile && max > 0 && hp > 0 && hp <= max * .3 && (category == "drones" || actor.S("kind") is "scout" or "cruiser") && !actor.B("post_carrier");
            string visual = VisualKey(actor, enemy, projectile);
            var bucket = GetBucket(category, visual, inSpace, critical);
            bucket.Transforms.Add(transform);
            float energy = Mathf.Clamp((float)(actor.N("energy_hp") / Math.Max(1, actor.N("energy_max_hp", 1))), 0, 1);
            float charge = Mathf.Clamp((float)Math.Max(actor.N("telegraph"), actor.N("weapon_charge", actor.N("fire_charge", actor.N("warmup_progress")))), 0, 1);
            bucket.States.Add(new(energy, charge, (float)((uid * .61803398875) % 1), 1));
            bucket.Min = bucket.Min.Min(position);
            bucket.Max = bucket.Max.Max(position);
        }
        foreach (var bucket in _buckets.Values)
            if (bucket.Category == category)
                Upload(bucket);
    }
    private static Vector3 ValidUp(Vector3 tangent, Vector3 up, float alignment)
    {
        if (!up.IsFinite() || up.LengthSquared() < .001f || Math.Abs(tangent.Dot(up.Normalized())) > alignment)
            up = Math.Abs(tangent.Dot(Vector3.Up)) < .98 ? Vector3.Up : Vector3.Right;
        return tangent.Cross(up).Normalized().Cross(tangent).Normalized();
    }
    private Bucket GetBucket(string category, string visual, bool inSpace, bool critical)
    {
        var lookup = (category, visual, inSpace, critical);
        if (_bucketLookup.TryGetValue(lookup, out var bucket)) return bucket;
        string key = category + ":" + visual + (inSpace ? ":space" : ":surface") + (critical ? ":critical" : "");
        if (!_prototypes.TryGetValue(visual, out var parts))
        {
            var list = new List<Part>();
            var model = RenderAssets.Model(visual);
            try
            {
                Collect(model, Transform3D.Identity, list);
            }
            finally
            {
                model.Free();
            }
            if (list.Count == 0)
            {
                // Never cache an empty prototype: it silently erases every actor in
                // this bucket. Preserve a visible category silhouette and diagnose it.
                RenderAssets.ReportModelFallback(visual, "No batchable mesh parts were collected.", "procedural model");
                var fallback = RenderAssets.CreateFallbackModel(visual);
                try
                {
                    Collect(fallback, Transform3D.Identity, list);
                }
                finally
                {
                    fallback.Free();
                }
            }
            if (list.Count == 0)
                throw new InvalidDataException("Combat model fallback contains no mesh parts: " + visual);
            parts = list.ToArray();
            _prototypes[visual] = parts;
        }
        var parent = inSpace ? _space : _surface;
        var nodes = new MultiMeshInstance3D[parts.Length];
        float radius = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = part.Mesh, UseCustomData = part.State, UseColors = false };
            var node = new MultiMeshInstance3D { Name = "ManagedFleet_" + key.Replace('/', '_').Replace(':', '_') + "_" + i, Multimesh = mm, MaterialOverride = part.Override, MaterialOverlay = critical ? _warning : null, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            parent.AddChild(node);
            if (part.State)
                node.SetInstanceShaderParameter("combat_batched", 1);
            nodes[i] = node;
            var bounds = part.Local * part.Mesh.GetAabb();
            for (int n = 0; n < 8; n++)
                radius = Math.Max(radius, bounds.GetEndpoint(n).Length());
        }
        bucket = new()
        {
            Key = key,
            Category = category,
            Parent = parent,
            Parts = parts,
            Nodes = nodes,
            Radius = radius
        };
        bucket.Reset();
        _bucketLookup[lookup] = bucket;
        return _buckets[key] = bucket;
    }
    private static void Collect(Node3D node, Transform3D parent, List<Part> parts)
    {
        var local = parent * node.Transform;
        if (node is MeshInstance3D mesh && mesh.Mesh != null && mesh.Mesh.GetSurfaceCount() > 0 && mesh.Visible)
        {
            bool state = mesh.MaterialOverride?.HasMeta("combat_state") == true;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                state |= mesh.Mesh.SurfaceGetMaterial(i)?.HasMeta("combat_state") == true;
            parts.Add(new(mesh.Mesh, mesh.MaterialOverride, local, state));
        }
        foreach (var child in node.GetChildren())
            if (child is Node3D spatial)
                Collect(spatial, local, parts);
    }
    private static void Upload(Bucket bucket)
    {
        int count = bucket.Transforms.Count;
        if (count == 0)
        {
            foreach (var node in bucket.Nodes)
            {
                node.Visible = false;
                node.Multimesh.VisibleInstanceCount = 0;
            }
            return;
        }
        if (count > bucket.Capacity)
        {
            bucket.Capacity = Math.Max(16, bucket.Capacity);
            while (bucket.Capacity < count)
                bucket.Capacity *= 2;
            bucket.Buffer12 = new float[bucket.Capacity * 12];
            bucket.Buffer16 = new float[bucket.Capacity * 16];
            foreach (var node in bucket.Nodes)
                node.Multimesh.InstanceCount = bucket.Capacity;
        }
        float maxScale = 1;
        foreach (var transform in bucket.Transforms)
            maxScale = Math.Max(maxScale, transform.Basis.X.Length());
        var extent = Vector3.One * bucket.Radius * maxScale;
        for (int part = 0; part < bucket.Nodes.Length; part++)
        {
            var node = bucket.Nodes[part];
            var source = bucket.Parts[part];
            int stride = source.State ? 16 : 12;
            var buffer = source.State ? bucket.Buffer16 : bucket.Buffer12;
            for (int i = 0; i < count; i++)
            {
                var t = source.Local == Transform3D.Identity ? bucket.Transforms[i] : bucket.Transforms[i] * source.Local;
                int p = i * stride;
                buffer[p] = t.Basis.X.X;
                buffer[p + 1] = t.Basis.Y.X;
                buffer[p + 2] = t.Basis.Z.X;
                buffer[p + 3] = t.Origin.X;
                buffer[p + 4] = t.Basis.X.Y;
                buffer[p + 5] = t.Basis.Y.Y;
                buffer[p + 6] = t.Basis.Z.Y;
                buffer[p + 7] = t.Origin.Y;
                buffer[p + 8] = t.Basis.X.Z;
                buffer[p + 9] = t.Basis.Y.Z;
                buffer[p + 10] = t.Basis.Z.Z;
                buffer[p + 11] = t.Origin.Z;
                if (source.State)
                {
                    var state = bucket.States[i];
                    buffer[p + 12] = state.R;
                    buffer[p + 13] = state.G;
                    buffer[p + 14] = state.B;
                    buffer[p + 15] = state.A;
                }
            }
            node.Multimesh.CustomAabb = new Aabb(bucket.Min - extent, bucket.Max - bucket.Min + extent * 2);
            node.Multimesh.Buffer = buffer;
            node.Multimesh.VisibleInstanceCount = count;
            node.Visible = true;
        }
    }
    public void Clear()
    {
        foreach (var bucket in _buckets.Values)
            foreach (var node in bucket.Nodes)
            {
                node.GetParent().RemoveChild(node);
                node.QueueFree();
            }
        _buckets.Clear();
        _bucketLookup.Clear();
    }
}
