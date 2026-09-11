using Godot;
using System.Collections;
using Earthward.Domain;

namespace Earthward.Presentation;

public enum ResearchConnectionKind
{
    Prerequisite,
    HiddenPrerequisite,
    HistoryBoundary,
    MissingPrerequisite
}

/// <summary>A real prerequisite edge, including explicitly identified parents outside a history window.</summary>
public sealed record ResearchConnection(string ParentId, string ChildId, ResearchConnectionKind Kind, bool ParentMet, string Label, Vector2 BoundaryPosition)
{
    public bool IsBoundary => Kind != ResearchConnectionKind.Prerequisite;
}

public partial class ResearchGraphView
{
    private readonly Dictionary<string, Vector2> _positions = new();
    private readonly Dictionary<string, string[]> _prerequisites = new();
    private readonly List<ResearchConnection> _connections = new();
    private readonly HashSet<(string Parent, string Child)> _secondaryConnections = new();
    public bool IsSecondaryConnection(ResearchConnection edge) => _secondaryConnections.Contains((edge.ParentId, edge.ChildId));
    public IReadOnlyList<ResearchConnection> Connections => _connections;
    private string _pressBoundary = "";

    public static string[] PrerequisiteIds(DataMap node)
    {
        var found = new List<string>();
        void Add(string id) { if (id.Length > 0 && !found.Contains(id)) found.Add(id); }
        switch (node.Value("requires"))
        {
            case DataMap map:
                foreach (string id in map.Keys) Add(id);
                break;
            case IDictionary map:
                foreach (var id in map.Keys) if (id is string text) Add(text);
                break;
            case IEnumerable sequence when node.Value("requires") is not string:
                foreach (var row in sequence)
                    if (row is string id) Add(id);
                    else if (row is DataMap detail) Add(detail.S("id"));
                break;
        }
        // Status details provide the same real prerequisites if a source omits its requires field.
        if (found.Count == 0)
            foreach (var detail in node.List("requirement_details").OfType<DataMap>()) Add(detail.S("id"));
        return found.ToArray();
    }

    private void RebuildConnections()
    {
        bool layoutChanged = _positions.Count != Nodes.Count;
        _prerequisites.Clear();
        _connections.Clear();
        _secondaryConnections.Clear();
        foreach (var node in Nodes)
        {
            string id = node.S("id");
            Vector2 position = ComputeWorldPosition(node);
            if (!_positions.TryGetValue(id, out var previous) || previous != position) layoutChanged = true;
            _positions[id] = position;
            _prerequisites[node.S("id")] = PrerequisiteIds(node);
        }
        foreach (string id in _positions.Keys.Where(id => !_index.ContainsKey(id)).ToArray()) _positions.Remove(id);
        var visibleIds = Nodes.Where(VisibleNode).Select(node => node.S("id")).ToArray();
        if (!_labelVisibleIds.SetEquals(visibleIds)) layoutChanged = true;
        _labelVisibleIds.Clear();
        _labelVisibleIds.UnionWith(visibleIds);
        if (layoutChanged) _labelZoom = float.NaN;
        foreach (var node in Nodes)
        {
            if (!VisibleNode(node)) continue;
            string child = node.S("id");
            var details = node.List("requirement_details").OfType<DataMap>().GroupBy(row => row.S("id")).ToDictionary(group => group.Key, group => group.Last());
            foreach (string parent in _prerequisites[child])
            {
                var detail = details.GetValueOrDefault(parent);
                bool exists = _index.TryGetValue(parent, out var parentNode);
                bool met = detail?.B("met") == true || exists && parentNode!.I("level") > 0;
                if (exists && VisibleNode(parentNode!))
                {
                    if (parentNode!.S("branch") != node.S("branch") || node.I("layout_depth") > parentNode.I("layout_depth") + 1)
                        _secondaryConnections.Add((parent, child));
                    _connections.Add(new(parent, child, ResearchConnectionKind.Prerequisite, met, "", Vector2.Zero));
                    continue;
                }
                int rank = 0;
                bool historical = !exists && node.B("is_successor") && DeepTechnology.TryParseSuccessor(parent, out _, out rank);
                ResearchConnectionKind kind = historical ? ResearchConnectionKind.HistoryBoundary : exists ? ResearchConnectionKind.HiddenPrerequisite : ResearchConnectionKind.MissingPrerequisite;
                string name = detail?.S("name", parent) ?? parentNode?.S("name", parent) ?? parent;
                string label = historical ? $"前置进阶 #{rank} · {(met ? "已研究" : "待研究")} ←" : exists ? name + " · 未显示" : "缺少前置 " + name;
                Vector2 position = exists ? WorldPosition(parentNode!) : HistoricalBoundaryPosition(node);
                _connections.Add(new(parent, child, kind, met, label, position));
            }
        }
        UpdateConnectionRoutes(layoutChanged);
    }

