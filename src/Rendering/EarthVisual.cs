using Godot;
namespace Earthward.Rendering;

/// <summary>Owns weather and terrain LOD; baked meshes expand radially without scaling relief or facilities.</summary>
public sealed class EarthVisual
{
    public Node3D Root
    {
        get;
    }
    // These radii describe the immutable meshes exported before the playable Earth grew.
    // Altitudes remain in world units; only the sea-level radius comes from WorldScale.
    public const float BakedPlanetRadius = 4f;
    public const float CloudBaseAltitude = .20f;
    public const float CloudHeightRange = .28f;
    public const float CloudProxyAltitude = .50f;
    public const float CirrusAltitude = .52f;
    // Lowered from 24 so the volumetric clouds stay translucent instead of
    // reading as a solid opaque shell. Must be set at runtime: this value is
    // baked into assets/managed/world/earth.scn at export time.
    private const float CloudExtinction = 14f;
    private const float BakedCloudProxyRadius = BakedPlanetRadius + .30f;
    private const float BakedCirrusRadius = BakedPlanetRadius + .32f;
    private const float AirProfileScale = WorldScale.AirProfileScale;
    private readonly MeshInstance3D _surface, _cloud, _cirrus;
    private readonly ShaderMaterial _groundMaterial, _cloudMaterial, _cirrusMaterial;
    private readonly ShaderMaterial[] _weather;
    private static Image? _height;
    private readonly Mesh[] _terrainLods;
    private int _lod = -1, _quality = 1;
    private float _projectedRadius = 360, _waterAngle;
    public double CloudTime
    {
        get; private set;
    }
    public float CloudMotionSpeed { get; set; } = 9f;
    private readonly Vector3 _sun;
    public EarthVisual(Node3D parent, Vector3 sun)
    {
        _sun = sun;
        Root = RenderAssets.Instantiate("res://assets/managed/world/earth.scn");
        parent.AddChild(Root);
        _surface = Root.GetNode<MeshInstance3D>("EarthTerrain");
        _cloud = Root.GetNode<MeshInstance3D>("CloudDrift/EarthClouds");
        _cirrus = Root.GetNode<MeshInstance3D>("CloudDrift/EarthCirrus");
        // Each world has its own shader time without duplicating the large textures.
        _groundMaterial = (ShaderMaterial)RenderAssets.ShaderMaterial(_surface).Duplicate();
        _surface.MaterialOverride = _groundMaterial;
        _cloudMaterial = (ShaderMaterial)RenderAssets.ShaderMaterial(_cloud).Duplicate();
        _cloud.MaterialOverride = _cloudMaterial;
        _cirrusMaterial = (ShaderMaterial)RenderAssets.ShaderMaterial(_cirrus).Duplicate();
        _cirrus.MaterialOverride = _cirrusMaterial;
        _weather = [_groundMaterial, _cloudMaterial, _cirrusMaterial];
        ConfigureWorldRadii();
        _terrainLods = [GD.Load<Mesh>("res://assets/earth/terrain_128.res"), GD.Load<Mesh>("res://assets/earth/terrain_256.res"), GD.Load<Mesh>("res://assets/earth/terrain_512.res")];
        if (_height == null)
        {
            _height = GD.Load<Texture2D>("res://assets/earth/earth_height_5400.png").GetImage();
            if (_height.IsCompressed())
                _height.Decompress();
        }
        SetCloudTime(0);
        UpdateLod(360);
    }
    private void ConfigureWorldRadii()
    {
        float radius = WorldScale.EarthRadius;
        float cloudBase = radius + CloudBaseAltitude;
        float cloudProxy = radius + CloudProxyAltitude;
        float cirrusRadius = radius + CirrusAltitude;

        // Do not scale Root or the terrain instance: vertex displacement adds only the
        // sea-level offset, preserving the baked .048-unit elevation and all factories.
        _groundMaterial.SetShaderParameter("planet_radius", radius);
        _groundMaterial.SetShaderParameter("baked_planet_radius", BakedPlanetRadius);
        _groundMaterial.SetShaderParameter("geometry_scale", _surface.Scale.X);
        _groundMaterial.SetShaderParameter("max_displacement", WorldScale.MaxElevation);
        _groundMaterial.SetShaderParameter("cloud_radius", WorldScale.CloudRadius);
        SetRadialBounds(_surface, radius + WorldScale.MaxElevation + .004f);

        // The volume proxy is empty bounding geometry. Cirrus displaces its own shell,
        // so its shader divisor must follow the instance's new absolute mesh scale.
        _cloud.Scale *= cloudProxy / BakedCloudProxyRadius;
        _cirrus.Scale *= cirrusRadius / BakedCirrusRadius;
        _cirrusMaterial.SetShaderParameter("cloud_mesh_scale", _cirrus.Scale.X);
        _cirrusMaterial.SetShaderParameter("cloud_radius", WorldScale.CloudRadius);
        SetRadialBounds(_cloud, cloudProxy + .002f);
        SetRadialBounds(_cirrus, cirrusRadius + .019f);

        foreach (var material in _weather)
        {
            material.SetShaderParameter("base_cloud_radius", cloudBase);
            material.SetShaderParameter("height_range", CloudHeightRange);
            material.SetShaderParameter("high_cloud_radius", cirrusRadius);
        }
        foreach (var material in new[] { _cloudMaterial, _cirrusMaterial })
        {
            material.SetShaderParameter("planet_radius", radius);
            material.SetShaderParameter("air_profile_scale", AirProfileScale);
        }
        _cloudMaterial.SetShaderParameter("atmosphere_radius", WorldScale.AtmosphereRadius);
        foreach (var material in new[] { _groundMaterial, _cloudMaterial })
        {
            // Preserve the exact angular weather and interior noise from the original
            // cloud columns instead of doubling noise frequency on the expanded globe.
            material.SetShaderParameter("volume_reference_base_radius", BakedPlanetRadius + .08f);
        }
    }

