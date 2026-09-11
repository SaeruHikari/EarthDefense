using Godot;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private long _cachedSurfaceRevision = -1;
    private long _factoryLaunchRevision = -1;
    private bool _factorySitesDirty = true;
    private readonly List<Earthward.Domain.DataMap> _factorySiteCache = new();
    private readonly List<(Vector3 Position, Vector3 Direction)> _factoryLaunchLocals = new();
    private Transform3D _cachedSurfaceTransform, _cachedSurfaceInverse;
    private bool _projectionCached;
    private Transform3D _cachedView;
    private Projection _cachedProjection;
    private Vector3 _cachedCameraOrigin;
    private float _cachedCameraNear;
    private (Vector3 Center, float Radius)[]? _occluders;

    private void CacheSurfaceTransform()
    {
        if (_cachedSurfaceRevision == SpatialRevision) return;
        _cachedSurfaceTransform = Globe.GlobalTransform;
        _cachedSurfaceInverse = _cachedSurfaceTransform.AffineInverse();
        _cachedSurfaceRevision = SpatialRevision;
    }
    private void CacheProjection()
    {
        if (_projectionCached) return;
        var transform = Camera.GetCameraTransform();
        _cachedCameraOrigin = transform.Origin;
        _cachedView = transform.AffineInverse();
        _cachedProjection = Camera.GetCameraProjection();
        _cachedCameraNear = Camera.Near;
        _projectionCached = true;
    }
    private Vector3 CachedSurfaceToSpace(Vector3 normal, double altitude)
    {
        if (!normal.IsFinite() || normal.LengthSquared() < .0001f) return Vector3.Zero;
        CacheSurfaceTransform();
        return _cachedSurfaceTransform * (normal.Normalized() * (WorldScale.EarthRadius + (float)altitude));
    }
    private Vector3 CachedSpaceToSurface(Vector3 position)
    {
        if (!position.IsFinite()) return Vector3.Zero;
        CacheSurfaceTransform();
        return (_cachedSurfaceInverse * position).Normalized();
    }
    private Vector2 CachedScreenPosition(Vector3 position)
    {
        if (!position.IsFinite()) return new(-1000, -1000);
        CacheProjection();
        var p = _cachedView * position;
        var clip = _cachedProjection * new Vector4(p.X, p.Y, p.Z, 1);
        if (Math.Abs(clip.W) < .000001f) return new(-1000, -1000);
        return new((clip.X / clip.W * .5f + .5f) * ViewSize.X, (.5f - clip.Y / clip.W * .5f) * ViewSize.Y);
    }
    private bool CachedSpaceVisible(Vector3 position)
    {
        if (!position.IsFinite()) return false;
        CacheProjection();
        if ((_cachedView * position).Z > -_cachedCameraNear) return false;
        var offset = position - _cachedCameraOrigin;
        float distance = offset.Length();
        if (distance < .0001f) return false;
        var ray = offset / distance;
        if (_occluders == null)
        {
            var list = new List<(Vector3, float)> { (Vector3.Zero, WorldScale.EarthCollisionRadius) };
            if (_celestial != null)
                foreach (var body in _celestial.Bodies)
                {
                    float radius = (float)body.N("radius");
                    if (body.S("id") == "moon") radius *= 1.007f;
                    else if (body.S("id") == "venus") radius = 3.855f;
                    list.Add((body.Vector3("position"), radius));
                }
            _occluders = list.ToArray();
        }
        foreach (var body in _occluders)
        {
            float hit = NavigationSphereHit(_cachedCameraOrigin, ray, body.Center, body.Radius);
            if (hit >= 0 && hit < distance - .0001f) return false;
        }
        return true;
    }
}
