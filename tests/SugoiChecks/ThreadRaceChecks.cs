using System.Collections.Concurrent;
using Sugoi.Data;
using Sugoi.Tasks;

internal static class ThreadRaceChecks
{
    private struct Cell { public int Value; }
    private struct Packet { public int Id; public bool Owned; }

    internal static async Task RunAsync()
    {
        await ConcurrentWaitRegistration();
        using var runtime = new EcsRuntime();
        runtime.Types.Register<Cell>(new Guid("5c348c61-6bcb-47fd-a3c3-31022e500a31"));
        await MultiProducerSubmissions(runtime);
        await ConcurrentMessages(runtime);
        await FinishPublication(runtime);
        await CloseDuringPrepare(runtime);
        await WorkerSyncDuringClose();
        await ParallelBatchCloseRace();
        Console.WriteLine("Thread races: concurrent wait registration, multi-producer hazards/messages, finish publication, preparation/close, and executor shutdown passed.");
    }

    private static async Task ConcurrentWaitRegistration()
    {
        const int rounds = 512, observers = 8;
        var events = Enumerable.Range(0, rounds).Select(_ => new JobEvent()).ToArray();
        var counters = Enumerable.Range(0, rounds).Select(_ => new JobCounter(1)).ToArray();
        using var start = new ManualResetEventSlim();
        int observations = 0;
        var threads = new Task[observers + 1];
        for (int observer = 0; observer < observers; observer++)
            threads[observer] = StartThread(() =>
            {
                start.Wait();
                for (int round = 0; round < rounds; round++)
                {
                    events[round].WaitAsync().AsTask().GetAwaiter().GetResult();
                    counters[round].WaitAsync().AsTask().GetAwaiter().GetResult();
                    Interlocked.Increment(ref observations);
                    if ((round & 15) == 0) Thread.Yield();
                }
            });
        threads[^1] = StartThread(() =>
        {
            start.Wait();
            for (int round = 0; round < rounds; round++)
            {
                events[round].Set(); counters[round].Decrement();
                Thread.Yield();
            }
        });
        start.Set();
        await Task.WhenAll(threads).WaitAsync(TimeSpan.FromSeconds(20));
        Check(observations == rounds * observers, "Concurrent completion/registration lost or duplicated an event/counter wake.");
    }

