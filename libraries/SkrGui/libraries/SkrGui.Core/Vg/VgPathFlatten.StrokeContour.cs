// Source: src/vg/vg_path_flatten.stroke_contour.cpp @ 611561f8.
using System.Diagnostics;
namespace SkrGui;
internal enum EStrokeContourSide : byte { Left, Right }
internal sealed class StrokeContourBuilder(VGPathFlatten source,VGPathFlatten output,VGStrokeOptions options,float bodyRadius,float roundTolerance,uint roundDivisions)
{
    public readonly VGPathFlatten Source=source,Output=output; public readonly VGStrokeOptions Options=options; public float BodyRadius=bodyRadius,RoundTolerance=roundTolerance; public uint RoundDivisions=roundDivisions;
    // Original line 84: calc_round_divisions.
    public static uint CalcRoundDivisions(float radius,
    float sweep_angle,
    float tolerance)
    {
        uint divisions = Arc.CenterRadius(
                                       Offsetf.Zero(),
                                       radius,
                                       0.0f,
                                       sweep_angle
        )
                                       .EstimateSegmentCount(tolerance);
        return divisions == 0u ? 1u : divisions;
    }
    // Original line 100: direction_from_angle.
    public static Offsetf DirectionFromAngle(float angle)
    {
        return new Offsetf(MathF.Cos(angle), MathF.Sin(angle));
    }
    // Original line 105: next_node.
    public static uint NextNode(VGPathFlattenContour contour,
    uint node_local_index)
    {
        uint next = node_local_index + 1u;
        if (next < contour.NodeCount)
        {
            return contour.NodeBegin + next;
        }
        return contour.NodeBegin;
    }
    // Original line 118: previous_node.
    public static uint PreviousNode(VGPathFlattenContour contour,
    uint node_local_index)
    {
        if (node_local_index > 0u)
        {
            return contour.NodeBegin + node_local_index - 1u;
        }
        return contour.NodeBegin + contour.NodeCount - 1u;
    }
    // Original line 130: side_point.
    public Offsetf SidePoint(Offsetf position,
    Offsetf direction,
    EStrokeContourSide side)
    {
        Offsetf normal = direction.CcwNormal();
        return side == EStrokeContourSide.Left ?
            position + normal * BodyRadius :
            position - normal * BodyRadius;
    }
    // Original line 142: miter_point.
    public Offsetf MiterPoint(VGPathFlattenNode node,
    Offsetf fallback_direction,
    EStrokeContourSide side)
    {
        Offsetf join_direction = node.JoinDirection;
        if (!join_direction.IsFinite() || join_direction.LengthSquared() <= 0.000001f)
        {
            join_direction = fallback_direction.CcwNormal();
        }

        return side == EStrokeContourSide.Left ?
            node.Position + join_direction * BodyRadius :
            node.Position - join_direction * BodyRadius;
    }
    // Original line 159: open_endpoint.
    public Offsetf OpenEndpoint(Offsetf position,
    Offsetf direction,
    EStrokeContourSide side,
    bool is_start)
    {
        float cap_extend = Options.Cap == EVGStrokeCap.Square ? BodyRadius : 0.0f;
        Offsetf center = is_start ?
            position - direction * cap_extend :
            position + direction * cap_extend;
        return SidePoint(center, direction, side);
    }
    // Original line 173: emit_arc.
    public void EmitArc(Offsetf center,
    float start_angle,
    float end_angle,
    bool reverse)
    {
        float sweep = end_angle - start_angle;
        uint divisions = CalcRoundDivisions(BodyRadius, sweep, RoundTolerance);
        if (divisions > RoundDivisions)
        {
            divisions = RoundDivisions;
        }

        for (uint step = 0u; step <= divisions; ++step)
        {
            uint index = reverse ? divisions - step : step;
            float ratio = (float)(index) / (float)(divisions);
            float angle = start_angle + sweep * ratio;
            Output.AddNode(
                center + DirectionFromAngle(angle) * BodyRadius,
                EVGPathFlattenNodeFlags.Corner
            );
        }
    }
    // Original line 199: emit_side_bevel.
    public void EmitSideBevel(VGPathFlattenNode node,
    Offsetf previous_direction,
    Offsetf next_direction,
    EStrokeContourSide side,
    bool reverse)
    {
        if (reverse)
        {
            Output.AddNode(SidePoint(node.Position, next_direction, side), EVGPathFlattenNodeFlags.Corner);
            Output.AddNode(SidePoint(node.Position, previous_direction, side), EVGPathFlattenNodeFlags.Corner);
            return;
        }

        Output.AddNode(SidePoint(node.Position, previous_direction, side), EVGPathFlattenNodeFlags.Corner);
        Output.AddNode(SidePoint(node.Position, next_direction, side), EVGPathFlattenNodeFlags.Corner);
    }
    // Original line 218: emit_side_round.
    public void EmitSideRound(VGPathFlattenNode node,
    Offsetf previous_direction,
    Offsetf next_direction,
    EStrokeContourSide side,
    bool reverse)
    {
        float start_angle = 0.0f;
        float end_angle = 0.0f;
        if (side == EStrokeContourSide.Left)
        {
            start_angle = previous_direction.CcwNormal().Radians();
            end_angle = next_direction.CcwNormal().Radians();
            while (end_angle < start_angle)
            {
                end_angle += (MathF.PI*2);
            }
        }
        else
        {
            start_angle = (-previous_direction.CcwNormal()).Radians();
            end_angle = (-next_direction.CcwNormal()).Radians();
            while (end_angle > start_angle)
            {
                end_angle -= (MathF.PI*2);
            }
        }

        EmitArc(node.Position, start_angle, end_angle, reverse);
    }
    // Original line 250: emit_side_miter.
    public void EmitSideMiter(VGPathFlattenNode node,
    Offsetf next_direction,
    EStrokeContourSide side)
    {
        Output.AddNode(MiterPoint(node, next_direction, side), EVGPathFlattenNodeFlags.Corner);
    }
    // Original line 259: emit_side_join.
    public void EmitSideJoin(VGPathFlattenContour contour,
    uint node_local_index,
    EStrokeContourSide side,
    bool reverse)
    {
        VGPathFlattenNode node = Source._nodes[contour.NodeBegin + node_local_index];
        VGPathFlattenNode previous = Source._nodes[PreviousNode(contour, node_local_index)];
        Offsetf previous_direction = previous.NextDirection;
        Offsetf next_direction = node.NextDirection;

        bool has_bevel = VgFlags.All(node.CacheFlags, EVGPathFlattenNodeCacheFlags.Bevel);
        bool has_inner_bevel = VgFlags.All(node.CacheFlags, EVGPathFlattenNodeCacheFlags.InnerBevel);
        bool outer_left = !VgFlags.All(node.Flags, EVGPathFlattenNodeFlags.Left);
        bool is_outer_side = (side == EStrokeContourSide.Left) == outer_left;

        if (!has_bevel && !has_inner_bevel)
        {
            EmitSideMiter(node, next_direction, side);
            return;
        }

        if (is_outer_side)
        {
            if (!has_bevel)
            {
                EmitSideMiter(node, next_direction, side);
                return;
            }

            if (Options.Join == EVGStrokeJoin.Round)
            {
                EmitSideRound(node, previous_direction, next_direction, side, reverse);
            }
            else
            {
                EmitSideBevel(node, previous_direction, next_direction, side, reverse);
            }
            return;
        }

        if (has_inner_bevel)
        {
            EmitSideBevel(node, previous_direction, next_direction, side, reverse);
            return;
        }

        EmitSideMiter(node, next_direction, side);
    }
    // Original line 310: emit_round_cap.
    public void EmitRoundCap(Offsetf center, Offsetf direction, bool start_cap)
    {
        Offsetf normal = direction.CcwNormal();
        float start_angle = start_cap ? (-normal).Radians() : normal.Radians();
        float end_angle = start_cap ? normal.Radians() : (-normal).Radians();
        while (end_angle < start_angle)
        {
            end_angle += (MathF.PI*2);
        }
        EmitArc(center, start_angle, end_angle, false);
    }
    // Original line 322: emit_open_contour.
    public void EmitOpenContour(VGPathFlattenContour contour)
    {
        VGPathFlattenNode first_node = Source._nodes[contour.NodeBegin];
        VGPathFlattenNode last_segment_node = Source._nodes[contour.NodeBegin + contour.NodeCount - 2u];
        VGPathFlattenNode last_node = Source._nodes[contour.NodeBegin + contour.NodeCount - 1u];
        Offsetf first_direction = first_node.NextDirection;
        Offsetf last_direction = last_segment_node.NextDirection;

        Output.NextContour();
        Output.AddNode(
            OpenEndpoint(first_node.Position, first_direction, EStrokeContourSide.Left, true),
            EVGPathFlattenNodeFlags.Corner
        );

        for (uint i_scope5 = 1u; i_scope5 + 1u < contour.NodeCount; ++i_scope5)
        {
            EmitSideJoin(contour, i_scope5, EStrokeContourSide.Left, false);
        }

        Output.AddNode(
            OpenEndpoint(last_node.Position, last_direction, EStrokeContourSide.Left, false),
            EVGPathFlattenNodeFlags.Corner
        );
        if (Options.Cap == EVGStrokeCap.Round)
        {
            EmitRoundCap(last_node.Position, last_direction, false);
        }
        else
        {
            Output.AddNode(
                OpenEndpoint(last_node.Position, last_direction, EStrokeContourSide.Right, false),
                EVGPathFlattenNodeFlags.Corner
            );
        }

        for (uint i_scope6 = contour.NodeCount - 1u; i_scope6 > 1u; --i_scope6)
        {
            EmitSideJoin(contour, i_scope6 - 1u, EStrokeContourSide.Right, true);
        }

        Output.AddNode(
            OpenEndpoint(first_node.Position, first_direction, EStrokeContourSide.Right, true),
            EVGPathFlattenNodeFlags.Corner
        );
        if (Options.Cap == EVGStrokeCap.Round)
        {
            EmitRoundCap(first_node.Position, first_direction, true);
        }
        Output.CloseContour();
    }
    // Original line 373: emit_closed_side_contour.
    public void EmitClosedSideContour(VGPathFlattenContour contour,
    EStrokeContourSide side)
    {
        Output.NextContour();
        if (side == EStrokeContourSide.Left)
        {
            for (uint i_scope0 = 0u; i_scope0 < contour.NodeCount; ++i_scope0)
            {
                EmitSideJoin(contour, i_scope0, side, false);
            }
        }
        else
        {
            for (uint i_scope1 = contour.NodeCount; i_scope1 > 0u; --i_scope1)
            {
                EmitSideJoin(contour, i_scope1 - 1u, side, true);
            }
        }
        Output.CloseContour();
    }
    // Original line 396: emit.
    public void Emit()
    {
        foreach (ref VGPathFlattenContour contour in Source._contours.AsSpan())
        {
            if (contour.Closed)
            {
                EmitClosedSideContour(contour, EStrokeContourSide.Left);
                EmitClosedSideContour(contour, EStrokeContourSide.Right);
            }
            else
            {
                EmitOpenContour(contour);
            }
        }
    }
}
public sealed partial class VGPathFlatten
{
    // Original line 416: stroke_contour_to.
    public bool StrokeContourTo(VGPathFlatten Output, VGStrokeOptions Options)
    {
        Debug.Assert(_finalized , "VGPathFlatten.StrokeContourTo requires finalized Contours");

        if (ReferenceEquals(Output, this))
        {
            Debug.Assert(false , "VGPathFlatten.StrokeContourTo does not support in-place output");
            return false;
        }

        Output.PointEqualsTolerance = PointEqualsTolerance;
        Output.Clear();

        float width = Options.Width;
        if (!float.IsFinite(width) || width <= KEpsilon || _contours.IsEmpty())
        {
            Output.Finalize();
            return true;
        }

        float BodyRadius = width * 0.5f;
        float RoundTolerance = Arc.CalcTolerance(
            Options.TessellationFactor,
            Options.PixelRatio
        );
        uint RoundDivisions = StrokeContourBuilder.CalcRoundDivisions(
            BodyRadius,
            MathF.PI,
            RoundTolerance
        );

        bool force_bevel = Options.Join == EVGStrokeJoin.Bevel || Options.Join == EVGStrokeJoin.Round;
        float inverse_body_radius = BodyRadius > KEpsilon ? 1.0f / BodyRadius : 0.0f;
        float valid_miter_limit = Options.MiterLimit > 0.0f ? Options.MiterLimit : 1.0f;
        float valid_miter_limit_squared = valid_miter_limit * valid_miter_limit;

        ulong reserve_nodes = 0u;
        ulong reserve_contours = 0u;
        foreach (ref VGPathFlattenContour contour in _contours.AsSpan())
        {
            Debug.Assert(
                contour.NodeCount >= (contour.Closed ? 3u : 2u) , "VGPathFlatten.StrokeContourTo requires finalized Contours");
            contour.BevelCount = 0u;
            reserve_contours += contour.Closed ? 2u : 1u;

            uint segment_count = contour.Closed ? contour.NodeCount : contour.NodeCount - 1u;
            for (uint i = 0u; i < contour.NodeCount; ++i)
            {
                ref VGPathFlattenNode node = ref _nodes[contour.NodeBegin + i];
                node.CacheFlags = EVGPathFlattenNodeCacheFlags.None;

                if (i < segment_count)
                {
                    Debug.Assert(
                        node.NextLength > KEpsilon , "VGPathFlatten.StrokeContourTo requires finalized non-zero segments");
                }

                if (!contour.Closed && (i == 0u || i + 1u == contour.NodeCount))
                {
                    continue;
                }

                uint previous_node_index = i == 0u ?
                    contour.NodeBegin + contour.NodeCount - 1u :
                    contour.NodeBegin + i - 1u;
                ref VGPathFlattenNode PreviousNode = ref _nodes[previous_node_index];
                float min_segment_length = PreviousNode.NextLength < node.NextLength ?
                    PreviousNode.NextLength :
                    node.NextLength;
                Debug.Assert(
                    min_segment_length > KEpsilon , "VGPathFlatten.StrokeContourTo requires finalized non-zero segments");

                if (IsPassThroughJoin(PreviousNode.NextDirection, node.NextDirection))
                {
                    node.CacheFlags |= EVGPathFlattenNodeCacheFlags.Collinear;
                    continue;
                }

                float miter_length_squared = node.JoinDirection.LengthSquared();
                float inner_limit = min_segment_length * inverse_body_radius;
                if (inner_limit < 1.01f)
                {
                    inner_limit = 1.01f;
                }
                if (miter_length_squared > inner_limit * inner_limit)
                {
                    node.CacheFlags |= EVGPathFlattenNodeCacheFlags.InnerBevel;
                }

                bool is_corner = VgFlags.All(node.Flags, EVGPathFlattenNodeFlags.Corner);
                if (is_corner && (miter_length_squared > valid_miter_limit_squared || force_bevel))
                {
                    node.CacheFlags |= EVGPathFlattenNodeCacheFlags.Bevel;
                }

                bool has_bevel = VgFlags.Any(
                    node.CacheFlags,
                    EVGPathFlattenNodeCacheFlags.Bevel | EVGPathFlattenNodeCacheFlags.InnerBevel
                );
                if (has_bevel)
                {
                    ++contour.BevelCount;
                }
            }

            ulong side_scale = Options.Join == EVGStrokeJoin.Round ? RoundDivisions + 1u : 2u;
            reserve_nodes += (ulong)(contour.NodeCount) * (contour.Closed ? 2u : 2u) * side_scale;
            if (!contour.Closed && Options.Cap == EVGStrokeCap.Round)
            {
                reserve_nodes += (ulong)(RoundDivisions + 1u) * 2u;
            }
        }
        Output.Reserve(reserve_nodes, reserve_contours);

        StrokeContourBuilder builder = new(this,
            Output,
            Options,
            BodyRadius,
            RoundTolerance,
            RoundDivisions);
        builder.Emit();
        Output.Finalize();
        return true;
    }
}
