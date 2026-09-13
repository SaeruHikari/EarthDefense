using System.Collections.Concurrent;
using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>Immediate online ECS scheduling. Callers retain ownership of structural phase boundaries.</summary>
public sealed partial class Scheduler : IAsyncDisposable
{
    private readonly WorkerExecutor _executor;
    private readonly bool _ownsExecutor;
    private readonly object _lifetime = new();
    private readonly ConcurrentQueue<Action> _admission = new();
    private readonly DependencyAnalyzer _analyzer = new();
    private readonly JobCounter _running = new();
    private readonly JobCounter _preparing = new();
    private readonly ConcurrentQueue<Exception> _failures = new();
    private readonly Dictionary<World, HashSet<Submission>> _worldUses = new();
    private readonly HashSet<Preparation> _preparations = new();
    private readonly HashSet<World> _closingWorlds = new();
    private readonly HashSet<Query> _closingQueries = new();
    private readonly Dictionary<World, MessageBus> _messageBuses = new();
    private readonly HashSet<MessageBus> _ownedMessageBuses = new();
    private int _draining;
    private bool _closing;
    private Task? _disposeTask;

    public Scheduler(int workerCount = 0) { _executor = new(workerCount); _ownsExecutor = true; }
    public Scheduler(WorkerExecutor executor) => _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    public WorkerExecutor Executor => _executor;
    public long RunningCount => _running.Count;

    public Query CreateQuery<TJob>(World world, in TJob job) where TJob : IQueryJob
    {
        using var usage = world.AcquireUsage();
        var copy = JobBody.Copy(in job);
        try
        {
            var builder = new JobAccessBuilder(world);
            copy.Build(builder);
            return world.CreateQuery(builder.Description);
        }
        finally { JobBody.Dispose(ref copy); }
    }

    public JobHandle Dispatch<TJob>(World world, in TJob job, TaskOptions options = default) where TJob : IQueryJob
        => DispatchCore(world, null, null, in job, options, true);

    public JobHandle Dispatch<TJob>(Query query, in TJob job, TaskOptions options = default) where TJob : IQueryJob
        => DispatchCore(query.World, query, null, in job, options, true);

    public JobHandle DispatchEntities<TJob>(World world, ReadOnlySpan<Entity> entities, in TJob job, TaskOptions options = default) where TJob : IQueryJob
        => DispatchCore(world, null, entities.ToArray(), in job, options, false);

    public JobHandle DispatchEntities<TJob>(Query query, ReadOnlySpan<Entity> entities, in TJob job, TaskOptions options = default) where TJob : IQueryJob
        => DispatchCore(query.World, query, entities.ToArray(), in job, options, false);

