using Godot;
namespace Earthward.Rendering;

public sealed class WorldHighlight
{
    private MeshInstance3D? _source;
    private readonly MeshInstance3D[] _shells = new MeshInstance3D[2];
    private readonly ShaderMaterial[] _materials = new ShaderMaterial[2];
    private readonly Camera3D _camera;
    private float _strength;
    public WorldHighlight(Node3D parent, Camera3D camera)
    {
        _camera = camera;
        for (int i = 0; i < 2; i++)
        {
            var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/world_target_outline.gdshader"), RenderPriority = 12 + i };
            mat.SetShaderParameter("outline_width", i == 0 ? 3.7f : 1.45f);
            mat.SetShaderParameter("outline_color", i == 0 ? new Color(.20f, .82f, .88f, .13f) : new Color(.51f, 1, .86f, .84f));
            mat.SetShaderParameter("luminance", i == 0 ? .65f : .95f);
            _materials[i] = mat;
            _shells[i] = new()
            {
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = .15f,
                Visible = false
            };
            parent.AddChild(_shells[i]);
        }
    }
    public void SetTarget(MeshInstance3D? source)
    {
        if (source == _source)
            return;
        _source = source;
        _strength = 0;
        foreach (var shell in _shells)
        {
            shell.Mesh = source?.Mesh;
            shell.CustomAabb = source?.CustomAabb ?? default;
            shell.Visible = false;
        }
        bool moon = source?.MaterialOverride is ShaderMaterial m && m.GetShaderParameter("use_lola_height").VariantType == Variant.Type.Bool && m.GetShaderParameter("use_lola_height").AsBool();
        foreach (var mat in _materials)
        {
            mat.SetShaderParameter("lunar_relief", moon);
            bool earth = source?.MaterialOverride is ShaderMaterial earthMaterial && earthMaterial.Shader.ResourcePath.EndsWith("earth_surface.gdshader");
            mat.SetShaderParameter("radial_offset", earth ? WorldScale.EarthRadiusDelta / WorldScale.TerrainScale : 0f);
            if (moon)
                mat.SetShaderParameter("height_map", ((ShaderMaterial)source!.MaterialOverride).GetShaderParameter("height_map"));
        }
    }
    public void Update(float delta, Vector2 size)
    {
        if (_source == null || !GodotObject.IsInstanceValid(_source) || !_source.IsVisibleInTree())
        {
            foreach (var shell in _shells)
                shell.Visible = false;
            return;
        }
        _strength = Mathf.MoveToward(_strength, 1, Math.Max(0, delta) / .12f);
        float eased = _strength * _strength * (3 - 2 * _strength);
        Aabb bounds = _camera.GlobalTransform.AffineInverse() * _source.GlobalTransform * (_source.CustomAabb.Size.LengthSquared() > 0 ? _source.CustomAabb : _source.Mesh.GetAabb());
        for (int i = 0; i < 2; i++)
        {
            _shells[i].Visible = true;
            _shells[i].Mesh = _source.Mesh;
            _shells[i].CustomAabb = _source.CustomAabb;
            _shells[i].GlobalTransform = _source.GlobalTransform;
            _materials[i].SetShaderParameter("logical_view_size", size);
            _materials[i].SetShaderParameter("strength", eased);
            _materials[i].SetShaderParameter("target_depth_interval", new Vector2(-bounds.End.Z - .015f, -bounds.Position.Z + .015f));
        }
    }
}
