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
    private readonly List<AlertTarget> _alertTargets = new();
    private readonly HashSet<string> _seenAlertTargets = new(StringComparer.Ordinal);
    private readonly List<(double Time, double Amount)> _attackSamples = new();
    private string _alertRunId = "", _previewTransition = "", _alertPress = "";
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
        _alertClock += Math.Max(0, delta);
        if (_alertRunId != Game.RunId)
        {
            _alertRunId = Game.RunId;
            _seenAlertTargets.Clear(); _alertTargets.Clear(); _attackSamples.Clear();
            _previewTransition = ""; _alertPress = ""; _lastAttackTime = -100;
            TacticalAlerts.Mother.Active = TacticalAlerts.Damage.Active = false;
            _alertPoll = 1;
        }
        TacticalAlerts.Mother.Age += Math.Max(0, delta);
        _alertPoll += Math.Max(0, delta);
        if (_alertPoll >= .15)
        {
            _alertPoll = 0;
            RefreshTacticalTargets();
        }
        // A confirmed signal is a timed notice, not a permanent indicator; it expires like a damage report.
        var mother = TacticalAlerts.Mother;
        mother.Active = _alertTargets.Count > 0 && !Defeated
            && (CurrentFrontierWarning != null || mother.Age < TacticalAlertView.AlertLifetime);
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
        var card = TacticalAlerts.Mother;
        string selected = card.TargetId;
        Vector3 previousPoint = card.WorldPosition;
        bool wasPreview = card.Preview;
        _alertTargets.Clear();
        var warning = CurrentFrontierWarning;
        bool fresh = false;
        if (warning != null)
        {
            var preview = new InvasionDirector(); preview.ConfigureAnchor(Battle.GetInvasionAnchor());
            int count = Math.Clamp(status.I("earth_next_carriers", 2), 1, 512);
            var isolatedRandom = new CombatRandom(122);
            for (int i = 0; i < count; i++)
            {
                var point = preview.FrontierSpawnPoint(i, count, warning.Radius, isolatedRandom);
                _alertTargets.Add(new($"{warning.TransitionId}:{i}", 0, point.Vector3("position"), true));
            }
            fresh = _previewTransition != warning.TransitionId;
            _previewTransition = warning.TransitionId;
            card.Remaining = warning.Remaining; card.Window = warning.WindowDuration;
            card.Title = "母舰即将抵达"; card.Detail = $"新阵地 · 离地 {UiTheme.Number(warning.Altitude)}";
        }
        else
        {
            foreach (var mother in Battle.Motherships.Values)
                if (mother.N("hp") > 0) AddLiveAlert(mother, ref fresh);
            foreach (var enemy in Battle.Enemies)
                if (enemy.N("hp") > 0 && enemy.S("kind") is "carrier" or "mothership") AddLiveAlert(enemy, ref fresh);
            _alertTargets.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            _seenAlertTargets.IntersectWith(_alertTargets.Select(target => target.Id));
            card.Title = "母舰信号确认"; card.Detail = "敌军增援 · 已锁定阵位";
            _previewTransition = "";
        }
        card.Active = _alertTargets.Count > 0 && !Defeated;
        if (!card.Active) return;
        int index = _alertTargets.FindIndex(target => target.Id == selected);
        if (index < 0 && wasPreview && warning == null)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < _alertTargets.Count; i++)
            {
                float distance = _alertTargets[i].Position.DistanceSquaredTo(previousPoint);
                if (distance < nearest) { nearest = distance; index = i; }
            }
        }
        card.Index = index >= 0 ? index : Math.Clamp(card.Index, 0, _alertTargets.Count - 1);
        card.Count = _alertTargets.Count;
        SelectAlertTarget(card.Index);
        if (fresh || wasPreview != card.Preview) { card.Age = 0; card.Acknowledged = false; }
        // RefreshTacticalTargets runs periodically while the target remains alive.
        // Do not let that polling revive an expired confirmation card; only a new
        // target (fresh=true above) or a pre-arrival warning may make it visible.
        card.Active = warning != null || card.Age < TacticalAlertView.AlertLifetime;
    }
    private void AddLiveAlert(DataMap enemy, ref bool fresh)
    {
        long uid = enemy.L("uid"); var position = enemy.Vector3("space_position");
        if (!position.IsFinite() || position.LengthSquared() < .01f) return;
        string id = "mother:" + uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_seenAlertTargets.Add(id)) fresh = true;
        _alertTargets.Add(new(id, uid, position, false));
    }
    private void SelectAlertTarget(int index)
    {
        if (_alertTargets.Count == 0) return;
        index = (index % _alertTargets.Count + _alertTargets.Count) % _alertTargets.Count;
        var target = _alertTargets[index]; var card = TacticalAlerts.Mother;
        card.Index = index; card.TargetId = target.Id; card.WorldPosition = target.Position; card.Preview = target.Preview;
        ProjectAlert(card);
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
        if (!IsInstanceValid(TacticalAlerts) || Modal != "" || Defeated) { _alertPress = ""; return false; }
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
                Dragging = false; _middleDragging = false; _navigationPressedTarget = new();
                if (_buildPainting) FinishBuildStroke();
                _upgradePointerDown = false;
            }
            else if (!button.Pressed && _alertPress != "")
            {
                string action = _alertPress; _alertPress = "";
                if (hit == action && button.Position.DistanceTo(_alertPressPosition) < 12) ActivateTacticalAlert(action);
                TacticalAlerts.PressedAction = "";
                GetViewport().SetInputAsHandled(); return true;
            }
        }
        if (hit == "" && _alertPress == "") return false;
        TacticalAlerts.HoverAction = hit; TacticalAlerts.PressedAction = _alertPress;
        Dragging = false;
        GetViewport().SetInputAsHandled(); return true;
    }
    private void ActivateTacticalAlert(string action)
    {
        if (action is "mother:prev" or "mother:next")
        {
            SelectAlertTarget(TacticalAlerts.Mother.Index + (action.EndsWith("next") ? 1 : -1));
            TacticalAlerts.Mother.Acknowledged = false;
            return;
        }
        ClearFactoryCoverage(); ExitSpectator(); CancelBuildSelection(); CancelResourceUpgrade();
        if (action == "mother:focus" && TacticalAlerts.Mother.Active)
        {
            // Resolve again at click time so movement during the last polling interval cannot misdirect a click.
            string selected = TacticalAlerts.Mother.TargetId;
            RefreshTacticalTargets();
            int index = _alertTargets.FindIndex(target => target.Id == selected);
            if (index < 0 && selected.StartsWith("mother:")) return;
            if (index >= 0) SelectAlertTarget(index);
            if (TacticalAlerts.Mother.Active && Planet.FocusPoint(TacticalAlerts.Mother.WorldPosition, TacticalAlerts.Mother.TargetId))
                TacticalAlerts.Mother.Acknowledged = true;
        }
        else if (action == "damage:focus" && TacticalAlerts.Damage.Active)
        {
            Planet.RotateEarthToDirection(_attackLocalNormal);
            TacticalAlerts.Damage.Acknowledged = true;
        }
    }
}
