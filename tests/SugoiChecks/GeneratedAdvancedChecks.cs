using System.Collections.Concurrent;
using Sugoi.Data;
using Sugoi.Tasks;
using SugoiFixtures;

internal static class GeneratedAdvancedChecks
{
    internal static async Task RunAsync()
    {
        await ComponentAndAsyncBindings();
        await CreationBindings();
        await MessageBindings();
        ResourceScanning();
        Console.WriteLine("PASS generated advanced bindings / async ECS / task ownership / message registration / resource scanning");
    }

    private static async Task ComponentAndAsyncBindings()
    {
        using var runtime = new EcsRuntime();
        Sugoi.Generated.SugoiFixturesModule.Register(runtime.Types);
        using var world = runtime.CreateWorld();
        var owner = world.Create(new EntityType(runtime.Types.Get<SharedScale>()));
        world.Get<SharedScale>(owner).Value = 4;
        var fast = new Entity[33];
        var slow = new Entity[17];
        var common = new EntityType([runtime.Types.Get<Position>(), runtime.Types.Get<ChunkStatistics>(), runtime.Types.GetBuffer<Link>()], [owner]);
        world.Create(common.With(runtime.Types.Get<Velocity>()), fast);
        world.Create(common, slow);
        foreach (var entity in fast) world.Get<Velocity>(entity).X = 3;
        await using var scheduler = new Scheduler(2);
        await scheduler.Dispatch(world, new AdvancedMoveJob(), new TaskOptions { BatchSize = 8 });
        foreach (var entity in fast) Check(world.Read<Position>(entity).X == 12, "owned/optional/shared wrapper result");
        foreach (var entity in slow) Check(world.Read<Position>(entity).X == 8, "missing optional column keeps fallback behavior");
        foreach (var entity in fast.Concat(slow))
        {
            Check(world.GetBuffer<Link>(entity).Count == 1 && world.GetBuffer<Link>(entity)[0].Target == entity, "generated buffer writes bind each row");
            Check(world.Read<ChunkStatistics>(entity).Ticks > 0, "generated chunk write uses singleton semantics");
        }
        await scheduler.Dispatch(world, new OptionalWriteJob(), new TaskOptions { BatchSize = 8 });
        foreach (var entity in fast) Check(world.Read<Velocity>(entity).Y == 3, "optional writes execute only when present");
        int observed = 0;
        await scheduler.Dispatch(world, new ReadBindingsJob { Observe = count => Interlocked.Add(ref observed, count) }, new TaskOptions { BatchSize = 8 });
        Check(observed == fast.Length + slow.Length, "buffer/chunk read and random read are fully generated");
        await scheduler.Dispatch(world, new RandomBindingsJob { VelocityTarget = fast[0], ScaleTarget = owner }, new TaskOptions { BatchSize = 8 });
        Check(world.Read<Velocity>(fast[0]).Z == 50 && world.Read<SharedScale>(owner).Value == 54, "generated random write/readwrite keeps whole-task self-conflict safety");

        var gate = new JobCounter(1);
        var entered = new JobEvent();
        var batches = new ConcurrentBag<(int Task, int Start)>();
        var asynchronous = scheduler.Dispatch(world, new AsyncMoveJob { Gate = gate, Entered = entered, Observe = (task, start) => batches.Add((task, start)) }, new TaskOptions { BatchSize = 8 });
        await entered.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        var dependent = scheduler.Dispatch(world, new MoveJob { Delta = 0 });
        Check(!asynchronous.IsCompleted && !dependent.IsCompleted, "async ECS body retains its component dependencies across await");
        gate.Decrement();
        await asynchronous;
        await dependent;
        foreach (var entity in fast.Concat(slow)) Check(world.Read<Position>(entity).Z == 5, "async helper rebinds native views after continuation");
        Check(batches.Count > 1 && batches.Select(batch => batch.Task).Distinct().Count() == batches.Count && batches.Min(batch => batch.Task) == 0, "TaskIndex is distinct from entity offset and unique per batch");

        OwnedJob.Clones = OwnedJob.Disposals = 0;
        using (var owned = OwnedJob.Create())
        {
            await scheduler.Dispatch(world, owned, new TaskOptions { BatchSize = 8 });
            Check(OwnedJob.Clones > 1 && OwnedJob.Disposals == OwnedJob.Clones, "generated owning struct clone/dispose covers submission and every batch");
        }
        Check(OwnedJob.Disposals == OwnedJob.Clones + 1, "caller retains independent task ownership");
        AsyncOwnedJob.Clones = AsyncOwnedJob.Disposals = 0;
        using (var owned = AsyncOwnedJob.Create())
        {
            await scheduler.Dispatch(world, owned, new TaskOptions { BatchSize = 8 });
            Check(AsyncOwnedJob.Clones > 1 && AsyncOwnedJob.Disposals == AsyncOwnedJob.Clones, "generated owning async class keeps one instance across await and cleanup");
        }
        foreach (var entity in fast.Concat(slow)) Check(world.Read<Position>(entity).Y == 36, "owned sync/async job bodies ran with valid native resources");
    }

