using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sugoi.Data;

/// <summary>Owns inline/heap transitions and element lifetime; all byte geometry comes from BufferLayout.</summary>
public sealed unsafe class BufferOps
{
    public BufferLayout Layout { get; }
    public ComponentOps Elements { get; }
    internal BufferOps(BufferLayout layout, ComponentOps elements) { Layout = layout; Elements = elements; }

    public int Count(nint address) => checked((int)((BufferHeader*)address)->Count);
    public int Capacity(nint address) => checked((int)((BufferHeader*)address)->Capacity);
    public nint Data(nint address) => ((BufferHeader*)address)->Data;
    public bool IsInline(nint address) => ((BufferHeader*)address)->Capacity <= (ulong)Layout.InlineCapacity;

    public void Construct(nint address)
    {
        var header = (BufferHeader*)address;
        header->Data = address + Layout.InlineOffset;
        header->Count = 0;
        header->Capacity = (ulong)Layout.InlineCapacity;
    }

    public void Destroy(in ComponentContext context, nint address)
    {
        var header = (BufferHeader*)address;
        if (!Elements.CanBulkDestroy)
            for (int i = 0, count = Count(address); i < count; i++) Elements.Destroy(context, header->Data + checked(i * Layout.ElementSize));
        if (!IsInline(address)) NativeMemory.AlignedFree((void*)header->Data);
        Construct(address);
    }

    public void Reserve(in ComponentContext context, nint address, int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        var header = (BufferHeader*)address;
        if ((ulong)capacity <= header->Capacity) return;
        Reallocate(context, address, capacity);
    }

    private void Reallocate(in ComponentContext context, nint address, int capacity)
    {
        var header = (BufferHeader*)address;
        if ((ulong)capacity < header->Count) throw new ArgumentOutOfRangeException(nameof(capacity));
        capacity = Math.Max(capacity, Layout.InlineCapacity);
        bool toInline = capacity == Layout.InlineCapacity;
        if ((ulong)capacity == header->Capacity) return;
        nint next = toInline ? address + Layout.InlineOffset :
            (nint)NativeMemory.AlignedAlloc((nuint)Layout.HeapBytes(capacity), (nuint)Layout.HeapAlignment);
        if (next == 0) throw new OutOfMemoryException();
        if (Elements.CanBulkMove)
            NativeMemory.Copy((void*)header->Data, (void*)next, checked((nuint)header->Count * (nuint)Layout.ElementSize));
        else
            for (int i = 0, count = Count(address); i < count; i++)
                Elements.Move(context, header->Data + checked(i * Layout.ElementSize), context, next + checked(i * Layout.ElementSize));
        if (!IsInline(address)) NativeMemory.AlignedFree((void*)header->Data);
        header->Data = next;
        header->Capacity = (ulong)capacity;
    }

    public void TrimExcess(in ComponentContext context, nint address)
    {
        int count = Count(address), capacity = Capacity(address);
        // Preserve SkrBase's conservative shrinking threshold.
        if (3L * count < 2L * capacity && (capacity - count > 64 || count == 0))
            Reallocate(context, address, Math.Max(count, Layout.InlineCapacity));
    }

    public void ShrinkToFit(in ComponentContext context, nint address) => Reallocate(context, address, Math.Max(Count(address), Layout.InlineCapacity));

    private void Grow(in ComponentContext context, nint address, int required)
    {
        int current = Capacity(address);
        if (required > current) Reserve(context, address, BufferLayout.GrowthCapacity(required, current));
    }

    public void Resize(in ComponentContext context, nint address, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        int oldCount = Count(address);
        Grow(context, address, count);
        var header = (BufferHeader*)address;
        if (count > oldCount)
        {
            if (Elements.CanBulkConstruct)
                NativeMemory.Clear((void*)(header->Data + checked(oldCount * Layout.ElementSize)), checked((nuint)(count - oldCount) * (nuint)Layout.ElementSize));
            else
                for (int i = oldCount; i < count; i++) Elements.Construct(context, header->Data + checked(i * Layout.ElementSize));
        }
        else if (!Elements.CanBulkDestroy)
            for (int i = count; i < oldCount; i++) Elements.Destroy(context, header->Data + checked(i * Layout.ElementSize));
        header->Count = (ulong)count;
    }

    /// <summary>Deep copies elements into an uninitialized destination buffer.</summary>
    public void Copy(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination)
        => CopyCore(sourceContext, source, destinationContext, destination, preserveCapacity: false);

