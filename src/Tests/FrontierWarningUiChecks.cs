using Godot;
using Earthward.Application;
using Earthward.Domain;
using Earthward.Combat;
using Earthward.Presentation;

namespace Earthward.Tests;

public partial class FrontierWarningUiChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool result, string label)
    {
        _checks++;
        if (!result) { _failures++; GD.PrintErr("FRONTIER_WARNING_FAIL: " + label); }
    }
    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Refresh() => await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
    private static DataMap Fixture(int stage, double remaining, double duration = 45, bool pending = true) => new()
    {
        ["enabled"] = true,
        ["frontier_warning"] = new DataMap
        {
            ["pending"] = pending, ["next_stage"] = stage, ["target_radius"] = stage switch { 1 => 56d, 2 => 88d, _ => 128d },
            ["remaining"] = remaining, ["window_duration"] = duration, ["transition_id"] = $"frontier:{stage}:1"
        }
    };
    private void RestoreCeasefire(double remaining)
    {
        var snapshot = _app.Campaign.Serialize();
        CombatSnapshotCodec.TryDecode(snapshot.Value("payload"), out var raw);
        var payload = (DataMap)raw!;
        payload["_earth_phase"] = "ceasefire";
        payload["_earth_timer"] = remaining;
        snapshot["payload"] = CombatSnapshotCodec.Encode(payload);
        Check(_app.Campaign.Restore(snapshot), "restore real director inside/outside warning window");
    }
    private async Task Capture(string name)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(80, 220), GlobalPosition = new Vector2(80, 220) });
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "native screenshot " + name);
    }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Warning tests need isolated runtime-tests profile");
            foreach (int stage in new[] { 1, 2, 3 })
            {
                foreach (double seconds in new[] { .001, 1, 23.4, 45 })
                {
                    var warning = FrontierWarning.FromStatus(Fixture(stage, seconds), 16);
                    Check(warning != null && warning.NextStage == stage && warning.Remaining == seconds, "all three new positions warn inside final cycle");
                    Check(warning != null && warning.Altitude == warning.Radius - 16, "warning altitude is measured above current Earth radius");
                }
                foreach (double seconds in new[] { 45.1, 90, 0, -1, double.NaN, double.PositiveInfinity })
                    Check(FrontierWarning.FromStatus(Fixture(stage, seconds), 16) == null, "no early expired or invalid countdown warning");
                Check(FrontierWarning.FromStatus(Fixture(stage, 4, 12), 16) != null && FrontierWarning.FromStatus(Fixture(stage, 13, 12), 16) == null, "configured wave window replaces default timing");
                Check(FrontierWarning.FromStatus(Fixture(stage, 2, 45, false), 16) == null, "non-relocating reinforcement remains quiet");
            }
            foreach (int stage in new[] { 0, 4, -1 }) Check(FrontierWarning.FromStatus(Fixture(stage, 4), 16) == null, "invalid frontier stage is rejected");
            Check(FrontierWarning.FromStatus(new DataMap(), 16) == null, "ordinary defense has no relocation notice");
            var disabled = Fixture(1, 4); disabled["enabled"] = false;
            Check(FrontierWarning.FromStatus(disabled, 16) == null, "disabled campaign remains quiet");

            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main(); AddChild(_app); _app.UserPaused = true; _app.PreserveCheckpoint = true;
            await Frames(6);
            string camera = _app.Planet.CaptureCameraState().ToJson();
            _app.Game.Expedition.SetEarthLiberated(true);
            _app.Campaign.SyncCampaign();
            double period = _app.Game.CombatSettings.N("enemy_wave_duration");
            RestoreCeasefire(period + 1);
            await Refresh();
            Check(_app.CurrentFrontierWarning == null, "real initial five-cycle ceasefire does not warn more than one cycle early");
            RestoreCeasefire(Math.Min(23, period));
            await Refresh();
            Check(_app.CurrentFrontierWarning?.NextStage == 1 && _app.FrontierWarningRect.Size.X > 200, "loaded actual director clock shows native first-frontier HUD");
            double remaining = _app.Campaign.GetStatus().Map("frontier_warning").N("remaining");
            await Refresh();
            Check(_app.Campaign.GetStatus().Map("frontier_warning").N("remaining") == remaining, "game pause freezes relocation countdown");
            _app.Campaign.Paused = false; _app.Campaign.SpeedScale = 2; _app.Campaign.Step(.5); _app.Campaign.Paused = true;
            Check(Math.Abs(_app.Campaign.GetStatus().Map("frontier_warning").N("remaining") - remaining + 1) < .00001, "double speed advances countdown by actual simulation time");
            await Capture("frontier-warning-global");
            var rect = _app.FrontierWarningRect;
            Check(rect.Position.Y >= 84 && rect.End.X < _app.WorldSize.X - 300, "notice avoids resource header pause badge and command sidebar");
            Check(_app.Planet.CaptureCameraState().ToJson() == camera, "warning never changes strategic camera");
            _app.Action("tab:tech");
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            await Capture("frontier-warning-research");
            Check(!_app.FrontierWarningRect.Intersects(_app.ResearchGraph.GetGlobalRect()), "warning avoids expanded research sidebar");
            _app.UpdateTacticalAlerts(TacticalAlertView.AlertLifetime + .01);
            Check(_app.CurrentFrontierWarning != null && !_app.TacticalAlerts.Mother.Active && _app.FrontierWarningRect.Size == Vector2.Zero, "advance warning has a fixed eight-second card lifetime independent of paused arrival countdown");
            _app.UpdateTacticalAlerts(.3);
            Check(!_app.TacticalAlerts.Mother.Active, "same transition does not repeatedly announce while countdown remains pending");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout); }
        GD.Print($"FRONTIER_WARNING_UI_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
