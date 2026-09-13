using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>An ECS job may suspend without releasing its chunk dependencies or World lifetime.</summary>
public interface IAsyncQueryJob : IQueryJob
{
    ValueTask ExecuteAsync(AsyncJobContext context);
    void IQueryJob.Execute(in JobContext context) =>
        throw new InvalidOperationException("Async query jobs must execute through the scheduler's asynchronous path.");
}

/// <summary>Stable batch identity across awaits. Bind native spans only inside synchronous Borrow scopes.</summary>
public readonly struct AsyncJobContext
{
    private readonly QueryRange _range;
    private readonly MessageBus? _messages;
    internal AsyncJobContext(QueryRange range, int index, int taskIndex, MessageBus? messages, CancellationToken cancellation)
    { _range = range; Index = index; TaskIndex = taskIndex; _messages = messages; CancellationToken = cancellation; }

    public World World => _range.World;
    public int Count => _range.Count;
    public int Index { get; }
    public int TaskIndex { get; }
    public CancellationToken CancellationToken { get; }
    public MessageBus MessagesBus => _messages ?? throw new InvalidOperationException("Configure a message bus on this scheduled World or TaskOptions before using a sender.");
    public ChunkView Borrow() => _range.View;
    public RandomReader<T> RandomRead<T>() where T : unmanaged => new(World);
    public RandomWriter<T> RandomWrite<T>() where T : unmanaged => new(World);
    public MessageSender<T> Sender<T>() where T : unmanaged => new(MessagesBus);
    public int Send<T>(MessageBus bus, Entity target, in T message) where T : unmanaged
    {
        if (!ReferenceEquals(bus.World, World)) throw new ArgumentException("A job may send only through its declared World.");
        return bus.SendUnderUsage(target, in message);
    }
}
