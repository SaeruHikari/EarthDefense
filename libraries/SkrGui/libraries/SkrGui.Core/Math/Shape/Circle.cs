// Source: SkrGuiCore/math/shape/circle.hpp @ 611561f8.
// Full geometry, sampling, projection and cubic conversion bodies.
using System.Diagnostics;
namespace SkrGui;
internal static class CircleCubicBezierHelper
{
    // Original line 141: count_90_degree.
    public static uint Count90Degree(float sweep_angle)
    {
            if (sweep_angle == 0f)
            {
                return 0u;
            }

            float segment_count =
                VgScalar.Abs(sweep_angle) / (MathF.PI * .5f);
            return CppMath.Clamp(
                (uint)(VgScalar.Ceiling(CppMath.Max(0f, segment_count - 1.0e-6f))),
                1u,
                4u
            );
        }
    // Original line 157: count_tolerance.
    public static uint CountTolerance(float radius_x,
        float radius_y,
        float sweep_angle,
        float tolerance)
    {
            if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
            {
                Debug.Assert(false , "cubic bezier tolerance must be positive and finite");
                return 0u;
            }

            if (sweep_angle == 0f)
            {
                return 0u;
            }

            float scaled_err = CppMath.Max(radius_x, radius_y) / tolerance;
            float n_err = CppMath.Max(
                VgScalar.Pow(1.1163f * scaled_err, 1.0f / 6.0f),
                3.999999f
            );
            float count = VgScalar.Ceiling(
                n_err * VgScalar.Abs(sweep_angle) / (MathF.PI * 2)
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
    // Original line 194: make_segment.
    public static CubicBezier MakeSegment(Offsetf Center,
        float Radius,
        float start_angle,
        float sweep_angle,
        Offsetf end)
    {
            float end_angle = start_angle + sweep_angle;
            float k = (4f / 3f) * VgScalar.Tan(sweep_angle * 0.25f);
            Offsetf start = Center + Offsetf.Radians(start_angle, Radius);
            Offsetf start_tangent = new Offsetf(
                -VgScalar.Sin(start_angle) * Radius,
                VgScalar.Cos(start_angle) * Radius
            );
            Offsetf end_tangent = new Offsetf(
                -VgScalar.Sin(end_angle) * Radius,
                VgScalar.Cos(end_angle) * Radius
            );

            return CubicBezier.CubicTo(
                start,
                start + start_tangent * k,
                end - end_tangent * k,
                end
            );
        }
}
public struct Circle : IEquatable<Circle>
{
    public Offsetf Center;
    public float Radius;
    public struct ProjectResult { public Offsetf Point; public float Angle, T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }
    public Circle(Offsetf center, float radius) { Center = center; Radius = radius; }
    // Original line 253: Zero.
    public static Circle Zero()
    {
        return new Circle();
    }
    // Original line 257: Invalid.
    public static Circle Invalid()
    {
        float kNaN = float.NaN;
        return new Circle(new Offsetf(kNaN, kNaN), kNaN);
    }
    // Original line 262: CenterRadius.
    public static Circle CenterRadius(Offsetf Center, float Radius)
    {
        return new Circle(Center, Radius);
    }
    // Original line 268: is_empty.
    public bool IsEmpty()
    {
        return Radius <= 0f;
    }
    // Original line 272: is_valid.
    public bool IsValid()
    {
        return Center.IsFinite() && VgScalar.IsFinite(Radius) && Radius >= 0f;
    }
    // Original line 276: is_normalized.
    public bool IsNormalized()
    {
        return IsValid();
    }
    // Original line 280: is_finite.
    public bool IsFinite()
    {
        return Center.IsFinite() && VgScalar.IsFinite(Radius);
    }
    // Original line 284: has_nan.
    public bool HasNan()
    {
        return VgScalar.IsNaN(Center.X) || VgScalar.IsNaN(Center.Y) || VgScalar.IsNaN(Radius);
    }
    // Original line 288: is_point.
    public bool IsPoint()
    {
        return Radius == 0f;
    }
    // Original line 294: diameter.
    public float Diameter()
    {
        return Radius * 2f;
    }
    // Original line 298: length.
    public float Length()
    {
        return (MathF.PI * 2) * Radius;
    }
    // Original line 302: area.
    public float Area()
    {
        return MathF.PI * Radius * Radius;
    }
    // Original line 306: rect.
    public Rectf Rect()
    {
        return Rectf.Circle(Center, Radius);
    }
    // Original line 310: bounds.
    public Rectf Bounds()
    {
        return Rect();
    }
    // Original line 314: contains.
    public bool Contains(Offsetf point)
    {
        if (!IsValid())
        {
            return false;
        }

        return (point - Center).LengthSquared() <= Radius * Radius;
    }
    // Original line 323: project.
    public ProjectResult Project(Offsetf point)
    {
        ProjectResult result = new();
        if (!IsFinite())
        {
            return result;
        }

        Circle value = Normalized();
        Offsetf direction = point - value.Center;
        float angle = direction.Radians();
        if (direction.LengthSquared() <= 0f || !VgScalar.IsFinite(angle))
        {
            angle = 0f;
        }
        float ratio = CppMath.Fmod(angle, (MathF.PI * 2));
        if (ratio < 0f)
        {
            ratio += (MathF.PI * 2);
        }

        result.Point = value.PointAtAngle(angle);
        result.Angle = angle;
        result.T = ratio / (MathF.PI * 2);
        result.Distance = (point - result.Point).Length();
        result.Valid = true;
        return result;
    }
    // Original line 351: signed_distance.
    public float SignedDistance(Offsetf point)
    {
        if (!IsFinite())
        {
            return 0f;
        }

        return (point - Center).Length() - CppMath.Max(Radius, 0f);
    }
    // Original line 360: distance.
    public float Distance(Offsetf point)
    {
        return CppMath.Max(0f, SignedDistance(point));
    }
    // Original line 364: closest_point.
    public Offsetf ClosestPoint(Offsetf point)
    {
        return Project(point).Point;
    }
    // Original line 368: create_sampler.
    public CircleSampler CreateSampler(float angle = 0)
    {
        return new CircleSampler(angle);
    }
    // Original line 372: point_at_angle.
    public Offsetf PointAtAngle(float angle)
    {
        CircleSampler sampler = CreateSampler(angle);
        return sampler.SamplePoint(Center, Radius);
    }
    // Original line 377: point_at_ratio.
    public Offsetf PointAtRatio(float ratio)
    {
        return PointAtAngle(ratio * (MathF.PI * 2));
    }
    // Original line 381: point_at_t.
    public Offsetf PointAtT(float t)
    {
        return PointAtRatio(t);
    }
    // Original line 387: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return .6f *
            ShapeSampleHelper.CalcGeometricTolerance(
                   tessellation_factor,
                   pixel_ratio
            );
    }
    // Original line 398: estimate_segment_count.
    public uint EstimateSegmentCount(float tolerance)
    {
        Circle value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return 0u;
        }

        return ShapeSampleHelper.CalcCircularSegmentCount(
            value.Radius,
            (MathF.PI * 2),
            tolerance,
            true
        );
    }
    // Original line 414: sample.
    public void Sample(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Circle value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        ShapeSampleHelper.SampleClosedUniform(
            desc.SegmentCount,
            desc.Direction,
            (float t) => { return value.PointAtT(t); },
            functor
        );
    }
    // Original line 433: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Circle value = Normalized();
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
    // Original line 453: sample.
    public void Sample(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Circle value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        value.Sample(
            new ShapeSegmentCountSampleDesc(ShapeSampleHelper.CalcStepLengthSegmentCount(
                    value.Length(),
                    desc.StepLength,
                    true
                ),
                desc.Direction),
            functor
        );
    }
    // Original line 477: sample_with_sampler.
    public void SampleWithSampler(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float,CircleSampler> functor)
    {
        Circle value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        CircleSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleClosedUniformWithSampler(
            desc.SegmentCount,
            desc.Direction,
            ref sampler,
            (ref CircleSampler sampler, float t) => {
                sampler.SetAngle(t * (MathF.PI * 2));
                return sampler.SamplePoint(value.Center, value.Radius);
            },
            functor
        );
    }
    // Original line 501: sample_with_sampler.
    public void SampleWithSampler(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float,CircleSampler> functor)
    {
        Circle value = Normalized();
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
    // Original line 521: sample_with_sampler.
    public void SampleWithSampler(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float,CircleSampler> functor)
    {
        Circle value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        value.SampleWithSampler(
            new ShapeSegmentCountSampleDesc(ShapeSampleHelper.CalcStepLengthSegmentCount(
                    value.Length(),
                    desc.StepLength,
                    true
                ),
                desc.Direction),
            functor
        );
    }
    // Original line 546: cubic_bezier_count.
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

        return CircleCubicBezierHelper.CountTolerance(
            Radius,
            Radius,
            (MathF.PI * 2),
            tolerance
        );
    }
    // Original line 566: cubic_bezier_count_fast.
    public uint CubicBezierCountFast()
    {
        if (!IsValid() || IsEmpty())
        {
            return 0u;
        }

        return CircleCubicBezierHelper.Count90Degree((MathF.PI * 2));
    }
    // Original line 578: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance, Action<CubicBezier> func)
    {
        SplitToCubicBeziers(
            tolerance,
            EShapeSampleDirection.Forward,
            func
        );
    }
    // Original line 587: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance,
    EShapeSampleDirection direction,
    Action<CubicBezier> func)
    {
        uint count = CubicBezierCount(tolerance);
        if (count == 0u)
        {
            return;
        }

        float segment_sweep =
            (direction == EShapeSampleDirection.Reverse ? -(MathF.PI * 2) : (MathF.PI * 2)) /
            (float)(count);
        Offsetf precise_end = PointAtAngle(0f);
        for (uint i = 0u; i < count; ++i)
        {
            float segment_start = segment_sweep * (float)(i);
            float segment_end = segment_sweep * (float)(i + 1u);
            Offsetf end =
                (i + 1u == count) ? precise_end : PointAtAngle(segment_end);
            CubicBezier cubic = CircleCubicBezierHelper.MakeSegment(
                Center,
                Radius,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 620: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(Action<CubicBezier> func)
    {
        SplitToCubicBeziersFast(
            EShapeSampleDirection.Forward,
            func
        );
    }
    // Original line 628: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(EShapeSampleDirection direction, Action<CubicBezier> func)
    {
        if (!IsValid() || IsEmpty())
        {
            return;
        }

        uint count = CubicBezierCountFast();
        float segment_sweep =
            (direction == EShapeSampleDirection.Reverse ? -(MathF.PI * 2) : (MathF.PI * 2)) /
            (float)(count);
        Offsetf precise_end = PointAtAngle(0f);
        for (uint i = 0u; i < count; ++i)
        {
            float segment_start = segment_sweep * (float)(i);
            float segment_end = segment_sweep * (float)(i + 1u);
            Offsetf end =
                (i + 1u == count) ? precise_end : PointAtAngle(segment_end);
            CubicBezier cubic = CircleCubicBezierHelper.MakeSegment(
                Center,
                Radius,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 658: normalized.
    public Circle Normalized()
    {
        return new Circle(Center, VgScalar.Abs(Radius));
    }
    // Original line 662: shift.
    public Circle Shift(Offsetf offset)
    {
        return new Circle(Center + offset, Radius);
    }
    // Original line 666: inflate.
    public Circle Inflate(float delta)
    {
        return new Circle(Center, CppMath.Max(0f, Radius + delta));
    }
    // Original line 670: deflate.
    public Circle Deflate(float delta)
    {
        return Inflate(-delta);
    }
    public readonly bool Equals(Circle other) => Center == other.Center && Radius == other.Radius;
    public override readonly bool Equals(object? obj) => obj is Circle other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Center, Radius);
    public static bool operator ==(Circle lhs, Circle rhs) => lhs.Equals(rhs);
    public static bool operator !=(Circle lhs, Circle rhs) => !lhs.Equals(rhs);
}
