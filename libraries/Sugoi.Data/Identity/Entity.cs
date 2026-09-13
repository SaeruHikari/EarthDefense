using System.Runtime.InteropServices;

namespace Sugoi.Data;

/// <summary>A world-local identity. Copying an identity does not copy or own an entity.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Entity : IEquatable<Entity>, IComparable<Entity>
{
    private readonly ulong _value;
    private Entity(ulong value) => _value = value;

    public static Entity Null => default;
    public ulong Value => _value;
    public uint Index => (uint)_value;
    public uint Generation => (uint)(_value >> 32);
    public bool IsNull => _value == 0;
    /// <summary>A staging-epoch identity. It is never accepted by a World registry.</summary>
    public bool IsTransient => Generation == uint.MaxValue && Index != uint.MaxValue;

    internal static Entity CreateTransient(uint index) => index != uint.MaxValue
        ? new Entity(((ulong)uint.MaxValue << 32) | index)
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal static Entity Create(uint index, uint generation)
    {
        if (index == uint.MaxValue || generation is 0 or uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index), "Reserved entity identity.");
        return new Entity(((ulong)generation << 32) | index);
    }

    public static bool TryFromRawValue(ulong value, out Entity entity)
    {
        uint index = (uint)value, generation = (uint)(value >> 32);
        if (value != 0 && (index == uint.MaxValue || generation == 0))
        {
            entity = default;
            return false;
        }
        entity = new Entity(value);
        return true;
    }

    public static Entity FromRawValue(ulong value) => TryFromRawValue(value, out var entity)
        ? entity : throw new ArgumentOutOfRangeException(nameof(value), "Reserved entity identity.");

    internal static uint NextGeneration(uint generation) => generation >= uint.MaxValue - 1 ? 1 : generation + 1;
    public bool Equals(Entity other) => _value == other._value;
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
    public int CompareTo(Entity other) => _value.CompareTo(other._value);
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
    public override string ToString() => IsNull ? "Entity.Null" : $"Entity({Index}:{Generation})";
}

public readonly record struct EntityMapping(Entity Source, Entity Destination);
