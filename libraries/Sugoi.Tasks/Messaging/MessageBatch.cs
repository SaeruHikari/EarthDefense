using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sugoi.Data;

namespace Sugoi.Tasks;

public delegate void RawMessageConsumer(in MessageBatchView batch);
public delegate void MessageVisitor<T>(Entity target, ref T message) where T : unmanaged;

/// <summary>Borrowed contiguous SoA payload and Entity64 targets. The owning consume callback bounds this view's lifetime.</summary>
public readonly unsafe ref struct MessageBatchView
{
    internal MessageBatchView(MessageDescriptor descriptor, nint values, nint targets, int count)
    { Descriptor = descriptor; _values = values; _targets = targets; Count = count; }
    private readonly nint _values, _targets;
    public MessageDescriptor Descriptor { get; }
    public int Count { get; }
    public ReadOnlySpan<Entity> Targets => new((void*)_targets, Count);
    public Span<byte> Bytes => new((void*)_values, checked(Count * Descriptor.Layout.Size));
    public Span<T> Values<T>() where T : unmanaged
    {
        if (Descriptor.RuntimeType != typeof(T)) throw new InvalidOperationException("Message view type differs from its registered representation.");
        return new((void*)_values, Count);
    }
    internal nint ValueAddress(int index) => _values + checked(index * Descriptor.Layout.Size);
}

/// <summary>Owns native message payloads after materialization. Writable payloads allow explicit move-out before destruction.</summary>
public unsafe class MessageBatch : IDisposable
{
    private nint _values, _targets;
    private int _count;
    private bool _disposed;
    internal MessageBatch(MessageDescriptor descriptor, int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Descriptor = descriptor; Capacity = capacity;
        if (capacity == 0) return;
        _values = MessageMemory.Allocate(descriptor.Layout.PayloadBytes(capacity), descriptor.Layout.Alignment);
        try { _targets = MessageMemory.Allocate(MessageLayout.EntityBytes(capacity), sizeof(Entity)); }
        catch { MessageMemory.Free(_values); _values = 0; throw; }
    }
    public MessageDescriptor Descriptor { get; }
    public int Capacity { get; }
    public int Count => _count;
    public bool IsDisposed => _disposed;
    public ReadOnlySpan<Entity> Targets { get { EnsureAlive(); return new((void*)_targets, _count); } }
    public Span<byte> Bytes { get { EnsureAlive(); return new((void*)_values, checked(_count * Descriptor.Layout.Size)); } }
    public MessageBatchView View { get { EnsureAlive(); return new(Descriptor, _values, _targets, _count); } }
    public Span<T> Values<T>() where T : unmanaged => View.Values<T>();

    internal void AppendMoved(Entity target, nint value)
    {
        if (_count == Capacity) throw new InvalidOperationException("Materialized message capacity exceeded.");
        Descriptor.Operations.Move(value, _values + checked(_count * Descriptor.Layout.Size));
        ((Entity*)_targets)[_count++] = target;
    }
    internal void AppendMoved(in MessageBatchView view)
    {
        if (view.Count > Capacity - _count) throw new InvalidOperationException("Materialized message capacity exceeded.");
        if (Descriptor.Operations.IsTrivial)
        {
            fixed (Entity* targets = view.Targets)
            fixed (byte* values = view.Bytes)
            {
                NativeMemory.Copy(targets, (Entity*)_targets + _count, (nuint)MessageLayout.EntityBytes(view.Count));
                NativeMemory.Copy(values, (byte*)_values + _count * Descriptor.Layout.Size, (nuint)checked(view.Count * Descriptor.Layout.Size));
            }
            _count += view.Count;
        }
        else for (int i = 0; i < view.Count; i++) AppendMoved(view.Targets[i], view.ValueAddress(i));
    }

    private void EnsureAlive() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        List<Exception>? failures = null;
        try
        {
            for (int i = 0; i < _count; i++)
                try { Descriptor.Operations.Destroy(_values + checked(i * Descriptor.Layout.Size)); }
                catch (Exception exception) { (failures ??= new()).Add(exception); }
        }
        finally
        {
            MessageMemory.Free(_values); MessageMemory.Free(_targets);
            _values = _targets = 0; _count = 0;
        }
        if (failures is not null) throw new AggregateException("Message destructor violated its no-throw contract.", failures);
    }
}

public sealed class MessageBatch<T> : IDisposable where T : unmanaged
{
    internal MessageBatch(MessageBatch owner) => Owner = owner;
    internal MessageBatch Owner { get; }
    public int Count => Owner.Count;
    public ReadOnlySpan<Entity> Targets => Owner.Targets;
    public Span<T> Values => Owner.Values<T>();
    public Span<T> Messages => Values;
    public void Dispose() => Owner.Dispose();
}

internal static unsafe class MessageMemory
{
    internal static nint Allocate(int bytes, int alignment)
    {
        if (bytes == 0) return 0;
        int actualAlignment = Math.Max(IntPtr.Size, alignment);
        nint address = (nint)NativeMemory.AlignedAlloc((nuint)LayoutMath.AlignUp(bytes, actualAlignment), (nuint)actualAlignment);
        if (address == 0) throw new OutOfMemoryException();
        return address;
    }
    internal static void Free(nint address) { if (address != 0) NativeMemory.AlignedFree((void*)address); }
}
