using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>Combines World, query subscriptions and task lifetime without introducing a second storage implementation.</summary>
public sealed class ScheduledWorld : IAsyncDisposable
{
    private readonly bool _ownsWorld;
    private readonly object _gate = new();
    private Task? _disposeTask;
    private bool _closing;

    public ScheduledWorld(World world, Scheduler scheduler, bool ownsWorld = true)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _ownsWorld = ownsWorld;
        Messages = scheduler.GetMessageBus(world);
    }
    public World World { get; }
    public Scheduler Scheduler { get; }
    public MessageBus Messages { get; }

    public JobHandle Dispatch<TJob>(in TJob job, TaskOptions options = default) where TJob : IQueryJob
    { lock (_gate) { EnsureOpen(); return Scheduler.Dispatch(World, in job, options with { MessageBus = Messages }); } }
    public JobHandle Dispatch<TJob>(Query query, in TJob job, TaskOptions options = default) where TJob : IQueryJob
    {
        lock (_gate) { EnsureOpen(); CheckQuery(query); return Scheduler.Dispatch(query, in job, options with { MessageBus = Messages }); }
    }
    public JobHandle DispatchEntities<TJob>(ReadOnlySpan<Entity> entities, in TJob job, TaskOptions options = default) where TJob : IQueryJob
    { lock (_gate) { EnsureOpen(); return Scheduler.DispatchEntities(World, entities, in job, options with { MessageBus = Messages }); } }
    public MessageSubscription<T> Subscribe<T>(Query query, int capacity = 8192) where T : unmanaged
    { lock (_gate) { EnsureOpen(); CheckQuery(query); return Messages.Subscribe<T>(query, capacity); } }
    public JobHandle DispatchMessages<TMessage, TJob>(MessageSubscription<TMessage> subscription, in TJob job, TaskOptions options = default)
        where TMessage : unmanaged where TJob : IMessageJob<TMessage>
    { lock (_gate) { EnsureOpen(); CheckQuery(subscription.Query); return Scheduler.DispatchMessages(subscription, in job, options); } }

    public async ValueTask ReleaseQueryAsync(Query query)
    {
        CheckQuery(query);
        try { await Scheduler.CloseQueryAsync(query); }
        finally { try { Messages.Unsubscribe(query); } finally { query.Dispose(); } }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is not null) return new(_disposeTask);
            _closing = true;
            _disposeTask = DisposeCoreAsync();
            return new(_disposeTask);
        }
    }
    private async Task DisposeCoreAsync()
    {
        try { await Scheduler.CloseWorldAsync(World).ConfigureAwait(false); }
        finally { try { Messages.Dispose(); } finally { if (_ownsWorld) World.Dispose(); } }
    }
    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_closing, this);
    private void CheckQuery(Query query)
    { if (!ReferenceEquals(query.World, World)) throw new ArgumentException("Query belongs to another World.", nameof(query)); }
}
