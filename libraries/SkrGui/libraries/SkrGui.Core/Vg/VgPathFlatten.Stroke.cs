// Source: src/vg/vg_path_flatten.stroke.cpp @ 611561f8.
using System.Diagnostics;
namespace SkrGui;
 internal struct StrokeSectionIndices { public uint LeftBody,RightBody,LeftAa,RightAa; }
 internal struct StrokeJoinSections(StrokeSectionIndices incoming,StrokeSectionIndices outgoing) { public StrokeSectionIndices Incoming=incoming,Outgoing=outgoing; }
 internal struct StrokeRoundArcIndices { public uint FirstBody,FirstAa,LastBody,LastAa,ExtraBegin,ExtraCount; }
internal sealed class StrokeBuildContext(VGMeshEmitter emitter,float bodyRadius,float aaRadius,float roundTolerance,uint roundDivisions,bool hasAa)
{
    public VGMeshEmitter Emitter=emitter; public float BodyRadius=bodyRadius,AaRadius=aaRadius,RoundTolerance=roundTolerance; public uint RoundDivisions=roundDivisions; public bool HasAa=hasAa;
    // Original line 170: line_intersection.
    public static Offsetf LineIntersection(Offsetf line_a_pos,
    Offsetf line_a_dir,
    Offsetf line_b_pos,
    Offsetf line_b_dir,
    Offsetf fallback)
    {
        float Cross = line_a_dir.Cross(line_b_dir);
        if (!float.IsFinite(Cross) || MathF.Abs(Cross) <= 0.000001f)
        {
            return fallback;
        }

        Offsetf delta = line_b_pos - line_a_pos;
        float t = delta.Cross(line_b_dir) / Cross;
        Offsetf result = line_a_pos + line_a_dir * t;
        return result.IsFinite() ? result : fallback;
    }
    // Original line 189: calc_bevel_aa_corner.
    public static Offsetf CalcBevelAaCorner(Offsetf body_corner,
    Offsetf segment_direction,
    Offsetf segment_outward,
    Offsetf bevel_line_pos,
    Offsetf bevel_direction,
    Offsetf bevel_outward,
    float AaRadius)
    {


        Offsetf segment_aa_pos = body_corner + segment_outward * AaRadius;
        Offsetf bevel_aa_pos = bevel_line_pos + bevel_outward * AaRadius;
        return LineIntersection(
            segment_aa_pos,
            segment_direction,
            bevel_aa_pos,
            bevel_direction,
            segment_aa_pos
        );
    }
    // Original line 211: calc_round_divisions.
    public static uint CalcRoundDivisions(float radius, float sweep_angle, float tolerance)
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
    // Original line 222: emit_section.
    public StrokeSectionIndices EmitSection(Offsetf LeftBody,
    Offsetf RightBody)
    {
        StrokeSectionIndices section = new();
        section.LeftBody = Emitter.PushVertex(LeftBody, 1.0f);
        section.RightBody = Emitter.PushVertex(RightBody, 1.0f);
        return section;
    }
    // Original line 232: emit_section.
    public StrokeSectionIndices EmitSection(Offsetf LeftBody,
    Offsetf RightBody,
    Offsetf LeftAa,
    Offsetf RightAa)
    {
        StrokeSectionIndices section = EmitSection(LeftBody, RightBody);
        if (HasAa)
        {
            section.LeftAa = Emitter.PushVertex(LeftAa, 0.0f);
            section.RightAa = Emitter.PushVertex(RightAa, 0.0f);
        }
        return section;
    }
    // Original line 247: emit_direction_section.
    public StrokeSectionIndices EmitDirectionSection(Offsetf center,
    Offsetf direction,
    Offsetf aa_axis_offset)
    {
        Offsetf normal = direction.CcwNormal();
        Offsetf body_offset = normal * BodyRadius;
        if (!HasAa)
        {
            return EmitSection(center + body_offset, center - body_offset);
        }

        Offsetf aa_offset = normal * (BodyRadius + AaRadius);
        return EmitSection(
            center + body_offset,
            center - body_offset,
            center + aa_axis_offset + aa_offset,
            center + aa_axis_offset - aa_offset
        );
    }
    // Original line 268: emit_direction_section.
    public StrokeSectionIndices EmitDirectionSection(Offsetf center,
    Offsetf direction)
    {
        Offsetf normal = direction.CcwNormal();
        Offsetf body_offset = normal * BodyRadius;
        return EmitSection(center + body_offset, center - body_offset);
    }
    // Original line 277: emit_miter_section.
    public StrokeSectionIndices EmitMiterSection(Offsetf position,
    Offsetf join_direction,
    Offsetf fallback_direction)
    {
        if (!join_direction.IsFinite() || join_direction.LengthSquared() <= 0.000001f)
        {
            join_direction = fallback_direction.CcwNormal();
        }

        Offsetf LeftBody = position + join_direction * BodyRadius;
        Offsetf RightBody = position - join_direction * BodyRadius;
        if (!HasAa)
        {
            return EmitSection(LeftBody, RightBody);
        }

        return EmitSection(
            LeftBody,
            RightBody,
            position + join_direction * (BodyRadius + AaRadius),
            position - join_direction * (BodyRadius + AaRadius)
        );
    }
    // Original line 303: build_start_cap.
    public StrokeSectionIndices BuildStartCap(Offsetf center,
    Offsetf direction,
    EVGStrokeCap cap)
    {
        float cap_extend = cap == EVGStrokeCap.Square ? BodyRadius : 0.0f;
        Offsetf section_center = center - direction * cap_extend;
        StrokeSectionIndices section = new();
        if (HasAa)
        {
            Offsetf aa_axis_offset = cap == EVGStrokeCap.Round ? Offsetf.Zero() : -direction * AaRadius;
            section = EmitDirectionSection(section_center, direction, aa_axis_offset);
        }
        else
        {
            section = EmitDirectionSection(section_center, direction);
        }

        if (cap == EVGStrokeCap.Round)
        {
            BuildRoundCap(section, center, direction, true);
        }
        else
        {
            BuildFlatCapAa(section);
        }
        return section;
    }
    // Original line 332: build_end_cap.
    public StrokeSectionIndices BuildEndCap(Offsetf center,
    Offsetf direction,
    EVGStrokeCap cap)
    {
        float cap_extend = cap == EVGStrokeCap.Square ? BodyRadius : 0.0f;
        Offsetf section_center = center + direction * cap_extend;
        StrokeSectionIndices section = new();
        if (HasAa)
        {
            Offsetf aa_axis_offset = cap == EVGStrokeCap.Round ? Offsetf.Zero() : direction * AaRadius;
            section = EmitDirectionSection(section_center, direction, aa_axis_offset);
        }
        else
        {
            section = EmitDirectionSection(section_center, direction);
        }

        if (cap == EVGStrokeCap.Round)
        {
            BuildRoundCap(section, center, direction, false);
        }
        else
        {
            BuildFlatCapAa(section);
        }
        return section;
    }
    // Original line 361: build_join.
    public StrokeJoinSections BuildJoin(VGPathFlattenNode node,
    Offsetf previous_direction,
    Offsetf next_direction,
    EVGStrokeJoin join)
    {
        bool has_bevel = VgFlags.All(node.CacheFlags, EVGPathFlattenNodeCacheFlags.Bevel);
        bool has_inner_bevel = VgFlags.All(node.CacheFlags, EVGPathFlattenNodeCacheFlags.InnerBevel);
        if (!has_bevel && !has_inner_bevel)
        {
            StrokeSectionIndices section = EmitMiterSection(node.Position, node.JoinDirection, next_direction);
            return new StrokeJoinSections(section, section);
        }

        bool outer_left = !VgFlags.All(node.Flags, EVGPathFlattenNodeFlags.Left);
        if (!has_bevel)
        {
            return BuildInnerBevelJoin(node, previous_direction, next_direction, outer_left);
        }

        if (join == EVGStrokeJoin.Round)
        {
            return BuildRoundJoin(node, previous_direction, next_direction, outer_left, has_inner_bevel);
        }
        return BuildBevelJoin(node, previous_direction, next_direction, outer_left, has_inner_bevel);
    }
    // Original line 388: build_inner_bevel_join.
    public StrokeJoinSections BuildInnerBevelJoin(VGPathFlattenNode node,
    Offsetf previous_direction,
    Offsetf next_direction,
    bool outer_left)
    {
        Offsetf previous_normal = previous_direction.CcwNormal();
        Offsetf next_normal = next_direction.CcwNormal();
        StrokeSectionIndices Incoming = new();
        StrokeSectionIndices Outgoing = new();

        if (outer_left)
        {
            Offsetf outer_body_scope4 = node.Position + node.JoinDirection * BodyRadius;
            Incoming.LeftBody = Emitter.PushVertex(outer_body_scope4, 1.0f);
            Incoming.RightBody = Emitter.PushVertex(node.Position - previous_normal * BodyRadius, 1.0f);
            Outgoing.LeftBody = Incoming.LeftBody;
            Outgoing.RightBody = Emitter.PushVertex(node.Position - next_normal * BodyRadius, 1.0f);

            if (HasAa)
            {
                Offsetf outer_aa_scope5 = node.Position + node.JoinDirection * (BodyRadius + AaRadius);
                Incoming.LeftAa = Emitter.PushVertex(outer_aa_scope5, 0.0f);
                Incoming.RightAa = Emitter.PushVertex(node.Position - previous_normal * (BodyRadius + AaRadius), 0.0f);
                Outgoing.LeftAa = Incoming.LeftAa;
                Outgoing.RightAa = Emitter.PushVertex(node.Position - next_normal * (BodyRadius + AaRadius), 0.0f);
            }

            Emitter.PushTriangle(Incoming.LeftBody, Incoming.RightBody, Outgoing.RightBody);
            if (HasAa)
            {
                Emitter.PushQuad(Incoming.RightBody, Incoming.RightAa, Outgoing.RightAa, Outgoing.RightBody);
            }
        }
        else
        {
            Offsetf outer_body_scope6 = node.Position - node.JoinDirection * BodyRadius;
            Incoming.LeftBody = Emitter.PushVertex(node.Position + previous_normal * BodyRadius, 1.0f);
            Incoming.RightBody = Emitter.PushVertex(outer_body_scope6, 1.0f);
            Outgoing.LeftBody = Emitter.PushVertex(node.Position + next_normal * BodyRadius, 1.0f);
            Outgoing.RightBody = Incoming.RightBody;

            if (HasAa)
            {
                Offsetf outer_aa_scope7 = node.Position - node.JoinDirection * (BodyRadius + AaRadius);
                Incoming.LeftAa = Emitter.PushVertex(node.Position + previous_normal * (BodyRadius + AaRadius), 0.0f);
                Incoming.RightAa = Emitter.PushVertex(outer_aa_scope7, 0.0f);
                Outgoing.LeftAa = Emitter.PushVertex(node.Position + next_normal * (BodyRadius + AaRadius), 0.0f);
                Outgoing.RightAa = Incoming.RightAa;
            }

            Emitter.PushTriangle(Incoming.LeftBody, Incoming.RightBody, Outgoing.LeftBody);
            if (HasAa)
            {
                Emitter.PushQuad(Incoming.LeftAa, Incoming.LeftBody, Outgoing.LeftBody, Outgoing.LeftAa);
            }
        }

        return new StrokeJoinSections(Incoming, Outgoing);
    }
    // Original line 449: build_bevel_join.
    public StrokeJoinSections BuildBevelJoin(VGPathFlattenNode node,
    Offsetf previous_direction,
    Offsetf next_direction,
    bool outer_left,
    bool inner_bevel)
    {
        Offsetf previous_normal = previous_direction.CcwNormal();
        Offsetf next_normal = next_direction.CcwNormal();
        StrokeSectionIndices Incoming = new();
        StrokeSectionIndices Outgoing = new();

        if (outer_left)
        {
            Offsetf incoming_left_body_scope4 = node.Position + previous_normal * BodyRadius;
            Offsetf outgoing_left_body_scope5 = node.Position + next_normal * BodyRadius;
            Offsetf incoming_right_body_scope6 = inner_bevel ?
                node.Position - previous_normal * BodyRadius :
                node.Position - node.JoinDirection * BodyRadius;
            Offsetf outgoing_right_body_scope7 = inner_bevel ?
                node.Position - next_normal * BodyRadius :
                incoming_right_body_scope6;

            Incoming.LeftBody = Emitter.PushVertex(incoming_left_body_scope4, 1.0f);
            Incoming.RightBody = Emitter.PushVertex(incoming_right_body_scope6, 1.0f);
            Outgoing.LeftBody = Emitter.PushVertex(outgoing_left_body_scope5, 1.0f);
            Outgoing.RightBody = inner_bevel ? Emitter.PushVertex(outgoing_right_body_scope7, 1.0f) : Incoming.RightBody;

            if (HasAa)
            {
                Offsetf bevel_edge_scope8 = outgoing_left_body_scope5 - incoming_left_body_scope4;
                float bevel_edge_length_scope9 = bevel_edge_scope8.Length();
                Offsetf incoming_left_aa_scope10 = incoming_left_body_scope4 + previous_normal * AaRadius;
                Offsetf outgoing_left_aa_scope11 = outgoing_left_body_scope5 + next_normal * AaRadius;
                Offsetf incoming_right_aa_scope12 = inner_bevel ?
                    node.Position - previous_normal * (BodyRadius + AaRadius) :
                    node.Position - node.JoinDirection * (BodyRadius + AaRadius);
                Offsetf outgoing_right_aa_scope13 = inner_bevel ?
                    node.Position - next_normal * (BodyRadius + AaRadius) :
                    incoming_right_aa_scope12;
                if (float.IsFinite(bevel_edge_length_scope9) && bevel_edge_length_scope9 > 0.000001f)
                {
                    Offsetf bevel_direction_scope14 = bevel_edge_scope8 / bevel_edge_length_scope9;
                    Offsetf bevel_outward_scope15 = bevel_direction_scope14.CcwNormal();

                    incoming_left_aa_scope10 = CalcBevelAaCorner(
                        incoming_left_body_scope4,
                        previous_direction,
                        previous_normal,
                        incoming_left_body_scope4,
                        bevel_direction_scope14,
                        bevel_outward_scope15,
                        AaRadius
                    );
                    outgoing_left_aa_scope11 = CalcBevelAaCorner(
                        outgoing_left_body_scope5,
                        next_direction,
                        next_normal,
                        incoming_left_body_scope4,
                        bevel_direction_scope14,
                        bevel_outward_scope15,
                        AaRadius
                    );
                }

                Incoming.LeftAa = Emitter.PushVertex(incoming_left_aa_scope10, 0.0f);
                Incoming.RightAa = Emitter.PushVertex(incoming_right_aa_scope12, 0.0f);
                Outgoing.LeftAa = Emitter.PushVertex(outgoing_left_aa_scope11, 0.0f);
                Outgoing.RightAa = inner_bevel ? Emitter.PushVertex(outgoing_right_aa_scope13, 0.0f) : Incoming.RightAa;
            }

            if (inner_bevel)
            {
                Emitter.PushQuad(Incoming.LeftBody, Incoming.RightBody, Outgoing.RightBody, Outgoing.LeftBody);
            }
            else
            {
                Emitter.PushTriangle(Incoming.LeftBody, Incoming.RightBody, Outgoing.LeftBody);
            }
            if (HasAa)
            {
                Emitter.PushQuad(Incoming.LeftAa, Incoming.LeftBody, Outgoing.LeftBody, Outgoing.LeftAa);
                if (inner_bevel)
                {
                    Emitter.PushQuad(Incoming.RightBody, Incoming.RightAa, Outgoing.RightAa, Outgoing.RightBody);
                }
            }
        }
        else
        {
            Offsetf incoming_left_body_scope16 = inner_bevel ?
                node.Position + previous_normal * BodyRadius :
                node.Position + node.JoinDirection * BodyRadius;
            Offsetf outgoing_left_body_scope17 = inner_bevel ?
                node.Position + next_normal * BodyRadius :
                incoming_left_body_scope16;
            Offsetf incoming_right_body_scope18 = node.Position - previous_normal * BodyRadius;
            Offsetf outgoing_right_body_scope19 = node.Position - next_normal * BodyRadius;

            Incoming.LeftBody = Emitter.PushVertex(incoming_left_body_scope16, 1.0f);
            Incoming.RightBody = Emitter.PushVertex(incoming_right_body_scope18, 1.0f);
            Outgoing.LeftBody = inner_bevel ? Emitter.PushVertex(outgoing_left_body_scope17, 1.0f) : Incoming.LeftBody;
            Outgoing.RightBody = Emitter.PushVertex(outgoing_right_body_scope19, 1.0f);

            if (HasAa)
            {
                Offsetf bevel_edge_scope20 = outgoing_right_body_scope19 - incoming_right_body_scope18;
                float bevel_edge_length_scope21 = bevel_edge_scope20.Length();
                Offsetf incoming_left_aa_scope22 = inner_bevel ?
                    node.Position + previous_normal * (BodyRadius + AaRadius) :
                    node.Position + node.JoinDirection * (BodyRadius + AaRadius);
                Offsetf outgoing_left_aa_scope23 = inner_bevel ?
                    node.Position + next_normal * (BodyRadius + AaRadius) :
                    incoming_left_aa_scope22;
                Offsetf incoming_right_aa_scope24 = incoming_right_body_scope18 - previous_normal * AaRadius;
                Offsetf outgoing_right_aa_scope25 = outgoing_right_body_scope19 - next_normal * AaRadius;
                if (float.IsFinite(bevel_edge_length_scope21) && bevel_edge_length_scope21 > 0.000001f)
                {
                    Offsetf bevel_direction_scope26 = bevel_edge_scope20 / bevel_edge_length_scope21;
                    Offsetf bevel_outward_scope27 = -bevel_direction_scope26.CcwNormal();

                    incoming_right_aa_scope24 = CalcBevelAaCorner(
                        incoming_right_body_scope18,
                        previous_direction,
                        -previous_normal,
                        incoming_right_body_scope18,
                        bevel_direction_scope26,
                        bevel_outward_scope27,
                        AaRadius
                    );
                    outgoing_right_aa_scope25 = CalcBevelAaCorner(
                        outgoing_right_body_scope19,
                        next_direction,
                        -next_normal,
                        incoming_right_body_scope18,
                        bevel_direction_scope26,
                        bevel_outward_scope27,
                        AaRadius
                    );
                }

                Incoming.LeftAa = Emitter.PushVertex(incoming_left_aa_scope22, 0.0f);
                Incoming.RightAa = Emitter.PushVertex(incoming_right_aa_scope24, 0.0f);
                Outgoing.LeftAa = inner_bevel ? Emitter.PushVertex(outgoing_left_aa_scope23, 0.0f) : Incoming.LeftAa;
                Outgoing.RightAa = Emitter.PushVertex(outgoing_right_aa_scope25, 0.0f);
            }

            if (inner_bevel)
            {
                Emitter.PushQuad(Incoming.LeftBody, Incoming.RightBody, Outgoing.RightBody, Outgoing.LeftBody);
            }
            else
            {
                Emitter.PushTriangle(Incoming.RightBody, Outgoing.RightBody, Incoming.LeftBody);
            }
            if (HasAa)
            {
                Emitter.PushQuad(Incoming.RightBody, Incoming.RightAa, Outgoing.RightAa, Outgoing.RightBody);
                if (inner_bevel)
                {
                    Emitter.PushQuad(Incoming.LeftAa, Incoming.LeftBody, Outgoing.LeftBody, Outgoing.LeftAa);
                }
            }
        }

        return new StrokeJoinSections(Incoming, Outgoing);
    }
    // Original line 617: build_round_join.
    public StrokeJoinSections BuildRoundJoin(VGPathFlattenNode node,
    Offsetf previous_direction,
    Offsetf next_direction,
    bool outer_left,
    bool inner_bevel)
    {
        Offsetf previous_normal = previous_direction.CcwNormal();
        Offsetf next_normal = next_direction.CcwNormal();
        uint center_index = Emitter.PushVertex(node.Position, 1.0f);
        StrokeSectionIndices Incoming = new();
        StrokeSectionIndices Outgoing = new();

        if (outer_left)
        {
            Offsetf incoming_right_body = inner_bevel ?
                node.Position - previous_normal * BodyRadius :
                node.Position - node.JoinDirection * BodyRadius;
            Offsetf outgoing_right_body = inner_bevel ?
                node.Position - next_normal * BodyRadius :
                incoming_right_body;
            Incoming.LeftBody = Emitter.PushVertex(node.Position + previous_normal * BodyRadius, 1.0f);
            Incoming.RightBody = Emitter.PushVertex(incoming_right_body, 1.0f);
            Outgoing.LeftBody = Emitter.PushVertex(node.Position + next_normal * BodyRadius, 1.0f);
            Outgoing.RightBody = inner_bevel ? Emitter.PushVertex(outgoing_right_body, 1.0f) : Incoming.RightBody;

            if (HasAa)
            {
                Offsetf incoming_right_aa = inner_bevel ?
                    node.Position - previous_normal * (BodyRadius + AaRadius) :
                    node.Position - node.JoinDirection * (BodyRadius + AaRadius);
                Offsetf outgoing_right_aa = inner_bevel ?
                    node.Position - next_normal * (BodyRadius + AaRadius) :
                    incoming_right_aa;
                Incoming.LeftAa = Emitter.PushVertex(node.Position + previous_normal * (BodyRadius + AaRadius), 0.0f);
                Incoming.RightAa = Emitter.PushVertex(incoming_right_aa, 0.0f);
                Outgoing.LeftAa = Emitter.PushVertex(node.Position + next_normal * (BodyRadius + AaRadius), 0.0f);
                Outgoing.RightAa = inner_bevel ? Emitter.PushVertex(outgoing_right_aa, 0.0f) : Incoming.RightAa;
            }

            float start_angle_scope9 = previous_normal.Radians();
            float end_angle_scope10 = next_normal.Radians();
            while (end_angle_scope10 < start_angle_scope9)
            {
                end_angle_scope10 += (MathF.PI*2);
            }
            StrokeRoundArcIndices arc = EmitRoundArcVertices(
                node.Position,
                Incoming.LeftBody,
                Incoming.LeftAa,
                Outgoing.LeftBody,
                Outgoing.LeftAa,
                start_angle_scope9,
                end_angle_scope10
            );

            Emitter.PushTriangle(Incoming.RightBody, center_index, Incoming.LeftBody);
            if (inner_bevel)
            {
                Emitter.PushTriangle(center_index, Outgoing.RightBody, Incoming.RightBody);
                Emitter.PushTriangle(Outgoing.RightBody, Outgoing.LeftBody, center_index);
            }
            else
            {
                Emitter.PushTriangle(Incoming.RightBody, Outgoing.LeftBody, center_index);
            }
            ConnectRoundArc(center_index, arc);
            if (HasAa && inner_bevel)
            {
                Emitter.PushQuad(Incoming.RightBody, Incoming.RightAa, Outgoing.RightAa, Outgoing.RightBody);
            }
        }
        else
        {
            Offsetf incoming_left_body = inner_bevel ?
                node.Position + previous_normal * BodyRadius :
                node.Position + node.JoinDirection * BodyRadius;
            Offsetf outgoing_left_body = inner_bevel ?
                node.Position + next_normal * BodyRadius :
                incoming_left_body;
            Incoming.LeftBody = Emitter.PushVertex(incoming_left_body, 1.0f);
            Incoming.RightBody = Emitter.PushVertex(node.Position - previous_normal * BodyRadius, 1.0f);
            Outgoing.LeftBody = inner_bevel ? Emitter.PushVertex(outgoing_left_body, 1.0f) : Incoming.LeftBody;
            Outgoing.RightBody = Emitter.PushVertex(node.Position - next_normal * BodyRadius, 1.0f);

            if (HasAa)
            {
                Offsetf incoming_left_aa = inner_bevel ?
                    node.Position + previous_normal * (BodyRadius + AaRadius) :
                    node.Position + node.JoinDirection * (BodyRadius + AaRadius);
                Offsetf outgoing_left_aa = inner_bevel ?
                    node.Position + next_normal * (BodyRadius + AaRadius) :
                    incoming_left_aa;
                Incoming.LeftAa = Emitter.PushVertex(incoming_left_aa, 0.0f);
                Incoming.RightAa = Emitter.PushVertex(node.Position - previous_normal * (BodyRadius + AaRadius), 0.0f);
                Outgoing.LeftAa = inner_bevel ? Emitter.PushVertex(outgoing_left_aa, 0.0f) : Incoming.LeftAa;
                Outgoing.RightAa = Emitter.PushVertex(node.Position - next_normal * (BodyRadius + AaRadius), 0.0f);
            }

            float start_angle_scope15 = (-previous_normal).Radians();
            float end_angle_scope16 = (-next_normal).Radians();
            while (end_angle_scope16 > start_angle_scope15)
            {
                end_angle_scope16 -= (MathF.PI*2);
            }
            StrokeRoundArcIndices arc = EmitRoundArcVertices(
                node.Position,
                Incoming.RightBody,
                Incoming.RightAa,
                Outgoing.RightBody,
                Outgoing.RightAa,
                start_angle_scope15,
                end_angle_scope16
            );

            Emitter.PushTriangle(Incoming.LeftBody, Incoming.RightBody, center_index);
            if (inner_bevel)
            {
                Emitter.PushTriangle(center_index, Incoming.LeftBody, Outgoing.LeftBody);
                Emitter.PushTriangle(Outgoing.LeftBody, center_index, Outgoing.RightBody);
            }
            else
            {
                Emitter.PushTriangle(Incoming.LeftBody, center_index, Outgoing.RightBody);
            }
            ConnectRoundArc(center_index, arc);
            if (HasAa && inner_bevel)
            {
                Emitter.PushQuad(Incoming.LeftAa, Incoming.LeftBody, Outgoing.LeftBody, Outgoing.LeftAa);
            }
        }

        return new StrokeJoinSections(Incoming, Outgoing);
    }
    // Original line 753: connect_segment.
    public void ConnectSegment(StrokeSectionIndices previous,
    StrokeSectionIndices current)
    {

        Emitter.PushQuad(
            previous.LeftBody,
            previous.RightBody,
            current.RightBody,
            current.LeftBody
        );

        if (!HasAa)
        {
            return;
        }


        Emitter.PushQuad(
            previous.LeftAa,
            previous.LeftBody,
            current.LeftBody,
            current.LeftAa
        );
        Emitter.PushQuad(
            previous.RightBody,
            previous.RightAa,
            current.RightAa,
            current.RightBody
        );
    }
    // Original line 785: build_flat_cap_aa.
    public void BuildFlatCapAa(StrokeSectionIndices section)
    {
        if (!HasAa)
        {
            return;
        }

        Emitter.PushQuad(section.LeftAa, section.LeftBody, section.RightBody, section.RightAa);
    }
    // Original line 794: build_round_cap.
    public void BuildRoundCap(StrokeSectionIndices section,
    Offsetf center,
    Offsetf direction,
    bool start_cap)
    {
        Offsetf normal = direction.CcwNormal();
        float start_angle = (-normal).Radians();
        float end_angle = normal.Radians();
        if (start_cap)
        {
            while (end_angle < start_angle)
            {
                end_angle += (MathF.PI*2);
            }
        }
        else
        {
            while (end_angle > start_angle)
            {
                end_angle -= (MathF.PI*2);
            }
        }

        uint center_index = Emitter.PushVertex(center, 1.0f);
        StrokeRoundArcIndices arc = EmitRoundArcVertices(
            center,
            section.RightBody,
            section.RightAa,
            section.LeftBody,
            section.LeftAa,
            start_angle,
            end_angle
        );
        ConnectRoundArc(center_index, arc);
    }
    // Original line 832: emit_round_arc_vertices.
    public StrokeRoundArcIndices EmitRoundArcVertices(Offsetf center,
    uint FirstBody,
    uint FirstAa,
    uint LastBody,
    uint LastAa,
    float start_angle,
    float end_angle)
    {
        float sweep = end_angle - start_angle;
        uint divisions = CalcRoundDivisions(BodyRadius, sweep, RoundTolerance);
        if (divisions > RoundDivisions)
        {
            divisions = RoundDivisions;
        }

        StrokeRoundArcIndices arc = new();
        arc.FirstBody = FirstBody;
        arc.FirstAa = FirstAa;
        arc.LastBody = LastBody;
        arc.LastAa = LastAa;
        arc.ExtraCount = divisions - 1u;

        if (arc.ExtraCount > 0u)
        {
            arc.ExtraBegin = (uint)(Emitter.VertexCount);
        }

        for (uint i = 1u; i < divisions; ++i)
        {
            float u = (float)(i) / (float)(divisions);
            float angle = start_angle + sweep * u;
            Offsetf direction = new Offsetf(MathF.Cos(angle), MathF.Sin(angle));
            Emitter.PushVertex(center + direction * BodyRadius, 1.0f);
            if (HasAa)
            {
                Emitter.PushVertex(center + direction * (BodyRadius + AaRadius), 0.0f);
            }
        }

        return arc;
    }
    // Original line 875: connect_round_arc.
    public void ConnectRoundArc(uint center_index, StrokeRoundArcIndices arc)
    {
        uint previous_body = arc.FirstBody;
        uint previous_aa = arc.FirstAa;
        for (uint i = 0u; i < arc.ExtraCount; ++i)
        {
            uint body = arc.ExtraBegin + i * (HasAa ? 2u : 1u);
            uint aa = HasAa ? body + 1u : body;

            Emitter.PushTriangle(center_index, previous_body, body);
            if (HasAa)
            {
                Emitter.PushQuad(previous_aa, previous_body, body, aa);
            }

            previous_body = body;
            previous_aa = aa;
        }

        Emitter.PushTriangle(center_index, previous_body, arc.LastBody);
        if (HasAa)
        {
            Emitter.PushQuad(previous_aa, previous_body, arc.LastBody, arc.LastAa);
        }
    }
}
internal sealed class StrokeReserveEstimate
{
    public ulong VertexCount,TriangleCount;
    // Original line 905: section_vertices.
    public static ulong SectionVertices(bool HasAa)
    {
        return HasAa ? 4u : 2u;
    }
    // Original line 909: segment_triangles.
    public static ulong SegmentTriangles(bool HasAa)
    {
        return HasAa ? 6u : 2u;
    }
    // Original line 913: round_arc_extra_vertices.
    public static ulong RoundArcExtraVertices(uint RoundDivisions, bool HasAa)
    {
        if (RoundDivisions <= 1u)
        {
            return 0u;
        }
        return (ulong)(RoundDivisions - 1u) * (HasAa ? 2u : 1u);
    }
    // Original line 921: round_arc_triangles.
    public static ulong RoundArcTriangles(uint RoundDivisions, bool HasAa)
    {
        return (ulong)(RoundDivisions) * (HasAa ? 3u : 1u);
    }
    // Original line 925: round_fan_vertices.
    public static ulong RoundFanVertices(uint RoundDivisions, bool HasAa)
    {
        return 1u + RoundArcExtraVertices(RoundDivisions, HasAa);
    }
    // Original line 929: round_join_triangles.
    public static ulong RoundJoinTriangles(uint RoundDivisions, bool HasAa)
    {
        return 3u + RoundArcTriangles(RoundDivisions, HasAa) + (HasAa ? 2u : 0u);
    }
    // Original line 933: add_contour.
    public static void AddContour(StrokeReserveEstimate estimate,
    VGPathFlattenContour contour,
    uint segment_count,
    VGStrokeOptions options,
    uint RoundDivisions,
    bool HasAa)
    {
        ulong bevel_count = contour.BevelCount;
        ulong section_count = (ulong)(contour.NodeCount) + bevel_count;



        estimate.VertexCount += section_count * SectionVertices(HasAa);
        estimate.TriangleCount += (ulong)(segment_count) * SegmentTriangles(HasAa);

        if (options.Join == EVGStrokeJoin.Round)
        {
            estimate.VertexCount += bevel_count * RoundFanVertices(RoundDivisions, HasAa);
            estimate.TriangleCount += bevel_count * RoundJoinTriangles(RoundDivisions, HasAa);
        }
        else
        {
            estimate.TriangleCount += bevel_count * SegmentTriangles(HasAa);
        }

        if (contour.Closed)
        {
            return;
        }

        if (options.Cap == EVGStrokeCap.Round)
        {
            estimate.VertexCount += 2u * RoundFanVertices(RoundDivisions, HasAa);
            estimate.TriangleCount += 2u * RoundArcTriangles(RoundDivisions, HasAa);
        }
        else if (HasAa)
        {
            estimate.TriangleCount += 4u;
        }
    }
}
public sealed partial class VGPathFlatten
{
    // Original line 980: stroke.
    public bool Stroke(VGBackend backend, VGStrokeOptions options)
    {
        Debug.Assert(_finalized , "VGPathFlatten.Stroke requires finalized contours");

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGPathFlatten.Stroke requires a valid VGBackend");
            return false;
        }

