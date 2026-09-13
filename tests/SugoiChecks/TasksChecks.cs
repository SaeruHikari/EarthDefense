using Sugoi.Data;
using Sugoi.Tasks;

internal static class TasksChecks
{
    private struct Number { public int Value; }
    private struct RegionA { }
    private struct RegionB { }
    private struct Ping { public int Value; }

    internal static async Task RunAsync()
    {
        await WaitersAndAffinity();
        using var runtime = new EcsRuntime();
        runtime.Types.Register(new ComponentRegistration<Number> { Id = new Guid("2FCEED75-6D35-46A9-97FB-CFE1E49D1C11") });
        runtime.Types.Register(new ComponentRegistration<RegionA> { Id = new Guid("8F3F12CB-82E0-45F3-8369-398BAED9C911"), Kind = ComponentKind.Tag });
        runtime.Types.Register(new ComponentRegistration<RegionB> { Id = new Guid("D4280492-97E7-4619-8CC0-BA9F459C7E34"), Kind = ComponentKind.Tag });
        using var world = runtime.CreateWorld();
        var type = runtime.Types.Get<Number>();
        var tagA = runtime.Types.Get<RegionA>();
        var tagB = runtime.Types.Get<RegionB>();
        var entitiesA = new Entity[256];
        var entitiesB = new Entity[256];
        world.Create(new EntityType(type, tagA), entitiesA);
        world.Create(new EntityType(type, tagB), entitiesB);
        await using var scheduler = new Scheduler(4);
        await DisjointWriterFrontiers(world, scheduler, tagA, tagB, entitiesA[0]);
        await ReadWriteFrontiers(world, scheduler, tagA, entitiesA[0]);
        await RandomDependencies(world, scheduler, tagA, tagB);
        await AdmissionAndPrepare(world, scheduler, tagA, entitiesA[0]);
        await ExplicitOwnership(world, scheduler, entitiesA);
        await BatchesAndSelfConflict(world, scheduler, tagA);
        await UsageLifetime(world, scheduler, type, entitiesA[0]);
        await PreparingLifetime(world, scheduler, type);
        await Messages(world, scheduler, type, tagA, entitiesA[0]);
        await scheduler.SyncAllAsync();
        await FailureDrains(runtime);
        await CanceledGateDrains(runtime);
        await OwnedAndShared(runtime);
        await ThreadRaceChecks.RunAsync();
        Console.WriteLine("Tasks: waiters, owned continuations, online hazards, gates, batches, messages and failure cleanup passed.");
    }

    private static async Task WaitersAndAffinity()
    {
        var signal = new JobEvent();
        var waits = Enumerable.Range(0, 32).Select(_ => signal.WaitAsync().AsTask()).ToArray();
        signal.Set();
        await Task.WhenAll(waits).WaitAsync(TimeSpan.FromSeconds(5));
        signal.Reset();
        Check(!signal.IsSet, "Manual reset must clear the event.");
        var next = signal.WaitAsync().AsTask();
        Check(!next.IsCompleted, "A new reset generation must await its own signal.");
        signal.Set(); await next;
        var counter = new JobCounter(2);
        var first = counter.WaitAsync().AsTask();
        var second = counter.WaitAsync().AsTask();
        counter.Decrement(); Check(!first.IsCompleted, "Counter must wait for zero.");
        counter.Decrement(); await Task.WhenAll(first, second);
        var nonzero = counter.WaitNonzeroAsync().AsTask();
        Check(!nonzero.IsCompleted, "Inverse counter waits for nonzero.");
        counter.Add(); await nonzero; counter.Decrement();

        await using var executor = new WorkerExecutor(2);
        var resume = new JobEvent();
        var suspended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int before = -1, after = -2;
        var owned = executor.RunAsync(async () =>
        {
            before = executor.CurrentWorkerIndex;
            suspended.SetResult();
            await resume.WaitAsync();
            after = executor.CurrentWorkerIndex;
            int total = 0;
            await executor.ForAsync(100, _ => Interlocked.Increment(ref total));
            Check(total == 100, "Nested async join must not block the worker budget.");
        }, 1).AsTask();
        await suspended.Task; resume.Set();
        await owned.WaitAsync(TimeSpan.FromSeconds(5));
        Check(before == 1 && after == 1, "Continuation must resume on its original executor worker.");
    }

