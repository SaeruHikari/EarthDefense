using Godot;
using Earthward.Domain;
using Earthward.Combat;
using Earthward.Presentation;
namespace Earthward.Application;

public partial class Main
{
    public FrontierWarning? CurrentFrontierWarning { get; private set; }
    public Rect2 FrontierWarningRect => TacticalAlerts?.Mother.Active == true ? TacticalAlerts.Mother.Rect : default;
    public TacticalAlertView TacticalAlerts { get; private set; } = null!;
    public Vector3 EarthAttackLocalDirection => _attackLocalNormal;
    private sealed record AlertTarget(string Id, long Uid, Vector3 Position, bool Preview);
    private readonly Dictionary<string, AlertTarget> _liveAlertTargets = new(StringComparer.Ordinal);
    private readonly Queue<AlertTarget> _pendingMotherAlerts = new();
    private readonly HashSet<string> _seenAlertTargets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenPreviewTransitions = new(StringComparer.Ordinal);
    private readonly List<(double Time, double Amount)> _attackSamples = new();
    private string _alertRunId = "", _alertPress = "", _alertPressedTarget = "";
    private int _alertTimelineEpoch = -1;
    private AlertTarget? _previewAlertTarget;
    private Vector2 _alertPressPosition;
    private Vector3 _attackLocalNormal;
    private double _alertPoll, _alertClock, _lastAttackTime = -100;

