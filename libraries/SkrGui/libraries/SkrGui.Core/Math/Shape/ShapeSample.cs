// Source: SkrGuiCore/math/shape/shape_sample.hpp @ 611561f8.
// Syntax-translated with tools/port_shape_sampling.py; source loops and recursion are retained.
namespace SkrGui;
public delegate Offsetf ShapeSampleEvaluator<TSampler>(ref TSampler sampler,float t);

public enum EShapeSampleDirection : byte { Forward, Reverse }
public struct ShapeSegmentCountSampleDesc { public uint SegmentCount; public EShapeSampleDirection Direction; public ShapeSegmentCountSampleDesc(uint count, EShapeSampleDirection direction = EShapeSampleDirection.Forward) { SegmentCount = count; Direction = direction; } }
public struct ShapeStepLengthSampleDesc { public float StepLength = 1; public EShapeSampleDirection Direction; public uint MaxDepth = 10; public ShapeStepLengthSampleDesc() { } public ShapeStepLengthSampleDesc(float step, EShapeSampleDirection direction = EShapeSampleDirection.Forward, uint maxDepth = 10) { StepLength = step; Direction = direction; MaxDepth = maxDepth; } }
public struct ShapeToleranceSampleDesc { public float Tolerance = .25f; public EShapeSampleDirection Direction; public uint MaxDepth = 10; public ShapeToleranceSampleDesc() { } public ShapeToleranceSampleDesc(float tolerance, EShapeSampleDirection direction = EShapeSampleDirection.Forward, uint maxDepth = 10) { Tolerance = tolerance; Direction = direction; MaxDepth = maxDepth; } }
public static class ShapeSampleHelper
{
    private const uint _k_default_max_depth = 10;
    private const float _k_default_geometric_tolerance = .6f, _k_default_bezier_tolerance = .6f, _k_circle_tolerance_scale = .6f;
    // Original line 336: sample_closed_uniform.
    public static void SampleClosedUniform(uint segment_count,
    EShapeSampleDirection direction,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {


        if (segment_count == 0u)
        {
            return;
        }

        if (IsForward(direction))
        {
            for (uint i = 0; i < segment_count; ++i)
            {
                float t =
                    (float)(i) /
                    (float)(segment_count);
                functor(eval(t), t);
            }
        }
        else
        {
            for (uint i = segment_count; i > 0u; --i)
            {
                uint sample_index = i - 1u;
                float t =
                    (float)(sample_index) /
                    (float)(segment_count);
                functor(eval(t), t);
            }
        }
    }
    // Original line 374: sample_open_uniform.
    public static void SampleOpenUniform(uint segment_count,
    EShapeSampleDirection direction,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {


        if (segment_count == 0u)
        {
            return;
        }

        if (IsForward(direction))
        {
            for (uint i = 0; i <= segment_count; ++i)
            {
                float t =
                    (float)(i) /
                    (float)(segment_count);
                functor(eval(t), t);
            }
        }
        else
        {
            for (ulong i = (ulong)(segment_count) + 1u; i > 0u; --i)
            {
                uint sample_index = (uint)(i - 1u);
                float t =
                    (float)(sample_index) /
                    (float)(segment_count);
                functor(eval(t), t);
            }
        }
    }
    // Original line 412: sample_closed_by_step_length.
    public static void SampleClosedByStepLength(float step_length,
    float build_tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {


        float resolved_step_length = ResolveStepLength(step_length);
        float tolerance = ResolveTolerance(build_tolerance);
        uint resolved_max_depth = ResolveMaxDepth(max_depth);
        float tolerance_sq = tolerance * tolerance;
        Offsetf start_point = eval(0f);
        Offsetf end_point = eval(1f);
        float total_length = ApproximateSpanLength(
            0f,
            1f,
            start_point,
            end_point,
            tolerance_sq,
            resolved_max_depth,
            0u,
            eval
        );
        uint segment_count = CalcStepLengthSegmentCount(
            total_length,
            resolved_step_length,
            true
        );
        if (segment_count == 0u)
        {
            return;
        }

        if (!(total_length > 0f))
        {
            SampleClosedUniform(
                segment_count,
                direction,
                eval,
                functor
            );
            return;
        }

        if (IsForward(direction))
        {
            for (uint i = 0; i < segment_count; ++i)
            {
                float ratio =
                    (float)(i) /
                    (float)(segment_count);
                float target_length = total_length * ratio;
                float t = i == 0u ?
                    0f :
                    SolveTAtLength(
                        0f,
                        1f,
                        start_point,
                        end_point,
                        tolerance_sq,
                        resolved_max_depth,
                        0u,
                        target_length,
                        eval
                    );
                Offsetf point = i == 0u ? start_point : eval(t);
                functor(point, t);
            }
        }
        else
        {
            for (uint i = segment_count; i > 0u; --i)
            {
                uint sample_index = i - 1u;
                float ratio =
                    (float)(sample_index) /
                    (float)(segment_count);
                float target_length = total_length * ratio;
                float t = sample_index == 0u ?
                    0f :
                    SolveTAtLength(
                        0f,
                        1f,
                        start_point,
                        end_point,
                        tolerance_sq,
                        resolved_max_depth,
                        0u,
                        target_length,
                        eval
                    );
                Offsetf point = sample_index == 0u ? start_point : eval(t);
                functor(point, t);
            }
        }
    }
    // Original line 514: sample_open_by_step_length.
    public static void SampleOpenByStepLength(float step_length,
    float build_tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {


        float resolved_step_length = ResolveStepLength(step_length);
        float tolerance = ResolveTolerance(build_tolerance);
        uint resolved_max_depth = ResolveMaxDepth(max_depth);
        float tolerance_sq = tolerance * tolerance;
        Offsetf start_point = eval(0f);
        Offsetf end_point = eval(1f);
        float total_length = ApproximateSpanLength(
            0f,
            1f,
            start_point,
            end_point,
            tolerance_sq,
            resolved_max_depth,
            0u,
            eval
        );
        uint segment_count = CalcStepLengthSegmentCount(
            total_length,
            resolved_step_length,
            false
        );
        if (segment_count == 0u)
        {
            return;
        }

        if (!(total_length > 0f))
        {
            SampleOpenUniform(
                segment_count,
                direction,
                eval,
                functor
            );
            return;
        }

        if (IsForward(direction))
        {
            for (uint i = 0; i <= segment_count; ++i)
            {
                float ratio =
                    (float)(i) /
                    (float)(segment_count);
                float target_length = total_length * ratio;
                float t =
                    i == 0u ?
                    0f :
                    (i == segment_count ?
                         1f :
                         SolveTAtLength(
                             0f,
                             1f,
                             start_point,
                             end_point,
                             tolerance_sq,
                             resolved_max_depth,
                             0u,
                             target_length,
                             eval
                         ));
                Offsetf point =
                    i == 0u ? start_point : (i == segment_count ? end_point : eval(t));
                functor(point, t);
            }
        }
        else
        {
            for (ulong i = (ulong)(segment_count) + 1u; i > 0u; --i)
            {
                uint sample_index = (uint)(i - 1u);
                float ratio =
                    (float)(sample_index) /
                    (float)(segment_count);
                float target_length = total_length * ratio;
                float t =
                    sample_index == 0u ?
                    0f :
                    (sample_index == segment_count ?
                         1f :
                         SolveTAtLength(
                             0f,
                             1f,
                             start_point,
                             end_point,
                             tolerance_sq,
                             resolved_max_depth,
                             0u,
                             target_length,
                             eval
                         ));
                Offsetf point = sample_index == 0u ?
                    start_point :
                    (sample_index == segment_count ? end_point : eval(t));
                functor(point, t);
            }
        }
    }
    // Original line 625: sample_closed_adaptive.
    public static void SampleClosedAdaptive(float tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {


        float tol = ResolveTolerance(tolerance);
        uint resolved_max_depth = ResolveMaxDepth(max_depth);
        float tolerance_sq = tol * tol;
        Offsetf start_point = eval(0f);
        Offsetf end_point = eval(1f);

        if (IsForward(direction))
        {
            functor(start_point, 0f);
            SampleClosedAdaptiveForward(
                0f,
                1f,
                start_point,
                end_point,
                tolerance_sq,
                resolved_max_depth,
                0u,
                eval,
                functor
            );
        }
        else
        {
            SampleClosedAdaptiveReverse(
                0f,
                1f,
                start_point,
                end_point,
                tolerance_sq,
                resolved_max_depth,
                0u,
                eval,
                functor
            );
            functor(start_point, 0f);
        }
    }
    // Original line 674: sample_open_adaptive.
    public static void SampleOpenAdaptive(float tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {


        float tol = ResolveTolerance(tolerance);
        uint resolved_max_depth = ResolveMaxDepth(max_depth);
        float tolerance_sq = tol * tol;
        Offsetf start_point = eval(0f);
        Offsetf end_point = eval(1f);

        if (IsForward(direction))
        {
            functor(start_point, 0f);
            SampleOpenAdaptiveForward(
                0f,
                1f,
                start_point,
                end_point,
                tolerance_sq,
                resolved_max_depth,
                0u,
                eval,
                functor
            );
        }
        else
        {
            functor(end_point, 1f);
            SampleOpenAdaptiveReverse(
                0f,
                1f,
                start_point,
                end_point,
                tolerance_sq,
                resolved_max_depth,
                0u,
                eval,
                functor
            );
        }
    }
    // Original line 724: sample_closed_uniform_with_sampler.
    public static void SampleClosedUniformWithSampler<TSampler>(uint segment_count,
    EShapeSampleDirection direction,
    ref TSampler sampler,
    ShapeSampleEvaluator<TSampler> eval,
    Action<Offsetf, float, TSampler> functor)
    {
     TSampler sampler_state=sampler;
     try {


        if (segment_count == 0u)
        {
            return;
        }

        if (IsForward(direction))
        {
            for (uint i = 0; i < segment_count; ++i)
            {
                float t =
                    (float)(i) /
                    (float)(segment_count);
                Offsetf point = eval(ref sampler_state, t);
                functor(point, t, sampler_state);
            }
        }
        else
        {
            for (uint i = segment_count; i > 0u; --i)
            {
                uint sample_index = i - 1u;
                float t =
                    (float)(sample_index) /
                    (float)(segment_count);
                Offsetf point = eval(ref sampler_state, t);
                functor(point, t, sampler_state);
            }
        }
    }
     finally {sampler=sampler_state;}
    }
    // Original line 765: sample_open_uniform_with_sampler.
    public static void SampleOpenUniformWithSampler<TSampler>(uint segment_count,
    EShapeSampleDirection direction,
    ref TSampler sampler,
    ShapeSampleEvaluator<TSampler> eval,
    Action<Offsetf, float, TSampler> functor)
    {
     TSampler sampler_state=sampler;
     try {


        if (segment_count == 0u)
        {
            return;
        }

        if (IsForward(direction))
        {
            for (uint i = 0; i <= segment_count; ++i)
            {
                float t =
                    (float)(i) /
                    (float)(segment_count);
                Offsetf point = eval(ref sampler_state, t);
                functor(point, t, sampler_state);
            }
        }
        else
        {
            for (ulong i = (ulong)(segment_count) + 1u; i > 0u; --i)
            {
                uint sample_index = (uint)(i - 1u);
                float t =
                    (float)(sample_index) /
                    (float)(segment_count);
                Offsetf point = eval(ref sampler_state, t);
                functor(point, t, sampler_state);
            }
        }
    }
     finally {sampler=sampler_state;}
    }
    // Original line 806: sample_closed_by_step_length_with_sampler.
    public static void SampleClosedByStepLengthWithSampler<TSampler>(float step_length,
    float build_tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    ref TSampler sampler,
    ShapeSampleEvaluator<TSampler> eval,
    Action<Offsetf, float, TSampler> functor)
    {
     TSampler sampler_state=sampler;
     try {


        Func<float, Offsetf> direct_eval = (float t) => {
            return eval(ref sampler_state, t);
        };

        Action<Offsetf, float> direct_functor = (Offsetf ignored, float t) => {
            Offsetf point = eval(ref sampler_state, t);
            functor(point, t, sampler_state);
        };

        SampleClosedByStepLength(
            step_length,
            build_tolerance,
            max_depth,
            direction,
            direct_eval,
            direct_functor
        );
    }
     finally {sampler=sampler_state;}
    }
    // Original line 838: sample_open_by_step_length_with_sampler.
    public static void SampleOpenByStepLengthWithSampler<TSampler>(float step_length,
    float build_tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    ref TSampler sampler,
    ShapeSampleEvaluator<TSampler> eval,
    Action<Offsetf, float, TSampler> functor)
    {
     TSampler sampler_state=sampler;
     try {


        Func<float, Offsetf> direct_eval = (float t) => {
            return eval(ref sampler_state, t);
        };

        Action<Offsetf, float> direct_functor = (Offsetf ignored, float t) => {
            Offsetf point = eval(ref sampler_state, t);
            functor(point, t, sampler_state);
        };

        SampleOpenByStepLength(
            step_length,
            build_tolerance,
            max_depth,
            direction,
            direct_eval,
            direct_functor
        );
    }
     finally {sampler=sampler_state;}
    }
    // Original line 870: sample_closed_adaptive_with_sampler.
    public static void SampleClosedAdaptiveWithSampler<TSampler>(float tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    ref TSampler sampler,
    ShapeSampleEvaluator<TSampler> eval,
    Action<Offsetf, float, TSampler> functor)
    {
     TSampler sampler_state=sampler;
     try {


        Func<float, Offsetf> direct_eval = (float t) => {
            return eval(ref sampler_state, t);
        };

        Action<Offsetf, float> direct_functor = (Offsetf ignored, float t) => {
            Offsetf point = eval(ref sampler_state, t);
            functor(point, t, sampler_state);
        };

        SampleClosedAdaptive(
            tolerance,
            max_depth,
            direction,
            direct_eval,
            direct_functor
        );
    }
     finally {sampler=sampler_state;}
    }
    // Original line 900: sample_open_adaptive_with_sampler.
    public static void SampleOpenAdaptiveWithSampler<TSampler>(float tolerance,
    uint max_depth,
    EShapeSampleDirection direction,
    ref TSampler sampler,
    ShapeSampleEvaluator<TSampler> eval,
    Action<Offsetf, float, TSampler> functor)
    {
     TSampler sampler_state=sampler;
     try {


        Func<float, Offsetf> direct_eval = (float t) => {
            return eval(ref sampler_state, t);
        };

        Action<Offsetf, float> direct_functor = (Offsetf ignored, float t) => {
            Offsetf point = eval(ref sampler_state, t);
            functor(point, t, sampler_state);
        };

        SampleOpenAdaptive(
            tolerance,
            max_depth,
            direction,
            direct_eval,
            direct_functor
        );
    }
     finally {sampler=sampler_state;}
    }
    // Original line 930: resolve_tolerance.
    public static float ResolveTolerance(float tolerance)
    {
        if (!float.IsFinite(tolerance) || tolerance <= 0f)
        {
            return 0.25f;
        }
        return tolerance;
    }
    // Original line 939: resolve_step_length.
    public static float ResolveStepLength(float step_length)
    {
        if (!float.IsFinite(step_length) || step_length <= 0f)
        {
            return 1f;
        }
        return step_length;
    }
    // Original line 948: resolve_max_depth.
    public static uint ResolveMaxDepth(uint max_depth)
    {
        return max_depth == 0u ? _k_default_max_depth : max_depth;
    }
    // Original line 953: calc_geometric_tolerance.
    public static float CalcGeometricTolerance(float tessellation_factor,
    float pixel_ratio)
    {
        return _k_default_geometric_tolerance /
            ResolveVisualToleranceScale(
                   tessellation_factor,
                   pixel_ratio
            );
    }
    // Original line 965: calc_bezier_tolerance.
    public static float CalcBezierTolerance(float tessellation_factor,
    float pixel_ratio)
    {
        return _k_default_bezier_tolerance /
            ResolveVisualToleranceScale(
                   tessellation_factor,
                   pixel_ratio
            );
    }
    // Original line 977: estimate_resample_tolerance.
    public static float EstimateResampleTolerance(Rectf bounds,
    uint segment_count,
    bool closed)
    {
        if (segment_count == 0u)
        {
            return 0.25f;
        }

        float perimeter = CppMath.Max(
            0f,
            (bounds.Width() + bounds.Height()) * 2f
        );
        float divisor = (float)(segment_count);
        float scale = closed ? divisor * 16f : divisor * 12f;
        return ResolveTolerance(perimeter / CppMath.Max(1f, scale));
    }
    // Original line 997: estimate_step_length_tolerance.
    public static float EstimateStepLengthTolerance(float step_length)
    {
        return ResolveTolerance(ResolveStepLength(step_length) * 0.25f);
    }
    // Original line 1002: calc_circular_segment_count.
    public static uint CalcCircularSegmentCount(float radius,
    float sweep_angle,
    float tolerance,
    bool closed)
    {
        if (!float.IsFinite(radius) || radius <= 0f ||
            !float.IsFinite(sweep_angle))
        {
            return 0u;
        }

        float tol = ResolveTolerance(tolerance);
        float sweep = MathF.Abs(sweep_angle);
        if (!(sweep > 0f))
        {
            return 0u;
        }

        float clamped = CppMath.Clamp(1f - tol / radius, -1f, 1f);
        float theta = 2f * MathF.Acos(clamped);
        if (!float.IsFinite(theta) || theta <= 0f)
        {
            theta = sweep;
        }

        float segment_count = MathF.Ceiling(sweep / theta);
        if (segment_count < 1f)
        {
            segment_count = 1f;
        }
        if (closed && segment_count < 3f)
        {
            segment_count = 3f;
        }

        float max_segment_count =
            (float)(uint.MaxValue - 1u);
        if (segment_count > max_segment_count)
        {
            segment_count = max_segment_count;
        }
        return (uint)(segment_count);
    }
    // Original line 1048: calc_step_length_segment_count.
    public static uint CalcStepLengthSegmentCount(float total_length,
    float step_length,
    bool closed)
    {
        if (!float.IsFinite(total_length) || total_length <= 0f)
        {
            return 0u;
        }

        float resolved_step_length = ResolveStepLength(step_length);
        float segment_count = MathF.Ceiling(total_length / resolved_step_length);
        if (segment_count < 1f)
        {
            segment_count = 1f;
        }
        if (closed && segment_count < 3f)
        {
            segment_count = 3f;
        }

        float max_segment_count =
            (float)(uint.MaxValue - 1u);
        if (segment_count > max_segment_count)
        {
            segment_count = max_segment_count;
        }
        return (uint)(segment_count);
    }
    // Original line 1103: _sample_open_adaptive_forward.
    public static void SampleOpenAdaptiveForward(float start_t,
    float end_t,
    Offsetf start_point,
    Offsetf end_point,
    float tolerance_sq,
    uint max_depth,
    uint depth,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {
        float mid_t = (start_t + end_t) * 0.5f;
        Offsetf mid_point = eval(mid_t);

        if (
            depth >= max_depth ||
            MidpointErrorSq(start_point, mid_point, end_point) <= tolerance_sq
        )
        {
            functor(end_point, end_t);
            return;
        }

        SampleOpenAdaptiveForward(
            start_t,
            mid_t,
            start_point,
            mid_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
        SampleOpenAdaptiveForward(
            mid_t,
            end_t,
            mid_point,
            end_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
    }
    // Original line 1152: _sample_open_adaptive_reverse.
    public static void SampleOpenAdaptiveReverse(float start_t,
    float end_t,
    Offsetf start_point,
    Offsetf end_point,
    float tolerance_sq,
    uint max_depth,
    uint depth,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {
        float mid_t = (start_t + end_t) * 0.5f;
        Offsetf mid_point = eval(mid_t);

        if (
            depth >= max_depth ||
            MidpointErrorSq(start_point, mid_point, end_point) <= tolerance_sq
        )
        {
            functor(start_point, start_t);
            return;
        }

        SampleOpenAdaptiveReverse(
            mid_t,
            end_t,
            mid_point,
            end_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
        SampleOpenAdaptiveReverse(
            start_t,
            mid_t,
            start_point,
            mid_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
    }
    // Original line 1201: _sample_closed_adaptive_forward.
    public static void SampleClosedAdaptiveForward(float start_t,
    float end_t,
    Offsetf start_point,
    Offsetf end_point,
    float tolerance_sq,
    uint max_depth,
    uint depth,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {
        float mid_t = (start_t + end_t) * 0.5f;
        Offsetf mid_point = eval(mid_t);

        if (
            depth >= max_depth ||
            MidpointErrorSq(start_point, mid_point, end_point) <= tolerance_sq
        )
        {
            if (end_t < 1f)
            {
                functor(end_point, end_t);
            }
            return;
        }

        SampleClosedAdaptiveForward(
            start_t,
            mid_t,
            start_point,
            mid_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
        SampleClosedAdaptiveForward(
            mid_t,
            end_t,
            mid_point,
            end_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
    }
    // Original line 1253: _sample_closed_adaptive_reverse.
    public static void SampleClosedAdaptiveReverse(float start_t,
    float end_t,
    Offsetf start_point,
    Offsetf end_point,
    float tolerance_sq,
    uint max_depth,
    uint depth,
    Func<float, Offsetf> eval,
    Action<Offsetf, float> functor)
    {
        float mid_t = (start_t + end_t) * 0.5f;
        Offsetf mid_point = eval(mid_t);

        if (
            depth >= max_depth ||
            MidpointErrorSq(start_point, mid_point, end_point) <= tolerance_sq
        )
        {
            if (start_t > 0f)
            {
                functor(start_point, start_t);
            }
            return;
        }

        SampleClosedAdaptiveReverse(
            mid_t,
            end_t,
            mid_point,
            end_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
        SampleClosedAdaptiveReverse(
            start_t,
            mid_t,
            start_point,
            mid_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval,
            functor
        );
    }
    // Original line 1305: _approximate_span_length.
    public static float ApproximateSpanLength(float start_t,
    float end_t,
    Offsetf start_point,
    Offsetf end_point,
    float tolerance_sq,
    uint max_depth,
    uint depth,
    Func<float, Offsetf> eval)
    {
        float mid_t = (start_t + end_t) * 0.5f;
        Offsetf mid_point = eval(mid_t);

        if (
            depth >= max_depth ||
            MidpointErrorSq(start_point, mid_point, end_point) <= tolerance_sq
        )
        {
            return ApproximateLeafLength(start_point, mid_point, end_point);
        }

        return ApproximateSpanLength(
                   start_t,
                   mid_t,
                   start_point,
                   mid_point,
                   tolerance_sq,
                   max_depth,
                   depth + 1u,
                   eval
               ) +
            ApproximateSpanLength(
                   mid_t,
                   end_t,
                   mid_point,
                   end_point,
                   tolerance_sq,
                   max_depth,
                   depth + 1u,
                   eval
            );
    }
    // Original line 1350: _solve_t_at_length.
    public static float SolveTAtLength(float start_t,
    float end_t,
    Offsetf start_point,
    Offsetf end_point,
    float tolerance_sq,
    uint max_depth,
    uint depth,
    float target_length,
    Func<float, Offsetf> eval)
    {
        if (!(target_length > 0f))
        {
            return start_t;
        }

        float mid_t = (start_t + end_t) * 0.5f;
        Offsetf mid_point = eval(mid_t);
        float leaf_length =
            ApproximateLeafLength(start_point, mid_point, end_point);

        if (
            depth >= max_depth ||
            MidpointErrorSq(start_point, mid_point, end_point) <= tolerance_sq
        )
        {
            if (!(leaf_length > 0f))
            {
                return end_t;
            }

            float ratio = CppMath.Clamp(target_length / leaf_length, 0f, 1f);
            return start_t + (end_t - start_t) * ratio;
        }

        float left_length = ApproximateSpanLength(
            start_t,
            mid_t,
            start_point,
            mid_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval
        );

        if (target_length <= left_length)
        {
            return SolveTAtLength(
                start_t,
                mid_t,
                start_point,
                mid_point,
                tolerance_sq,
                max_depth,
                depth + 1u,
                target_length,
                eval
            );
        }

        float right_length = ApproximateSpanLength(
            mid_t,
            end_t,
            mid_point,
            end_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            eval
        );
        if (!(right_length > 0f))
        {
            return end_t;
        }

        return SolveTAtLength(
            mid_t,
            end_t,
            mid_point,
            end_point,
            tolerance_sq,
            max_depth,
            depth + 1u,
            CppMath.Min(target_length - left_length, right_length),
            eval
        );
    }
    // Original line 1440: _approximate_leaf_length.
    public static float ApproximateLeafLength(Offsetf start_point,
    Offsetf mid_point,
    Offsetf end_point)
    {
        float polyline_length =
            (mid_point - start_point).Length() +
            (end_point - mid_point).Length();
        float chord_length = (end_point - start_point).Length();
        return (polyline_length + chord_length) * 0.5f;
    }
    // Original line 1453: _is_forward.
    public static bool IsForward(EShapeSampleDirection direction)
    {
        return direction == EShapeSampleDirection.Forward;
    }
    // Original line 1460: _midpoint_error_sq.
    public static float MidpointErrorSq(Offsetf start_point,
    Offsetf mid_point,
    Offsetf end_point)
    {
        Offsetf chord_mid = Offsetf.Lerp(start_point, end_point, 0.5f);
        return (mid_point - chord_mid).LengthSquared();
    }
    // Original line 1470: _resolve_visual_tolerance_scale.
    public static float ResolveVisualToleranceScale(float tessellation_factor,
    float pixel_ratio)
    {
        if (!float.IsFinite(tessellation_factor) || tessellation_factor <= 0f ||
            !float.IsFinite(pixel_ratio) || pixel_ratio <= 0f)
        {
            return 1f;
        }

        float scale = tessellation_factor * pixel_ratio;
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            return 1f;
        }
        return scale;
    }
}
