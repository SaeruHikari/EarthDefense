using Earthward;
using Earthward.Combat;
using Earthward.Domain;
using Godot;

/// <summary>
/// Managed campaign surface using the production level-six grid and starter
/// coordinates. The grid algorithm and ordering mirror SphericalGrid.GetTopology;
/// no Node, scene tree, renderer, or native Godot object is constructed.
/// Factory foundations use the spherical Earth radius by default. A caller may
/// supply the production terrain radius sampler when one is available.
/// </summary>
internal sealed class CampaignTestSurface : ICombatSurface
{
    public const double RotationRadiansPerSecond = .007 * WorldScale.LegacyEarthRadius / WorldScale.EarthRadius;
    public readonly List<DataMap> AllSites = new(), Factories = new(), Shields = new();
    public long SpatialRevision { get; private set; }
    public double RotationY { get; private set; } = Math.PI / 15;
    public Vector3 InitialFactoryNormal { get; }
    public int GridCellCount => Grid.Value.Centers.Length;
    public int OccupiedCellCount => _occupied.Count;
    public bool UsesTerrainSampler { get; }

    private sealed record Topology(Vector3[] Centers, int[][] Neighbors);
    private static readonly Lazy<Topology> Grid = new(BuildTopology);
    private static readonly Vector2[] StarterCoordinates =
    [
        new(12, 2), new(-51, -10), new(25, 47), new(-73, 48),
        new(-16, 48), new(-5, -37), new(56, -18), new(-78, -20),
        new(87, 35), new(118, -15), new(152, 48), new(170, -35),
        new(-148, 21), new(-115, -38), new(80, -56), new(-110, 60)
    ];
    private static readonly HashSet<string> StructureKinds =
        new(StringComparer.Ordinal) { "mine", "solar", "interceptor", "missile", "laser", "shield", "satellite_launcher" };
    private readonly Dictionary<int, int> _cellToSlot = new();
    private readonly List<int> _slotCells = new();
    private readonly Dictionary<int, DataMap> _built = new();
    private readonly HashSet<int> _occupied = new();
    private readonly List<Vector3> _occupiedNormals = new();
    private readonly Func<Vector3, float> _surfaceRadius;
    private Basis _rotation = new(Vector3.Up, Mathf.DegToRad(12));
    private long _launchRevision = -1;

    public CampaignTestSurface(Func<Vector3, float>? surfaceRadius = null)
    {
        UsesTerrainSampler = surfaceRadius != null;
        _surfaceRadius = surfaceRadius ?? (_ => WorldScale.EarthRadius);
        // Production creates all sixteen named slots, including empty ones.
        // Empty slots keep their site IDs but occupy no construction footprint.
        foreach (var coordinate in StarterCoordinates)
        {
            int cell = NearestCell(CoordinateNormal(coordinate), c => !_cellToSlot.ContainsKey(c));
            _cellToSlot.Add(cell, _slotCells.Count);
            _slotCells.Add(cell);
        }
        InitialFactoryNormal = Grid.Value.Centers[_slotCells[3]];
        CommitBuild("mine", Candidate(0, _slotCells[0]));
        CommitBuild("solar", Candidate(1, _slotCells[1]));
        CommitBuild("interceptor", Candidate(3, _slotCells[3]));
    }

    /// <summary>
    /// Finds the nearest legal grid cell without spending resources or reserving
    /// it. Call Game.Build(kind, site.L("site_id")) and then CommitBuild together.
    /// The desired normal is Earth-local: convert world impacts with SpaceToSurface.
    /// </summary>
    public DataMap? FindBuildSite(string kind, Vector3 localDesiredNormal)
    {
        if (!StructureKinds.Contains(kind) || !localDesiredNormal.IsFinite()
            || localDesiredNormal.LengthSquared() < .0001f) return null;
        int cell = NearestCell(localDesiredNormal, c => FootprintAvailable(kind, c));
        if (cell < 0) return null;
        return Candidate(_cellToSlot.GetValueOrDefault(cell, _slotCells.Count), cell);
    }

