using System.Runtime.InteropServices;
using Sugoi.Data;
using Sugoi.Tasks;

namespace SugoiFixtures;

[Component("dc08b22f-9697-42d4-b86c-b6cf6613f211")]
public partial struct SharedScale { public float Value; }

[QueryJob]
public partial struct AdvancedMoveJob
{
    private void Execute(Span<Position> positions, OptionalRead<Velocity> velocities,
        SharedRead<SharedScale> scale, ChunkWrite<ChunkStatistics> statistics,
        BufferWrite<Link> links, ReadOnlySpan<Entity> entities)
    {
        statistics.Value.Ticks++;
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i].X += (velocities.HasValue ? velocities[i].X : 2f) * scale.Value.Value;
            links[i].Add(new Link { Target = entities[i], Weight = 7 });
        }
    }
}

[QueryJob]
public partial struct OptionalWriteJob
{
    private void Execute(ReadOnlySpan<Position> positions, OptionalWrite<Velocity> velocities)
    {
        if (!velocities.HasValue) return;
        for (int i = 0; i < positions.Length; i++) velocities[i].Y += 3;
    }
}

[QueryJob]
public partial struct ReadBindingsJob
{
    public Action<int>? Observe;
    private void Execute(ChunkRead<ChunkStatistics> statistics, BufferRead<Link> links, RandomReader<Position> positions)
    {
        int count = 0;
        for (int row = 0; row < links.Count; row++)
            foreach (ref readonly var link in links[row]) if (positions.Read(link.Target).X > 0) count++;
        if (statistics.Value.Ticks > 0) Observe?.Invoke(count);
    }
}

[QueryJob]
public partial struct RandomBindingsJob
{
    public Entity VelocityTarget;
    public Entity ScaleTarget;
    private void Execute(ReadOnlySpan<Position> positions, RandomWriter<Velocity> velocities, RandomReadWrite<SharedScale> scales)
    {
        velocities.Write(VelocityTarget).Z += positions.Length;
        float value = scales.Read(ScaleTarget).Value;
        scales.Write(ScaleTarget).Value = value + positions.Length;
    }
}

[QueryJob]
[Write(typeof(Position))]
public partial struct AsyncMoveJob
{
    public JobCounter? Gate;
    public JobEvent? Entered;
    public Action<int, int>? Observe;
    private async ValueTask ExecuteAsync(AsyncJobContext context)
    {
        Entered?.Set();
        if (Gate is not null) await Gate.WaitAsync();
        Move(context);
        Observe?.Invoke(context.TaskIndex, context.Index);
    }
    private static void Move(AsyncJobContext context)
    {
        var positions = context.Borrow().WriteOwned<Position>();
        for (int i = 0; i < positions.Length; i++) positions[i].Z += 5;
    }
}

[QueryJob]
[JobOwnership]
public unsafe partial struct OwnedJob
{
    public nint Pointer;
    public static int Clones, Disposals;
    public static OwnedJob Create()
    {
        var pointer = (int*)NativeMemory.Alloc((nuint)sizeof(int));
        *pointer = 17;
        return new OwnedJob { Pointer = (nint)pointer };
    }
    [JobClone]
    private OwnedJob CopyBody()
    {
        var result = Create();
        if (Pointer != 0) *(int*)result.Pointer = *(int*)Pointer;
        Interlocked.Increment(ref Clones);
        return result;
    }
    [JobDispose]
    private void ReleaseBody()
    {
        NativeMemory.Free((void*)Pointer);
        Pointer = 0;
        Interlocked.Increment(ref Disposals);
    }
    private void Execute(Span<Position> positions)
    {
        for (int i = 0; i < positions.Length; i++) positions[i].Y += *(int*)Pointer;
    }
}

