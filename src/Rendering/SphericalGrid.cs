using Godot;
namespace Earthward.Rendering;

public sealed partial class SphericalGrid : Node3D
{
    private sealed record Topology(Vector3[] Centers, Vector3[] Corners, int[][] Polygons, int[][] Neighbors);
    private static readonly Dictionary<int, Topology> Cache = new();
    private Topology _data = null!;
    public int SubdivisionLevel { get; private set; } = WorldScale.GridLevel;
    public IReadOnlyList<Vector3> Centers => _data.Centers;
    public int CellCount => _data.Centers.Length;
    private EarthVisual _terrain = null!;
    private MeshInstance3D _base = null!, _occupied = null!, _hover = null!, _selected = null!;
    private ShaderMaterial _baseMat = null!, _occupiedMat = null!, _hoverMat = null!, _selectedMat = null!;
    private bool _buildMode, _hoverValid;
    private int[] _hoverCells = [], _selectedCells = [];
    public void Setup(EarthVisual terrain, int level = WorldScale.GridLevel)
    {
        _terrain = terrain;
        (_base, _baseMat) = Create(new(.60f, .84f, .88f, .075f));
        (_occupied, _occupiedMat) = Create(new(.42f, .88f, .72f, .10f));
        (_hover, _hoverMat) = Create(new(.55f, 1, .81f, .27f));
        (_selected, _selectedMat) = Create(new(.61f, 1, .85f, .19f));
        SetSubdivision(level);
    }
    private (MeshInstance3D, ShaderMaterial) Create(Color color)
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/spherical_grid.gdshader"), RenderPriority = -20 };
        mat.SetShaderParameter("tint", color);
        var node = new MeshInstance3D { MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        return (node, mat);
    }
    public void SetSubdivision(int level)
    {
        SubdivisionLevel = Math.Max(WorldScale.GridLevel, level);
        _data = GetTopology(SubdivisionLevel);
        _base.Mesh = AllEdges();
        _occupied.Mesh = null;
        _hover.Mesh = null;
        _selected.Mesh = null;
        _hoverCells = [];
        _selectedCells = [];
    }
    public Vector3 GetCellCenter(int cell) => cell >= 0 && cell < CellCount ? _data.Centers[cell] : Vector3.Zero;
    public int GetCellCount() => CellCount;
    public int FindNearestCell(Vector3 normal, ISet<int>? excluded = null)
    {
        if (!normal.IsFinite() || normal.LengthSquared() < .0001f)
            return -1;
        var n = normal.Normalized();
        int best = -1;
        float score = -2;
        for (int i = 0; i < CellCount; i++)
        {
            if (excluded?.Contains(i) == true)
                continue;
            float s = _data.Centers[i].Dot(n);
            if (s > score)
            {
                score = s;
                best = i;
            }
        }
        return best;
    }
    public void SetBuildMode(bool value)
    {
        if (value == _buildMode)
            return;
        _buildMode = value;
        _baseMat.SetShaderParameter("tint", new Color(.62f, .91f, .91f, value ? .26f : .075f));
        _occupiedMat.SetShaderParameter("tint", new Color(.44f, .90f, .73f, value ? .19f : .10f));
        _hover.Visible = value;
    }
    public void SetOccupied(IEnumerable<int> cells) => _occupied.Mesh = CellsMesh(cells, false, .005f);
    public void SetHover(int cell) => SetHoverCells(cell >= 0 ? [cell] : [], true);
    public void SetSelected(int cell) => SetSelectedCells(cell >= 0 ? [cell] : []);
    public void SetHoverCells(IEnumerable<int> cells, bool valid = true)
    {
        var next = cells.ToArray();
        if (valid == _hoverValid && next.SequenceEqual(_hoverCells))
            return;
        _hoverCells = next;
        _hoverValid = valid;
        _hover.Mesh = CellsMesh(next, true, .007f);
        _hoverMat.SetShaderParameter("tint", valid ? new Color(.55f, 1, .81f, .27f) : new Color(1, .42f, .26f, .28f));
    }
    public void SetSelectedCells(IEnumerable<int> cells)
    {
        var next = cells.ToArray();
        if (next.SequenceEqual(_selectedCells))
            return;
        _selectedCells = next;
        _selected.Mesh = CellsMesh(next, true, .009f);
    }
    public IReadOnlyList<int> GetCellNeighbors(int cell) => cell >= 0 && cell < CellCount ? _data.Neighbors[cell] : Array.Empty<int>();
    public int[] GetClusterCells(int cell)
    {
        if (cell < 0 || cell >= CellCount)
            return [];
        var result = new List<int> { cell };
        var visited = new HashSet<int> { cell };
        var frontier = new List<int> { cell };
        for (int ring = 0; ring < 2; ring++)
        {
            var next = new List<int>();
            foreach (int current in frontier)
            {
                if (_data.Polygons[current].Length != 6)
                    return [];
                foreach (int adjacent in _data.Neighbors[current])
                    if (visited.Add(adjacent))
                    {
                        result.Add(adjacent);
                        next.Add(adjacent);
                    }
            }
            frontier = next;
        }
        return result.Count == 19 && result.All(c => _data.Polygons[c].Length == 6) ? result.ToArray() : [];
    }
    private Vector3 Point(Vector3 n, float lift) => n * (_terrain.SurfaceRadius(n) + lift);
    // Smaller angular cells need fewer arc samples; retain the original drawing
    // budget instead of quadrupling world-wide grid vertices with every expansion.
    private int EdgeSegments => Math.Max(1, 4 >> (SubdivisionLevel - WorldScale.BakedGridLevel));
    private ArrayMesh? AllEdges()
    {
        var vertices = new List<Vector3>();
        var visited = new HashSet<long>();
        int total = _data.Corners.Length;
        foreach (var polygon in _data.Polygons)
            for (int i = 0; i < polygon.Length; i++)
            {
                int a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                if (!visited.Add((long)Math.Min(a, b) * total + Math.Max(a, b)))
                    continue;
                for (int j = 0; j < EdgeSegments; j++)
                {
                    vertices.Add(Point(_data.Corners[a].Slerp(_data.Corners[b], j / (float)EdgeSegments), .004f));
                    vertices.Add(Point(_data.Corners[a].Slerp(_data.Corners[b], (j + 1) / (float)EdgeSegments), .004f));
                }
            }
        return Mesh(vertices, Godot.Mesh.PrimitiveType.Lines);
    }
    private ArrayMesh? CellsMesh(IEnumerable<int> cells, bool fill, float lift)
    {
        var vertices = new List<Vector3>();
        foreach (int cell in cells)
        {
            if (cell < 0 || cell >= CellCount)
                continue;
            var polygon = _data.Polygons[cell];
            for (int i = 0; i < polygon.Length; i++)
            {
                var a = _data.Corners[polygon[i]];
                var b = _data.Corners[polygon[(i + 1) % polygon.Length]];
                for (int j = 0; j < EdgeSegments; j++)
                {
                    if (fill)
                        vertices.Add(Point(_data.Centers[cell], lift + .008f));
                    vertices.Add(Point(a.Slerp(b, j / (float)EdgeSegments), lift));
                    vertices.Add(Point(a.Slerp(b, (j + 1) / (float)EdgeSegments), lift));
                }
            }
        }
        return Mesh(vertices, fill ? Godot.Mesh.PrimitiveType.Triangles : Godot.Mesh.PrimitiveType.Lines);
    }
    private static ArrayMesh? Mesh(List<Vector3> vertices, Godot.Mesh.PrimitiveType primitive)
    {
        if (vertices.Count == 0)
            return null;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(primitive, arrays);
        return mesh;
    }
    private static Topology GetTopology(int level)
    {
        if (Cache.TryGetValue(level, out var existing))
            return existing;
        if (level == WorldScale.BakedGridLevel)
        {
            var data = RenderAssets.ReadMap("res://assets/managed/world/grid.json");
            return Cache[level] = new(data.List("centers").Cast<Vector3>().ToArray(), data.List("corners").Cast<Vector3>().ToArray(), ReadIndices(data.List("polygons")), ReadIndices(data.List("neighbors")));
        }
        // Same refinement and stable face ordering as the original save grid.
        float g = (1 + Mathf.Sqrt(5)) * .5f;
        var points = new List<Vector3> { new(-1, g, 0), new(1, g, 0), new(-1, -g, 0), new(1, -g, 0), new(0, -1, g), new(0, 1, g), new(0, -1, -g), new(0, 1, -g), new(g, 0, -1), new(g, 0, 1), new(-g, 0, -1), new(-g, 0, 1) };
        for (int i = 0; i < points.Count; i++)
            points[i] = points[i].Normalized();
        var faces = new List<int[]> { new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 }, new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 }, new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 }, new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 } };
        for (int r = 0; r < level; r++)
        {
            var mid = new Dictionary<(int, int), int>();
            var refined = new List<int[]>();
            foreach (var f in faces)
            {
                var edges = new int[3];
                for (int e = 0; e < 3; e++)
                {
                    int a = f[e], b = f[(e + 1) % 3];
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    if (!mid.TryGetValue(key, out int v))
                    {
                        v = points.Count;
                        mid[key] = v;
                        points.Add((points[a] + points[b]).Normalized());
                    }
                    edges[e] = v;
                }
                int ab = edges[0], bc = edges[1], ca = edges[2];
                refined.AddRange([new[] { f[0], ab, ca }, new[] { f[1], bc, ab }, new[] { f[2], ca, bc }, new[] { ab, bc, ca }]);
            }
            faces = refined;
        }
        var corners = new Vector3[faces.Count];
        var adjacent = Enumerable.Range(0, points.Count).Select(_ => new List<int>()).ToArray();
        var neighbors = Enumerable.Range(0, points.Count).Select(_ => new List<int>()).ToArray();
        for (int i = 0; i < faces.Count; i++)
        {
            var f = faces[i];
            var a = points[f[0]];
            var b = points[f[1]];
            var c = points[f[2]];
            var corner = (b - a).Cross(c - a).Normalized();
            if (corner.Dot(a + b + c) < 0)
                corner = -corner;
            corners[i] = corner;
            foreach (int v in f)
                adjacent[v].Add(i);
            for (int e = 0; e < 3; e++)
            {
                int x = f[e], y = f[(e + 1) % 3];
                if (!neighbors[x].Contains(y))
                    neighbors[x].Add(y);
                if (!neighbors[y].Contains(x))
                    neighbors[y].Add(x);
            }
        }
        var polygons = new int[points.Count][];
        for (int i = 0; i < points.Count; i++)
        {
            var n = points[i];
            var axis = n.Cross(Vector3.Up).Normalized();
            if (axis.LengthSquared() < .01f)
                axis = n.Cross(Vector3.Right).Normalized();
            var second = n.Cross(axis).Normalized();
            polygons[i] = adjacent[i].OrderBy(f => Mathf.Atan2((corners[f] - n).Dot(second), (corners[f] - n).Dot(axis))).ToArray();
        }
        return Cache[level] = new(points.ToArray(), corners, polygons, neighbors.Select(n => n.ToArray()).ToArray());
    }
    public static bool ValidateStructureLayout(IReadOnlyList<object?> slots, IReadOnlyList<object?> directions)
    {
        if (!slots.Contains("starship_silo"))
            return true;
        if (slots.Count != directions.Count)
            return false;
        int level = WorldScale.GridLevel;
        while (10 * Math.Pow(4, level) + 2 < slots.Count)
            level++;
        var topology = GetTopology(level);
        var anchors = new HashSet<int>();
        var occupied = new HashSet<int>();
        for (int site = 0; site < slots.Count; site++)
        {
            Vector3 normal = Vector3.Zero;
            if (directions[site] is Vector3 v)
                normal = v;
            else if (directions[site] is List<object?> row && row.Count == 3)
                normal = new((float)Earthward.Domain.DataMap.Number(row[0]), (float)Earthward.Domain.DataMap.Number(row[1]), (float)Earthward.Domain.DataMap.Number(row[2]));
            if (!normal.IsFinite() || normal.LengthSquared() < .001f)
                return false;
            normal = normal.Normalized();
            int cell = -1;
            float best = -2;
            for (int candidate = 0; candidate < topology.Centers.Length; candidate++)
            {
                if (anchors.Contains(candidate))
                    continue;
                float alignment = normal.Dot(topology.Centers[candidate]);
                if (alignment > best)
                {
                    best = alignment;
                    cell = candidate;
                }
            }
            if (cell < 0)
                return false;
            anchors.Add(cell);
            string kind = slots[site] as string ?? "";
            if (kind == "")
                continue;
            var footprint = new List<int> { cell };
            if (kind == "starship_silo")
            {
                var visited = new HashSet<int> { cell };
                var frontier = new List<int> { cell };
                for (int ring = 0; ring < 2; ring++)
                {
                    var next = new List<int>();
                    foreach (int current in frontier)
                    {
                        if (topology.Polygons[current].Length != 6)
                            return false;
                        foreach (int adjacent in topology.Neighbors[current])
                            if (visited.Add(adjacent))
                            {
                                footprint.Add(adjacent);
                                next.Add(adjacent);
                            }
                    }
                    frontier = next;
                }
                if (footprint.Count != 19 || footprint.Any(c => topology.Polygons[c].Length != 6))
                    return false;
            }
            foreach (int member in footprint)
                if (!occupied.Add(member))
                    return false;
        }
        return true;
    }
    private static int[][] ReadIndices(List<object?> rows) => rows.Select(row => ((List<object?>)row!).Select(Convert.ToInt32).ToArray()).ToArray();
}
