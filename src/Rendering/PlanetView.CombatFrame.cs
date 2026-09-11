using Godot;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    // Called on the main thread before simulation jobs. Workers receive only the
    // value transform, never the Globe Node or a callback into the scene tree.
    public bool TryGetCoordinateFrame(out Transform3D localToWorld)
    {
        CacheSurfaceTransform();
        localToWorld = _cachedSurfaceTransform;
        return true;
    }
}
