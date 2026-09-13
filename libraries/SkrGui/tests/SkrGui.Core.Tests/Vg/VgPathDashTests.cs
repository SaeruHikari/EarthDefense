using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
public sealed class VgPathDashTests : VgTestHelpers
{
 [GuiTest("gui/vg/VGPathFlatten dash_to cuts open contours by distance")] public static void Case1()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());

    float[] values = { 3.0f, 2.0f };
    VGPathFlatten dashed = new();
    True(flat.DashTo(dashed,new VGDashPattern{Values=values,Offset=0.0f}));

    True(dashed.Nodes().Count == 4u);
    True(dashed.Contours().Count == 2u);
    check_contour(dashed.Contours()[(int)(0)], 0u, 2u, 3.0f, false);
    check_contour(dashed.Contours()[(int)(1)], 2u, 2u, 3.0f, false);
    check_node(
        dashed.Nodes()[(int)(0)],
        0.0f,
        0.0f,
        0.0f,
        3.0f,
        EVGPathFlattenNodeFlags.Corner | EVGPathFlattenNodeFlags.DashCutStart
    );
    check_node(dashed.Nodes()[(int)(1)], 3.0f, 0.0f, 3.0f, 0.0f, EVGPathFlattenNodeFlags.DashCutEnd);
    check_node(dashed.Nodes()[(int)(2)], 5.0f, 0.0f, 0.0f, 3.0f, EVGPathFlattenNodeFlags.DashCutStart);
    check_node(dashed.Nodes()[(int)(3)], 8.0f, 0.0f, 3.0f, 0.0f, EVGPathFlattenNodeFlags.DashCutEnd);
}
 [GuiTest("gui/vg/VGPathFlatten need_dash detects effective dash work")] public static void Case2()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());

    float[] cut_values = { 3.0f, 2.0f };
    True(flat.NeedDash(new VGDashPattern{Values=cut_values,Offset=0.0f}));

    float[] cover_values = { 12.0f, 4.0f };
    False(flat.NeedDash(new VGDashPattern{Values=cover_values,Offset=0.0f}));
    True(flat.NeedDash(new VGDashPattern{Values=cover_values,Offset=11.0f}));

    float[] zero_values = { 0.0f, 0.0f };
    False(flat.NeedDash(new VGDashPattern{Values=zero_values,Offset=0.0f}));

    float[] solid_values = { 3.0f, 0.0f };
    False(flat.NeedDash(new VGDashPattern{Values=solid_values,Offset=0.0f}));

    float[] no_on_values = { 0.0f, 4.0f };
    True(flat.NeedDash(new VGDashPattern{Values=no_on_values,Offset=0.0f}));

    VGPath empty_path = new();
    VGPathFlatten empty = empty_path.Flatten(new VGPathFlattenOptions());
    True(empty.IsFinalized());
    False(empty.NeedDash(new VGDashPattern{Values=cut_values,Offset=0.0f}));
}
 [GuiTest("gui/vg/VGPathFlatten dash_to merges closed contour seam interval")] public static void Case3()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(3.0f, 0.0f));
    path.LineTo(new Offsetf(3.0f, 4.0f));
    path.Close();

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());

    float[] values = { 3.0f, 6.0f };
    VGPathFlatten dashed = new();
    True(flat.DashTo(dashed,new VGDashPattern{Values=values,Offset=0.0f}));

    True(dashed.Nodes().Count == 3u);
    True(dashed.Contours().Count == 1u);
    check_contour(dashed.Contours()[(int)(0)], 0u, 3u, 6.0f, false);
    check_node(dashed.Nodes()[(int)(0)], 1.8f, 2.4f, 0.0f, 3.0f, EVGPathFlattenNodeFlags.DashCutStart);
    check_node(dashed.Nodes()[(int)(1)], 0.0f, 0.0f, 3.0f, 3.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(dashed.Nodes()[(int)(2)], 3.0f, 0.0f, 6.0f, 0.0f, EVGPathFlattenNodeFlags.Corner | EVGPathFlattenNodeFlags.DashCutEnd);
}
 [GuiTest("gui/vg/VGPathFlatten dash_to normalizes pattern and offset")] public static void Case4()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());

    float[] odd_values = { 2.0f, 1.0f, 3.0f };
    VGPathFlatten odd_dashed = new();
    True(flat.DashTo(odd_dashed,new VGDashPattern{Values=odd_values,Offset=0.0f}));
    True(odd_dashed.Contours().Count == 3u);
    check_offsetf(odd_dashed.Nodes()[(int)(0)].Position, 0.0f, 0.0f);
    check_offsetf(odd_dashed.Nodes()[(int)(1)].Position, 2.0f, 0.0f);
    check_offsetf(odd_dashed.Nodes()[(int)(2)].Position, 3.0f, 0.0f);
    check_offsetf(odd_dashed.Nodes()[(int)(3)].Position, 6.0f, 0.0f);
    check_offsetf(odd_dashed.Nodes()[(int)(4)].Position, 8.0f, 0.0f);
    check_offsetf(odd_dashed.Nodes()[(int)(5)].Position, 9.0f, 0.0f);

    float[] offset_values = { 4.0f, 2.0f };
    VGPathFlatten offset_dashed = new();
    True(flat.DashTo(offset_dashed,new VGDashPattern{Values=offset_values,Offset=-1.0f}));
    True(offset_dashed.Contours().Count == 2u);
    check_offsetf(offset_dashed.Nodes()[(int)(0)].Position, 1.0f, 0.0f);
    check_offsetf(offset_dashed.Nodes()[(int)(1)].Position, 5.0f, 0.0f);
    check_offsetf(offset_dashed.Nodes()[(int)(2)].Position, 7.0f, 0.0f);
    check_offsetf(offset_dashed.Nodes()[(int)(3)].Position, 10.0f, 0.0f);
}
 [GuiTest("gui/vg/VGPathFlatten dash_to resets pattern per contour and keeps exact cuts")] public static void Case5()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.MoveTo(new Offsetf(0.0f, 10.0f));
    path.LineTo(new Offsetf(10.0f, 10.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());

    float[] values = { 4.0f, 2.0f };
    VGPathFlatten dashed = new();
    True(flat.DashTo(dashed,new VGDashPattern{Values=values,Offset=0.0f}));

    True(dashed.Nodes().Count == 8u);
    True(dashed.Contours().Count == 4u);
    check_contour(dashed.Contours()[(int)(0)], 0u, 2u, 4.0f, false);
    check_contour(dashed.Contours()[(int)(1)], 2u, 2u, 4.0f, false);
    check_contour(dashed.Contours()[(int)(2)], 4u, 2u, 4.0f, false);
    check_contour(dashed.Contours()[(int)(3)], 6u, 2u, 4.0f, false);
    check_offsetf(dashed.Nodes()[(int)(0)].Position, 0.0f, 0.0f);
    check_offsetf(dashed.Nodes()[(int)(1)].Position, 4.0f, 0.0f);
    check_offsetf(dashed.Nodes()[(int)(2)].Position, 6.0f, 0.0f);
    check_offsetf(dashed.Nodes()[(int)(3)].Position, 10.0f, 0.0f);
    check_offsetf(dashed.Nodes()[(int)(4)].Position, 0.0f, 10.0f);
    check_offsetf(dashed.Nodes()[(int)(5)].Position, 4.0f, 10.0f);
    check_offsetf(dashed.Nodes()[(int)(6)].Position, 6.0f, 10.0f);
    check_offsetf(dashed.Nodes()[(int)(7)].Position, 10.0f, 10.0f);
}
 [GuiTest("gui/vg/VGPathFlatten dash_to handles none and degenerate patterns")] public static void Case6()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    VGFlattenSnapshot snapshot = snapshot_flatten(flat);

    VGPathFlatten no_dash = new();
    True(no_dash.IsFinalized());
    True(flat.DashTo(no_dash, new()));
    True(no_dash.IsFinalized());
    check_flatten_snapshot(no_dash, snapshot);
    True(no_dash.Contours()[(int)(0)].TotalLength == flat.Contours()[(int)(0)].TotalLength);

    float[] zero_values = { 0.0f, 0.0f };
    VGPathFlatten zero_dash = new();
    True(flat.DashTo(zero_dash,new VGDashPattern{Values=zero_values,Offset=0.0f}));
    True(zero_dash.IsFinalized());
    check_flatten_snapshot(zero_dash, snapshot);

    float[] solid_values = { 3.0f, 0.0f };
    VGPathFlatten solid_dash = new();
    True(flat.DashTo(solid_dash,new VGDashPattern{Values=solid_values,Offset=0.0f}));
    True(solid_dash.IsFinalized());
    check_flatten_snapshot(solid_dash, snapshot);

    float[] no_on_values = { 0.0f, 4.0f };
    VGPathFlatten empty_dash = new();
    True(flat.DashTo(empty_dash,new VGDashPattern{Values=no_on_values,Offset=0.0f}));
    True(empty_dash.IsFinalized());
    True(empty_dash.IsEmpty());
}
}
