using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
namespace Earthward.Presentation;

public partial class ResearchGraphView : Control
{
    public event Action<string>? PurchaseRequested;
    public event Action<string>? FocusWindowRequested;
    public static readonly string[] BranchIds = { "K", "M", "L", "I", "D", "C" };
    public static readonly string[] BranchNames = { "动能武装", "导弹工程", "光束科技", "工业科研", "防御生存", "航程指挥" };
    public const float MinZoom = .16f;
    public const float MaxZoom = 1.8f;
    public const float DragThreshold = 5;
    public const float FirstRingRadius = 220;
    public const float RingSpacing = 84;
    public static float ResearchRingRadius(int depth) => FirstRingRadius + Math.Max(0, depth - 1) * RingSpacing;

    public List<DataMap> Nodes { get; } = new();

    public Dictionary<string, Button> NodeButtons { get; } = new();
    public string SelectedId { get; private set; } = "";
    public string HoverId { get; private set; } = "";
    public string TutorialHighlightId { get; private set; } = "";
    public bool IsPointerActive => _pointerActive;
    public float Zoom { get; set; } = 1;
    public Vector2 Pan { get; set; } = Vector2.Zero;
    public string Notice { get; set; } = "";
    public Control Canvas { get; private set; } = null!;
    public Button FitButton { get; private set; } = null!;
    private PanelContainer _hoverPopup = null!;
    private Label _hoverLabel = null!;
    private HBoxContainer _historyToolbar = null!;
    public Button PreviousHistoryButton { get; private set; } = null!;
    public Button LatestHistoryButton { get; private set; } = null!;
    public Button NextHistoryButton { get; private set; } = null!;

    private readonly Dictionary<string, DataMap> _index = new();

    private readonly Dictionary<string, (float Age, string Text)> _pulses = new();
    private float _tutorialHighlightTime;
    private bool _pointerActive;
    private bool _dragging;
    private bool _fitOnLayout;
    private float _dragDistance;
    private string _pressNode = "";
    private Vector2 _previous;
    private static readonly string[][] SmallIcons = { new[] { "damage", "cycle", "speed", "crack", "range" }, new[] { "damage", "cycle", "blast", "turn", "speed" }, new[] { "damage", "cycle", "range", "turn", "energy_break" }, new[] { "mineral", "power", "science", "capacity", "assembly" }, new[] { "armor", "shield", "repair", "death_blast", "blast" }, new[] { "orbit", "range", "speed", "turn", "return" } };

