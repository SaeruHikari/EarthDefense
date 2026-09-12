using System;

namespace Earthward.Combat;

/// <summary>
/// Immutable presentation data for one local shield generator.  The combat
/// simulation keeps the mutable tower state private; the HUD receives this
/// small snapshot so selecting a tower never exposes or mutates simulation
/// dictionaries.
/// </summary>
public sealed record LocalShieldCoverageSnapshot(
    long SiteId,
    double Hp,
    double Capacity,
    double SurfaceRadius,
    double Altitude,
    double Regeneration,
    double EarthRepairRatePerGenerator)
{
    public double Integrity => Math.Clamp(Hp / Math.Max(.001, Capacity), 0, 1);
    public double AngleRadians => Math.Min(Math.PI, Math.Max(0, SurfaceRadius) / CombatScale.EarthRadius);
    public double ShieldRadius => CombatScale.EarthRadius + Math.Max(0, Altitude);
}
