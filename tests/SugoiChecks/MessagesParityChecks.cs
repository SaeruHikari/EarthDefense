using System.Runtime.InteropServices;
using Sugoi.Data;
using Sugoi.Tasks;

internal static class MessagesParityChecks
{
    private struct Cell { public int Value; }
    private struct Selected { }
    private struct Packet { public int Value; }
    private struct Owned { public nint Pointer; }
    private sealed class Counts { public int Copies, Moves, Destructors, Freed; }

    internal static async Task RunAsync()
    {
        using var runtime = new EcsRuntime();
        runtime.Types.Register<Cell>(new Guid("bca6f639-0fcb-477b-bc48-d38789822f01"));
        runtime.Types.Register(new ComponentRegistration<Selected> { Id = new Guid("bca6f639-0fcb-477b-bc48-d38789822f02"), Kind = ComponentKind.Tag });
        RingOverflowAndIdentity(runtime);
        await TicketPublication(runtime);
        MoveAndRawOwnership(runtime);
        await ValidationAndOffsets(runtime);
        await LazyAndAsyncLifetime(runtime);
        Console.WriteLine("Messages parity: native ring/overflow, ticket publication, GUID/typed registration, idempotency, move/copy/destroy, raw fanout, lazy admission, validation, offsets and async ownership passed.");
    }

