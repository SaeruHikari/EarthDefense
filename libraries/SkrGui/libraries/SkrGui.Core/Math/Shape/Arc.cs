// Source: SkrGuiCore/math/shape/arc.hpp @ 611561f8.
// Full geometry, sampling, projection and cubic conversion bodies.
using System.Diagnostics;
namespace SkrGui;
internal static class ArcFactoryHelper
{
    public const float kEpsilon = 1.0e-6f;
    // Original line 165: signed_sweep.
    public static float SignedSweep(float StartAngle, float EndAngle, bool is_cw)
    {
            float sweep = EndAngle - StartAngle;
            if (is_cw)
            {
                if (sweep <= 0f)
                {
                    sweep += (MathF.PI * 2);
                }
            }
            else if (sweep >= 0f)
            {
                sweep -= (MathF.PI * 2);
            }
            return sweep;
        }
}
internal static class ArcCubicBezierHelper
{
    // Original line 185: count_90_degree.
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
    // Original line 201: count_tolerance.
    public static uint CountTolerance(float radius_x,
        float radius_y,
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

            float scaled_err = CppMath.Max(radius_x, radius_y) / tolerance;
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
    // Original line 238: make_segment.
    public static CubicBezier MakeSegment(Offsetf Center,
        float Radius,
        float StartAngle,
        float SweepAngle,
        Offsetf end)
    {
            float EndAngle = StartAngle + SweepAngle;
            float k = (4f / 3f) * VgScalar.Tan(SweepAngle * 0.25f);
            Offsetf start = Center + Offsetf.Radians(StartAngle, Radius);
            Offsetf start_tangent = new Offsetf(
                -VgScalar.Sin(StartAngle) * Radius,
                VgScalar.Cos(StartAngle) * Radius
            );
            Offsetf end_tangent = new Offsetf(
                -VgScalar.Sin(EndAngle) * Radius,
                VgScalar.Cos(EndAngle) * Radius
            );

            return CubicBezier.CubicTo(
                start,
                start + start_tangent * k,
                end - end_tangent * k,
                end
            );
        }
}
public struct Arc : IEquatable<Arc>
{
    public Offsetf Center;
    public float Radius;
    public float StartAngle;
    public float SweepAngle;
    public struct ProjectResult { public Offsetf Point; public float Angle, T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }
    public Arc(Offsetf center, float radius, float start_angle, float sweep_angle) { Center = center; Radius = radius; StartAngle = start_angle; SweepAngle = sweep_angle; }
    // Original line 301: Zero.
    public static Arc Zero()
    {
        return new Arc();
    }
    // Original line 305: Invalid.
    public static Arc Invalid()
    {
        float kNaN = float.NaN;
        return new Arc(new Offsetf(kNaN, kNaN), kNaN, kNaN, kNaN);
    }
    // Original line 310: ArcTo.
    public static Arc ArcTo(Offsetf from,
    Offsetf corner,
    Offsetf to,
    float Radius)
    {
        if (!from.IsFinite() || !corner.IsFinite() || !to.IsFinite() ||
            !VgScalar.IsFinite(Radius) || Radius <= 0f)
        {
            return Invalid();
        }

        Offsetf in_direction = (corner - from).Normalize();
        Offsetf out_direction = (to - corner).Normalize();
        float turn = in_direction.Cross(out_direction);
        if (in_direction.LengthSquared() <= 0f ||
            out_direction.LengthSquared() <= 0f ||
            VgScalar.Abs(turn) <= ArcFactoryHelper.kEpsilon)
        {
            return Invalid();
        }

        float cosine = CppMath.Clamp(
            in_direction.Dot(out_direction),
            -1f,
            1f
        );
        float half_angle_tangent = VgScalar.Tan(VgScalar.Acos(cosine) * 0.5f);
        if (!VgScalar.IsFinite(half_angle_tangent) ||
            VgScalar.Abs(half_angle_tangent) <= ArcFactoryHelper.kEpsilon)
        {
            return Invalid();
        }

        float tangent_distance = Radius / half_angle_tangent;
        if (!VgScalar.IsFinite(tangent_distance) || tangent_distance <= 0f)
        {
            return Invalid();
        }

        Offsetf start = corner - in_direction * tangent_distance;
        Offsetf end = corner + out_direction * tangent_distance;
        bool is_cw = turn > 0f;
        Offsetf center_from_start =
            start +
            (is_cw ?
                 in_direction.CwNormal() :
                 in_direction.CcwNormal()) *
                Radius;
        Offsetf center_from_end =
            end +
            (is_cw ?
                 out_direction.CwNormal() :
                 out_direction.CcwNormal()) *
                Radius;
        Offsetf Center = Offsetf.Lerp(center_from_start, center_from_end, 0.5f);
        if (!Center.IsFinite())
        {
            return Invalid();
        }

        float StartAngle = (start - Center).Radians();
        float EndAngle = (end - Center).Radians();
        float sweep =
            ArcFactoryHelper.SignedSweep(StartAngle, EndAngle, is_cw);
        if (!VgScalar.IsFinite(StartAngle) || !VgScalar.IsFinite(sweep) || sweep == 0f)
        {
            return Invalid();
        }

        return CenterRadius(Center, Radius, StartAngle, sweep).Normalized();
    }
    // Original line 383: CenterRadius.
    public static Arc CenterRadius(Offsetf Center,
    float Radius,
    float StartAngle,
    float SweepAngle)
    {
        return new Arc(Center, Radius, StartAngle, SweepAngle);
    }
    // Original line 394: is_empty.
    public bool IsEmpty()
    {
        return Radius <= 0f || SweepAngle == 0f;
    }
    // Original line 398: is_valid.
    public bool IsValid()
    {
        return Center.IsFinite() && VgScalar.IsFinite(Radius) &&
            VgScalar.IsFinite(StartAngle) && VgScalar.IsFinite(SweepAngle) &&
            Radius >= 0f;
    }
    // Original line 404: is_normalized.
    public bool IsNormalized()
    {
        return Center.IsFinite() && VgScalar.IsFinite(Radius) &&
            VgScalar.IsFinite(StartAngle) && VgScalar.IsFinite(SweepAngle) &&
            Radius >= 0f &&
            StartAngle == WrapAngle(StartAngle) &&
            VgScalar.Abs(SweepAngle) <= (MathF.PI * 2);
    }
    // Original line 412: is_finite.
    public bool IsFinite()
    {
        return Center.IsFinite() && VgScalar.IsFinite(Radius) &&
            VgScalar.IsFinite(StartAngle) && VgScalar.IsFinite(SweepAngle);
    }
    // Original line 417: has_nan.
    public bool HasNan()
    {
        return VgScalar.IsNaN(Center.X) || VgScalar.IsNaN(Center.Y) || VgScalar.IsNaN(Radius) ||
            VgScalar.IsNaN(StartAngle) || VgScalar.IsNaN(SweepAngle);
    }
    // Original line 422: is_point.
    public bool IsPoint()
    {
        return Radius >= 0f && (Radius == 0f || SweepAngle == 0f);
    }
    // Original line 428: end_angle.
    public float EndAngle()
    {
        return StartAngle + SweepAngle;
    }
    // Original line 432: length.
    public float Length()
    {
        return VgScalar.Abs(SweepAngle) * Radius;
    }
    // Original line 436: start_point.
    public Offsetf StartPoint()
    {
        return PointAtAngle(StartAngle);
    }
    // Original line 440: end_point.
    public Offsetf EndPoint()
    {
        return PointAtAngle(EndAngle());
    }
    // Original line 444: rect.
    public Rectf Rect()
    {
        Offsetf start = StartPoint();
        Offsetf end = EndPoint();
        Rectf Bounds = Rectf.Points(start, end);

        if (VgScalar.Abs(SweepAngle) >= (MathF.PI * 2))
        {
            return Rectf.Circle(Center, Radius);
        }

        float quarter_turn = (MathF.PI * 2) * 0.25f;
        float half_turn = quarter_turn * 2f;
        float[] candidates = [ 0f, quarter_turn, half_turn, quarter_turn * 3f ];
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
    // Original line 469: bounds.
    public Rectf Bounds()
    {
        return Rect();
    }
    // Original line 473: contains_angle.
    public bool ContainsAngle(float angle)
    {
        return ContainsAngle(angle, StartAngle, SweepAngle);
    }
    // Original line 477: project.
    public ProjectResult Project(Offsetf point)
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

        Offsetf direction = point - value.Center;
        if (direction.LengthSquared() > 0f)
        {
            float candidate_angle = WrapAngle(direction.Radians());
            if (value.ContainsAngle(candidate_angle))
            {
                result.Point = value.PointAtAngle(candidate_angle);
                result.Angle = candidate_angle;
                result.T = RatioFromAngle(
                    candidate_angle,
                    value.StartAngle,
                    value.SweepAngle
                );
                result.Distance = (point - result.Point).Length();
                result.Valid = true;
                return result;
            }
        }

        Offsetf start = value.StartPoint();
        Offsetf end = value.EndPoint();
        float start_distance_sq = (point - start).LengthSquared();
        float end_distance_sq = (point - end).LengthSquared();
        bool use_start = start_distance_sq <= end_distance_sq;

        result.Point = use_start ? start : end;
        result.Angle = use_start ? value.StartAngle : value.EndAngle();
        result.T = use_start ? 0f : 1f;
        result.Distance = VgScalar.Sqrt(use_start ? start_distance_sq : end_distance_sq);
        result.Valid = true;
        return result;
    }
    // Original line 528: distance.
    public float Distance(Offsetf point)
    {
        return Project(point).Distance;
    }
    // Original line 532: closest_point.
    public Offsetf ClosestPoint(Offsetf point)
    {
        return Project(point).Point;
    }
    // Original line 536: create_sampler.
    public CircleSampler CreateSampler(float angle = 0)
    {
        return new CircleSampler(angle);
    }
    // Original line 540: point_at_angle.
    public Offsetf PointAtAngle(float angle)
    {
        CircleSampler sampler = CreateSampler(angle);
        return sampler.SamplePoint(Center, Radius);
    }
    // Original line 545: point_at_ratio.
    public Offsetf PointAtRatio(float ratio)
    {
        return PointAtT(ratio);
    }
    // Original line 549: point_at_t.
    public Offsetf PointAtT(float t)
    {
        return PointAtAngle(StartAngle + SweepAngle * t);
    }
    // Original line 555: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return .6f *
            ShapeSampleHelper.CalcGeometricTolerance(
                   tessellation_factor,
                   pixel_ratio
            );
    }
    // Original line 566: estimate_segment_count.
    public uint EstimateSegmentCount(float tolerance)
    {
        Arc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return 0u;
        }

