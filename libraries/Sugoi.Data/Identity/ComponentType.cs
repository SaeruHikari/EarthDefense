namespace Sugoi.Data;

[Flags]
public enum ComponentKind : uint
{
    None = 0,
    Pinned = 1u << 28,
    Buffer = 1u << 29,
    Chunk = 1u << 30,
    Tag = 1u << 31,
}

public readonly struct ComponentType : IEquatable<ComponentType>, IComparable<ComponentType>
{
    public ComponentType(uint value) => Value = value;
    public uint Value { get; }
    public uint Index => Value & 0x0fff_ffffu;
    public ComponentKind Kind => (ComponentKind)(Value & 0xf000_0000u);
    public bool IsTag => (Kind & ComponentKind.Tag) != 0;
    public bool IsBuffer => (Kind & ComponentKind.Buffer) != 0;
    public bool IsChunk => (Kind & ComponentKind.Chunk) != 0;
    public bool IsPinned => (Kind & ComponentKind.Pinned) != 0;
    public bool Equals(ComponentType other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is ComponentType other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(ComponentType other) => Value.CompareTo(other.Value);
    public static bool operator ==(ComponentType a, ComponentType b) => a.Equals(b);
    public static bool operator !=(ComponentType a, ComponentType b) => !a.Equals(b);
    public override string ToString() => $"Component({Index}, {Kind})";
}