    public bool CanPlace(string kind, DataMap site)
    {
        if (!StructureKinds.Contains(kind)) return false;
        int id = site.I("site_id", -1), cell = site.I("cell_id", -1);
        if (id < 0 || id > _slotCells.Count || cell < 0 || cell >= GridCellCount || _built.ContainsKey(id)) return false;
        if (id < _slotCells.Count && _slotCells[id] != cell) return false;
        if (id == _slotCells.Count && _cellToSlot.ContainsKey(cell)) return false;
        return site.Vector3("normal").DistanceSquaredTo(Grid.Value.Centers[cell]) < 1e-10f
            && FootprintAvailable(kind, cell);
    }

    public void CommitBuild(string kind, DataMap site)
    {
        if (!CanPlace(kind, site)) throw new InvalidOperationException("Campaign surface placement is stale or illegal.");
        int id = site.I("site_id"), cell = site.I("cell_id");
        if (id == _slotCells.Count)
        {
            _cellToSlot.Add(cell, id);
            _slotCells.Add(cell);
        }
        var row = Candidate(id, cell);
        row["kind"] = kind;
        row["footprint_cells"] = Footprint(kind, cell).Select(c => (object?)(long)c).ToList();
        _built.Add(id, row);
        AllSites.Add(row);
        _occupiedNormals.Add(row.Vector3("normal"));
        foreach (int member in Footprint(kind, cell)) _occupied.Add(member);
        if (kind is "interceptor" or "missile" or "laser") Factories.Add(row);
        if (kind == "shield") Shields.Add(row);
        SpatialRevision++;
        RefreshLaunches();
    }

    /// <summary>
    /// Matches PlanetView's speed-one automatic spin. Do not call with elapsed
    /// gameplay time during a paused guide; buildingMode also freezes the spin.
    /// </summary>
    public void Step(double delta, bool buildingMode = false)
    {
        if (buildingMode || !double.IsFinite(delta) || delta <= 0) return;
        // Preserve production's float operation order and small per-frame rotation.
        float amount = (float)delta * .007f * WorldScale.LegacyEarthRadius / WorldScale.EarthRadius;
        _rotation = _rotation.Rotated(Vector3.Up, amount);
        RotationY += amount;
        SpatialRevision++;
        RefreshLaunches();
    }

    public IReadOnlyList<DataMap> GetFactorySites() { RefreshLaunches(); return Factories; }
    public IReadOnlyList<DataMap> GetShieldSites() => Shields;
    public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals() => _occupiedNormals;
    public Vector3 SurfaceToSpace(Vector3 normal, double altitude)
        => normal.IsFinite() && normal.LengthSquared() >= .0001f
            ? _rotation * (normal.Normalized() * (WorldScale.EarthRadius + (float)altitude)) : Vector3.Zero;
    public Vector3 SpaceToSurface(Vector3 position)
        => position.IsFinite() ? (_rotation.Inverse() * position).Normalized() : Vector3.Zero;
    public bool TryGetCoordinateFrame(out Transform3D frame)
    {
        frame = new(_rotation, Vector3.Zero);
        return true;
    }
    public Vector3 GetSiteNormal(int site) => site >= 0 && site < _slotCells.Count ? Grid.Value.Centers[_slotCells[site]] : Vector3.Zero;
    public IReadOnlyList<int> GetOccupiedCells() => _occupied.Order().ToArray();

    private void RefreshLaunches()
    {
        if (_launchRevision == SpatialRevision) return;
        _launchRevision = SpatialRevision;
        foreach (var site in Factories)
        {
            Vector3 normal = site.Vector3("normal");
            // PlanetView.AppendSite aligns local Up to the cell normal; the
            // FacilityVisual launch socket is at (0,.106,.045), looking local +Z.
            Basis orientation = new(new Quaternion(Vector3.Up, normal));
            Vector3 localLaunch = normal * (_surfaceRadius(normal) + .003f)
                + orientation * new Vector3(0, .106f, .045f);
            site["launch_position"] = _rotation * localLaunch;
            site["launch_direction"] = (_rotation * orientation.Z).Normalized();
        }
    }