    private static async Task DisjointWriterFrontiers(World world, Scheduler scheduler, ComponentType tagA, ComponentType tagB, Entity entityA)
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int observed = -1;
        var writerA = new ProbeJob { Tag = tagA, UseTag = true, Value = 31, Before = () => { started.Set(); Check(release.Wait(5000), "Blocked writer was not released."); } };
        var first = scheduler.Dispatch(world, in writerA);
        Check(started.Wait(5000), "Writer A did not start.");
        try
        {
            var writerB = new ProbeJob { Tag = tagB, UseTag = true, Value = 71 };
            var second = scheduler.Dispatch(world, in writerB);
            var readerA = new ProbeJob { Tag = tagA, UseTag = true, ReadOnly = true, ReadValue = value => observed = value };
            var third = scheduler.Dispatch(world, in readerA);
            await scheduler.FlushDispatchAsync();
            await second.AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Check(!third.IsCompleted && observed == -1, "W(A), W(B), R(A) must retain the earlier disjoint writer frontier.");
            release.Set();
            await Task.WhenAll(first.AsTask(), third.AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
            Check(observed == 31 && ReadNumber(world, entityA) == 31, "Reader must observe completed writer A.");
        }
        finally { release.Set(); }
    }

    private static async Task RandomDependencies(World world, Scheduler scheduler, ComponentType tagA, ComponentType tagB)
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writer = new ProbeJob { Tag = tagA, UseTag = true, Value = 9, Before = () => { started.Set(); Check(release.Wait(5000), "Random dependency writer timed out."); } };
        var first = scheduler.Dispatch(world, in writer);
        Check(started.Wait(5000), "Random predecessor did not start.");
        try
        {
            int ran = 0;
            var reader = new ProbeJob { Tag = tagB, UseTag = true, Random = true, ReadOnly = true, Before = () => Interlocked.Increment(ref ran) };
            var second = scheduler.Dispatch(world, in reader);
            await scheduler.FlushDispatchAsync();
            Check(!second.IsCompleted && ran == 0, "Random access must wait the whole predecessor despite disjoint query groups.");
            release.Set();
            await Task.WhenAll(first.AsTask(), second.AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
            Check(ran != 0, "Random reader should eventually execute.");
        }
        finally { release.Set(); }
    }

    private static async Task ReadWriteFrontiers(World world, Scheduler scheduler, ComponentType tag, Entity entity)
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var reader = new ProbeJob { Tag = tag, UseTag = true, ReadOnly = true, Before = () => { started.Set(); Check(release.Wait(5000), "WAR reader timed out."); } };
        var readHandle = scheduler.Dispatch(world, in reader);
        Check(started.Wait(5000), "WAR reader did not start.");
        try
        {
            var firstWriter = new ProbeJob { Tag = tag, UseTag = true, Value = 61 };
            var secondWriter = new ProbeJob { Tag = tag, UseTag = true, Value = 62 };
            var first = scheduler.Dispatch(world, in firstWriter);
            var second = scheduler.Dispatch(world, in secondWriter);
            await scheduler.FlushDispatchAsync();
            Check(!first.IsCompleted && !second.IsCompleted, "WAR and WAW must both retain the running reader frontier.");
            release.Set();
            await Task.WhenAll(readHandle.AsTask(), first.AsTask(), second.AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
            Check(ReadNumber(world, entity) == 62, "Successive same-chunk writers must commit in admission order.");
        }
        finally { release.Set(); }
    }

