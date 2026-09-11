using Godot;
using Earthward.Domain;

namespace Earthward.Presentation;

public partial class ResearchGraphView
{
    private float _labelZoom = float.NaN;
    private readonly HashSet<string> _labelVisibleIds = new();
    private readonly Dictionary<string, Vector2> _labelOffsets = new();
    private readonly Dictionary<Vector2I, List<(string Id, Vector2 Point, float Radius)>> _labelGrid = new();
    private const float LabelCellSize = 96;

    private static IEnumerable<Vector2I> LabelCells(Rect2 rect)
    {
        int left = Mathf.FloorToInt(rect.Position.X / LabelCellSize), right = Mathf.FloorToInt(rect.End.X / LabelCellSize);
        int top = Mathf.FloorToInt(rect.Position.Y / LabelCellSize), bottom = Mathf.FloorToInt(rect.End.Y / LabelCellSize);
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++) yield return new Vector2I(x, y);
    }

    // Panning translates labels and nodes together. Recompute only when zoom or topology changes.
    private void UpdateNodeLabels()
    {
        if (_labelZoom == Zoom) return;
        _labelZoom = Zoom;
        _labelOffsets.Clear();
        _labelGrid.Clear();
        if (Zoom < .72f) return;
        foreach (var node in Nodes)
        {
            if (!VisibleNode(node)) continue;
            Vector2 point = WorldPosition(node) * Zoom;
            float radius = Radius(node) + 4;
            var item = (node.S("id"), point, radius);
            foreach (var cell in LabelCells(new Rect2(point - Vector2.One * radius, Vector2.One * radius * 2)))
            {
                if (!_labelGrid.TryGetValue(cell, out var list)) _labelGrid[cell] = list = new();
                list.Add(item);
            }
        }
        var used = new List<Rect2>();
        foreach (var node in Nodes.Where(node => VisibleNode(node) && SizeKind(node) != "small").OrderByDescending(node => UnscaledRadius(node)).ThenBy(node => node.I("layout_depth")).ThenBy(node => node.S("id"), StringComparer.Ordinal))
        {
            string id = node.S("id"), text = UiTheme.Fit(node.S("name"), 140, 12);
            float width = Math.Min(140, UiTheme.Font.GetStringSize(text, fontSize: 12).X), radius = Radius(node);
            Vector2 point = WorldPosition(node) * Zoom;
            Vector2[] candidates = [new(-width * .5f, radius + 18), new(-width * .5f, -radius - 7), new(radius + 10, 4), new(-radius - 10 - width, 4)];
            foreach (var offset in candidates)
            {
                Rect2 label = new(point + offset - new Vector2(2, 12), new Vector2(width + 4, 16));
                bool blocked = used.Any(rect => rect.Intersects(label));
                foreach (var cell in LabelCells(label))
                {
                    if (blocked) break;
                    if (!_labelGrid.TryGetValue(cell, out var circles)) continue;
                    foreach (var circle in circles)
                    {
                        if (circle.Id == id) continue;
                        Vector2 closest = circle.Point.Clamp(label.Position, label.End);
                        if (closest.DistanceSquaredTo(circle.Point) < circle.Radius * circle.Radius) { blocked = true; break; }
                    }
                }
                if (blocked) continue;
                _labelOffsets[id] = offset;
                used.Add(label);
                break;
            }
        }
    }
}
