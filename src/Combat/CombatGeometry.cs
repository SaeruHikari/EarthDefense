using System;
using Godot;
namespace Earthward.Combat;

public static class CombatGeometry
{
    public static Vector3 AxisBetween(Vector3 from, Vector3 to)
    {
        var axis = from.Cross(to);
        if (axis.LengthSquared() < .000001)
            axis = from.Cross(Math.Abs(from.Y) < .9 ? Vector3.Up : Vector3.Right);
        return axis.Normalized();
    }
    public static Vector3 OrthogonalUp(Vector3 forward, Vector3 preferred)
    {
        var up = preferred - C.Scale(forward, preferred.Dot(forward));
        if (up.LengthSquared() < .000001)
        {
            var fallback = Math.Abs(forward.Y) < .9 ? Vector3.Up : Vector3.Right;
            up = fallback - C.Scale(forward, fallback.Dot(forward));
        }
        return up.Normalized();
    }
    public static bool HasLineOfSight(Vector3 origin, Vector3 target)
    {
        var delta = target - origin;
        double r = CombatScale.EarthCollisionRadius;
        if (origin.LengthSquared() > r * r && origin.Dot(delta) >= 0)
            return true;
        return double.IsPositiveInfinity(SphereHitFraction(origin, target, Vector3.Zero, r));
    }
    public static double SphereHitFraction(Vector3 from, Vector3 to, Vector3 center, double radius)
    {
        var start = from - center;
        var direction = to - from;
        double c = start.LengthSquared() - radius * radius;
        if (c <= 0)
            return 0;
        double a = direction.LengthSquared();
        if (a < .00000001)
            return double.PositiveInfinity;
        double b = 2 * start.Dot(direction), discriminant = b * b - 4 * a * c;
        if (discriminant < 0)
            return double.PositiveInfinity;
        double t = (-b - Math.Sqrt(discriminant)) / (2 * a);
        return t >= 0 && t <= 1 ? t : double.PositiveInfinity;
    }
    public static double InterceptTime(Vector3 relative, Vector3 velocity, double speed, double lifetime)
    {
        double a = velocity.LengthSquared() - speed * speed, b = 2 * relative.Dot(velocity), c = relative.LengthSquared(), solution = double.PositiveInfinity;
        if (Math.Abs(a) < .000001)
        {
            if (Math.Abs(b) > .000001 && -c / b > 0)
                solution = -c / b;
        }
        else
        {
            double disc = b * b - 4 * a * c;
            if (disc >= 0)
            {
                double root = Math.Sqrt(disc), one = (-b - root) / (2 * a), two = (-b + root) / (2 * a);
                if (one > 0)
                    solution = Math.Min(solution, one);
                if (two > 0)
                    solution = Math.Min(solution, two);
            }
        }
        if (!double.IsFinite(solution))
            solution = relative.Length() / Math.Max(speed, .001);
        return C.Clamp(solution, 0, lifetime);
    }
    public static Vector3 VolleyDirection(Vector3 center, Vector3 origin, int index, int count)
    {
        if (count <= 1)
            return center;
        var axis = center.Cross(origin.Normalized());
        if (axis.LengthSquared() < .00001)
            axis = center.Cross(Math.Abs(center.Y) < .9 ? Vector3.Up : Vector3.Right);
        double half = Math.Min(.10, .018 * (count - 1)), angle = (2d * index / (count - 1) - 1) * half;
        return center.Rotated(axis.Normalized(), (float)angle).Normalized();
    }
}
