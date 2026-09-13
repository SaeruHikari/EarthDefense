using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sugoi.Data;

public sealed class ComponentLayout : IEquatable<ComponentLayout>
{
    public int Size { get; }
    public int Stride => Size;
    public int Alignment { get; }
    public ComponentKind Kind { get; }
    public BufferLayout? Buffer { get; }

    internal ComponentLayout(int size, int alignment, ComponentKind kind, BufferLayout? buffer = null)
    {
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
        LayoutMath.ValidateAlignment(alignment);
        Size = size;
        Alignment = alignment;
        Kind = kind;
        Buffer = buffer;
    }

    public bool Equals(ComponentLayout? other) => other is not null && Size == other.Size && Alignment == other.Alignment && Kind == other.Kind && Equals(Buffer, other.Buffer);
    public override bool Equals(object? obj) => obj is ComponentLayout other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Size, Alignment, Kind, Buffer);
}

/// <summary>Buffer header and inline payload geometry. Column alignment never changes element stride.</summary>
public sealed record BufferLayout
{
    public int HeaderSize { get; }
    public int HeaderAlignment { get; }
    public int ElementSize { get; }
    public int ElementAlignment { get; }
    public int InlineAlignment { get; }
    public int InlineCapacity { get; }
    public int InlineOffset { get; }
    public int StorageSize { get; }
    public int StorageAlignment { get; }
    public int HeapAlignment => Math.Max(IntPtr.Size, ElementAlignment);

    public int HeapBytes(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        return LayoutMath.AlignUp(checked(capacity * ElementSize), HeapAlignment);
    }

    public static int GrowthCapacity(int required, int current)
    {
        if (required <= current || required <= 0) throw new ArgumentOutOfRangeException(nameof(required));
        // SkrBase default_capicity_policy.hpp: first 4, then requested + 3/8 requested + 16.
        long grown = current == 0 && required <= 4 ? 4 : (long)required + 3L * required / 8 + 16;
        return checked((int)Math.Min(grown, int.MaxValue));
    }

    public BufferLayout(int elementSize, int elementAlignment, int inlineCapacity, int inlineAlignment = 0)
    {
        if (elementSize <= 0) throw new ArgumentOutOfRangeException(nameof(elementSize));
        if (inlineCapacity < 0) throw new ArgumentOutOfRangeException(nameof(inlineCapacity));
        LayoutMath.ValidateAlignment(elementAlignment);
        HeaderSize = Unsafe.SizeOf<BufferHeader>();
        HeaderAlignment = IntPtr.Size;
        ElementSize = elementSize;
        ElementAlignment = elementAlignment;
        InlineAlignment = inlineAlignment == 0 ? Math.Max(HeaderAlignment, elementAlignment) : inlineAlignment;
        LayoutMath.ValidateAlignment(InlineAlignment);
        if (InlineAlignment < elementAlignment) throw new ArgumentOutOfRangeException(nameof(inlineAlignment));
        InlineCapacity = inlineCapacity;
        InlineOffset = LayoutMath.AlignUp(HeaderSize, InlineAlignment);
        StorageAlignment = Math.Max(HeaderAlignment, InlineAlignment);
        StorageSize = LayoutMath.AlignUp(checked(InlineOffset + elementSize * inlineCapacity), StorageAlignment);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct BufferHeader
{
    public nint Data;
    public ulong Count;
    public ulong Capacity;
}