        float width = options.Width;
        if (!float.IsFinite(width) || width <= KEpsilon || _contours.IsEmpty())
        {
            return true;
        }

        float AaRadius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float BodyRadius = width * 0.5f;
        float RoundTolerance = Arc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint RoundDivisions = StrokeBuildContext.CalcRoundDivisions(
            BodyRadius,
            MathF.PI,
            RoundTolerance
        );

        bool HasAa = AaRadius > 0.0f;
        bool force_bevel = options.Join == EVGStrokeJoin.Bevel || options.Join == EVGStrokeJoin.Round;
        float inverse_body_radius = BodyRadius > KEpsilon ? 1.0f / BodyRadius : 0.0f;
        float valid_miter_limit = options.MiterLimit > 0.0f ? options.MiterLimit : 1.0f;
        float valid_miter_limit_squared = valid_miter_limit * valid_miter_limit;
        StrokeReserveEstimate reserve = new();


        foreach (ref VGPathFlattenContour contour in _contours.AsSpan())
        {
            Debug.Assert(
                contour.NodeCount >= (contour.Closed ? 3u : 2u) , "VGPathFlatten.Stroke requires finalized contours");
            contour.BevelCount = 0u;
            uint segment_count = contour.Closed ? contour.NodeCount : contour.NodeCount - 1u;
            for (uint i_scope9 = 0u; i_scope9 < contour.NodeCount; ++i_scope9)
            {
                ref VGPathFlattenNode node_scope10 = ref _nodes[contour.NodeBegin + i_scope9];
                node_scope10.CacheFlags = EVGPathFlattenNodeCacheFlags.None;

                if (i_scope9 < segment_count)
                {
                    Debug.Assert(
                        node_scope10.NextLength > KEpsilon , "VGPathFlatten.Stroke requires finalized non-zero segments");
                }

                if (!contour.Closed && (i_scope9 == 0u || i_scope9 + 1u == contour.NodeCount))
                {
                    continue;
                }

                uint previous_node_index = i_scope9 == 0u ?
                    contour.NodeBegin + contour.NodeCount - 1u :
                    contour.NodeBegin + i_scope9 - 1u;
                ref VGPathFlattenNode previous_node_scope12 = ref _nodes[previous_node_index];
                float min_segment_length = previous_node_scope12.NextLength < node_scope10.NextLength ?
                    previous_node_scope12.NextLength :
                    node_scope10.NextLength;
                Debug.Assert(
                    min_segment_length > KEpsilon , "VGPathFlatten.Stroke requires finalized non-zero segments");

                if (IsPassThroughJoin(previous_node_scope12.NextDirection, node_scope10.NextDirection))
                {
                    node_scope10.CacheFlags |= EVGPathFlattenNodeCacheFlags.Collinear;
                    continue;
                }

                float miter_length_squared = node_scope10.JoinDirection.LengthSquared();
                float inner_limit = min_segment_length * inverse_body_radius;
                if (inner_limit < 1.01f)
                {
                    inner_limit = 1.01f;
                }
                if (miter_length_squared > inner_limit * inner_limit)
                {
                    node_scope10.CacheFlags |= EVGPathFlattenNodeCacheFlags.InnerBevel;
                }

                bool is_corner = VgFlags.All(node_scope10.Flags, EVGPathFlattenNodeFlags.Corner);
                if (is_corner && (miter_length_squared > valid_miter_limit_squared || force_bevel))
                {
                    node_scope10.CacheFlags |= EVGPathFlattenNodeCacheFlags.Bevel;
                }

                bool has_bevel = VgFlags.Any(
                    node_scope10.CacheFlags,
                    EVGPathFlattenNodeCacheFlags.Bevel | EVGPathFlattenNodeCacheFlags.InnerBevel
                );
                if (has_bevel)
                {
                    ++contour.BevelCount;
                }
            }

            StrokeReserveEstimate.AddContour(
                reserve,
                contour,
                segment_count,
                options,
                RoundDivisions,
                HasAa
            );
        }

