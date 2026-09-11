using Godot;
using Earthward.Application;
using Earthward.Presentation;

namespace Earthward.Tests;

/// <summary>Actual redraws retain immediate mouse controls while stable statistics and text layout are reused.</summary>
public partial class HudCacheChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool value, string label) { _checks++; if (!value) { _failures++; GD.PrintErr("HUD_CACHE_FAIL: " + label); } }
    private async Task Frames(int count = 2) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Draw() { _app.QueueRedraw(); await Frames(); }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Isolated profile required");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main(); AddChild(_app); _app.SetProcess(false); _app.Planet.SetProcess(false); _app.Sounds.Muted = true;
            _app.Modal = ""; _app.DockOpen = true; _app.Tab = "build"; _app.Elapsed = 10;
            _app.InvalidateHudData(); await Draw();
            long refreshes = _app.HeaderSnapshotRefreshCount, measures = _app.HudTextMeasureCount;
            var target = _app.Buttons.First(row => row.S("action") == "build:mine").Get<Rect2>("rect");
            for (int i = 0; i < 5; i++) { _app.Mouse = target.Position + new Vector2(4 + i, 5); await Draw(); }
            Check(_app.HeaderSnapshotRefreshCount == refreshes, "mouse hover redraws reuse unchanged statistics");
            Check(_app.HudTextMeasureCount == measures, "unchanged buttons and labels reuse original font metrics");
            Check(_app.Buttons.Any(row => row.S("action") == "build:mine" && row.Get<Rect2>("rect") == target), "every cached redraw retains live building hitboxes");

            for (int i = 0; i < 24; i++) { _app.Elapsed += 1d / 120; await Draw(); }
            long updates = _app.HeaderSnapshotRefreshCount - refreshes;
            Check(updates >= 2 && updates <= 3, "24 display frames over 200ms trigger only 2-3 fifteen-Hz snapshots");
            _app.Game.Minerals += 37; _app.Elapsed += .07; await Draw();
            Check(_app.HudDisplayedValue("minerals") == UiTheme.Number(_app.Game.Minerals), "next scheduled snapshot reflects current income");
            refreshes = _app.HeaderSnapshotRefreshCount;
            _app.Game.Wave++; await Draw();
            Check(_app.HeaderSnapshotRefreshCount > refreshes && _app.HudDisplayedValue("wave") == _app.Game.Wave.ToString("00"), "wave transition invalidates immediately without waiting for cadence");

            var camera = _app.Planet.Camera.GlobalTransform;
            _app.Action("build:mine"); await Draw();
            Check(_app.SelectedBuild == "mine" && _app.HeaderSnapshotRefreshCount > refreshes, "selection updates immediately while data cache is active");
            refreshes = _app.HeaderSnapshotRefreshCount;
            _app.Game.SetCombatSetting("patrol_coverage_multiplier", 3); await Draw();
            Check(_app.HeaderSnapshotRefreshCount > refreshes, "real parameter/stat revision invalidates cached values immediately");
            Check(_app.Planet.Camera.GlobalTransform.IsEqualApprox(camera), "HUD data cache never changes the camera");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true }); await Frames();
            var pause = _app.Buttons.First(row => row.S("action") == "pause").Get<Rect2>("rect").GetCenter();
            bool previousPause = _app.UserPaused;
            Input.ParseInputEvent(new InputEventMouseButton { Position = pause, GlobalPosition = pause, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }); await Frames();
            Input.ParseInputEvent(new InputEventMouseButton { Position = pause, GlobalPosition = pause, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
            Check(_app.UserPaused != previousPause, "native pause click remains responsive with cached HUD");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout); }
        GD.Print($"HUD_CACHE_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
