namespace SkrGui;

// Source raw pointers express a non-owning borrow. Keep the public object API,
// but do not let a child, State, or slot acquire ownership of its parent/owner.
internal struct BorrowedReference<T> where T:class
{
    private WeakReference<T>? _reference;
    internal T? Value
    {
        get=>_reference!=null&&_reference.TryGetTarget(out var value)?value:null;
        set=>_reference=value==null?null:new WeakReference<T>(value);
    }
}