        return ShapeSampleHelper.CalcCircularSegmentCount(
            value.Radius,
            value.SweepAngle,
            tolerance,
            false
        );
    }
    // Original line 582: sample.
    public void Sample(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Arc value = Normalized();
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
    // Original line 601: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Arc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        value.Sample(
            new ShapeSegmentCountSampleDesc(value.EstimateSegmentCount(desc.Tolerance),
                desc.Direction),
            functor
        );
    }
    // Original line 621: sample.
    public void Sample(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Arc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        value.Sample(
            new ShapeSegmentCountSampleDesc(ShapeSampleHelper.CalcStepLengthSegmentCount(
                    value.Length(),
                    desc.StepLength,
                    false
                ),
                desc.Direction),
            functor
        );
    }
    // Original line 645: sample_with_sampler.
    public void SampleWithSampler(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float,CircleSampler> functor)
    {
        Arc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        CircleSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleOpenUniformWithSampler(
            desc.SegmentCount,
            desc.Direction,
            ref sampler,
            (ref CircleSampler sampler, float t) => {
                sampler.SetAngle(value.StartAngle + value.SweepAngle * t);
                return sampler.SamplePoint(value.Center, value.Radius);
            },
            functor
        );
    }
    // Original line 669: sample_with_sampler.
    public void SampleWithSampler(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float,CircleSampler> functor)
    {
        Arc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        value.SampleWithSampler(
            new ShapeSegmentCountSampleDesc(value.EstimateSegmentCount(desc.Tolerance),
                desc.Direction),
            functor
        );
    }
    // Original line 689: sample_with_sampler.
    public void SampleWithSampler(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float,CircleSampler> functor)
    {
        Arc value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        value.SampleWithSampler(
            new ShapeSegmentCountSampleDesc(ShapeSampleHelper.CalcStepLengthSegmentCount(
                    value.Length(),
                    desc.StepLength,
                    false
                ),
                desc.Direction),
            functor
        );
    }
    // Original line 714: cubic_bezier_count.
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

        Arc value = Normalized();
        return ArcCubicBezierHelper.CountTolerance(
            value.Radius,
            value.Radius,
            value.SweepAngle,
            tolerance
        );
    }
    // Original line 735: cubic_bezier_count_fast.
    public uint CubicBezierCountFast()
    {
        if (!IsValid() || IsEmpty())
        {
            return 0u;
        }

        Arc value = Normalized();
        return ArcCubicBezierHelper.Count90Degree(value.SweepAngle);
    }
    // Original line 748: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance, Action<CubicBezier> func)
    {
        uint count = CubicBezierCount(tolerance);
        if (count == 0u)
        {
            return;
        }

        Arc value = Normalized();
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
            CubicBezier cubic = ArcCubicBezierHelper.MakeSegment(
                value.Center,
                value.Radius,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 778: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(Action<CubicBezier> func)
    {
        if (!IsValid() || IsEmpty())
        {
            return;
        }

        Arc value = Normalized();
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
            CubicBezier cubic = ArcCubicBezierHelper.MakeSegment(
                value.Center,
                value.Radius,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 814: normalized.
    public Arc Normalized()
    {
        Arc result = this;
        if (VgScalar.IsFinite(result.Radius) && result.Radius < 0f)
        {
            result.Radius = -result.Radius;
            result.StartAngle += MathF.PI;
        }

        result.StartAngle = WrapAngle(result.StartAngle);
        result.SweepAngle = ClampSweep(result.SweepAngle);
        return result;
    }
    // Original line 827: shift.
    public Arc Shift(Offsetf offset)
    {
        return new Arc(Center + offset, Radius, StartAngle, SweepAngle);
    }
    // Original line 831: inflate.
    public Arc Inflate(float delta)
    {
        return new Arc(Center, CppMath.Max(0f, Radius + delta), StartAngle, SweepAngle);
    }
    // Original line 835: deflate.
    public Arc Deflate(float delta)
    {
        return Inflate(-delta);
    }
    // Original line 839: trim.
    public Arc Trim(float start_t, float end_t)
    {
        Arc value = Normalized();
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

        return new Arc(value.Center,
            value.Radius,
            value.StartAngle + value.SweepAngle * clamped_start,
            value.SweepAngle * (clamped_end - clamped_start));
    }
    // Original line 861: reversed.
    public Arc Reversed()
    {
        return new Arc(Center, Radius, EndAngle(), -SweepAngle);
    }
    // Original line 878: _wrap_angle.
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
    // Original line 896: _clamp_sweep.
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
    // Original line 913: _clamp_unit.
    private static float ClampUnit(float t)
    {
        if (!VgScalar.IsFinite(t))
        {
            return 0f;
        }
        return CppMath.Clamp(t, 0f, 1f);
    }
    // Original line 921: _ratio_from_angle.
    private static float RatioFromAngle(float angle,
    float StartAngle,
    float SweepAngle)
    {
        float full_turn = (MathF.PI * 2);
        if (SweepAngle == 0f)
        {
            return 0f;
        }

        if (VgScalar.Abs(SweepAngle) >= full_turn)
        {
            float delta = CppMath.Fmod(angle - StartAngle, full_turn);
            if (delta < 0f)
            {
                delta += full_turn;
            }
            return delta / full_turn;
        }

        if (SweepAngle > 0f)
        {
            float delta = CppMath.Fmod(angle - StartAngle, full_turn);
            if (delta < 0f)
            {
                delta += full_turn;
            }
            return CppMath.Clamp(delta / SweepAngle, 0f, 1f);
        }

        float reverse_delta = CppMath.Fmod(StartAngle - angle, full_turn);
        if (reverse_delta < 0f)
        {
            reverse_delta += full_turn;
        }
        return CppMath.Clamp(reverse_delta / -SweepAngle, 0f, 1f);
    }
    // Original line 960: _contains_angle.
    private static bool ContainsAngle(float angle, float StartAngle, float SweepAngle)
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
    public readonly bool Equals(Arc other) => Center == other.Center && Radius == other.Radius && StartAngle == other.StartAngle && SweepAngle == other.SweepAngle;
    public override readonly bool Equals(object? obj) => obj is Arc other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Center, Radius, StartAngle, SweepAngle);
    public static bool operator ==(Arc lhs, Arc rhs) => lhs.Equals(rhs);
    public static bool operator !=(Arc lhs, Arc rhs) => !lhs.Equals(rhs);
}