    private static void SetRadialBounds(MeshInstance3D instance, float worldRadius)
    {
        float localRadius = worldRadius / instance.Scale.X;
        var extent = Vector3.One * localRadius;
        instance.CustomAabb = new Aabb(-extent, extent * 2f);
    }

    public static Vector2 NormalToUv(Vector3 normal)
    {
        var n = normal.Normalized();
        return new(Mathf.Atan2(n.X, n.Z) / Mathf.Tau + .5f, Mathf.Acos(Mathf.Clamp(n.Y, -1, 1)) / Mathf.Pi);
    }
    public float SurfaceRadius(Vector3 normal)
    {
        if (_height == null || !normal.IsFinite() || normal.LengthSquared() < .0001f)
            return WorldScale.EarthRadius;
        var uv = NormalToUv(normal);
        int w = _height.GetWidth(), h = _height.GetHeight();
        float x = Mathf.PosMod(uv.X * w - .5f, w), y = Mathf.Clamp(uv.Y * h - .5f, 0, h - 1);
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float upper = Mathf.Lerp(_height.GetPixel(x0, y0).R, _height.GetPixel((x0 + 1) % w, y0).R, x - x0);
        float lower = Mathf.Lerp(_height.GetPixel(x0, Math.Min(y0 + 1, h - 1)).R, _height.GetPixel((x0 + 1) % w, Math.Min(y0 + 1, h - 1)).R, x - x0);
        return WorldScale.EarthRadius + Mathf.Clamp(Mathf.Lerp(upper, lower, y - y0), 0, 1) * WorldScale.MaxElevation;
    }
    public void SetCloudTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            return;
        CloudTime = seconds;
        _waterAngle = (float)Mathf.Wrap(seconds * .0009, -Math.PI, Math.PI);
        foreach (var material in _weather)
            material.SetShaderParameter("cloud_time", seconds);
        _groundMaterial.SetShaderParameter("cloud_angle", _waterAngle);
    }
    public void Update(double delta, bool paused)
    {
        var inverse = Root.GlobalBasis.Orthonormalized().Inverse();
        var localSun = inverse * _sun;
        _cloudMaterial.SetShaderParameter("world_to_cloud", inverse);
        _cloudMaterial.SetShaderParameter("cloud_center", Root.GlobalPosition);
        foreach (var mat in _weather)
        {
            mat.SetShaderParameter("local_sun_direction", localSun);
            mat.SetShaderParameter("sun_direction", _sun);
        }
        if (paused || delta <= 0 || !double.IsFinite(delta))
            return;
        float water = Mathf.Wrap(_waterAngle + (float)delta * .0009f, -Mathf.Pi, Mathf.Pi);
        SetCloudTime(CloudTime + delta * CloudMotionSpeed);
        _waterAngle = water;
        _groundMaterial.SetShaderParameter("cloud_angle", water);
    }
    public void UpdateLod(float radius)
    {
        if (!float.IsFinite(radius))
            return;
        _projectedRadius = Math.Max(0, radius);
        if (_quality == 0 && radius > 200)
            _quality = radius > 650 ? 2 : 1;
        else if (_quality == 2 && radius < 550)
            _quality = radius < 160 ? 0 : 1;
        else if (_quality == 1)
        {
            if (radius < 160)
                _quality = 0;
            else if (radius > 650)
                _quality = 2;
        }
        float high = _quality > 0 ? Mathf.SmoothStep(200, 280, radius) : 0, relief = Mathf.SmoothStep(550, 750, radius);
        foreach (var m in _weather)
        {
            m.SetShaderParameter("cloud_detail_amount", high);
            m.SetShaderParameter("cloud_relief_amount", relief);
            m.SetShaderParameter("cloud_quality", _quality);
            m.SetShaderParameter("clouds_enabled", true);
            m.SetShaderParameter("high_cloud_amount", high);
        }
        _cloudMaterial.SetShaderParameter("volume_steps", new[] { 24, 40, 64 }[_quality]);
        _cloudMaterial.SetShaderParameter("volume_shadow_steps", new[] { 3, 4, 5 }[_quality]);
        _cloudMaterial.SetShaderParameter("cloud_volume_enabled", true);
        _groundMaterial.SetShaderParameter("cloud_volume_enabled", true);
        foreach (var material in new[] { _cloudMaterial, _groundMaterial })
            material.SetShaderParameter("volume_extinction", CloudExtinction);
        _cirrus.Visible = high > .001f;
        int next = radius < 135 ? 0 : radius > 330 ? 2 : 1;
        if (next == _lod)
            return;
        _lod = next;
        _surface.Mesh = _terrainLods[next];
    }
}
