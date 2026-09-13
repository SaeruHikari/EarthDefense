using System.Collections.Concurrent;

namespace Sugoi.Tasks;

/// <summary>Fixed worker budget; awaited continuations return to the worker where a job first ran.</summary>
public sealed class WorkerExecutor : IAsyncDisposable
{
    private readonly Worker[] _workers;
    private readonly object _lifetime = new();
    private readonly JobCounter _roots = new();
    private readonly HashSet<Task> _suspendedOperations = new();
    private int _nextWorker = -1;
    private bool _closing;
    private Task? _disposeTask;

    public WorkerExecutor(int workerCount = 0, string threadName = "Sugoi")
    {
        if (workerCount < 0) throw new ArgumentOutOfRangeException(nameof(workerCount));
        WorkerCount = workerCount == 0 ? Math.Max(1, Environment.ProcessorCount) : workerCount;
        _workers = new Worker[WorkerCount];
        for (int i = 0; i < WorkerCount; i++) _workers[i] = new Worker(this, i, threadName);
        foreach (var worker in _workers) worker.Thread.Start();
    }

    public int WorkerCount { get; }
    public bool IsWorkerThread => SynchronizationContext.Current is WorkerContext context && ReferenceEquals(context.Worker.Owner, this) && Thread.CurrentThread == context.Worker.Thread;
    public int CurrentWorkerIndex => SynchronizationContext.Current is WorkerContext context && ReferenceEquals(context.Worker.Owner, this) && Thread.CurrentThread == context.Worker.Thread ? context.Worker.Index : -1;

    public ValueTask RunAsync(Action action, int workerIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync(() => { action(); return ValueTask.CompletedTask; }, workerIndex);
    }

    public ValueTask RunAsync(Func<ValueTask> action, int workerIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (workerIndex < -1 || workerIndex >= WorkerCount) throw new ArgumentOutOfRangeException(nameof(workerIndex));
        var completion = JobEvent.NewSignal();
        lock (_lifetime)
        {
            // Disposal closes host admission; already running roots may still spawn the children they must join.
            ObjectDisposedException.ThrowIf(_closing && !IsWorkerThread, this);
            _roots.Add();
            int selected = workerIndex >= 0 ? workerIndex : (int)((uint)Interlocked.Increment(ref _nextWorker) % (uint)WorkerCount);
            _workers[selected].Enqueue(() => RunRoot(action, completion));
        }
        return new(completion.Task);
    }

    private async void RunRoot(Func<ValueTask> action, TaskCompletionSource completion)
    {
        Task? suspended = null;
        try
        {
            ValueTask operation = action();
            if (operation.IsCompleted) operation.GetAwaiter().GetResult();
            else
            {
                suspended = operation.AsTask();
                // A scheduler owns suspended fibers. Keep the corresponding async operation rooted too:
                // an event -> continuation -> event cycle alone does not make either object a GC root.
                lock (_lifetime) _suspendedOperations.Add(suspended);
                await suspended;
            }
            completion.TrySetResult();
        }
        catch (OperationCanceledException exception) { completion.TrySetCanceled(exception.CancellationToken); }
        catch (Exception exception) { completion.TrySetException(exception); }
        finally
        {
            if (suspended is not null) lock (_lifetime) _suspendedOperations.Remove(suspended);
            _roots.Decrement();
        }
    }

