// Source: SkrGuiCore/math/shape/ellipse.hpp @ 611561f8.
// Full geometry, sampling, projection and cubic conversion bodies.
using System.Diagnostics;
namespace SkrGui;
internal static class ShapeEstimateArcHelper
{
    public struct CanonicalArc { public double StartAngle,SweepAngle,Aspect=1; public CanonicalArc() {} }
    public const double kMinValue = 1.0e-9;
    // Original line 163: count_from_quarter.
    public static uint CountFromQuarter(double quarter)
    {
            double estimate = VgScalar.Ceiling(CppMath.Max(1.0, quarter) * 4.0);
            if (estimate >= (double)(uint.MaxValue))
            {
                return uint.MaxValue;
            }
            return (uint)(estimate);
        }
    // Original line 173: count_from_arc_accumulated.
    public static uint CountFromArcAccumulated(double accumulated,
        double safety,
        double bias)
    {
            double estimate = VgScalar.Ceiling(CppMath.Max(2.0, accumulated * safety - bias));
            if (!VgScalar.IsFinite(estimate) ||
                estimate >= (double)(uint.MaxValue))
            {
                return uint.MaxValue;
            }
            return (uint)(estimate);
        }
    // Original line 188: canonical_aspect.
    public static double CanonicalAspect(double RadiusX, double RadiusY)
    {
            double max_radius = CppMath.Max(RadiusX, RadiusY);
            double min_radius = CppMath.Max(kMinValue, CppMath.Min(RadiusX, RadiusY));
            return CppMath.Max(1.0, max_radius / min_radius);
        }
    // Original line 195: canonical_arc.
    public static CanonicalArc MakeCanonicalArc(double RadiusX,
        double RadiusY,
        double start_angle,
        double sweep_angle)
    {
            CanonicalArc arc = new();
            arc.StartAngle = start_angle;
            arc.SweepAngle = CppMath.Clamp(
                sweep_angle,
                -(double)((MathF.PI * 2)),
                (double)((MathF.PI * 2))
            );
            arc.Aspect = CanonicalAspect(RadiusX, RadiusY);

            if (RadiusX < RadiusY)
            {
                arc.StartAngle = (double)((MathF.PI * .5f)) - arc.StartAngle;
                arc.SweepAngle = -arc.SweepAngle;
            }
            return arc;
        }
    // Original line 219: is_full_sweep.
    public static bool IsFullSweep(double sweep_angle)
    {
            return VgScalar.Abs(sweep_angle) >= (double)((MathF.PI * 2)) - 1.0e-6;
        }
    // Original line 224: map_to_first_quadrant.
    public static double MapToFirstQuadrant(double angle)
    {
            double wrapped = CppMath.Fmod(angle, (double)((MathF.PI * 2)));
            if (wrapped < 0.0)
            {
                wrapped += (double)((MathF.PI * 2));
            }

            double quadrant = VgScalar.Floor(wrapped / (double)((MathF.PI * .5f)));
            double local_angle =
                wrapped - quadrant * (double)((MathF.PI * .5f));
            int quadrant_index = (int)(quadrant) & 1;
            return quadrant_index == 0 ?
                local_angle :
                (double)((MathF.PI * .5f)) - local_angle;
        }
    // Original line 242: accumulate_quadrant_spans.
    public static double AccumulateQuadrantSpans(double RadiusX,
        double RadiusY,
        double start_angle,
        double sweep_angle,
        double quarter_count,
        Func<double,double,double,double> weight_func,
        uint max_iterations = 8u)
    {
            double kEpsilon = 1.0e-9;
            CanonicalArc arc = MakeCanonicalArc(RadiusX, RadiusY, start_angle, sweep_angle);
            double direction = arc.SweepAngle >= 0.0 ? 1.0 : -1.0;

            double remaining = VgScalar.Abs(arc.SweepAngle);
            double current = arc.StartAngle;
            double accumulated = 0.0;


            for (uint i = 0u; i < max_iterations && remaining > kEpsilon; ++i)
            {
                double unit = current / (double)((MathF.PI * .5f));
                double boundary = direction > 0.0 ?
                    (VgScalar.Floor(unit + 1.0e-12) + 1.0) *
                        (double)((MathF.PI * .5f)) :
                    (VgScalar.Ceiling(unit - 1.0e-12) - 1.0) *
                        (double)((MathF.PI * .5f));
                double boundary_delta = VgScalar.Abs(boundary - current);
                double span = CppMath.Min(remaining, CppMath.Max(kEpsilon, boundary_delta));
                double next = current + direction * span;
                accumulated += quarter_count *
                    weight_func(
                                   arc.Aspect,
                                   MapToFirstQuadrant(current),
                                   MapToFirstQuadrant(next)
                    );
                remaining -= span;
                current = next;
            }
            return accumulated;
        }
}
internal static class EllipseEstimateHelper
{
    public readonly record struct AspectModel(double MaxAspect, double Coeff0, double Coeff1, double Coeff2, double QuarterSafety, double ArcSafety, double ArcBias, double ArcDistributionPower);
    private static readonly AspectModel kEllipseGlobalModel = new(double.PositiveInfinity, -0.266750, 0.502935, -0.113286, 1.012092, 1.0, 0.0, 0.187500);
    private static readonly AspectModel[] kAspectModels = [ new(1.25, -0.216607, 0.502734, -0.408525, 0.989027, 0.96, 0.0, 0.0), new(1.75, -0.309862, 0.504166, -0.029477, 1.011027, 0.99, 0.0, 0.0), new(2.5, -0.302963, 0.495681, -0.042617, 1.038945, 1.00, 0.0, 0.187500), new(4.0, -0.266146, 0.485696, -0.057625, 1.016320, 1.03, 0.025, 0.187500), new(8.0, -0.193707, 0.491017, -0.123919, 1.031435, 1.00, 0.0, 0.187500), new(16.0, -0.345103, 0.514928, -0.098277, 1.031644, 1.00, 0.0, 0.187500), new(double.PositiveInfinity, -0.356905, 0.534253, -0.128001, 1.022637, 0.98, 0.0, 0.125000)];
    public const double kMinValue = ShapeEstimateArcHelper.kMinValue;
    public const double kEllipseAspectModelMinAspect = 1.0;
    // Original line 316: canonical_aspect.
    public static double CanonicalAspect(double RadiusX, double RadiusY)
    {
            return ShapeEstimateArcHelper.CanonicalAspect(RadiusX, RadiusY);
        }
    // Original line 321: select_aspect_model.
    public static AspectModel SelectAspectModel(double aspect)
    {
            foreach (AspectModel model in kAspectModels)
            {
                if (aspect < model.MaxAspect)
                {
                    return model;
                }
            }
            return kAspectModels[kAspectModels.Length - 1u];
        }
    // Original line 333: select_ellipse_model.
    public static AspectModel SelectEllipseModel(double aspect)
    {
            return aspect < kEllipseAspectModelMinAspect ? kEllipseGlobalModel : SelectAspectModel(aspect);
        }
    // Original line 338: quarter_raw.
    public static double QuarterRaw(AspectModel model,
        double max_radius,
        double min_radius,
        double tolerance)
    {
            double safe_tolerance = CppMath.Max(kMinValue, tolerance);
            double log_radius_tolerance =
                VgScalar.Log(CppMath.Max(kMinValue, max_radius / safe_tolerance));
            double log_aspect = VgScalar.Log(CppMath.Max(1.0, max_radius / min_radius));
            double log_count =
                model.Coeff0 +
                model.Coeff1 * log_radius_tolerance +
                model.Coeff2 * log_aspect;
            return VgScalar.Exp(log_count);
        }
    // Original line 356: first_quadrant_weight_prefix.
    public static double FirstQuadrantWeightPrefix(double aspect,
        double distribution_power,
        double angle)
    {
            double clamped_angle = CppMath.Clamp(
                angle,
                0.0,
                (double)((MathF.PI * .5f))
            );
            if (clamped_angle <= 0.0)
            {
                return 0.0;
            }
            if (clamped_angle >= (double)((MathF.PI * .5f)))
            {
                return 1.0;
            }



            double aspect_scale = VgScalar.Pow(CppMath.Max(1.0, aspect), CppMath.Max(0.0, distribution_power));
            return VgScalar.Atan(aspect_scale * VgScalar.Tan(clamped_angle)) /
                (double)((MathF.PI * .5f));
        }
    // Original line 383: first_quadrant_weight_span.
    public static double FirstQuadrantWeightSpan(double aspect,
        double distribution_power,
        double start_angle,
        double end_angle)
    {
            return VgScalar.Abs(
                FirstQuadrantWeightPrefix(aspect, distribution_power, end_angle) -
                FirstQuadrantWeightPrefix(aspect, distribution_power, start_angle)
            );
        }
    // Original line 396: estimate_ellipse.
    public static uint EstimateEllipse(float RadiusX,
        float RadiusY,
        float tolerance)
    {
            double k_max_count =
                (double)(uint.MaxValue);
            double max_radius = (double)(CppMath.Max(RadiusX, RadiusY));
            double min_radius = CppMath.Max(
                kMinValue,
                (double)(CppMath.Min(RadiusX, RadiusY))
            );
            double aspect = CanonicalAspect(max_radius, min_radius);
            AspectModel model = SelectEllipseModel(aspect);
            double raw_count =
                QuarterRaw(model, max_radius, min_radius, tolerance) * model.QuarterSafety;
            if (!VgScalar.IsFinite(raw_count) || raw_count * 4.0 >= k_max_count)
            {
                return uint.MaxValue;
            }
            return ShapeEstimateArcHelper.CountFromQuarter(raw_count);
        }
    // Original line 420: estimate_arc.
    public static uint EstimateArc(float RadiusX,
        float RadiusY,
        float start_angle,
        float sweep_angle,
        float tolerance)
    {
            double max_radius = (double)(CppMath.Max(RadiusX, RadiusY));
            double min_radius = CppMath.Max(
                kMinValue,
                (double)(CppMath.Min(RadiusX, RadiusY))
            );
            ShapeEstimateArcHelper.CanonicalArc arc =
                ShapeEstimateArcHelper.MakeCanonicalArc(
                    (double)(RadiusX),
                    (double)(RadiusY),
                    (double)(start_angle),
                    (double)(sweep_angle)
                );
            double aspect = arc.Aspect;
            AspectModel model = SelectAspectModel(aspect);
            double quarter_count =
                CppMath.Max(1.0, QuarterRaw(model, max_radius, min_radius, tolerance) * model.QuarterSafety);

            if (ShapeEstimateArcHelper.IsFullSweep(arc.SweepAngle))
            {
                return EstimateEllipse(RadiusX, RadiusY, tolerance);
            }

            double accumulated = ShapeEstimateArcHelper.AccumulateQuadrantSpans(
                (double)(RadiusX),
                (double)(RadiusY),
                (double)(start_angle),
                (double)(sweep_angle),
                quarter_count,
                (double span_aspect, double start, double end) => {
                    return FirstQuadrantWeightSpan(
                        span_aspect,
                        model.ArcDistributionPower,
                        start,
                        end
                    );
                }
            );
            return ShapeEstimateArcHelper.CountFromArcAccumulated(
                accumulated,
                model.ArcSafety,
                model.ArcBias
            );
        }
}
internal static class EllipseCubicBezierHelper
{
    // Original line 475: count_90_degree.
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
    // Original line 491: count_tolerance.
    public static uint CountTolerance(float RadiusX,
        float RadiusY,
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

            float scaled_err = CppMath.Max(RadiusX, RadiusY) / tolerance;
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
    // Original line 528: transform_point.
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
    // Original line 549: transform_tangent.
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
    // Original line 568: make_segment.
    public static CubicBezier MakeSegment(Ellipse ellipse,
        float start_angle,
        float sweep_angle,
        Offsetf end)
    {
            float end_angle = start_angle + sweep_angle;
            float k = (4f / 3f) * VgScalar.Tan(sweep_angle * 0.25f);
            Offsetf start = TransformPoint(
                ellipse.Center,
                ellipse.RadiusX,
                ellipse.RadiusY,
                ellipse.Rotation,
                start_angle
            );
            Offsetf start_tangent = TransformTangent(
                ellipse.RadiusX,
                ellipse.RadiusY,
                ellipse.Rotation,
                start_angle
            );
            Offsetf end_tangent = TransformTangent(
                ellipse.RadiusX,
                ellipse.RadiusY,
                ellipse.Rotation,
                end_angle
            );

            return CubicBezier.CubicTo(
                start,
                start + start_tangent * k,
                end - end_tangent * k,
                end
            );
        }
}
public struct Ellipse : IEquatable<Ellipse>
{
    public Offsetf Center;
    public float RadiusX;
    public float RadiusY;
    public float Rotation;
    public struct ProjectResult { public Offsetf Point; public float Angle, T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }
    public Ellipse(Offsetf center, float radius_x, float radius_y, float rotation) { Center = center; RadiusX = radius_x; RadiusY = radius_y; Rotation = rotation; }
    // Original line 640: Zero.
    public static Ellipse Zero()
    {
        return new Ellipse();
    }
    // Original line 644: Invalid.
    public static Ellipse Invalid()
    {
        float kNaN = float.NaN;
        return new Ellipse(new Offsetf(kNaN, kNaN), kNaN, kNaN, kNaN);
    }
    // Original line 649: CenterRadius.
    public static Ellipse CenterRadius(Offsetf Center,
    float RadiusX,
    float RadiusY,
    float Rotation = 0)
    {
        return new Ellipse(Center, RadiusX, RadiusY, Rotation);
    }
    // Original line 660: is_empty.
    public bool IsEmpty()
    {
        return RadiusX <= 0f || RadiusY <= 0f;
    }
    // Original line 664: is_valid.
    public bool IsValid()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            RadiusX >= 0f && RadiusY >= 0f;
    }
    // Original line 670: is_normalized.
    public bool IsNormalized()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            RadiusX >= 0f && RadiusY >= 0f &&
            Rotation == WrapAngle(Rotation);
    }
    // Original line 677: is_finite.
    public bool IsFinite()
    {
        return Center.IsFinite() &&
            VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) &&
            VgScalar.IsFinite(Rotation);
    }
    // Original line 684: has_nan.
    public bool HasNan()
    {
        return VgScalar.IsNaN(Center.X) || VgScalar.IsNaN(Center.Y) ||
            VgScalar.IsNaN(RadiusX) || VgScalar.IsNaN(RadiusY) || VgScalar.IsNaN(Rotation);
    }
    // Original line 689: is_point.
    public bool IsPoint()
    {
        return RadiusX == 0f && RadiusY == 0f;
    }
    // Original line 695: area.
    public float Area()
    {
        return MathF.PI * VgScalar.Abs(RadiusX * RadiusY);
    }
    // Original line 699: eccentricity.
    public float Eccentricity()
    {
        float major_radius = CppMath.Max(VgScalar.Abs(RadiusX), VgScalar.Abs(RadiusY));
        float minor_radius = CppMath.Min(VgScalar.Abs(RadiusX), VgScalar.Abs(RadiusY));
        if (major_radius <= 0f)
        {
            return 0f;
        }

        float ratio = CppMath.Max(0f, 1f - (minor_radius * minor_radius) / (major_radius * major_radius));
        return VgScalar.Sqrt(ratio);
    }
    // Original line 711: rect.
    public Rectf Rect()
    {
        float cos_rotation = VgScalar.Cos(Rotation);
        float sin_rotation = VgScalar.Sin(Rotation);
        float extent_x = VgScalar.Sqrt(
            RadiusX * RadiusX * cos_rotation * cos_rotation +
            RadiusY * RadiusY * sin_rotation * sin_rotation
        );
        float extent_y = VgScalar.Sqrt(
            RadiusX * RadiusX * sin_rotation * sin_rotation +
            RadiusY * RadiusY * cos_rotation * cos_rotation
        );
        return Rectf.Center(Center, new Sizef(extent_x * 2f, extent_y * 2f));
    }
    // Original line 725: bounds.
    public Rectf Bounds()
    {
        return Rect();
    }
    // Original line 729: contains.
    public bool Contains(Offsetf point)
    {
        var value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return false;
        }

        Offsetf local_point = value.ToLocal(point);
        float x = local_point.X / value.RadiusX;
        float y = local_point.Y / value.RadiusY;
        return x * x + y * y <= 1f;
    }
    // Original line 742: project.
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
            result.Point = value.Center;
            result.Angle = 0f;
            result.T = 0f;
            result.Distance = (point - value.Center).Length();
            result.Valid = true;
            return result;
        }

        Offsetf local_point = value.ToLocal(point);
        float closest_angle = 0f;
        if (local_point.LengthSquared() <= 1.0e-12f)
        {
            closest_angle =
                value.RadiusX <= value.RadiusY ? 0f : MathF.PI * 0.5f;
        }
        else if (VgScalar.Abs(local_point.Y) <= 1.0e-6f)
        {
            closest_angle = local_point.X >= 0f ? 0f : MathF.PI;
        }
        else if (VgScalar.Abs(local_point.X) <= 1.0e-6f)
        {
            closest_angle =
                local_point.Y >= 0f ? MathF.PI * 0.5f : -MathF.PI * 0.5f;
        }
        else
        {
            closest_angle = value.ClosestAngleLocal(local_point, tolerance);
        }

        float wrapped_angle = WrapAngle(closest_angle);
        float ratio = CppMath.Fmod(wrapped_angle, (MathF.PI * 2));
        if (ratio < 0f)
        {
            ratio += (MathF.PI * 2);
        }

        result.Point = value.PointAtAngle(closest_angle);
        result.Angle = wrapped_angle;
        result.T = ratio / (MathF.PI * 2);
        result.Distance = (point - result.Point).Length();
        result.Valid = true;
        return result;
    }
    // Original line 799: signed_distance.
    public float SignedDistance(Offsetf point, float tolerance = .01f)
    {
        var value = Normalized();
        if (!value.IsFinite())
        {
            return 0f;
        }

        if (value.IsPoint())
        {
            return (point - value.Center).Length();
        }

        float unsigned_distance = value.Project(point, tolerance).Distance;
        return value.Contains(point) ? -unsigned_distance : unsigned_distance;
    }
    // Original line 815: distance.
    public float Distance(Offsetf point, float tolerance = .01f)
    {
        return Project(point, tolerance).Distance;
    }
    // Original line 819: closest_point.
    public Offsetf ClosestPoint(Offsetf point, float tolerance = .01f)
    {
        return Project(point, tolerance).Point;
    }
    // Original line 823: create_sampler.
    public EllipseSampler CreateSampler(float angle = 0)
    {
        return new EllipseSampler(angle, Rotation);
    }
    // Original line 827: point_at_angle.
    public Offsetf PointAtAngle(float angle)
    {
        EllipseSampler sampler = CreateSampler(angle);
        return sampler.SamplePoint(Center, RadiusX, RadiusY);
    }
    // Original line 832: point_at_ratio.
    public Offsetf PointAtRatio(float ratio)
    {
        return PointAtAngle(ratio * (MathF.PI * 2));
    }
    // Original line 836: point_at_t.
    public Offsetf PointAtT(float t)
    {
        return PointAtRatio(t);
    }
    // Original line 842: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return ShapeSampleHelper.CalcGeometricTolerance(
            tessellation_factor,
            pixel_ratio
        );
    }
    // Original line 852: estimate_segment_count.
    public uint EstimateSegmentCount(float tolerance)
    {

        Ellipse value = Normalized();
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f || !value.IsValid() || value.IsEmpty())
        {
            return 0u;
        }

        return EllipseEstimateHelper.EstimateEllipse(
            value.RadiusX,
            value.RadiusY,
            tolerance
        );
    }
    // Original line 868: sample.
    public void Sample(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Ellipse value = Normalized();
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
    // Original line 887: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Ellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        ShapeSampleHelper.SampleClosedAdaptive(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            (float t) => { return value.PointAtT(t); },
            functor
        );
    }
    // Original line 907: sample.
    public void Sample(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Ellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        ShapeSampleHelper.SampleClosedByStepLength(
            desc.StepLength,
            ShapeSampleHelper.EstimateStepLengthTolerance(desc.StepLength),
            desc.MaxDepth,
            desc.Direction,
            (float t) => { return value.PointAtT(t); },
            functor
        );
    }
    // Original line 928: sample_with_sampler.
    public void SampleWithSampler(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float,EllipseSampler> functor)
    {
        Ellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        EllipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleClosedUniformWithSampler(
            desc.SegmentCount,
            desc.Direction,
            ref sampler,
            (ref EllipseSampler sampler, float t) => {
                sampler.SetAngle(t * (MathF.PI * 2));
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 952: sample_with_sampler.
    public void SampleWithSampler(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float,EllipseSampler> functor)
    {
        Ellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        EllipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleClosedAdaptiveWithSampler(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            ref sampler,
            (ref EllipseSampler sampler, float t) => {
                sampler.SetAngle(t * (MathF.PI * 2));
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 977: sample_with_sampler.
    public void SampleWithSampler(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float,EllipseSampler> functor)
    {
        Ellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        EllipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleClosedByStepLengthWithSampler(
            desc.StepLength,
            ShapeSampleHelper.EstimateStepLengthTolerance(desc.StepLength),
            desc.MaxDepth,
            desc.Direction,
            ref sampler,
            (ref EllipseSampler sampler, float t) => {
                sampler.SetAngle(t * (MathF.PI * 2));
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 1004: cubic_bezier_count.
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

        Ellipse value = Normalized();
        return EllipseCubicBezierHelper.CountTolerance(
            value.RadiusX,
            value.RadiusY,
            (MathF.PI * 2),
            tolerance
        );
    }
    // Original line 1025: cubic_bezier_count_fast.
    public uint CubicBezierCountFast()
    {
        if (!IsValid() || IsEmpty())
        {
            return 0u;
        }

        return EllipseCubicBezierHelper.Count90Degree((MathF.PI * 2));
    }
    // Original line 1037: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance, Action<CubicBezier> func)
    {
        SplitToCubicBeziers(
            tolerance,
            EShapeSampleDirection.Forward,
            func
        );
    }
    // Original line 1046: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance,
    EShapeSampleDirection direction,
    Action<CubicBezier> func)
    {
        uint count = CubicBezierCount(tolerance);
        if (count == 0u)
        {
            return;
        }

        Ellipse value = Normalized();
        float segment_sweep =
            (direction == EShapeSampleDirection.Reverse ? -(MathF.PI * 2) : (MathF.PI * 2)) /
            (float)(count);
        Offsetf precise_end = value.PointAtAngle(0f);
        for (uint i = 0u; i < count; ++i)
        {
            float segment_start = segment_sweep * (float)(i);
            float segment_end = segment_sweep * (float)(i + 1u);
            Offsetf end =
                (i + 1u == count) ? precise_end : value.PointAtAngle(segment_end);
            CubicBezier cubic = EllipseCubicBezierHelper.MakeSegment(
                value,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 1079: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(Action<CubicBezier> func)
    {
        SplitToCubicBeziersFast(
            EShapeSampleDirection.Forward,
            func
        );
    }
    // Original line 1087: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(EShapeSampleDirection direction, Action<CubicBezier> func)
    {
        if (!IsValid() || IsEmpty())
        {
            return;
        }

        Ellipse value = Normalized();
        uint count = value.CubicBezierCountFast();
        float segment_sweep =
            (direction == EShapeSampleDirection.Reverse ? -(MathF.PI * 2) : (MathF.PI * 2)) /
            (float)(count);
        Offsetf precise_end = value.PointAtAngle(0f);
        for (uint i = 0u; i < count; ++i)
        {
            float segment_start = segment_sweep * (float)(i);
            float segment_end = segment_sweep * (float)(i + 1u);
            Offsetf end =
                (i + 1u == count) ? precise_end : value.PointAtAngle(segment_end);
            CubicBezier cubic = EllipseCubicBezierHelper.MakeSegment(
                value,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 1117: normalized.
    public Ellipse Normalized()
    {
        return new Ellipse(Center,
            VgScalar.Abs(RadiusX),
            VgScalar.Abs(RadiusY),
            WrapAngle(Rotation));
    }
    // Original line 1126: shift.
    public Ellipse Shift(Offsetf offset)
    {
        return new Ellipse(Center + offset, RadiusX, RadiusY, Rotation);
    }
    // Original line 1130: inflate.
    public Ellipse Inflate(float delta)
    {
        return new Ellipse(Center,
            CppMath.Max(0f, RadiusX + delta),
            CppMath.Max(0f, RadiusY + delta),
            Rotation);
    }
    // Original line 1139: deflate.
    public Ellipse Deflate(float delta)
    {
        return Inflate(-delta);
    }
    // Original line 1156: _wrap_angle.
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
    // Original line 1174: _positive_tolerance.
    private static float PositiveTolerance(float tolerance)
    {
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
        {
            return 0.01f;
        }
        return tolerance;
    }
    // Original line 1182: _to_local.
    private Offsetf ToLocal(Offsetf point)
    {
        Offsetf delta = point - Center;
        float cos_rotation = VgScalar.Cos(Rotation);
        float sin_rotation = VgScalar.Sin(Rotation);
        return new Offsetf(delta.X * cos_rotation + delta.Y * sin_rotation,
            -delta.X * sin_rotation + delta.Y * cos_rotation);
    }
    // Original line 1192: _to_world.
    private Offsetf ToWorld(Offsetf point)
    {
        float cos_rotation = VgScalar.Cos(Rotation);
        float sin_rotation = VgScalar.Sin(Rotation);
        return Center +
            new Offsetf(
                   point.X * cos_rotation - point.Y * sin_rotation,
                   point.X * sin_rotation + point.Y * cos_rotation
            );
    }
    // Original line 1202: _closest_angle_local.
    private float ClosestAngleLocal(Offsetf local_point,
    float tolerance)
    {
        float tol = PositiveTolerance(tolerance);
        uint sample_count = CppMath.Clamp(
            (uint)(VgScalar.Ceiling((MathF.PI * 2) * CppMath.Max(RadiusX, RadiusY) / tol)),
            32u,
            2048u
        );
        float step = (MathF.PI * 2) / (float)(sample_count);

        float best_angle = 0f;
        float best_distance_sq = float.PositiveInfinity;
        for (uint i = 0; i < sample_count; ++i)
        {
            float angle = step * (float)(i);
            Offsetf candidate = new Offsetf(RadiusX * VgScalar.Cos(angle), RadiusY * VgScalar.Sin(angle));
            float distance_sq = (candidate - local_point).LengthSquared();
            if (distance_sq < best_distance_sq)
            {
                best_distance_sq = distance_sq;
                best_angle = angle;
            }
        }

        float left = best_angle - step;
        float right = best_angle + step;
        for (uint i = 0; i < 24u; ++i)
        {
            float l = left + (right - left) / 3f;
            float r = right - (right - left) / 3f;
            Offsetf left_point = new Offsetf(RadiusX * VgScalar.Cos(l), RadiusY * VgScalar.Sin(l));
            Offsetf right_point = new Offsetf(RadiusX * VgScalar.Cos(r), RadiusY * VgScalar.Sin(r));
            if ((left_point - local_point).LengthSquared() <=
                (right_point - local_point).LengthSquared())
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
    public readonly bool Equals(Ellipse other) => Center == other.Center && RadiusX == other.RadiusX && RadiusY == other.RadiusY && Rotation == other.Rotation;
    public override readonly bool Equals(object? obj) => obj is Ellipse other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Center, RadiusX, RadiusY, Rotation);
    public static bool operator ==(Ellipse lhs, Ellipse rhs) => lhs.Equals(rhs);
    public static bool operator !=(Ellipse lhs, Ellipse rhs) => !lhs.Equals(rhs);
}
