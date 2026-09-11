namespace Earthward;

/// <summary>Earth size is independent of aircraft, buildings and local weapon distances.</summary>
public static class WorldScale
{
    public const float LegacyEarthRadius = 4f;
    public const float PreviousEarthRadius = 8f;
    public const float EarthSizeMultiplier = 4f;
    public const float EarthRadius = LegacyEarthRadius * EarthSizeMultiplier;
    public const float EarthRadiusDelta = EarthRadius - LegacyEarthRadius;
    public const float TerrainScale = 2f, MaxElevation = .048f;
    public const float CloudRadius = EarthRadius + .24f;
    public const float AtmosphereRadius = EarthRadius + 1.00f;
    public const float AirProfileScale = 3.4f;
    public const float ShieldAltitude = 1.28f;
    public const float EarthCollisionRadius = EarthRadius + .07f;
    public const float DroneAltitude = .70f;
    public const float DefaultCameraDistance = 16.5f * EarthSizeMultiplier;
    public const float MinCameraDistance = EarthRadius + 3.2f, MaxCameraDistance = 160f;
    public const float SpawnMinRadius = EarthRadius + 4f, SpawnMaxRadius = EarthRadius + 5.2f;
    public const float RetreatExitRadius = EarthRadius + 8f;
    // Two refinements preserve the radius-4 hexagon size on the four-times-wider world.
    public const int BakedGridLevel = 4, GridLevel = 6;
    public const int DefaultGridCellCount = 40962;
    public const float InterplanetaryDistanceMultiplier = 2f;
    public const float CruiserStandoff = EarthRadius + .9f, BossStandoff = EarthRadius + 1.3f;
}
