using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
namespace SkrGui.Tests;
internal static class LayoutTests
{
[GuiTest("math/layout_tests.cpp::gui/math/BoxFit apply matches Flutter semantics")]
public static void Case0(){

    Sizef input_size = new Sizef(20.0f, 10.0f);
    Sizef output_size = new Sizef(100.0f, 100.0f);

    FittedSizes sizes = GuiMath.ApplyBoxFit(EBoxFit.Fill, input_size, output_size);
    CheckSizef(sizes.Source, 20.0f, 10.0f);
    CheckSizef(sizes.Destination, 100.0f, 100.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.Contain, input_size, output_size);
    CheckSizef(sizes.Source, 20.0f, 10.0f);
    CheckSizef(sizes.Destination, 100.0f, 50.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.Cover, input_size, output_size);
    CheckSizef(sizes.Source, 10.0f, 10.0f);
    CheckSizef(sizes.Destination, 100.0f, 100.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.FitWidth, input_size, output_size);
    CheckSizef(sizes.Source, 20.0f, 10.0f);
    CheckSizef(sizes.Destination, 100.0f, 50.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.FitHeight, input_size, output_size);
    CheckSizef(sizes.Source, 10.0f, 10.0f);
    CheckSizef(sizes.Destination, 100.0f, 100.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.None, input_size, output_size);
    CheckSizef(sizes.Source, 20.0f, 10.0f);
    CheckSizef(sizes.Destination, 20.0f, 10.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.ScaleDown, input_size, output_size);
    CheckSizef(sizes.Source, 20.0f, 10.0f);
    CheckSizef(sizes.Destination, 20.0f, 10.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.ScaleDown, input_size, new Sizef(10.0f, 10.0f));
    CheckSizef(sizes.Source, 20.0f, 10.0f);
    CheckSizef(sizes.Destination, 10.0f, 5.0f);

    sizes = GuiMath.ApplyBoxFit(EBoxFit.Contain, Sizef.Zero(), output_size);
    CheckSizef(sizes.Source, 0.0f, 0.0f);
    CheckSizef(sizes.Destination, 0.0f, 0.0f);

}
[GuiTest("math/layout_tests.cpp::gui/math/Alignment factories math and positioning")]
public static void Case1(){

    Check.That(Alignment.TopLeft() == new Alignment(-1.0f, -1.0f));
    Check.That(Alignment.TopCenter() == new Alignment(0.0f, -1.0f));
    Check.That(Alignment.TopRight() == new Alignment(1.0f, -1.0f));
    Check.That(Alignment.CenterLeft() == new Alignment(-1.0f, 0.0f));
    Check.That(Alignment.Center() == new Alignment(0.0f, 0.0f));
    Check.That(Alignment.CenterRight() == new Alignment(1.0f, 0.0f));
    Check.That(Alignment.BottomLeft() == new Alignment(-1.0f, 1.0f));
    Check.That(Alignment.BottomCenter() == new Alignment(0.0f, 1.0f));
    Check.That(Alignment.BottomRight() == new Alignment(1.0f, 1.0f));

    Alignment value = new Alignment(0.25f, 0.75f);
    ExpectNear(value.Horizontal(), 0.25f, kFloatEpsilon);
    ExpectNear(value.Vertical(), 0.75f, kFloatEpsilon);
    Check.That(-value == new Alignment(-0.25f, -0.75f));
    Check.That(value + new Alignment(0.25f, 0.25f) == new Alignment(0.5f, 1.0f));
    Check.That(value - new Alignment(0.25f, 0.25f) == new Alignment(0.0f, 0.5f));
    Check.That(value * new Alignment(2.0f, 2.0f) == new Alignment(0.5f, 1.5f));
    Check.That(value / new Alignment(0.5f, 0.25f) == new Alignment(0.5f, 3.0f));
    Check.That(value % new Alignment(0.2f, 0.5f) == new Alignment(CppMath.Fmod(0.25f, 0.2f), CppMath.Fmod(0.75f, 0.5f)));
    Check.That(value * 2.0f == new Alignment(0.5f, 1.5f));
    Check.That(2.0f * value == new Alignment(0.5f, 1.5f));
    Check.That(value / 2.0f == new Alignment(0.125f, 0.375f));
    Check.That(value % 0.5f == new Alignment(CppMath.Fmod(0.25f, 0.5f), CppMath.Fmod(0.75f, 0.5f)));
    CheckAlignment(value.CopyWith(null, 0.25f), 0.25f, 0.25f);
    CheckMixedAlignment(value.Add(new AlignmentDirectional(-0.75f, 0.25f)), 0.25f, -0.75f, 1.0f);

    CheckOffsetf(value.AlongOffset(new Offsetf(8.0f, 12.0f)), 5.0f, 10.5f);
    CheckOffsetf(value.AlongSize(new Sizef(20.0f, 8.0f)), 12.5f, 7.0f);
    CheckOffsetf(value.AlongRect(Rectf.LTWH(10.0f, 20.0f, 16.0f, 8.0f)), 20.0f, 27.0f);
    CheckOffsetf(value.WithinRect(Rectf.LTWH(10.0f, 20.0f, 16.0f, 8.0f)), 20.0f, 27.0f);

    Rectf inscribed = Alignment.Center().Inscribe(
        new Sizef(10.0f, 4.0f),
        Rectf.LTWH(0.0f, 0.0f, 30.0f, 10.0f)
    );
    CheckRectf(inscribed, 10.0f, 3.0f, 20.0f, 7.0f);

    CheckRectf(
        Alignment.TopLeft().Inscribe(new Sizef(10.0f, 4.0f), Rectf.LTWH(0.0f, 0.0f, 30.0f, 10.0f)),
        0.0f,
        0.0f,
        10.0f,
        4.0f
    );
    CheckRectf(
        Alignment.BottomRight().Inscribe(new Sizef(10.0f, 4.0f), Rectf.LTWH(0.0f, 0.0f, 30.0f, 10.0f)),
        20.0f,
        6.0f,
        30.0f,
        10.0f
    );
    Check.That(value.Resolve(ETextDirection.LTR) == value);
    Check.That(value.Resolve(ETextDirection.RTL) == value);
    Check.That(Alignment.Lerp(Alignment.Center(), Alignment.BottomRight(), 0.25f) == new Alignment(0.25f, 0.25f));
    Check.That(Alignment.Center().Lerp(Alignment.BottomRight(), 0.25f) == new Alignment(0.25f, 0.25f));

}
[GuiTest("math/layout_tests.cpp::gui/math/AlignmentDirectional factories arithmetic and resolve")]
public static void Case2(){

    Check.That(AlignmentDirectional.TopStart() == new AlignmentDirectional(-1.0f, -1.0f));
    Check.That(AlignmentDirectional.TopCenter() == new AlignmentDirectional(0.0f, -1.0f));
    Check.That(AlignmentDirectional.TopEnd() == new AlignmentDirectional(1.0f, -1.0f));
    Check.That(AlignmentDirectional.CenterStart() == new AlignmentDirectional(-1.0f, 0.0f));
    Check.That(AlignmentDirectional.Center() == new AlignmentDirectional(0.0f, 0.0f));
    Check.That(AlignmentDirectional.CenterEnd() == new AlignmentDirectional(1.0f, 0.0f));
    Check.That(AlignmentDirectional.BottomStart() == new AlignmentDirectional(-1.0f, 1.0f));
    Check.That(AlignmentDirectional.BottomCenter() == new AlignmentDirectional(0.0f, 1.0f));
    Check.That(AlignmentDirectional.BottomEnd() == new AlignmentDirectional(1.0f, 1.0f));

    AlignmentDirectional value = new AlignmentDirectional(-0.25f, 0.75f);
    ExpectNear(value.Horizontal(), -0.25f, kFloatEpsilon);
    ExpectNear(value.Vertical(), 0.75f, kFloatEpsilon);
    CheckDirectionalAlignment(-value, 0.25f, -0.75f);
    CheckDirectionalAlignment(value + new AlignmentDirectional(0.5f, 0.25f), 0.25f, 1.0f);
    CheckDirectionalAlignment(value - new AlignmentDirectional(0.5f, 0.25f), -0.75f, 0.5f);
    CheckDirectionalAlignment(value * new AlignmentDirectional(2.0f, 2.0f), -0.5f, 1.5f);
    CheckDirectionalAlignment(value / new AlignmentDirectional(0.5f, 0.25f), -0.5f, 3.0f);
    CheckDirectionalAlignment(
        value % new AlignmentDirectional(0.2f, 0.5f),
        CppMath.Fmod(-0.25f, 0.2f),
        CppMath.Fmod(0.75f, 0.5f)
    );
    CheckDirectionalAlignment(value * 2.0f, -0.5f, 1.5f);
    CheckDirectionalAlignment(2.0f * value, -0.5f, 1.5f);
    CheckDirectionalAlignment(value / 2.0f, -0.125f, 0.375f);
    CheckDirectionalAlignment(value % 0.5f, CppMath.Fmod(-0.25f, 0.5f), CppMath.Fmod(0.75f, 0.5f));
    CheckDirectionalAlignment(value.CopyWith(0.5f, null), 0.5f, 0.75f);
    CheckMixedAlignment(value.Add(new Alignment(0.25f, 0.5f)), 0.25f, -0.25f, 1.25f);

    CheckAlignment(value.Resolve(ETextDirection.LTR), -0.25f, 0.75f);
    CheckAlignment(value.Resolve(ETextDirection.RTL), 0.25f, 0.75f);
    CheckDirectionalAlignment(AlignmentDirectional.Lerp(AlignmentDirectional.Center(), AlignmentDirectional.BottomEnd(), 0.25f), 0.25f, 0.25f);

}
[GuiTest("math/layout_tests.cpp::gui/math/AlignmentMixed arithmetic and resolve")]
public static void Case3(){

    Check.That(AlignmentMixed.Zero() == new AlignmentMixed(0.0f, 0.0f, 0.0f));
    Check.That(AlignmentMixed.FromXY(0.25f, 0.5f) == new AlignmentMixed(0.25f, 0.0f, 0.5f));
    Check.That(AlignmentMixed.FromStartY(-0.75f, 0.25f) == new AlignmentMixed(0.0f, -0.75f, 0.25f));

    AlignmentMixed physical = new Alignment(0.25f, 0.5f);
    AlignmentMixed directional = new AlignmentDirectional(-0.75f, 0.25f);
    Check.That(physical == AlignmentMixed.FromXY(0.25f, 0.5f));
    Check.That(directional == AlignmentMixed.FromStartY(-0.75f, 0.25f));

    AlignmentMixed value = AlignmentMixed.FromXStartY(0.25f, -0.75f, 0.5f);
    ExpectNear(value.Horizontal(), -0.5f, kFloatEpsilon);
    ExpectNear(value.Vertical(), 0.5f, kFloatEpsilon);
    CheckMixedAlignment(-value, -0.25f, 0.75f, -0.5f);
    CheckMixedAlignment(value + AlignmentMixed.FromXStartY(0.25f, 0.5f, 0.25f), 0.5f, -0.25f, 0.75f);
    CheckMixedAlignment(value - AlignmentMixed.FromXStartY(0.25f, 0.5f, 0.25f), 0.0f, -1.25f, 0.25f);
    CheckMixedAlignment(value * 2.0f, 0.5f, -1.5f, 1.0f);
    CheckMixedAlignment(value / 2.0f, 0.125f, -0.375f, 0.25f);
    CheckMixedAlignment(value % 0.5f, CppMath.Fmod(0.25f, 0.5f), CppMath.Fmod(-0.75f, 0.5f), CppMath.Fmod(0.5f, 0.5f));
    CheckMixedAlignment(value.CopyWith(null, 0.25f, null), 0.25f, 0.25f, 0.5f);
    CheckMixedAlignment(value.Add(new Alignment(0.25f, 0.5f)), 0.5f, -0.75f, 1.0f);
    CheckMixedAlignment(value.Add(new AlignmentDirectional(0.5f, 0.25f)), 0.25f, -0.25f, 0.75f);
    CheckAlignment(value.Resolve(ETextDirection.LTR), -0.5f, 0.5f);
    CheckAlignment(value.Resolve(ETextDirection.RTL), 1.0f, 0.5f);

    AlignmentMixed mixed_from_add = new Alignment(0.25f, 0.5f) + new AlignmentDirectional(-0.75f, 0.25f);
    CheckMixedAlignment(mixed_from_add, 0.25f, -0.75f, 0.75f);
    CheckAlignment(mixed_from_add.Resolve(ETextDirection.LTR), -0.5f, 0.75f);
    CheckAlignment(mixed_from_add.Resolve(ETextDirection.RTL), 1.0f, 0.75f);

    AlignmentMixed mixed_from_sub = new AlignmentDirectional(-0.75f, 0.25f) - new Alignment(0.25f, 0.5f);
    CheckMixedAlignment(mixed_from_sub, -0.25f, -0.75f, -0.25f);
    CheckAlignment(mixed_from_sub.Resolve(ETextDirection.LTR), -1.0f, -0.25f);
    CheckAlignment(mixed_from_sub.Resolve(ETextDirection.RTL), 0.5f, -0.25f);

    CheckMixedAlignment(
        AlignmentMixed.Lerp(AlignmentMixed.Zero(), AlignmentMixed.FromXStartY(1.0f, -1.0f, 1.0f), 0.25f),
        0.25f,
        -0.25f,
        0.25f
    );
    CheckMixedAlignment(
        AlignmentMixed.Lerp(new Alignment(0.25f, 0.5f), new AlignmentDirectional(-0.75f, 0.25f), 0.5f),
        0.125f,
        -0.375f,
        0.375f
    );

}
[GuiTest("math/layout_tests.cpp::gui/math/EdgeInsets factories arithmetic and geometry")]
public static void Case4(){

    Check.That(EdgeInsets.Zero() == new EdgeInsets(0.0f, 0.0f, 0.0f, 0.0f));
    Check.That(EdgeInsets.All(2.0f) == new EdgeInsets(2.0f, 2.0f, 2.0f, 2.0f));
    Check.That(EdgeInsets.Symmetric(3.0f, 4.0f) == new EdgeInsets(3.0f, 4.0f, 3.0f, 4.0f));
    Check.That(EdgeInsets.Only(1.0f, 2.0f, 3.0f, 4.0f) == new EdgeInsets(1.0f, 2.0f, 3.0f, 4.0f));

    EdgeInsets value = new EdgeInsets(1.0f, 2.0f, 3.0f, 4.0f);
    ExpectNear(value.Horizontal(), 4.0f, kFloatEpsilon);
    ExpectNear(value.Vertical(), 6.0f, kFloatEpsilon);
    Check.False(value.IsZero());
    Check.That(EdgeInsets.Zero().IsZero());
    Check.That(value.IsNonNegative());
    CheckInsets(value.Flipped(), 3.0f, 4.0f, 1.0f, 2.0f);
    CheckInsets(value.Resolve(ETextDirection.LTR), 1.0f, 2.0f, 3.0f, 4.0f);
    CheckInsets(value.Resolve(ETextDirection.RTL), 1.0f, 2.0f, 3.0f, 4.0f);

    CheckOffsetf(value.TopLeft(), 1.0f, 2.0f);
    CheckOffsetf(value.TopRight(), -3.0f, 2.0f);
    CheckOffsetf(value.BottomLeft(), 1.0f, -4.0f);
    CheckOffsetf(value.BottomRight(), -3.0f, -4.0f);
    CheckSizef(value.CollapsedSize(), 4.0f, 6.0f);
    ExpectNear(value.Along(EAxis.Horizontal), 4.0f, kFloatEpsilon);
    ExpectNear(value.Along(EAxis.Vertical), 6.0f, kFloatEpsilon);
    CheckSizef(value.InflateSize(new Sizef(10.0f, 20.0f)), 14.0f, 26.0f);
    CheckSizef(value.DeflateSize(new Sizef(10.0f, 20.0f)), 6.0f, 14.0f);
    CheckSizef(value.DeflateSize(new Sizef(2.0f, 5.0f)), -2.0f, -1.0f);
    CheckRectf(value.InflateRect(Rectf.LTWH(10.0f, 20.0f, 30.0f, 40.0f)), 9.0f, 18.0f, 43.0f, 64.0f);
    CheckRectf(value.DeflateRect(Rectf.LTWH(10.0f, 20.0f, 30.0f, 40.0f)), 11.0f, 22.0f, 37.0f, 56.0f);
    RRect rrect = new RRect(
        10.0f,
        20.0f,
        40.0f,
        60.0f,
        new Radius(2.0f, 3.0f),
        new Radius(4.0f, 5.0f),
        new Radius(6.0f, 7.0f),
        new Radius(8.0f, 9.0f)
    );
    CheckRrect(
        value.InflateRrect(rrect),
        9.0f,
        18.0f,
        43.0f,
        64.0f,
        new Radius(3.0f, 5.0f),
        new Radius(7.0f, 7.0f),
        new Radius(9.0f, 11.0f),
        new Radius(9.0f, 13.0f)
    );
    CheckRrect(
        value.DeflateRrect(rrect),
        11.0f,
        22.0f,
        37.0f,
        56.0f,
        new Radius(1.0f, 1.0f),
        new Radius(1.0f, 3.0f),
        new Radius(3.0f, 3.0f),
        new Radius(7.0f, 5.0f)
    );
    CheckRrect(
        value.DeflateRrect(RRect.FromLTRBXY(0.0f, 0.0f, 10.0f, 10.0f, 1.0f, 1.0f)),
        1.0f,
        2.0f,
        7.0f,
        6.0f,
        Radius.Zero(),
        Radius.Zero(),
        Radius.Zero(),
        Radius.Zero()
    );
    CheckInsets(value.CopyWith(10.0f, null, null, 40.0f), 10.0f, 2.0f, 3.0f, 40.0f);

    CheckInsets(value + EdgeInsets.All(1.0f), 2.0f, 3.0f, 4.0f, 5.0f);
    CheckInsets(value - EdgeInsets.All(1.0f), 0.0f, 1.0f, 2.0f, 3.0f);
    CheckInsets(-value, -1.0f, -2.0f, -3.0f, -4.0f);
    CheckInsets(value * 2.0f, 2.0f, 4.0f, 6.0f, 8.0f);
    CheckInsets(2.0f * value, 2.0f, 4.0f, 6.0f, 8.0f);
    CheckInsets(value / 2.0f, 0.5f, 1.0f, 1.5f, 2.0f);

    EdgeInsets accum = value;
    accum += EdgeInsets.Only(1.0f, 1.0f, 1.0f, 1.0f);
    CheckInsets(accum, 2.0f, 3.0f, 4.0f, 5.0f);
    accum -= EdgeInsets.Only(1.0f, 1.0f, 1.0f, 1.0f);
    CheckInsets(accum, 1.0f, 2.0f, 3.0f, 4.0f);
    accum *= 2.0f;
    CheckInsets(accum, 2.0f, 4.0f, 6.0f, 8.0f);
    accum /= 2.0f;
    CheckInsets(accum, 1.0f, 2.0f, 3.0f, 4.0f);

    CheckInsets(
        EdgeInsets.Lerp(EdgeInsets.Zero(), new EdgeInsets(2.0f, 4.0f, 6.0f, 8.0f), 0.5f),
        1.0f,
        2.0f,
        3.0f,
        4.0f
    );
    CheckInsets(
        new EdgeInsets(-1.0f, 5.0f, 10.0f, 2.0f).Clamp(EdgeInsets.Zero(), EdgeInsets.All(4.0f)),
        0.0f,
        4.0f,
        4.0f,
        2.0f
    );

}
[GuiTest("math/layout_tests.cpp::gui/math/EdgeInsetsDirectional factories arithmetic and resolve")]
public static void Case5(){

    Check.That(EdgeInsetsDirectional.Zero() == new EdgeInsetsDirectional(0.0f, 0.0f, 0.0f, 0.0f));
    Check.That(EdgeInsetsDirectional.All(2.0f) == new EdgeInsetsDirectional(2.0f, 2.0f, 2.0f, 2.0f));
    Check.That(EdgeInsetsDirectional.Symmetric(3.0f, 4.0f) == new EdgeInsetsDirectional(3.0f, 4.0f, 3.0f, 4.0f));
    Check.That(EdgeInsetsDirectional.Only(1.0f, 2.0f, 3.0f, 4.0f) == new EdgeInsetsDirectional(1.0f, 2.0f, 3.0f, 4.0f));
    Check.That(EdgeInsetsDirectional.FromSTEB(1.0f, 2.0f, 3.0f, 4.0f) == new EdgeInsetsDirectional(1.0f, 2.0f, 3.0f, 4.0f));

    EdgeInsetsDirectional value = new EdgeInsetsDirectional(1.0f, 2.0f, 3.0f, 4.0f);
    ExpectNear(value.Horizontal(), 4.0f, kFloatEpsilon);
    ExpectNear(value.Vertical(), 6.0f, kFloatEpsilon);
    Check.False(value.IsZero());
    Check.That(EdgeInsetsDirectional.Zero().IsZero());
    Check.That(value.IsNonNegative());
    CheckDirectionalInsets(value.Flipped(), 3.0f, 4.0f, 1.0f, 2.0f);

    CheckSizef(value.CollapsedSize(), 4.0f, 6.0f);
    ExpectNear(value.Along(EAxis.Horizontal), 4.0f, kFloatEpsilon);
    ExpectNear(value.Along(EAxis.Vertical), 6.0f, kFloatEpsilon);
    CheckSizef(value.InflateSize(new Sizef(10.0f, 20.0f)), 14.0f, 26.0f);
    CheckSizef(value.DeflateSize(new Sizef(10.0f, 20.0f)), 6.0f, 14.0f);
    CheckSizef(value.DeflateSize(new Sizef(2.0f, 5.0f)), -2.0f, -1.0f);
    CheckInsets(value.Resolve(ETextDirection.LTR), 1.0f, 2.0f, 3.0f, 4.0f);
    CheckInsets(value.Resolve(ETextDirection.RTL), 3.0f, 2.0f, 1.0f, 4.0f);
    CheckDirectionalInsets(value.CopyWith(10.0f, null, null, 40.0f), 10.0f, 2.0f, 3.0f, 40.0f);

    CheckDirectionalInsets(value + EdgeInsetsDirectional.All(1.0f), 2.0f, 3.0f, 4.0f, 5.0f);
    CheckDirectionalInsets(value - EdgeInsetsDirectional.All(1.0f), 0.0f, 1.0f, 2.0f, 3.0f);
    CheckDirectionalInsets(-value, -1.0f, -2.0f, -3.0f, -4.0f);
    CheckDirectionalInsets(value * 2.0f, 2.0f, 4.0f, 6.0f, 8.0f);
    CheckDirectionalInsets(2.0f * value, 2.0f, 4.0f, 6.0f, 8.0f);
    CheckDirectionalInsets(value / 2.0f, 0.5f, 1.0f, 1.5f, 2.0f);

    EdgeInsetsDirectional accum = value;
    accum += EdgeInsetsDirectional.All(1.0f);
    CheckDirectionalInsets(accum, 2.0f, 3.0f, 4.0f, 5.0f);
    accum -= EdgeInsetsDirectional.All(1.0f);
    CheckDirectionalInsets(accum, 1.0f, 2.0f, 3.0f, 4.0f);
    accum *= 2.0f;
    CheckDirectionalInsets(accum, 2.0f, 4.0f, 6.0f, 8.0f);
    accum /= 2.0f;
    CheckDirectionalInsets(accum, 1.0f, 2.0f, 3.0f, 4.0f);

    CheckDirectionalInsets(
        EdgeInsetsDirectional.Lerp(EdgeInsetsDirectional.Zero(), new EdgeInsetsDirectional(2.0f, 4.0f, 6.0f, 8.0f), 0.5f),
        1.0f,
        2.0f,
        3.0f,
        4.0f
    );
    CheckDirectionalInsets(
        new EdgeInsetsDirectional(-1.0f, 5.0f, 10.0f, 2.0f).Clamp(EdgeInsetsDirectional.Zero(), EdgeInsetsDirectional.All(4.0f)),
        0.0f,
        4.0f,
        4.0f,
        2.0f
    );

}
[GuiTest("math/layout_tests.cpp::gui/math/EdgeInsetsMixed arithmetic and resolve")]
public static void Case6(){

    Check.That(EdgeInsetsMixed.Zero() == new EdgeInsetsMixed(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
    Check.That(EdgeInsetsMixed.FromLTRB(1.0f, 2.0f, 3.0f, 4.0f) == new EdgeInsetsMixed(1.0f, 3.0f, 0.0f, 0.0f, 2.0f, 4.0f));
    Check.That(EdgeInsetsMixed.FromSTEB(1.0f, 2.0f, 3.0f, 4.0f) == new EdgeInsetsMixed(0.0f, 0.0f, 1.0f, 3.0f, 2.0f, 4.0f));
    EdgeInsetsMixed physical = new EdgeInsets(1.0f, 2.0f, 3.0f, 4.0f);
    EdgeInsetsMixed directional = new EdgeInsetsDirectional(1.0f, 2.0f, 3.0f, 4.0f);
    Check.That(physical == EdgeInsetsMixed.FromLTRB(1.0f, 2.0f, 3.0f, 4.0f));
    Check.That(directional == EdgeInsetsMixed.FromSTEB(1.0f, 2.0f, 3.0f, 4.0f));

    EdgeInsetsMixed value = EdgeInsetsMixed.FromLRSETB(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f);
    ExpectNear(value.Horizontal(), 10.0f, kFloatEpsilon);
    ExpectNear(value.Vertical(), 11.0f, kFloatEpsilon);
    Check.False(value.IsZero());
    Check.That(EdgeInsetsMixed.Zero().IsZero());
    Check.That(value.IsNonNegative());
    CheckMixedInsets(value.Flipped(), 2.0f, 1.0f, 4.0f, 3.0f, 6.0f, 5.0f);

    CheckSizef(value.CollapsedSize(), 10.0f, 11.0f);
    ExpectNear(value.Along(EAxis.Horizontal), 10.0f, kFloatEpsilon);
    ExpectNear(value.Along(EAxis.Vertical), 11.0f, kFloatEpsilon);
    CheckSizef(value.InflateSize(new Sizef(10.0f, 20.0f)), 20.0f, 31.0f);
    CheckSizef(value.DeflateSize(new Sizef(20.0f, 20.0f)), 10.0f, 9.0f);
    CheckSizef(value.DeflateSize(new Sizef(5.0f, 6.0f)), -5.0f, -5.0f);
    CheckInsets(value.Resolve(ETextDirection.LTR), 4.0f, 5.0f, 6.0f, 6.0f);
    CheckInsets(value.Resolve(ETextDirection.RTL), 5.0f, 5.0f, 5.0f, 6.0f);
    CheckMixedInsets(value.CopyWith(10.0f, null, null, null, null, 60.0f), 10.0f, 2.0f, 3.0f, 4.0f, 5.0f, 60.0f);

    EdgeInsetsMixed mixed_from_add = new EdgeInsets(10.0f, 2.0f, 20.0f, 4.0f) + new EdgeInsetsDirectional(1.0f, 3.0f, 5.0f, 7.0f);
    CheckMixedInsets(mixed_from_add, 10.0f, 20.0f, 1.0f, 5.0f, 5.0f, 11.0f);
    CheckInsets(mixed_from_add.Resolve(ETextDirection.LTR), 11.0f, 5.0f, 25.0f, 11.0f);
    CheckInsets(mixed_from_add.Resolve(ETextDirection.RTL), 15.0f, 5.0f, 21.0f, 11.0f);

    CheckMixedInsets(value + EdgeInsetsMixed.FromLRSETB(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f), 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f);
    CheckMixedInsets(value - EdgeInsetsMixed.FromLRSETB(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f), 0.0f, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f);
    CheckMixedInsets(-value, -1.0f, -2.0f, -3.0f, -4.0f, -5.0f, -6.0f);
    CheckMixedInsets(value * 2.0f, 2.0f, 4.0f, 6.0f, 8.0f, 10.0f, 12.0f);
    CheckMixedInsets(2.0f * value, 2.0f, 4.0f, 6.0f, 8.0f, 10.0f, 12.0f);
    CheckMixedInsets(value / 2.0f, 0.5f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f);

    EdgeInsetsMixed accum = value;
    accum += EdgeInsetsMixed.FromLRSETB(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
    CheckMixedInsets(accum, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f);
    accum -= EdgeInsetsMixed.FromLRSETB(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
    CheckMixedInsets(accum, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f);
    accum *= 2.0f;
    CheckMixedInsets(accum, 2.0f, 4.0f, 6.0f, 8.0f, 10.0f, 12.0f);
    accum /= 2.0f;
    CheckMixedInsets(accum, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f);

    CheckMixedInsets(
        EdgeInsetsMixed.Lerp(EdgeInsetsMixed.Zero(), EdgeInsetsMixed.FromLRSETB(2.0f, 4.0f, 6.0f, 8.0f, 10.0f, 12.0f), 0.5f),
        1.0f,
        2.0f,
        3.0f,
        4.0f,
        5.0f,
        6.0f
    );
    CheckMixedInsets(
        EdgeInsetsMixed.FromLRSETB(-1.0f, 5.0f, 10.0f, 2.0f, -3.0f, 7.0f).Clamp(EdgeInsetsMixed.Zero(), EdgeInsetsMixed.FromLRSETB(4.0f, 4.0f, 4.0f, 4.0f, 4.0f, 4.0f)),
        0.0f,
        4.0f,
        4.0f,
        2.0f,
        0.0f,
        4.0f
    );

}
[GuiTest("math/layout_tests.cpp::gui/math/BoxConstraints factories and predicates")]
public static void Case7(){

    BoxConstraints unconstrained = new();
    CheckConstraints(
        unconstrained,
        0.0f,
        float.PositiveInfinity,
        0.0f,
        float.PositiveInfinity
    );

    var tight = BoxConstraints.Tight(new Sizef(10.0f, 20.0f));
    CheckConstraints(tight, 10.0f, 10.0f, 20.0f, 20.0f);
    Check.That(tight.IsTight());
    Check.That(tight.HasBoundedWidth());
    Check.That(tight.HasBoundedHeight());

    CheckConstraints(BoxConstraints.TightWidth(15.0f), 15.0f, 15.0f, 0.0f, float.PositiveInfinity);
    CheckConstraints(BoxConstraints.TightHeight(7.0f), 0.0f, float.PositiveInfinity, 7.0f, 7.0f);
    CheckConstraints(BoxConstraints.Loose(new Sizef(8.0f, 9.0f)), 0.0f, 8.0f, 0.0f, 9.0f);
    CheckConstraints(BoxConstraints.LooseWidth(11.0f), 0.0f, 11.0f, 0.0f, float.PositiveInfinity);
    CheckConstraints(BoxConstraints.LooseHeight(12.0f), 0.0f, float.PositiveInfinity, 0.0f, 12.0f);

    var expand = BoxConstraints.Expand();
    Check.That(expand.HasInfiniteWidth());
    Check.That(expand.HasInfiniteHeight());
    Check.False(expand.HasBoundedWidth());
    Check.False(expand.HasBoundedHeight());

    CheckConstraints(BoxConstraints.ExpandWidth(9.0f), float.PositiveInfinity, float.PositiveInfinity, 9.0f, 9.0f);
    CheckConstraints(BoxConstraints.ExpandWidth(3.0f, 9.0f), float.PositiveInfinity, float.PositiveInfinity, 3.0f, 9.0f);
    CheckConstraints(BoxConstraints.ExpandHeight(3.0f), 3.0f, 3.0f, float.PositiveInfinity, float.PositiveInfinity);
    CheckConstraints(BoxConstraints.ExpandHeight(4.0f, 12.0f), 4.0f, 12.0f, float.PositiveInfinity, float.PositiveInfinity);

}
[GuiTest("math/layout_tests.cpp::gui/math/BoxConstraints constrain semantics and enforcement")]
public static void Case8(){

    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 20.0f);

    Check.That(constraints == new BoxConstraints(10.0f, 50.0f, 5.0f, 20.0f));
    Check.False(constraints != new BoxConstraints(10.0f, 50.0f, 5.0f, 20.0f));

    CheckSizef(constraints.MinSize(), 10.0f, 5.0f);
    CheckSizef(constraints.MaxSize(), 50.0f, 20.0f);

    constraints.SetMinSize(new Sizef(12.0f, 8.0f));
    constraints.SetMaxSize(new Sizef(40.0f, 16.0f));
    CheckConstraints(constraints, 12.0f, 40.0f, 8.0f, 16.0f);

    CheckSizef(constraints.Smallest(), 12.0f, 8.0f);
    CheckSizef(constraints.Biggest(), 40.0f, 16.0f);
    ExpectNear(constraints.ConstrainWidth(100.0f), 40.0f, kFloatEpsilon);
    ExpectNear(constraints.ConstrainHeight(1.0f), 8.0f, kFloatEpsilon);
    CheckSizef(constraints.Constrain(new Sizef(100.0f, 1.0f)), 40.0f, 8.0f);

    CheckConstraints(constraints.Loosen(), 0.0f, 40.0f, 0.0f, 16.0f);
    CheckConstraints(
        new BoxConstraints(0.0f, 100.0f, 0.0f, 100.0f).Enforce(new BoxConstraints(10.0f, 30.0f, 20.0f, 40.0f)),
        10.0f,
        30.0f,
        20.0f,
        40.0f
    );

}
[GuiTest("math/layout_tests.cpp::gui/math/BoxConstraints helpers transforms and validation")]
public static void Case9(){

    float? none = null;
    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 20.0f);

    CheckConstraints(BoxConstraints.TightFor(12.0f, none), 12.0f, 12.0f, 0.0f, float.PositiveInfinity);
    CheckConstraints(BoxConstraints.TightFor(none, 7.0f), 0.0f, float.PositiveInfinity, 7.0f, 7.0f);
    CheckConstraints(BoxConstraints.TightForFinite(float.PositiveInfinity, 9.0f), 0.0f, float.PositiveInfinity, 9.0f, 9.0f);

    CheckConstraints(constraints.CopyWith(12.0f, none, none, 18.0f), 12.0f, 50.0f, 5.0f, 18.0f);
    CheckConstraints(constraints.Tighten(40.0f, 1.0f), 40.0f, 40.0f, 5.0f, 5.0f);
    CheckConstraints(constraints.Deflate(3.0f, 4.0f, 5.0f, 6.0f), 2.0f, 42.0f, 0.0f, 10.0f);
    CheckConstraints(constraints.Deflate(new EdgeInsets(3.0f, 4.0f, 5.0f, 6.0f)), 2.0f, 42.0f, 0.0f, 10.0f);
    CheckConstraints(constraints.Flipped(), 5.0f, 20.0f, 10.0f, 50.0f);
    CheckConstraints(constraints.WidthConstraints(), 10.0f, 50.0f, 0.0f, float.PositiveInfinity);
    CheckConstraints(constraints.HeightConstraints(), 0.0f, float.PositiveInfinity, 5.0f, 20.0f);

    Check.That(constraints.IsNormalized());
    Check.False(new BoxConstraints(10.0f, 5.0f, 0.0f, 1.0f).IsNormalized());
    Check.That(constraints.IsSatisfiedBy(new Sizef(20.0f, 10.0f)));
    Check.False(constraints.IsSatisfiedBy(new Sizef(60.0f, 10.0f)));
    constraints.AssertValid();

    CheckSizef(constraints.Constrain(100.0f, 1.0f), 50.0f, 5.0f);
    CheckSizef(
        constraints.ConstrainKeepAspect(new Sizef(100.0f, 25.0f)),
        50.0f,
        12.5f
    );
    CheckSizef(
        BoxConstraints.Tight(new Sizef(12.0f, 8.0f)).ConstrainKeepAspect(new Sizef(100.0f, 25.0f)),
        12.0f,
        8.0f
    );
    CheckSizef(
        constraints.ConstrainKeepAspect(Sizef.Zero()),
        10.0f,
        5.0f
    );

    CheckConstraints(
        BoxConstraints.Lerp(
            new BoxConstraints(0.0f, 10.0f, 0.0f, 20.0f),
            new BoxConstraints(10.0f, 20.0f, 10.0f, 30.0f),
            0.5f
        ),
        5.0f,
        15.0f,
        5.0f,
        25.0f
    );

}
}
