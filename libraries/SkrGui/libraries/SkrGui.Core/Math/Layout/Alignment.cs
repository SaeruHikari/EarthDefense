namespace SkrGui;
// Source: SkrGuiCore/math/layout/alignment.hpp (611561f8).
public partial struct Alignment : IEquatable<Alignment>
{
    public float X,Y;
    public Alignment(float x,float y) { X=x; Y=y; }
    public static Alignment TopLeft()
    {
        return new Alignment(-1.0f, -1.0f);
    }
    public static Alignment TopCenter()
    {
        return new Alignment(0.0f, -1.0f);
    }
    public static Alignment TopRight()
    {
        return new Alignment(1.0f, -1.0f);
    }
    public static Alignment CenterLeft()
    {
        return new Alignment(-1.0f, 0.0f);
    }
    public static Alignment Center()
    {
        return new Alignment(0.0f, 0.0f);
    }
    public static Alignment CenterRight()
    {
        return new Alignment(1.0f, 0.0f);
    }
    public static Alignment BottomLeft()
    {
        return new Alignment(-1.0f, 1.0f);
    }
    public static Alignment BottomCenter()
    {
        return new Alignment(0.0f, 1.0f);
    }
    public static Alignment BottomRight()
    {
        return new Alignment(1.0f, 1.0f);
    }
    public float Horizontal()
    {
        return X;
    }
    public float Vertical()
    {
        return Y;
    }
    public Offsetf AlongOffset(Offsetf offset)
    {
        float center_x = offset.X / 2.0f;
        float center_y = offset.Y / 2.0f;
        return new Offsetf(center_x + X * center_x, center_y + Y * center_y);
    }
    public Offsetf AlongSize(Sizef size)
    {
        float center_x = size.Width / 2.0f;
        float center_y = size.Height / 2.0f;
        return new Offsetf(center_x + X * center_x, center_y + Y * center_y);
    }
    public Offsetf AlongRect(Rectf rect)
    {
        float half_width = rect.Width() / 2.0f;
        float half_height = rect.Height() / 2.0f;
        return new Offsetf(rect.Left + half_width + X * half_width, rect.Top + half_height + Y * half_height);
    }
    public Offsetf WithinRect(Rectf rect)
    {
        return AlongRect(rect);
    }
    public Rectf Inscribe(Sizef child_size,Rectf parent_rect)
    {
        float half_width_delta = (parent_rect.Width() - child_size.Width) / 2.0f;
        float half_height_delta = (parent_rect.Height() - child_size.Height) / 2.0f;
        return Rectf.LTWH(
        parent_rect.Left + half_width_delta + X * half_width_delta,
        parent_rect.Top + half_height_delta + Y * half_height_delta,
        child_size.Width,
        child_size.Height
        );
    }
    public Alignment Resolve(ETextDirection direction)
    {
        return this;
    }
    public static bool operator==(Alignment lhs,Alignment rhs)
    {
        return lhs.X == rhs.X && lhs.Y == rhs.Y;
    }
    public static bool operator!=(Alignment lhs,Alignment rhs)
    {
        return !(lhs == rhs);
    }
    public static Alignment operator+(Alignment lhs,Alignment rhs)
    {
        return new Alignment(lhs.X + rhs.X, lhs.Y + rhs.Y);
    }
    public static Alignment operator-(Alignment lhs,Alignment rhs)
    {
        return new Alignment(lhs.X - rhs.X, lhs.Y - rhs.Y);
    }
    public static Alignment operator*(Alignment lhs,Alignment rhs)
    {
        return new Alignment(lhs.X * rhs.X, lhs.Y * rhs.Y);
    }
    public static Alignment operator/(Alignment lhs,Alignment rhs)
    {
        return new Alignment(lhs.X / rhs.X, lhs.Y / rhs.Y);
    }
    public static Alignment operator%(Alignment lhs,Alignment rhs)
    {
        return new Alignment(CppMath.Fmod(lhs.X, rhs.X), CppMath.Fmod(lhs.Y, rhs.Y));
    }
    public static Alignment operator-(Alignment lhs)
    {
        return new Alignment(-lhs.X, -lhs.Y);
    }
    public AlignmentMixed Add(AlignmentMixed rhs)
    {
        return new AlignmentMixed(this) + rhs;
    }
    public static Alignment Lerp(Alignment a,Alignment b,float t)
    {
        return new Alignment(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
    public Alignment Lerp(Alignment rhs,float t)
    {
        return Lerp(this, rhs, t);
    }
    public Alignment CopyWith(float? X=null,float? Y=null)
    {
        return new Alignment(X.HasValue ? X.Value : this.X,
        Y.HasValue ? Y.Value : this.Y);
    }
    public static Alignment operator+(Alignment lhs,float rhs)
    {
        return new Alignment(lhs.X + rhs, lhs.Y + rhs);
    }
    public static Alignment operator-(Alignment lhs,float rhs)
    {
        return new Alignment(lhs.X - rhs, lhs.Y - rhs);
    }
    public static Alignment operator*(Alignment lhs,float rhs)
    {
        return new Alignment(lhs.X * rhs, lhs.Y * rhs);
    }
    public static Alignment operator/(Alignment lhs,float rhs)
    {
        return new Alignment(lhs.X / rhs, lhs.Y / rhs);
    }
    public static Alignment operator%(Alignment lhs,float rhs)
    {
        return new Alignment(CppMath.Fmod(lhs.X, rhs), CppMath.Fmod(lhs.Y, rhs));
    }
    public static Alignment operator+(float lhs,Alignment rhs)
    {
        return new Alignment(lhs + rhs.X, lhs + rhs.Y);
    }
    public static Alignment operator-(float lhs,Alignment rhs)
    {
        return new Alignment(lhs - rhs.X, lhs - rhs.Y);
    }
    public static Alignment operator*(float lhs,Alignment rhs)
    {
        return new Alignment(lhs * rhs.X, lhs * rhs.Y);
    }
    public static Alignment operator/(float lhs,Alignment rhs)
    {
        return new Alignment(lhs / rhs.X, lhs / rhs.Y);
    }
    public static Alignment operator%(float lhs,Alignment rhs)
    {
        return new Alignment(CppMath.Fmod(lhs, rhs.X), CppMath.Fmod(lhs, rhs.Y));
    }
    public static AlignmentMixed operator+(Alignment lhs,AlignmentDirectional rhs)
    {
        return AlignmentMixed.FromXY(lhs.X, lhs.Y) + rhs;
    }
    public static AlignmentMixed operator-(Alignment lhs,AlignmentDirectional rhs)
    {
        return AlignmentMixed.FromXY(lhs.X, lhs.Y) - rhs;
    }
    public static AlignmentMixed operator+(Alignment lhs,AlignmentMixed rhs)
    {
        return rhs + lhs;
    }
    public static AlignmentMixed operator-(Alignment lhs,AlignmentMixed rhs)
    {
        return AlignmentMixed.FromXY(lhs.X, lhs.Y) - rhs;
    }
    public bool Equals(Alignment rhs)=>this==rhs;
    public override bool Equals(object? value)=>value is Alignment rhs && this==rhs;
    public override int GetHashCode()=>HashCode.Combine(X,Y);
}
