namespace SkrGui;
// Source: SkrGuiCore/math/layout/edge_insets.hpp (611561f8).
public partial struct EdgeInsetsMixed : IEquatable<EdgeInsetsMixed>
{
    public float Left,Right,Start,End,Top,Bottom;
    public EdgeInsetsMixed(float left,float right,float start,float end,float top,float bottom) { Left=left; Right=right; Start=start; End=end; Top=top; Bottom=bottom; }
    public EdgeInsetsMixed(EdgeInsets value):this(value.Left,value.Right,0,0,value.Top,value.Bottom) {}
    public EdgeInsetsMixed(EdgeInsetsDirectional value):this(0,0,value.Start,value.End,value.Top,value.Bottom) {}
    public static implicit operator EdgeInsetsMixed(EdgeInsets value)=>new(value);
    public static implicit operator EdgeInsetsMixed(EdgeInsetsDirectional value)=>new(value);
    public static EdgeInsetsMixed Zero()
    {
        return new EdgeInsetsMixed();
    }
    public static EdgeInsetsMixed Infinity()
    {
        return new EdgeInsetsMixed(float.PositiveInfinity,
        float.PositiveInfinity,
        float.PositiveInfinity,
        float.PositiveInfinity,
        float.PositiveInfinity,
        float.PositiveInfinity);
    }
    public static EdgeInsetsMixed FromLRSETB(float Left,float Right,float Start,float End,float Top,float Bottom)
    {
        return new EdgeInsetsMixed(Left, Right, Start, End, Top, Bottom);
    }
    public static EdgeInsetsMixed FromLTRB(float Left,float Top,float Right,float Bottom)
    {
        return new EdgeInsetsMixed(Left, Right, 0.0f, 0.0f, Top, Bottom);
    }
    public static EdgeInsetsMixed FromSTEB(float Start,float Top,float End,float Bottom)
    {
        return new EdgeInsetsMixed(0.0f, 0.0f, Start, End, Top, Bottom);
    }
    public float Horizontal()
    {
        return Left + Right + Start + End;
    }
    public float Vertical()
    {
        return Top + Bottom;
    }
    public float Along(EAxis axis)
    {
        switch (axis)
        {
        case EAxis.Horizontal:
        return Horizontal();
        case EAxis.Vertical:
        return Vertical();
        }
        return 0.0f;
    }
    public Sizef CollapsedSize()
    {
        return new Sizef(Horizontal(), Vertical());
    }
    public bool IsZero()
    {
        return Left == 0.0f && Right == 0.0f && Start == 0.0f && End == 0.0f && Top == 0.0f && Bottom == 0.0f;
    }
    public bool IsNonNegative()
    {
        return Left >= 0.0f && Right >= 0.0f && Start >= 0.0f && End >= 0.0f && Top >= 0.0f && Bottom >= 0.0f;
    }
    public EdgeInsetsMixed Flipped()
    {
        return new EdgeInsetsMixed(Right, Left, End, Start, Bottom, Top);
    }
    public Sizef InflateSize(Sizef size)
    {
        return new Sizef(size.Width + Horizontal(), size.Height + Vertical());
    }
    public Sizef DeflateSize(Sizef size)
    {
        return new Sizef(size.Width - Horizontal(),
        size.Height - Vertical());
    }
    public EdgeInsets Resolve(ETextDirection direction)
    {
        switch (direction)
        {
        case ETextDirection.RTL:
        return new EdgeInsets(Left + End, Top, Right + Start, Bottom);
        case ETextDirection.LTR:
        return new EdgeInsets(Left + Start, Top, Right + End, Bottom);
        }
        return new EdgeInsets(Left + Start, Top, Right + End, Bottom);
    }
    public static bool operator==(EdgeInsetsMixed lhs,EdgeInsetsMixed rhs)
    {
        return lhs.Left == rhs.Left && lhs.Right == rhs.Right && lhs.Start == rhs.Start && lhs.End == rhs.End && lhs.Top == rhs.Top && lhs.Bottom == rhs.Bottom;
    }
    public static bool operator!=(EdgeInsetsMixed lhs,EdgeInsetsMixed rhs)
    {
        return !(lhs == rhs);
    }
    public static EdgeInsetsMixed operator+(EdgeInsetsMixed lhs,EdgeInsetsMixed rhs)
    {
        return new EdgeInsetsMixed(lhs.Left + rhs.Left, lhs.Right + rhs.Right, lhs.Start + rhs.Start, lhs.End + rhs.End, lhs.Top + rhs.Top, lhs.Bottom + rhs.Bottom);
    }
    public static EdgeInsetsMixed operator-(EdgeInsetsMixed lhs,EdgeInsetsMixed rhs)
    {
        return new EdgeInsetsMixed(lhs.Left - rhs.Left, lhs.Right - rhs.Right, lhs.Start - rhs.Start, lhs.End - rhs.End, lhs.Top - rhs.Top, lhs.Bottom - rhs.Bottom);
    }
    public static EdgeInsetsMixed operator+(EdgeInsetsMixed lhs,EdgeInsets rhs)
    {
        return new EdgeInsetsMixed(lhs.Left + rhs.Left, lhs.Right + rhs.Right, lhs.Start, lhs.End, lhs.Top + rhs.Top, lhs.Bottom + rhs.Bottom);
    }
    public static EdgeInsetsMixed operator-(EdgeInsetsMixed lhs,EdgeInsets rhs)
    {
        return new EdgeInsetsMixed(lhs.Left - rhs.Left, lhs.Right - rhs.Right, lhs.Start, lhs.End, lhs.Top - rhs.Top, lhs.Bottom - rhs.Bottom);
    }
    public static EdgeInsetsMixed operator+(EdgeInsetsMixed lhs,EdgeInsetsDirectional rhs)
    {
        return new EdgeInsetsMixed(lhs.Left, lhs.Right, lhs.Start + rhs.Start, lhs.End + rhs.End, lhs.Top + rhs.Top, lhs.Bottom + rhs.Bottom);
    }
    public static EdgeInsetsMixed operator-(EdgeInsetsMixed lhs,EdgeInsetsDirectional rhs)
    {
        return new EdgeInsetsMixed(lhs.Left, lhs.Right, lhs.Start - rhs.Start, lhs.End - rhs.End, lhs.Top - rhs.Top, lhs.Bottom - rhs.Bottom);
    }
    public static EdgeInsetsMixed operator-(EdgeInsetsMixed lhs)
    {
        return new EdgeInsetsMixed(-lhs.Left, -lhs.Right, -lhs.Start, -lhs.End, -lhs.Top, -lhs.Bottom);
    }
    public static EdgeInsetsMixed Lerp(EdgeInsetsMixed a,EdgeInsetsMixed b,float t)
    {
        return new EdgeInsetsMixed(a.Left + (b.Left - a.Left) * t,
        a.Right + (b.Right - a.Right) * t,
        a.Start + (b.Start - a.Start) * t,
        a.End + (b.End - a.End) * t,
        a.Top + (b.Top - a.Top) * t,
        a.Bottom + (b.Bottom - a.Bottom) * t);
    }
    public EdgeInsetsMixed Clamp(EdgeInsetsMixed min,EdgeInsetsMixed max)
    {
        return new EdgeInsetsMixed(CppMath.Clamp(Left, min.Left, max.Left),
        CppMath.Clamp(Right, min.Right, max.Right),
        CppMath.Clamp(Start, min.Start, max.Start),
        CppMath.Clamp(End, min.End, max.End),
        CppMath.Clamp(Top, min.Top, max.Top),
        CppMath.Clamp(Bottom, min.Bottom, max.Bottom));
    }
    public EdgeInsetsMixed CopyWith(float? Left=null,float? Right=null,float? Start=null,float? End=null,float? Top=null,float? Bottom=null)
    {
        return new EdgeInsetsMixed(Left.HasValue ? Left.Value : this.Left,
        Right.HasValue ? Right.Value : this.Right,
        Start.HasValue ? Start.Value : this.Start,
        End.HasValue ? End.Value : this.End,
        Top.HasValue ? Top.Value : this.Top,
        Bottom.HasValue ? Bottom.Value : this.Bottom);
    }
    public static EdgeInsetsMixed operator*(EdgeInsetsMixed lhs,float rhs)
    {
        return new EdgeInsetsMixed(lhs.Left * rhs, lhs.Right * rhs, lhs.Start * rhs, lhs.End * rhs, lhs.Top * rhs, lhs.Bottom * rhs);
    }
    public static EdgeInsetsMixed operator/(EdgeInsetsMixed lhs,float rhs)
    {
        return new EdgeInsetsMixed(lhs.Left / rhs, lhs.Right / rhs, lhs.Start / rhs, lhs.End / rhs, lhs.Top / rhs, lhs.Bottom / rhs);
    }
    public static EdgeInsetsMixed operator*(float lhs,EdgeInsetsMixed rhs)
    {
        return rhs * lhs;
    }
    public bool Equals(EdgeInsetsMixed rhs)=>this==rhs;
    public override bool Equals(object? value)=>value is EdgeInsetsMixed rhs && this==rhs;
    public override int GetHashCode()=>HashCode.Combine(Left,Right,Start,End,Top,Bottom);
}
