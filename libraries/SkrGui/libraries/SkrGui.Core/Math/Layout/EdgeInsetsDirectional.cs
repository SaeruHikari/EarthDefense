namespace SkrGui;
// Source: SkrGuiCore/math/layout/edge_insets.hpp (611561f8).
public partial struct EdgeInsetsDirectional : IEquatable<EdgeInsetsDirectional>
{
    public float Start,Top,End,Bottom;
    public EdgeInsetsDirectional(float start,float top,float end,float bottom) { Start=start; Top=top; End=end; Bottom=bottom; }
    public static EdgeInsetsDirectional Zero()
    {
        return new EdgeInsetsDirectional();
    }
    public static EdgeInsetsDirectional FromSTEB(float Start,float Top,float End,float Bottom)
    {
        return new EdgeInsetsDirectional(Start, Top, End, Bottom);
    }
    public static EdgeInsetsDirectional All(float value)
    {
        return new EdgeInsetsDirectional(value, value, value, value);
    }
    public static EdgeInsetsDirectional Symmetric(float horizontal=0,float vertical=0)
    {
        return new EdgeInsetsDirectional(horizontal, vertical, horizontal, vertical);
    }
    public static EdgeInsetsDirectional Only(float Start=0,float Top=0,float End=0,float Bottom=0)
    {
        return new EdgeInsetsDirectional(Start, Top, End, Bottom);
    }
    public float Horizontal()
    {
        return Start + End;
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
        return Start == 0.0f && Top == 0.0f && End == 0.0f && Bottom == 0.0f;
    }
    public bool IsNonNegative()
    {
        return Start >= 0.0f && Top >= 0.0f && End >= 0.0f && Bottom >= 0.0f;
    }
    public EdgeInsetsDirectional Flipped()
    {
        return new EdgeInsetsDirectional(End, Bottom, Start, Top);
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
        return new EdgeInsets(End, Top, Start, Bottom);
        case ETextDirection.LTR:
        return new EdgeInsets(Start, Top, End, Bottom);
        }
        return new EdgeInsets(Start, Top, End, Bottom);
    }
    public static bool operator==(EdgeInsetsDirectional lhs,EdgeInsetsDirectional rhs)
    {
        return lhs.Start == rhs.Start && lhs.Top == rhs.Top && lhs.End == rhs.End && lhs.Bottom == rhs.Bottom;
    }
    public static bool operator!=(EdgeInsetsDirectional lhs,EdgeInsetsDirectional rhs)
    {
        return !(lhs == rhs);
    }
    public static EdgeInsetsDirectional operator+(EdgeInsetsDirectional lhs,EdgeInsetsDirectional rhs)
    {
        return new EdgeInsetsDirectional(lhs.Start + rhs.Start, lhs.Top + rhs.Top, lhs.End + rhs.End, lhs.Bottom + rhs.Bottom);
    }
    public static EdgeInsetsDirectional operator-(EdgeInsetsDirectional lhs,EdgeInsetsDirectional rhs)
    {
        return new EdgeInsetsDirectional(lhs.Start - rhs.Start, lhs.Top - rhs.Top, lhs.End - rhs.End, lhs.Bottom - rhs.Bottom);
    }
    public static EdgeInsetsDirectional operator-(EdgeInsetsDirectional lhs)
    {
        return new EdgeInsetsDirectional(-lhs.Start, -lhs.Top, -lhs.End, -lhs.Bottom);
    }
    public static EdgeInsetsDirectional Lerp(EdgeInsetsDirectional a,EdgeInsetsDirectional b,float t)
    {
        return new EdgeInsetsDirectional(a.Start + (b.Start - a.Start) * t,
        a.Top + (b.Top - a.Top) * t,
        a.End + (b.End - a.End) * t,
        a.Bottom + (b.Bottom - a.Bottom) * t);
    }
    public EdgeInsetsDirectional Clamp(EdgeInsetsDirectional min,EdgeInsetsDirectional max)
    {
        return new EdgeInsetsDirectional(CppMath.Clamp(Start, min.Start, max.Start),
        CppMath.Clamp(Top, min.Top, max.Top),
        CppMath.Clamp(End, min.End, max.End),
        CppMath.Clamp(Bottom, min.Bottom, max.Bottom));
    }
    public EdgeInsetsDirectional CopyWith(float? Start=null,float? Top=null,float? End=null,float? Bottom=null)
    {
        return new EdgeInsetsDirectional(Start.HasValue ? Start.Value : this.Start,
        Top.HasValue ? Top.Value : this.Top,
        End.HasValue ? End.Value : this.End,
        Bottom.HasValue ? Bottom.Value : this.Bottom);
    }
    public static EdgeInsetsDirectional operator*(EdgeInsetsDirectional lhs,float rhs)
    {
        return new EdgeInsetsDirectional(lhs.Start * rhs, lhs.Top * rhs, lhs.End * rhs, lhs.Bottom * rhs);
    }
    public static EdgeInsetsDirectional operator/(EdgeInsetsDirectional lhs,float rhs)
    {
        return new EdgeInsetsDirectional(lhs.Start / rhs, lhs.Top / rhs, lhs.End / rhs, lhs.Bottom / rhs);
    }
    public static EdgeInsetsDirectional operator*(float lhs,EdgeInsetsDirectional rhs)
    {
        return rhs * lhs;
    }
    public static EdgeInsetsMixed operator+(EdgeInsetsDirectional lhs,EdgeInsets rhs)
    {
        return EdgeInsetsMixed.FromSTEB(lhs.Start, lhs.Top, lhs.End, lhs.Bottom) + rhs;
    }
    public static EdgeInsetsMixed operator-(EdgeInsetsDirectional lhs,EdgeInsets rhs)
    {
        return EdgeInsetsMixed.FromSTEB(lhs.Start, lhs.Top, lhs.End, lhs.Bottom) - rhs;
    }
    public static EdgeInsetsMixed operator+(EdgeInsetsDirectional lhs,EdgeInsetsMixed rhs)
    {
        return rhs + lhs;
    }
    public static EdgeInsetsMixed operator-(EdgeInsetsDirectional lhs,EdgeInsetsMixed rhs)
    {
        return EdgeInsetsMixed.FromSTEB(lhs.Start, lhs.Top, lhs.End, lhs.Bottom) - rhs;
    }
    public bool Equals(EdgeInsetsDirectional rhs)=>this==rhs;
    public override bool Equals(object? value)=>value is EdgeInsetsDirectional rhs && this==rhs;
    public override int GetHashCode()=>HashCode.Combine(Start,Top,End,Bottom);
}
