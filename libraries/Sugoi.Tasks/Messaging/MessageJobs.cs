using Sugoi.Data;

namespace Sugoi.Tasks;

public interface IMessageJob<T> where T : unmanaged
{
    void Build(JobAccessBuilder builder);
    void Prepare(int entityCount) { }
    void Execute(in MessageJobContext<T> context);
}

public interface IAsyncMessageJob<T> : IMessageJob<T> where T : unmanaged
{
    ValueTask ExecuteAsync(MessageTaskBatch<T> batch);
    void IMessageJob<T>.Execute(in MessageJobContext<T> context) => throw new InvalidOperationException("An asynchronous message job must be dispatched through its async batch entry.");
}

public interface ICloneableMessageJob<T> : IMessageJob<T> where T : unmanaged
{
    IMessageJob<T> CloneForBatch();
}

public readonly ref struct MessageJobContext<T> where T : unmanaged
{
    internal MessageJobContext(QueryRange range, int index, int taskIndex, Span<T> messages, MessageBus bus)
    { View = range.View; World = range.World; Index = index; TaskIndex = taskIndex; Messages = messages; MessagesBus = bus; }
    public World World { get; }
    public ChunkView View { get; }
    public int Index { get; }
    public int TaskIndex { get; }
    public ReadOnlySpan<Entity> Entities => View.Entities;
    public Span<T> Messages { get; }
    public MessageConsumer<T> Consumer => new(Messages);
    public MessageBus MessagesBus { get; }
    public int Count => View.Count;
    public MessageSender<TOut> Sender<TOut>() where TOut : unmanaged => new(MessagesBus);
    public int Send<TOut>(MessageBus bus, Entity target, in TOut message) where TOut : unmanaged
    {
        if (!ReferenceEquals(bus.World, World)) throw new ArgumentException("A job may send only through the bus of its declared World.", nameof(bus));
        return bus.SendUnderUsage(target, in message);
    }
}

/// <summary>Owned task descriptor across await; obtain borrowed payload/component spans only inside synchronous Bind scopes.</summary>
public readonly struct MessageTaskBatch<T> where T : unmanaged
{
    private readonly QueryRange _range;
    private readonly MessageBatch<T> _messages;
    internal MessageTaskBatch(QueryRange range, int index, int taskIndex, MessageBatch<T> messages, MessageBus bus)
    { _range = range; Index = index; TaskIndex = taskIndex; _messages = messages; MessagesBus = bus; }
    public World World => _range.World;
    public int Count => _range.Count;
    public int Index { get; }
    public int TaskIndex { get; }
    public MessageBus MessagesBus { get; }
    public MessageJobContext<T> Bind() => new(_range, Index, TaskIndex, _messages.Values.Slice(Index, Count), MessagesBus);
    public MessageSender<TOut> Sender<TOut>() where TOut : unmanaged => new(MessagesBus);
}

internal sealed class MessageSubmission<TJob, TMessage> : Submission
    where TJob : IMessageJob<TMessage> where TMessage : unmanaged
{
    internal required TJob Job;
    internal override bool IsAsynchronous => Job is IAsyncMessageJob<TMessage>;
    internal required MessageBus Bus;
    internal MessageSubscription<TMessage>? Subscription;
    private MessageBatch<TMessage>? _messages;

    internal override void Materialize()
    {
        Subscription ??= Bus.GetSubscription<TMessage>(Query);
        if (Subscription is null)
        {
            Subscription = Bus.Subscribe<TMessage>(Query);
            BuildUnits(Array.Empty<WorkRange>());
            return; // The original lazy registration turn performs no message work.
        }
        _messages = Subscription.ConsumeForDispatch();
        var targets = _messages.Targets;
        var ranges = new List<WorkRange>();
        int cursor = 0;
        while (cursor < targets.Length)
        {
            int sourceIndex = cursor;
            if (!World.Registry.TryGet(targets[cursor], out var first) ||
                Options.ValidateMessages && !Query.MatchesGroupUnderUsage(first.Chunk!.Group.Id)) { cursor++; continue; }
            int count = 1;
            while (cursor + count < targets.Length && World.Registry.TryGet(targets[cursor + count], out var next) &&
                   ReferenceEquals(next.Chunk, first.Chunk) && next.Row == first.Row + count) count++;
            ranges.Add(new(new QueryRange(World, first.Chunk!, first.Row, count), sourceIndex));
            cursor += count;
        }
        // Preserve source payload offsets when a dead/nonmatching target is skipped; never reindex the payload array.
        BuildUnits(ranges);
    }

    internal override void ExecuteBatch(QueryRange range, int index)
    {
        var body = JobBody.CopyMessage<TJob, TMessage>(in Job);
        try
        {
            int taskIndex = NextTaskIndex();
            using var activity = JobDiagnostics.StartBatch(Options.DebugName, range, index, taskIndex);
            Prefetch.Apply(range, PrefetchPlan);
            var context = new MessageJobContext<TMessage>(range, index, taskIndex, _messages!.Values.Slice(index, range.Count), Bus);
            body.Execute(in context);
        }
        finally { JobBody.Dispose(ref body); }
    }

    internal override async ValueTask ExecuteBatchAsync(QueryRange range, int index)
    {
        var body = JobBody.CopyMessage<TJob, TMessage>(in Job);
        if (body is IAsyncMessageJob<TMessage> asynchronous)
        {
            // Retain the same async object through completion; owning async structs are rejected by JobBody.
            body = default!;
            try
            {
                int taskIndex = NextTaskIndex();
                using var activity = JobDiagnostics.StartBatch(Options.DebugName, range, index, taskIndex);
                Prefetch.Apply(range, PrefetchPlan);
                await asynchronous.ExecuteAsync(new MessageTaskBatch<TMessage>(range, index, taskIndex, _messages!, Bus));
            }
            finally { if (asynchronous is IDisposable disposable) disposable.Dispose(); }
        }
        else
        {
            try { ExecuteSynchronous(ref body, range, index); }
            finally { JobBody.Dispose(ref body); }
        }
    }

    private void ExecuteSynchronous(ref TJob body, QueryRange range, int index)
    {
        int taskIndex = NextTaskIndex();
        using var activity = JobDiagnostics.StartBatch(Options.DebugName, range, index, taskIndex);
        Prefetch.Apply(range, PrefetchPlan);
        var context = new MessageJobContext<TMessage>(range, index, taskIndex, _messages!.Values.Slice(index, range.Count), Bus);
        body.Execute(in context);
    }

    internal override void ReleasePayload()
    {
        try { _messages?.Dispose(); }
        finally { JobBody.Dispose(ref Job); }
    }
}
