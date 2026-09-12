using Godot;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    public bool HasOrbitalResearchStation => _researchStation != null && GodotObject.IsInstanceValid(_researchStation.Model);
    public Vector3 GetOrbitalResearchStationWorldPosition()
        => HasOrbitalResearchStation ? _researchStation.Model.GlobalPosition : Vector3.Zero;
    public Vector2 GetOrbitalResearchStationScreenPosition()
        => HasOrbitalResearchStation ? GetSpaceScreenPosition(_researchStation.Model.GlobalPosition) : new(-1000, -1000);
}
