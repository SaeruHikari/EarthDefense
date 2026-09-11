using Godot;
using System;
namespace Earthward.Presentation;

public partial class Backdrop : Node2D
{
    private ColorRect? _skyRect;
    private ShaderMaterial? _material;
    private Texture2D? _texture;
    private Basis _basis = Basis.Identity;
    private Vector2 _origin = Vector2.Zero;

    private Vector2 _viewSize = new(1440, 900);

    private Vector2 _projectionSize = new(1440, 900);
    private float _horizontalScale = 1;
    private float _fov = 36;
    private bool _motion = true;
    public bool MotionEnabled
    {
        get => _motion; set
        {
            _motion = value;
            _material?.SetShaderParameter("motion_amount", value ? 1f : 0f);
        }
    }

    public override void _Ready()
    {
        _texture = GD.Load<Texture2D>("res://assets/space/deep_sky.png");
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/deep_space.gdshader") };
        _material.SetShaderParameter("sky_panorama", _texture);
        _material.SetShaderParameter("motion_amount", _motion ? 1f : 0f);
        _skyRect = new ColorRect { Name = "CelestialPanorama", MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White, Material = _material };
        AddChild(_skyRect);
        GetViewport().SizeChanged += ResizeCanvas;
        SetView(_origin, _viewSize, _fov);
        ApplyBasis();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(GetViewport()))
            GetViewport().SizeChanged -= ResizeCanvas;
    }

    private void ResizeCanvas()
    {
        if (_skyRect == null || _material == null)
            return;
        var size = GetViewportRect().Size;
        _skyRect.Size = size;
        _material.SetShaderParameter("canvas_size", size);
        _projectionSize = new Vector2((int)_viewSize.X, (int)_viewSize.Y);
        var transform = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
        var physical = new Vector2(Math.Max(2, Mathf.RoundToInt(_projectionSize.X * transform.X.Length())), Math.Max(2, Mathf.RoundToInt(_projectionSize.Y * transform.Y.Length())));
        _horizontalScale = (physical.X / physical.Y) / (_projectionSize.X / _projectionSize.Y);
        _material.SetShaderParameter("view_size", _projectionSize);
        _material.SetShaderParameter("horizontal_projection_scale", _horizontalScale);
    }

    public void SetView(Vector2 origin, Vector2 dimensions, float fov = 36)
    {
        _origin = origin;
        _viewSize = new Vector2(Math.Max(1, dimensions.X), Math.Max(1, dimensions.Y));
        _fov = Math.Clamp(fov, 10, 120);
        if (_material == null)
            return;
        _material.SetShaderParameter("view_origin", _origin);
        _material.SetShaderParameter("view_size", _viewSize);
        _material.SetShaderParameter("vertical_fov", _fov);
        ResizeCanvas();
    }

    public void SetCameraBasis(Basis basis)
    {
        if (!basis.IsFinite() || basis.IsEqualApprox(_basis))
            return;
        _basis = basis.Orthonormalized();
        ApplyBasis();
    }

    private void ApplyBasis()
    {
        _material?.SetShaderParameter("camera_right", _basis.X);
        _material?.SetShaderParameter("camera_up", _basis.Y);
        _material?.SetShaderParameter("camera_back", _basis.Z);
    }

    public Vector2I GetSkyTextureSize() => _texture == null ? Vector2I.Zero : new Vector2I(_texture.GetWidth(), _texture.GetHeight());

    public Vector3 GetWorldDirection(Vector2 point)
    {
        Vector2 centered = (point - _origin - _projectionSize * .5f) / (_projectionSize.Y * .5f);
        float lens = Mathf.Tan(Mathf.DegToRad(_fov) * .5f);
        return (_basis * new Vector3(centered.X * lens * _horizontalScale, -centered.Y * lens, -1)).Normalized();
    }
}
