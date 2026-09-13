using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
public sealed class VgPathShapeTests : VgTestHelpers
{
 [GuiTest("gui/vg/VGPath arc primitives lower to cubic commands")] public static void Case1()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.ArcTo(new Offsetf(50.0f, 0.0f), new Offsetf(50.0f, 50.0f), 20.0f);
    True(path.Commands().Count >= 3u);
    True(path.Commands()[(int)(0)].Type == EVGPathCommandType.MoveTo);
    True(path.Commands()[(int)(1)].Type == EVGPathCommandType.LineTo);
    True(path.Commands()[(int)(path.Commands().Count - 1u)].Type == EVGPathCommandType.CubicTo);
    True(path.CursorPos());

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Contours().Count == 1u);
    True(flat.Nodes().Count >= 3u);
    check_offsetf(flat.Nodes()[(int)(0)].Position, 0.0f, 0.0f);
}
 [GuiTest("gui/vg/VGPath elliptical arc primitive lowers to cubic commands")] public static void Case2()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.EllipticalArcTo(
        new Offsetf(100.0f, 0.0f),
        new Offsetf(60.0f, 30.0f),
        0.25f,
        EEllipticalArcSize.Large,
        EEllipticalArcSweep.CW
    );

    True(path.Commands().Count >= 2u);
    True(path.Commands()[(int)(0)].Type == EVGPathCommandType.MoveTo);
    True(path.Commands()[(int)(1)].Type == EVGPathCommandType.CubicTo);
    check_offsetf(path.CursorPos()!.Value, 100.0f, 0.0f);

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Contours().Count == 1u);
    True(flat.Nodes().Count >= 3u);
    check_offsetf(flat.Nodes()[(int)(0)].Position, 0.0f, 0.0f);
    check_offsetf(flat.Nodes()[(int)(flat.Nodes().Count - 1u)].Position, 100.0f, 0.0f);
}
 [GuiTest("gui/vg/VGPath add shape primitives build closed contours")] public static void Case3()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));
    path.AddCircle(Circle.CenterRadius(new Offsetf(30.0f, 10.0f), 6.0f));
    path.AddEllipse(Ellipse.CenterRadius(new Offsetf(60.0f, 10.0f), 12.0f, 6.0f, 0.2f));
    path.AddSuperellipse(Superellipse.CenterRadius(new Offsetf(90.0f, 10.0f), 14.0f, 8.0f, 0.1f * kPi, 4.5f));
    path.AddRrect(RRect.FromRectRadius(Rectf.LTWH(110.0f, 0.0f, 20.0f, 14.0f), Radius.Circular(4.0f)));
    path.AddSmoothRrect(
        RRect.FromRectRadius(Rectf.LTWH(140.0f, 0.0f, 24.0f, 16.0f), Radius.Circular(6.0f)),
        4.5f
    );

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Contours().Count == 6u);
    True(flat.Contours()[(int)(0)].Closed);
    True(flat.Contours()[(int)(1)].Closed);
    True(flat.Contours()[(int)(2)].Closed);
    True(flat.Contours()[(int)(3)].Closed);
    True(flat.Contours()[(int)(4)].Closed);
    True(flat.Contours()[(int)(5)].Closed);
    True(flat.Nodes().Count >= 12u);
    True(flat.Bounds().Left <= 0.0f);
    True(flat.Bounds().Top <= 0.0f);
    True(flat.Bounds().Right >= 164.0f);
    True(flat.Bounds().Bottom >= 16.0f);
}
 [GuiTest("gui/vg/VGPath closed shape primitives support explicit winding")] public static void Case4()
{
    VGPath rect_cw = new();
    rect_cw.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));
    True(rect_cw.Commands().Count == 5u);
    check_offsetf(rect_cw.Commands()[(int)(1)].Line.Target, 10.0f, 0.0f);
    check_offsetf(rect_cw.Commands()[(int)(2)].Line.Target, 10.0f, 8.0f);
    check_offsetf(rect_cw.Commands()[(int)(3)].Line.Target, 0.0f, 8.0f);

    VGPath rect_ccw = new();
    rect_ccw.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f), EVGPathWinding.CCW);
    True(rect_ccw.Commands().Count == 5u);
    check_offsetf(rect_ccw.Commands()[(int)(1)].Line.Target, 0.0f, 8.0f);
    check_offsetf(rect_ccw.Commands()[(int)(2)].Line.Target, 10.0f, 8.0f);
    check_offsetf(rect_ccw.Commands()[(int)(3)].Line.Target, 10.0f, 0.0f);

    VGPath circle_cw = new();
    circle_cw.AddCircle(Circle.CenterRadius(Offsetf.Zero(), 10.0f));
    VGPath circle_ccw = new();
    circle_ccw.AddCircle(Circle.CenterRadius(Offsetf.Zero(), 10.0f), EVGPathWinding.CCW);
    True(circle_cw.Commands().Count == circle_ccw.Commands().Count);
    True(circle_cw.Commands()[(int)(1)].Type == EVGPathCommandType.CubicTo);
    True(circle_ccw.Commands()[(int)(1)].Type == EVGPathCommandType.CubicTo);
    True(circle_cw.Commands()[(int)(1)].Cubic.Control0.Y > 0.0f);
    True(circle_ccw.Commands()[(int)(1)].Cubic.Control0.Y < 0.0f);

    VGPath ellipse_cw = new();
    ellipse_cw.AddEllipse(Ellipse.CenterRadius(Offsetf.Zero(), 12.0f, 6.0f));
    VGPath ellipse_ccw = new();
    ellipse_ccw.AddEllipse(Ellipse.CenterRadius(Offsetf.Zero(), 12.0f, 6.0f), EVGPathWinding.CCW);
    True(ellipse_cw.Commands().Count == ellipse_ccw.Commands().Count);
    True(ellipse_cw.Commands()[(int)(1)].Cubic.Control0.Y > 0.0f);
    True(ellipse_ccw.Commands()[(int)(1)].Cubic.Control0.Y < 0.0f);

    VGPath superellipse_cw = new();
    superellipse_cw.AddSuperellipse(Superellipse.CenterRadius(Offsetf.Zero(), 12.0f, 6.0f, 0.0f, 4.5f));
    VGPath superellipse_ccw = new();
    superellipse_ccw.AddSuperellipse(
        Superellipse.CenterRadius(Offsetf.Zero(), 12.0f, 6.0f, 0.0f, 4.5f),
        EVGPathWinding.CCW
    );
    True(superellipse_cw.Commands().Count == superellipse_ccw.Commands().Count);
    True(superellipse_cw.Commands()[(int)(1)].Cubic.Control0.Y > 0.0f);
    True(superellipse_ccw.Commands()[(int)(1)].Cubic.Control0.Y < 0.0f);

    VGPath rrect_cw = new();
    rrect_cw.AddRrect(RRect.FromRectRadius(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f), Radius.Circular(3.0f)));
    VGPath rrect_ccw = new();
    rrect_ccw.AddRrect(
        RRect.FromRectRadius(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f), Radius.Circular(3.0f)),
        EVGPathWinding.CCW
    );
    True(rrect_cw.Commands().Count == rrect_ccw.Commands().Count);
    True(rrect_cw.Commands()[(int)(1)].Type == EVGPathCommandType.LineTo);
    True(rrect_ccw.Commands()[(int)(1)].Type == EVGPathCommandType.CubicTo);
    check_offsetf(rrect_cw.Commands()[(int)(1)].Line.Target, 9.0f, 0.0f);
    True(rrect_ccw.Commands()[(int)(1)].Cubic.Control0.X < 3.0f);

    VGPath capsule = new();
    capsule.AddRrect(RRect.FromRectRadius(Rectf.LTWH(0.0f, 0.0f, 32.0f, 16.0f), Radius.Circular(16.0f)));
    VGPathFlatten capsule_flat = capsule.Flatten(new VGPathFlattenOptions());
    True(capsule_flat.Contours().Count == 1u);
    True(capsule_flat.Contours()[(int)(0)].Closed);
    True(capsule_flat.Bounds().Left <= 0.0f);
    True(capsule_flat.Bounds().Right >= 32.0f);

    VGPath smooth_cw = new();
    smooth_cw.AddSmoothRrect(
        RRect.FromRectRadius(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f), Radius.Circular(3.0f)),
        4.5f
    );
    VGPath smooth_ccw = new();
    smooth_ccw.AddSmoothRrect(
        RRect.FromRectRadius(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f), Radius.Circular(3.0f)),
        4.5f,
        EVGPathWinding.CCW
    );
    True(smooth_cw.Commands().Count == smooth_ccw.Commands().Count);
    True(smooth_cw.Commands()[(int)(1)].Type == EVGPathCommandType.LineTo);
    True(smooth_ccw.Commands()[(int)(1)].Type == EVGPathCommandType.CubicTo);
    check_offsetf(smooth_cw.Commands()[(int)(1)].Line.Target, 9.0f, 0.0f);
    True(smooth_ccw.Commands()[(int)(1)].Cubic.Control0.X < 3.0f);
}
}
