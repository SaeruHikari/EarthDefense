using System.Runtime.CompilerServices;
using Sugoi.Data;
using Sugoi.Tasks;

internal static class WeakSignalChecks
{
    private struct Cell { public int Value; }

    internal static async Task RunAsync()
    {
        WeakJobEvent expiredEvent = CreateExpiredEvent();
        WeakJobCounter expiredCounter = CreateExpiredCounter();
        Collect();
        Check(!expiredEvent.IsAlive && !expiredCounter.IsAlive, "Weak wrappers unexpectedly own their targets.");
        using var runtime = new EcsRuntime();
        runtime.Types.Register<Cell>(new Guid("73c6b038-2b4b-48d3-a2c0-0a2d28c41130"));
        using var world = runtime.CreateWorld();
        Entity entity = world.Create(new EntityType(runtime.Types.Get<Cell>()));
        await using var scheduler = new Scheduler(2);
        var job = new Increment();
        var expired = scheduler.Dispatch(world, in job, new TaskOptions
        {
            AfterEvents = [expiredEvent], AfterCounters = [expiredCounter],
            OnFinishEvents = [expiredEvent], OnFinishCounters = [expiredCounter]
        });
        await expired.AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Check(Read(world, entity) == 1, "Expired dependencies/notifications were not skipped.");

        var directEvent = CreateEventWait();
        var directCounter = CreateCounterWait();
        Collect();
        Check(directEvent.Weak.IsAlive && directCounter.Weak.IsAlive, "A live direct wait lost its signal owner.");
        Signal(directEvent.Weak); Decrement(directCounter.Weak);
        await Task.WhenAll(directEvent.Wait, directCounter.Wait).WaitAsync(TimeSpan.FromSeconds(10));

        var eventGate = await StartWeakEventGate(scheduler, world);
        var counterGate = await StartWeakCounterGate(scheduler, world);
        Collect();
        Check(eventGate.Weak.IsAlive && counterGate.Weak.IsAlive && !eventGate.Handle.IsCompleted && !counterGate.Handle.IsCompleted,
            "A suspended scheduler wait was collected or incorrectly skipped.");
        Signal(eventGate.Weak); Decrement(counterGate.Weak);
        await Task.WhenAll(eventGate.Handle.AsTask(), counterGate.Handle.AsTask()).WaitAsync(TimeSpan.FromSeconds(10));
        Check(Read(world, entity) == 3 && world.ActiveUsers == 0, "Weak admission waits did not resume and release their World.");
        await ExecutorRootsSuspendedWait();
        Console.WriteLine("Weak signals: expired options skipped, direct waits hold owners, suspended executor/scheduler operations survive forced GC.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakJobEvent CreateExpiredEvent() => new(new JobEvent());
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakJobCounter CreateExpiredCounter() => new(new JobCounter(1));
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakJobEvent Weak, Task Wait) CreateEventWait()
    { var owner = new JobEvent(); return (new(owner), owner.WaitAsync().AsTask()); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakJobCounter Weak, Task Wait) CreateCounterWait()
    { var owner = new JobCounter(1); return (new(owner), owner.WaitAsync().AsTask()); }

    private static async Task<(WeakJobEvent Weak, JobHandle Handle)> StartWeakEventGate(Scheduler scheduler, World world)
    {
        var owner = new JobEvent(); var job = new Increment();
        var handle = scheduler.Dispatch(world, in job, new TaskOptions { AfterEvents = [owner], WorkerIndex = 0 });
        // FIFO on worker zero: its admission root has entered the wait before this marker can execute.
        await scheduler.Executor.RunAsync(static () => { }, 0);
        GC.KeepAlive(owner);
        return (new(owner), handle);
    }
    private static async Task<(WeakJobCounter Weak, JobHandle Handle)> StartWeakCounterGate(Scheduler scheduler, World world)
    {
        var owner = new JobCounter(1); var job = new Increment();
        var handle = scheduler.Dispatch(world, in job, new TaskOptions { AfterCounters = [owner], WorkerIndex = 0 });
        await scheduler.Executor.RunAsync(static () => { }, 0);
        GC.KeepAlive(owner);
        return (new(owner), handle);
    }

    private static async Task ExecutorRootsSuspendedWait()
    {
        var executor = new WorkerExecutor(1);
        var published = new TaskCompletionSource<WeakJobEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task operation = executor.RunAsync(async () =>
        {
            var owner = new JobEvent();
            published.SetResult(new(owner));
            await owner.WaitAsync();
        }).AsTask();
        WeakJobEvent weak = await published.Task;
        await executor.RunAsync(static () => { }, 0);
        Collect();
        // On regression, report immediately: disposing a lost-root executor would itself wait forever.
        Check(weak.IsAlive && !operation.IsCompleted, "Executor retained only a counter, allowing the suspended async operation to disappear.");
        Signal(weak);
        await operation.WaitAsync(TimeSpan.FromSeconds(10));
        await executor.DisposeAsync();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Signal(WeakJobEvent weak)
    { if (!weak.TryGetTarget(out var target)) throw new InvalidOperationException("Waiting event owner vanished."); target.Set(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Decrement(WeakJobCounter weak)
    { if (!weak.TryGetTarget(out var target)) throw new InvalidOperationException("Waiting counter owner vanished."); target.Decrement(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect()
    { for (int i = 0; i < 3; i++) { GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true); GC.WaitForPendingFinalizers(); } }
    private static int Read(World world, Entity entity) => world.Read<Cell>(entity).Value;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private struct Increment : IQueryJob
    {
        public void Build(JobAccessBuilder builder) => builder.Write<Cell>();
        public void Execute(in JobContext context)
        { var values = context.View.WriteOwned<Cell>(); for (int i = 0; i < values.Length; i++) values[i].Value++; }
    }
}
