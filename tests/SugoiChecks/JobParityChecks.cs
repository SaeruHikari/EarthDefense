using System.Collections.Concurrent;
using Sugoi.Data;
using Sugoi.Tasks;

internal static class JobParityChecks
{
    private struct Value { public int Number; }
    private struct Tag { }
    private static void Check(bool condition, string text) { if (!condition) throw new InvalidOperationException("Job parity: " + text); }
    private static EcsRuntime Runtime()
    {
        var runtime = new EcsRuntime();
        runtime.Types.Register<Value>(new Guid("ef100001-0000-0000-0000-000000000001"));
        runtime.Types.Register<Tag>(new Guid("ef100002-0000-0000-0000-000000000001"), kind: ComponentKind.Tag);
        return runtime;
    }

    public static async Task RunAsync()
    {
        await AsyncSuspensionAndDependencies();
        await AsyncBatchIndices();
        await BodyOwnership();
        await AsyncOwnedBody();
        await QueryMetaAndExplicitSelection();
        await AsyncForJoin();
        await MoreSuspendedBatchesThanWorkers();
        await CreationDoesNotHoldSchedulerLock();
        PrefetchPolicy();
        StagingPhaseVisibility();
        await DiagnosticNames();
        Console.WriteLine("PASS source Job parity: in-body suspension, ownership, task indices, reused meta, explicit lists and async joins");
    }

    private struct Increment : IQueryJob
    {
        public void Build(JobAccessBuilder builder) => builder.Write<Value>();
        public void Execute(in JobContext context)
        {
            var values = context.View.WriteOwned<Value>();
            for (int i = 0; i < values.Length; i++) values[i].Number++;
        }
    }

    private struct AwaitingWriter : IAsyncQueryJob
    {
        public JobEvent Entered, Resume;
        public WorkerExecutor Executor;
        public int[] Threads;
        public void Build(JobAccessBuilder builder) => builder.Write<Value>();
        public async ValueTask ExecuteAsync(AsyncJobContext context)
        {
            Threads[0] = Environment.CurrentManagedThreadId;
            Entered.Set();
            await Resume.WaitAsync(context.CancellationToken);
            Threads[1] = Environment.CurrentManagedThreadId;
            IncrementValues(context);
        }
        private static void IncrementValues(AsyncJobContext context)
        {
            var values = context.Borrow().WriteOwned<Value>();
            for (int i = 0; i < values.Length; i++) values[i].Number += 10;
        }
    }

    private static async Task AsyncSuspensionAndDependencies()
    {
        using var runtime = Runtime();
        using var world = runtime.CreateWorld();
        using var other = runtime.CreateWorld();
        var entity = world.Create(new EntityType(runtime.Types.Get<Value>()));
        var independent = other.Create(new EntityType(runtime.Types.Get<Value>()));
        await using var scheduler = new Scheduler(1);
        var entered = new JobEvent();
        var resume = new JobEvent();
        var threads = new int[2];
        var writer = scheduler.Dispatch(world, new AwaitingWriter { Entered = entered, Resume = resume, Threads = threads, Executor = scheduler.Executor });
        await entered.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var dependent = scheduler.Dispatch(world, new Increment());
        await scheduler.Dispatch(other, new Increment()).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Check(!dependent.IsCompleted && world.ActiveUsers > 0 && other.Read<Value>(independent).Number == 1, "await releases worker but retains ECS hazards/lifetime");
        using var stage = new StagingWorld(runtime);
        stage.Add(entity, new Value { Number = 100 });
        bool rejected = false;
        try { stage.Apply(world); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "staging apply cannot race a suspended ECS job");
        resume.Set();
        await writer;
        await dependent;
        Check(threads[0] == threads[1] && world.Read<Value>(entity).Number == 11, "resume on owned worker and dependency waits actual async completion");
        stage.Apply(world);
        Check(world.Read<Value>(entity).Number == 100, "rejected apply retains its staged update until safe");
    }

    private struct IndexedJob : IAsyncQueryJob
    {
        public ConcurrentDictionary<int, int> Indices;
        public void Build(JobAccessBuilder builder) => builder.Write<Value>();
        public async ValueTask ExecuteAsync(AsyncJobContext context)
        {
            await Task.Yield();
            if (!Indices.TryAdd(context.TaskIndex, context.Index)) throw new InvalidOperationException("Repeated TaskIndex");
            Store(context);
        }
        private static void Store(AsyncJobContext context)
        {
            var values = context.Borrow().WriteOwned<Value>();
            for (int i = 0; i < values.Length; i++) values[i].Number = context.Index + i + 1;
        }
    }

