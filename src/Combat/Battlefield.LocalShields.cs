using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private readonly Dictionary<long, DataMap> _localShields = new();
    private readonly List<DataMap> _localShieldView = new();
    private long _localShieldRevision = -1;
    private DataMap _localShieldStats = new();
    private DefenseState? _localShieldOwner;
    private double _localShieldSyncClock = double.NaN, _localShieldShellRadius, _localShieldCosine;
    private long _localShieldSpatialRevision = long.MinValue;

    private void BindLocalShieldCallbacks()
    {
        if (Game == null || ReferenceEquals(_localShieldOwner, Game))
            return;
        if (_localShieldOwner != null)
        {
            if (ReferenceEquals(_localShieldOwner.RechargeLocalShields?.Target, this))
                _localShieldOwner.RechargeLocalShields = null;
        }
        _localShieldOwner = Game;
        _localShieldRevision = -1;
        Game.RechargeLocalShields = RechargeLocalShieldBudget;
    }
    private void SyncLocalShields()
    {
        if (Game == null)
            return;
        long spatial = Surface?.SpatialRevision ?? 0;
        if (spatial >= 0 && ReferenceEquals(_localShieldOwner, Game) && _localShieldSyncClock == Clock && _localShieldSpatialRevision == spatial && _localShieldRevision == Game.FactoryStatsRevision)
            return;
        BindLocalShieldCallbacks();
        if (_localShieldRevision != Game.FactoryStatsRevision)
        {
            _localShieldRevision = Game.FactoryStatsRevision;
            _localShieldStats = Game.ShieldFacilityStats();
        }
        double capacity = Math.Max(.001, C.N(_localShieldStats, "capacity", 100));
        double radius = Math.Max(.001, C.N(_localShieldStats, "surface_radius", 5));
        double altitude = Math.Max(.001, C.N(_localShieldStats, "altitude", WorldScale.ShieldAltitude));
        _localShieldShellRadius = CombatScale.EarthRadius + altitude;
        _localShieldCosine = Math.Cos(Math.Min(Math.PI, radius / CombatScale.EarthRadius));
        var sites = Surface?.GetShieldSites() ?? _localShields.Values.ToArray();
        _localShieldSyncClock = Clock;
        _localShieldSpatialRevision = Surface?.SpatialRevision ?? 0;
        if (sites.Count == 0 && _localShields.Count == 0)
            return;
        bool membershipChanged = _localShieldView.Count != _localShields.Count;
        var seen = new HashSet<long>();
        foreach (var site in sites)
        {
            long id = C.L(site, "site_id", -1);
            var normal = C.V(site, "normal");
            if (id < 0 || C.S(site, "kind", "shield") != "shield" || !normal.IsFinite() || normal.LengthSquared() < .001 || !seen.Add(id))
                continue;
            if (!_localShields.TryGetValue(id, out var tower))
            {
                _localShields[id] = tower = new() { ["site_id"] = id, ["hp"] = capacity, ["max_hp"] = capacity, ["hit"] = 0d, ["hold_until"] = 0d, ["break_ready_at"] = 0d };
                membershipChanged = true;
            }
            double previousMax = C.N(tower, "max_hp", capacity);
            if (!C.Near(previousMax, capacity))
                tower["hp"] = capacity * C.Clamp(C.N(tower, "hp") / Math.Max(.001, previousMax), 0, 1);
            tower["max_hp"] = capacity;
            tower["normal"] = normal.Normalized();
            tower["world_normal"] = SurfaceToSpace(normal, 0).Normalized();
            tower["surface_radius"] = radius;
            tower["altitude"] = altitude;
            tower["shield_radius"] = CombatScale.EarthRadius + altitude;
            tower["angle_radians"] = Math.Min(Math.PI, radius / CombatScale.EarthRadius);
            tower["regeneration"] = Math.Max(0, C.N(_localShieldStats, "regeneration"));
        }
        foreach (var id in _localShields.Keys.ToArray())
            if (!seen.Contains(id))
                membershipChanged |= _localShields.Remove(id);
        if (membershipChanged)
        {
            _localShieldView.Clear();
            _localShieldView.AddRange(_localShields.Values);
            _localShieldView.Sort((a, b) => C.L(a, "site_id").CompareTo(C.L(b, "site_id")));
        }
    }
    public IReadOnlyList<DataMap> GetLocalShieldState()
    {
        SyncLocalShields();
        return _localShieldView;
    }
    public DataMap GetLocalShieldSummary()
    {
        SyncLocalShields();
        double hp = 0, capacity = 0;
        int active = 0;
        foreach (var tower in _localShieldView)
        {
            hp += C.N(tower, "hp");
            capacity += C.N(tower, "max_hp");
            if (C.N(tower, "hp") > 0)
                active++;
        }
        return new()
        {
            ["hp"] = hp,
            ["capacity"] = capacity,
            ["count"] = _localShieldView.Count,
            ["active"] = active,
            ["build_limit"] = Game?.LocalShieldBuildLimit ?? 0,
            ["build_cooldown_remaining"] = Game?.LocalShieldBuildCooldownRemaining ?? 0,
            ["earth_repair_rate"] = Game?.LocalShieldEarthRepairRate() ?? 0
        };
    }
    private void UpdateLocalShields(double dt)
    {
        SyncLocalShields();
        foreach (var tower in _localShieldView)
        {
            tower["hit"] = Math.Max(0, C.N(tower, "hit") - Math.Max(0, dt));
            if (Active && !Dead)
                tower["hp"] = Math.Min(C.N(tower, "max_hp"), C.N(tower, "hp") + C.N(tower, "regeneration") * Math.Max(0, dt));
        }
    }
    private double RechargeLocalShieldBudget(double budget)
    {
        if (!double.IsFinite(budget) || budget <= 0)
            return 0;
        SyncLocalShields();
        double remaining = budget;
        // One fixed reward budget is shared, never cloned for every overlapping tower.
        foreach (var tower in _localShieldView.OrderBy(s => C.N(s, "hp") / Math.Max(.001, C.N(s, "max_hp"))))
        {
            double amount = Math.Min(remaining, Math.Max(0, C.N(tower, "max_hp") - C.N(tower, "hp")));
            tower["hp"] = C.N(tower, "hp") + amount;
            remaining -= amount;
            if (remaining <= .000001)
                break;
        }
        return budget - remaining;
    }
    private DataMap? ShieldCoveringPoint(Vector3 at)
    {
        var normal = at.Normalized();
        double facing = double.NegativeInfinity;
        DataMap? selected = null;
        foreach (var tower in _localShieldView)
        {
            if (C.N(tower, "hp") <= 0)
                continue;
            double dot = C.V(tower, "world_normal").Dot(normal);
            if (dot < _localShieldCosine || dot <= facing)
                continue;
            facing = dot;
            selected = tower;
        }
        return selected;
    }
    private double AbsorbLocalShield(DataMap tower, double damage, Vector3 at)
    {
        if (damage <= 0 || C.N(tower, "hp") <= 0)
            return damage;
        double old = C.N(tower, "hp"), absorbed = Math.Min(old, damage);
        if (Clock >= C.N(tower, "hold_until"))
            tower["hp"] = Math.Max(0, old - absorbed);
        tower["hit"] = .22;
        if (C.N(tower, "hp") <= 0 && Clock >= C.N(tower, "break_ready_at") && C.N(_localShieldStats, "break_recovery_fraction") > 0)
        {
            tower["hp"] = C.N(tower, "max_hp") * C.N(_localShieldStats, "break_recovery_fraction");
            tower["hold_until"] = Clock + C.N(_localShieldStats, "break_hold_seconds", 2);
            tower["break_ready_at"] = Clock + C.N(_localShieldStats, "break_cooldown", 30);
        }
        AddBurst(C.Scale(at.Normalized(), C.N(tower, "shield_radius")), CombatScale.Cyan, 12);
        return damage - absorbed;
    }
    private double AbsorbPlanetaryImpact(double damage, Vector3 at)
    {
        // Also covers attacks originating inside the dome, including a parked enemy.
        // Such attacks cannot intersect its outer shell, but still strike defended ground.
        if (_localShieldRevision != Game.FactoryStatsRevision)
            SyncLocalShields();
        var tower = ShieldCoveringPoint(at);
        return tower == null ? damage : AbsorbLocalShield(tower, damage, at);
    }
    private static bool ValidateLocalShieldSnapshot(DataMap towers)
    {
        foreach (var (key, value) in towers)
        {
            if (!long.TryParse(key, out long id) || id < 0 || value is not DataMap tower || !CombatSnapshotCodec.HasFields(tower, "site_id:int normal:v3 world_normal:v3 hp:number max_hp:number surface_radius:number altitude:number shield_radius:number angle_radians:number regeneration:number hit:number hold_until:number break_ready_at:number") || C.L(tower, "site_id") != id)
                return false;
            if (!Nonnegative(tower.Value("hp"), 1e300) || !Nonnegative(tower.Value("max_hp"), 1e300) || C.N(tower, "max_hp") <= 0 || C.N(tower, "hp") > C.N(tower, "max_hp") || !Nonnegative(tower.Value("regeneration"), 1e300))
                return false;
            if (Math.Abs(C.V(tower, "normal").Length() - 1) > .001 || Math.Abs(C.V(tower, "world_normal").Length() - 1) > .001 || !Nonnegative(tower.Value("surface_radius"), 10000) || C.N(tower, "surface_radius") <= 0 || !Nonnegative(tower.Value("altitude"), 1000) || C.N(tower, "altitude") <= 0)
                return false;
            if (Math.Abs(C.N(tower, "shield_radius") - CombatScale.EarthRadius - C.N(tower, "altitude")) > .001 || Math.Abs(C.N(tower, "angle_radians") - Math.Min(Math.PI, C.N(tower, "surface_radius") / CombatScale.EarthRadius)) > .001 || !Nonnegative(tower.Value("hit"), .220001) || !Nonnegative(tower.Value("hold_until"), 1e12) || !Nonnegative(tower.Value("break_ready_at"), 1e12))
                return false;
        }
        return true;
    }
    private bool InterceptLocalShieldSegment(DataMap attack, Vector3 from, Vector3 to, double beforeHit, string damageKey, out Vector3 impact)
    {
        impact = Vector3.Zero;
        // Every current tower uses the same shell altitude. Parked enemies are
        // inside it: reject once here instead of scanning every tower per shot.
        if (_localShieldView.Count == 0 || C.B(attack, "local_shield_checked") || C.N(attack, damageKey) <= 0 || from.LengthSquared() <= _localShieldShellRadius * _localShieldShellRadius || from.Dot(to - from) >= 0)
            return false;
        double contact = CombatGeometry.SphereHitFraction(from, to, Vector3.Zero, _localShieldShellRadius);
        if (double.IsPositiveInfinity(contact) || contact > beforeHit)
            return false;
        impact = from.Lerp(to, (float)contact);
        var selected = ShieldCoveringPoint(impact);
        if (selected == null)
            return false;
        attack[damageKey] = AbsorbLocalShield(selected, C.N(attack, damageKey), impact);
        attack["local_shield_checked"] = true;
        return C.N(attack, damageKey) <= .000001;
    }
}
