using Godot;
using System;
using System.Collections.Generic;
using Earthward.Combat;
using Earthward.Domain;
using Earthward.Rendering;
namespace Earthward.Presentation;

public partial class AircraftSpectator : Node
{
    public event Action<bool>? ActiveChanged;
    public event Action<long>? AircraftChanged;
    public const float CameraFov = 72;
    public const float CameraNear = .015f;
    public const float PositionResponse = 22;
    public const float RotationResponse = 28;

    private static readonly HashSet<string> FlyingStates = new() { "patrol", "engaging", "pursuit", "returning", "suiciding" };
    private PlanetView? _planet;
    private Battlefield? _battle;
    private bool _active;
    private long _uid = -1;

    private DataMap _savedCamera = new();
    private Transform3D _smoothed = Transform3D.Identity;

    private readonly Dictionary<long, DataMap> _sources = new();

    private readonly RandomNumberGenerator _rng = new();
    public int CandidateScans
    {
        get;
        private set;
    }

    public override void _Ready()
    {
        ProcessPriority = 20;
        _rng.Randomize();
    }

    public void Setup(PlanetView planet, Battlefield battle)
    {
        if (_active)
            Exit();
        if (IsInstanceValid(_planet))
            _planet!.StrategicCameraRequested -= OnStrategicCameraRequested;
        _planet = planet;
        _battle = battle;
        _planet.StrategicCameraRequested += OnStrategicCameraRequested;
    }

    public bool Enter()
    {
        if (_active)
            return true;
        if (!IsInstanceValid(_planet) || _battle == null)
            return false;
        long uid = RandomCandidate(-1);
        if (uid < 0)
            return false;
        _savedCamera = _planet!.CaptureCameraState();
        if (_savedCamera.Count == 0)
            return false;
        _active = true;
        Select(uid);
        ActiveChanged?.Invoke(true);
        return true;
    }

    public bool NextAircraft()
    {
        if (!_active)
            return Enter();
        long uid = RandomCandidate(_uid);
        if (uid < 0)
        {
            Exit();
            return false;
        }
        Select(uid);
        return true;
    }

    public void Exit()
    {
        if (!_active)
            return;
        _active = false;
        _uid = -1;
        if (IsInstanceValid(_planet) && _planet!.IsInsideTree())
            _planet.RestoreCameraState(_savedCamera);
        _savedCamera.Clear();
        _sources.Clear();
        ActiveChanged?.Invoke(false);
    }

    public bool IsActive() => _active;

    public DataMap GetInfo()
    {
        var pose = ReadPose(_uid);
        return new()
        {
            ["active"] = _active,
            ["uid"] = _uid,
            ["kind"] = pose.S("kind"),
            ["state"] = pose.S("state"),
            ["candidate_scans"] = CandidateScans
        };
    }

    public override void _Process(double delta) => UpdateView(delta);

    public void UpdateView(double delta)
    {
        if (!_active)
            return;
        if (!IsInstanceValid(_planet) || _battle == null)
        {
            Exit();
            return;
        }
        var pose = ReadPose(_uid);
        if (pose.Count == 0)
        {
            NextAircraft();
            return;
        }
        if (_battle.Paused)
            return;
        var desired = CameraPose(pose);
        float dt = (float)Math.Max(0, delta);
        _smoothed.Origin = _smoothed.Origin.Lerp(desired.Origin, 1 - Mathf.Exp(-PositionResponse * dt));
        _smoothed.Basis = new Basis(_smoothed.Basis.GetRotationQuaternion().Slerp(desired.Basis.GetRotationQuaternion(), 1 - Mathf.Exp(-RotationResponse * dt)));
        _planet!.ApplyAircraftCamera(_smoothed, CameraFov, CameraNear);
    }

    private long RandomCandidate(long exclude)
    {
        _sources.Clear();
        CandidateScans++;
        if (_battle == null)
            return -1;
        var candidates = new List<long>();
        long fallback = -1;
        foreach (var source in _battle.Drones)
        {
            long uid = source.L("uid", -1);
            if (uid < 0)
                continue;
            _sources[uid] = source;
            if (ReadPose(uid).Count == 0)
                continue;
            if (uid == exclude)
                fallback = uid;
            else
                candidates.Add(uid);
        }
        return candidates.Count == 0 ? fallback : candidates[_rng.RandiRange(0, candidates.Count - 1)];
    }

    private DataMap ReadPose(long uid)
    {
        if (_battle == null || !_battle.Active || _battle.GetActiveDroneCount() == 0 || !_battle.IsLiveDrone(uid) || !_sources.TryGetValue(uid, out var source) || source.N("hp") <= 0)
            return new();
        string state = source.S("state");
        if (!FlyingStates.Contains(state) || source.N("launch_age") < 1.3)
            return new();
        if (!_battle.Factories.TryGetValue(source.L("factory_site_id", -1), out var factory) || factory.S("kind") != source.S("kind"))
            return new();
        Vector3 position = _battle.GetDroneWorldPosition(source), forward = source.Vector3("aim_direction"), up = source.Vector3("aim_up");
        if (!position.IsFinite() || position.Length() < WorldScale.EarthRadius + WorldScale.DroneAltitude - .05f || !forward.IsFinite() || !up.IsFinite() || forward.LengthSquared() < .000001f)
            return new();
        forward = forward.Normalized();
        up -= forward * up.Dot(forward);
        if (up.LengthSquared() < .000001f)
            return new();
        return new()
        {
            ["uid"] = uid,
            ["kind"] = source.S("kind"),
            ["state"] = state,
            ["position"] = position,
            ["forward"] = forward,
            ["up"] = up.Normalized(),
            ["scale"] = _battle.GetDroneScale()
        };
    }

    private static Transform3D CameraPose(DataMap pose)
    {
        var forward = pose.Vector3("forward");
        var up = pose.Vector3("up");
        var right = forward.Cross(up).Normalized();
        up = right.Cross(forward).Normalized();
        var eye = pose.Vector3("position") + (forward + up) * (.025f * (float)pose.N("scale"));
        return new Transform3D(new Basis(right, up, -forward), eye);
    }

    private void Select(long uid)
    {
        _uid = uid;
        _smoothed = CameraPose(ReadPose(uid));
        _planet!.BeginAircraftView(uid);
        _planet.ApplyAircraftCamera(_smoothed, CameraFov, CameraNear);
        AircraftChanged?.Invoke(uid);
    }

    private void OnStrategicCameraRequested() => Exit();

    public override void _ExitTree()
    {
        Exit();
        if (IsInstanceValid(_planet))
            _planet!.StrategicCameraRequested -= OnStrategicCameraRequested;
    }
}
