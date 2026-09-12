using Godot;
using Earthward.Domain;
namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private string _focusId = "earth";
    private float _focusRadius = WorldScale.EarthRadius, _overviewDistance, _cameraYaw, _cameraPitch, _cameraDistance = WorldScale.DefaultCameraDistance, _focusElapsed = 1;
    private Vector3 _cameraTarget, _cameraViewTarget, _focusFromPosition, _focusFromTarget, _focusToPosition;
    private bool _surfaceFocus;
    private float _focusDuration = 1.15f, _surfaceFocusAngle, _surfaceFocusRoll;
    private Vector3 _surfaceFocusAxis = Vector3.Up, _surfaceFocusFromUp = Vector3.Up;
    private bool _spectatorActive;
    private long _spectatorUid = -1;
    private DataMap _navigationHover = new();
    private WorldHighlight? _highlight;
    public string GetFocusId() => _focusId;
    public float GetCameraDistance() => _cameraDistance;
    public bool IsCameraTransitionActive => !_spectatorActive && _focusElapsed < 1;
    public float GetFocusTransitionProgress() => Mathf.Clamp(_focusElapsed, 0, 1);
    public Vector2 GetCameraHeading() => new(_cameraYaw, _cameraPitch);
    public float GetProjectedPlanetRadius()
    {
        float focal = ViewSize.Y * .5f / Mathf.Tan(Mathf.DegToRad(Camera?.Fov ?? 36) * .5f);
        float distance = Camera?.GlobalPosition.Length() ?? DefaultCameraDistance;
        return focal * WorldScale.EarthRadius / Mathf.Sqrt(Math.Max(distance * distance - WorldScale.EarthRadius * WorldScale.EarthRadius, .0001f));
    }
    public void OrbitCamera(float yaw, float pitch = 0)
    {
        if (_spectatorActive || !float.IsFinite(yaw) || !float.IsFinite(pitch))
            return;
        InterruptSurfaceFocusTransition();
        _focusElapsed = 1;
        _cameraYaw = Mathf.Wrap(_cameraYaw + yaw, -Mathf.Pi, Mathf.Pi);
        _cameraPitch = Mathf.Clamp(_cameraPitch + pitch, Mathf.DegToRad(-80), Mathf.DegToRad(80));
        UpdateCameraTransform();
    }
    public void ZoomCamera(float amount)
    {
        if (_spectatorActive || !float.IsFinite(amount))
            return;
        InterruptSurfaceFocusTransition();
        _focusElapsed = 1;
        float closest = _focusId == "earth" ? WorldScale.MinCameraDistance : Math.Max(_focusRadius * 1.45f, _focusRadius + .5f), farthest = _focusId == "system" ? Math.Max(160, _overviewDistance * 1.4f) : 160;
        _cameraDistance = Mathf.Clamp(_cameraDistance + amount * Math.Max(1, _cameraDistance / 24), closest, farthest);
        UpdateCameraTransform();
    }
    public void ResetCamera()
    {
        RequestStrategicCamera();
        bool changed = _focusId != "earth";
        _focusId = "earth";
        _focusRadius = WorldScale.EarthRadius;
        _cameraTarget = _cameraViewTarget = Vector3.Zero;
        _surfaceFocus = false;
        _focusElapsed = 1;
        _cameraYaw = _cameraPitch = 0;
        _cameraDistance = Mathf.Clamp(DefaultCameraDistance, WorldScale.MinCameraDistance, WorldScale.MaxCameraDistance);
        UpdateCameraTransform();
        if (changed)
            FocusChanged?.Invoke("earth");
    }
    private Vector3 ViewDirection() => new(Mathf.Sin(_cameraYaw) * Mathf.Cos(_cameraPitch), Mathf.Sin(_cameraPitch), Mathf.Cos(_cameraYaw) * Mathf.Cos(_cameraPitch));
    private void UpdateCameraTransform()
    {
        if (_spectatorActive || Camera == null)
            return;
        Camera.GlobalPosition = AvoidBodyCollision(_cameraTarget + ViewDirection() * _cameraDistance);
        _cameraViewTarget = _cameraTarget;
        Camera.LookAt(_cameraViewTarget, Vector3.Up);
        CameraVisualsChanged();
    }
    private void CameraVisualsChanged()
    {
        _projectionCached = false;
        if (Camera == null)
            return;
        if (!_spectatorActive)
            Camera.Far = Math.Max(420, _cameraDistance + (_focusId == "system" ? 360 : 160));
        _earth?.UpdateLod(GetProjectedPlanetRadius() * GetRenderSize().Y / ViewSize.Y);
        _celestial?.Update(0, true, Camera);
        CameraChanged?.Invoke();
    }
    public IReadOnlyList<DataMap> GetCelestialBodies()
    {
        var result = new List<DataMap> { new() { ["id"] = "earth", ["name"] = "地球", ["position"] = Vector3.Zero, ["radius"] = WorldScale.EarthRadius, ["focus_distance"] = DefaultCameraDistance, ["description"] = $"防御母星 · {Grid?.CellCount ?? WorldScale.DefaultGridCellCount} 地块 · 更广阔的建设与巡航空间" } };
        if (_celestial != null)
            result.AddRange(_celestial.Bodies);
        return result;
    }
    public bool FocusBody(string id, bool instant = false)
    {
        var body = GetCelestialBodies().FirstOrDefault(b => b.S("id") == id);
        if (body == null)
            return false;
        RequestStrategicCamera();
        _focusId = id;
        _focusRadius = (float)body.N("radius");
        _cameraTarget = body.Vector3("position");
        _cameraDistance = (float)body.N("focus_distance");
        var direction = (Vector3.Back * .62f + SunDirection * .58f).Normalized();
        _cameraYaw = id == "earth" ? 0 : Mathf.Atan2(direction.X, direction.Z);
        _cameraPitch = id == "earth" ? 0 : Mathf.Asin(direction.Y);
        StartFocusTransition(instant);
        FocusChanged?.Invoke(id);
        return true;
    }
    public void FocusSystem(bool instant = false)
    {
        RequestStrategicCamera();
        var bodies = GetCelestialBodies();
        Vector3 lower = Vector3.One * float.PositiveInfinity, upper = Vector3.One * float.NegativeInfinity;
        foreach (var body in bodies)
        {
            var extent = Vector3.One * (float)body.N("radius");
            var p = body.Vector3("position");
            lower = lower.Min(p - extent);
            upper = upper.Max(p + extent);
        }
        _focusId = "system";
        _focusRadius = 10;
        _cameraTarget = (lower + upper) * .5f;
        _cameraYaw = .70f;
        _cameraPitch = .25f;
        var back = ViewDirection();
        var right = new Vector3(Mathf.Cos(_cameraYaw), 0, -Mathf.Sin(_cameraYaw));
        var up = back.Cross(right).Normalized();
        float lens = Mathf.Tan(Mathf.DegToRad(Camera.Fov) * .5f), aspect = (float)GetRenderSize().X / GetRenderSize().Y, required = 30;
        foreach (var body in bodies)
        {
            var p = body.Vector3("position") - _cameraTarget;
            float extent = (float)body.N("radius") * 1.2f;
            required = Math.Max(required, p.Dot(back) + (Math.Abs(p.Dot(right)) + extent) / (lens * aspect));
            required = Math.Max(required, p.Dot(back) + (Math.Abs(p.Dot(up)) + extent) / lens);
        }
        _cameraDistance = _overviewDistance = required * 1.1f;
        StartFocusTransition(instant);
        FocusChanged?.Invoke("system");
    }
    private void StartFocusTransition(bool instant)
    {
        _surfaceFocus = false;
        _focusDuration = 1.15f;
        SelectedSlot = -1;
        PlacingBuilding = false;
        Grid?.SetHover(-1);
        Grid?.SetSelected(-1);
        _focusFromPosition = Camera.GlobalPosition;
        _focusFromTarget = _cameraViewTarget;
        _focusToPosition = _cameraTarget + ViewDirection() * _cameraDistance;
        _focusElapsed = instant ? 1 : 0;
        if (instant)
            UpdateCameraTransform();
    }
    private void UpdateFocusTransition(double delta)
    {
        if (_spectatorActive || _focusElapsed >= 1)
            return;
        if (!double.IsFinite(delta)) return;
        _focusElapsed = Math.Min(1, _focusElapsed + (float)Math.Max(0, delta) / _focusDuration);
        float t = _focusElapsed * _focusElapsed * (3 - 2 * _focusElapsed);
        if (_surfaceFocus)
        {
            // Orbit around the globe instead of taking a chord through its interior.
            // The small outward arc leaves extra clearance during a large turn.
            Vector3 direction = _surfaceFocusAngle < .001f
                ? _focusFromPosition.Normalized().Lerp(_focusToPosition.Normalized(), t).Normalized()
                : _focusFromPosition.Normalized().Rotated(_surfaceFocusAxis, _surfaceFocusAngle * t);
            float radius = Mathf.Lerp(_focusFromPosition.Length(), _focusToPosition.Length(), t);
            float outward = WorldScale.EarthRadius * .14f * Mathf.Sin(_surfaceFocusAngle * .5f) * Mathf.Sin(Mathf.Pi * t);
            Camera.GlobalPosition = AvoidBodyCollision(_focusElapsed >= 1 ? _focusToPosition : direction * (radius + outward));
        }
        else
            Camera.GlobalPosition = AvoidBodyCollision(_focusFromPosition.Lerp(_focusToPosition, t));
        _cameraViewTarget = _focusFromTarget.Lerp(_cameraTarget, t);
        if (_surfaceFocus)
        {
            var back = (Camera.GlobalPosition - _cameraViewTarget).Normalized();
            var up = _surfaceFocusFromUp.Rotated(_surfaceFocusAxis, _surfaceFocusAngle * t);
            up = SafeSurfaceFocusUp(back, up).Rotated(back, _surfaceFocusRoll * t);
            Camera.LookAt(_cameraViewTarget, up);
            if (_focusElapsed >= 1) _surfaceFocus = false;
        }
        else
            Camera.LookAt(_cameraViewTarget, Vector3.Up);
        CameraVisualsChanged();
    }

    private void InterruptSurfaceFocusTransition()
    {
        if (!_surfaceFocus) return;
        if (IsCameraTransitionActive && Camera != null)
        {
            // Manual input continues from the actual on-screen pose, not the planned destination.
            _cameraTarget = _cameraViewTarget;
            var offset = Camera.GlobalPosition - _cameraTarget;
            _cameraDistance = Math.Max(.001f, offset.Length());
            var direction = offset / _cameraDistance;
            _cameraYaw = Mathf.Atan2(direction.X, direction.Z);
            _cameraPitch = Mathf.Asin(Mathf.Clamp(direction.Y, -.99999f, .99999f));
        }
        _surfaceFocus = false;
        _focusElapsed = 1;
    }

    private Vector3 SafeSurfaceFocusUp(Vector3 back, Vector3 preferred)
    {
        Vector3 up = preferred - back * preferred.Dot(back);
        if (up.LengthSquared() < .0001f)
            up = back.Cross(Camera.GlobalBasis.X);
        if (up.LengthSquared() < .0001f)
        {
            Vector3 reference = Math.Abs(back.Y) < .9f ? Vector3.Up : Vector3.Back;
            up = reference - back * reference.Dot(back);
        }
        return up.Normalized();
    }
    private Vector3 AvoidBodyCollision(Vector3 candidate)
    {
        foreach (var body in GetCelestialBodies())
        {
            var center = body.Vector3("position");
            var offset = candidate - center;
            float radius = (float)body.N("radius"), clearance = radius + Math.Max(.35f, radius * .1f);
            if (offset.Length() < clearance)
                candidate = center + (offset.LengthSquared() > .0001f ? offset.Normalized() : Vector3.Up) * clearance;
        }
        return candidate;
    }
    public Vector3 ScreenToSurface(Vector2 point)
    {
        if (_focusId != "earth" || _focusElapsed < 1)
            return Vector3.Zero;
        var pixel = LogicalToRender(point);
        var origin = Camera.ProjectRayOrigin(pixel) - Globe.GlobalPosition;
        var ray = Camera.ProjectRayNormal(pixel).Normalized();
        float b = origin.Dot(ray), c = origin.LengthSquared() - MathF.Pow(WorldScale.EarthRadius + WorldScale.MaxElevation + .02f, 2), d = b * b - c;
        if (d < 0)
            return Vector3.Zero;
        float distance = -b - Mathf.Sqrt(d);
        return distance < 0 ? Vector3.Zero : (Globe.GlobalBasis.Inverse() * (origin + ray * distance)).Normalized();
    }
    public bool IsAircraftViewActive() => _spectatorActive;
    public long GetSpectatorDroneUid() => _spectatorUid;
    public void BeginAircraftView(long uid)
    {
        InterruptSurfaceFocusTransition();
        _spectatorActive = true;
        _spectatorUid = uid;
    }
    public void ApplyAircraftCamera(Transform3D pose, float fov = 72, float nearClip = .015f)
    {
        if (!_spectatorActive)
            return;
        Camera.GlobalTransform = pose;
        Camera.Fov = fov;
        Camera.Near = nearClip;
        Camera.Far = Math.Max(420, Camera.Far);
        _cameraViewTarget = pose.Origin - pose.Basis.Z * 10;
        CameraVisualsChanged();
    }
    private void RequestStrategicCamera()
    {
        if (!_spectatorActive)
            return;
        StrategicCameraRequested?.Invoke();
        _spectatorActive = false;
        _spectatorUid = -1;
    }
    public DataMap CaptureCameraState() => new() { ["transform"] = Camera.GlobalTransform, ["fov"] = Camera.Fov, ["near"] = Camera.Near, ["far"] = Camera.Far, ["focus_id"] = _focusId, ["target"] = _cameraTarget, ["view_target"] = _cameraViewTarget, ["focus_radius"] = _focusRadius, ["overview_distance"] = _overviewDistance, ["yaw"] = _cameraYaw, ["pitch"] = _cameraPitch, ["distance"] = _cameraDistance, ["focus_elapsed"] = _focusElapsed, ["focus_from_position"] = _focusFromPosition, ["focus_from_target"] = _focusFromTarget, ["focus_to_position"] = _focusToPosition, ["surface_focus"] = _surfaceFocus, ["focus_duration"] = _focusDuration, ["surface_focus_axis"] = _surfaceFocusAxis, ["surface_focus_angle"] = _surfaceFocusAngle, ["surface_focus_roll"] = _surfaceFocusRoll, ["surface_focus_from_up"] = _surfaceFocusFromUp };
    public void RestoreCameraState(DataMap state)
    {
        _spectatorActive = false;
        _spectatorUid = -1;
        if (state.Count == 0)
            return;
        string before = _focusId;
        _focusId = state.S("focus_id", "earth");
        _cameraTarget = state.Vector3("target");
        _cameraViewTarget = state.Vector3("view_target");
        _focusRadius = (float)state.N("focus_radius", WorldScale.EarthRadius);
        _overviewDistance = (float)state.N("overview_distance");
        _cameraYaw = (float)state.N("yaw");
        _cameraPitch = (float)state.N("pitch");
        _cameraDistance = (float)state.N("distance", WorldScale.DefaultCameraDistance);
        _focusElapsed = (float)state.N("focus_elapsed", 1);
        _focusFromPosition = state.Vector3("focus_from_position");
        _focusFromTarget = state.Vector3("focus_from_target");
        _focusToPosition = state.Vector3("focus_to_position");
        _surfaceFocus = state.B("surface_focus") && _focusElapsed < 1;
        _focusDuration = (float)state.N("focus_duration", 1.15);
        if (!float.IsFinite(_focusDuration) || _focusDuration <= 0) _focusDuration = 1.15f;
        _surfaceFocusAxis = state.Vector3("surface_focus_axis", Vector3.Up);
        _surfaceFocusAngle = (float)state.N("surface_focus_angle");
        _surfaceFocusRoll = (float)state.N("surface_focus_roll");
        _surfaceFocusFromUp = state.Vector3("surface_focus_from_up", Vector3.Up);
        Camera.GlobalTransform = state.Get<Transform3D>("transform", Camera.GlobalTransform);
        Camera.Fov = (float)state.N("fov", 36);
        Camera.Near = (float)state.N("near", .1);
        Camera.Far = (float)state.N("far", 420);
        CameraVisualsChanged();
        if (before != _focusId)
            FocusChanged?.Invoke(_focusId);
    }
    public DataMap CaptureCelestialState() => new() { ["elapsed"] = _celestial.Elapsed, ["earth_rotation_y"] = Globe.Rotation.Y };
    public bool RestoreCelestialState(DataMap state)
    {
        double elapsed = state.N("elapsed"), angle = state.N("earth_rotation_y", Mathf.DegToRad(12));
        if (!double.IsFinite(elapsed) || elapsed < 0 || !double.IsFinite(angle))
            return false;
        Globe.Rotation = new(0, (float)angle, 0);
        _spatialRevision++;
        _celestial.RestoreClock(elapsed);
        return true;
    }
    public Transform3D GetBodySurfaceTransform(string id) => id == "earth" ? Globe.GlobalTransform : _celestial.GetTransform(id);
    public DataMap PickNavigationTarget(Vector2 point)
    {
        if (_spectatorActive || !point.IsFinite() || !new Rect2(Vector2.Zero, ViewSize).HasPoint(point))
            return new();
        var origin = Camera.ProjectRayOrigin(LogicalToRender(point));
        var ray = ProjectViewRay(point);
        DataMap result = new();
        float nearest = Camera.Far;
        foreach (var body in GetCelestialBodies())
        {
            string id = body.S("id");
            float radius = (float)body.N("radius");
            if (id == "moon")
                radius *= 1.003f;
            if (id == "venus")
                radius = 3.855f;
            float hit = NavigationSphereHit(origin, ray, body.Vector3("position"), radius);
            if (hit >= Camera.Near && hit < nearest)
            {
                nearest = hit;
                result = id == _focusId ? new() : new()
                {
                    ["kind"] = "body",
                    ["id"] = id,
                    ["distance"] = hit,
                    ["position"] = body.Vector3("position")
                };
            }
        }
        var satellite = PickResearchSatellite(origin, ray, nearest);
        return satellite.Count > 0 ? satellite : result;
    }
    public static float NavigationSphereHit(Vector3 origin, Vector3 ray, Vector3 center, float radius)
    {
        var offset = origin - center;
        float b = offset.Dot(ray), d = b * b - offset.LengthSquared() + radius * radius;
        if (d < 0)
            return -1;
        float t = -b - MathF.Sqrt(d);
        return t >= 0 ? t : -1;
    }
    public void SetNavigationHover(DataMap target)
    {
        _navigationHover = target;
        _highlight ??= new WorldHighlight(SpaceRoot, Camera);
        string id = target.S("id");
        MeshInstance3D? surface = id == "earth" ? _earth.Root.GetNode<MeshInstance3D>("EarthTerrain")
            : id == "research_satellite" && HasOrbitalResearchStation ? _researchStation.Model.GetNode<MeshInstance3D>("CoreBus")
            : _celestial.NavigationSurface(id);
        _highlight.SetTarget(surface);
    }
    public void ClearNavigationHover()
    {
        _navigationHover = new();
        _highlight?.SetTarget(null);
    }
    public DataMap GetNavigationHover() => _navigationHover;
    private void UpdateNavigationHighlight(float delta) => _highlight?.Update(delta, ViewSize);
}
