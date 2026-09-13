using System.Runtime.CompilerServices;
using Sugoi.Data;

/// <summary>Behavior ports of runtime/tests/ecs/ecs_staging_tests.cpp plus ownership and concurrency boundaries.</summary>
internal static unsafe class StagingChecks
{
    private struct Position { public int X; public int Y; }
    private struct Score { public int Value; }
    private struct Velocity { public int Value; }
    private struct Link { public Entity Target; }
    private struct ArrayItem { public Entity Target; public int Value; }
    private struct Tag { }
    private struct Owned { public int Token; }
    private struct GateValue { public int Value; }
    private struct Asset { public Guid Id; }
    private readonly record struct Types(ComponentType Position, ComponentType Score, ComponentType Velocity, ComponentType Link, ComponentType Array, ComponentType Tag);
    private static Types Register(EcsRuntime runtime) => new(
        runtime.Types.Register<Position>(new Guid("d8000001-0000-0000-0000-000000000001")),
        runtime.Types.Register<Score>(new Guid("d8000002-0000-0000-0000-000000000001")),
        runtime.Types.Register<Velocity>(new Guid("d8000003-0000-0000-0000-000000000001")),
        runtime.Types.Register(new ComponentRegistration<Link> { Id = new("d8000004-0000-0000-0000-000000000001"), Remap = static (ref Link v, EntityRemapper m) => v.Target = m(v.Target) }),
        runtime.Types.RegisterBuffer(new ComponentRegistration<ArrayItem> { Id = new("d8000005-0000-0000-0000-000000000001"), Remap = static (ref ArrayItem v, EntityRemapper m) => v.Target = m(v.Target) }, 2),
        runtime.Types.Register<Tag>(new Guid("d8000006-0000-0000-0000-000000000001"), kind: ComponentKind.Tag));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Staging: " + message);
    }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Staging expected " + typeof(T).Name + ": " + message);
    }
    private sealed class Sink(Types types) : IStructuralChangeSink
    {
        internal readonly Dictionary<int, Entity> Positions = [];
        internal readonly List<string> Events = [];
        internal readonly Dictionary<Entity, int> Removed = [];
        internal readonly Dictionary<Entity, int> Destroyed = [];
        internal readonly Dictionary<Entity, int> AddedScores = [];
        public void ComponentAdded(World world, Entity entity, ComponentType type, nint payload)
        {
            Check(!entity.IsTransient && world.Exists(entity), "sink addition sees real, attached identity");
            Check(payload != 0, "tag additions must not emit source-style payload notifications");
            if (type == types.Position) Positions[Unsafe.Read<Position>((void*)payload).X] = entity;
            if (type == types.Score)
            {
                AddedScores[entity] = Unsafe.Read<Score>((void*)payload).Value;
                Check(world.Read<Score>(entity).Value == AddedScores[entity], "added sink payload is already installed");
            }
            Events.Add("add:" + entity.Value + ":" + type.Value);
        }
        public void ComponentRemoved(World world, Entity entity, ComponentType type, nint payload)
        {
            if (type == types.Score && payload != 0)
            {
                Removed[entity] = Unsafe.Read<Score>((void*)payload).Value;
                Check(world.Read<Score>(entity).Value == Removed[entity], "removed payload remains readable in callback");
            }
            Events.Add("remove:" + entity.Value + ":" + type.Value);
        }
        public void EntityDestroyed(World world, Entity entity)
        {
            Check(world.Exists(entity), "destroy callback precedes removal");
            Destroyed[entity] = world.Has<Score>(entity) ? world.Read<Score>(entity).Value : -1;
            Events.Add("destroy:" + entity.Value);
        }
    }
    public static void Run()
    {
        ExistingAndSinks(); SpawnsAndReferences(); ConcurrentAndGrowth(); CopyUpdateReplace(); LifetimesAndBoundaries(); ProducerGateAndResources();
        Console.WriteLine("PASS staging: native rows, coalescing, all Apply phases, update/replace, sinks, concurrent producers");
    }

    private static void ExistingAndSinks()
    {
        using var runtime = new EcsRuntime(); var types = Register(runtime);
        using var world = runtime.CreateWorld(); using var staging = new StagingWorld(runtime, 1, 1);
        Entity[] entities = new Entity[8]; world.Create(new EntityType(types.Position), entities);
        staging.Add(entities[0], new Score { Value = 10 }); staging.Add(entities[2], new Score { Value = 20 });
        Check(!world.Has<Score>(entities[0]), "staging does not mutate main storage before Apply");
        staging.Apply(world);
        Check(world.Read<Score>(entities[0]).Value == 10 && world.Read<Score>(entities[2]).Value == 20 && !world.Has<Score>(entities[1]), "noncontiguous entity patches remain exact");
        staging.Add(entities[0], new Score { Value = 30 }); staging.Add(entities[0], new Score { Value = 31 });
        staging.Remove<Score>(entities[0]); staging.Add(entities[0], new Score { Value = 32 });
        staging.Apply(world);
        Check(world.Read<Score>(entities[0]).Value == 32, "add/remove/add coalesces and ordinary overwrite keeps final value");
        var sink = new Sink(types);
        staging.Remove<Score>(entities[0]); staging.Destroy(entities[2]); staging.Add(entities[2], new Score { Value = 999 });
        staging.Apply(world, sink);
        Check(sink.Removed[entities[0]] == 32 && sink.Destroyed[entities[2]] == 20, "removed and destroyed callbacks expose pre-change values");
        Check(!world.Has<Score>(entities[0]) && !world.Exists(entities[2]), "destroy dominates queued payload changes");
        Check(!sink.Events.Any(e => e.StartsWith("add:")), "destroyed record never publishes unused payload");
        using var query = world.CreateQuery(QueryDescription.Create().WithAll(types.Position));
        staging.Add(entities[1], new Score { Value = 88 }); staging.Apply(world);
        staging.Remove(query, types.Score); sink.Events.Clear(); staging.Apply(world, sink);
        Check(sink.Events.Count == 0 && !world.Has<Score>(entities[1]), "query-scoped structural changes do not emit per-record sink callbacks");
        staging.AddTag(query, types.Tag);
        Throws<InvalidOperationException>(query.Dispose, "pending query delta owns query lifetime");
        Entity transient = staging.NewEntity(); staging.Add(transient, new Position { X = 70 });
        staging.Apply(world, sink);
        Check(world.Has(entities[1], types.Tag) && !world.Has(sink.Positions[70], types.Tag), "query scope evaluates before staged spawns");
        staging.Remove(query, types.Tag); staging.AddTag(query, types.Tag); staging.Remove(query, types.Tag);
        staging.Apply(world);
        Check(!world.Has(entities[1], types.Tag), "query tag delta coalescing");
        Check(staging.RecordCount == 0 && staging.TransientCount == 0, "epoch state resets after Apply");
    }

    private static void SpawnsAndReferences()
    {
        using var runtime = new EcsRuntime(); var types = Register(runtime);
        using var world = runtime.CreateWorld(); using var staging = new StagingWorld(runtime, 1, 1);
        Entity host = world.Create(new EntityType(types.Position));
        Entity meta = staging.NewEntity(), a = staging.NewEntity(), b = staging.NewEntity();
        staging.Add(meta, new Position { X = 100 }); staging.Add(meta, new Score { Value = 77 });
        staging.Add(a, new Position { X = 101 }); staging.Add(b, new Position { X = 102 });
        staging.Add(a, new Link { Target = b }); staging.Add(b, new Link { Target = a });
        staging.Add(host, new Link { Target = a });
        staging.AddMeta(a, meta); staging.AddMeta(b, a); staging.AddMeta(a, host); staging.RemoveMeta(a, host);
        staging.Add(a, new ArrayItem { Target = b, Value = 1 });
        staging.Add(a, new ArrayItem { Target = meta, Value = 2 });
        staging.Add(a, new ArrayItem { Target = Entity.Null, Value = 3 });
        staging.Add(a, new Tag());
        var sink = new Sink(types); staging.Apply(world, sink);
        Entity actualA = sink.Positions[101], actualB = sink.Positions[102], actualMeta = sink.Positions[100];
        Check(sink.AddedScores[actualMeta] == 77, "spawn sink reports the staged score on its real identity");
        Check(world.Read<Link>(host).Target == actualA && world.Read<Link>(actualA).Target == actualB && world.Read<Link>(actualB).Target == actualA, "existing and cyclic spawned references map to real identities");
        Check(world.GetEntityType(actualA).MetaEntities.SequenceEqual(new[] { actualMeta }) && world.Read<Score>(actualA).Value == 77, "transient meta remaps and canceled meta disappears");
        var values = world.GetBuffer<ArrayItem>(actualA);
        Check(values.Count == 3 && values[0].Target == actualB && values[1].Target == actualMeta && values[2].Target.IsNull, "heap buffer and Null reference mapping");
        staging.SetBuffer(actualA, new ArrayItem[] { new() { Value = 41 }, new() { Target = host, Value = 42 } });
        staging.Apply(world);
        Check(world.GetBuffer<ArrayItem>(actualA).Count == 2 && world.GetBuffer<ArrayItem>(actualA)[0].Value == 41, "SetBuffer replaces instead of appending");
        staging.DestroyOwned(actualMeta); staging.Apply(world, sink);
        Check(world.Exists(actualMeta) && !world.Exists(actualA) && !world.Exists(actualB), "meta destruction recursively visits ownership without deleting root");
        staging.Add(host, new Link()); staging.Apply(world);
        Check(world.Read<Link>(host).Target.IsNull, "Null reference in an ordinary existing component remains empty");
        Entity issuedOnly = staging.NewEntity();
        Check(issuedOnly.IsTransient && issuedOnly.Generation == uint.MaxValue && !world.Exists(issuedOnly), "Entity64 transient is outside ordinary Registry");
        int before = world.EntityCount; staging.Apply(world);
        Check(world.EntityCount == before, "NewEntity without a record does not spawn");
        Entity removedAll = staging.NewEntity(); staging.Add(removedAll, new Score { Value = 1 }); staging.Remove<Score>(removedAll);
        staging.Apply(world);
        Check(world.EntityCount == before + 1, "record with empty final signature creates an empty entity");
    }

    private static void ConcurrentAndGrowth()
    {
        using var runtime = new EcsRuntime(); var types = Register(runtime);
        using var world = runtime.CreateWorld(); using var staging = new StagingWorld(runtime, 1, 1);
        const int count = 4096;
        Parallel.For(0, count, i =>
        {
            Entity entity = staging.NewEntity();
            staging.Add(entity, new Position { X = i, Y = i * 2 });
            staging.Add(entity, new ArrayItem { Value = i * 3 }); staging.Add(entity, new Tag());
        });
        Check(staging.RecordCount == count && staging.TransientCount == count && staging.AllocatedPayloadBytes > 0, "concurrent row initialization grows beyond hints");
        staging.Apply(world);
        using var query = world.CreateQuery(QueryDescription.Create().WithAll(types.Position, types.Array, types.Tag));
        long sum = 0;
        foreach (var view in query)
            for (int i = 0; i < view.Count; i++)
            {
                var entity = view.Entities[i]; int x = view.ReadOwned<Position>()[i].X; sum += x;
                Check(world.GetBuffer<ArrayItem>(entity)[0].Value == x * 3, "record-to-payload pairing survives concurrent order");
            }
        Check(query.Count == count && sum == (long)count * (count - 1) / 2, "4096 spawn data integrity");
        Entity[] entities = world.Registry.Snapshot();
        Parallel.For(0, entities.Length, i => staging.Add(entities[i], new Score { Value = i + 100 }));
        staging.Apply(world);
        for (int i = 0; i < entities.Length; i++) Check(world.Read<Score>(entities[i]).Value == i + 100, "concurrent existing patches");
        Parallel.For(0, 1024, i => staging.Append(entities[0], new ArrayItem { Value = i }));
        staging.Apply(world);
        var append = world.GetBuffer<ArrayItem>(entities[0]);
        Check(append.Count == 1024 && append.Span.ToArray().Select(v => v.Value).Distinct().Count() == 1024, "same-record parallel append is serialized without lost elements");
        long retained = staging.AllocatedPayloadBytes;
        Entity next = staging.NewEntity(); staging.Add(next, new Position { X = 9000 }); staging.Apply(world);
        Check(staging.AllocatedPayloadBytes == retained, "Clear reuses per-type native blocks");
    }

    private static void CopyUpdateReplace()
    {
        using var runtime = new EcsRuntime(); var types = Register(runtime);
        using var source = runtime.CreateWorld(); using var target = runtime.CreateWorld(); using var staging = new StagingWorld(runtime, 1, 1);
        Entity original = source.Create(new EntityType(types.Position, types.Score, types.Link, types.Array));
        source.Add(original, types.Tag);
        source.Set(original, new Position { X = 11, Y = 12 }); source.Set(original, new Score { Value = 13 }); source.Set(original, new Link { Target = original });
        var srcBuffer = source.GetBuffer<ArrayItem>(original); srcBuffer.Reserve(80); srcBuffer.Add(new() { Target = original, Value = 19 });
        Entity existing = target.Create(new EntityType(types.Position, types.Score, types.Velocity));
        target.Set(existing, new Velocity { Value = 3 });
        Check(staging.UpdateFrom(source, original, target, existing), "UpdateFrom accepted");
        staging.Apply(target);
        Check(target.Read<Position>(existing).X == 11 && target.Read<Score>(existing).Value == 13 && target.Read<Velocity>(existing).Value == 3,
            "update retains runtime extra components");
        Check(!target.Has(existing, types.Tag), "copy helpers transfer ordinary columns, not source tags");
        Check(target.Read<Link>(existing).Target == existing && target.GetBuffer<ArrayItem>(existing)[0].Target == existing,
            "default update reference map maps root identity");
        Check(target.GetBuffer<ArrayItem>(existing).Capacity >= 80, "World-to-staging buffer copy retains capacity");
        Entity small = source.Create(new EntityType(types.Position));
        target.Add(existing, runtime.DisabledType);
        Check(staging.ReplaceFrom(source, small, target, existing, [types.Velocity]), "ReplaceFrom accepted");
        staging.Apply(target);
        Check(!target.Has<Score>(existing) && !target.Has<Link>(existing) && target.Has<Velocity>(existing) && target.Has(existing, runtime.DisabledType),
            "replace removes only unpreserved missing ordinary columns, retaining target tags");
        Entity stagedA = staging.NewEntity(), stagedB = staging.NewEntity();
        source.Set(original, new Link { Target = small });
        source.Set(small, new Position { X = 22 });
        var map = new Dictionary<Entity, Entity> { [original] = stagedA, [small] = stagedB };
        staging.AddMeta(stagedA, existing);
        Check(staging.ReplaceFrom(source, original, target, stagedA, [types.Score], map), "batch mapping to transient destination");
        Check(staging.UpdateFrom(source, small, target, stagedB, entityRemap: map), "second mapped destination");
        staging.Add(existing, new Link { Target = stagedA }); staging.Apply(target);
        Entity imported = target.Read<Link>(existing).Target;
        Entity referenced = target.Read<Link>(imported).Target;
        Check(referenced != imported && target.Exists(referenced) && target.Read<Position>(referenced).X == 22 &&
            target.GetBuffer<ArrayItem>(imported)[0].Target == imported && !target.Has<Score>(imported) && target.GetEntityType(imported).MetaEntities.Contains(existing),
            "batch-copy mapping repairs cross-source and self references, exclusions, and staged meta");
        int records = staging.RecordCount;
        Check(!staging.ReplaceFrom(source, original, target, existing, entityRemap: new Dictionary<Entity, Entity>()), "invalid explicit root mapping rejects");
        Check(staging.RecordCount == records, "invalid mapping does not leave removal side effects");
    }

    private static void LifetimesAndBoundaries()
    {
        using var runtime = new EcsRuntime(); var types = Register(runtime);
        using var world = runtime.CreateWorld(); using var staging = new StagingWorld(runtime, 1, 1);
        int next = 0, stagedDestroy = 0, worldDestroy = 0;
        var alive = new HashSet<int>();
        var owned = runtime.Types.Register(new ComponentRegistration<Owned>
        {
            Id = new("d9000001-0000-0000-0000-000000000001"),
            Construct = (in ComponentContext context, ref Owned value) => { value.Token = ++next; alive.Add(value.Token); },
            Move = static (in ComponentContext sc, ref Owned from, in ComponentContext dc, ref Owned to) => { to = from; from = default; },
            Destroy = (in ComponentContext context, ref Owned value) =>
            {
                Check(value.Token != 0 && alive.Remove(value.Token), "owning staged value is destroyed once");
                if (context.IsStaged) stagedDestroy++; else worldDestroy++;
            }
        });
        Owned NewOwner() { var result = new Owned { Token = ++next }; alive.Add(result.Token); return result; }
        Entity entity = staging.NewEntity(); var first = NewOwner(); staging.AddMove(entity, ref first);
        var second = NewOwner(); staging.AddMove(entity, ref second);
        Check(first.Token == 0 && second.Token == 0 && stagedDestroy == 1, "owning AddMove transfers and replaces staged value immediately");
        staging.Remove(entity, owned);
        Check(stagedDestroy == 2 && alive.Count == 0, "remove destroys staged payload immediately");
        staging.Clear();
        Entity target = world.Create(new EntityType(owned));
        var replacement = NewOwner(); staging.AddMove(target, ref replacement); staging.Apply(world);
        Check(alive.Count == 1 && worldDestroy == 1 && stagedDestroy == 2, "Apply ends existing value and transfers staged ownership without cleanup double destruction");
        world.Destroy(target); Check(alive.Count == 0 && worldDestroy == 2, "target owns final destructor");
        _ = runtime.Types.RegisterBuffer(new ComponentRegistration<Owned>
        {
            Id = new("d9000004-0000-0000-0000-000000000001"),
            Move = static (in ComponentContext sc, ref Owned from, in ComponentContext dc, ref Owned to) => { to = from; from = default; },
            Destroy = (in ComponentContext context, ref Owned value) =>
            {
                Check(value.Token != 0 && alive.Remove(value.Token), "buffer element owns one destructor");
                if (context.IsStaged) stagedDestroy++; else worldDestroy++;
            }
        }, 2);
        Entity bufferTarget = world.Create(new EntityType(types.Position));
        Throws<InvalidOperationException>(() => staging.Append(bufferTarget, default(Owned)), "copy-only append rejects move-only element before staging a delta");
        Check(staging.RecordCount == 0 && !staging.IsFaulted, "invalid ownership operation leaves no partial record");
        for (int i = 0; i < 4; i++) { Owned item = NewOwner(); staging.AppendMove(bufferTarget, ref item); Check(item.Token == 0, "AppendMove consumes element"); }
        staging.Apply(world);
        Check(alive.Count == 4 && world.GetBuffer<Owned>(bufferTarget).Count == 4, "move-only buffer heap transfers into existing entity");
        world.Destroy(bufferTarget); Check(alive.Count == 0 && worldDestroy == 6, "transferred heap elements are destroyed only by their owning World");
        Entity transient = staging.NewEntity();
        Throws<ArgumentException>(() => staging.Destroy(transient), "source forbids destroying a transient");
        staging.Clear();
        Entity stale = world.Create(new EntityType(types.Position)); world.Destroy(stale);
        staging.Add(stale, new Score { Value = 1 });
        Throws<ArgumentException>(() => staging.Apply(world), "full-generation stale target fails before applying");
        Check(!world.IsFaulted, "stale target preflight has no payload mutation"); staging.Clear();
        Entity live = world.Create(new EntityType(types.Position));
        staging.Add(live, new Score { Value = 2 });
        using (world.AcquireUsage()) Throws<InvalidOperationException>(() => staging.Apply(world), "Apply requires completed World jobs");
        staging.Apply(world);
        Check(world.Read<Score>(live).Value == 2, "a rejected busy Apply preserves the queued epoch");
    }

    private static void ProducerGateAndResources()
    {
        using var runtime = new EcsRuntime(); var types = Register(runtime);
        using var world = runtime.CreateWorld(); using var staging = new StagingWorld(runtime, 1, 1);
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        _ = runtime.Types.Register(new ComponentRegistration<GateValue>
        {
            Id = new("d9000002-0000-0000-0000-000000000001"),
            Copy = (in ComponentContext sc, in GateValue source, in ComponentContext dc, ref GateValue destination) =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test producer gate timeout.");
                destination = source;
            }
        });
        Entity entity = world.Create(new EntityType(types.Position));
        Task producer = Task.Run(() => staging.Add(entity, new GateValue { Value = 73 }));
        try
        {
            Check(entered.Wait(TimeSpan.FromSeconds(5)), "producer entered its native payload operation");
            Throws<InvalidOperationException>(() => staging.Apply(world), "Apply rejects active producers without blocking workers");
            Throws<InvalidOperationException>(staging.Clear, "Clear rejects active producer");
            Throws<InvalidOperationException>(staging.Dispose, "Dispose rejects active producer");
        }
        finally { release.Set(); producer.GetAwaiter().GetResult(); }
        staging.Apply(world);
        Check(world.Read<GateValue>(entity).Value == 73, "producer result survives rejected Apply/Clear/Dispose");
        _ = runtime.Types.Register(new ComponentRegistration<Asset>
        {
            Id = new("d9000003-0000-0000-0000-000000000001"),
            ScanResources = static (in Asset value, ResourceVisitor visitor) => visitor(value.Id)
        });
        Guid resource = new("d900ffff-0000-0000-0000-000000000001");
        staging.Add(entity, new Asset { Id = resource });
        var seen = new List<Guid>(); staging.ScanResourceReferences(seen.Add);
        Check(seen.SequenceEqual(new[] { resource }), "resource scanner sees pending native payloads");
        staging.Clear(); seen.Clear(); staging.ScanResourceReferences(seen.Add);
        Check(seen.Count == 0, "cleared payload is not scanned again");
        using var invalidWorld = runtime.CreateWorld(); using var invalid = new StagingWorld(runtime, 1, 1);
        Entity unused = invalid.NewEntity(), holder = invalid.NewEntity();
        invalid.Add(holder, new Link { Target = unused });
        Throws<InvalidOperationException>(() => invalid.Apply(invalidWorld), "unrecorded transient reference is rejected rather than turned into Null");
        invalid.Clear();
    }
}