    internal void CopyPreservingCapacity(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination)
        => CopyCore(sourceContext, source, destinationContext, destination, preserveCapacity: true);

    private void CopyCore(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination, bool preserveCapacity)
    {
        if (source == destination) return;
        if (!Elements.CanCopy) throw new InvalidOperationException("The buffer element cannot be copied.");
        Construct(destination);
        int count = Count(source);
        Reserve(destinationContext, destination, preserveCapacity ? Capacity(source) : count);
        var from = (BufferHeader*)source;
        var to = (BufferHeader*)destination;
        if (Elements.CanBulkCopy)
        {
            NativeMemory.Copy((void*)from->Data, (void*)to->Data, checked((nuint)count * (nuint)Layout.ElementSize));
            to->Count = (ulong)count;
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                Elements.Copy(sourceContext, from->Data + checked(i * Layout.ElementSize), destinationContext, to->Data + checked(i * Layout.ElementSize));
                to->Count++;
            }
        }
    }

    /// <summary>Transfers heap ownership or rebinds inline storage. Source becomes an empty inline buffer.</summary>
    public void Move(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination)
    {
        if (source == destination) return;
        var from = (BufferHeader*)source;
        var to = (BufferHeader*)destination;
        if (!IsInline(source)) *to = *from;
        else
        {
            Construct(destination);
            int count = Count(source);
            if (Elements.CanBulkMove)
                NativeMemory.Copy((void*)from->Data, (void*)to->Data, checked((nuint)count * (nuint)Layout.ElementSize));
            else
                for (int i = 0; i < count; i++)
                    Elements.Move(sourceContext, from->Data + checked(i * Layout.ElementSize), destinationContext, to->Data + checked(i * Layout.ElementSize));
            to->Count = (ulong)count;
        }
        Construct(source);
    }

    public void Remap(nint address, EntityRemapper remapper)
    {
        if (!Elements.HasReferences) return;
        var header = (BufferHeader*)address;
        for (int i = 0, count = Count(address); i < count; i++) Elements.Remap(header->Data + checked(i * Layout.ElementSize), remapper);
    }

    public void Append<T>(in ComponentContext context, nint address, in T value) where T : unmanaged
    {
        if (Unsafe.SizeOf<T>() != Layout.ElementSize) throw new ArgumentException("Buffer element type mismatch.");
        if (!Elements.CanCopy) throw new InvalidOperationException("The buffer element cannot be copied.");
        // The argument may alias an element whose storage Reserve moves. Owning values must
        // be cloned before relocation: a shallow pointer snapshot is insufficient for custom moves.
        T copy = value;
        int count = Count(address);
        if (Elements.CanBulkCopy)
        {
            Grow(context, address, checked(count + 1));
            var header = (BufferHeader*)address;
            Elements.Copy(context, (nint)Unsafe.AsPointer(ref copy), context, header->Data + checked(count * Layout.ElementSize));
            header->Count++;
            return;
        }
        T ownedCopy = default;
        nint temporary = (nint)Unsafe.AsPointer(ref ownedCopy);
        Elements.Copy(context, (nint)Unsafe.AsPointer(ref copy), context, temporary);
        bool moved = false;
        try
        {
            Grow(context, address, checked(count + 1));
            var header = (BufferHeader*)address;
            Elements.Move(context, temporary, context, header->Data + checked(count * Layout.ElementSize));
            moved = true;
            header->Count++;
        }
        finally { if (!moved) Elements.Destroy(context, temporary); }
    }

    internal void AppendMove<T>(in ComponentContext context, nint address, ref T value) where T : unmanaged
    {
        if (Unsafe.SizeOf<T>() != Layout.ElementSize) throw new ArgumentException("Buffer element type mismatch.");
        int count = Count(address);
        Grow(context, address, checked(count + 1));
        var header = (BufferHeader*)address;
        Elements.Move(context, (nint)Unsafe.AsPointer(ref value), context, header->Data + checked(count * Layout.ElementSize));
        header->Count++;
    }

    /// <summary>Replaces the complete buffer, including when the input span aliases the current buffer.</summary>
    public void Set<T>(in ComponentContext context, nint address, ReadOnlySpan<T> values) where T : unmanaged
    {
        if (Unsafe.SizeOf<T>() != Layout.ElementSize) throw new ArgumentException("Buffer element type mismatch.");
        if (!Elements.CanCopy) throw new InvalidOperationException("The buffer element cannot be copied.");
        nint temporary = (nint)NativeMemory.AlignedAlloc((nuint)Layout.StorageSize, (nuint)Layout.StorageAlignment);
        if (temporary == 0) throw new OutOfMemoryException();
        Construct(temporary);
        try
        {
            Reserve(context, temporary, values.Length);
            var header = (BufferHeader*)temporary;
            fixed (T* source = values)
            {
                if (Elements.CanBulkCopy)
                {
                    NativeMemory.Copy(source, (void*)header->Data, checked((nuint)values.Length * (nuint)Layout.ElementSize));
                    header->Count = (ulong)values.Length;
                }
                else
                    for (int i = 0; i < values.Length; i++)
                    {
                        Elements.Copy(context, (nint)(source + i), context, header->Data + checked(i * Layout.ElementSize));
                        header->Count++;
                    }
            }
            Destroy(context, address);
            Move(context, temporary, context, address);
        }
        finally
        {
            Destroy(context, temporary);
            NativeMemory.AlignedFree((void*)temporary);
        }
    }

    public void RemoveAt(in ComponentContext context, nint address, int index)
    {
        int count = Count(address);
        if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
        var header = (BufferHeader*)address;
        nint item = header->Data + checked(index * Layout.ElementSize);
        Elements.Destroy(context, item);
        if (Elements.CanBulkMove)
        {
            int bytes = checked((count - index - 1) * Layout.ElementSize);
            new ReadOnlySpan<byte>((void*)(item + Layout.ElementSize), bytes).CopyTo(new Span<byte>((void*)item, bytes));
        }
        else
            for (int i = index; i < count - 1; i++)
                Elements.Move(context, header->Data + checked((i + 1) * Layout.ElementSize), context, header->Data + checked(i * Layout.ElementSize));
        header->Count--;
    }
}