    private static async Task AsyncBatchIndices()
    {
        using var runtime = Runtime(); using var world = runtime.CreateWorld();
        var entities = new Entity[32]; world.Create(new EntityType(runtime.Types.Get<Value>()), entities);
        await using var scheduler = new Scheduler(4);
        var indices = new ConcurrentDictionary<int, int>();
        await scheduler.Dispatch(world, new IndexedJob { Indices = indices }, new TaskOptions { BatchSize = 4 });
        Check(indices.Count == 8 && indices.Keys.Order().SequenceEqual(Enumerable.Range(0, 8)), "independent batch execution ordinal");
        Check(indices.Values.Order().SequenceEqual(Enumerable.Range(0, 8).Select(i => i * 4)), "entity start index remains distinct");
        for (int i = 0; i < entities.Length; i++) Check(world.Read<Value>(entities[i]).Number == i + 1, "async range alignment");
    }

    private sealed class Owners
    {
        private int _next;
        public int Copies, Releases;
        public readonly ConcurrentDictionary<int, byte> Live = new();
        public int Copy()
        {
            int id = Interlocked.Increment(ref _next);
            Check(Live.TryAdd(id, 0), "unique body ownership");
            Interlocked.Increment(ref Copies); return id;
        }
        public void Release(int id)
        {
            Check(id != 0 && Live.TryRemove(id, out _), "body released exactly once");
            Interlocked.Increment(ref Releases);
        }
    }

    private struct OwnedJob : IQueryJob, IJobCloneable<OwnedJob>, IDisposable
    {
        public Owners Owners;
        public int Token;
        public bool Fail;
        public OwnedJob CloneForBatch() => new() { Owners = Owners, Token = Owners.Copy(), Fail = Fail };
        public void Dispose() => Owners.Release(Token);
        public void Build(JobAccessBuilder builder) => builder.Write<Value>();
        public void Execute(in JobContext context)
        {
            Check(Owners.Live.ContainsKey(Token), "live batch body");
            if (Fail) throw new InvalidOperationException("Deliberate batch failure");
            var values = context.View.WriteOwned<Value>();
            for (int i = 0; i < values.Length; i++) values[i].Number++;
        }
    }

    private static async Task BodyOwnership()
    {
        foreach (bool fail in new[] { false, true })
        {
            using var runtime = Runtime(); using var world = runtime.CreateWorld();
            world.Create(new EntityType(runtime.Types.Get<Value>()), new Entity[12]);
            await using var scheduler = new Scheduler(4);
            var owners = new Owners();
            var job = scheduler.Dispatch(world, new OwnedJob { Owners = owners, Fail = fail }, new TaskOptions { BatchSize = 3 });
            try { await job; Check(!fail, "expected failure surfaced"); }
            catch (InvalidOperationException) when (fail) { }
            try { await scheduler.SyncAllAsync(); } catch (AggregateException) when (fail) { }
            Check(owners.Live.IsEmpty && owners.Copies == owners.Releases, "submission and all accepted batch bodies release on success/failure");
            if (!fail) Check(owners.Copies == 5, "one submission plus four batch copies");
        }
    }

    private sealed class AsyncOwner : IAsyncQueryJob, IJobCloneable<AsyncOwner>, IDisposable
    {
        public required Owners Owners;
        private int _token, _phase;
        public AsyncOwner CloneForBatch() => new() { Owners = Owners, _token = Owners.Copy() };
        public void Build(JobAccessBuilder builder) => builder.Write<Value>();
        public async ValueTask ExecuteAsync(AsyncJobContext context)
        {
            _phase = 1;
            await Task.Yield();
            _phase = 2;
            Check(Owners.Live.ContainsKey(_token), "resource survives await");
        }
        public void Dispose()
        {
            Check(_phase != 1, "async ownership cleanup waits final state");
            Owners.Release(_token);
        }
    }