    private static async Task MultiProducerSubmissions(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        var entities = new Entity[4096];
        world.Create(new EntityType(runtime.Types.Get<Cell>()), entities);
        using var query = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Cell>()));
        await using var scheduler = new Scheduler(4);
        const int producers = 8, submissions = 64;
        var handles = new ConcurrentQueue<JobHandle>();
        using var start = new ManualResetEventSlim();
        var threads = new Task[producers];
        for (int producer = 0; producer < producers; producer++)
            threads[producer] = StartThread(() =>
            {
                start.Wait();
                var job = new IncrementJob();
                for (int iteration = 0; iteration < submissions; iteration++)
                {
                    handles.Enqueue(scheduler.Dispatch(query, in job, new TaskOptions { BatchSize = 512 }));
                    if ((iteration & 3) == 0) Thread.Yield();
                }
            });
        start.Set();
        await Task.WhenAll(threads).WaitAsync(TimeSpan.FromSeconds(20));
        var complete = handles.Select(h => h.AsTask()).ToArray();
        await Task.WhenAll(complete).WaitAsync(TimeSpan.FromSeconds(20));
        await scheduler.SyncAllAsync();
        VerifyCells(world, entities, producers * submissions);
        Check(world.ActiveUsers == 0 && handles.Count == producers * submissions, "Concurrent submission cleanup leaked leases or handles.");
    }

    private static async Task ConcurrentMessages(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        var target = world.Create(new EntityType(runtime.Types.Get<Cell>()));
        using var query = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Cell>()));
        using var bus = new MessageBus(world);
        int copied = 0, destroyed = 0;
        bus.Registry.Register(new MessageRegistration<Packet>
        {
            Id = new Guid("93938628-61b2-4424-a380-2a41e50a8fdf"),
            Copy = (in Packet source, ref Packet destination) => { Interlocked.Increment(ref copied); destination = source; },
            Move = (ref Packet source, ref Packet destination) => { destination = source; source = default; },
            Destroy = (ref Packet value) => { if (value.Owned) Interlocked.Increment(ref destroyed); value = default; }
        });
        const int producers = 8, packets = 512;
        using var subscription = bus.Subscribe<Packet>(query, capacity: producers * packets);
        using var start = new ManualResetEventSlim();
        int producersRemaining = producers;
        var producerTasks = new Task[producers];
        for (int producer = 0; producer < producers; producer++)
        {
            int owner = producer;
            producerTasks[producer] = StartThread(() =>
            {
                start.Wait();
                try
                {
                    for (int index = 0; index < packets; index++)
                    {
                        var payload = new Packet { Id = owner * packets + index, Owned = true };
                        Check(bus.Send(target, in payload) == 1, "A nonfull queue rejected a producer message.");
                        if ((index & 7) == 0) Thread.Yield();
                    }
                }
                finally { Interlocked.Decrement(ref producersRemaining); }
            });
        }
        var seen = new HashSet<int>();
        Task consumer = StartThread(() =>
        {
            start.Wait();
            while (Volatile.Read(ref producersRemaining) != 0 || subscription.Count != 0)
            {
                using var batch = subscription.Consume();
                for (int i = 0; i < batch.Count; i++)
                    Check(batch.Targets[i] == target && seen.Add(batch.Values[i].Id), "MPSC consumption duplicated/corrupted a target payload.");
                if (batch.Count == 0) Thread.Yield();
            }
        });
        start.Set();
        await Task.WhenAll(producerTasks.Append(consumer)).WaitAsync(TimeSpan.FromSeconds(20));
        Check(seen.Count == producers * packets && copied == seen.Count && destroyed == copied && subscription.DroppedCount == 0,
            "MPSC copies, consumed IDs and destruction counts diverged under concurrent send/consume.");
    }

    private static async Task FinishPublication(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        var entity = world.Create(new EntityType(runtime.Types.Get<Cell>()));
        await using var scheduler = new Scheduler(2);
        var signals = Enumerable.Range(0, 8192).Select(_ => new JobEvent()).ToArray();
        var observer = StartThread(() =>
        {
            signals[0].WaitAsync().AsTask().GetAwaiter().GetResult();
            // This observer can win the race against publication of the remaining finish events.
            world.Destroy(entity);
        });
        var job = new IncrementJob();
        var handle = scheduler.Dispatch(world, in job, new TaskOptions { OnFinishEvents = signals.Select(signal => (WeakJobEvent)signal).ToArray() });
        await Task.WhenAll(handle.AsTask(), observer).WaitAsync(TimeSpan.FromSeconds(20));
        Check(!world.Exists(entity) && signals[^1].IsSet, "External finish notification preceded release of native-data ownership.");
    }

    private static async Task CloseDuringPrepare(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        var entity = world.Create(new EntityType(runtime.Types.Get<Cell>()));
        var scheduler = new Scheduler(2);
        var preparing = new JobEvent();
        using var release = new ManualResetEventSlim();
        var job = new PreparingJob { Preparing = preparing, Release = release };
        JobHandle handle = default;
        Task submit = StartThread(() => handle = scheduler.Dispatch(world, in job));
        await preparing.WaitAsync();
        Task closing = scheduler.DisposeAsync().AsTask();
        Task[] observers = Enumerable.Range(0, 16).Select(_ => scheduler.SyncAllAsync().AsTask()).ToArray();
        try
        {
            Check(!closing.IsCompleted, "Scheduler disposal missed a still-running Prepare.");
            bool rejected = false;
            var late = new IncrementJob();
            // Probe closed admission. An incorrectly accepted handle is still drained by the already-started disposal.
            try { _ = scheduler.Dispatch(world, in late); } catch (ObjectDisposedException) { rejected = true; }
            Check(rejected, "Closing scheduler admitted a new host submission.");
        }
        finally { release.Set(); }
        await submit;
        await Task.WhenAll(observers.Append(closing).Append(handle.AsTask())).WaitAsync(TimeSpan.FromSeconds(20));
        Check(ReadCell(world, entity) == 1 && world.ActiveUsers == 0, "Already-started preparation did not finish and release ownership during close.");
    }

    private static async Task WorkerSyncDuringClose()
    {
        var scheduler = new Scheduler(2);
        var started = new JobEvent();
        var proceed = new JobEvent();
        Task root = scheduler.Executor.RunAsync(async () =>
        {
            started.Set();
            await proceed.WaitAsync();
            await scheduler.SyncAllAsync();
        }).AsTask();
        await started.WaitAsync();
        Task closing = scheduler.DisposeAsync().AsTask();
        proceed.Set();
        await Task.WhenAll(root, closing).WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static async Task ParallelBatchCloseRace()
    {
        // Race host batch admission against close on real threads. A rejected suffix must still join admitted callbacks.
        for (int round = 0; round < 24; round++)
        {
            var executor = new WorkerExecutor(8);
            using var start = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int started = 0, finished = 0;
            Task? batch = null;
            Task submitting = StartThread(() =>
            {
                start.Wait();
                batch = executor.ForAsync(64, _ =>
                {
                    Interlocked.Increment(ref started);
                    Check(release.Wait(10000), "Close-race callback was not released.");
                    Interlocked.Increment(ref finished);
                }).AsTask();
            });
            start.Set();
            while (Volatile.Read(ref started) == 0 && !submitting.IsCompleted) Thread.Yield();
            Task closing = executor.DisposeAsync().AsTask();
            await submitting;
            try
            {
                Check(batch is not null, "Host batch did not return an ownership handle.");
                if (Volatile.Read(ref started) != 0)
                    Check(!batch!.IsCompleted, "Partially admitted batch returned while an accepted callback still owned its output.");
            }
            finally { release.Set(); }
            try { await batch!.WaitAsync(TimeSpan.FromSeconds(20)); } catch (ObjectDisposedException) { }
            await closing.WaitAsync(TimeSpan.FromSeconds(20));
            Check(started == finished, "Executor close abandoned accepted callbacks.");
        }
    }

    private static Task StartThread(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true }.Start();
        return completion.Task;
    }

    private static int ReadCell(World world, Entity entity) => world.Read<Cell>(entity).Value;
    private static void VerifyCells(World world, Entity[] entities, int expected)
    { foreach (var entity in entities) Check(ReadCell(world, entity) == expected, "Concurrent non-atomic RMW jobs lost component updates."); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private struct IncrementJob : IQueryJob
    {
        public void Build(JobAccessBuilder builder) => builder.Write<Cell>();
        public void Execute(in JobContext context)
        {
            var cells = context.View.WriteOwned<Cell>();
            for (int i = 0; i < cells.Length; i++)
            {
                int value = cells[i].Value;
                if (i == 0) Thread.Yield();
                cells[i].Value = value + 1;
            }
        }
    }

    private struct PreparingJob : IQueryJob
    {
        public JobEvent Preparing;
        public ManualResetEventSlim Release;
        public void Build(JobAccessBuilder builder) => builder.Write<Cell>();
        public void Prepare(int count) { Preparing.Set(); Check(Release.Wait(10000), "Closing Prepare was not released."); }
        public void Execute(in JobContext context)
        { var cells = context.View.WriteOwned<Cell>(); for (int i = 0; i < cells.Length; i++) cells[i].Value++; }
    }
}
