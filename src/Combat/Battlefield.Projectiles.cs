using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private sealed class ColliderGrid
    {
        public readonly List<(DataMap Actor, Vector3 Center, double Radius, Aabb Bounds)> Items = new();
        private readonly Dictionary<Vector3I, List<int>> _cells = new();
        private readonly Stack<List<int>> _buckets = new();
        private double _maximum;
        public void Build(IEnumerable<DataMap> actors, Func<DataMap, Vector3> position)
        {
            Items.Clear();
            foreach (var bucket in _cells.Values) { bucket.Clear(); _buckets.Push(bucket); }
            _cells.Clear();
            _maximum = 0;
            foreach (var actor in actors)
            {
                var p = position(actor);
                double radius = C.N(actor, "hit_radius") + .018;
                var extent = C.Scale(Vector3.One, radius + .00001);
                int i = Items.Count;
                Items.Add((actor, p, radius, new Aabb(p - extent, extent * 2)));
                _maximum = Math.Max(_maximum, radius);
                var key = Sector(p, .75);
                if (!_cells.TryGetValue(key, out var bucket))
                    _cells[key] = bucket = _buckets.Count > 0 ? _buckets.Pop() : new();
                bucket.Add(i);
            }
        }
        public IEnumerable<int> Query(Aabb bounds, bool smallFallback = false)
        {
            var expanded = bounds.Grow((float)(_maximum + .00002));
            var lo = Sector(expanded.Position, .75);
            var hi = Sector(expanded.End, .75);
            long cells = (long)(hi.X - lo.X + 1) * (hi.Y - lo.Y + 1) * (hi.Z - lo.Z + 1);
            if (smallFallback && Items.Count <= 48 || cells > Math.Max(27, Items.Count * 2))
            {
                for (int i = 0; i < Items.Count; i++)
                    yield return i;
                yield break;
            }
            for (int x = lo.X; x <= hi.X; x++)
                for (int y = lo.Y; y <= hi.Y; y++)
                    for (int z = lo.Z; z <= hi.Z; z++)
                        if (_cells.TryGetValue(new(x, y, z), out var bucket))
                            foreach (int i in bucket)
                                yield return i;
        }
    }
    private readonly ColliderGrid _enemyColliders = new(), _friendlyColliders = new();
    private static Aabb SegmentBounds(Vector3 a, Vector3 b)
    {
        var min = a.Min(b);
        return new(min, a.Max(b) - min);
    }
    public void UpdateShots(double dt)
    {
        SyncLocalShields();
        int epoch = _epoch;
        long colliderStart = Performance?.Timestamp() ?? 0;
        _enemyColliders.Build(_targets, e => C.V(e, "space_position"));
        Performance?.Record(CombatStage.EnemyColliderBuild, colliderStart);
        for (int i = Shots.Count - 1; i >= 0; i--)
        {
            var s = Shots[i];
            var previous = C.V(s, "space_position");
            if (s.ContainsKey("delayed_effect"))
            {
                s["life"] = C.N(s, "life") - dt;
                if (C.S(s, "delayed_effect") == "shield_field")
                {
                    s["query_clock"] = C.N(s, "query_clock") - dt;
                    if (C.N(s, "query_clock") <= 0)
                    {
                        s["query_clock"] = .25;
                        foreach (var e in Neighbors(previous, C.N(s, "blast_radius"), null, 0))
                            e["energy_recovery_blocked_until"] = Clock + .3;
                    }
                }
                else if (C.N(s, "life") <= 0)
                    DetonateMissile(previous, C.N(s, "damage"), C.N(s, "blast_radius"), C.M(s, "source"));
                if (epoch != _epoch)
                    return;
                if (C.N(s, "life") <= 0)
                    Shots.RemoveAt(i);
                continue;
            }
            if (C.B(s, "missile"))
            {
                long guidanceStart = Performance?.Timestamp() ?? 0;
                var target = PickTarget(previous, C.N(s, "range", WeaponRangeForStats("missile", _globalWeapons)));
                if (C.S(s, "proc_kind") == "cluster")
                {
                    target = EnemyByUid(C.L(s, "homing_target_uid", -1));
                    var ids = C.A(s, "hit_ids").Select(v => DataMap.Integer(v)).ToArray();
                    if (target == null || ids.Contains(C.L(target, "uid")))
                        target = Neighbors(previous, C.N(s, "range", 2.2), ids, 1).FirstOrDefault();
                }
                if (target != null)
                {
                    var desired = (C.V(target, "space_position") - previous).Normalized();
                    double speed = C.N(s, "projectile_speed", 3.6 * C.N(_globalWeapons, "missile_speed_multiplier", 1));
                    var forward = C.V(s, "velocity").Normalized();
                    double turn = Math.Min(forward.AngleTo(desired), dt * C.N(s, "turn_rate", C.N(_globalWeapons, "missile_turn_rate", 3.5)));
                    s["velocity"] = C.Scale(forward.Rotated(CombatGeometry.AxisBetween(forward, desired), (float)turn).Normalized(), speed);
                }
                Performance?.Record(CombatStage.MissileGuidance, guidanceStart);
            }
            var current = previous + C.Scale(C.V(s, "velocity"), dt);
            s["space_position"] = current;
            s["tangent"] = C.V(s, "velocity").Normalized();
            s["life"] = C.N(s, "life") - dt;
            long impactStart = Performance?.Timestamp() ?? 0;
            bool finished = ResolveFriendlySegment(s, previous, current);
            Performance?.Record(CombatStage.FriendlyImpacts, impactStart);
            if (InvasionWon || epoch != _epoch)
                return;
            if (finished || C.N(s, "life") <= 0)
                Shots.RemoveAt(i);
        }
        if (HostileShots.Count > 0)
        {
            colliderStart = Performance?.Timestamp() ?? 0;
            _friendlyColliders.Build(Drones, GetDroneWorldPosition);
            Performance?.Record(CombatStage.FriendlyColliderBuild, colliderStart);
        }
        long hostileStart = Performance?.Timestamp() ?? 0;
        for (int i = HostileShots.Count - 1; i >= 0; i--)
        {
            var s = HostileShots[i];
            if (C.B(s, "interceptable") && C.N(s, "hp", 1) <= 0)
            {
                HostileShots.RemoveAt(i);
                continue;
            }
            var previous = C.V(s, "space_position");
            var velocity = C.V(s, "velocity");
            double travel = velocity.Length() * Math.Min(dt, Math.Max(0, C.N(s, "life")));
            if (s.ContainsKey("travel_remaining"))
            {
                travel = Math.Min(travel, Math.Max(0, C.N(s, "travel_remaining")));
                s["travel_remaining"] = C.N(s, "travel_remaining") - travel;
            }
            var current = previous + C.Scale(velocity.Normalized(), travel);
            s["space_position"] = current;
            s["life"] = C.N(s, "life") - dt;
            double hit = CombatGeometry.SphereHitFraction(previous, current, Vector3.Zero, CombatScale.EarthCollisionRadius);
            DataMap? victim = null;
            int best = int.MaxValue;
            var bounds = SegmentBounds(previous, current);
            foreach (int index in _friendlyColliders.Query(bounds))
            {
                var c = _friendlyColliders.Items[index];
                if (C.N(c.Actor, "hp") <= 0 || !bounds.Intersects(c.Bounds))
                    continue;
                double contact = CombatGeometry.SphereHitFraction(previous, current, c.Center, c.Radius);
                if (contact < hit || victim != null && contact == hit && index < best)
                {
                    hit = contact;
                    victim = c.Actor;
                    best = index;
                }
            }
            if (InterceptLocalShieldSegment(s, previous, current, hit, "damage", out _))
            {
                HostileShots.RemoveAt(i);
                continue;
            }
            if (!double.IsPositiveInfinity(hit))
            {
                DetonateHostile(previous.Lerp(current, (float)hit), s, victim, victim == null);
                if (InvasionWon || epoch != _epoch)
                    return;
                HostileShots.RemoveAt(i);
                // A first-impact tutorial can pause us from EarthDamaged. The
                // triggering shell is consumed; the remaining volley waits.
                if (Dead || Paused)
                    return;
            }
            else if (C.N(s, "life") <= 0 || C.N(s, "travel_remaining", 1) <= .00001)
                HostileShots.RemoveAt(i);
        }
        Performance?.Record(CombatStage.HostileImpacts, hostileStart);
    }
    public bool ResolveFriendlySegment(DataMap shot, Vector3 previous, Vector3 current)
    {
        int epoch = _epoch;
        double earth = CombatGeometry.SphereHitFraction(previous, current, Vector3.Zero, CombatScale.EarthCollisionRadius);
        var packet = C.M(shot, "source");
        double proximity = C.B(shot, "missile") && C.B(C.M(packet, "tech_abilities"), "M_A1") ? C.Packet(packet, "proximity_fuse_radius", .3) : 0;
        var bounds = SegmentBounds(previous, current).Grow((float)proximity);
        var candidates = _enemyColliders.Query(bounds, true).ToArray();
        var visited = C.A(shot, "hit_ids");
        for (int j = 0; j < 4; j++)
        {
            double hit = earth;
            int best = int.MaxValue;
            DataMap? victim = null;
            foreach (int index in candidates)
            {
                var c = _enemyColliders.Items[index];
                if (!bounds.Intersects(c.Bounds.Grow((float)proximity)) || C.N(c.Actor, "hp") <= 0 || HasVisited(visited, C.L(c.Actor, "uid")))
                    continue;
                double contact = CombatGeometry.SphereHitFraction(previous, current, c.Center, c.Radius + proximity);
                if (contact < hit || victim != null && contact == hit && index < best)
                {
                    hit = contact;
                    victim = c.Actor;
                    best = index;
                }
            }
            if (victim == null)
                return !double.IsPositiveInfinity(earth);
            var point = previous.Lerp(current, (float)hit);
            var source = C.Shallow(packet);
            source["impact_target_uid"] = C.L(victim, "uid");
            visited.Add(C.L(victim, "uid"));
            shot["hit_ids"] = visited;
            if (C.B(shot, "missile"))
            {
                DetonateMissile(C.V(victim, "space_position"), C.N(shot, "damage"), C.N(shot, "blast_radius", -1), source);
                if (epoch != _epoch)
                    return true;
                if (!C.B(shot, "secondary"))
                    SpawnCluster(shot, point, victim);
                return true;
            }
            if (ApplyEnemyDamage(victim, C.N(shot, "damage"), CombatScale.Cyan, source) <= 0 || epoch != _epoch)
                return true;
            if (C.S(shot, "proc_kind") == "ricochet")
                SpawnRicochet(shot, point, C.I(shot, "ricochet_left"));
            else if (!C.B(shot, "ricochet_started"))
            {
                shot["ricochet_started"] = true;
                SpawnRicochet(shot, point, Math.Clamp(C.I(C.M(source, "effects"), "ricochet_targets"), 0, 2));
            }
            if (InvasionWon)
                return true;
            if (C.I(shot, "pierce_left") <= 0)
                return true;
            shot["pierce_left"] = C.I(shot, "pierce_left") - 1;
            shot["damage"] = C.N(shot, "damage") * C.Clamp(C.N(C.M(source, "effects"), "pierce_damage_retention", .65), 0, 1);
        }
        return false;
    }
    private static bool HasVisited(List<object?> visited, long uid)
    {
        foreach (var id in visited) if (DataMap.Integer(id) == uid) return true;
        return false;
    }
    private void SpawnRicochet(DataMap parent, Vector3 origin, int count)
    {
        if (count <= 0)
            return;
        var visited = C.A(parent, "hit_ids");
        var neighbors = Neighbors(origin, 2, visited.Select(v => DataMap.Integer(v)).ToArray(), 1);
        if (neighbors.Count == 0)
            return;
        var destination = C.V(neighbors[0], "space_position");
        var direction = (destination - origin).Normalized();
        double speed = Math.Max(8, C.N(parent, "projectile_speed", 8)), fraction = C.Clamp(C.N(C.M(C.M(parent, "source"), "effects"), "ricochet_damage_retention", .35), 0, 1), damage = C.N(parent, "ricochet_damage", C.N(parent, "base_damage", C.N(parent, "damage")) * fraction);
        var start = origin + C.Scale(direction, .025);
        Shots.Add(new()
        {
            ["uid"] = NewUid(),
            ["kind"] = "friendly",
            ["space_position"] = start,
            ["velocity"] = C.Scale(direction, speed),
            ["tangent"] = direction,
            ["life"] = origin.DistanceTo(destination) / speed + .2,
            ["damage"] = damage,
            ["missile"] = false,
            ["projectile_speed"] = speed,
            ["source"] = SecondarySource(C.M(parent, "source")),
            ["hit_ids"] = new List<object?>(visited),
            ["pierce_left"] = 0,
            ["secondary"] = true,
            ["proc_kind"] = "ricochet",
            ["ricochet_left"] = count - 1,
            ["ricochet_damage"] = damage,
            ["base_damage"] = damage
        });
        AddBeam(origin, start + C.Scale(direction, .22), CombatScale.Gold);
    }
    private void SpawnCluster(DataMap parent, Vector3 origin, DataMap victim)
    {
        if (InvasionWon)
            return;
        var effects = C.M(C.M(parent, "source"), "effects");
        int count = Math.Clamp(C.I(effects, "cluster_fragments"), 0, 3);
        if (count <= 0)
            return;
        double blast = Math.Max(.05, C.N(parent, "blast_radius", .22) * .55);
        var neighbors = Neighbors(origin, 2.2, new long[] { C.L(victim, "uid") }, count);
        var forward = C.V(parent, "velocity", Vector3.Forward).Normalized();
        var axis = CombatGeometry.OrthogonalUp(forward, origin.Normalized());
        double speed = Math.Max(4.5, C.N(parent, "projectile_speed", 4.5) * 1.15);
        for (int i = 0; i < count; i++)
        {
            var direction = forward.Rotated(axis, (float)((i - (count - 1) * .5) * .65));
            var start = origin + C.Scale(direction, .09) + C.Scale(axis, (i - 1) * .035);
            if (i < neighbors.Count)
                direction = (C.V(neighbors[i], "space_position") - start).Normalized();
            Shots.Add(new()
            {
                ["uid"] = NewUid(),
                ["kind"] = "missile",
                ["space_position"] = start,
                ["velocity"] = C.Scale(direction, speed),
                ["tangent"] = direction,
                ["life"] = .8,
                ["damage"] = C.N(parent, "damage") * C.Clamp(C.N(effects, "cluster_damage_retention", .15), 0, 1),
                ["missile"] = true,
                ["range"] = 2.2,
                ["projectile_speed"] = speed,
                ["turn_rate"] = 5d,
                ["blast_radius"] = blast,
                ["source"] = SecondarySource(C.M(parent, "source")),
                ["hit_ids"] = new List<object?> { C.L(victim, "uid") },
                ["pierce_left"] = 0,
                ["secondary"] = true,
                ["proc_kind"] = "cluster",
                ["homing_target_uid"] = i < neighbors.Count ? C.L(neighbors[i], "uid") : -1L
            });
        }
        AddBurst(origin, new("ffd5a0"), 14);
    }
    public void DetonateHostile(Vector3 at, DataMap shot, DataMap? direct = null, bool earthHit = false)
    {
        int epoch = _epoch;
        double damage = C.N(shot, "damage"), radius = Math.Max(0, C.N(shot, "blast_radius"));
        if (earthHit && C.S(shot, "target_kind", "drone") == "earth")
            DamageEarth(damage, at, C.B(shot, "local_shield_checked"));
        if (direct != null)
            ApplyDroneDamage(direct, damage);
        if (epoch != _epoch || radius <= 0 || InvasionWon)
            return;
        AddBurst(at, CombatScale.Coral, radius * CombatScale.PlanetPixelRadius * .5);
        var extent = C.Scale(Vector3.One, radius);
        long uid = C.L(direct, "uid", -1);
        var sight = C.Scale(at.Normalized(), Math.Max(at.Length(), CombatScale.EarthCollisionRadius + .002));
        foreach (int i in _friendlyColliders.Query(new(at - extent, extent * 2)))
        {
            var c = _friendlyColliders.Items[i];
            if (C.L(c.Actor, "uid") == uid || C.N(c.Actor, "hp") <= 0)
                continue;
            double distance = at.DistanceTo(c.Center);
            if (distance > radius || !CombatGeometry.HasLineOfSight(sight, c.Center))
                continue;
            double edge = C.Clamp((distance / radius - .4) / .6, 0, 1);
            ApplyDroneDamage(c.Actor, damage * C.Lerp(1, .35, edge) * C.Clamp(C.N(shot, "secondary_damage_fraction", 1), 0, 1));
            if (epoch != _epoch || InvasionWon)
                return;
        }
    }
}