    private static async Task MessageBindings()
    {
        using var runtime = new EcsRuntime();
        Sugoi.Generated.SugoiFixturesModule.Register(runtime.Types);
        using var world = runtime.CreateWorld();
        var entity = world.Create(new EntityType(runtime.Types.Get<Position>()));
        using var query = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Position>()));
        using var bus = new MessageBus(world);
        Sugoi.Generated.SugoiFixturesMessages.Register(bus.Registry);
        Sugoi.Generated.SugoiFixturesMessages.Register(bus.Registry);
        using var damage = bus.Subscribe<DamageMessage>(query);
        using var acknowledgment = bus.Subscribe<AckMessage>(query);
        await using var scheduler = new Scheduler(2);
        bus.Send(entity, new DamageMessage { Amount = 9 });
        await scheduler.DispatchMessages(damage, new ReceiveDamageJob());
        Check(world.Read<Position>(entity).X == 9, "generated message consumer aligns component and payload spans");
        using (var batch = acknowledgment.Consume())
            Check(batch.Count == 1 && batch.Targets[0] == entity && batch.Values[0].Amount == 9, "generated typed sender uses the bound World bus");

        var gate = new JobCounter(1);
        var entered = new JobEvent();
        bus.Send(entity, new DamageMessage { Amount = 11 });
        var asyncJob = scheduler.DispatchMessages(damage, new AsyncDamageJob { Gate = gate, Entered = entered });
        await entered.WaitAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        Check(!asyncJob.IsCompleted, "async message task retains owned payload while waiting");
        gate.Decrement();
        await asyncJob;
        Check(world.Read<Position>(entity).Y == 11, "message binding can be reacquired after await");
        using var ownedSubscription = bus.Subscribe<OwnedMessage>(query);
        OwnedMessage.Copies = OwnedMessage.Moves = OwnedMessage.Disposals = 0;
        var owned = OwnedMessage.Create(74);
        bus.SendMove(entity, ref owned);
        Check(owned.Pointer == 0 && OwnedMessage.Moves > 0, "generated message move hook transfers native ownership");
        using (var batch = ownedSubscription.Consume()) Check(batch.Count == 1 && batch.Values[0].Pointer != 0, "owned message survives native queue and batch transfer");
        Check(OwnedMessage.Disposals > 0, "generated message destroy hook is invoked on consumed ownership");
    }

    private static async Task CreationBindings()
    {
        using var runtime = new EcsRuntime();
        Sugoi.Generated.SugoiFixturesModule.Register(runtime.Types);
        using var world = runtime.CreateWorld();
        var owner = world.Create(new EntityType(runtime.Types.Get<SharedScale>()));
        world.Get<SharedScale>(owner).Value = 6;
        await using var scheduler = new Scheduler(2);
        var creation = new FleetCreation { Owner = owner };
        var created = new Entity[12000];
        scheduler.CreateEntities(world, created, ref creation);
        Check(creation.Initialized == created.Length && creation.Calls > 1, "generated creation body keeps mutable state across native range callbacks");
        Check(((IJobDebugInfo)creation).DebugName == "Initialize fleet", "generated creation debug name");
        for (int i = 0; i < created.Length; i++)
        {
            Check(world.Read<Position>(created[i]).X == i && world.Read<Velocity>(created[i]).X == 6, "creation spans initialize before returning to caller");
            Check(world.GetEntityType(created[i]).Contains(runtime.Types.Get<Selected>()) && world.GetEntityType(created[i]).MetaEntities[0] == owner, "creation component/meta attributes build the final signature");
        }
        var reserved = new Entity[19];
        world.ReserveEntities(reserved);
        var identities = reserved.ToArray();
        creation.Initialized = creation.Calls = 0;
        scheduler.CreateReservedEntities(world, reserved, ref creation);
        Check(creation.Initialized == reserved.Length && reserved.SequenceEqual(identities), "reserved creation uses the original identities and same mutable body");
        foreach (var entity in reserved) Check(world.GetBuffer<Link>(entity)[0].Target == entity, "reserved creation buffer binder");
    }

    private static void ResourceScanning()
    {
        using var runtime = new EcsRuntime();
        Sugoi.Generated.SugoiFixturesModule.Register(runtime.Types);
        using var world = runtime.CreateWorld();
        Guid[] ids = Enumerable.Range(1, 8).Select(index => new Guid(index, 0, 0, new byte[8])).ToArray();
        var entities = new Entity[2];
        world.Create(new EntityType(runtime.Types.Get<ResourceSet>(), runtime.Types.GetBuffer<ResourceElement>(), runtime.Types.Get<ResourceHeader>()), entities);
        for (int i = 0; i < entities.Length; i++)
        {
            ref var resources = ref world.Get<ResourceSet>(entities[i]);
            resources.Primary = new(ids[0]);
            resources.Nested = new ResourceNested(new(ids[1]));
            resources.Extras[0] = new(ids[2]);
            resources.Extras[1] = default;
            resources.SetEffect(ids[3]);
            var buffer = world.GetBuffer<ResourceElement>(entities[i]);
            for (int j = 0; j < 5; j++) buffer.Add(new ResourceElement { Asset = new(ids[4]) });
        }
        world.Get<ResourceHeader>(entities[0]).Asset = new(ids[5]);
        var custom = world.Create(new EntityType(runtime.Types.Get<CustomResource>(), runtime.DisabledType));
        world.Set(custom, new CustomResource(ids[6]));
        var seen = new Dictionary<Guid, int>();
        world.ScanResourceReferences(id => { seen.TryGetValue(id, out int count); seen[id] = count + 1; });
        Check(seen.Count == 7 && !seen.ContainsKey(Guid.Empty), "resource scanner skips empty handles and preserves custom/disabled values");
        for (int i = 0; i < 4; i++) Check(seen[ids[i]] == 2, "ordinary/nested/inline/marked GUID resource path");
        Check(seen[ids[4]] == 10, "buffer scanner visits every heap element");
        Check(seen[ids[5]] == 1 && seen[ids[6]] == 1, "chunk resource scanned once and custom hook once");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Advanced generated runtime: " + message);
    }
}
