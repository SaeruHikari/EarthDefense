using Godot;
using Earthward.Domain;

namespace Earthward.Presentation;

public partial class ResearchGraphView
{
    private readonly record struct RouteObstacle(string Id, Vector2 Point, float Radius);
    private readonly Dictionary<(string Parent, string Child), Vector2[]> _routes = new();
    private readonly Dictionary<Vector2I, List<string>> _routeCells = new();
    private readonly List<RouteObstacle> _routeObstacles = new();
    private const float RouteGridSize = 8;
    private static readonly Vector2I[] RouteNeighbors = [new(1, 0), new(0, 1), new(-1, 0), new(0, -1), new(1, 1), new(-1, 1), new(-1, -1), new(1, -1)];

    private bool RouteSegmentClear(Vector2 from, Vector2 to, string parent, string child)
    {
        Vector2 delta = to - from;
        float squared = delta.LengthSquared();
        if (squared < .0001f) return true;
        foreach (var obstacle in _routeObstacles)
        {
            if (obstacle.Id == parent || obstacle.Id == child) continue;
            float t = Math.Clamp((obstacle.Point - from).Dot(delta) / squared, 0, 1);
            if (obstacle.Point.DistanceSquaredTo(from + delta * t) < (obstacle.Radius + 4) * (obstacle.Radius + 4)) return false;
        }
        return true;
    }

    private void UpdateConnectionRoutes(bool geometryChanged)
    {
        var keys = _connections.Where(edge => !edge.IsBoundary).Select(edge => (edge.ParentId, edge.ChildId)).ToHashSet();
        if (!geometryChanged && keys.SetEquals(_routes.Keys)) return;
        _routes.Clear();
        _routeCells.Clear();
        _routeObstacles.Clear();
        foreach (var node in Nodes)
        {
            if (!VisibleNode(node)) continue;
            string id = node.S("id");
            Vector2 point = WorldPosition(node);
            float radius = UnscaledRadius(node);
            _routeObstacles.Add(new(id, point, radius));
            float expanded = radius + 10;
            Vector2I low = new(Mathf.FloorToInt((point.X - expanded) / RouteGridSize), Mathf.FloorToInt((point.Y - expanded) / RouteGridSize));
            Vector2I high = new(Mathf.CeilToInt((point.X + expanded) / RouteGridSize), Mathf.CeilToInt((point.Y + expanded) / RouteGridSize));
            for (int y = low.Y; y <= high.Y; y++)
                for (int x = low.X; x <= high.X; x++)
                {
                    var cell = new Vector2I(x, y);
                    if ((new Vector2(x, y) * RouteGridSize).DistanceSquaredTo(point) > expanded * expanded) continue;
                    if (!_routeCells.TryGetValue(cell, out var occupants)) _routeCells[cell] = occupants = new();
                    occupants.Add(id);
                }
        }
        foreach (var edge in _connections)
        {
            if (edge.IsBoundary) continue;
            Vector2 from = WorldPosition(Node(edge.ParentId)), to = WorldPosition(Node(edge.ChildId));
            Vector2[] path = [from, to];
            if (!RouteSegmentClear(from, to, edge.ParentId, edge.ChildId))
            {
                var candidates = FindConnectionRoute(from, to, edge.ParentId, edge.ChildId, 128);
                if (candidates.Length == 0) candidates = FindConnectionRoute(from, to, edge.ParentId, edge.ChildId, 320);
                if (candidates.Length > 0) path = candidates;
            }
            _routes[(edge.ParentId, edge.ChildId)] = path;
        }
    }

    private bool RouteCellOpen(Vector2I cell, string parent, string child) => !_routeCells.TryGetValue(cell, out var occupants) || occupants.All(id => id == parent || id == child);

    private Vector2[] FindConnectionRoute(Vector2 from, Vector2 to, string parent, string child, int margin)
    {
        Vector2I start = new(Mathf.RoundToInt(from.X / RouteGridSize), Mathf.RoundToInt(from.Y / RouteGridSize));
        Vector2I goal = new(Mathf.RoundToInt(to.X / RouteGridSize), Mathf.RoundToInt(to.Y / RouteGridSize));
        int padding = Mathf.CeilToInt(margin / RouteGridSize);
        var lower = new Vector2I(Math.Min(start.X, goal.X) - padding, Math.Min(start.Y, goal.Y) - padding);
        var upper = new Vector2I(Math.Max(start.X, goal.X) + padding, Math.Max(start.Y, goal.Y) + padding);
        var open = new PriorityQueue<(Vector2I Cell, float Cost), double>();
        var costs = new Dictionary<Vector2I, float> { [start] = 0 };
        var previous = new Dictionary<Vector2I, Vector2I>();
        long order = 0;
        open.Enqueue((start, 0), 0);
        int expanded = 0;
        while (open.Count > 0 && expanded++ < 60000)
        {
            var item = open.Dequeue();
            if (item.Cost > costs[item.Cell] + .0001f) continue;
            if (item.Cell == goal)
            {
                var raw = new List<Vector2> { to };
                Vector2I current = goal;
                while (current != start)
                {
                    raw.Add(new Vector2(current.X, current.Y) * RouteGridSize);
                    current = previous[current];
                }
                raw.Add(from);
                raw.Reverse();
                var simple = new List<Vector2> { from };
                for (int i = 0; i < raw.Count - 1;)
                {
                    int next = i + 1;
                    for (int candidate = i + 2; candidate < raw.Count; candidate++)
                    {
                        if (!RouteSegmentClear(raw[i], raw[candidate], parent, child)) break;
                        next = candidate;
                    }
                    if (simple[^1].DistanceSquaredTo(raw[next]) > .01f) simple.Add(raw[next]);
                    i = next;
                }
                return simple.ToArray();
            }
            foreach (var offset in RouteNeighbors)
            {
                Vector2I next = item.Cell + offset;
                if (next.X < lower.X || next.Y < lower.Y || next.X > upper.X || next.Y > upper.Y || !RouteCellOpen(next, parent, child)) continue;
                bool diagonal = offset.X != 0 && offset.Y != 0;
                if (diagonal && (!RouteCellOpen(item.Cell + new Vector2I(offset.X, 0), parent, child) || !RouteCellOpen(item.Cell + new Vector2I(0, offset.Y), parent, child))) continue;
                float cost = item.Cost + (diagonal ? 1.41421356f : 1);
                if (costs.TryGetValue(next, out float best) && cost >= best - .0001f) continue;
                costs[next] = cost;
                previous[next] = item.Cell;
                float heuristic = new Vector2(goal.X - next.X, goal.Y - next.Y).Length();
                open.Enqueue((next, cost), cost + heuristic + (++order) * .000000001);
            }
        }
        return Array.Empty<Vector2>();
    }

    public IReadOnlyList<Vector2> ConnectionWorldPath(ResearchConnection edge)
        => _routes.TryGetValue((edge.ParentId, edge.ChildId), out var path) ? path : new[] { edge.BoundaryPosition, WorldPosition(Node(edge.ChildId)) };

    public bool ConnectionAvoidsUnrelatedNodes(ResearchConnection edge)
    {
        var path = ConnectionWorldPath(edge);
        for (int i = 1; i < path.Count; i++)
            if (!RouteSegmentClear(path[i - 1], path[i], edge.ParentId, edge.ChildId)) return false;
        return true;
    }
}
