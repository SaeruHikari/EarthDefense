using Sugoi.Data;

namespace Sugoi.Tasks;

public sealed partial class Scheduler
{
    private readonly HashSet<MessageBus> _knownMessageBuses = new();

    public MessageBus GetMessageBus(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            world.EnsureAlive();
            if (_messageBuses.TryGetValue(world, out var existing)) return existing;
            var bus = new MessageBus(world);
            _messageBuses.Add(world, bus);
            _knownMessageBuses.Add(bus);
            _ownedMessageBuses.Add(bus);
            return bus;
        }
    }

    private MessageBus? ResolveMessageBus(World world, MessageBus? supplied)
    {
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            if (supplied is not null)
            {
                if (!ReferenceEquals(supplied.World, world)) throw new ArgumentException("The message bus must belong to the job World.");
                _knownMessageBuses.Add(supplied);
                _messageBuses.TryAdd(world, supplied);
                return supplied;
            }
            return _messageBuses.TryGetValue(world, out var existing) ? existing : null;
        }
    }

    public Query CreateMessageQuery<TMessage, TJob>(MessageBus bus, in TJob job)
        where TMessage : unmanaged where TJob : IMessageJob<TMessage>
    {
        ArgumentNullException.ThrowIfNull(bus);
        using var use = bus.World.AcquireUsage();
        var copy = JobBody.CopyMessage<TJob, TMessage>(in job);
        try
        {
            var builder = new JobAccessBuilder(bus.World);
            copy.Build(builder);
            return bus.World.CreateQuery(builder.Description);
        }
        finally { JobBody.Dispose(ref copy); }
    }

    /// <summary>Like the original dispatch path: if no queue exists at admission, register it and execute no message work.</summary>
    public JobHandle DispatchMessages<TMessage, TJob>(MessageBus bus, in TJob job, Query? reused = null, TaskOptions options = default)
        where TMessage : unmanaged where TJob : IMessageJob<TMessage>
    {
        ArgumentNullException.ThrowIfNull(bus);
        if (reused is not null && !ReferenceEquals(reused.World, bus.World)) throw new ArgumentException("Query belongs to another World.");
        ValidateOptions(options);
        options = options with { MessageBus = ResolveMessageBus(bus.World, bus) };
        var preparation = BeginPreparation(bus.World, reused);
        Query? query = null;
        TJob copy = default!;
        bool copied = false;
        try
        {
            copy = JobBody.CopyMessage<TJob, TMessage>(in job);
            copied = true;
            var builder = new JobAccessBuilder(bus.World);
            copy.Build(builder);
            query = reused ?? bus.World.CreateQuery(builder.Description);
            if (reused is null) preparation.QueryLease = query.AcquireUsage();
            query.SetTaskMeta(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(builder.Description.MetaAll),
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(builder.Description.MetaNone));
            copy.Prepare(query.Count);
            options = options with { DebugName = options.DebugName ?? (copy is IJobDebugInfo named ? named.DebugName : typeof(TJob).Name) };
            var submission = new MessageSubmission<TJob, TMessage>
            {
                World = bus.World, Query = query, OwnsQuery = false, Bus = bus, Subscription = null,
                Accesses = builder.Accesses, Options = options.Snapshot(), Job = copy,
                UsageLease = preparation.WorldLease, QueryLease = preparation.QueryLease!, Completion = preparation.Completion
            };
            // A created message query is deliberately retained for its persistent queue, just as the
            // original returned query. JobHandle.Query lets callers reuse or release it explicitly.
            Register(submission, preparation);
            return new(submission.Completion);
        }
        catch
        {
            try { if (copied) JobBody.Dispose(ref copy); }
            finally { AbortPreparation(preparation, reused is null ? query : null); }
            throw;
        }
    }

    private void UnsubscribeAll(Query query)
    {
        MessageBus[] buses;
        lock (_lifetime) buses = _knownMessageBuses.Where(bus => ReferenceEquals(bus.World, query.World)).ToArray();
        foreach (var bus in buses) bus.Unsubscribe(query);
    }
}