    public JobHandle DispatchMessages<TMessage, TJob>(MessageSubscription<TMessage> subscription, in TJob job, TaskOptions options = default)
        where TMessage : unmanaged where TJob : IMessageJob<TMessage>
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (job is null) throw new ArgumentNullException(nameof(job));
        ValidateOptions(options);
        options = options with { MessageBus = ResolveMessageBus(subscription.Query.World, subscription.Bus) };
        var preparation = BeginPreparation(subscription.Query.World, subscription.Query);
        TJob copy = default!;
        bool copied = false;
        try
        {
            copy = JobBody.CopyMessage<TJob, TMessage>(in job);
            copied = true;
            var builder = new JobAccessBuilder(subscription.Query.World);
            copy.Build(builder);
            subscription.Query.SetTaskMeta(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(builder.Description.MetaAll),
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(builder.Description.MetaNone));
            copy.Prepare(subscription.Query.Count);
            options = options with { DebugName = options.DebugName ?? (copy is IJobDebugInfo named ? named.DebugName : typeof(TJob).Name) };
            var submission = new MessageSubmission<TJob, TMessage>
            {
                World = subscription.Query.World, Query = subscription.Query, OwnsQuery = false,
                Accesses = builder.Accesses, Options = options.Snapshot(), Job = copy, Subscription = subscription, Bus = subscription.Bus,
                UsageLease = preparation.WorldLease, QueryLease = preparation.QueryLease!, Completion = preparation.Completion
            };
            Register(submission, preparation);
            return new(submission.Completion);
        }
        catch
        {
            try { if (copied) JobBody.Dispose(ref copy); }
            finally { AbortPreparation(preparation); }
            throw;
        }
    }

    private JobHandle DispatchCore<TJob>(World world, Query? reused, Entity[]? entities, in TJob job, TaskOptions options, bool prepare) where TJob : IQueryJob
    {
        ArgumentNullException.ThrowIfNull(world);
        if (job is null) throw new ArgumentNullException(nameof(job));
        ValidateOptions(options);
        options = options with { MessageBus = ResolveMessageBus(world, options.MessageBus) };
        // Build and prepare are intentionally synchronous and happen BEFORE any admission gate.
        // The coordinator must not Dispatch across a concurrent structural operation.
        var preparation = BeginPreparation(world, reused);
        Query? query = null;
        TJob copy = default!;
        bool copied = false;
        try
        {
            copy = JobBody.Copy(in job);
            copied = true;
            var builder = new JobAccessBuilder(world);
            copy.Build(builder);
            query = reused ?? world.CreateQuery(builder.Description);
            if (reused is null) preparation.QueryLease = query.AcquireUsage();
            // The source updates meta on the query-based overload, including reused queries.
            if (prepare)
            {
                query.SetTaskMeta(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(builder.Description.MetaAll),
                    System.Runtime.InteropServices.CollectionsMarshal.AsSpan(builder.Description.MetaNone));
                copy.Prepare(query.Count);
            }
            options = options with { DebugName = options.DebugName ?? (copy is IJobDebugInfo named ? named.DebugName : typeof(TJob).Name) };
            var submission = new QuerySubmission<TJob>
            {
                World = world, Query = query, OwnsQuery = reused is null, Entities = entities,
                Job = copy, Accesses = builder.Accesses, Options = options.Snapshot(),
                UsageLease = preparation.WorldLease, QueryLease = preparation.QueryLease!, Completion = preparation.Completion
            };
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

    private sealed class Preparation(World world, Query? query, IDisposable worldLease, IDisposable? queryLease)
    {
        internal readonly World World = world;
        internal readonly Query? Query = query;
        internal readonly IDisposable WorldLease = worldLease;
        internal IDisposable? QueryLease = queryLease;
        internal readonly SubmissionCompletion Completion = new();
    }

    private Preparation BeginPreparation(World world, Query? query)
    {
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            if (_closingWorlds.Contains(world) || query is not null && _closingQueries.Contains(query))
                throw new InvalidOperationException("The scheduled World or Query is being released.");
            var worldLease = world.AcquireUsage();
            try
            {
                var preparation = new Preparation(world, query, worldLease, query?.AcquireUsage());
                _preparations.Add(preparation);
                _preparing.Add();
                return preparation;
            }
            catch { worldLease.Dispose(); throw; }
        }
    }

    private void AbortPreparation(Preparation preparation, Query? ownedQuery = null)
    {
        preparation.QueryLease?.Dispose();
        ownedQuery?.Dispose();
        preparation.WorldLease.Dispose();
        lock (_lifetime) _preparations.Remove(preparation);
        preparation.Completion.Computed.TrySetResult();
        preparation.Completion.Cleaned.TrySetResult();
        _preparing.Decrement();
    }

    private void Register(Submission submission, Preparation preparation)
    {
        submission.Completion.Query = submission.Query;
        lock (_lifetime)
        {
            // Preparation already owns admission. Closing waits it rather than rejecting it halfway through user Build/Prepare.
            _preparations.Remove(preparation);
            if (!_worldUses.TryGetValue(submission.World, out var uses)) _worldUses.Add(submission.World, uses = new());
            uses.Add(submission);
            _running.Add();
            _preparing.Decrement();
        }
        try
        {
            if (HasGate(submission.Options)) _ = ObserveAdmissionAsync(submission);
            else Enqueue(() => Admit(submission));
        }
        catch (Exception exception) { Finish(submission, exception); }
    }

    private async Task ObserveAdmissionAsync(Submission submission)
    {
        try
        {
            await _executor.RunAsync(async () =>
            {
                var options = submission.Options;
                var cancellation = options.CancellationToken;
                if (options.After is { } after) foreach (var handle in after) await handle.AsTask().WaitAsync(cancellation);
                if (options.AfterEvents is { } events) foreach (var weak in events)
                    if (weak.TryGetTarget(out var signal)) await signal.WaitAsync(cancellation);
                if (options.AfterCounters is { } counters) foreach (var weak in counters)
                    if (weak.TryGetTarget(out var counter)) await counter.WaitAsync(cancellation);
                cancellation.ThrowIfCancellationRequested();
                Enqueue(() => Admit(submission));
            }, submission.Options.WorkerIndex ?? -1).ConfigureAwait(false);
        }
        catch (Exception exception) { Finish(submission, exception); }
    }

    private void Admit(Submission submission)
    {
        try
        {
            submission.Options.CancellationToken.ThrowIfCancellationRequested();
            // Message submissions consume/copy now, before component hazard waits, matching the original generator.
            submission.PrefetchPlan = Prefetch.BuildPlan(submission.Accesses, submission.Options, submission.Entities is not null);
            submission.Materialize();
            _analyzer.Analyze(submission);
            _ = ExecuteAsync(submission);
        }
        catch (Exception exception) { Finish(submission, exception); }
    }

    private async Task ExecuteAsync(Submission submission)
    {
        Exception? failure = null;
        try
        {
            if (submission.TaskDependencies.Count != 0) await Task.WhenAll(submission.TaskDependencies);
            if (submission.Serial)
            {
                foreach (var unit in submission.Units) await ExecuteUnitAsync(submission, unit);
            }
            else
            {
                var units = new Task[submission.Units.Count];
                for (int i = 0; i < units.Length; i++) units[i] = ExecuteUnitAsync(submission, submission.Units[i]);
                await Task.WhenAll(units);
            }
        }
        catch (Exception exception) { failure = exception; }
        finally
        {
            // A failed dependency/serial unit must still release every unit fence.
            foreach (var unit in submission.Units)
                Complete(unit.Finish, failure);
            Finish(submission, failure);
        }
    }

    private async Task ExecuteUnitAsync(Submission submission, WorkUnit unit)
    {
        try
        {
            await _executor.RunAsync(async () =>
            {
                if (unit.Dependencies.Count != 0) await Task.WhenAll(unit.Dependencies);
                submission.Options.CancellationToken.ThrowIfCancellationRequested();
                foreach (var work in unit.Ranges)
                {
                    int batchSize = submission.Options.BatchSize;
                    if (batchSize <= 0 || work.Range.Count <= batchSize || submission.SerialBatches || submission.Options.WorkerIndex.HasValue)
                    {
                        if (batchSize <= 0) await submission.ExecuteBatchAsync(work.Range, work.Index);
                        else for (int offset = 0; offset < work.Range.Count; offset += batchSize)
                            await submission.ExecuteBatchAsync(work.Range.Slice(offset, Math.Min(batchSize, work.Range.Count - offset)), work.Index + offset);
                    }
                    else
                    {
                        int batches = checked((work.Range.Count + batchSize - 1) / batchSize);
                        if (submission.IsAsynchronous)
                            await _executor.ForAsync(batches, async batch =>
                            {
                                int offset = batch * batchSize;
                                submission.Options.CancellationToken.ThrowIfCancellationRequested();
                                await submission.ExecuteBatchAsync(work.Range.Slice(offset, Math.Min(batchSize, work.Range.Count - offset)), work.Index + offset);
                            });
                        else
                            await _executor.ForAsync(batches, batch =>
                            {
                                int offset = batch * batchSize;
                                submission.Options.CancellationToken.ThrowIfCancellationRequested();
                                submission.ExecuteBatch(work.Range.Slice(offset, Math.Min(batchSize, work.Range.Count - offset)), work.Index + offset);
                            });
                    }
                }
            }, submission.Options.WorkerIndex ?? -1);
            unit.Finish.TrySetResult();
        }
        catch (Exception exception) { Complete(unit.Finish, exception); throw; }
    }

    private void Finish(Submission submission, Exception? failure)
    {
        // Only one owner can commit computation, payload cleanup, external notification and final completion.
        if (Interlocked.Exchange(ref submission.CompletionCommitted, 1) != 0) return;
        foreach (var unit in submission.Units)
            Complete(unit.Finish, failure);
        Complete(submission.Completion.Computed, failure);
        try { submission.ReleasePayload(); }
        catch (Exception exception) { failure = Combine(failure, exception); }
        try { submission.QueryLease.Dispose(); }
        catch (Exception exception) { failure = Combine(failure, exception); }
        try { if (submission.OwnsQuery) submission.Query.Dispose(); }
        catch (Exception exception) { failure = Combine(failure, exception); }
        // No data access remains. External finish observers may now enter a structural phase.
        try { submission.UsageLease.Dispose(); }
        catch (Exception exception) { failure = Combine(failure, exception); }
        if (submission.Options.OnFinishEvents is { } events)
            foreach (var weak in events) { try { if (weak.TryGetTarget(out var signal)) signal.Set(); } catch (Exception exception) { failure = Combine(failure, exception); } }
        if (submission.Options.OnFinishCounters is { } counters)
            foreach (var weak in counters) { try { if (weak.TryGetTarget(out var counter)) counter.Decrement(); } catch (Exception exception) { failure = Combine(failure, exception); } }
        if (failure is not null) _failures.Enqueue(failure);
        lock (_lifetime)
        {
            if (_worldUses.TryGetValue(submission.World, out var uses)) { uses.Remove(submission); if (uses.Count == 0) _worldUses.Remove(submission.World); }
        }
        _running.Decrement();
        Complete(submission.Completion.Cleaned, failure);
    }

    private void Enqueue(Action action)
    {
        _admission.Enqueue(action);
        if (Interlocked.CompareExchange(ref _draining, 1, 0) == 0) _ = _executor.RunAsync(DrainAdmission);
    }

    private void DrainAdmission()
    {
        do
        {
            while (_admission.TryDequeue(out var action)) action();
            Interlocked.Exchange(ref _draining, 0);
        } while (!_admission.IsEmpty && Interlocked.CompareExchange(ref _draining, 1, 0) == 0);
    }

    /// <summary>Fence of tasks already admitted to the queue. Gated tasks not yet enqueued are deliberately excluded.</summary>
    public ValueTask FlushDispatchAsync()
    {
        var fence = JobEvent.NewSignal();
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            Enqueue(() => fence.TrySetResult());
        }
        return new(fence.Task);
    }

    public async ValueTask SyncAllAsync()
    {
        await _running.WaitAsync();
        Task? closing;
        ValueTask prune;
        lock (_lifetime)
        {
            closing = _disposeTask;
            // Enqueue this maintenance fence before disposal can close the executor.
            prune = closing is null ? PruneAsync() : ValueTask.CompletedTask;
        }
        if (closing is not null)
        {
            // A worker root cannot await disposal of its own executor: disposal is waiting for that root to return.
            if (_executor.IsWorkerThread) { ThrowFailures(); return; }
            await closing;
            return;
        }
        await prune;
        ThrowFailures();
    }

    /// <summary>Waits uses already submitted for this world, including gated tasks. The caller must stop new admissions.</summary>
    public ValueTask SyncWorldAsync(World world)
    {
        Task[] uses;
        lock (_lifetime)
        {
            var pending = _preparations.Where(p => ReferenceEquals(p.World, world)).Select(p => p.Completion.Cleaned.Task)
                .Concat(_creations.Where(c => ReferenceEquals(c.World, world)).Select(c => c.Completion.Task));
            uses = _worldUses.TryGetValue(world, out var active)
                ? active.Select(s => s.Completion.Cleaned.Task).Concat(pending).ToArray() : pending.ToArray();
        }
        return new(Task.WhenAll(uses));
    }

    public async ValueTask ReleaseQueryAsync(Query query)
    {
        try { await CloseQueryAsync(query); }
        finally { try { UnsubscribeAll(query); } finally { query.Dispose(); } }
    }

    internal ValueTask CloseQueryAsync(Query query)
    {
        Task[] uses;
        lock (_lifetime)
        {
            _closingQueries.Add(query);
            var pending = _preparations.Where(p => ReferenceEquals(p.Query, query)).Select(p => p.Completion.Cleaned.Task);
            uses = _worldUses.TryGetValue(query.World, out var active)
                ? active.Where(s => ReferenceEquals(s.Query, query)).Select(s => s.Completion.Cleaned.Task).Concat(pending).ToArray()
                : pending.ToArray();
        }
        return new(Task.WhenAll(uses));
    }

    internal async ValueTask CloseWorldAsync(World world)
    {
        lock (_lifetime) _closingWorlds.Add(world);
        await SyncWorldAsync(world);
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetime)
        {
            if (_disposeTask is not null) return new(_disposeTask);
            if (_executor.IsWorkerThread) throw new InvalidOperationException("Dispose the scheduler from the host after its jobs complete.");
            _closing = true;
            _disposeTask = DisposeCoreAsync();
            return new(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await _preparing.WaitAsync().ConfigureAwait(false);
            await _running.WaitAsync().ConfigureAwait(false);
            await PruneAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                MessageBus[] owned;
                lock (_lifetime) owned = _ownedMessageBuses.ToArray();
                foreach (var bus in owned) bus.Dispose();
            }
            finally { if (_ownsExecutor) await _executor.DisposeAsync().ConfigureAwait(false); }
        }
        ThrowFailures();
    }

    private ValueTask PruneAsync()
    {
        var fence = JobEvent.NewSignal();
        Enqueue(() => { _analyzer.PruneCompleted(); fence.TrySetResult(); });
        return new(fence.Task);
    }

    private void ThrowFailures()
    {
        List<Exception>? failures = null;
        while (_failures.TryDequeue(out var exception)) (failures ??= new()).Add(exception);
        if (failures is not null) throw new AggregateException("One or more ECS submissions failed; all owned work has been drained.", failures);
    }

    private static Exception Combine(Exception? first, Exception second) => first is null ? second : new AggregateException(first, second);
    private static void Complete(TaskCompletionSource completion, Exception? failure)
    {
        if (failure is OperationCanceledException canceled) completion.TrySetCanceled(canceled.CancellationToken);
        else if (failure is not null) completion.TrySetException(failure);
        else completion.TrySetResult();
    }
    private static bool HasGate(TaskOptions options) => options.After?.Count > 0 || options.AfterEvents?.Count > 0 || options.AfterCounters?.Count > 0;
    private void ValidateOptions(TaskOptions options)
    {
        if (options.BatchSize < 0) throw new ArgumentOutOfRangeException(nameof(options), "BatchSize cannot be negative.");
        if (options.WorkerIndex is { } index && (index < 0 || index >= _executor.WorkerCount)) throw new ArgumentOutOfRangeException(nameof(options), "Worker index is out of range.");
    }
}
