using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
public sealed class VgPathFillTests : VgTestHelpers
{
 static bool has_duplicate_positions(FillMeshCapture mesh, float tolerance = 0.001f)
{
    for (uint i = 0u; i < mesh.Vertices.Count; ++i)
    {
        for (uint j = i + 1u; j < mesh.Vertices.Count; ++j)
        {
            Offsetf delta = mesh.Vertices[(int)(i)].Pos - mesh.Vertices[(int)(j)].Pos;
            if (MathF.Abs(delta.X) <= tolerance && MathF.Abs(delta.Y) <= tolerance)
            {
                return true;
            }
        }
    }
    return false;
}
 static bool has_consistent_positive_winding(FillMeshCapture mesh)
{
    if ((mesh.Triangles.Count == 0))
    {
        return false;
    }

    foreach (FillMeshTriangle triangle in mesh.Triangles)
    {
        Offsetf a = mesh.Vertices[(int)(triangle.I0)].Pos;
        Offsetf b = mesh.Vertices[(int)(triangle.I1)].Pos;
        Offsetf c = mesh.Vertices[(int)(triangle.I2)].Pos;
        float winding = (b - a).Cross(c - a);
        if (winding <= kFloatEpsilon)
        {
            return false;
        }
    }
    return true;
}
 [GuiTest("gui/vg/VGPathFlatten fill convex fast path advances unique odd and even endpoints")] public static void Case1()
{
    VGFillOptions options = new();
    options.OptimizeForSingleConvex = true;


    {
        VGPath path = new();
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(12.0f, 0.0f));
        path.LineTo(new Offsetf(17.0f, 7.0f));
        path.LineTo(new Offsetf(8.0f, 14.0f));
        path.LineTo(new Offsetf(-2.0f, 8.0f));
        path.Close(EVGPathWinding.CW);

        VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());
        FillMeshCapture mesh = capture_fill(flatten, options);

