using System;
using System.Linq;

namespace Earthward.Domain;

public sealed partial class DefenseState
{
    /// <summary>
    /// The current science satellite is a run-owned asset.  It becomes active
    /// only after the launch sequence reaches the orbital insertion point.
    /// </summary>
    public bool ResearchSatelliteDeployed { get; private set; }
    public bool ResearchSatelliteLaunchInProgress { get; private set; }
    public bool HasSatelliteLauncher => Buildings.L("satellite_launcher") > 0;

    public bool CanLaunchResearchSatellite()
        => HasSatelliteLauncher && !ResearchSatelliteDeployed && !ResearchSatelliteLaunchInProgress && EarthHp > 0;

    public bool BeginResearchSatelliteLaunch()
    {
        if (!CanLaunchResearchSatellite())
            return false;
        ResearchSatelliteLaunchInProgress = true;
        Changed?.Invoke();
        return true;
    }

    public bool CompleteResearchSatelliteLaunch()
    {
        if (!ResearchSatelliteLaunchInProgress || ResearchSatelliteDeployed)
            return false;
        ResearchSatelliteLaunchInProgress = false;
        ResearchSatelliteDeployed = true;
        Changed?.Invoke();
        return true;
    }

    internal bool CancelResearchSatelliteLaunch()
    {
        if (!ResearchSatelliteLaunchInProgress)
            return false;
        ResearchSatelliteLaunchInProgress = false;
        Changed?.Invoke();
        return true;
    }

    public long BuildingMaxCount(string id)
    {
        var definition = BuildingDefinitions.FirstOrDefault(row => row.S("id") == id);
        long limit = definition?.L("max_count", MaxExactInteger) ?? 0;
        return limit <= 0 ? MaxExactInteger : Math.Min(MaxExactInteger, limit);
    }

    internal bool SetResearchSatelliteState(bool deployed, bool launching)
    {
        if (deployed && launching)
            return false;
        if ((deployed || launching) && !HasSatelliteLauncher)
            return false;
        ResearchSatelliteDeployed = deployed;
        ResearchSatelliteLaunchInProgress = launching;
        return true;
    }
}