    private static DataMap Candidate(int site, int cell) => new()
    {
        ["site_id"] = (long)site, ["cell_id"] = (long)cell, ["normal"] = Grid.Value.Centers[cell]
    };
    private bool FootprintAvailable(string kind, int cell)
    {
        if (_occupied.Contains(cell)) return false;
        if (kind != "satellite_launcher") return true;
        int[] neighbors = Grid.Value.Neighbors[cell];
        return neighbors.Length == 6 && neighbors.All(c => Grid.Value.Neighbors[c].Length == 6 && !_occupied.Contains(c));
    }
    private static int[] Footprint(string kind, int cell)
        => kind == "satellite_launcher" ? new[] { cell }.Concat(Grid.Value.Neighbors[cell]).ToArray() : [cell];
    private static int NearestCell(Vector3 normal, Func<int, bool> allowed)
    {
        Vector3 n = normal.Normalized();
        int best = -1;
        float score = -2;
        for (int cell = 0; cell < Grid.Value.Centers.Length; cell++)
        {
            float candidate = Grid.Value.Centers[cell].Dot(n);
            if (candidate > score && allowed(cell)) { score = candidate; best = cell; }
        }
        return best;
    }
    private static Vector3 CoordinateNormal(Vector2 coordinate)
    {
        float lon = Mathf.DegToRad(coordinate.X), lat = Mathf.DegToRad(coordinate.Y);
        return new(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat));
    }

    private static Topology BuildTopology()
    {
        // Exact production order, rather than a Fibonacci approximation. At level
        // six these are the 40,962 cell centers and their five/six neighbors.
        float g = (1 + Mathf.Sqrt(5)) * .5f;
        var points = new List<Vector3> { new(-1, g, 0), new(1, g, 0), new(-1, -g, 0), new(1, -g, 0), new(0, -1, g), new(0, 1, g), new(0, -1, -g), new(0, 1, -g), new(g, 0, -1), new(g, 0, 1), new(-g, 0, -1), new(-g, 0, 1) };
        for (int i = 0; i < points.Count; i++) points[i] = points[i].Normalized();
        var faces = new List<int[]> { new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 }, new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 }, new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 }, new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 } };
        for (int level = 0; level < WorldScale.GridLevel; level++)
        {
            var midpoints = new Dictionary<(int, int), int>();
            var refined = new List<int[]>(faces.Count * 4);
            foreach (var face in faces)
            {
                var edges = new int[3];
                for (int edge = 0; edge < 3; edge++)
                {
                    int a = face[edge], b = face[(edge + 1) % 3];
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    if (!midpoints.TryGetValue(key, out int index))
                    {
                        index = points.Count;
                        midpoints.Add(key, index);
                        points.Add((points[a] + points[b]).Normalized());
                    }
                    edges[edge] = index;
                }
                int ab = edges[0], bc = edges[1], ca = edges[2];
                refined.AddRange([new[] { face[0], ab, ca }, new[] { face[1], bc, ab }, new[] { face[2], ca, bc }, new[] { ab, bc, ca }]);
            }
            faces = refined;
        }
        var neighbors = Enumerable.Range(0, points.Count).Select(_ => new List<int>()).ToArray();
        foreach (var face in faces)
            for (int edge = 0; edge < 3; edge++)
            {
                int a = face[edge], b = face[(edge + 1) % 3];
                if (!neighbors[a].Contains(b)) neighbors[a].Add(b);
                if (!neighbors[b].Contains(a)) neighbors[b].Add(a);
            }
        return new(points.ToArray(), neighbors.Select(n => n.ToArray()).ToArray());
    }
}
