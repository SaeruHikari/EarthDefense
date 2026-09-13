namespace SkrGui;

// Source: SkrGuiCore/math/geometry/rect.hpp (611561f8).
public partial struct Rectf : IEquatable<Rectf>
{
    public float Left,Top,Right,Bottom;
    public Rectf(float left,float top,float right,float bottom) { Left=left; Top=top; Right=right; Bottom=bottom; }
    public static Rectf Zero() => new(0,0,0,0);
    public static Rectf Largest() => new(-1000000000,-1000000000,1000000000,1000000000);
    public static Rectf LTWH(float left,float top,float width,float height) => new(left,top,left+width,top+height);
    public static Rectf Circle(Offsetf center,float radius) => new(center.X-radius,center.Y-radius,center.X+radius,center.Y+radius);
    public static Rectf Center(Offsetf center,Sizef size) => new(center.X-size.Width/2,center.Y-size.Height/2,center.X+size.Width/2,center.Y+size.Height/2);
    public static Rectf OffsetSize(Offsetf offset,Sizef size) => new(offset.X,offset.Y,offset.X+size.Width,offset.Y+size.Height);
    public static Rectf Points(Offsetf a,Offsetf b) => new(CppMath.Min(a.X,b.X),CppMath.Min(a.Y,b.Y),CppMath.Max(a.X,b.X),CppMath.Max(a.Y,b.Y));
    public bool IsEmpty() => Left>=Right || Top>=Bottom;
    public bool IsPoint() => Left==Right && Top==Bottom;
    public float Width() => Right-Left;
    public float Height() => Bottom-Top;
    public Sizef Size() => new(Width(),Height());
    public Offsetf TopLeft() => new(Left,Top);
    public Offsetf TopCenter() => new((Left+Right)/2,Top);
    public Offsetf TopRight() => new(Right,Top);
    public Offsetf CenterLeft() => new(Left,(Top+Bottom)/2);
    public Offsetf Center() => new((Left+Right)/2,(Top+Bottom)/2);
    public Offsetf CenterRight() => new(Right,(Top+Bottom)/2);
    public Offsetf BottomLeft() => new(Left,Bottom);
    public Offsetf BottomCenter() => new((Left+Right)/2,Bottom);
    public Offsetf BottomRight() => new(Right,Bottom);
    public Rectf Shift(Offsetf offset) => new(Left+offset.X,Top+offset.Y,Right+offset.X,Bottom+offset.Y);
    public Rectf Hold(Offsetf point) => new(CppMath.Min(Left,point.X),CppMath.Min(Top,point.Y),CppMath.Max(Right,point.X),CppMath.Max(Bottom,point.Y));
    public Rectf Inflate(float delta) => new(Left-delta,Top-delta,Right+delta,Bottom+delta);
    public Rectf Deflate(float delta) => Inflate(-delta);
    public Rectf Intersect(Rectf rhs) => new(CppMath.Max(Left,rhs.Left),CppMath.Max(Top,rhs.Top),CppMath.Min(Right,rhs.Right),CppMath.Min(Bottom,rhs.Bottom));
    public Rectf Unite(Rectf rhs) => new(CppMath.Min(Left,rhs.Left),CppMath.Min(Top,rhs.Top),CppMath.Max(Right,rhs.Right),CppMath.Max(Bottom,rhs.Bottom));
    public bool Overlaps(Rectf rhs) { if(Right<=rhs.Left||rhs.Right<=Left) return false; if(Bottom<=rhs.Top||rhs.Bottom<=Top) return false; return true; }
    public bool Contains(Offsetf p) => p.X>=Left && p.X<Right && p.Y>=Top && p.Y<Bottom;
    public static bool operator==(Rectf lhs,Rectf rhs) => lhs.Left==rhs.Left && lhs.Top==rhs.Top && lhs.Right==rhs.Right && lhs.Bottom==rhs.Bottom;
    public static bool operator!=(Rectf lhs,Rectf rhs) => !(lhs==rhs);
    public bool Equals(Rectf rhs) => this==rhs;
    public override bool Equals(object? obj) => obj is Rectf rhs && this==rhs;
    public override int GetHashCode() => HashCode.Combine(Left,Top,Right,Bottom);
    public static implicit operator Recti(Rectf v) => new((int)v.Left,(int)v.Top,(int)v.Right,(int)v.Bottom);
    public bool IsInfinite() => Left<=float.NegativeInfinity || Top<=float.NegativeInfinity || Right>=float.PositiveInfinity || Bottom>=float.PositiveInfinity;
    public bool IsFinite() => float.IsFinite(Left)&&float.IsFinite(Top)&&float.IsFinite(Right)&&float.IsFinite(Bottom);
    public bool HasNan() => float.IsNaN(Left)||float.IsNaN(Top)||float.IsNaN(Right)||float.IsNaN(Bottom);
    public static Rectf? Lerp(Rectf? a,Rectf? b,float t) { if(!b.HasValue) { if(!a.HasValue) return null; float k=1-t; return new Rectf(a.Value.Left*k,a.Value.Top*k,a.Value.Right*k,a.Value.Bottom*k); } if(!a.HasValue) return new Rectf(b.Value.Left*t,b.Value.Top*t,b.Value.Right*t,b.Value.Bottom*t); var av=a.Value; var bv=b.Value; return new Rectf(av.Left+(bv.Left-av.Left)*t,av.Top+(bv.Top-av.Top)*t,av.Right+(bv.Right-av.Right)*t,av.Bottom+(bv.Bottom-av.Bottom)*t); }
}