    private static async Task AdmissionAndPrepare(World world, Scheduler scheduler, ComponentType tag, Entity entity)
    {
        var gate = new JobEvent();
        int prepared = -1;
        var delayed = new ProbeJob { Tag = tag, UseTag = true, Value = 42, Prepared = count => prepared = count };
        var delayedHandle = scheduler.Dispatch(world, in delayed, new TaskOptions { AfterEvents = [gate] });
        Check(prepared == 256, "prepare must run synchronously before admission gates.");
        var immediate = new ProbeJob { Tag = tag, UseTag = true, Value = 11 };
        await scheduler.Dispatch(world, in immediate).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await scheduler.FlushDispatchAsync();
        Check(!delayedHandle.IsCompleted && ReadNumber(world, entity) == 11, "A gated task must not enter hazard history or block a later ready task.");
        var sync = scheduler.SyncWorldAsync(world).AsTask();
        Check(!sync.IsCompleted, "World synchronization includes gated tasks, unlike dispatch flush.");
        gate.Set(); await delayedHandle.AsTask().WaitAsync(TimeSpan.FromSeconds(5)); await sync;
        Check(ReadNumber(world, entity) == 42, "The task admitted later must write later.");
    }

    private static async Task ExplicitOwnership(World world, Scheduler scheduler, Entity[] source)
    {
        var gate = new JobEvent();
        int prepares = 0;
        var input = new[] { source[1], source[2] };
        var job = new ProbeJob { Value = 123, Prepared = _ => prepares++ };
        var handle = scheduler.DispatchEntities(world, input, in job, new TaskOptions { AfterEvents = [gate] });
        input[0] = source[50]; input[1] = source[51];
        gate.Set(); await handle;
        Check(prepares == 0, "Explicit entity dispatch preserves the reference API's separate prepare behavior.");
        Check(ReadNumber(world, source[1]) == 123 && ReadNumber(world, source[50]) != 123, "Dispatch must own the caller's entity list before returning.");
    }

    private static async Task BatchesAndSelfConflict(World world, Scheduler scheduler, ComponentType tag)
    {
        int touched = 0;
        var job = new ProbeJob { Tag = tag, UseTag = true, Value = 19, Counted = count => Interlocked.Add(ref touched, count) };
        await scheduler.Dispatch(world, in job, new TaskOptions { BatchSize = 17 });
        Check(touched == 256, "Batch tails must execute every entity once.");
        int active = 0, max = 0;
        var randomWriter = new ProbeJob
        {
            Random = true, Value = 5,
            Before = () => { int now = Interlocked.Increment(ref active); max = Math.Max(max, now); },
            After = () => Interlocked.Decrement(ref active)
        };
        await scheduler.Dispatch(world, in randomWriter, new TaskOptions { BatchSize = 7 });
        Check(max == 1 && active == 0, "Random-write self conflicts must serialize units and internal batches.");
    }

