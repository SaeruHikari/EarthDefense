using System.Diagnostics;
using System.Numerics;
using Sugoi.Data;
using Sugoi.Tasks;

internal static class TasksStressChecks
{
    private struct Position { public Vector3 Value; }
    private struct Velocity { public Vector3 Value; }
    private struct Health { public float Value; }

    /// <summary>Reproducible scheduler/data kernel check, deliberately not labelled as a game/frame-rate benchmark.</summary>
    internal static async Task RunAsync()
    {
        using var runtime = new EcsRuntime();
        runtime.Types.Register<Position>(new Guid("E284D958-8A90-4484-8BAE-F7FF562A5211"));
        runtime.Types.Register<Velocity>(new Guid("C0392259-5F87-4320-A5B4-ECF78293A9DF"));
        runtime.Types.Register<Health>(new Guid("A31A8B02-8522-4D9B-B0A0-78C2E68DEB13"));
        await using var scheduler = new Scheduler(Math.Min(4, Environment.ProcessorCount));
        foreach (int count in new[] { 5000, 10000, 50000 }) await Measure(runtime, scheduler, count);
    }

    private static async Task Measure(EcsRuntime runtime, Scheduler scheduler, int count)
    {
        using var world = runtime.CreateWorld();
        var entities = new Entity[count];
        world.Create(new EntityType(runtime.Types.Get<Position>(), runtime.Types.Get<Velocity>(), runtime.Types.Get<Health>()), entities);
        Initialize(world, runtime);
        var move = new Move { DeltaTime = 0.5f };
        var combat = new Combat();
        using var movement = scheduler.CreateQuery(world, in move);
        using var damage = scheduler.CreateQuery(world, in combat);
        var options = new TaskOptions { BatchSize = 512 };
        const int warmup = 16, frames = 120;
        var samples = new double[frames];
        long before = 0;
        int gen0Before = 0, gen1Before = 0, gen2Before = 0;
        for (int frame = -warmup; frame < frames; frame++)
        {
            if (frame == 0)
            {
                before = GC.GetTotalAllocatedBytes(true);
                gen0Before = GC.CollectionCount(0); gen1Before = GC.CollectionCount(1); gen2Before = GC.CollectionCount(2);
            }
            long start = Stopwatch.GetTimestamp();
            var movementHandle = scheduler.Dispatch(movement, in move, options);
            var combatHandle = scheduler.Dispatch(damage, in combat, options);
            await movementHandle;
            await combatHandle;
            if (frame >= 0) samples[frame] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        long allocated = GC.GetTotalAllocatedBytes(true) - before;
        int gen0 = GC.CollectionCount(0) - gen0Before, gen1 = GC.CollectionCount(1) - gen1Before, gen2 = GC.CollectionCount(2) - gen2Before;
        await scheduler.SyncAllAsync();
        Verify(world, entities, warmup + frames);
        Array.Sort(samples);
        Console.WriteLine($"Tasks kernel {count:N0} entities / 2 stages / 512 batch: median {samples[frames / 2]:F3} ms, p95 {samples[(int)(frames * 0.95)]:F3} ms, managed {allocated / (double)frames:F0} B/step, GC {gen0}/{gen1}/{gen2}; deterministic columns verified.");
    }

    private static void Initialize(World world, EcsRuntime runtime)
    {
        using var query = world.CreateQuery(new QueryDescription().WithAll(runtime.Types.Get<Position>()));
        foreach (var view in query)
        {
            var velocity = view.WriteOwned<Velocity>();
            var health = view.WriteOwned<Health>();
            for (int i = 0; i < view.Count; i++) { velocity[i].Value = new(2, 4, 6); health[i].Value = 100; }
        }
    }

    private static void Verify(World world, Entity[] entities, int steps)
    {
        var expected = new Vector3(steps, steps * 2, steps * 3);
        foreach (var entity in entities)
        {
            if (world.Read<Position>(entity).Value != expected || world.Read<Health>(entity).Value != 100 - steps * 0.125f)
                throw new InvalidOperationException("Scheduled dependent movement/combat columns diverged.");
        }
    }

    private struct Move : IQueryJob
    {
        public float DeltaTime;
        public void Build(JobAccessBuilder builder) => builder.Write<Position>().Read<Velocity>();
        public void Execute(in JobContext context)
        {
            var positions = context.View.WriteOwned<Position>();
            var velocities = context.View.ReadOwned<Velocity>();
            for (int i = 0; i < context.Count; i++) positions[i].Value += velocities[i].Value * DeltaTime;
        }
    }

    private struct Combat : IQueryJob
    {
        public void Build(JobAccessBuilder builder) => builder.Read<Position>().Write<Health>();
        public void Execute(in JobContext context)
        {
            var positions = context.View.ReadOwned<Position>();
            var health = context.View.WriteOwned<Health>();
            for (int i = 0; i < context.Count; i++) if (positions[i].Value.X > 0) health[i].Value -= 0.125f;
        }
    }
}
