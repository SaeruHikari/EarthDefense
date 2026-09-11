using System;
using System.Collections;
using System.Collections.Generic;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

/// <summary>All simulation records remain managed. Only renderer buffers cross the engine boundary.</summary>
public interface ICombatSurface
{
    IReadOnlyList<DataMap> GetFactorySites();
    Vector3 SurfaceToSpace(Vector3 normal, double altitude);
    Vector3 SpaceToSurface(Vector3 position);
    IReadOnlyList<Vector3> GetOccupiedSurfaceNormals();
    IReadOnlyList<DataMap> GetShieldSites() => Array.Empty<DataMap>();
    long SpatialRevision => -1;
}

internal static class C
{
    public static readonly DataMap Empty = new();
    public static double N(DataMap? m, string k, double d = 0) => m?.N(k, d) ?? d;
    public static long L(DataMap? m, string k, long d = 0) => m?.L(k, d) ?? d;
    public static int I(DataMap? m, string k, int d = 0) => m?.I(k, d) ?? d;
    public static string S(DataMap? m, string k, string d = "") => m?.S(k, d) ?? d;
    public static bool B(DataMap? m, string k, bool d = false) => m?.B(k, d) ?? d;
    public static DataMap M(DataMap? m, string k) => m != null && m.TryGetValue(k, out var v) && v is DataMap map ? map : Empty;
    public static Vector3 V(DataMap? m, string k, Vector3 d = default) => m != null && m.TryGetValue(k, out var v) && v is Vector3 value ? value : d;
    public static Color Color(DataMap? m, string k, Color d = default) => m != null && m.TryGetValue(k, out var v) && v is Color value ? value : d;
    public static List<object?> A(DataMap? m, string k) => m != null && m.TryGetValue(k, out var v) && v is List<object?> a ? a : new();
    public static DataMap Clone(DataMap m) => m.DeepClone();
    public static DataMap Shallow(DataMap m)
    {
        var n = new DataMap();
        foreach (var pair in m)
            n[pair.Key] = pair.Value;
        return n;
    }
    public static Vector3 Vec(double x, double y, double z) => new((float)x, (float)y, (float)z);
    public static Vector3 Scale(Vector3 a, double n) => a * (float)n;
    public static double Clamp(double v, double lo, double hi) => Math.Clamp(v, lo, hi);
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static double Smooth(double a, double b, double v)
    {
        var x = Clamp((v - a) / (b - a), 0, 1);
        return x * x * (3 - 2 * x);
    }
    public static long Posmod(long x, long n) => (x % n + n) % n;
    public static bool Near(double a, double b) => Math.Abs(a - b) < 0.00001 * Math.Max(1, Math.Abs(a));
    public static bool Large(DataMap e) => S(e, "kind") is "cruiser" or "boss" or "small_boss" or "carrier" or "mothership";
    public static double Packet(DataMap context, string key, double fallback) => context.TryGetValue("ability_values", out var v) && v is DataMap map ? N(map, key, fallback) : N(context, key, fallback);
}

public static class CombatScale
{
    public const double EarthRadius = WorldScale.EarthRadius, LegacyEarthRadius = WorldScale.LegacyEarthRadius, PreviousEarthRadius = WorldScale.PreviousEarthRadius, EarthRadiusDelta = WorldScale.EarthRadiusDelta;
    public const double EarthCollisionRadius = EarthRadius + .07, DroneAltitude = .70, PlanetPixelRadius = 208;
    public const double SpawnMin = EarthRadius + 4, SpawnMax = EarthRadius + 5.2, RetreatExit = EarthRadius + 8, LaunchDuration = 1.3;
    public const double DefenseEntryRadius = EarthRadius + 3, LegacyCarrierStandoff = EarthRadius + 1.45;
    public const double WeaponRange = 4.5, KineticRange = 1.65, CloseAssault = EarthRadius + .35;
    public static double DefaultFrontierRadius(int stage) => EarthRadius + new double[] { 40, 72, 112 }[Math.Clamp(stage, 1, 3) - 1];
    public static readonly Color Cyan = new("8ae6eb"), Coral = new("ef947e"), Gold = new("d5bd85"), Violet = new("c2acf4");
}

