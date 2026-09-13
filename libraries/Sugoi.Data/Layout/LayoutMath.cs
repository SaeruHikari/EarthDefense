using System.Runtime.CompilerServices;

namespace Sugoi.Data;

/// <summary>The sole checked alignment arithmetic used by native layout builders.</summary>
public static class LayoutMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignUp(int value, int alignment)
    {
        ValidateAlignment(alignment);
        return checked(value + alignment - 1) & -alignment;
    }

    public static int AlignDown(int value, int alignment)
    {
        ValidateAlignment(alignment);
        return value & -alignment;
    }

    public static void ValidateAlignment(int alignment)
    {
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be a positive power of two.");
    }

    public static int NaturalAlignment<T>() where T : unmanaged
    {
        // CLR sequential layout rather than Marshal.SizeOf (notably bool/char).
        var probe = new AlignmentProbe<T> { Prefix = 0, Value = default };
        return checked((int)Unsafe.ByteOffset(ref probe.Prefix, ref Unsafe.As<T, byte>(ref probe.Value)));
    }

    private struct AlignmentProbe<T> where T : unmanaged
    {
        public byte Prefix;
        public T Value;
    }
}

public readonly record struct BlockLayout(int TotalBytes, int PayloadOffset, int PayloadBytes, int Alignment)
{
    public static BlockLayout Create(int totalBytes, int headerBytes, int alignment)
    {
        var offset = LayoutMath.AlignUp(headerBytes, alignment);
        if (totalBytes < offset) throw new ArgumentOutOfRangeException(nameof(totalBytes));
        return new(totalBytes, offset, totalBytes - offset, alignment);
    }

    public int Capacity(int stride) => stride > 0 ? PayloadBytes / stride : throw new ArgumentOutOfRangeException(nameof(stride));
}
