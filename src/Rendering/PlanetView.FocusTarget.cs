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

    /// <summary>Rotate the viewing direction, not the simulated Earth, to reveal a local ground coordinate.</summary>
    public bool RotateEarthToDirection(Vector3 localNormal)
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
        StartFocusTransition(false);
        FocusChanged?.Invoke("earth");
        return true;
    }
}
