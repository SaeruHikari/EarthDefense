namespace Sugoi.Tasks;

/// <summary>Creates an independently owned submission or batch body. The caller retains its original body.</summary>
public interface IJobCloneable<TSelf>
{
    TSelf CloneForBatch();
}

internal static class JobBody
{
    internal static T Copy<T>(in T source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (typeof(T).IsValueType && source is IAsyncQueryJob && source is IDisposable)
            throw new ArgumentException("An owning async job must use a class so state changes across await remain visible to cleanup.");
        if (source is IJobCloneable<T> typed) return typed.CloneForBatch();
        if (typeof(T).IsValueType)
        {
            if (source is IDisposable)
                throw new ArgumentException("A disposable value job must implement IJobCloneable<T> so every copy owns its resources.");
            return source;
        }
        if (source is ICloneableQueryJob cloneable) return (T)cloneable.CloneForBatch();
        throw new ArgumentException("Reference jobs must implement IJobCloneable<T> or ICloneableQueryJob.");
    }

    internal static TJob CopyMessage<TJob, TMessage>(in TJob source)
        where TJob : IMessageJob<TMessage> where TMessage : unmanaged
    {
        if (typeof(TJob).IsValueType && source is IAsyncMessageJob<TMessage> && source is IDisposable)
            throw new ArgumentException("An owning async message job must use a class so cleanup observes its final state.");
        if (source is IJobCloneable<TJob> typed) return typed.CloneForBatch();
        if (!typeof(TJob).IsValueType && source is ICloneableMessageJob<TMessage> legacy)
            return (TJob)legacy.CloneForBatch();
        return Copy(in source);
    }

    internal static void Dispose<T>(ref T body)
    {
        T owned = body;
        body = default!;
        if (owned is IDisposable disposable) disposable.Dispose();
    }
}