    public void InitializeTacticalAlerts()
    {
        if (IsInstanceValid(TacticalAlerts)) return;
        TacticalAlerts = new TacticalAlertView { Name = "TacticalAlerts", ZIndex = 30 };
        AddChild(TacticalAlerts);
        _alertPoll = 1;
        UpdateTacticalAlerts(0);
    }
    public void UpdateTacticalAlerts(double delta)
    {
        if (!IsInstanceValid(TacticalAlerts) || Game == null || Battle == null || Campaign == null) return;
        // Count the time a notice is actually available to the player. A modal
        // covers the HUD completely, while a user-paused world leaves it visible.
        double elapsed = double.IsFinite(delta) ? Math.Max(0, delta) : 0;
        double visibleElapsed = Modal == "" && !Defeated ? elapsed : 0;
        _alertClock += visibleElapsed;
        if (_alertRunId != Game.RunId || _alertTimelineEpoch != Battle.TimelineEpoch)
        {
            _alertRunId = Game.RunId;
            _alertTimelineEpoch = Battle.TimelineEpoch;
            _seenAlertTargets.Clear(); _liveAlertTargets.Clear(); _pendingMotherAlerts.Clear();
            _seenPreviewTransitions.Clear(); _attackSamples.Clear(); _previewAlertTarget = null;
            _alertPress = ""; _alertPressedTarget = ""; _lastAttackTime = -100;
            TacticalAlerts.Mother.Active = TacticalAlerts.Damage.Active = false;
            _alertPoll = 1;
        }
        if (TacticalAlerts.Mother.Active) TacticalAlerts.Mother.Age += visibleElapsed;
        _alertPoll += elapsed;
        if (_alertPoll >= .15)
        {
            _alertPoll = 0;
            RefreshTacticalTargets();
        }
        AdvanceMotherAlert();
        var damage = TacticalAlerts.Damage;
        _attackSamples.RemoveAll(sample => _alertClock - sample.Time >= 8);
        damage.Active = _attackSamples.Count > 0 && !Defeated;
        damage.Amount = _attackSamples.Sum(sample => sample.Amount);
        damage.Age = _alertClock - _lastAttackTime;
        damage.Title = "地球遭到攻击"; damage.Detail = "地表损伤 · 防线告急";
        damage.WorldPosition = Planet.Globe.ToGlobal(_attackLocalNormal * WorldScale.EarthRadius);
        damage.TargetId = "earth:impact";
        TacticalAlerts.Clock = _alertClock;
        LayoutTacticalAlerts();
        ProjectAlert(TacticalAlerts.Mother); ProjectAlert(damage);
        TacticalAlerts.HoverAction = TacticalAlerts.HitTest(Mouse);
        TacticalAlerts.PressedAction = _alertPress;
        TacticalAlerts.Visible = Modal == "" && !Defeated;
        TacticalAlerts.QueueRedraw();
    }
    private void RefreshTacticalTargets()
    {
        var status = Campaign.GetStatus();
        CurrentFrontierWarning = FrontierWarning.FromStatus(status, WorldScale.EarthRadius);
        _liveAlertTargets.Clear();
        foreach (var mother in Battle.Motherships.Values)
            if (mother.N("hp") > 0) AddLiveAlert(mother);
        foreach (var enemy in Battle.Enemies)
            if (enemy.N("hp") > 0 && enemy.S("kind") is "carrier" or "mothership") AddLiveAlert(enemy);
        foreach (var target in _liveAlertTargets.Values.OrderBy(target => target.Uid))
            if (_seenAlertTargets.Add(target.Id)) _pendingMotherAlerts.Enqueue(target);

        // Keep the existing one-wave relocation warning as a single timed
        // direction notice. Physical arrivals are separate per-UID events.
        var warning = CurrentFrontierWarning;
        _previewAlertTarget = null;
        if (warning != null)
        {
            var preview = new InvasionDirector(); preview.ConfigureAnchor(Battle.GetInvasionAnchor());
            int count = Math.Clamp(status.I("earth_next_carriers", 2), 1, 512);
            var point = preview.FrontierSpawnPoint(0, count, warning.Radius, new CombatRandom(122));
            _previewAlertTarget = new(warning.TransitionId + ":preview", 0, point.Vector3("position"), true);
            if (_seenPreviewTransitions.Add(warning.TransitionId)) _pendingMotherAlerts.Enqueue(_previewAlertTarget);
        }
    }
    private void AddLiveAlert(DataMap enemy)
    {
        long uid = enemy.L("uid"); var position = enemy.Vector3("space_position");
        if (!position.IsFinite() || position.LengthSquared() < .01f) return;
        string id = "mother:" + uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _liveAlertTargets.TryAdd(id, new(id, uid, position, false));
    }
    private AlertTarget? ResolveAlertTarget(string id)
    {
        if (_liveAlertTargets.TryGetValue(id, out var target)) return target;
        return _previewAlertTarget?.Id == id ? _previewAlertTarget : null;
    }
    private void AdvanceMotherAlert()
    {
        var card = TacticalAlerts.Mother;
        if (Defeated) { card.Active = false; return; }
        var target = card.Active ? ResolveAlertTarget(card.TargetId) : null;
        if (card.Active && (card.Age >= TacticalAlertView.AlertLifetime || target == null
            || card.Preview && _pendingMotherAlerts.Any(pending => !pending.Preview)))
            card.Active = false;
        if (!card.Active)
        {
            target = null;
            while (_pendingMotherAlerts.TryDequeue(out var pending))
            {
                target = ResolveAlertTarget(pending.Id);
                if (target == null) continue;
                card.Active = true;
                card.TargetId = target.Id;
                card.Preview = target.Preview;
                card.Age = 0;
                card.Acknowledged = false;
                break;
            }
        }
        if (!card.Active || target == null) return;
        card.WorldPosition = target.Position;
        card.Title = card.Preview ? "母舰即将抵达" : "检测到高能信号";
        card.Detail = card.Preview && CurrentFrontierWarning != null
            ? $"新阵地 · 离地 {UiTheme.Number(CurrentFrontierWarning.Altitude)}" : "敌方母舰 · 已锁定阵位";
        card.Remaining = card.Preview ? CurrentFrontierWarning!.Remaining : 0;
        card.Window = card.Preview ? CurrentFrontierWarning!.WindowDuration : 0;
    }
    public void NotifyEarthAttack(double amount, Vector3 worldPosition)
    {
        if (!double.IsFinite(amount) || amount <= 0 || !worldPosition.IsFinite() || worldPosition.LengthSquared() < .001f || !IsInstanceValid(TacticalAlerts)) return;
        // A direction belongs to the rotating ground, while carriers remain in inertial world space.
        _attackLocalNormal = Planet.Globe.ToLocal(worldPosition).Normalized();
        if (_attackSamples.Count > 0 && _alertClock - _attackSamples[^1].Time < .05)
        {
            var last = _attackSamples[^1]; _attackSamples[^1] = (last.Time, last.Amount + amount);
        }
        else _attackSamples.Add((_alertClock, amount));
        _lastAttackTime = _alertClock;
        TacticalAlerts.Damage.Acknowledged = false;
        UpdateTacticalAlerts(0);
    }
    private void LayoutTacticalAlerts()
    {
        if (!IsInstanceValid(TacticalAlerts)) return;
        // The command title rail remains visible when collapsed; its footprint is never free HUD space.
        float right = WorldSize.X - Math.Max(RailWidth, _researchWidth) - 22;
        float width = Math.Min(290, Math.Max(220, right - 24));
        float x = Math.Max(12, right - width), y = 96;
        TacticalAlerts.Mother.Rect = new(x, y, width, 165);
        TacticalAlerts.Damage.Rect = new(x, y + (TacticalAlerts.Mother.Active ? 179 : 0), width, 165);
        TacticalAlerts.PlayArea = new Rect2(20, 88, Math.Max(40, right - 40), Math.Max(80, WorldSize.Y - 190));
    }
    private void ProjectAlert(TacticalAlertView.Card card)
    {
        if (!card.Active || Planet.Camera == null) return;
        var camera = Planet.Camera;
        Vector3 offset = card.WorldPosition - camera.GlobalPosition;
        var local = camera.GlobalBasis.Inverse() * offset;
        card.Direction = TacticalAlertView.DirectionFor(local);
        float distance = offset.Length();
        float earthHit = Rendering.PlanetView.NavigationSphereHit(camera.GlobalPosition - Planet.Globe.GlobalPosition, offset.Normalized(), Vector3.Zero, WorldScale.EarthRadius - .01f);
        card.Backside = earthHit >= 0 && earthHit < distance - .15f;
        var bounds = TacticalAlerts.PlayArea.Grow(-18);
        var projected = local.Z < -.001f ? Planet.RenderToLogical(camera.UnprojectPosition(card.WorldPosition)) : bounds.GetCenter();
        card.Offscreen = local.Z >= -.001f || !bounds.HasPoint(projected);
        if (card.Backside || card.Offscreen)
        {
            var half = bounds.Size * .5f;
            float scale = Math.Min(half.X / Math.Max(Math.Abs(card.Direction.X), .001f), half.Y / Math.Max(Math.Abs(card.Direction.Y), .001f));
            card.Reticle = bounds.GetCenter() + card.Direction * scale * .98f;
        }
        else card.Reticle = projected;
    }
    // Kept as the existing HUD layout entry point; the shared view owns all drawing.
    private void DrawFrontierWarning()
    {
        LayoutTacticalAlerts();
        if (IsInstanceValid(TacticalAlerts)) TacticalAlerts.QueueRedraw();
    }
    private bool TacticalAlertContains(Vector2 point) => IsInstanceValid(TacticalAlerts) && Modal == "" && TacticalAlerts.HitTest(point) != "";
    private bool TacticalAlertInput(InputEvent e)
    {
        if (!IsInstanceValid(TacticalAlerts) || Modal != "" || Defeated) { _alertPress = ""; _alertPressedTarget = ""; return false; }
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape && Planet.GetFocusId().StartsWith("alert:"))
        {
            FocusBody("earth"); GetViewport().SetInputAsHandled(); return true;
        }
        if (e is not InputEventMouse mouse) return false;
        string hit = TacticalAlerts.HitTest(mouse.Position);
        if (e is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            if (button.Pressed && hit != "")
            {
                _alertPress = hit; _alertPressPosition = button.Position;
                _alertPressedTarget = hit == "mother:focus" ? TacticalAlerts.Mother.TargetId : TacticalAlerts.Damage.TargetId;
                Dragging = false; _middleDragging = false; _navigationPressedTarget = new();
                if (_buildPainting) FinishBuildStroke();
                _upgradePointerDown = false;
            }
            else if (!button.Pressed && _alertPress != "")
            {
                string action = _alertPress, target = _alertPressedTarget;
                _alertPress = ""; _alertPressedTarget = "";
                if (hit == action && button.Position.DistanceTo(_alertPressPosition) < 12) ActivateTacticalAlert(action, target);
                TacticalAlerts.PressedAction = "";
                GetViewport().SetInputAsHandled(); return true;
            }
        }
        if (hit == "" && _alertPress == "") return false;
        TacticalAlerts.HoverAction = hit; TacticalAlerts.PressedAction = _alertPress;
        Dragging = false;
        GetViewport().SetInputAsHandled(); return true;
    }
    private void ActivateTacticalAlert(string action, string pressedTarget)
    {
        if (action == "mother:focus" && TacticalAlerts.Mother.Active)
        {
            // A queued arrival may replace a timed-out card between mouse down
            // and mouse up. Never redirect that click to the replacement ship.
            if (TacticalAlerts.Mother.TargetId != pressedTarget) return;
            RefreshTacticalTargets();
            var target = ResolveAlertTarget(pressedTarget);
            if (target == null || TacticalAlerts.Mother.Age >= TacticalAlertView.AlertLifetime)
            {
                AdvanceMotherAlert();
                return;
            }
            ClearFactoryCoverage(); ExitSpectator(); CancelBuildSelection(); CancelResourceUpgrade();
            TacticalAlerts.Mother.WorldPosition = target.Position;
            if (Planet.FocusPoint(target.Position, target.Id))
                TacticalAlerts.Mother.Acknowledged = true;
        }
        else if (action == "damage:focus" && TacticalAlerts.Damage.Active)
        {
            ClearFactoryCoverage(); ExitSpectator(); CancelBuildSelection(); CancelResourceUpgrade();
            Planet.RotateEarthToDirection(_attackLocalNormal);
            TacticalAlerts.Damage.Acknowledged = true;
        }
    }
}
