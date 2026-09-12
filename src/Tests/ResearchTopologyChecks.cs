using Godot;
using Earthward.Application;
using Earthward.Domain;
using Earthward.Presentation;

namespace Earthward.Tests;

/// <summary>Dependency completeness, layout geometry, historical boundaries and real graph input.</summary>
public partial class ResearchTopologyChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool value, string label)
    {
        _checks++;
        if (!value) { _failures++; GD.PrintErr("RESEARCH_TOPOLOGY_FAIL: " + label); }
    }
    private async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Click(Vector2 point)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
    }
    private async Task Capture(string name)
    {
        await ToSignal(GetTree().CreateTimer(1.1), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "capture " + name);
    }
    private static float NodeRadius(DataMap node) => node.S("size") == "small" ? 12 : node.S("size") == "large" ? 32 : 20;
    private void InspectStaticTree(ResearchGraphView graph)
    {
        var nodes = graph.Nodes.Where(node => !node.B("is_successor")).ToArray();
        Check(nodes.Length == 248 && nodes.Count(node => node.S("size") == "small") == 195, "248 static nodes include 195 independent small technologies");
        Check(nodes.All(node => node.B("visible") && graph.NodeButtons[node.S("id")].Visible), "every ordinary technology is browsable before unlock");
        Check(nodes.Any(node => !node.B("available") && node.S("lock_reason") != ""), "unlocked visibility preserves real research locks");
        int expected = nodes.Sum(node => ResearchGraphView.PrerequisiteIds(node).Length);
        Check(graph.Connections.Count == expected && expected == 287, "all 287 actual prerequisite links have exactly one connection");
        Check(graph.Connections.All(edge => edge.Kind == ResearchConnectionKind.Prerequisite), "complete ordinary catalog has no hidden or missing parent edges");
        bool exact = true, ordered = true;
        foreach (var node in nodes)
            foreach (string parent in ResearchGraphView.PrerequisiteIds(node))
            {
                exact &= graph.Connections.Count(edge => edge.ParentId == parent && edge.ChildId == node.S("id")) == 1;
                ordered &= node.N("layout_depth") > graph.Node(parent).N("layout_depth");
            }
        Check(exact, "all dependency IDs connect to their real parent rather than the root or another row");
        Check(ordered, "layout depth follows the actual dependency longest path");
        bool rings = true, singleSteps = true, deepestSteps = true, pairedRays = true;
        int singleCount = 0, multiCount = 0, pairCount = 0;
        foreach (var node in nodes)
        {
            int depth = node.I("layout_depth");
            rings &= Math.Abs(graph.WorldPosition(node).Length() - ResearchGraphView.ResearchRingRadius(depth)) < .002;
            var parents = ResearchGraphView.PrerequisiteIds(node);
            if (parents.Length == 1) { singleCount++; singleSteps &= depth == graph.Node(parents[0]).I("layout_depth") + 1; }
            if (parents.Length > 1) { multiCount++; deepestSteps &= depth == parents.Max(id => graph.Node(id).I("layout_depth")) + 1 && parents.All(id => graph.Node(id).I("layout_depth") < depth); }
            if (node.B("is_pair_second"))
            {
                pairCount++;
                var parent = graph.WorldPosition(graph.Node(parents.Single()));
                var child = graph.WorldPosition(node);
                pairedRays &= Math.Abs(parent.Normalized().Cross(child.Normalized())) < .00001f && Math.Abs(child.DistanceTo(parent) - ResearchGraphView.RingSpacing) < .002;
            }
        }
        Check(rings, "all 248 static technologies occupy their exact real prerequisite-depth ring");
        Check(singleSteps && singleCount == 187, "all 187 single-parent upgrades advance by exactly one ring");
        Check(deepestSteps && multiCount == 44, "all 44 multi-parent upgrades sit immediately outside their deepest prerequisite");
        Check(pairedRays && pairCount == 98, "all 98 marked adjacent pairs including shield repair remain on exactly the same ray");
        Check(graph.Connections.Count(graph.IsSecondaryConnection) == 54, "54 earlier or cross-branch prerequisite links remain present as secondary connections");
        bool separated = true;
        string closestPair = "";
        float closestGap = float.PositiveInfinity;
        for (int i = 0; i < nodes.Length; i++)
            for (int j = i + 1; j < nodes.Length; j++)
            {
                float gap = graph.WorldPosition(nodes[i]).DistanceTo(graph.WorldPosition(nodes[j])) - NodeRadius(nodes[i]) - NodeRadius(nodes[j]);
                if (gap < closestGap) { closestGap = gap; closestPair = nodes[i].S("id") + "/" + nodes[j].S("id"); }
                separated &= gap >= 6;
            }
        Check(separated, $"compact branches remain separated: minimum gap {closestGap:0.00} at {closestPair}");
        graph.Zoom = 1;
        graph.PositionButtons();
        bool ends = true;
        foreach (var edge in graph.Connections)
        {
            var geometry = graph.ConnectionEndpoints(edge);
            Vector2 parent = graph.Point(graph.Node(edge.ParentId)), child = graph.Point(graph.Node(edge.ChildId));
            ends &= Math.Abs(geometry.From.DistanceTo(parent) - graph.NodeBoundaryRadius(graph.Node(edge.ParentId), geometry.From - parent)) < .01;
            ends &= Math.Abs(geometry.To.DistanceTo(child) - graph.NodeBoundaryRadius(graph.Node(edge.ChildId), geometry.To - child)) < .01;
        }
        Check(ends, "connections terminate precisely at circle or octagon boundaries");
        var obstructed = graph.Connections.Where(edge => !graph.ConnectionAvoidsUnrelatedNodes(edge)).Select(edge => edge.ParentId + "->" + edge.ChildId).ToArray();
        Check(obstructed.Length == 0, "cached dependency routes avoid unrelated nodes: " + string.Join(",", obstructed));
        var positions = nodes.ToDictionary(node => node.S("id"), node => graph.WorldPosition(node));
        graph.Pan += new Vector2(77, -32); graph.Zoom = .8f; graph.PositionButtons();
        Check(nodes.All(node => graph.WorldPosition(node) == positions[node.S("id")]), "pan and zoom do not change stable dependency layout");
    }
    private async Task InspectZoomAndDrag(ResearchGraphView graph)
    {
        bool before = _app.Game.HasResearch("K_S02");
        double science = _app.Game.Science;
        foreach(float zoom in new[]{ .65f, 1.15f, 1.6f })
        {
            graph.Zoom = zoom;
            graph.Pan = -graph.WorldPosition(graph.Node("K_S02")) * zoom;
            graph.PositionButtons(); await Frames();
            Vector2 start = graph.Canvas.GlobalPosition + graph.Point(graph.Node("K_S02"));
            Input.ParseInputEvent(new InputEventMouseMotion { Position=start, GlobalPosition=start }); await Frames();
            Check(graph.HoverId == "K_S02", "native hover identifies the same adjacent node at zoom " + zoom);
            var pan = graph.Pan;
            Input.ParseInputEvent(new InputEventMouseButton { Position=start, GlobalPosition=start, ButtonIndex=MouseButton.Left, Pressed=true, ButtonMask=MouseButtonMask.Left }); await Frames();
            Vector2 finish=start+new Vector2(29,13);
            Input.ParseInputEvent(new InputEventMouseMotion { Position=finish, GlobalPosition=finish, Relative=finish-start, ButtonMask=MouseButtonMask.Left }); await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { Position=finish, GlobalPosition=finish, ButtonIndex=MouseButton.Left, Pressed=false }); await Frames();
            Check(graph.Pan.DistanceTo(pan)>20 && _app.Game.HasResearch("K_S02")==before && _app.Game.Science==science, "native drag pans without buying adjacent nodes at zoom " + zoom);
        }
    }

    private void InspectFallbackConnections(ResearchGraphView graph)
    {
        var saved = graph.Nodes.Select(node => node.DeepClone()).ToArray();
        DataMap Node(string id, Vector2 p, bool visible = true) => new() { ["id"] = id, ["branch"] = "K", ["size"] = "small", ["visible"] = visible, ["draw_position"] = p, ["requires"] = new List<object?>() };
        var parent = Node("K_S01", new Vector2(-120, 0)); parent["level"] = 1;
        var hidden = Node("K_S02", new Vector2(-120, 100), false);
        var child = Node("K_N1", new Vector2(140, 0));
        child["requires"] = new DataMap { ["K_S01"] = 1L, ["K_S02"] = 1L, ["K_missing"] = 1L };
        var history = Node("K_R00010", new Vector2(0, -400)); history["is_successor"] = true;
        history["requires"] = new List<object?> { "K_R00009", "K_R00009" };
        history["requirement_details"] = new List<object?> { new DataMap { ["id"] = "K_R00009", ["name"] = "Previous real research", ["met"] = true } };
        graph.SetNodes(new[] { parent, hidden, child, history });
        Check(ResearchGraphView.PrerequisiteIds(child).Length == 3 && ResearchGraphView.PrerequisiteIds(history).Length == 1, "list and map prerequisite shapes normalize without losing IDs or duplicating edges");
        Check(graph.Connections.Count == 4, "hidden and absent parents do not silently lose their dependencies");
        Check(graph.Connections.Count(edge => edge.Kind == ResearchConnectionKind.Prerequisite) == 1 && graph.Connections.Count(edge => edge.Kind == ResearchConnectionKind.HiddenPrerequisite) == 1 && graph.Connections.Count(edge => edge.Kind == ResearchConnectionKind.MissingPrerequisite) == 1, "hidden and invalid references have explicit distinct boundary markers");
        var stub = graph.Connections.Single(edge => edge.Kind == ResearchConnectionKind.HistoryBoundary);
        Check(stub.ParentId == "K_R00009" && stub.ChildId == "K_R00010" && stub.ParentMet && stub.Label.Contains("#9"), "historical stub identifies the actual completed predecessor");
        graph.SetNodes(saved);
    }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Research topology checks require an isolated profile");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main(); AddChild(_app); _app.UserPaused = true; _app.PreserveCheckpoint = true;
            await Frames(5);
            string camera = _app.Planet.CaptureCameraState().ToJson();
            _app.Action("tab:tech"); await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            var graph = _app.ResearchGraph;
            InspectStaticTree(graph);
            InspectFallbackConnections(graph);
            graph.FitView(); await Frames(4);
            Check(graph.Nodes.All(node => new Rect2(Vector2.Zero, graph.Canvas.Size).HasPoint(graph.Point(node))), "fit view includes the complete deeper ordinary tree");
            await Capture("tech127-overview");
            await InspectZoomAndDrag(graph);
            graph.Zoom = 1.1f; graph.Pan = -graph.WorldPosition(graph.Node("K_S01")) * graph.Zoom; graph.PositionButtons(); await Frames();
            await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node("K_S01")));
            Check(_app.Game.HasResearch("K_S01"), "native left click still researches exactly the selected ordinary node");
            double science = _app.Game.Science;
            await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node("K_S01")));
            Check(_app.Game.Science == science, "owned node click never repeats its cost");
            Check(_app.Planet.CaptureCameraState().ToJson() == camera, "technology browsing keeps strategic camera unchanged");
            Check(_app.Game.UnlockAllTechnologyCheat(), "fixture unlocks regular research without touching advances");
            _app.Game.Science = 1e12; _app.Game.AlienPoints = 1000000000;
            for (int rank = 1; rank <= 12; rank++) Check(_app.Game.PurchaseGroup(DeepTechnology.SuccessorId("K", rank)), "fixture buys real unique advanced node " + rank);
            _app.FocusResearchWindow("K_R00013"); await Frames();
            var external = graph.Connections.Single(edge => edge.Kind == ResearchConnectionKind.HistoryBoundary && edge.ChildId == "K_R00010");
            Check(external.ParentId == "K_R00009" && external.ParentMet, "live history window connects to its actual completed outside predecessor");
            var historyNodes=graph.Nodes.Where(node=>node.B("is_successor") && node.S("chain_branch")=="K").OrderBy(node=>node.I("chain_rank")).ToArray();
            Vector2 capstone=graph.WorldPosition(graph.Node("K_G2"));
            Check(historyNodes.Length<=6 && Math.Abs(graph.WorldPosition(historyNodes[0]).DistanceTo(capstone)-ResearchGraphView.RingSpacing)<.002, "history window starts immediately outside its own branch capstone");
            Check(historyNodes.Zip(historyNodes.Skip(1)).All(pair=>Math.Abs(graph.WorldPosition(pair.Second).DistanceTo(graph.WorldPosition(pair.First))-ResearchGraphView.RingSpacing)<.002), "every displayed historical successor is exactly one adjacent ring after its predecessor");
            graph.Zoom = .95f; graph.Pan = -graph.WorldPosition(graph.Node("K_R00010")) * graph.Zoom; graph.PositionButtons(); await Frames();
            await Capture("tech127-history-boundary");
            await Click(graph.Canvas.GlobalPosition + graph.BoundaryPoint(external));
            Check(graph.SelectedId == "K_R00009" && graph.Node("K_R00009").I("level") == 1, "native boundary marker opens the exact prior historical node");
            Check(graph.Nodes.Count(node => node.B("is_successor") && node.S("chain_branch") == "K") <= 6, "historical browsing retains a bounded six-node window");
            await Click(graph.LatestHistoryButton.GetGlobalRect().GetCenter());
            Check(graph.SelectedId == "K_R00013", "history latest navigation remains available after boundary jump");
            _app.FocusResearchWindow("I_N4"); await Frames();
            await Capture("tech127-capacity-branch");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout); }
        GD.Print($"RESEARCH_TOPOLOGY_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