[QueryJob]
[JobOwnership]
[Write(typeof(Position))]
public partial class AsyncOwnedJob
{
    public nint Pointer;
    public static int Clones, Disposals;
    public static unsafe AsyncOwnedJob Create()
    {
        var pointer = (int*)NativeMemory.Alloc((nuint)sizeof(int));
        *pointer = 19;
        return new AsyncOwnedJob { Pointer = (nint)pointer };
    }
    [JobClone]
    private unsafe AsyncOwnedJob CopyBody()
    {
        var copy = Create();
        if (Pointer != 0) *(int*)copy.Pointer = *(int*)Pointer;
        Interlocked.Increment(ref Clones);
        return copy;
    }
    [JobDispose]
    private unsafe void ReleaseBody()
    {
        NativeMemory.Free((void*)Pointer);
        Pointer = 0;
        Interlocked.Increment(ref Disposals);
    }
    // Keep the unsafe pointer work in a synchronous helper; the async state machine owns this class instance.
    private ValueTask ExecuteAsync(AsyncJobContext context) => RunAsync(this, context);
    private static async ValueTask RunAsync(AsyncOwnedJob job, AsyncJobContext context)
    {
        await Task.Yield();
        job.Apply(context);
    }
    private unsafe void Apply(AsyncJobContext context)
    {
        var values = context.Borrow().WriteOwned<Position>();
        for (int i = 0; i < values.Length; i++) values[i].Y += *(int*)Pointer;
    }
}

[Message("a2b1f3ed-97a8-430c-88af-07ba36f0b401")]
public partial struct DamageMessage { public int Amount; }

[Message("a2b1f3ed-97a8-430c-88af-07ba36f0b402")]
public partial struct AckMessage { public int Amount; }

[Message("a2b1f3ed-97a8-430c-88af-07ba36f0b403")]
public unsafe partial struct OwnedMessage
{
    public nint Pointer;
    public static int Copies, Moves, Disposals;
    public static OwnedMessage Create(int value)
    {
        var pointer = (int*)NativeMemory.Alloc((nuint)sizeof(int));
        *pointer = value;
        return new OwnedMessage { Pointer = (nint)pointer };
    }
    [MessageCopy]
    private static void Copy(in OwnedMessage source, ref OwnedMessage destination)
    {
        destination = source.Pointer == 0 ? default : Create(*(int*)source.Pointer);
        Interlocked.Increment(ref Copies);
    }
    [MessageMove]
    private static void Move(ref OwnedMessage source, ref OwnedMessage destination)
    {
        destination = source;
        source = default;
        Interlocked.Increment(ref Moves);
    }
    [MessageDestroy]
    private static void Destroy(ref OwnedMessage value)
    {
        NativeMemory.Free((void*)value.Pointer);
        value = default;
        Interlocked.Increment(ref Disposals);
    }
}

[MessageJob(typeof(DamageMessage))]
public partial struct ReceiveDamageJob
{
    private void Execute(MessageConsumer<DamageMessage> messages, Span<Position> positions,
        MessageSender<AckMessage> replies, in MessageJobContext<DamageMessage> context)
    {
        for (int i = 0; i < messages.Count; i++)
        {
            positions[i].X += messages[i].Amount;
            replies.Send(context.Entities[i], new AckMessage { Amount = messages[i].Amount });
        }
    }
}

[MessageJob(typeof(DamageMessage))]
[Write(typeof(Position))]
public partial struct AsyncDamageJob
{
    public JobCounter? Gate;
    public JobEvent? Entered;
    private async ValueTask ExecuteAsync(MessageTaskBatch<DamageMessage> context)
    {
        Entered?.Set();
        if (Gate is not null) await Gate.WaitAsync();
        Apply(context);
    }
    private static void Apply(MessageTaskBatch<DamageMessage> context)
    {
        var batch = context.Bind();
        var positions = batch.View.WriteOwned<Position>();
        for (int i = 0; i < batch.Count; i++) positions[i].Y += batch.Consumer[i].Amount;
    }
}
