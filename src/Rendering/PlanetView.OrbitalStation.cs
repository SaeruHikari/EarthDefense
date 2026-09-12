using Godot;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    public bool HasOrbitalResearchStation => _researchStation != null && _researchStation.IsDeployed && GodotObject.IsInstanceValid(_researchStation.Model);
    public bool HasOrbitalResearchOrbit => _researchStation != null && GodotObject.IsInstanceValid(_researchStation.Root) && _researchStation.Root.IsVisibleInTree();
    public Vector3 GetOrbitalResearchStationWorldPosition()
        => HasOrbitalResearchOrbit ? _researchStation.Model.GlobalPosition : Vector3.Zero;
    public Vector2 GetOrbitalResearchStationScreenPosition()
        => HasOrbitalResearchOrbit ? GetSpaceScreenPosition(_researchStation.Model.GlobalPosition) : new(-1000, -1000);
}
