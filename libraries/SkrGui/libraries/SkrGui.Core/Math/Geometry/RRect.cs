namespace SkrGui;

// Source: SkrGuiCore/math/geometry/rrect.hpp (611561f8).
public struct RRect : IEquatable<RRect>
{
    public float Left,Top,Right,Bottom;
    public Radius TlRadius,TrRadius,BrRadius,BlRadius;
    public RRect(float left,float top,float right,float bottom,Radius tlRadius=default,Radius trRadius=default,Radius brRadius=default,Radius blRadius=default)
    { Left=left; Top=top; Right=right; Bottom=bottom; TlRadius=tlRadius; TrRadius=trRadius; BrRadius=brRadius; BlRadius=blRadius; }
    public static RRect Zero() => default;
    public static RRect FromLTRBXY(float left,float top,float right,float bottom,float radiusX,float radiusY) { var radius=Radius.Elliptical(radiusX,radiusY); return new(left,top,right,bottom,radius,radius,radius,radius); }
    public static RRect FromRectXY(Rectf rect,float radiusX,float radiusY) => FromLTRBXY(rect.Left,rect.Top,rect.Right,rect.Bottom,radiusX,radiusY);
    public static RRect FromRectRadius(Rectf rect,Radius radius) => new(rect.Left,rect.Top,rect.Right,rect.Bottom,radius,radius,radius,radius);
    public bool IsEmpty() => Left>=Right || Top>=Bottom;
    public bool IsFinite() => float.IsFinite(Left)&&float.IsFinite(Top)&&float.IsFinite(Right)&&float.IsFinite(Bottom)&&float.IsFinite(TlRadius.X)&&float.IsFinite(TlRadius.Y)&&float.IsFinite(TrRadius.X)&&float.IsFinite(TrRadius.Y)&&float.IsFinite(BrRadius.X)&&float.IsFinite(BrRadius.Y)&&float.IsFinite(BlRadius.X)&&float.IsFinite(BlRadius.Y);
    public bool HasNan() => float.IsNaN(Left)||float.IsNaN(Top)||float.IsNaN(Right)||float.IsNaN(Bottom)||float.IsNaN(TlRadius.X)||float.IsNaN(TlRadius.Y)||float.IsNaN(TrRadius.X)||float.IsNaN(TrRadius.Y)||float.IsNaN(BrRadius.X)||float.IsNaN(BrRadius.Y)||float.IsNaN(BlRadius.X)||float.IsNaN(BlRadius.Y);
    public float Width() => Right-Left;
    public float Height() => Bottom-Top;
    public Sizef Size() => new(Width(),Height());
    public Offsetf Center() => new(Left+Width()/2,Top+Height()/2);
    public Rectf Rect() => new(Left,Top,Right,Bottom);
    public Rectf OuterRect() => Rect();
    public Rectf SafeInnerRect()
    {
        const float insetFactor=0.29289321881f;
        float leftRadius=CppMath.Max(BlRadius.X,TlRadius.X),topRadius=CppMath.Max(TlRadius.Y,TrRadius.Y),rightRadius=CppMath.Max(TrRadius.X,BrRadius.X),bottomRadius=CppMath.Max(BrRadius.Y,BlRadius.Y);
        return new(Left+leftRadius*insetFactor,Top+topRadius*insetFactor,Right-rightRadius*insetFactor,Bottom-bottomRadius*insetFactor);
    }
    public Rectf MiddleRect() => new(Left+CppMath.Max(BlRadius.X,TlRadius.X),Top+CppMath.Max(TlRadius.Y,TrRadius.Y),Right-CppMath.Max(TrRadius.X,BrRadius.X),Bottom-CppMath.Max(BrRadius.Y,BlRadius.Y));
    public Rectf WideMiddleRect() => new(Left,Top+CppMath.Max(TlRadius.Y,TrRadius.Y),Right,Bottom-CppMath.Max(BrRadius.Y,BlRadius.Y));
    public Rectf TallMiddleRect() => new(Left+CppMath.Max(BlRadius.X,TlRadius.X),Top,Right-CppMath.Max(TrRadius.X,BrRadius.X),Bottom);
    public RRect Shift(Offsetf offset) => new(Left+offset.X,Top+offset.Y,Right+offset.X,Bottom+offset.Y,TlRadius,TrRadius,BrRadius,BlRadius);
    public RRect Inflate(float delta)
    {
        Radius InflateRadius(Radius radius) => Radius.Elliptical(CppMath.Max(0,radius.X+delta),CppMath.Max(0,radius.Y+delta));
        return new(Left-delta,Top-delta,Right+delta,Bottom+delta,InflateRadius(TlRadius),InflateRadius(TrRadius),InflateRadius(BrRadius),InflateRadius(BlRadius));
    }
    public RRect Deflate(float delta) => Inflate(-delta);
    public RRect ScaleRadii()
    {
        static float ScaleMin(float current,float radius1,float radius2,float limit) { float sum=radius1+radius2; if(sum>limit&&sum!=0) return CppMath.Min(current,limit/sum); return current; }
        float scale=1;
        scale=ScaleMin(scale,BlRadius.Y,TlRadius.Y,Height());
        scale=ScaleMin(scale,TlRadius.X,TrRadius.X,Width());
        scale=ScaleMin(scale,TrRadius.Y,BrRadius.Y,Height());
        scale=ScaleMin(scale,BrRadius.X,BlRadius.X,Width());
        if(scale>=1) return this;
        return new(Left,Top,Right,Bottom,TlRadius*scale,TrRadius*scale,BrRadius*scale,BlRadius*scale);
    }
    public bool Contains(Offsetf point)
    {
        var scaled=ScaleRadii();
        if(point.X<Left||point.X>=Right||point.Y<Top||point.Y>=Bottom) return false;
        float x=0,y=0,radiusX=0,radiusY=0;
        if(point.X<Left+scaled.TlRadius.X&&point.Y<Top+scaled.TlRadius.Y) { x=point.X-Left-scaled.TlRadius.X; y=point.Y-Top-scaled.TlRadius.Y; radiusX=scaled.TlRadius.X; radiusY=scaled.TlRadius.Y; }
        else if(point.X>Right-scaled.TrRadius.X&&point.Y<Top+scaled.TrRadius.Y) { x=point.X-Right+scaled.TrRadius.X; y=point.Y-Top-scaled.TrRadius.Y; radiusX=scaled.TrRadius.X; radiusY=scaled.TrRadius.Y; }
        else if(point.X>Right-scaled.BrRadius.X&&point.Y>Bottom-scaled.BrRadius.Y) { x=point.X-Right+scaled.BrRadius.X; y=point.Y-Bottom+scaled.BrRadius.Y; radiusX=scaled.BrRadius.X; radiusY=scaled.BrRadius.Y; }
        else if(point.X<Left+scaled.BlRadius.X&&point.Y>Bottom-scaled.BlRadius.Y) { x=point.X-Left-scaled.BlRadius.X; y=point.Y-Bottom+scaled.BlRadius.Y; radiusX=scaled.BlRadius.X; radiusY=scaled.BlRadius.Y; }
        else return true;
        if(radiusX<=0||radiusY<=0) return true;
        x/=radiusX; y/=radiusY;
        return x*x+y*y<=1;
    }
    public static bool operator==(RRect a,RRect b) => a.Left==b.Left&&a.Top==b.Top&&a.Right==b.Right&&a.Bottom==b.Bottom&&a.TlRadius==b.TlRadius&&a.TrRadius==b.TrRadius&&a.BrRadius==b.BrRadius&&a.BlRadius==b.BlRadius;
    public static bool operator!=(RRect a,RRect b) => !(a==b);
    public bool Equals(RRect b) => this==b;
    public override bool Equals(object? obj) => obj is RRect b && this==b;
    public override int GetHashCode() => HashCode.Combine(Left,Top,Right,Bottom,TlRadius,TrRadius,BrRadius,BlRadius);
    public static RRect Lerp(RRect a,RRect b,float t) => new(a.Left+(b.Left-a.Left)*t,a.Top+(b.Top-a.Top)*t,a.Right+(b.Right-a.Right)*t,a.Bottom+(b.Bottom-a.Bottom)*t,Radius.Lerp(a.TlRadius,b.TlRadius,t),Radius.Lerp(a.TrRadius,b.TrRadius,t),Radius.Lerp(a.BrRadius,b.BrRadius,t),Radius.Lerp(a.BlRadius,b.BlRadius,t));
}
