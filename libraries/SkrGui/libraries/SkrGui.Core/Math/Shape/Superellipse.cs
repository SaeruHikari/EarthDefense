// Source: SkrGuiCore/math/shape/superellipse.hpp @ 611561f8.
// Full geometry, sampling, projection and cubic conversion bodies.
using System.Diagnostics;
namespace SkrGui;
internal static class SuperellipseEstimateHelper
{
    public readonly record struct SegmentModel(double MaxExponent,double[] Coeff,double QuarterSafety,double ArcSafety,double ArcBias,double DistributionExponent);
    private static readonly SegmentModel[] kSegmentModels = [ new(0.75,
            new double[] {  -0.772364, 0.539092, -0.137376, 2.492449, -0.053997, -0.020776, -0.574183  },
            0.980218,
            0.960000,
            0.0,
            0.750000), new(1.25,
            new double[] {  0.083735, 0.238703, -0.060629, 0.041988, 0.841640, -0.249340, -0.819674  },
            1.067512,
            1.050000,
            0.050000,
            1.250000), new(1.75,
            new double[] {  -0.183362, 0.360602, -0.097841, 1.375537, 0.132580, -0.043667, -1.666910  },
            1.065596,
            1.185000,
            1.050000,
            1.677923), new(2.25,
            new double[] {  0.100322, 0.418682, -0.117511, 0.027987, 0.011939, -0.007583, 0.028008  },
            1.048029,
            1.140000,
            0.950000,
            1.834436), new(3.75,
            new double[] {  -0.050164, 0.448026, -0.129540, 0.345057, -0.025257, 0.006649, -0.130183  },
            1.051874,
            1.095000,
            0.100000,
            2.250000), new(4.75,
            new double[] {  -0.597873, 0.447704, -0.150275, 1.114172, -0.024351, 0.022769, -0.402657  },
            1.040940,
            1.140000,
            0.075000,
            3.750000), new(8.0,
            new double[] {  -0.373505, 0.439305, -0.137082, 0.646441, -0.020541, 0.014146, -0.191532  },
            1.030045,
            1.110000,
            0.0,
            4.750000), new(16.0,
            new double[] {  0.011810, 0.425490, -0.138982, 0.210929, -0.013497, 0.015118, -0.072001  },
            1.048222,
            1.035000,
            0.025000,
            8.000000)];
    public const double kMinValue = ShapeEstimateArcHelper.kMinValue;
    // Original line 244: select_segment.
    public static SegmentModel SelectSegment(double Exponent)
    {
            double safe_exponent = CppMath.Max(kMinValue, Exponent);
            foreach (SegmentModel model in kSegmentModels)
            {
                if (safe_exponent < model.MaxExponent)
                {
                    return model;
                }
            }
            return kSegmentModels[kSegmentModels.Length - 1u];
        }
    // Original line 257: quarter_raw.
    public static double QuarterRaw(double max_radius,
        double min_radius,
        double Exponent,
        double tolerance)
    {
            SegmentModel model = SelectSegment(Exponent);
            double safe_tolerance = CppMath.Max(kMinValue, tolerance);
            double safe_exponent = CppMath.Max(kMinValue, Exponent);
            double log_radius_tolerance =
                VgScalar.Log(CppMath.Max(kMinValue, max_radius / safe_tolerance));
            double log_aspect =
                VgScalar.Log(CppMath.Max(1.0, max_radius / CppMath.Max(kMinValue, min_radius)));
            double exponent_delta = VgScalar.Abs(VgScalar.Log(safe_exponent));
            double radius_exponent = log_radius_tolerance * exponent_delta;
            double aspect_exponent = log_aspect * exponent_delta;
            double exponent_delta_sq = exponent_delta * exponent_delta;
            double log_count =
                model.Coeff[0] +
                model.Coeff[1] * log_radius_tolerance +
                model.Coeff[2] * log_aspect +
                model.Coeff[3] * exponent_delta +
                model.Coeff[4] * radius_exponent +
                model.Coeff[5] * aspect_exponent +
                model.Coeff[6] * exponent_delta_sq;
            return VgScalar.Exp(log_count);
        }
    // Original line 286: first_quadrant_weight_prefix.
    public static double FirstQuadrantWeightPrefix(double distribution_exponent,
        double aspect,
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



            double aspect_u =
                VgScalar.Atan(VgScalar.Sqrt(CppMath.Max(1.0, aspect)) * VgScalar.Tan(clamped_angle)) /
                (double)((MathF.PI * .5f));
            double power = CppMath.Clamp(
                2.0 / CppMath.Max(kMinValue, distribution_exponent),
                0.125,
                8.0
            );
            double lhs = VgScalar.Pow(CppMath.Max(kMinValue, aspect_u), power);
            double rhs = VgScalar.Pow(CppMath.Max(kMinValue, 1.0 - aspect_u), power);
            return lhs / (lhs + rhs);
        }
    // Original line 321: first_quadrant_weight_span.
    public static double FirstQuadrantWeightSpan(double distribution_exponent,
        double aspect,
        double start_angle,
        double end_angle)
    {
            return VgScalar.Abs(
                FirstQuadrantWeightPrefix(distribution_exponent, aspect, end_angle) -
                FirstQuadrantWeightPrefix(distribution_exponent, aspect, start_angle)
            );
        }
    // Original line 334: accumulate_arc_quarters.
    public static double AccumulateArcQuarters(double RadiusX,
        double RadiusY,
        double Exponent,
        double start_angle,
        double sweep_angle,
        double quarter_count)
    {
            SegmentModel model = SelectSegment(Exponent);
            return AccumulateArcQuartersWithDistributionExponent(
                RadiusX,
                RadiusY,
                start_angle,
                sweep_angle,
                quarter_count,
                model.DistributionExponent
            );
        }
    // Original line 354: accumulate_arc_quarters_with_distribution_exponent.
    public static double AccumulateArcQuartersWithDistributionExponent(double RadiusX,
        double RadiusY,
        double start_angle,
        double sweep_angle,
        double quarter_count,
        double distribution_exponent)
    {
            return ShapeEstimateArcHelper.AccumulateQuadrantSpans(
                RadiusX,
                RadiusY,
                start_angle,
                sweep_angle,
                quarter_count,
                (double aspect, double start, double end) => {
                    return FirstQuadrantWeightSpan(
                        distribution_exponent,
                        aspect,
                        start,
                        end
                    );
                },
                16u
            );
        }
    // Original line 381: estimate_superellipse.
    public static uint EstimateSuperellipse(float RadiusX,
        float RadiusY,
        float Exponent,
        float tolerance)
    {
            double max_radius = (double)(CppMath.Max(RadiusX, RadiusY));
            double min_radius = CppMath.Max(
                kMinValue,
                (double)(CppMath.Min(RadiusX, RadiusY))
            );
            double raw_count = QuarterRaw(
                                         max_radius,
                                         min_radius,
                                         (double)(Exponent),
                                         (double)(tolerance)
                                     ) *
                SelectSegment((double)(Exponent)).QuarterSafety;
            if (!VgScalar.IsFinite(raw_count))
            {
                return uint.MaxValue;
            }
            return ShapeEstimateArcHelper.CountFromQuarter(raw_count);
        }
    // Original line 407: estimate_arc.
    public static uint EstimateArc(float RadiusX,
        float RadiusY,
        float Exponent,
        float start_angle,
        float sweep_angle,
        float tolerance)
    {
            ShapeEstimateArcHelper.CanonicalArc arc =
                ShapeEstimateArcHelper.MakeCanonicalArc(
                    (double)(RadiusX),
                    (double)(RadiusY),
                    (double)(start_angle),
                    (double)(sweep_angle)
                );
            if (ShapeEstimateArcHelper.IsFullSweep(arc.SweepAngle))
            {
                return EstimateSuperellipse(RadiusX, RadiusY, Exponent, tolerance);
            }

            double max_radius = (double)(CppMath.Max(RadiusX, RadiusY));
            double min_radius = CppMath.Max(
                kMinValue,
                (double)(CppMath.Min(RadiusX, RadiusY))
            );
            SegmentModel model = SelectSegment((double)(Exponent));
            double quarter_count = CppMath.Max(
                1.0,
                QuarterRaw(
                    max_radius,
                    min_radius,
                    (double)(Exponent),
                    (double)(tolerance)
                ) * model.QuarterSafety
            );
            double accumulated = AccumulateArcQuarters(
                (double)(RadiusX),
                (double)(RadiusY),
                (double)(Exponent),
                (double)(start_angle),
                (double)(sweep_angle),
                quarter_count
            );
            return ShapeEstimateArcHelper.CountFromArcAccumulated(
                accumulated,
                model.ArcSafety,
                model.ArcBias
            );
        }
}
internal static class SuperellipseSampleHelper
{
    public readonly record struct Sample(Offsetf Point,float T);
    public const float _k_t_epsilon = 1.0e-6f;
    // Original line 462: sample_closed_quadrants.
    public static void SampleClosedQuadrants(float tolerance,
        uint max_depth,
        EShapeSampleDirection direction,
        Superellipse shape,
        Action<Offsetf,float> functor)
    {

            bool has_last_t = false;
            float last_t = 0f;

            var emit = (Offsetf point, float t) => {
                float sample_t = NormalizeClosedT(t);
                if (sample_t >= 1f)
                {
                    return;
                }
                if (has_last_t && IsSameT(sample_t, last_t))
                {
                    return;
                }

                functor(point, sample_t);
                last_t = sample_t;
                has_last_t = true;
            };

            if (IsDiamondExponent(shape.Exponent))
            {
                SampleClosedDiamond(direction, shape, emit);
                return;
            }



            if (direction == EShapeSampleDirection.Forward)
            {
                for (uint i = 0u; i < 4u; ++i)
                {
                    SampleOpenSpan(i, 0u, tolerance, max_depth, direction, shape, emit);
                    SampleOpenSpan(i, 1u, tolerance, max_depth, direction, shape, emit);
                }
            }
            else
            {
                for (uint i = 4u; i > 0u; --i)
                {
                    SampleOpenSpan(i - 1u, 1u, tolerance, max_depth, direction, shape, emit);
                    SampleOpenSpan(i - 1u, 0u, tolerance, max_depth, direction, shape, emit);
                }
            }
        }
    // Original line 516: sample_arc.
    public static void SampleArc(float tolerance,
        uint max_depth,
        EShapeSampleDirection direction,
        Superellipse shape,
        float start_angle,
        float sweep_angle,
        Action<Offsetf,float> functor)
    {
            float sweep_closed_t = sweep_angle / (MathF.PI * 2);
            if (sweep_closed_t == 0f)
            {
                return;
            }




            bool forward = direction == EShapeSampleDirection.Forward;
            float arc_start_closed_t = start_angle / (MathF.PI * 2);
            float start_arc_t = forward ? 0f : 1f;
            float end_arc_t = forward ? 1f : 0f;
            float target_closed_t = arc_start_closed_t + sweep_closed_t * end_arc_t;
            float current_closed_t = arc_start_closed_t + sweep_closed_t * start_arc_t;

            bool has_last_t = false;
            float last_t = 0f;
            var emit = (Offsetf point, float t) => {
                float arc_t = NormalizeArcT(t);
                if (has_last_t && IsSameT(arc_t, last_t))
                {
                    return;
                }

                functor(point, arc_t);
                last_t = arc_t;
                has_last_t = true;
            };

            if (IsDiamondExponent(shape.Exponent))
            {
                SampleArcDiamond(
                    current_closed_t,
                    target_closed_t,
                    arc_start_closed_t,
                    sweep_closed_t,
                    shape,
                    emit
                );
                return;
            }

            while (!IsSameT(current_closed_t, target_closed_t))
            {
                float next_closed_t = NextHalfBoundary(current_closed_t, target_closed_t);
                SampleArcSpan(
                    current_closed_t,
                    next_closed_t,
                    arc_start_closed_t,
                    sweep_closed_t,
                    tolerance,
                    max_depth,
                    shape,
                    emit
                );
                current_closed_t = next_closed_t;
            }
        }
    // Original line 595: _is_same_t.
    public static bool IsSameT(float lhs, float rhs)
    {
            return VgScalar.Abs(lhs - rhs) <= _k_t_epsilon;
        }
    // Original line 599: _is_diamond_exponent.
    public static bool IsDiamondExponent(float Exponent)
    {
            return VgScalar.NearlyEqual(Exponent, 1f);
        }
    // Original line 604: _normalize_closed_t.
    public static float NormalizeClosedT(float t)
    {
            if (t <= _k_t_epsilon)
            {
                return 0f;
            }
            if (t >= 1f - _k_t_epsilon)
            {
                return 1f;
            }
            return CppMath.Clamp(t, 0f, 1f);
        }
    // Original line 617: _normalize_arc_t.
    public static float NormalizeArcT(float t)
    {
            if (t <= _k_t_epsilon)
            {
                return 0f;
            }
            if (t >= 1f - _k_t_epsilon)
            {
                return 1f;
            }
            return t;
        }
    // Original line 630: _normalize_loop_t.
    public static float NormalizeLoopT(float t)
    {
            float Normalized = CppMath.Fmod(t, 1f);
            if (Normalized < 0f)
            {
                Normalized += 1f;
            }
            return Normalized;
        }
    // Original line 640: _next_half_boundary.
    public static float NextHalfBoundary(float current_t, float target_t)
    {
            if (target_t > current_t)
            {
                float boundary_index_scope0 = VgScalar.Floor(current_t * 8f + _k_t_epsilon) + 1f;
                float boundary_t_scope1 = boundary_index_scope0 * 0.125f;
                return boundary_t_scope1 >= target_t - _k_t_epsilon ? target_t : boundary_t_scope1;
            }

            float boundary_index_scope2 = VgScalar.Ceiling(current_t * 8f - _k_t_epsilon) - 1f;
            float boundary_t_scope3 = boundary_index_scope2 * 0.125f;
            return boundary_t_scope3 <= target_t + _k_t_epsilon ? target_t : boundary_t_scope3;
        }
    // Original line 654: _next_quarter_boundary.
    public static float NextQuarterBoundary(float current_t, float target_t)
    {
            if (target_t > current_t)
            {
                float boundary_index_scope0 = VgScalar.Floor(current_t * 4f + _k_t_epsilon) + 1f;
                float boundary_t_scope1 = boundary_index_scope0 * 0.25f;
                return boundary_t_scope1 >= target_t - _k_t_epsilon ? target_t : boundary_t_scope1;
            }

            float boundary_index_scope2 = VgScalar.Ceiling(current_t * 4f - _k_t_epsilon) - 1f;
            float boundary_t_scope3 = boundary_index_scope2 * 0.25f;
            return boundary_t_scope3 <= target_t + _k_t_epsilon ? target_t : boundary_t_scope3;
        }
    // Original line 669: _sample_closed_diamond.
    public static void SampleClosedDiamond(EShapeSampleDirection direction,
        Superellipse shape,
        Action<Offsetf,float> functor)
    {
            if (direction == EShapeSampleDirection.Forward)
            {
                for (uint i = 0u; i < 4u; ++i)
                {
                    float t = (float)(i) * 0.25f;
                    functor(shape.PointAtT(t), t);
                }
            }
            else
            {
                for (uint i = 4u; i > 0u; --i)
                {
                    float t = (float)(i - 1u) * 0.25f;
                    functor(shape.PointAtT(t), t);
                }
            }
        }
    // Original line 694: _sample_arc_diamond.
    public static void SampleArcDiamond(float start_closed_t,
        float end_closed_t,
        float arc_start_closed_t,
        float sweep_closed_t,
        Superellipse shape,
        Action<Offsetf,float> functor)
    {
            float current_closed_t = start_closed_t;
            while (true)
            {
                float arc_t = (current_closed_t - arc_start_closed_t) / sweep_closed_t;
                functor(
                    shape.PointAtT(NormalizeLoopT(current_closed_t)),
                    arc_t
                );

                if (IsSameT(current_closed_t, end_closed_t))
                {
                    break;
                }
                current_closed_t = NextQuarterBoundary(current_closed_t, end_closed_t);
            }
        }
    // Original line 721: _sample_arc_span.
    public static void SampleArcSpan(float start_closed_t,
        float end_closed_t,
        float arc_start_closed_t,
        float sweep_closed_t,
        float tolerance,
        uint max_depth,
        Superellipse shape,
        Action<Offsetf,float> functor)
    {
            uint half_span = HalfSpanIndex((start_closed_t + end_closed_t) * 0.5f);
            uint quadrant = half_span >> 1;
            uint half = half_span & 1u;
            float start_u = EvalUFromClosedT(shape.Exponent, quadrant, half, start_closed_t);
            float end_u = EvalUFromClosedT(shape.Exponent, quadrant, half, end_closed_t);

            var span_eval = (float t) => {
                float u_scope0 = start_u + (end_u - start_u) * t;
                return EvalSample(shape, quadrant, half, u_scope0).Point;
            };
            var span_functor = (Offsetf ignored, float t) => {
                float u_scope1 = start_u + (end_u - start_u) * t;
                float closed_t = UnwrapClosedT(
                    EvalSampleT(shape, quadrant, half, u_scope1),
                    start_closed_t,
                    end_closed_t
                );
                functor(
                    shape.PointAtT(NormalizeLoopT(closed_t)),
                    (closed_t - arc_start_closed_t) / sweep_closed_t
                );
            };

            ShapeSampleHelper.SampleOpenAdaptive(
                tolerance,
                max_depth,
                EShapeSampleDirection.Forward,
                span_eval,
                span_functor
            );
        }
    // Original line 764: _half_span_index.
    public static uint HalfSpanIndex(float closed_t)
    {
            float Normalized = NormalizeLoopT(closed_t);
            return CppMath.Min(
                (uint)(Normalized * 8f),
                7u
            );
        }
    // Original line 773: _eval_u_from_closed_t.
    public static float EvalUFromClosedT(float Exponent,
        uint quadrant,
        uint half,
        float closed_t)
    {
            float angle = NormalizeLoopT(closed_t) * (MathF.PI * 2);
            float point_power = 2f / Exponent;
            float x_abs = VgScalar.Pow(VgScalar.Abs(VgScalar.Cos(angle)), point_power);
            float y_abs = VgScalar.Pow(VgScalar.Abs(VgScalar.Sin(angle)), point_power);
            float diagonal = VgScalar.Pow(0.5f, 1f / Exponent);

            if (StartsOnHorizontalAxis(quadrant))
            {
                float u_scope0 = half == 0u ? y_abs / diagonal : 1f - x_abs / diagonal;
                return CppMath.Clamp(u_scope0, 0f, 1f);
            }

            float u_scope1 = half == 0u ? x_abs / diagonal : 1f - y_abs / diagonal;
            return CppMath.Clamp(u_scope1, 0f, 1f);
        }
    // Original line 796: _unwrap_closed_t.
    public static float UnwrapClosedT(float closed_t,
        float start_closed_t,
        float end_closed_t)
    {
            float reference = (start_closed_t + end_closed_t) * 0.5f;
            return closed_t + VgScalar.Round(reference - closed_t);
        }
    // Original line 807: _sample_open_span.
    public static void SampleOpenSpan(uint quadrant,
        uint half,
        float tolerance,
        uint max_depth,
        EShapeSampleDirection direction,
        Superellipse shape,
        Action<Offsetf,float> functor)
    {
            var span_eval = (float local_t) => {
                return EvalSample(shape, quadrant, half, local_t).Point;
            };
            var span_functor = (Offsetf point, float local_t) => {
                functor(point, EvalSampleT(shape, quadrant, half, local_t));
            };

            ShapeSampleHelper.SampleOpenAdaptive(
                tolerance,
                max_depth,
                direction,
                span_eval,
                span_functor
            );
        }
    // Original line 833: _eval_sample.
    public static Sample EvalSample(Superellipse shape,
        uint quadrant,
        uint half,
        float u)
    {
            float x_abs = 0f;
            float y_abs = 0f;
            EvalAxisAbs(shape.Exponent, quadrant, half, u, ref x_abs, ref y_abs);

            float x_sign = XSign(quadrant);
            float y_sign = YSign(quadrant);
            float local_x = shape.RadiusX * x_sign * x_abs;
            float local_y = shape.RadiusY * y_sign * y_abs;
            float cos_rotation = VgScalar.Cos(shape.Rotation);
            float sin_rotation = VgScalar.Sin(shape.Rotation);

            return new Sample(shape.Center +
                    new Offsetf(
                        local_x * cos_rotation - local_y * sin_rotation,
                        local_x * sin_rotation + local_y * cos_rotation
                    ),
                EvalT(shape.Exponent, quadrant, half, u, x_abs, y_abs));
        }
    // Original line 861: _eval_sample_t.
    public static float EvalSampleT(Superellipse shape,
        uint quadrant,
        uint half,
        float u)
    {
            float x_abs = 0f;
            float y_abs = 0f;
            EvalAxisAbs(shape.Exponent, quadrant, half, u, ref x_abs, ref y_abs);
            return EvalT(shape.Exponent, quadrant, half, u, x_abs, y_abs);
        }
    // Original line 874: _eval_axis_abs.
    public static void EvalAxisAbs(float Exponent,
        uint quadrant,
        uint half,
        float u,
        ref float x_abs,
        ref float y_abs)
    {
            float clamped_u = CppMath.Clamp(u, 0f, 1f);
            float diagonal = VgScalar.Pow(0.5f, 1f / Exponent);
            if (StartsOnHorizontalAxis(quadrant))
            {
                if (half == 0u)
                {
                    y_abs = diagonal * clamped_u;
                    x_abs = PairedAbs(y_abs, Exponent);
                }
                else
                {
                    x_abs = diagonal * (1f - clamped_u);
                    y_abs = PairedAbs(x_abs, Exponent);
                }
            }
            else
            {
                if (half == 0u)
                {
                    x_abs = diagonal * clamped_u;
                    y_abs = PairedAbs(x_abs, Exponent);
                }
                else
                {
                    y_abs = diagonal * (1f - clamped_u);
                    x_abs = PairedAbs(y_abs, Exponent);
                }
            }
        }
    // Original line 913: _eval_t.
    public static float EvalT(float Exponent,
        uint quadrant,
        uint half,
        float u,
        float x_abs,
        float y_abs)
    {
            float angle_power = Exponent * 0.5f;
            float cos_angle = XSign(quadrant) * VgScalar.Pow(CppMath.Clamp(x_abs, 0f, 1f), angle_power);
            float sin_angle = YSign(quadrant) * VgScalar.Pow(CppMath.Clamp(y_abs, 0f, 1f), angle_power);
            float angle = VgScalar.Atan2(sin_angle, cos_angle);
            if (angle < 0f)
            {
                angle += (MathF.PI * 2);
            }

            if (quadrant == 3u && half == 1u && u >= 1f - _k_t_epsilon)
            {
                angle = (MathF.PI * 2);
            }

            return angle / (MathF.PI * 2);
        }
    // Original line 939: _paired_abs.
    public static float PairedAbs(float axis_abs, float Exponent)
    {
            float powered = VgScalar.Pow(CppMath.Clamp(axis_abs, 0f, 1f), Exponent);
            return VgScalar.Pow(CppMath.Max(0f, 1f - powered), 1f / Exponent);
        }
    // Original line 945: _starts_on_horizontal_axis.
    public static bool StartsOnHorizontalAxis(uint quadrant)
    {
            return (quadrant & 1u) == 0u;
        }
    // Original line 950: _x_sign.
    public static float XSign(uint quadrant)
    {
            return quadrant == 0u || quadrant == 3u ? 1f : -1f;
        }
    // Original line 955: _y_sign.
    public static float YSign(uint quadrant)
    {
            return quadrant < 2u ? 1f : -1f;
        }
}
internal static class SuperellipseCubicBezierHelper
{
    // Original line 963: clean_trig_value.
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
    // Original line 981: count_90_degree.
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
    // Original line 997: count_tolerance.
    public static uint CountTolerance(float RadiusX,
        float RadiusY,
        float Exponent,
        float sweep_angle,
        float tolerance,
        bool closed)
    {
            if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
            {
                Debug.Assert(false , "cubic bezier tolerance must be positive and finite");
                return 0u;
            }

            if (sweep_angle == 0f || Exponent <= 0f)
            {
                return 0u;
            }

            if (Superellipse.IsEllipseExponent(Exponent))
            {
                float scaled_err = CppMath.Max(RadiusX, RadiusY) / tolerance;
                float n_err = CppMath.Max(
                    VgScalar.Pow(1.1163f * scaled_err, 1.0f / 6.0f),
                    3.999999f
                );
                float count_scope0 = VgScalar.Ceiling(
                    n_err * VgScalar.Abs(sweep_angle) / (MathF.PI * 2)
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
                sweep_angle,
                tolerance,
                closed
            );
            base_count = CppMath.Max(base_count, Count90Degree(sweep_angle));

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
    // Original line 1062: transform_point.
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
            float local_x = RadiusX * Superellipse.SignedPow(cos_angle, power);
            float local_y = RadiusY * Superellipse.SignedPow(sin_angle, power);
            return Center +
                new Offsetf(
                       local_x * cos_rotation - local_y * sin_rotation,
                       local_x * sin_rotation + local_y * cos_rotation
                );
        }
    // Original line 1085: transform_tangent.
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
    // Original line 1104: make_ellipse_segment.
    public static CubicBezier MakeEllipseSegment(Superellipse shape,
        float start_angle,
        float sweep_angle,
        Offsetf end)
    {
            float end_angle = start_angle + sweep_angle;
            float k = (4f / 3f) * VgScalar.Tan(sweep_angle * 0.25f);
            Offsetf start = TransformPoint(
                shape.Center,
                shape.RadiusX,
                shape.RadiusY,
                shape.Rotation,
                shape.Exponent,
                start_angle
            );
            Offsetf start_tangent = TransformTangent(
                shape.RadiusX,
                shape.RadiusY,
                shape.Rotation,
                start_angle
            );
            Offsetf end_tangent = TransformTangent(
                shape.RadiusX,
                shape.RadiusY,
                shape.Rotation,
                end_angle
            );

            return CubicBezier.CubicTo(
                start,
                start + start_tangent * k,
                end - end_tangent * k,
                end
            );
        }
    // Original line 1142: make_catmull_segment.
    public static CubicBezier MakeCatmullSegment(Superellipse shape,
        float start_angle,
        float sweep_angle,
        Offsetf end)
    {
            float end_angle = start_angle + sweep_angle;
            Offsetf p0 = shape.PointAtAngle(start_angle - sweep_angle);
            Offsetf p1 = shape.PointAtAngle(start_angle);
            Offsetf p2 = end;
            Offsetf p3 = shape.PointAtAngle(end_angle + sweep_angle);

            return CubicBezier.CubicTo(
                p1,
                p1 + (p2 - p0) * (1f / 6f),
                p2 - (p3 - p1) * (1f / 6f),
                p2
            );
        }
    // Original line 1163: make_segment.
    public static CubicBezier MakeSegment(Superellipse shape,
        float start_angle,
        float sweep_angle,
        Offsetf end)
    {
            if (Superellipse.IsEllipseExponent(shape.Exponent))
            {
                return MakeEllipseSegment(shape, start_angle, sweep_angle, end);
            }
            return MakeCatmullSegment(shape, start_angle, sweep_angle, end);
        }
    // Original line 1177: sample_derivative.
    public static Offsetf SampleDerivative(Superellipse shape,
        float angle,
        float sweep_angle,
        bool at_start)
    {


            float sign = sweep_angle >= 0f ? 1f : -1f;
            float h = CppMath.Max(VgScalar.Abs(sweep_angle) * 0.25f, 1.0e-5f);
            Offsetf point = shape.PointAtAngle(angle);
            if (at_start)
            {
                Offsetf next = shape.PointAtAngle(angle + sign * h);
                return (next - point) / (sign * h);
            }

            Offsetf previous = shape.PointAtAngle(angle - sign * h);
            return (point - previous) / (sign * h);
        }
    // Original line 1199: make_adaptive_segment.
    public static CubicBezier MakeAdaptiveSegment(Superellipse shape,
        float start_angle,
        float sweep_angle)
    {
            float end_angle = start_angle + sweep_angle;
            Offsetf start = shape.PointAtAngle(start_angle);
            Offsetf end = shape.PointAtAngle(end_angle);
            if (VgScalar.NearlyEqual(shape.Exponent, 1f))
            {
                return CubicBezier.Line(start, end);
            }

            Offsetf start_derivative = SampleDerivative(shape, start_angle, sweep_angle, true);
            Offsetf end_derivative = SampleDerivative(shape, end_angle, sweep_angle, false);
            return CubicBezier.CubicTo(
                start,
                start + start_derivative * (sweep_angle / 3f),
                end - end_derivative * (sweep_angle / 3f),
                end
            );
        }
    // Original line 1223: fast_quadrant_segment_count.
    public static uint FastQuadrantSegmentCount(float Exponent)
    {


            if (VgScalar.NearlyEqual(Exponent, 1f) ||
                Superellipse.IsEllipseExponent(Exponent))
            {
                return 1u;
            }

            float safe_exponent = CppMath.Max(Exponent, 1.0e-6f);
            float stress = VgScalar.Abs(VgScalar.Log(safe_exponent));
            return stress < 0.75f ? 2u : 3u;
        }
    // Original line 1239: split_fast_span.
    public static void SplitFastSpan(Superellipse shape,
        float start_angle,
        float sweep_angle,
        uint segment_count,
        Action<CubicBezier> func)
    {


            if (segment_count == 0u)
            {
                return;
            }

            float segment_sweep = sweep_angle / (float)(segment_count);
            for (uint i = 0u; i < segment_count; ++i)
            {
                float segment_start = start_angle + segment_sweep * (float)(i);
                float segment_end = segment_start + segment_sweep;
                Offsetf end = shape.PointAtAngle(segment_end);
                CubicBezier cubic = Superellipse.IsEllipseExponent(shape.Exponent) ?
                    MakeSegment(shape, segment_start, segment_sweep, end) :
                    MakeAdaptiveSegment(shape, segment_start, segment_sweep);
                func(cubic);
            }
        }
    // Original line 1268: split_fast.
    public static void SplitFast(Superellipse shape,
        EShapeSampleDirection direction,
        Action<CubicBezier> func)
    {


            uint quadrant_segment_count = FastQuadrantSegmentCount(shape.Exponent);
            float quadrant_sweep = direction == EShapeSampleDirection.Reverse ?
                -(MathF.PI * .5f) :
                (MathF.PI * .5f);
            for (uint i = 0u; i < 4u; ++i)
            {
                SplitFastSpan(
                    shape,
                    quadrant_sweep * (float)(i),
                    quadrant_sweep,
                    quadrant_segment_count,
                    func
                );
            }
        }
    // Original line 1292: count_fast.
    public static uint CountFast(Superellipse shape)
    {
            return FastQuadrantSegmentCount(shape.Exponent) * 4u;
        }
    // Original line 1297: adaptive_segment_error.
    public static float AdaptiveSegmentError(Superellipse shape,
        CubicBezier cubic,
        float start_angle,
        float sweep_angle,
        float tolerance)
    {


            float[] samples = [ 0.125f, 0.25f, 0.5f, 0.75f, 0.875f ];
            float max_error = 0f;
            foreach (float u in samples)
            {
                Offsetf shape_point = shape.PointAtAngle(start_angle + sweep_angle * u);
                if (VgScalar.NearlyEqual(shape.Exponent, 1f))
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
    // Original line 1327: distance_to_segment.
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
    // Original line 1349: split_adaptive_span.
    public static void SplitAdaptiveSpan(Superellipse shape,
        float start_angle,
        float sweep_angle,
        float tolerance,
        uint max_depth,
        uint depth,
        Action<CubicBezier> func)
    {
            CubicBezier cubic = MakeAdaptiveSegment(shape, start_angle, sweep_angle);
            if (depth >= max_depth ||
                AdaptiveSegmentError(shape, cubic, start_angle, sweep_angle, tolerance) <= tolerance)
            {
                func(cubic);
                return;
            }

            float half_sweep = sweep_angle * 0.5f;
            SplitAdaptiveSpan(
                shape,
                start_angle,
                half_sweep,
                tolerance,
                max_depth,
                depth + 1u,
                func
            );
            SplitAdaptiveSpan(
                shape,
                start_angle + half_sweep,
                half_sweep,
                tolerance,
                max_depth,
                depth + 1u,
                func
            );
        }
    // Original line 1389: split_adaptive.
    public static void SplitAdaptive(Superellipse shape,
        float tolerance,
        uint max_depth,
        EShapeSampleDirection direction,
        Action<CubicBezier> func)
    {


            float sweep = direction == EShapeSampleDirection.Reverse ?
                -(MathF.PI * .5f) :
                (MathF.PI * .5f);
            for (uint i = 0u; i < 4u; ++i)
            {
                SplitAdaptiveSpan(
                    shape,
                    sweep * (float)(i),
                    sweep,
                    tolerance,
                    ShapeSampleHelper.ResolveMaxDepth(max_depth),
                    0u,
                    func
                );
            }
        }
    // Original line 1416: count_adaptive.
    public static uint CountAdaptive(Superellipse shape,
        float tolerance,
        uint max_depth)
    {
            uint count = 0u;
            SplitAdaptive(
                shape,
                tolerance,
                max_depth,
                EShapeSampleDirection.Forward,
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
public struct Superellipse : IEquatable<Superellipse>
{
    public Offsetf Center;
    public float RadiusX;
    public float RadiusY;
    public float Rotation;
    public float Exponent;
    public Superellipse() { Exponent = 2; }
    public Superellipse(Offsetf center, float radius_x, float radius_y, float rotation, float exponent) { Center = center; RadiusX = radius_x; RadiusY = radius_y; Rotation = rotation; Exponent = exponent; }
    // Original line 1480: Zero.
    public static Superellipse Zero()
    {
        return new Superellipse();
    }
    // Original line 1484: Invalid.
    public static Superellipse Invalid()
    {
        float kNaN = float.NaN;
        return new Superellipse(new Offsetf(kNaN, kNaN), kNaN, kNaN, kNaN, kNaN);
    }
    // Original line 1489: CenterRadius.
    public static Superellipse CenterRadius(Offsetf Center,
    float RadiusX,
    float RadiusY,
    float Rotation = 0,
    float Exponent = 2)
    {
        return new Superellipse(Center, RadiusX, RadiusY, Rotation, Exponent);
    }
    // Original line 1501: is_empty.
    public bool IsEmpty()
    {
        return RadiusX <= 0f || RadiusY <= 0f;
    }
    // Original line 1505: is_valid.
    public bool IsValid()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(Exponent) && RadiusX >= 0f &&
            RadiusY >= 0f && Exponent > 0f;
    }
    // Original line 1512: is_normalized.
    public bool IsNormalized()
    {
        return Center.IsFinite() && VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) && VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(Exponent) && RadiusX >= 0f &&
            RadiusY >= 0f && Exponent > 0f &&
            Rotation == WrapAngle(Rotation);
    }
    // Original line 1520: is_finite.
    public bool IsFinite()
    {
        return Center.IsFinite() &&
            VgScalar.IsFinite(RadiusX) &&
            VgScalar.IsFinite(RadiusY) &&
            VgScalar.IsFinite(Rotation) &&
            VgScalar.IsFinite(Exponent);
    }
    // Original line 1528: has_nan.
    public bool HasNan()
    {
        return VgScalar.IsNaN(Center.X) || VgScalar.IsNaN(Center.Y) ||
            VgScalar.IsNaN(RadiusX) || VgScalar.IsNaN(RadiusY) ||
            VgScalar.IsNaN(Rotation) || VgScalar.IsNaN(Exponent);
    }
    // Original line 1534: is_point.
    public bool IsPoint()
    {
        return RadiusX == 0f && RadiusY == 0f;
    }
    // Original line 1540: length.
    public float Length(float tolerance = .01f)
    {
        Superellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty() || value.IsPoint())
        {
            return 0f;
        }

        float tol = PositiveTolerance(tolerance);
        float result = 0f;
        for (uint i = 0u; i < 4u; ++i)
        {
            float start_t = (float)(i) * 0.25f;
            float end_t = (float)(i + 1u) * 0.25f;
            result += ApproximateLengthSpan(
                value,
                start_t,
                end_t,
                value.PointAtT(start_t),
                value.PointAtT(end_t),
                tol,
                0u
            );
        }
        return result;
    }
    // Original line 1566: rect.
    public Rectf Rect()
    {
        Superellipse value = Normalized();
        if (!value.IsFinite() || value.IsEmpty())
        {
            return Rectf.Points(value.Center, value.Center);
        }

        if (IsEllipseExponent(value.Exponent))
        {
            float cos_rotation = VgScalar.Cos(value.Rotation);
            float sin_rotation = VgScalar.Sin(value.Rotation);
            float extent_x = VgScalar.Sqrt(
                value.RadiusX * value.RadiusX * cos_rotation * cos_rotation +
                value.RadiusY * value.RadiusY * sin_rotation * sin_rotation
            );
            float extent_y = VgScalar.Sqrt(
                value.RadiusX * value.RadiusX * sin_rotation * sin_rotation +
                value.RadiusY * value.RadiusY * cos_rotation * cos_rotation
            );
            return Rectf.Center(value.Center, new Sizef(extent_x * 2f, extent_y * 2f));
        }

        if (value.Rotation == 0f)
        {
            return Rectf.Center(
                value.Center,
                new Sizef(value.RadiusX * 2f, value.RadiusY * 2f)
            );
        }

        uint count = CalcSampleBoundCount((MathF.PI * 2));
        Rectf Bounds = Rectf.Points(value.PointAtAngle(0f), value.PointAtAngle(0f));
        for (uint i = 1u; i < count; ++i)
        {
            float angle =
                (MathF.PI * 2) * (float)(i) / (float)(count);
            Bounds = Bounds.Hold(value.PointAtAngle(angle));
        }
        return Bounds;
    }
    // Original line 1607: bounds.
    public Rectf Bounds()
    {
        return Rect();
    }
    // Original line 1611: contains.
    public bool Contains(Offsetf point)
    {
        Superellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return false;
        }

        Offsetf local_point = value.ToLocal(point);
        float x = VgScalar.Abs(local_point.X / value.RadiusX);
        float y = VgScalar.Abs(local_point.Y / value.RadiusY);
        return VgScalar.Pow(x, value.Exponent) + VgScalar.Pow(y, value.Exponent) <= 1f;
    }
    // Original line 1624: create_sampler.
    public SuperellipseSampler CreateSampler(float angle = 0)
    {
        return new SuperellipseSampler(angle, Rotation, Exponent);
    }
    // Original line 1628: point_at_angle.
    public Offsetf PointAtAngle(float angle)
    {
        SuperellipseSampler sampler = CreateSampler(angle);
        return sampler.SamplePoint(Center, RadiusX, RadiusY);
    }
    // Original line 1633: point_at_ratio.
    public Offsetf PointAtRatio(float ratio)
    {
        return PointAtAngle(ratio * (MathF.PI * 2));
    }
    // Original line 1637: point_at_t.
    public Offsetf PointAtT(float t)
    {
        return PointAtRatio(t);
    }
    // Original line 1643: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return ShapeSampleHelper.CalcGeometricTolerance(
            tessellation_factor,
            pixel_ratio
        );
    }
    // Original line 1653: estimate_segment_count.
    public uint EstimateSegmentCount(float tolerance)
    {

        Superellipse value = Normalized();
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f || !value.IsValid() || value.IsEmpty())
        {
            return 0u;
        }


