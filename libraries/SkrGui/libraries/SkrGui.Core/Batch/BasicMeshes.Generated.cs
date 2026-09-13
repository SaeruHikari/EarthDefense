using System.Runtime.InteropServices;
namespace SkrGui;
// Source: src/batch/mesh.cpp, complete RRectMeshHelper and all BasicMeshes methods.
internal static class RRectMeshHelper
{
    internal static bool IsRect(RRect rrect)
    {
        return !IsCornerRadius(rrect.TlRadius) &&
            !IsCornerRadius(rrect.TrRadius) &&
            !IsCornerRadius(rrect.BrRadius) &&
            !IsCornerRadius(rrect.BlRadius);
    }

    internal static RRect Normalized(RRect rrect)
    {
        Func<Radius,Radius> positive_radius = (Radius radius) => {
            if (radius.X <= 0.0f || radius.Y <= 0.0f)
            {
                return Radius.Zero();
            }
            return Radius.Elliptical(radius.X, radius.Y);
        };

        return new RRect(
                   rrect.Left,
                   rrect.Top,
                   rrect.Right,
                   rrect.Bottom,
                   positive_radius(rrect.TlRadius),
                   positive_radius(rrect.TrRadius),
                   positive_radius(rrect.BrRadius),
                   positive_radius(rrect.BlRadius)
        )
            .ScaleRadii();
    }

    internal static void AddPartialBorder(
        VGPath path,
        RRect outer,
        RRect inner,
        bool[] sides
    )
    {
        uint start_break = 0u;
        while (start_break < 4u && sides[start_break])
        {
            ++start_break;
        }
        if (start_break >= 4u)
        {
            return;
        }

        uint break_side = start_break;
        for (;;)
        {
            uint run_start = NextSide(break_side);
            while (!sides[run_start])
            {
                break_side = run_start;
                if (break_side == start_break)
                {
                    return;
                }
                run_start = NextSide(break_side);
            }

            uint[] run_sides = new uint[4];
            uint run_count = 0u;
            uint side = run_start;
            path.MoveTo(OuterSideStart(outer, run_start));
            do
            {
                run_sides[run_count] = side;
                ++run_count;
                AppendOuterSide(path, outer, side, side == run_start);
                side = NextSide(side);
            }
            while (sides[side] && side != run_start);

            uint next_break = side;
            uint end_side = run_sides[run_count - 1u];
            LineTo(path, InnerSideEnd(inner, end_side));

            for (uint i = run_count; i > 0u; --i)
            {
                uint side_index = i - 1u;
                bool include_end_corner = side_index == 0u;
                AppendInnerReverseSide(path, inner, run_sides[side_index], include_end_corner);
            }
            path.Close(EVGPathWinding.CW);

            break_side = next_break;
            if (break_side == start_break)
            {
                return;
            }
        }
    }


    private const float _point_epsilon = 1.0e-4f;

    internal static bool IsCornerRadius(Radius radius)
    {
        return radius.X > 0.0f && radius.Y > 0.0f;
    }

    internal static uint NextSide(uint side)
    {
        return (side + 1u) & 3u;
    }

    internal static void LineTo(VGPath path, Offsetf to)
    {
        Offsetf? cursor = path.CursorPos();
        if (!cursor.HasValue || !cursor.Value.NearlyEqual(to, _point_epsilon))
        {
            path.LineTo(to);
        }
    }

