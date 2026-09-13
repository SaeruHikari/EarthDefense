using Sugoi.Data;

namespace Sugoi.Tasks;

internal sealed class WorkUnit(long chunkId)
{
    internal long ChunkId { get; } = chunkId;
    internal readonly List<WorkRange> Ranges = new();
    internal readonly HashSet<Task> Dependencies = new();
    internal readonly TaskCompletionSource Finish = JobEvent.NewSignal();
}

internal readonly record struct WorkRange(QueryRange Range, int Index);

internal abstract class Submission
{
    internal required World World;
    internal required Query Query;
    internal required bool OwnsQuery;
    internal required ComponentAccess[] Accesses;
    internal required TaskOptions Options;
    internal required IDisposable UsageLease;
    internal required IDisposable QueryLease;
    internal Entity[]? Entities;
    internal required SubmissionCompletion Completion;
    internal readonly List<WorkUnit> Units = new();
    internal readonly HashSet<Task> TaskDependencies = new();
    internal bool Serial;
    internal bool SerialBatches;
    internal int CompletionCommitted;
    internal PrefetchPlan PrefetchPlan;
    private int _execCounter;
    internal int NextTaskIndex() => Interlocked.Increment(ref _execCounter) - 1;
    internal virtual bool IsAsynchronous => false;
    internal abstract void ExecuteBatch(QueryRange range, int index);
    internal virtual ValueTask ExecuteBatchAsync(QueryRange range, int index)
    { ExecuteBatch(range, index); return ValueTask.CompletedTask; }
    internal virtual void ReleasePayload() { }

    internal virtual void Materialize()
    {
        QueryRange[] ranges;
        if (Entities is null) ranges = Query.GetWorkRanges();
        else
        {
            // The original run-with path batches the explicit list without applying the job's query filter.
            ranges = World.GetEntityRanges(Entities);
        }
        BuildUnits(ranges);
    }

    protected void BuildUnits(QueryRange[] ranges)
    {
        var indexed = new WorkRange[ranges.Length];
        int index = 0;
        for (int i = 0; i < ranges.Length; i++)
        {
            indexed[i] = new(ranges[i], index);
            index = checked(index + ranges[i].Count);
        }
        BuildUnits(indexed);
    }

    protected void BuildUnits(IReadOnlyList<WorkRange> ranges)
    {
        var chunks = new Dictionary<long, WorkUnit>();
        foreach (var work in ranges)
        {
            var range = work.Range;
            if (!chunks.TryGetValue(range.ChunkId, out var unit))
            {
                chunks.Add(range.ChunkId, unit = new WorkUnit(range.ChunkId));
                Units.Add(unit);
            }
            unit.Ranges.Add(work);
        }
        Serial = Options.NoParallelization || Accesses.Any(access => access.Mode == AccessMode.Random && access.Write);
        SerialBatches = Serial || Accesses.Any(access => access.Write && access.Type.IsChunk);
    }
}

internal sealed class QuerySubmission<TJob> : Submission where TJob : IQueryJob
{
    internal required TJob Job;
    internal override bool IsAsynchronous => Job is IAsyncQueryJob;
    internal override void ExecuteBatch(QueryRange range, int index)
    {
        Prefetch.Apply(range, PrefetchPlan);
        var body = JobBody.Copy(in Job);
        try
        {
            int taskIndex = NextTaskIndex();
            using var activity = JobDiagnostics.StartBatch(Options.DebugName, range, index, taskIndex);
            var context = new JobContext(range, index, taskIndex, Options.MessageBus, Options.CancellationToken);
            body.Execute(in context);
        }
        finally { JobBody.Dispose(ref body); }
    }

    internal override ValueTask ExecuteBatchAsync(QueryRange range, int index)
    {
        if (Job is not IAsyncQueryJob) { ExecuteBatch(range, index); return ValueTask.CompletedTask; }
        return ExecuteOwnedAsync(range, index);
    }

    private async ValueTask ExecuteOwnedAsync(QueryRange range, int index)
    {
        Prefetch.Apply(range, PrefetchPlan);
        var body = JobBody.Copy(in Job);
        // Retain the same boxed value for execution and cleanup when the async body is a struct.
        var asyncBody = (IAsyncQueryJob)body;
        try
        {
            int taskIndex = NextTaskIndex();
            using var activity = JobDiagnostics.StartBatch(Options.DebugName, range, index, taskIndex);
            await asyncBody.ExecuteAsync(new AsyncJobContext(range, index, taskIndex, Options.MessageBus, Options.CancellationToken));
        }
        finally
        {
            if (asyncBody is IDisposable owner) owner.Dispose();
            body = default!;
        }
    }

    internal override void ReleasePayload() => JobBody.Dispose(ref Job);
}
