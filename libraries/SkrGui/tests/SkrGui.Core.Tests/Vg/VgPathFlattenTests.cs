using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
public sealed class VgPathFlattenTests : VgTestHelpers
{
 [GuiTest("gui/vg/VGPath open line flatten builds cache metadata")] public static void Case1()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(1.0f, 2.0f));
    path.LineTo(new Offsetf(4.0f, 6.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());

    False(flat.IsEmpty());
    True(flat.Nodes().Count == 2u);
    True(flat.Contours().Count == 1u);
    check_rectf(flat.Bounds(), 1.0f, 2.0f, 4.0f, 6.0f);
    check_node(flat.Nodes()[(int)(0)], 1.0f, 2.0f, 0.0f, 5.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 4.0f, 6.0f, 5.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
    check_node_direction(flat.Nodes()[(int)(0)], 0.6f, 0.8f, 0.0f, 0.0f);
    check_node_direction(flat.Nodes()[(int)(1)], 0.0f, 0.0f, 0.0f, 0.0f);
    check_contour(flat.Contours()[(int)(0)], 0u, 2u, 5.0f, false);
}
 [GuiTest("gui/vg/VGPath MoveTo finalizes multiple open contours")] public static void Case2()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.MoveTo(new Offsetf(20.0f, 0.0f));
    path.LineTo(new Offsetf(20.0f, 5.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 4u);
    True(flat.Contours().Count == 2u);
    check_contour(flat.Contours()[(int)(0)], 0u, 2u, 10.0f, false);
    check_contour(flat.Contours()[(int)(1)], 2u, 2u, 5.0f, false);
    check_rectf(flat.Bounds(), 0.0f, 0.0f, 20.0f, 5.0f);
}
 [GuiTest("gui/vg/VGPath flatten marks line-line left turn join flag and direction")] public static void Case3()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 1.0f));
    path.LineTo(new Offsetf(1.0f, 1.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 3u);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 1.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(
        flat.Nodes()[(int)(1)],
        0.0f,
        1.0f,
        1.0f,
        1.0f,
        EVGPathFlattenNodeFlags.Corner | EVGPathFlattenNodeFlags.Left
    );
    check_node(flat.Nodes()[(int)(2)], 1.0f, 1.0f, 2.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
    check_node_direction(flat.Nodes()[(int)(0)], 0.0f, 1.0f, 0.0f, 0.0f);
    check_node_direction(flat.Nodes()[(int)(1)], 1.0f, 0.0f, 1.0f, -1.0f);
    check_node_direction(flat.Nodes()[(int)(2)], 0.0f, 0.0f, 0.0f, 0.0f);
}
 [GuiTest("gui/vg/VGPath close adds closing edge without duplicating start")] public static void Case4()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(3.0f, 0.0f));
    path.LineTo(new Offsetf(3.0f, 4.0f));
    path.Close();

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 3u);
    True(flat.Contours().Count == 1u);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 3.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 3.0f, 0.0f, 3.0f, 4.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(
        flat.Nodes()[(int)(2)],
        3.0f,
        4.0f,
        7.0f,
        5.0f,
        EVGPathFlattenNodeFlags.Corner
    );
    check_node_direction(flat.Nodes()[(int)(0)], 1.0f, 0.0f, -2.0f, -1.0f);
    check_node_direction(flat.Nodes()[(int)(1)], 0.0f, 1.0f, 1.0f, -1.0f);
    check_node_direction(flat.Nodes()[(int)(2)], -0.6f, -0.8f, 1.0f, 3.0f);
    check_contour(flat.Contours()[(int)(0)], 0u, 3u, 12.0f, true, true);
    check_rectf(flat.Bounds(), 0.0f, 0.0f, 3.0f, 4.0f);
}
 [GuiTest("gui/vg/VGPath close removes explicit repeated start tail")] public static void Case5()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(5.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 0.0f));
    path.Close();

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 0);
    True(flat.Contours().Count == 0);
}
 [GuiTest("gui/vg/VGPathFlatten remove_collinear_segments simplifies open contour")] public static void Case6()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(30.0f, 0.0f));
    path.LineTo(new Offsetf(65.0f, 0.0f));
    path.LineTo(new Offsetf(100.0f, 0.0f));
    path.LineTo(new Offsetf(150.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 5u);
    True(flat.RemoveCollinearSegments() == 3u);
    True(flat.Nodes().Count == 2u);
    True(flat.Contours().Count == 1u);
    check_contour(flat.Contours()[(int)(0)], 0u, 2u, 150.0f, false);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 150.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 150.0f, 0.0f, 150.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
    check_node_direction(flat.Nodes()[(int)(0)], 1.0f, 0.0f, 0.0f, 0.0f);
    check_node_direction(flat.Nodes()[(int)(1)], 0.0f, 0.0f, 0.0f, 0.0f);
    check_rectf(flat.Bounds(), 0.0f, 0.0f, 150.0f, 0.0f);
}
 [GuiTest("gui/vg/VGPathFlatten remove_collinear_segments simplifies closed contour")] public static void Case7()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.LineTo(new Offsetf(20.0f, 0.0f));
    path.LineTo(new Offsetf(20.0f, 10.0f));
    path.LineTo(new Offsetf(20.0f, 20.0f));
    path.LineTo(new Offsetf(0.0f, 20.0f));
    path.Close();

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 6u);
    True(flat.RemoveCollinearSegments(0.001f) == 2u);
    True(flat.Nodes().Count == 4u);
    True(flat.Contours().Count == 1u);
    check_contour(flat.Contours()[(int)(0)], 0u, 4u, 80.0f, true, true);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 20.0f, 0.0f, 20.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(2)], 20.0f, 20.0f, 40.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(3)], 0.0f, 20.0f, 60.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_rectf(flat.Bounds(), 0.0f, 0.0f, 20.0f, 20.0f);
}
 [GuiTest("gui/vg/VGPathFlatten remove_collinear_segments can remove closed contour first node")] public static void Case8()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(10.0f, 0.0f));
    path.LineTo(new Offsetf(20.0f, 0.0f));
    path.LineTo(new Offsetf(20.0f, 20.0f));
    path.LineTo(new Offsetf(0.0f, 20.0f));
    path.LineTo(new Offsetf(0.0f, 0.0f));
    path.Close();

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 5u);
    True(flat.RemoveCollinearSegments(0.001f) == 1u);
    True(flat.Nodes().Count == 4u);
    True(flat.Contours().Count == 1u);
    check_contour(flat.Contours()[(int)(0)], 0u, 4u, 80.0f, true, true);
    check_node(flat.Nodes()[(int)(0)], 20.0f, 0.0f, 0.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 20.0f, 20.0f, 20.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(2)], 0.0f, 20.0f, 40.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(3)], 0.0f, 0.0f, 60.0f, 20.0f, EVGPathFlattenNodeFlags.Corner);
    check_rectf(flat.Bounds(), 0.0f, 0.0f, 20.0f, 20.0f);
}
 [GuiTest("gui/vg/VGPathFlatten remove_collinear_segments respects tolerance")] public static void Case9()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(50.0f, 0.04f));
    path.LineTo(new Offsetf(100.0f, 0.0f));

    VGPathFlatten low_tolerance = path.Flatten(new VGPathFlattenOptions());
    True(low_tolerance.RemoveCollinearSegments(0.0001f) == 0u);
    True(low_tolerance.Nodes().Count == 3u);

    VGPathFlatten high_tolerance = path.Flatten(new VGPathFlattenOptions());
    True(high_tolerance.RemoveCollinearSegments(0.01f) == 1u);
    True(high_tolerance.Nodes().Count == 2u);
    check_node(high_tolerance.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 100.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(high_tolerance.Nodes()[(int)(1)], 100.0f, 0.0f, 100.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
}
 [GuiTest("gui/vg/VGPathFlatten remove_collinear_segments keeps opposite collinear turn")] public static void Case10()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(50.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.RemoveCollinearSegments() == 0u);
    True(flat.Nodes().Count == 3u);
}
 [GuiTest("gui/vg/VGPathFlatten add_line_intersections inserts sorted open contour vertices")] public static void Case11()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(100.0f, 0.0f));

    VGPathFlattenLine[] lines = {
        new VGPathFlattenLine{Position=new Offsetf(75.0f, -10.0f),Direction=new Offsetf(0.0f, 1.0f)},
        new VGPathFlattenLine{Position=new Offsetf(25.0f, -10.0f),Direction=new Offsetf(0.0f, 1.0f)},
        new VGPathFlattenLine{Position=new Offsetf(50.0f, -10.0f),Direction=new Offsetf(0.0f, 1.0f)},
    };

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.AddLineIntersections(lines) == 3u);
    True(flat.Nodes().Count == 5u);
    True(flat.Contours().Count == 1u);
    check_contour(flat.Contours()[(int)(0)], 0u, 5u, 100.0f, false);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 25.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 25.0f, 0.0f, 25.0f, 25.0f, EVGPathFlattenNodeFlags.None);
    check_node(flat.Nodes()[(int)(2)], 50.0f, 0.0f, 50.0f, 25.0f, EVGPathFlattenNodeFlags.None);
    check_node(flat.Nodes()[(int)(3)], 75.0f, 0.0f, 75.0f, 25.0f, EVGPathFlattenNodeFlags.None);
    check_node(flat.Nodes()[(int)(4)], 100.0f, 0.0f, 100.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
    check_rectf(flat.Bounds(), 0.0f, 0.0f, 100.0f, 0.0f);
}
 [GuiTest("gui/vg/VGPathFlatten add_line_intersections splits closed contour closing edge")] public static void Case12()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 10.0f));
    path.LineTo(new Offsetf(0.0f, 10.0f));
    path.Close();

    VGPathFlattenLine[] lines = {
        new VGPathFlattenLine{Position=new Offsetf(-5.0f, 5.0f),Direction=new Offsetf(1.0f, 0.0f)},
    };

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.AddLineIntersections(lines) == 2u);
    True(flat.Nodes().Count == 6u);
    True(flat.Contours().Count == 1u);
    check_contour(flat.Contours()[(int)(0)], 0u, 6u, 40.0f, true, true);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 10.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 10.0f, 0.0f, 10.0f, 5.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(2)], 10.0f, 5.0f, 15.0f, 5.0f, EVGPathFlattenNodeFlags.None);
    check_node(flat.Nodes()[(int)(3)], 10.0f, 10.0f, 20.0f, 10.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(4)], 0.0f, 10.0f, 30.0f, 5.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(5)], 0.0f, 5.0f, 35.0f, 5.0f, EVGPathFlattenNodeFlags.None);
    check_rectf(flat.Bounds(), 0.0f, 0.0f, 10.0f, 10.0f);
}
 [GuiTest("gui/vg/VGPathFlatten add_line_intersections skips degenerate line cases")] public static void Case13()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(100.0f, 0.0f));

    VGPathFlattenLine[] lines = {
        new VGPathFlattenLine{Position=new Offsetf(50.0f, 0.0f),Direction=new Offsetf(1.0f, 0.0f)},
        new VGPathFlattenLine{Position=new Offsetf(50.0f, 5.0f),Direction=new Offsetf(1.0f, 0.0f)},
        new VGPathFlattenLine{Position=new Offsetf(0.0f, -10.0f),Direction=new Offsetf(0.0f, 1.0f)},
        new VGPathFlattenLine{Position=new Offsetf(100.0f, -10.0f),Direction=new Offsetf(0.0f, 1.0f)},
        new VGPathFlattenLine{Position=new Offsetf(50.0f, -10.0f),Direction=new Offsetf(0.0f, 1.0f)},
        new VGPathFlattenLine{Position=new Offsetf(50.0f, 0.0f),Direction=new Offsetf(1.0f, 1.0f)},
        new VGPathFlattenLine{Position=new Offsetf(25.0f, 0.0f),Direction=new Offsetf(0.0f, 0.0f)},
    };

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.AddLineIntersections(lines) == 1u);
    True(flat.Nodes().Count == 3u);
    True(flat.Contours().Count == 1u);
    check_contour(flat.Contours()[(int)(0)], 0u, 3u, 100.0f, false);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 50.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 50.0f, 0.0f, 50.0f, 50.0f, EVGPathFlattenNodeFlags.None);
    check_node(flat.Nodes()[(int)(2)], 100.0f, 0.0f, 100.0f, 0.0f, EVGPathFlattenNodeFlags.Corner);
}
 [GuiTest("gui/vg/VGPath flatten preserves explicit contour winding")] public static void Case14()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 4.0f));
    path.LineTo(new Offsetf(4.0f, 0.0f));
    path.Close();

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 3u);
    check_node(
        flat.Nodes()[(int)(0)],
        0.0f,
        0.0f,
        0.0f,
        4.0f,
        EVGPathFlattenNodeFlags.Corner | EVGPathFlattenNodeFlags.Left
    );
    check_node(
        flat.Nodes()[(int)(1)],
        0.0f,
        4.0f,
        4.0f,
        5.656854f,
        EVGPathFlattenNodeFlags.Corner | EVGPathFlattenNodeFlags.Left
    );
    check_node(
        flat.Nodes()[(int)(2)],
        4.0f,
        0.0f,
        9.656854f,
        4.0f,
        EVGPathFlattenNodeFlags.Corner | EVGPathFlattenNodeFlags.Left
    );
    check_contour(flat.Contours()[(int)(0)], 0u, 3u, 13.656854f, true, true);
}
 [GuiTest("gui/vg/VGPath flatten marks clockwise contour convex")] public static void Case15()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(4.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 4.0f));
    path.Close();

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    True(flat.Nodes().Count == 3u);
    check_node(flat.Nodes()[(int)(0)], 0.0f, 0.0f, 0.0f, 4.0f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(1)], 4.0f, 0.0f, 4.0f, 5.656854f, EVGPathFlattenNodeFlags.Corner);
    check_node(flat.Nodes()[(int)(2)], 0.0f, 4.0f, 9.656854f, 4.0f, EVGPathFlattenNodeFlags.Corner);
    check_contour(flat.Contours()[(int)(0)], 0u, 3u, 13.656854f, true, true);
}
 [GuiTest("gui/vg/VGPath curve flatten responds to tolerance")] public static void Case16()
{
    VGPath quad_path = new();
    quad_path.MoveTo(new Offsetf(0.0f, 0.0f));
    quad_path.QuadTo(new Offsetf(50.0f, 100.0f), new Offsetf(100.0f, 0.0f));

    VGPath cubic_path = new();
    cubic_path.MoveTo(new Offsetf(0.0f, 0.0f));
    cubic_path.CubicTo(new Offsetf(30.0f, 100.0f), new Offsetf(70.0f, -100.0f), new Offsetf(100.0f, 0.0f));

    VGPathFlattenOptions loose_options = new();
    loose_options.TessellationFactor = 1.0f;
    loose_options.MaxDepth = 8u;

    VGPathFlattenOptions dense_options = new();
    dense_options.TessellationFactor = 8.0f;
    dense_options.MaxDepth = 8u;

    VGPathFlatten loose_quad = quad_path.Flatten(loose_options);
    VGPathFlatten dense_quad = quad_path.Flatten(dense_options);
    True(dense_quad.Nodes().Count >= loose_quad.Nodes().Count);
    check_offsetf(dense_quad.Nodes()[(int)(0)].Position, 0.0f, 0.0f);
    check_offsetf(dense_quad.Nodes()[(int)(dense_quad.Nodes().Count - 1u)].Position, 100.0f, 0.0f);
    False(flag_all(dense_quad.Nodes()[(int)(1)].Flags, EVGPathFlattenNodeFlags.Corner));

    VGPathFlatten loose_cubic = cubic_path.Flatten(loose_options);
    VGPathFlatten dense_cubic = cubic_path.Flatten(dense_options);
    True(dense_cubic.Nodes().Count >= loose_cubic.Nodes().Count);
    check_offsetf(dense_cubic.Nodes()[(int)(0)].Position, 0.0f, 0.0f);
    check_offsetf(dense_cubic.Nodes()[(int)(dense_cubic.Nodes().Count - 1u)].Position, 100.0f, 0.0f);
    False(flag_all(dense_cubic.Nodes()[(int)(1)].Flags, EVGPathFlattenNodeFlags.Corner));
}
}
