namespace SkrGui;
// Source: SkrGuiCore/math/layout/alignment.hpp (611561f8).
public partial struct AlignmentDirectional : IEquatable<AlignmentDirectional>
{
    public float Start,Y;
    public AlignmentDirectional(float start,float y) { Start=start; Y=y; }
    public static AlignmentDirectional TopStart()
    {
        return new AlignmentDirectional(-1.0f, -1.0f);
    }
    public static AlignmentDirectional TopCenter()
    {
        return new AlignmentDirectional(0.0f, -1.0f);
    }
    public static AlignmentDirectional TopEnd()
    {
        return new AlignmentDirectional(1.0f, -1.0f);
    }
    public static AlignmentDirectional CenterStart()
    {
        return new AlignmentDirectional(-1.0f, 0.0f);
    }
    public static AlignmentDirectional Center()
    {
        return new AlignmentDirectional(0.0f, 0.0f);
    }
    public static AlignmentDirectional CenterEnd()
    {
        return new AlignmentDirectional(1.0f, 0.0f);
    }
    public static AlignmentDirectional BottomStart()
    {
        return new AlignmentDirectional(-1.0f, 1.0f);
    }
    public static AlignmentDirectional BottomCenter()
    {
        return new AlignmentDirectional(0.0f, 1.0f);
    }
    public static AlignmentDirectional BottomEnd()
    {
        return new AlignmentDirectional(1.0f, 1.0f);
    }
    public float Horizontal()
    {
        return Start;
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
        return new Alignment(-Start, Y);
        case ETextDirection.LTR:
        return new Alignment(Start, Y);
        }
        return new Alignment(Start, Y);
    }
    public static bool operator==(AlignmentDirectional lhs,AlignmentDirectional rhs)
    {
        return lhs.Start == rhs.Start && lhs.Y == rhs.Y;
    }
    public static bool operator!=(AlignmentDirectional lhs,AlignmentDirectional rhs)
    {
        return !(lhs == rhs);
    }
    public static AlignmentDirectional operator+(AlignmentDirectional lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs.Start + rhs.Start, lhs.Y + rhs.Y);
    }
    public static AlignmentDirectional operator-(AlignmentDirectional lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs.Start - rhs.Start, lhs.Y - rhs.Y);
    }
    public static AlignmentDirectional operator*(AlignmentDirectional lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs.Start * rhs.Start, lhs.Y * rhs.Y);
    }
    public static AlignmentDirectional operator/(AlignmentDirectional lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs.Start / rhs.Start, lhs.Y / rhs.Y);
    }
    public static AlignmentDirectional operator%(AlignmentDirectional lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(CppMath.Fmod(lhs.Start, rhs.Start), CppMath.Fmod(lhs.Y, rhs.Y));
    }
    public static AlignmentDirectional operator-(AlignmentDirectional lhs)
    {
        return new AlignmentDirectional(-lhs.Start, -lhs.Y);
    }
    public AlignmentMixed Add(AlignmentMixed rhs)
    {
        return new AlignmentMixed(this) + rhs;
    }
    public static AlignmentDirectional Lerp(AlignmentDirectional a,AlignmentDirectional b,float t)
    {
        return new AlignmentDirectional(a.Start + (b.Start - a.Start) * t, a.Y + (b.Y - a.Y) * t);
    }
    public AlignmentDirectional CopyWith(float? Start=null,float? Y=null)
    {
        return new AlignmentDirectional(Start.HasValue ? Start.Value : this.Start,
        Y.HasValue ? Y.Value : this.Y);
    }
    public static AlignmentDirectional operator+(AlignmentDirectional lhs,float rhs)
    {
        return new AlignmentDirectional(lhs.Start + rhs, lhs.Y + rhs);
    }
    public static AlignmentDirectional operator-(AlignmentDirectional lhs,float rhs)
    {
        return new AlignmentDirectional(lhs.Start - rhs, lhs.Y - rhs);
    }
    public static AlignmentDirectional operator*(AlignmentDirectional lhs,float rhs)
    {
        return new AlignmentDirectional(lhs.Start * rhs, lhs.Y * rhs);
    }
    public static AlignmentDirectional operator/(AlignmentDirectional lhs,float rhs)
    {
        return new AlignmentDirectional(lhs.Start / rhs, lhs.Y / rhs);
    }
    public static AlignmentDirectional operator%(AlignmentDirectional lhs,float rhs)
    {
        return new AlignmentDirectional(CppMath.Fmod(lhs.Start, rhs), CppMath.Fmod(lhs.Y, rhs));
    }
    public static AlignmentDirectional operator+(float lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs + rhs.Start, lhs + rhs.Y);
    }
    public static AlignmentDirectional operator-(float lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs - rhs.Start, lhs - rhs.Y);
    }
    public static AlignmentDirectional operator*(float lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs * rhs.Start, lhs * rhs.Y);
    }
    public static AlignmentDirectional operator/(float lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(lhs / rhs.Start, lhs / rhs.Y);
    }
    public static AlignmentDirectional operator%(float lhs,AlignmentDirectional rhs)
    {
        return new AlignmentDirectional(CppMath.Fmod(lhs, rhs.Start), CppMath.Fmod(lhs, rhs.Y));
    }
    public static AlignmentMixed operator+(AlignmentDirectional lhs,Alignment rhs)
    {
        return AlignmentMixed.FromStartY(lhs.Start, lhs.Y) + rhs;
    }
    public static AlignmentMixed operator-(AlignmentDirectional lhs,Alignment rhs)
    {
        return AlignmentMixed.FromStartY(lhs.Start, lhs.Y) - rhs;
    }
    public static AlignmentMixed operator+(AlignmentDirectional lhs,AlignmentMixed rhs)
    {
        return rhs + lhs;
    }
    public static AlignmentMixed operator-(AlignmentDirectional lhs,AlignmentMixed rhs)
    {
        return AlignmentMixed.FromStartY(lhs.Start, lhs.Y) - rhs;
    }
    public bool Equals(AlignmentDirectional rhs)=>this==rhs;
    public override bool Equals(object? value)=>value is AlignmentDirectional rhs && this==rhs;
    public override int GetHashCode()=>HashCode.Combine(Start,Y);
}
