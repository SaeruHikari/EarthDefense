namespace SkrGui;

// Source: SkrGuiCore/math/geometry/radius.hpp (611561f8).
public struct Radius : IEquatable<Radius>
{
    public float X,Y;
    public Radius(float x,float y) { X=x; Y=y; }
    public static Radius Zero() => new(0,0);
    public static Radius Circular(float radius) => new(radius,radius);
    public static Radius Elliptical(float x,float y) => new(x,y);
    public bool IsZero() => X==0 && Y==0;
    public static bool operator==(Radius a,Radius b) => a.X==b.X && a.Y==b.Y;
    public static bool operator!=(Radius a,Radius b) => !(a==b);
    public bool Equals(Radius b) => this==b;
    public override bool Equals(object? obj) => obj is Radius b && this==b;
    public override int GetHashCode() => HashCode.Combine(X,Y);
    public static Radius operator-(Radius a) => new(-a.X,-a.Y);
    public static Radius operator+(Radius a,Radius b) => new(a.X+b.X,a.Y+b.Y);
    public static Radius operator-(Radius a,Radius b) => new(a.X-b.X,a.Y-b.Y);
    public static Radius operator*(Radius a,float b) => new(a.X*b,a.Y*b);
    public static Radius operator/(Radius a,float b) => new(a.X/b,a.Y/b);
    public static Radius operator*(float a,Radius b) => b*a;
    public static Radius Lerp(Radius a,Radius b,float t) => new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
}
