using System;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public const int WorldScaleVersion = 3;
    public const double WorldScaleRadiusDelta = WorldScale.EarthRadiusDelta;

    public DataMap? MigrateCombatSettings(DataMap values, int sourceWorldScaleVersion)
    {
        if (sourceWorldScaleVersion != WorldScaleVersion)
            return null;
        return ValidatedCombatSettings(values.DeepClone());
    }

    public DataMap SerializeCombatPreferences() => new()
    {
        ["world_scale_version"] = WorldScaleVersion,
        ["settings"] = CombatSettings.DeepClone()
    };

    public DataMap? ReadCombatPreferences(DataMap stored)
    {
        if (stored.Count != 2 || !DataMap.ValidNumber(stored.Value("world_scale_version"), WorldScaleVersion, WorldScaleVersion, true) || stored.Value("settings") is not DataMap values)
            return null;
        return MigrateCombatSettings(values, stored.I("world_scale_version"));
    }
}
