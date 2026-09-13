namespace SkrGui;
// Source: SkrGuiCore/math/layout/edge_insets.hpp (611561f8).
public partial struct EdgeInsets : IEquatable<EdgeInsets>
{
    public float Left,Top,Right,Bottom;
    public EdgeInsets(float left,float top,float right,float bottom) { Left=left; Top=top; Right=right; Bottom=bottom; }
    public static EdgeInsets Zero()
    {
        return new EdgeInsets();
    }
    public static EdgeInsets FromLTRB(float Left,float Top,float Right,float Bottom)
    {
        return new EdgeInsets(Left, Top, Right, Bottom);
    }
    public static EdgeInsets All(float value)
    {
        return new EdgeInsets(value, value, value, value);
    }
    public static EdgeInsets Symmetric(float horizontal=0,float vertical=0)
    {
        return new EdgeInsets(horizontal, vertical, horizontal, vertical);
    }
    public static EdgeInsets Only(float Left=0,float Top=0,float Right=0,float Bottom=0)
    {
        return new EdgeInsets(Left, Top, Right, Bottom);
    }
    public float Horizontal()
    {
        return Left + Right;
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
        return Left == 0.0f && Top == 0.0f && Right == 0.0f && Bottom == 0.0f;
    }
    public bool IsNonNegative()
    {
        return Left >= 0.0f && Top >= 0.0f && Right >= 0.0f && Bottom >= 0.0f;
    }
    public EdgeInsets Flipped()
    {
        return new EdgeInsets(Right, Bottom, Left, Top);
    }
    public Offsetf TopLeft()
    {
        return new Offsetf(Left, Top);
    }
    public Offsetf TopRight()
    {
        return new Offsetf(-Right, Top);
    }
    public Offsetf BottomLeft()
    {
        return new Offsetf(Left, -Bottom);
    }
    public Offsetf BottomRight()
    {
        return new Offsetf(-Right, -Bottom);
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
    public Rectf InflateRect(Rectf rect)
    {
        return new Rectf(rect.Left - Left,
        rect.Top - Top,
        rect.Right + Right,
        rect.Bottom + Bottom);
    }
    public Rectf DeflateRect(Rectf rect)
    {
        return new Rectf(rect.Left + Left,
        rect.Top + Top,
        rect.Right - Right,
        rect.Bottom - Bottom);
    }
    public RRect InflateRrect(RRect rect)
    {
        Func<Radius,Radius> clamp_radius = (Radius radius) => {
        return Radius.Elliptical(
        CppMath.Max(0.0f, radius.X),
        CppMath.Max(0.0f, radius.Y)
        );
        };
        return new RRect(rect.Left - Left,
        rect.Top - Top,
        rect.Right + Right,
        rect.Bottom + Bottom,
        clamp_radius(rect.TlRadius + Radius.Elliptical(Left, Top)),
        clamp_radius(rect.TrRadius + Radius.Elliptical(Right, Top)),
        clamp_radius(rect.BrRadius + Radius.Elliptical(Right, Bottom)),
        clamp_radius(rect.BlRadius + Radius.Elliptical(Left, Bottom)));
    }
    public RRect DeflateRrect(RRect rect)
    {
        Func<Radius,Radius> clamp_radius = (Radius radius) => {
        return Radius.Elliptical(
        CppMath.Max(0.0f, radius.X),
        CppMath.Max(0.0f, radius.Y)
        );
        };
        return new RRect(rect.Left + Left,
        rect.Top + Top,
        rect.Right - Right,
        rect.Bottom - Bottom,
        clamp_radius(rect.TlRadius - Radius.Elliptical(Left, Top)),
        clamp_radius(rect.TrRadius - Radius.Elliptical(Right, Top)),
        clamp_radius(rect.BrRadius - Radius.Elliptical(Right, Bottom)),
        clamp_radius(rect.BlRadius - Radius.Elliptical(Left, Bottom)));
    }
    public EdgeInsets Resolve(ETextDirection direction)
    {
        return this;
    }
    public static bool operator==(EdgeInsets lhs,EdgeInsets rhs)
    {
        return lhs.Left == rhs.Left && lhs.Top == rhs.Top && lhs.Right == rhs.Right && lhs.Bottom == rhs.Bottom;
    }
    public static bool operator!=(EdgeInsets lhs,EdgeInsets rhs)
    {
        return !(lhs == rhs);
    }
    public static EdgeInsets operator+(EdgeInsets lhs,EdgeInsets rhs)
    {
        return new EdgeInsets(lhs.Left + rhs.Left, lhs.Top + rhs.Top, lhs.Right + rhs.Right, lhs.Bottom + rhs.Bottom);
    }
    public static EdgeInsets operator-(EdgeInsets lhs,EdgeInsets rhs)
    {
        return new EdgeInsets(lhs.Left - rhs.Left, lhs.Top - rhs.Top, lhs.Right - rhs.Right, lhs.Bottom - rhs.Bottom);
    }
    public static EdgeInsets operator-(EdgeInsets lhs)
    {
        return new EdgeInsets(-lhs.Left, -lhs.Top, -lhs.Right, -lhs.Bottom);
    }
    public static EdgeInsets Lerp(EdgeInsets a,EdgeInsets b,float t)
    {
        return new EdgeInsets(a.Left + (b.Left - a.Left) * t,
        a.Top + (b.Top - a.Top) * t,
        a.Right + (b.Right - a.Right) * t,
        a.Bottom + (b.Bottom - a.Bottom) * t);
    }
    public EdgeInsets Clamp(EdgeInsets min,EdgeInsets max)
    {
        return new EdgeInsets(CppMath.Clamp(Left, min.Left, max.Left),
        CppMath.Clamp(Top, min.Top, max.Top),
        CppMath.Clamp(Right, min.Right, max.Right),
        CppMath.Clamp(Bottom, min.Bottom, max.Bottom));
    }
    public EdgeInsets CopyWith(float? Left=null,float? Top=null,float? Right=null,float? Bottom=null)
    {
        return new EdgeInsets(Left.HasValue ? Left.Value : this.Left,
        Top.HasValue ? Top.Value : this.Top,
        Right.HasValue ? Right.Value : this.Right,
        Bottom.HasValue ? Bottom.Value : this.Bottom);
    }
    public static EdgeInsets operator*(EdgeInsets lhs,float rhs)
    {
        return new EdgeInsets(lhs.Left * rhs, lhs.Top * rhs, lhs.Right * rhs, lhs.Bottom * rhs);
    }
    public static EdgeInsets operator/(EdgeInsets lhs,float rhs)
    {
        return new EdgeInsets(lhs.Left / rhs, lhs.Top / rhs, lhs.Right / rhs, lhs.Bottom / rhs);
    }
    public static EdgeInsets operator*(float lhs,EdgeInsets rhs)
    {
        return rhs * lhs;
    }
    public static EdgeInsetsMixed operator+(EdgeInsets lhs,EdgeInsetsDirectional rhs)
    {
        return EdgeInsetsMixed.FromLTRB(lhs.Left, lhs.Top, lhs.Right, lhs.Bottom) + rhs;
    }
    public static EdgeInsetsMixed operator-(EdgeInsets lhs,EdgeInsetsDirectional rhs)
    {
        return EdgeInsetsMixed.FromLTRB(lhs.Left, lhs.Top, lhs.Right, lhs.Bottom) - rhs;
    }
    public static EdgeInsetsMixed operator+(EdgeInsets lhs,EdgeInsetsMixed rhs)
    {
        return rhs + lhs;
    }
    public static EdgeInsetsMixed operator-(EdgeInsets lhs,EdgeInsetsMixed rhs)
    {
        return EdgeInsetsMixed.FromLTRB(lhs.Left, lhs.Top, lhs.Right, lhs.Bottom) - rhs;
    }
    public bool Equals(EdgeInsets rhs)=>this==rhs;
    public override bool Equals(object? value)=>value is EdgeInsets rhs && this==rhs;
    public override int GetHashCode()=>HashCode.Combine(Left,Top,Right,Bottom);
}
