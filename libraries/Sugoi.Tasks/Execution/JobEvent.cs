namespace Sugoi.Tasks;

/// <summary>A reusable manual-reset event. Every call returns an independently awaitable observation.</summary>
public sealed class JobEvent
{
    private readonly object _gate = new();
    private TaskCompletionSource _signal = NewSignal();
    private bool _isSet;

    public JobEvent(bool signaled = false) { if (signaled) Set(); }
    public bool IsSet { get { lock (_gate) return _isSet; } }

    public void Set()
    {
        TaskCompletionSource signal;
        lock (_gate) { if (_isSet) return; _isSet = true; signal = _signal; }
        signal.TrySetResult();
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (!_isSet) return;
            _isSet = false;
            _signal = NewSignal();
        }
    }

    public ValueTask WaitAsync(CancellationToken cancellationToken = default)
    {
        Task task;
        lock (_gate) task = _isSet ? Task.CompletedTask : _signal.Task;
        if (task.IsCompleted) return new(cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task);
        return WaitWithOwnerAsync(this, task, cancellationToken);
    }

    private static async ValueTask WaitWithOwnerAsync(JobEvent owner, Task task, CancellationToken cancellationToken)
    {
        try { await (cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task); }
        finally { GC.KeepAlive(owner); }
    }

    internal static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
