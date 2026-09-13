namespace SkrGui;

// Source: SkrGuiCore/math/geometry/offset.hpp (611561f8).
public partial struct Offsetf : IEquatable<Offsetf>
{
    public float X, Y;
    public Offsetf(float x, float y) { X=x; Y=y; }
    public static Offsetf Zero() => new(0,0);
    public static Offsetf operator-(Offsetf value) => new(-value.X,-value.Y);
    public static Offsetf operator+(Offsetf lhs, Offsetf rhs) => new(lhs.X+rhs.X,lhs.Y+rhs.Y);
    public static Offsetf operator+(Offsetf lhs, float rhs) => new(lhs.X+rhs,lhs.Y+rhs);
    public static Offsetf operator+(float lhs, Offsetf rhs) => new(lhs+rhs.X,lhs+rhs.Y);
    public static Offsetf operator-(Offsetf lhs, Offsetf rhs) => new(lhs.X-rhs.X,lhs.Y-rhs.Y);
    public static Offsetf operator-(Offsetf lhs, float rhs) => new(lhs.X-rhs,lhs.Y-rhs);
    public static Offsetf operator-(float lhs, Offsetf rhs) => new(lhs-rhs.X,lhs-rhs.Y);
    public static Offsetf operator*(Offsetf lhs, Offsetf rhs) => new(lhs.X*rhs.X,lhs.Y*rhs.Y);
    public static Offsetf operator*(Offsetf lhs, float rhs) => new(lhs.X*rhs,lhs.Y*rhs);
    public static Offsetf operator*(float lhs, Offsetf rhs) => new(lhs*rhs.X,lhs*rhs.Y);
    public static Offsetf operator/(Offsetf lhs, Offsetf rhs) => new(lhs.X/rhs.X,lhs.Y/rhs.Y);
    public static Offsetf operator/(Offsetf lhs, float rhs) => new(lhs.X/rhs,lhs.Y/rhs);
    public static Offsetf operator/(float lhs, Offsetf rhs) => new(lhs/rhs.X,lhs/rhs.Y);
    public static Offsetf operator%(Offsetf lhs, Offsetf rhs) => new(lhs.X%rhs.X,lhs.Y%rhs.Y);
    public static Offsetf operator%(Offsetf lhs, float rhs) => new(lhs.X%rhs,lhs.Y%rhs);
    public static Offsetf operator%(float lhs, Offsetf rhs) => new(lhs%rhs.X,lhs%rhs.Y);
    public static bool operator==(Offsetf lhs,Offsetf rhs) => lhs.X==rhs.X && lhs.Y==rhs.Y;
    public static bool operator!=(Offsetf lhs,Offsetf rhs) => !(lhs==rhs);
    public bool Equals(Offsetf rhs) => this==rhs;
    public override bool Equals(object? obj) => obj is Offsetf rhs && this==rhs;
    public override int GetHashCode() => HashCode.Combine(X,Y);
    public static implicit operator Offseti(Offsetf v) => new((int)v.X,(int)v.Y);
    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y);
    public bool IsInfinite() => X>=float.PositiveInfinity || Y>=float.PositiveInfinity;
    public static Offsetf Infinite() => new(float.PositiveInfinity,float.PositiveInfinity);
    public static Offsetf Lerp(Offsetf a,Offsetf b,float t) => new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);
    public static Offsetf Radians(float radians,float radius=1) => new(radius*MathF.Cos(radians),radius*MathF.Sin(radians));
    public bool NearlyEqual(Offsetf rhs,float tolerance) => (this-rhs).LengthSquared() <= tolerance*tolerance;
    public float Length() => MathF.Sqrt(X*X+Y*Y);
    public float LengthSquared() => X*X+Y*Y;
    public float Radians() => MathF.Atan2(Y,X);
    public Offsetf Normalize(Offsetf fallback=default) { float lengthSq=LengthSquared(); if(!float.IsFinite(lengthSq)||lengthSq<=0) return fallback; return this/MathF.Sqrt(lengthSq); }
    public Offsetf CwNormal() => new(-Y,X);
    public Offsetf CcwNormal() => new(Y,-X);
    public float Cross(Offsetf rhs) => X*rhs.Y-Y*rhs.X;
    public float Dot(Offsetf rhs) => X*rhs.X+Y*rhs.Y;
}
