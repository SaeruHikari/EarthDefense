using System.Diagnostics.CodeAnalysis;
namespace SkrGui;

// Source: SkrGuiCore/framework/key.hpp and nexus/nexus_slot.hpp (611561f8).
public enum EKeyType : byte { None,Int,UInt,Real,Guid,String }
public struct Key : IEquatable<Key>
{
    private EKeyType _type;
    private long _int;private ulong _uint;private double _real;private Guid _guid;private OwnedUtf8String _string;
    public Key(long value):this(){_type=EKeyType.Int;_int=value;}
    public Key(ulong value):this(){_type=EKeyType.UInt;_uint=value;}
    public Key(double value):this(){_type=EKeyType.Real;_real=value;}
    public Key(Guid value):this(){_type=EKeyType.Guid;_guid=value;}
    public Key(Utf8StringView value):this(){_type=EKeyType.String;_string=new OwnedUtf8String(value);}
    public static Key None()=>new();
    public static Key FromInt(long v)=>new(v);
    public static Key FromUInt(ulong v)=>new(v);
    public static Key FromReal(double v)=>new(v);
    public static Key FromGuid(Guid v)=>new(v);
    public static Key FromString(Utf8StringView v)=>new(v);
    public EKeyType Type()=>_type;
    public bool IsNone()=>_type==EKeyType.None;
    public bool IsInt()=>_type==EKeyType.Int;
    public bool IsUInt()=>_type==EKeyType.UInt;
    public bool IsReal()=>_type==EKeyType.Real;
    public bool IsGuid()=>_type==EKeyType.Guid;
    public bool IsString()=>_type==EKeyType.String;
    public long GetInt(){GuiAssert.Require(IsInt(),"Key is not Int");return _int;}
    public ulong GetUInt(){GuiAssert.Require(IsUInt(),"Key is not UInt");return _uint;}
    public double GetReal(){GuiAssert.Require(IsReal(),"Key is not Real");return _real;}
    public Guid GetGuid(){GuiAssert.Require(IsGuid(),"Key is not Guid");return _guid;}
    [UnscopedRef] public ref readonly OwnedUtf8String GetString(){GuiAssert.Require(IsString(),"Key is not String");return ref _string;}
    // C# names distinguish the source const and mutable reference overloads.
    [UnscopedRef] public ref OwnedUtf8String GetStringMutable(){GuiAssert.Require(IsString(),"Key is not String");return ref _string;}
    public void SetNone()=>this=new();
    public void SetInt(long v)=>this=new(v);
    public void SetUInt(ulong v)=>this=new(v);
    public void SetReal(double v)=>this=new(v);
    public void SetGuid(Guid v)=>this=new(v);
    public void SetString(Utf8StringView v)=>this=new(v);
    public static bool operator==(Key a,Key b)=>a._type==b._type&&(a._type switch { EKeyType.None=>true,EKeyType.Int=>a._int==b._int,EKeyType.UInt=>a._uint==b._uint,EKeyType.Real=>a._real==b._real,EKeyType.Guid=>a._guid==b._guid,EKeyType.String=>a._string==b._string,_=>throw new InvalidOperationException("Unreachable Key type") });
    public static bool operator!=(Key a,Key b)=>!(a==b);
    public bool Equals(Key value)=>this==value;
    public override bool Equals(object? value)=>value is Key k&&this==k;
    public override int GetHashCode()=>HashCode.Combine(_type,_type switch{EKeyType.None=>0,EKeyType.Int=>_int.GetHashCode(),EKeyType.UInt=>_uint.GetHashCode(),EKeyType.Real=>_real.GetHashCode(),EKeyType.Guid=>_guid.GetHashCode(),EKeyType.String=>_string.GetHashCode(),_=>0});
}
public readonly struct NexusSlot : IEquatable<NexusSlot>,IComparable<NexusSlot>
{
    // Encoded zero is invalid, including default(T) and array-zero initialization.
    private readonly ulong _encodedIndex;
    private ulong RawIndex=>unchecked(_encodedIndex-1);
    public NexusSlot(){_encodedIndex=0;}
    public NexusSlot(ulong index){_encodedIndex=unchecked(index+1);}
    public static NexusSlot Invalid()=>new();
    public ulong Index(){GuiAssert.Require(IsValid(),"NexusSlot.Index requires valid slot");return RawIndex;}
    public bool IsValid()=>_encodedIndex!=0;
    public static bool operator==(NexusSlot a,NexusSlot b)=>a._encodedIndex==b._encodedIndex;
    public static bool operator!=(NexusSlot a,NexusSlot b)=>a._encodedIndex!=b._encodedIndex;
    public static explicit operator bool(NexusSlot slot)=>slot.IsValid();
    public int CompareTo(NexusSlot b)=>RawIndex.CompareTo(b.RawIndex);
    public bool Equals(NexusSlot b)=>this==b;
    public override bool Equals(object? obj)=>obj is NexusSlot b&&this==b;
    public override int GetHashCode()=>RawIndex.GetHashCode();
}