        backend.CallReserve(reserve.VertexCount, reserve.TriangleCount);

        VGMeshEmitter Emitter = new(backend);
        StrokeBuildContext build = new(Emitter,
            BodyRadius,
            AaRadius,
            RoundTolerance,
            RoundDivisions,
            HasAa);

        foreach (ref VGPathFlattenContour contour in _contours.AsSpan())
        {
            Debug.Assert(
                contour.NodeCount >= (contour.Closed ? 3u : 2u) , "VGPathFlatten.Stroke requires finalized contours");

            if (!contour.Closed)
            {
                ref VGPathFlattenNode first_node_scope16 = ref _nodes[contour.NodeBegin];
                ref VGPathFlattenNode last_segment_node = ref _nodes[contour.NodeBegin + contour.NodeCount - 2u];
                ref VGPathFlattenNode last_node = ref _nodes[contour.NodeBegin + contour.NodeCount - 1u];
                Debug.Assert(
                    first_node_scope16.NextLength > KEpsilon , "VGPathFlatten.Stroke requires finalized open segments");
                Debug.Assert(
                    last_segment_node.NextLength > KEpsilon , "VGPathFlatten.Stroke requires finalized open segments");

                Offsetf first_direction = first_node_scope16.NextDirection;
                Offsetf last_direction = last_segment_node.NextDirection;

                StrokeSectionIndices previous_scope21 = build.BuildStartCap(
                    first_node_scope16.Position,
                    first_direction,
                    options.Cap
                );

                for (uint i_scope22 = 1u; i_scope22 + 1u < contour.NodeCount; ++i_scope22)
                {
                    ref VGPathFlattenNode node_scope23 = ref _nodes[contour.NodeBegin + i_scope22];
                    ref VGPathFlattenNode previous_node_scope24 = ref _nodes[PreviousNode(contour, i_scope22)];
                    StrokeJoinSections join_scope25 = build.BuildJoin(
                        node_scope23,
                        previous_node_scope24.NextDirection,
                        node_scope23.NextDirection,
                        options.Join
                    );
                    build.ConnectSegment(previous_scope21, join_scope25.Incoming);
                    previous_scope21 = join_scope25.Outgoing;
                }

                StrokeSectionIndices end = build.BuildEndCap(
                    last_node.Position,
                    last_direction,
                    options.Cap
                );
                build.ConnectSegment(previous_scope21, end);
                continue;
            }

            ref VGPathFlattenNode first_node_scope27 = ref _nodes[contour.NodeBegin];
            ref VGPathFlattenNode tail_node = ref _nodes[contour.NodeBegin + contour.NodeCount - 1u];
            StrokeJoinSections first_join = build.BuildJoin(
                first_node_scope27,
                tail_node.NextDirection,
                first_node_scope27.NextDirection,
                options.Join
            );
            StrokeSectionIndices close_section = first_join.Incoming;
            StrokeSectionIndices previous_scope31 = first_join.Outgoing;

            for (uint i_scope32 = 1u; i_scope32 < contour.NodeCount; ++i_scope32)
            {
                ref VGPathFlattenNode node_scope33 = ref _nodes[contour.NodeBegin + i_scope32];
                ref VGPathFlattenNode previous_node_scope34 = ref _nodes[PreviousNode(contour, i_scope32)];
                StrokeJoinSections join_scope35 = build.BuildJoin(
                    node_scope33,
                    previous_node_scope34.NextDirection,
                    node_scope33.NextDirection,
                    options.Join
                );
                build.ConnectSegment(previous_scope31, join_scope35.Incoming);
                previous_scope31 = join_scope35.Outgoing;
            }

            build.ConnectSegment(previous_scope31, close_section);
        }

        return true;
    }
}
