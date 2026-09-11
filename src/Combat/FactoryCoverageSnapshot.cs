using System;
using System.Collections.Generic;
using System.Linq;

namespace Earthward.Combat;

/// <summary>
/// A factory's geometric capability envelope, in world units and radians.
/// PatrolRadius/OuterRange are surface arc lengths. PursuitRadius/AttackRadius
/// are distances from the Earth's center, never arc lengths. Current sight lines,
/// armor immunity and temporary launch/repair state remain part of combat targeting.
/// </summary>
public sealed record FactoryCoverageBand(
    double PatrolRadius,
    double OuterRange,
    double WeaponRange,
    double PursuitRadius,
    double AttackRadius,
    double AngleRadians,
    double LargeTargetWeaponRange,
    double LargeTargetAttackRadius)
{
    public double MaximumPursuitAltitude => Math.Max(0, PursuitRadius - CombatScale.EarthRadius);
    public double MaximumCombatAltitude => Math.Max(0, AttackRadius - CombatScale.EarthRadius);
    public double MaximumLargeTargetCombatAltitude => Math.Max(0, LargeTargetAttackRadius - CombatScale.EarthRadius);
}

/// <summary>An immutable union of the factory's distinct live coverage profiles, or its planned production profiles.</summary>
public sealed record FactoryCoverageSnapshot
{
    public long SiteId { get; }
    public string Kind { get; }
    public bool Preview { get; }
    public int AircraftCount { get; }
    public IReadOnlyList<FactoryCoverageBand> Bands { get; }

    public FactoryCoverageSnapshot(long siteId, string kind, bool preview, int aircraftCount, IEnumerable<FactoryCoverageBand> bands)
    {
        SiteId = siteId;
        Kind = kind;
        Preview = preview;
        AircraftCount = aircraftCount;
        Bands = Array.AsReadOnly(bands.ToArray());
    }
}
