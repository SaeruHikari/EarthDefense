namespace Sugoi.Tasks;

/// <summary>Shared nonnegative counter with multi-observer zero and nonzero waits.</summary>
public sealed class JobCounter
{
    private readonly object _gate = new();
    private long _count;
    private TaskCompletionSource _zero = JobEvent.NewSignal();
    private TaskCompletionSource _nonzero = JobEvent.NewSignal();

    public JobCounter(long initialCount = 0)
    {
        if (initialCount < 0) throw new ArgumentOutOfRangeException(nameof(initialCount));
        _count = initialCount;
        if (initialCount == 0) _zero.SetResult(); else _nonzero.SetResult();
    }

    public long Count { get { lock (_gate) return _count; } }

    public void Add(long count = 1)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        TaskCompletionSource? signal = null;
        lock (_gate)
        {
            var next = checked(_count + count);
            if (_count == 0 && next != 0) { _zero = JobEvent.NewSignal(); signal = _nonzero; }
            _count = next;
        }
        signal?.TrySetResult();
    }

    public void Decrement(long count = 1)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        TaskCompletionSource? signal = null;
        lock (_gate)
        {
            if (count > _count) throw new InvalidOperationException("Counter cannot become negative.");
            if (_count != 0 && count == _count) { signal = _zero; _nonzero = JobEvent.NewSignal(); }
            _count -= count;
        }
        signal?.TrySetResult();
    }

    public ValueTask WaitAsync(CancellationToken cancellationToken = default) => WaitForAsync(false, cancellationToken);
    public ValueTask WaitNonzeroAsync(CancellationToken cancellationToken = default) => WaitForAsync(true, cancellationToken);

    private ValueTask WaitForAsync(bool nonzero, CancellationToken cancellationToken)
    {
        Task task;
        lock (_gate) task = nonzero ? _nonzero.Task : _zero.Task;
        if (task.IsCompleted) return new(cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task);
        return WaitWithOwnerAsync(this, task, cancellationToken);
    }

    private static async ValueTask WaitWithOwnerAsync(JobCounter owner, Task task, CancellationToken cancellationToken)
    {
        try { await (cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task); }
        finally { GC.KeepAlive(owner); }
    }
}
