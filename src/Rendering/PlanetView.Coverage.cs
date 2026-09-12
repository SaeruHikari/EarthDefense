using Godot;
using Earthward.Combat;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private MeshInstance3D? _coverageSurface;
    private ShaderMaterial? _coverageMaterial;
    private Vector3 _coverageNormal;
    private float _coveragePatrolAngle = -1, _coverageAttackAngle = -1;
    private bool _coverageShieldMode;
    public long CoverageSiteId { get; private set; } = -1;
    public bool IsFactoryCoverageVisible => _coverageSurface?.Visible == true && !_coverageShieldMode;
    public bool IsShieldCoverageVisible => _coverageSurface?.Visible == true && _coverageShieldMode;

    /// <summary>
    /// Ground projection of the selected factory's union of patrol/engagement sectors.
    /// Radial limits remain separate in the HUD; they are not added to the angular boundary.
    /// One immutable mesh and one material are reused across all selections and stat changes.
    /// </summary>
    public void SetFactoryCoverage(FactoryCoverageSnapshot? coverage)
    {
        if (coverage == null || coverage.SiteId < 0 || coverage.SiteId >= _normals.Count
            || _slots[(int)coverage.SiteId] is not ("interceptor" or "missile" or "laser")
            || coverage.Bands.Count == 0)
        {
            CoverageSiteId = -1;
            _coverageShieldMode = false;
            _coverageMaterial?.SetShaderParameter("shield_mode", false);
            UpdateCoverageHeight(null);
            if (_coverageSurface != null)
                _coverageSurface.Visible = false;
            return;
        }

        EnsureCoverageSurface();
        _coverageShieldMode = false;
        _coverageMaterial!.SetShaderParameter("shield_mode", false);
        var normal = _normals[(int)coverage.SiteId];
        float patrol = (float)Math.Clamp(coverage.Bands.Max(b => b.PatrolRadius) / WorldScale.EarthRadius, 0, Math.PI);
        float attack = (float)Math.Clamp(coverage.Bands.Max(b => b.AngleRadians), 0, Math.PI);
        if (normal != _coverageNormal)
        {
            _coverageNormal = normal;
            _coverageMaterial!.SetShaderParameter("factory_normal", normal);
        }
        if (patrol != _coveragePatrolAngle)
        {
            _coveragePatrolAngle = patrol;
            _coverageMaterial!.SetShaderParameter("patrol_angle", patrol);
        }
        if (attack != _coverageAttackAngle)
        {
            _coverageAttackAngle = attack;
            _coverageMaterial!.SetShaderParameter("attack_angle", attack);
        }
        CoverageSiteId = coverage.SiteId;
        _coverageSurface!.Visible = true;
        UpdateCoverageHeight(coverage);
    }

    /// <summary>
    /// Display the selected local shield's ground projection.  It reuses the
    /// factory coverage mesh and material so selection remains allocation-free;
    /// the shader switches to a clean cyan dome without the factory's dashed
    /// pursuit line.
    /// </summary>
    public void SetShieldCoverage(LocalShieldCoverageSnapshot? coverage)
    {
        if (coverage == null || coverage.SiteId < 0 || coverage.SiteId >= _normals.Count
            || _slots[(int)coverage.SiteId] != "shield")
        {
            CoverageSiteId = -1;
            _coverageShieldMode = false;
            _coverageMaterial?.SetShaderParameter("shield_mode", false);
            UpdateCoverageHeight(null);
            if (_coverageSurface != null)
                _coverageSurface.Visible = false;
            return;
        }

        EnsureCoverageSurface();
        _coverageShieldMode = true;
        _coverageMaterial!.SetShaderParameter("shield_mode", true);
        var normal = _normals[(int)coverage.SiteId];
        float angle = (float)coverage.AngleRadians;
        if (normal != _coverageNormal)
        {
            _coverageNormal = normal;
            _coverageMaterial.SetShaderParameter("factory_normal", normal);
        }
        if (angle != _coveragePatrolAngle)
        {
            _coveragePatrolAngle = angle;
            _coverageMaterial.SetShaderParameter("patrol_angle", angle);
        }
        if (angle != _coverageAttackAngle)
        {
            _coverageAttackAngle = angle;
            _coverageMaterial.SetShaderParameter("attack_angle", angle);
        }
        CoverageSiteId = coverage.SiteId;
        _coverageSurface!.Visible = true;
        UpdateCoverageHeight(null);
    }

    private void EnsureCoverageSurface()
    {
        if (_coverageSurface != null)
            return;
        _coverageMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://shaders/factory_coverage.gdshader"),
            RenderPriority = 90
        };
        float radius = WorldScale.EarthRadius + WorldScale.MaxElevation + .025f;
        _coverageSurface = new MeshInstance3D
        {
            Name = "SelectedFactoryCoverage",
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 128, Rings = 64 },
            MaterialOverride = _coverageMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            IgnoreOcclusionCulling = true,
            Visible = false
        };
        Globe.AddChild(_coverageSurface);
    }
}
