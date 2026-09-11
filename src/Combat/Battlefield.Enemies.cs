using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private double _lastEnemySpeed = 1.5;
    private readonly PointSearchTree<DefenderCandidate> _defenderTree = new();
    private readonly record struct DefenderCandidate(DataMap Actor, Vector3 Position, Vector3 Normal, Vector3 Home, double Cosine, double Range, bool Interceptor, int Order);
    private bool _defendersValid;
    public bool UseEmp()
    {
        if (!Active || Dead || EmpCooldown > 0)
            return false;
        foreach (var e in Enemies)
        {
            e["stagger_until"] = Clock + (C.Large(e) ? CombatCatalog.Current.Values.EmpLargeStagger : CombatCatalog.Current.Values.EmpSmallStagger);
            e["energy_recovery_blocked_until"] = Clock + CombatCatalog.Current.Values.EmpRecoveryBlockSeconds;
        }
        foreach (var s in HostileShots)
            if (C.B(s, "interceptable"))
                s["life"] = 0d;
        EmpCooldown = CombatCatalog.Current.Values.EmpCooldown;
        EmpAge = 0;
        return true;
    }
    public double GetEnemyProjectileSpeed(string targetKind = "drone") => Math.Max(.1, Game.CombatSettings.N(targetKind == "drone" ? "enemy_bullet_speed" : "enemy_bombard_speed", targetKind == "drone" ? 6 : 3.2));
    public double EnemyFireCooldown(string kind, string targetKind = "drone") { var data = CombatCatalog.Current.Kind(kind); return targetKind == "drone" ? data.DroneCooldown : data.EarthCooldown; }
    private bool CanBombardEarth(DataMap e) => C.B(e, "post_carrier")
        ? C.S(e, "phase") == "ground_attack" && C.V(e, "space_position").Length() <= CombatScale.LegacyCarrierStandoff + .01
        : UsesStationaryBombardment(e)
            ? C.S(e, "phase") != "retreat" && InsideStationaryBombardmentZone(e)
            : C.S(e, "kind") != "meteor" && C.S(e, "phase") != "retreat" && C.V(e, "space_position").Length() <= CombatScale.CloseAssault + .0001;
    private void EnsureDefenders()
    {
        if (_defendersValid)
            return;
        long begin = Performance?.Timestamp() ?? 0;
        _defenderTree.Clear(Drones.Count);
        foreach (var d in Drones)
        {
            if (C.N(d, "hp") <= 0 || C.N(d, "launch_age") < CombatScale.LaunchDuration || C.S(d, "state") is "launching" or "landing" or "repairing")
                continue;
            var position = GetDroneWorldPosition(d);
            var profile = GetProfile(d);
            int order = C.I(d, "_actor_order");
            _defenderTree.Add(position, new(d, position, position.Normalized(), Home(d), profile.Cosine, profile.Range, C.S(d, "kind") == "interceptor", order), order, C.L(d, "uid"));
        }
        _defenderTree.Build();
        _defendersValid = true;
        Performance?.Record(CombatStage.DefenderIndexBuild, begin);
    }
    private struct DefenderFilter : PointSearchTree<DefenderCandidate>.IBoundsFilter
    {
        public Vector3 Origin, Normal;
        public double Cosine, Sine, RangeMultiplier;
        public bool Interceptor, RequireRange;
        public bool MayContain(Vector3 center, float radius) => SphereIntersectsDirectionCone(center, radius, Normal, Cosine, Sine);
        public bool Accept(in PointSearchTree<DefenderCandidate>.Entry entry)
        {
            var candidate = entry.Value;
            if (candidate.Interceptor != Interceptor || candidate.Normal.Dot(Normal) < Cosine || candidate.Home.Dot(Normal) < candidate.Cosine || C.N(candidate.Actor, "hp") <= 0)
                return false;
            double range = candidate.Range * RangeMultiplier;
            return !RequireRange || candidate.Position.DistanceSquaredTo(Origin) <= range * range && CombatGeometry.HasLineOfSight(candidate.Position, Origin);
        }
    }
    private DataMap QueryDefender(Vector3 origin, bool requireRange, bool specialists = false, bool large = false)
    {
        EnsureDefenders();
        long begin = Performance?.Timestamp() ?? 0;
        var filter = new DefenderFilter
        {
            Origin = origin, Normal = origin.Normalized(),
            Cosine = Math.Cos(Math.Min(Math.PI, 2.4 / CombatScale.EarthRadius)),
            Sine = Math.Sin(Math.Min(Math.PI, 2.4 / CombatScale.EarthRadius)),
            RangeMultiplier = large && HasTech("C_N1") ? C.N(_globalWeapons, "large_target_range_multiplier", 1) : 1,
            RequireRange = requireRange, Interceptor = !specialists
        };
        DataMap result;
        if (_defenderTree.FindNearestBounded(origin, double.PositiveInfinity, ref filter, out var first))
            result = first.Actor;
        else
        {
            filter.Interceptor = !filter.Interceptor;
            result = _defenderTree.FindNearestBounded(origin, double.PositiveInfinity, ref filter, out var second) ? second.Actor : C.Empty;
        }
        Performance?.Record(CombatStage.DefenderQueries, begin);
        return result;
    }
    private DataMap PickEnemyDefender(DataMap e)
    {
        var target = GetDroneByUid(C.L(e, "locked_defender_uid", -1));
        if (target != null && Clock < C.N(e, "target_lock_until") && CanEngage(target, C.V(e, "space_position"), e))
        {
            e["combat_slow_until"] = Clock + .8;
            return target;
        }
        target = QueryDefender(C.V(e, "space_position"), true, C.S(e, "enemy_role_id") == "needle", C.Large(e));
        e["locked_defender_uid"] = C.L(target, "uid", -1);
        e["target_lock_until"] = Clock + .8;
        if (target.Count > 0)
            e["combat_slow_until"] = Clock + .8;
        return target;
    }
    private void UpdateEnemies(double dt)
    {
        _updatingEnemyRoutes = true;
        try { UpdateEnemyActors(dt); }
        finally { _updatingEnemyRoutes = false; }
    }
    private void UpdateEnemyActors(double dt)
    {
        _defendersValid = false;
        if (!C.Near(GetEnemySpeedMultiplier(), _lastEnemySpeed))
        {
            RefreshEnemyMovement();
            _lastEnemySpeed = GetEnemySpeedMultiplier();
        }
        int epoch = _epoch;
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            if (i >= Enemies.Count)
                continue;
            var e = Enemies[i];
            UpdatePerkStatus(e, dt);
            var previous = C.V(e, "space_position");
            string kind = C.S(e, "kind");
            bool managed = UpdateEnemySkill(e, dt);
            if (epoch != _epoch)
                return;
            e["age"] = C.N(e, "age") + dt;
            e["hit"] = Math.Max(0, C.N(e, "hit") - dt);
            if (kind == "meteor")
                e["space_position"] = previous + C.Scale(C.V(e, "velocity"), dt * MovementMultiplier(e));
            else
            {
                if (C.B(e, "post_carrier"))
                {
                    e["hangar_open"] = Math.Max(0, C.N(e, "hangar_open") - dt);
                    AdvancePostCarrier(e, dt);
                }
                else
                    AdvanceEnemyRoute(e, dt);
                if (C.S(e, "phase") != "retreat" && !managed && Clock >= C.N(e, "stagger_until"))
                {
                    e["fire"] = C.N(e, "fire") - dt;
                    if (C.N(e, "fire") <= 0)
                    {
                        if (CanBombardEarth(e))
                        {
                            FireHostile(e, C.N(e, "ground_damage", 2.5));
                            e["fire"] = C.N(e, "attack_cooldown", EnemyFireCooldown(kind, "earth"));
                        }
                        else
                        {
                            var defender = PickEnemyDefender(e);
                            if (defender.Count > 0)
                            {
                                FireAtDrone(e, defender);
                                e["fire"] = C.N(e, "attack_cooldown", EnemyFireCooldown(kind));
                            }
                            else
                                e["fire"] = .12;
                        }
                    }
                }
            }
            var current = C.V(e, "space_position");
            if (kind == "meteor")
            {
                double contact = CombatGeometry.SphereHitFraction(previous, current, Vector3.Zero, CombatScale.EarthCollisionRadius + C.N(e, "hit_radius") * .3);
                e.TryAdd("earth_impact_damage", 3d);
                if (InterceptLocalShieldSegment(e, previous, current, contact, "earth_impact_damage", out var shieldImpact))
                {
                    AddBurst(shieldImpact, CombatScale.Cyan, 22);
                    Enemies.Remove(e);
                    _enemyById.Remove(C.L(e, "uid"));
                    continue;
                }
                if (!double.IsPositiveInfinity(contact))
                {
                    var at = previous.Lerp(current, (float)contact);
                    DamageEarth(C.N(e, "earth_impact_damage", 3), at, C.B(e, "local_shield_checked"));
                    AddBurst(at, CombatScale.Coral, 28);
                    Enemies.Remove(e);
                    _enemyById.Remove(C.L(e, "uid"));
                    if (Dead || epoch != _epoch)
                        return;
                }
            }
            else if (C.S(e, "phase") == "retreat" && current.Length() > CombatScale.RetreatExit)
            {
                Enemies.Remove(e);
                _enemyById.Remove(C.L(e, "uid"));
            }
        }
        RebuildTargets();
    }
    private void AdvanceEnemyRoute(DataMap e, double dt)
    {
        var previous = C.V(e, "space_position");
        double radius = previous.Length();
        var normal = C.Scale(previous, 1 / Math.Max(radius, .000001));
        if (!e.ContainsKey("route_axis"))
        {
            var tangent = CombatGeometry.OrthogonalUp(normal, Vector3.Up).Rotated(normal, (float)(C.L(e, "uid") * 2.39996323 % Math.Tau));
            e["route_axis"] = normal.Cross(tangent).Normalized();
        }
        var axis = C.V(e, "route_axis");
        string phase = C.S(e, "phase");
        if (phase == "retreat")
            ClearStationaryBombardment(e);
        else if (UsesStationaryBombardment(e) && InsideStationaryBombardmentZone(e))
        {
            HoldBombardmentPosition(e, dt);
            return;
        }
        double slow = MovementMultiplier(e), tactical = Math.Max(.05, C.N(e, "tactical_speed", C.N(e, "speed"))), entry = Math.Max(CombatScale.DefenseEntryRadius, C.N(e, "combat_entry_radius", CombatScale.DefenseEntryRadius)), far = Math.Max(tactical, (C.N(e, "spawn_radius", radius) - entry) / 24), blend = C.Smooth(entry, entry + Math.Max(2, far * 2), radius), speed = C.Lerp(tactical, far, blend) * slow;
        if (Clock < C.N(e, "combat_slow_until"))
            speed = Math.Min(speed, tactical * slow);
        speed = C.Lerp(C.N(e, "route_speed", speed), speed, 1 - Math.Exp(-Math.Max(dt, 0) * 5));
        e["route_speed"] = speed;
        if (Clock < C.N(e, "stagger_until"))
            speed *= .15;
        e["skill_phase"] = blend > .5 && C.S(e, "skill_phase") != "charging" ? "cruise" : C.S(e, "skill_phase", "travel");
        if (phase != "retreat" && UsesStationaryBombardment(e))
        {
            AdvanceToBombardmentPosition(e, dt, speed);
            return;
        }
        if (phase == "retreat")
        {
            double age = C.N(e, "exit_age") + dt;
            e["exit_age"] = age;
            double outward = Math.Min(.72, age * .35), exit = Math.Max(speed, .58 * GetEnemySpeedMultiplier() * slow);
            e["space_position"] = C.Scale(normal.Rotated(axis, (float)(exit * dt * Math.Sqrt(1 - outward * outward) / Math.Max(radius, .01))).Normalized(), radius + exit * dt * outward);
        }
        else
        {
            double inward = radius > CombatScale.CloseAssault + .0001 ? C.Clamp((radius - CombatScale.CloseAssault) / .25, .04, 1) : 0, next = Math.Max(CombatScale.CloseAssault, radius - speed * dt * inward);
            if (next <= CombatScale.CloseAssault + .0001)
            {
                next = CombatScale.CloseAssault;
                if (phase != "ground_attack")
                {
                    e["phase"] = "ground_attack";
                    e["fire"] = Math.Min(C.N(e, "fire"), 0);
                }
                e["bombard_time"] = C.N(e, "bombard_time") + dt;
            }
            else
                e["phase"] = "approach";
            e["space_position"] = C.Scale(normal.Rotated(axis, (float)(speed * dt * Math.Sqrt(Math.Max(0, 1 - inward * inward)) / Math.Max(radius, .01))).Normalized(), next);
            if (C.N(e, "bombard_time") >= C.N(e, "bombard_duration"))
            {
                e["phase"] = "retreat";
                e["exit_age"] = 0d;
            }
        }
        UpdateEnemyFlightHeading(e, previous, dt);
    }
    private void AdvancePostCarrier(DataMap e, double dt)
    {
        var previous = C.V(e, "space_position");
        if (e.ContainsKey("frontier_radius"))
        {
            e["velocity"] = Vector3.Zero;
            e["tangent"] = -previous.Normalized();
            return;
        }
        var radial = previous.Normalized();
        if (!e.ContainsKey("route_axis"))
            e["route_axis"] = CombatGeometry.AxisBetween(radial, Vector3.Up);
        double radius = Math.Max(CombatScale.LegacyCarrierStandoff, previous.Length() - C.N(e, "speed") * dt * MovementMultiplier(e));
        if (radius <= CombatScale.LegacyCarrierStandoff + .00001)
        {
            e["phase"] = "ground_attack";
            radial = radial.Rotated(C.V(e, "route_axis"), (float)(.018 * dt));
        }
        e["space_position"] = C.Scale(radial, radius);
        var velocity = C.Scale(C.V(e, "space_position") - previous, 1 / Math.Max(dt, .000001));
        e["velocity"] = velocity;
        if (velocity.LengthSquared() > .000001)
            e["tangent"] = velocity.Normalized();
        e["world_up"] = radial;
    }
    private DataMap HostilePacket(Vector3 origin, Vector3 heading, double speed, double damage, int index, int count, DataMap enemy, bool interceptable, string target) => new() { ["uid"] = NewUid(), ["space_position"] = origin, ["velocity"] = C.Scale(heading, speed), ["tangent"] = heading, ["kind"] = "hostile", ["life"] = target == "earth" ? 7d : 5d, ["damage"] = damage / Math.Max(count, 1), ["target_kind"] = target, ["interceptable"] = interceptable, ["hp"] = 8d, ["max_hp"] = 8d, ["projectile_role"] = C.S(enemy, "boss_variant_id") == "brood" ? "spore" : "torpedo", ["secondary_damage_fraction"] = interceptable ? 1d : .25, ["blast_radius"] = interceptable ? .38 : .16, ["volley_index"] = index, ["volley_count"] = count };
    private void FireHostile(DataMap e, double damage, bool interceptable = false)
    {
        if (!CanBombardEarth(e))
            return;
        var origin = C.V(e, "space_position");
        var direction = C.B(e, "stationary_bombard") ? -origin.Normalized() : (FacilitySpaceTarget(origin) - origin).Normalized();
        int count = C.I(e, "locked_volley_count", Game.EnemyBulletCount());
        double speed = GetEnemyProjectileSpeed("earth") * (interceptable ? .7 : 1);
        for (int i = 0; i < count; i++)
        {
            var heading = CombatGeometry.VolleyDirection(direction, origin, i, count);
            HostileShots.Add(HostilePacket(origin + C.Scale(heading, .06 * GetEnemyScale()), heading, speed, damage, i, count, e, interceptable, "earth"));
        }
    }
    private void FireAtDrone(DataMap e, DataMap d, bool interceptable = false)
    {
        if (!CanEngage(d, C.V(e, "space_position")))
            return;
        var origin = C.V(e, "space_position");
        var position = GetDroneWorldPosition(d);
        var velocity = C.V(d, "velocity");
        double speed = GetEnemyProjectileSpeed() * (interceptable ? CombatCatalog.Current.Values.SporeProjectileSpeedMultiplier : 1), time = CombatGeometry.InterceptTime(position - origin, velocity, speed, 5);
        var target = position + C.Scale(velocity, time);
        var direction = (target - origin).Normalized();
        origin += C.Scale(direction, .08 * GetEnemyScale());
        time = CombatGeometry.InterceptTime(position - origin, velocity, speed, 5);
        target = position + C.Scale(velocity, time);
        direction = (target - origin).Normalized();
        int count = C.I(e, "locked_volley_count", Game.EnemyBulletCount());
        double reach = Math.Min(speed * 5, origin.DistanceTo(target) + .22);
        for (int i = 0; i < count; i++)
        {
            var shot = HostilePacket(origin, CombatGeometry.VolleyDirection(direction, origin, i, count), speed, C.N(e, "attack_damage", 8), i, count, e, interceptable, "drone");
            shot["target_uid"] = d["uid"];
            shot["intercept_time"] = time;
            shot["aim_point"] = target;
            shot["travel_remaining"] = reach;
            HostileShots.Add(shot);
        }
    }
    private bool UpdateEnemySkill(DataMap e, double dt)
    {
        string role = C.S(e, "enemy_role_id"), boss = C.S(e, "boss_variant_id");
        if (Clock < C.N(e, "stagger_until"))
            return true;
        if (e.ContainsKey("cargo"))
        {
            var cargo = C.A(e, "cargo");
            for (int i = 0; i < cargo.Count;)
            {
                if (cargo[i] is DataMap item && Clock >= C.N(item, "release_at"))
                {
                    var entry = C.M(item, "entry");
                    SpawnEnemy(C.S(entry, "kind"), entry, _invasion.FrontierAircraftSpawn(C.V(e, "space_position"), Random));
                    cargo.RemoveAt(i);
                }
                else
                    i++;
            }
        }
        if (role is "weaver" or "jammer")
        {
            e["skill_clock"] = C.N(e, "skill_clock") - dt;
            if (C.N(e, "skill_clock") <= 0)
            {
                e["skill_clock"] = role == "weaver" ? CombatCatalog.Current.Values.WeaverCooldown : CombatCatalog.Current.Values.JammerCooldown;
                if (role == "weaver")
                    WeaveEnergy(e);
                else
                {
                    var defender = PickEnemyDefender(e);
                    if (defender.Count > 0)
                    {
                        defender["jammed_until"] = Clock + CombatCatalog.Current.Values.JammerDuration;
                        AddBeam(C.V(e, "space_position"), GetDroneWorldPosition(defender), CombatScale.Violet);
                    }
                }
            }
        }
        bool managed = role is "rock" or "siege" or "prism" || boss != "";
        if (!managed)
            return false;
        e["fire"] = C.N(e, "fire") - dt;
        if (C.S(e, "skill_phase") != "charging")
        {
            if (C.N(e, "fire") > 0)
                return true;
            if (!CanBombardEarth(e) && PickEnemyDefender(e).Count == 0)
                return true;
            e["charge_total"] = role == "siege" || boss is "forge" or "prism" ? CombatCatalog.Current.Values.HeavyChargeSeconds : CombatCatalog.Current.Values.LightChargeSeconds;
            e["charge_remaining"] = e["charge_total"];
            e["skill_phase"] = "charging";
        }
        e["charge_remaining"] = Math.Max(0, C.N(e, "charge_remaining") - dt);
        e["telegraph"] = C.Clamp(1 - C.N(e, "charge_remaining") / C.N(e, "charge_total"), 0, 1);
        if (C.N(e, "charge_remaining") <= 0)
        {
            bool spore = role == "siege" || boss is "brood" or "forge";
            if (CanBombardEarth(e))
                FireHostile(e, C.N(e, "ground_damage", 4), spore);
            else
            {
                var defender = PickEnemyDefender(e);
                if (defender.Count > 0)
                {
                    if (role == "prism" || boss == "prism")
                    {
                        AddBeam(C.V(e, "space_position"), GetDroneWorldPosition(defender), CombatScale.Violet, laser: true);
                        ApplyDroneDamage(defender, C.N(e, "attack_damage", 10));
                    }
                    else
                        FireAtDrone(e, defender, spore);
                }
            }
            if (boss == "forge")
                e["armor_exposed_until"] = Clock + CombatCatalog.Current.Values.ForgeExposureSeconds;
            e["fire"] = C.N(e, "attack_cooldown", 2);
            e["skill_phase"] = "recover";
            e["telegraph"] = 0d;
        }
        return true;
    }
    private void WeaveEnergy(DataMap e)
    {
        var links = C.A(e, "energy_links");
        links.RemoveAll(v => EnemyByUid(DataMap.Integer(v, -1)) == null);
        foreach (var target in Neighbors(C.V(e, "space_position"), CombatCatalog.Current.Values.WeaverRange, new long[] { C.L(e, "uid") }, (int)CombatCatalog.Current.Values.WeaverMaxLinks))
        {
            if (C.B(target, "shield_broken") || Clock < C.N(target, "energy_recovery_blocked_until"))
                continue;
            if (C.N(target, "energy_max_hp") <= 0)
            {
                if (C.B(target, "energy_granted") || links.Count >= CombatCatalog.Current.Values.WeaverMaxLinks)
                    continue;
                target["energy_granted"] = true;
                target["energy_owner_uid"] = C.L(e, "uid");
                target["energy_max_hp"] = C.N(target, "max_hp") * CombatCatalog.Current.Values.WeaverShieldFraction;
                target["energy_hp"] = target["energy_max_hp"];
                target["energy_recovery_budget"] = C.N(target, "energy_max_hp") * CombatCatalog.Current.Values.WeaverRecoveryBudget;
                links.Add(C.L(target, "uid"));
            }
            else
            {
                double budget = C.N(target, "energy_recovery_budget", C.N(target, "energy_max_hp") * CombatCatalog.Current.Values.WeaverRecoveryBudget), amount = Math.Min(budget, Math.Min(C.N(target, "energy_max_hp") - C.N(target, "energy_hp"), C.N(target, "energy_max_hp") * CombatCatalog.Current.Values.WeaverRecoveryFraction));
                target["energy_hp"] = C.N(target, "energy_hp") + amount;
                target["energy_recovery_budget"] = budget - amount;
            }
            AddBeam(C.V(e, "space_position"), C.V(target, "space_position"), new Color("88adf1"));
        }
        e["energy_links"] = links;
    }
}