        if (VgScalar.Abs(value.Exponent - 1.0f) <= 1.0e-6f)
        {
            return 4u;
        }

        if (IsEllipseExponent(value.Exponent))
        {
            return Ellipse.CenterRadius(
                       value.Center,
                       value.RadiusX,
                       value.RadiusY,
                       value.Rotation
            )
                .EstimateSegmentCount(tolerance);
        }

        return SuperellipseEstimateHelper.EstimateSuperellipse(
            value.RadiusX,
            value.RadiusY,
            value.Exponent,
            tolerance
        );
    }
    // Original line 1687: sample.
    public void Sample(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Superellipse value = Normalized();
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
    // Original line 1706: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Superellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampleHelper.SampleClosedQuadrants(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            value,
            functor
        );
    }
    // Original line 1726: sample.
    public void Sample(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float> functor)
    {
        Superellipse value = Normalized();
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
    // Original line 1747: sample_with_sampler.
    public void SampleWithSampler(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf,float,SuperellipseSampler> functor)
    {
        Superellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleClosedUniformWithSampler(
            desc.SegmentCount,
            desc.Direction,
            ref sampler,
            (ref SuperellipseSampler sampler, float t) => {
                sampler.SetAngle(t * (MathF.PI * 2));
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 1771: sample_with_sampler.
    public void SampleWithSampler(ShapeToleranceSampleDesc desc,
    Action<Offsetf,float,SuperellipseSampler> functor)
    {
        Superellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampler emit_sampler = value.CreateSampler();
        SuperellipseSampleHelper.SampleClosedQuadrants(
            desc.Tolerance,
            desc.MaxDepth,
            desc.Direction,
            value,
            (Offsetf ignored, float t) => {
                emit_sampler.SetAngle(t * (MathF.PI * 2));
                Offsetf point = emit_sampler.SamplePoint(
                    value.Center,
                    value.RadiusX,
                    value.RadiusY
                );
                functor(point, t, emit_sampler);
            }
        );
    }
    // Original line 1800: sample_with_sampler.
    public void SampleWithSampler(ShapeStepLengthSampleDesc desc,
    Action<Offsetf,float,SuperellipseSampler> functor)
    {
        Superellipse value = Normalized();
        if (!value.IsValid() || value.IsEmpty())
        {
            return;
        }

        SuperellipseSampler sampler = value.CreateSampler();
        ShapeSampleHelper.SampleClosedByStepLengthWithSampler(
            desc.StepLength,
            ShapeSampleHelper.EstimateStepLengthTolerance(desc.StepLength),
            desc.MaxDepth,
            desc.Direction,
            ref sampler,
            (ref SuperellipseSampler sampler, float t) => {
                sampler.SetAngle(t * (MathF.PI * 2));
                return sampler.SamplePoint(value.Center, value.RadiusX, value.RadiusY);
            },
            functor
        );
    }
    // Original line 1827: cubic_bezier_count.
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

        Superellipse value = Normalized();
        if (!Superellipse.IsEllipseExponent(value.Exponent))
        {
            return SuperellipseCubicBezierHelper.CountAdaptive(value, tolerance, max_depth);
        }

        return SuperellipseCubicBezierHelper.CountTolerance(
            value.RadiusX,
            value.RadiusY,
            value.Exponent,
            (MathF.PI * 2),
            tolerance,
            true
        );
    }
    // Original line 1855: cubic_bezier_count_fast.
    public uint CubicBezierCountFast()
    {
        if (!IsValid() || IsEmpty())
        {
            return 0u;
        }

        return SuperellipseCubicBezierHelper.CountFast(Normalized());
    }
    // Original line 1867: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance, Action<CubicBezier> func, uint max_depth = 10)
    {
        SplitToCubicBeziers(
            tolerance,
            EShapeSampleDirection.Forward,
            func,
            max_depth
        );
    }
    // Original line 1877: split_to_cubic_beziers.
    public void SplitToCubicBeziers(float tolerance,
    EShapeSampleDirection direction,
    Action<CubicBezier> func,
    uint max_depth = 10)
    {
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
        {
            Debug.Assert(false , "cubic bezier tolerance must be positive and finite");
            return;
        }

        if (!IsValid() || IsEmpty())
        {
            return;
        }

        Superellipse value = Normalized();
        if (!Superellipse.IsEllipseExponent(value.Exponent))
        {
            SuperellipseCubicBezierHelper.SplitAdaptive(
                value,
                tolerance,
                max_depth,
                direction,
                func
            );
            return;
        }

        uint count = value.CubicBezierCount(tolerance, max_depth);
        if (count == 0u)
        {
            return;
        }

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
            CubicBezier cubic = SuperellipseCubicBezierHelper.MakeSegment(
                value,
                segment_start,
                segment_sweep,
                end
            );
            func(cubic);
        }
    }
    // Original line 1934: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(Action<CubicBezier> func)
    {
        SplitToCubicBeziersFast(
            EShapeSampleDirection.Forward,
            func
        );
    }
    // Original line 1942: split_to_cubic_beziers_fast.
    public void SplitToCubicBeziersFast(EShapeSampleDirection direction,
    Action<CubicBezier> func)
    {
        if (!IsValid() || IsEmpty())
        {
            return;
        }

        SuperellipseCubicBezierHelper.SplitFast(
            Normalized(),
            direction,
            func
        );
    }
    // Original line 1960: normalized.
    public Superellipse Normalized()
    {
        return new Superellipse(Center,
            VgScalar.Abs(RadiusX),
            VgScalar.Abs(RadiusY),
            WrapAngle(Rotation),
            Exponent);
    }
    // Original line 1970: shift.
    public Superellipse Shift(Offsetf offset)
    {
        return new Superellipse(Center + offset, RadiusX, RadiusY, Rotation, Exponent);
    }
    // Original line 1974: inflate.
    public Superellipse Inflate(float delta)
    {
        return new Superellipse(Center,
            CppMath.Max(0f, RadiusX + delta),
            CppMath.Max(0f, RadiusY + delta),
            Rotation,
            Exponent);
    }
    // Original line 1984: deflate.
    public Superellipse Deflate(float delta)
    {
        return Inflate(-delta);
    }
    // Original line 1988: reversed.
    public Superellipse Reversed()
    {
        return this;
    }
    // Original line 2006: _wrap_angle.
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
    // Original line 2024: _positive_tolerance.
    internal static float PositiveTolerance(float tolerance)
    {
        if (!VgScalar.IsFinite(tolerance) || tolerance <= 0f)
        {
            return 0.01f;
        }
        return tolerance;
    }
    // Original line 2032: _signed_pow.
    internal static float SignedPow(float value, float power)
    {
        float magnitude = VgScalar.Pow(VgScalar.Abs(value), power);
        return value < 0f ? -magnitude : magnitude;
    }
    // Original line 2037: _is_ellipse_exponent.
    internal static bool IsEllipseExponent(float Exponent)
    {
        return VgScalar.NearlyEqual(Exponent, 2f);
    }
    // Original line 2041: _calc_sample_bound_count.
    internal static uint CalcSampleBoundCount(float sweep_angle)
    {
        float count = VgScalar.Ceiling(VgScalar.Abs(sweep_angle) / (MathF.PI * 2) * 256f);
        return CppMath.Clamp((uint)(CppMath.Max(8f, count)), 8u, 512u);
    }
    // Original line 2046: _approximate_length_span.
    internal static float ApproximateLengthSpan(Superellipse shape,
    float start_t,
    float end_t,
    Offsetf start_point,
    Offsetf end_point,
    float tolerance,
    uint depth)
    {
        float mid_t = (start_t + end_t) * 0.5f;
        Offsetf mid_point = shape.PointAtT(mid_t);
        float polyline_length =
            (mid_point - start_point).Length() + (end_point - mid_point).Length();
        float chord_length = (end_point - start_point).Length();

        if (depth >= 16u || polyline_length - chord_length <= tolerance)
        {
            return (polyline_length + chord_length) * 0.5f;
        }

        return ApproximateLengthSpan(
                   shape,
                   start_t,
                   mid_t,
                   start_point,
                   mid_point,
                   tolerance,
                   depth + 1u
               ) +
            ApproximateLengthSpan(
                   shape,
                   mid_t,
                   end_t,
                   mid_point,
                   end_point,
                   tolerance,
                   depth + 1u
            );
    }
    // Original line 2086: _to_local.
    internal Offsetf ToLocal(Offsetf point)
    {
        Offsetf delta = point - Center;
        float cos_rotation = VgScalar.Cos(Rotation);
        float sin_rotation = VgScalar.Sin(Rotation);
        return new Offsetf(delta.X * cos_rotation + delta.Y * sin_rotation,
            -delta.X * sin_rotation + delta.Y * cos_rotation);
    }
    public readonly bool Equals(Superellipse other) => Center == other.Center && RadiusX == other.RadiusX && RadiusY == other.RadiusY && Rotation == other.Rotation && Exponent == other.Exponent;
    public override readonly bool Equals(object? obj) => obj is Superellipse other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Center, RadiusX, RadiusY, Rotation, Exponent);
    public static bool operator ==(Superellipse lhs, Superellipse rhs) => lhs.Equals(rhs);
    public static bool operator !=(Superellipse lhs, Superellipse rhs) => !lhs.Equals(rhs);
}