    /// <summary>Parallel batch join. A worker caller uses async joining and never blocks its worker.</summary>
    public async ValueTask ForAsync(int count, Action<int> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return;
        int participants = Math.Min(count, WorkerCount);
        var tasks = new Task[participants];
        int next = -1;
        int submitted = 0;
        Exception? admissionFailure = null;
        try
        {
            for (; submitted < participants; submitted++)
                tasks[submitted] = RunAsync(() => { int index; while ((index = Interlocked.Increment(ref next)) < count) action(index); }).AsTask();
        }
        catch (Exception exception) { admissionFailure = exception; }
        // Even partially admitted host batches own their callbacks until all accepted children finish.
        if (submitted != tasks.Length) Array.Resize(ref tasks, submitted);
        try { await Task.WhenAll(tasks); }
        catch (Exception executionFailure)
        {
            if (admissionFailure is not null) throw new AggregateException(admissionFailure, executionFailure);
            throw;
        }
        if (admissionFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(admissionFailure).Throw();
    }

    /// <summary>Parallel asynchronous bodies retain their worker on awaits; every accepted body is joined.</summary>
    public async ValueTask ForAsync(int count, Func<int, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return;
        int submitted = 0;
        var tasks = new Task[count];
        Exception? admissionFailure = null;
        try
        {
            // Fibers are bounded by ready work, not by the number of workers. All logical
            // bodies must be admitted so a barrier larger than WorkerCount can be reached.
            for (; submitted < count; submitted++)
            {
                int index = submitted;
                tasks[index] = RunAsync(() => action(index)).AsTask();
            }
        }
        catch (Exception exception) { admissionFailure = exception; }
        if (submitted != tasks.Length) Array.Resize(ref tasks, submitted);
        try { await Task.WhenAll(tasks); }
        catch (Exception executionFailure)
        {
            if (admissionFailure is not null) throw new AggregateException(admissionFailure, executionFailure);
            throw;
        }
        if (admissionFailure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(admissionFailure).Throw();
    }

    /// <summary>Data's synchronous batch adapter: nested worker calls run inline to avoid a blocked-pool join.</summary>
    public void For(int count, Action<int> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (IsWorkerThread) { for (int i = 0; i < count; i++) action(i); }
        else ForAsync(count, action).AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DrainAsync() => _roots.WaitAsync();

    public ValueTask DisposeAsync()
    {
        lock (_lifetime)
        {
            if (_disposeTask is not null) return new(_disposeTask);
            if (IsWorkerThread) throw new InvalidOperationException("Dispose the executor from its host after its jobs complete.");
            _closing = true;
            _disposeTask = StopAsync();
            return new(_disposeTask);
        }
    }

    private async Task StopAsync()
    {
        await _roots.WaitAsync().ConfigureAwait(false);
        foreach (var worker in _workers) worker.Stop();
        foreach (var worker in _workers) { worker.Thread.Join(); worker.Wake.Dispose(); }
    }

    private sealed class Worker
    {
        internal readonly WorkerExecutor Owner;
        internal readonly int Index;
        internal readonly Thread Thread;
        internal readonly AutoResetEvent Wake = new(false);
        private readonly ConcurrentQueue<Action> _queue = new();
        private volatile bool _stop;

        internal Worker(WorkerExecutor owner, int index, string name)
        {
            Owner = owner; Index = index;
            Thread = new Thread(Loop) { IsBackground = true, Name = $"{name}-{index}" };
        }
        internal void Enqueue(Action action) { _queue.Enqueue(action); Wake.Set(); }
        internal void Stop() { _stop = true; Wake.Set(); }
        private void Loop()
        {
            SynchronizationContext.SetSynchronizationContext(new WorkerContext(this));
            while (true)
            {
                while (_queue.TryDequeue(out var action)) action();
                if (_stop) return;
                Wake.WaitOne();
            }
        }
    }

    private sealed class WorkerContext(Worker worker) : SynchronizationContext
    {
        internal readonly Worker Worker = worker;
        public override void Post(SendOrPostCallback callback, object? state) => Worker.Enqueue(() => callback(state));
        public override void Send(SendOrPostCallback callback, object? state)
        {
            if (Current == this && Thread.CurrentThread == Worker.Thread) callback(state);
            else throw new NotSupportedException("Cross-worker synchronous Send would block execution; use Post or await.");
        }
        public override SynchronizationContext CreateCopy() => this;
    }
}
