namespace SkrGui;

// Source: SkrGuiCore/math/geometry/offset.hpp (611561f8).
public partial struct Offseti : IEquatable<Offseti>
{
    public int X, Y;
    public Offseti(int x, int y) { X=x; Y=y; }
    public static Offseti Zero() => new(0,0);
    public static Offseti operator-(Offseti value) => new(-value.X,-value.Y);
    public static Offseti operator+(Offseti lhs, Offseti rhs) => new(lhs.X+rhs.X,lhs.Y+rhs.Y);
    public static Offseti operator+(Offseti lhs, int rhs) => new(lhs.X+rhs,lhs.Y+rhs);
    public static Offseti operator+(int lhs, Offseti rhs) => new(lhs+rhs.X,lhs+rhs.Y);
    public static Offseti operator-(Offseti lhs, Offseti rhs) => new(lhs.X-rhs.X,lhs.Y-rhs.Y);
    public static Offseti operator-(Offseti lhs, int rhs) => new(lhs.X-rhs,lhs.Y-rhs);
    public static Offseti operator-(int lhs, Offseti rhs) => new(lhs-rhs.X,lhs-rhs.Y);
    public static Offseti operator*(Offseti lhs, Offseti rhs) => new(lhs.X*rhs.X,lhs.Y*rhs.Y);
    public static Offseti operator*(Offseti lhs, int rhs) => new(lhs.X*rhs,lhs.Y*rhs);
    public static Offseti operator*(int lhs, Offseti rhs) => new(lhs*rhs.X,lhs*rhs.Y);
    public static Offseti operator/(Offseti lhs, Offseti rhs) => new(lhs.X/rhs.X,lhs.Y/rhs.Y);
    public static Offseti operator/(Offseti lhs, int rhs) => new(lhs.X/rhs,lhs.Y/rhs);
    public static Offseti operator/(int lhs, Offseti rhs) => new(lhs/rhs.X,lhs/rhs.Y);
    public static Offseti operator%(Offseti lhs, Offseti rhs) => new(lhs.X%rhs.X,lhs.Y%rhs.Y);
    public static Offseti operator%(Offseti lhs, int rhs) => new(lhs.X%rhs,lhs.Y%rhs);
    public static Offseti operator%(int lhs, Offseti rhs) => new(lhs%rhs.X,lhs%rhs.Y);
    public static bool operator==(Offseti lhs,Offseti rhs) => lhs.X==rhs.X && lhs.Y==rhs.Y;
    public static bool operator!=(Offseti lhs,Offseti rhs) => !(lhs==rhs);
    public bool Equals(Offseti rhs) => this==rhs;
    public override bool Equals(object? obj) => obj is Offseti rhs && this==rhs;
    public override int GetHashCode() => HashCode.Combine(X,Y);
    public static implicit operator Offsetf(Offseti v) => new((float)v.X,(float)v.Y);
}
