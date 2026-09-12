using Godot;

namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    private int _satelliteLauncherSite = -1;
    private void ConfigureSatelliteLauncherOrbit(int site)
    {
        _satelliteLauncherSite = site;
        _researchStation?.SetOrbitAnchor(GetSiteWorldNormal(site));
        _researchStation?.SetOrbitAvailable(true);
    }

    private void UpdateSatelliteLauncherOrbit()
    {
        if (_satelliteLauncherSite >= 0 && _satelliteLaunch == null && !_researchStation.IsDeployed)
            _researchStation.SetOrbitAnchor(GetSiteWorldNormal(_satelliteLauncherSite));
        if (_satelliteLauncherSite >= 0 && _facilities.TryGetValue(_satelliteLauncherSite, out var pad))
            pad.SetActivity(_satelliteLaunch == null ? 0 : Mathf.SmoothStep(0, .10f, _satelliteLaunch.Progress), 0);
    }
    public bool HasSatelliteLauncher(int site = -1)
    {
        if (site >= 0)
            return site < _slots.Count && _slots[site] == "satellite_launcher";
        return _slots.Contains("satellite_launcher");
    }

    public bool IsSatelliteLaunchActive => _satelliteLaunch != null;
    public float SatelliteLaunchProgress => _satelliteLaunch?.Progress ?? 0;
    public bool IsResearchSatelliteDeployed => _researchStation?.IsDeployed == true;
    public Vector3 ResearchSatelliteOrbitPoint(float phase = 0) => _researchStation != null ? _researchStation.GetOrbitPoint(phase) : OrbitalResearchStationVisual.OrbitPoint(phase);
    public Vector3 GetSiteWorldNormal(int site)
        => site >= 0 && site < _normals.Count ? (Globe.GlobalBasis * _normals[site]).Normalized() : Vector3.Zero;

    public bool StartResearchSatelliteLaunch(int site)
    {
        if (!HasSatelliteLauncher(site) || _satelliteLaunch != null || _researchStation.IsDeployed)
            return false;
        if (site < 0 || site >= _siteNodes.Count)
            return false;

        Transform3D pad = _siteNodes[site].GlobalTransform;
        Vector3 normal = pad.Basis.Y.Normalized();
        if (normal.LengthSquared() < .5f)
            return false;
        Vector3 origin = pad.Origin + normal * .12f;
        _researchStation.SetOrbitAnchor(normal);
        _satelliteLaunchTargetPhase = 0;
        _researchStation.SetOrbitPhase(_satelliteLaunchTargetPhase);
        _researchStation.SetDeployed(false);
        _satelliteLaunch = new SatelliteLaunchVehicleVisual(SpaceRoot, origin, _researchStation.Model);
        return true;
    }

    public void SyncResearchSatellite(bool deployed, bool launching)
    {
        if (_satelliteLaunch != null)
        {
            _satelliteLaunch.Root.QueueFree();
            _satelliteLaunch = null;
        }
        int launcher = _slots.IndexOf("satellite_launcher");
        _researchStation.SetOrbitAvailable(launcher >= 0);
        if (launcher >= 0)
            _researchStation.SetOrbitAnchor(GetSiteWorldNormal(launcher));
        _researchStation.SetDeployed(deployed && launcher >= 0);
        if (!launching || deployed)
            return;
        if (launcher >= 0)
            StartResearchSatelliteLaunch(launcher);
    }

    public void CancelResearchSatelliteLaunch()
    {
        if (_satelliteLaunch != null)
        {
            _satelliteLaunch.Root.QueueFree();
            _satelliteLaunch = null;
        }
        _researchStation.SetDeployed(false);
    }
}
