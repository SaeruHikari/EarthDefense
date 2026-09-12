using Godot;
using Earthward.Domain;
namespace Earthward.Rendering;

public sealed partial class PlanetView
{
    public void ResetPlanet()
    {
        CancelResearchSatelliteLaunch();
        _researchStation?.ResetOrbitAnchor();
        ClearSites();
        ClearCombatUnits();
        _restoring = true;
        Grid.SetSubdivision(WorldScale.GridLevel);
        Globe.Rotation = new(0, Mathf.DegToRad(12), 0);
        ResetCamera();
        // The third starter cell remains open for the one-time satellite
        // launcher and future surface infrastructure. Science comes online
        // only after the first satellite reaches orbit.
        string[] initial = ["mine", "solar", "", "interceptor"];
        for (int i = 0; i < SiteCoordinates.Length; i++)
            AppendSite(CoordinateNormal(SiteCoordinates[i]), i < 4 ? initial[i] : "");
        _restoring = false;
        RefreshGridOccupancy();
    }
    private void ClearSites()
    {
        _satelliteLauncherSite = -1;
        _researchStation?.SetOrbitAvailable(false);
        foreach (var site in _siteNodes)
        {
            Globe.RemoveChild(site);
            site.QueueFree();
        }
        _slots.Clear();
        _normals.Clear();
        _siteCells.Clear();
        _siteNodes.Clear();
        _facilities.Clear();
        _cellToSite.Clear();
        _footprintToSite.Clear();
        SelectedSlot = -1;
        _lastSelected = -2;
        Grid?.SetHover(-1);
        Grid?.SetSelected(-1);
        _shieldSitesDirty = true;
        _factorySitesDirty = true;
        _spatialRevision++;
        FactoryLayoutChanged?.Invoke();
    }
    public static Vector3 CoordinateNormal(Vector2 coordinate)
    {
        float lon = Mathf.DegToRad(coordinate.X), lat = Mathf.DegToRad(coordinate.Y);
        return new(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat));
    }
    private int AppendSite(Vector3 normal, string kind = "")
    {
        EnsureGridCapacity(_slots.Count + 1);
        int cell = Grid.FindNearestCell(normal, _cellToSite.Keys.ToHashSet());
        if (cell < 0)
            return -1;
        var direction = Grid.GetCellCenter(cell);
        int id = _slots.Count;
        _normals.Add(direction);
        _slots.Add("");
        _siteCells.Add(cell);
        _cellToSite[cell] = id;
        var site = new Node3D { Name = "Site_" + id, Position = direction * (_earth.SurfaceRadius(direction) + .003f), Quaternion = new Quaternion(Vector3.Up, direction) };
        Globe.AddChild(site);
        _siteNodes.Add(site);
        if (kind != "")
            SetSlot(id, kind);
        return id;
    }
    private void EnsureGridCapacity(int required)
    {
        if (required <= Grid.CellCount)
            return;
        int level = Grid.SubdivisionLevel;
        while (10 * Math.Pow(4, level) + 2 < required)
            level++;
        Grid.SetSubdivision(level);
        _cellToSite.Clear();
        for (int i = 0; i < _normals.Count; i++)
        {
            int cell = Grid.FindNearestCell(_normals[i], _cellToSite.Keys.ToHashSet());
            _cellToSite[cell] = i;
            _siteCells[i] = cell;
            _normals[i] = Grid.GetCellCenter(cell);
            _siteNodes[i].Position = _normals[i] * (_earth.SurfaceRadius(_normals[i]) + .003f);
            _siteNodes[i].Quaternion = new(Vector3.Up, _normals[i]);
        }
        RefreshGridOccupancy();
        _shieldSitesDirty = true;
        _factorySitesDirty = true;
        _spatialRevision++;
        FactoryLayoutChanged?.Invoke();
    }
    public bool SetSlot(int index, string kind)
    {
        if (index < 0 || index >= _slots.Count)
            return false;
        if (!_restoring && PlacementReason(index, kind, true) != "")
            return false;
        _slots[index] = kind;
        if (kind == "satellite_launcher" && _researchStation != null)
            ConfigureSatelliteLauncherOrbit(index);
        _facilities.Remove(index);
        var site = _siteNodes[index];
        foreach (var child in site.GetChildren())
        {
            site.RemoveChild(child);
            child.QueueFree();
        }
        if (kind is "interceptor" or "missile" or "laser" or "mine" or "solar" or "shield" or "satellite_launcher")
            _facilities[index] = new(site, kind);
        if (kind == "satellite_launcher")
            SatelliteLauncherModel.AddHoneycombFoundation(_facilities[index].Root, Grid, _siteCells[index], site.Transform.AffineInverse(), _earth.SurfaceRadius);
        if (!_restoring)
        {
            if (!_slots.Contains("") && _slots.Count >= Grid.CellCount)
                EnsureGridCapacity(_slots.Count + 1);
            RefreshGridOccupancy();
        }
        _shieldSitesDirty = true;
        _factorySitesDirty = true;
        _spatialRevision++;
        FactoryLayoutChanged?.Invoke();
        return true;
    }
    private void RefreshGridOccupancy()
    {
        _footprintToSite.Clear();
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != "")
                foreach (int cell in GetStructureFootprint(i))
                    _footprintToSite[cell] = i;
        Grid.SetOccupied(_footprintToSite.Keys);
        UpdateSelectedFootprint();
    }
    public List<object?> GetSlots() => _slots.Cast<object?>().ToList();
    public List<object?> GetSiteDirections() => _normals.Select(n => (object?)new List<object?> { n.X, n.Y, n.Z }).ToList();
    public Vector3 GetSiteNormal(int index) => index >= 0 && index < _normals.Count ? _normals[index] : Vector3.Zero;
    public int GetSiteCell(int index) => index >= 0 && index < _siteCells.Count ? _siteCells[index] : -1;
    public int GetGridCellCount() => Grid.CellCount;
    public int GetSiteAtCell(int cell) => _footprintToSite.GetValueOrDefault(cell, _cellToSite.GetValueOrDefault(cell, -1));
    public int PickBuildCell(Vector2 point)
    {
        var normal = ScreenToSurface(point);
        return normal == Vector3.Zero ? -1 : Grid.FindNearestCell(normal);
    }
    public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals()
    {
        var list = new List<Vector3>();
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != "")
                list.Add(_normals[i]);
        return list;
    }
    public void RestoreSlots(IReadOnlyList<object?> data) => RestoreSites(data);
    public void RestoreSites(IReadOnlyList<object?> slots, IReadOnlyList<object?>? directions = null)
    {
        ClearSites();
        _restoring = true;
        int level = WorldScale.GridLevel;
        while (10 * Math.Pow(4, level) + 2 < slots.Count)
            level++;
        Grid.SetSubdivision(level);
        for (int i = 0; i < slots.Count; i++)
        {
            Vector3 n = Vector3.Zero;
            if (directions != null && i < directions.Count)
            {
                if (directions[i] is Vector3 v)
                    n = v;
                else if (directions[i] is List<object?> row && row.Count >= 3)
                    n = new((float)DataMap.Number(row[0]), (float)DataMap.Number(row[1]), (float)DataMap.Number(row[2]));
            }
            if (!n.IsFinite() || n.LengthSquared() < .01f)
            {
                if (i < SiteCoordinates.Length)
                    n = CoordinateNormal(SiteCoordinates[i]);
                else
                {
                    float latitude = Mathf.Asin(1 - 2 * (i + .5f) / Math.Max(slots.Count, 1));
                    float longitude = i * 2.399963f;
                    n = new(Mathf.Cos(latitude) * Mathf.Sin(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Cos(longitude));
                }
            }
            AppendSite(n.Normalized(), slots[i] as string ?? "");
        }
        _restoring = false;
        if (_slots.Count > 0 && !_slots.Contains("") && _slots.Count >= Grid.CellCount)
            EnsureGridCapacity(_slots.Count + 1);
        RefreshGridOccupancy();
    }
    public IReadOnlyList<DataMap> GetShieldSites()
    {
        if (!_shieldSitesDirty) return _shieldSitesCache;
        _shieldSitesDirty = false;
        _shieldSitesCache.Clear();
        for (int site = 0; site < _slots.Count; site++)
            if (_slots[site] == "shield")
                _shieldSitesCache.Add(new() { ["site_id"] = site, ["kind"] = "shield", ["normal"] = _normals[site] });
        return _shieldSitesCache;
    }
    public IReadOnlyList<DataMap> GetFactorySites()
    {
        CacheSurfaceTransform();
        if (_factorySitesDirty)
        {
            _factorySitesDirty = false;
            _factorySiteCache.Clear();
            _factoryLaunchLocals.Clear();
            foreach (var (site, visual) in _facilities)
                if (visual.IsFactory)
                {
                    _factorySiteCache.Add(new() { ["site_id"] = site, ["kind"] = visual.Kind, ["normal"] = _normals[site] });
                    _factoryLaunchLocals.Add((_cachedSurfaceInverse * visual.LaunchPosition, _cachedSurfaceInverse.Basis * visual.LaunchDirection));
                }
            _factoryLaunchRevision = -1;
        }
        if (_factoryLaunchRevision != SpatialRevision)
        {
            _factoryLaunchRevision = SpatialRevision;
            for (int i = 0; i < _factorySiteCache.Count; i++)
            {
                _factorySiteCache[i]["launch_position"] = _cachedSurfaceTransform * _factoryLaunchLocals[i].Position;
                _factorySiteCache[i]["launch_direction"] = (_cachedSurfaceTransform.Basis * _factoryLaunchLocals[i].Direction).Normalized();
            }
        }
        return _factorySiteCache;
    }
    public void SyncFactoryActivity(IReadOnlyList<DataMap> activity)
    {
        var seen = new HashSet<int>();
        foreach (var data in activity)
        {
            int id = data.I("site_id", -1);
            if (_facilities.TryGetValue(id, out var visual))
            {
                visual.SetActivity((float)data.N("phase"), (float)data.N("progress"));
                seen.Add(id);
            }
        }
        foreach (var (id, visual) in _facilities)
            if (visual.IsFactory && !seen.Contains(id))
                visual.SetActivity(0, 0);
    }
    public int[] GetStructureFootprint(int site, string kind = "")
    {
        if (site < 0 || site >= _siteCells.Count)
            return [];
        if (kind == "")
            kind = _slots[site];
        return kind == "satellite_launcher" ? Grid.GetClusterCells(_siteCells[site]) : [_siteCells[site]];
    }
    private string PlacementReason(int site, string kind, bool replace = false)
    {
        if (site < 0 || site >= _slots.Count)
            return "请先选择地表六边形";
        if (!replace && _slots[site] != "")
            return "该地块已有设施";
        var cells = GetStructureFootprint(site, kind);
        if (cells.Length == 0)
            return "此处无法容纳完整蜂窝占地";
        foreach (int cell in cells)
            if (_footprintToSite.TryGetValue(cell, out int other) && other != site)
                return "建筑占地与已有设施重叠";
        return "";
    }
    public bool CanPlaceStructure(int site, string kind) => PlacementReason(site, kind) == "";
    public string GetStructurePlacementReason(int site, string kind) => PlacementReason(site, kind);
    public int[] GetOccupiedCells() => _footprintToSite.Keys.ToArray();
    private void UpdateSelectedFootprint() => Grid?.SetSelectedCells(GetStructureFootprint(SelectedSlot));
    public void SetBuildHover(Vector2 point)
    {
        _hoverLocal = point;
        int cell = PickBuildCell(point);
        if (!PlacingBuilding || cell < 0)
        {
            Grid.SetHoverCells([]);
            return;
        }
        var cells = PlacingKind == "satellite_launcher" ? Grid.GetClusterCells(cell) : [cell];
        bool valid = cells.Length > 0 && cells.All(c => !_footprintToSite.ContainsKey(c));
        Grid.SetHoverCells(cells.Length > 0 ? cells : [cell], valid);
    }
    public int PickExistingSite(Vector2 point)
    {
        int cell = PickBuildCell(point);
        int site = GetSiteAtCell(cell);
        if (site >= 0 && site < _slots.Count && _slots[site] != "")
            return site;
        // A projected facility can sit on a hex boundary after the globe has
        // rotated or the camera has been rescaled.  Keep the hit target stable
        // by accepting the nearest occupied site's projected center as a small
        // screen-space fallback.  This also makes the whole building card
        // clickable instead of requiring a mathematically exact cell ray.
        if (ScreenToSurface(point) == Vector3.Zero)
            return -1;
        const float hitRadius = 16f;
        float nearest = hitRadius * hitRadius;
        int candidate = -1;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == "" || !IsSlotVisible(i))
                continue;
            float distance = GetSlotScreenPosition(i).DistanceSquaredTo(point);
            if (distance < nearest)
            {
                nearest = distance;
                candidate = i;
            }
        }
        return candidate;
    }
    public int SetResourceUpgradeHover(Vector2 point)
    {
        int id = PickExistingSite(point);
        if (id < 0 || _slots[id] is not ("mine" or "solar"))
        {
            ClearResourceUpgradeHover();
            return -1;
        }
        _resourceHover = id;
        Grid.SetSelected(_siteCells[id]);
        return id;
    }
    public void ClearResourceUpgradeHover()
    {
        if (_resourceHover < 0)
            return;
        _resourceHover = -1;
        UpdateSelectedFootprint();
    }
    public bool HandleClick(Vector2 point)
    {
        int cell = PickBuildCell(point);
        if (cell < 0)
        {
            SelectedSlot = -1;
            Grid.SetSelected(-1);
            return false;
        }
        int site = GetSiteAtCell(cell);
        if (site < 0 && !PlacingBuilding)
            site = PickExistingSite(point);
        if (site < 0 && PlacingBuilding)
            site = AppendSite(Grid.GetCellCenter(cell));
        SelectedSlot = site;
        UpdateSelectedFootprint();
        if (site < 0)
            return false;
        SlotSelected?.Invoke(site);
        return true;
    }
    public Vector2 GetSlotScreenPosition(int index) => index >= 0 && index < _siteNodes.Count ? GetSpaceScreenPosition(_siteNodes[index].GlobalPosition) : new(-1000, -1000);
    public bool IsSlotVisible(int index) => index >= 0 && index < _normals.Count && (Globe.GlobalBasis * _normals[index]).Dot((Camera.GlobalPosition - _siteNodes[index].GlobalPosition).Normalized()) > .12f;
    public Transform3D GetSurfaceSiteTransform(int index) => index >= 0 && index < _siteNodes.Count ? _siteNodes[index].GlobalTransform : Transform3D.Identity;
    public static bool ValidateStructureLayout(IReadOnlyList<object?> slots, IReadOnlyList<object?> directions) => SphericalGrid.ValidateStructureLayout(slots, directions);
}
