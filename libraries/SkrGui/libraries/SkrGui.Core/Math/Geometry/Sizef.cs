namespace SkrGui;

// Source: SkrGuiCore/math/geometry/size.hpp (611561f8).
public partial struct Sizef : IEquatable<Sizef>
{
    public float Width, Height;
    public Sizef(float width, float height) { Width=width; Height=height; }
    public static Sizef Zero() => new(0,0);
    public static Sizef operator+(Sizef lhs, Sizef rhs) => new(lhs.Width+rhs.Width,lhs.Height+rhs.Height);
    public static Sizef operator+(Sizef lhs, float rhs) => new(lhs.Width+rhs,lhs.Height+rhs);
    public static Sizef operator+(float lhs, Sizef rhs) => new(lhs+rhs.Width,lhs+rhs.Height);
    public static Sizef operator-(Sizef lhs, Sizef rhs) => new(lhs.Width-rhs.Width,lhs.Height-rhs.Height);
    public static Sizef operator-(Sizef lhs, float rhs) => new(lhs.Width-rhs,lhs.Height-rhs);
    public static Sizef operator-(float lhs, Sizef rhs) => new(lhs-rhs.Width,lhs-rhs.Height);
    public static Sizef operator*(Sizef lhs, Sizef rhs) => new(lhs.Width*rhs.Width,lhs.Height*rhs.Height);
    public static Sizef operator*(Sizef lhs, float rhs) => new(lhs.Width*rhs,lhs.Height*rhs);
    public static Sizef operator*(float lhs, Sizef rhs) => new(lhs*rhs.Width,lhs*rhs.Height);
    public static Sizef operator/(Sizef lhs, Sizef rhs) => new(lhs.Width/rhs.Width,lhs.Height/rhs.Height);
    public static Sizef operator/(Sizef lhs, float rhs) => new(lhs.Width/rhs,lhs.Height/rhs);
    public static Sizef operator/(float lhs, Sizef rhs) => new(lhs/rhs.Width,lhs/rhs.Height);
    public static Sizef operator%(Sizef lhs, Sizef rhs) => new(lhs.Width%rhs.Width,lhs.Height%rhs.Height);
    public static Sizef operator%(Sizef lhs, float rhs) => new(lhs.Width%rhs,lhs.Height%rhs);
    public static Sizef operator%(float lhs, Sizef rhs) => new(lhs%rhs.Width,lhs%rhs.Height);
    public static bool operator==(Sizef lhs,Sizef rhs) => lhs.Width==rhs.Width && lhs.Height==rhs.Height;
    public static bool operator!=(Sizef lhs,Sizef rhs) => !(lhs==rhs);
    public bool Equals(Sizef rhs) => this==rhs;
    public override bool Equals(object? obj) => obj is Sizef rhs && this==rhs;
    public override int GetHashCode() => HashCode.Combine(Width,Height);
    public static implicit operator Sizei(Sizef v) => new((int)v.Width,(int)v.Height);
    public static Sizef Square(float size) => new(size,size);
    public static Sizef Radius(float radius) => new(radius*2,radius*2);
    public bool IsEmpty() => Width<=0 || Height<=0;
    public float Area() => IsFinite() ? MathF.Abs(Width*Height) : float.PositiveInfinity;
    public float ShortestSide() => CppMath.Min(Math.Abs(Width),Math.Abs(Height));
    public float LongestSide() => CppMath.Max(Math.Abs(Width),Math.Abs(Height));
    public Sizef Flipped() => new(Height,Width);
    public bool Contains(Offsetf p) => p.X>=0 && p.X<=Width && p.Y>=0 && p.Y<=Height;
    public static Sizef Min(Sizef a,Sizef b) => new(CppMath.Min(a.Width,b.Width),CppMath.Min(a.Height,b.Height));
    public static Sizef Max(Sizef a,Sizef b) => new(CppMath.Max(a.Width,b.Width),CppMath.Max(a.Height,b.Height));
    public Offsetf TopLeft(Offsetf offset) => new(offset.X+0,offset.Y+0);
    public Offsetf TopCenter(Offsetf offset) => new(offset.X+Width/2,offset.Y+0);
    public Offsetf TopRight(Offsetf offset) => new(offset.X+Width,offset.Y+0);
    public Offsetf CenterLeft(Offsetf offset) => new(offset.X+0,offset.Y+Height/2);
    public Offsetf Center(Offsetf offset) => new(offset.X+Width/2,offset.Y+Height/2);
    public Offsetf CenterRight(Offsetf offset) => new(offset.X+Width,offset.Y+Height/2);
    public Offsetf BottomLeft(Offsetf offset) => new(offset.X+0,offset.Y+Height);
    public Offsetf BottomCenter(Offsetf offset) => new(offset.X+Width/2,offset.Y+Height);
    public Offsetf BottomRight(Offsetf offset) => new(offset.X+Width,offset.Y+Height);
    public bool IsFinite() => float.IsFinite(Width) && float.IsFinite(Height);
    public bool IsInfinite() => Width>=float.PositiveInfinity || Height>=float.PositiveInfinity;
    public static Sizef Infinite() => new(float.PositiveInfinity,float.PositiveInfinity);
    public static Sizef Lerp(Sizef a,Sizef b,float t) => new(a.Width+(b.Width-a.Width)*t,a.Height+(b.Height-a.Height)*t);
    public static Sizef InfiniteWidth(float height) => new(float.PositiveInfinity,height);
    public static Sizef InfiniteHeight(float width) => new(width,float.PositiveInfinity);
    public float AspectRatio() { if(Height!=0) return Width/Height; if(Width>0) return float.PositiveInfinity; if(Width<0) return float.NegativeInfinity; return 0; }
}
