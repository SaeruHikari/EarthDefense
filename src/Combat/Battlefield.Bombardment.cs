using System;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private static bool UsesStationaryBombardment(DataMap enemy) => C.S(enemy, "kind") is "scout" or "carrier" && !C.B(enemy, "post_carrier");
    private static double MinimumBombardmentRadius(DataMap enemy) => Math.Max(CombatScale.ShieldShellRadius, CombatScale.EarthCollisionRadius + Math.Max(0, C.N(enemy, "hit_radius")) + .025);
    private static double StationaryBombardmentRadius(DataMap enemy) => Math.Max(CombatScale.CloseAssault, MinimumBombardmentRadius(enemy));
    private static bool InsideStationaryBombardmentZone(DataMap enemy)
    {
        double radius = C.V(enemy, "space_position").Length();
        return radius >= MinimumBombardmentRadius(enemy) - .0001 && radius <= StationaryBombardmentRadius(enemy) + .0001;
    }
    private static void ClearStationaryBombardment(DataMap enemy)
    {
        if (!C.B(enemy, "stationary_bombard"))
            return;
        enemy.Remove("stationary_bombard");
        enemy.Remove("aim_direction");
        enemy.Remove("aim_up");
    }
    private void HoldBombardmentPosition(DataMap enemy, double dt)
    {
        bool arriving = C.S(enemy, "phase") != "ground_attack";
        enemy["phase"] = "ground_attack";
        enemy["stationary_bombard"] = true;
        // space_position is the existing saved world-space anchor. Do not attach it
        // to the rotating surface or duplicate it in a second persistent position.
        var position = C.V(enemy, "space_position");
        var heading = -position.Normalized();
        enemy["velocity"] = Vector3.Zero;
        enemy["route_speed"] = 0d;
        enemy["tangent"] = heading; // Enemy renderer consumes tangent even at zero velocity.
        enemy["aim_direction"] = heading;
        enemy["aim_up"] = CombatGeometry.OrthogonalUp(heading, C.V(enemy, "aim_up", Vector3.Up));
        enemy["target_space"] = C.Scale(position.Normalized(), CombatScale.EarthRadius - .1);
        if (arriving && C.N(enemy, "bombard_time") <= 0)
            enemy["fire"] = Math.Min(C.N(enemy, "fire"), 0);
        enemy["bombard_time"] = C.N(enemy, "bombard_time") + Math.Max(0, dt);
        if (C.N(enemy, "bombard_time") >= C.N(enemy, "bombard_duration"))
        {
            enemy["phase"] = "retreat";
            enemy["exit_age"] = 0d;
            ClearStationaryBombardment(enemy);
        }
    }
    private void AdvanceToBombardmentPosition(DataMap enemy, double dt, double speed)
    {
        var previous = C.V(enemy, "space_position");
        double radius = previous.Length();
        if (InsideStationaryBombardmentZone(enemy))
        {
            HoldBombardmentPosition(enemy, dt);
            return;
        }
        ClearStationaryBombardment(enemy);
        enemy["phase"] = "approach";
        double desired = StationaryBombardmentRadius(enemy), step = Math.Max(0, speed * dt);
        double next = radius + Math.Clamp(desired - radius, -step, step);
        enemy["space_position"] = C.Scale(previous.Normalized(), next);
        if (InsideStationaryBombardmentZone(enemy))
            HoldBombardmentPosition(enemy, dt);
        else
            UpdateEnemyFlightHeading(enemy, previous, dt);
    }
    private static void UpdateEnemyFlightHeading(DataMap enemy, Vector3 previous, double dt)
    {
        var velocity = C.Scale(C.V(enemy, "space_position") - previous, 1 / Math.Max(dt, .000001));
        enemy["velocity"] = velocity;
        if (velocity.LengthSquared() <= .000001)
            return;
        var heading = velocity.Normalized();
        var old = C.V(enemy, "tangent", heading);
        enemy["tangent"] = old.Slerp(heading, (float)Math.Min(1, 2.0 * dt / Math.Max(old.AngleTo(heading), .000001))).Normalized();
    }
}
