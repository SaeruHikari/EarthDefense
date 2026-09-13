// Source: SkrGuiCore/math/shape/quad_bezier.hpp @ 611561f8.
// Full algorithm bodies translated by tools/port_beziers.py.
namespace SkrGui;
public struct QuadBezier : IEquatable<QuadBezier>
{
    public Offsetf Start, Control, End;
    private const uint KMaxDepth = 16;
    public struct ProjectResult { public Offsetf Point; public float T, Distance; public bool Valid; public readonly bool IsValid() => Valid; }
    public QuadBezier(Offsetf start, Offsetf control, Offsetf end) { Start = start; Control = control; End = end; }
    // Original line 180: Zero.
    public static QuadBezier Zero()
    {
        return new QuadBezier(Offsetf.Zero(), Offsetf.Zero(), Offsetf.Zero());
    }
    // Original line 184: Invalid.
    public static QuadBezier Invalid()
    {
        float kNaN = float.NaN;
        return new QuadBezier(new Offsetf(kNaN, kNaN),
            new Offsetf(kNaN, kNaN),
            new Offsetf(kNaN, kNaN));
    }
    // Original line 193: Point.
    public static QuadBezier Point(Offsetf point)
    {
        return new QuadBezier(point, point, point);
    }
    // Original line 197: Line.
    public static QuadBezier Line(Offsetf Start, Offsetf End)
    {
        return new QuadBezier(Start, Offsetf.Lerp(Start, End, 0.5f), End);
    }
    // Original line 201: QuadTo.
    public static QuadBezier QuadTo(Offsetf from,
    Offsetf Control,
    Offsetf to)
    {
        return new QuadBezier(from, Control, to);
    }
    // Original line 211: is_valid.
    public bool IsValid()
    {
        return IsFinite();
    }
    // Original line 215: is_finite.
    public bool IsFinite()
    {
        return Start.IsFinite() && Control.IsFinite() && End.IsFinite();
    }
    // Original line 219: has_nan.
    public bool HasNan()
    {
        return float.IsNaN(Start.X) || float.IsNaN(Start.Y) ||
            float.IsNaN(Control.X) || float.IsNaN(Control.Y) ||
            float.IsNaN(End.X) || float.IsNaN(End.Y);
    }
    // Original line 225: is_point.
    public bool IsPoint()
    {
        return Start == Control && Start == End;
    }
    // Original line 231: start_point.
    public Offsetf StartPoint()
    {
        return Start;
    }
    // Original line 235: control_point.
    public Offsetf ControlPoint()
    {
        return Control;
    }
    // Original line 239: end_point.
    public Offsetf EndPoint()
    {
        return End;
    }
    // Original line 243: bounding_rect.
    public Rectf BoundingRect()
    {
        Rectf bounds = Rectf.Points(Start, End);

        float t = 0.0f;
        if (SolveAxisExtremum(Start.X, Control.X, End.X, ref t))
        {
            bounds = bounds.Hold(PointAt(t));
        }
        if (SolveAxisExtremum(Start.Y, Control.Y, End.Y, ref t))
        {
            bounds = bounds.Hold(PointAt(t));
        }
        return bounds;
    }
    // Original line 260: point_at.
    public Offsetf PointAt(float t)
    {
        Offsetf p01 = Offsetf.Lerp(Start, Control, t);
        Offsetf p12 = Offsetf.Lerp(Control, End, t);
        return Offsetf.Lerp(p01, p12, t);
    }
    // Original line 266: derivative_at.
    public Offsetf DerivativeAt(float t)
    {
        return Offsetf.Lerp(Control - Start, End - Control, t) * 2.0f;
    }
    // Original line 270: tangent_at.
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
    // Original line 280: length.
    public float Length(float tolerance = .01f)
    {
        if (!IsFinite() || IsPoint())
        {
            return 0f;
        }

        return ApproximateLength(this, PositiveTolerance(tolerance), 0u);
    }
    // Original line 291: calc_tolerance.
    public static float CalcTolerance(float tessellation_factor = 1, float pixel_ratio = 1)
    {
        return ShapeSampleHelper.CalcBezierTolerance(
            tessellation_factor,
            pixel_ratio
        );
    }
    // Original line 303: sample.
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
    // Original line 321: sample.
    public void Sample(ShapeToleranceSampleDesc desc,
    Action<Offsetf, float> functor)
    {




        if (!IsValid() || IsPoint())
        {
            return;
        }

        float tolerance = PositiveTolerance(desc.Tolerance);
        uint max_depth = ShapeSampleHelper.ResolveMaxDepth(desc.MaxDepth);
        void RecurseForward(QuadBezier curve, float start_t, float end_t, uint depth) {
            if (depth >= max_depth || IsFlatEnough(curve, tolerance))
            {
                functor(curve.End, end_t);
                return;
            }

            QuadBezier left = QuadBezier.Zero();
            QuadBezier right = QuadBezier.Zero();
            curve.Split(0.5f, ref left, ref right);

            float mid_t = (start_t + end_t) * 0.5f;
            RecurseForward( left, start_t, mid_t, depth + 1u);
            RecurseForward( right, mid_t, end_t, depth + 1u);
        }
        void RecurseReverse(QuadBezier curve, float start_t, float end_t, uint depth) {
            if (depth >= max_depth || IsFlatEnough(curve, tolerance))
            {
                functor(curve.Start, start_t);
                return;
            }

            QuadBezier left = QuadBezier.Zero();
            QuadBezier right = QuadBezier.Zero();
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
    // Original line 394: sample.
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
    // Original line 415: project.
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
    // Original line 454: closest_point.
    public Offsetf ClosestPoint(Offsetf point,
    float tolerance = .01f)
    {
        return Project(point, tolerance).Point;
    }
    // Original line 461: distance.
    public float Distance(Offsetf point, float tolerance = .01f)
    {
        return Project(point, tolerance).Distance;
    }
    // Original line 467: split.
    public void Split(float t, ref QuadBezier left, ref QuadBezier right)
    {
        Offsetf p01 = Offsetf.Lerp(Start, Control, t);
        Offsetf p12 = Offsetf.Lerp(Control, End, t);
        Offsetf mid = Offsetf.Lerp(p01, p12, t);

        left = new QuadBezier( Start, p01, mid );
        right = new QuadBezier( mid, p12, End );
    }
    // Original line 476: subcurve.
    public QuadBezier Subcurve(float start_t, float end_t)
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

        QuadBezier left = QuadBezier.Zero();
        QuadBezier right = QuadBezier.Zero();
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

        QuadBezier discard = QuadBezier.Zero();
        QuadBezier middle = QuadBezier.Zero();
        left.Split(clamped_start / clamped_end, ref discard, ref middle);
        return middle;
    }
    // Original line 523: reversed.
    public QuadBezier Reversed()
    {
        return new QuadBezier(End, Control, Start);
    }
    // Original line 539: _positive_tolerance.
    private static float PositiveTolerance(float tolerance)
    {


        if (!float.IsFinite(tolerance) || tolerance <= 0f)
        {
            return 0.01f;
        }
        return tolerance;
    }
    // Original line 549: _clamp_unit.
    private static float ClampUnit(float t)
    {
        if (!float.IsFinite(t))
        {
            return 0f;
        }
        return CppMath.Clamp(t, 0f, 1f);
    }
    // Original line 557: _is_flat_enough.
    private static bool IsFlatEnough(QuadBezier curve,
    float tolerance)
    {


        return DistanceToLineSq(curve.Control, curve.Start, curve.End) <=
            tolerance * tolerance;
    }
    // Original line 567: _distance_to_line_sq.
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
    // Original line 586: _approximate_length.
    private static float ApproximateLength(QuadBezier curve,
    float tolerance,
    uint depth)
    {
        float control_polygon_length =
            (curve.Control - curve.Start).Length() +
            (curve.End - curve.Control).Length();
        float chord_length = (curve.End - curve.Start).Length();
        if (depth >= KMaxDepth ||
            control_polygon_length - chord_length <= tolerance)
        {
            return (control_polygon_length + chord_length) * 0.5f;
        }

        QuadBezier left = QuadBezier.Zero();
        QuadBezier right = QuadBezier.Zero();
        curve.Split(0.5f, ref left, ref right);
        return ApproximateLength(left, tolerance, depth + 1u) +
            ApproximateLength(right, tolerance, depth + 1u);
    }
    // Original line 608: _closest_point_on_segment.
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
    // Original line 632: _accumulate_closest_point.
    private static void AccumulateClosestPoint(QuadBezier curve,
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

        QuadBezier left = QuadBezier.Zero();
        QuadBezier right = QuadBezier.Zero();
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
    // Original line 685: _solve_axis_extremum.
    private static bool SolveAxisExtremum(float p0, float p1, float p2, ref float t)
    {
        float denom = p0 - 2.0f * p1 + p2;
        if (denom == 0.0f)
        {
            return false;
        }

        t = (p0 - p1) / denom;
        return t > 0.0f && t < 1.0f;
    }
    public readonly bool Equals(QuadBezier other) => Start == other.Start && Control == other.Control && End == other.End;
    public override readonly bool Equals(object? obj) => obj is QuadBezier other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Start, Control, End);
    public static bool operator ==(QuadBezier lhs, QuadBezier rhs) => lhs.Equals(rhs);
    public static bool operator !=(QuadBezier lhs, QuadBezier rhs) => !lhs.Equals(rhs);
}
