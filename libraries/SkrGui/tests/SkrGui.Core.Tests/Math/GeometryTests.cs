using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
namespace SkrGui.Tests;
internal static class GeometryTests
{
[GuiTest("math/geometry_tests.cpp::gui/math/Offsetf factories and getters")]
public static void Case0(){

    var zero = Offsetf.Zero();
    CheckOffsetf(zero, 0.0f, 0.0f);

    var infinite = Offsetf.Infinite();
    Check.That(infinite.IsInfinite());
    Check.False(infinite.IsFinite());
    Check.False(new Offsetf(-float.PositiveInfinity, 0.0f).IsFinite());

    var radians = Offsetf.Radians(kPi * 0.5f, 2.0f);
    ExpectNear(radians.X, 0.0f, kFloatEpsilon);
    ExpectNear(radians.Y, 2.0f, kFloatEpsilon);

    Offsetf value = new Offsetf(3.0f, 4.0f);
    ExpectNear(value.Length(), 5.0f, kFloatEpsilon);
    ExpectNear(value.LengthSquared(), 25.0f, kFloatEpsilon);
    ExpectNear(value.Radians(), MathF.Atan2(4.0f, 3.0f), kFloatEpsilon);
    CheckOffsetf(value.Normalize(), 0.6f, 0.8f);
    CheckOffsetf(Offsetf.Zero().Normalize(), 0.0f, 0.0f);
    CheckOffsetf(Offsetf.Zero().Normalize(new Offsetf(1.0f, -2.0f)), 1.0f, -2.0f);
    CheckOffsetf(value.CwNormal(), -4.0f, 3.0f);
    CheckOffsetf(value.CcwNormal(), 4.0f, -3.0f);

}
[GuiTest("math/geometry_tests.cpp::gui/math/Offsetf arithmetic and scalar operators")]
public static void Case1(){

    Offsetf value = new Offsetf(3.0f, 4.0f);
    Offsetf rhs = new Offsetf(1.5f, -2.0f);

    CheckOffsetf(value + rhs, 4.5f, 2.0f);
    CheckOffsetf(value - rhs, 1.5f, 6.0f);
    CheckOffsetf(value * rhs, 4.5f, -8.0f);
    CheckOffsetf(value / new Offsetf(3.0f, 2.0f), 1.0f, 2.0f);
    CheckOffsetf(value % new Offsetf(2.0f, 3.0f), 1.0f, 1.0f);
    CheckOffsetf(-value, -3.0f, -4.0f);

    CheckOffsetf(value + 2.0f, 5.0f, 6.0f);
    CheckOffsetf(value - 2.0f, 1.0f, 2.0f);
    CheckOffsetf(value * 2.0f, 6.0f, 8.0f);
    CheckOffsetf(value / 2.0f, 1.5f, 2.0f);
    CheckOffsetf(value % 2.0f, 1.0f, 0.0f);

    CheckOffsetf(2.0f + value, 5.0f, 6.0f);
    CheckOffsetf(2.0f - value, -1.0f, -2.0f);
    CheckOffsetf(2.0f * value, 6.0f, 8.0f);
    CheckOffsetf(12.0f / value, 4.0f, 3.0f);
    CheckOffsetf(8.0f % value, 2.0f, 0.0f);

    value += rhs;
    CheckOffsetf(value, 4.5f, 2.0f);
    value -= new Offsetf(0.5f, 1.0f);
    CheckOffsetf(value, 4.0f, 1.0f);
    value *= 2.0f;
    CheckOffsetf(value, 8.0f, 2.0f);
    value /= 2.0f;
    CheckOffsetf(value, 4.0f, 1.0f);
    value %= 3.0f;
    CheckOffsetf(value, 1.0f, 1.0f);

    var lerped = Offsetf.Lerp(new Offsetf(0.0f, 10.0f), new Offsetf(10.0f, 20.0f), 0.25f);
    CheckOffsetf(lerped, 2.5f, 12.5f);

}
[GuiTest("math/geometry_tests.cpp::gui/math/Offseti arithmetic and conversions")]
public static void Case2(){

    Offseti value = new Offseti(7, 9);
    Offseti rhs = new Offseti(2, 4);

    Check.That(Offseti.Zero() == new Offseti(0, 0));
    Check.That(value == new Offseti(7, 9));
    Check.That(value != rhs);
    Check.That(-value == new Offseti(-7, -9));

    Check.That(value + rhs == new Offseti(9, 13));
    Check.That(value - rhs == new Offseti(5, 5));
    Check.That(value * rhs == new Offseti(14, 36));
    Check.That(value / rhs == new Offseti(3, 2));
    Check.That(value % rhs == new Offseti(1, 1));

    value += 3;
    Check.That(value == new Offseti(10, 12));
    value -= 2;
    Check.That(value == new Offseti(8, 10));
    value *= 2;
    Check.That(value == new Offseti(16, 20));
    value /= 4;
    Check.That(value == new Offseti(4, 5));
    value %= 3;
    Check.That(value == new Offseti(1, 2));

    Check.That(new Offseti(1, 2) + 4 == new Offseti(5, 6));
    Check.That(4 + new Offseti(1, 2) == new Offseti(5, 6));
    Check.That(10 - new Offseti(1, 2) == new Offseti(9, 8));

    Offsetf as_float = (Offsetf)(new Offseti(3, 4));
    CheckOffsetf(as_float, 3.0f, 4.0f);

    Offseti as_int = (Offseti)(new Offsetf(3.9f, 4.2f));
    Check.That(as_int == new Offseti(3, 4));

}
[GuiTest("math/geometry_tests.cpp::gui/math/Sizef factories predicates and helpers")]
public static void Case3(){

    Check.That(Sizef.Zero() == new Sizef(0.0f, 0.0f));
    Check.That(Sizef.Square(3.0f) == new Sizef(3.0f, 3.0f));
    Check.That(Sizef.Radius(2.0f) == new Sizef(4.0f, 4.0f));

    var infinite = Sizef.Infinite();
    Check.That(infinite.IsInfinite());
    Check.False(infinite.IsFinite());
    Check.False(new Sizef(-float.PositiveInfinity, 1.0f).IsFinite());

    Check.That(Sizef.InfiniteWidth(5.0f).IsInfinite());
    Check.That(Sizef.InfiniteHeight(5.0f).IsInfinite());

    Sizef value = new Sizef(6.0f, 4.0f);
    Check.False(value.IsInfinite());
    Check.That(value.IsFinite());
    Check.False(value.IsEmpty());
    ExpectNear(value.AspectRatio(), 1.5f, kFloatEpsilon);
    ExpectNear(value.Area(), 24.0f, kFloatEpsilon);
    ExpectNear(value.ShortestSide(), 4.0f, kFloatEpsilon);
    ExpectNear(value.LongestSide(), 6.0f, kFloatEpsilon);
    Check.That(value.Flipped() == new Sizef(4.0f, 6.0f));

    Check.That(new Sizef(-2.0f, 3.0f).IsEmpty());
    Check.That(double.IsInfinity(new Sizef(6.0f, 0.0f).AspectRatio()));

}
[GuiTest("math/geometry_tests.cpp::gui/math/Sizef offsets arithmetic and conversions")]
public static void Case4(){

    Sizef value = new Sizef(10.0f, 6.0f);
    Offsetf origin = new Offsetf(1.0f, 2.0f);

    Check.That(value.Contains(new Offsetf(10.0f, 6.0f)));
    Check.False(value.Contains(new Offsetf(10.1f, 6.0f)));

    CheckOffsetf(value.TopLeft(origin), 1.0f, 2.0f);
    CheckOffsetf(value.TopCenter(origin), 6.0f, 2.0f);
    CheckOffsetf(value.TopRight(origin), 11.0f, 2.0f);
    CheckOffsetf(value.CenterLeft(origin), 1.0f, 5.0f);
    CheckOffsetf(value.Center(origin), 6.0f, 5.0f);
    CheckOffsetf(value.CenterRight(origin), 11.0f, 5.0f);
    CheckOffsetf(value.BottomLeft(origin), 1.0f, 8.0f);
    CheckOffsetf(value.BottomCenter(origin), 6.0f, 8.0f);
    CheckOffsetf(value.BottomRight(origin), 11.0f, 8.0f);

    Check.That(value + new Sizef(2.0f, 3.0f) == new Sizef(12.0f, 9.0f));
    Check.That(value - new Sizef(2.0f, 1.0f) == new Sizef(8.0f, 5.0f));
    Check.That(value * new Sizef(2.0f, 3.0f) == new Sizef(20.0f, 18.0f));
    Check.That(value / new Sizef(2.0f, 3.0f) == new Sizef(5.0f, 2.0f));

    var modded = value % new Sizef(4.0f, 4.0f);
    CheckSizef(modded, 2.0f, 2.0f);

    Check.That(Sizef.Min(new Sizef(3.0f, 8.0f), new Sizef(5.0f, 4.0f)) == new Sizef(3.0f, 4.0f));
    Check.That(Sizef.Max(new Sizef(3.0f, 8.0f), new Sizef(5.0f, 4.0f)) == new Sizef(5.0f, 8.0f));
    CheckSizef(Sizef.Lerp(new Sizef(0.0f, 10.0f), new Sizef(10.0f, 20.0f), 0.5f), 5.0f, 15.0f);

    Sizei as_int = (Sizei)(new Sizef(10.8f, 6.2f));
    Check.That(as_int == new Sizei(10, 6));

}
[GuiTest("math/geometry_tests.cpp::gui/math/Size semantics keep absolute area and inclusive local bounds")]
public static void Case5(){

    Sizef signed_size = new Sizef(-6.0f, 4.0f);
    Check.That(signed_size.IsEmpty());
    ExpectNear(signed_size.Area(), 24.0f, kFloatEpsilon);

    var negative_ratio = new Sizef(-6.0f, 0.0f).AspectRatio();
    Check.That(double.IsInfinity(negative_ratio));
    Check.That(negative_ratio < 0.0f);

    Sizei grid = new Sizei(4, 3);
    Check.That(grid.Contains(new Offseti(4, 3)));
    Check.False(grid.Contains(new Offseti(-1, 0)));

}
[GuiTest("math/geometry_tests.cpp::gui/math/Sizei helpers arithmetic and conversions")]
public static void Case6(){

    Check.That(Sizei.Zero() == new Sizei(0, 0));
    Check.That(Sizei.Square(4) == new Sizei(4, 4));
    Check.That(Sizei.Radius(3) == new Sizei(6, 6));

    Sizei value = new Sizei(9, 5);
    Check.False(value.IsEmpty());
    EqualNumeric(value.Area(), 45);
    EqualNumeric(value.ShortestSide(), 5);
    EqualNumeric(value.LongestSide(), 9);
    Check.That(value.Flipped() == new Sizei(5, 9));
    Check.That(new Sizei(-1, 5).IsEmpty());

    Offseti origin = new Offseti(2, 3);
    Check.That(value.Contains(new Offseti(9, 5)));
    Check.False(value.Contains(new Offseti(10, 5)));
    Check.That(value.Center(origin) == new Offseti(6, 5));
    Check.That(value.BottomRight(origin) == new Offseti(11, 8));

    Check.That(value + new Sizei(1, 2) == new Sizei(10, 7));
    Check.That(value - new Sizei(1, 1) == new Sizei(8, 4));
    Check.That(value * new Sizei(2, 3) == new Sizei(18, 15));
    Check.That(value / new Sizei(3, 5) == new Sizei(3, 1));
    Check.That(value % new Sizei(4, 3) == new Sizei(1, 2));

    Check.That(Sizei.Min(new Sizei(3, 7), new Sizei(4, 5)) == new Sizei(3, 5));
    Check.That(Sizei.Max(new Sizei(3, 7), new Sizei(4, 5)) == new Sizei(4, 7));

    Sizef as_float = (Sizef)(value);
    CheckSizef(as_float, 9.0f, 5.0f);

}
[GuiTest("math/geometry_tests.cpp::gui/math/Rectf factories and predicates")]
public static void Case7(){

    Check.That(Rectf.Zero() == new Rectf(0.0f, 0.0f, 0.0f, 0.0f));
    Check.That(Rectf.Largest() == new Rectf(-1.0e9f, -1.0e9f, 1.0e9f, 1.0e9f));
    Check.That(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f) == new Rectf(1.0f, 2.0f, 4.0f, 6.0f));
    CheckRectf(Rectf.Circle(new Offsetf(5.0f, 6.0f), 2.0f), 3.0f, 4.0f, 7.0f, 8.0f);
    CheckRectf(Rectf.Center(new Offsetf(10.0f, 20.0f), new Sizef(6.0f, 4.0f)), 7.0f, 18.0f, 13.0f, 22.0f);
    CheckRectf(Rectf.OffsetSize(new Offsetf(2.0f, 3.0f), new Sizef(4.0f, 5.0f)), 2.0f, 3.0f, 6.0f, 8.0f);
    CheckRectf(Rectf.Points(new Offsetf(5.0f, 1.0f), new Offsetf(3.0f, 7.0f)), 3.0f, 1.0f, 5.0f, 7.0f);

