using System.Text;
namespace SkrGui;

// StringView is a byte view in the source. Never substitute UTF-16 positions.
public readonly struct Utf8StringView : IEquatable<Utf8StringView>
{
    private readonly ReadOnlyMemory<byte> _bytes;
    public Utf8StringView(ReadOnlyMemory<byte> bytes) => _bytes = bytes;
    public ReadOnlyMemory<byte> Memory => _bytes;
    public ReadOnlySpan<byte> Bytes => _bytes.Span;
    public ulong Size() => (ulong)_bytes.Length;
    public bool IsEmpty() => _bytes.IsEmpty;
    public Utf8StringView Slice(int start, int length) => new(_bytes.Slice(start, length));
    public override string ToString() => Encoding.UTF8.GetString(_bytes.Span);
    public bool Equals(Utf8StringView other) => Bytes.SequenceEqual(other.Bytes);
    public override bool Equals(object? obj) => obj is Utf8StringView other && Equals(other);
    public override int GetHashCode() { var hash = new HashCode(); foreach (byte value in Bytes) hash.Add(value); return hash.ToHashCode(); }
    public static bool operator ==(Utf8StringView a, Utf8StringView b) => a.Equals(b);
    public static bool operator !=(Utf8StringView a, Utf8StringView b) => !a.Equals(b);
    public static implicit operator Utf8StringView(string value) => new(Encoding.UTF8.GetBytes(value));
    public static explicit operator string(Utf8StringView value) => value.ToString();
}
