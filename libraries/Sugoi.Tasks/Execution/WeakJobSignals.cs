using System.Diagnostics.CodeAnalysis;

namespace Sugoi.Tasks;

/// <summary>Non-owning event reference used by task options. Resolve once; a successful wait holds the resolved owner.</summary>
public readonly struct WeakJobEvent
{
    private readonly WeakReference<JobEvent>? _target;
    public WeakJobEvent(JobEvent target) => _target = new(target ?? throw new ArgumentNullException(nameof(target)));
    public bool TryGetTarget([NotNullWhen(true)] out JobEvent? target)
    {
        if (_target is not null) return _target.TryGetTarget(out target);
        target = null; return false;
    }
    public bool IsAlive => TryGetTarget(out _);
    public static implicit operator WeakJobEvent(JobEvent target) => new(target);
}

/// <summary>Non-owning counter reference. Expired task-option dependencies and finish notifications are skipped.</summary>
public readonly struct WeakJobCounter
{
    private readonly WeakReference<JobCounter>? _target;
    public WeakJobCounter(JobCounter target) => _target = new(target ?? throw new ArgumentNullException(nameof(target)));
    public bool TryGetTarget([NotNullWhen(true)] out JobCounter? target)
    {
        if (_target is not null) return _target.TryGetTarget(out target);
        target = null; return false;
    }
    public bool IsAlive => TryGetTarget(out _);
    public static implicit operator WeakJobCounter(JobCounter target) => new(target);
}
