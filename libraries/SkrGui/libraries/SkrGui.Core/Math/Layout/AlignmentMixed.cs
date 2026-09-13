namespace SkrGui;
// Source: SkrGuiCore/math/layout/alignment.hpp (611561f8).
public partial struct AlignmentMixed : IEquatable<AlignmentMixed>
{
    public float X,Start,Y;
    public AlignmentMixed(float x,float start,float y) { X=x; Start=start; Y=y; }
    public AlignmentMixed(Alignment value):this(value.X,0,value.Y) {}
    public AlignmentMixed(AlignmentDirectional value):this(0,value.Start,value.Y) {}
    public static implicit operator AlignmentMixed(Alignment value)=>new(value);
    public static implicit operator AlignmentMixed(AlignmentDirectional value)=>new(value);
    public static AlignmentMixed Zero()
    {
        return new AlignmentMixed();
    }
    public static AlignmentMixed FromXStartY(float X,float Start,float Y)
    {
        return new AlignmentMixed(X, Start, Y);
    }
    public static AlignmentMixed FromXY(float X,float Y)
    {
        return new AlignmentMixed(X, 0.0f, Y);
    }
    public static AlignmentMixed FromStartY(float Start,float Y)
    {
        return new AlignmentMixed(0.0f, Start, Y);
    }
    public float Horizontal()
    {
        return X + Start;
    }
    public float Vertical()
    {
        return Y;
    }
    public Alignment Resolve(ETextDirection direction)
    {
        switch (direction)
        {
        case ETextDirection.RTL:
        return new Alignment(X - Start, Y);
        case ETextDirection.LTR:
        return new Alignment(X + Start, Y);
        }
        return new Alignment(X + Start, Y);
    }
    public static bool operator==(AlignmentMixed lhs,AlignmentMixed rhs)
    {
        return lhs.X == rhs.X && lhs.Start == rhs.Start && lhs.Y == rhs.Y;
    }
    public static bool operator!=(AlignmentMixed lhs,AlignmentMixed rhs)
    {
        return !(lhs == rhs);
    }
    public static AlignmentMixed operator+(AlignmentMixed lhs,AlignmentMixed rhs)
    {
        return new AlignmentMixed(lhs.X + rhs.X, lhs.Start + rhs.Start, lhs.Y + rhs.Y);
    }
    public static AlignmentMixed operator-(AlignmentMixed lhs,AlignmentMixed rhs)
    {
        return new AlignmentMixed(lhs.X - rhs.X, lhs.Start - rhs.Start, lhs.Y - rhs.Y);
    }
    public static AlignmentMixed operator+(AlignmentMixed lhs,Alignment rhs)
    {
        return new AlignmentMixed(lhs.X + rhs.X, lhs.Start, lhs.Y + rhs.Y);
    }
    public static AlignmentMixed operator-(AlignmentMixed lhs,Alignment rhs)
    {
        return new AlignmentMixed(lhs.X - rhs.X, lhs.Start, lhs.Y - rhs.Y);
    }
    public static AlignmentMixed operator+(AlignmentMixed lhs,AlignmentDirectional rhs)
    {
        return new AlignmentMixed(lhs.X, lhs.Start + rhs.Start, lhs.Y + rhs.Y);
    }
    public static AlignmentMixed operator-(AlignmentMixed lhs,AlignmentDirectional rhs)
    {
        return new AlignmentMixed(lhs.X, lhs.Start - rhs.Start, lhs.Y - rhs.Y);
    }
    public static AlignmentMixed operator-(AlignmentMixed lhs)
    {
        return new AlignmentMixed(-lhs.X, -lhs.Start, -lhs.Y);
    }
    public AlignmentMixed Add(AlignmentMixed rhs)
    {
        return this + rhs;
    }
    public static AlignmentMixed Lerp(AlignmentMixed a,AlignmentMixed b,float t)
    {
        return new AlignmentMixed(a.X + (b.X - a.X) * t,
        a.Start + (b.Start - a.Start) * t,
        a.Y + (b.Y - a.Y) * t);
    }
    public AlignmentMixed CopyWith(float? X=null,float? Start=null,float? Y=null)
    {
        return new AlignmentMixed(X.HasValue ? X.Value : this.X,
        Start.HasValue ? Start.Value : this.Start,
        Y.HasValue ? Y.Value : this.Y);
    }
    public static AlignmentMixed operator+(AlignmentMixed lhs,float rhs)
    {
        return new AlignmentMixed(lhs.X + rhs, lhs.Start + rhs, lhs.Y + rhs);
    }
    public static AlignmentMixed operator-(AlignmentMixed lhs,float rhs)
    {
        return new AlignmentMixed(lhs.X - rhs, lhs.Start - rhs, lhs.Y - rhs);
    }
    public static AlignmentMixed operator*(AlignmentMixed lhs,float rhs)
    {
        return new AlignmentMixed(lhs.X * rhs, lhs.Start * rhs, lhs.Y * rhs);
    }
    public static AlignmentMixed operator/(AlignmentMixed lhs,float rhs)
    {
        return new AlignmentMixed(lhs.X / rhs, lhs.Start / rhs, lhs.Y / rhs);
    }
    public static AlignmentMixed operator%(AlignmentMixed lhs,float rhs)
    {
        return new AlignmentMixed(CppMath.Fmod(lhs.X, rhs), CppMath.Fmod(lhs.Start, rhs), CppMath.Fmod(lhs.Y, rhs));
    }
    public static AlignmentMixed operator+(float lhs,AlignmentMixed rhs)
    {
        return new AlignmentMixed(lhs + rhs.X, lhs + rhs.Start, lhs + rhs.Y);
    }
    public static AlignmentMixed operator-(float lhs,AlignmentMixed rhs)
    {
        return new AlignmentMixed(lhs - rhs.X, lhs - rhs.Start, lhs - rhs.Y);
    }
    public static AlignmentMixed operator*(float lhs,AlignmentMixed rhs)
    {
        return new AlignmentMixed(lhs * rhs.X, lhs * rhs.Start, lhs * rhs.Y);
    }
    public static AlignmentMixed operator/(float lhs,AlignmentMixed rhs)
    {
        return new AlignmentMixed(lhs / rhs.X, lhs / rhs.Start, lhs / rhs.Y);
    }
    public static AlignmentMixed operator%(float lhs,AlignmentMixed rhs)
    {
        return new AlignmentMixed(CppMath.Fmod(lhs, rhs.X), CppMath.Fmod(lhs, rhs.Start), CppMath.Fmod(lhs, rhs.Y));
    }
    public bool Equals(AlignmentMixed rhs)=>this==rhs;
    public override bool Equals(object? value)=>value is AlignmentMixed rhs && this==rhs;
    public override int GetHashCode()=>HashCode.Combine(X,Start,Y);
}