    private Vector2 HistoricalBoundaryPosition(DataMap child)
    {
        Vector2 outward = Vector2.Up.Rotated(Mathf.DegToRad(BranchIndex(child) * 60));
        Vector2 position = WorldPosition(child) - outward * 66;
        // A boundary marker identifies a missing historical parent; it must never sit on an unrelated node.
        var perpendicular = new Vector2(-outward.Y, outward.X);
        foreach (float side in new[] { 0f, 48f, -48f, 84f, -84f })
        {
            Vector2 candidate = position + perpendicular * side;
            if (Nodes.All(node => node.S("id") == child.S("id") || WorldPosition(node).DistanceTo(candidate) > UnscaledRadius(node) + 14))
                return candidate;
        }
        return position + perpendicular * 110;
    }

    private static float UnscaledRadius(DataMap node) => SizeKind(node) == "small" ? 12 : SizeKind(node) == "medium" ? 20 : 32;

    public float NodeBoundaryRadius(DataMap node, Vector2 direction)
    {
        float radius = Radius(node);
        if (SizeKind(node) != "medium" || direction.LengthSquared() < .001f) return radius;
        float halfSector = Mathf.Pi / 8, vertexRotation = AlienNode(node) ? Mathf.Pi / 4 : Mathf.Pi / 8;
        float edgeAngle = vertexRotation + halfSector;
        float relative = Mathf.Wrap(direction.Angle() - edgeAngle, -halfSector, halfSector);
        return radius * Mathf.Cos(halfSector) / Mathf.Cos(relative);
    }

    public Vector2 BoundaryPoint(ResearchConnection edge) => Canvas.Size * .5f + Pan + edge.BoundaryPosition * Zoom;

    /// <summary>Screen-space endpoints terminate at actual node/marker boundaries, not at their centers.</summary>
    public (Vector2 From, Vector2 To) ConnectionEndpoints(ResearchConnection edge)
    {
        Vector2 from = edge.IsBoundary ? BoundaryPoint(edge) : Point(Node(edge.ParentId));
        Vector2 to = Point(Node(edge.ChildId));
        var path = ConnectionWorldPath(edge);
        Vector2 direction = (path[1] - path[0]).Normalized();
        Vector2 incoming = (path[^1] - path[^2]).Normalized();
        float parentRadius = edge.IsBoundary ? 5 : NodeBoundaryRadius(Node(edge.ParentId), direction);
        float childRadius = NodeBoundaryRadius(Node(edge.ChildId), -incoming);
        float length = from.DistanceTo(to);
        if (length <= parentRadius + childRadius + 1) return (from, to);
        return (from + direction * parentRadius, to - incoming * childRadius);
    }

    private string HitBoundary(Vector2 point)
    {
        foreach (var edge in _connections)
            if (edge.Kind == ResearchConnectionKind.HistoryBoundary && BoundaryPoint(edge).DistanceTo(point) <= Math.Max(9, 7 * Zoom))
                return edge.ParentId;
        return "";
    }

    private void DrawConnections(string active)
    {
        foreach (var edge in _connections)
        {
            var (from, to) = ConnectionEndpoints(edge);
            bool selected = edge.ChildId == active || edge.ParentId == active;
            Color color = selected ? UiTheme.Amber : edge.ParentMet ? new Color("527e74") : new Color("3f5968");
            bool secondary = IsSecondaryConnection(edge);
            if (secondary && !selected) color = UiTheme.Alpha(color, .54f);
            float width = selected ? 1.8f : secondary ? .9f : 1.15f;
            if (edge.IsBoundary)
            {
                if (edge.Kind == ResearchConnectionKind.MissingPrerequisite) color = UiTheme.Coral;
                Vector2 delta = to - from;
                float length = delta.Length();
                if (length > .01f)
                    for (float start = 0; start < length; start += 10)
                        Canvas.DrawLine(from + delta * (start / length), from + delta * (Math.Min(start + 6, length) / length), color, width, true);
                Vector2 marker = BoundaryPoint(edge);
                Canvas.DrawCircle(marker, 5, new Color("0b1c27"));
                Canvas.DrawArc(marker, 5, 0, Mathf.Tau, 16, color, 1.4f, true);
                if (Zoom >= .55f || selected)
                    Canvas.DrawString(UiTheme.Font, marker + new Vector2(-100, -13), UiTheme.Fit(edge.Label, 200, 10), HorizontalAlignment.Center, 200, 10, color);
            }
            else
            {
                var path = ConnectionWorldPath(edge);
                Vector2 origin = Canvas.Size * .5f + Pan;
                for (int i = 1; i < path.Count; i++)
                {
                    Vector2 a = i == 1 ? from : origin + path[i - 1] * Zoom;
                    Vector2 b = i == path.Count - 1 ? to : origin + path[i] * Zoom;
                    if (secondary && !selected) Canvas.DrawDashedLine(a, b, color, width, 5, true, true);
                    else Canvas.DrawLine(a, b, color, width, true);
                }
            }
            if (Zoom >= .72f && from.DistanceSquaredTo(to) > 144)
            {
                var path = ConnectionWorldPath(edge);
                Vector2 direction = (path[^1] - path[^2]).Normalized(), side = new(-direction.Y, direction.X);
                Canvas.DrawPolyline(new[] { to - direction * 6 + side * 2.5f, to, to - direction * 6 - side * 2.5f }, color, width, true);
            }
        }
    }
}
