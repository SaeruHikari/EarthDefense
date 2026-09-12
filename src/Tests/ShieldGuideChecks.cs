using Godot;
using System.Reflection;
using Earthward.Application;
using Earthward.Combat;
using Earthward.Domain;

namespace Earthward.Tests;

/// <summary>Runs the first-impact tutorial through actual combat, input and rendering paths.</summary>
public partial class ShieldGuideChecks : Node
{
    private const string ShieldResearch = "D_N4";
    private Main _app = null!;
    private int _checks, _failures;

    private void Check(bool okay, string label)
    {
        _checks++;
        if (okay) return;
        _failures++;
        GD.PrintErr("SHIELD_GUIDE_FAIL: " + label);
    }

    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Wait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task Click(Vector2 point)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left, Pressed = true
        });
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false
        });
        await Frames(3);
    }

    private async Task Space()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
        await Frames(3);
    }

    private void DamageEarth(Vector3 worldPosition, double amount = 2)
    {
        var method = typeof(Battlefield).GetMethod("DamageEarth", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(Battlefield), "DamageEarth");
        method.Invoke(_app.Battle, [amount, worldPosition, true]);
    }

    private async Task<byte[]> Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok,
            "capture " + name);
        var graph = _app.ResearchGraph;
        Vector2 center = graph.Canvas.GlobalPosition + graph.Point(graph.Node(ShieldResearch));
        Vector2 scale = new Vector2(image.GetWidth(), image.GetHeight()) / GetViewport().GetVisibleRect().Size;
        Vector2I topLeft = (Vector2I)((center - Vector2.One * 52) * scale);
        Vector2I dimensions = (Vector2I)(Vector2.One * 104 * scale);
        var region = new Rect2I(topLeft, dimensions).Intersection(new Rect2I(0, 0, image.GetWidth(), image.GetHeight()));
        using var nodeImage = image.GetRegion(region);
        return nodeImage.GetData();
    }

    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Shield guide checks require an isolated runtime-tests profile.");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main();
            AddChild(_app);
            _app.PreserveCheckpoint = true;
            await Frames(8);
            Check(_app.Restart(), "fixture resets current run through the real application path");
            _app.PreserveCheckpoint = true;
            _app.Action("tab:build");
            await Frames(5);
            Check(!_app.Game.HasResearch(ShieldResearch) && !_app.LocalShieldGuidePending,
                "fresh run has no shield research or inherited guide state");
            var definition = DeepTechnology.Definition(ShieldResearch);
            var status = _app.Game.GetGroupStatus(ShieldResearch);
            Check(definition.S("size") == "medium" && status.I("max") == 1,
                "local shield unlock is a one-level medium technology");
            Check(definition.List("requires").Count == 0 && status.B("can_purchase"),
                "local shield unlock is available without prerequisite research");
            Check(status.Map("cost").Values.All(value => Convert.ToDouble(value) == 0),
                "local shield research costs zero across every listed resource");

            _app.StartWave();
            _app.PreserveCheckpoint = true;
            Check(_app.Started && !_app.UserPaused, "first wave is running before real impact");
            float fov = _app.Planet.Camera.Fov;
            var viewSize = _app.Planet.GetViewSize();
            var renderSize = _app.Planet.GetRenderSize();
            var appWorldSize = _app.WorldSize;
            Vector3 worldImpact = -_app.Planet.Camera.GlobalPosition.Normalized() * WorldScale.EarthRadius;
            Vector3 localImpact = _app.Planet.Globe.ToLocal(worldImpact).Normalized();
            double hp = _app.Game.EarthHp;
            DamageEarth(worldImpact);
            Check(Math.Abs(_app.Game.EarthHp - (hp - 2)) < .00001,
                "real combat impact applies actual Earth health damage");
            Check(_app.UserPaused && _app.Battle.Paused && _app.Campaign.Paused && _app.Planet.Paused,
                "first impact synchronously pauses all simulation owners");
            double combatClock = _app.Battle.Clock;
            Transform3D earthTransform = _app.Planet.Globe.GlobalTransform;
            await Frames(6);
            var graph = _app.ResearchGraph;
            Check(_app.LocalShieldGuidePending && _app.ResearchSidebarOpen() && graph.Visible,
                "first impact automatically opens the research sidebar tutorial");
            Check(graph.SelectedId == ShieldResearch && graph.TutorialHighlightId == ShieldResearch,
                "tutorial focuses and red-highlights the actual local-shield node");
            Check(graph.Zoom >= .85f, "tutorial node has readable zoom");
            Check(new Rect2(new Vector2(60, 60), graph.Canvas.Size - new Vector2(120, 120))
                .HasPoint(graph.Point(graph.Node(ShieldResearch))), "tutorial node is safely inside its canvas");
            Check(graph.NodeButtons[ShieldResearch].HasFocus(), "tutorial node receives keyboard focus");
            await Wait(1.6);
            Vector3 focusedWorldPoint = _app.Planet.Globe.ToGlobal(localImpact * WorldScale.EarthRadius);
            Vector2 screenPoint = _app.Planet.RenderToLogical(_app.Planet.Camera.UnprojectPosition(focusedWorldPoint));
            var visibleEarth = new Rect2(18, 87, _app.ResearchTargetRect().Position.X - 38, _app.WorldSize.Y - 172);
            Check(!_app.Planet.Camera.IsPositionBehind(focusedWorldPoint) && visibleEarth.HasPoint(screenPoint),
                "backside attack is revealed in the unobscured area left of the research sidebar");
            Check((_app.Planet.Camera.GlobalPosition - focusedWorldPoint).Dot(focusedWorldPoint.Normalized()) > 0,
                "focused impact lies on the visible side of Earth");
            Check(_app.Planet.Camera.Fov == fov && _app.Planet.GetViewSize() == viewSize
                && _app.Planet.GetRenderSize() == renderSize && _app.WorldSize == appWorldSize,
                "research tutorial preserves lens and full game viewport dimensions");
            Check(_app.Battle.Clock == combatClock && _app.Planet.Globe.GlobalTransform.IsEqualApprox(earthTransform),
                "combat clock and Earth transform stay frozen throughout focus animation");
            Check(graph.IsProcessing(), "red warning keeps UI animation processing while combat is paused");
            byte[] pulseA = await Capture("shield-guide-focus");
            await Wait(.58);
            byte[] pulseB = await Capture("shield-guide-focus-pulse");
            Check(!pulseA.SequenceEqual(pulseB), "rendered highlighted-node region changes between pulse phases");
            Check(_app.Battle.Clock == combatClock && _app.Planet.Globe.GlobalTransform.IsEqualApprox(earthTransform),
                "UI pulse advances without advancing paused combat or Earth rotation");

            await Space();
            Check(!_app.UserPaused && !_app.Battle.Paused && !_app.Campaign.Paused && !_app.Planet.Paused,
                "Space explicitly resumes all simulation owners while the node is focused");
            Check(!_app.Game.HasResearch(ShieldResearch), "Space does not accidentally purchase the focused technology");
            graph.FocusNode("K_S01", center: true);
            await Frames(2);
            Transform3D cameraBeforeRepeat = _app.Planet.Camera.GlobalTransform;
            Vector2 graphPan = graph.Pan;
            DamageEarth(_app.Planet.Camera.GlobalPosition.Normalized() * WorldScale.EarthRadius);
            await Frames(4);
            Check(!_app.UserPaused && !_app.Battle.Paused,
                "later impacts do not force the player back into pause");
            Check(graph.SelectedId == "K_S01" && graph.Pan.IsEqualApprox(graphPan)
                && graph.NodeButtons["K_S01"].HasFocus(), "later impacts do not steal the player's research focus");
            Check(_app.Planet.Camera.GlobalTransform.IsEqualApprox(cameraBeforeRepeat),
                "later impacts do not force another camera transition");

            _app.UserPaused = true;
            await Frames(3);
            _app.Game.Minerals = _app.Game.Energy = _app.Game.Science = 0;
            _app.Game.AlienPoints = _app.Game.ResourceCores = 0;
            _app.FocusResearchWindow(ShieldResearch);
            graph.FocusNode(ShieldResearch, center: true);
            await Frames(3);
            await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node(ShieldResearch)));
            Check(_app.Game.HasResearch(ShieldResearch) && _app.Game.GetGroupStatus(ShieldResearch).I("level") == 1,
                "one real mouse click purchases the one-level medium shield technology");
            Check(_app.Game.Minerals == 0 && _app.Game.Energy == 0 && _app.Game.Science == 0
                && _app.Game.AlienPoints == 0 && _app.Game.ResourceCores == 0,
                "real UI purchase succeeds at zero resources and consumes nothing");
            Check(!_app.LocalShieldGuidePending && graph.TutorialHighlightId == "",
                "successful research clears the tutorial highlight");
            Check(_app.UserPaused && _app.Battle.Paused && _app.Planet.Paused,
                "research completion preserves the player's existing pause");
            Check(_app.Game.BuildingUnlocked("shield"), "research enables the local shield building");
            await Space();
            Check(!_app.UserPaused && _app.Game.GetGroupStatus(ShieldResearch).I("level") == 1,
                "Space after completion resumes without repeat research");

            string priorRun = _app.Game.RunId;
            Check(_app.Restart(), "real Restart succeeds after completing the guide");
            _app.PreserveCheckpoint = true;
            await Frames(5);
            Check(_app.Game.RunId != priorRun && !_app.LocalShieldGuidePending && graph.TutorialHighlightId == ""
                && !_app.Game.HasResearch(ShieldResearch), "Restart clears guide and research state for the new run");
            _app.StartWave();
            _app.PreserveCheckpoint = true;
            DamageEarth(_app.Planet.Camera.GlobalPosition.Normalized() * WorldScale.EarthRadius);
            await Frames(5);
            Check(_app.LocalShieldGuidePending && _app.UserPaused && graph.TutorialHighlightId == ShieldResearch,
                "first impact in the new run can trigger its own guide again");

            Check(_app.PauseAndFocusUi("工程参数引导测试",
                revealUi: () => _app.OpenCombatSettings(),
                focusUi: () => _app.CombatFields["enemy_health"].GrabFocus()),
                "shared paused-focus entry accepts a non-research UI target");
            await Frames(5);
            Check(_app.Modal == "combat" && _app.CombatFields["enemy_health"].HasFocus()
                && _app.UserPaused && _app.Battle.Paused,
                "shared entry opens and focuses a real parameter input without resuming combat");
            CheckSurfaceCameraAnimation();
        }
        catch (Exception error)
        {
            Check(false, error.ToString());
        }
        if (IsInstanceValid(_app))
        {
            _app.PreserveCheckpoint = true;
            _app.QueueFree();
            await Frames(4);
        }
        GD.Print($"SHIELD_GUIDE_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void CheckSurfaceCameraAnimation()
    {
        _app.SetProcess(false);
        var planet = _app.Planet;
        planet.SetProcess(false);
        planet.Paused = true;
        planet.ResetCamera();
        Vector3 start = planet.Camera.GlobalPosition;
        Vector3 target = planet.Globe.GlobalBasis.Inverse() * Vector3.Forward;
        planet.RotateEarthToDirection(target, .25f, 1.4);
        Check(planet.IsCameraTransitionActive && planet.Camera.GlobalPosition.IsEqualApprox(start),
            "surface focus begins at the current camera pose without teleporting");
        bool safe = true, finite = true;
        for (int step = 0; step < 16; step++)
        {
            planet._Process(.1);
            safe &= planet.Camera.GlobalPosition.Length() > WorldScale.EarthRadius + 1;
            finite &= planet.Camera.GlobalPosition.IsFinite() && planet.Camera.GlobalBasis.X.IsFinite();
            if (step == 5)
                Check(planet.IsCameraTransitionActive && planet.Camera.GlobalPosition.DistanceTo(start) > 1,
                    "backside focus has an intermediate animated pose");
        }
        Check(safe && finite && !planet.IsCameraTransitionActive,
            "entire backside camera arc remains finite and outside Earth before finishing");
        planet.RotateEarthToDirection(planet.Globe.GlobalBasis.Inverse() * Vector3.Up, .3f, 1.4);
        planet._Process(.3);
        Vector3 beforeInput = planet.Camera.GlobalPosition;
        planet.OrbitCamera(0);
        Check(!planet.IsCameraTransitionActive && planet.Camera.GlobalPosition.DistanceTo(beforeInput) < .001f,
            "manual camera input takes over mid-animation without a position jump");
    }
}