    private static async Task Messages(World world, Scheduler scheduler, ComponentType type, ComponentType tag, Entity entity)
    {
        using var query = world.CreateQuery(new QueryDescription().WithAll(type, tag));
        using var bus = new MessageBus(world);
        int copies = 0, destroyed = 0;
        bus.Registry.Register(new MessageRegistration<Ping>
        {
            Id = new Guid("8a4322ad-1d5c-4d4c-97fd-4250c0bc77ff"),
            Copy = (in Ping source, ref Ping destination) => { Interlocked.Increment(ref copies); destination = source; },
            Move = (ref Ping source, ref Ping destination) => { destination = source; source = default; },
            Destroy = (ref Ping value) => { if (value.Value != 0) Interlocked.Increment(ref destroyed); value = default; }
        });
        using var subscription = bus.Subscribe<Ping>(query, capacity: 2);
        var ping = new Ping { Value = 8 };
        Check(bus.Send(entity, in ping) == 1 && bus.Send(entity, in ping) == 1 && bus.Send(entity, in ping) == 1, "A full ring must extend into overflow storage.");
        Check(subscription.DroppedCount == 0 && copies == 3 && subscription.OverflowMessageCount == 1, "Copies and overflow accounting must be exact.");
        var consume = new PingJob();
        await scheduler.DispatchMessages<Ping, PingJob>(subscription, in consume);
        Check(subscription.Count == 0 && destroyed == 3 && ReadNumber(world, entity) == 29, "Message tasks consume aligned targets and release each owned payload after calculation.");

        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var producer = new ProbeJob { Tag = tag, UseTag = true, Value = 1, Before = () => { started.Set(); Check(release.Wait(5000), "Message timing producer timed out."); }, After = () => bus.Send(entity, in ping) };
        var producerHandle = scheduler.Dispatch(world, in producer);
        Check(started.Wait(5000), "Message producer did not start.");
        try
        {
            var earlyConsumer = scheduler.DispatchMessages<Ping, PingJob>(subscription, in consume);
            await scheduler.FlushDispatchAsync();
            release.Set(); await Task.WhenAll(producerHandle.AsTask(), earlyConsumer.AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
            Check(subscription.Count == 1, "Component dependencies alone do not move message materialization after the producer.");
            await scheduler.DispatchMessages<Ping, PingJob>(subscription, in consume, new TaskOptions { After = new[] { producerHandle } });
            Check(subscription.Count == 0 && destroyed == 4, "Explicit admission gate makes newly sent messages visible to this dispatch.");
        }
        finally { release.Set(); }
    }

    private static async Task UsageLifetime(World world, Scheduler scheduler, ComponentType type, Entity entity)
    {
        var query = world.CreateQuery(new QueryDescription().WithAll(type));
        var gate = new JobEvent();
        var job = new ProbeJob { ReadOnly = true };
        var handle = scheduler.Dispatch(query, in job, new TaskOptions { AfterEvents = [gate] });
        bool structureRejected = false, queryRejected = false;
        try { world.Destroy(entity); } catch (InvalidOperationException) { structureRejected = true; }
        try { query.Dispose(); } catch (InvalidOperationException) { queryRejected = true; }
        Check(structureRejected && queryRejected, "A gated submission must retain World addresses and Query lifetime before it enters analysis.");
        var releasing = scheduler.ReleaseQueryAsync(query).AsTask();
        Check(!releasing.IsCompleted, "Query release must wait submitted gated uses.");
        gate.Set(); await handle; await releasing;
        Check(world.Exists(entity), "Rejected structure changes must have no effect.");
    }

    private static async Task PreparingLifetime(World world, Scheduler scheduler, ComponentType type)
    {
        var query = world.CreateQuery(new QueryDescription().WithAll(type));
        using var preparing = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var job = new ProbeJob
        {
            ReadOnly = true,
            Prepared = _ =>
            {
                preparing.Set();
                Check(release.Wait(5000), "Prepare callback was not released.");
                // Host preparation may synchronize prior submitted work; no scheduler mutex may be held across this callback.
                scheduler.SyncAllAsync().AsTask().GetAwaiter().GetResult();
            }
        };
        var dispatching = Task.Run(() => scheduler.Dispatch(query, in job));
        Check(preparing.Wait(5000), "Preparation did not enter its callback.");
        var disposing = scheduler.ReleaseQueryAsync(query).AsTask();
        Check(!disposing.IsCompleted, "Query disposal must see a submission whose Prepare has not returned yet.");
        release.Set();
        var handle = await dispatching.WaitAsync(TimeSpan.FromSeconds(5));
        await handle;
        await disposing.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task FailureDrains(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        world.Create(new EntityType(runtime.Types.Get<Number>()));
        var scheduler = new Scheduler(2);
        var cleanup = new JobCounter(1);
        var signal = new JobEvent();
        var failure = new ProbeJob { Before = () => throw new InvalidOperationException("intentional task failure") };
        var handle = scheduler.Dispatch(world, in failure, new TaskOptions { OnFinishCounters = [cleanup], OnFinishEvents = [signal] });
        bool sawFailure = false;
        try { await handle; } catch (InvalidOperationException) { sawFailure = true; }
        Check(sawFailure && cleanup.Count == 0 && signal.IsSet, "Failed jobs still perform completion notifications.");
        try { await scheduler.SyncAllAsync(); } catch (AggregateException) { }
        await scheduler.DisposeAsync();
    }

    private static async Task CanceledGateDrains(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        world.Create(new EntityType(runtime.Types.Get<Number>()));
        using var cancellation = new CancellationTokenSource();
        var scheduler = new Scheduler(1);
        var job = new ProbeJob { Value = 99 };
        var gate = new JobEvent();
        var handle = scheduler.Dispatch(world, in job, new TaskOptions { AfterEvents = [gate], CancellationToken = cancellation.Token });
        cancellation.Cancel();
        try { await handle; } catch (OperationCanceledException) { }
        Check(handle.IsCanceled && world.ActiveUsers == 0, "Canceling admission must release leases and complete the handle as canceled.");
        GC.KeepAlive(gate);
        try { await scheduler.SyncAllAsync(); } catch (AggregateException) { }
        await scheduler.DisposeAsync();
    }

    private static async Task OwnedAndShared(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        Entity owner = world.Create(new EntityType(runtime.Types.Get<Number>(), runtime.Types.Get<RegionB>()));
        world.Set(owner, new Number { Value = 27 });
        Entity child = world.Create(new EntityType(runtime.Types.Get<RegionA>()).WithMeta(owner));
        using var inherited = world.CreateQuery(new QueryDescription().WithAll(runtime.Types.Get<Number>()));
        using var owned = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Number>()));
        Check(inherited.Count == 2 && inherited.Matches(child), "WithAll must retain shared/meta inheritance semantics.");
        Check(owned.Count == 1 && !owned.Matches(child), "WithOwned must require a physical component column.");
        await using var scheduler = new Scheduler(2);
        int ownedCalls = 0;
        var ownedJob = new ProbeJob { UseTag = true, Tag = runtime.Types.Get<RegionA>(), ReadOnly = true, Before = () => ownedCalls++ };
        await scheduler.Dispatch(world, in ownedJob);
        Check(ownedCalls == 0, "Owned task binding must not run on a group that satisfies its component only through shared data.");

        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writer = new ProbeJob { UseTag = true, Tag = runtime.Types.Get<RegionB>(), Value = 28, Before = () => { started.Set(); Check(release.Wait(5000), "Shared predecessor timed out."); } };
        var predecessor = scheduler.Dispatch(world, in writer);
        Check(started.Wait(5000), "Shared predecessor did not start.");
        try
        {
            int observed = -1;
            var sharedJob = new SharedJob { Tag = runtime.Types.Get<RegionA>(), Observed = value => observed = value };
            var shared = scheduler.Dispatch(world, in sharedJob);
            await scheduler.FlushDispatchAsync();
            Check(!shared.IsCompleted && observed == -1, "Shared read must wait the component owner's other chunk.");
            release.Set();
            await Task.WhenAll(predecessor.AsTask(), shared.AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
            Check(observed == 28, "Shared binding must read the meta owner's updated component.");
        }
        finally { release.Set(); }
    }

    private static int ReadNumber(World world, Entity entity) => world.Read<Number>(entity).Value;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private struct ProbeJob : IQueryJob
    {
        public bool UseTag, ReadOnly, Random;
        public ComponentType Tag;
        public int Value;
        public Action? Before, After;
        public Action<int>? Prepared, ReadValue, Counted;
        public void Build(JobAccessBuilder builder)
        {
            if (UseTag) builder.Description.WithAll(Tag);
            if (Random) { builder.Has<Number>(); if (ReadOnly) builder.RandomRead<Number>(); else builder.RandomWrite<Number>(); }
            else if (ReadOnly) builder.Read<Number>(); else builder.Write<Number>();
        }
        public void Prepare(int count) => Prepared?.Invoke(count);
        public void Execute(in JobContext context)
        {
            Before?.Invoke();
            if (ReadOnly) { if (context.Count != 0) ReadValue?.Invoke(context.View.ReadOwned<Number>()[0].Value); }
            else { var values = context.View.WriteOwned<Number>(); for (int i = 0; i < values.Length; i++) values[i].Value = Value; }
            Counted?.Invoke(context.Count);
            After?.Invoke();
        }
    }

    private struct PingJob : IMessageJob<Ping>
    {
        public void Build(JobAccessBuilder builder) => builder.Write<Number>();
        public void Execute(in MessageJobContext<Ping> context)
        {
            var values = context.View.WriteOwned<Number>();
            for (int i = 0; i < context.Count; i++) values[i].Value += context.Messages[i].Value;
        }
    }

    private struct SharedJob : IQueryJob
    {
        public ComponentType Tag;
        public Action<int> Observed;
        public void Build(JobAccessBuilder builder) => builder.ReadShared<Number>().Description.WithAll(Tag);
        public void Execute(in JobContext context) => Observed(context.View.ReadShared<Number>().Value);
    }
}