    internal static void CornerTo(
        VGPath path,
        Offsetf center,
        Radius radius,
        float start_angle,
        float sweep_angle
    )
    {
        Offsetf end = center + new Offsetf(MathF.Cos(start_angle + sweep_angle) * radius.X, MathF.Sin(start_angle + sweep_angle) * radius.Y);
        if (radius.X <= 0.0f || radius.Y <= 0.0f)
        {
            LineTo(path, end);
            return;
        }

        EllipticalArc arc = EllipticalArc.CenterRadius(
            center,
            radius.X,
            radius.Y,
            0.0f,
            start_angle,
            sweep_angle
        );
        Offsetf start = arc.StartPoint();
        Offsetf? cursor = path.CursorPos();
        if (!cursor.HasValue || !cursor.Value.NearlyEqual(start, _point_epsilon))
        {
            LineTo(path, start);
        }
        arc.SplitToCubicBeziersFast(
            (CubicBezier cubic) => {
                path.CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
    }

    internal static Offsetf OuterSideStart(RRect rrect, uint side)
    {
        switch (side)
        {
        case 0:
            return new Offsetf(rrect.Left, rrect.Top + rrect.TlRadius.Y);
        case 1:
            return new Offsetf(rrect.Right - rrect.TrRadius.X, rrect.Top);
        case 2:
            return new Offsetf(rrect.Right, rrect.Bottom - rrect.BrRadius.Y);
        case 3:
        default:
            return new Offsetf(rrect.Left + rrect.BlRadius.X, rrect.Bottom);
        }
    }

    internal static Offsetf InnerSideEnd(RRect rrect, uint side)
    {
        switch (side)
        {
        case 0:
            return new Offsetf(rrect.Right, rrect.Top + rrect.TrRadius.Y);
        case 1:
            return new Offsetf(rrect.Right - rrect.BrRadius.X, rrect.Bottom);
        case 2:
            return new Offsetf(rrect.Left, rrect.Bottom - rrect.BlRadius.Y);
        case 3:
        default:
            return new Offsetf(rrect.Left + rrect.TlRadius.X, rrect.Top);
        }
    }

    internal static void AppendOuterLeadingCorner(VGPath path, RRect rrect, uint side)
    {
        switch (side)
        {
        case 0:
            CornerTo(
                path,
                new Offsetf(rrect.Left + rrect.TlRadius.X, rrect.Top + rrect.TlRadius.Y),
                rrect.TlRadius,
                MathF.PI,
                (MathF.PI * .5f)
            );
            break;
        case 1:
            CornerTo(
                path,
                new Offsetf(rrect.Right - rrect.TrRadius.X, rrect.Top + rrect.TrRadius.Y),
                rrect.TrRadius,
                -(MathF.PI * .5f),
                (MathF.PI * .5f)
            );
            break;
        case 2:
            CornerTo(
                path,
                new Offsetf(rrect.Right - rrect.BrRadius.X, rrect.Bottom - rrect.BrRadius.Y),
                rrect.BrRadius,
                0.0f,
                (MathF.PI * .5f)
            );
            break;
        case 3:
        default:
            CornerTo(
                path,
                new Offsetf(rrect.Left + rrect.BlRadius.X, rrect.Bottom - rrect.BlRadius.Y),
                rrect.BlRadius,
                (MathF.PI * .5f),
                (MathF.PI * .5f)
            );
            break;
        }
    }

    internal static void AppendOuterSide(VGPath path, RRect rrect, uint side, bool include_leading_corner)
    {
        if (include_leading_corner)
        {
            AppendOuterLeadingCorner(path, rrect, side);
        }

        switch (side)
        {
        case 0:
            LineTo(path, new Offsetf(rrect.Right - rrect.TrRadius.X, rrect.Top));
            CornerTo(
                path,
                new Offsetf(rrect.Right - rrect.TrRadius.X, rrect.Top + rrect.TrRadius.Y),
                rrect.TrRadius,
                -(MathF.PI * .5f),
                (MathF.PI * .5f)
            );
            break;
        case 1:
            LineTo(path, new Offsetf(rrect.Right, rrect.Bottom - rrect.BrRadius.Y));
            CornerTo(
                path,
                new Offsetf(rrect.Right - rrect.BrRadius.X, rrect.Bottom - rrect.BrRadius.Y),
                rrect.BrRadius,
                0.0f,
                (MathF.PI * .5f)
            );
            break;
        case 2:
            LineTo(path, new Offsetf(rrect.Left + rrect.BlRadius.X, rrect.Bottom));
            CornerTo(
                path,
                new Offsetf(rrect.Left + rrect.BlRadius.X, rrect.Bottom - rrect.BlRadius.Y),
                rrect.BlRadius,
                (MathF.PI * .5f),
                (MathF.PI * .5f)
            );
            break;
        case 3:
        default:
            LineTo(path, new Offsetf(rrect.Left, rrect.Top + rrect.TlRadius.Y));
            CornerTo(
                path,
                new Offsetf(rrect.Left + rrect.TlRadius.X, rrect.Top + rrect.TlRadius.Y),
                rrect.TlRadius,
                MathF.PI,
                (MathF.PI * .5f)
            );
            break;
        }
    }

    internal static void AppendInnerReverseSide(
        VGPath path,
        RRect rrect,
        uint side,
        bool include_end_corner
    )
    {
        switch (side)
        {
        case 0:
            CornerTo(
                path,
                new Offsetf(rrect.Right - rrect.TrRadius.X, rrect.Top + rrect.TrRadius.Y),
                rrect.TrRadius,
                0.0f,
                -(MathF.PI * .5f)
            );
            LineTo(path, new Offsetf(rrect.Left + rrect.TlRadius.X, rrect.Top));
            if (include_end_corner)
            {
                CornerTo(
                    path,
                    new Offsetf(rrect.Left + rrect.TlRadius.X, rrect.Top + rrect.TlRadius.Y),
                    rrect.TlRadius,
                    -(MathF.PI * .5f),
                    -(MathF.PI * .5f)
                );
            }
            break;
        case 1:
            CornerTo(
                path,
                new Offsetf(rrect.Right - rrect.BrRadius.X, rrect.Bottom - rrect.BrRadius.Y),
                rrect.BrRadius,
                (MathF.PI * .5f),
                -(MathF.PI * .5f)
            );
            LineTo(path, new Offsetf(rrect.Right, rrect.Top + rrect.TrRadius.Y));
            if (include_end_corner)
            {
                CornerTo(
                    path,
                    new Offsetf(rrect.Right - rrect.TrRadius.X, rrect.Top + rrect.TrRadius.Y),
                    rrect.TrRadius,
                    0.0f,
                    -(MathF.PI * .5f)
                );
            }
            break;
        case 2:
            CornerTo(
                path,
                new Offsetf(rrect.Left + rrect.BlRadius.X, rrect.Bottom - rrect.BlRadius.Y),
                rrect.BlRadius,
                MathF.PI,
                -(MathF.PI * .5f)
            );
            LineTo(path, new Offsetf(rrect.Right - rrect.BrRadius.X, rrect.Bottom));
            if (include_end_corner)
            {
                CornerTo(
                    path,
                    new Offsetf(rrect.Right - rrect.BrRadius.X, rrect.Bottom - rrect.BrRadius.Y),
                    rrect.BrRadius,
                    (MathF.PI * .5f),
                    -(MathF.PI * .5f)
                );
            }
            break;
        case 3:
        default:
            CornerTo(
                path,
                new Offsetf(rrect.Left + rrect.TlRadius.X, rrect.Top + rrect.TlRadius.Y),
                rrect.TlRadius,
                -(MathF.PI * .5f),
                -(MathF.PI * .5f)
            );
            LineTo(path, new Offsetf(rrect.Left, rrect.Bottom - rrect.BlRadius.Y));
            if (include_end_corner)
            {
                CornerTo(
                    path,
                    new Offsetf(rrect.Left + rrect.BlRadius.X, rrect.Bottom - rrect.BlRadius.Y),
                    rrect.BlRadius,
                    MathF.PI,
                    -(MathF.PI * .5f)
                );
            }
            break;
        }
    }
}
public static partial class BasicMeshes
{
    // Original mesh.cpp:396 rect_fill
    public static bool RectFill(Mesh output,
    Rectf rect,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!rect.IsFinite() || rect.IsEmpty() || !gradient.IsValid())
    {
        return false;
    }


    MeshBuilder builder = new(output, rect, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();

    List<VGPathFlattenLine> split_lines = new();
    bool need_gradient_split = CollectCurveSplitLines(rect, gradient, split_lines);

    if (need_gradient_split)
    {
        VGPathFlatten local_flatten = new();
        VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;


        flatten.Clear();
        flatten.NextContour();
        flatten.AddNode(rect.TopLeft(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(rect.TopRight(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(rect.BottomRight(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(rect.BottomLeft(), EVGPathFlattenNodeFlags.Corner);
        flatten.CloseContour(EVGPathWinding.CW);
        flatten.Finalize();
        flatten.AddLineIntersections(split_lines.ToArray());


        if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }
    }
    else
    {

        if (!VGBasicPrimitives.Rect(backend, rect, new VGBasicPrimitiveOptions(){ PixelRatio = options.PixelRatio, TessellationFactor = options.TessellationFactor, AaRadius = options.AaRadius,
                                                    }))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }
    }

    return true;
}
    // Original mesh.cpp:460 rect_stroke
    public static bool RectStroke(Mesh output,
    Rectf rect,
    float thickness,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!rect.IsFinite() || rect.IsEmpty() || !float.IsFinite(thickness) ||
        thickness <= 0.0f || !gradient.IsValid())
    {
        return false;
    }


    MeshBuilder builder = new(output, rect, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();

    List<VGPathFlattenLine> split_lines = new();
    bool need_gradient_split = CollectCurveSplitLines(rect, gradient, split_lines);

    if (need_gradient_split)
    {
        VGPathFlatten center_flatten = new();
        VGPathFlatten local_stroke_flatten = new();
        VGPathFlatten stroke_flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_stroke_flatten;


        center_flatten.Clear();
        center_flatten.NextContour();
        center_flatten.AddNode(rect.TopLeft(), EVGPathFlattenNodeFlags.Corner);
        center_flatten.AddNode(rect.TopRight(), EVGPathFlattenNodeFlags.Corner);
        center_flatten.AddNode(rect.BottomRight(), EVGPathFlattenNodeFlags.Corner);
        center_flatten.AddNode(rect.BottomLeft(), EVGPathFlattenNodeFlags.Corner);
        center_flatten.CloseContour(EVGPathWinding.CW);
        center_flatten.Finalize();


        if (!center_flatten.StrokeContourTo(stroke_flatten, StrokeOptions(thickness, options)))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }
        stroke_flatten.AddLineIntersections(split_lines.ToArray());


        if (!stroke_flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }
    }
    else
    {

        if (!VGBasicPrimitives.RectStroke(backend, rect, thickness, new VGBasicPrimitiveOptions(){ PixelRatio = options.PixelRatio, TessellationFactor = options.TessellationFactor, AaRadius = options.AaRadius,
                                                                      }))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }
    }

    return true;
}
    // Original mesh.cpp:535 diff_rect
    public static bool DiffRect(Mesh output,
    Rectf outer,
    Rectf inner,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!outer.IsFinite() || outer.IsEmpty() || !inner.IsFinite() ||
        !gradient.IsValid())
    {
        return false;
    }

    if (inner.IsEmpty() || !outer.Overlaps(inner))
    {
        return RectFill(output, outer, gradient, options);
    }

    Rectf hole = outer.Intersect(inner);
    if (hole.IsEmpty())
    {
        return RectFill(output, outer, gradient, options);
    }
    if (hole.Left <= outer.Left && hole.Top <= outer.Top &&
        hole.Right >= outer.Right && hole.Bottom >= outer.Bottom)
    {
        return true;
    }


    MeshBuilder builder = new(output, outer, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();

    List<VGPathFlattenLine> split_lines = new();
    bool need_gradient_split = CollectCurveSplitLines(outer, gradient, split_lines);

    if (need_gradient_split)
    {
        VGPathFlatten local_flatten = new();
        VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;


        flatten.Clear();
        flatten.NextContour();
        flatten.AddNode(outer.TopLeft(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(outer.TopRight(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(outer.BottomRight(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(outer.BottomLeft(), EVGPathFlattenNodeFlags.Corner);
        flatten.CloseContour(EVGPathWinding.CW);


        flatten.NextContour();
        flatten.AddNode(hole.TopLeft(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(hole.BottomLeft(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(hole.BottomRight(), EVGPathFlattenNodeFlags.Corner);
        flatten.AddNode(hole.TopRight(), EVGPathFlattenNodeFlags.Corner);
        flatten.CloseContour(EVGPathWinding.CCW);
        flatten.Finalize();
        flatten.AddLineIntersections(split_lines.ToArray());


        if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }
    }
    else
    {

        if (!VGBasicPrimitives.DiffRect(backend, outer, hole, new VGBasicPrimitiveOptions(){ PixelRatio = options.PixelRatio, TessellationFactor = options.TessellationFactor, AaRadius = options.AaRadius,
                                                                }))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }
    }

    return true;
}
    // Original mesh.cpp:625 rect_fill_colored
    public static bool RectFillColored(Mesh output,
    Rectf rect,
    SRGBColor LT,
    SRGBColor RT,
    SRGBColor RB,
    SRGBColor LB,
    BasicMeshOptions options)
{

    if (!rect.IsFinite() || rect.IsEmpty())
    {
        return false;
    }


    float aa_radius = VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
    bool has_aa = aa_radius > 0.0f;


    MeshBuilder builder = new(output, rect, options.UvRect, null, options.PremultiplyColor);
    builder.Reserve(has_aa ? 8u : 4u, has_aa ? 10u : 2u);


    uint body_tl = builder.PushVertex(rect.TopLeft(), LT, 1.0f);
    uint body_tr = builder.PushVertex(rect.TopRight(), RT, 1.0f);
    uint body_br = builder.PushVertex(rect.BottomRight(), RB, 1.0f);
    uint body_bl = builder.PushVertex(rect.BottomLeft(), LB, 1.0f);

    if (has_aa)
    {
        Rectf aa_rect = rect.Inflate(aa_radius);
        uint aa_tl = builder.PushVertex(aa_rect.TopLeft(), LT, 0.0f);
        uint aa_tr = builder.PushVertex(aa_rect.TopRight(), RT, 0.0f);
        uint aa_br = builder.PushVertex(aa_rect.BottomRight(), RB, 0.0f);
        uint aa_bl = builder.PushVertex(aa_rect.BottomLeft(), LB, 0.0f);

        builder.PushQuad(aa_tl, aa_tr, body_tr, body_tl);
        builder.PushQuad(body_tr, aa_tr, aa_br, body_br);
        builder.PushQuad(body_br, aa_br, aa_bl, body_bl);
        builder.PushQuad(aa_bl, aa_tl, body_tl, body_bl);
    }

    builder.PushQuad(body_tl, body_tr, body_br, body_bl);

    return true;
}
    // Original mesh.cpp:674 ninepatch
    public static bool Ninepatch(Mesh output,
    Rectf rect,
    EdgeInsets rect_inset,
    EdgeInsets uv_inset,
    SRGBColor color,
    BasicMeshOptions options)
{

    if (!rect.IsFinite() || rect.IsEmpty() ||
        !options.UvRect.IsFinite() || options.UvRect.IsEmpty() ||
        !color.IsFinite())
    {
        return false;
    }

    Rectf inner_rect = rect_inset.DeflateRect(rect);
    Rectf inner_uv_rect = uv_inset.DeflateRect(options.UvRect);
    if (!rect_inset.IsNonNegative() || !uv_inset.IsNonNegative() ||
        !inner_rect.IsFinite() || inner_rect.IsEmpty() ||
        !inner_uv_rect.IsFinite() || inner_uv_rect.IsEmpty())
    {
        return false;
    }


    float aa_radius = VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
    bool has_aa = aa_radius > 0.0f;
    SRGBColor mesh_color = color;
    SRGBColor mesh_aa_color = color;
    mesh_aa_color.A = 0.0f;
    if (options.PremultiplyColor)
    {
        mesh_color = mesh_color.Premultiplied();
        mesh_aa_color = mesh_aa_color.Premultiplied();
    }
    uint packed_color = mesh_color.ToRgba32();
    uint packed_aa_color = mesh_aa_color.ToRgba32();


    MeshBuilder builder = new(output, rect, null, null, options.PremultiplyColor);
    builder.Reserve(has_aa ? 28u : 16u, has_aa ? 42u : 18u);


    uint inner_top_left = builder.PushVertexRaw(
        new Offsetf( inner_rect.Left, inner_rect.Top ),
        new Offsetf( inner_uv_rect.Left, inner_uv_rect.Top ),
        packed_color
    );
    uint inner_top_right = builder.PushVertexRaw(
        new Offsetf( inner_rect.Right, inner_rect.Top ),
        new Offsetf( inner_uv_rect.Right, inner_uv_rect.Top ),
        packed_color
    );
    uint inner_bottom_right = builder.PushVertexRaw(
        new Offsetf( inner_rect.Right, inner_rect.Bottom ),
        new Offsetf( inner_uv_rect.Right, inner_uv_rect.Bottom ),
        packed_color
    );
    uint inner_bottom_left = builder.PushVertexRaw(
        new Offsetf( inner_rect.Left, inner_rect.Bottom ),
        new Offsetf( inner_uv_rect.Left, inner_uv_rect.Bottom ),
        packed_color
    );


    uint outer_top_left = builder.PushVertexRaw(
        new Offsetf( rect.Left, rect.Top ),
        new Offsetf( options.UvRect.Left, options.UvRect.Top ),
        packed_color
    );
    uint outer_top_inner_left = builder.PushVertexRaw(
        new Offsetf( inner_rect.Left, rect.Top ),
        new Offsetf( inner_uv_rect.Left, options.UvRect.Top ),
        packed_color
    );
    uint outer_top_inner_right = builder.PushVertexRaw(
        new Offsetf( inner_rect.Right, rect.Top ),
        new Offsetf( inner_uv_rect.Right, options.UvRect.Top ),
        packed_color
    );
    uint outer_top_right = builder.PushVertexRaw(
        new Offsetf( rect.Right, rect.Top ),
        new Offsetf( options.UvRect.Right, options.UvRect.Top ),
        packed_color
    );
    uint outer_right_inner_top = builder.PushVertexRaw(
        new Offsetf( rect.Right, inner_rect.Top ),
        new Offsetf( options.UvRect.Right, inner_uv_rect.Top ),
        packed_color
    );
    uint outer_right_inner_bottom = builder.PushVertexRaw(
        new Offsetf( rect.Right, inner_rect.Bottom ),
        new Offsetf( options.UvRect.Right, inner_uv_rect.Bottom ),
        packed_color
    );
    uint outer_bottom_right = builder.PushVertexRaw(
        new Offsetf( rect.Right, rect.Bottom ),
        new Offsetf( options.UvRect.Right, options.UvRect.Bottom ),
        packed_color
    );
    uint outer_bottom_inner_right = builder.PushVertexRaw(
        new Offsetf( inner_rect.Right, rect.Bottom ),
        new Offsetf( inner_uv_rect.Right, options.UvRect.Bottom ),
        packed_color
    );
    uint outer_bottom_inner_left = builder.PushVertexRaw(
        new Offsetf( inner_rect.Left, rect.Bottom ),
        new Offsetf( inner_uv_rect.Left, options.UvRect.Bottom ),
        packed_color
    );
    uint outer_bottom_left = builder.PushVertexRaw(
        new Offsetf( rect.Left, rect.Bottom ),
        new Offsetf( options.UvRect.Left, options.UvRect.Bottom ),
        packed_color
    );
    uint outer_left_inner_bottom = builder.PushVertexRaw(
        new Offsetf( rect.Left, inner_rect.Bottom ),
        new Offsetf( options.UvRect.Left, inner_uv_rect.Bottom ),
        packed_color
    );
    uint outer_left_inner_top = builder.PushVertexRaw(
        new Offsetf( rect.Left, inner_rect.Top ),
        new Offsetf( options.UvRect.Left, inner_uv_rect.Top ),
        packed_color
    );


    uint aa_top_left = 0u;
    uint aa_top_inner_left = 0u;
    uint aa_top_inner_right = 0u;
    uint aa_top_right = 0u;
    uint aa_right_inner_top = 0u;
    uint aa_right_inner_bottom = 0u;
    uint aa_bottom_right = 0u;
    uint aa_bottom_inner_right = 0u;
    uint aa_bottom_inner_left = 0u;
    uint aa_bottom_left = 0u;
    uint aa_left_inner_bottom = 0u;
    uint aa_left_inner_top = 0u;
    if (has_aa)
    {
        aa_top_left = builder.PushVertexRaw(
            new Offsetf( rect.Left - aa_radius, rect.Top - aa_radius ),
            new Offsetf( options.UvRect.Left, options.UvRect.Top ),
            packed_aa_color
        );
        aa_top_inner_left = builder.PushVertexRaw(
            new Offsetf( inner_rect.Left, rect.Top - aa_radius ),
            new Offsetf( inner_uv_rect.Left, options.UvRect.Top ),
            packed_aa_color
        );
        aa_top_inner_right = builder.PushVertexRaw(
            new Offsetf( inner_rect.Right, rect.Top - aa_radius ),
            new Offsetf( inner_uv_rect.Right, options.UvRect.Top ),
            packed_aa_color
        );
        aa_top_right = builder.PushVertexRaw(
            new Offsetf( rect.Right + aa_radius, rect.Top - aa_radius ),
            new Offsetf( options.UvRect.Right, options.UvRect.Top ),
            packed_aa_color
        );
        aa_right_inner_top = builder.PushVertexRaw(
            new Offsetf( rect.Right + aa_radius, inner_rect.Top ),
            new Offsetf( options.UvRect.Right, inner_uv_rect.Top ),
            packed_aa_color
        );
        aa_right_inner_bottom = builder.PushVertexRaw(
            new Offsetf( rect.Right + aa_radius, inner_rect.Bottom ),
            new Offsetf( options.UvRect.Right, inner_uv_rect.Bottom ),
            packed_aa_color
        );
        aa_bottom_right = builder.PushVertexRaw(
            new Offsetf( rect.Right + aa_radius, rect.Bottom + aa_radius ),
            new Offsetf( options.UvRect.Right, options.UvRect.Bottom ),
            packed_aa_color
        );
        aa_bottom_inner_right = builder.PushVertexRaw(
            new Offsetf( inner_rect.Right, rect.Bottom + aa_radius ),
            new Offsetf( inner_uv_rect.Right, options.UvRect.Bottom ),
            packed_aa_color
        );
        aa_bottom_inner_left = builder.PushVertexRaw(
            new Offsetf( inner_rect.Left, rect.Bottom + aa_radius ),
            new Offsetf( inner_uv_rect.Left, options.UvRect.Bottom ),
            packed_aa_color
        );
        aa_bottom_left = builder.PushVertexRaw(
            new Offsetf( rect.Left - aa_radius, rect.Bottom + aa_radius ),
            new Offsetf( options.UvRect.Left, options.UvRect.Bottom ),
            packed_aa_color
        );
        aa_left_inner_bottom = builder.PushVertexRaw(
            new Offsetf( rect.Left - aa_radius, inner_rect.Bottom ),
            new Offsetf( options.UvRect.Left, inner_uv_rect.Bottom ),
            packed_aa_color
        );
        aa_left_inner_top = builder.PushVertexRaw(
            new Offsetf( rect.Left - aa_radius, inner_rect.Top ),
            new Offsetf( options.UvRect.Left, inner_uv_rect.Top ),
            packed_aa_color
        );
    }


    builder.PushQuad(outer_top_left, outer_top_inner_left, inner_top_left, outer_left_inner_top);
    builder.PushQuad(outer_top_inner_left, outer_top_inner_right, inner_top_right, inner_top_left);
    builder.PushQuad(outer_top_inner_right, outer_top_right, outer_right_inner_top, inner_top_right);
    builder.PushQuad(outer_left_inner_top, inner_top_left, inner_bottom_left, outer_left_inner_bottom);
    builder.PushQuad(inner_top_left, inner_top_right, inner_bottom_right, inner_bottom_left);
    builder.PushQuad(inner_top_right, outer_right_inner_top, outer_right_inner_bottom, inner_bottom_right);
    builder.PushQuad(outer_left_inner_bottom, inner_bottom_left, outer_bottom_inner_left, outer_bottom_left);
    builder.PushQuad(inner_bottom_left, inner_bottom_right, outer_bottom_inner_right, outer_bottom_inner_left);
    builder.PushQuad(inner_bottom_right, outer_right_inner_bottom, outer_bottom_right, outer_bottom_inner_right);


    if (has_aa)
    {
        builder.PushQuad(aa_top_left, aa_top_inner_left, outer_top_inner_left, outer_top_left);
        builder.PushQuad(aa_top_inner_left, aa_top_inner_right, outer_top_inner_right, outer_top_inner_left);
        builder.PushQuad(aa_top_inner_right, aa_top_right, outer_top_right, outer_top_inner_right);
        builder.PushQuad(outer_top_right, aa_top_right, aa_right_inner_top, outer_right_inner_top);
        builder.PushQuad(outer_right_inner_top, aa_right_inner_top, aa_right_inner_bottom, outer_right_inner_bottom);
        builder.PushQuad(outer_right_inner_bottom, aa_right_inner_bottom, aa_bottom_right, outer_bottom_right);
        builder.PushQuad(outer_bottom_right, aa_bottom_right, aa_bottom_inner_right, outer_bottom_inner_right);
        builder.PushQuad(outer_bottom_inner_right, aa_bottom_inner_right, aa_bottom_inner_left, outer_bottom_inner_left);
        builder.PushQuad(outer_bottom_inner_left, aa_bottom_inner_left, aa_bottom_left, outer_bottom_left);
        builder.PushQuad(aa_bottom_left, aa_left_inner_bottom, outer_left_inner_bottom, outer_bottom_left);
        builder.PushQuad(aa_left_inner_bottom, aa_left_inner_top, outer_left_inner_top, outer_left_inner_bottom);
        builder.PushQuad(aa_left_inner_top, aa_top_left, outer_top_left, outer_left_inner_top);
    }

    return true;
}
    // Original mesh.cpp:910 rrect_fill
    public static bool RRectFill(Mesh output,
    RRect rrect,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!rrect.IsFinite() || rrect.IsEmpty() || !gradient.IsValid())
    {
        return false;
    }
    if (RRectMeshHelper.IsRect(rrect))
    {
        return RectFill(output, rrect.Rect(), gradient, options);
    }


    MeshBuilder builder = new(output, rrect.Rect(), options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();
    path.AddRrect(rrect, EVGPathWinding.CW);
    path.FlattenTo(flatten, FlattenOptions(options));

    List<VGPathFlattenLine> split_lines = new();
    bool need_gradient_split = CollectCurveSplitLines(rrect.Rect(), gradient, split_lines);
    if (need_gradient_split)
    {
        flatten.AddLineIntersections(split_lines.ToArray());
    }

    VGFillOptions fill_options = need_gradient_split ?
        FillOptions(options) :
        SingleConvexFillOptions(options);
    if (!flatten.Fill(backend, fill_options, options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:962 rrect_stroke
    public static bool RRectStroke(Mesh output,
    RRect rrect,
    float thickness,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!rrect.IsFinite() || rrect.IsEmpty() || !float.IsFinite(thickness) ||
        thickness <= 0.0f || !gradient.IsValid())
    {
        return false;
    }
    if (RRectMeshHelper.IsRect(rrect))
    {
        return RectStroke(output, rrect.Rect(), thickness, gradient, options);
    }


    MeshBuilder builder = new(output, rrect.Rect(), options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten center_flatten = new();
    VGPathFlatten local_stroke_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten stroke_flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_stroke_flatten;
    path.Clear();
    path.AddRrect(rrect, EVGPathWinding.CW);
    path.FlattenTo(center_flatten, FlattenOptions(options));


    if (!center_flatten.StrokeContourTo(stroke_flatten, StrokeOptions(thickness, options)))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(rrect.Rect(), gradient, split_lines))
    {
        stroke_flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!stroke_flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1021 diff_rrect
    public static bool DiffRRect(Mesh output,
    RRect outer,
    RRect inner,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!outer.IsFinite() || outer.IsEmpty() || !inner.IsFinite() ||
        !gradient.IsValid())
    {
        return false;
    }
    if (RRectMeshHelper.IsRect(outer) && RRectMeshHelper.IsRect(inner))
    {
        return DiffRect(output, outer.Rect(), inner.Rect(), gradient, options);
    }

    if (inner.IsEmpty() || !outer.Rect().Overlaps(inner.Rect()))
    {
        return RRectFill(output, outer, gradient, options);
    }


    MeshBuilder builder = new(output, outer.Rect(), options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();
    path.AddRrect(outer, EVGPathWinding.CW);
    path.AddRrect(inner, EVGPathWinding.CCW);
    path.FlattenTo(flatten, FlattenOptions(options));

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(outer.Rect(), gradient, split_lines))
    {
        flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1077 rrect_border
    public static bool RRectBorder(Mesh output,
    RRect rrect,
    EdgeInsets inset,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!rrect.IsFinite() || rrect.IsEmpty() || !inset.IsNonNegative() ||
        !gradient.IsValid())
    {
        return false;
    }

    bool has_left = inset.Left > 0.0f;
    bool has_top = inset.Top > 0.0f;
    bool has_right = inset.Right > 0.0f;
    bool has_bottom = inset.Bottom > 0.0f;
    if (!has_left && !has_top && !has_right && !has_bottom)
    {
        return false;
    }

    RRect inner = inset.DeflateRrect(rrect);
    if (!inner.IsFinite())
    {
        return false;
    }

    if (RRectMeshHelper.IsRect(rrect))
    {
        return DiffRect(output, rrect.Rect(), inner.Rect(), gradient, options);
    }

    if (inner.IsEmpty() || !rrect.Rect().Overlaps(inner.Rect()))
    {
        return RRectFill(output, rrect, gradient, options);
    }
    if (has_left && has_top && has_right && has_bottom)
    {
        return DiffRRect(output, rrect, inner, gradient, options);
    }

    RRect normalized_outer = RRectMeshHelper.Normalized(rrect);


    MeshBuilder builder = new(output, rrect.Rect(), options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();

    bool[] sides = [
        has_top,
        has_right,
        has_bottom,
        has_left,
    ];
    RRect normalized_inner = RRectMeshHelper.Normalized(inner);
    RRectMeshHelper.AddPartialBorder(path, normalized_outer, normalized_inner, sides);

    path.FlattenTo(flatten, FlattenOptions(options));

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(rrect.Rect(), gradient, split_lines))
    {
        flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1163 superellipse_fill
    public static bool SuperellipseFill(Mesh output,
    Superellipse superellipse,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!superellipse.IsValid() || superellipse.IsEmpty() ||
        !gradient.IsValid())
    {
        return false;
    }
    Rectf bounds = superellipse.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();
    path.AddSuperellipse(superellipse, EVGPathWinding.CW);
    path.FlattenTo(flatten, FlattenOptions(options));

    List<VGPathFlattenLine> split_lines = new();
    bool need_gradient_split = CollectCurveSplitLines(bounds, gradient, split_lines);
    if (need_gradient_split)
    {
        flatten.AddLineIntersections(split_lines.ToArray());
    }

    VGFillOptions fill_options = superellipse.Exponent >= 1.0f && !need_gradient_split ?
        SingleConvexFillOptions(options) :
        FillOptions(options);
    if (!flatten.Fill(backend, fill_options, options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1217 superellipse_stroke
    public static bool SuperellipseStroke(Mesh output,
    Superellipse superellipse,
    float thickness,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!superellipse.IsValid() || superellipse.IsEmpty() ||
        !float.IsFinite(thickness) || thickness <= 0.0f ||
        !gradient.IsValid())
    {
        return false;
    }
    Rectf bounds = superellipse.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten center_flatten = new();
    VGPathFlatten local_stroke_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten stroke_flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_stroke_flatten;
    path.Clear();
    path.AddSuperellipse(superellipse, EVGPathWinding.CW);
    path.FlattenTo(center_flatten, FlattenOptions(options));


    if (!center_flatten.StrokeContourTo(stroke_flatten, StrokeOptions(thickness, options)))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        stroke_flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!stroke_flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1278 circle_filled
    public static bool CircleFilled(Mesh output,
    Circle circle,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!circle.IsValid() || circle.IsEmpty() ||
        !gradient.IsValid())
    {
        return false;
    }
    Rectf bounds = circle.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    List<VGPathFlattenLine> split_lines = new();
    if (!CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        if (!VGBasicPrimitives.Circle(
                backend,
                circle,
                options.PixelRatio,
                options.TessellationFactor,
                options.AaRadius
            ))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }

        return true;
    }

    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();
    path.AddCircle(circle, EVGPathWinding.CW);
    path.FlattenTo(flatten, FlattenOptions(options));
    flatten.AddLineIntersections(split_lines.ToArray());

    if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1342 circle_stroke
    public static bool CircleStroke(Mesh output,
    Circle circle,
    float thickness,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!circle.IsValid() || circle.IsEmpty() ||
        !float.IsFinite(thickness) || thickness <= 0.0f ||
        !gradient.IsValid())
    {
        return false;
    }
    Rectf bounds = circle.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten center_flatten = new();
    VGPathFlatten local_stroke_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten stroke_flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_stroke_flatten;
    path.Clear();
    path.AddCircle(circle, EVGPathWinding.CW);
    path.FlattenTo(center_flatten, FlattenOptions(options));


    if (!center_flatten.StrokeContourTo(stroke_flatten, StrokeOptions(thickness, options)))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        stroke_flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!stroke_flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1403 ellipse_filled
    public static bool EllipseFilled(Mesh output,
    Ellipse ellipse,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!ellipse.IsValid() || ellipse.IsEmpty() ||
        !gradient.IsValid())
    {
        return false;
    }
    Rectf bounds = ellipse.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    List<VGPathFlattenLine> split_lines = new();
    if (!CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        if (!VGBasicPrimitives.Ellipse(
                backend,
                ellipse,
                options.PixelRatio,
                options.TessellationFactor,
                options.AaRadius
            ))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }

        return true;
    }

    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();
    path.AddEllipse(ellipse, EVGPathWinding.CW);
    path.FlattenTo(flatten, FlattenOptions(options));
    flatten.AddLineIntersections(split_lines.ToArray());

    if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1467 ellipse_stroke
    public static bool EllipseStroke(Mesh output,
    Ellipse ellipse,
    float thickness,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!ellipse.IsValid() || ellipse.IsEmpty() ||
        !float.IsFinite(thickness) || thickness <= 0.0f ||
        !gradient.IsValid())
    {
        return false;
    }
    Rectf bounds = ellipse.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten center_flatten = new();
    VGPathFlatten local_stroke_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten stroke_flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_stroke_flatten;
    path.Clear();
    path.AddEllipse(ellipse, EVGPathWinding.CW);
    path.FlattenTo(center_flatten, FlattenOptions(options));


    if (!center_flatten.StrokeContourTo(stroke_flatten, StrokeOptions(thickness, options)))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        stroke_flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!stroke_flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1528 fan
    public static bool Fan(Mesh output,
    Arc arc,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!arc.IsValid() || arc.IsEmpty() ||
        !gradient.IsValid())
    {
        return false;
    }
    Arc body_arc = arc.Normalized();
    Rectf bounds = body_arc.Bounds().Unite(Rectf.Points(body_arc.Center, body_arc.Center));
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    List<VGPathFlattenLine> split_lines = new();
    if (!CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        if (!VGBasicPrimitives.Fan(
                backend,
                body_arc,
                new VGBasicPrimitiveOptions(){ PixelRatio = options.PixelRatio, TessellationFactor = options.TessellationFactor, AaRadius = options.AaRadius,
                }
            ))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }

        return true;
    }

    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();
    if (MathF.Abs(body_arc.SweepAngle) >= (MathF.PI * 2))
    {
        path.AddCircle(Circle.CenterRadius(body_arc.Center, body_arc.Radius), EVGPathWinding.CW);
    }
    else
    {
        path.MoveTo(body_arc.Center);
        path.LineTo(body_arc.StartPoint());
        body_arc.SplitToCubicBeziersFast(
            (CubicBezier cubic) => {
                path.CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
        path.Close(body_arc.SweepAngle >= 0.0f ? EVGPathWinding.CW : EVGPathWinding.CCW);
    }
    path.FlattenTo(flatten, FlattenOptions(options));
    flatten.AddLineIntersections(split_lines.ToArray());

    if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1609 arc_stroke
    public static bool ArcStroke(Mesh output,
    Arc arc,
    float thickness,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!arc.IsValid() || arc.IsEmpty() ||
        !float.IsFinite(thickness) || thickness <= 0.0f ||
        !gradient.IsValid())
    {
        return false;
    }
    Arc center_arc = arc.Normalized();
    Rectf bounds = center_arc.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten center_flatten = new();
    VGPathFlatten local_stroke_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten stroke_flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_stroke_flatten;
    path.Clear();
    if (MathF.Abs(center_arc.SweepAngle) >= (MathF.PI * 2))
    {
        path.AddCircle(Circle.CenterRadius(center_arc.Center, center_arc.Radius), EVGPathWinding.CW);
    }
    else
    {
        path.MoveTo(center_arc.StartPoint());
        center_arc.SplitToCubicBeziersFast(
            (CubicBezier cubic) => {
                path.CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
    }
    path.FlattenTo(center_flatten, FlattenOptions(options));


    if (!center_flatten.StrokeContourTo(stroke_flatten, StrokeOptions(thickness, options)))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        stroke_flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!stroke_flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1683 elliptical_fan
    public static bool EllipticalFan(Mesh output,
    EllipticalArc arc,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!arc.IsValid() || arc.IsEmpty() ||
        !gradient.IsValid())
    {
        return false;
    }
    EllipticalArc body_arc = arc.Normalized();
    Rectf bounds = body_arc.Bounds().Unite(Rectf.Points(body_arc.Center, body_arc.Center));
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    List<VGPathFlattenLine> split_lines = new();
    if (!CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        if (!VGBasicPrimitives.EllipticalFan(
                backend,
                body_arc,
                new VGBasicPrimitiveOptions(){ PixelRatio = options.PixelRatio, TessellationFactor = options.TessellationFactor, AaRadius = options.AaRadius,
                }
            ))
        {
            output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
            output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
            return false;
        }

        return true;
    }

    VGPath local_path = new();
    VGPathFlatten local_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_flatten;
    path.Clear();
    if (MathF.Abs(body_arc.SweepAngle) >= (MathF.PI * 2))
    {
        path.AddEllipse(
            Ellipse.CenterRadius(
                body_arc.Center,
                body_arc.RadiusX,
                body_arc.RadiusY,
                body_arc.Rotation
            ),
            EVGPathWinding.CW
        );
    }
    else
    {
        path.MoveTo(body_arc.Center);
        path.LineTo(body_arc.StartPoint());
        body_arc.SplitToCubicBeziersFast(
            (CubicBezier cubic) => {
                path.CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
        path.Close(body_arc.SweepAngle >= 0.0f ? EVGPathWinding.CW : EVGPathWinding.CCW);
    }
    path.FlattenTo(flatten, FlattenOptions(options));
    flatten.AddLineIntersections(split_lines.ToArray());

    if (!flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
    // Original mesh.cpp:1772 elliptical_arc_stroke
    public static bool EllipticalArcStroke(Mesh output,
    EllipticalArc arc,
    float thickness,
    ColorGradient gradient,
    BasicMeshOptions options)
{

    if (!arc.IsValid() || arc.IsEmpty() ||
        !float.IsFinite(thickness) || thickness <= 0.0f ||
        !gradient.IsValid())
    {
        return false;
    }
    EllipticalArc center_arc = arc.Normalized();
    Rectf bounds = center_arc.Bounds();
    if (!bounds.IsFinite() || bounds.IsEmpty())
    {
        return false;
    }


    MeshBuilder builder = new(output, bounds, options.UvRect, gradient, options.PremultiplyColor);
    ulong vertex_begin = builder.VertexStart();
    ulong index_begin = builder.IndexStart();
    VGBackend backend = builder.MakeVgBackend();


    VGPath local_path = new();
    VGPathFlatten center_flatten = new();
    VGPathFlatten local_stroke_flatten = new();
    VGPath path = options.ReusePath != null ? options.ReusePath : local_path;
    VGPathFlatten stroke_flatten = options.ReuseFlattenPath != null ? options.ReuseFlattenPath : local_stroke_flatten;
    path.Clear();
    if (MathF.Abs(center_arc.SweepAngle) >= (MathF.PI * 2))
    {
        path.AddEllipse(
            Ellipse.CenterRadius(
                center_arc.Center,
                center_arc.RadiusX,
                center_arc.RadiusY,
                center_arc.Rotation
            ),
            EVGPathWinding.CW
        );
    }
    else
    {
        path.MoveTo(center_arc.StartPoint());
        center_arc.SplitToCubicBeziersFast(
            (CubicBezier cubic) => {
                path.CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
    }
    path.FlattenTo(center_flatten, FlattenOptions(options));


    if (!center_flatten.StrokeContourTo(stroke_flatten, StrokeOptions(thickness, options)))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    List<VGPathFlattenLine> split_lines = new();
    if (CollectCurveSplitLines(bounds, gradient, split_lines))
    {
        stroke_flatten.AddLineIntersections(split_lines.ToArray());
    }

    if (!stroke_flatten.Fill(backend, FillOptions(options), options.ReuseFillWorkspace))
    {
        output.Vertices.RemoveRange((int)vertex_begin, output.Vertices.Count-(int)vertex_begin);
        output.Indices.RemoveRange((int)index_begin, output.Indices.Count-(int)index_begin);
        return false;
    }

    return true;
}
}
