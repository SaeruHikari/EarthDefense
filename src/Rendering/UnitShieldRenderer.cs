using Godot;
using Earthward.Domain;

namespace Earthward.Rendering;

/// <summary>Visible energy shells share a low-poly mesh and one buffer per faction/frame.</summary>
public sealed class UnitShieldRenderer
{
    private sealed class Batch
    {
        public required string Category;
        public required bool InSpace;
        public required MultiMeshInstance3D Node;
        public float[] Buffer = [];
        public int Count, Capacity;
        public Vector3 Min, Max;
    }
    private readonly Node3D _space, _surface;
    private readonly Dictionary<(string Category, bool InSpace), Batch> _batches = new();
    private readonly SphereMesh _mesh = new() { Radius = 1, Height = 2, RadialSegments = 12, Rings = 8 };
    private readonly ShaderMaterial _material = new() { Shader = GD.Load<Shader>("res://shaders/unit_shield.gdshader") };
    public int InstanceCount => _batches.Values.Sum(batch => batch.Count);
    public int BatchCount => _batches.Count;
    public UnitShieldRenderer(Node3D space, Node3D surface) { _space = space; _surface = surface; }
    public void BeginCategory(string category)
    {
        foreach (var batch in _batches.Values)
            if (batch.Category == category)
            {
                batch.Count = 0;
                batch.Min = Vector3.One * float.PositiveInfinity;
                batch.Max = Vector3.One * float.NegativeInfinity;
            }
    }
    public static Aabb MeasureModelBounds(Node3D root)
    {
        bool found = false; Aabb bounds = default;
        void Visit(Node3D node, Transform3D parent)
        {
            var local = parent * node.Transform;
            if (node is MeshInstance3D part && part.Visible && part.Mesh != null && part.MaterialOverride is not ShaderMaterial)
            {
                var current = local * part.Mesh.GetAabb();
                bounds = found ? bounds.Merge(current) : current; found = true;
            }
            foreach (var child in node.GetChildren()) if (child is Node3D spatial) Visit(spatial, local);
        }
        foreach (var child in root.GetChildren()) if (child is Node3D spatial) Visit(spatial, Transform3D.Identity);
        return bounds;
    }
    public static Transform3D ShellTransform(Transform3D hullTransform, Aabb hullBounds)
    {
        Vector3 half = hullBounds.Size.Abs() * .5f;
        float pad = Math.Max(.002f, Math.Max(half.X, Math.Max(half.Y, half.Z)) * .035f);
        Vector3 radii = (half * 1.18f + Vector3.One * pad).Max(Vector3.One * .005f);
        return hullTransform * new Transform3D(Basis.FromScale(radii), hullBounds.GetCenter());
    }
    public void Add(string category, bool inSpace, Transform3D hullTransform, Aabb hullBounds, float ratio, bool friendly)
    {
        if (!float.IsFinite(ratio) || ratio <= 0) return;
        var key = (category, inSpace);
        if (!_batches.TryGetValue(key, out var batch))
        {
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = _mesh };
            var node = new MultiMeshInstance3D
            {
                Name = "UnitShields_" + category + (inSpace ? "_world" : "_surface"),
                Multimesh = multi, MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            (inSpace ? _space : _surface).AddChild(node);
            batch = new() { Category = category, InSpace = inSpace, Node = node,
                Min = Vector3.One * float.PositiveInfinity, Max = Vector3.One * float.NegativeInfinity };
            _batches[key] = batch;
        }
        if (batch.Count >= batch.Capacity)
        {
            batch.Capacity = Math.Max(16, batch.Capacity * 2);
            Array.Resize(ref batch.Buffer, batch.Capacity * 16);
            batch.Node.Multimesh.InstanceCount = batch.Capacity;
        }
        var t = ShellTransform(hullTransform, hullBounds);
        int at = batch.Count++ * 16;
        var buffer = batch.Buffer;
        buffer[at] = t.Basis.X.X; buffer[at + 1] = t.Basis.Y.X; buffer[at + 2] = t.Basis.Z.X; buffer[at + 3] = t.Origin.X;
        buffer[at + 4] = t.Basis.X.Y; buffer[at + 5] = t.Basis.Y.Y; buffer[at + 6] = t.Basis.Z.Y; buffer[at + 7] = t.Origin.Y;
        buffer[at + 8] = t.Basis.X.Z; buffer[at + 9] = t.Basis.Y.Z; buffer[at + 10] = t.Basis.Z.Z; buffer[at + 11] = t.Origin.Z;
        buffer[at + 12] = Mathf.Clamp(ratio, 0, 1); buffer[at + 13] = friendly ? 1 : 0; buffer[at + 14] = 0; buffer[at + 15] = 1;
        Vector3 extent = t.Basis.X.Abs() + t.Basis.Y.Abs() + t.Basis.Z.Abs();
        batch.Min = batch.Min.Min(t.Origin - extent); batch.Max = batch.Max.Max(t.Origin + extent);
    }
    public void EndCategory(string category)
    {
        foreach (var batch in _batches.Values)
        {
            if (batch.Category != category) continue;
            var multi = batch.Node.Multimesh;
            if (batch.Count > 0)
            {
                multi.CustomAabb = new(batch.Min, batch.Max - batch.Min);
                multi.Buffer = batch.Buffer;
            }
            multi.VisibleInstanceCount = batch.Count;
            batch.Node.Visible = batch.Count > 0;
        }
    }
    public IReadOnlyList<DataMap> Diagnostics()
    {
        var rows = new List<DataMap>();
        foreach (var batch in _batches.Values)
        {
            var data = new List<object?>();
            for (int i = 0; i < batch.Count; i++)
            {
                int at = i * 16; var b = batch.Buffer;
                data.Add(new DataMap { ["ratio"] = b[at + 12], ["friendly"] = b[at + 13] > 0,
                    ["transform"] = new Transform3D(new Basis(new(b[at], b[at + 4], b[at + 8]), new(b[at + 1], b[at + 5], b[at + 9]), new(b[at + 2], b[at + 6], b[at + 10])), new(b[at + 3], b[at + 7], b[at + 11])) });
            }
            rows.Add(new() { ["category"] = batch.Category, ["in_space"] = batch.InSpace, ["count"] = batch.Count, ["instances"] = data });
        }
        return rows;
    }
    public void Clear()
    {
        foreach (var batch in _batches.Values)
        {
            batch.Count = 0;
            batch.Node.Multimesh.VisibleInstanceCount = 0;
            batch.Node.Visible = false;
        }
    }
}
