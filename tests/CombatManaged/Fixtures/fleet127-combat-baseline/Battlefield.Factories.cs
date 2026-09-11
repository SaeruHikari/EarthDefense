using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private List<(int Berth, string Frame, int Cost)> ProductionPlan(DataMap f)
    {
        int capacity = C.I(f, "capacity");
        var cacheKey = (C.L(f, "site_id"), capacity);
        if (_productionPlans.TryGetValue(cacheKey, out var cached))
            return cached;
        var rows = new List<(int, string, int)>();
        for (int berth = 0; berth < capacity;)
        {
            string frame = Game.FactoryAirframe(C.S(f, "kind"), C.L(f, "site_id"), berth);
            int cost = C.I(Frame(frame), "capacity_cost", 1);
            if (berth + cost <= capacity)
                rows.Add((berth, frame, cost));
            berth += cost;
        }
        _productionPlans[cacheKey] = rows;
        return rows;
    }
    private double ProductionInterval(string kind, long site, string frame = "")
    {
        var key = (site, frame);
        if (!_productionIntervals.TryGetValue(key, out double value))
        {
            value = Math.Max(.5, Game.FactorySpawnIntervalForSite(kind, site, frame));
            _productionIntervals[key] = value;
        }
        return value;
    }
    private HashSet<int> Occupied(long site)
    {
        var occupied = new HashSet<int>();
        foreach (var d in Drones)
            if (C.L(d, "factory_site_id") == site)
                for (int i = 0; i < C.I(d, "capacity_cost", 1); i++)
                    occupied.Add(C.I(d, "patrol_slot") + i);
        return occupied;
    }
    private static bool SlotFree(HashSet<int> occupied, int berth, int cost)
    {
        for (int i = 0; i < cost; i++)
            if (occupied.Contains(berth + i))
                return false;
        return true;
    }
    private (int Berth, string Frame, int Cost) NextSlot(List<(int Berth, string Frame, int Cost)> plan, HashSet<int> occupied)
    {
        foreach (var row in plan)
            if (SlotFree(occupied, row.Berth, row.Cost))
                return row;
        return (-1, "K1", 1);
    }
    public void UpdateFactories(double dt)
    {
        if (!Active || Paused || Dead)
            return;
        FactoryActivity.Clear();
        var occupants = new Dictionary<long, HashSet<int>>();
        foreach (var f in Factories.Values)
        {
            f["active"] = 0;
            f["used_points"] = 0;
            occupants[C.L(f, "site_id")] = new();
        }
        foreach (var d in Drones)
            if (Factories.TryGetValue(C.L(d, "factory_site_id"), out var f))
            {
                f["active"] = C.I(f, "active") + 1;
                int cost = C.I(d, "capacity_cost", 1);
                f["used_points"] = C.I(f, "used_points") + cost;
                for (int i = 0; i < cost; i++)
                    occupants[C.L(f, "site_id")].Add(C.I(d, "patrol_slot") + i);
            }
        foreach (var f in Factories.Values)
        {
            long site = C.L(f, "site_id");
            string kind = C.S(f, "kind");
            var plan = ProductionPlan(f);
            var occupied = occupants[site];
            var next = NextSlot(plan, occupied);
            double interval = ProductionInterval(kind, site, next.Berth < 0 ? Game.FactoryAirframe(kind, site, 0) : next.Frame);
            int lanes = Game.FactoryProductionLanes(kind, site);
            f["door"] = Math.Max(0, C.N(f, "door") - dt);
            if (next.Berth >= 0)
            {
                f["timer"] = C.N(f, "timer") + dt;
                if (lanes > 1)
                    f["second_timer"] = C.N(f, "second_timer") + dt;
                f["assembly_elapsed"] = C.N(f, "assembly_elapsed") + dt;
                for (int lane = 0; lane < lanes && next.Berth >= 0; lane++)
                {
                    string key = lane == 0 ? "timer" : "second_timer";
                    if (C.N(f, key) + 1e-7 < interval)
                        continue;
                    f[key] = Math.Max(0, C.N(f, key) - interval);
                    SpawnFactoryDrone(f, next.Berth);
                    f["active"] = C.I(f, "active") + 1;
                    f["used_points"] = C.I(f, "used_points") + next.Cost;
                    for (int i = 0; i < next.Cost; i++)
                        occupied.Add(next.Berth + i);
                    f["door"] = CombatScale.LaunchDuration + .4;
                    f["assembly_credit"] = 0d;
                    f["assembly_elapsed"] = 0d;
                    next = NextSlot(plan, occupied);
                    interval = ProductionInterval(kind, site, next.Berth < 0 ? Game.FactoryAirframe(kind, site, 0) : next.Frame);
                }
            }
            else
            {
                f["timer"] = 0d;
                f["second_timer"] = 0d;
                f["assembly_credit"] = 0d;
                f["assembly_elapsed"] = 0d;
            }
            double progress = C.Clamp(C.N(f, "timer") / interval, 0, 1), preopen = next.Berth >= 0 ? C.Clamp((C.N(f, "timer") - (interval - .5)) / .35, 0, 1) : 0;
            FactoryActivity.Add(new()
            {
                ["site_id"] = site,
                ["phase"] = Math.Max(preopen, C.Clamp(C.N(f, "door") / .3, 0, 1)),
                ["progress"] = progress,
                ["active"] = C.I(f, "active"),
                ["capacity"] = C.I(f, "capacity"),
                ["used_points"] = C.I(f, "used_points"),
                ["lanes"] = lanes
            });
        }
    }
    public DataMap SpawnFactoryDrone(DataMap factory, int requestedSlot = -1)
    {
        var site = C.M(factory, "site");
        var origin = C.V(site, "launch_position");
        var direction = C.V(site, "launch_direction", Vector3.Right).Normalized();
        var normal = C.V(site, "normal", Vector3.Back);
        string kind = C.S(factory, "kind");
        long owner = C.L(factory, "site_id");
        var occupied = Occupied(owner);
        int slot = requestedSlot;
        if (slot < 0)
        {
            slot = 0;
            while (occupied.Contains(slot))
                slot++;
        }
        string frame = Game.FactoryAirframe(kind, owner, slot);
        int cost = C.I(Frame(frame), "capacity_cost", 1);
        var d = new DataMap { ["uid"] = NewUid(), ["factory_site_id"] = owner, ["patrol_slot"] = slot, ["airframe_id"] = frame, ["capacity_cost"] = cost, ["state"] = "launching", ["normal"] = normal, ["axis"] = CombatGeometry.AxisBetween(normal, Vector3.Up), ["tangent"] = direction, ["kind"] = kind, ["altitude"] = CombatScale.DroneAltitude, ["angular_speed"] = .22, ["fire"] = 0d, ["flash"] = 0d, ["hp"] = 45d, ["max_hp"] = 45d, ["hit_radius"] = .11 * GetDroneScale(), ["hit"] = 0d, ["launch_age"] = 0d, ["flight_age"] = 0d, ["spawn_space"] = origin, ["launch_direction"] = direction, ["space_position"] = origin, ["world_up"] = origin.Normalized(), ["velocity"] = Vector3.Zero, ["target_uid"] = -1L, ["aim_target_uid"] = -1L, ["aim_direction"] = direction, ["aim_up"] = CombatGeometry.OrthogonalUp(direction, origin.Normalized()) };
        var profile = GetProfile(d);
        d["max_hp"] = C.N(profile.Patrol, "health", 45);
        d["hp"] = d["max_hp"];
        d["hit_radius"] = .11 * GetDroneScale() * C.N(profile.Weapons, "scale_multiplier", 1);
        Drones.Add(d);
        _droneById[C.L(d, "uid")] = d;
        CachePosition(d);
        return d;
    }
    public DataMap FleetCapacityState()
    {
        int capacity = 0, used = 0, pending = 0;
        foreach (var d in Drones)
            used += C.I(d, "capacity_cost", 1);
        foreach (var f in Factories.Values)
        {
            capacity += C.I(f, "capacity");
            var occupied = Occupied(C.L(f, "site_id"));
            foreach (var row in ProductionPlan(f))
                if (SlotFree(occupied, row.Berth, row.Cost))
                {
                    pending++;
                    for (int i = 0; i < row.Cost; i++)
                        occupied.Add(row.Berth + i);
                }
        }
        return new()
        {
            ["active"] = Drones.Count,
            ["capacity_points"] = capacity,
            ["used_points"] = used,
            ["pending"] = pending
        };
    }
    public DataMap GetFleetSummary()
    {
        var result = new DataMap { ["total_active"] = Drones.Count, ["total_capacity"] = 0, ["destroyed"] = DestroyedDrones, ["factory_spawn_interval"] = C.N(Game.CombatSettings, "factory_spawn_interval", 5) };
        foreach (var kind in new[] { "interceptor", "laser", "missile" })
            result[kind] = new DataMap { ["active"] = 0, ["capacity"] = 0, ["returning"] = 0, ["repairing"] = 0, ["suiciding"] = 0 };
        foreach (var f in Factories.Values)
        {
            var row = C.M(result, C.S(f, "kind"));
            row["capacity"] = C.I(row, "capacity") + C.I(f, "capacity");
            result["total_capacity"] = C.I(result, "total_capacity") + C.I(f, "capacity");
        }
        foreach (var d in Drones)
        {
            var row = C.M(result, C.S(d, "kind"));
            row["active"] = C.I(row, "active") + 1;
            string phase = C.S(d, "state");
            string key = phase is "returning" or "landing" ? "returning" : phase;
            if (key is "returning" or "repairing" or "suiciding")
                row[key] = C.I(row, key) + 1;
        }
        return result;
    }
    public bool CreditFactoryProduction(long site, double seconds, bool kill, double cooldown = 1)
    {
        if (seconds <= 0 || !Factories.TryGetValue(site, out var f) || C.I(f, "active") >= C.I(f, "capacity"))
            return false;
        if (kill && Clock < C.N(f, "perk_next_kill_credit", -1))
            return false;
        if (kill)
            f["perk_next_kill_credit"] = Clock + Math.Max(1, cooldown);
        double interval = ProductionInterval(C.S(f, "kind"), site), available = Math.Max(0, interval * .5 - C.N(f, "assembly_credit")), applied = Math.Min(seconds, Math.Min(available, Math.Max(0, interval * .75 - C.N(f, "timer"))));
        if (applied <= 0)
            return false;
        f["assembly_credit"] = C.N(f, "assembly_credit") + applied;
        f["timer"] = Math.Min(interval * .75, C.N(f, "timer") + applied);
        return true;
    }
}

