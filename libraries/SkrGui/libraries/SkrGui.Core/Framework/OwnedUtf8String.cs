namespace SkrGui;

// Small value adapter for the source owning String. Conversion from a borrowed
// byte view always owns a copy; copying this immutable value may share storage.
public readonly struct OwnedUtf8String : IEquatable<OwnedUtf8String>
{
    private readonly Utf8StringView _value;
    public OwnedUtf8String(Utf8StringView value)=>_value=new Utf8StringView(value.Bytes.ToArray());
    public Utf8StringView View()=>_value;
    public ReadOnlySpan<byte> Bytes=>_value.Bytes;
    public ulong Size()=>_value.Size();
    public bool IsEmpty()=>_value.IsEmpty();
    public override string ToString()=>_value.ToString();
    public bool Equals(OwnedUtf8String other)=>_value==other._value;
    public override bool Equals(object? value)=>value is OwnedUtf8String other&&Equals(other);
    public override int GetHashCode()=>_value.GetHashCode();
    public static bool operator==(OwnedUtf8String a,OwnedUtf8String b)=>a.Equals(b);
    public static bool operator!=(OwnedUtf8String a,OwnedUtf8String b)=>!a.Equals(b);
    public static implicit operator OwnedUtf8String(Utf8StringView value)=>new(value);
    public static implicit operator OwnedUtf8String(string value)=>new((Utf8StringView)value);
    public static implicit operator Utf8StringView(OwnedUtf8String value)=>value._value;
}
