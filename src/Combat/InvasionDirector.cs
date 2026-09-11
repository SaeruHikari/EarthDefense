using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed class InvasionDirector
{
    public static double MothershipRadius => CombatScale.EarthRadius + CombatCatalog.Current.Values.MotherAltitude;
    public static long[] UnlockWaves => CombatCatalog.Current.UnlockWaves;
    public static Vector3[] LocalDirections => CombatCatalog.Current.LocalDirections;
    private readonly HashSet<string> _destroyed = new(StringComparer.Ordinal);
    public Vector3 Anchor { get; private set; } = Vector3.Back;
    public bool AnchorConfigured
    {
        get; private set;
    }
    private Basis _basis = Basis.Identity;
    private static string FrontId(int index) => $"front_{index + 1:00}";
    public bool ConfigureAnchor(Vector3 direction)
    {
        if (AnchorConfigured || !direction.IsFinite() || direction.LengthSquared() < .000001)
            return false;
        Anchor = direction.Normalized();
        _basis = BasisForDirection(Anchor);
        AnchorConfigured = true;
        return true;
    }
    public bool HasFront(string id) => Enumerable.Range(0, 8).Any(i => FrontId(i) == id);
    public bool IsFrontDestroyed(string id) => _destroyed.Contains(id);
    public bool DestroyFront(string id) => HasFront(id) && _destroyed.Add(id);
    public List<string> GetDestroyedFronts() => Enumerable.Range(0, 8).Select(FrontId).Where(_destroyed.Contains).ToList();
    public static bool ValidateDestroyedFronts(IEnumerable<object?>? values)
    {
        if (values == null)
            return false;
        var seen = new HashSet<string>();
        foreach (var value in values)
        {
            if (value is not string id || !Enumerable.Range(0, 8).Any(i => FrontId(i) == id) || !seen.Add(id))
                return false;
        }
        return seen.Count <= 8;
    }
    public bool RestoreDestroyedFronts(IEnumerable<object?> values)
    {
        var list = values.ToList();
        if (!ValidateDestroyedFronts(list))
            return false;
        _destroyed.Clear();
        foreach (string id in list.Cast<string>())
            _destroyed.Add(id);
        return true;
    }
    public long NextAvailableWave(long wave)
    {
        for (int i = 0; i < 8; i++)
            if (!_destroyed.Contains(FrontId(i)))
                return Math.Max(Math.Max(1, wave), UnlockWaves[i]);
        return -1;
    }
    public static Basis BasisForDirection(Vector3 direction)
    {
        var outward = direction.Normalized();
        var reference = Math.Abs(outward.Dot(Vector3.Up)) > .94 ? Vector3.Right : Vector3.Up;
        var right = reference.Cross(outward).Normalized();
        return new Basis(right, outward.Cross(right).Normalized(), outward);
    }
    private DataMap Front(int i, long wave)
    {
        var direction = (_basis * LocalDirections[i].Normalized()).Normalized();
        var id = FrontId(i);
        bool destroyed = _destroyed.Contains(id);
        return new()
        {
            ["id"] = id,
            ["name"] = CombatCatalog.Current.Fronts[i].Name,
            ["ordinal"] = i + 1,
            ["direction"] = direction,
            ["world_position"] = C.Scale(direction, MothershipRadius),
            ["unlock_wave"] = UnlockWaves[i],
            ["active"] = wave >= UnlockWaves[i] && !destroyed,
            ["preview"] = wave < UnlockWaves[i] && !destroyed,
            ["destroyed"] = destroyed,
            ["color"] = CombatCatalog.Current.Fronts[i].Color
        };
    }
    public List<DataMap> FrontsForWave(long wave)
    {
        var result = new List<DataMap>(8);
        for (int i = 0; i < 8; i++)
            if (UnlockWaves[i] <= Math.Max(1, wave) && !_destroyed.Contains(FrontId(i)))
                result.Add(Front(i, wave));
        return result;
    }
    public DataMap NextFrontForWave(long wave)
    {
        for (int i = 0; i < 8; i++)
            if (UnlockWaves[i] > wave && !_destroyed.Contains(FrontId(i)))
                return Front(i, wave);
        return new();
    }
    public double SpawnConeRadians(long wave)
    {
        int unlocked = 0;
        foreach (long threshold in UnlockWaves)
            if (threshold <= Math.Max(wave, 1))
                unlocked++;
        return C.Lerp(CombatCatalog.Current.Values.SpawnConeMin, CombatCatalog.Current.Values.SpawnConeMax, Math.Max(0, unlocked - 1) / 7d);
    }
    public DataMap SpawnPoint(long wave, CombatRandom rng)
    {
        var fronts = FrontsForWave(wave);
        if (wave <= 0 || fronts.Count == 0)
            return new();
        var front = fronts[rng.Range(0, fronts.Count - 1)];
        var direction = C.V(front, "direction");
        var basis = BasisForDirection(direction);
        var mother = C.V(front, "world_position");
        double cone = SpawnConeRadians(wave);
        var position = C.Scale(direction, CombatScale.SpawnMin);
        for (int attempt = 0; attempt < 32; attempt++)
        {
            // Preserve the original world-width scatter as the Earth grows; angles must not multiply the battlefield footprint.
            double radius = rng.Range(CombatScale.SpawnMin, CombatScale.SpawnMax), angle = rng.Randf() * Math.Tau, lateral = Math.Sqrt(rng.Randf()) * (radius - CombatScale.EarthRadiusDelta) * Math.Sin(cone), axial = Math.Sqrt(Math.Max(radius * radius - lateral * lateral, 0));
            var candidate = basis * C.Vec(Math.Cos(angle) * lateral, Math.Sin(angle) * lateral, axial);
            if (candidate.DistanceTo(mother) < CombatCatalog.Current.Values.SpawnMotherClearance)
                continue;
            position = candidate;
            break;
        }
        return new()
        {
            ["position"] = position,
            ["front_id"] = front["id"],
            ["front_name"] = front["name"],
            ["mothership_position"] = mother,
            ["direction"] = direction,
            ["launch_direction"] = -position.Normalized(),
            ["unlock_wave"] = front["unlock_wave"],
            ["cone_radians"] = cone
        };
    }
    public double SurvivingFrontRatio(long wave)
    {
        int unlocked = 0, surviving = 0;
        for (int i = 0; i < 8; i++)
            if (UnlockWaves[i] <= wave)
            {
                unlocked++;
                if (!_destroyed.Contains(FrontId(i)))
                    surviving++;
            }
        return surviving / (double)Math.Max(1, unlocked);
    }
    public long WaveBudget(long wave, long baseCount = 10, long growth = 2, double multiplier = 1)
    {
        if (wave <= 0)
            return 0;
        return (long)Math.Ceiling(C.Clamp((baseCount + (double)growth * (wave - 1)) * multiplier * SurvivingFrontRatio(wave), 0, 9007199254740991));
    }
    public double SpawnInterval(long wave, double duration = 30, long baseCount = 10, long growth = 2, double multiplier = 2) => duration / Math.Max(1, WaveBudget(wave, baseCount, growth, multiplier));
    public DataMap FrontierSpawnPoint(int index, int count, double radius, CombatRandom rng)
    {
        int ordinal = Math.Max(0, index);
        Vector3 local;
        if (count <= 8)
            local = LocalDirections[ordinal % 8].Normalized();
        else
        {
            double y = 1 - 2 * (ordinal + .5) / Math.Max(count, 1), angle = ordinal * 2.399963229728653, r = Math.Sqrt(Math.Max(0, 1 - y * y));
            local = C.Vec(r * Math.Sin(angle), y, r * Math.Cos(angle));
        }
        var normal = (_basis * local).Normalized();
        return new()
        {
            ["position"] = C.Scale(normal, radius),
            ["direction"] = normal,
            ["front_id"] = $"frontier_{ordinal:000}"
        };
    }
    public Vector3 FrontierAircraftSpawn(Vector3 origin, CombatRandom rng)
    {
        var basis = BasisForDirection(origin.Normalized());
        double angle = rng.Randf() * Math.Tau, lateral = Math.Sqrt(rng.Randf()) * CombatCatalog.Current.Values.FrontierLateralRadius + CombatCatalog.Current.Values.FrontierLateralMinimum;
        return origin + basis * C.Vec(Math.Cos(angle) * lateral, Math.Sin(angle) * lateral, -rng.Range(CombatCatalog.Current.Values.FrontierForwardMin, CombatCatalog.Current.Values.FrontierForwardMax));
    }
}
