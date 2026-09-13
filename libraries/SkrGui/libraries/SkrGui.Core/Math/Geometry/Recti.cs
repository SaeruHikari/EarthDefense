namespace SkrGui;

// Source: SkrGuiCore/math/geometry/rect.hpp (611561f8).
public partial struct Recti : IEquatable<Recti>
{
    public int Left,Top,Right,Bottom;
    public Recti(int left,int top,int right,int bottom) { Left=left; Top=top; Right=right; Bottom=bottom; }
    public static Recti Zero() => new(0,0,0,0);
    public static Recti Largest() => new(-1000000000,-1000000000,1000000000,1000000000);
    public static Recti LTWH(int left,int top,int width,int height) => new(left,top,left+width,top+height);
    public static Recti Circle(Offseti center,int radius) => new(center.X-radius,center.Y-radius,center.X+radius,center.Y+radius);
    public static Recti Center(Offseti center,Sizei size) => new(center.X-size.Width/2,center.Y-size.Height/2,center.X+size.Width/2,center.Y+size.Height/2);
    public static Recti OffsetSize(Offseti offset,Sizei size) => new(offset.X,offset.Y,offset.X+size.Width,offset.Y+size.Height);
    public static Recti Points(Offseti a,Offseti b) => new(CppMath.Min(a.X,b.X),CppMath.Min(a.Y,b.Y),CppMath.Max(a.X,b.X),CppMath.Max(a.Y,b.Y));
    public bool IsEmpty() => Left>=Right || Top>=Bottom;
    public bool IsPoint() => Left==Right && Top==Bottom;
    public int Width() => Right-Left;
    public int Height() => Bottom-Top;
    public Sizei Size() => new(Width(),Height());
    public Offseti TopLeft() => new(Left,Top);
    public Offseti TopCenter() => new((Left+Right)/2,Top);
    public Offseti TopRight() => new(Right,Top);
    public Offseti CenterLeft() => new(Left,(Top+Bottom)/2);
    public Offseti Center() => new((Left+Right)/2,(Top+Bottom)/2);
    public Offseti CenterRight() => new(Right,(Top+Bottom)/2);
    public Offseti BottomLeft() => new(Left,Bottom);
    public Offseti BottomCenter() => new((Left+Right)/2,Bottom);
    public Offseti BottomRight() => new(Right,Bottom);
    public Recti Shift(Offseti offset) => new(Left+offset.X,Top+offset.Y,Right+offset.X,Bottom+offset.Y);
    public Recti Hold(Offseti point) => new(CppMath.Min(Left,point.X),CppMath.Min(Top,point.Y),CppMath.Max(Right,point.X),CppMath.Max(Bottom,point.Y));
    public Recti Inflate(int delta) => new(Left-delta,Top-delta,Right+delta,Bottom+delta);
    public Recti Deflate(int delta) => Inflate(-delta);
    public Recti Intersect(Recti rhs) => new(CppMath.Max(Left,rhs.Left),CppMath.Max(Top,rhs.Top),CppMath.Min(Right,rhs.Right),CppMath.Min(Bottom,rhs.Bottom));
    public Recti Unite(Recti rhs) => new(CppMath.Min(Left,rhs.Left),CppMath.Min(Top,rhs.Top),CppMath.Max(Right,rhs.Right),CppMath.Max(Bottom,rhs.Bottom));
    public bool Overlaps(Recti rhs) { if(Right<=rhs.Left||rhs.Right<=Left) return false; if(Bottom<=rhs.Top||rhs.Bottom<=Top) return false; return true; }
    public bool Contains(Offseti p) => p.X>=Left && p.X<Right && p.Y>=Top && p.Y<Bottom;
    public static bool operator==(Recti lhs,Recti rhs) => lhs.Left==rhs.Left && lhs.Top==rhs.Top && lhs.Right==rhs.Right && lhs.Bottom==rhs.Bottom;
    public static bool operator!=(Recti lhs,Recti rhs) => !(lhs==rhs);
    public bool Equals(Recti rhs) => this==rhs;
    public override bool Equals(object? obj) => obj is Recti rhs && this==rhs;
    public override int GetHashCode() => HashCode.Combine(Left,Top,Right,Bottom);
    public static implicit operator Rectf(Recti v) => new((float)v.Left,(float)v.Top,(float)v.Right,(float)v.Bottom);
}
