using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
public sealed class VgBasicPrimitiveTests : VgTestHelpers
{
 static MeshCapture capture_circle(
    Circle circle,
    float pixel_ratio = 1.0f,
    float tessellation_factor = 1.0f,
    float aa_radius = 0.0f
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.Circle(
        backend,
        circle,
        pixel_ratio,
        tessellation_factor,
        aa_radius
    );
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_circle_stroke(
    Circle circle,
    float stroke_width) => capture_circle_stroke(circle,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_circle_stroke(
    Circle circle,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.CircleStroke(backend, circle, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_ellipse(
    Ellipse ellipse,
    float pixel_ratio = 1.0f,
    float tessellation_factor = 1.0f,
    float aa_radius = 0.0f
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.Ellipse(
        backend,
        ellipse,
        pixel_ratio,
        tessellation_factor,
        aa_radius
    );
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_ellipse_stroke(
    Ellipse ellipse,
    float stroke_width) => capture_ellipse_stroke(ellipse,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_ellipse_stroke(
    Ellipse ellipse,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.EllipseStroke(backend, ellipse, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_superellipse(
    Superellipse superellipse) => capture_superellipse(superellipse,new VGBasicPrimitiveOptions());
 static MeshCapture capture_superellipse(
    Superellipse superellipse,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.Superellipse(backend, superellipse, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_superellipse_stroke(
    Superellipse superellipse,
    float stroke_width) => capture_superellipse_stroke(superellipse,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_superellipse_stroke(
    Superellipse superellipse,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.SuperellipseStroke(backend, superellipse, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_fan(
    Arc arc) => capture_fan(arc,new VGBasicPrimitiveOptions());
 static MeshCapture capture_fan(
    Arc arc,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.Fan(backend, arc, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_arc_stroke(
    Arc arc,
    float stroke_width) => capture_arc_stroke(arc,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_arc_stroke(
    Arc arc,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.ArcStroke(backend, arc, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_elliptical_fan(
    EllipticalArc arc) => capture_elliptical_fan(arc,new VGBasicPrimitiveOptions());
 static MeshCapture capture_elliptical_fan(
    EllipticalArc arc,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.EllipticalFan(backend, arc, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_elliptical_arc_stroke(
    EllipticalArc arc,
    float stroke_width) => capture_elliptical_arc_stroke(arc,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_elliptical_arc_stroke(
    EllipticalArc arc,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.EllipticalArcStroke(backend, arc, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_superellipse_fan(
    SuperellipseArc arc) => capture_superellipse_fan(arc,new VGBasicPrimitiveOptions());
 static MeshCapture capture_superellipse_fan(
    SuperellipseArc arc,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.SuperellipseFan(backend, arc, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_superellipse_arc_stroke(
    SuperellipseArc arc,
    float stroke_width) => capture_superellipse_arc_stroke(arc,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_superellipse_arc_stroke(
    SuperellipseArc arc,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.SuperellipseArcStroke(backend, arc, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_rect(
    Rectf rect) => capture_rect(rect,new VGBasicPrimitiveOptions());
 static MeshCapture capture_rect(
    Rectf rect,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.Rect(backend, rect, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_rect_stroke(
    Rectf rect,
    float stroke_width) => capture_rect_stroke(rect,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_rect_stroke(
    Rectf rect,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.RectStroke(backend, rect, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_diff_rect(
    Rectf outer,
    Rectf inner) => capture_diff_rect(outer,inner,new VGBasicPrimitiveOptions());
 static MeshCapture capture_diff_rect(
    Rectf outer,
    Rectf inner,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.DiffRect(backend, outer, inner, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_rrect(
    RRect rrect) => capture_rrect(rrect,new VGBasicPrimitiveOptions());
 static MeshCapture capture_rrect(
    RRect rrect,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.RRect(backend, rrect, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_rrect_stroke(
    RRect rrect,
    float stroke_width) => capture_rrect_stroke(rrect,stroke_width,new VGBasicPrimitiveOptions());
 static MeshCapture capture_rrect_stroke(
    RRect rrect,
    float stroke_width,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.RRectStroke(backend, rrect, stroke_width, options);
    capture.CheckValid();
    return capture;
}
 static MeshCapture capture_diff_rrect(
    RRect outer,
    RRect inner) => capture_diff_rrect(outer,inner,new VGBasicPrimitiveOptions());
 static MeshCapture capture_diff_rrect(
    RRect outer,
    RRect inner,
    VGBasicPrimitiveOptions options
)
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitives.DiffRRect(backend, outer, inner, options);
    capture.CheckValid();
    return capture;
}
 static uint expected_circle_segments(
    float radius,
    float pixel_ratio = 1.0f,
    float tessellation_factor = 1.0f
)
{
    float tolerance = Circle.CalcTolerance(
        tessellation_factor,
        pixel_ratio
    );
    return Circle.CenterRadius(Offsetf.Zero(), radius).EstimateSegmentCount(tolerance);
}
 static uint expected_ellipse_segments(
    Ellipse ellipse,
    float radius_delta = 0.0f,
    float pixel_ratio = 1.0f,
    float tessellation_factor = 1.0f
)
{
    Ellipse value = ellipse.Normalized().Inflate(radius_delta);
    float tolerance = Ellipse.CalcTolerance(
        tessellation_factor,
        pixel_ratio
    );
    uint segment_count = 0u;
    value.SampleWithSampler(
        new ShapeToleranceSampleDesc{Tolerance=
            tolerance,Direction=
            EShapeSampleDirection.Forward,MaxDepth=
            16u},
        (Offsetf unused0, float unused1, EllipseSampler unused2) => {
            ++segment_count;
        }
    );
    return segment_count;
}
 static uint expected_superellipse_segments(
    Superellipse superellipse,
    float radius_delta = 0.0f) => expected_superellipse_segments(superellipse,radius_delta,new VGBasicPrimitiveOptions());
 static uint expected_superellipse_segments(
    Superellipse superellipse,
    float radius_delta,
    VGBasicPrimitiveOptions options
)
{
    Superellipse value = superellipse.Normalized().Inflate(radius_delta);
    float tolerance = Superellipse.CalcTolerance(
        options.TessellationFactor,
        options.PixelRatio
    );
    uint segment_count = 0u;
    value.SampleWithSampler(
        new ShapeToleranceSampleDesc{Tolerance=
            tolerance,Direction=
            EShapeSampleDirection.Forward,MaxDepth=
            16u},
        (Offsetf unused0, float unused1, SuperellipseSampler unused2) => {
            ++segment_count;
        }
    );
    return segment_count;
}
 static uint expected_arc_segments(
    Arc arc,
    float radius_delta = 0.0f) => expected_arc_segments(arc,radius_delta,new VGBasicPrimitiveOptions());
 static uint expected_arc_segments(
    Arc arc,
    float radius_delta,
    VGBasicPrimitiveOptions options
)
{
    Arc value = arc.Normalized().Inflate(radius_delta);
    float tolerance = Arc.CalcTolerance(
        options.TessellationFactor,
        options.PixelRatio
    );
    uint segment_count = 0u;
    value.SampleWithSampler(
        new ShapeToleranceSampleDesc{Tolerance=
            tolerance,Direction=
            EShapeSampleDirection.Forward,MaxDepth=
            16u},
        (Offsetf unused0, float unused1, CircleSampler unused2) => {
            ++segment_count;
        }
    );
    return segment_count > 0u ? segment_count - 1u : 0u;
}
 static uint expected_elliptical_arc_segments(
    EllipticalArc arc,
    float radius_delta = 0.0f) => expected_elliptical_arc_segments(arc,radius_delta,new VGBasicPrimitiveOptions());
 static uint expected_elliptical_arc_segments(
    EllipticalArc arc,
    float radius_delta,
    VGBasicPrimitiveOptions options
)
{
    EllipticalArc value = arc.Normalized().Inflate(radius_delta);
    float tolerance = EllipticalArc.CalcTolerance(
        options.TessellationFactor,
        options.PixelRatio
    );
    uint segment_count = 0u;
    value.SampleWithSampler(
        new ShapeToleranceSampleDesc{Tolerance=
            tolerance,Direction=
            EShapeSampleDirection.Forward,MaxDepth=
            16u},
        (Offsetf unused0, float unused1, EllipseSampler unused2) => {
            ++segment_count;
        }
    );
    return segment_count > 0u ? segment_count - 1u : 0u;
}
 static uint expected_superellipse_arc_segments(
    SuperellipseArc arc,
    float radius_delta = 0.0f) => expected_superellipse_arc_segments(arc,radius_delta,new VGBasicPrimitiveOptions());
 static uint expected_superellipse_arc_segments(
    SuperellipseArc arc,
    float radius_delta,
    VGBasicPrimitiveOptions options
)
{
    SuperellipseArc value = arc.Normalized().Inflate(radius_delta);
    float tolerance = SuperellipseArc.CalcTolerance(
        options.TessellationFactor,
        options.PixelRatio
    );
    uint segment_count = 0u;
    value.SampleWithSampler(
        new ShapeToleranceSampleDesc{Tolerance=
            tolerance,Direction=
            EShapeSampleDirection.Forward,MaxDepth=
            16u},
        (Offsetf unused0, float unused1, SuperellipseSampler unused2) => {
            ++segment_count;
        }
    );
    return segment_count > 0u ? segment_count - 1u : 0u;
}
 static uint count_vertices_with_converge(MeshCapture mesh, float converge)
{
    uint count = 0u;
    foreach (VGVertex vertex in mesh.Vertices)
    {
        if (MathF.Abs(vertex.Converge - converge) <= 0.0001f)
        {
            ++count;
        }
    }
    return count;
}
 static uint count_duplicate_vertices_with_converge(MeshCapture mesh, float converge)
{
    uint count = 0u;
    for (uint i = 0u; i < mesh.Vertices.Count; ++i)
    {
        VGVertex vertex = mesh.Vertices[(int)(i)];
        if (MathF.Abs(vertex.Converge - converge) > 0.0001f)
        {
            continue;
        }

        for (uint j = 0u; j < i; ++j)
        {
            VGVertex previous = mesh.Vertices[(int)(j)];
            if (
                MathF.Abs(previous.Converge - converge) <= 0.0001f &&
                previous.Pos.NearlyEqual(vertex.Pos, 0.0001f)
            )
            {
                ++count;
                break;
            }
        }
    }
    return count;
}
 [GuiTest("gui/vg/VGBasicPrimitives returns operation status")] public static void Case1()
{
    MeshCapture capture = new();
    VGBackend backend = capture.Backend();

    VGBasicPrimitiveOptions aa_options = new(){ PixelRatio = 1.0f, TessellationFactor = 1.0f, AaRadius = 1.0f,
    };
    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 16.0f);
    Ellipse ellipse = Ellipse.CenterRadius(Offsetf.Zero(), 18.0f, 10.0f, 0.2f);
    Superellipse superellipse = Superellipse.CenterRadius(Offsetf.Zero(), 18.0f, 10.0f, 0.2f, 4.0f);
    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 16.0f, 0.1f, 0.75f * MathF.PI);
    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(Offsetf.Zero(), 18.0f, 10.0f, 0.2f, 0.1f, 0.75f * MathF.PI);
    SuperellipseArc superellipse_arc = SuperellipseArc.CenterRadius(Offsetf.Zero(), 18.0f, 10.0f, 0.2f, 0.1f, 0.75f * MathF.PI, 4.0f);
    Rectf rect = Rectf.LTWH(-12.0f, -8.0f, 24.0f, 16.0f);
    RRect rrect = RRect.FromLTRBXY(-14.0f, -9.0f, 16.0f, 11.0f, 5.0f, 4.0f);

    True(VGBasicPrimitives.Circle(backend, circle, 1.0f, 1.0f, 0.0f));
    True(VGBasicPrimitives.CircleStroke(backend, circle, 3.0f, aa_options));
    True(VGBasicPrimitives.Ellipse(backend, ellipse, 1.0f, 1.0f, 0.0f));
    True(VGBasicPrimitives.EllipseStroke(backend, ellipse, 3.0f, aa_options));
    True(VGBasicPrimitives.Superellipse(backend, superellipse, aa_options));
    True(VGBasicPrimitives.SuperellipseStroke(backend, superellipse, 3.0f, aa_options));
    True(VGBasicPrimitives.Fan(backend, arc, aa_options));
    True(VGBasicPrimitives.ArcStroke(backend, arc, 3.0f, aa_options));
    True(VGBasicPrimitives.EllipticalFan(backend, elliptical_arc, aa_options));
    True(VGBasicPrimitives.EllipticalArcStroke(backend, elliptical_arc, 3.0f, aa_options));
    True(VGBasicPrimitives.SuperellipseFan(backend, superellipse_arc, aa_options));
    True(VGBasicPrimitives.SuperellipseArcStroke(backend, superellipse_arc, 3.0f, aa_options));
    True(VGBasicPrimitives.Rect(backend, rect, aa_options));
    True(VGBasicPrimitives.RectStroke(backend, rect, 3.0f, aa_options));
    True(VGBasicPrimitives.DiffRect(backend, rect.Inflate(4.0f), rect.Deflate(4.0f), aa_options));
    True(VGBasicPrimitives.RRect(backend, rrect, aa_options));
    True(VGBasicPrimitives.RRectStroke(backend, rrect, 3.0f, aa_options));
    True(VGBasicPrimitives.DiffRRect(backend, rrect.Inflate(4.0f), rrect.Deflate(4.0f), aa_options));

    False(VGBasicPrimitives.Circle(backend, Circle.Zero()));
    False(VGBasicPrimitives.CircleStroke(backend, circle, 0.0f, aa_options));
    False(VGBasicPrimitives.Ellipse(backend, Ellipse.Zero()));
    False(VGBasicPrimitives.EllipseStroke(backend, ellipse, 0.0f, aa_options));
    False(VGBasicPrimitives.Superellipse(backend, Superellipse.Zero(), aa_options));
    False(VGBasicPrimitives.SuperellipseStroke(backend, superellipse, 0.0f, aa_options));
    False(VGBasicPrimitives.Fan(backend, Arc.Zero(), aa_options));
    False(VGBasicPrimitives.ArcStroke(backend, arc, 0.0f, aa_options));
    False(VGBasicPrimitives.EllipticalFan(backend, EllipticalArc.Zero(), aa_options));
    False(VGBasicPrimitives.EllipticalArcStroke(backend, elliptical_arc, 0.0f, aa_options));
    False(VGBasicPrimitives.SuperellipseFan(backend, SuperellipseArc.Zero(), aa_options));
    False(VGBasicPrimitives.SuperellipseArcStroke(backend, superellipse_arc, 0.0f, aa_options));
    False(VGBasicPrimitives.Rect(backend, Rectf.Zero(), aa_options));
    False(VGBasicPrimitives.RectStroke(backend, rect, 0.0f, aa_options));
    False(VGBasicPrimitives.DiffRect(backend, Rectf.Zero(), rect, aa_options));
    False(VGBasicPrimitives.RRect(backend, RRect.Zero(), aa_options));
    False(VGBasicPrimitives.RRectStroke(backend, rrect, 0.0f, aa_options));
    False(VGBasicPrimitives.DiffRRect(backend, RRect.Zero(), rrect, aa_options));

    True(VGBasicPrimitives.DiffRect(backend, rect, rect.Inflate(1.0f), aa_options));
    True(VGBasicPrimitives.DiffRRect(backend, rrect, rrect.Inflate(1.0f), aa_options));
    capture.CheckValid();
}
 [GuiTest("gui/vg/VGBasicPrimitives circle emits direct fan mesh")] public static void Case2()
{
    Circle circle = Circle.CenterRadius(new Offsetf(10.0f, -4.0f), 12.0f);
    uint segment_count = expected_circle_segments(circle.Radius);

    MeshCapture mesh = capture_circle(circle);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) + 1u);
    True(mesh.Triangles.Count == segment_count);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, circle.Center));

    MeshBounds bounds = calc_bounds(mesh);
    True(bounds.MinX >= circle.Center.X - circle.Radius - 0.001f);
    True(bounds.MaxX <= circle.Center.X + circle.Radius + 0.001f);
    True(bounds.MinY >= circle.Center.Y - circle.Radius - 0.001f);
    True(bounds.MaxY <= circle.Center.Y + circle.Radius + 0.001f);
}
 [GuiTest("gui/vg/VGBasicPrimitives circle AA emits outer strip")] public static void Case3()
{
    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 20.0f);
    const float k_aa_radius = 2.0f;
    uint segment_count = expected_circle_segments(circle.Radius);

    MeshCapture mesh = capture_circle(circle, 1.0f, 1.0f, k_aa_radius);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 1u + (uint)(segment_count) * 2u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 3u);
    True(count_vertices_with_converge(mesh, 1.0f) == (uint)(segment_count) + 1u);
    True(count_vertices_with_converge(mesh, 0.0f) == segment_count);
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, circle.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives circle AA radius follows pixel ratio")] public static void Case4()
{
    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 20.0f);
    const float k_physical_aa_radius = 4.0f;
    const float k_pixel_ratio = 2.0f;
    const float k_logical_aa_radius = k_physical_aa_radius / k_pixel_ratio;

    MeshCapture mesh = capture_circle(circle, k_pixel_ratio, 1.0f, k_physical_aa_radius);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 2u);
    True(mesh.Vertices[(int)(2)].Converge == 0.0f);
    check_near(mesh.Vertices[(int)(2)].Pos.X, circle.Center.X + circle.Radius + k_logical_aa_radius);
    check_near(mesh.Vertices[(int)(2)].Pos.Y, circle.Center.Y);
}
 [GuiTest("gui/vg/VGBasicPrimitives circle ignores empty and invalid inputs")] public static void Case5()
{
    MeshCapture empty_mesh = capture_circle(Circle.Zero());
    True(empty_mesh.ReserveCallCount == 0u);
    True((empty_mesh.Vertices.Count == 0));
    True((empty_mesh.Triangles.Count == 0));

    MeshCapture invalid_mesh = capture_circle(Circle.Invalid());
    True(invalid_mesh.ReserveCallCount == 0u);
    True((invalid_mesh.Vertices.Count == 0));
    True((invalid_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives circle_stroke emits direct ring mesh")] public static void Case6()
{
    Circle circle = Circle.CenterRadius(new Offsetf(5.0f, 7.0f), 18.0f);
    const float k_stroke_width = 6.0f;
    float outer_radius = circle.Radius + k_stroke_width * 0.5f;
    uint segment_count = expected_circle_segments(outer_radius);

    MeshCapture mesh = capture_circle_stroke(circle, k_stroke_width);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) * 2u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 2u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh_covers_point(mesh, circle.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives circle_stroke AA emits inner and outer strips")] public static void Case7()
{
    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 24.0f);
    const float k_stroke_width = 8.0f;
    const float k_aa_radius = 1.5f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;

    float outer_radius = circle.Radius + k_stroke_width * 0.5f;
    uint segment_count = expected_circle_segments(outer_radius);

    MeshCapture mesh = capture_circle_stroke(circle, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) * 4u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 6u);
    True(count_vertices_with_converge(mesh, 1.0f) == (uint)(segment_count) * 2u);
    True(count_vertices_with_converge(mesh, 0.0f) == (uint)(segment_count) * 2u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, circle.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives circle_stroke ignores invalid widths")] public static void Case8()
{
    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 8.0f);

    MeshCapture zero_width_mesh = capture_circle_stroke(circle, 0.0f);
    True(zero_width_mesh.ReserveCallCount == 0u);
    True((zero_width_mesh.Vertices.Count == 0));
    True((zero_width_mesh.Triangles.Count == 0));

    MeshCapture nan_width_mesh = capture_circle_stroke(
        circle,
        float.NaN
    );
    True(nan_width_mesh.ReserveCallCount == 0u);
    True((nan_width_mesh.Vertices.Count == 0));
    True((nan_width_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives ellipse emits direct fan mesh")] public static void Case9()
{
    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(10.0f, -4.0f),
        18.0f,
        8.0f,
        0.25f
    );
    uint segment_count = expected_ellipse_segments(ellipse);

    MeshCapture mesh = capture_ellipse(ellipse);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) + 1u);
    True(mesh.Triangles.Count == segment_count);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, ellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives ellipse AA emits outer strip")] public static void Case10()
{
    Ellipse ellipse = Ellipse.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        10.0f,
        0.35f
    );
    const float k_aa_radius = 2.0f;
    uint segment_count = expected_ellipse_segments(ellipse);

    MeshCapture mesh = capture_ellipse(ellipse, 1.0f, 1.0f, k_aa_radius);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 1u + (uint)(segment_count) * 2u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 3u);
    True(count_vertices_with_converge(mesh, 1.0f) == (uint)(segment_count) + 1u);
    True(count_vertices_with_converge(mesh, 0.0f) == segment_count);
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, ellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives ellipse ignores empty and invalid inputs")] public static void Case11()
{
    MeshCapture empty_mesh = capture_ellipse(Ellipse.Zero());
    True(empty_mesh.ReserveCallCount == 0u);
    True((empty_mesh.Vertices.Count == 0));
    True((empty_mesh.Triangles.Count == 0));

    MeshCapture invalid_mesh = capture_ellipse(Ellipse.Invalid());
    True(invalid_mesh.ReserveCallCount == 0u);
    True((invalid_mesh.Vertices.Count == 0));
    True((invalid_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives ellipse_stroke emits direct ring mesh")] public static void Case12()
{
    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(5.0f, 7.0f),
        28.0f,
        14.0f,
        0.4f
    );
    const float k_stroke_width = 6.0f;
    uint segment_count = expected_ellipse_segments(
        ellipse,
        k_stroke_width * 0.5f
    );

    MeshCapture mesh = capture_ellipse_stroke(ellipse, k_stroke_width);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) * 2u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 2u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh_covers_point(mesh, ellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives ellipse_stroke AA emits inner and outer strips")] public static void Case13()
{
    Ellipse ellipse = Ellipse.CenterRadius(
        Offsetf.Zero(),
        30.0f,
        16.0f,
        0.2f
    );
    const float k_stroke_width = 6.0f;
    const float k_aa_radius = 1.5f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;

    uint segment_count = expected_ellipse_segments(
        ellipse,
        k_stroke_width * 0.5f
    );

    MeshCapture mesh = capture_ellipse_stroke(ellipse, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) * 4u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 6u);
    True(count_vertices_with_converge(mesh, 1.0f) == (uint)(segment_count) * 2u);
    True(count_vertices_with_converge(mesh, 0.0f) == (uint)(segment_count) * 2u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, ellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives ellipse_stroke ignores invalid widths")] public static void Case14()
{
    Ellipse ellipse = Ellipse.CenterRadius(Offsetf.Zero(), 12.0f, 6.0f, 0.0f);

    MeshCapture zero_width_mesh = capture_ellipse_stroke(ellipse, 0.0f);
    True(zero_width_mesh.ReserveCallCount == 0u);
    True((zero_width_mesh.Vertices.Count == 0));
    True((zero_width_mesh.Triangles.Count == 0));

    MeshCapture nan_width_mesh = capture_ellipse_stroke(
        ellipse,
        float.NaN
    );
    True(nan_width_mesh.ReserveCallCount == 0u);
    True((nan_width_mesh.Vertices.Count == 0));
    True((nan_width_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse emits direct fan mesh")] public static void Case15()
{
    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(10.0f, -4.0f),
        18.0f,
        8.0f,
        0.25f,
        4.5f
    );
    uint segment_count = expected_superellipse_segments(superellipse);

    MeshCapture mesh = capture_superellipse(superellipse);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) + 1u);
    True(mesh.Triangles.Count == segment_count);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, superellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse AA emits outer strip")] public static void Case16()
{
    Superellipse superellipse = Superellipse.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        10.0f,
        0.35f,
        4.0f
    );
    const float k_aa_radius = 2.0f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;
    uint segment_count = expected_superellipse_segments(superellipse, 0.0f, options);

    MeshCapture mesh = capture_superellipse(superellipse, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 1u + (uint)(segment_count) * 2u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 3u);
    True(count_vertices_with_converge(mesh, 1.0f) == (uint)(segment_count) + 1u);
    True(count_vertices_with_converge(mesh, 0.0f) == segment_count);
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, superellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse ignores empty and invalid inputs")] public static void Case17()
{
    MeshCapture empty_mesh = capture_superellipse(Superellipse.Zero());
    True(empty_mesh.ReserveCallCount == 0u);
    True((empty_mesh.Vertices.Count == 0));
    True((empty_mesh.Triangles.Count == 0));

    MeshCapture invalid_mesh = capture_superellipse(Superellipse.Invalid());
    True(invalid_mesh.ReserveCallCount == 0u);
    True((invalid_mesh.Vertices.Count == 0));
    True((invalid_mesh.Triangles.Count == 0));

    MeshCapture bad_exponent_mesh = capture_superellipse(
        Superellipse.CenterRadius(Offsetf.Zero(), 10.0f, 4.0f, 0.0f, 0.0f)
    );
    True(bad_exponent_mesh.ReserveCallCount == 0u);
    True((bad_exponent_mesh.Vertices.Count == 0));
    True((bad_exponent_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse_stroke emits direct ring mesh")] public static void Case18()
{
    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(5.0f, 7.0f),
        28.0f,
        14.0f,
        0.4f,
        4.0f
    );
    const float k_stroke_width = 6.0f;
    uint segment_count = expected_superellipse_segments(
        superellipse,
        k_stroke_width * 0.5f
    );

    MeshCapture mesh = capture_superellipse_stroke(superellipse, k_stroke_width);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) * 2u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 2u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh_covers_point(mesh, superellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse_stroke AA emits inner and outer strips")] public static void Case19()
{
    Superellipse superellipse = Superellipse.CenterRadius(
        Offsetf.Zero(),
        30.0f,
        16.0f,
        0.2f,
        4.5f
    );
    const float k_stroke_width = 6.0f;
    const float k_aa_radius = 1.5f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;

    uint segment_count = expected_superellipse_segments(
        superellipse,
        k_stroke_width * 0.5f,
        options
    );

    MeshCapture mesh = capture_superellipse_stroke(superellipse, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) * 4u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 6u);
    True(count_vertices_with_converge(mesh, 1.0f) == (uint)(segment_count) * 2u);
    True(count_vertices_with_converge(mesh, 0.0f) == (uint)(segment_count) * 2u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, superellipse.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse_stroke ignores invalid widths")] public static void Case20()
{
    Superellipse superellipse = Superellipse.CenterRadius(
        Offsetf.Zero(),
        12.0f,
        6.0f,
        0.0f,
        4.0f
    );

    MeshCapture zero_width_mesh = capture_superellipse_stroke(superellipse, 0.0f);
    True(zero_width_mesh.ReserveCallCount == 0u);
    True((zero_width_mesh.Vertices.Count == 0));
    True((zero_width_mesh.Triangles.Count == 0));

    MeshCapture nan_width_mesh = capture_superellipse_stroke(
        superellipse,
        float.NaN
    );
    True(nan_width_mesh.ReserveCallCount == 0u);
    True((nan_width_mesh.Vertices.Count == 0));
    True((nan_width_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives fan emits direct open fan mesh")] public static void Case21()
{
    Arc arc = Arc.CenterRadius(new Offsetf(3.0f, -2.0f), 18.0f, 0.15f, 0.65f * MathF.PI);
    uint segment_count = expected_arc_segments(arc);

    MeshCapture mesh = capture_fan(arc);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) + 2u);
    True(mesh.Triangles.Count == segment_count);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives fan AA emits connected fill fringe")] public static void Case22()
{
    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 22.0f, -0.2f, 0.55f * MathF.PI);
    const float k_aa_radius = 1.5f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;
    uint segment_count = expected_arc_segments(arc, 0.0f, options);

    MeshCapture mesh = capture_fan(arc, options);
    mesh.CheckReservedHintOnce();
    uint boundary_count = (uint)(segment_count) + 2u;
    True(count_vertices_with_converge(mesh, 1.0f) == boundary_count);
    True(count_vertices_with_converge(mesh, 0.0f) >= boundary_count);
    True(mesh.Triangles.Count >= (uint)(segment_count) + boundary_count * 2u);
    True(mesh.Vertices[(int)(0)].Converge == 1.0f);
    True(mesh.Vertices[(int)(1)].Converge == 1.0f);
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives arc_stroke emits direct open ring mesh")] public static void Case23()
{
    Arc arc = Arc.CenterRadius(new Offsetf(4.0f, 5.0f), 24.0f, 0.1f, 0.75f * MathF.PI);
    const float k_stroke_width = 6.0f;
    uint segment_count = expected_arc_segments(arc, k_stroke_width * 0.5f);

    MeshCapture mesh = capture_arc_stroke(arc, k_stroke_width);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == ((uint)(segment_count) + 1u) * 2u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 2u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives arc_stroke AA emits inner outer and cap strips")] public static void Case24()
{
    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 26.0f, 0.25f, -0.7f * MathF.PI);
    const float k_stroke_width = 6.0f;
    const float k_aa_radius = 1.25f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;
    uint segment_count = expected_arc_segments(arc, k_stroke_width * 0.5f, options);

    MeshCapture mesh = capture_arc_stroke(arc, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == ((uint)(segment_count) + 1u) * 4u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 6u + 4u);
    True(count_vertices_with_converge(mesh, 1.0f) == ((uint)(segment_count) + 1u) * 2u);
    True(count_vertices_with_converge(mesh, 0.0f) == ((uint)(segment_count) + 1u) * 2u);
    True(mesh.Vertices[(int)(0)].Converge == 1.0f);
    True(mesh.Vertices[(int)(1)].Converge == 1.0f);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives elliptical_fan emits direct open fan mesh")] public static void Case25()
{
    EllipticalArc arc = EllipticalArc.CenterRadius(
        new Offsetf(6.0f, -3.0f),
        28.0f,
        12.0f,
        0.2f,
        -0.1f,
        0.8f * MathF.PI
    );
    uint segment_count = expected_elliptical_arc_segments(arc);

    MeshCapture mesh = capture_elliptical_fan(arc);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) + 2u);
    True(mesh.Triangles.Count == segment_count);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives elliptical_fan AA emits connected fill fringe")] public static void Case26()
{
    EllipticalArc arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        28.0f,
        12.0f,
        0.2f,
        -0.1f,
        0.65f * MathF.PI
    );
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = 1.5f;
    uint segment_count = expected_elliptical_arc_segments(arc, 0.0f, options);

    MeshCapture mesh = capture_elliptical_fan(arc, options);
    mesh.CheckReservedHintOnce();
    uint boundary_count = (uint)(segment_count) + 2u;
    True(count_vertices_with_converge(mesh, 1.0f) == boundary_count);
    True(count_vertices_with_converge(mesh, 0.0f) >= boundary_count);
    True(mesh.Triangles.Count >= (uint)(segment_count) + boundary_count * 2u);
    True(mesh.Vertices[(int)(0)].Converge == 1.0f);
    True(mesh.Vertices[(int)(1)].Converge == 1.0f);
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives elliptical_arc_stroke AA emits open ring mesh")] public static void Case27()
{
    EllipticalArc arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        32.0f,
        14.0f,
        0.35f,
        0.25f,
        0.65f * MathF.PI
    );
    const float k_stroke_width = 6.0f;
    const float k_aa_radius = 1.25f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;
    uint segment_count = expected_elliptical_arc_segments(arc, k_stroke_width * 0.5f, options);

    MeshCapture mesh = capture_elliptical_arc_stroke(arc, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == ((uint)(segment_count) + 1u) * 4u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 6u + 4u);
    True(count_vertices_with_converge(mesh, 1.0f) == ((uint)(segment_count) + 1u) * 2u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse_fan emits direct open fan mesh")] public static void Case28()
{
    SuperellipseArc arc = SuperellipseArc.CenterRadius(
        new Offsetf(6.0f, -3.0f),
        28.0f,
        12.0f,
        0.2f,
        -0.1f,
        0.8f * MathF.PI,
        4.5f
    );
    uint segment_count = expected_superellipse_arc_segments(arc);

    MeshCapture mesh = capture_superellipse_fan(arc);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == (uint)(segment_count) + 2u);
    True(mesh.Triangles.Count == segment_count);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse_fan AA emits connected fill fringe")] public static void Case29()
{
    SuperellipseArc arc = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        28.0f,
        12.0f,
        0.2f,
        -0.1f,
        0.65f * MathF.PI,
        4.25f
    );
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = 1.5f;
    uint segment_count = expected_superellipse_arc_segments(arc, 0.0f, options);

    MeshCapture mesh = capture_superellipse_fan(arc, options);
    mesh.CheckReservedHintOnce();
    uint boundary_count = (uint)(segment_count) + 2u;
    True(count_vertices_with_converge(mesh, 1.0f) == boundary_count);
    True(count_vertices_with_converge(mesh, 0.0f) >= boundary_count);
    True(mesh.Triangles.Count >= (uint)(segment_count) + boundary_count * 2u);
    True(mesh.Vertices[(int)(0)].Converge == 1.0f);
    True(mesh.Vertices[(int)(1)].Converge == 1.0f);
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives superellipse_arc_stroke AA emits open ring mesh")] public static void Case30()
{
    SuperellipseArc arc = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        32.0f,
        14.0f,
        0.35f,
        0.25f,
        0.65f * MathF.PI,
        4.25f
    );
    const float k_stroke_width = 6.0f;
    const float k_aa_radius = 1.25f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;
    uint segment_count = expected_superellipse_arc_segments(arc, k_stroke_width * 0.5f, options);

    MeshCapture mesh = capture_superellipse_arc_stroke(arc, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == ((uint)(segment_count) + 1u) * 4u);
    True(mesh.Triangles.Count == (uint)(segment_count) * 6u + 4u);
    True(count_vertices_with_converge(mesh, 1.0f) == ((uint)(segment_count) + 1u) * 2u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, arc.Center));
}
 [GuiTest("gui/vg/VGBasicPrimitives fan primitives ignore empty invalid and bad stroke widths")] public static void Case31()
{
    MeshCapture empty_mesh = capture_fan(Arc.Zero());
    True(empty_mesh.ReserveCallCount == 0u);
    True((empty_mesh.Vertices.Count == 0));
    True((empty_mesh.Triangles.Count == 0));

    MeshCapture invalid_mesh = capture_elliptical_fan(EllipticalArc.Invalid());
    True(invalid_mesh.ReserveCallCount == 0u);
    True((invalid_mesh.Vertices.Count == 0));
    True((invalid_mesh.Triangles.Count == 0));

    SuperellipseArc arc = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        12.0f,
        6.0f,
        0.0f,
        0.0f,
        0.5f * MathF.PI,
        4.0f
    );
    MeshCapture zero_width_mesh = capture_superellipse_arc_stroke(arc, 0.0f);
    True(zero_width_mesh.ReserveCallCount == 0u);
    True((zero_width_mesh.Vertices.Count == 0));
    True((zero_width_mesh.Triangles.Count == 0));

    MeshCapture bad_exponent_mesh = capture_superellipse_fan(
        SuperellipseArc.CenterRadius(Offsetf.Zero(), 12.0f, 6.0f, 0.0f, 0.0f, 0.5f * MathF.PI, 0.0f)
    );
    True(bad_exponent_mesh.ReserveCallCount == 0u);
    True((bad_exponent_mesh.Vertices.Count == 0));
    True((bad_exponent_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives rect emits direct quad mesh")] public static void Case32()
{
    Rectf rect = Rectf.LTWH(3.0f, -4.0f, 20.0f, 12.0f);

    MeshCapture mesh = capture_rect(rect);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 4u);
    True(mesh.Triangles.Count == 2u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, rect.Center()));

    MeshBounds bounds = calc_bounds(mesh);
    check_near(bounds.MinX, rect.Left);
    check_near(bounds.MinY, rect.Top);
    check_near(bounds.MaxX, rect.Right);
    check_near(bounds.MaxY, rect.Bottom);
}
 [GuiTest("gui/vg/VGBasicPrimitives rect AA emits outer strips")] public static void Case33()
{
    Rectf rect = Rectf.LTWH(0.0f, 0.0f, 18.0f, 10.0f);
    const float k_aa_radius = 1.5f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;

    MeshCapture mesh = capture_rect(rect, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 8u);
    True(mesh.Triangles.Count == 10u);
    True(count_vertices_with_converge(mesh, 1.0f) == 4u);
    True(count_vertices_with_converge(mesh, 0.0f) == 4u);
    True(mesh_has_aa_vertex(mesh));
    True(mesh_covers_point(mesh, rect.Center()));

    MeshBounds bounds = calc_bounds(mesh);
    check_near(bounds.MinX, rect.Left - k_aa_radius);
    check_near(bounds.MinY, rect.Top - k_aa_radius);
    check_near(bounds.MaxX, rect.Right + k_aa_radius);
    check_near(bounds.MaxY, rect.Bottom + k_aa_radius);
}
 [GuiTest("gui/vg/VGBasicPrimitives rect AA radius follows pixel ratio")] public static void Case34()
{
    Rectf rect = Rectf.LTWH(0.0f, 0.0f, 18.0f, 10.0f);
    const float k_physical_aa_radius = 4.0f;
    const float k_pixel_ratio = 2.0f;
    const float k_logical_aa_radius = k_physical_aa_radius / k_pixel_ratio;
    VGBasicPrimitiveOptions options = new();
    options.PixelRatio = k_pixel_ratio;
    options.AaRadius = k_physical_aa_radius;

    MeshCapture mesh = capture_rect(rect, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 8u);
    True(mesh_has_aa_vertex(mesh));

    MeshBounds bounds = calc_bounds(mesh);
    check_near(bounds.MinX, rect.Left - k_logical_aa_radius);
    check_near(bounds.MinY, rect.Top - k_logical_aa_radius);
    check_near(bounds.MaxX, rect.Right + k_logical_aa_radius);
    check_near(bounds.MaxY, rect.Bottom + k_logical_aa_radius);
}
 [GuiTest("gui/vg/VGBasicPrimitives rect_stroke emits direct ring mesh")] public static void Case35()
{
    Rectf rect = Rectf.LTWH(-10.0f, -6.0f, 30.0f, 18.0f);
    const float k_stroke_width = 4.0f;

    MeshCapture mesh = capture_rect_stroke(rect, k_stroke_width);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 8u);
    True(mesh.Triangles.Count == 8u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh_covers_point(mesh, rect.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives rect_stroke AA emits inner and outer strips")] public static void Case36()
{
    Rectf rect = Rectf.LTWH(-12.0f, -8.0f, 36.0f, 24.0f);
    const float k_stroke_width = 4.0f;
    const float k_aa_radius = 1.0f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;

    MeshCapture mesh = capture_rect_stroke(rect, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 16u);
    True(mesh.Triangles.Count == 24u);
    True(count_vertices_with_converge(mesh, 1.0f) == 8u);
    True(count_vertices_with_converge(mesh, 0.0f) == 8u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, rect.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives diff_rect emits direct ring mesh")] public static void Case37()
{
    Rectf outer = Rectf.LTWH(0.0f, 0.0f, 40.0f, 30.0f);
    Rectf inner = Rectf.LTWH(12.0f, 8.0f, 16.0f, 10.0f);

    MeshCapture mesh = capture_diff_rect(outer, inner);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 8u);
    True(mesh.Triangles.Count == 8u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    True(mesh_covers_point(mesh, new Offsetf(4.0f, 4.0f)));
    False(mesh_covers_point(mesh, inner.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives diff_rect AA emits inner and outer strips")] public static void Case38()
{
    Rectf outer = Rectf.LTWH(-20.0f, -12.0f, 44.0f, 34.0f);
    Rectf inner = Rectf.LTWH(-4.0f, -3.0f, 12.0f, 8.0f);
    const float k_aa_radius = 1.0f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;

    MeshCapture mesh = capture_diff_rect(outer, inner, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count == 16u);
    True(mesh.Triangles.Count == 24u);
    True(count_vertices_with_converge(mesh, 1.0f) == 8u);
    True(count_vertices_with_converge(mesh, 0.0f) == 8u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, inner.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives rect primitives ignore invalid and covered inputs")] public static void Case39()
{
    MeshCapture empty_rect_mesh = capture_rect(Rectf.Zero());
    True(empty_rect_mesh.ReserveCallCount == 0u);
    True((empty_rect_mesh.Vertices.Count == 0));
    True((empty_rect_mesh.Triangles.Count == 0));

    Rectf rect = Rectf.LTWH(0.0f, 0.0f, 12.0f, 8.0f);
    MeshCapture zero_width_mesh = capture_rect_stroke(rect, 0.0f);
    True(zero_width_mesh.ReserveCallCount == 0u);
    True((zero_width_mesh.Vertices.Count == 0));
    True((zero_width_mesh.Triangles.Count == 0));

    Rectf nan_rect = new Rectf(
        float.NaN,
        0.0f,
        1.0f,
        1.0f
    );
    MeshCapture invalid_rect_mesh = capture_rect(nan_rect);
    True(invalid_rect_mesh.ReserveCallCount == 0u);
    True((invalid_rect_mesh.Vertices.Count == 0));
    True((invalid_rect_mesh.Triangles.Count == 0));

    MeshCapture covered_diff_mesh = capture_diff_rect(rect, rect);
    True(covered_diff_mesh.ReserveCallCount == 0u);
    True((covered_diff_mesh.Vertices.Count == 0));
    True((covered_diff_mesh.Triangles.Count == 0));
}
 [GuiTest("gui/vg/VGBasicPrimitives rrect emits direct rounded fan mesh")] public static void Case40()
{
    RRect rrect = RRect.FromLTRBXY(-20.0f, -12.0f, 24.0f, 18.0f, 8.0f, 5.0f);

    MeshCapture mesh = capture_rrect(rrect);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 8u);
    True(mesh.Triangles.Count > 6u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh.Vertices[(int)(1)].Pos.NearlyEqual(mesh.Vertices[(int)(mesh.Vertices.Count - 1u)].Pos, 0.0001f));
    True(mesh_covers_point(mesh, rrect.Center()));

    MeshBounds bounds = calc_bounds(mesh);
    True(bounds.MinX >= rrect.Left - 0.001f);
    True(bounds.MinY >= rrect.Top - 0.001f);
    True(bounds.MaxX <= rrect.Right + 0.001f);
    True(bounds.MaxY <= rrect.Bottom + 0.001f);
}
 [GuiTest("gui/vg/VGBasicPrimitives rrect AA emits outer ring")] public static void Case41()
{
    RRect rrect = RRect.FromLTRBXY(-20.0f, -12.0f, 24.0f, 18.0f, 8.0f, 5.0f);
    const float k_aa_radius = 1.5f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = k_aa_radius;

    MeshCapture mesh = capture_rrect(rrect, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 16u);
    True(mesh.Triangles.Count > 18u);
    True(mesh_has_aa_vertex(mesh));
    True(count_vertices_with_converge(mesh, 0.0f) > 0u);
    True(mesh_covers_point(mesh, rrect.Center()));

    MeshBounds bounds = calc_bounds(mesh);
    True(bounds.MinX >= rrect.Left - k_aa_radius - 0.001f);
    True(bounds.MinY >= rrect.Top - k_aa_radius - 0.001f);
    True(bounds.MaxX <= rrect.Right + k_aa_radius + 0.001f);
    True(bounds.MaxY <= rrect.Bottom + k_aa_radius + 0.001f);
}
 [GuiTest("gui/vg/VGBasicPrimitives rrect reuses adjacent corner seam vertices")] public static void Case42()
{
    RRect rrect = new(
        -10.0f,
        -15.0f,
        10.0f,
        18.0f,
        new Radius(10.0f, 6.0f),
        new Radius(10.0f, 6.0f),
        new Radius(4.0f, 4.0f),
        new Radius(4.0f, 4.0f)
    );

    MeshCapture mesh = capture_rrect(rrect);
    mesh.CheckReservedHintOnce();
    True(count_duplicate_vertices_with_converge(mesh, 1.0f) == 0u);
    True(mesh_covers_point(mesh, rrect.Center()));

    VGBasicPrimitiveOptions options = new();
    options.AaRadius = 1.25f;
    MeshCapture aa_mesh = capture_rrect(rrect, options);
    aa_mesh.CheckReservedHintOnce();
    True(count_duplicate_vertices_with_converge(aa_mesh, 1.0f) == 0u);
    True(count_duplicate_vertices_with_converge(aa_mesh, 0.0f) == 0u);
    True(mesh_covers_point(aa_mesh, rrect.Center()));

    RRect oversized = RRect.FromLTRBXY(-120.0f, 190.0f, 120.0f, 280.0f, 200.0f, 120.0f);
    MeshCapture scaled_mesh = capture_rrect(oversized);
    scaled_mesh.CheckReservedHintOnce();
    True(count_duplicate_vertices_with_converge(scaled_mesh, 1.0f) == 0u);
    True(mesh_covers_point(scaled_mesh, oversized.Center()));

    MeshCapture scaled_aa_mesh = capture_rrect(oversized, options);
    scaled_aa_mesh.CheckReservedHintOnce();
    True(count_duplicate_vertices_with_converge(scaled_aa_mesh, 1.0f) == 0u);
    True(count_duplicate_vertices_with_converge(scaled_aa_mesh, 0.0f) == 0u);
    True(mesh_covers_point(scaled_aa_mesh, oversized.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives rrect degenerates to rect and ellipse primitives")] public static void Case43()
{
    Rectf rect = Rectf.LTWH(0.0f, 0.0f, 20.0f, 12.0f);
    MeshCapture rect_mesh = capture_rrect(RRect.FromRectRadius(rect, Radius.Zero()));
    rect_mesh.CheckReservedHintOnce();
    True(rect_mesh.Vertices.Count == 4u);
    True(rect_mesh.Triangles.Count == 2u);

    RRect ellipse_rrect = RRect.FromLTRBXY(-10.0f, -6.0f, 10.0f, 6.0f, 10.0f, 6.0f);
    MeshCapture ellipse_mesh = capture_rrect(ellipse_rrect);
    MeshCapture direct_ellipse_mesh = capture_ellipse(
        Ellipse.CenterRadius(ellipse_rrect.Center(), 10.0f, 6.0f, 0.0f)
    );
    ellipse_mesh.CheckReservedHintOnce();
    True(ellipse_mesh.Vertices.Count == direct_ellipse_mesh.Vertices.Count);
    True(ellipse_mesh.Triangles.Count == direct_ellipse_mesh.Triangles.Count);
}
 [GuiTest("gui/vg/VGBasicPrimitives rrect_stroke emits direct rounded ring mesh")] public static void Case44()
{
    RRect rrect = RRect.FromLTRBXY(-24.0f, -16.0f, 28.0f, 20.0f, 9.0f, 7.0f);
    const float k_stroke_width = 5.0f;

    MeshCapture mesh = capture_rrect_stroke(rrect, k_stroke_width);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 16u);
    True(mesh.Triangles.Count > 12u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh.Vertices[(int)(0)].Pos.NearlyEqual(mesh.Vertices[(int)(mesh.Vertices.Count - 2u)].Pos, 0.0001f));
    False(mesh.Vertices[(int)(1)].Pos.NearlyEqual(mesh.Vertices[(int)(mesh.Vertices.Count - 1u)].Pos, 0.0001f));
    False(mesh_covers_point(mesh, rrect.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives rrect_stroke AA emits inner and outer rings")] public static void Case45()
{
    RRect rrect = RRect.FromLTRBXY(-24.0f, -16.0f, 28.0f, 20.0f, 9.0f, 7.0f);
    const float k_stroke_width = 5.0f;
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = 1.25f;

    MeshCapture mesh = capture_rrect_stroke(rrect, k_stroke_width, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 32u);
    True(mesh.Triangles.Count > 36u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, rrect.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives diff_rrect emits direct rounded ring mesh")] public static void Case46()
{
    RRect outer = RRect.FromLTRBXY(-30.0f, -20.0f, 32.0f, 22.0f, 10.0f, 8.0f);
    RRect inner = RRect.FromLTRBXY(-9.0f, -6.0f, 12.0f, 8.0f, 4.0f, 3.0f);

    MeshCapture mesh = capture_diff_rrect(outer, inner);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 24u);
    True(mesh.Triangles.Count > 18u);
    True(count_vertices_with_converge(mesh, 1.0f) == mesh.Vertices.Count);
    False(mesh.Vertices[(int)(0)].Pos.NearlyEqual(mesh.Vertices[(int)(mesh.Vertices.Count - 2u)].Pos, 0.0001f));
    False(mesh.Vertices[(int)(1)].Pos.NearlyEqual(mesh.Vertices[(int)(mesh.Vertices.Count - 1u)].Pos, 0.0001f));
    True(mesh_covers_point(mesh, new Offsetf(-24.0f, -14.0f)));
    False(mesh_covers_point(mesh, inner.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives diff_rrect AA emits inner and outer rings")] public static void Case47()
{
    RRect outer = RRect.FromLTRBXY(-30.0f, -20.0f, 32.0f, 22.0f, 10.0f, 8.0f);
    RRect inner = RRect.FromLTRBXY(-9.0f, -6.0f, 12.0f, 8.0f, 4.0f, 3.0f);
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = 1.25f;

    MeshCapture mesh = capture_diff_rrect(outer, inner, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 48u);
    True(mesh.Triangles.Count > 54u);
    True(mesh_has_aa_vertex(mesh));
    False(mesh_covers_point(mesh, inner.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives diff_rrect AA handles rectangular inner hole")] public static void Case48()
{
    RRect outer = RRect.FromLTRBXY(-130.0f, -82.0f, 130.0f, 82.0f, 40.0f, 32.0f);
    RRect inner = RRect.FromLTRBXY(-65.0f, -35.0f, 65.0f, 35.0f, 0.0f, 0.0f);
    VGBasicPrimitiveOptions options = new();
    options.AaRadius = 1.25f;

    MeshCapture mesh = capture_diff_rrect(outer, inner, options);
    mesh.CheckReservedHintOnce();
    True(mesh.Vertices.Count > 48u);
    True(mesh.Triangles.Count > 54u);
    True(mesh_has_aa_vertex(mesh));
    True(count_duplicate_vertices_with_converge(mesh, 1.0f) == 0u);
    True(count_duplicate_vertices_with_converge(mesh, 0.0f) == 0u);
    True(mesh_covers_point(mesh, new Offsetf(-100.0f, -50.0f)));
    False(mesh_covers_point(mesh, inner.Center()));
}
 [GuiTest("gui/vg/VGBasicPrimitives rrect primitives ignore invalid and covered inputs")] public static void Case49()
{
    MeshCapture empty_mesh = capture_rrect(RRect.Zero());
    True(empty_mesh.ReserveCallCount == 0u);
    True((empty_mesh.Vertices.Count == 0));
    True((empty_mesh.Triangles.Count == 0));

    RRect rrect = RRect.FromLTRBXY(0.0f, 0.0f, 20.0f, 12.0f, 4.0f, 4.0f);
    MeshCapture zero_width_mesh = capture_rrect_stroke(rrect, 0.0f);
    True(zero_width_mesh.ReserveCallCount == 0u);
    True((zero_width_mesh.Vertices.Count == 0));
    True((zero_width_mesh.Triangles.Count == 0));

    RRect nan_rrect = new(
        float.NaN,
        0.0f,
        1.0f,
        1.0f,
        Radius.Zero(),
        Radius.Zero(),
        Radius.Zero(),
        Radius.Zero()
    );
    MeshCapture invalid_mesh = capture_rrect(nan_rrect);
    True(invalid_mesh.ReserveCallCount == 0u);
    True((invalid_mesh.Vertices.Count == 0));
    True((invalid_mesh.Triangles.Count == 0));

    MeshCapture covered_mesh = capture_diff_rrect(rrect, rrect);
    True(covered_mesh.ReserveCallCount == 0u);
    True((covered_mesh.Vertices.Count == 0));
    True((covered_mesh.Triangles.Count == 0));
}
}
