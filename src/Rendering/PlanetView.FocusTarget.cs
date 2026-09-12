using Godot;
namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    /// <summary>Explicit user navigation; uses the existing 1.15 second strategic-camera transition.</summary>
    public bool FocusPoint(Vector3 worldPosition, string targetId = "alert")
    {
        if (!worldPosition.IsFinite() || worldPosition.LengthSquared() < .001f || Camera == null) return false;
        RequestStrategicCamera();
        _focusId = "alert:" + targetId;
        _focusRadius = 2;
        _cameraTarget = worldPosition;
        var outward = worldPosition.Normalized();
        _cameraYaw = Mathf.Atan2(outward.X, outward.Z);
        _cameraPitch = Mathf.Asin(Mathf.Clamp(outward.Y, -.985f, .985f));
        _cameraDistance = 13;
        StartFocusTransition(false);
        FocusChanged?.Invoke(_focusId);
        return true;
    }

    /// <summary>
    /// Reveal a local ground coordinate along a smooth exterior camera arc. The animation
    /// uses UI time while the simulation is paused; a zero duration applies the pose immediately.
    /// </summary>
    public bool RotateEarthToDirection(Vector3 localNormal, float screenFractionX = .5f, double durationSeconds = 1.4)
    {
        if (!localNormal.IsFinite() || localNormal.LengthSquared() < .001f || Camera == null) return false;
        RequestStrategicCamera();
        var normal = (Globe.GlobalBasis * localNormal.Normalized()).Normalized();
        _focusId = "earth";
        _focusRadius = WorldScale.EarthRadius;
        _cameraTarget = Vector3.Zero;
        _cameraYaw = Mathf.Atan2(normal.X, normal.Z);
        _cameraPitch = Mathf.Asin(Mathf.Clamp(normal.Y, -.985f, .985f));
        _cameraDistance = Mathf.Clamp(DefaultCameraDistance * .72f, WorldScale.MinCameraDistance, WorldScale.MaxCameraDistance);
        if (float.IsFinite(screenFractionX) && Math.Abs(screenFractionX - .5f) > .001f)
        {
            Vector3 view = ViewDirection();
            Vector3 right = Vector3.Up.Cross(view).Normalized();
            float depth = _cameraDistance - normal.Dot(view) * WorldScale.EarthRadius;
            float halfWidth = depth * Mathf.Tan(Mathf.DegToRad(Camera.Fov) * .5f) * ViewSize.X / Math.Max(1, ViewSize.Y);
            _cameraTarget = right * ((1 - 2 * Mathf.Clamp(screenFractionX, .1f, .9f)) * halfWidth);
        }
        StartFocusTransition(durationSeconds <= 0);
        if (_focusElapsed < 1)
        {
            _surfaceFocus = true;
            _focusDuration = (float)(double.IsFinite(durationSeconds) ? Math.Clamp(durationSeconds, .15, 8) : 1.4);
            _surfaceFocusFromUp = Camera.GlobalBasis.Y.Normalized();
            Vector3 from = _focusFromPosition.Normalized(), to = _focusToPosition.Normalized();
            _surfaceFocusAngle = Mathf.Acos(Mathf.Clamp(from.Dot(to), -1, 1));
            Vector3 axis = from.Cross(to);
            if (axis.LengthSquared() < .000001f)
            {
                // Antipodal vectors have no unique great circle; use the camera's right-hand
                // tangent so a backside hit produces a stable lateral orbit rather than a flip.
                Vector3 tangent = Camera.GlobalBasis.X - from * Camera.GlobalBasis.X.Dot(from);
                if (tangent.LengthSquared() < .000001f)
                    tangent = Math.Abs(from.Y) < .9f ? Vector3.Up.Cross(from) : Vector3.Back.Cross(from);
                axis = from.Cross(tangent);
            }
            _surfaceFocusAxis = axis.Normalized();
            Vector3 finalBack = (_focusToPosition - _cameraTarget).Normalized();
            Vector3 transportedUp = SafeSurfaceFocusUp(finalBack,
                _surfaceFocusFromUp.Rotated(_surfaceFocusAxis, _surfaceFocusAngle));
            Vector3 upright = SafeSurfaceFocusUp(finalBack, Vector3.Up);
            _surfaceFocusRoll = Mathf.Atan2(finalBack.Dot(transportedUp.Cross(upright)), transportedUp.Dot(upright));
        }
        FocusChanged?.Invoke("earth");
        return true;
    }
}
