using System;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    // Historical schema constants: only geocentric distances change for the radius 4 -> 8 -> 16 expansions.
    public const int WorldScaleVersion = 3;
    public const double WorldScaleRadiusDelta = WorldScale.EarthRadiusDelta;
    private static readonly string[] FrontierRadiusKeys = { "frontier_radius_1", "frontier_radius_2", "frontier_radius_3" };

    public static double RadiusForWorldScaleVersion(int version) => version switch
    {
        1 => WorldScale.LegacyEarthRadius,
        2 => WorldScale.LegacyEarthRadius * 2d,
        3 => WorldScale.EarthRadius,
        _ => throw new ArgumentOutOfRangeException(nameof(version))
    };

    public DataMap? MigrateCombatSettings(DataMap values, int sourceWorldScaleVersion)
    {
        if (sourceWorldScaleVersion is < 1 or > WorldScaleVersion)
            return null;
        double radiusDelta = WorldScale.EarthRadius - RadiusForWorldScaleVersion(sourceWorldScaleVersion);
        var current = values.DeepClone();
        if (sourceWorldScaleVersion < WorldScaleVersion)
            foreach (string key in FrontierRadiusKeys)
            {
                // An absent historical option inherits today's default, without adding the offset twice.
                if (!current.ContainsKey(key))
                    continue;
                var bounds = CombatSettingBounds(key);
                if (!DataMap.ValidNumber(current.Value(key), bounds.X - radiusDelta, bounds.Y - radiusDelta))
                    return null;
                current[key] = current.N(key) + radiusDelta;
            }
        return ValidatedCombatSettings(current);
    }

    public DataMap SerializeCombatPreferences() => new()
    {
        ["world_scale_version"] = WorldScaleVersion,
        ["settings"] = CombatSettings.DeepClone()
    };

    public DataMap? ReadCombatPreferences(DataMap stored)
    {
        if (!stored.ContainsKey("world_scale_version") && !stored.ContainsKey("settings"))
            return MigrateCombatSettings(stored, 1);
        if (stored.Count != 2 || !DataMap.ValidNumber(stored.Value("world_scale_version"), 1, WorldScaleVersion, true) || stored.Value("settings") is not DataMap values)
            return null;
        return MigrateCombatSettings(values, stored.I("world_scale_version"));
    }
}
