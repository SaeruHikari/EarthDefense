using System.Collections.Generic;
using System.Linq;

namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private sealed record CoverageCacheEntry(int AircraftCount, bool Preview, int Capacity, HashSet<Profile> Profiles, FactoryCoverageSnapshot Snapshot);
    private readonly Dictionary<long, CoverageCacheEntry> _coverageSnapshots = new();

    /// <summary>
    /// Query on selection and at the UI's capped refresh rate (at most 4 Hz), not per
    /// render frame. One roster pass reads cached profiles; no per-aircraft catalog
    /// resolution or targeting/physics work is performed. Stable results reuse the
    /// immutable snapshot, while roster and research changes are visible immediately.
    /// Living launching/returning/repairing aircraft retain their life configuration;
    /// Preview means no living aircraft, so the actual production plan is shown.
    /// </summary>
    public FactoryCoverageSnapshot? GetFactoryCoverage(long siteId)
    {
        if (Game == null || siteId < 0)
            return null;
        RefreshConfiguration();
        SyncFleet();
        if (!Factories.TryGetValue(siteId, out var factory) || C.S(factory, "kind") is not ("interceptor" or "missile" or "laser"))
        {
            _coverageSnapshots.Remove(siteId);
            return null;
        }
        string kind = C.S(factory, "kind");
        var profiles = new HashSet<Profile>();
        int count = 0;
        foreach (var aircraft in Drones)
            if (C.L(aircraft, "factory_site_id") == siteId && C.S(aircraft, "kind") == kind && C.N(aircraft, "hp") > 0)
            {
                count++;
                profiles.Add(GetProfile(aircraft));
            }
        bool preview = count == 0;
        int capacity = C.I(factory, "capacity");
        _coverageSnapshots.TryGetValue(siteId, out var old);
        // The selected plan can have thousands of berths. Settings and loadout edits
        // clear this cache through FactoryStatsRevision; do not rescan an unchanged plan.
        if (preview && old is { Preview: true } && old.Capacity == capacity && old.Snapshot.Kind == kind)
            return old.Snapshot;
        if (preview)
            foreach (var row in ProductionPlan(factory))
                profiles.Add(GetLoadoutProfile(kind, siteId, row.Berth, row.Frame));
        if (old != null && old.AircraftCount == count && old.Preview == preview && old.Profiles.SetEquals(profiles))
            return old.Snapshot;
        var bands = profiles.Select(CoverageBand).Distinct()
            .OrderBy(b => b.AngleRadians).ThenBy(b => b.AttackRadius).ThenBy(b => b.PatrolRadius)
            .ThenBy(b => b.OuterRange).ThenBy(b => b.WeaponRange).ThenBy(b => b.LargeTargetAttackRadius).ToArray();
        var snapshot = new FactoryCoverageSnapshot(siteId, kind, preview, count, bands);
        _coverageSnapshots[siteId] = new(count, preview, capacity, profiles, snapshot);
        return snapshot;
    }

    private FactoryCoverageBand CoverageBand(Profile profile) => new(
        C.N(profile.Patrol, "patrol_radius"),
        C.N(profile.Patrol, "patrol_outer_range"),
        TargetRangeForProfile(profile, false),
        profile.Radial,
        MaximumTargetRadius(profile, false),
        profile.Angle,
        TargetRangeForProfile(profile, true),
        MaximumTargetRadius(profile, true));
}
