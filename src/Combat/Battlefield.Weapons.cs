using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private static readonly double[] SingleWeights = { 1 }, TripleWeights = { 1, .5, .5 }, SalvoAngles = { 0, -.075, .075 };
    public static int MissileSalvoCount(DataMap stats) => Math.Max(1, stats.I("missile_salvo", 1) + Math.Max(0, stats.I("missile_extra_projectiles")));
    private static IEnumerable<double> SalvoWeights(DataMap stats) => stats.TryGetValue("bullet_damage_weights", out var value) && value is IEnumerable<object?> values ? values.Select(v => DataMap.Number(v)) : C.I(stats, "bullet_count", 1) > 1 ? TripleWeights : SingleWeights;
    private void UpdateFiring(double dt)
    {
        int epoch = _epoch;
        _projectileSectors.Clear();
        foreach (var s in HostileShots)
            if (C.B(s, "interceptable") && C.N(s, "hp") > 0)
            {
                var key = Sector(C.V(s, "space_position"), 2);
                if (!_projectileSectors.TryGetValue(key, out var list))
                    _projectileSectors[key] = list = new();
                list.Add(s);
            }
        bool pointDefenseEnabled = _projectileSectors.Count > 0 && HasTech("K_A1");
        for (int i = 0; i < Drones.Count; i++)
        {
            var d = Drones[i];
            if (C.N(d, "launch_age", CombatScale.LaunchDuration) < CombatScale.LaunchDuration || C.S(d, "state") is "landing" or "repairing")
                continue;
            d["fire"] = C.N(d, "fire") - dt;
            if (C.N(d, "fire") > 0)
                continue;
            var target = EnemyByUid(C.L(d, "aim_target_uid", -1));
            if (target == null && (!pointDefenseEnabled || C.S(d, "kind") != "interceptor"))
            {
                d["fire"] = .02;
                continue;
            }
            var origin = GetDroneMuzzlePosition(d);
            if (TryPointDefense(d, origin))
                continue;
            if (target == null)
            {
                d["fire"] = .02;
                continue;
            }
            string kind = C.S(d, "kind");
            var stats = GetProfile(d).Weapons;
            if (EnemyArmor.Multiplier(target, EnemyArmor.Family(kind), stats, Clock) <= 0)
            {
                d["fire"] = .08;
                continue;
            }
            var aim = AimTargetPosition(d, target, origin);
            var direction = (aim - origin).Normalized();
            if (!CanEngage(d, C.V(target, "space_position"), target) || !CombatGeometry.HasLineOfSight(origin, aim))
            {
                d["fire"] = .08;
                continue;
            }
            if (C.V(d, "aim_direction").Dot(direction) < .999390827)
            {
                d["fire"] = .02;
                continue;
            }
            if (!FrameWeaponReady(d, target, stats, dt))
            {
                d["fire"] = .02;
                continue;
            }
            double damage = LaunchDamage(d, stats, C.N(stats, kind == "interceptor" ? "damage" : kind + "_damage"));
            damage = FrameFiringDamage(d, target, stats, damage);
            var source = WeaponSource(d, stats);
            source["weapon_range"] = TargetRange(d, target);
            double rate = C.N(stats, kind == "interceptor" ? "fire_rate" : kind + "_fire_rate");
            if (Clock < C.N(d, "dense_until"))
                rate *= C.N(stats, "k2_dense_fire_rate_multiplier", 1);
            d["fire"] = 1 / Math.Max(.3, rate) * Random.Range(.9, 1.1);
            d["total_weapon_shots"] = C.L(d, "total_weapon_shots") + 1;
            d["flash"] = 1d;
            if (kind == "laser")
            {
                if (C.S(d, "airframe_id") == "L3")
                    FireSuppressionPulse(d, target, stats);
                for (int beam = 0; beam < C.I(stats, "laser_beam_count", 1); beam++)
                {
                    FireLaser(origin + C.Scale(C.V(d, "aim_up"), beam * .015), target, damage, stats, source);
                    if (epoch != _epoch)
                        return;
                }
            }
            else if (kind == "missile")
            {
                int salvo = MissileSalvoCount(stats);
                for (int j = 0; j < salvo; j++)
                    MakeShot(origin, aim, damage, true, (j - (salvo - 1) * .5) * .045, stats, source);
            }
            else
            {
                var weights = SalvoWeights(stats).ToArray();
                if (C.S(d, "airframe_id") == "K2")
                {
                    var volley = new List<DataMap> { target };
                    foreach (var other in Neighbors(C.V(target, "space_position"), GetProfile(d).Range, new long[] { C.L(target, "uid") }, 8))
                    {
                        if (volley.Count >= 3)
                            break;
                        if (EnemyArmor.Multiplier(other, EnemyArmor.Family(kind), stats, Clock) > 0 && CanEngage(d, C.V(other, "space_position"), other))
                            volley.Add(other);
                    }
                    double budget = weights.Sum();
                    foreach (var other in volley)
                    {
                        var packet = C.Shallow(source);
                        packet["primary_target_uid"] = C.L(other, "uid");
                        MakeShot(origin, AimTargetPosition(d, other, origin), damage * budget / volley.Count, false, 0, stats, packet);
                    }
                    continue;
                }
                for (int j = 0; j < Math.Min(3, weights.Length); j++)
                    MakeShot(origin, aim, damage * weights[j], false, SalvoAngles[j], stats, source);
            }
            if (InvasionWon || epoch != _epoch)
            {
                Shots.Clear();
                Beams.Clear();
                return;
            }
        }
    }
    public double FireLaser(Vector3 origin, DataMap target, double damage, DataMap? stats = null, DataMap? source = null)
    {
        int epoch = _epoch;
        var destination = C.V(target, "space_position");
        if (!CombatGeometry.HasLineOfSight(origin, destination))
            return 0;
        AddBeam(origin, destination, CombatScale.Violet, laser: true);
        var context = source ?? new()
        {
            ["effects"] = Capture(stats ?? C.Empty, PerkEffectKeys)
        };
        context["damage_type"] = "beam";
        context["primary"] = true;
        var effects = C.M(context, "effects");
        double hit = ApplyEnemyDamage(target, damage, CombatScale.Violet, context);
        if (epoch != _epoch)
            return hit;
        if (hit > 0)
            ApplyErosion(target, effects);
        int count = Math.Clamp(C.I(effects, "refraction_targets"), 0, 2);
        if (count > 0)
        {
            double fraction = C.Clamp(C.N(effects, "refraction_damage_retention", .35), 0, 1);
            foreach (var other in Neighbors(destination, 2, new long[] { C.L(target, "uid") }, count))
            {
                AddBeam(destination, C.V(other, "space_position"), new("a7def5"), laser: true);
                ApplyEnemyDamage(other, damage * fraction, CombatScale.Violet, SecondarySource(context));
                if (epoch != _epoch)
                    return hit;
            }
        }
        return hit;
    }
    public DataMap MakeShot(Vector3 origin, Vector3 target, double damage, bool missile, double spread = 0, DataMap? stats = null, DataMap? source = null)
    {
        stats ??= _globalWeapons;
        var direction = (target - origin).Normalized();
        var axis = direction.Cross(origin.Normalized());
        if (axis.LengthSquared() < .0001)
            axis = direction.Cross(Math.Abs(direction.Y) < .9 ? Vector3.Up : Vector3.Right);
        direction = direction.Rotated(axis.Normalized(), (float)spread);
        double speed = missile ? 3.6 * C.N(stats, "missile_speed_multiplier", 1) : 6.4 * C.N(stats, "projectile_speed_multiplier", 1), range = C.N(source, "weapon_range", WeaponRangeForStats(missile ? "missile" : "interceptor", stats) * C.N(stats, "frame_range_multiplier", 1)), life = Math.Max(missile ? 2.8 : 1.1, range / Math.Max(speed, .001) + .15);
        var shot = new DataMap { ["uid"] = NewUid(), ["space_position"] = origin, ["velocity"] = C.Scale(direction, speed), ["tangent"] = direction, ["kind"] = missile ? "missile" : "friendly", ["life"] = life, ["damage"] = damage, ["missile"] = missile, ["range"] = range, ["projectile_speed"] = speed, ["turn_rate"] = C.N(stats, "missile_turn_rate", 3.5), ["blast_radius"] = C.N(stats, "missile_blast_radius", 96 / CombatScale.PlanetPixelRadius) };
        shot["source"] = source ?? new()
        {
            ["effects"] = Capture(stats, PerkEffectKeys)
        };
        var packet = C.M(shot, "source");
        if (missile) CaptureMissileBlastMultiplier(packet, stats);
        packet["damage_type"] = missile ? "explosive" : "kinetic";
        packet["primary"] = true;
        shot["hit_ids"] = new List<object?>();
        shot["pierce_left"] = missile ? 0 : Math.Clamp(C.I(C.M(packet, "effects"), "pierce_extra_targets"), 0, 3);
        shot["base_damage"] = damage;
        Shots.Add(shot);
        return shot;
    }
    private bool FrameWeaponReady(DataMap d, DataMap target, DataMap stats, double dt)
    {
        string frame = C.S(d, "airframe_id", C.S(stats, "airframe_id", "K1"));
        long uid = C.L(target, "uid");
        double warmup = C.N(stats, "warmup_seconds");
        if (frame == "K3")
            warmup = .6;
        if (frame == "M3" && stats.ContainsKey("m3_escort_lock_multiplier"))
        {
            var nearby = QueryDefender(GetDroneWorldPosition(d), false);
            if (nearby.Count > 0 && C.L(nearby, "uid") != C.L(d, "uid") && C.S(nearby, "kind") == "interceptor" && GetDroneWorldPosition(nearby).DistanceTo(GetDroneWorldPosition(d)) <= C.N(stats, "m3_escort_radius", 1.8))
                warmup *= C.N(stats, "m3_escort_lock_multiplier");
        }
        if (warmup <= 0)
            return true;
        double last = C.N(d, "lock_tick", Clock - dt), elapsed = Math.Max(0, Clock - last);
        if (C.L(d, "lock_uid", -1) != uid)
        {
            double retained = Clock - C.N(d, "lock_lost_at", last) <= C.N(stats, "l2_memory_seconds") ? C.N(d, "lock_progress") * C.N(stats, "l2_memory_fraction") : 0;
            d["lock_progress"] = retained;
            d["lock_uid"] = uid;
            elapsed = 0;
        }
        if (frame == "K3" && C.V(d, "velocity").Length() > .25)
            elapsed *= C.N(stats, "k3_mobile_warmup_retention", .35);
        d["lock_tick"] = Clock;
        d["lock_progress"] = Math.Min(warmup, C.N(d, "lock_progress") + elapsed);
        d["fire_charge"] = C.Clamp(C.N(d, "lock_progress") / Math.Max(warmup, .001), 0, 1);
        return frame == "L2" || C.N(d, "lock_progress") >= warmup;
    }
    private double FrameFiringDamage(DataMap d, DataMap target, DataMap stats, double damage)
    {
        string frame = C.S(d, "airframe_id", C.S(stats, "airframe_id", "K1"));
        if (frame == "K3" && stats.ContainsKey("k3_mobile_warmup_retention"))
            damage *= C.N(stats, "k3_peak_damage_multiplier", .9);
        if (frame == "M3" && C.Large(target))
            damage *= C.N(stats, "frame_large_target_multiplier", 1.75);
        if (frame == "M2" && stats.ContainsKey("m2_shock_required") && C.I(d, "shock_hits") >= C.I(stats, "m2_shock_required") && Clock >= C.N(d, "shock_next"))
        {
            damage *= C.N(stats, "m2_next_damage_multiplier");
            d["shock_hits"] = 0;
            d["shock_next"] = Clock + C.N(stats, "m2_shock_cooldown", 4);
        }
        if (frame == "L2")
        {
            if (C.N(d, "lock_progress") >= C.N(stats, "warmup_seconds", 2))
                damage *= C.N(stats, "warmup_damage_multiplier", 1.65);
            if (EnemyArmor.Layer(target) == "energy")
            {
                double time = Math.Max(0, Clock - C.N(d, "resonance_started", Clock));
                if (C.L(d, "resonance_uid", -1) != C.L(target, "uid"))
                {
                    d["resonance_uid"] = C.L(target, "uid");
                    d["resonance_started"] = Clock;
                    time = 0;
                }
                damage *= 1 + Math.Min(C.N(stats, "l2_resonance_cap"), time * C.N(stats, "l2_resonance_per_second"));
            }
        }
        if (frame == "L3")
            damage *= C.N(stats, "l3_damage_multiplier", 1);
        if (C.S(d, "kind") == "interceptor" && C.B(C.M(stats, "tech_abilities"), "K_G1"))
        {
            if (Clock - C.N(d, "continuous_last", -100) > 1.5)
                d["continuous_start"] = Clock;
            d["continuous_last"] = Clock;
            if (Clock >= C.N(d, "burst_ready") && Clock - C.N(d, "continuous_start", Clock) >= C.N(stats, "kinetic_burst_warmup", 2))
            {
                d["burst_until"] = Clock + C.N(stats, "kinetic_burst_duration", 3);
                d["burst_ready"] = Clock + C.N(stats, "kinetic_burst_cooldown", 8);
            }
            if (Clock < C.N(d, "burst_until"))
                damage *= C.N(stats, "kinetic_burst_multiplier", 2);
        }
        return damage;
    }
    private void FireSuppressionPulse(DataMap d, DataMap target, DataMap stats)
    {
        if (Clock < C.N(d, "pulse_next"))
            return;
        d["pulse_next"] = Clock + C.N(stats, "suppression_cooldown", 3);
        double radius = C.N(stats, "suppression_radius", 1.8) * C.N(stats, "l3_pulse_radius_multiplier", 1);
        foreach (var e in Neighbors(C.V(target, "space_position"), radius, null, 0))
        {
            if (Clock < C.N(e, "l3_pulse_ready"))
                continue;
            double delay = (C.N(stats, "suppression_delay", .5) + C.N(stats, "l3_afterpulse_seconds")) * (C.Large(e) ? .5 : 1);
            e["l3_pulse_ready"] = Clock + Math.Max(1.5, delay * 2);
            e["fire"] = C.N(e, "fire") + delay;
        }
        AddBurst(C.V(target, "space_position"), CombatScale.Violet, radius * 20);
    }
    private void QueueDelayedBlast(Vector3 at, double radius, double damage, double delay, DataMap source)
    {
        if (Shots.Count > 4096)
            return;
        Shots.Add(new()
        {
            ["uid"] = NewUid(),
            ["space_position"] = at,
            ["velocity"] = Vector3.Zero,
            ["tangent"] = Vector3.Forward,
            ["kind"] = "missile",
            ["missile"] = true,
            ["life"] = delay,
            ["damage"] = damage,
            ["blast_radius"] = radius,
            ["source"] = source,
            ["delayed_effect"] = "blast",
            ["secondary"] = true,
            ["hit_ids"] = new List<object?>()
        });
    }
    private void CreateSuppressionField(Vector3 at, double radius, double duration)
    {
        if (Shots.Count(s => s.ContainsKey("delayed_effect")) >= 64)
            return;
        Shots.Add(new()
        {
            ["uid"] = NewUid(),
            ["space_position"] = at,
            ["velocity"] = Vector3.Zero,
            ["tangent"] = Vector3.Forward,
            ["kind"] = "friendly",
            ["missile"] = false,
            ["life"] = duration,
            ["damage"] = 0d,
            ["blast_radius"] = radius,
            ["delayed_effect"] = "shield_field",
            ["query_clock"] = 0d,
            ["hit_ids"] = new List<object?>()
        });
    }
    private bool TryPointDefense(DataMap d, Vector3 origin)
    {
        if (_projectileSectors.Count == 0 || C.S(d, "kind") != "interceptor" || !HasTech("K_A1"))
            return false;
        // The firepower fraction spendable on interception is table-driven by the owning technology node (K_A1 25%, K_G2 50%).
        double budget = C.N(_globalWeapons, "point_defense_budget", .25);
        int maximum = Math.Max(1, (int)Math.Round(1 / Math.Max(.05, budget)));
        long spent = C.L(d, "point_defense_shots"), total = C.L(d, "total_weapon_shots", maximum);
        if (spent * maximum >= total)
            return false;
        double range = Math.Min(4, GetProfile(d).Range * (HasTech("K_G2") ? 1.15 : .85)), distance = range * range;
        DataMap? best = null;
        var lower = Sector(origin - C.Scale(Vector3.One, range), 2);
        var upper = Sector(origin + C.Scale(Vector3.One, range), 2);
        for (int x = lower.X; x <= upper.X; x++)
            for (int y = lower.Y; y <= upper.Y; y++)
                for (int z = lower.Z; z <= upper.Z; z++)
                {
                    if (!_projectileSectors.TryGetValue(new(x, y, z), out var list))
                        continue;
                    foreach (var shot in list)
                    {
                        if (C.N(shot, "hp") <= 0)
                            continue;
                        double dd = origin.DistanceSquaredTo(C.V(shot, "space_position"));
                        if (dd < distance && CombatGeometry.HasLineOfSight(origin, C.V(shot, "space_position")))
                        {
                            best = shot;
                            distance = dd;
                        }
                    }
                }
        if (best == null)
            return false;
        var stats = GetProfile(d).Weapons;
        best["hp"] = Math.Max(0, C.N(best, "hp") - C.N(stats, "damage"));
        d["point_defense_shots"] = spent + 1;
        d["total_weapon_shots"] = total + 1;
        d["fire"] = 1 / Math.Max(.3, C.N(stats, "fire_rate"));
        AddBeam(origin, C.V(best, "space_position"), CombatScale.Cyan);
        if (C.N(best, "hp") <= 0)
            AddBurst(C.V(best, "space_position"), CombatScale.Cyan, 10);
        return true;
    }
}
