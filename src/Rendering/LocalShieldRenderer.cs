using Godot;
using Earthward.Domain;

namespace Earthward.Rendering;

/// <summary>Local spherical caps outside the atmosphere. Identical emitters share geometry.</summary>
public sealed class LocalShieldRenderer
{
    private sealed class Visual(MeshInstance3D node, ShaderMaterial material)
    {
        public readonly MeshInstance3D Node = node;
        public readonly ShaderMaterial Material = material;
        public Mesh? Mesh;
        public Vector3 Normal;
        public float Integrity = -1, Impact = -1;
    }
    private readonly Node3D _parent;
    private readonly Dictionary<long, Visual> _shields = new();
    private readonly Dictionary<(double, double), ArrayMesh> _meshes = new();
    private readonly Shader _shader = GD.Load<Shader>("res://shaders/local_shield.gdshader");
    public LocalShieldRenderer(Node3D parent) => _parent = parent;

    public void Sync(IReadOnlyList<DataMap> states)
    {
        var seen = new HashSet<long>();
        foreach (var state in states)
        {
            long id = state.L("site_id", -1);
            Vector3 normal = state.Vector3("normal");
            if (id < 0 || !normal.IsFinite() || normal.LengthSquared() < .5f) continue;
            seen.Add(id);
            if (!_shields.TryGetValue(id, out var visual))
            {
                var material = new ShaderMaterial { Shader = _shader, RenderPriority = 18 };
                var node = new MeshInstance3D { Name = "LocalShield_" + id, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                _parent.AddChild(node);
                _shields[id] = visual = new(node, material);
            }
            double radius = state.N("shield_radius", WorldScale.EarthRadius + WorldScale.ShieldAltitude);
            double angle = state.N("angle_radians", 5 / (double)WorldScale.EarthRadius);
            var key = (Math.Round(radius, 5), Math.Round(angle, 6));
            if (!_meshes.TryGetValue(key, out var mesh)) _meshes[key] = mesh = CreateCap((float)radius, (float)angle);
            if (!ReferenceEquals(visual.Mesh, mesh)) { visual.Mesh = mesh; visual.Node.Mesh = mesh; }
            if (visual.Normal != normal) { visual.Normal = normal; visual.Node.Quaternion = new(Vector3.Up, normal.Normalized()); }
            float integrity = (float)Math.Clamp(state.N("hp") / Math.Max(1, state.N("max_hp")), 0, 1);
            float impact = (float)Math.Clamp(state.N("hit") / .22, 0, 1);
            if (visual.Integrity != integrity) { visual.Integrity = integrity; visual.Material.SetShaderParameter("integrity", integrity); }
            if (visual.Impact != impact) { visual.Impact = impact; visual.Material.SetShaderParameter("impact", impact); }
        }
        foreach (long id in _shields.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            _shields[id].Node.QueueFree();
            _shields.Remove(id);
        }
    }
    private static ArrayMesh CreateCap(float radius, float angle)
    {
        const int slices = 64, rings = 12;
        var vertices = new Vector3[(rings + 1) * (slices + 1)];
        var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
        var indices = new List<int>(rings * slices * 6);
        for (int ring = 0; ring <= rings; ring++)
        {
            float theta = Math.Min(Mathf.Pi, angle) * ring / rings;
            for (int slice = 0; slice <= slices; slice++)
            {
                float phase = slice * Mathf.Tau / slices;
                int index = ring * (slices + 1) + slice;
                var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phase), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phase));
                vertices[index] = n * radius; normals[index] = n; uv[index] = new(slice / (float)slices, ring / (float)rings);
                if (ring == 0 || slice == slices) continue;
                int previous = index - slices - 1;
                indices.AddRange([previous, index + 1, index, previous, previous + 1, index + 1]);
            }
        }
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices; arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uv; arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