    private static void RingOverflowAndIdentity(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        Entity target = world.Create(new EntityType(runtime.Types.Get<Cell>()));
        using var query = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Cell>()));
        using var bus = new MessageBus(world);
        var descriptor = bus.Registry.Register<Packet>(new Guid("bca6f639-0fcb-477b-bc48-d38789822f03"));
        using var subscription = bus.Subscribe<Packet>(query, 3);
        Check(ReferenceEquals(subscription, bus.Subscribe<Packet>(query, 9)) && ReferenceEquals(subscription, bus.Subscribe(descriptor.Id, query)), "(GUID, Query) registration must be idempotent.");
        for (int i = 0; i < 11; i++) { var packet = new Packet { Value = i }; Check(bus.Send(target, in packet) == 1, "Overflow must retain the incoming message."); }
        Check(subscription.PublishedCount == 3 && subscription.OverflowMessageCount == 8 && subscription.OverflowBlockCount == 3 && subscription.DroppedCount == 0, "Ring/overflow counters differ from the reference block mechanism.");
        Check(bus.HasPending<Packet>() && bus.HasPending(descriptor.Id, query), "Typed/GUID pending lookup lost the subscription.");
        var seen = new List<int>();
        subscription.Visit((Entity entity, ref Packet packet) => { Check(entity == target, "Entity64 column was corrupted."); seen.Add(packet.Value); });
        Check(seen.SequenceEqual(Enumerable.Range(0, 11)) && !subscription.HasPending && !bus.HasPending<Packet>(query), "Ring then overflow consumption order or cleanup is incorrect.");

        // Advance the ring head to a nonzero slot, then force a two-segment ring wrap without overflow.
        var seed = new Packet { Value = 20 }; bus.Send(target, in seed);
        subscription.Visit(static (Entity _, ref Packet _) => { });
        for (int i = 21; i < 24; i++) { var packet = new Packet { Value = i }; bus.Send(target, in packet); }
        seen.Clear(); subscription.Visit((Entity _, ref Packet packet) => seen.Add(packet.Value));
        Check(seen.SequenceEqual(new[] { 21, 22, 23 }), "Wrapped ring linearization changed ticket order.");
    }

    private static async Task TicketPublication(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        Entity target = world.Create(new EntityType(runtime.Types.Get<Cell>()));
        using var query = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Cell>()));
        using var bus = new MessageBus(world);
        using var copying = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bus.Registry.Register(new MessageRegistration<Packet>
        {
            Id = new Guid("bca6f639-0fcb-477b-bc48-d38789822f04"),
            Copy = (in Packet source, ref Packet destination) =>
            {
                if (source.Value == 1) { copying.Set(); Check(release.Wait(10000), "Delayed constructor was not released."); }
                destination = source;
            }
        });
        using var subscription = bus.Subscribe<Packet>(query, 4);
        Task first = Task.Run(() => { var packet = new Packet { Value = 1 }; bus.Send(target, in packet); });
        Check(copying.Wait(10000), "First producer did not reserve its slot.");
        try
        {
            var second = new Packet { Value = 2 }; bus.Send(target, in second);
            Check(subscription.Count == 0 && !subscription.HasPending, "A later publication crossed an unpublished earlier ticket.");
            using (var empty = subscription.Consume()) Check(empty.Count == 0, "Consumer observed a payload beyond the contiguous published prefix.");
        }
        finally { release.Set(); }
        await first;
        // Consumption would invoke the copy fallback again; permit that constructor to proceed immediately.
        using var received = subscription.Consume();
        Check(ReadPackets(received).SequenceEqual(new[] { 1, 2 }), "Queue consumed enqueue-completion order instead of reservation-ticket order.");
    }

    private static unsafe void MoveAndRawOwnership(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        Entity target = world.Create(new EntityType(runtime.Types.Get<Cell>()));
        using var firstQuery = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Cell>()));
        using var secondQuery = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Cell>()));
        var moveCounts = new Counts();
        using (var bus = new MessageBus(world))
        {
            bus.Registry.Register(OwnershipRegistration(new Guid("bca6f639-0fcb-477b-bc48-d38789822f05"), moveCounts, false));
            using var first = bus.Subscribe<Owned>(firstQuery, 2);
            using var second = bus.Subscribe<Owned>(secondQuery, 2);
            var value = Allocate(77);
            Check(bus.SendMove(target, ref value) == 1 && value.Pointer == 0 && first.Count == 1 && second.Count == 0 && bus.MoveOnlyFanoutCount == 1, "Move-only fanout must move to only the first matching receiver.");
            Owned movedOut = default;
            first.Visit((Entity _, ref Owned message) => { movedOut = message; message = default; });
            Check(moveCounts.Moves == 1 && moveCounts.Destructors == 1 && moveCounts.Freed == 0 && *(int*)movedOut.Pointer == 77, "Writable consumer failed to transfer ownership out of the queue.");
            Destroy(ref movedOut, moveCounts);
            Check(moveCounts.Freed == 1, "Moved-out ownership was not freed exactly once.");
        }

        var counts = new Counts();
        using (var bus = new MessageBus(world))
        {
            var descriptor = bus.Registry.Register(OwnershipRegistration(new Guid("bca6f639-0fcb-477b-bc48-d38789822f06"), counts, true));
            using var first = bus.Subscribe<Owned>(firstQuery, 8);
            using var second = bus.Subscribe<Owned>(secondQuery, 8);
            var moved = Allocate(31);
            Check(bus.SendMove(target, ref moved) == 2 && moved.Pointer == 0 && counts.Moves == 1 && counts.Copies == 2 && counts.Freed == 1, "Typed fanout must construct one stable moved value and copy it to both queues.");
            var raw = Allocate(32);
            Check(bus.SendRaw(target, descriptor.Id, (nint)(&raw)) && counts.Copies == 4, "Raw GUID fanout must copy directly to each receiver.");
            Destroy(ref raw, counts);
            using (var left = first.Consume(false)) using (var right = second.Consume(false))
            {
                Check(*(int*)left.Values[0].Pointer == 31 && *(int*)left.Values[1].Pointer == 32, "Left payload values differ after materialization.");
                Check(*(int*)right.Values[0].Pointer == 31 && *(int*)right.Values[1].Pointer == 32, "Right payload values differ after materialization.");
            }
            Check(counts.Copies == 4 && counts.Moves == 5 && counts.Freed == 6, "Copy/move/free counts disagree with stable fanout plus materialization.");
            var unknown = Guid.NewGuid();
            Check(!bus.SendRaw(target, unknown, (nint)(&raw)), "Unknown raw GUID must be rejected.");
        }
    }

    private static async Task ValidationAndOffsets(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        var type = new EntityType(runtime.Types.Get<Cell>(), runtime.Types.Get<Selected>());
        Entity missing = world.Create(type), valid = world.Create(type);
        using var query = world.CreateQuery(new QueryDescription().WithOwned(runtime.Types.Get<Cell>()).WithAll(runtime.Types.Get<Selected>()));
        using var bus = new MessageBus(world);
        bus.Registry.Register<Packet>(new Guid("bca6f639-0fcb-477b-bc48-d38789822f07"));
        using var subscription = bus.Subscribe<Packet>(query, 8);
        var first = new Packet { Value = 101 }; var second = new Packet { Value = 202 };
        bus.Send(missing, in first); bus.Send(valid, in second); world.Destroy(missing);
        await using var scheduler = new Scheduler(2);
        var offsets = new List<int>();
        var job = new ConsumeJob { Offsets = offsets };
        await scheduler.DispatchMessages<Packet, ConsumeJob>(subscription, in job);
        Check(ReadCell(world, valid) == 202 && offsets.SequenceEqual(new[] { 1 }), "Skipping an invalid prefix changed the original payload offset (explicit repair of the C++ offset defect).");

        var third = new Packet { Value = 303 }; bus.Send(valid, in third); world.Remove<Selected>(valid);
        await scheduler.DispatchMessages<Packet, ConsumeJob>(subscription, in job);
        Check(ReadCell(world, valid) == 303, "Default task validation must be disabled, as in the reference scheduler.");
        world.Add(valid, new Selected());
        var fourth = new Packet { Value = 404 }; bus.Send(valid, in fourth); world.Remove<Selected>(valid);
        await scheduler.DispatchMessages<Packet, ConsumeJob>(subscription, in job, new TaskOptions { ValidateMessages = true });
        Check(ReadCell(world, valid) == 303, "Explicit validation must drop targets whose group no longer matches.");
    }

    private static async Task LazyAndAsyncLifetime(EcsRuntime runtime)
    {
        using var world = runtime.CreateWorld();
        var entities = new Entity[3]; world.Create(new EntityType(runtime.Types.Get<Cell>()), entities);
        using var bus = new MessageBus(world);
        bus.Registry.Register<Packet>(new Guid("bca6f639-0fcb-477b-bc48-d38789822f08"));
        await using var scheduler = new Scheduler(2);
        int prepared = -1;
        var job = new ConsumeJob { Prepared = count => prepared = count };
        var gate = new JobEvent();
        var lazy = scheduler.DispatchMessages<Packet, ConsumeJob>(bus, in job, options: new TaskOptions { AfterEvents = [gate] });
        var value = new Packet { Value = 7 };
        Check(prepared == 3 && bus.Send(entities[0], in value) == 0, "Message Prepare is query count and lazy subscription must not occur before admission.");
        gate.Set(); await lazy;
        Query query = lazy.Query!;
        using var subscription = bus.GetSubscription<Packet>(query) ?? throw new InvalidOperationException("Lazy admission did not create its queue.");
        Check(ReadCell(world, entities[0]) == 0, "The first lazy dispatch must only register, without consuming work.");
        bus.Send(entities[0], in value);
        await scheduler.DispatchMessages<Packet, ConsumeJob>(subscription, in job);
        Check(ReadCell(world, entities[0]) == 7, "Lazy registration did not retain subsequent messages.");

        var lifecycle = new JobCounts();
        var started = new JobEvent(); var resume = new JobEvent();
        var asynchronous = new AsyncConsumeJob { Counts = lifecycle, Started = started, Resume = resume };
        bus.Send(entities[1], in value);
        var handle = scheduler.DispatchMessages<Packet, AsyncConsumeJob>(subscription, in asynchronous);
        await started.WaitAsync();
        Check(!handle.IsCompleted && world.ActiveUsers != 0 && lifecycle.Disposed == 0, "Async message task released its job/payload/world before await completed.");
        resume.Set(); await handle;
        Check(ReadCell(world, entities[1]) == 7 && lifecycle.Copies == 2 && lifecycle.Disposed == 2, "Submission and async batch copies must each be cleaned exactly once.");
        subscription.Dispose();
        await scheduler.ReleaseQueryAsync(query);
    }

    private static int[] ReadPackets(MessageBatch<Packet> batch)
    { var result = new int[batch.Count]; for (int i = 0; i < result.Length; i++) result[i] = batch.Values[i].Value; return result; }
    private static int ReadCell(World world, Entity entity) => world.Read<Cell>(entity).Value;
    private static unsafe Owned Allocate(int value)
    { var pointer = (int*)NativeMemory.Alloc((nuint)sizeof(int)); *pointer = value; return new Owned { Pointer = (nint)pointer }; }
    private static unsafe void Destroy(ref Owned value, Counts counts)
    {
        counts.Destructors++;
        if (value.Pointer != 0) { NativeMemory.Free((void*)value.Pointer); value.Pointer = 0; counts.Freed++; }
    }
    private static unsafe MessageRegistration<Owned> OwnershipRegistration(Guid id, Counts counts, bool copyable) => new()
    {
        Id = id, IsCopyable = copyable,
        Copy = copyable ? (in Owned source, ref Owned destination) => { counts.Copies++; destination = Allocate(*(int*)source.Pointer); } : null,
        Move = (ref Owned source, ref Owned destination) => { counts.Moves++; destination = source; source = default; },
        Destroy = (ref Owned value) => Destroy(ref value, counts)
    };
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private struct ConsumeJob : IMessageJob<Packet>
    {
        public List<int>? Offsets;
        public Action<int>? Prepared;
        public void Build(JobAccessBuilder builder) => builder.Write<Cell>();
        public void Prepare(int entityCount) => Prepared?.Invoke(entityCount);
        public void Execute(in MessageJobContext<Packet> context)
        {
            Offsets?.Add(context.Index);
            var values = context.View.WriteOwned<Cell>();
            for (int i = 0; i < context.Count; i++) values[i].Value = context.Messages[i].Value;
        }
    }
    private sealed class JobCounts { public int Copies, Disposed; }
    private sealed class AsyncConsumeJob : IAsyncMessageJob<Packet>, IJobCloneable<AsyncConsumeJob>, IDisposable
    {
        public required JobCounts Counts;
        public required JobEvent Started, Resume;
        public AsyncConsumeJob CloneForBatch() { Interlocked.Increment(ref Counts.Copies); return (AsyncConsumeJob)MemberwiseClone(); }
        public void Build(JobAccessBuilder builder) => builder.Write<Cell>();
        public async ValueTask ExecuteAsync(MessageTaskBatch<Packet> batch)
        { Started.Set(); await Resume.WaitAsync(); Apply(batch); }
        private static void Apply(MessageTaskBatch<Packet> batch)
        {
            var context = batch.Bind(); var cells = context.View.WriteOwned<Cell>();
            for (int i = 0; i < context.Count; i++) cells[i].Value = context.Messages[i].Value;
        }
        public void Dispose() => Interlocked.Increment(ref Counts.Disposed);
    }
}