internal sealed class BufferComponentOps : ComponentOps
{
    private readonly BufferOps _buffer;
    public override BufferOps Buffer => _buffer;
    public override bool IsTrivial => false;
    public override bool HasReferences => _buffer.Elements.HasReferences;
    public override bool HasResources => _buffer.Elements.HasResources;
    public override bool CanCopy => _buffer.Elements.CanCopy;
    internal BufferComponentOps(ComponentLayout layout, BufferOps buffer) : base(layout) => _buffer = buffer;
    public override void Construct(in ComponentContext context, nint destination) => _buffer.Construct(destination);
    public override void Copy(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination) => _buffer.Copy(sourceContext, source, destinationContext, destination);
    public override void Move(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination) => _buffer.Move(sourceContext, source, destinationContext, destination);
    public override void Destroy(in ComponentContext context, nint value) => _buffer.Destroy(context, value);
    public override void Remap(nint value, EntityRemapper remapper) => _buffer.Remap(value, remapper);
    public override void ScanResources(nint value, ResourceVisitor visitor) => _buffer.Elements.ScanResources(_buffer.Data(value), _buffer.Count(value), visitor);
}

/// <summary>A synchronous borrowed buffer. A World structure change invalidates the borrow.</summary>
public readonly unsafe ref struct BufferColumn<T> where T : unmanaged
{
    private readonly nint _address;
    private readonly BufferOps _ops;
    private readonly ComponentContext _context;
    public BufferColumn(nint address, BufferOps ops, ComponentContext context)
    {
        if (Unsafe.SizeOf<T>() != ops.Layout.ElementSize) throw new ArgumentException("Buffer element type mismatch.");
        _address = address; _ops = ops; _context = context;
    }
    public int Count => _ops.Count(_address);
    public int Capacity => _ops.Capacity(_address);
    public bool IsInline => _ops.IsInline(_address);
    public Span<T> Span => new((void*)_ops.Data(_address), Count);
    public ref T this[int index] => ref Span[index];
    public void Add(in T value) => _ops.Append(_context, _address, value);
    public void Set(ReadOnlySpan<T> values) => _ops.Set(_context, _address, values);
    public void Reserve(int capacity) => _ops.Reserve(_context, _address, capacity);
    public void Resize(int count) => _ops.Resize(_context, _address, count);
    public void TrimExcess() => _ops.TrimExcess(_context, _address);
    public void ShrinkToFit() => _ops.ShrinkToFit(_context, _address);
    public void RemoveAt(int index) => _ops.RemoveAt(_context, _address, index);
    public void Clear() => _ops.Resize(_context, _address, 0);
}