    public override void _Ready()
    {
        Name = "ResearchGraphView";
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        AddThemeFontOverride("font", UiTheme.Font);
        Canvas = new Control { MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
        AddChild(Canvas);
        Canvas.Draw += DrawMap;
        Canvas.GuiInput += CanvasInput;
        Canvas.MouseExited += HideHover;
        CreateHoverPopup();
        CreateHistoryToolbar();
        FitButton = UiTheme.Button("回中", 12);
        FitButton.Name = "FitResearchGraph";
        FitButton.TooltipText = "查看全部已开放科技 · Home";
        FitButton.Pressed += FitView;
        AddChild(FitButton);
        Resized += Layout;
        VisibilityChanged += () => { if (!IsVisibleInTree()) { _pointerActive = false; _dragging = false; HideHover(); } };
        Layout();
        RebuildButtons();
        SetProcess(_pulses.Count > 0 || TutorialHighlightId != "");
    }

    public void SetNodes(IEnumerable<DataMap> value)
    {
        string[] old = _index.Keys.ToArray();
        Nodes.Clear();
        _index.Clear();
        foreach (var source in value)
        {
            string id = source.S("id");
            if (id == "" || _index.ContainsKey(id))
                continue;
            var node = source.DeepClone();
            Nodes.Add(node);
            _index[id] = node;
        }
        RebuildConnections();
        if (!Available(SelectedId))
        {
            SelectedId = "";
        }
        if (!Available(HoverId))
            HideHover();
        if (Canvas == null)
            return;
        if (!old.SequenceEqual(_index.Keys))
            RebuildButtons();
        else
            PositionButtons();
        if (HoverId != "")
            ShowHover(HoverId);
        Canvas.QueueRedraw();
    }

    public void SetExpeditionUnlocked(bool _)
    { /* The active research catalog owns visibility. */
    }

    public DataMap Node(string id) => _index.TryGetValue(id, out var node) ? node : new();

    private static bool VisibleNode(DataMap node) => node.B("visible", true);

    private bool Available(string id) => id != "" && _index.TryGetValue(id, out var node) && VisibleNode(node);

    public static int BranchIndex(DataMap node)
    {
        string id = node.S("id", "K");
        int index = Array.IndexOf(BranchIds, node.S("branch_id", node.S("branch", id[..1])));
        return index < 0 ? Math.Abs(node.I("branch_index")) % 6 : index;
    }

    private static string SizeKind(DataMap node) => node.S("size", "medium") == "alien_medium" ? "medium" : node.S("size", "medium");

    private static bool AlienNode(DataMap node) => node.B("alien") || node.S("id").Contains("_A") || SizeKind(node) == "large";

    public Vector2 WorldPosition(DataMap node) => _positions.TryGetValue(node.S("id"), out var position) ? position : ComputeWorldPosition(node);

    private Vector2 ComputeWorldPosition(DataMap node)
    {
        if (node.B("is_successor") && _index.TryGetValue(node.S("chain_branch", node.S("branch")) + "_G2", out var capstone))
        {
            // History is a bounded window. Its six visible nodes continue directly outside their own
            // capstone, instead of jumping to one global outer radius; the missing earlier parent has a named stub.
            Vector2 anchor = ComputeWorldPosition(capstone);
            int first = Math.Max(1, node.I("chain_window_start", node.I("chain_rank", 1)));
            int localRank = Math.Max(0, node.I("chain_rank", first) - first);
            return anchor.Normalized() * (anchor.Length() + RingSpacing * (localRank + 1));
        }
        if (node.TryGetValue("position", out var raw) || node.TryGetValue("draw_position", out raw))
        {
            if (raw is Vector2 position)
                return position;
            if (raw is System.Collections.IList list && list.Count == 2)
                return new Vector2(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]));
        }
        if (node.ContainsKey("layout_depth"))
        {
            // Layout metadata is generated from the dependency graph, independent of purchase state.
            float angle = Mathf.DegToRad(BranchIndex(node) * 60 + (float)node.N("layout_lane") * 6);
            return Vector2.Up.Rotated(angle) * ResearchRingRadius(node.I("layout_depth", 1));
        }
        string code = node.S("id", "K_S01").Split('_').Last();
        int ordinal = int.TryParse(code.Length > 1 ? code[1..] : "1", out int number) ? Math.Max(1, number) : 1;
        Vector2 local;
        if (code.StartsWith("S"))
        {
            int tier = (ordinal - 1) / 5;
            local = new Vector2(((ordinal - 1) % 5 - 2) * (56 + Math.Min(tier, 4) * 8), 220 + tier * 176);
        }
        else if (code.StartsWith("N") || code.StartsWith("A"))
        {
            int tier = Math.Clamp(ordinal - 1, 0, 2);
            local = new Vector2(new[] { 125f, 175f, 220f }[tier] * (code.StartsWith("A") ? 1 : -1), new[] { 365f, 605f, 850f }[tier]);
        }
        else
            local = new Vector2(0, ordinal == 1 ? 615 : 1120);
        return new Vector2(local.X, -local.Y).Rotated(Mathf.DegToRad(BranchIndex(node) * 60));
    }

    public Vector2 Point(DataMap node) => Canvas.Size * .5f + Pan + WorldPosition(node) * Zoom;

    public float Radius(DataMap node) => Math.Max(SizeKind(node) == "small" ? 3 : SizeKind(node) == "medium" ? 5 : 8, (SizeKind(node) == "small" ? 12 : SizeKind(node) == "medium" ? 20 : 32) * Zoom);

    private float HitRadius(DataMap node) => Radius(node) + Math.Max(2, 6 * Math.Min(Zoom, 1));

    public string SmallIcon(DataMap node)
    {
        if (node.S("icon") != "")
            return node.S("icon");
        string code = node.S("id", "K_S01").Split('_').Last();
        int.TryParse(code.Length > 1 ? code[1..] : "1", out int n);
        return SmallIcons[BranchIndex(node)][((n - 1) % 5 + 5) % 5];
    }

    public void PositionButtons()
    {
        UpdateHistoryToolbar();
        if (Canvas == null)
            return;
        UpdateNodeLabels();
        foreach (var node in Nodes)
            if (NodeButtons.TryGetValue(node.S("id"), out var button))
            {
                button.Visible = VisibleNode(node);
                button.Size = Vector2.One * HitRadius(node) * 2;
                button.Position = Point(node) - button.Size * .5f;
            }
        Canvas.QueueRedraw();
    }

    private void RebuildButtons()
    {
        foreach (var button in NodeButtons.Values)
        {
            button.Hide();
            button.QueueFree();
        }
        NodeButtons.Clear();
        foreach (var node in Nodes)
        {
            string id = node.S("id");
            var button = new Button { Name = "Research_" + id, Flat = true, MouseFilter = MouseFilterEnum.Ignore, FocusMode = FocusModeEnum.All };
            foreach (string state in new[] { "normal", "hover", "pressed" })
                button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            button.Pressed += () => ActivateNode(id);
            button.FocusEntered += () => { EnsureVisible(id); ShowHover(id); };
            button.GuiInput += e => NodeKeyboard(e, id);
            Canvas.AddChild(button);
            NodeButtons[id] = button;
        }
        PositionButtons();
    }

    private void Layout()
    {
        if (Canvas == null)
            return;
        Canvas.Position = Vector2.Zero;
        Canvas.Size = Size;
        FitButton.Position = new Vector2(Math.Max(8, Size.X - 66), 10);
        FitButton.Size = new Vector2(54, 30);
        PositionButtons();
        if (_fitOnLayout)
        {
            _fitOnLayout = false;
            FitView();
        }
        if (HoverId != "")
            ShowHover(HoverId);
    }

    public void FitView()
    {
        if (Canvas == null || Canvas.Size.X <= 1 || Canvas.Size.Y <= 1)
        {
            _fitOnLayout = true;
            return;
        }
        bool first = true;
        Rect2 bounds = new();
        foreach (var node in Nodes.Where(VisibleNode))
        {
            var p = WorldPosition(node);
            if (first)
            {
                bounds = new Rect2(p, Vector2.Zero);
                first = false;
            }
            else
                bounds = bounds.Expand(p);
        }
        Vector2 space = (Canvas.Size - new Vector2(142, 150)).Max(new Vector2(80, 80));
        Zoom = Math.Clamp(Math.Min(space.X / Math.Max(1, bounds.Size.X), space.Y / Math.Max(1, bounds.Size.Y)), MinZoom, 1);
        Pan = -bounds.GetCenter() * Zoom;
        HideHover();
        PositionButtons();
    }

    public void FocusNode(string id, bool center = false)
    {
        if (!Available(id))
        {
            if (id.Contains("_R")) FocusWindowRequested?.Invoke(id);
            return;
        }
        if (Canvas == null) return;
        if (center)
        {
            // A tutorial may interrupt an in-progress graph drag. Do not let
            // its eventual mouse-up activate a node after the canvas moved.
            _pointerActive = false;
            _dragging = false;
            _pressNode = "";
            _pressBoundary = "";
            Zoom = Math.Clamp(Math.Max(Zoom, .85f), MinZoom, MaxZoom);
            Pan = -WorldPosition(Node(id)) * Zoom;
            PositionButtons();
        }
        else
            EnsureVisible(id);
        NodeButtons[id].GrabFocus();
        SelectedId = id;
        UpdateHistoryToolbar();
        ShowHover(id);
        Canvas.QueueRedraw();
    }

    public void SetTutorialHighlight(string id)
    {
        if (TutorialHighlightId == id)
            return;
        TutorialHighlightId = id;
        _tutorialHighlightTime = 0;
        SetProcess(_pulses.Count > 0 || TutorialHighlightId != "");
        Canvas?.QueueRedraw();
    }

    private void EnsureVisible(string id)
    {
        if (!new Rect2(new Vector2(96, 96), (Canvas.Size - new Vector2(192, 220)).Max(Vector2.One)).HasPoint(Point(Node(id))))
        {
            Pan = -WorldPosition(Node(id)) * Zoom;
            PositionButtons();
        }
    }



    public void ActivateNode(string id)
    {
        if (!Available(id))
            return;
        var node = Node(id);
        bool canBuy = node.B("available") && node.I("level") < node.I("max", 1);
        if (canBuy)
        {
            SelectedId = id;
            HideHover();
            int old = node.I("level");
            string effect = node.S("preview").Split('\n')[0];
            PurchaseRequested?.Invoke(id);
            if (Node(id).I("level") > old)
            {
                _pulses[id] = (0, effect);
                SetProcess(true);
            }
            Canvas.QueueRedraw();
        }
        else
            FocusNode(id);
    }



    private string HitNode(Vector2 point)
    {
        string closest = "";
        float distance = float.PositiveInfinity;
        foreach (var node in Nodes.Where(VisibleNode))
        {
            float d = Point(node).DistanceSquaredTo(point);
            float r = HitRadius(node);
            if (d <= r * r && d < distance)
            {
                distance = d;
                closest = node.S("id");
            }
        }
        return closest;
    }

    private void CanvasInput(InputEvent e)
    {
        if (e is InputEventMouse m && !new Rect2(Vector2.Zero, Canvas.Size).HasPoint(m.Position))
            return;
        if (e is InputEventMouseButton button && button.Pressed)
        {
            if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                ZoomAt(button.ButtonIndex == MouseButton.WheelUp ? 1.12f : 1 / 1.12f, button.Position);
                Canvas.AcceptEvent();
            }
            else if (button.ButtonIndex == MouseButton.Left)
            {
                _pointerActive = true;
                _dragging = false;
                _dragDistance = 0;
                _previous = button.Position;
                _pressNode = HitNode(button.Position);
                _pressBoundary = _pressNode == "" ? HitBoundary(button.Position) : "";
                HideHover();
                Canvas.AcceptEvent();
            }
        }
        else if (e is InputEventMouseMotion motion && !_pointerActive)
        {
            string id = HitNode(motion.Position);
            if (id != HoverId)
            {
                if (id == "")
                    HideHover();
                else
                    ShowHover(id);
            }
        }
    }

    public override void _Input(InputEvent e)
    {
        if (!_pointerActive || !IsVisibleInTree())
            return;
        if (e is InputEventMouseMotion motion)
        {
            Vector2 point = GetGlobalTransformWithCanvas().AffineInverse() * motion.Position;
            Vector2 delta = point - _previous;
            _previous = point;
            _dragDistance += delta.Length();
            if (_dragDistance > DragThreshold)
            {
                _dragging = true;
                Pan += delta;
                PositionButtons();
            }
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left && !button.Pressed)
        {
            Vector2 point = GetGlobalTransformWithCanvas().AffineInverse() * button.Position;
            _pointerActive = false;
            if (!_dragging)
            {
                if (_pressNode != "" && HitNode(point) == _pressNode)
                    ActivateNode(_pressNode);
                else if (_pressBoundary != "" && HitBoundary(point) == _pressBoundary)
                    FocusWindowRequested?.Invoke(_pressBoundary);
                else if (_pressNode == "")
                {
                    SelectedId = "";
                    HideHover();
                    UpdateHistoryToolbar();
                }
            }
            _dragging = false;
            _pressNode = "";
            _pressBoundary = "";
        }
    }

    private void ZoomAt(float factor, Vector2 point)
    {
        float old = Zoom;
        Vector2 relative = point - Canvas.Size * .5f;
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        Pan = relative - (relative - Pan) * Zoom / old;
        HideHover();
        PositionButtons();
    }

    public override void _Process(double delta)
    {
        // Uses UI frame time, independent of the paused combat simulation.
        if (TutorialHighlightId != "")
            _tutorialHighlightTime = (_tutorialHighlightTime + (float)delta) % 1.4f;
        foreach (string id in _pulses.Keys.ToArray())
        {
            var p = _pulses[id];
            p.Age += (float)delta;
            if (p.Age > .65f)
                _pulses.Remove(id);
            else
                _pulses[id] = p;
        }
        Canvas.QueueRedraw();
        if (_pulses.Count == 0 && TutorialHighlightId == "")
            SetProcess(false);
    }

    private void CreateHistoryToolbar()
    {
        _historyToolbar = new HBoxContainer { Name = "ResearchHistory", Position = new Vector2(12, 10), ZIndex = 12 };
        _historyToolbar.AddThemeConstantOverride("separation", 5);
        AddChild(_historyToolbar);
        PreviousHistoryButton = UiTheme.Button("← 前3项", 11);
        LatestHistoryButton = UiTheme.Button("最新", 11);
        NextHistoryButton = UiTheme.Button("后3项 →", 11);
        foreach (var button in new[] { PreviousHistoryButton, LatestHistoryButton, NextHistoryButton })
        {
            button.CustomMinimumSize = new Vector2(64, 28);
            _historyToolbar.AddChild(button);
        }
        PreviousHistoryButton.Pressed += () => FocusWindowRequested?.Invoke(Node(SelectedId).S("prev_page"));
        LatestHistoryButton.Pressed += () => FocusWindowRequested?.Invoke(Node(SelectedId).S("chain_latest_id"));
        NextHistoryButton.Pressed += () => FocusWindowRequested?.Invoke(Node(SelectedId).S("next_page"));
        _historyToolbar.Hide();
    }

    private void UpdateHistoryToolbar()
    {
        if (_historyToolbar == null) return;
        var node = Node(SelectedId);
        _historyToolbar.Visible = node.B("is_successor");
        if (!_historyToolbar.Visible) return;
        PreviousHistoryButton.Disabled = node.S("prev_page") == "";
        LatestHistoryButton.Disabled = node.S("chain_latest_id") == "" || node.S("id") == node.S("chain_latest_id");
        NextHistoryButton.Disabled = node.S("next_page") == "";
        _historyToolbar.TooltipText = $"每个后继节点只能研究一次 · 当前查看 #{node.I("chain_rank")} · 已完成 {node.I("completed_chain_count")} 项";
    }

    private void CreateHoverPopup()
    {
        _hoverPopup = new PanelContainer { Name = "ResearchHover", MouseFilter = MouseFilterEnum.Ignore };
        _hoverPopup.AddThemeStyleboxOverride("panel", UiTheme.Panel());
        _hoverLabel = UiTheme.Label("", 13);
        _hoverPopup.AddChild(_hoverLabel);
        AddChild(_hoverPopup);
        _hoverPopup.Hide();
    }

    private Vector2 PopupPosition(string id, Vector2 dimensions)
    {
        Vector2 c = Point(Node(id));
        float gap = Radius(Node(id)) + 15;
        Rect2 bounds = new(new Vector2(8, 8), (Canvas.Size - new Vector2(16, 47)).Max(Vector2.One));
        Vector2[] candidates = { new(c.X + gap, c.Y - dimensions.Y * .4f), new(c.X - gap - dimensions.X, c.Y - dimensions.Y * .4f), new(c.X - dimensions.X * .5f, c.Y + gap), new(c.X - dimensions.X * .5f, c.Y - gap - dimensions.Y) };
        foreach (var at in candidates)
            if (bounds.Encloses(new Rect2(at, dimensions)))
                return at;
        var selected = c.X < Canvas.Size.X * .5f ? candidates[0] : candidates[1];
        return new Vector2(Math.Clamp(selected.X, bounds.Position.X, Math.Max(bounds.Position.X, bounds.End.X - dimensions.X)), Math.Clamp(selected.Y, bounds.Position.Y, Math.Max(bounds.Position.Y, bounds.End.Y - dimensions.Y)));
    }

    private void ShowHover(string id)
    {
        if (!Available(id) || _hoverPopup == null)
            return;
        HoverId = id;
        var node = Node(id);
        var lines = node.S("preview").Split('\n');
        string preview = string.Join('\n', lines.Take(3)) + (lines.Length > 3 ? "\n…" : "");
        string cost = node.I("level") > 0 ? "已完成 · 不会再次扣费" : node.S("cost_text");
        _hoverLabel.Text = $"{node.S("name", id)}  {node.I("level")} / {node.I("max", 1)}\n{preview}\n{cost}";
        var requirements = UiTheme.Maps(node.List("requirement_details")).ToArray();
        if (requirements.Length > 0)
            _hoverLabel.Text += "\n前置：" + string.Join("、", requirements.Select(row => row.S("name", row.S("id")) + (row.B("met") ? " ✓" : " · 待研究")));

        if (node.B("is_successor"))
            _hoverLabel.Text += $"\n后继 #{node.I("chain_rank")} · 已完成 {node.I("completed_chain_count")} 项 · 每节点只研究一次";
        if (node.S("lock_reason") != "")
            _hoverLabel.Text += "\n" + node.S("lock_reason");
        float width = Math.Min(285, Math.Max(210, Canvas.Size.X * .39f));
        _hoverLabel.CustomMinimumSize = new Vector2(width - 26, 0);
        _hoverPopup.Size = new Vector2(width, 0);
        _hoverPopup.ResetSize();
        _hoverPopup.Position = PopupPosition(id, _hoverPopup.Size);
        _hoverPopup.Show();
        Canvas.QueueRedraw();
    }

    private void HideHover()
    {
        HoverId = "";
        _hoverPopup?.Hide();
        Canvas?.QueueRedraw();
    }



    private void DrawMap()
    {
        Canvas.DrawRect(new Rect2(Vector2.Zero, Canvas.Size), new Color("0b1c27"));
        Vector2 center = Canvas.Size * .5f + Pan;
        string active = HoverId != "" ? HoverId : SelectedId;
        var required = _prerequisites.GetValueOrDefault(active, Array.Empty<string>());
        DrawConnections(active);
        foreach (var node in Nodes)
            if (VisibleNode(node) && _prerequisites[node.S("id")].Length == 0)
            {
                Vector2 target = Point(node), direction = (target - center).Normalized();
                Canvas.DrawLine(center + direction * 31, target - direction * Radius(node), new Color("25434d"), 1, true);
            }
        Canvas.DrawCircle(center, 26, new Color("193a3c"));
        Canvas.DrawArc(center, 31, 0, Mathf.Tau, 48, new Color("53776f"), 1, true);
        Canvas.DrawString(UiTheme.Font, center + new Vector2(-14, 5), "地球", fontSize: 14, modulate: UiTheme.Mint);
        for (int branch = 0; branch < 6; branch++)
            if (Zoom >= .68f && Nodes.Any(n => VisibleNode(n) && BranchIndex(n) == branch))
                Canvas.DrawString(UiTheme.Font, center + new Vector2(0, -172).Rotated(Mathf.Tau * branch / 6f) * Zoom + new Vector2(-48, 5), BranchNames[branch], HorizontalAlignment.Center, 96, 12, UiTheme.Muted);
        foreach (var node in Nodes.Where(VisibleNode))
        {
            string id = node.S("id");
            Vector2 p = Point(node);
            float r = Radius(node);
            if (!new Rect2(new Vector2(-100, -100), Canvas.Size + new Vector2(200, 200)).HasPoint(p))
                continue;
            bool owned = node.I("level") > 0;
            bool available = node.B("available");
            bool alien = AlienNode(node);
            string size = SizeKind(node);
            Color color = owned || available ? (alien ? UiTheme.Alien : UiTheme.Mint) : new Color(alien ? "716985" : "667d89");
            Color fill = new(alien ? (owned ? "392d51" : "231e37") : (owned ? "285249" : "112733"));
            if (size == "medium")
            {
                // Fill uses eight unique vertices; only the outline needs a repeated closing point.
                var polygon = Enumerable.Range(0, 8).Select(i => p + Vector2.FromAngle((alien ? Mathf.Pi / 4 : Mathf.Pi / 8) + Mathf.Tau * i / 8) * r).ToArray();
                Canvas.DrawColoredPolygon(polygon, fill);
                Canvas.DrawPolyline(polygon.Append(polygon[0]).ToArray(), color, 1.6f, true);
            }
            else
            {
                Canvas.DrawCircle(p, r, fill);
                Canvas.DrawArc(p, r, 0, Mathf.Tau, 48, color, 1.6f, true);
            }
            if (size == "large")
                Canvas.DrawArc(p, r + 5 * Math.Min(Zoom, 1), 0, Mathf.Tau, 48, UiTheme.Amber, 1.2f, true);
            if (id == active || required.Contains(id))
                Canvas.DrawArc(p, r + 9 * Math.Min(Zoom, 1), 0, Mathf.Tau, 48, UiTheme.Amber, 1.6f, true);
            if (id == TutorialHighlightId && !owned)
                DrawTutorialHighlight(p, r);
            string icon = node.S("icon", size == "small" ? SmallIcon(node) : new[] { "target", "missile", "laser", "mineral", "shield", "interceptor" }[BranchIndex(node)]);
            VectorIcons.Draw(Canvas, icon, p, size == "small" ? Math.Max(2.3f, r * .55f) : r * .55f, color, size == "small" ? Math.Clamp(r * .55f * .23f, .85f, 1.55f) : 1.5f);
            if (owned)
            {
                Vector2 badge = p + Vector2.One * r * .72f;
                Canvas.DrawCircle(badge, Math.Max(3, 6.5f * Math.Min(Zoom, 1)), new Color("112733"));
                Canvas.DrawPolyline(new[] { badge + new Vector2(-3, 0), badge + new Vector2(-1, 2), badge + new Vector2(4, -3) }, UiTheme.Mint, 1.5f, true);
            }
            if (_labelOffsets.TryGetValue(id, out var labelOffset))
                Canvas.DrawString(UiTheme.Font, p + labelOffset, UiTheme.Fit(node.S("name"), 140, 12), HorizontalAlignment.Left, 140, 12, UiTheme.Ink);
            else if (id == active)
                Canvas.DrawString(UiTheme.Font, p + new Vector2(-70, r + 18), UiTheme.Fit(node.S("name"), 140, 12), HorizontalAlignment.Center, 140, 12, UiTheme.Ink);
        }
        foreach (var (id, effect) in _pulses)
            if (Available(id))
            {
                float fraction = Math.Clamp(effect.Age / .65f, 0, 1);
                Vector2 at = Point(Node(id));
                Canvas.DrawArc(at, Radius(Node(id)) + fraction * 22, 0, Mathf.Tau, 32, UiTheme.Alpha(UiTheme.Mint, 1 - fraction), 2, true);
                Canvas.DrawString(UiTheme.Font, at + new Vector2(-72, -26 - fraction * 24), effect.Text, HorizontalAlignment.Center, 144, 11, UiTheme.Alpha(UiTheme.Mint, 1 - fraction));
            }
        Canvas.DrawRect(new Rect2(0, Canvas.Size.Y - 31, Canvas.Size.X, 31), new Color("0b1c27"));
        Canvas.DrawString(UiTheme.Font, new Vector2(12, Canvas.Size.Y - 11), Notice != "" ? Notice : "拖拽浏览 · 悬停详情 · 单击研究 · 每节点仅一次", HorizontalAlignment.Left, Canvas.Size.X - 100, 12, UiTheme.Muted);
        Canvas.DrawString(UiTheme.Font, new Vector2(Canvas.Size.X - 62, Canvas.Size.Y - 11), $"{Zoom * 100:0}%", HorizontalAlignment.Right, 50, 12, UiTheme.Mint);
    }

    private void DrawTutorialHighlight(Vector2 position, float radius)
    {
        // A slow, continuously visible warning pulse leaves the actual technology icon clear.
        float pulse = .5f + .5f * Mathf.Cos(_tutorialHighlightTime * Mathf.Tau / 1.4f);
        Color warning = new("ff6873");
        float ring = radius + 12;
        Canvas.DrawArc(position, radius + 2, 0, Mathf.Tau, 64, UiTheme.Alpha(warning, .60f + .35f * pulse), 2.5f, true);
        Canvas.DrawArc(position, ring + pulse * 3, 0, Mathf.Tau, 64, UiTheme.Alpha(warning, .16f + .19f * pulse), 5, true);
        Canvas.DrawArc(position, ring, 0, Mathf.Tau, 64, UiTheme.Alpha(warning, .40f + .30f * pulse), 1.2f, true);
        float cornerRadius = ring + 7;
        for (int i = 0; i < 4; i++)
        {
            float angle = Mathf.Pi * .25f + i * Mathf.Pi * .5f;
            Canvas.DrawArc(position, cornerRadius, angle - .19f, angle + .19f, 8,
                UiTheme.Alpha(warning, .68f + .32f * pulse), 2.8f, true);
        }
        Vector2 badge = position + Vector2.Up * (radius + 24);
        Canvas.DrawCircle(badge, 7, new Color("291b27"));
        Canvas.DrawArc(badge, 7, 0, Mathf.Tau, 24, UiTheme.Alpha(warning, .68f + .32f * pulse), 1.4f, true);
        Canvas.DrawLine(badge + Vector2.Up * 3.6f, badge + Vector2.Up * .1f, warning, 1.7f, true);
        Canvas.DrawCircle(badge + Vector2.Down * 2.7f, 1, warning);
    }

    private void NodeKeyboard(InputEvent e, string id)
    {
        if (e is not InputEventKey key || !key.Pressed)
            return;
        if (key.Keycode == Key.Home)
        {
            FitView();
            AcceptEvent();
            return;
        }
        if (key.Keycode is Key.Plus or Key.Equal or Key.Minus)
        {
            ZoomAt(key.Keycode == Key.Minus ? 1 / 1.12f : 1.12f, Canvas.Size * .5f);
            AcceptEvent();
            return;
        }
        Vector2 direction = key.Keycode switch
        {
            Key.Left => Vector2.Left,
            Key.Right => Vector2.Right,
            Key.Up => Vector2.Up,
            Key.Down => Vector2.Down,
            _ => Vector2.Zero
        };
        if (direction == Vector2.Zero)
            return;
        Vector2 origin = WorldPosition(Node(id));
        float best = float.PositiveInfinity;
        string next = "";
        foreach (var node in Nodes.Where(VisibleNode))
        {
            var delta = WorldPosition(node) - origin;
            if (delta.Dot(direction) <= 0)
                continue;
            float score = delta.Length() + Math.Abs(delta.Cross(direction)) * 2;
            if (score < best)
            {
                best = score;
                next = node.S("id");
            }
        }
        if (next != "")
        {
            EnsureVisible(next);
            NodeButtons[next].GrabFocus();
        }
        AcceptEvent();
    }
}

