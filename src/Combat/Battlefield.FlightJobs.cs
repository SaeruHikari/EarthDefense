using System;
using System.Threading.Tasks;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

public sealed partial class Battlefield
{
    public bool ForceSerialFlight { get; set; }
    private struct FlightJob { public DataMap Actor; public Profile Profile; public Vector3 Home, Position; }
    private sealed class FlightContext
    {
        public Battlefield? Owner; public DataMap Actor = null!; public Profile Profile = null!;
        public Vector3 Home, Position; public Transform3D Frame, Inverse;
    }
    [ThreadStatic] private static FlightContext? _flightContext;
    private FlightJob[] _flightJobs = Array.Empty<FlightJob>();
    private Vector3[] _flightResults = Array.Empty<Vector3>();
    private int _flightJobCount, _serialFlightScanEnd;
    public int FlightEligibilityChecks { get; private set; }
    private double _flightDelta;
    private Transform3D _flightFrame, _flightInverse;
    private readonly ParallelOptions _flightOptions = new() { MaxDegreeOfParallelism = Math.Max(1, Math.Min(8, System.Environment.ProcessorCount / 2)) };
    private Action<int>? _flightWorker;
    private bool IsIndependentFlight(DataMap drone)
    {
        if (Performance != null) FlightEligibilityChecks++;
        if (C.N(drone, "launch_age") < CombatScale.LaunchDuration || C.S(drone, "state") is not ("patrol" or "engaging")) return false;
        return !Active || InvasionWon || C.N(drone, "hp") > C.N(drone, "max_hp") * C.N(GetProfile(drone).Patrol, "repair_threshold", .35);
    }
    private int RunFlightBatch(int start, double dt)
    {
        if (ForceSerialFlight || Drones.Count < 512 || _flightOptions.MaxDegreeOfParallelism < 2 || start < _serialFlightScanEnd) return 0;
        long mark = Performance?.Timestamp() ?? 0;
        int end = start;
        while (end < Drones.Count && IsIndependentFlight(Drones[end])) end++;
        int count = end - start;
        if (count < 128)
        {
            // The healthy segment is processed sequentially in original order.
            // Do not rescan its remaining suffix at every following aircraft.
            _serialFlightScanEnd = end;
            return 0;
        }
        if (Surface != null && !Surface.TryGetCoordinateFrame(out _flightFrame))
        {
            _serialFlightScanEnd = end;
            return 0;
        }
        if (Surface == null) _flightFrame = Transform3D.Identity;
        _flightInverse = _flightFrame.AffineInverse();
        if (_flightJobs.Length < count) { Array.Resize(ref _flightJobs, Math.Max(count, _flightJobs.Length * 2)); Array.Resize(ref _flightResults, _flightJobs.Length); }
        for (int i = 0; i < count; i++)
        {
            var drone = Drones[start + i];
            _flightJobs[i] = new() { Actor = drone, Profile = GetProfile(drone), Home = Home(drone), Position = GetDroneWorldPosition(drone) };
        }
        _flightDelta = dt; _flightJobCount = count;
        _flightWorker ??= ExecuteFlightChunk;
        if (Performance != null) mark = Performance.Record(CombatStage.FlightPreparation, mark);
        Parallel.For(0, (count + 127) / 128, _flightOptions, _flightWorker);
        if (Performance != null) mark = Performance.Record(CombatStage.FlightSimulation, mark);
        // Shared actor indexes and render caches are committed in original order on the main thread.
        for (int i = 0; i < count; i++) WorldCache[C.L(_flightJobs[i].Actor, "uid")] = _flightResults[i];
        Performance?.Record(CombatStage.FlightCommit, mark);
        return count;
    }
    private void ExecuteFlightChunk(int chunk)
    {
        var context = _flightContext ??= new();
        context.Owner = this; context.Frame = _flightFrame; context.Inverse = _flightInverse;
        try
        {
            int end = Math.Min(_flightJobCount, (chunk + 1) * 128);
            for (int i = chunk * 128; i < end; i++)
            {
                var job = _flightJobs[i]; context.Actor = job.Actor; context.Profile = job.Profile; context.Home = job.Home; context.Position = job.Position;
                UpdateIndependentFlight(job.Actor, _flightDelta);
                _flightResults[i] = context.Position;
            }
        }
        finally { context.Owner = null; }
    }
    private void UpdateIndependentFlight(DataMap drone, double dt)
    {
        drone["flight_age"] = C.N(drone, "flight_age", 1e6) + Math.Max(dt, 0);
        var previous = GetDroneWorldPosition(drone);
        drone["hit"] = Math.Max(0, C.N(drone, "hit") - dt);
        drone["flash"] = Math.Max(0, C.N(drone, "flash") - dt * 5);
        if (Active) UpdateInterceptor(drone, dt); else ReturnToPatrol(drone, dt);
        CachePosition(drone);
        var position = GetDroneWorldPosition(drone);
        if (dt > 0)
        {
            var velocity = C.Scale(position - previous, 1 / dt);
            drone["velocity"] = velocity;
            if (velocity.LengthSquared() > .000001) drone["tangent"] = velocity.Normalized();
        }
        drone["world_up"] = position.Normalized();
        UpdateDroneAttitude(drone, dt);
    }
}
