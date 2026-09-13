// Source: SkrGuiCore/math/shape/superellipse_arc.hpp @ 611561f8.
// Full geometry, sampling, projection and cubic conversion bodies.
using System.Diagnostics;
namespace SkrGui;
internal static class SuperellipseArcCubicBezierHelper
{
    // Original line 171: clean_trig_value.
    public static float CleanTrigValue(float value)
    {
            float kTrigSnapEpsilon = 1.0e-6f;
            if (VgScalar.Abs(value) <= kTrigSnapEpsilon)
            {
                return 0f;
            }
            if (VgScalar.Abs(value - 1f) <= kTrigSnapEpsilon)
            {
                return 1f;
            }
            if (VgScalar.Abs(value + 1f) <= kTrigSnapEpsilon)
            {
                return -1f;
            }
            return value;
        }
    // Original line 189: count_90_degree.
    public static uint Count90Degree(float SweepAngle)
    {
            if (SweepAngle == 0f)
            {
                return 0u;
            }

            float segment_count =
                VgScalar.Abs(SweepAngle) / (MathF.PI * .5f);
            return CppMath.Clamp(
                (uint)(VgScalar.Ceiling(CppMath.Max(0f, segment_count - 1.0e-6f))),
                1u,
                4u
            );
        }
    // Original line 205: count_tolerance.
    public static uint CountTolerance(float RadiusX,
        float RadiusY,
        float Exponent,
        float SweepAngle,
        float tolerance)
    {
            if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
            {
                Debug.Assert(false , "cubic bezier tolerance must be positive and finite");
                return 0u;
            }

            if (SweepAngle == 0f || Exponent <= 0f)
            {
                return 0u;
            }

            if (SuperellipseArc.IsEllipseExponent(Exponent))
            {
                float scaled_err = CppMath.Max(RadiusX, RadiusY) / tolerance;
                float n_err = CppMath.Max(
                    VgScalar.Pow(1.1163f * scaled_err, 1.0f / 6.0f),
                    3.999999f
                );
                float count_scope0 = VgScalar.Ceiling(
                    n_err * VgScalar.Abs(SweepAngle) / (MathF.PI * 2)
                );
                if (!VgScalar.IsFinite(count_scope0) ||
                    count_scope0 <= 0f ||
                    count_scope0 > (float)(uint.MaxValue))
                {
                    Debug.Assert(false , "cubic bezier segment count_scope0 must be finite");
                    return 0u;
                }

                return (uint)(count_scope0);
            }

            float radius = CppMath.Max(RadiusX, RadiusY);
            uint base_count = ShapeSampleHelper.CalcCircularSegmentCount(
                radius,
                SweepAngle,
                tolerance,
                false
            );
            base_count = CppMath.Max(base_count, Count90Degree(SweepAngle));

            float shape_factor = VgScalar.Sqrt(CppMath.Max(Exponent, 1f / Exponent));
            float count_scope1 = VgScalar.Ceiling(
                (float)(base_count) * CppMath.Max(1f, shape_factor * 0.5f)
            );
            if (!VgScalar.IsFinite(count_scope1) ||
                count_scope1 <= 0f ||
                count_scope1 > (float)(uint.MaxValue))
            {
                Debug.Assert(false , "cubic bezier segment count_scope1 must be finite");
                return 0u;
            }

            return (uint)(count_scope1);
        }
    // Original line 269: transform_point.
    public static Offsetf TransformPoint(Offsetf Center,
        float RadiusX,
        float RadiusY,
        float Rotation,
        float Exponent,
        float angle)
    {
            float power = 2f / Exponent;
            float cos_angle = CleanTrigValue(VgScalar.Cos(angle));
            float sin_angle = CleanTrigValue(VgScalar.Sin(angle));
            float cos_rotation = VgScalar.Cos(Rotation);
            float sin_rotation = VgScalar.Sin(Rotation);
            float local_x = RadiusX * SuperellipseArc.SignedPow(cos_angle, power);
            float local_y = RadiusY * SuperellipseArc.SignedPow(sin_angle, power);
            return Center +
                new Offsetf(
                       local_x * cos_rotation - local_y * sin_rotation,
                       local_x * sin_rotation + local_y * cos_rotation
                );
        }
    // Original line 292: transform_tangent.
    public static Offsetf TransformTangent(float RadiusX,
        float RadiusY,
        float Rotation,
        float angle)
    {
            float cos_angle = VgScalar.Cos(angle);
            float sin_angle = VgScalar.Sin(angle);
            float cos_rotation = VgScalar.Cos(Rotation);
            float sin_rotation = VgScalar.Sin(Rotation);
            float local_x = -RadiusX * sin_angle;
            float local_y = RadiusY * cos_angle;
            return new Offsetf(local_x * cos_rotation - local_y * sin_rotation,
                local_x * sin_rotation + local_y * cos_rotation);
        }
    // Original line 311: make_ellipse_segment.
    public static CubicBezier MakeEllipseSegment(SuperellipseArc arc,
        float StartAngle,
        float SweepAngle,
        Offsetf end)
    {
            float EndAngle = StartAngle + SweepAngle;
            float k = (4f / 3f) * VgScalar.Tan(SweepAngle * 0.25f);
            Offsetf start = TransformPoint(
                arc.Center,
                arc.RadiusX,
                arc.RadiusY,
                arc.Rotation,
                arc.Exponent,
                StartAngle
            );
            Offsetf start_tangent = TransformTangent(
                arc.RadiusX,
                arc.RadiusY,
                arc.Rotation,
                StartAngle
            );
            Offsetf end_tangent = TransformTangent(
                arc.RadiusX,
                arc.RadiusY,
                arc.Rotation,
                EndAngle
            );

            return CubicBezier.CubicTo(
                start,
                start + start_tangent * k,
                end - end_tangent * k,
                end
            );
        }
    // Original line 349: make_catmull_segment.
    public static CubicBezier MakeCatmullSegment(SuperellipseArc arc,
        float StartAngle,
        float SweepAngle,
        Offsetf end)
    {
            float EndAngle = StartAngle + SweepAngle;
            Offsetf p0 = arc.PointAtAngle(StartAngle - SweepAngle);
            Offsetf p1 = arc.PointAtAngle(StartAngle);
            Offsetf p2 = end;
            Offsetf p3 = arc.PointAtAngle(EndAngle + SweepAngle);

            return CubicBezier.CubicTo(
                p1,
                p1 + (p2 - p0) * (1f / 6f),
                p2 - (p3 - p1) * (1f / 6f),
                p2
            );
        }
    // Original line 370: make_segment.
    public static CubicBezier MakeSegment(SuperellipseArc arc,
        float StartAngle,
        float SweepAngle,
        Offsetf end)
    {
            if (SuperellipseArc.IsEllipseExponent(arc.Exponent))
            {
                return MakeEllipseSegment(arc, StartAngle, SweepAngle, end);
            }
            return MakeCatmullSegment(arc, StartAngle, SweepAngle, end);
        }
    // Original line 384: sample_derivative.
    public static Offsetf SampleDerivative(SuperellipseArc arc,
        float angle,
        float SweepAngle,
        bool at_start)
    {


            float sign = SweepAngle >= 0f ? 1f : -1f;
            float h = CppMath.Max(VgScalar.Abs(SweepAngle) * 0.25f, 1.0e-5f);
            Offsetf point = arc.PointAtAngle(angle);
            if (at_start)
            {
                Offsetf next = arc.PointAtAngle(angle + sign * h);
                return (next - point) / (sign * h);
            }

            Offsetf previous = arc.PointAtAngle(angle - sign * h);
            return (point - previous) / (sign * h);
        }
    // Original line 406: make_adaptive_segment.
    public static CubicBezier MakeAdaptiveSegment(SuperellipseArc arc,
        float StartAngle,
        float SweepAngle)
    {
            float EndAngle = StartAngle + SweepAngle;
            Offsetf start = arc.PointAtAngle(StartAngle);
            Offsetf end = arc.PointAtAngle(EndAngle);
            if (VgScalar.NearlyEqual(arc.Exponent, 1f))
            {
                return CubicBezier.Line(start, end);
            }

            Offsetf start_derivative = SampleDerivative(arc, StartAngle, SweepAngle, true);
            Offsetf end_derivative = SampleDerivative(arc, EndAngle, SweepAngle, false);
            return CubicBezier.CubicTo(
                start,
                start + start_derivative * (SweepAngle / 3f),
                end - end_derivative * (SweepAngle / 3f),
                end
            );
        }
    // Original line 430: adaptive_segment_error.
    public static float AdaptiveSegmentError(SuperellipseArc arc,
        CubicBezier cubic,
        float StartAngle,
        float SweepAngle,
        float tolerance)
    {


            float[] samples = [ 0.125f, 0.25f, 0.5f, 0.75f, 0.875f ];
            float max_error = 0f;
            foreach (float u in samples)
            {
                Offsetf shape_point = arc.PointAtAngle(StartAngle + SweepAngle * u);
                if (VgScalar.NearlyEqual(arc.Exponent, 1f))
                {
                    max_error = CppMath.Max(
                        max_error,
                        DistanceToSegment(shape_point, cubic.StartPoint(), cubic.EndPoint())
                    );
                }
                else
                {
                    max_error = CppMath.Max(max_error, cubic.Distance(shape_point, tolerance * 0.25f));
                }
            }
            return max_error;
        }
    // Original line 460: distance_to_segment.
    public static float DistanceToSegment(Offsetf point,
        Offsetf line_start,
        Offsetf line_end)
    {
            Offsetf line = line_end - line_start;
            float length_sq = line.LengthSquared();
            if (length_sq <= 1.0e-12f)
            {
                return (point - line_start).Length();
            }

            float t = CppMath.Clamp(
                ((point - line_start).X * line.X + (point - line_start).Y * line.Y) / length_sq,
                0f,
                1f
            );
            return (point - Offsetf.Lerp(line_start, line_end, t)).Length();
        }
    // Original line 482: split_adaptive_span.
    public static void SplitAdaptiveSpan(SuperellipseArc arc,
        float StartAngle,
        float SweepAngle,
        float tolerance,
        uint max_depth,
        uint depth,
        Action<CubicBezier> func)
    {
            CubicBezier cubic = MakeAdaptiveSegment(arc, StartAngle, SweepAngle);
            if (depth >= max_depth ||
                AdaptiveSegmentError(arc, cubic, StartAngle, SweepAngle, tolerance) <= tolerance)
            {
                func(cubic);
                return;
            }

            float half_sweep = SweepAngle * 0.5f;
            SplitAdaptiveSpan(
                arc,
                StartAngle,
                half_sweep,
                tolerance,
                max_depth,
                depth + 1u,
                func
            );
            SplitAdaptiveSpan(
                arc,
                StartAngle + half_sweep,
                half_sweep,
                tolerance,
                max_depth,
                depth + 1u,
                func
            );
        }
    // Original line 522: split_adaptive.
    public static void SplitAdaptive(SuperellipseArc arc,
        float tolerance,
        uint max_depth,
        Action<CubicBezier> func)
    {


            float kBoundaryEpsilon = 1.0e-5f;
            uint resolved_max_depth = ShapeSampleHelper.ResolveMaxDepth(max_depth);
            float EndAngle = arc.StartAngle + arc.SweepAngle;
            float direction = arc.SweepAngle >= 0f ? 1f : -1f;
            float current_angle = arc.StartAngle;

            if (direction > 0f)
            {
                float next_boundary =
                    VgScalar.Ceiling((current_angle + kBoundaryEpsilon) / (MathF.PI * .5f)) *
                    (MathF.PI * .5f);
                while (next_boundary < EndAngle - kBoundaryEpsilon)
                {
                    SplitAdaptiveSpan(
                        arc,
                        current_angle,
                        next_boundary - current_angle,
                        tolerance,
                        resolved_max_depth,
                        0u,
                        func
                    );
                    current_angle = next_boundary;
                    next_boundary += (MathF.PI * .5f);
                }
            }
            else
            {
                float next_boundary =
                    VgScalar.Floor((current_angle - kBoundaryEpsilon) / (MathF.PI * .5f)) *
                    (MathF.PI * .5f);
                while (next_boundary > EndAngle + kBoundaryEpsilon)
                {
                    SplitAdaptiveSpan(
                        arc,
                        current_angle,
                        next_boundary - current_angle,
                        tolerance,
                        resolved_max_depth,
                        0u,
                        func
                    );
                    current_angle = next_boundary;
                    next_boundary -= (MathF.PI * .5f);
                }
            }

            SplitAdaptiveSpan(
                arc,
                current_angle,
                EndAngle - current_angle,
                tolerance,
                resolved_max_depth,
                0u,
                func
            );
        }
    // Original line 589: count_adaptive.
    public static uint CountAdaptive(SuperellipseArc arc,
        float tolerance,
        uint max_depth)
    {
            uint count = 0u;
            SplitAdaptive(
                arc,
                tolerance,
                max_depth,
                (CubicBezier ignored) => {
                    if (count < uint.MaxValue)
                    {
                        ++count;
                    }
                }
            );
            return count;
        }
    // Original line 610: fast_span_segment_count.
    public static uint FastSpanSegmentCount(float SweepAngle, uint quadrant_segment_count)
    {
            float segment_count =
                VgScalar.Abs(SweepAngle) / (MathF.PI * .5f) *
                (float)(quadrant_segment_count);
            return CppMath.Max(
                1u,
                (uint)(VgScalar.Ceiling(CppMath.Max(0f, segment_count - 1.0e-6f)))
            );
        }
    // Original line 622: split_fast_span.
    public static void SplitFastSpan(SuperellipseArc arc,
        float StartAngle,
        float SweepAngle,
        uint quadrant_segment_count,
        Action<CubicBezier> func)
    {


            uint count = FastSpanSegmentCount(SweepAngle, quadrant_segment_count);
            float segment_sweep = SweepAngle / (float)(count);
            for (uint i = 0u; i < count; ++i)
            {
                float segment_start = StartAngle + segment_sweep * (float)(i);
                float segment_end = segment_start + segment_sweep;
                Offsetf end = arc.PointAtAngle(segment_end);
                CubicBezier cubic = SuperellipseArc.IsEllipseExponent(arc.Exponent) ?
                    MakeSegment(arc, segment_start, segment_sweep, end) :
                    MakeAdaptiveSegment(arc, segment_start, segment_sweep);
                func(cubic);
            }
        }
    // Original line 647: split_fast.
    public static void SplitFast(SuperellipseArc arc, Action<CubicBezier> func)
    {


            float kBoundaryEpsilon = 1.0e-5f;
            uint quadrant_segment_count =
                SuperellipseCubicBezierHelper.FastQuadrantSegmentCount(arc.Exponent);
            float EndAngle = arc.StartAngle + arc.SweepAngle;
            float direction = arc.SweepAngle >= 0f ? 1f : -1f;
            float current_angle = arc.StartAngle;

            if (direction > 0f)
            {
                float next_boundary =
                    VgScalar.Ceiling((current_angle + kBoundaryEpsilon) / (MathF.PI * .5f)) *
                    (MathF.PI * .5f);
                while (next_boundary < EndAngle - kBoundaryEpsilon)
                {
                    SplitFastSpan(
                        arc,
                        current_angle,
                        next_boundary - current_angle,
                        quadrant_segment_count,
                        func
                    );
                    current_angle = next_boundary;
                    next_boundary += (MathF.PI * .5f);
                }
            }
            else
            {
                float next_boundary =
                    VgScalar.Floor((current_angle - kBoundaryEpsilon) / (MathF.PI * .5f)) *
                    (MathF.PI * .5f);
                while (next_boundary > EndAngle + kBoundaryEpsilon)
                {
                    SplitFastSpan(
                        arc,
                        current_angle,
                        next_boundary - current_angle,
                        quadrant_segment_count,
                        func
                    );
                    current_angle = next_boundary;
                    next_boundary -= (MathF.PI * .5f);
                }
            }

            SplitFastSpan(
                arc,
                current_angle,
                EndAngle - current_angle,
                quadrant_segment_count,
                func
            );
        }
    // Original line 704: count_fast.
    public static uint CountFast(SuperellipseArc arc)
    {
            uint count = 0u;
            SplitFast(
                arc,
                (CubicBezier ignored) => {
                    if (count < uint.MaxValue)
                    {
                        ++count;
                    }
                }
            );
            return count;
        }
}
public struct SuperellipseArc : IEquatable<SuperellipseArc>
{
    public Offsetf Center;
    public float RadiusX;
    public float RadiusY;
    public float Rotation;
    public float Exponent;
    public float StartAngle;
    public float SweepAngle;
    public SuperellipseArc() { Exponent = 2; }
    public SuperellipseArc(Offsetf center, float radius_x, float radius_y, float rotation, float exponent, float start_angle, float sweep_angle) { Center = center; RadiusX = radius_x; RadiusY = radius_y; Rotation = rotation; Exponent = exponent; StartAngle = start_angle; SweepAngle = sweep_angle; }
    // Original line 767: Zero.
    public static SuperellipseArc Zero()
    {
        return new SuperellipseArc();
    }
    // Original line 771: Invalid.
    public static SuperellipseArc Invalid()
    {
        float kNaN = float.NaN;
        return new SuperellipseArc(new Offsetf(kNaN, kNaN), kNaN, kNaN, kNaN, kNaN, kNaN, kNaN);
    }
    // Original line 776: CenterRadius.
    public static SuperellipseArc CenterRadius(Offsetf Center,
    float RadiusX,
    float RadiusY,
    float Rotation,
    float StartAngle,
    float SweepAngle,
    float Exponent)
    {
        return new SuperellipseArc(Center,
            RadiusX,
            RadiusY,
            Rotation,
            Exponent,
            StartAngle,
            SweepAngle);
    }
    // Original line 798: is_empty.
    public bool IsEmpty()
    {
        return RadiusX <= 0f || RadiusY <= 0f || SweepAngle == 0f;
    }
    // Original line 802: is_valid.
    public bool IsValid()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(Exponent) && VgScalar.IsFinite(StartAngle) &&
            VgScalar.IsFinite(SweepAngle) && RadiusX >= 0f &&
            RadiusY >= 0f && Exponent > 0f;
    }
    // Original line 810: is_normalized.
    public bool IsNormalized()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(Exponent) && VgScalar.IsFinite(StartAngle) &&
            VgScalar.IsFinite(SweepAngle) && RadiusX >= 0f &&
            RadiusY >= 0f && Exponent > 0f &&
            Rotation == WrapAngle(Rotation) &&
            StartAngle == WrapAngle(StartAngle) &&
            VgScalar.Abs(SweepAngle) <= (MathF.PI * 2);
    }
    // Original line 821: is_finite.
    public bool IsFinite()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(Exponent) && VgScalar.IsFinite(StartAngle) &&
            VgScalar.IsFinite(SweepAngle);
    }
    // Original line 828: has_nan.
    public bool HasNan()
    {
        return VgScalar.IsNaN(Center.X) || VgScalar.IsNaN(Center.Y) ||
            VgScalar.IsNaN(RadiusX) || VgScalar.IsNaN(RadiusY) ||
            VgScalar.IsNaN(Rotation) || VgScalar.IsNaN(Exponent) ||
            VgScalar.IsNaN(StartAngle) || VgScalar.IsNaN(SweepAngle);
    }
    // Original line 835: is_point.
    public bool IsPoint()
    {
        return RadiusX >= 0f && RadiusY >= 0f &&
            ((RadiusX == 0f && RadiusY == 0f) || SweepAngle == 0f);
    }
    // Original line 842: end_angle.
    public float EndAngle()
    {
        return StartAngle + SweepAngle;
    }
    // Original line 846: length.
    public float Length(float tolerance = .01f)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty() || value.IsPoint())
        {
            return 0f;
        }

        return ApproximateLengthSpan(
            value,
            0f,
            1f,
            value.StartPoint(),
            value.EndPoint(),
            PositiveTolerance(tolerance),
            0u
        );
    }
    // Original line 864: start_point.
    public Offsetf StartPoint()
    {
        return PointAtAngle(StartAngle);
    }
    // Original line 868: end_point.
    public Offsetf EndPoint()
    {
        return PointAtAngle(EndAngle());
    }
    // Original line 872: rect.
    public Rectf Rect()
    {
        SuperellipseArc value = Normalized();
        if (!value.IsFinite() || value.IsEmpty())
        {
            return Rectf.Points(value.Center, value.Center);
        }

        if (VgScalar.Abs(value.SweepAngle) >= (MathF.PI * 2))
        {
            return Superellipse.CenterRadius(
                       value.Center,
                       value.RadiusX,
                       value.RadiusY,
                       value.Rotation,
                       value.Exponent
            )
                .Rect();
        }

        Offsetf start = value.StartPoint();
        Offsetf end = value.EndPoint();
        Rectf Bounds = Rectf.Points(start, end);

        if (IsEllipseExponent(value.Exponent))
        {
            float cos_rotation = VgScalar.Cos(value.Rotation);
            float sin_rotation = VgScalar.Sin(value.Rotation);
            float half_turn = (MathF.PI * 2) * 0.5f;
            float x_extremum =
                VgScalar.Atan2(-value.RadiusY * sin_rotation, value.RadiusX * cos_rotation);
            float y_extremum =
                VgScalar.Atan2(value.RadiusY * cos_rotation, value.RadiusX * sin_rotation);
            float[] candidates = [
                x_extremum,
                x_extremum + half_turn,
                y_extremum,
                y_extremum + half_turn,
            ];

            foreach (float angle in candidates)
            {
                if (ContainsAngle(angle, value.StartAngle, value.SweepAngle))
                {
                    Bounds = Bounds.Hold(value.PointAtAngle(angle));
                }
            }
            return Bounds;
        }

        float[] cardinal_angles = [
            0f,
            (MathF.PI * .5f),
            MathF.PI,
            -(MathF.PI * .5f),
        ];
        foreach (float angle in cardinal_angles)
        {
            if (ContainsAngle(angle, value.StartAngle, value.SweepAngle))
            {
                Bounds = Bounds.Hold(value.PointAtAngle(angle));
            }
        }

        uint count = CalcSampleBoundCount(value.SweepAngle);
        for (uint i = 1u; i < count; ++i)
        {
            float t = (float)(i) / (float)(count);
            Bounds = Bounds.Hold(value.PointAtT(t));
        }
        return Bounds;
    }
    // Original line 944: bounds.
    public Rectf Bounds()
    {
        return Rect();
    }
    // Original line 948: contains_angle.
    public bool ContainsAngle(float angle)
    {
        return ContainsAngle(angle, StartAngle, SweepAngle);
    }
    // Original line 952: create_sampler.
    public SuperellipseSampler CreateSampler(float angle = 0)
    {
        return new SuperellipseSampler(angle, Rotation, Exponent);
    }
    // Original line 956: point_at_angle.
    public Offsetf PointAtAngle(float angle)
    {
        SuperellipseSampler sampler = CreateSampler(angle);
        return sampler.SamplePoint(Center, RadiusX, RadiusY);
    }
    // Original line 961: point_at_ratio.
    public Offsetf PointAtRatio(float ratio)
    {
        return PointAtT(ratio);
    }
    // Original line 965: point_at_t.
    public Offsetf PointAtT(float t)
    {
        return PointAtAngle(StartAngle + SweepAngle * t);
    }
    // Original line 971: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return ShapeSampleHelper.CalcGeometricTolerance(
            tessellation_factor,
            pixel_ratio
        );
    }
    // Original line 981: estimate_segment_count.
    public uint EstimateSegmentCount(float tolerance)
    {

        SuperellipseArc value = Normalized();
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f || !value.IsValid() || value.IsEmpty())
        {
            return 0u;
        }



        if (IsDiamondExponent(value.Exponent))
        {
            return CalcDiamondSegmentCount(value.StartAngle, value.SweepAngle);
        }

        if (IsEllipseExponent(value.Exponent))
        {
            return EllipticalArc.CenterRadius(
                       value.Center,
                       value.RadiusX,
                       value.RadiusY,
                       value.Rotation,
                       value.StartAngle,
                       value.SweepAngle
            )
                .EstimateSegmentCount(tolerance);
        }

        return SuperellipseEstimateHelper.EstimateArc(
            value.RadiusX,
            value.RadiusY,
            value.Exponent,
            value.StartAngle,
            value.SweepAngle,
            tolerance
        );
    }
    // Original line 1020: sample.
    public void Sample(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        ShapeSampleHelper.SampleOpenUniform(
            desc.SegmentCount,
            desc.Direction,
            (float t) => { return value.PointAtT(t); },
            functor
        );
    }
    // Original line 1039: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampleHelper.SampleArc(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            Superellipse.CenterRadius(
                value.Center,
                value.RadiusX,
                value.RadiusY,
                value.Rotation,
                value.Exponent
            ),
            value.StartAngle,
            value.SweepAngle,
            functor
        );
    }
    // Original line 1067: sample.
    public void Sample(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        ShapeSampleHelper.SampleOpenByStepLength(
            desc.StepLength,
            ShapeSampleHelper.EstimateStepLengthTolerance(desc.StepLength),
            desc.MaxDepth,
            desc.Direction,
            (float t) => { return value.PointAtT(t); },
            functor
        );
    }
    // Original line 1088: sample_with_sampler.
    public void SampleWithSampler(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float,SuperellipseSampler> functor)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleOpenUniformWithSampler(
            desc.SegmentCount,
            desc.Direction,
            ref sampler,
            (ref SuperellipseSampler sampler, float t) => {
                sampler.SetAngle(value.StartAngle + value.SweepAngle * t);
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 1112: sample_with_sampler.
    public void SampleWithSampler(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float,SuperellipseSampler> functor)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampler emit_sampler = value.CreateSampler();
        SuperellipseSampleHelper.SampleArc(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            Superellipse.CenterRadius(
                value.Center,
                value.RadiusX,
                value.RadiusY,
                value.Rotation,
                value.Exponent
            ),
            value.StartAngle,
            value.SweepAngle,
            (Offsetf ignored, float t) => {
                emit_sampler.SetAngle(value.StartAngle + value.SweepAngle * t);
                Offsetf point = emit_sampler.SamplePoint(
                    value.Center,
                    value.RadiusX,
                    value.RadiusY
                );
                functor(point, t, emit_sampler);
            }
        );
    }
    // Original line 1149: sample_with_sampler.
    public void SampleWithSampler(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float,SuperellipseSampler> functor)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleOpenByStepLengthWithSampler(
            desc.StepLength,
            ShapeSampleHelper.EstimateStepLengthTolerance(desc.StepLength),
            desc.MaxDepth,
            desc.Direction,
            ref sampler,
            (ref SuperellipseSampler sampler, float t) => {
                sampler.SetAngle(value.StartAngle + value.SweepAngle * t);
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 1176: cubic_bezier_count.
    public uint CubicBezierCount(float tolerance, uint max_depth = 10)
    {
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
        {
            Debug.Assert(false , "cubic bezier tolerance must be positive and finite");
            return 0u;
        }

        if (!IsValid() || IsEmpty())
        {
            return 0u;
        }

        SuperellipseArc value = Normalized();
        if (!SuperellipseArc.IsEllipseExponent(value.Exponent))
        {
            return SuperellipseArcCubicBezierHelper.CountAdaptive(value, tolerance, max_depth);
        }

        return SuperellipseArcCubicBezierHelper.CountTolerance(
            value.RadiusX,
            value.RadiusY,
            value.Exponent,
            value.SweepAngle,
            tolerance
        );
    }
    // Original line 1203: cubic_bezier_count_fast.
    public uint CubicBezierCountFast()
    {
        if (!IsValid() || IsEmpty())
        {
            return 0u;
        }

        return SuperellipseArcCubicBezierHelper.CountFast(Normalized());
    }
    // Original line 1215: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance,
    Action<CubicBezier> func,
    uint max_depth = 10)
    {
        uint count = CubicBezierCount(tolerance, max_depth);
        if (count == 0u)
        {
            return;
        }

        SuperellipseArc value = Normalized();
        if (!SuperellipseArc.IsEllipseExponent(value.Exponent))
        {
            SuperellipseArcCubicBezierHelper.SplitAdaptive(
                value,
                tolerance,
                max_depth,
                func
            );
            return;
        }

        float segment_sweep = value.SweepAngle / (float)(count);
        Offsetf precise_end = value.EndPoint();
        for (uint i = 0u; i < count; ++i)
        {
            float segment_start =
                value.StartAngle + segment_sweep * (float)(i);
            float segment_end =
                value.StartAngle + segment_sweep * (float)(i + 1u);
            Offsetf end =
                (i + 1u == count) ? precise_end : value.PointAtAngle(segment_end);
            CubicBezier cubic = SuperellipseArcCubicBezierHelper.MakeSegment(
                value,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 1259: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(Action<CubicBezier> func)
    {
        if (!IsValid() || IsEmpty())
        {
            return;
        }

        SuperellipseArcCubicBezierHelper.SplitFast(
            Normalized(),
            func
        );
    }
    // Original line 1273: normalized.
    public SuperellipseArc Normalized()
    {
        SuperellipseArc result = this;

        bool flip_x = VgScalar.IsFinite(result.RadiusX) && result.RadiusX < 0f;
        bool flip_y = VgScalar.IsFinite(result.RadiusY) && result.RadiusY < 0f;
        result.RadiusX = VgScalar.Abs(result.RadiusX);
        result.RadiusY = VgScalar.Abs(result.RadiusY);

        if (flip_x && flip_y)
        {
            result.StartAngle += MathF.PI;
        }
        else if (flip_x)
        {
            result.StartAngle = MathF.PI - result.StartAngle;
            result.SweepAngle = -result.SweepAngle;
        }
        else if (flip_y)
        {
            result.StartAngle = -result.StartAngle;
            result.SweepAngle = -result.SweepAngle;
        }

        result.Rotation = WrapAngle(result.Rotation);
        result.StartAngle = WrapAngle(result.StartAngle);
        result.SweepAngle = ClampSweep(result.SweepAngle);
        return result;
    }
    // Original line 1302: shift.
    public SuperellipseArc Shift(Offsetf offset)
    {
        return new SuperellipseArc(Center + offset,
            RadiusX,
            RadiusY,
            Rotation,
            Exponent,
            StartAngle,
            SweepAngle);
    }
    // Original line 1314: inflate.
    public SuperellipseArc Inflate(float delta)
    {
        return new SuperellipseArc(Center,
            CppMath.Max(0f, RadiusX + delta),
            CppMath.Max(0f, RadiusY + delta),
            Rotation,
            Exponent,
            StartAngle,
            SweepAngle);
    }
    // Original line 1326: deflate.
    public SuperellipseArc Deflate(float delta)
    {
        return Inflate(-delta);
    }
    // Original line 1330: trim.
    public SuperellipseArc Trim(float start_t, float end_t)
    {
        SuperellipseArc value = Normalized();
        if (!value.IsValid())
        {
            return value;
        }

        float clamped_start = ClampUnit(start_t);
        float clamped_end = ClampUnit(end_t);
        if (clamped_start > clamped_end)
        {
            return value.Trim(clamped_end, clamped_start).Reversed();
        }

        return new SuperellipseArc(value.Center,
            value.RadiusX,
            value.RadiusY,
            value.Rotation,
            value.Exponent,
            value.StartAngle + value.SweepAngle * clamped_start,
            value.SweepAngle * (clamped_end - clamped_start));
    }
    // Original line 1355: reversed.
    public SuperellipseArc Reversed()
    {
        return new SuperellipseArc(Center,
            RadiusX,
            RadiusY,
            Rotation,
            Exponent,
            EndAngle(),
            -SweepAngle);
    }
    // Original line 1382: _wrap_angle.
    internal static float WrapAngle(float angle)
    {
        if (!VgScalar.IsFinite(angle))
        {
            return angle;
        }

        float wrapped = CppMath.Fmod(angle, (MathF.PI * 2));
        if (wrapped <= -MathF.PI)
        {
            wrapped += (MathF.PI * 2);
        }
        else if (wrapped > MathF.PI)
        {
            wrapped -= (MathF.PI * 2);
        }
        return wrapped;
    }
    // Original line 1400: _clamp_sweep.
    internal static float ClampSweep(float SweepAngle)
    {
        if (!VgScalar.IsFinite(SweepAngle))
        {
            return SweepAngle;
        }

        if (SweepAngle > (MathF.PI * 2))
        {
            return (MathF.PI * 2);
        }
        if (SweepAngle < -(MathF.PI * 2))
        {
            return -(MathF.PI * 2);
        }
        return SweepAngle;
    }
    // Original line 1417: _clamp_unit.
    internal static float ClampUnit(float t)
    {
        if (!VgScalar.IsFinite(t))
        {
            return 0f;
        }
        return CppMath.Clamp(t, 0f, 1f);
    }
    // Original line 1425: _positive_tolerance.
    internal static float PositiveTolerance(float tolerance)
    {
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
        {
            return 0.01f;
        }
        return tolerance;
    }
    // Original line 1433: _signed_pow.
    internal static float SignedPow(float value, float power)
    {
        float magnitude = VgScalar.Pow(VgScalar.Abs(value), power);
        return value < 0f ? -magnitude : magnitude;
    }
    // Original line 1438: _is_ellipse_exponent.
    internal static bool IsEllipseExponent(float Exponent)
    {
        return VgScalar.NearlyEqual(Exponent, 2f);
    }
    // Original line 1442: _is_diamond_exponent.
    internal static bool IsDiamondExponent(float Exponent)
    {
        return VgScalar.NearlyEqual(Exponent, 1f);
    }
    // Original line 1446: _calc_sample_bound_count.
    internal static uint CalcSampleBoundCount(float SweepAngle)
    {
        float count = VgScalar.Ceiling(VgScalar.Abs(SweepAngle) / (MathF.PI * 2) * 256f);
        return CppMath.Clamp((uint)(CppMath.Max(8f, count)), 8u, 512u);
    }
    // Original line 1451: _calc_diamond_segment_count.
    internal static uint CalcDiamondSegmentCount(float StartAngle,
    float SweepAngle)
    {
        if (SweepAngle == 0f)
        {
            return 0u;
        }

        float k_epsilon = 1.0e-6f;
        float start_t = StartAngle / (MathF.PI * 2);
        float end_t = start_t + SweepAngle / (MathF.PI * 2);



        uint segment_count = 1u;
        float current_t = start_t;
        while (VgScalar.Abs(current_t - end_t) > k_epsilon)
        {
            float next_t = 0f;
            if (end_t > current_t)
            {
                float boundary_index_scope0 = VgScalar.Floor(current_t * 4f + k_epsilon) + 1f;
                float boundary_t_scope1 = boundary_index_scope0 * 0.25f;
                next_t = boundary_t_scope1 >= end_t - k_epsilon ? end_t : boundary_t_scope1;
            }
            else
            {
                float boundary_index_scope2 = VgScalar.Ceiling(current_t * 4f - k_epsilon) - 1f;
                float boundary_t_scope3 = boundary_index_scope2 * 0.25f;
                next_t = boundary_t_scope3 <= end_t + k_epsilon ? end_t : boundary_t_scope3;
            }

            if (VgScalar.Abs(next_t - end_t) <= k_epsilon)
            {
                break;
            }

            ++segment_count;
            current_t = next_t;
        }
        return segment_count;
    }
    // Original line 1495: _approximate_length_span.
    internal static float ApproximateLengthSpan(SuperellipseArc arc,
    float start_ratio,
    float end_ratio,
    Offsetf StartPoint,
    Offsetf EndPoint,
    float tolerance,
    uint depth)
    {
        float mid_ratio = (start_ratio + end_ratio) * 0.5f;
        Offsetf mid_point = arc.PointAtT(mid_ratio);
        float polyline_length =
            (mid_point - StartPoint).Length() + (EndPoint - mid_point).Length();
        float chord_length = (EndPoint - StartPoint).Length();

        if (depth >= 16u || polyline_length - chord_length <= tolerance)
        {
            return (polyline_length + chord_length) * 0.5f;
        }

        return ApproximateLengthSpan(
                   arc,
                   start_ratio,
                   mid_ratio,
                   StartPoint,
                   mid_point,
                   tolerance,
                   depth + 1u
               ) +
            ApproximateLengthSpan(
                   arc,
                   mid_ratio,
                   end_ratio,
                   mid_point,
                   EndPoint,
                   tolerance,
                   depth + 1u
            );
    }
    // Original line 1535: _contains_angle.
    internal static bool ContainsAngle(float angle,
    float StartAngle,
    float SweepAngle)
    {
        float full_turn = (MathF.PI * 2);
        if (SweepAngle == 0f)
        {
            return false;
        }
        if (VgScalar.Abs(SweepAngle) >= full_turn)
        {
            return true;
        }

        if (SweepAngle > 0f)
        {
            float delta = CppMath.Fmod(angle - StartAngle, full_turn);
            if (delta < 0f)
            {
                delta += full_turn;
            }
            return delta >= 0f && delta <= SweepAngle;
        }

        float reverse_delta = CppMath.Fmod(StartAngle - angle, full_turn);
        if (reverse_delta < 0f)
        {
            reverse_delta += full_turn;
        }
        return reverse_delta >= 0f && reverse_delta <= -SweepAngle;
    }
    public readonly bool Equals(SuperellipseArc other) => Center == other.Center && RadiusX == other.RadiusX && RadiusY == other.RadiusY && Rotation == other.Rotation && Exponent == other.Exponent && StartAngle == other.StartAngle && SweepAngle == other.SweepAngle;
    public override readonly bool Equals(object? obj) => obj is SuperellipseArc other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Center, RadiusX, RadiusY, Rotation, Exponent, StartAngle, SweepAngle);
    public static bool operator ==(SuperellipseArc lhs, SuperellipseArc rhs) => lhs.Equals(rhs);
    public static bool operator !=(SuperellipseArc lhs, SuperellipseArc rhs) => !lhs.Equals(rhs);
}
