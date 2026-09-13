using System.Numerics;
using Sugoi.Data;
using Sugoi.Tasks;

namespace Sugoi.DefenseSimulation;

[QueryJob]
internal partial struct MovementJob
{
    public float DeltaTime;
    private void Execute(Span<Position> positions, ReadOnlySpan<Velocity> velocities)
    {
        for (int i = 0; i < positions.Length; i++) positions[i].Value += velocities[i].Value * DeltaTime;
    }
}

internal readonly record struct TargetSnapshot(Entity Entity, Vector3 Position);
internal readonly record struct HitResult(Entity Target, Entity Attacker, int Frame, int ShotSerial, float Damage);

/// <summary>Output ownership follows this frame, including every submitted snapshot/attack job and its batches.</summary>
internal sealed class CombatFrame
{
    internal CombatFrame(int human, int alien)
    {
        HumanTargets = new TargetSnapshot[human]; AlienTargets = new TargetSnapshot[alien];
        HumanHits = new HitResult[human]; AlienHits = new HitResult[alien];
    }
    internal readonly TargetSnapshot[] HumanTargets, AlienTargets;
    internal readonly HitResult[] HumanHits, AlienHits;
}

internal struct SnapshotJob : IQueryJob
{
    public ComponentType FleetTag;
    public TargetSnapshot[] Output;
    public void Build(JobAccessBuilder builder) => builder.Read<Position>().Has<Aircraft>().Description.WithAll(FleetTag);
    public void Execute(in JobContext context)
    {
        var positions = context.View.ReadOwned<Position>();
        var entities = context.Entities;
        for (int i = 0; i < context.Count; i++) Output[context.Index + i] = new(entities[i], positions[i].Value);
    }
}

internal struct AttackJob : IQueryJob
{
    public ComponentType FleetTag;
    public int Frame;
    public TargetSnapshot[] Targets;
    public HitResult[] Results;
    public void Build(JobAccessBuilder builder) => builder.Read<Position>().Read<Aircraft>().Read<Weapon>().Description.WithAll(FleetTag);
    public void Execute(in JobContext context)
    {
        var positions = context.View.ReadOwned<Position>();
        var aircraft = context.View.ReadOwned<Aircraft>();
        var weapons = context.View.ReadOwned<Weapon>();
        var entities = context.Entities;
        if (Targets.Length == 0) return;

        for (int i = 0; i < context.Count; i++)
        {
            int serial = aircraft[i].Serial;
            if ((Frame + serial) % weapons[i].FireEverySteps != 0) continue;
            uint seed = unchecked((uint)serial * 2654435761u + (uint)Frame * 97u);
            int start = (int)(seed % (uint)Targets.Length);
            float nearest = weapons[i].RangeSquared;
            Entity target = Entity.Null;
            // Fixed eight-candidate budget: no all-pairs target search hidden behind ECS scheduling.
            for (int candidate = 0; candidate < Math.Min(8, Targets.Length); candidate++)
            {
                int index = (start + candidate * 131) % Targets.Length;
                var snapshot = Targets[index];
                float distance = Vector3.DistanceSquared(positions[i].Value, snapshot.Position);
                if (distance < nearest) { nearest = distance; target = snapshot.Entity; }
            }
            if (!target.IsNull) Results[context.Index + i] = new(target, entities[i], Frame, serial, weapons[i].Damage);
        }
    }
}
