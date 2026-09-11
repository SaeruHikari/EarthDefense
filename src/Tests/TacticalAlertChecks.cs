using Godot;
using System.Reflection;
using Earthward.Application;
using Earthward.Combat;
using Earthward.Domain;
using Earthward.Presentation;
namespace Earthward.Tests;

public partial class TacticalAlertChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool okay, string label)
    {
        _checks++;
        if (!okay) { _failures++; GD.PrintErr("TACTICAL_ALERT_FAIL: " + label); }
    }
    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Click(Vector2 p)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
        await Frames(2);
    }
    private async Task Capture(string name)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = new(35, 250), GlobalPosition = new(35, 250) });
        await Frames(3);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok, "capture " + name);
    }
    private void SetCeasefire(double seconds)
    {
        var snapshot = _app.Campaign.Serialize();
        Check(CombatSnapshotCodec.TryDecode(snapshot.Value("payload"), out var raw) && raw is DataMap, "director payload decodes");
        var payload = (DataMap)raw!;
        payload["_earth_phase"] = "ceasefire"; payload["_earth_timer"] = seconds;
        snapshot["payload"] = CombatSnapshotCodec.Encode(payload);
        Check(_app.Campaign.Restore(snapshot), "real director restores configured ceasefire");
        _app.UpdateTacticalAlerts(.2);
    }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tactical alert native test requires isolated runtime-tests profile.");
            GetWindow().Size = new(1440, 900);
            _app = new Main(); AddChild(_app);
            _app.UserPaused = true; _app.PreserveCheckpoint = true;
            await Frames(8);
            Check(IsInstanceValid(_app.TacticalAlerts), "Main initializes shared tactical view");
            string initialCamera = _app.Planet.CaptureCameraState().ToJson();
            float fov = _app.Planet.Camera.Fov;
            _app.Battle.StartWave(); _app.UpdateTacticalAlerts(.2);
            var card = _app.TacticalAlerts.Mother;
            Check(card.Active && !card.Preview && card.Count > 0, "first-wave physical mothership announces actual UID");
            Check(card.TargetId.StartsWith("mother:"), "arrival target has stable UID identity");
            Check(_app.Battle.Motherships.Values.Any(m => "mother:" + m.L("uid") == card.TargetId && m.Vector3("space_position").IsEqualApprox(card.WorldPosition)), "arrival matches actual world position");
            Check(_app.Planet.CaptureCameraState().ToJson() == initialCamera, "arrival does not change camera");
            Check(card.Rect.Size == new Vector2(290, 165) && card.Rect.End.X < _app.WorldSize.X - 300, "large cut-corner card occupies playable area beside sidebar");
            int oldBuildings = _app.Game.Buildings.Values.Sum(v => Convert.ToInt32(v));
            _app.SelectedBuild = "mine";
            string firstTarget = card.TargetId;
            Vector3 firstPosition = card.WorldPosition;
            await Click(card.Rect.Position + new Vector2(150, 55));
            Check(_app.Planet.GetFocusId() == "alert:" + firstTarget, "actual mouse click focuses matching mothership identity");
            Check(_app.Planet.CaptureCameraState().Vector3("target").IsEqualApprox(firstPosition), "click sets authoritative world-space camera target");
            Check(!_app.Dragging && _app.Game.Buildings.Values.Sum(v => Convert.ToInt32(v)) == oldBuildings, "alert click neither builds nor rotates camera drag");
            Check(card.Acknowledged, "clicked warning acknowledges");
            _app.UpdateTacticalAlerts(.3);
            Check(card.Acknowledged, "same alive UID is not reannounced each poll");
            await Wait(1.3);
            Check(_app.Planet.Camera.Fov == fov, "tactical focus preserves strategic FOV");
            Check((_app.Planet.Camera.GlobalPosition - firstPosition).Length() > 10, "focus keeps reasonable standoff");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true }); await Frames();
            Check(_app.Planet.GetFocusId() == "earth", "Escape returns ordinary Earth navigation");
            _app.Planet.ResetCamera(); _app.DockOpen = true;
            _app.Battle.ResetBattle(); _app.Game.Wave = 4; _app.Battle.StartWave(); _app.UpdateTacticalAlerts(.2);
            Check(card.Count >= 2 && !card.Acknowledged, "new front reawakens alert without duplicating old UID");
            string beforeCycle = card.TargetId;
            await Click(TacticalAlertView.NextRect(card).GetCenter());
            Check(card.TargetId != beforeCycle, "small arrow selects another actual direction");
            Check(_app.Planet.GetFocusId() == "earth", "cycling targets alone does not move camera");
            var worldBack = -_app.Planet.Camera.GlobalPosition.Normalized() * WorldScale.EarthRadius;
            var damageMethod = typeof(Battlefield).GetMethod("DamageEarth", BindingFlags.Instance | BindingFlags.NonPublic)!;
            double hpBefore = _app.Game.EarthHp;
            damageMethod.Invoke(_app.Battle, [4d, worldBack, true]);
            _app.UpdateTacticalAlerts(.1);
            Check(Math.Abs(_app.Game.EarthHp - hpBefore + 4) < .00001 && _app.TacticalAlerts.Damage.Active, "real EarthDamaged event feeds only actual health damage");
            var damage = _app.TacticalAlerts.Damage;
            Check(damage.Backside && damage.Direction.IsFinite() && Math.Abs(damage.Direction.Length() - 1) < .001, "backside impact keeps finite direction arrow instead of false front reticle");
            Vector3 ground = _app.EarthAttackLocalDirection;
            _app.NotifyEarthAttack(3, worldBack);
            Check(Math.Abs(damage.Amount - 7) < .001, "hits merge inside rolling eight-second window");
            _app.NotifyEarthAttack(0, worldBack); _app.NotifyEarthAttack(double.NaN, worldBack);
            Check(Math.Abs(damage.Amount - 7) < .001, "zero and invalid damage never create false impact values");
            Check(damage.Rect.Position.Y > card.Rect.End.Y && !damage.Rect.Intersects(card.Rect), "two warnings stack without overlap");
            var celestial = _app.Planet.CaptureCelestialState(); celestial["earth_rotation_y"] = celestial.N("earth_rotation_y") + .55;
            _app.Planet.RestoreCelestialState(celestial); _app.UpdateTacticalAlerts(.1);
            Check(_app.EarthAttackLocalDirection.IsEqualApprox(ground), "ground coordinate remains local during Earth rotation");
            Check(damage.WorldPosition.IsEqualApprox(_app.Planet.Globe.ToGlobal(ground * WorldScale.EarthRadius)), "world marker follows rotating impact site");
            await Capture("tactical-alerts-arrival-impact");
            await Click(damage.Rect.Position + new Vector2(150, 55));
            Check(_app.Planet.GetFocusId() == "earth" && damage.Acknowledged, "impact mouse click selects Earth surface navigation");
            await Wait(1.3);
            var aim = _app.Planet.Camera.GlobalPosition.Normalized();
            var normal = (_app.Planet.Globe.GlobalBasis * ground).Normalized();
            Check(aim.Dot(normal) > .99f && _app.Planet.Camera.Fov == fov, "impact focus reveals correct rotated surface with unchanged FOV");
            _app.Action("tab:tech"); await Wait(.55); _app.UpdateTacticalAlerts(.2);
            Check(!card.Rect.Intersects(_app.ResearchGraph.GetGlobalRect()) && !damage.Rect.Intersects(_app.ResearchGraph.GetGlobalRect()), "both cards avoid expanded technology canvas");
            await Capture("tactical-alerts-research");
            _app.DockOpen = false; _app.Planet.ResetCamera();
            for (int wait = 0; wait < 120 && _app.ResearchGraph.Visible; wait++) await Frames(1);
            Check(!_app.ResearchGraph.Visible, "research collapse has finished before preview click coordinates are sampled");
            _app.UpdateTacticalAlerts(8.1);
            Check(!damage.Active, "impact alarm expires after eight seconds without more damage");
            _app.Game.Expedition.SetEarthLiberated(true); _app.Campaign.SyncCampaign();
            _app.Battle.ClearPostDefenseAttack();
            double period = _app.Game.CombatSettings.N("enemy_wave_duration");
            SetCeasefire(period + 1);
            Check(_app.CurrentFrontierWarning == null, "no relocation warning before final configured cycle");
            SetCeasefire(18);
            Check(card.Preview && _app.CurrentFrontierWarning != null, "final ceasefire cycle previews committed frontier");
            Check(!card.Rect.Intersects(new Rect2(_app.WorldSize.X - 318, 87, 300, 43)), "collapsed command title rail remains completely unobstructed");
            var director = new InvasionDirector(); director.ConfigureAnchor(_app.Battle.GetInvasionAnchor());
            var status = _app.Campaign.GetStatus();
            var expected = director.FrontierSpawnPoint(card.Index, status.I("earth_next_carriers"), _app.CurrentFrontierWarning!.Radius, new CombatRandom(122)).Vector3("position");
            Check(card.WorldPosition.IsEqualApprox(expected), "preview uses actual frontier anchor count and radius");
            double remaining = card.Remaining;
            await Wait(.3);
            Check(Math.Abs(card.Remaining - remaining) < .00001, "paused warning countdown remains frozen");
            await Capture("tactical-alerts-frontier-preview");
            await Click(card.Rect.Position + new Vector2(150, 55));
            if (!_app.Planet.CaptureCameraState().Vector3("target").IsEqualApprox(expected)) GD.PrintErr($"TACTICAL_PREVIEW_DIAGNOSTIC: expected={expected}, actual={_app.Planet.CaptureCameraState().Vector3("target")}, id={card.TargetId}, focus={_app.Planet.GetFocusId()}, rect={card.Rect}, modal={_app.Modal}, dock={_app.DockOpen}");
            Check(_app.Planet.CaptureCameraState().Vector3("target").IsEqualApprox(expected), "preview click focuses exact planned position");
            _app.Planet.ResetCamera();
            _app.Campaign.Paused = false; _app.Campaign.SpeedScale = 2; _app.Campaign.Step(.5); _app.Campaign.Paused = true; _app.UpdateTacticalAlerts(.2);
            Check(Math.Abs(card.Remaining - (remaining - 1)) < .00001, "countdown follows actual accelerated director clock");
            SetCeasefire(.001);
            _app.Campaign.Paused = false; _app.Campaign.SpeedScale = 1; _app.Campaign.Step(.02); _app.Campaign.Paused = true;
            _app.Battle.Paused = false; _app.Battle.Step(1.5); _app.Battle.Paused = true; _app.UpdateTacticalAlerts(.2);
            Check(!card.Preview && card.Active && card.TargetId.StartsWith("mother:"), "real spawn replaces preview with actual live UID");
            Check(_app.Battle.Enemies.Any(e => "mother:" + e.L("uid") == card.TargetId && e.Vector3("space_position").IsEqualApprox(card.WorldPosition)), "spawn alert follows actual carrier position");
            Check(!card.Acknowledged, "new arrival is announced even after preview acknowledgement");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); }
        GD.Print($"TACTICAL_ALERT_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
