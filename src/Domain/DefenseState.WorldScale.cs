using System;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public const int WorldScaleVersion = 3;
    public const double WorldScaleRadiusDelta = WorldScale.EarthRadiusDelta;
    public const int CombatBalanceRevision = 2;

    public DataMap SerializeCombatPreferences() => new()
    {
        ["world_scale_version"] = WorldScaleVersion,
        ["balance_revision"] = CombatBalanceRevision,
        ["settings"] = CombatSettings.DeepClone()
    };

    public DataMap? ReadCombatPreferences(DataMap stored)
    {
        if (stored.Count != 3 || stored.Keys.Any(key => key is not ("world_scale_version" or "balance_revision" or "settings"))
            || !DataMap.ValidNumber(stored.Value("world_scale_version"), WorldScaleVersion, WorldScaleVersion, true) || stored.Value("settings") is not DataMap values)
            return null;
        return ReadBalancedCombatSettings(values, stored.Value("balance_revision"));
    }

    private DataMap? ReadBalancedCombatSettings(DataMap source, object? revision)
    {
        if (!DataMap.ValidNumber(revision, CombatBalanceRevision, CombatBalanceRevision, true)
            || source.Count != DefaultCombatSettings.Count) return null;
        return ValidatedCombatSettings(source);
    }
}
