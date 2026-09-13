using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
namespace SkrGui.Tests;
internal static class PlacementTests
{
private static BoxConstraints RequireConstraints(
    Placement placement,
    BoxConstraints parent_constraints,
    EPlacementSizeMode size_mode
){
    BoxConstraints? result = placement.ResolveConstraints(
        parent_constraints,
        size_mode
    );
    Check.That(result.HasValue);
    return result.Value;
}
private static Sizef RequireSize(Placement placement, Sizef parent_size_px){
    Sizef? result = placement.ResolveSize(parent_size_px);
    Check.That(result.HasValue);
    return result.Value;
}
private static Offsetf RequirePlaceOffset(
    Placement placement,
    Sizef parent_size_px,
    Sizef child_size_px
){
    Offsetf? result = placement.PlaceOffset(
        parent_size_px,
        child_size_px
    );
    Check.That(result.HasValue);
    return result.Value;
}
private static Rectf RequirePlaceRect(
    Placement placement,
    Rectf parent_rect_px,
    Sizef child_size_px
){
    Rectf? result = placement.PlaceRect(parent_rect_px, child_size_px);
    Check.That(result.HasValue);
    return result.Value;
}
private static void CheckPlacement(
    Placement placement,
    Rectf anchor,
    Sizef size_delta,
    Alignment pivot,
    Offsetf pivot_offset
){
    Check.That(placement.Anchor == anchor);
    Check.That(placement.SizeDelta == size_delta);
    Check.That(placement.Pivot == pivot);
    Check.That(placement.PivotOffset == pivot_offset);
}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/construction and value semantics")]
public static void Case0(){

    Placement default_placement = new();
    Placement fill_factory = Placement.Fill();
    CheckPlacement(
        default_placement,
        new Rectf(0.0f, 0.0f, 1.0f, 1.0f),
        Sizef.Zero(),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    Check.That(default_placement == fill_factory);

    Placement reset = Placement.Pin(
        new Offsetf(0.25f, 0.75f),
        new Offsetf(4.0f, -2.0f),
        new Sizef(20.0f, 10.0f),
        Alignment.Center()
    );
    ref Placement reset_result = ref reset.ResetFill();
    Check.That(System.Runtime.CompilerServices.Unsafe.AreSame(ref reset_result,ref reset));
    Check.That(reset == fill_factory);

    Placement direct = new Placement(
        new Rectf(-0.25f, 0.2f, 0.75f, 0.8f),
        new Sizef(12.0f, -6.0f),
        Alignment.CenterRight(),
        new Offsetf(3.0f, -4.0f)
    );
    CheckPlacement(
        direct,
        new Rectf(-0.25f, 0.2f, 0.75f, 0.8f),
        new Sizef(12.0f, -6.0f),
        Alignment.CenterRight(),
        new Offsetf(3.0f, -4.0f)
    );

    Placement pin_factory = Placement.Pin(
        new Offsetf(1.25f, -0.25f),
        new Offsetf(-12.0f, 8.0f),
        new Sizef(80.0f, 36.0f),
        Alignment.Center()
    );
    CheckPlacement(
        pin_factory,
        new Rectf(1.25f, -0.25f, 1.25f, -0.25f),
        new Sizef(80.0f, 36.0f),
        Alignment.Center(),
        new Offsetf(-12.0f, 8.0f)
    );

    Placement align_factory = Placement.Align(
        new Alignment(0.25f, -0.5f),
        new Sizef(40.0f, 20.0f),
        new Offsetf(-5.0f, 3.0f)
    );
    CheckPlacement(
        align_factory,
        new Rectf(0.625f, 0.25f, 0.625f, 0.25f),
        new Sizef(40.0f, 20.0f),
        new Alignment(0.25f, -0.5f),
        new Offsetf(-5.0f, 3.0f)
    );

    Placement copied = pin_factory;
    Check.That(copied == pin_factory);
    Placement moved = Identity(copied);
    Check.That(moved == pin_factory);

    Placement copy_assigned = new();
    copy_assigned = align_factory;
    Check.That(copy_assigned == align_factory);
    Placement move_assigned = new();
    move_assigned = Identity(copy_assigned);
    Check.That(move_assigned == align_factory);
    Check.That(move_assigned != pin_factory);

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/pin builders")]
public static void Case1(){

    Placement generic = new Placement(
        new Rectf(0.0f, 0.0f, 1.0f, 1.0f),
        new Sizef(99.0f, 88.0f),
        Alignment.BottomRight(),
        new Offsetf(7.0f, 6.0f)
    );
    generic.PinAt(new Offsetf(0.25f, 0.75f), new Offsetf(2.0f, -3.0f))
        .SizePx(new Sizef(30.0f, 18.0f))
        .Pivot(Alignment.Center())
        .OffsetPx(new Offsetf(12.0f, -6.0f));
    CheckPlacement(
        generic,
        new Rectf(0.25f, 0.75f, 0.25f, 0.75f),
        new Sizef(30.0f, 18.0f),
        Alignment.Center(),
        new Offsetf(12.0f, -6.0f)
    );

    Placement left_top = new();
    left_top.PinLT(new Offsetf(0.1f, 0.2f), new Offsetf(3.0f, 4.0f))
        .SizePx(new Sizef(20.0f, 10.0f));
    CheckPlacement(
        left_top,
        new Rectf(0.1f, 0.2f, 0.1f, 0.2f),
        new Sizef(20.0f, 10.0f),
        Alignment.TopLeft(),
        new Offsetf(3.0f, 4.0f)
    );

    Placement right_top = new();
    right_top.PinRT(new Offsetf(0.1f, 0.2f), new Offsetf(3.0f, 4.0f))
        .SizePx(new Sizef(20.0f, 10.0f));
    CheckPlacement(
        right_top,
        new Rectf(0.9f, 0.2f, 0.9f, 0.2f),
        new Sizef(20.0f, 10.0f),
        Alignment.TopRight(),
        new Offsetf(-3.0f, 4.0f)
    );

    Placement left_bottom = new();
    left_bottom.PinLB(new Offsetf(0.1f, 0.2f), new Offsetf(3.0f, 4.0f))
        .SizePx(new Sizef(20.0f, 10.0f));
    CheckPlacement(
        left_bottom,
        new Rectf(0.1f, 0.8f, 0.1f, 0.8f),
        new Sizef(20.0f, 10.0f),
        Alignment.BottomLeft(),
        new Offsetf(3.0f, -4.0f)
    );

    Placement right_bottom = new();
    right_bottom.PinRB(new Offsetf(0.1f, 0.2f), new Offsetf(3.0f, 4.0f))
        .SizePx(new Sizef(20.0f, 10.0f));
    CheckPlacement(
        right_bottom,
        new Rectf(0.9f, 0.8f, 0.9f, 0.8f),
        new Sizef(20.0f, 10.0f),
        Alignment.BottomRight(),
        new Offsetf(-3.0f, -4.0f)
    );

    Placement preset = new();
    preset.PinLTPct(0.1f, 0.2f);
    CheckPlacement(
        preset,
        new Rectf(0.1f, 0.2f, 0.1f, 0.2f),
        Sizef.Zero(),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    preset.PinRTPct(0.1f, 0.2f);
    CheckPlacement(
        preset,
        new Rectf(0.9f, 0.2f, 0.9f, 0.2f),
        Sizef.Zero(),
        Alignment.TopRight(),
        Offsetf.Zero()
    );
    preset.PinLBPct(0.1f, 0.2f);
    CheckPlacement(
        preset,
        new Rectf(0.1f, 0.8f, 0.1f, 0.8f),
        Sizef.Zero(),
        Alignment.BottomLeft(),
        Offsetf.Zero()
    );
    preset.PinRBPct(0.1f, 0.2f);
    CheckPlacement(
        preset,
        new Rectf(0.9f, 0.8f, 0.9f, 0.8f),
        Sizef.Zero(),
        Alignment.BottomRight(),
        Offsetf.Zero()
    );

    preset.PinLTPx(3.0f, 4.0f);
    CheckPlacement(
        preset,
        new Rectf(0.0f, 0.0f, 0.0f, 0.0f),
        Sizef.Zero(),
        Alignment.TopLeft(),
        new Offsetf(3.0f, 4.0f)
    );
    preset.PinRTPx(3.0f, 4.0f);
    CheckPlacement(
        preset,
        new Rectf(1.0f, 0.0f, 1.0f, 0.0f),
        Sizef.Zero(),
        Alignment.TopRight(),
        new Offsetf(-3.0f, 4.0f)
    );
    preset.PinLBPx(3.0f, 4.0f);
    CheckPlacement(
        preset,
        new Rectf(0.0f, 1.0f, 0.0f, 1.0f),
        Sizef.Zero(),
        Alignment.BottomLeft(),
        new Offsetf(3.0f, -4.0f)
    );
    preset.PinRBPx(3.0f, 4.0f);
    CheckPlacement(
        preset,
        new Rectf(1.0f, 1.0f, 1.0f, 1.0f),
        Sizef.Zero(),
        Alignment.BottomRight(),
        new Offsetf(-3.0f, -4.0f)
    );

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/align builders")]
public static void Case2(){

    Placement generic = Placement.Pin(
        Offsetf.Zero(),
        new Offsetf(5.0f, 6.0f),
        new Sizef(10.0f, 20.0f),
        Alignment.BottomRight()
    );
    generic.AlignTo(new Alignment(0.25f, -0.5f))
        .SizePx(new Sizef(40.0f, 20.0f))
        .OffsetPx(new Offsetf(-5.0f, 3.0f));
    CheckPlacement(
        generic,
        new Rectf(0.625f, 0.25f, 0.625f, 0.25f),
        new Sizef(40.0f, 20.0f),
        new Alignment(0.25f, -0.5f),
        new Offsetf(-5.0f, 3.0f)
    );

    Placement preset = new();
    preset.AlignLeftTop();
    CheckPlacement(
        preset,
        new Rectf(0.0f, 0.0f, 0.0f, 0.0f),
        Sizef.Zero(),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    preset.AlignCenterTop();
    CheckPlacement(
        preset,
        new Rectf(0.5f, 0.0f, 0.5f, 0.0f),
        Sizef.Zero(),
        Alignment.TopCenter(),
        Offsetf.Zero()
    );
    preset.AlignRightTop();
    CheckPlacement(
        preset,
        new Rectf(1.0f, 0.0f, 1.0f, 0.0f),
        Sizef.Zero(),
        Alignment.TopRight(),
        Offsetf.Zero()
    );
    preset.AlignLeftCenter();
    CheckPlacement(
        preset,
        new Rectf(0.0f, 0.5f, 0.0f, 0.5f),
        Sizef.Zero(),
        Alignment.CenterLeft(),
        Offsetf.Zero()
    );
    preset.AlignCenter();
    CheckPlacement(
        preset,
        new Rectf(0.5f, 0.5f, 0.5f, 0.5f),
        Sizef.Zero(),
        Alignment.Center(),
        Offsetf.Zero()
    );
    preset.AlignRightCenter();
    CheckPlacement(
        preset,
        new Rectf(1.0f, 0.5f, 1.0f, 0.5f),
        Sizef.Zero(),
        Alignment.CenterRight(),
        Offsetf.Zero()
    );
    preset.AlignLeftBottom();
    CheckPlacement(
        preset,
        new Rectf(0.0f, 1.0f, 0.0f, 1.0f),
        Sizef.Zero(),
        Alignment.BottomLeft(),
        Offsetf.Zero()
    );
    preset.AlignCenterBottom();
    CheckPlacement(
        preset,
        new Rectf(0.5f, 1.0f, 0.5f, 1.0f),
        Sizef.Zero(),
        Alignment.BottomCenter(),
        Offsetf.Zero()
    );
    preset.AlignRightBottom();
    CheckPlacement(
        preset,
        new Rectf(1.0f, 1.0f, 1.0f, 1.0f),
        Sizef.Zero(),
        Alignment.BottomRight(),
        Offsetf.Zero()
    );

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/inset combined setters")]
public static void Case3(){

    Placement all = Placement.Pin(
        Offsetf.Zero(),
        new Offsetf(3.0f, 4.0f),
        new Sizef(20.0f, 10.0f),
        Alignment.BottomRight()
    );
    all.Inset(Alignment.Center()).All(0.1f, 6.0f);
    CheckPlacement(
        all,
        new Rectf(0.1f, 0.1f, 0.9f, 0.9f),
        new Sizef(-12.0f, -12.0f),
        Alignment.Center(),
        Offsetf.Zero()
    );

    Placement horizontal = new();
    horizontal.Inset(Alignment.BottomRight()).Horizontal(0.1f, 3.0f);
    CheckPlacement(
        horizontal,
        new Rectf(0.1f, 0.0f, 0.9f, 1.0f),
        new Sizef(-6.0f, 0.0f),
        Alignment.BottomRight(),
        new Offsetf(-3.0f, 0.0f)
    );

    Placement vertical = new();
    vertical.Inset(Alignment.BottomRight()).Vertical(0.2f, 4.0f);
    CheckPlacement(
        vertical,
        new Rectf(0.0f, 0.2f, 1.0f, 0.8f),
        new Sizef(0.0f, -8.0f),
        Alignment.BottomRight(),
        new Offsetf(0.0f, -4.0f)
    );

    Placement edges = new();
    edges.Inset(Alignment.Center())
        .Left(0.1f, 10.0f)
        .Top(0.2f, 5.0f)
        .Right(0.25f, 20.0f)
        .Bottom(0.1f, 15.0f);
    CheckPlacement(
        edges,
        new Rectf(0.1f, 0.2f, 0.75f, 0.9f),
        new Sizef(-30.0f, -20.0f),
        Alignment.Center(),
        new Offsetf(-5.0f, -5.0f)
    );

    Placement overwritten = new();
    overwritten.Inset()
        .Left(0.1f, 3.0f)
        .Right(0.1f, 7.0f)
        .Left(0.2f, 9.0f);
    CheckPlacement(
        overwritten,
        new Rectf(0.2f, 0.0f, 0.9f, 1.0f),
        new Sizef(-16.0f, 0.0f),
        Alignment.TopLeft(),
        new Offsetf(9.0f, 0.0f)
    );

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/inset percent setters")]
public static void Case4(){

    Placement all = new();
    all.Inset(Alignment.Center()).AllPx(6.0f).AllPct(0.1f);
    CheckPlacement(
        all,
        new Rectf(0.1f, 0.1f, 0.9f, 0.9f),
        new Sizef(-12.0f, -12.0f),
        Alignment.Center(),
        Offsetf.Zero()
    );

    Placement axes = new();
    axes.Inset()
        .HorizontalPx(3.0f)
        .VerticalPx(4.0f)
        .HorizontalPct(0.1f)
        .VerticalPct(0.2f);
    CheckPlacement(
        axes,
        new Rectf(0.1f, 0.2f, 0.9f, 0.8f),
        new Sizef(-6.0f, -8.0f),
        Alignment.TopLeft(),
        new Offsetf(3.0f, 4.0f)
    );

    Placement edges = new();
    edges.Inset(Alignment.Center())
        .LeftPx(10.0f)
        .TopPx(5.0f)
        .RightPx(20.0f)
        .BottomPx(15.0f)
        .LeftPct(0.1f)
        .TopPct(0.2f)
        .RightPct(0.25f)
        .BottomPct(0.1f);
    CheckPlacement(
        edges,
        new Rectf(0.1f, 0.2f, 0.75f, 0.9f),
        new Sizef(-30.0f, -20.0f),
        Alignment.Center(),
        new Offsetf(-5.0f, -5.0f)
    );

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/inset pixel setters")]
public static void Case5(){

    Placement all = new();
    all.Inset(Alignment.BottomRight()).AllPct(0.1f).AllPx(6.0f);
    CheckPlacement(
        all,
        new Rectf(0.1f, 0.1f, 0.9f, 0.9f),
        new Sizef(-12.0f, -12.0f),
        Alignment.BottomRight(),
        new Offsetf(-6.0f, -6.0f)
    );

    Placement axes = new();
    axes.Inset()
        .HorizontalPct(0.1f)
        .VerticalPct(0.2f)
        .HorizontalPx(3.0f)
        .VerticalPx(4.0f);
    CheckPlacement(
        axes,
        new Rectf(0.1f, 0.2f, 0.9f, 0.8f),
        new Sizef(-6.0f, -8.0f),
        Alignment.TopLeft(),
        new Offsetf(3.0f, 4.0f)
    );

    Placement edges = new();
    edges.Inset(Alignment.BottomRight())
        .LeftPct(0.1f)
        .TopPct(0.2f)
        .RightPct(0.25f)
        .BottomPct(0.1f)
        .LeftPx(10.0f)
        .TopPx(5.0f)
        .RightPx(20.0f)
        .BottomPx(15.0f);
    CheckPlacement(
        edges,
        new Rectf(0.1f, 0.2f, 0.75f, 0.9f),
        new Sizef(-30.0f, -20.0f),
        Alignment.BottomRight(),
        new Offsetf(-20.0f, -15.0f)
    );

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/modes and validation")]
public static void Case6(){

    Placement frame = Placement.Fill();
    Check.That(frame.IsWidthInset());
    Check.That(frame.IsHeightInset());
    Check.That(frame.IsInset());
    Check.False(frame.IsWidthPinned());
    Check.False(frame.IsHeightPinned());
    Check.False(frame.IsPinned());
    Check.That(frame.IsValid());

    Placement pin = Placement.Pin(
        Offsetf.Zero(),
        Offsetf.Zero(),
        new Sizef(20.0f, 10.0f)
    );
    Check.That(pin.IsWidthPinned());
    Check.That(pin.IsHeightPinned());
    Check.That(pin.IsPinned());
    Check.False(pin.IsWidthInset());
    Check.False(pin.IsHeightInset());
    Check.False(pin.IsInset());
    Check.That(pin.IsValid());

    Placement width_frame = new Placement(
        new Rectf(0.1f, 0.5f, 0.9f, 0.5f),
        new Sizef(-10.0f, 20.0f),
        Alignment.Center(),
        Offsetf.Zero()
    );
    Check.That(width_frame.IsWidthInset());
    Check.That(width_frame.IsHeightPinned());
    Check.False(width_frame.IsInset());
    Check.False(width_frame.IsPinned());
    Check.That(width_frame.IsValid());

    Placement height_frame = new Placement(
        new Rectf(0.5f, 0.1f, 0.5f, 0.9f),
        new Sizef(20.0f, -10.0f),
        Alignment.Center(),
        Offsetf.Zero()
    );
    Check.That(height_frame.IsWidthPinned());
    Check.That(height_frame.IsHeightInset());
    Check.False(height_frame.IsInset());
    Check.False(height_frame.IsPinned());
    Check.That(height_frame.IsValid());

    Placement outside = new Placement(
        new Rectf(-0.5f, -0.25f, 1.5f, 1.25f),
        Sizef.Zero(),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    Check.That(outside.IsValid());

    Placement reversed_width = outside;
    reversed_width.Anchor.Left = 2.0f;
    Check.False(reversed_width.IsWidthValid());
    Check.That(reversed_width.IsHeightValid());
    Check.False(reversed_width.IsValid());

    Placement reversed_height = outside;
    reversed_height.Anchor.Top = 2.0f;
    Check.That(reversed_height.IsWidthValid());
    Check.False(reversed_height.IsHeightValid());
    Check.False(reversed_height.IsValid());

    Placement non_finite_anchor = outside;
    non_finite_anchor.Anchor.Left = float.PositiveInfinity;
    Check.False(non_finite_anchor.IsWidthValid());

    Placement negative_pin_width = pin;
    negative_pin_width.SizeDelta.Width = -1.0f;
    Check.False(negative_pin_width.IsWidthValid());

    Placement negative_pin_height = pin;
    negative_pin_height.SizeDelta.Height = -1.0f;
    Check.False(negative_pin_height.IsHeightValid());

    Placement infinite_frame_delta = frame;
    infinite_frame_delta.SizeDelta.Width = float.PositiveInfinity;
    Check.False(infinite_frame_delta.IsWidthValid());

    Placement invalid_pivot = frame;
    invalid_pivot.Pivot.X = float.NaN;
    Check.False(invalid_pivot.IsWidthValid());

    Placement invalid_offset = frame;
    invalid_offset.PivotOffset.Y = float.PositiveInfinity;
    Check.False(invalid_offset.IsHeightValid());

    Placement infinite_loose_pin = Placement.Pin(
        Offsetf.Zero(),
        Offsetf.Zero(),
        Sizef.Infinite()
    );
    Check.That(infinite_loose_pin.IsValid());

    Placement conflicting_inset = new();
    conflicting_inset.Inset().HorizontalPct(0.6f);
    Check.False(conflicting_inset.IsWidthValid());
    Check.False(conflicting_inset.IsValid());

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/constraint propagation")]
public static void Case7(){

    BoxConstraints parent = new BoxConstraints(100.0f, 200.0f, 50.0f, 150.0f);

    Placement pin = Placement.Pin(
        new Offsetf(0.25f, 0.75f),
        Offsetf.Zero(),
        new Sizef(80.0f, 40.0f)
    );
    CheckConstraints(
        RequireConstraints(pin, parent, EPlacementSizeMode.Tight),
        80.0f,
        80.0f,
        40.0f,
        40.0f
    );
    CheckConstraints(
        RequireConstraints(pin, parent, EPlacementSizeMode.Loose),
        0.0f,
        80.0f,
        0.0f,
        40.0f
    );

    Placement frame = new Placement(
        new Rectf(0.1f, 0.2f, 0.6f, 0.8f),
        new Sizef(-10.0f, -5.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    CheckConstraints(
        RequireConstraints(frame, parent, EPlacementSizeMode.Tight),
        40.0f,
        90.0f,
        25.0f,
        85.0f
    );
    CheckConstraints(
        RequireConstraints(frame, parent, EPlacementSizeMode.Loose),
        40.0f,
        90.0f,
        25.0f,
        85.0f
    );

    Placement width_frame = new Placement(
        new Rectf(0.0f, 0.25f, 0.5f, 0.25f),
        new Sizef(10.0f, 30.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    CheckConstraints(
        RequireConstraints(width_frame, parent, EPlacementSizeMode.Tight),
        60.0f,
        110.0f,
        30.0f,
        30.0f
    );
    CheckConstraints(
        RequireConstraints(width_frame, parent, EPlacementSizeMode.Loose),
        60.0f,
        110.0f,
        0.0f,
        30.0f
    );

    Placement height_frame = new Placement(
        new Rectf(0.25f, 0.25f, 0.25f, 0.75f),
        new Sizef(25.0f, -10.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    CheckConstraints(
        RequireConstraints(height_frame, parent, EPlacementSizeMode.Tight),
        25.0f,
        25.0f,
        15.0f,
        65.0f
    );
    CheckConstraints(
        RequireConstraints(height_frame, parent, EPlacementSizeMode.Loose),
        0.0f,
        25.0f,
        15.0f,
        65.0f
    );

    Placement unbounded_frame = new Placement(
        new Rectf(0.25f, 0.1f, 0.75f, 0.9f),
        new Sizef(-10.0f, -5.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    BoxConstraints unbounded_parent = new BoxConstraints(
        0.0f,
        float.PositiveInfinity,
        20.0f,
        100.0f
    );
    CheckConstraints(
        RequireConstraints(unbounded_frame, unbounded_parent, EPlacementSizeMode.Loose),
        0.0f,
        float.PositiveInfinity,
        11.0f,
        75.0f
    );

    Placement infinite_pin = Placement.Pin(
        Offsetf.Zero(),
        Offsetf.Zero(),
        Sizef.Infinite()
    );
    CheckConstraints(
        RequireConstraints(infinite_pin, parent, EPlacementSizeMode.Loose),
        0.0f,
        float.PositiveInfinity,
        0.0f,
        float.PositiveInfinity
    );

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/exact size propagation")]
public static void Case8(){

    Sizef parent_size = new Sizef(200.0f, 150.0f);

    Placement pin = Placement.Pin(
        new Offsetf(0.25f, 0.75f),
        Offsetf.Zero(),
        new Sizef(80.0f, 40.0f)
    );
    CheckSizef(RequireSize(pin, parent_size), 80.0f, 40.0f);
    CheckSizef(RequireSize(pin, Sizef.Zero()), 80.0f, 40.0f);

    Placement frame = new Placement(
        new Rectf(0.1f, 0.2f, 0.6f, 0.8f),
        new Sizef(-10.0f, -5.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    CheckSizef(RequireSize(frame, parent_size), 90.0f, 85.0f);

    Placement mixed = new Placement(
        new Rectf(0.0f, 0.25f, 0.5f, 0.25f),
        new Sizef(10.0f, 30.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    CheckSizef(RequireSize(mixed, parent_size), 110.0f, 30.0f);

    Placement outside = new Placement(
        new Rectf(-0.5f, -0.25f, 1.5f, 1.25f),
        new Sizef(-20.0f, -10.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    CheckSizef(RequireSize(outside, parent_size), 380.0f, 215.0f);

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/positive size failure contract")]
public static void Case9(){

    BoxConstraints parent = BoxConstraints.Tight(new Sizef(100.0f, 100.0f));

    Placement pin_without_size = Placement.Pin(Offsetf.Zero());
    Check.That(pin_without_size.IsValid());
    Check.False(
        pin_without_size.ResolveConstraints(parent, EPlacementSizeMode.Tight).HasValue
    );
    Check.False(
        pin_without_size.ResolveConstraints(parent, EPlacementSizeMode.Loose).HasValue
    );
    Check.False(pin_without_size.ResolveSize(parent.Biggest()).HasValue);

    Placement align_without_size = Placement.Align(Alignment.Center());
    Check.That(align_without_size.IsValid());
    Check.False(
        align_without_size.ResolveConstraints(parent, EPlacementSizeMode.Tight).HasValue
    );
    Check.False(align_without_size.ResolveSize(parent.Biggest()).HasValue);

    Placement zero_height_pin = Placement.Pin(
        Offsetf.Zero(),
        Offsetf.Zero(),
        new Sizef(20.0f, 0.0f)
    );
    Check.That(zero_height_pin.IsValid());
    Check.False(
        zero_height_pin.ResolveConstraints(parent, EPlacementSizeMode.Loose).HasValue
    );

    Placement collapsed_frame = new Placement(
        new Rectf(0.0f, 0.0f, 0.5f, 1.0f),
        new Sizef(-50.0f, 0.0f),
        Alignment.TopLeft(),
        Offsetf.Zero()
    );
    Check.That(collapsed_frame.IsValid());
    Check.False(
        collapsed_frame.ResolveConstraints(parent, EPlacementSizeMode.Tight).HasValue
    );
    Check.False(collapsed_frame.ResolveSize(parent.Biggest()).HasValue);

    Placement fill = Placement.Fill();
    BoxConstraints zero_width_parent = BoxConstraints.Tight(new Sizef(0.0f, 100.0f));
    Check.False(
        fill.ResolveConstraints(zero_width_parent, EPlacementSizeMode.Tight).HasValue
    );
    Check.False(fill.ResolveSize(zero_width_parent.Biggest()).HasValue);

}
[GuiTest("math/placement_tests.cpp::gui/math/Placement/placement")]
public static void Case10(){

    Placement outside_pin = Placement.Pin(
        new Offsetf(1.25f, -0.5f),
        new Offsetf(2.0f, 3.0f),
        new Sizef(20.0f, 10.0f),
        Alignment.Center()
    );
    CheckOffsetf(
        RequirePlaceOffset(outside_pin, new Sizef(100.0f, 50.0f), new Sizef(20.0f, 10.0f)),
        117.0f,
        -27.0f
    );
    CheckRectf(
        RequirePlaceRect(
            outside_pin,
            Rectf.LTWH(10.0f, 20.0f, 100.0f, 50.0f),
            new Sizef(20.0f, 10.0f)
        ),
        127.0f,
        -7.0f,
        147.0f,
        3.0f
    );

    Placement aligned = Placement.Align(
        Alignment.Center(),
        new Sizef(20.0f, 10.0f),
        new Offsetf(4.0f, -2.0f)
    );
    CheckOffsetf(
        RequirePlaceOffset(aligned, new Sizef(100.0f, 50.0f), new Sizef(20.0f, 10.0f)),
        44.0f,
        18.0f
    );

    Placement inset_left_top = new();
    inset_left_top.Inset(Alignment.TopLeft())
        .LeftPx(10.0f)
        .TopPx(20.0f)
        .RightPx(15.0f)
        .BottomPx(25.0f);
    CheckOffsetf(
        RequirePlaceOffset(inset_left_top, new Sizef(100.0f, 100.0f), new Sizef(30.0f, 10.0f)),
        10.0f,
        20.0f
    );

    Placement inset_center = new();
    inset_center.Inset(Alignment.Center())
        .LeftPx(10.0f)
        .TopPx(20.0f)
        .RightPx(15.0f)
        .BottomPx(25.0f);
    CheckOffsetf(
        RequirePlaceOffset(inset_center, new Sizef(100.0f, 100.0f), new Sizef(30.0f, 10.0f)),
        32.5f,
        42.5f
    );

    Placement inset_right_bottom = new();
    inset_right_bottom.Inset(Alignment.BottomRight())
        .LeftPx(10.0f)
        .TopPx(20.0f)
        .RightPx(15.0f)
        .BottomPx(25.0f);
    CheckOffsetf(
        RequirePlaceOffset(
            inset_right_bottom,
            new Sizef(100.0f, 100.0f),
            new Sizef(30.0f, 10.0f)
        ),
        55.0f,
        65.0f
    );

    Placement mixed = new Placement(
        new Rectf(0.25f, 1.0f, 0.75f, 1.0f),
        new Sizef(0.0f, 10.0f),
        Alignment.BottomCenter(),
        new Offsetf(2.0f, -3.0f)
    );
    CheckOffsetf(
        RequirePlaceOffset(mixed, new Sizef(100.0f, 100.0f), new Sizef(20.0f, 10.0f)),
        42.0f,
        87.0f
    );

}
}
