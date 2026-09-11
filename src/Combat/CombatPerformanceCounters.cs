using System;
using System.Diagnostics;

namespace Earthward.Combat;

public enum CombatStage { Configuration, WorldSynchronization, EffectsAndShields, Factories, Drones, Assignments, EnemyAI, SpatialIndex, Weapons, Projectiles, EnemyColliderBuild, FriendlyColliderBuild, MissileGuidance, FriendlyImpacts, HostileImpacts, DefenderIndexBuild, DefenderQueries, DamageArmor, DamageFeedback, DamageAfterEffects, NeighborhoodQueries, FlightPreparation, FlightSimulation, FlightCommit }

/// <summary>Optional per-frame timings. A null Battlefield.Performance performs no timing reads or allocations.</summary>
public sealed class CombatPerformanceCounters
{
    private readonly long[] _ticks = new long[Enum.GetValues<CombatStage>().Length];
    private readonly int[] _calls = new int[Enum.GetValues<CombatStage>().Length];
    public long FrameNumber { get; private set; }
    internal long BeginFrame() { Array.Clear(_ticks); Array.Clear(_calls); FrameNumber++; return Stopwatch.GetTimestamp(); }
    internal long Timestamp() => Stopwatch.GetTimestamp();
    internal long Record(CombatStage stage, long started)
    {
        long now = Stopwatch.GetTimestamp(); _ticks[(int)stage] += now - started; _calls[(int)stage]++; return now;
    }
    public double Milliseconds(CombatStage stage) => _ticks[(int)stage] * 1000d / Stopwatch.Frequency;
    public int Calls(CombatStage stage) => _calls[(int)stage];
}
