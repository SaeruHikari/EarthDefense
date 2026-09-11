using System;
using System.Numerics;
namespace Earthward.Combat;

/// <summary>Managed PCG32 and IEEE float sampling, matching Godot 4.6 RandomNumberGenerator.</summary>
/// <remarks>Algorithm reference: Godot core/math/random_pcg.h (MIT), thirdparty/misc/pcg.cpp (M. E. O'Neill, Apache-2.0).</remarks>
public sealed class CombatRandom
{
    private const ulong Increment = (1442695040888963407UL << 1) | 1;
    private ulong _seed;
    public ulong Seed
    {
        get => _seed; set
        {
            _seed = value;
            State = 0;
            NextUInt();
            State = unchecked(State + value);
            NextUInt();
        }
    }
    public ulong State
    {
        get; set;
    }
    public CombatRandom(ulong seed = 122)
    {
        Seed = seed;
    }
    public uint NextUInt()
    {
        var old = State;
        State = unchecked(old * 6364136223846793005UL + Increment);
        var word = (uint)(((old >> 18) ^ old) >> 27);
        var rotate = (int)(old >> 59);
        return BitOperations.RotateRight(word, rotate);
    }
    public float Randf()
    {
        var exponent = NextUInt();
        if (exponent == 0)
            return 0;
        var significand = NextUInt() | 0x80000001U;
        return MathF.ScaleB((float)significand, -32 - BitOperations.LeadingZeroCount(exponent));
    }
    public double Range(double from, double to)
    {
        float a = (float)from, b = (float)to;
        return Randf() * (b - a) + a;
    }
    public int Range(int from, int to)
    {
        if (from == to)
            return from;
        long min = Math.Min(from, to);
        uint range = unchecked((uint)((long)Math.Max(from, to) - min));
        if (range == uint.MaxValue)
            return (int)((long)NextUInt() + min);
        uint bound = range + 1, threshold = unchecked(0U - bound) % bound, r;
        do
        {
            r = NextUInt();
        } while (r < threshold);
        return (int)(min + r % bound);
    }
}
