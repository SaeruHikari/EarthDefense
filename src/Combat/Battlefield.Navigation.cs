using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private void RebuildTargets()
    {
        _nearestTargetDirty = true;
        _targets.Clear();
        _targets.AddRange(Enemies);
        if (C.B(_globalWeapons, "mothership_assault_unlocked"))
            foreach (var m in Motherships.Values)
                if (C.N(m, "hp") > 0)
                    _targets.Add(m);
        _enemyById.Clear();
        foreach (var e in _targets)
            _enemyById[C.L(e, "uid")] = e;
        _sectorsDirty = true;
    }
    private DataMap? EnemyByUid(long uid) => _enemyById.TryGetValue(uid, out var e) && C.N(e, "hp") > 0 ? e : null;
    public bool AssignmentValid(DataMap d, DataMap? e)
    {
        if (e == null || C.N(e, "hp") <= 0 || C.S(e, "phase") == "retreat")
            return false;
        var p = GetProfile(d);
        if (EnemyArmor.Multiplier(e, EnemyArmor.Family(C.S(d, "kind")), p.Weapons, Clock) <= 0)
            return false;
        var position = C.V(e, "space_position");
        return position.Length() <= MaximumTargetRadius(p, C.Large(e)) && Home(d).Dot(position.Normalized()) >= p.Cosine && CombatGeometry.HasLineOfSight(GetDroneWorldPosition(d), position);
    }
    private readonly record struct ArmorMatchSignature(string Family, double Light, double KineticLight, double Energy, double LaserEnergy, double LegacyLaserEnergy, bool PostShield, double PostShieldMultiplier, double PostShieldSeconds);
    private static void PrepareProfileDerived(Profile profile)
    {
        var stats = profile.Weapons;
        if (ReferenceEquals(profile.DerivedWeapons, stats)) return;
        string kind = profile.Kind;
        double damage = C.N(stats, kind == "interceptor" ? "damage" : kind + "_damage", 15), rate = C.N(stats, kind == "interceptor" ? "fire_rate" : kind + "_fire_rate", 1), rounds = kind == "interceptor" ? SalvoWeights(stats).Take(3).Sum() : kind == "laser" ? C.N(stats, "laser_beam_count", 1) : MissileSalvoCount(stats);
        profile.AssignedPower = Math.Max(1, damage * rate * rounds * 2);
        profile.ArmorSignature = new(EnemyArmor.Family(kind), C.N(stats, "light_damage_multiplier", 1), C.N(stats, "kinetic_light_damage_bonus"), C.N(stats, "energy_damage_multiplier", 1), C.N(stats, "laser_energy_damage_bonus"), C.Packet(stats, "laser_energy_multiplier", 1), C.B(C.M(stats, "tech_abilities"), "K_A3"), C.Packet(stats, "post_shield_kinetic_multiplier", 1.4), C.Packet(stats, "post_shield_seconds", 3));
        profile.DerivedWeapons = stats;
    }
    private double AssignedFirepower(DataMap drone)
    {
        var profile = GetProfile(drone); PrepareProfileDerived(profile); return profile.AssignedPower;
    }
    public void AssignDefenseTargets()
    {
        if (HasTech("C_N3"))
            RotateDamagedPatrols();
        RebuildTargets();
        if (_targets.Count == 0)
        {
            foreach (var d in Drones)
                if (C.S(d, "state") != "suiciding")
                    d["target_uid"] = -1L;
            return;
        }
        var budgets = new Dictionary<long, double>();
        foreach (var shot in Shots)
        {
            var source = C.M(shot, "source");
            long uid = C.L(source, "primary_target_uid", -1);
            if (EnemyByUid(uid) is not { } target)
                continue;
            budgets[uid] = budgets.GetValueOrDefault(uid) + C.N(shot, "damage") * EnemyArmor.Multiplier(target, C.S(source, "damage_type", "kinetic"), source, Clock);
        }
        var counts = new Dictionary<long, int>();
        var unassigned = new List<DataMap>();
        foreach (var d in Drones)
        {
            string phase = C.S(d, "state", "patrol");
            if (phase == "suiciding")
                continue;
            if (phase is "launching" or "returning" or "landing" or "repairing")
            {
                d["target_uid"] = -1L;
                continue;
            }
            long uid = C.L(d, "target_uid", -1);
            var target = EnemyByUid(uid);
            if (AssignmentValid(d, target) && counts.GetValueOrDefault(uid) < 8 && budgets.GetValueOrDefault(uid) < (C.N(target, "hp") + C.N(target, "energy_hp")) * 1.35)
            {
                budgets[uid] = budgets.GetValueOrDefault(uid) + AssignedFirepower(d) * EnemyArmor.Multiplier(target!, EnemyArmor.Family(C.S(d, "kind")), GetProfile(d).Weapons, Clock);
                counts[uid] = counts.GetValueOrDefault(uid) + 1;
            }
            else
            {
                d["target_uid"] = -1L;
                unassigned.Add(d);
            }
        }
        _assignmentGeneration++;
        if (_assignmentTargets.Length < _targets.Count) Array.Resize(ref _assignmentTargets, Math.Max(_targets.Count, _assignmentTargets.Length * 2));
        _assignmentTree.Clear(_targets.Count);
        for (int i = 0; i < _targets.Count; i++)
        {
            var enemy = _targets[i]; var position = C.V(enemy, "space_position");
            _assignmentTargets[i] = new(enemy, C.L(enemy, "uid"), position, position.Normalized(), position.Length(), (C.N(enemy, "hp") + C.N(enemy, "energy_hp")) * 1.35, C.N(enemy, "hp") > 0 && C.S(enemy, "phase") != "retreat", C.Large(enemy));
            _assignmentTree.Add(position, enemy, i, C.L(enemy, "uid"));
        }
        _assignmentTree.Build();
        foreach (var d in unassigned)
        {
            var profile = GetProfile(d);
            PrepareProfileDerived(profile);
            if (!_assignmentGroups.TryGetValue(profile, out var group)) _assignmentGroups[profile] = group = new();
            if (group.Generation != _assignmentGeneration)
            {
                if (!_armorMatchGroups.TryGetValue(profile.ArmorSignature, out var armor)) _armorMatchGroups[profile.ArmorSignature] = armor = new();
                armor.Ensure(_targets.Count);
                if (armor.Generation != _assignmentGeneration)
                {
                    for (int i = 0; i < _targets.Count; i++) armor.Matches[i] = _assignmentTargets[i].Active ? EnemyArmor.Multiplier(_targets[i], profile.ArmorSignature.Family, profile.Weapons, Clock) : 0;
                    armor.Generation = _assignmentGeneration;
                }
                group.Ensure(_targets.Count); group.LowerMultiplier = double.PositiveInfinity;
                var home = Home(d);
                group.Home = home; group.Cosine = profile.Cosine; group.Sine = Math.Sin(profile.Angle);
                 double ordinaryRadius = MaximumTargetRadius(profile, false), largeRadius = MaximumTargetRadius(profile, true);
                for (int i = 0; i < _targets.Count; i++)
                {
                    var target = _assignmentTargets[i]; double match = armor.Matches[i];
                    if (!target.Active || match <= 0 || target.Radius > (target.Large ? largeRadius : ordinaryRadius) || home.Dot(target.Normal) < profile.Cosine) { group.Matches[i] = 0; continue; }
                    group.Matches[i] = match;
                    group.LowerMultiplier = Math.Min(group.LowerMultiplier, 1 / Math.Max(.1, match));
                }
                group.Generation = _assignmentGeneration;
            }
            if (double.IsPositiveInfinity(group.LowerMultiplier)) continue;
            var filter = new AssignmentFilter { Origin = GetDroneWorldPosition(d), Group = group, Targets = _assignmentTargets, Counts = counts, Budgets = budgets };
            if (!_assignmentTree.FindBestBounded(filter.Origin, group.LowerMultiplier, ref filter, out var best)) continue;
            long targetUid = C.L(best, "uid");
            d["target_uid"] = targetUid;
            double bestMatch = EnemyArmor.Multiplier(best, EnemyArmor.Family(profile.Kind), profile.Weapons, Clock);
            budgets[targetUid] = budgets.GetValueOrDefault(targetUid) + AssignedFirepower(d) * bestMatch;
            counts[targetUid] = counts.GetValueOrDefault(targetUid) + 1;
            if (HasTech("C_A1") && Clock >= C.N(d, "intercept_boost_next"))
            {
                d["intercept_boost_until"] = Clock + C.N(_globalWeapons, "intercept_boost_duration", 3);
                d["intercept_boost_next"] = Clock + C.N(_globalWeapons, "intercept_boost_cooldown", 10);
            }
        }
    }
    private readonly PointSearchTree<DataMap> _assignmentTree = new();
    private sealed class AssignmentGroup
    {
        public Vector3 Home; public double Cosine, Sine;
        public double[] Matches = Array.Empty<double>(); public double LowerMultiplier = double.PositiveInfinity; public long Generation = -1;
        public void Ensure(int count) { if (Matches.Length < count) { int size = Math.Max(count, Matches.Length * 2); Array.Resize(ref Matches, size); } }
    }
    private readonly record struct AssignmentTarget(DataMap Actor, long Uid, Vector3 Position, Vector3 Normal, double Radius, double BudgetLimit, bool Active, bool Large);
    private AssignmentTarget[] _assignmentTargets = Array.Empty<AssignmentTarget>();
    private long _assignmentGeneration;
    private readonly Dictionary<Profile, AssignmentGroup> _assignmentGroups = new();
    private readonly Dictionary<ArmorMatchSignature, AssignmentGroup> _armorMatchGroups = new();
    private struct AssignmentFilter : PointSearchTree<DataMap>.IBoundedScoreFilter
    {
        public Vector3 Origin; public AssignmentGroup Group; public AssignmentTarget[] Targets; public Dictionary<long,int> Counts; public Dictionary<long,double> Budgets;
        public double Score(in PointSearchTree<DataMap>.Entry entry, double distanceSquared)
        {
            double match = Group.Matches[entry.Order];
            if (match <= 0) return double.PositiveInfinity;
            var target = Targets[entry.Order]; long uid = target.Uid; int count = Counts.GetValueOrDefault(uid);
            if (count >= 8 || Budgets.GetValueOrDefault(uid) >= target.BudgetLimit) return double.PositiveInfinity;
            double score = (distanceSquared + count * .4) / Math.Max(.1, match);
            return score;
        }
        public bool Accept(in PointSearchTree<DataMap>.Entry entry) => CombatGeometry.HasLineOfSight(Origin, entry.Position);
        public bool MayContain(Vector3 center, float radius) => SphereIntersectsDirectionCone(center, radius, Group.Home, Group.Cosine, Group.Sine);
    }
    private static bool SphereIntersectsDirectionCone(Vector3 center, float radius, Vector3 direction, double cosine, double sine)
    {
        if (cosine <= 0) return true; // Broad/global patrols use the unchanged leaf predicate.
        double axial = center.Dot(direction);
        double perpendicular = Math.Sqrt(Math.Max(0, center.LengthSquared() - axial * axial));
        // Signed cone distance is 1-Lipschitz, so an enclosing sphere can be
        // rejected only when its entire extent lies outside. Float slack keeps
        // the original normalized leaf comparisons authoritative at boundaries.
        return perpendicular * cosine - axial * sine <= radius + .0001 + center.Length() * .000001;
    }
    public void UpdateDrones(double dt)
    {
        _serialFlightScanEnd = 0;
        FlightEligibilityChecks = 0;
        _assignmentClock -= dt;
        if (_assignmentClock <= 0)
        {
            long started = Performance?.Timestamp() ?? 0;
            AssignDefenseTargets();
            Performance?.Record(CombatStage.Assignments, started);
            _assignmentClock = .35;
        }
        int index = 0;
        while (index < Drones.Count)
        {
            int advanced = RunFlightBatch(index, dt);
            if (advanced > 0) { index += advanced; continue; }
            var d = Drones[index];
            if (C.N(d, "launch_age") >= CombatScale.LaunchDuration)
                d["flight_age"] = C.N(d, "flight_age", 1e6) + Math.Max(dt, 0);
            var previous = GetDroneWorldPosition(d);
            d["hit"] = Math.Max(0, C.N(d, "hit") - dt);
            d["flash"] = Math.Max(0, C.N(d, "flash") - dt * 5);
            var stats = GetProfile(d).Patrol;
            string phase = C.S(d, "state", "patrol");
            bool critical = C.N(d, "hp") <= C.N(d, "max_hp") * C.N(stats, "repair_threshold", .35), returningDamaged = phase == "returning" && C.N(d, "hp") < C.N(d, "max_hp");
            if (Active && !InvasionWon && C.N(d, "launch_age") >= CombatScale.LaunchDuration && ((phase is "patrol" or "engaging") && critical || returningDamaged))
            {
                var nearby = PickSuicideTarget(d);
                if (nearby != null)
                {
                    d["state"] = "suiciding";
                    d["target_uid"] = C.L(nearby, "uid");
                }
                else
                {
                    d["state"] = "returning";
                    d["target_uid"] = -1L;
                    d["aim_target_uid"] = -1L;
                }
                phase = C.S(d, "state");
            }
            if (C.N(d, "launch_age") < CombatScale.LaunchDuration)
                UpdateLaunch(d, dt);
            else if (phase == "suiciding")
                UpdateSuicideCharge(d, dt);
            else if (phase is "returning" or "landing" or "repairing")
                UpdateDroneRepair(d, dt);
            else if (Active)
                UpdateInterceptor(d, dt);
            else
                ReturnToPatrol(d, dt);
            if (C.N(d, "hp") <= 0)
            {
                if (index < Drones.Count && ReferenceEquals(Drones[index], d))
                    index++;
                continue;
            }
            index++;
            CachePosition(d);
            var position = GetDroneWorldPosition(d);
            if (dt > 0)
            {
                var velocity = C.Scale(position - previous, 1 / dt);
                d["velocity"] = velocity;
                if (velocity.LengthSquared() > .000001)
                    d["tangent"] = velocity.Normalized();
            }
            d["world_up"] = position.Normalized();
            UpdateDroneAttitude(d, dt);
        }
    }
    private DataMap? PickSuicideTarget(DataMap d)
    {
        var origin = GetDroneWorldPosition(d);
        DataMap? best = null;
        double nearest = 2.6 * 2.6;
        foreach (var e in Neighbors(origin, 2.6, null, 0))
        {
            double distance = origin.DistanceSquaredTo(C.V(e, "space_position"));
            if (distance > nearest || !SuicideTargetValid(d, e))
                continue;
            if (distance < nearest || best == null || C.L(e, "uid") < C.L(best, "uid"))
            {
                nearest = distance;
                best = e;
            }
        }
        return best;
    }
    private bool SuicideTargetValid(DataMap d, DataMap? e)
    {
        if (!AssignmentValid(d, e))
            return false;
        double radius = C.V(e, "space_position").Length(), blast = C.N(GetProfile(d).Weapons, "death_blast_radius", .55);
        return radius >= CombatScale.EarthRadius + CombatScale.DroneAltitude - blast + .001 && radius <= GetProfile(d).Radial + blast - .001;
    }
    private void UpdateSuicideCharge(DataMap d, double dt)
    {
        var target = EnemyByUid(C.L(d, "target_uid", -1));
        var position = GetDroneWorldPosition(d);
        if (!SuicideTargetValid(d, target) || position.DistanceSquaredTo(C.V(target, "space_position", position)) > 3.2 * 3.2)
        {
            d["state"] = "returning";
            d["target_uid"] = -1L;
            d["aim_target_uid"] = -1L;
            UpdateDroneRepair(d, dt);
            return;
        }
        var p = C.V(target, "space_position");
        double radius = C.N(GetProfile(d).Weapons, "death_blast_radius", .55);
        if (position.DistanceSquaredTo(p) > radius * radius)
        {
            MoveDroneSafe(d, p, dt * 1.6);
            position = GetDroneWorldPosition(d);
        }
        if (position.DistanceSquaredTo(p) <= radius * radius && CombatGeometry.HasLineOfSight(position, p))
            ApplyDroneDamage(d, C.N(d, "hp") * 5 + 1);
    }
    private void UpdateLaunch(DataMap d, double dt)
    {
        d["state"] = "launching";
        d["launch_age"] = Math.Min(CombatScale.LaunchDuration, C.N(d, "launch_age") + dt);
        double t = C.N(d, "launch_age") / CombatScale.LaunchDuration;
        var site = Factories.TryGetValue(C.L(d, "factory_site_id"), out var factory) ? C.M(factory, "site") : C.Empty;
        var start = C.V(site, "launch_position", C.V(d, "spawn_space"));
        var direction = C.V(site, "launch_direction", C.V(d, "launch_direction"));
        d["spawn_space"] = start;
        d["launch_direction"] = direction;
        var control = start + C.Scale(direction, .54);
        var finish = C.Scale((start + C.Scale(direction, .45)).Normalized(), CombatScale.EarthRadius + CombatScale.DroneAltitude);
        d["space_position"] = C.Scale(C.Scale(start, 1 - t), 1 - t) + C.Scale(C.Scale(C.Scale(control, 2), 1 - t), t) + C.Scale(C.Scale(finish, t), t);
        CachePosition(d);
        if (t >= 1)
        {
            d["normal"] = SpaceToSurface(finish);
            d["axis"] = CombatGeometry.AxisBetween(C.V(d, "normal"), Vector3.Up);
            d["state"] = "patrol";
            _assignmentClock = 0;
        }
    }
    private void UpdateInterceptor(DataMap d, double dt)
    {
        var target = EnemyByUid(C.L(d, "target_uid", -1));
        if (!AssignmentValid(d, target))
        {
            d["target_uid"] = -1L;
            ReturnToPatrol(d, dt);
            return;
        }
        d["state"] = "engaging";
        var position = C.V(target, "space_position");
        var outward = position.Normalized();
        var right = CombatGeometry.AxisBetween(outward, Vector3.Up);
        var up = outward.Cross(right).Normalized();
        double phase = C.L(d, "uid") * 2.39996323 + Clock * .35, standOff = C.S(d, "kind") == "interceptor" ? 1.12 : C.S(d, "kind") == "laser" ? 2.2 : 2.7;
        MoveDroneSafe(d, position - C.Scale(outward, standOff) + C.Scale(C.Scale(right, Math.Cos(phase)) + C.Scale(up, Math.Sin(phase)), .42), dt);
    }
    private void ReturnToPatrol(DataMap d, double dt)
    {
        d["state"] = "patrol";
        var p = GetProfile(d);
        var home = Home(d);
        Factories.TryGetValue(C.L(d, "factory_site_id"), out var factory);
        var east = C.V(factory, "_patrol_east", CombatGeometry.AxisBetween(home, Vector3.Up));
        var north = C.V(factory, "_patrol_north", home.Cross(east).Normalized());
        int slot = C.I(d, "patrol_slot"), ring = slot % 3;
        double angle = C.N(p.Patrol, "patrol_radius") * (.58 + .09 * ring) / CombatScale.EarthRadius, sine = Math.Sin(angle), cosine = Math.Cos(angle), rate = C.N(p.Patrol, "patrol_speed") / Math.Max(.25, sine * (CombatScale.EarthRadius + CombatScale.DroneAltitude));
        double phase = slot * Math.Tau / Math.Max(1, C.I(factory, "capacity", 6)) + C.L(d, "factory_site_id") * .71 + Clock * rate;
        var normal = C.Scale(home, cosine) + C.Scale(C.Scale(east, Math.Cos(phase)) + C.Scale(north, Math.Sin(phase)), sine);
        MoveDroneSafe(d, C.Scale(normal, CombatScale.EarthRadius + CombatScale.DroneAltitude), dt);
        if (Math.Abs(C.V(d, "space_position").Length() - (CombatScale.EarthRadius + CombatScale.DroneAltitude)) < .005)
            d.Remove("space_position");
    }
    public void MoveDroneSafe(DataMap d, Vector3 desired, double dt)
    {
        var p = GetProfile(d);
        var stats = p.Patrol;
        var current = GetDroneWorldPosition(d);
        var normal = current.Normalized();
        var home = Home(d);
        var desiredNormal = desired.Normalized();
        double limit = p.Angle;
        if (home.Dot(desiredNormal) < p.Cosine)
            desiredNormal = home.Rotated(CombatGeometry.AxisBetween(home, desiredNormal), (float)limit).Normalized();
        double radius = current.Length(), desiredRadius = C.Clamp(desired.Length(), CombatScale.EarthRadius + CombatScale.DroneAltitude, p.Radial), radialStep = desiredRadius - radius, speed = C.N(stats, "patrol_speed"), travel = current.DistanceTo(desired);
        if (C.B(stats, "strategic_enabled") && travel > p.Range * 1.5)
            speed *= C.Lerp(1, C.N(stats, "strategic_speed_multiplier", 5), C.Smooth(p.Range * 1.5, p.Range * 3, travel));
        if (C.S(d, "state") is "returning" or "landing")
            speed *= C.N(stats, "return_speed_multiplier", 1);
        if (Clock < C.N(d, "intercept_boost_until"))
            speed *= C.N(stats, "intercept_boost_multiplier", 1.5);
        double budget = Math.Max(0, dt) * speed;
        if (C.S(d, "state") == "patrol" && Math.Abs(radialStep) < .000001)
        {
            double angular = Math.Sqrt(Math.Max(0, budget * budget - radialStep * radialStep)) / Math.Max(radius, .01), cross = normal.Cross(desiredNormal).LengthSquared(), sin = Math.Sin(Math.Min(Math.PI * .5, angular));
            if (cross >= .000001 && normal.Dot(desiredNormal) >= 0 && cross <= sin * sin)
            {
                d["space_position"] = C.Scale(desiredNormal, desiredRadius);
                d["normal"] = SpaceToSurface(C.V(d, "space_position"));
                CachePosition(d);
                return;
            }
        }
        double angle = normal.AngleTo(desiredNormal);
        var movement = new Vector2((float)(angle * radius), (float)radialStep);
        movement = movement.LimitLength((float)budget);
        var moved = normal.Rotated(CombatGeometry.AxisBetween(normal, desiredNormal), (float)(movement.X / Math.Max(radius, .01))).Normalized();
        d["space_position"] = C.Scale(moved, radius + movement.Y);
        d["normal"] = SpaceToSurface(C.V(d, "space_position"));
        CachePosition(d);
    }
    private void UpdateDroneRepair(DataMap d, double dt)
    {
        if (!Factories.TryGetValue(C.L(d, "factory_site_id"), out var f))
            return;
        var site = C.M(f, "site");
        var dock = C.V(site, "launch_position");
        string phase = C.S(d, "state");
        if (phase == "returning")
        {
            var point = C.Scale(dock.Normalized(), CombatScale.EarthRadius + CombatScale.DroneAltitude);
            MoveDroneSafe(d, point, dt);
            if (GetDroneWorldPosition(d).DistanceTo(point) < .055)
            {
                d["state"] = "landing";
                d["landing_age"] = 0d;
                d["landing_start"] = GetDroneWorldPosition(d);
                f["door"] = CombatScale.LaunchDuration + .4;
            }
        }
        else if (phase == "landing")
        {
            d["landing_age"] = Math.Min(CombatScale.LaunchDuration, C.N(d, "landing_age") + dt);
            double t = C.Smooth(0, 1, C.N(d, "landing_age") / CombatScale.LaunchDuration);
            var start = C.V(d, "landing_start");
            d["space_position"] = C.Scale(start.Normalized().Slerp(dock.Normalized(), (float)t).Normalized(), C.Lerp(start.Length(), dock.Length(), t));
            f["door"] = Math.Max(C.N(f, "door"), .35);
            if (t >= 1)
                d["state"] = "repairing";
        }
        else if (phase == "repairing")
        {
            d["space_position"] = dock;
            d["hp"] = Math.Min(C.N(d, "max_hp"), C.N(d, "hp") + C.N(d, "max_hp") * C.N(GetProfile(d).Patrol, "repair_rate", .12) * dt);
            if (C.N(d, "hp") >= C.N(d, "max_hp") - .001)
            {
                d["spawn_space"] = dock;
                d["launch_direction"] = site["launch_direction"];
                d["launch_age"] = 0d;
                d["flight_age"] = 0d;
                d["state"] = "launching";
                f["door"] = CombatScale.LaunchDuration + .4;
            }
        }
        CachePosition(d);
    }
    private Vector3 CruiseDirection(DataMap d)
    {
        var velocity = C.V(d, "velocity");
        if (velocity.LengthSquared() > .000001)
            return velocity.Normalized();
        var tangent = C.V(d, "tangent", Vector3.Forward);
        return d.ContainsKey("space_position") ? tangent.Normalized() : SurfaceToSpace(tangent, 0).Normalized();
    }
    public Vector3 AimTargetPosition(DataMap d, DataMap target, Vector3 origin)
    {
        var destination = C.V(target, "space_position");
        if (C.S(d, "kind") == "interceptor")
            destination += C.Scale(C.V(target, "velocity"), origin.DistanceTo(destination) / (6.4 * C.N(GetProfile(d).Weapons, "projectile_speed_multiplier", 1)));
        return destination;
    }
    private void UpdateDroneAttitude(DataMap d, double dt)
    {
        var travel = CruiseDirection(d);
        var forward = C.V(d, "aim_direction", travel).Normalized();
        var radial = C.V(d, "world_up", GetDroneWorldPosition(d).Normalized());
        var up = CombatGeometry.OrthogonalUp(forward, C.V(d, "aim_up", radial));
        DataMap? target = null;
        if (Active && C.N(d, "launch_age", CombatScale.LaunchDuration) >= CombatScale.LaunchDuration && C.S(d, "state") is not ("landing" or "repairing"))
        {
            target = EnemyByUid(C.L(d, "target_uid", -1));
            if (target == null && C.S(d, "state") == "returning")
                target = PickTarget(GetDroneWorldPosition(d), GetProfile(d).Range);
            if (!AssignmentValid(d, target))
                target = null;
        }
        long next = target == null ? -1 : C.L(target, "uid");
        if (next != C.L(d, "aim_target_uid", -1))
            d["lock_lost_at"] = Clock;
        d["aim_target_uid"] = next;
        var desired = travel;
        if (target != null)
        {
            var muzzle = GetDroneMuzzlePosition(d);
            var aim = AimTargetPosition(d, target, muzzle);
            d["aim_point"] = aim;
            desired = (aim - muzzle).Normalized();
        }
        else
            d.Remove("aim_point");
        double angle = forward.AngleTo(desired);
        if (angle > .000001 && dt > 0)
        {
            var axis = forward.Cross(desired);
            axis = axis.LengthSquared() < .000001 ? up : axis.Normalized();
            double step = Math.Min(angle, C.N(GetProfile(d).Patrol, "combat_turn_rate", 4.2) * dt);
            forward = forward.Rotated(axis, (float)step).Normalized();
            up = up.Rotated(axis, (float)step).Normalized();
        }
        var level = radial - C.Scale(forward, radial.Dot(forward));
        if (level.LengthSquared() > .04 && dt > 0)
        {
            double roll = up.SignedAngleTo(level.Normalized(), forward);
            up = up.Rotated(forward, (float)C.Clamp(roll, -2.4 * dt, 2.4 * dt));
        }
        d["aim_direction"] = forward;
        d["aim_up"] = CombatGeometry.OrthogonalUp(forward, up);
    }
    public Vector3 GetDroneMuzzleLocalPosition(DataMap d)
    {
        double side = C.L(d, "uid") % 2 == 0 ? 1 : -1;
        return C.S(d, "airframe_id", BaseFrame(C.S(d, "kind"))) switch
        {
            "K3" => C.Vec(0, .018, -.232),
            "M1" => C.Vec(side * .061, .008, -.082),
            "M2" => C.Vec(side * .09, .008, -.053),
            "M3" => C.Vec(side * .051, 0, -.158),
            "L1" => C.Vec(0, .033, -.174),
            "L2" => C.Vec(0, 0, -.202),
            "L3" => C.Vec(0, .034, -.15),
            _ => C.Vec(0, .018, -.15)
        };
    }
    public Vector3 GetDroneMuzzlePosition(DataMap d)
    {
        var position = GetDroneWorldPosition(d);
        var forward = d.TryGetValue("aim_direction", out var direction) && direction is Vector3 aimed ? aimed : CruiseDirection(d);
        var up = CombatGeometry.OrthogonalUp(forward, C.V(d, "aim_up", position.Normalized()));
        var local = C.Scale(GetDroneMuzzleLocalPosition(d), GetDroneScale() * C.N(GetProfile(d).Weapons, "scale_multiplier", 1));
        return position + C.Scale(forward.Cross(up).Normalized(), local.X) + C.Scale(up, local.Y) - C.Scale(forward, local.Z);
    }
    private void RotateDamagedPatrols()
    {
        var ready = new Dictionary<long, int>();
        foreach (var d in Drones)
            if (C.S(d, "state") is "patrol" or "engaging" && C.N(d, "hp") >= C.N(d, "max_hp") * .8)
            {
                long owner = C.L(d, "factory_site_id");
                ready[owner] = ready.GetValueOrDefault(owner) + 1;
            }
        foreach (var d in Drones)
        {
            long owner = C.L(d, "factory_site_id");
            if (ready.GetValueOrDefault(owner) < 2 || C.S(d, "state") is not ("patrol" or "engaging") || C.N(d, "hp") >= C.N(d, "max_hp") * .7 || PickSuicideTarget(d) != null)
                continue;
            d["state"] = "returning";
            d["target_uid"] = -1L;
            d["aim_target_uid"] = -1L;
            ready[owner]--;
        }
    }
    public void RebuildTargetSectors()
    {
        _enemySectors.Clear();
        _sectorKeys.Clear();
        foreach (var e in _targets)
        {
            var p = C.V(e, "space_position");
            var key = Sector(p, 2);
            if (!_enemySectors.TryGetValue(key, out var group))
            {
                group = new();
                _enemySectors[key] = group;
                _sectorKeys.Add(key);
            }
            group.Add(e);
        }
        _sectorKeys.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z));
        _neighborTreeValid = false;
        if (!_updatingEnemyRoutes) BuildNeighborhoodTree();
        _sectorsDirty = false;
    }
    private static Vector3I Sector(Vector3 p, double cell) => new((int)Math.Floor(p.X / cell), (int)Math.Floor(p.Y / cell), (int)Math.Floor(p.Z / cell));
    private readonly PointSearchTree<DataMap> _neighborhoodTree = new();
    private readonly List<PointSearchTree<DataMap>.Entry> _neighborCandidates = new();
    private static readonly Comparison<PointSearchTree<DataMap>.Entry> NeighborhoodOrder = (a, b) => a.Order.CompareTo(b.Order);
    private bool _neighborTreeValid, _updatingEnemyRoutes;
    private void BuildNeighborhoodTree()
    {
        _neighborhoodTree.Clear(_targets.Count);int order=0;
        foreach(var key in _sectorKeys) foreach(var enemy in _enemySectors[key]) _neighborhoodTree.Add(C.V(enemy,"space_position"),enemy,order++,C.L(enemy,"uid"));
        _neighborhoodTree.Build();_neighborTreeValid=true;
    }
    private List<DataMap> Neighbors(Vector3 origin, double radius, IReadOnlyCollection<long>? excluded, int count)
    {
        long neighborMark = Performance?.Timestamp() ?? 0;
        if (_sectorsDirty) RebuildTargetSectors();
        var result = new List<DataMap>();
        double limit = Math.Max(0, radius) * Math.Max(0, radius), selectedMaximum = double.PositiveInfinity;
        if (_updatingEnemyRoutes)
        {
            // Enemy routes are advanced one at a time. Preserve the existing conservative
            // sector query while those positions are changing inside the AI pass.
            var low = Sector(origin - C.Scale(Vector3.One, radius), 2);var high = Sector(origin + C.Scale(Vector3.One, radius), 2);
            foreach(var key in _sectorKeys)
            {
                if(key.X<low.X||key.X>high.X||key.Y<low.Y||key.Y>high.Y||key.Z<low.Z||key.Z>high.Z)continue;
                foreach(var enemy in _enemySectors[key]) ConsiderNeighbor(result,enemy,origin,limit,excluded,count,ref selectedMaximum);
            }
        }
        else if(radius>=0)
        {
            if(!_neighborTreeValid)BuildNeighborhoodTree();
            _neighborhoodTree.CollectInRadius(origin,limit,_neighborCandidates);
            _neighborCandidates.Sort(NeighborhoodOrder);
            foreach(var candidate in _neighborCandidates) ConsiderNeighbor(result,candidate.Value,origin,limit,excluded,count,ref selectedMaximum);
        }
        Performance?.Record(CombatStage.NeighborhoodQueries, neighborMark);
        return result;
    }
    private static void ConsiderNeighbor(List<DataMap> result,DataMap enemy,Vector3 origin,double limit,IReadOnlyCollection<long>? excluded,int count,ref double selectedMaximum)
    {
        var position=C.V(enemy,"space_position");double distance=origin.DistanceSquaredTo(position);
        if(distance>limit || count>0 && result.Count>=count && distance>selectedMaximum && !C.Near(selectedMaximum,distance))return;
        if(C.N(enemy,"hp")<=0||excluded?.Contains(C.L(enemy,"uid"))==true)return;
        // Compute insertion position before the relatively expensive planet-occlusion test.
        int index=0;
        if(count>0)
        {
            while(index<result.Count)
            {
                double previous=origin.DistanceSquaredTo(C.V(result[index],"space_position"));
                if(previous>distance||C.Near(previous,distance)&&C.L(result[index],"uid")>C.L(enemy,"uid"))break;
                index++;
            }
            if(index>=count)return;
        }
        if(!CombatGeometry.HasLineOfSight(origin,position))return;
        if(count<=0){result.Add(enemy);return;}
        result.Insert(index,enemy);if(result.Count>count)result.RemoveAt(result.Count-1);
        if(result.Count>=count)
        {
            selectedMaximum=0;
            foreach(var selected in result)selectedMaximum=Math.Max(selectedMaximum,origin.DistanceSquaredTo(C.V(selected,"space_position")));
        }
    }
    private readonly PointSearchTree<DataMap> _nearestTargets = new();
    private bool _nearestTargetDirty = true;
    private readonly struct NearestEnemyFilter : PointSearchTree<DataMap>.IFilter
    {
        private readonly Vector3 _origin;
        public NearestEnemyFilter(Vector3 origin) => _origin = origin;
        public bool Accept(in PointSearchTree<DataMap>.Entry entry) => C.N(entry.Value, "hp") > 0 && CombatGeometry.HasLineOfSight(_origin, entry.Position);
    }
    private DataMap? PickTarget(Vector3 origin, double range)
    {
        if (_nearestTargetDirty)
        {
            _nearestTargets.Clear(_targets.Count);
            for (int i = 0; i < _targets.Count; i++)
                _nearestTargets.Add(C.V(_targets[i], "space_position"), _targets[i], i, C.L(_targets[i], "uid"));
            _nearestTargets.Build();
            _nearestTargetDirty = false;
        }
        var filter = new NearestEnemyFilter(origin);
        return _nearestTargets.FindNearest(origin, range * range, ref filter, out var found) ? found : null;
    }
}