    Rectf rect = Rectf.LTWH(1.0f, 2.0f, 6.0f, 4.0f);
    Check.False(rect.IsInfinite());
    Check.That(rect.IsFinite());
    Check.False(new Rectf(-float.PositiveInfinity, 0.0f, 1.0f, 1.0f).IsFinite());
    Check.False(rect.HasNan());
    Check.False(rect.IsEmpty());
    Check.False(rect.IsPoint());
    ExpectNear(rect.Width(), 6.0f, kFloatEpsilon);
    ExpectNear(rect.Height(), 4.0f, kFloatEpsilon);
    Check.That(rect.Size() == new Sizef(6.0f, 4.0f));
    CheckOffsetf(rect.Center(), 4.0f, 4.0f);
    CheckOffsetf(rect.BottomRight(), 7.0f, 6.0f);

    Rectf nan_rect = new Rectf(
        float.NaN,
        0.0f,
        1.0f,
        1.0f
    );
    Check.That(nan_rect.HasNan());

}
[GuiTest("math/geometry_tests.cpp::gui/math/Rectf transforms contains overlaps and lerp")]
public static void Case8(){

    Rectf rect = Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f);

    CheckRectf(rect.Shift(new Offsetf(2.0f, -1.0f)), 3.0f, 1.0f, 6.0f, 5.0f);
    CheckRectf(rect.Hold(new Offsetf(-2.0f, 10.0f)), -2.0f, 2.0f, 4.0f, 10.0f);
    CheckRectf(rect.Inflate(2.0f), -1.0f, 0.0f, 6.0f, 8.0f);
    CheckRectf(rect.Deflate(1.0f), 2.0f, 3.0f, 3.0f, 5.0f);
    CheckRectf(rect.Intersect(Rectf.LTWH(2.0f, 4.0f, 3.0f, 3.0f)), 2.0f, 4.0f, 4.0f, 6.0f);
    CheckRectf(rect.Unite(Rectf.LTWH(-1.0f, 1.0f, 2.0f, 2.0f)), -1.0f, 1.0f, 4.0f, 6.0f);

    Check.That(rect.Overlaps(Rectf.LTWH(3.5f, 4.0f, 10.0f, 10.0f)));
    Check.False(rect.Overlaps(Rectf.LTWH(4.0f, 0.0f, 1.0f, 1.0f)));
    Check.That(rect.Contains(new Offsetf(1.0f, 2.0f)));
    Check.False(rect.Contains(new Offsetf(4.0f, 6.0f)));

    Rectf? none = null;
    Rectf? a = Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f);
    Rectf? b = Rectf.LTWH(10.0f, 10.0f, 10.0f, 10.0f);

    var none_to_value = Rectf.Lerp(none, b, 0.5f);
    Check.That(none_to_value.HasValue);
    CheckRectf(none_to_value.Value, 5.0f, 5.0f, 10.0f, 10.0f);

    var value_to_none = Rectf.Lerp(a, none, 0.25f);
    Check.That(value_to_none.HasValue);
    CheckRectf(value_to_none.Value, 0.0f, 0.0f, 7.5f, 7.5f);

    var value_to_value = Rectf.Lerp(a, b, 0.5f);
    Check.That(value_to_value.HasValue);
    CheckRectf(value_to_value.Value, 5.0f, 5.0f, 15.0f, 15.0f);

    Check.False(Rectf.Lerp(none, none, 0.5f).HasValue);

}
[GuiTest("math/geometry_tests.cpp::gui/math/Rect containment uses half open edges")]
public static void Case9(){

    Rectf rect = Rectf.LTWH(1.0f, 2.0f, 6.0f, 4.0f);
    Check.That(rect.Contains(new Offsetf(1.0f, 2.0f)));
    Check.That(rect.Contains(new Offsetf(6.999f, 5.999f)));
    Check.False(rect.Contains(new Offsetf(7.0f, 3.0f)));
    Check.False(rect.Contains(new Offsetf(4.0f, 6.0f)));

    Rectf disjoint = rect.Intersect(Rectf.LTWH(10.0f, 10.0f, 1.0f, 1.0f));
    Check.That(disjoint.IsEmpty());

    Recti rect_i = Recti.LTWH(1, 2, 6, 4);
    Check.That(rect_i.Contains(new Offseti(1, 2)));
    Check.False(rect_i.Contains(new Offseti(7, 3)));
    Check.False(rect_i.Contains(new Offseti(4, 6)));
    Check.That(rect_i.Intersect(Recti.LTWH(10, 10, 1, 1)).IsEmpty());

}
[GuiTest("math/geometry_tests.cpp::gui/math/Recti transforms and conversions")]
public static void Case10(){

    Check.That(Recti.Zero() == new Recti(0, 0, 0, 0));
    Check.That(Recti.Largest() == new Recti(-1000000000, -1000000000, 1000000000, 1000000000));
    Check.That(Recti.LTWH(1, 2, 3, 4) == new Recti(1, 2, 4, 6));
    Check.That(Recti.Circle(new Offseti(5, 6), 2) == new Recti(3, 4, 7, 8));
    Check.That(Recti.Points(new Offseti(5, 1), new Offseti(3, 7)) == new Recti(3, 1, 5, 7));

    Recti rect = Recti.LTWH(1, 2, 6, 4);
    Check.False(rect.IsEmpty());
    Check.False(rect.IsPoint());
    EqualNumeric(rect.Width(), 6);
    EqualNumeric(rect.Height(), 4);
    Check.That(rect.Size() == new Sizei(6, 4));
    Check.That(rect.Center() == new Offseti(4, 4));

    Check.That(rect.Shift(new Offseti(2, -1)) == new Recti(3, 1, 9, 5));
    Check.That(rect.Hold(new Offseti(-2, 10)) == new Recti(-2, 2, 7, 10));
    Check.That(rect.Inflate(2) == new Recti(-1, 0, 9, 8));
    Check.That(rect.Deflate(1) == new Recti(2, 3, 6, 5));
    Check.That(rect.Intersect(Recti.LTWH(2, 4, 3, 3)) == new Recti(2, 4, 5, 6));
    Check.That(rect.Unite(Recti.LTWH(-1, 1, 2, 2)) == new Recti(-1, 1, 7, 6));

    Check.That(rect.Overlaps(Recti.LTWH(3, 4, 10, 10)));
    Check.False(rect.Overlaps(Recti.LTWH(7, 0, 1, 1)));
    Check.That(rect.Contains(new Offseti(1, 2)));
    Check.False(rect.Contains(new Offseti(7, 6)));

    Rectf as_float = (Rectf)(rect);
    CheckRectf(as_float, 1.0f, 2.0f, 7.0f, 6.0f);

}
[GuiTest("math/geometry_tests.cpp::gui/math/Radius arithmetic and lerp")]
public static void Case11(){

    Check.That(Radius.Zero() == new Radius(0.0f, 0.0f));
    Check.That(Radius.Circular(3.0f) == new Radius(3.0f, 3.0f));
    Check.That(Radius.Elliptical(2.0f, 4.0f) == new Radius(2.0f, 4.0f));

    Radius value = new Radius(2.0f, 4.0f);
    Check.False(value.IsZero());
    Check.That(Radius.Zero().IsZero());
    CheckRadius(-value, -2.0f, -4.0f);
    CheckRadius(value + new Radius(1.0f, 3.0f), 3.0f, 7.0f);
    CheckRadius(value - new Radius(1.0f, 3.0f), 1.0f, 1.0f);
    CheckRadius(value * 2.0f, 4.0f, 8.0f);
    CheckRadius(2.0f * value, 4.0f, 8.0f);
    CheckRadius(value / 2.0f, 1.0f, 2.0f);

    Radius accum = value;
    accum += new Radius(1.0f, 1.0f);
    CheckRadius(accum, 3.0f, 5.0f);
    accum -= new Radius(1.0f, 2.0f);
    CheckRadius(accum, 2.0f, 3.0f);
    accum *= 2.0f;
    CheckRadius(accum, 4.0f, 6.0f);
    accum /= 2.0f;
    CheckRadius(accum, 2.0f, 3.0f);

    CheckRadius(Radius.Lerp(Radius.Zero(), new Radius(4.0f, 8.0f), 0.25f), 1.0f, 2.0f);

}
[GuiTest("math/geometry_tests.cpp::gui/math/RRect factories transforms contains and lerp")]
public static void Case12(){

    Check.That(RRect.Zero() == new RRect(0.0f, 0.0f, 0.0f, 0.0f));
    Check.That(RRect.Zero().IsFinite());
    Check.False(RRect.Zero().HasNan());

    var uniform = RRect.FromLTRBXY(10.0f, 20.0f, 40.0f, 60.0f, 5.0f, 7.0f);
    CheckRrect(
        uniform,
        10.0f,
        20.0f,
        40.0f,
        60.0f,
        new Radius(5.0f, 7.0f),
        new Radius(5.0f, 7.0f),
        new Radius(5.0f, 7.0f),
        new Radius(5.0f, 7.0f)
    );
    Check.False(uniform.IsEmpty());
    Check.That(uniform.IsFinite());
    Check.False(new RRect(-float.PositiveInfinity, 0.0f, 1.0f, 1.0f).IsFinite());
    Check.False(uniform.HasNan());
    ExpectNear(uniform.Width(), 30.0f, kFloatEpsilon);
    ExpectNear(uniform.Height(), 40.0f, kFloatEpsilon);
    CheckSizef(uniform.Size(), 30.0f, 40.0f);
    CheckOffsetf(uniform.Center(), 25.0f, 40.0f);
    CheckRectf(uniform.Rect(), 10.0f, 20.0f, 40.0f, 60.0f);
    CheckRectf(uniform.OuterRect(), 10.0f, 20.0f, 40.0f, 60.0f);
    CheckRectf(uniform.SafeInnerRect(), 11.4644661f, 22.0502524f, 38.5355339f, 57.9497476f);
    CheckRectf(uniform.MiddleRect(), 15.0f, 27.0f, 35.0f, 53.0f);
    CheckRectf(uniform.WideMiddleRect(), 10.0f, 27.0f, 40.0f, 53.0f);
    CheckRectf(uniform.TallMiddleRect(), 15.0f, 20.0f, 35.0f, 60.0f);

    var from_rect_xy = RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 20.0f, 10.0f), 3.0f, 4.0f);
    CheckRrect(
        from_rect_xy,
        0.0f,
        0.0f,
        20.0f,
        10.0f,
        new Radius(3.0f, 4.0f),
        new Radius(3.0f, 4.0f),
        new Radius(3.0f, 4.0f),
        new Radius(3.0f, 4.0f)
    );

    var from_rect_radius = RRect.FromRectRadius(
        Rectf.LTWH(5.0f, 6.0f, 10.0f, 12.0f),
        new Radius(2.0f, 3.0f)
    );
    CheckRrect(
        from_rect_radius,
        5.0f,
        6.0f,
        15.0f,
        18.0f,
        new Radius(2.0f, 3.0f),
        new Radius(2.0f, 3.0f),
        new Radius(2.0f, 3.0f),
        new Radius(2.0f, 3.0f)
    );

    CheckRrect(
        uniform.Shift(new Offsetf(2.0f, -3.0f)),
        12.0f,
        17.0f,
        42.0f,
        57.0f,
        new Radius(5.0f, 7.0f),
        new Radius(5.0f, 7.0f),
        new Radius(5.0f, 7.0f),
        new Radius(5.0f, 7.0f)
    );
    CheckRrect(
        uniform.Inflate(2.0f),
        8.0f,
        18.0f,
        42.0f,
        62.0f,
        new Radius(7.0f, 9.0f),
        new Radius(7.0f, 9.0f),
        new Radius(7.0f, 9.0f),
        new Radius(7.0f, 9.0f)
    );
    CheckRrect(
        uniform.Deflate(3.0f),
        13.0f,
        23.0f,
        37.0f,
        57.0f,
        new Radius(2.0f, 4.0f),
        new Radius(2.0f, 4.0f),
        new Radius(2.0f, 4.0f),
        new Radius(2.0f, 4.0f)
    );

    Check.That(uniform.Contains(new Offsetf(25.0f, 40.0f)));
    Check.That(uniform.Contains(new Offsetf(12.0f, 24.0f)));
    Check.False(uniform.Contains(new Offsetf(10.5f, 20.5f)));
    Check.False(uniform.Contains(new Offsetf(40.0f, 30.0f)));

    RRect oversized = RRect.FromLTRBXY(0.0f, 0.0f, 10.0f, 10.0f, 8.0f, 8.0f);
    var scaled = oversized.ScaleRadii();
    CheckRrect(
        scaled,
        0.0f,
        0.0f,
        10.0f,
        10.0f,
        new Radius(5.0f, 5.0f),
        new Radius(5.0f, 5.0f),
        new Radius(5.0f, 5.0f),
        new Radius(5.0f, 5.0f)
    );
    Check.That(oversized.Contains(new Offsetf(5.0f, 5.0f)));
    Check.False(oversized.Contains(new Offsetf(0.5f, 0.5f)));

    var lerped = RRect.Lerp(
        RRect.FromRectRadius(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f), new Radius(0.0f, 0.0f)),
        RRect.FromRectRadius(Rectf.LTWH(10.0f, 20.0f, 30.0f, 40.0f), new Radius(4.0f, 8.0f)),
        0.5f
    );
    CheckRrect(
        lerped,
        5.0f,
        10.0f,
        25.0f,
        35.0f,
        new Radius(2.0f, 4.0f),
        new Radius(2.0f, 4.0f),
        new Radius(2.0f, 4.0f),
        new Radius(2.0f, 4.0f)
    );

    RRect nan_rrect = new RRect(
        float.NaN,
        0.0f,
        1.0f,
        1.0f,
        Radius.Zero(),
        Radius.Zero(),
        Radius.Zero(),
        Radius.Zero()
    );
    Check.That(nan_rrect.HasNan());
    Check.False(nan_rrect.IsFinite());

}
[GuiTest("math/geometry_tests.cpp::gui/math/RRect oversized radii keep helper rects empty before scaling")]
public static void Case13(){

    RRect oversized = RRect.FromLTRBXY(0.0f, 0.0f, 10.0f, 10.0f, 8.0f, 8.0f);
    Check.That(oversized.MiddleRect().IsEmpty());
    Check.That(oversized.WideMiddleRect().IsEmpty());
    Check.That(oversized.TallMiddleRect().IsEmpty());

    RRect scaled = oversized.ScaleRadii();
    Check.That(scaled.MiddleRect().IsPoint());
    Check.That(oversized.Contains(new Offsetf(5.0f, 5.0f)));
    Check.False(oversized.Contains(new Offsetf(10.0f, 5.0f)));
    Check.False(oversized.Contains(new Offsetf(5.0f, 10.0f)));

}
}
