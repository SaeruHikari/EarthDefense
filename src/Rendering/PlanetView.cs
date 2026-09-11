using Godot;
using Environment = Godot.Environment;
using Earthward.Domain;
using Earthward.Combat;
namespace Earthward.Rendering;

/// <summary>World-space presentation facade. Simulation never calls into a script.</summary>
public sealed partial class PlanetView : SubViewportContainer, ICombatSurface
{
    public DefenseState? Game
    {
        get; set;
    }
    public Vector2I ViewSize { get; set; } = new(900, 640);
    public float DefaultCameraDistance { get; set; } = WorldScale.DefaultCameraDistance;
    public bool Paused
    {
        get; set;
    }
    private bool _placing;
    public bool PlacingBuilding
    {
        get => _placing; set
        {
            _placing = value;
            if (Grid != null)
                Grid.SetBuildMode(value);
        }
    }
    public string PlacingKind { get; set; } = "";
    public int SelectedSlot { get; set; } = -1;
    private long _spatialRevision;
    public long SpatialRevision => _spatialRevision;
    private bool _shieldSitesDirty = true;
    private readonly List<DataMap> _shieldSitesCache = new();
    public Camera3D Camera { get; private set; } = null!;
    public SphericalGrid Grid { get; private set; } = null!;
    public Node3D Globe { get; private set; } = null!;
    public Node3D SpaceRoot { get; private set; } = null!;
    public event Action<int>? SlotSelected;
    public event Action? CameraChanged, FactoryLayoutChanged, StrategicCameraRequested;
    public event Action<string>? FocusChanged;
    private SubViewport _viewport = null!;
    private SubViewport _cloudViewport = null!;
    private Camera3D _cloudCamera = null!;
    private TextureRect _presenter = null!;
    // The volumetric cloud layer is low frequency and expensive per pixel, so it renders
    // through its own reduced-resolution camera on a dedicated visual layer. The main
    // camera skips that layer and the presenter composites the two buffers.
    private const uint CloudLayer = 1u << 19;
    private const int CloudResolutionDivisor = 2;
    // The low-resolution cloud buffer is already tonemapped, so compositing it directly over the
    // tonemapped scene skips the filmic compression the clouds used to receive inside the 3D pass
    // (measured +7% brighter, highlights blown out). Squaring both layers approximates the inverse
    // display transform so the square root compresses the sum once, matching the single-pass result
    // (measured 3.7% -> 0.8% mean deviation against the full-resolution reference).
    private const string CloudCompositeShader = "shader_type canvas_item;\nrender_mode blend_premul_alpha;\nuniform sampler2D cloud_layer : filter_linear, repeat_disable;\nvoid fragment() {\n\tvec4 base = texture(TEXTURE, UV);\n\tvec4 cloud = texture(cloud_layer, UV);\n\tvec3 sum = cloud.rgb * cloud.rgb + base.rgb * base.rgb * (1.0 - cloud.a);\n\tCOLOR = vec4(sqrt(sum), cloud.a + base.a * (1.0 - cloud.a));\n}";
    private EarthVisual _earth = null!;
    private CelestialVisual _celestial = null!;
    private FleetRenderer _fleet = null!;
    private CombatEffectsView _effects = null!;
    private Environment _environment = null!;
    private MeshInstance3D _atmosphere = null!;
    private ShaderMaterial _atmosphereMaterial = null!;
    private Node3D _frontRoot = null!;
    private readonly Dictionary<string, Node3D> _frontModels = new();
    private double _clock, _facilityClock;
    private float _hitDrive, _hitValue;
    private Vector3 _hitDirectionLocal;
    private Vector2 _hoverLocal = new(-1000, -1000);
    private readonly List<string> _slots = new();
    private readonly List<Vector3> _normals = new();
    private readonly List<int> _siteCells = new();
    private readonly List<Node3D> _siteNodes = new();
    private readonly Dictionary<int, int> _cellToSite = new(), _footprintToSite = new();
    private readonly Dictionary<int, FacilityVisual> _facilities = new();
    private bool _restoring;
    private int _resourceHover = -1, _lastSelected = -2;
    public static readonly Vector2[] SiteCoordinates = [new(12, 2), new(-51, -10), new(25, 47), new(-73, 48), new(-16, 48), new(-5, -37), new(56, -18), new(-78, -20), new(87, 35), new(118, -15), new(152, 48), new(170, -35), new(-148, 21), new(-115, -38), new(80, -56), new(-110, 60)];
    public static Vector3 SunDirection => new Basis(Quaternion.FromEuler(new Vector3(-32, -38, 0) * Mathf.Pi / 180f)).Z.Normalized();
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = false;
        CustomMinimumSize = ViewSize;
        Size = ViewSize;
        var host = new Node { Name = "PhysicalViewportHost" };
        AddChild(host);
        _viewport = new SubViewport { TransparentBg = true, OwnWorld3D = true, Size = NativeRenderSize(), Msaa3D = Viewport.Msaa.Msaa4X, AnisotropicFilteringLevel = Viewport.AnisotropicFiltering.Anisotropy16X, UseDebanding = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        host.AddChild(_viewport);
        // Shares the parent viewport's world: the cloud proxy mesh keeps its exact transform
        // and material parameters, only the camera resolution and cull mask differ.
        _cloudViewport = new SubViewport { TransparentBg = true, Size = CloudRenderSize(_viewport.Size), Msaa3D = Viewport.Msaa.Disabled, UseDebanding = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _viewport.AddChild(_cloudViewport);
        _cloudCamera = new Camera3D { CullMask = CloudLayer, Fov = 36, Near = .1f, Far = 420, Current = true };
        _cloudViewport.AddChild(_cloudCamera);
        var compositor = new ShaderMaterial { Shader = new Shader { Code = CloudCompositeShader } };
        compositor.SetShaderParameter("cloud_layer", _cloudViewport.GetTexture());
        _presenter = new TextureRect { Texture = _viewport.GetTexture(), Material = compositor, Size = ViewSize, MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        AddChild(_presenter);
        var scene = new Node3D();
        _viewport.AddChild(scene);
        _environment = new Environment { BackgroundMode = Environment.BGMode.ClearColor, AmbientLightSource = Environment.AmbientSource.Color, AmbientLightColor = new("a4bfd6"), AmbientLightEnergy = .24f, TonemapMode = Environment.ToneMapper.Filmic, TonemapExposure = .88f, GlowEnabled = true, GlowBlendMode = Environment.GlowBlendModeEnum.Additive, GlowIntensity = .44f, GlowStrength = 1, GlowBloom = 0, GlowHdrThreshold = 1.15f, GlowHdrScale = 2, GlowHdrLuminanceCap = 12 };
        float[] levels = [.22f, .55f, .22f, .055f, 0, 0, 0];
        for (int i = 0; i < levels.Length; i++)
            _environment.SetGlowLevel(i, levels[i]);
        var sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = GD.Load<Texture2D>("res://assets/pbr/orbital_reflections.exr") }, RadianceSize = Sky.RadianceSizeEnum.Size128, ProcessMode = Sky.ProcessModeEnum.Quality };
        _environment.Sky = sky;
        _environment.ReflectedLightSource = Environment.ReflectionSource.Sky;
        scene.AddChild(new WorldEnvironment { Environment = _environment });
        Camera = new Camera3D { Fov = 36, Near = .1f, Far = 420, Current = true };
        scene.AddChild(Camera);
        Camera.CullMask &= ~CloudLayer;
        AddLight(scene, new(-32, -38, 0), new("fff4e5"), 1.5f, true);
        AddLight(scene, new(8, 139, 0), new("6687b4"), .16f, false);
        AddLight(scene, new(30, 15, 0), new("b7cde6"), .035f, false);
        Globe = new Node3D { Name = "RotatingEarth", Rotation = new(0, Mathf.DegToRad(12), 0) };
        scene.AddChild(Globe);
        SpaceRoot = new Node3D { Name = "InertialSpace" };
        scene.AddChild(SpaceRoot);
        _earth = new EarthVisual(Globe, SunDirection);
        _earth.SetCloudLayer(CloudLayer);
        _celestial = new CelestialVisual(SpaceRoot);
        Grid = new SphericalGrid { Name = "SurfaceConstructionGrid" };
        Globe.AddChild(Grid);
        Grid.Setup(_earth);
        _atmosphere = (MeshInstance3D)RenderAssets.Instantiate("res://assets/managed/world/atmosphere.scn");
        Globe.AddChild(_atmosphere);
        _atmosphereMaterial = (ShaderMaterial)RenderAssets.ShaderMaterial(_atmosphere).Duplicate();
        _atmosphere.MaterialOverride = _atmosphereMaterial;
        _atmosphere.Scale *= WorldScale.AtmosphereRadius / (WorldScale.LegacyEarthRadius + .48f);
        _atmosphereMaterial.SetShaderParameter("planet_radius", WorldScale.EarthRadius);
        _atmosphereMaterial.SetShaderParameter("atmosphere_radius", WorldScale.AtmosphereRadius);
        _atmosphereMaterial.SetShaderParameter("air_profile_scale", WorldScale.AirProfileScale);
        _frontRoot = new Node3D { Name = "FixedInvasionMotherships" };
        SpaceRoot.AddChild(_frontRoot);
        _fleet = new FleetRenderer(SpaceRoot, Globe);
        _effects = new CombatEffectsView(SpaceRoot, Globe, Camera);
        ResetCamera();
        ResetPlanet();
        SetViewSize(ViewSize);
    }
    private static void AddLight(Node3D parent, Vector3 degrees, Color color, float energy, bool shadows) => parent.AddChild(new DirectionalLight3D { RotationDegrees = degrees, LightColor = color, LightEnergy = energy, ShadowEnabled = shadows, LightSpecular = shadows ? 1 : 0, DirectionalShadowMaxDistance = WorldScale.DefaultCameraDistance + WorldScale.EarthRadius, ShadowBias = .03f, ShadowNormalBias = .35f });
    public override void _Process(double delta)
    {
        if (_earth == null)
            return;
        long processStarted = CaptureFrameTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        _clock += delta;
        UpdateAtmosphereHit(delta);
        if (!Paused)
        {
            _facilityClock += delta;
            foreach (var visual in _facilities.Values)
                visual.Update(_facilityClock);
            if (!PlacingBuilding)
                RotatePlanet((float)delta * .007f * WorldScale.LegacyEarthRadius / WorldScale.EarthRadius);
        }
        _earth.Update(delta, Paused);
        _celestial.Update(delta, Paused, Camera);
        UpdateFocusTransition(delta);
        UpdateNavigationHighlight((float)delta);
        if (PlacingBuilding)
            SetBuildHover(_hoverLocal);
        if (_lastSelected != SelectedSlot)
        {
            _lastSelected = SelectedSlot;
            UpdateSelectedFootprint();
        }
        if (CaptureFrameTimings) LastPlanetProcessMs = System.Diagnostics.Stopwatch.GetElapsedTime(processStarted).TotalMilliseconds;
        SyncCloudCamera();
    }
    private void SyncCloudCamera()
    {
        _cloudCamera.GlobalTransform = Camera.GlobalTransform;
        _cloudCamera.Fov = Camera.Fov;
        _cloudCamera.Near = Camera.Near;
        _cloudCamera.Far = Camera.Far;
    }
    private static Vector2I CloudRenderSize(Vector2I full) => new(Math.Max(2, full.X / CloudResolutionDivisor), Math.Max(2, full.Y / CloudResolutionDivisor));
    public void SetViewSize(Vector2I dimensions, Vector2I physicalDimensions = default)
    {
        ViewSize = new(Math.Max(1, dimensions.X), Math.Max(1, dimensions.Y));
        CustomMinimumSize = ViewSize;
        Size = ViewSize;
        if (_viewport == null)
            return;
        _viewport.Size = physicalDimensions.X > 0 && physicalDimensions.Y > 0 ? physicalDimensions : NativeRenderSize();
        _cloudViewport.Size = CloudRenderSize(_viewport.Size);
        _presenter.Size = ViewSize;
        if (_focusId == "system")
            FocusSystem(true);
        CameraVisualsChanged();
    }
    private Vector2I NativeRenderSize()
    {
        if (!IsInsideTree())
            return ViewSize;
        var transform = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
        return new(Math.Max(2, Mathf.RoundToInt(ViewSize.X * transform.X.Length())), Math.Max(2, Mathf.RoundToInt(ViewSize.Y * transform.Y.Length())));
    }
    public Vector2I GetViewSize() => ViewSize;
    public Vector2I GetRenderSize() => _viewport?.Size ?? ViewSize;
    public Vector2 LogicalToRender(Vector2 point) => point * (Vector2)GetRenderSize() / (Vector2)ViewSize;
    public Vector2 RenderToLogical(Vector2 point) => point * (Vector2)ViewSize / (Vector2)GetRenderSize();
    public Vector3 ProjectViewRay(Vector2 point) => Camera.ProjectRayNormal(LogicalToRender(point)).Normalized();
    public void SetRenderSize(Vector2I dimensions) => SetViewSize(ViewSize, dimensions);
    public void ConfigureAntialiasing(Viewport.Msaa msaa, Viewport.ScreenSpaceAAEnum postAa = Viewport.ScreenSpaceAAEnum.Disabled, bool temporal = false)
    {
        _viewport.Msaa3D = msaa;
        _viewport.ScreenSpaceAA = postAa;
        _viewport.UseTaa = temporal;
    }
    public Vector3 SurfaceToSpace(Vector3 normal, double altitude = .14) => CachedSurfaceToSpace(normal, altitude);
    public Vector3 SpaceToSurface(Vector3 position) => CachedSpaceToSurface(position);
    public Vector2 GetSpaceScreenPosition(Vector3 position) => CachedScreenPosition(position);
    public Vector2 GetSurfaceScreenPosition(Vector3 normal, float altitude = .08f) => GetSpaceScreenPosition(SurfaceToSpace(normal, altitude));
    public Vector3 GetCameraRelativePosition(Vector3 position) { CacheProjection(); return _cachedView * position; }
    public Vector3 GetWorldSpawnPosition(Vector3 direction, float radius = WorldScale.SpawnMaxRadius) => (direction.IsFinite() && direction.LengthSquared() > .0001f ? direction : new Vector3(1, .3f, .4f)).Normalized() * Math.Max(radius, WorldScale.EarthRadius + 1.2f);
    public bool IsSurfaceVisible(Vector3 normal, float altitude = .08f) => IsSpaceVisible(SurfaceToSpace(normal, altitude));
    public bool IsSpaceVisible(Vector3 position) => CachedSpaceVisible(position);
    public void RotatePlanet(float amount)
    {
        Globe.RotateY(amount);
        _spatialRevision++;
    }
    public void PulseAtmosphereHit(double amount = 1, Vector3 worldImpact = default)
    {
        if (!double.IsFinite(amount) || amount <= 0)
            return;
        var settings = Game?.CombatSettings ?? new();
        float max = Mathf.Clamp((float)settings.N("atmosphere_hit_max_strength", 1), 0, 1);
        float full = Math.Max(.1f, (float)settings.N("atmosphere_hit_full_percent", 8));
        double durability = 100 + (Game?.ShieldMax() ?? 100);
        _hitDrive = Math.Min(max, _hitDrive + max * Mathf.Clamp((float)(amount / Math.Max(1, durability) * 100) / full, 0, 1));
        // Keep the geographic anchor in Earth's frame; the shader's model basis
        // rotates the highlight with the same surface point automatically.
        _hitDirectionLocal = Vector3.Zero;
        if (Globe != null && worldImpact.IsFinite() && worldImpact.LengthSquared() > .0001f)
        {
            Vector3 localPoint = Globe.ToLocal(worldImpact);
            if (localPoint.LengthSquared() > .0001f)
                _hitDirectionLocal = localPoint.Normalized();
        }
        if (_atmosphereMaterial != null)
        {
            Vector3 meshDirection = _hitDirectionLocal == Vector3.Zero ? Vector3.Zero
                : (_atmosphere.Transform.Basis.Inverse() * _hitDirectionLocal).Normalized();
            _atmosphereMaterial.SetShaderParameter("hit_direction_local", meshDirection);
        }
    }
    private void UpdateAtmosphereHit(double delta)
    {
        if (Paused || delta <= 0 || (_hitDrive <= 0 && _hitValue <= 0))
            return;
        var settings = Game?.CombatSettings ?? new();
        float max = Mathf.Clamp((float)settings.N("atmosphere_hit_max_strength", 1), 0, 1);
        float rate = Mathf.Clamp((float)settings.N("atmosphere_hit_lerp_speed", 12), 1, 30);
        _hitDrive = Math.Min(max, _hitDrive) * MathF.Exp(-(float)delta * 4.5f);
        _hitValue = Math.Min(_hitValue, max);
        float response = _hitDrive > _hitValue ? rate : rate * .5f;
        _hitValue = Mathf.Lerp(_hitValue, _hitDrive, 1 - MathF.Exp(-(float)delta * response));
        if (_hitDrive < .00005f && _hitValue < .00005f)
        {
            ClearAtmosphereHit();
            return;
        }
        _atmosphereMaterial.SetShaderParameter("hit_pulse", _hitValue);
    }
    public void ClearAtmosphereHit()
    {
        _hitDrive = _hitValue = 0;
        _hitDirectionLocal = Vector3.Zero;
        _atmosphereMaterial?.SetShaderParameter("hit_pulse", 0);
        _atmosphereMaterial?.SetShaderParameter("hit_direction_local", Vector3.Zero);
    }
    public DataMap GetAtmosphereHitState() => new()
    {
        ["drive"] = _hitDrive, ["value"] = _hitValue,
        ["direction_local"] = _hitDirectionLocal,
        ["direction_world"] = _hitDirectionLocal == Vector3.Zero || Globe == null ? Vector3.Zero
            : (Globe.GlobalBasis * _hitDirectionLocal).Normalized()
    };
    public void ClearCombatUnits()
    {
        _fleet.Clear();
        _effects.Clear();
        ClearAtmosphereHit();
        SyncFactoryActivity([]);
    }
    public void SyncCombatUnits(IReadOnlyList<DataMap> drones, IReadOnlyList<DataMap> enemies, IReadOnlyList<DataMap> projectiles, IReadOnlyList<DataMap> beams, IReadOnlyList<DataMap>? bursts = null)
    {
        long stage = CaptureFrameTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        EnsureUnitShields();
        _fleet.Sync("drones", drones, (float)(Game?.CombatSettings.N("aircraft_scale", .5) ?? .5), _clock, _spectatorUid);
        if (CaptureFrameTimings) { LastDroneSyncMs = System.Diagnostics.Stopwatch.GetElapsedTime(stage).TotalMilliseconds; stage = System.Diagnostics.Stopwatch.GetTimestamp(); }
        _fleet.Sync("enemies", enemies, (float)(Game?.CombatSettings.N("enemy_aircraft_scale", .5) ?? .5), _clock);
        if (CaptureFrameTimings) { LastEnemySyncMs = System.Diagnostics.Stopwatch.GetElapsedTime(stage).TotalMilliseconds; stage = System.Diagnostics.Stopwatch.GetTimestamp(); }
        _fleet.Sync("projectiles", projectiles, 1, _clock);
        if (CaptureFrameTimings) { LastProjectileSyncMs = System.Diagnostics.Stopwatch.GetElapsedTime(stage).TotalMilliseconds; stage = System.Diagnostics.Stopwatch.GetTimestamp(); }
        _effects.Sync(beams, bursts ?? []);
        if (CaptureFrameTimings) LastEffectsSyncMs = System.Diagnostics.Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
    }
}