    private static async Task AsyncOwnedBody()
    {
        using var runtime = Runtime(); using var world = runtime.CreateWorld();
        world.Create(new EntityType(runtime.Types.Get<Value>()), new Entity[12]);
        await using var scheduler = new Scheduler(3);
        var owners = new Owners();
        await scheduler.Dispatch(world, new AsyncOwner { Owners = owners }, new TaskOptions { BatchSize = 3 });
        Check(owners.Copies == 5 && owners.Releases == 5 && owners.Live.IsEmpty, "owning async class has complete clone/await/dispose lifetime");
    }

    private struct MetaJob : IQueryJob
    {
        public Entity Meta;
        public void Build(JobAccessBuilder builder) => builder.Write<Value>().WithMeta(Meta);
        public void Execute(in JobContext context)
        {
            var values = context.View.WriteOwned<Value>();
            for (int i = 0; i < values.Length; i++) values[i].Number++;
        }
    }
    private struct FilteredJob : IQueryJob
    {
        public void Build(JobAccessBuilder builder) => builder.Write<Value>().Has<Tag>();
        public void Execute(in JobContext context)
        {
            var values = context.View.WriteOwned<Value>();
            for (int i = 0; i < values.Length; i++) values[i].Number += 10;
        }
    }

    private static async Task QueryMetaAndExplicitSelection()
    {
        using var runtime = Runtime(); using var world = runtime.CreateWorld();
        var a = world.Create(EntityType.Empty); var b = world.Create(EntityType.Empty);
        var x = world.Create(new EntityType([runtime.Types.Get<Value>()], [a]));
        var y = world.Create(new EntityType([runtime.Types.Get<Value>()], [b]));
        await using var scheduler = new Scheduler(2);
        using var query = scheduler.CreateQuery(world, new MetaJob { Meta = a });
        await scheduler.Dispatch(query, new MetaJob { Meta = b });
        Check(world.Read<Value>(x).Number == 0 && world.Read<Value>(y).Number == 1, "reused query applies current Build meta");
        await scheduler.DispatchEntities(world, new[] { x, y, x }, new FilteredJob(), new TaskOptions { BatchSize = 1 });
        Check(world.Read<Value>(x).Number == 20 && world.Read<Value>(y).Number == 11, "explicit list bypasses query filters and preserves duplicates");
    }

    private static void PrefetchPolicy()
    {
        var types = Enumerable.Range(0, 5).Select(i => new ComponentType((uint)i)).ToArray();
        var accesses = types.Select(t => new ComponentAccess(t, AccessMode.Sequential, true, false, PrefetchMode.Auto)).ToArray();
        Check(Prefetch.BuildPlan(accesses, default, false).Streams.Length == 0, "source Auto disables more than four streams");
        Check(Prefetch.BuildPlan(accesses[..1], default, true).Streams.Length == 0, "source Auto disables explicit-entity dispatch");
        Check(Prefetch.BuildPlan(accesses[..1], new TaskOptions { BatchSize = 127 }, false).Streams.Length == 0, "source Auto disables small batches");
        var mixed = new[]
        {
            new ComponentAccess(types[0], AccessMode.Sequential, true, false, PrefetchMode.Force),
            new ComponentAccess(types[1], AccessMode.Sequential, true, false, PrefetchMode.Auto),
            new ComponentAccess(types[2], AccessMode.Sequential, true, false, PrefetchMode.Off),
            new ComponentAccess(types[3], AccessMode.Random, true, false, PrefetchMode.Force)
        };
        var promoted = Prefetch.BuildPlan(mixed, default, false);
        Check(promoted.Mode == PrefetchMode.Force && promoted.Streams.SequenceEqual(types[..2]), "source merges per-access prefetch mode at task scope");
        var forced = Prefetch.BuildPlan(mixed, new TaskOptions { Prefetch = PrefetchMode.Force }, true);
        Check(forced.Streams.SequenceEqual(types[..3]), "source Force override includes sequential Off streams, excludes Random");
    }

