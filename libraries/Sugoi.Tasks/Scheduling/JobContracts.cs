using System.Runtime.CompilerServices;
using Sugoi.Data;

namespace Sugoi.Tasks;

public enum AccessMode : byte { Sequential, Random }
public enum PrefetchMode : byte { Off, Auto, Force }

public readonly record struct ComponentAccess(ComponentType Type, AccessMode Mode, bool Read, bool Write, PrefetchMode Prefetch);

/// <summary>One submission's declaration. Read/Write require owned columns; random access does not narrow the query.</summary>
public sealed class JobAccessBuilder
{
    private readonly Dictionary<ComponentType, ComponentAccess> _accesses = new();
    internal JobAccessBuilder(World world) { World = world; Description = new QueryDescription(); }
    public World World { get; }
    public QueryDescription Description { get; }
    internal ComponentAccess[] Accesses => _accesses.Values.ToArray();

    public JobAccessBuilder Read<T>(PrefetchMode prefetch = PrefetchMode.Off) where T : unmanaged => Access(World.Types.Get<T>(), true, false, AccessMode.Sequential, true, prefetch);
    public JobAccessBuilder Write<T>(PrefetchMode prefetch = PrefetchMode.Off) where T : unmanaged => Access(World.Types.Get<T>(), true, true, AccessMode.Sequential, true, prefetch);
    public JobAccessBuilder RandomRead<T>() where T : unmanaged => Access(World.Types.Get<T>(), true, false, AccessMode.Random, false, PrefetchMode.Off);
    public JobAccessBuilder RandomWrite<T>() where T : unmanaged => Access(World.Types.Get<T>(), false, true, AccessMode.Random, false, PrefetchMode.Off);
    public JobAccessBuilder RandomReadWrite<T>() where T : unmanaged => Access(World.Types.Get<T>(), true, true, AccessMode.Random, false, PrefetchMode.Off);
    public JobAccessBuilder OptionalRead<T>() where T : unmanaged => Access(World.Types.Get<T>(), true, false, AccessMode.Sequential, false, PrefetchMode.Off);
    public JobAccessBuilder OptionalWrite<T>() where T : unmanaged => Access(World.Types.Get<T>(), true, true, AccessMode.Sequential, false, PrefetchMode.Off);
    public JobAccessBuilder ReadBuffer<T>(PrefetchMode prefetch = PrefetchMode.Off) where T : unmanaged => Access(World.Types.GetBuffer<T>(), true, false, AccessMode.Sequential, true, prefetch);
    public JobAccessBuilder WriteBuffer<T>(PrefetchMode prefetch = PrefetchMode.Off) where T : unmanaged => Access(World.Types.GetBuffer<T>(), true, true, AccessMode.Sequential, true, prefetch);
    public JobAccessBuilder ReadChunk<T>() where T : unmanaged => Read<T>();
    public JobAccessBuilder WriteChunk<T>() where T : unmanaged => Write<T>();
    public JobAccessBuilder ReadShared<T>() where T : unmanaged
    {
        var type = World.Types.Get<T>();
        Description.WithShared(type);
        return Access(type, true, false, AccessMode.Random, false);
    }
    public JobAccessBuilder WithMeta(Entity entity) { Description.WithMeta(entity); return this; }
    public JobAccessBuilder WithoutMeta(Entity entity) { Description.WithoutMeta(entity); return this; }
    public JobAccessBuilder WritePhase<T>(int phase) where T : unmanaged { Description.WritePhase(World.Types.Get<T>(), phase); return Write<T>(); }
    public JobAccessBuilder ReadEnabledMask() => OptionalRead<EnabledMask>();
    public JobAccessBuilder WriteEnabledMask() => OptionalWrite<EnabledMask>();
    public JobAccessBuilder Has<T>() where T : unmanaged { Description.WithAll(World.Types.Get<T>()); return this; }
    public JobAccessBuilder Without<T>() where T : unmanaged { Description.Without(World.Types.Get<T>()); return this; }
    public JobAccessBuilder Access(ComponentType type, bool read, bool write, AccessMode mode = AccessMode.Sequential, bool require = true, PrefetchMode prefetch = PrefetchMode.Off)
    {
        if (require) Description.WithOwned(type);
        if (_accesses.TryGetValue(type, out var previous))
        {
            read |= previous.Read; write |= previous.Write;
            if (previous.Mode == AccessMode.Random) mode = AccessMode.Random;
            prefetch = (PrefetchMode)Math.Max((int)prefetch, (int)previous.Prefetch);
        }
        _accesses[type] = new(type, mode, read, write, prefetch);
        return this;
    }
}

