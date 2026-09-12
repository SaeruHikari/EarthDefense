using Godot;
using Earthward.Domain;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    public bool HasOrbitalResearchStation => _researchStation != null && _researchStation.IsDeployed && GodotObject.IsInstanceValid(_researchStation.Model) && _researchStation.Model.IsVisibleInTree();
    public bool HasOrbitalResearchOrbit => _researchStation != null && GodotObject.IsInstanceValid(_researchStation.Root) && _researchStation.Root.IsVisibleInTree();
    public Vector3 GetOrbitalResearchStationWorldPosition()
        => HasOrbitalResearchOrbit ? _researchStation.Model.GlobalPosition : Vector3.Zero;
    public Vector2 GetOrbitalResearchStationScreenPosition()
        => HasOrbitalResearchOrbit ? GetSpaceScreenPosition(_researchStation.Model.GlobalPosition) : new(-1000, -1000);

    private DataMap PickResearchSatellite(Vector3 origin, Vector3 ray, float nearest)
    {
        if (!HasOrbitalResearchStation) return new();
        Vector3 position = _researchStation.Model.GlobalPosition;
        if (!IsSpaceVisible(position)) return new();
        // Give the small spacecraft a usable click target at overview zoom.
        float pixels = origin.DistanceTo(position) * 2 * Mathf.Tan(Mathf.DegToRad(Camera.Fov) * .5f) / Math.Max(1, ViewSize.Y);
        float radius = Math.Max(.95f, pixels * 9);
        float hit = NavigationSphereHit(origin, ray, position, radius);
        return hit >= Camera.Near && hit < nearest
            ? new() { ["kind"] = "research_satellite", ["id"] = "research_satellite", ["position"] = position, ["distance"] = hit }
            : new();
    }
}
