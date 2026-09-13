// Source: SkrGuiCore/math/shape/elliptical_arc.hpp @ 611561f8.
// Full geometry, sampling, projection and cubic conversion bodies.
using System.Diagnostics;
namespace SkrGui;
public enum EEllipticalArcSize : byte { Small, Large }
public enum EEllipticalArcSweep : byte { CW, CCW }
internal static class EllipticalArcFactoryHelper
{
    public const float kEpsilon = 1.0e-6f;
    // Original line 201: vector_angle.
    public static float VectorAngle(float ux,
        float uy,
        float vx,
        float vy)
    {
            return VgScalar.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        }
}
internal static class EllipticalArcCubicBezierHelper
{
    // Original line 214: count_90_degree.
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
    // Original line 230: count_tolerance.
    public static uint CountTolerance(float RadiusX,
        float RadiusY,
        float SweepAngle,
        float tolerance)
    {
            if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
            {
                Debug.Assert(false , "cubic bezier tolerance must be positive and finite");
                return 0u;
            }

            if (SweepAngle == 0f)
            {
                return 0u;
            }

            float scaled_err = CppMath.Max(RadiusX, RadiusY) / tolerance;
            float n_err = CppMath.Max(
                VgScalar.Pow(1.1163f * scaled_err, 1.0f / 6.0f),
                3.999999f
            );
            float count = VgScalar.Ceiling(
                n_err * VgScalar.Abs(SweepAngle) / (MathF.PI * 2)
            );
            if (!VgScalar.IsFinite(count) ||
                count <= 0f ||
                count > (float)(uint.MaxValue))
            {
                Debug.Assert(false , "cubic bezier segment count must be finite");
                return 0u;
            }

            return (uint)(count);
        }
    // Original line 267: transform_point.
    public static Offsetf TransformPoint(Offsetf Center,
        float RadiusX,
        float RadiusY,
        float Rotation,
        float angle)
    {
            float cos_angle = VgScalar.Cos(angle);
            float sin_angle = VgScalar.Sin(angle);
            float cos_rotation = VgScalar.Cos(Rotation);
            float sin_rotation = VgScalar.Sin(Rotation);
            float local_x = RadiusX * cos_angle;
            float local_y = RadiusY * sin_angle;
            return Center +
                new Offsetf(
                       local_x * cos_rotation - local_y * sin_rotation,
                       local_x * sin_rotation + local_y * cos_rotation
                );
        }
    // Original line 288: transform_tangent.
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
    // Original line 307: make_segment.
    public static CubicBezier MakeSegment(EllipticalArc arc,
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
}
public struct EllipticalArc : IEquatable<EllipticalArc>
{
    public Offsetf Center;
    public float RadiusX;
    public float RadiusY;
    public float Rotation;
    public float StartAngle;
    public float SweepAngle;
    public struct ProjectResult { public Offsetf Point; public float Angle, T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }
    public EllipticalArc(Offsetf center, float radius_x, float radius_y, float rotation, float start_angle, float sweep_angle) { Center = center; RadiusX = radius_x; RadiusY = radius_y; Rotation = rotation; StartAngle = start_angle; SweepAngle = sweep_angle; }
    // Original line 390: Zero.
    public static EllipticalArc Zero()
    {
        return new EllipticalArc();
    }
    // Original line 394: Invalid.
    public static EllipticalArc Invalid()
    {
        float kNaN = float.NaN;
        return new EllipticalArc(new Offsetf(kNaN, kNaN), kNaN, kNaN, kNaN, kNaN, kNaN);
    }
    // Original line 399: ArcTo.
    public static EllipticalArc ArcTo(Offsetf from,
    Offsetf to,
    Offsetf radii,
    float Rotation = 0,
    EEllipticalArcSize arc_size = EEllipticalArcSize.Small,
    EEllipticalArcSweep sweep = EEllipticalArcSweep.CW)
    {
        if (!from.IsFinite() || !to.IsFinite() || !radii.IsFinite() ||
            !VgScalar.IsFinite(Rotation))
        {
            return Invalid();
        }
        if ((from - to).LengthSquared() <= EllipticalArcFactoryHelper.kEpsilon)
        {
            return Invalid();
        }

        float RadiusX = VgScalar.Abs(radii.X);
        float RadiusY = VgScalar.Abs(radii.Y);
        if (RadiusX <= 0f || RadiusY <= 0f)
        {
            return Invalid();
        }

        float phi = WrapAngle(Rotation);
        float cos_phi = VgScalar.Cos(phi);
        float sin_phi = VgScalar.Sin(phi);
        float dx_half = (from.X - to.X) * 0.5f;
        float dy_half = (from.Y - to.Y) * 0.5f;
        float x1_prime = cos_phi * dx_half + sin_phi * dy_half;
        float y1_prime = -sin_phi * dx_half + cos_phi * dy_half;
        if (!VgScalar.IsFinite(x1_prime) || !VgScalar.IsFinite(y1_prime))
        {
            return Invalid();
        }

        float radius_x_sq = RadiusX * RadiusX;
        float radius_y_sq = RadiusY * RadiusY;
        float x1_prime_sq = x1_prime * x1_prime;
        float y1_prime_sq = y1_prime * y1_prime;
        float radii_scale =
            x1_prime_sq / radius_x_sq + y1_prime_sq / radius_y_sq;
        if (radii_scale > 1f)
        {
            float scale = VgScalar.Sqrt(radii_scale);
            RadiusX *= scale;
            RadiusY *= scale;
        }

        float scaled_radius_x_sq = RadiusX * RadiusX;
        float scaled_radius_y_sq = RadiusY * RadiusY;
        float denominator =
            scaled_radius_x_sq * y1_prime_sq +
            scaled_radius_y_sq * x1_prime_sq;
        if (!VgScalar.IsFinite(denominator) || denominator <= 0f)
        {
            return Invalid();
        }

        float numerator =
            scaled_radius_x_sq * scaled_radius_y_sq -
            scaled_radius_x_sq * y1_prime_sq -
            scaled_radius_y_sq * x1_prime_sq;
        float sign =
            (arc_size != EEllipticalArcSize.Large) !=
                (sweep != EEllipticalArcSweep.CW) ?
            1f :
            -1f;
        float factor = sign * VgScalar.Sqrt(CppMath.Max(0f, numerator / denominator));
        float center_x_prime =
            factor * (RadiusX * y1_prime / RadiusY);
        float center_y_prime =
            factor * (-RadiusY * x1_prime / RadiusX);
        Offsetf Center = new Offsetf(
            cos_phi * center_x_prime - sin_phi * center_y_prime +
                (from.X + to.X) * 0.5f,
            sin_phi * center_x_prime + cos_phi * center_y_prime +
                (from.Y + to.Y) * 0.5f
        );
        if (!Center.IsFinite())
        {
            return Invalid();
        }

        float ux = (x1_prime - center_x_prime) / RadiusX;
        float uy = (y1_prime - center_y_prime) / RadiusY;
        float vx = (-x1_prime - center_x_prime) / RadiusX;
        float vy = (-y1_prime - center_y_prime) / RadiusY;
        float StartAngle = VgScalar.Atan2(uy, ux);
        float SweepAngle =
            EllipticalArcFactoryHelper.VectorAngle(ux, uy, vx, vy);
        if (sweep == EEllipticalArcSweep.CW)
        {
            if (SweepAngle < 0f)
            {
                SweepAngle += (MathF.PI * 2);
            }
        }
        else if (SweepAngle > 0f)
        {
            SweepAngle -= (MathF.PI * 2);
        }

        if (!VgScalar.IsFinite(StartAngle) ||
            !VgScalar.IsFinite(SweepAngle) ||
            SweepAngle == 0f)
        {
            return Invalid();
        }

        return CenterRadius(
                   Center,
                   RadiusX,
                   RadiusY,
                   phi,
                   StartAngle,
                   SweepAngle
        )
            .Normalized();
    }
    // Original line 521: CenterRadius.
    public static EllipticalArc CenterRadius(Offsetf Center,
    float RadiusX,
    float RadiusY,
    float Rotation,
    float StartAngle,
    float SweepAngle)
    {
        return new EllipticalArc(Center, RadiusX, RadiusY, Rotation, StartAngle, SweepAngle);
    }
    // Original line 534: is_empty.
    public bool IsEmpty()
    {
        return RadiusX <= 0f || RadiusY <= 0f || SweepAngle == 0f;
    }
    // Original line 538: is_valid.
    public bool IsValid()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(StartAngle) && VgScalar.IsFinite(SweepAngle) &&
            RadiusX >= 0f && RadiusY >= 0f;
    }
    // Original line 545: is_normalized.
    public bool IsNormalized()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(StartAngle) && VgScalar.IsFinite(SweepAngle) &&
            RadiusX >= 0f && RadiusY >= 0f &&
            Rotation == WrapAngle(Rotation) &&
            StartAngle == WrapAngle(StartAngle) &&
            VgScalar.Abs(SweepAngle) <= (MathF.PI * 2);
    }
    // Original line 555: is_finite.
    public bool IsFinite()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(StartAngle) && VgScalar.IsFinite(SweepAngle);
    }
    // Original line 561: has_nan.
    public bool HasNan()
    {
        return VgScalar.IsNaN(Center.X) || VgScalar.IsNaN(Center.Y) ||
            VgScalar.IsNaN(RadiusX) || VgScalar.IsNaN(RadiusY) || VgScalar.IsNaN(Rotation) ||
            VgScalar.IsNaN(StartAngle) || VgScalar.IsNaN(SweepAngle);
    }
    // Original line 567: is_point.
    public bool IsPoint()
    {
        return RadiusX >= 0f && RadiusY >= 0f &&
            ((RadiusX == 0f && RadiusY == 0f) || SweepAngle == 0f);
    }
    // Original line 574: end_angle.
    public float EndAngle()
    {
        return StartAngle + SweepAngle;
    }
    // Original line 578: length.
    public float Length(float tolerance = .01f)
    {
        var value = Normalized();
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
    // Original line 596: start_point.
    public Offsetf StartPoint()
    {
        return PointAtAngle(StartAngle);
    }
    // Original line 600: end_point.
    public Offsetf EndPoint()
    {
        return PointAtAngle(EndAngle());
    }
    // Original line 604: rect.
    public Rectf Rect()
    {
        Offsetf start = StartPoint();
        Offsetf end = EndPoint();
        Rectf Bounds = Rectf.Points(start, end);

        if (VgScalar.Abs(SweepAngle) >= (MathF.PI * 2))
        {
            float full_cos_rotation = VgScalar.Cos(Rotation);
            float full_sin_rotation = VgScalar.Sin(Rotation);
            float extent_x = VgScalar.Sqrt(
                RadiusX * RadiusX * full_cos_rotation * full_cos_rotation +
                RadiusY * RadiusY * full_sin_rotation * full_sin_rotation
            );
            float extent_y = VgScalar.Sqrt(
                RadiusX * RadiusX * full_sin_rotation * full_sin_rotation +
                RadiusY * RadiusY * full_cos_rotation * full_cos_rotation
            );
            return Rectf.Center(Center, new Sizef(extent_x * 2f, extent_y * 2f));
        }

        float cos_rotation = VgScalar.Cos(Rotation);
        float sin_rotation = VgScalar.Sin(Rotation);
        float half_turn = (MathF.PI * 2) * 0.5f;
        float x_extremum = VgScalar.Atan2(-RadiusY * sin_rotation, RadiusX * cos_rotation);
        float y_extremum = VgScalar.Atan2(RadiusY * cos_rotation, RadiusX * sin_rotation);
        float[] candidates = [
            x_extremum,
            x_extremum + half_turn,
            y_extremum,
            y_extremum + half_turn,
        ];

        foreach (float angle in candidates)
        {
            if (ContainsAngle(angle, StartAngle, SweepAngle))
            {
                Offsetf point = PointAtAngle(angle);
                Bounds = Bounds.Unite(Rectf.Points(point, point));
            }
        }

        return Bounds;
    }
    // Original line 648: bounds.
    public Rectf Bounds()
    {
        return Rect();
    }
    // Original line 652: contains_angle.
    public bool ContainsAngle(float angle)
    {
        return ContainsAngle(angle, StartAngle, SweepAngle);
    }
    // Original line 656: project.
    public ProjectResult Project(Offsetf point,
    float tolerance = .01f)
    {
        ProjectResult result = new();
        var value = Normalized();
        if (!value.IsFinite())
        {
            return result;
        }

        if (value.IsPoint() || value.IsEmpty())
        {
            result.Point = value.StartPoint();
            result.Angle = value.StartAngle;
            result.T = 0f;
            result.Distance = (point - result.Point).Length();
            result.Valid = true;
            return result;
        }

        float ratio = ClosestRatio(value, point, tolerance);
        result.Point = value.PointAtT(ratio);
        result.Angle = WrapAngle(value.StartAngle + value.SweepAngle * ratio);
        result.T = ratio;
        result.Distance = (point - result.Point).Length();
        result.Valid = true;
        return result;
    }
    // Original line 686: distance.
    public float Distance(Offsetf point, float tolerance = .01f)
    {
        return Project(point, tolerance).Distance;
    }
    // Original line 690: closest_point.
    public Offsetf ClosestPoint(Offsetf point,
    float tolerance = .01f)
    {
        return Project(point, tolerance).Point;
    }
    // Original line 697: create_sampler.
    public EllipseSampler CreateSampler(float angle = 0)
    {
        return new EllipseSampler(angle, Rotation);
    }
    // Original line 701: point_at_angle.
    public Offsetf PointAtAngle(float angle)
    {
        EllipseSampler sampler = CreateSampler(angle);
        return sampler.SamplePoint(Center, RadiusX, RadiusY);
    }
    // Original line 706: point_at_ratio.
    public Offsetf PointAtRatio(float ratio)
    {
        return PointAtT(ratio);
    }
    // Original line 710: point_at_t.
    public Offsetf PointAtT(float t)
    {
        return PointAtAngle(StartAngle + SweepAngle * t);
    }
    // Original line 716: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return ShapeSampleHelper.CalcGeometricTolerance(
            tessellation_factor,
            pixel_ratio
        );
    }
    // Original line 726: estimate_segment_count.
    public uint EstimateSegmentCount(float tolerance)
    {

        EllipticalArc value = Normalized();
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f || !value.IsValid() || value.IsEmpty())
        {
            return 0u;
        }

        return EllipseEstimateHelper.EstimateArc(
            value.RadiusX,
            value.RadiusY,
            value.StartAngle,
            value.SweepAngle,
            tolerance
        );
    }
    // Original line 744: sample.
    public void Sample(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        EllipticalArc value = Normalized();
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
    // Original line 763: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        EllipticalArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        ShapeSampleHelper.SampleOpenAdaptive(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            (float t) => { return value.PointAtT(t); },
            functor
        );
    }
    // Original line 783: sample.
    public void Sample(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        EllipticalArc value = Normalized();
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
    // Original line 804: sample_with_sampler.
    public void SampleWithSampler(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float,EllipseSampler> functor)
    {
        EllipticalArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        EllipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleOpenUniformWithSampler(
            desc.SegmentCount,
            desc.Direction,
            ref sampler,
            (ref EllipseSampler sampler, float t) => {
                sampler.SetAngle(value.StartAngle + value.SweepAngle * t);
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 828: sample_with_sampler.
    public void SampleWithSampler(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float,EllipseSampler> functor)
    {
        EllipticalArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        EllipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleOpenAdaptiveWithSampler(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            ref sampler,
            (ref EllipseSampler sampler, float t) => {
                sampler.SetAngle(value.StartAngle + value.SweepAngle * t);
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 853: sample_with_sampler.
    public void SampleWithSampler(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float,EllipseSampler> functor)
    {
        EllipticalArc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        EllipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleOpenByStepLengthWithSampler(
            desc.StepLength,
            ShapeSampleHelper.EstimateStepLengthTolerance(desc.StepLength),
            desc.MaxDepth,
            desc.Direction,
            ref sampler,
            (ref EllipseSampler sampler, float t) => {
                sampler.SetAngle(value.StartAngle + value.SweepAngle * t);
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 880: cubic_bezier_count.
    public uint CubicBezierCount(float tolerance)
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

        EllipticalArc value = Normalized();
        return EllipticalArcCubicBezierHelper.CountTolerance(
            value.RadiusX,
            value.RadiusY,
            value.SweepAngle,
            tolerance
        );
    }
    // Original line 901: cubic_bezier_count_fast.
    public uint CubicBezierCountFast()
    {
        if (!IsValid() || IsEmpty())
        {
            return 0u;
        }

        EllipticalArc value = Normalized();
        return EllipticalArcCubicBezierHelper.Count90Degree(value.SweepAngle);
    }
    // Original line 914: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance, Action<CubicBezier> func)
    {
        uint count = CubicBezierCount(tolerance);
        if (count == 0u)
        {
            return;
        }

        EllipticalArc value = Normalized();
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
            CubicBezier cubic = EllipticalArcCubicBezierHelper.MakeSegment(
                value,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 943: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(Action<CubicBezier> func)
    {
        if (!IsValid() || IsEmpty())
        {
            return;
        }

        EllipticalArc value = Normalized();
        uint count = value.CubicBezierCountFast();
        if (count == 0u)
        {
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
            CubicBezier cubic = EllipticalArcCubicBezierHelper.MakeSegment(
                value,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 978: normalized.
    public EllipticalArc Normalized()
    {
        EllipticalArc result = this;

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
    // Original line 1007: shift.
    public EllipticalArc Shift(Offsetf offset)
    {
        return new EllipticalArc(Center + offset, RadiusX, RadiusY, Rotation, StartAngle, SweepAngle);
    }
    // Original line 1011: inflate.
    public EllipticalArc Inflate(float delta)
    {
        return new EllipticalArc(Center,
            CppMath.Max(0f, RadiusX + delta),
            CppMath.Max(0f, RadiusY + delta),
            Rotation,
            StartAngle,
            SweepAngle);
    }
    // Original line 1022: deflate.
    public EllipticalArc Deflate(float delta)
    {
        return Inflate(-delta);
    }
    // Original line 1026: trim.
    public EllipticalArc Trim(float start_t, float end_t)
    {
        EllipticalArc value = Normalized();
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

        return new EllipticalArc(value.Center,
            value.RadiusX,
            value.RadiusY,
            value.Rotation,
            value.StartAngle + value.SweepAngle * clamped_start,
            value.SweepAngle * (clamped_end - clamped_start));
    }
    // Original line 1050: reversed.
    public EllipticalArc Reversed()
    {
        return new EllipticalArc(Center, RadiusX, RadiusY, Rotation, EndAngle(), -SweepAngle);
    }
    // Original line 1068: _wrap_angle.
    private static float WrapAngle(float angle)
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
    // Original line 1086: _clamp_sweep.
    private static float ClampSweep(float SweepAngle)
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
    // Original line 1103: _clamp_unit.
    private static float ClampUnit(float t)
    {
        if (!VgScalar.IsFinite(t))
        {
            return 0f;
        }
        return CppMath.Clamp(t, 0f, 1f);
    }
    // Original line 1111: _positive_tolerance.
    private static float PositiveTolerance(float tolerance)
    {
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
        {
            return 0.01f;
        }
        return tolerance;
    }
    // Original line 1119: _approximate_length_span.
    private static float ApproximateLengthSpan(EllipticalArc arc,
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
    // Original line 1159: _closest_ratio.
    private static float ClosestRatio(EllipticalArc arc,
    Offsetf point,
    float tolerance)
    {
        float tol = PositiveTolerance(tolerance);
        uint sample_count = CppMath.Clamp(
            (uint)(VgScalar.Ceiling(
                VgScalar.Abs(arc.SweepAngle) * CppMath.Max(arc.RadiusX, arc.RadiusY) / tol
            )),
            32u,
            2048u
        );
        float step = 1f / (float)(sample_count);

        float best_ratio = 0f;
        float best_distance_sq = float.PositiveInfinity;
        for (uint i = 0; i <= sample_count; ++i)
        {
            float ratio = step * (float)(i);
            float clamped_ratio = CppMath.Min(1f, ratio);
            float distance_sq =
                (arc.PointAtT(clamped_ratio) - point).LengthSquared();
            if (distance_sq < best_distance_sq)
            {
                best_distance_sq = distance_sq;
                best_ratio = clamped_ratio;
            }
        }

        float left = CppMath.Max(0f, best_ratio - step);
        float right = CppMath.Min(1f, best_ratio + step);
        for (uint i = 0; i < 24u; ++i)
        {
            float l = left + (right - left) / 3f;
            float r = right - (right - left) / 3f;
            float left_distance_sq =
                (arc.PointAtT(l) - point).LengthSquared();
            float right_distance_sq =
                (arc.PointAtT(r) - point).LengthSquared();
            if (left_distance_sq <= right_distance_sq)
            {
                right = r;
            }
            else
            {
                left = l;
            }
        }

        return (left + right) * 0.5f;
    }
    // Original line 1212: _contains_angle.
    private static bool ContainsAngle(float angle,
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
    public readonly bool Equals(EllipticalArc other) => Center == other.Center && RadiusX == other.RadiusX && RadiusY == other.RadiusY && Rotation == other.Rotation && StartAngle == other.StartAngle && SweepAngle == other.SweepAngle;
    public override readonly bool Equals(object? obj) => obj is EllipticalArc other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Center, RadiusX, RadiusY, Rotation, StartAngle, SweepAngle);
    public static bool operator ==(EllipticalArc lhs, EllipticalArc rhs) => lhs.Equals(rhs);
    public static bool operator !=(EllipticalArc lhs, EllipticalArc rhs) => !lhs.Equals(rhs);
}