public interface IQueryJob
{
    void Build(JobAccessBuilder builder);
    void Prepare(int entityCount) { }
    void Execute(in JobContext context);
}

/// <summary>Reference-type task bodies explicitly provide a per-batch copy. Value types are copied directly.</summary>
public interface ICloneableQueryJob : IQueryJob
{
    IQueryJob CloneForBatch();
}

public readonly ref struct JobContext
{
    private readonly MessageBus? _messages;
    internal JobContext(QueryRange range, int index, int taskIndex = 0, MessageBus? messages = null, CancellationToken cancellation = default)
    { View = range.View; World = range.World; Index = index; TaskIndex = taskIndex; _messages = messages; CancellationToken = cancellation; }
    internal JobContext(in ChunkView view, int index, int taskIndex = 0, MessageBus? messages = null, CancellationToken cancellation = default)
    { View = view; World = view.World; Index = index; TaskIndex = taskIndex; _messages = messages; CancellationToken = cancellation; }
    public ChunkView View { get; }
    public World World { get; }
    public int Index { get; }
    public int TaskIndex { get; }
    public CancellationToken CancellationToken { get; }
    public MessageBus MessagesBus => _messages ?? throw new InvalidOperationException("Register a message bus on this scheduled World or TaskOptions before using a sender.");
    public MessageSender<T> Sender<T>() where T : unmanaged => new(MessagesBus);
    public ReadOnlySpan<Entity> Entities => View.Entities;
    public int Count => View.Count;
    public RandomReader<T> RandomRead<T>() where T : unmanaged => new(World);
    public RandomWriter<T> RandomWrite<T>() where T : unmanaged => new(World);
    public int Send<T>(MessageBus bus, Entity target, in T message) where T : unmanaged
    {
        if (!ReferenceEquals(bus.World, World)) throw new ArgumentException("A job may send only through the bus of its declared World.", nameof(bus));
        return bus.SendUnderUsage(target, in message);
    }
}

/// <summary>Arrays are copied during Dispatch; mutating a caller's options afterwards cannot change a submission.</summary>
public readonly record struct TaskOptions
{
    public IReadOnlyList<JobHandle>? After { get; init; }
    public IReadOnlyList<WeakJobEvent>? AfterEvents { get; init; }
    public IReadOnlyList<WeakJobCounter>? AfterCounters { get; init; }
    public IReadOnlyList<WeakJobEvent>? OnFinishEvents { get; init; }
    /// <summary>Caller must Add before submitting; cleanup decrements each counter exactly once.</summary>
    public IReadOnlyList<WeakJobCounter>? OnFinishCounters { get; init; }
    public int BatchSize { get; init; }
    public string? DebugName { get; init; }
    public MessageBus? MessageBus { get; init; }
    public bool ValidateMessages { get; init; }
    public bool NoParallelization { get; init; }
    public int? WorkerIndex { get; init; }
    public PrefetchMode? Prefetch { get; init; }
    public CancellationToken CancellationToken { get; init; }

    internal TaskOptions Snapshot() => this with
    {
        After = After?.ToArray(), AfterEvents = AfterEvents?.ToArray(), AfterCounters = AfterCounters?.ToArray(),
        OnFinishEvents = OnFinishEvents?.ToArray(), OnFinishCounters = OnFinishCounters?.ToArray()
    };
}

/// <summary>Copyable, multiply awaitable handle. Hazards observe Computation; callers normally await cleanup.</summary>
public readonly struct JobHandle
{
    private readonly SubmissionCompletion? _completion;
    internal JobHandle(SubmissionCompletion completion) => _completion = completion;
    public Query? Query => _completion?.Query;
    public bool IsCompleted => _completion?.Cleaned.Task.IsCompleted ?? true;
    public bool IsComputationCompleted => _completion?.Computed.Task.IsCompleted ?? true;
    public bool IsFaulted => _completion?.Cleaned.Task.IsFaulted ?? false;
    public bool IsCanceled => _completion?.Cleaned.Task.IsCanceled ?? false;
    public ValueTask Completion => new(_completion?.Cleaned.Task ?? Task.CompletedTask);
    public ValueTask Computation => new(_completion?.Computed.Task ?? Task.CompletedTask);
    public TaskAwaiter GetAwaiter() => (_completion?.Cleaned.Task ?? Task.CompletedTask).GetAwaiter();
    public Task AsTask() => _completion?.Cleaned.Task ?? Task.CompletedTask;
    public static JobHandle Completed => default;
}

internal sealed class SubmissionCompletion
{
    internal Query? Query;
    internal readonly TaskCompletionSource Computed = JobEvent.NewSignal();
    internal readonly TaskCompletionSource Cleaned = JobEvent.NewSignal();
}
