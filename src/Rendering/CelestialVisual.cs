using Godot;
using Earthward.Domain;
namespace Earthward.Rendering;

public sealed class CelestialVisual
{
    public Node3D Root
    {
        get;
    }
    public IReadOnlyList<DataMap> Bodies => _bodies;
    public double Elapsed
    {
        get; private set;
    }
    private readonly List<DataMap> _bodies;
    private readonly Dictionary<string, Node3D> _nodes = new();
    private readonly Dictionary<string, MeshInstance3D> _surfaces = new();
    private readonly List<ShaderMaterial> _timedMaterials = new();
    private readonly Dictionary<string, int> _lods = new();
    private readonly Mesh[] _meshes;
    public CelestialVisual(Node3D parent)
    {
        Root = RenderAssets.Instantiate("res://assets/managed/world/celestial.scn");
        parent.AddChild(Root);
        _bodies = RenderAssets.ReadList("res://assets/managed/world/celestial.json").OfType<DataMap>().Where(b => b.S("id") != "earth").ToList();
        _meshes = Enumerable.Range(0, 4).Select(i => GD.Load<Mesh>($"res://assets/managed/world/celestial_lod_{i}.res")).ToArray();
        foreach (var body in _bodies)
        {
            string id = body.S("id");
            var node = Root.GetNode<Node3D>(body.S("name"));
            // Preserve each body's clearance above the enlarged Earth.
            var original = body.Vector3("position");
            if (original.LengthSquared() > .0001f)
                body["position"] = original.Normalized() * (original.Length() + WorldScale.EarthRadiusDelta);
            node.Position = body.Vector3("position");
            _nodes[id] = node;
            foreach (var surface in RenderAssets.Meshes(node))
            {
                if (surface.Name == "GeographicSurface" || surface.Name == "HDRPhotosphere")
                {
                    _surfaces[id] = surface;
                    _lods[id] = 1;
                }
                if (surface.MaterialOverride is ShaderMaterial old)
                {
                    var material = (ShaderMaterial)old.Duplicate();
                    surface.MaterialOverride = material;
                    if (id == "sun" || material.Shader.ResourcePath.EndsWith("celestial_clouds.gdshader"))
                        _timedMaterials.Add(material);
                }
            }
        }
    }
    public void Update(double delta, bool paused, Camera3D camera)
    {
        if (!paused && double.IsFinite(delta) && delta > 0)
        {
            Elapsed += Math.Min(delta, .25);
            ApplyClock();
        }
        float height = camera.GetViewport().GetVisibleRect().Size.Y, projection = height / (2 * Mathf.Tan(Mathf.DegToRad(camera.Fov) * .5f));
        foreach (var body in _bodies)
        {
            string id = body.S("id");
            float radius = (float)body.N("radius"), distance = Math.Max(camera.GlobalPosition.DistanceTo(_nodes[id].GlobalPosition), radius * 1.05f);
            float projected = radius * projection / distance;
            int lod = projected < 90 ? 0 : projected < 220 ? 1 : projected < 580 ? 2 : 3;
            if (_lods.GetValueOrDefault(id) == lod)
                continue;
            _lods[id] = lod;
            if (_surfaces.TryGetValue(id, out var surface))
                surface.Mesh = _meshes[lod];
            if (id == "venus")
                foreach (var mesh in RenderAssets.Meshes(_nodes[id]))
                    if (mesh.MaterialOverride is ShaderMaterial mat && mat.Shader.ResourcePath.EndsWith("celestial_clouds.gdshader"))
                        mesh.Mesh = _meshes[lod];
        }
    }
    public void RestoreClock(double value)
    {
        Elapsed = Math.Max(0, value);
        ApplyClock();
    }
    private void ApplyClock()
    {
        foreach (var body in _bodies)
        {
            var node = _nodes[body.S("id")];
            node.Rotation = node.Rotation with
            {
                Y = (float)(body.N("rotation_y") + Elapsed * body.N("spin"))
            };
        }
        foreach (var material in _timedMaterials)
            material.SetShaderParameter("simulation_time", Elapsed);
    }
    public MeshInstance3D? NavigationSurface(string id) => _surfaces.GetValueOrDefault(id);
    public Transform3D GetTransform(string id) => _nodes.TryGetValue(id, out var node) ? node.GlobalTransform : Transform3D.Identity;
}