        True(flatten.Contours().Count == 1u);
        True(flatten.Contours()[(int)(0)].Convex);
        True(mesh.Vertices.Count == 5u);
        True(mesh.Triangles.Count == 3u);
        False(has_duplicate_positions(mesh));
        True(has_consistent_positive_winding(mesh));
        True(mesh_covers_point(mesh, new Offsetf(7.0f, 6.0f)));
    }


    {
        VGPath path = new();
        path.AddRect(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f), EVGPathWinding.CCW);

        VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());
        FillMeshCapture mesh = capture_fill(flatten, options);

        True(flatten.Contours().Count == 1u);
        True(flatten.Contours()[(int)(0)].Convex);
        True(mesh.Vertices.Count == 4u);
        True(mesh.Triangles.Count == 2u);
        False(has_duplicate_positions(mesh));
        True(has_consistent_positive_winding(mesh));
        True(mesh_covers_point(mesh, new Offsetf(6.0f, 4.0f)));
    }
}
 [GuiTest("gui/vg/VGPathFlatten fill convex fast path emits allocation free AA")] public static void Case2()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f));
    VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());

    FillAllocatorCounter counter = new();
    VGFillWorkspace workspace = new();
    workspace.Allocator = MakeAllocator(counter);
    VGFillOptions options = fill_aa_options(1.0f, 100.0f);
    options.OptimizeForSingleConvex = true;

    FillMeshCapture mesh = capture_fill(flatten, options, workspace);

    mesh.CheckReservedOnce();
    True(counter.AllocCount == 0u);
    True(counter.ReallocCount == 0u);
    True(counter.FreeCount == 0u);
    True((workspace.ScratchContourVertices.Count == 0));
    True((workspace.VertexLookup.Count == 0));
    True(count_body_vertices(mesh) == 4u);
    True(mesh.Vertices.Count == 8u);
    True(mesh.Triangles.Count == 10u);
    False(has_duplicate_positions(mesh));
    True(has_consistent_positive_winding(mesh));
    True(triangle_has_aa_vertex(mesh, mesh.Triangles[(int)(0)]));
    False(triangle_has_aa_vertex(mesh, mesh.Triangles[(int)(mesh.Triangles.Count - 1u)]));
    True(count_aa_vertices_outside_rect(mesh, Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f)) == 4u);
    True(mesh_covers_point(mesh, new Offsetf(6.0f, 4.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill convex fast path keeps acute AA winding")] public static void Case3()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(42.0f, 152.0f));
    path.LineTo(new Offsetf(50.0f, 144.0f));
    path.LineTo(new Offsetf(318.0f, 138.0f));
    path.LineTo(new Offsetf(354.0f, 152.0f));
    path.LineTo(new Offsetf(318.0f, 166.0f));
    path.LineTo(new Offsetf(50.0f, 160.0f));
    path.Close(EVGPathWinding.CW);
    VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());

    VGFillOptions options = fill_aa_options(1.0f, 2.4f);
    options.OptimizeForSingleConvex = true;
    FillMeshCapture mesh = capture_fill(flatten, options);

    True(flatten.Contours().Count == 1u);
    True(flatten.Contours()[(int)(0)].Convex);
    True(count_body_vertices(mesh) == 6u);
    False(has_duplicate_positions(mesh));
    True(has_consistent_positive_winding(mesh));
}
 [GuiTest("gui/vg/VGPathFlatten fill convex fast path normalizes counter clockwise AA")] public static void Case4()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f), EVGPathWinding.CCW);
    VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());

    FillAllocatorCounter counter = new();
    VGFillWorkspace workspace = new();
    workspace.Allocator = MakeAllocator(counter);
    VGFillOptions options = fill_aa_options(1.0f, 2.4f);
    options.OptimizeForSingleConvex = true;
    FillMeshCapture mesh = capture_fill(flatten, options, workspace);

    True(counter.AllocCount == 0u);
    True(counter.ReallocCount == 0u);
    True(counter.FreeCount == 0u);
    True(mesh.Vertices.Count == 8u);
    True(mesh.Triangles.Count == 10u);
    False(has_duplicate_positions(mesh));
    True(has_consistent_positive_winding(mesh));
    True(count_aa_vertices_outside_rect(mesh, Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f)) == 4u);
}
 [GuiTest("gui/vg/VGPathFlatten fill convex fast path handles high node contours")] public static void Case5()
{
    VGPath path = new();
    path.AddCircle(Circle.CenterRadius(new Offsetf(0.0f, 0.0f), 40.0f), EVGPathWinding.CCW);
    VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());

    VGFillOptions options = new();
    options.OptimizeForSingleConvex = true;
    FillMeshCapture mesh = capture_fill(flatten, options);

    True(flatten.Contours().Count == 1u);
    True(flatten.Contours()[(int)(0)].Convex);
    True(mesh.Vertices.Count == flatten.Contours()[(int)(0)].NodeCount);
    True(mesh.Triangles.Count == flatten.Contours()[(int)(0)].NodeCount - 2u);
    False(has_duplicate_positions(mesh));
    True(has_consistent_positive_winding(mesh));
    True(mesh_covers_point(mesh, Offsetf.Zero()));
}
 [GuiTest("gui/vg/VGPathFlatten fill convex fast path requires explicit opt in")] public static void Case6()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f));
    VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());

    FillAllocatorCounter counter = new();
    VGFillWorkspace workspace = new();
    workspace.Allocator = MakeAllocator(counter);
    VGFillOptions options = new();
    options.UseDelaunay = false;
    _ = capture_fill(flatten, options, workspace);

    True(flatten.Contours().Count == 1u);
    True(flatten.Contours()[(int)(0)].Convex);
    True(counter.AllocCount > 0u);
    True(counter.FreeCount > 0u);
}
 [GuiTest("gui/vg/VGPathFlatten fill convex optimization rejects multiple contours")] public static void Case7()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f), EVGPathWinding.CW);
    path.AddRect(Rectf.LTWH(6.0f, 6.0f, 8.0f, 8.0f), EVGPathWinding.CCW);
    VGPathFlatten flatten = path.Flatten(new VGPathFlattenOptions());

    FillAllocatorCounter counter = new();
    VGFillWorkspace workspace = new();
    workspace.Allocator = MakeAllocator(counter);
    VGFillOptions options = new();
    options.FillRule = EVGFillRule.NonZero;
    options.OptimizeForSingleConvex = true;

    FillMeshCapture mesh = capture_fill(flatten, options, workspace);

    True(counter.AllocCount > 0u);
    True(counter.FreeCount > 0u);
    True(mesh_covers_point(mesh, new Offsetf(3.0f, 10.0f)));
    False(mesh_covers_point(mesh, new Offsetf(10.0f, 10.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill ignores open line contours")] public static void Case8()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    FillMeshCapture mesh = capture_fill(path, new());

    True(mesh.ReserveCallCount == 0u);
    True((mesh.Vertices.Count == 0));
    True((mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGPathFlatten fill implicitly closes open contour")] public static void Case9()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(8.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 6.0f));

    FillMeshCapture mesh = capture_fill(path, new());

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(2.0f, 2.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill ignores fully degenerate closed contours")] public static void Case10()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(5.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.Close();

    FillMeshCapture mesh = capture_fill(path, new());

    True(mesh.ReserveCallCount == 0u);
    True((mesh.Vertices.Count == 0));
    True((mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGPathFlatten fill emits closed triangle mesh")] public static void Case11()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(8.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 6.0f));
    path.Close();

    FillMeshCapture mesh = capture_fill(path, new());

    mesh.CheckReservedOnce();
    True(mesh.Vertices.Count == 3u);
    True(mesh.Triangles.Count == 1u);
}
 [GuiTest("gui/vg/VGPathFlatten fill emits closed rect mesh")] public static void Case12()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));

    FillMeshCapture mesh = capture_fill(path, new());

    mesh.CheckReservedOnce();
    True(mesh.Vertices.Count == 4u);
    True(mesh.Triangles.Count == 2u);
}
 [GuiTest("gui/vg/VGPathFlatten fill AA emits rect fringe and keeps body coverage")] public static void Case13()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));

    FillMeshCapture mesh = capture_fill(path, fill_aa_options());

    mesh.CheckReservedOnce();
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, new Offsetf(5.0f, 4.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill AA radius follows pixel ratio")] public static void Case14()
{
    Rectf rect = Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f);
    const float k_physical_aa_radius = 4.0f;
    const float k_pixel_ratio = 2.0f;
    const float k_logical_aa_radius = k_physical_aa_radius / k_pixel_ratio;
    VGPath path = new();
    path.AddRect(rect);

    VGFillOptions options = new();
    options.PixelRatio = k_pixel_ratio;
    options.AaRadius = k_physical_aa_radius;
    FillMeshCapture mesh = capture_fill(path, options);

    mesh.CheckReservedOnce();
    True(mesh_has_aa_vertex(mesh));

    MeshBounds bounds = calc_bounds(mesh);
    check_near(bounds.MinX, rect.Left - k_logical_aa_radius);
    check_near(bounds.MinY, rect.Top - k_logical_aa_radius);
    check_near(bounds.MaxX, rect.Right + k_logical_aa_radius);
    check_near(bounds.MaxY, rect.Bottom + k_logical_aa_radius);
}
 [GuiTest("gui/vg/VGPathFlatten fill AA reuses body vertices at fringe seam")] public static void Case15()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));

    FillMeshCapture mesh = capture_fill(path, fill_aa_options());

    mesh.CheckReservedOnce();
    True(count_body_vertices(mesh) == 4u);
    True(mesh.Vertices.Count == 8u);
}
 [GuiTest("gui/vg/VGPathFlatten fill AA emits fringe triangles before body triangles")] public static void Case16()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));

    FillMeshCapture mesh = capture_fill(path, fill_aa_options());

    mesh.CheckReservedOnce();
    True(mesh.Triangles.Count >= 2u);
    True(triangle_has_aa_vertex(mesh, mesh.Triangles[(int)(0)]));
    False(triangle_has_aa_vertex(mesh, mesh.Triangles[(int)(mesh.Triangles.Count - 1u)]));
}
 [GuiTest("gui/vg/VGPathFlatten fill options default AA miter limit")] public static void Case17()
{
    VGFillOptions options = new();

    expect_near(options.AaMiterLimit, 2.4f, kFloatEpsilon);
    True(options.UseDelaunay);
    False(options.OptimizeForSingleConvex);
}
 [GuiTest("gui/vg/VGPathFlatten fill constrained Delaunay emits valid mesh")] public static void Case18()
{
    VGPath path = new();
    path.AddCircle(Circle.CenterRadius(new Offsetf(142.0f, 152.0f), 86.0f), EVGPathWinding.CW);
    path.AddCircle(Circle.CenterRadius(new Offsetf(228.0f, 152.0f), 86.0f), EVGPathWinding.CW);
    path.AddRect(Rectf.LTWH(118.0f, 70.0f, 136.0f, 160.0f), EVGPathWinding.CW);

    VGFillOptions fill_options = new();
    fill_options.UseDelaunay = true;
    FillMeshCapture mesh = capture_fill(path, fill_options);

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(180.0f, 152.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill Delaunay on and off both emit legal coverage")] public static void Case19()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 40.0f, 28.0f));
    path.AddRect(Rectf.LTWH(10.0f, 8.0f, 20.0f, 10.0f), EVGPathWinding.CCW);

    VGFillOptions delaunay_options = new();
    delaunay_options.FillRule = EVGFillRule.NonZero;
    delaunay_options.UseDelaunay = true;
    FillMeshCapture delaunay_mesh = capture_fill(path, delaunay_options);

    VGFillOptions fallback_options = delaunay_options;
    fallback_options.UseDelaunay = false;
    FillMeshCapture fallback_mesh = capture_fill(path, fallback_options);

    delaunay_mesh.CheckReservedOnce();
    fallback_mesh.CheckReservedOnce();
    False((delaunay_mesh.Triangles.Count == 0));
    False((fallback_mesh.Triangles.Count == 0));
    True(mesh_covers_point(delaunay_mesh, new Offsetf(5.0f, 14.0f)));
    True(mesh_covers_point(fallback_mesh, new Offsetf(5.0f, 14.0f)));
    False(mesh_covers_point(delaunay_mesh, new Offsetf(20.0f, 13.0f)));
    False(mesh_covers_point(fallback_mesh, new Offsetf(20.0f, 13.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill workspace reuses scratch storage")] public static void Case20()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));

    VGFillWorkspace workspace = new();
    FillMeshCapture first_mesh = capture_fill(path, fill_aa_options(), workspace);
    FillMeshCapture second_mesh = capture_fill(path, fill_aa_options(), workspace);

    first_mesh.CheckReservedOnce();
    second_mesh.CheckReservedOnce();
    True(first_mesh.Vertices.Count == second_mesh.Vertices.Count);
    True(first_mesh.Triangles.Count == second_mesh.Triangles.Count);
    False((workspace.ScratchContourVertices.Count == 0));
    False((workspace.VertexLookup.Count == 0));
}
 [GuiTest("gui/vg/VGPathFlatten fill workspace custom allocator is used")] public static void Case21()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));

    FillAllocatorCounter counter = new();
    VGFillWorkspace workspace = new();
    workspace.Allocator = MakeAllocator(counter);

    FillMeshCapture mesh = capture_fill(path, new(), workspace);

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(counter.AllocCount > 0u);
    True(counter.FreeCount > 0u);
}
 [GuiTest("gui/vg/VGFillAllocator default factory is valid")] public static void Case22()
{
    VGFillAllocator allocator = VGFillAllocator.Default();
    True(allocator.UserData != null);
    True(allocator.Alloc != null);
    True(allocator.Realloc != null);
    True(allocator.Free != null);
    if (allocator.Alloc == null || allocator.Realloc == null || allocator.Free == null)
    {
        return;
    }

    nint ptr = allocator.Alloc(allocator.UserData, 16u);
    True(ptr != 0);
    if (ptr == 0)
    {
        return;
    }

    ptr = allocator.Realloc(allocator.UserData, ptr, 32u);
    True(ptr != 0);
    if (ptr == 0)
    {
        return;
    }

    allocator.Free(allocator.UserData, ptr);
}
 [GuiTest("gui/vg/VGPathFlatten fill AA emits triangle fringe and keeps body coverage")] public static void Case23()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(8.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 6.0f));
    path.Close();

    FillMeshCapture mesh = capture_fill(path, fill_aa_options());

    mesh.CheckReservedOnce();
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, new Offsetf(2.0f, 2.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill AA miter limit bevels sharp corners")] public static void Case24()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 10.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.LineTo(new Offsetf(20.0f, 10.0f));
    path.LineTo(new Offsetf(12.0f, 10.0f));
    path.LineTo(new Offsetf(12.0f, 24.0f));
    path.LineTo(new Offsetf(8.0f, 24.0f));
    path.LineTo(new Offsetf(8.0f, 10.0f));
    path.Close();

    FillMeshCapture bevel_mesh = capture_fill(path, fill_aa_options(1.0f, 1.05f));
    FillMeshCapture miter_mesh = capture_fill(path, fill_aa_options(1.0f, 100.0f));

    bevel_mesh.CheckReservedOnce();
    miter_mesh.CheckReservedOnce();
    True(bevel_mesh.Vertices.Count > miter_mesh.Vertices.Count);
    True(bevel_mesh.Triangles.Count > miter_mesh.Triangles.Count);
    True(mesh_covers_point(bevel_mesh, new Offsetf(10.0f, 12.0f)));
    True(mesh_covers_point(miter_mesh, new Offsetf(10.0f, 12.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill AA inner bevel protects short edges")] public static void Case25()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 0.2f));

    FillMeshCapture mesh = capture_fill(path, fill_aa_options(1.0f, 100.0f));

    mesh.CheckReservedOnce();
    True(count_body_vertices(mesh) == 4u);
    True(mesh.Vertices.Count > 8u);
    True(mesh.Triangles.Count > 10u);
}
 [GuiTest("gui/vg/VGPathFlatten fill EvenOdd nested rectangles leaves center hole")] public static void Case26()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f));
    path.AddRect(Rectf.LTWH(6.0f, 6.0f, 8.0f, 8.0f));

    VGFillOptions fill_options = new();
    fill_options.FillRule = EVGFillRule.EvenOdd;
    FillMeshCapture mesh = capture_fill(path, fill_options);

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(3.0f, 10.0f)));
    False(mesh_covers_point(mesh, new Offsetf(10.0f, 10.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill AA EvenOdd nested rectangles leaves center hole")] public static void Case27()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f));
    path.AddRect(Rectf.LTWH(6.0f, 6.0f, 8.0f, 8.0f));

    VGFillOptions fill_options = fill_aa_options();
    fill_options.FillRule = EVGFillRule.EvenOdd;
    FillMeshCapture mesh = capture_fill(path, fill_options);

    mesh.CheckReservedOnce();
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, new Offsetf(3.0f, 10.0f)));
    False(mesh_covers_point(mesh, new Offsetf(10.0f, 10.0f)));
    True(count_aa_vertices_outside_rect(mesh, Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f)) >= 4u);
    True(count_aa_vertices_in_rect(mesh, Rectf.LTWH(6.1f, 6.1f, 7.8f, 7.8f)) >= 4u);
}
 [GuiTest("gui/vg/VGPathFlatten fill AA EvenOdd multiple holes keeps holes uncovered and emits fringe")] public static void Case28()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 48.0f, 28.0f));
    path.AddRect(Rectf.LTWH(8.0f, 6.0f, 10.0f, 10.0f));
    path.AddRect(Rectf.LTWH(30.0f, 8.0f, 9.0f, 12.0f));

    VGFillOptions fill_options = fill_aa_options();
    fill_options.FillRule = EVGFillRule.EvenOdd;
    FillMeshCapture mesh = capture_fill(path, fill_options);

    mesh.CheckReservedOnce();
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, new Offsetf(4.0f, 14.0f)));
    True(mesh_covers_point(mesh, new Offsetf(24.0f, 14.0f)));
    True(mesh_covers_point(mesh, new Offsetf(43.0f, 14.0f)));
    False(mesh_covers_point(mesh, new Offsetf(13.0f, 11.0f)));
    False(mesh_covers_point(mesh, new Offsetf(34.0f, 14.0f)));
    True(count_aa_vertices_outside_rect(mesh, Rectf.LTWH(0.0f, 0.0f, 48.0f, 28.0f)) >= 4u);
    True(count_aa_vertices_in_rect(mesh, Rectf.LTWH(8.1f, 6.1f, 9.8f, 9.8f)) >= 4u);
    True(count_aa_vertices_in_rect(mesh, Rectf.LTWH(30.1f, 8.1f, 8.8f, 11.8f)) >= 4u);
}
 [GuiTest("gui/vg/VGPathFlatten fill NonZero reverse inner rectangle leaves hole")] public static void Case29()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f));
    path.AddRect(Rectf.LTWH(6.0f, 6.0f, 8.0f, 8.0f), EVGPathWinding.CCW);

    VGFillOptions fill_options = new();
    fill_options.FillRule = EVGFillRule.NonZero;
    FillMeshCapture mesh = capture_fill(path, fill_options);

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(3.0f, 10.0f)));
    False(mesh_covers_point(mesh, new Offsetf(10.0f, 10.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill NonZero same winding nested rectangles stays solid")] public static void Case30()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f));
    path.AddRect(Rectf.LTWH(6.0f, 6.0f, 8.0f, 8.0f));

    VGFillOptions fill_options = new();
    fill_options.FillRule = EVGFillRule.NonZero;
    FillMeshCapture mesh = capture_fill(path, fill_options);

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(3.0f, 10.0f)));
    True(mesh_covers_point(mesh, new Offsetf(10.0f, 10.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill overlapping rectangles emits union mesh")] public static void Case31()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f));
    path.AddRect(Rectf.LTWH(5.0f, 0.0f, 10.0f, 10.0f));

    FillMeshCapture mesh = capture_fill(path, new());

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(2.0f, 5.0f)));
    True(mesh_covers_point(mesh, new Offsetf(7.0f, 5.0f)));
    True(mesh_covers_point(mesh, new Offsetf(12.0f, 5.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill self intersecting bowtie keeps legal mesh")] public static void Case32()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 10.0f));
    path.LineTo(new Offsetf(0.0f, 10.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.Close();

    FillMeshCapture mesh = capture_fill(path, new());

    True(mesh.ReserveCallCount <= 1u);
    False((mesh.Triangles.Count == 0));
    True(mesh.LegalIndices);
}
 [GuiTest("gui/vg/VGPathFlatten fill curved closed contour emits valid mesh")] public static void Case33()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.CubicTo(new Offsetf(12.0f, -8.0f), new Offsetf(28.0f, -8.0f), new Offsetf(40.0f, 0.0f));
    path.CubicTo(new Offsetf(36.0f, 18.0f), new Offsetf(4.0f, 18.0f), new Offsetf(0.0f, 0.0f));
    path.Close();

    FillMeshCapture mesh = capture_fill(path, new());

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(20.0f, 5.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill ignores open and degenerate contour noise")] public static void Case34()
{
    VGPath path = new();
    path.AddRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 8.0f));

    path.MoveTo(new Offsetf(20.0f, 0.0f));
    path.LineTo(new Offsetf(30.0f, 0.0f));

    path.MoveTo(new Offsetf(40.0f, 0.0f));
    path.LineTo(new Offsetf(50.0f, 0.0f));
    path.LineTo(new Offsetf(60.0f, 0.0f));
    path.Close();

    FillMeshCapture mesh = capture_fill(path, new());

    mesh.CheckReservedOnce();
    False((mesh.Triangles.Count == 0));
    True(mesh_covers_point(mesh, new Offsetf(5.0f, 4.0f)));
    False(mesh_covers_point(mesh, new Offsetf(25.0f, 0.0f)));
    False(mesh_covers_point(mesh, new Offsetf(50.0f, 0.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill AA implicitly closes open triangle")] public static void Case35()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(8.0f, 0.0f));
    path.LineTo(new Offsetf(0.0f, 6.0f));

    FillMeshCapture mesh = capture_fill(path, fill_aa_options());

    mesh.CheckReservedOnce();
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, new Offsetf(2.0f, 2.0f)));
}
 [GuiTest("gui/vg/VGPathFlatten fill AA ignores open line contours")] public static void Case36()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    FillMeshCapture mesh = capture_fill(path, fill_aa_options());

    True(mesh.ReserveCallCount == 0u);
    False(mesh_has_aa_vertex(mesh));
    True((mesh.Vertices.Count == 0));
    True((mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGPathFlatten fill AA ignores degenerate closed contours")] public static void Case37()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(5.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));
    path.Close();

    FillMeshCapture mesh = capture_fill(path, fill_aa_options());

    True(mesh.ReserveCallCount == 0u);
    False(mesh_has_aa_vertex(mesh));
    True((mesh.Vertices.Count == 0));
    True((mesh.Triangles.Count == 0));
}
}
