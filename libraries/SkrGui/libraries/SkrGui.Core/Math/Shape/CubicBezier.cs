// Source: SkrGuiCore/math/shape/cubic_bezier.hpp @ 611561f8.
// Full algorithm bodies translated by tools/port_beziers.py.
namespace SkrGui;
public struct CubicBezier : IEquatable<CubicBezier>
{
    public Offsetf Start, Control1, Control2, End;
    private const uint KMaxDepth = 16;
    public struct ProjectResult { public Offsetf Point; public float T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }
    public CubicBezier(Offsetf start, Offsetf control_1, Offsetf control_2, Offsetf end) { Start = start; Control1 = control_1; Control2 = control_2; End = end; }
    // Original line 203: Zero.
    public static CubicBezier Zero()
    {
        return new CubicBezier(Offsetf.Zero(), Offsetf.Zero(), Offsetf.Zero(), Offsetf.Zero());
    }
    // Original line 207: Invalid.
    public static CubicBezier Invalid()
    {
        float kNaN = float.NaN;
        return new CubicBezier(new Offsetf(kNaN, kNaN),
            new Offsetf(kNaN, kNaN),
            new Offsetf(kNaN, kNaN),
            new Offsetf(kNaN, kNaN));
    }
    // Original line 217: Point.
    public static CubicBezier Point(Offsetf point)
    {
        return new CubicBezier(point, point, point, point);
    }
    // Original line 221: Line.
    public static CubicBezier Line(Offsetf Start, Offsetf End)
    {
        return new CubicBezier(Start,
            Offsetf.Lerp(Start, End, 1.0f / 3.0f),
            Offsetf.Lerp(Start, End, 2.0f / 3.0f),
            End);
    }
    // Original line 230: CubicTo.
    public static CubicBezier CubicTo(Offsetf from,
    Offsetf Control1,
    Offsetf Control2,
    Offsetf to)
    {
        return new CubicBezier(from, Control1, Control2, to);
    }
    // Original line 241: is_valid.
    public bool IsValid()
    {
        return IsFinite();
    }
    // Original line 245: is_finite.
    public bool IsFinite()
    {
        return Start.IsFinite() && Control1.IsFinite() && Control2.IsFinite() && End.IsFinite();
    }
    // Original line 249: has_nan.
    public bool HasNan()
    {
        return float.IsNaN(Start.X) || float.IsNaN(Start.Y) ||
            float.IsNaN(Control1.X) || float.IsNaN(Control1.Y) ||
            float.IsNaN(Control2.X) || float.IsNaN(Control2.Y) ||
            float.IsNaN(End.X) || float.IsNaN(End.Y);
    }
    // Original line 256: is_point.
    public bool IsPoint()
    {
        return Start == Control1 && Start == Control2 && Start == End;
    }
    // Original line 262: start_point.
    public Offsetf StartPoint()
    {
        return Start;
    }
    // Original line 266: control_point_1.
    public Offsetf ControlPoint1()
    {
        return Control1;
    }
    // Original line 270: control_point_2.
    public Offsetf ControlPoint2()
    {
        return Control2;
    }
    // Original line 274: end_point.
    public Offsetf EndPoint()
    {
        return End;
    }
    // Original line 278: bounding_rect.
    public Rectf BoundingRect()
    {
        Rectf bounds = Rectf.Points(Start, End);

        float t0 = 0.0f;
        float t1 = 0.0f;
        uint x_root_count = SolveAxisExtrema(Start.X, Control1.X, Control2.X, End.X, ref t0, ref t1);
        if (x_root_count >= 1u)
        {
            if (t0 > 0.0f && t0 < 1.0f)
            {
                bounds = bounds.Hold(PointAt(t0));
            }
            if (x_root_count >= 2u && t1 > 0.0f && t1 < 1.0f)
            {
                bounds = bounds.Hold(PointAt(t1));
            }
        }

        uint y_root_count = SolveAxisExtrema(Start.Y, Control1.Y, Control2.Y, End.Y, ref t0, ref t1);
        if (y_root_count >= 1u)
        {
            if (t0 > 0.0f && t0 < 1.0f)
            {
                bounds = bounds.Hold(PointAt(t0));
            }
            if (y_root_count >= 2u && t1 > 0.0f && t1 < 1.0f)
            {
                bounds = bounds.Hold(PointAt(t1));
            }
        }
        return bounds;
    }
    // Original line 313: point_at.
    public Offsetf PointAt(float t)
    {
        Offsetf p01 = Offsetf.Lerp(Start, Control1, t);
        Offsetf p12 = Offsetf.Lerp(Control1, Control2, t);
        Offsetf p23 = Offsetf.Lerp(Control2, End, t);
        Offsetf p0112 = Offsetf.Lerp(p01, p12, t);
        Offsetf p1223 = Offsetf.Lerp(p12, p23, t);
        return Offsetf.Lerp(p0112, p1223, t);
    }
    // Original line 322: derivative_at.
    public Offsetf DerivativeAt(float t)
    {
        Offsetf d01 = Control1 - Start;
        Offsetf d12 = Control2 - Control1;
        Offsetf d23 = End - Control2;
        Offsetf q0 = Offsetf.Lerp(d01, d12, t);
        Offsetf q1 = Offsetf.Lerp(d12, d23, t);
        return Offsetf.Lerp(q0, q1, t) * 3.0f;
    }
    // Original line 331: tangent_at.
    public Offsetf TangentAt(float t)
    {
        Offsetf derivative = DerivativeAt(t);
        float length_sq = derivative.LengthSquared();
        if (length_sq <= 0.0f)
        {
            return Offsetf.Zero();
        }
        return derivative / MathF.Sqrt(length_sq);
    }
    // Original line 341: length.
    public float Length(float tolerance = .01f)
    {
        if (!IsFinite() || IsPoint())
        {
            return 0f;
        }

        return ApproximateLength(this, PositiveTolerance(tolerance), 0u);
    }
    // Original line 352: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return ShapeSampleHelper.CalcBezierTolerance(
            tessellation_factor,
            pixel_ratio
        );
    }
    // Original line 364: sample.
    public void Sample(ShapeSegmentCountSampleDesc desc,
    Action<Offsetf, float> functor)
    {
        var value = this;
        if (!IsValid() || IsPoint())
        {
            return;
        }

        ShapeSampleHelper.SampleOpenUniform(
            desc.SegmentCount,
            desc.Direction,
            (float t) => value.PointAt(t),
            functor
        );
    }
    // Original line 382: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf, float> functor)
    {




        if (!IsValid() || IsPoint())
        {
            return;
        }

        float tolerance = PositiveTolerance(desc.Tolerance);
        uint max_depth = ShapeSampleHelper.ResolveMaxDepth(desc.MaxDepth);
        void RecurseForward(CubicBezier curve, float start_t, float end_t, uint depth) {
            if (depth >= max_depth || IsFlatEnough(curve, tolerance))
            {
                functor(curve.End, end_t);
                return;
            }

            CubicBezier left = CubicBezier.Zero();
            CubicBezier right = CubicBezier.Zero();
            curve.Split(0.5f, ref left, ref right);

            float mid_t = (start_t + end_t) * 0.5f;
            RecurseForward( left, start_t, mid_t, depth + 1u);
            RecurseForward( right, mid_t, end_t, depth + 1u);
        }
        void RecurseReverse(CubicBezier curve, float start_t, float end_t, uint depth) {
            if (depth >= max_depth || IsFlatEnough(curve, tolerance))
            {
                functor(curve.Start, start_t);
                return;
            }

            CubicBezier left = CubicBezier.Zero();
            CubicBezier right = CubicBezier.Zero();
            curve.Split(0.5f, ref left, ref right);

            float mid_t = (start_t + end_t) * 0.5f;
            RecurseReverse( right, mid_t, end_t, depth + 1u);
            RecurseReverse( left, start_t, mid_t, depth + 1u);
        }

        if (desc.Direction == EShapeSampleDirection.Forward)
        {
            functor(Start, 0f);
            RecurseForward( this, 0f, 1f, 0u);
        }
        else
        {
            functor(End, 1f);
            RecurseReverse( this, 0f, 1f, 0u);
        }
    }
    // Original line 455: sample.
    public void Sample(ShapeStepLengthSampleDesc desc,
    Action<Offsetf, float> functor)
    {
        var value = this;
        if (!IsValid() || IsPoint())
        {
            return;
        }

        ShapeSampleHelper.SampleOpenByStepLength(
            desc.StepLength,
            ShapeSampleHelper.EstimateStepLengthTolerance(desc.StepLength),
            desc.MaxDepth,
            desc.Direction,
            (float t) => value.PointAt(t),
            functor
        );
    }
    // Original line 476: project.
    public ProjectResult Project(Offsetf point,
    float tolerance = .01f)
    {
        ProjectResult result = new();
        if (!IsFinite())
        {
            return result;
        }
        if (IsPoint())
        {
            result.Point = Start;
            result.T = 0f;
            result.Distance = (point - Start).Length();
            result.Valid = true;
            return result;
        }

        float best_distance_sq = float.PositiveInfinity;
        float best_t = 0f;
        Offsetf best_point = Start;
        AccumulateClosestPoint(
            this,
            point,
            PositiveTolerance(tolerance),
            0u,
            0f,
            1f,
            ref best_distance_sq,
            ref best_t,
            ref best_point
        );
        result.Point = best_point;
        result.T = best_t;
        result.Distance = MathF.Sqrt(best_distance_sq);
        result.Valid = true;
        return result;
    }
    // Original line 515: closest_point.
    public Offsetf ClosestPoint(Offsetf point,
    float tolerance = .01f)
    {
        return Project(point, tolerance).Point;
    }
    // Original line 522: distance.
    public float Distance(Offsetf point, float tolerance = .01f)
    {
        return Project(point, tolerance).Distance;
    }
    // Original line 528: split.
    public void Split(float t, ref CubicBezier left, ref CubicBezier right)
    {
        Offsetf p01 = Offsetf.Lerp(Start, Control1, t);
        Offsetf p12 = Offsetf.Lerp(Control1, Control2, t);
        Offsetf p23 = Offsetf.Lerp(Control2, End, t);
        Offsetf p0112 = Offsetf.Lerp(p01, p12, t);
        Offsetf p1223 = Offsetf.Lerp(p12, p23, t);
        Offsetf mid = Offsetf.Lerp(p0112, p1223, t);

        left = new CubicBezier( Start, p01, p0112, mid );
        right = new CubicBezier( mid, p1223, p23, End );
    }
    // Original line 540: subcurve.
    public CubicBezier Subcurve(float start_t, float end_t)
    {
        float clamped_start = ClampUnit(start_t);
        float clamped_end = ClampUnit(end_t);
        if (clamped_start > clamped_end)
        {
            return Subcurve(clamped_end, clamped_start).Reversed();
        }

        if (clamped_start == clamped_end)
        {
            return Point(PointAt(clamped_start));
        }
        if (clamped_start <= 0f && clamped_end >= 1f)
        {
            return this;
        }
        if (clamped_end <= 0f)
        {
            return Point(Start);
        }
        if (clamped_start >= 1f)
        {
            return Point(End);
        }

        CubicBezier left = CubicBezier.Zero();
        CubicBezier right = CubicBezier.Zero();
        if (clamped_end < 1f)
        {
            Split(clamped_end, ref left, ref right);
        }
        else
        {
            left = this;
        }

        if (clamped_start <= 0f)
        {
            return left;
        }

        CubicBezier discard = CubicBezier.Zero();
        CubicBezier middle = CubicBezier.Zero();
        left.Split(clamped_start / clamped_end, ref discard, ref middle);
        return middle;
    }
    // Original line 587: reversed.
    public CubicBezier Reversed()
    {
        return new CubicBezier(End, Control2, Control1, Start);
    }
    // Original line 603: _positive_tolerance.
    private static float PositiveTolerance(float tolerance)
    {


        if (!float.IsFinite(tolerance) || tolerance <= 0f)
        {
            return 0.01f;
        }
        return tolerance;
    }
    // Original line 613: _clamp_unit.
    private static float ClampUnit(float t)
    {
        if (!float.IsFinite(t))
        {
            return 0f;
        }
        return CppMath.Clamp(t, 0f, 1f);
    }
    // Original line 621: _is_flat_enough.
    private static bool IsFlatEnough(CubicBezier curve,
    float tolerance)
    {


        float tolerance_sq = tolerance * tolerance;
        return DistanceToLineSq(curve.Control1, curve.Start, curve.End) <= tolerance_sq &&
            DistanceToLineSq(curve.Control2, curve.Start, curve.End) <= tolerance_sq;
    }
    // Original line 632: _distance_to_line_sq.
    private static float DistanceToLineSq(Offsetf point,
    Offsetf line_start,
    Offsetf line_end)
    {
        Offsetf line = line_end - line_start;
        float length_sq = line.LengthSquared();
        if (length_sq <= 1.0e-12f)
        {
            return (point - line_start).LengthSquared();
        }

        float area_twice = MathF.Abs(
            (point.X - line_start.X) * line.Y -
            (point.Y - line_start.Y) * line.X
        );
        return (area_twice * area_twice) / length_sq;
    }
    // Original line 651: _approximate_length.
    private static float ApproximateLength(CubicBezier curve,
    float tolerance,
    uint depth)
    {
        float control_polygon_length =
            (curve.Control1 - curve.Start).Length() +
            (curve.Control2 - curve.Control1).Length() +
            (curve.End - curve.Control2).Length();
        float chord_length = (curve.End - curve.Start).Length();
        if (depth >= KMaxDepth ||
            control_polygon_length - chord_length <= tolerance)
        {
            return (control_polygon_length + chord_length) * 0.5f;
        }

        CubicBezier left = CubicBezier.Zero();
        CubicBezier right = CubicBezier.Zero();
        curve.Split(0.5f, ref left, ref right);
        return ApproximateLength(left, tolerance, depth + 1u) +
            ApproximateLength(right, tolerance, depth + 1u);
    }
    // Original line 674: _closest_point_on_segment.
    private static Offsetf ClosestPointOnSegment(Offsetf point,
    Offsetf line_start,
    Offsetf line_end,
    ref float segment_t)
    {
        Offsetf direction = line_end - line_start;
        float length_sq = direction.LengthSquared();
        if (length_sq <= 1.0e-12f)
        {
            segment_t = 0f;
            return line_start;
        }

        segment_t = CppMath.Clamp(
            ((point - line_start).X * direction.X +
             (point - line_start).Y * direction.Y) /
                length_sq,
            0f,
            1f
        );
        return Offsetf.Lerp(line_start, line_end, segment_t);
    }
    // Original line 698: _accumulate_closest_point.
    private static void AccumulateClosestPoint(CubicBezier curve,
    Offsetf point,
    float tolerance,
    uint depth,
    float start_t,
    float end_t,
    ref float best_distance_sq,
    ref float best_t,
    ref Offsetf best_point)
    {
        if (depth >= KMaxDepth || IsFlatEnough(curve, tolerance))
        {
            float segment_t = 0f;
            Offsetf candidate =
                ClosestPointOnSegment(point, curve.Start, curve.End, ref segment_t);
            float distance_sq = (candidate - point).LengthSquared();
            if (distance_sq < best_distance_sq)
            {
                best_distance_sq = distance_sq;
                best_t = (start_t + (end_t - start_t) * segment_t);
                best_point = candidate;
            }
            return;
        }

        CubicBezier left = CubicBezier.Zero();
        CubicBezier right = CubicBezier.Zero();
        curve.Split(0.5f, ref left, ref right);
        AccumulateClosestPoint(
            left,
            point,
            tolerance,
            depth + 1u,
            start_t,
            (start_t + end_t) * 0.5f,
            ref best_distance_sq,
            ref best_t,
            ref best_point
        );
        AccumulateClosestPoint(
            right,
            point,
            tolerance,
            depth + 1u,
            (start_t + end_t) * 0.5f,
            end_t,
            ref best_distance_sq,
            ref best_t,
            ref best_point
        );
    }
    // Original line 751: _solve_axis_extrema.
    private static uint SolveAxisExtrema(float p0,
    float p1,
    float p2,
    float p3,
    ref float t0,
    ref float t1)
    {
        float a = -p0 + 3.0f * p1 - 3.0f * p2 + p3;
        float b = 2.0f * (p0 - 2.0f * p1 + p2);
        float c = p1 - p0;
        return SolveQuadratic(a, b, c, ref t0, ref t1);
    }
    // Original line 766: _solve_quadratic.
    private static uint SolveQuadratic(float a, float b, float c, ref float t0, ref float t1)
    {
        if (a == 0.0f)
        {
            if (b == 0.0f)
            {
                return 0u;
            }
            t0 = -c / b;
            return 1u;
        }

        float discriminant = b * b - 4.0f * a * c;
        if (discriminant < 0.0f)
        {
            return 0u;
        }

        if (discriminant == 0.0f)
        {
            t0 = -0.5f * b / a;
            return 1u;
        }

        float sqrt_discriminant = MathF.Sqrt(discriminant);
        float q = -0.5f * (b + MathF.CopySign(sqrt_discriminant, b));
        if (q == 0.0f)
        {
            t0 = -b / (2.0f * a);
            t1 = t0;
            return 1u;
        }

        t0 = q / a;
        t1 = c / q;
        if (t0 > t1)
        {
            (t0, t1) = (t1, t0);
        }
        return 2u;
    }
    public readonly bool Equals(CubicBezier other) => Start == other.Start && Control1 == other.Control1 && Control2 == other.Control2 && End == other.End;
    public override readonly bool Equals(object? obj) => obj is CubicBezier other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Start, Control1, Control2, End);
    public static bool operator ==(CubicBezier lhs, CubicBezier rhs) => lhs.Equals(rhs);
    public static bool operator !=(CubicBezier lhs, CubicBezier rhs) => !lhs.Equals(rhs);
}
