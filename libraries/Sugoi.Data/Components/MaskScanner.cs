using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Sugoi.Data;

public enum MaskScanMode { Auto, Scalar, Sse2, Avx2, Avx512, AdvSimd }
public readonly record struct MaskRange(int Start, int Count);

/// <summary>Scans enabled masks into contiguous matching views, retaining sugoi's no-hit/full-hit fast paths.</summary>
public static class MaskScanner
{
    public static MaskScanMode ActiveMode => Avx512F.IsSupported ? MaskScanMode.Avx512 :
        Avx2.IsSupported ? MaskScanMode.Avx2 : Avx.IsSupported ? MaskScanMode.Scalar :
        AdvSimd.IsSupported ? MaskScanMode.AdvSimd : Sse2.IsSupported ? MaskScanMode.Sse2 : MaskScanMode.Scalar;

    public static RangeEnumerator Scan(ReadOnlySpan<uint> masks, uint allMask, uint noneMask, MaskScanMode mode = MaskScanMode.Auto) => new(masks, allMask, noneMask, mode);

    public static void ForEach(ReadOnlySpan<uint> masks, uint allMask, uint noneMask, Action<int, int> action, MaskScanMode mode = MaskScanMode.Auto)
    {
        ArgumentNullException.ThrowIfNull(action);
        foreach (var range in Scan(masks, allMask, noneMask, mode)) action(range.Start, range.Count);
    }

    public ref struct RangeEnumerator
    {
        private readonly ReadOnlySpan<uint> _masks;
        private readonly uint _all, _none;
        private readonly MaskScanMode _mode;
        private readonly int _width;
        private int _cursor;
        public MaskRange Current { get; private set; }
        internal RangeEnumerator(ReadOnlySpan<uint> masks, uint all, uint none, MaskScanMode mode)
        {
            _masks = masks; _all = all; _none = none; _cursor = 0; Current = default;
            _mode = mode == MaskScanMode.Auto ? ActiveMode : mode;
            _width = _mode switch
            {
                MaskScanMode.Scalar => 1,
                MaskScanMode.Sse2 when Sse2.IsSupported => 4,
                MaskScanMode.Avx2 when Avx2.IsSupported => 8,
                MaskScanMode.Avx512 when Avx512F.IsSupported => 16,
                MaskScanMode.AdvSimd when AdvSimd.IsSupported => 4,
                _ => throw new PlatformNotSupportedException($"Requested mask kernel {_mode} is unavailable.")
            };
        }
        public RangeEnumerator GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Match(int index) => (_masks[index] & _all) == _all && (_masks[index] & _none) == 0;

        public bool MoveNext()
        {
            int length = _masks.Length;
            while (_cursor < length)
            {
                if (_width > 1 && _cursor <= length - _width)
                {
                    uint hits = Block(_cursor);
                    if (hits == 0) { _cursor += _width; continue; }
                    _cursor += BitOperations.TrailingZeroCount(hits);
                    break;
                }
                if (Match(_cursor)) break;
                _cursor++;
            }
            if (_cursor == length) return false;
            int start = _cursor;
            uint complete = (1u << _width) - 1u;
            while (_cursor < length)
            {
                if (_width > 1 && _cursor <= length - _width)
                {
                    uint hits = Block(_cursor);
                    if (hits == complete) { _cursor += _width; continue; }
                    _cursor += BitOperations.TrailingZeroCount(~hits);
                    break;
                }
                if (!Match(_cursor)) break;
                _cursor++;
            }
            Current = new(start, _cursor - start);
            return true;
        }

        private uint Block(int start)
        {
            ref uint source = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(_masks);
            if (_mode == MaskScanMode.Avx512)
            {
                var value = Vector512.LoadUnsafe(ref source, (nuint)start);
                var all = Vector512.Create(_all);
                var included = Vector512.Equals(Vector512.BitwiseAnd(value, all), all);
                var excluded = Vector512.Equals(Vector512.BitwiseAnd(value, Vector512.Create(_none)), Vector512<uint>.Zero);
                return (uint)Vector512.ExtractMostSignificantBits(Vector512.BitwiseAnd(included, excluded));
            }
            if (_mode == MaskScanMode.Avx2)
            {
                var value = Vector256.LoadUnsafe(ref source, (nuint)start);
                var all = Vector256.Create(_all);
                var included = Avx2.CompareEqual(Avx2.And(value, all), all);
                var excluded = Avx2.CompareEqual(Avx2.And(value, Vector256.Create(_none)), Vector256<uint>.Zero);
                return Vector256.ExtractMostSignificantBits(Avx2.And(included, excluded));
            }
            var value128 = Vector128.LoadUnsafe(ref source, (nuint)start);
            var all128 = Vector128.Create(_all);
            if (_mode == MaskScanMode.Sse2)
            {
                var included = Sse2.CompareEqual(Sse2.And(value128, all128), all128);
                var excluded = Sse2.CompareEqual(Sse2.And(value128, Vector128.Create(_none)), Vector128<uint>.Zero);
                return Vector128.ExtractMostSignificantBits(Sse2.And(included, excluded));
            }
            var armIncluded = AdvSimd.CompareEqual(AdvSimd.And(value128, all128), all128);
            var armExcluded = AdvSimd.CompareEqual(AdvSimd.And(value128, Vector128.Create(_none)), Vector128<uint>.Zero);
            return Vector128.ExtractMostSignificantBits(AdvSimd.And(armIncluded, armExcluded));
        }
    }
}

/// <summary>Atomic bit merges are mask operations, not component locks.</summary>
public static class ComponentMask
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Enable(ref uint mask, uint bits) => (uint)Interlocked.Or(ref Unsafe.As<uint, int>(ref mask), (int)bits);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Disable(ref uint mask, uint bits) => (uint)Interlocked.And(ref Unsafe.As<uint, int>(ref mask), (int)~bits);
}
