namespace SkrGui;

// Source: SkrGuiCore/math/geometry/size.hpp (611561f8).
public partial struct Sizei : IEquatable<Sizei>
{
    public int Width, Height;
    public Sizei(int width, int height) { Width=width; Height=height; }
    public static Sizei Zero() => new(0,0);
    public static Sizei operator+(Sizei lhs, Sizei rhs) => new(lhs.Width+rhs.Width,lhs.Height+rhs.Height);
    public static Sizei operator+(Sizei lhs, int rhs) => new(lhs.Width+rhs,lhs.Height+rhs);
    public static Sizei operator+(int lhs, Sizei rhs) => new(lhs+rhs.Width,lhs+rhs.Height);
    public static Sizei operator-(Sizei lhs, Sizei rhs) => new(lhs.Width-rhs.Width,lhs.Height-rhs.Height);
    public static Sizei operator-(Sizei lhs, int rhs) => new(lhs.Width-rhs,lhs.Height-rhs);
    public static Sizei operator-(int lhs, Sizei rhs) => new(lhs-rhs.Width,lhs-rhs.Height);
    public static Sizei operator*(Sizei lhs, Sizei rhs) => new(lhs.Width*rhs.Width,lhs.Height*rhs.Height);
    public static Sizei operator*(Sizei lhs, int rhs) => new(lhs.Width*rhs,lhs.Height*rhs);
    public static Sizei operator*(int lhs, Sizei rhs) => new(lhs*rhs.Width,lhs*rhs.Height);
    public static Sizei operator/(Sizei lhs, Sizei rhs) => new(lhs.Width/rhs.Width,lhs.Height/rhs.Height);
    public static Sizei operator/(Sizei lhs, int rhs) => new(lhs.Width/rhs,lhs.Height/rhs);
    public static Sizei operator/(int lhs, Sizei rhs) => new(lhs/rhs.Width,lhs/rhs.Height);
    public static Sizei operator%(Sizei lhs, Sizei rhs) => new(lhs.Width%rhs.Width,lhs.Height%rhs.Height);
    public static Sizei operator%(Sizei lhs, int rhs) => new(lhs.Width%rhs,lhs.Height%rhs);
    public static Sizei operator%(int lhs, Sizei rhs) => new(lhs%rhs.Width,lhs%rhs.Height);
    public static bool operator==(Sizei lhs,Sizei rhs) => lhs.Width==rhs.Width && lhs.Height==rhs.Height;
    public static bool operator!=(Sizei lhs,Sizei rhs) => !(lhs==rhs);
    public bool Equals(Sizei rhs) => this==rhs;
    public override bool Equals(object? obj) => obj is Sizei rhs && this==rhs;
    public override int GetHashCode() => HashCode.Combine(Width,Height);
    public static implicit operator Sizef(Sizei v) => new((float)v.Width,(float)v.Height);
    public static Sizei Square(int size) => new(size,size);
    public static Sizei Radius(int radius) => new(radius*2,radius*2);
    public bool IsEmpty() => Width<=0 || Height<=0;
    public int Area() => Math.Abs(Width*Height);
    public int ShortestSide() => CppMath.Min(Math.Abs(Width),Math.Abs(Height));
    public int LongestSide() => CppMath.Max(Math.Abs(Width),Math.Abs(Height));
    public Sizei Flipped() => new(Height,Width);
    public bool Contains(Offseti p) => p.X>=0 && p.X<=Width && p.Y>=0 && p.Y<=Height;
    public static Sizei Min(Sizei a,Sizei b) => new(CppMath.Min(a.Width,b.Width),CppMath.Min(a.Height,b.Height));
    public static Sizei Max(Sizei a,Sizei b) => new(CppMath.Max(a.Width,b.Width),CppMath.Max(a.Height,b.Height));
    public Offseti TopLeft(Offseti offset) => new(offset.X+0,offset.Y+0);
    public Offseti TopCenter(Offseti offset) => new(offset.X+Width/2,offset.Y+0);
    public Offseti TopRight(Offseti offset) => new(offset.X+Width,offset.Y+0);
    public Offseti CenterLeft(Offseti offset) => new(offset.X+0,offset.Y+Height/2);
    public Offseti Center(Offseti offset) => new(offset.X+Width/2,offset.Y+Height/2);
    public Offseti CenterRight(Offseti offset) => new(offset.X+Width,offset.Y+Height/2);
    public Offseti BottomLeft(Offseti offset) => new(offset.X+0,offset.Y+Height);
    public Offseti BottomCenter(Offseti offset) => new(offset.X+Width/2,offset.Y+Height);
    public Offseti BottomRight(Offseti offset) => new(offset.X+Width,offset.Y+Height);
}