    private static async Task DiagnosticNames()
    {
        using var runtime = Runtime(); using var world = runtime.CreateWorld();
        world.Create(new EntityType(runtime.Types.Get<Value>()), new Entity[8]);
        await using var scheduler = new Scheduler(2);
        var stopped = new ConcurrentBag<string>();
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => source.Name == JobDiagnostics.SourceName,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity.DisplayName)
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);
        await scheduler.Dispatch(world, new Increment(), new TaskOptions { BatchSize = 2, DebugName = "Named artillery update" });
        Check(stopped.Count == 4 && stopped.All(name => name == "Named artillery update"), "named source-equivalent batch profiling");
    }

    private struct BarrierJob : IAsyncQueryJob
    {
        public JobCounter Entered;
        public JobEvent Release;
        public void Build(JobAccessBuilder builder) => builder.Write<Value>();
        public async ValueTask ExecuteAsync(AsyncJobContext context)
        {
            Entered.Decrement();
            await Release.WaitAsync();
            Store(context);
        }
        private static void Store(AsyncJobContext context) => context.Borrow().WriteOwned<Value>()[0].Number++;
    }

    private static async Task MoreSuspendedBatchesThanWorkers()
    {
        using var runtime = Runtime(); using var world = runtime.CreateWorld();
        var entities = new Entity[16]; world.Create(new EntityType(runtime.Types.Get<Value>()), entities);
        await using var scheduler = new Scheduler(2);
        var entered = new JobCounter(16); var release = new JobEvent();
        var handle = scheduler.Dispatch(world, new BarrierJob { Entered = entered, Release = release }, new TaskOptions { BatchSize = 1 });
        bool reached;
        try { await entered.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); reached = true; }
        catch (TimeoutException) { reached = false; }
        release.Set();
        await handle;
        Check(reached, "sixteen suspended ECS batches must all enter on two workers before their barrier is released");
        foreach (var entity in entities) Check(world.Read<Value>(entity).Number == 1, "barrier batch completed");
    }

    private struct JoiningCreation : ICreationJob
    {
        public Scheduler Scheduler;
        public JobEvent Release;
        public int Initialized;
        public void Build(CreationBuilder builder) => builder.Add<Value>();
        public void Execute(in JobContext context)
        {
            Release.Set();
            Scheduler.SyncAllAsync().AsTask().GetAwaiter().GetResult();
            Initialized += context.Count;
        }
    }

    private static async Task CreationDoesNotHoldSchedulerLock()
    {
        using var runtime = Runtime(); using var target = runtime.CreateWorld(); using var other = runtime.CreateWorld();
        other.Create(new EntityType(runtime.Types.Get<Value>()));
        await using var scheduler = new Scheduler(2);
        var entered = new JobEvent(); var release = new JobEvent();
        var writer = scheduler.Dispatch(other, new AwaitingWriter { Entered = entered, Resume = release, Threads = new int[2], Executor = scheduler.Executor });
        await entered.WaitAsync();
        var creationTask = Task.Run(() =>
        {
            var initializer = new JoiningCreation { Scheduler = scheduler, Release = release };
            scheduler.CreateEntities(target, new Entity[2], ref initializer);
            Check(initializer.Initialized == 2, "Creation ref state is returned to its caller");
        });
        await creationTask.WaitAsync(TimeSpan.FromSeconds(5));
        await writer;
    }

    private sealed class SharedVisibilitySink(Entity child) : IStructuralChangeSink
    {
        public bool SawAddition;
        public void ComponentAdded(World world, Entity entity, ComponentType type, nint payload)
        {
            SawAddition = true;
            Check(!world.Has<Value>(child), "Apply sink sees shared owner changes from the preceding query phase");
        }
    }

    private static void StagingPhaseVisibility()
    {
        using var runtime = Runtime(); using var world = runtime.CreateWorld();
        var value = runtime.Types.Get<Value>(); var tag = runtime.Types.Get<Tag>();
        var owner = world.Create(new EntityType(value));
        var child = world.Create(new EntityType([tag], [owner]));
        var trigger = world.Create(new EntityType(value, tag));
        Check(world.Has<Value>(child), "prewarm inherited component cache");
        using var query = world.CreateQuery(new QueryDescription().WithOwned(value).Without(tag));
        using var stage = new StagingWorld(runtime);
        stage.Remove(query, value);
        stage.Add(trigger, new Value { Number = 77 });
        var sink = new SharedVisibilitySink(child);
        stage.Apply(world, sink);
        Check(sink.SawAddition && world.Read<Value>(trigger).Number == 77, "existing-value sink executes after query delta");
    }

    private static async Task AsyncForJoin()
    {
        await using var executor = new WorkerExecutor(1);
        var slots = new int[64];
        await executor.ForAsync(64, async i => { await Task.Yield(); slots[i] = 1; });
        Check(slots.All(v => v == 1), "asynchronous parallel join completes every accepted child");
    }
}
