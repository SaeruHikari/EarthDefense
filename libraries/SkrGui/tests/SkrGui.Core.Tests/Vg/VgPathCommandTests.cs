using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
public sealed class VgPathCommandTests : VgTestHelpers
{
 static float signed_contour_area(VGPathFlatten flatten, VGPathFlattenContour contour)
{
    if (!contour.Closed || contour.NodeCount < 3u)
    {
        return 0.0f;
    }

    float area = 0.0f;
    Offsetf origin = flatten.Nodes()[(int)(contour.NodeBegin)].Position;
    for (uint i = 2u; i < contour.NodeCount; ++i)
    {
        Offsetf b = flatten.Nodes()[(int)(contour.NodeBegin + i - 1u)].Position;
        Offsetf c = flatten.Nodes()[(int)(contour.NodeBegin + i)].Position;
        area += (b - origin).Cross(c - origin);
    }
    return area;
}
 [GuiTest("gui/vg/VGPath command writes validate state")] public static void Case1()
{
    VGPath path = new();
    True(path.IsEmpty());
    True(path.State() == EVGPathCommandState.Rest);
    False(path.CursorPos());

    path.MoveTo(new Offsetf(0.0f, 0.0f));
    True(path.State() == EVGPathCommandState.PrepareDraw);
    check_offsetf(path.CursorPos()!.Value, 0.0f, 0.0f);
    path.LineTo(new Offsetf(1.0f, 0.0f));
    True(path.State() == EVGPathCommandState.Drawing);
    check_offsetf(path.CursorPos()!.Value, 1.0f, 0.0f);
    path.QuadTo(new Offsetf(2.0f, 1.0f), new Offsetf(3.0f, 0.0f));
    True(path.State() == EVGPathCommandState.Drawing);
    check_offsetf(path.CursorPos()!.Value, 3.0f, 0.0f);
    path.CubicTo(new Offsetf(4.0f, 1.0f), new Offsetf(5.0f, -1.0f), new Offsetf(6.0f, 0.0f));
    True(path.State() == EVGPathCommandState.Drawing);
    check_offsetf(path.CursorPos()!.Value, 6.0f, 0.0f);
    path.Close();
    True(path.State() == EVGPathCommandState.Rest);
    False(path.CursorPos());
    True(path.Commands().Count == 5u);
    False(path.Commands()[(int)(4)].Closing.WindingHint);
}
 [GuiTest("gui/vg/VGPath close stores winding hint in close command data")] public static void Case2()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 10.0f));
    path.Close(EVGPathWinding.CCW);

    True(path.Commands().Count == 4u);
    True(path.Commands()[(int)(3)].Type == EVGPathCommandType.Close);
    True(path.Commands()[(int)(3)].Closing.WindingHint);
    True(path.Commands()[(int)(3)].Closing.WindingHint!.Value == EVGPathWinding.CCW);
}
 [GuiTest("gui/vg/VGPathFlatten finalize enforces close winding hint")] public static void Case3()
{
    VGPath cw_path = new();
    cw_path.MoveTo(new Offsetf(0.0f, 0.0f));
    cw_path.LineTo(new Offsetf(10.0f, 0.0f));
    cw_path.LineTo(new Offsetf(10.0f, 10.0f));
    cw_path.LineTo(new Offsetf(0.0f, 10.0f));
    cw_path.Close();

    VGPathFlatten cw_flat = cw_path.Flatten(new VGPathFlattenOptions());
    True(cw_flat.Contours().Count == 1u);
    True(cw_flat.Contours()[(int)(0)].Closed);
    False(cw_flat.Contours()[(int)(0)].WindingHint);
    True(signed_contour_area(cw_flat, cw_flat.Contours()[(int)(0)]) > 0.0f);

    VGPath ccw_hint_path = new();
    ccw_hint_path.MoveTo(new Offsetf(0.0f, 0.0f));
    ccw_hint_path.LineTo(new Offsetf(10.0f, 0.0f));
    ccw_hint_path.LineTo(new Offsetf(10.0f, 10.0f));
    ccw_hint_path.LineTo(new Offsetf(0.0f, 10.0f));
    ccw_hint_path.Close(EVGPathWinding.CCW);

    VGPathFlatten ccw_flat = ccw_hint_path.Flatten(new VGPathFlattenOptions());
    True(ccw_flat.Contours().Count == 1u);
    True(ccw_flat.Contours()[(int)(0)].Closed);
    True(ccw_flat.Contours()[(int)(0)].WindingHint);
    True(ccw_flat.Contours()[(int)(0)].WindingHint!.Value == EVGPathWinding.CCW);
    True(signed_contour_area(ccw_flat, ccw_flat.Contours()[(int)(0)]) < 0.0f);
}
 [GuiTest("gui/vg/VGPath invalid and degenerate writes leave command buffer unchanged")] public static void Case4()
{
    const float k_inf = float.PositiveInfinity;

    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    True(path.Commands().Count == 1u);
    check_offsetf(path.CursorPos()!.Value, 0.0f, 0.0f);

#if !DEBUG
    path.LineTo(new Offsetf(k_inf, 0.0f));
    path.LineTo(new Offsetf(0.0f, 0.0f));
    path.QuadTo(new Offsetf(k_inf, 0.0f), new Offsetf(1.0f, 0.0f));
    path.CubicTo(new Offsetf(1.0f, 0.0f), new Offsetf(k_inf, 0.0f), new Offsetf(2.0f, 0.0f));
    path.ArcTo(new Offsetf(1.0f, 0.0f), new Offsetf(2.0f, 0.0f), k_inf);
    path.EllipticalArcTo(
        new Offsetf(2.0f, 0.0f),
        new Offsetf(k_inf, 1.0f),
        0.0f,
        EEllipticalArcSize.Small,
        EEllipticalArcSweep.CW
    );
    True(path.Commands().Count == 1u);
    check_offsetf(path.CursorPos()!.Value, 0.0f, 0.0f);
#else


    _ = k_inf;
#endif

    path.AddRect(Rectf.LTWH(1.0f, 1.0f, 0.0f, 8.0f));
    path.AddCircle(Circle.CenterRadius(new Offsetf(4.0f, 4.0f), 0.0f));
    path.AddEllipse(Ellipse.CenterRadius(new Offsetf(4.0f, 4.0f), 6.0f, 0.0f));
    True(path.Commands().Count == 1u);
    check_offsetf(path.CursorPos()!.Value, 0.0f, 0.0f);

    path.LineTo(new Offsetf(4.0f, 0.0f));
    path.Close();
    True(path.Commands().Count == 3u);
    True(path.State() == EVGPathCommandState.Rest);

#if !DEBUG
    path.Close();
    True(path.Commands().Count == 3u);
    True(path.State() == EVGPathCommandState.Rest);
#endif
}
 [GuiTest("gui/vg/VGPath close after pending MoveTo ignores empty contour and finalizes drawn contour")] public static void Case5()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.MoveTo(new Offsetf(20.0f, 0.0f));
    True(path.State() == EVGPathCommandState.PrepareDraw);
    check_offsetf(path.CursorPos()!.Value, 20.0f, 0.0f);

    path.Close();
    True(path.State() == EVGPathCommandState.Rest);
    False(path.CursorPos());
    True(path.Commands().Count == 4u);
    True(path.Commands()[(int)(3)].Type == EVGPathCommandType.Close);

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.IsFinalized());
    True(flat.Nodes().Count == 2u);
    True(flat.Contours().Count == 1u);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 10.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 10.0f, 0.0f, 10.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
    check_contour(flat.Contours()[(int)(0)], 0u, 2u, 10.0f, false);
}
 [GuiTest("gui/vg/VGPath consecutive MoveTo normalizes contours")] public static void Case6()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.MoveTo(new Offsetf(20.0f, 0.0f));
    path.MoveTo(new Offsetf(30.0f, 0.0f));
    path.LineTo(new Offsetf(40.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 4u);
    True(flat.Contours().Count == 2u);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 10.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 10.0f, 0.0f, 10.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(2)], 30.0f, 0.0f, 0.0f, 10.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(3)], 40.0f, 0.0f, 10.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
    check_contour(flat.Contours()[(int)(0)], 0u, 2u, 10.0f, false);
    check_contour(flat.Contours()[(int)(1)], 2u, 2u, 10.0f, false);
}
}
