using Godot;
using Earthward.Application;
using Earthward.Domain;
using Earthward.Rendering;

namespace Earthward.Tests;

/// <summary>Real Main input, snapshot refresh and cancellation in an isolated profile.</summary>
public partial class FactoryCoverageUiChecks : Node
{
    private Main _app = null!;
    private int _checks;
    private readonly List<string> _failures = new();

    private void Check(bool value, string label)
    {
        _checks++;
        if (value) return;
        _failures.Add(label);
        GD.PrintErr("COVERAGE_UI_FAIL: " + label);
    }

    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Move(Vector2 point)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        await Frames();
    }

    private async Task Click(Vector2 point, MouseButton button = MouseButton.Left)
    {
        await Move(point);
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true, ButtonMask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right });
        await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = false });
        await Frames();
    }

    private async Task Escape()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = false });
        await Frames();
    }

    private bool Cleared => _app.SelectedCoverageSiteId == -1 && _app.CurrentFactoryCoverage == null && !_app.Planet.IsFactoryCoverageVisible;

    private async Task Select()
    {
        await Click(_app.Planet.GetSlotScreenPosition(3));
        Check(_app.SelectedCoverageSiteId == 3 && _app.CurrentFactoryCoverage != null, "native factory click selects capability snapshot");
    }

    private async Task Capture(string name)
    {
        await Move(new Vector2(84, 222));
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "capture " + name);
        var rect = _app.FactoryCoverageHudRect;
        Check(rect.Size.X > 200 && rect.Position.X >= 0 && rect.End.X <= _app.WorldSize.X && rect.Position.Y >= 0 && rect.End.Y <= _app.WorldSize.Y, "floating coverage HUD remains inside viewport");
        if (rect.Size.X > 0)
        {
            using var crop = image.GetRegion(new Rect2I((Vector2I)rect.Position, (Vector2I)rect.Size));
            crop.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + "-hud.png"));
        }
    }

    private void InstallFrontFixture()
    {
        // Only this fresh test profile changes placement. Production saves are never opened.
        _app.Planet.RestoreSites(new object?[] { "mine", "solar", "", "interceptor" }, new object?[]
        {
            new Vector3(-.24f, .18f, 1).Normalized(),
            new Vector3(-.24f, -.18f, 1).Normalized(),
            new Vector3(.22f, .18f, 1).Normalized(),
            Vector3.Back
        });
        _app.Planet.ResetCamera();
        _app.Planet.Paused = true;
        _app.Battle.SyncFleet();
    }

    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Factory coverage UI checks require an isolated runtime-tests profile");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main();
            AddChild(_app);
            _app.UserPaused = true;
            _app.PreserveCheckpoint = true;
            await Frames(6);
            InstallFrontFixture();
            await Frames(4);
            Check(Math.Abs(_app.Game.CombatSettings.N("patrol_coverage_multiplier") - 2) < .000001, "default patrol multiplier is two");
            string camera = _app.Planet.CaptureCameraState().ToJson();
            string tab = _app.Tab;
            bool dock = _app.DockOpen;
            var view = _app.Planet.GetViewSize();
            await Select();
            var snapshot = _app.CurrentFactoryCoverage!;
            Check(snapshot.Preview && snapshot.AircraftCount == 0 && snapshot.Bands.Count > 0, "empty live roster presents production preview");
            Check(_app.Planet.CoverageSiteId == 3 && _app.Planet.IsFactoryCoverageVisible, "Main supplies selected snapshot to world shader");
            Check(_app.Planet.CaptureCameraState().ToJson() == camera && _app.Planet.GetViewSize() == view, "selecting factory preserves camera and world viewport");
            Check(_app.Tab == tab && _app.DockOpen == dock, "selecting factory preserves sidebar state");
            await Capture("factory-coverage-global");
            double previousPatrol = snapshot.Bands[0].PatrolRadius, previousWeapon = snapshot.Bands[0].WeaponRange;

            _app.SetProcess(false);
            _app.SelectFactoryCoverage(3);
            long queries = _app.FactoryCoverageQueryCount;
            for (int i = 0; i < 24; i++) _app._Process(.01);
            Check(_app.FactoryCoverageQueryCount == queries, "coverage query throttles sub-quarter-second frames");
            _app._Process(.02);
            Check(_app.FactoryCoverageQueryCount == queries + 1, "coverage refreshes after quarter second");
            _app.ClearFactoryCoverage();
            queries = _app.FactoryCoverageQueryCount;
            for (int i = 0; i < 30; i++) _app._Process(.02);
            Check(_app.FactoryCoverageQueryCount == queries, "unselected frames do not query aircraft coverage");
            _app.SetProcess(true);
            await Select();

            _app.OpenCombatSettings();
            Check(_app.CombatFields.Count == Main.CombatKeys.Length && _app.CombatFields.ContainsKey("patrol_coverage_multiplier"), "settings includes new native editable field");
            var field = _app.CombatFields["patrol_coverage_multiplier"];
            Check(field.Visible && field.Text == "2", "patrol multiplier is visible in basic settings with current value");
            field.Text = "0.1";
            Check(!_app.ApplyCombatSettings() && _app.Game.CombatSettings.N("patrol_coverage_multiplier") == 2, "out of bounds multiplier is rejected atomically");
            field.Text = "3";
            Check(_app.ApplyCombatSettings(), "valid multiplier applies through actual settings UI");
            _app.CloseCombatSettings();
            await ToSignal(GetTree().CreateTimer(.32), SceneTreeTimer.SignalName.Timeout);
            Check(_app.CurrentFactoryCoverage!.Bands[0].PatrolRadius > previousPatrol, "selected factory updates live patrol range after setting changes");
            Check(_app.CurrentFactoryCoverage.Bands[0].WeaponRange == previousWeapon, "patrol multiplier does not invent weapon range");
            _app.Game.SetCombatSetting("patrol_coverage_multiplier", 2);
            await ToSignal(GetTree().CreateTimer(.32), SceneTreeTimer.SignalName.Timeout);

            float distance = _app.Planet.GetCameraDistance();
            _app.Planet.ZoomCamera((42 - distance) / Math.Max(1, distance / 24));
            await Capture("factory-coverage-close");
            camera = _app.Planet.CaptureCameraState().ToJson();
            await Click(_app.FactoryCoverageHudRect.Position + new Vector2(95, 52));
            Check(_app.SelectedCoverageSiteId == 3 && _app.Planet.CaptureCameraState().ToJson() == camera, "HUD consumes pointer without selecting ground or moving camera");
            var close = _app.Buttons.First(button => button.S("action") == "coverage:close").Get<Rect2>("rect");
            await Click(close.GetCenter());
            Check(Cleared, "floating close button clears snapshot and rings");
            await Select();
            await Escape();
            Check(Cleared && _app.UserPaused, "Escape clears selection without toggling pause");
            await Select();
            await Click(_app.FactoryCoverageHudRect.GetCenter(), MouseButton.Right);
            Check(Cleared, "right click over HUD clears selection");
            await Select();
            await Click(new Vector2(100, 220));
            Check(Cleared, "empty space click clears selection");
            await Select();
            Vector2 ground = _app.Planet.GetSlotScreenPosition(3) + new Vector2(-100, 120);
            Check(_app.Planet.ScreenToSurface(ground).LengthSquared() > .5f && _app.Planet.PickExistingSite(ground) < 0, "fixture has empty visible ground");
            await Click(ground);
            Check(Cleared, "empty ground click clears selection");
            await Select();
            await Click(_app.Planet.GetSlotScreenPosition(0));
            Check(Cleared, "resource facility click clears aircraft coverage");
            await Select();
            _app.ChooseBuild("mine");
            Check(Cleared, "entering continuous building clears selection");
            await Click(new Vector2(100, 220), MouseButton.Right);
            await Select();
            _app.Action("tool:resource_upgrade");
            Check(Cleared, "resource upgrade tool clears selection");
            _app.Action("tool:resource_upgrade");

            _app.Started = true;
            _app.Battle.Active = true;
            _app.Battle.Paused = false;
            for (int i = 0; i < 600; i++) _app.Battle.Step(1d / 60);
            _app.Battle.Paused = true;
            _app.SyncRender();
            await Select();
            Check(!_app.CurrentFactoryCoverage!.Preview && _app.CurrentFactoryCoverage.AircraftCount > 0, "real factory production switches preview to live roster coverage");
            _app.Action("tab:perks");
            await Frames(4);
            await Select();
            Check(_app.Tab == "perks" && _app.FactoryPerkSidebar.SelectedSiteId == 3, "factory click also selects exact factory in existing perks sidebar");
            _app.Action("spectator:toggle");
            await Frames(4);
            Check(_app.IsSpectating() && Cleared, "entering actual aircraft spectator clears factory coverage");
            _app.ExitSpectator();
            _app.Action("tab:build");
            await Frames(4);
            await Select();
            _app.Action("body:moon");
            Check(Cleared, "other planet focus clears factory selection");
            _app.Planet.FocusBody("earth", true);
            await Frames(4);
            await Select();
            Check(_app.Restart() && Cleared, "new run clears transient factory HUD and overlay");
        }
        catch (Exception error)
        {
            Check(false, error.ToString());
        }
        if (IsInstanceValid(_app))
        {
            _app.QueueFree();
            await Frames(4);
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
        }
        GD.Print($"FACTORY_COVERAGE_UI_CHECKS: {_checks} checks / {_failures.Count} failures");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}
