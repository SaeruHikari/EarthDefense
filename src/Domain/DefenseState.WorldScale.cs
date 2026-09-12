using System;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public const int WorldScaleVersion = 3;
    public const double WorldScaleRadiusDelta = WorldScale.EarthRadiusDelta;
    public const int CombatBalanceRevision = 2;
    private static readonly string[] PacingSettings =
    ["enemy_wave_base_count", "enemy_wave_growth", "enemy_wave_duration", "enemy_spawn_duration", "enemy_bullet_growth_interval"];

    public DataMap SerializeCombatPreferences() => new()
    {
        ["world_scale_version"] = WorldScaleVersion,
        ["balance_revision"] = CombatBalanceRevision,
        ["settings"] = CombatSettings.DeepClone()
    };

    public DataMap? ReadCombatPreferences(DataMap stored)
    {
        if (stored.Count is not (2 or 3) || stored.Keys.Any(key => key is not ("world_scale_version" or "balance_revision" or "settings"))
            || !DataMap.ValidNumber(stored.Value("world_scale_version"), WorldScaleVersion, WorldScaleVersion, true) || stored.Value("settings") is not DataMap values)
            return null;
        return ReadBalancedCombatSettings(values, stored.Value("balance_revision", 0));
    }

    private DataMap? ReadBalancedCombatSettings(DataMap source, object? revision)
    {
        if (!DataMap.ValidNumber(revision, 0, CombatBalanceRevision, true)) return null;
        var values = source.DeepClone();
        long savedRevision = DataMap.Integer(revision);
        var defaults = DefaultCombatSettings;
        if (savedRevision < 1)
        {
            // Apply the original pacing update only to saves predating it.
            foreach (string key in PacingSettings) values[key] = defaults[key];
        }
        if (savedRevision < 2)
        {
            // Adopt the gentler campaign growth only when the player retained
            // the former defaults. Custom growth and revision-one pacing stay intact.
            foreach (var (key, previousDefault) in new[] { ("enemy_health_growth", .035), ("enemy_damage_growth", .018) })
                if (values.N(key, double.NaN) == previousDefault) values[key] = defaults[key];
        }
        return ValidatedCombatSettings(values);
    }
}
