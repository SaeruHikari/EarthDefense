using SkrGui;
namespace SkrGui.Tests;
internal static class OriginalMathHelpers
{
 public const float kFloatEpsilon=0.0001f,kPi=3.14159265358979323846f;
 public static void ExpectNear(double a,double b,double epsilon)=>Check.That(Math.Abs(a-b)<=epsilon,$"{a} != {b} within {epsilon}");
 public static T Identity<T>(T value)=>value;
 public static void EqualNumeric<T,U>(T a,U b) { if(a is IConvertible&&b is IConvertible)Check.That(Convert.ToDouble(a)==Convert.ToDouble(b),$"{a} != {b}");else Check.That(Equals(a,b),$"{a} != {b}"); }
 public static void NotEqualNumeric<T,U>(T a,U b)=>Check.False(Equals(a,b));
 public static void LessEqual(double a,double b)=>Check.That(a<=b);
 public static void Less(double a,double b)=>Check.That(a<b);
 public static void Greater(double a,double b)=>Check.That(a>b);
public static void CheckOffsetf(Offsetf value, float x, float y){
    ExpectNear(value.X, x, kFloatEpsilon);
    ExpectNear(value.Y, y, kFloatEpsilon);
}
public static void CheckSizef(Sizef value, float width, float height){
    ExpectNear(value.Width, width, kFloatEpsilon);
    ExpectNear(value.Height, height, kFloatEpsilon);
}
public static void CheckRectf(
    Rectf value,
    float left,
    float top,
    float right,
    float bottom
){
    ExpectNear(value.Left, left, kFloatEpsilon);
    ExpectNear(value.Top, top, kFloatEpsilon);
    ExpectNear(value.Right, right, kFloatEpsilon);
    ExpectNear(value.Bottom, bottom, kFloatEpsilon);
}
public static void CheckColor(
    SRGBColor value,
    float r,
    float g,
    float b,
    float a
){
    ExpectNear(value.R, r, kFloatEpsilon);
    ExpectNear(value.G, g, kFloatEpsilon);
    ExpectNear(value.B, b, kFloatEpsilon);
    ExpectNear(value.A, a, kFloatEpsilon);
}
public static void CheckConstraints(
    BoxConstraints value,
    float min_width,
    float max_width,
    float min_height,
    float max_height
){
    if (double.IsInfinity(min_width))
    {
        Check.That(double.IsInfinity(value.MinWidth));
    }
    else
    {
        ExpectNear(value.MinWidth, min_width, kFloatEpsilon);
    }

    if (double.IsInfinity(max_width))
    {
        Check.That(double.IsInfinity(value.MaxWidth));
    }
    else
    {
        ExpectNear(value.MaxWidth, max_width, kFloatEpsilon);
    }

    if (double.IsInfinity(min_height))
    {
        Check.That(double.IsInfinity(value.MinHeight));
    }
    else
    {
        ExpectNear(value.MinHeight, min_height, kFloatEpsilon);
    }

    if (double.IsInfinity(max_height))
    {
        Check.That(double.IsInfinity(value.MaxHeight));
    }
    else
    {
        ExpectNear(value.MaxHeight, max_height, kFloatEpsilon);
    }
}
public static void CheckAlignment(
    Alignment value,
    float x,
    float y
){
    ExpectNear(value.X, x, kFloatEpsilon);
    ExpectNear(value.Y, y, kFloatEpsilon);
}
public static void CheckDirectionalAlignment(
    AlignmentDirectional value,
    float start,
    float y
){
    ExpectNear(value.Start, start, kFloatEpsilon);
    ExpectNear(value.Y, y, kFloatEpsilon);
}
public static void CheckMixedAlignment(
    AlignmentMixed value,
    float x,
    float start,
    float y
){
    ExpectNear(value.X, x, kFloatEpsilon);
    ExpectNear(value.Start, start, kFloatEpsilon);
    ExpectNear(value.Y, y, kFloatEpsilon);
}
public static void CheckInsets(
    EdgeInsets value,
    float left,
    float top,
    float right,
    float bottom
){
    ExpectNear(value.Left, left, kFloatEpsilon);
    ExpectNear(value.Top, top, kFloatEpsilon);
    ExpectNear(value.Right, right, kFloatEpsilon);
    ExpectNear(value.Bottom, bottom, kFloatEpsilon);
}
public static void CheckDirectionalInsets(
    EdgeInsetsDirectional value,
    float start,
    float top,
    float end,
    float bottom
){
    ExpectNear(value.Start, start, kFloatEpsilon);
    ExpectNear(value.Top, top, kFloatEpsilon);
    ExpectNear(value.End, end, kFloatEpsilon);
    ExpectNear(value.Bottom, bottom, kFloatEpsilon);
}
public static void CheckMixedInsets(
    EdgeInsetsMixed value,
    float left,
    float right,
    float start,
    float end,
    float top,
    float bottom
){
    ExpectNear(value.Left, left, kFloatEpsilon);
    ExpectNear(value.Right, right, kFloatEpsilon);
    ExpectNear(value.Start, start, kFloatEpsilon);
    ExpectNear(value.End, end, kFloatEpsilon);
    ExpectNear(value.Top, top, kFloatEpsilon);
    ExpectNear(value.Bottom, bottom, kFloatEpsilon);
}
public static void CheckRadius(Radius value, float x, float y){
    ExpectNear(value.X, x, kFloatEpsilon);
    ExpectNear(value.Y, y, kFloatEpsilon);
}
public static void CheckRrect(
    RRect value,
    float left,
    float top,
    float right,
    float bottom,
    Radius tl_radius,
    Radius tr_radius,
    Radius br_radius,
    Radius bl_radius
){
    ExpectNear(value.Left, left, kFloatEpsilon);
    ExpectNear(value.Top, top, kFloatEpsilon);
    ExpectNear(value.Right, right, kFloatEpsilon);
    ExpectNear(value.Bottom, bottom, kFloatEpsilon);
    Check.That(value.TlRadius == tl_radius);
    Check.That(value.TrRadius == tr_radius);
    Check.That(value.BrRadius == br_radius);
    Check.That(value.BlRadius == bl_radius);
}}
