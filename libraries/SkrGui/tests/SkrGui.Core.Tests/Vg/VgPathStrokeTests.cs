using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
public sealed class VgPathStrokeTests : VgTestHelpers
{
 static uint expected_round_half_segments(VGStrokeOptions options)
{
    float radius = options.Width * 0.5f;
    float tolerance = Arc.CalcTolerance(
        options.TessellationFactor,
        options.PixelRatio
    );
    return Arc.CenterRadius(
               Offsetf.Zero(),
               radius,
               0.0f,
               MathF.PI
    )
        .EstimateSegmentCount(tolerance);
}
 static bool has_duplicate_positions(MeshCapture mesh, float tolerance = 0.001f)
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
 static bool mesh_has_position(MeshCapture mesh, Offsetf position, float tolerance = 0.001f)
{
    foreach (VGVertex vertex in mesh.Vertices)
    {
        Offsetf delta = vertex.Pos - position;
        if (MathF.Abs(delta.X) <= tolerance && MathF.Abs(delta.Y) <= tolerance)
        {
            return true;
        }
    }
    return false;
}
 static bool flatten_has_position(VGPathFlatten flatten, Offsetf position, float tolerance = 0.001f)
{
    foreach (VGPathFlattenNode node in flatten.Nodes())
    {
        Offsetf delta = node.Position - position;
        if (MathF.Abs(delta.X) <= tolerance && MathF.Abs(delta.Y) <= tolerance)
        {
            return true;
        }
    }
    return false;
}
 static void check_strict_collinear_mesh_side_positions(MeshCapture mesh)
{
    float y0 = -5.0f;
    float y1 = 5.0f;
    float[] xs = { 30.0f, 65.0f, 100.0f };
    foreach (float x in xs)
    {
        True(mesh_has_position(mesh, new Offsetf(x, y0)));
        True(mesh_has_position(mesh, new Offsetf(x, y1)));
    }
}
 static void check_strict_collinear_flatten_side_positions(VGPathFlatten flatten)
{
    float y0 = -5.0f;
    float y1 = 5.0f;
    float[] xs = { 30.0f, 65.0f, 100.0f };
    foreach (float x in xs)
    {
        True(flatten_has_position(flatten, new Offsetf(x, y0)));
        True(flatten_has_position(flatten, new Offsetf(x, y1)));
    }
}
 static VGPath make_strict_collinear_path()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(30.0f, 0.0f));
    path.LineTo(new Offsetf(65.0f, 0.0f));
    path.LineTo(new Offsetf(100.0f, 0.0f));
    path.LineTo(new Offsetf(150.0f, 0.0f));
    return path;
}
 static void check_mesh_bounds_equal(MeshBounds lhs, MeshBounds rhs)
{
    check_near(lhs.MinX, rhs.MinX);
    check_near(lhs.MinY, rhs.MinY);
    check_near(lhs.MaxX, rhs.MaxX);
    check_near(lhs.MaxY, rhs.MaxY);
}
 [GuiTest("gui/vg/VGPathFlatten stroke emits butt line body and AA rings")] public static void Case1()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(100.0f, 0.0f));

    VGStrokeOptions options = new();
    options.Width = 10.0f;
    options.Cap = EVGStrokeCap.Butt;
    options.Join = EVGStrokeJoin.Miter;
    options.AaRadius = 0.0f;

    MeshCapture no_aa_mesh = capture_stroke(path, options);
    True(no_aa_mesh.Vertices.Count == 4u);
    True(no_aa_mesh.Triangles.Count == 2u);
    no_aa_mesh.CheckReservedOnce();
    MeshBounds no_aa_bounds = calc_bounds(no_aa_mesh);
    check_near(no_aa_bounds.MinX, 0.0f);
    check_near(no_aa_bounds.MaxX, 100.0f);
    check_near(no_aa_bounds.MinY, -5.0f);
    check_near(no_aa_bounds.MaxY, 5.0f);

    options.AaRadius = 2.0f;
    MeshCapture aa_mesh = capture_stroke(path, options);
    True(aa_mesh.Vertices.Count == 8u);
    True(aa_mesh.Triangles.Count == 10u);
    aa_mesh.CheckReservedOnce();
    MeshBounds aa_bounds = calc_bounds(aa_mesh);
    check_near(aa_bounds.MinX, -2.0f);
    check_near(aa_bounds.MaxX, 102.0f);
    check_near(aa_bounds.MinY, -7.0f);
    check_near(aa_bounds.MaxY, 7.0f);

    options.AaRadius = 4.0f;
    options.PixelRatio = 2.0f;
    MeshCapture high_pixel_aa_mesh = capture_stroke(path, options);
    True(high_pixel_aa_mesh.Vertices.Count == aa_mesh.Vertices.Count);
    True(high_pixel_aa_mesh.Triangles.Count == aa_mesh.Triangles.Count);
    high_pixel_aa_mesh.CheckReservedOnce();
    MeshBounds high_pixel_aa_bounds = calc_bounds(high_pixel_aa_mesh);
    check_mesh_bounds_equal(high_pixel_aa_bounds, aa_bounds);
}
 [GuiTest("gui/vg/VGPathFlatten stroke ignores empty paths and invalid widths")] public static void Case2()
{
    VGStrokeOptions options = new();
    options.Width = 10.0f;

    VGPath empty_path = new();
    MeshCapture empty_mesh = capture_stroke(empty_path, options);
    True(empty_mesh.ReserveCallCount == 0u);
    True((empty_mesh.Vertices.Count == 0));
    True((empty_mesh.Triangles.Count == 0));

    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    options.Width = 0.0f;
    MeshCapture zero_width_mesh = capture_stroke(path, options);
    True(zero_width_mesh.ReserveCallCount == 0u);
    True((zero_width_mesh.Vertices.Count == 0));
    True((zero_width_mesh.Triangles.Count == 0));

    options.Width = float.NaN;
    MeshCapture nan_width_mesh = capture_stroke(path, options);
    True(nan_width_mesh.ReserveCallCount == 0u);
    True((nan_width_mesh.Vertices.Count == 0));
    True((nan_width_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGPathFlatten stroke handles square cap and closed miter contour")] public static void Case3()
{
    VGPath open_path = new();
    open_path.MoveTo(new Offsetf(0.0f, 0.0f));
    open_path.LineTo(new Offsetf(10.0f, 0.0f));

    VGStrokeOptions options = new();
    options.Width = 10.0f;
    options.Cap = EVGStrokeCap.Square;
    options.Join = EVGStrokeJoin.Miter;
    options.AaRadius = 0.0f;

    MeshCapture square_mesh = capture_stroke(open_path, options);
    True(square_mesh.Vertices.Count == 4u);
    True(square_mesh.Triangles.Count == 2u);
    square_mesh.CheckReservedOnce();
    MeshBounds square_bounds = calc_bounds(square_mesh);
    check_near(square_bounds.MinX, -5.0f);
    check_near(square_bounds.MaxX, 15.0f);
    check_near(square_bounds.MinY, -5.0f);
    check_near(square_bounds.MaxY, 5.0f);

    VGPath closed_path = new();
    closed_path.MoveTo(new Offsetf(0.0f, 0.0f));
    closed_path.LineTo(new Offsetf(10.0f, 0.0f));
    closed_path.LineTo(new Offsetf(10.0f, 10.0f));
    closed_path.LineTo(new Offsetf(0.0f, 10.0f));
    closed_path.Close();

    MeshCapture closed_mesh = capture_stroke(closed_path, options);
    True(closed_mesh.Vertices.Count == 8u);
    True(closed_mesh.Triangles.Count == 8u);
    closed_mesh.CheckReservedOnce();
    MeshBounds closed_bounds = calc_bounds(closed_mesh);
    check_near(closed_bounds.MinX, -5.0f);
    check_near(closed_bounds.MaxX, 15.0f);
    check_near(closed_bounds.MinY, -5.0f);
    check_near(closed_bounds.MaxY, 15.0f);
}
 [GuiTest("gui/vg/VGPathFlatten stroke expands dashed contours with cap geometry")] public static void Case4()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(20.0f, 0.0f));

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    float[] dash_values = { 4.0f, 3.0f };
    VGPathFlatten dashed = new();
    True(flat.DashTo(dashed,new VGDashPattern{Values=dash_values,Offset=0.0f}));
    True(dashed.Contours().Count == 3u);

    VGStrokeOptions options = new();
    options.Width = 4.0f;
    options.Cap = EVGStrokeCap.Square;
    options.Join = EVGStrokeJoin.Miter;
    options.AaRadius = 0.0f;

    MeshCapture mesh = capture_stroke(dashed, options);
    mesh.CheckReservedOnce();
    True(mesh.Vertices.Count == 12u);
    True(mesh.Triangles.Count == 6u);
    MeshBounds bounds = calc_bounds(mesh);
    check_near(bounds.MinX, -2.0f);
    check_near(bounds.MaxX, 20.0f);
    check_near(bounds.MinY, -2.0f);
    check_near(bounds.MaxY, 2.0f);
}
 [GuiTest("gui/vg/VGPathFlatten stroke round tessellation follows circular tolerance")] public static void Case5()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(60.0f, 0.0f));

    VGStrokeOptions base_options = new();
    base_options.Width = 12.0f;
    base_options.Cap = EVGStrokeCap.Round;
    base_options.Join = EVGStrokeJoin.Round;
    base_options.AaRadius = 0.0f;
    base_options.PixelRatio = 1.0f;
    base_options.TessellationFactor = 1.0f;

    VGStrokeOptions wide_options = base_options;
    wide_options.Width = 48.0f;

    VGStrokeOptions high_pixel_options = base_options;
    high_pixel_options.PixelRatio = 3.0f;

    VGStrokeOptions dense_options = base_options;
    dense_options.TessellationFactor = 3.0f;

    VGStrokeOptions aa_options = base_options;
    aa_options.AaRadius = 8.0f;

    uint base_segments = expected_round_half_segments(base_options);
    uint wide_segments = expected_round_half_segments(wide_options);
    uint high_pixel_segments = expected_round_half_segments(high_pixel_options);
    uint dense_segments = expected_round_half_segments(dense_options);
    True(base_segments > 0u);
    True(wide_segments > base_segments);
    True(high_pixel_segments > base_segments);
    True(dense_segments > base_segments);

    MeshCapture base_mesh = capture_stroke(path, base_options);
    base_mesh.CheckReservedOnce();
    True(base_mesh.Vertices.Count == (uint)(base_segments) * 2u + 4u);
    True(base_mesh.Triangles.Count == (uint)(base_segments) * 2u + 2u);

    MeshCapture aa_mesh = capture_stroke(path, aa_options);
    aa_mesh.CheckReservedOnce();
    True(count_body_vertices(aa_mesh) == (ulong)base_mesh.Vertices.Count);

    MeshCapture wide_mesh = capture_stroke(path, wide_options);
    MeshCapture high_pixel_mesh = capture_stroke(path, high_pixel_options);
    MeshCapture dense_mesh = capture_stroke(path, dense_options);
    wide_mesh.CheckReservedOnce();
    high_pixel_mesh.CheckReservedOnce();
    dense_mesh.CheckReservedOnce();
    True(wide_mesh.Vertices.Count > base_mesh.Vertices.Count);
    True(high_pixel_mesh.Vertices.Count > base_mesh.Vertices.Count);
    True(dense_mesh.Vertices.Count > base_mesh.Vertices.Count);
}
 [GuiTest("gui/vg/VGPathFlatten stroke bevel and round joins add corner geometry")] public static void Case6()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(40.0f, 0.0f));
    path.LineTo(new Offsetf(40.0f, 40.0f));

    VGStrokeOptions bevel_options = new();
    bevel_options.Width = 10.0f;
    bevel_options.Cap = EVGStrokeCap.Butt;
    bevel_options.Join = EVGStrokeJoin.Bevel;

    VGStrokeOptions round_options = bevel_options;
    round_options.Join = EVGStrokeJoin.Round;
    round_options.TessellationFactor = 3.0f;

    MeshCapture bevel_mesh = capture_stroke(path, bevel_options);
    MeshCapture round_mesh = capture_stroke(path, round_options);
    True(bevel_mesh.Vertices.Count > 4u);
    True(bevel_mesh.Triangles.Count > 2u);
    bevel_mesh.CheckReservedOnce();
    True(round_mesh.Vertices.Count > bevel_mesh.Vertices.Count);
    True(round_mesh.Triangles.Count > bevel_mesh.Triangles.Count);
    round_mesh.CheckReservedOnce();
}
 [GuiTest("gui/vg/VGPathFlatten stroke emits strict collinear pass-through line nodes")] public static void Case7()
{
    VGStrokeOptions options = new();
    options.Width = 10.0f;
    options.Cap = EVGStrokeCap.Butt;
    options.AaRadius = 0.0f;
    options.TessellationFactor = 3.0f;

    options.Join = EVGStrokeJoin.Miter;
    MeshCapture miter_mesh = capture_stroke(make_strict_collinear_path(), options);
    miter_mesh.CheckReservedOnce();
    True(miter_mesh.Vertices.Count == 10u);
    True(miter_mesh.Triangles.Count == 8u);
    check_strict_collinear_mesh_side_positions(miter_mesh);
    False(has_duplicate_positions(miter_mesh));

    options.Join = EVGStrokeJoin.Bevel;
    MeshCapture bevel_mesh = capture_stroke(make_strict_collinear_path(), options);
    bevel_mesh.CheckReservedOnce();
    True(bevel_mesh.Vertices.Count == 10u);
    True(bevel_mesh.Triangles.Count == 8u);
    check_strict_collinear_mesh_side_positions(bevel_mesh);
    False(has_duplicate_positions(bevel_mesh));

    options.Join = EVGStrokeJoin.Round;
    MeshCapture round_mesh = capture_stroke(make_strict_collinear_path(), options);
    round_mesh.CheckReservedOnce();
    True(round_mesh.Vertices.Count == 10u);
    True(round_mesh.Triangles.Count == 8u);
    check_strict_collinear_mesh_side_positions(round_mesh);
    False(has_duplicate_positions(round_mesh));

    MeshBounds bounds = calc_bounds(round_mesh);
    check_near(bounds.MinX, 0.0f);
    check_near(bounds.MaxX, 150.0f);
    check_near(bounds.MinY, -5.0f);
    check_near(bounds.MaxY, 5.0f);
}
 [GuiTest("gui/vg/VGPathFlatten stroke_contour_to and fill emit strict collinear line nodes")] public static void Case8()
{
    VGStrokeOptions stroke_options = new();
    stroke_options.Width = 10.0f;
    stroke_options.Cap = EVGStrokeCap.Butt;
    stroke_options.Join = EVGStrokeJoin.Round;
    stroke_options.AaRadius = 0.0f;
    stroke_options.TessellationFactor = 3.0f;

    VGPathFlatten flat = make_strict_collinear_path().Flatten(new VGPathFlattenOptions());
    VGPathFlatten stroke_contour = new();
    True(flat.StrokeContourTo(stroke_contour, stroke_options));
    True(stroke_contour.Contours().Count == 1u);
    True(stroke_contour.Contours()[(int)(0)].Closed);
    True(stroke_contour.Nodes().Count == 10u);
    check_strict_collinear_flatten_side_positions(stroke_contour);

    FillMeshCapture fill_mesh = capture_fill(stroke_contour, new());
    fill_mesh.CheckReservedOnce();
    check_strict_collinear_mesh_side_positions(fill_mesh);

    MeshCapture stroke_mesh = capture_stroke(flat, stroke_options);
    check_mesh_bounds_equal(calc_bounds(stroke_mesh), calc_bounds(fill_mesh));
}
 [GuiTest("gui/vg/VGPathFlatten stroke_contour_to emits fillable open stroke contour")] public static void Case9()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(100.0f, 0.0f));

    VGStrokeOptions stroke_options = new();
    stroke_options.Width = 10.0f;
    stroke_options.Cap = EVGStrokeCap.Butt;
    stroke_options.Join = EVGStrokeJoin.Miter;

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    VGPathFlatten stroke_contour = new();
    True(flat.StrokeContourTo(stroke_contour, stroke_options));
    True(stroke_contour.Contours().Count == 1u);
    True(stroke_contour.Contours()[(int)(0)].Closed);
    True(stroke_contour.Nodes().Count == 4u);

    MeshCapture stroke_mesh = capture_stroke(flat, stroke_options);
    FillMeshCapture fill_mesh = capture_fill(stroke_contour, new());
    stroke_mesh.CheckReservedOnce();
    fill_mesh.CheckReservedOnce();
    True(fill_mesh.Vertices.Count >= 4u);
    True(fill_mesh.Triangles.Count >= 2u);
    check_mesh_bounds_equal(calc_bounds(stroke_mesh), calc_bounds(fill_mesh));
}
 [GuiTest("gui/vg/VGPathFlatten stroke_contour_to matches direct square cap bounds")] public static void Case10()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(10.0f, 0.0f));

    VGStrokeOptions stroke_options = new();
    stroke_options.Width = 10.0f;
    stroke_options.Cap = EVGStrokeCap.Square;
    stroke_options.Join = EVGStrokeJoin.Miter;

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    VGPathFlatten stroke_contour = new();
    True(flat.StrokeContourTo(stroke_contour, stroke_options));
    True(stroke_contour.Contours().Count == 1u);
    True(stroke_contour.Contours()[(int)(0)].Closed);

    MeshCapture stroke_mesh = capture_stroke(flat, stroke_options);
    FillMeshCapture fill_mesh = capture_fill(stroke_contour, new());
    check_mesh_bounds_equal(calc_bounds(stroke_mesh), calc_bounds(fill_mesh));
}
 [GuiTest("gui/vg/VGPathFlatten stroke_contour_to matches direct round cap bounds")] public static void Case11()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(60.0f, 0.0f));

    VGStrokeOptions stroke_options = new();
    stroke_options.Width = 12.0f;
    stroke_options.Cap = EVGStrokeCap.Round;
    stroke_options.Join = EVGStrokeJoin.Round;
    stroke_options.TessellationFactor = 3.0f;

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    VGPathFlatten stroke_contour = new();
    True(flat.StrokeContourTo(stroke_contour, stroke_options));
    True(stroke_contour.Contours().Count == 1u);
    True(stroke_contour.Contours()[(int)(0)].Closed);
    True(stroke_contour.Nodes().Count > 4u);

    MeshCapture stroke_mesh = capture_stroke(flat, stroke_options);
    FillMeshCapture fill_mesh = capture_fill(stroke_contour, new());
    check_mesh_bounds_equal(calc_bounds(stroke_mesh), calc_bounds(fill_mesh));
}
 [GuiTest("gui/vg/VGPathFlatten stroke_contour_to matches direct round join bounds")] public static void Case12()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(40.0f, 0.0f));
    path.LineTo(new Offsetf(40.0f, 40.0f));

    VGStrokeOptions stroke_options = new();
    stroke_options.Width = 10.0f;
    stroke_options.Cap = EVGStrokeCap.Butt;
    stroke_options.Join = EVGStrokeJoin.Round;
    stroke_options.TessellationFactor = 3.0f;

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    VGPathFlatten stroke_contour = new();
    True(flat.StrokeContourTo(stroke_contour, stroke_options));
    True(stroke_contour.Contours().Count == 1u);
    True(stroke_contour.Contours()[(int)(0)].Closed);
    True(stroke_contour.Nodes().Count > 4u);

    MeshCapture stroke_mesh = capture_stroke(flat, stroke_options);
    FillMeshCapture fill_mesh = capture_fill(stroke_contour, new());
    check_mesh_bounds_equal(calc_bounds(stroke_mesh), calc_bounds(fill_mesh));
}
 [GuiTest("gui/vg/VGPathFlatten stroke_contour_to emits two closed contours for closed path")] public static void Case13()
{
    VGPath path = new();
    path.MoveTo(new Offsetf(0.0f, 0.0f));
    path.LineTo(new Offsetf(40.0f, 0.0f));
    path.LineTo(new Offsetf(40.0f, 30.0f));
    path.LineTo(new Offsetf(0.0f, 30.0f));
    path.Close();

    VGStrokeOptions stroke_options = new();
    stroke_options.Width = 10.0f;
    stroke_options.Cap = EVGStrokeCap.Butt;
    stroke_options.Join = EVGStrokeJoin.Miter;

    VGPathFlatten flat = path.Flatten(new VGPathFlattenOptions());
    VGPathFlatten stroke_contour = new();
    True(flat.StrokeContourTo(stroke_contour, stroke_options));
    True(stroke_contour.Contours().Count == 2u);
    True(stroke_contour.Contours()[(int)(0)].Closed);
    True(stroke_contour.Contours()[(int)(1)].Closed);

    MeshCapture stroke_mesh = capture_stroke(flat, stroke_options);
    FillMeshCapture fill_mesh = capture_fill(stroke_contour, new());
    True(fill_mesh.Vertices.Count >= 8u);
    True(fill_mesh.Triangles.Count >= 8u);
    check_mesh_bounds_equal(calc_bounds(stroke_mesh), calc_bounds(fill_mesh));
}
}
