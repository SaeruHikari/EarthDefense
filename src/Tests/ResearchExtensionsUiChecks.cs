using Godot;
using Earthward.Application;
using Earthward.Domain;

namespace Earthward.Tests;

public partial class ResearchExtensionsUiChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool value, string label) { _checks++; if (!value) { _failures++; GD.PrintErr("RESEARCH_EXTENSION_UI_FAIL: " + label); } }
    private async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Click(Vector2 point)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
    }
    private void Prepare(string id)
    {
        if (_app.Game.HasResearch(id)) return;
        foreach (string parent in DeepTechnology.Definition(id).List("requires").Cast<string>()) Prepare(parent);
        Check(_app.Game.PurchaseGroup(id), "research fixture purchases real prerequisite " + id);
    }
    private async Task Capture(string name)
    {
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "capture " + name);
    }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Research UI needs isolated profile");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main(); AddChild(_app); _app.UserPaused = true; _app.PreserveCheckpoint = true;
            await Frames(6);
            var shieldCard = _app.Buttons.First(row => row.S("action") == "build:shield").Get<Rect2>("rect");
            Check(!_app.Game.BuildingUnlocked("shield"), "shield tower initially has research lock");
            await Click(shieldCard.GetCenter());
            Check(_app.SelectedBuild != "shield", "locked shield card explains research instead of entering build mode");
            _app.Game.Minerals = _app.Game.Energy = _app.Game.Science = 1e25;
            _app.Game.AlienPoints = 1000000000000; _app.Game.ResourceCores = 1000;
            _app.Game.Wave = 100; _app.Game.CompletedWaves = 100;
            _app.Game.RewardKill("boss"); _app.Game.SetDefenseReachStage(3);
            Prepare("D_N4"); Prepare("M_N1");
            foreach (string id in new[] { "M_S21", "M_S22", "M_S23", "M_N4" })
                foreach (string parent in DeepTechnology.Definition(id).List("requires").Cast<string>()) Prepare(parent);
            _app.Action("tab:tech");
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            var graph = _app.ResearchGraph;
            Check(graph.Nodes.Count(node => !node.B("is_successor")) == 300, "full static tree includes deeper branches, capacity and shield unlock");
            foreach (var (id, icon) in new[] { ("M_S21", "blast"), ("M_S22", "range"), ("M_S23", "speed") })
            {
                var node = graph.Node(id);
                Check(graph.SmallIcon(node) == icon, "new small node has its actual attribute icon");
                float nearest = graph.Nodes.Where(other => other.S("id") != id).Min(other => graph.WorldPosition(node).DistanceTo(graph.WorldPosition(other)));
                Check(nearest >= 38, "missile extension remains separated in compact branch layout");
                graph.Zoom = 1.15f; graph.Pan = -graph.WorldPosition(node) * graph.Zoom; graph.PositionButtons(); await Frames();
                await Click(graph.Canvas.GlobalPosition + graph.Point(node));
                Check(_app.Game.HasResearch(id), "native single click researches small missile extension");
            }
            double fundedScience = _app.Game.Science;
            _app.Game.Science = 0;
            _app.FocusResearchWindow("M_N4"); await Frames();
            await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node("M_N4")));
            Check(!_app.Game.HasResearch("M_N4") && _app.Game.Science == 0, "unaffordable direct click never buys or spends resources");
            _app.Game.Science = fundedScience;
            _app.FocusResearchWindow("M_N4"); await Frames();
            Vector2 dragStart = graph.Canvas.GlobalPosition + graph.Point(graph.Node("M_N4"));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = dragStart, GlobalPosition = dragStart }); await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { Position = dragStart, GlobalPosition = dragStart, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }); await Frames();
            Vector2 dragEnd = dragStart + new Vector2(34, 15);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = dragEnd, GlobalPosition = dragEnd, Relative = dragEnd - dragStart, ButtonMask = MouseButtonMask.Left }); await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { Position = dragEnd, GlobalPosition = dragEnd, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
            Check(!_app.Game.HasResearch("M_N4") && _app.Game.Science == fundedScience, "drag beginning on medium node pans without accidental purchase");
            graph.FocusNode("M_N4"); await Frames();
            Check(graph.Node("M_N4").B("available"), "new medium salvo node is ready for direct purchase");
            await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node(graph.SelectedId)));
            Check(_app.Game.HasResearch("M_N4"), "native medium confirm purchases extra missile tech");
            graph.FocusNode("M_N4"); await Frames();
            double science = _app.Game.Science;
            Check(graph.FindChildren("ResearchPurchaseCard", "", true, false).Count == 0, "secondary research panel is absent from scene tree");
            graph.ActivateNode("M_N4");
            Check(_app.Game.Science == science, "clicking completed medium never charges again");
            await Capture("missile-tech-extension-ui");

            foreach (string parent in DeepTechnology.Definition("K_G2").List("requires").Cast<string>()) Prepare(parent);
            _app.FocusResearchWindow("K_G2"); await Frames();
            await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node("K_G2")));
            Check(_app.Game.HasResearch("K_G2"), "native single click purchases large technology without second panel");
            _app.FocusResearchWindow("K_R00001"); await Frames();
            Check(graph.Node("K_R00001").B("is_successor"), "successor is its own visible named research node");
            await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node(graph.SelectedId)));
            Check(_app.Game.HasResearch("K_R00001") && _app.Game.SuccessorCount("K") == 1, "successor purchase advances exactly one independent node");
            graph.FocusNode("K_R00001"); await Frames();
            science = _app.Game.Science;
            graph.ActivateNode("K_R00001");
            Check(_app.Game.Science == science, "owned successor cannot be bought twice");
            for (int rank = 2; rank <= 12; rank++) Check(_app.Game.PurchaseGroup(DeepTechnology.SuccessorId("K", rank)), "fixture completes unique successor " + rank);
            _app.FocusResearchWindow("K_R00013"); await Frames();
            Check(!graph.Nodes.Any(node => node.S("id") == "K_R00002"), "latest window does not instantiate full chain history");
            graph.FocusNode("K_R00002"); await Frames();
            Check(graph.SelectedId == "K_R00002" && graph.Node("K_R00002").I("level") == 1, "focusing old stable ID requests real historical window");
            Check(graph.Nodes.Count(node => node.B("is_successor") && node.S("chain_branch") == "K") <= 6, "history window limits one branch to six real nodes");
            await Click(graph.LatestHistoryButton.GetGlobalRect().GetCenter());
            Check(graph.SelectedId == "K_R00013" && graph.Node("K_R00013").B("available"), "native latest button returns to next unpurchased successor");
            await Capture("single-level-successor-ui");
            _app.Action("tab:build"); await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            Check(_app.Game.BuildingUnlocked("shield"), "shield research unlocks building card");
            shieldCard = _app.Buttons.First(row => row.S("action") == "build:shield").Get<Rect2>("rect");
            await Click(shieldCard.GetCenter());
            Check(_app.SelectedBuild == "shield", "unlocked shield card selects continuous building mode");
            Vector2 ground = Vector2.Zero;
            for (int y = 280; y <= 680 && ground == Vector2.Zero; y += 75)
                for (int x = 440; x <= 990 && ground == Vector2.Zero; x += 75)
                {
                    var point = new Vector2(x, y);
                    if (_app.Planet.ScreenToSurface(point).LengthSquared() > .5 && _app.Planet.PickExistingSite(point) < 0 && !_app.IsOverUi(point)) ground = point;
                }
            Check(ground != Vector2.Zero, "shield placement has visible free hex cell");
            long count = _app.Game.Buildings.L("shield");
            await Click(ground);
            Check(_app.Game.Buildings.L("shield") == count + 1, "native ground click builds unlocked shield on actual hex cell");
            await Capture("shield-construction-ui");
            int shieldSite = _app.Planet.PickExistingSite(ground);
            _app.SelectedBuild = "";
            _app.Planet.PlacingBuilding = false;
            await Click(shieldSite >= 0 ? _app.Planet.GetSlotScreenPosition(shieldSite) : ground);
            Check(_app.SelectedShieldCoverageSiteId == shieldSite && _app.CurrentShieldCoverage != null, "clicking a shield tower selects its coverage card");
            Check(_app.Planet.IsShieldCoverageVisible && !_app.Planet.IsFactoryCoverageVisible, "shield selection switches the world projection to a cyan local dome");
            Check(_app.ShieldCoverageHudRect.Size.X > 240 && _app.ShieldCoverageHudRect.Size.Y > 175, "shield coverage card matches the floating factory HUD size");
            var shieldCoverage = _app.CurrentShieldCoverage;
            Check(shieldCoverage != null, "shield card snapshot is available after selection");
            if (shieldCoverage != null)
                Check(shieldCoverage.SurfaceRadius > 0 && shieldCoverage.Capacity > 0, "shield card exposes range and concrete capacity values");
            await Capture("shield-coverage-ui");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout); }
        GD.Print($"RESEARCH_EXTENSION_UI_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
