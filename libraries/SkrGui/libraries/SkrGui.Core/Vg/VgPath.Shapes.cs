// Source: src/vg/vg_path.cpp:176-623 @ 611561f8.
using System.Diagnostics;
namespace SkrGui;
public sealed partial class VGPath
{
    public void ArcTo(Offsetf corner, Offsetf to, float radius)
    {
        Offsetf? cursor = CursorPos();
        if (!cursor.HasValue || !corner.IsFinite() || !to.IsFinite() ||
            !float.IsFinite(radius))
        {
            Debug.Assert(false , "VGPath.ArcTo requires an active cursor and finite parameters");
            return;
        }

        Arc arc = Arc.ArcTo(cursor.Value, corner, to, radius);
        if (!arc.IsValid() || arc.IsEmpty())
        {
            return;
        }

        Offsetf start = arc.StartPoint();
        if (!cursor.Value.NearlyEqual(start, PointEqualsTolerance))
        {
            LineTo(start);
        }

        arc.SplitToCubicBeziersFast(
            (CubicBezier cubic) => {
                CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
    }
    public void EllipticalArcTo(Offsetf to,
    Offsetf radii,
    float rotation,
    EEllipticalArcSize arc_size,
    EEllipticalArcSweep sweep)
    {
        Offsetf? cursor = CursorPos();
        if (!cursor.HasValue || !to.IsFinite() || !radii.IsFinite() ||
            !float.IsFinite(rotation))
        {
            Debug.Assert(false , "VGPath.EllipticalArcTo requires an active cursor and finite parameters");
            return;
        }

        EllipticalArc arc = EllipticalArc.ArcTo(
            cursor.Value,
            to,
            radii,
            rotation,
            arc_size,
            sweep
        );
        if (!arc.IsValid() || arc.IsEmpty())
        {
            return;
        }

        arc.SplitToCubicBeziersFast(
            (CubicBezier cubic) => {
                CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
    }
    public void AddCircle(Circle circle, EVGPathWinding winding = EVGPathWinding.CW)
    {
        if (!circle.IsValid())
        {
            Debug.Assert(false , "VGPath.AddCircle requires finite parameters");
            return;
        }
        if (circle.IsEmpty())
        {
            return;
        }

        MoveTo(circle.PointAtAngle(0.0f));
        circle.SplitToCubicBeziersFast(
            winding == EVGPathWinding.CW ? EShapeSampleDirection.Forward : EShapeSampleDirection.Reverse,
            (CubicBezier cubic) => {
                CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
        Close(winding);
    }
    public void AddEllipse(Ellipse ellipse, EVGPathWinding winding = EVGPathWinding.CW)
    {
        if (!ellipse.IsValid())
        {
            Debug.Assert(false , "VGPath.AddEllipse requires finite parameters");
            return;
        }
        if (ellipse.IsEmpty())
        {
            return;
        }

        MoveTo(ellipse.PointAtAngle(0.0f));
        ellipse.SplitToCubicBeziersFast(
            winding == EVGPathWinding.CW ? EShapeSampleDirection.Forward : EShapeSampleDirection.Reverse,
            (CubicBezier cubic) => {
                CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
        Close(winding);
    }
    public void AddSuperellipse(Superellipse superellipse,
    EVGPathWinding winding = EVGPathWinding.CW)
    {
        if (!superellipse.IsFinite())
        {
            Debug.Assert(false , "VGPath.AddSuperellipse requires finite parameters");
            return;
        }

        Superellipse value = superellipse.Normalized();
        if (!value.IsValid())
        {
            Debug.Assert(false , "VGPath.AddSuperellipse requires valid parameters");
            return;
        }
        if (value.IsEmpty())
        {
            return;
        }

        MoveTo(value.PointAtAngle(0.0f));
        value.SplitToCubicBeziersFast(
            winding == EVGPathWinding.CW ? EShapeSampleDirection.Forward : EShapeSampleDirection.Reverse,
            (CubicBezier cubic) => {
                CubicTo(cubic.Control1, cubic.Control2, cubic.End);
            }
        );
        Close(winding);
    }
    public void AddRrect(RRect rrect, EVGPathWinding winding = EVGPathWinding.CW)
    {
        if (!rrect.IsFinite())
        {
            Debug.Assert(false , "VGPath.AddRrect requires a finite rounded Rect");
            return;
        }
        if (rrect.IsEmpty())
        {
            return;
        }

        var positive_radius = (Radius radius) => {
            if (radius.X <= 0.0f || radius.Y <= 0.0f)
            {
                return Radius.Zero();
            }
            return Radius.Elliptical(
                radius.X,
                radius.Y
            );
        };
        RRect scaled =
            new RRect(
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

        var has_radius = (Radius radius) => {
            return radius.X > 0.0f && radius.Y > 0.0f;
        };
        if (
            !has_radius(scaled.TlRadius) &&
            !has_radius(scaled.TrRadius) &&
            !has_radius(scaled.BrRadius) &&
            !has_radius(scaled.BlRadius)
        )
        {
            AddRect(scaled.Rect(), winding);
            return;
        }

        var append_line = (Offsetf to) => {
            Offsetf? cursor = CursorPos();
            if (!cursor.HasValue || !cursor.Value.NearlyEqual(to, PointEqualsTolerance))
            {
                LineTo(to);
            }
        };
        var append_corner = (
                                       Offsetf center,
                                       Radius radius,
                                       float start_angle,
                                       float sweep_angle
                                   ) => {
            Offsetf end = center + new Offsetf(MathF.Cos(start_angle + sweep_angle) * radius.X, MathF.Sin(start_angle + sweep_angle) * radius.Y);
            if (radius.X <= 0.0f || radius.Y <= 0.0f)
            {
                append_line(end);
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
            Offsetf? cursor = CursorPos();
            if (!cursor.HasValue || !cursor.Value.NearlyEqual(start, PointEqualsTolerance))
            {
                append_line(start);
            }
            arc.SplitToCubicBeziersFast(
                (CubicBezier cubic) => {
                    CubicTo(cubic.Control1, cubic.Control2, cubic.End);
                }
            );
        };

        float l = scaled.Left;
        float t = scaled.Top;
        float r = scaled.Right;
        float b = scaled.Bottom;
        Radius tl = scaled.TlRadius;
        Radius tr = scaled.TrRadius;
        Radius br = scaled.BrRadius;
        Radius bl = scaled.BlRadius;

        Offsetf tl_center = new Offsetf(l + tl.X, t + tl.Y);
        Offsetf tr_center = new Offsetf(r - tr.X, t + tr.Y);
        Offsetf br_center = new Offsetf(r - br.X, b - br.Y);
        Offsetf bl_center = new Offsetf(l + bl.X, b - bl.Y);

        MoveTo(new Offsetf(l + tl.X, t));
        if (winding == EVGPathWinding.CW)
        {
            append_line(new Offsetf(r - tr.X, t));
            append_corner(tr_center, tr, -(MathF.PI * .5f), (MathF.PI * .5f));
            append_line(new Offsetf(r, b - br.Y));
            append_corner(br_center, br, 0.0f, (MathF.PI * .5f));
            append_line(new Offsetf(l + bl.X, b));
            append_corner(bl_center, bl, (MathF.PI * .5f), (MathF.PI * .5f));
            append_line(new Offsetf(l, t + tl.Y));
            append_corner(tl_center, tl, MathF.PI, (MathF.PI * .5f));
        }
        else
        {
            append_corner(tl_center, tl, -(MathF.PI * .5f), -(MathF.PI * .5f));
            append_line(new Offsetf(l, b - bl.Y));
            append_corner(bl_center, bl, MathF.PI, -(MathF.PI * .5f));
            append_line(new Offsetf(r - br.X, b));
            append_corner(br_center, br, (MathF.PI * .5f), -(MathF.PI * .5f));
            append_line(new Offsetf(r, t + tr.Y));
            append_corner(tr_center, tr, 0.0f, -(MathF.PI * .5f));
            append_line(new Offsetf(l + tl.X, t));
        }
        Close(winding);
    }
    public void AddSmoothRrect(RRect rrect,
    float exponent,
    EVGPathWinding winding = EVGPathWinding.CW)
    {
        if (!rrect.IsFinite())
        {
            Debug.Assert(false , "VGPath.AddSmoothRrect requires a finite rounded Rect");
            return;
        }
        if (!float.IsFinite(exponent) || exponent <= 0.0f)
        {
            Debug.Assert(false , "VGPath.AddSmoothRrect requires finite smooth corner parameters");
            return;
        }
        if (rrect.IsEmpty())
        {
            return;
        }

        var positive_radius = (Radius radius) => {
            if (radius.X <= 0.0f || radius.Y <= 0.0f)
            {
                return Radius.Zero();
            }
            return Radius.Elliptical(
                radius.X,
                radius.Y
            );
        };
        RRect scaled =
            new RRect(
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

        var has_radius = (Radius radius) => {
            return radius.X > 0.0f && radius.Y > 0.0f;
        };
        if (
            !has_radius(scaled.TlRadius) &&
            !has_radius(scaled.TrRadius) &&
            !has_radius(scaled.BrRadius) &&
            !has_radius(scaled.BlRadius)
        )
        {
            AddRect(scaled.Rect(), winding);
            return;
        }

        var append_line = (Offsetf to) => {
            Offsetf? cursor = CursorPos();
            if (!cursor.HasValue || !cursor.Value.NearlyEqual(to, PointEqualsTolerance))
            {
                LineTo(to);
            }
        };
        var append_smooth_corner = (
                                              Offsetf center,
                                              Radius radius,
                                              float start_angle,
                                              float sweep_angle
                                          ) => {
            SuperellipseArc arc = SuperellipseArc.CenterRadius(
                center,
                radius.X,
                radius.Y,
                0.0f,
                start_angle,
                sweep_angle,
                exponent
            );
            Offsetf start = arc.StartPoint();
            Offsetf? cursor = CursorPos();
            if (!cursor.HasValue || !cursor.Value.NearlyEqual(start, PointEqualsTolerance))
            {
                append_line(start);
            }
            arc.SplitToCubicBeziersFast(
                (CubicBezier cubic) => {
                    CubicTo(cubic.Control1, cubic.Control2, cubic.End);
                }
            );
        };

        float l = scaled.Left;
        float t = scaled.Top;
        float r = scaled.Right;
        float b = scaled.Bottom;
        Radius tl = scaled.TlRadius;
        Radius tr = scaled.TrRadius;
        Radius br = scaled.BrRadius;
        Radius bl = scaled.BlRadius;

        Offsetf tl_center = new Offsetf(l + tl.X, t + tl.Y);
        Offsetf tr_center = new Offsetf(r - tr.X, t + tr.Y);
        Offsetf br_center = new Offsetf(r - br.X, b - br.Y);
        Offsetf bl_center = new Offsetf(l + bl.X, b - bl.Y);

        MoveTo(new Offsetf(l + tl.X, t));
        if (winding == EVGPathWinding.CW)
        {
            append_line(new Offsetf(r - tr.X, t));
            append_smooth_corner(tr_center, tr, -(MathF.PI * .5f), (MathF.PI * .5f));
            append_line(new Offsetf(r, b - br.Y));
            append_smooth_corner(br_center, br, 0.0f, (MathF.PI * .5f));
            append_line(new Offsetf(l + bl.X, b));
            append_smooth_corner(bl_center, bl, (MathF.PI * .5f), (MathF.PI * .5f));
            append_line(new Offsetf(l, t + tl.Y));
            append_smooth_corner(tl_center, tl, MathF.PI, (MathF.PI * .5f));
        }
        else
        {
            append_smooth_corner(tl_center, tl, -(MathF.PI * .5f), -(MathF.PI * .5f));
            append_line(new Offsetf(l, b - bl.Y));
            append_smooth_corner(bl_center, bl, MathF.PI, -(MathF.PI * .5f));
            append_line(new Offsetf(r - br.X, b));
            append_smooth_corner(br_center, br, (MathF.PI * .5f), -(MathF.PI * .5f));
            append_line(new Offsetf(r, t + tr.Y));
            append_smooth_corner(tr_center, tr, 0.0f, -(MathF.PI * .5f));
            append_line(new Offsetf(l + tl.X, t));
        }
        Close(winding);
    }
}
