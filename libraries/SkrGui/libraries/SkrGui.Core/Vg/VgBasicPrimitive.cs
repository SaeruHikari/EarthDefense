// Source: src/vg/vg_basic_primitive.cpp @ 611561f8.
using System.Diagnostics;
namespace SkrGui;
public struct VGBasicPrimitiveOptions { public float PixelRatio=1,TessellationFactor=1,AaRadius; public VGBasicPrimitiveOptions() {} }
internal struct VGBasicOpenStrokeSection
{
    public uint InnerAa = 0u;
    public uint InnerBody = 0u;
    public uint OuterBody = 0u;
    public uint OuterAa = 0u;
    public Offsetf InnerAaPos = Offsetf.Zero();
    public Offsetf InnerBodyPos = Offsetf.Zero();
    public Offsetf OuterBodyPos = Offsetf.Zero();
    public Offsetf OuterAaPos = Offsetf.Zero();
    public VGBasicOpenStrokeSection() {}
    public VGBasicOpenStrokeSection(uint inner_aa, uint inner_body, uint outer_body, uint outer_aa, Offsetf inner_aa_pos, Offsetf inner_body_pos, Offsetf outer_body_pos, Offsetf outer_aa_pos) { this.InnerAa=inner_aa;this.InnerBody=inner_body;this.OuterBody=outer_body;this.OuterAa=outer_aa;this.InnerAaPos=inner_aa_pos;this.InnerBodyPos=inner_body_pos;this.OuterBodyPos=outer_body_pos;this.OuterAaPos=outer_aa_pos; }
}
internal struct VGBasicClosedStrokeSection
{
    public uint InnerAa = 0u;
    public uint InnerBody = 0u;
    public uint OuterBody = 0u;
    public uint OuterAa = 0u;
    public VGBasicClosedStrokeSection() {}
    public VGBasicClosedStrokeSection(uint inner_aa, uint inner_body, uint outer_body, uint outer_aa) { this.InnerAa=inner_aa;this.InnerBody=inner_body;this.OuterBody=outer_body;this.OuterAa=outer_aa; }
}
internal struct VGBasicRectRingVertices
{
    public uint InnerTl = 0u;
    public uint InnerTr = 0u;
    public uint InnerBr = 0u;
    public uint InnerBl = 0u;
    public uint OuterTl = 0u;
    public uint OuterTr = 0u;
    public uint OuterBr = 0u;
    public uint OuterBl = 0u;
    public VGBasicRectRingVertices() {}
    public VGBasicRectRingVertices(uint inner_tl, uint inner_tr, uint inner_br, uint inner_bl, uint outer_tl, uint outer_tr, uint outer_br, uint outer_bl) { this.InnerTl=inner_tl;this.InnerTr=inner_tr;this.InnerBr=inner_br;this.InnerBl=inner_bl;this.OuterTl=outer_tl;this.OuterTr=outer_tr;this.OuterBr=outer_br;this.OuterBl=outer_bl; }
}
internal struct VGBasicRRectCorner
{
    public Offsetf Center = Offsetf.Zero();
    public Offsetf ZeroPoint = Offsetf.Zero();
    public Radius Radius = Radius.Zero();
    public float StartAngle = 0.0f;
    public float SweepAngle = 0.0f;
    public VGBasicRRectCorner() {}
    public VGBasicRRectCorner(Offsetf center, Offsetf zero_point, Radius radius, float start_angle, float sweep_angle) { this.Center=center;this.ZeroPoint=zero_point;this.Radius=radius;this.StartAngle=start_angle;this.SweepAngle=sweep_angle; }
}
internal struct VGBasicRRectPairedSection
{
    public uint Inner = 0u;
    public uint Outer = 0u;
    public uint InnerAa = 0u;
    public uint OuterAa = 0u;
    public Offsetf InnerPos = Offsetf.Zero();
    public Offsetf OuterPos = Offsetf.Zero();
    public Offsetf InnerAaPos = Offsetf.Zero();
    public Offsetf OuterAaPos = Offsetf.Zero();
    public VGBasicRRectPairedSection() {}
    public VGBasicRRectPairedSection(uint inner, uint outer, uint inner_aa, uint outer_aa, Offsetf inner_pos, Offsetf outer_pos, Offsetf inner_aa_pos, Offsetf outer_aa_pos) { this.Inner=inner;this.Outer=outer;this.InnerAa=inner_aa;this.OuterAa=outer_aa;this.InnerPos=inner_pos;this.OuterPos=outer_pos;this.InnerAaPos=inner_aa_pos;this.OuterAaPos=outer_aa_pos; }
}
internal struct VGBasicRRectBoundaryRange
{
    public uint StartIndex = 0u;
    public uint EndIndex = 0u;
    public uint Count = 0u;
    public bool Wraps = false;
    public VGBasicRRectBoundaryRange() {}
    public VGBasicRRectBoundaryRange(uint start_index, uint end_index, uint count, bool wraps) { this.StartIndex=start_index;this.EndIndex=end_index;this.Count=count;this.Wraps=wraps; }
}
internal struct VGBasicRRectBoundaryRecord
{
    public RRect Value = new();
    public float ArcTolerance = 1.0f;
    public float EllipticalTolerance = 1.0f;
    public VGBasicRRectBoundaryRange[] Ranges = new VGBasicRRectBoundaryRange[(int)EVGBasicRRectRange.Count];
    public VGBasicRRectBoundaryRecord() {}
}
internal struct VGBasicRRectBoundaryPoint
{
    public EVGBasicRRectRange Range = EVGBasicRRectRange.TLCorner;
    public uint PointIndex = 0u;
    public VGBasicRRectBoundaryPoint() {}
    public VGBasicRRectBoundaryPoint(EVGBasicRRectRange range, uint point_index) { this.Range=range;this.PointIndex=point_index; }
}
internal struct VGBasicDiffRectGridVertex
{
    public Offsetf Position = Offsetf.Zero();
    public uint Index = 0u;
    public VGBasicDiffRectGridVertex() {}
    public VGBasicDiffRectGridVertex(Offsetf position, uint index) { this.Position=position;this.Index=index; }
}
internal struct VGBasicDiffRectGridCell
{
    public uint Tl = 0u;
    public uint Tr = 0u;
    public uint Br = 0u;
    public uint Bl = 0u;
    public bool Filled = false;
    public VGBasicDiffRectGridCell() {}
    public VGBasicDiffRectGridCell(uint tl, uint tr, uint br, uint bl, bool filled) { this.Tl=tl;this.Tr=tr;this.Br=br;this.Bl=bl;this.Filled=filled; }
}
internal struct VGBasicDiffRectGridEdge
{
    public uint From = 0u;
    public uint To = 0u;
    public bool Used = false;
    public VGBasicDiffRectGridEdge() {}
    public VGBasicDiffRectGridEdge(uint from, uint to, bool used) { this.From=from;this.To=to;this.Used=used; }
}
internal enum EVGBasicRRectRange : byte { TLCorner,TopEdge,TRCorner,RightEdge,BRCorner,BottomEdge,BLCorner,LeftEdge,Count }
internal static class VGBasicOpenPrimitiveHelper
{
    // Original line 37: push_stroke_section_aa.
    public static void PushStrokeSectionAa(VGMeshEmitter emitter, ref VGBasicOpenStrokeSection section, Offsetf cap_offset, bool has_inner_aa)
    {



            if (has_inner_aa)
            {
                section.InnerAa = emitter.PushVertex(section.InnerAaPos + cap_offset, 0.0f);
            }

            section.OuterAa = emitter.PushVertex(section.OuterAaPos + cap_offset, 0.0f);
        }
    // Original line 55: connect_stroke_sections.
    public static void ConnectStrokeSections(VGMeshEmitter emitter, VGBasicOpenStrokeSection previous, VGBasicOpenStrokeSection current, bool has_aa, bool has_inner_aa)
    {


            if (has_aa)
            {
                emitter.PushQuad(
                    previous.OuterBody,
                    previous.OuterAa,
                    current.OuterAa,
                    current.OuterBody
                );
                if (has_inner_aa)
                {
                    emitter.PushQuad(
                        previous.InnerAa,
                        previous.InnerBody,
                        current.InnerBody,
                        current.InnerAa
                    );
                }
            }

            emitter.PushQuad(
                previous.InnerBody,
                previous.OuterBody,
                current.OuterBody,
                current.InnerBody
            );
        }
    // Original line 92: push_flat_cap_aa.
    public static void PushFlatCapAa(VGMeshEmitter emitter, VGBasicOpenStrokeSection section, bool start_cap, bool has_inner_aa)
    {



            if (has_inner_aa)
            {
                if (start_cap)
                {
                    emitter.PushQuad(section.InnerAa, section.InnerBody, section.OuterBody, section.OuterAa);
                }
                else
                {
                    emitter.PushQuad(section.InnerBody, section.InnerAa, section.OuterAa, section.OuterBody);
                }
                return;
            }

            if (start_cap)
            {
                emitter.PushTriangle(section.InnerBody, section.OuterBody, section.OuterAa);
            }
            else
            {
                emitter.PushTriangle(section.InnerBody, section.OuterAa, section.OuterBody);
            }
        }
}
internal static class VGBasicClosedPrimitiveHelper
{
    // Original line 128: connect_stroke_sections.
    public static void ConnectStrokeSections(VGMeshEmitter emitter, VGBasicClosedStrokeSection previous, VGBasicClosedStrokeSection current, bool has_aa, bool has_inner_aa)
    {



            if (has_aa)
            {
                emitter.PushQuad(
                    previous.OuterBody,
                    previous.OuterAa,
                    current.OuterAa,
                    current.OuterBody
                );
                if (has_inner_aa)
                {
                    emitter.PushQuad(
                        previous.InnerAa,
                        previous.InnerBody,
                        current.InnerBody,
                        current.InnerAa
                    );
                }
            }

            emitter.PushQuad(
                previous.InnerBody,
                previous.OuterBody,
                current.OuterBody,
                current.InnerBody
            );
        }
}
internal static class VGBasicRectRingHelper
{
    // Original line 181: push_body_vertices.
    public static VGBasicRectRingVertices PushBodyVertices(VGMeshEmitter emitter, Rectf outer, Rectf inner)
    {
            return new VGBasicRectRingVertices(
                emitter.PushVertex(inner.TopLeft(), 1.0f),
                emitter.PushVertex(inner.TopRight(), 1.0f),
                emitter.PushVertex(inner.BottomRight(), 1.0f),
                emitter.PushVertex(inner.BottomLeft(), 1.0f),
                emitter.PushVertex(outer.TopLeft(), 1.0f),
                emitter.PushVertex(outer.TopRight(), 1.0f),
                emitter.PushVertex(outer.BottomRight(), 1.0f),
                emitter.PushVertex(outer.BottomLeft(), 1.0f));
        }
    // Original line 199: push_body_quads.
    public static void PushBodyQuads(VGMeshEmitter emitter, VGBasicRectRingVertices vertices)
    {
            emitter.PushQuad(vertices.OuterTl, vertices.OuterTr, vertices.InnerTr, vertices.InnerTl);
            emitter.PushQuad(vertices.InnerTr, vertices.OuterTr, vertices.OuterBr, vertices.InnerBr);
            emitter.PushQuad(vertices.InnerBr, vertices.OuterBr, vertices.OuterBl, vertices.InnerBl);
            emitter.PushQuad(vertices.OuterBl, vertices.OuterTl, vertices.InnerTl, vertices.InnerBl);
        }
    // Original line 207: emit_fixed_ring.
    public static void EmitFixedRing(VGMeshEmitter emitter, Rectf outer_body, Rectf inner_body, Rectf outer_aa, Rectf inner_aa, bool has_aa, bool has_inner_aa)
    {


            VGBasicRectRingVertices vertices = PushBodyVertices(emitter, outer_body, inner_body);
            if (!has_aa)
            {
                PushBodyQuads(emitter, vertices);
                return;
            }

            uint outer_aa_tl = emitter.PushVertex(outer_aa.TopLeft(), 0.0f);
            uint outer_aa_tr = emitter.PushVertex(outer_aa.TopRight(), 0.0f);
            uint outer_aa_br = emitter.PushVertex(outer_aa.BottomRight(), 0.0f);
            uint outer_aa_bl = emitter.PushVertex(outer_aa.BottomLeft(), 0.0f);
            emitter.PushQuad(outer_aa_tl, outer_aa_tr, vertices.OuterTr, vertices.OuterTl);
            emitter.PushQuad(vertices.OuterTr, outer_aa_tr, outer_aa_br, vertices.OuterBr);
            emitter.PushQuad(vertices.OuterBr, outer_aa_br, outer_aa_bl, vertices.OuterBl);
            emitter.PushQuad(outer_aa_bl, outer_aa_tl, vertices.OuterTl, vertices.OuterBl);

            if (has_inner_aa)
            {
                uint inner_aa_tl = emitter.PushVertex(inner_aa.TopLeft(), 0.0f);
                uint inner_aa_tr = emitter.PushVertex(inner_aa.TopRight(), 0.0f);
                uint inner_aa_br = emitter.PushVertex(inner_aa.BottomRight(), 0.0f);
                uint inner_aa_bl = emitter.PushVertex(inner_aa.BottomLeft(), 0.0f);
                emitter.PushQuad(inner_aa_tl, vertices.InnerTl, vertices.InnerTr, inner_aa_tr);
                emitter.PushQuad(inner_aa_tr, vertices.InnerTr, vertices.InnerBr, inner_aa_br);
                emitter.PushQuad(inner_aa_br, vertices.InnerBr, vertices.InnerBl, inner_aa_bl);
                emitter.PushQuad(inner_aa_bl, vertices.InnerBl, vertices.InnerTl, inner_aa_tl);
            }

            PushBodyQuads(emitter, vertices);
        }
}
internal static class VGBasicDiffRectGridHelper
{
    public const float k_same_epsilon = 0.0001f;
    public const uint k_max_vertices = 16u;
    public const uint k_max_cells = 9u;
    public const uint k_max_edges = 32u;
    // Original line 336: is_positive_span.
    public static bool IsPositiveSpan(float value)
    {
            return value > k_same_epsilon;
        }
    // Original line 341: is_collinear.
    public static bool IsCollinear(Offsetf previous, Offsetf current, Offsetf next)
    {
            Offsetf in_delta = current - previous;
            Offsetf out_delta = next - current;
            return MathF.Abs(in_delta.Cross(out_delta)) <= k_same_epsilon;
        }
    // Original line 348: cell_index.
    public static uint CellIndex(uint x, uint y)
    {
            return y * 3u + x;
        }
    // Original line 353: is_filled_cell.
    public static bool IsFilledCell(VGBasicDiffRectGridCell[] cells, int x, int y)
    {
            if (x < 0 || y < 0 || x >= 3 || y >= 3)
            {
                return false;
            }
            return cells[CellIndex((uint)(x), (uint)(y))].Filled;
        }
    // Original line 362: find_or_push_vertex.
    public static uint FindOrPushVertex(VGMeshEmitter emitter, VGBasicDiffRectGridVertex[] vertices, ref uint vertex_count, Offsetf position)
    {
            for (uint i = 0u; i < vertex_count; ++i)
            {
                if (vertices[i].Position.NearlyEqual(position, k_same_epsilon))
                {
                    return i;
                }
            }

            Debug.Assert(vertex_count < k_max_vertices , "diff_rect grid vertex buffer overflow");
            uint slot = vertex_count++;
            vertices[slot].Position = position;
            vertices[slot].Index = emitter.PushVertex(position, 1.0f);
            return slot;
        }
    // Original line 384: push_edge.
    public static void PushEdge(VGBasicDiffRectGridEdge[] edges, ref uint edge_count, uint from, uint to)
    {
            if (from == to)
            {
                return;
            }

            Debug.Assert(edge_count < k_max_edges , "diff_rect grid edge buffer overflow");
            edges[edge_count++] = new VGBasicDiffRectGridEdge(from,
                to,
                false);
        }
    // Original line 404: find_unused_edge_from.
    public static int FindUnusedEdgeFrom(VGBasicDiffRectGridEdge[] edges, uint edge_count, uint from)
    {
            for (uint i = 0u; i < edge_count; ++i)
            {
                if (!edges[i].Used && edges[i].From == from)
                {
                    return (int)(i);
                }
            }
            return -1;
        }
    // Original line 420: compress_contour.
    public static uint CompressContour(VGBasicDiffRectGridVertex[] vertices, uint[] input, uint input_count, uint[] output)
    {
            if (input_count <= 2u)
            {
                for (uint i_scope0 = 0u; i_scope0 < input_count; ++i_scope0)
                {
                    output[i_scope0] = input[i_scope0];
                }
                return input_count;
            }

            uint output_count = 0u;
            for (uint i_scope2 = 0u; i_scope2 < input_count; ++i_scope2)
            {
                uint previous_i = i_scope2 == 0u ? input_count - 1u : i_scope2 - 1u;
                uint next_i = i_scope2 + 1u == input_count ? 0u : i_scope2 + 1u;
                Offsetf previous = vertices[input[previous_i]].Position;
                Offsetf current = vertices[input[i_scope2]].Position;
                Offsetf next = vertices[input[next_i]].Position;
                if (IsCollinear(previous, current, next))
                {
                    continue;
                }

                output[output_count++] = input[i_scope2];
            }
            return output_count;
        }
    // Original line 454: emit_aa_contours.
    public static void EmitAaContours(VGMeshEmitter emitter, VGBasicDiffRectGridVertex[] vertices, VGBasicDiffRectGridEdge[] edges, uint edge_count, float aa_radius)
    {
            if (aa_radius <= 0.0f)
            {
                return;
            }

            for (uint edge_i = 0u; edge_i < edge_count; ++edge_i)
            {
                if (edges[edge_i].Used)
                {
                    continue;
                }

                uint[] contour = new uint[k_max_edges];
                uint contour_count = 0u;
                uint start_vertex = edges[edge_i].From;
                int current_edge_i = (int)(edge_i);
                for (uint guard = 0u; guard < edge_count && current_edge_i >= 0; ++guard)
                {
                    ref VGBasicDiffRectGridEdge edge = ref edges[(uint)(current_edge_i)];
                    edge.Used = true;
                    contour[contour_count++] = edge.From;

                    if (edge.To == start_vertex)
                    {
                        break;
                    }

                    current_edge_i = FindUnusedEdgeFrom(edges, edge_count, edge.To);
                }

                uint[] compressed = new uint[k_max_edges];
                uint compressed_count = CompressContour(vertices, contour, contour_count, compressed);
                if (compressed_count < 3u)
                {
                    continue;
                }

                VGFillAAHelper.EmitIndexedRing(
                    emitter,
                    compressed_count,
                    (uint point_index) => {
                        return vertices[compressed[point_index]].Position;
                    },
                    (uint point_index) => {
                        return vertices[compressed[point_index]].Index;
                    },
                    aa_radius
                );
            }
        }
    // Original line 518: emit.
    public static void Emit(VGMeshEmitter emitter, Rectf outer, Rectf hole, float aa_radius)
    {
            float[] xs = {
                outer.Left,
                hole.Left,
                hole.Right,
                outer.Right,
            };
            float[] ys = {
                outer.Top,
                hole.Top,
                hole.Bottom,
                outer.Bottom,
            };

            VGBasicDiffRectGridVertex[] vertices = new VGBasicDiffRectGridVertex[k_max_vertices];
            VGBasicDiffRectGridCell[] cells = new VGBasicDiffRectGridCell[k_max_cells];
            VGBasicDiffRectGridEdge[] edges = new VGBasicDiffRectGridEdge[k_max_edges];
            uint vertex_count = 0u;
            uint edge_count = 0u;



            for (uint y_scope2 = 0u; y_scope2 < 3u; ++y_scope2)
            {
                for (uint x_scope3 = 0u; x_scope3 < 3u; ++x_scope3)
                {
                    if (x_scope3 == 1u && y_scope2 == 1u)
                    {
                        continue;
                    }
                    if (!IsPositiveSpan(xs[x_scope3 + 1u] - xs[x_scope3]) || !IsPositiveSpan(ys[y_scope2 + 1u] - ys[y_scope2]))
                    {
                        continue;
                    }

                    ref VGBasicDiffRectGridCell cell_scope4 = ref cells[CellIndex(x_scope3, y_scope2)];
                    cell_scope4.Tl = FindOrPushVertex(emitter, vertices, ref vertex_count, new Offsetf(xs[x_scope3], ys[y_scope2]));
                    cell_scope4.Tr = FindOrPushVertex(emitter, vertices, ref vertex_count, new Offsetf(xs[x_scope3 + 1u], ys[y_scope2]));
                    cell_scope4.Br = FindOrPushVertex(emitter, vertices, ref vertex_count, new Offsetf(xs[x_scope3 + 1u], ys[y_scope2 + 1u]));
                    cell_scope4.Bl = FindOrPushVertex(emitter, vertices, ref vertex_count, new Offsetf(xs[x_scope3], ys[y_scope2 + 1u]));
                    cell_scope4.Filled = true;
                }
            }



            for (uint y_scope5 = 0u; y_scope5 < 3u; ++y_scope5)
            {
                for (uint x_scope6 = 0u; x_scope6 < 3u; ++x_scope6)
                {
                    ref VGBasicDiffRectGridCell cell_scope7 = ref cells[CellIndex(x_scope6, y_scope5)];
                    if (!cell_scope7.Filled)
                    {
                        continue;
                    }

                    int ix = (int)(x_scope6);
                    int iy = (int)(y_scope5);
                    if (!IsFilledCell(cells, ix, iy - 1))
                    {
                        PushEdge(edges, ref edge_count, cell_scope7.Tl, cell_scope7.Tr);
                    }
                    if (!IsFilledCell(cells, ix + 1, iy))
                    {
                        PushEdge(edges, ref edge_count, cell_scope7.Tr, cell_scope7.Br);
                    }
                    if (!IsFilledCell(cells, ix, iy + 1))
                    {
                        PushEdge(edges, ref edge_count, cell_scope7.Br, cell_scope7.Bl);
                    }
                    if (!IsFilledCell(cells, ix - 1, iy))
                    {
                        PushEdge(edges, ref edge_count, cell_scope7.Bl, cell_scope7.Tl);
                    }
                }
            }

            EmitAaContours(emitter, vertices, edges, edge_count, aa_radius);



            for (uint cell_i = 0u; cell_i < k_max_cells; ++cell_i)
            {
                ref VGBasicDiffRectGridCell cell_scope9 = ref cells[cell_i];
                if (!cell_scope9.Filled)
                {
                    continue;
                }

                emitter.PushQuad(
                    vertices[cell_scope9.Tl].Index,
                    vertices[cell_scope9.Tr].Index,
                    vertices[cell_scope9.Br].Index,
                    vertices[cell_scope9.Bl].Index
                );
            }
        }
}
internal static class VGBasicRRectHelper
{
    public const float k_same_epsilon = 0.0001f;
    // Original line 627: positive_radius.
    public static Radius PositiveRadius(Radius radius)
    {
            if (radius.X <= 0.0f || radius.Y <= 0.0f)
            {
                return Radius.Zero();
            }
            return radius;
        }
    // Original line 636: has_radius.
    public static bool HasRadius(Radius radius)
    {
            return radius.X > 0.0f && radius.Y > 0.0f;
        }
    // Original line 641: normalized.
    public static RRect Normalized(RRect value)
    {
            return new RRect(value.Left, value.Top, value.Right, value.Bottom, PositiveRadius(value.TlRadius), PositiveRadius(value.TrRadius), PositiveRadius(value.BrRadius), PositiveRadius(value.BlRadius))
                .ScaleRadii();
        }
    // Original line 656: has_any_radius.
    public static bool HasAnyRadius(RRect value)
    {
            return HasRadius(value.TlRadius) ||
                HasRadius(value.TrRadius) ||
                HasRadius(value.BrRadius) ||
                HasRadius(value.BlRadius);
        }
    // Original line 664: is_uniform_radius.
    public static bool IsUniformRadius(RRect value)
    {
            return value.TlRadius == value.TrRadius &&
                value.TrRadius == value.BrRadius &&
                value.BrRadius == value.BlRadius;
        }
    // Original line 671: is_full_ellipse.
    public static bool IsFullEllipse(RRect value)
    {
            if (!IsUniformRadius(value) || !HasRadius(value.TlRadius))
            {
                return false;
            }
            return value.TlRadius.X * 2.0f == value.Width() &&
                value.TlRadius.Y * 2.0f == value.Height();
        }
    // Original line 681: corner_tl.
    public static VGBasicRRectCorner CornerTl(RRect value)
    {
            return new VGBasicRRectCorner(
                new Offsetf(value.Left + value.TlRadius.X, value.Top + value.TlRadius.Y),
                new Offsetf(value.Left, value.Top),
                value.TlRadius,
                MathF.PI,
                (MathF.PI/2));
        }
    // Original line 692: corner_tr.
    public static VGBasicRRectCorner CornerTr(RRect value)
    {
            return new VGBasicRRectCorner(
                new Offsetf(value.Right - value.TrRadius.X, value.Top + value.TrRadius.Y),
                new Offsetf(value.Right, value.Top),
                value.TrRadius,
                -(MathF.PI/2),
                (MathF.PI/2));
        }
    // Original line 703: corner_br.
    public static VGBasicRRectCorner CornerBr(RRect value)
    {
            return new VGBasicRRectCorner(
                new Offsetf(value.Right - value.BrRadius.X, value.Bottom - value.BrRadius.Y),
                new Offsetf(value.Right, value.Bottom),
                value.BrRadius,
                0.0f,
                (MathF.PI/2));
        }
    // Original line 714: corner_bl.
    public static VGBasicRRectCorner CornerBl(RRect value)
    {
            return new VGBasicRRectCorner(
                new Offsetf(value.Left + value.BlRadius.X, value.Bottom - value.BlRadius.Y),
                new Offsetf(value.Left, value.Bottom),
                value.BlRadius,
                (MathF.PI/2),
                (MathF.PI/2));
        }
    // Original line 725: corner_point.
    public static Offsetf CornerPoint(VGBasicRRectCorner corner, float t, float radius_delta)
    {
            if (!HasRadius(corner.Radius))
            {
                return corner.ZeroPoint;
            }

            float angle = corner.StartAngle + corner.SweepAngle * t;
            if (corner.Radius.X == corner.Radius.Y)
            {
                CircleSampler sampler_scope1 = new(angle);
                return sampler_scope1.SamplePoint(
                    corner.Center,
                    CppMath.Max(0.0f, corner.Radius.X + radius_delta)
                );
            }

            EllipseSampler sampler_scope2 = new(angle, 0.0f);
            return sampler_scope2.SamplePoint(
                corner.Center,
                CppMath.Max(0.0f, corner.Radius.X + radius_delta),
                CppMath.Max(0.0f, corner.Radius.Y + radius_delta)
            );
        }
    // Original line 754: is_circle_radius.
    public static bool IsCircleRadius(Radius radius)
    {
            return radius.X == radius.Y;
        }
    // Original line 759: corner_sample_desc.
    public static ShapeToleranceSampleDesc CornerSampleDesc(float tolerance)
    {
            return new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=10u};
        }
    // Original line 769: sample_corner.
    public static void SampleCorner(VGBasicRRectCorner corner, float arc_tolerance, float elliptical_tolerance, Action<Offsetf, float> func)
    {
            if (!HasRadius(corner.Radius))
            {
                func(corner.ZeroPoint, 0.0f);
                return;
            }




            if (IsCircleRadius(corner.Radius))
            {
                Arc arc_scope0 = global::SkrGui.Arc.CenterRadius(
                    corner.Center,
                    corner.Radius.X,
                    corner.StartAngle,
                    corner.SweepAngle
                );
                arc_scope0.SampleWithSampler(
                    CornerSampleDesc(arc_tolerance),
                    (Offsetf point, float t, CircleSampler _unused2) => {
                        func(point, t);
                    }
                );
                return;
            }

            EllipticalArc arc_scope1 = global::SkrGui.EllipticalArc.CenterRadius(
                corner.Center,
                corner.Radius.X,
                corner.Radius.Y,
                0.0f,
                corner.StartAngle,
                corner.SweepAngle
            );
            arc_scope1.SampleWithSampler(
                CornerSampleDesc(elliptical_tolerance),
                (Offsetf point, float t, EllipseSampler _unused2) => {
                    func(point, t);
                }
            );
        }
    // Original line 818: sampled_corner_point_at.
    public static Offsetf SampledCornerPointAt(VGBasicRRectCorner corner, uint point_index, float arc_tolerance, float elliptical_tolerance)
    {
            Offsetf result = CornerPoint(corner, 1.0f, 0.0f);
            uint current_index = 0u;
            SampleCorner(corner, arc_tolerance, elliptical_tolerance, (Offsetf point, float _unused1) => {
                    if (current_index == point_index)
                    {
                        result = point;
                    }
                    ++current_index;
                });
            return result;
        }
    // Original line 842: find_next_corner_sample_t.
    public static bool FindNextCornerSampleT(VGBasicRRectCorner corner, float arc_tolerance, float elliptical_tolerance, float after_t, bool include_end, ref float out_t)
    {
            bool found = false;
            float best_t = 2.0f;
            SampleCorner(corner, arc_tolerance, elliptical_tolerance, (Offsetf _unused0, float t) => {
                    if (t <= after_t + k_same_epsilon)
                    {
                        return;
                    }
                    if (!include_end && t >= 1.0f - k_same_epsilon)
                    {
                        return;
                    }
                    if (t < best_t)
                    {
                        best_t = t;
                        found = true;
                    }
                });

            if (found)
            {
                out_t = best_t;
            }
            return found;
        }
    // Original line 881: corner_segment_count.
    public static uint CornerSegmentCount(Radius radius, float arc_tolerance, float elliptical_tolerance)
    {
            if (!HasRadius(radius))
            {
                return 1u;
            }

            if (IsCircleRadius(radius))
            {
                return CppMath.Max(
                    1u,
                    global::SkrGui.Arc.CenterRadius(
                        Offsetf.Zero(),
                        radius.X,
                        0.0f,
                        (MathF.PI/2)
                    )
                        .EstimateSegmentCount(arc_tolerance)
                );
            }

            return CppMath.Max(
                1u,
                global::SkrGui.EllipticalArc.CenterRadius(
                    Offsetf.Zero(),
                    radius.X,
                    radius.Y,
                    0.0f,
                    0.0f,
                    (MathF.PI/2)
                )
                    .EstimateSegmentCount(elliptical_tolerance)
            );
        }
    // Original line 920: estimate_boundary_section_count.
    public static ulong EstimateBoundarySectionCount(RRect value, float arc_tolerance, float elliptical_tolerance)
    {
            ulong section_count = 4u +
                CornerSegmentCount(value.TlRadius, arc_tolerance, elliptical_tolerance) +
                CornerSegmentCount(value.TrRadius, arc_tolerance, elliptical_tolerance) +
                CornerSegmentCount(value.BrRadius, arc_tolerance, elliptical_tolerance) +
                CornerSegmentCount(value.BlRadius, arc_tolerance, elliptical_tolerance);

            return section_count > 0u ? section_count - 1u : 0u;
        }
    // Original line 935: range_index.
    public static uint RangeIndex(EVGBasicRRectRange range)
    {
            return (uint)(range);
        }
    // Original line 940: is_corner_range.
    public static bool IsCornerRange(EVGBasicRRectRange range)
    {
            return range == EVGBasicRRectRange.TLCorner ||
                range == EVGBasicRRectRange.TRCorner ||
                range == EVGBasicRRectRange.BRCorner ||
                range == EVGBasicRRectRange.BLCorner;
        }
    // Original line 948: corner_for_range.
    public static VGBasicRRectCorner CornerForRange(RRect value, EVGBasicRRectRange range)
    {
            switch (range)
            {
            case EVGBasicRRectRange.TLCorner:
                return CornerTl(value);
            case EVGBasicRRectRange.TRCorner:
                return CornerTr(value);
            case EVGBasicRRectRange.BRCorner:
                return CornerBr(value);
            case EVGBasicRRectRange.BLCorner:
                return CornerBl(value);
            default:
                return new VGBasicRRectCorner();
            }
        }
    // Original line 965: edge_start_point.
    public static Offsetf EdgeStartPoint(RRect value, EVGBasicRRectRange range)
    {
            switch (range)
            {
            case EVGBasicRRectRange.TopEdge:
                return new Offsetf(value.Left + value.TlRadius.X, value.Top);
            case EVGBasicRRectRange.RightEdge:
                return new Offsetf(value.Right, value.Top + value.TrRadius.Y);
            case EVGBasicRRectRange.BottomEdge:
                return new Offsetf(value.Right - value.BrRadius.X, value.Bottom);
            case EVGBasicRRectRange.LeftEdge:
                return new Offsetf(value.Left, value.Bottom - value.BlRadius.Y);
            default:
                return Offsetf.Zero();
            }
        }
    // Original line 982: edge_end_point.
    public static Offsetf EdgeEndPoint(RRect value, EVGBasicRRectRange range)
    {
            switch (range)
            {
            case EVGBasicRRectRange.TopEdge:
                return new Offsetf(value.Right - value.TrRadius.X, value.Top);
            case EVGBasicRRectRange.RightEdge:
                return new Offsetf(value.Right, value.Bottom - value.BrRadius.Y);
            case EVGBasicRRectRange.BottomEdge:
                return new Offsetf(value.Left + value.BlRadius.X, value.Bottom);
            case EVGBasicRRectRange.LeftEdge:
                return new Offsetf(value.Left, value.Top + value.TlRadius.Y);
            default:
                return Offsetf.Zero();
            }
        }
    // Original line 999: edge_range_count.
    public static uint EdgeRangeCount(RRect value, EVGBasicRRectRange range)
    {
            return EdgeStartPoint(value, range).NearlyEqual(EdgeEndPoint(value, range), k_same_epsilon) ? 1u : 2u;
        }
    // Original line 1004: range_point.
    public static Offsetf RangePoint(RRect value, EVGBasicRRectRange range, uint point_index, uint point_count, float radius_delta)
    {
            if (IsCornerRange(range))
            {
                VGBasicRRectCorner corner = CornerForRange(value, range);
                if (point_count <= 1u)
                {
                    return CornerPoint(corner, 0.0f, radius_delta);
                }

                float t =
                    (float)(point_index) /
                    (float)(point_count - 1u);
                return CornerPoint(corner, t, radius_delta);
            }

            if (point_count <= 1u || point_index == 0u)
            {
                return EdgeStartPoint(value, range);
            }
            return EdgeEndPoint(value, range);
        }
    // Original line 1033: body_index_at.
    public static uint BodyIndexAt(VGBasicRRectBoundaryRecord record, EVGBasicRRectRange range, uint point_index)
    {
            ref VGBasicRRectBoundaryRange body_range = ref record.Ranges[RangeIndex(range)];
            if (body_range.Count <= 1u || point_index == 0u)
            {
                return body_range.StartIndex;
            }
            if (body_range.Wraps && point_index + 1u == body_range.Count)
            {
                return body_range.EndIndex;
            }
            return body_range.StartIndex + point_index;
        }
    // Original line 1051: boundary_contour_point_count.
    public static uint BoundaryContourPointCount(VGBasicRRectBoundaryRecord record)
    {



            uint count = 0u;
            for (uint range_i = 0u; range_i < RangeIndex(EVGBasicRRectRange.LeftEdge); ++range_i)
            {
                ref VGBasicRRectBoundaryRange range = ref record.Ranges[range_i];
                uint first_point = range_i == 0u ? 0u : 1u;
                if (range.Count > first_point)
                {
                    uint range_count = range.Count - first_point;
                    if (range.Wraps && range_count > 0u)
                    {
                        --range_count;
                    }
                    count += range_count;
                }
            }
            return count;
        }
    // Original line 1074: boundary_contour_point_at.
    public static VGBasicRRectBoundaryPoint BoundaryContourPointAt(VGBasicRRectBoundaryRecord record, uint point_index)
    {


            for (uint range_i = 0u; range_i < RangeIndex(EVGBasicRRectRange.LeftEdge); ++range_i)
            {
                ref VGBasicRRectBoundaryRange range = ref record.Ranges[range_i];
                uint first_point = range_i == 0u ? 0u : 1u;
                uint range_point_count = range.Count > first_point ? range.Count - first_point : 0u;
                if (range.Wraps && range_point_count > 0u)
                {
                    --range_point_count;
                }
                if (point_index < range_point_count)
                {
                    return new VGBasicRRectBoundaryPoint(
                        (EVGBasicRRectRange)(range_i),
                        first_point + point_index);
                }
                point_index -= range_point_count;
            }

            Debug.Assert(false , "VGBasicRRectHelper.boundary_contour_point_at out of range");
            return new VGBasicRRectBoundaryPoint();
        }
    // Original line 1104: boundary_body_index_at.
    public static uint BoundaryBodyIndexAt(VGBasicRRectBoundaryRecord record, uint point_index)
    {
            VGBasicRRectBoundaryPoint point = BoundaryContourPointAt(record, point_index);
            return BodyIndexAt(record, point.Range, point.PointIndex);
        }
    // Original line 1113: boundary_position_at.
    public static Offsetf BoundaryPositionAt(VGBasicRRectBoundaryRecord record, uint point_index)
    {
            VGBasicRRectBoundaryPoint point = BoundaryContourPointAt(record, point_index);
            if (IsCornerRange(point.Range))
            {
                return SampledCornerPointAt(CornerForRange(record.Value, point.Range), point.PointIndex, record.ArcTolerance, record.EllipticalTolerance);
            }

            ref VGBasicRRectBoundaryRange range = ref record.Ranges[RangeIndex(point.Range)];
            return RangePoint(record.Value, point.Range, point.PointIndex, range.Count, 0.0f);
        }
    // Original line 1133: nearest_point_index.
    public static uint NearestPointIndex(uint source_count, uint target_index, uint target_count)
    {


            if (source_count <= 1u || target_count <= 1u)
            {
                return 0u;
            }

            ulong numerator =
                (ulong)(target_index) * (ulong)(source_count - 1u) +
                (ulong)(target_count - 1u) / 2u;
            return (uint)(numerator / (ulong)(target_count - 1u));
        }
    // Original line 1148: push_triangle_if_valid.
    public static void PushTriangleIfValid(VGMeshEmitter emitter, uint i0, uint i1, uint i2)
    {
            if (i0 == i1 || i1 == i2 || i2 == i0)
            {
                return;
            }

            emitter.PushTriangle(i0, i1, i2);
        }
    // Original line 1158: push_quad_or_triangle.
    public static void PushQuadOrTriangle(VGMeshEmitter emitter, uint i0, uint i1, uint i2, uint i3)
    {
            if (i0 == i1 && i2 == i3)
            {
                return;
            }
            if (i0 == i1)
            {
                PushTriangleIfValid(emitter, i0, i2, i3);
                return;
            }
            if (i1 == i2)
            {
                PushTriangleIfValid(emitter, i0, i1, i3);
                return;
            }
            if (i2 == i3)
            {
                PushTriangleIfValid(emitter, i0, i1, i2);
                return;
            }
            if (i3 == i0)
            {
                PushTriangleIfValid(emitter, i0, i1, i2);
                return;
            }

            emitter.PushQuad(i0, i1, i2, i3);
        }
    // Original line 1194: emit_aa_ring.
    public static void EmitAaRing(VGMeshEmitter emitter, VGBasicRRectBoundaryRecord record, float aa_radius, bool reverse)
    {
            uint count = BoundaryContourPointCount(record);
            var remap = (uint point_index) => {
                return reverse ? count - 1u - point_index : point_index;
            };




            VGFillAAHelper.EmitIndexedRing(
                emitter,
                count,
                (uint point_index) => {
                    return BoundaryPositionAt(record, remap(point_index));
                },
                (uint point_index) => {
                    return BoundaryBodyIndexAt(record, remap(point_index));
                },
                aa_radius
            );
        }
    // Original line 1222: emit_boundary_record.
    public static VGBasicRRectBoundaryRecord EmitBoundaryRecord(VGMeshEmitter emitter, RRect value, float aa_radius, float arc_tolerance, float elliptical_tolerance, bool has_aa, bool reverse_aa)
    {
            VGBasicRRectBoundaryRecord record = new();
            record.Value = value;
            record.ArcTolerance = arc_tolerance;
            record.EllipticalTolerance = elliptical_tolerance;




            Offsetf first_position =
                CornerPoint(CornerForRange(value, EVGBasicRRectRange.TLCorner), 0.0f, 0.0f);
            uint first_index = emitter.PushVertex(first_position, 1.0f);
            uint previous_index = first_index;
            for (uint range_i = 0u; range_i < RangeIndex(EVGBasicRRectRange.Count); ++range_i)
            {
                EVGBasicRRectRange range = (EVGBasicRRectRange)(range_i);

                record.Ranges[range_i].StartIndex = previous_index;

                if (IsCornerRange(range))
                {



                    SampleCorner(CornerForRange(value, range), arc_tolerance, elliptical_tolerance, (Offsetf point, float _unused1) => {
                            uint point_i_scope5 = record.Ranges[range_i].Count++;
                            if (point_i_scope5 == 0u)
                            {
                                return;
                            }

                            if (point.NearlyEqual(first_position, k_same_epsilon))
                            {
                                previous_index = first_index;
                                record.Ranges[range_i].Wraps = true;
                                return;
                            }

                            previous_index = emitter.PushVertex(point, 1.0f);
                        });
                    record.Ranges[range_i].EndIndex = previous_index;
                    continue;
                }

                record.Ranges[range_i].Count = EdgeRangeCount(value, range);

                if (range == EVGBasicRRectRange.LeftEdge)
                {
                    record.Ranges[range_i].EndIndex = first_index;
                    record.Ranges[range_i].Wraps = record.Ranges[range_i].Count > 1u;
                    previous_index = first_index;
                    continue;
                }

                for (uint point_i_scope6 = 1u; point_i_scope6 < record.Ranges[range_i].Count; ++point_i_scope6)
                {
                    Offsetf point = RangePoint(value, range, point_i_scope6, record.Ranges[range_i].Count, 0.0f);
                    if (point.NearlyEqual(first_position, k_same_epsilon))
                    {
                        previous_index = first_index;
                        record.Ranges[range_i].Wraps = true;
                        continue;
                    }

                    previous_index = emitter.PushVertex(point, 1.0f);
                }
                record.Ranges[range_i].EndIndex = previous_index;
            }

            if (has_aa)
            {
                EmitAaRing(emitter, record, aa_radius, reverse_aa);
            }

            return record;
        }
    // Original line 1313: rect_touches_boundary.
    public static bool RectTouchesBoundary(Rectf inner, Rectf outer)
    {
            return inner.Left <= outer.Left + k_same_epsilon ||
                inner.Top <= outer.Top + k_same_epsilon ||
                inner.Right >= outer.Right - k_same_epsilon ||
                inner.Bottom >= outer.Bottom - k_same_epsilon;
        }
    // Original line 1322: emit_paired_boundary.
    public static void EmitPairedBoundary(RRect outer, RRect inner, float aa_radius, float arc_tolerance, float elliptical_tolerance, Action<Offsetf, Offsetf, Offsetf, Offsetf> func)
    {
            var push_pair = (Offsetf outer_point, Offsetf outer_aa, Offsetf inner_point, Offsetf inner_aa) => {
                func(inner_point, outer_point, inner_aa, outer_aa);
            };
            var push_corner_pair =
                (VGBasicRRectCorner outer_corner, VGBasicRRectCorner inner_corner, bool include_end) => {



                    float current_t = -1.0f;
                    for (;;)
                    {
                        float outer_t = 0.0f;
                        float inner_t = 0.0f;
                        bool has_outer = FindNextCornerSampleT(outer_corner, arc_tolerance, elliptical_tolerance, current_t, include_end, ref outer_t);
                        bool has_inner = FindNextCornerSampleT(inner_corner, arc_tolerance, elliptical_tolerance, current_t, include_end, ref inner_t);
                        if (!has_outer && !has_inner)
                        {
                            return;
                        }

                        float t = has_outer ? outer_t : inner_t;
                        if (has_inner && inner_t < t)
                        {
                            t = inner_t;
                        }
                        current_t = t;

                        push_pair(
                            CornerPoint(outer_corner, t, 0.0f),
                            CornerPoint(outer_corner, t, aa_radius),
                            CornerPoint(inner_corner, t, 0.0f),
                            CornerPoint(inner_corner, t, -aa_radius)
                        );
                    }
                };

            VGBasicRRectCorner outer_tl = CornerTl(outer);
            VGBasicRRectCorner outer_tr = CornerTr(outer);
            VGBasicRRectCorner outer_br = CornerBr(outer);
            VGBasicRRectCorner outer_bl = CornerBl(outer);
            VGBasicRRectCorner inner_tl = CornerTl(inner);
            VGBasicRRectCorner inner_tr = CornerTr(inner);
            VGBasicRRectCorner inner_br = CornerBr(inner);
            VGBasicRRectCorner inner_bl = CornerBl(inner);

            push_pair(
                new Offsetf(outer.Left + outer.TlRadius.X, outer.Top),
                new Offsetf(outer.Left + outer.TlRadius.X, outer.Top - aa_radius),
                new Offsetf(inner.Left + inner.TlRadius.X, inner.Top),
                new Offsetf(inner.Left + inner.TlRadius.X, inner.Top + aa_radius)
            );
            push_pair(
                new Offsetf(outer.Right - outer.TrRadius.X, outer.Top),
                new Offsetf(outer.Right - outer.TrRadius.X, outer.Top - aa_radius),
                new Offsetf(inner.Right - inner.TrRadius.X, inner.Top),
                new Offsetf(inner.Right - inner.TrRadius.X, inner.Top + aa_radius)
            );
            push_corner_pair(outer_tr, inner_tr, true);
            push_pair(
                new Offsetf(outer.Right, outer.Bottom - outer.BrRadius.Y),
                new Offsetf(outer.Right + aa_radius, outer.Bottom - outer.BrRadius.Y),
                new Offsetf(inner.Right, inner.Bottom - inner.BrRadius.Y),
                new Offsetf(inner.Right - aa_radius, inner.Bottom - inner.BrRadius.Y)
            );
            push_corner_pair(outer_br, inner_br, true);
            push_pair(
                new Offsetf(outer.Left + outer.BlRadius.X, outer.Bottom),
                new Offsetf(outer.Left + outer.BlRadius.X, outer.Bottom + aa_radius),
                new Offsetf(inner.Left + inner.BlRadius.X, inner.Bottom),
                new Offsetf(inner.Left + inner.BlRadius.X, inner.Bottom - aa_radius)
            );
            push_corner_pair(outer_bl, inner_bl, true);
            push_pair(
                new Offsetf(outer.Left, outer.Top + outer.TlRadius.Y),
                new Offsetf(outer.Left - aa_radius, outer.Top + outer.TlRadius.Y),
                new Offsetf(inner.Left, inner.Top + inner.TlRadius.Y),
                new Offsetf(inner.Left + aa_radius, inner.Top + inner.TlRadius.Y)
            );



            push_corner_pair(outer_tl, inner_tl, false);
        }
    // Original line 1429: connect_paired_sections.
    public static void ConnectPairedSections(VGMeshEmitter emitter, VGBasicRRectPairedSection previous, VGBasicRRectPairedSection current, bool has_aa)
    {


            if (has_aa)
            {
                PushQuadOrTriangle(emitter, previous.OuterAa, current.OuterAa, current.Outer, previous.Outer);
                PushQuadOrTriangle(emitter, previous.InnerAa, previous.Inner, current.Inner, current.InnerAa);
            }

            PushQuadOrTriangle(emitter, previous.Inner, previous.Outer, current.Outer, current.Inner);
        }
    // Original line 1447: emit_paired_diff_rrect.
    public static void EmitPairedDiffRrect(VGMeshEmitter emitter, RRect outer, RRect inner, float aa_radius, float arc_tolerance, float elliptical_tolerance, bool has_aa)
    {
            VGBasicRRectPairedSection first = new();
            VGBasicRRectPairedSection previous = new();
            bool has_previous = false;

            var reuse_body_index =
                (Offsetf point, ref uint out_index, VGBasicRRectPairedSection current, bool reuse_current_inner) => {
                    if (reuse_current_inner && point.NearlyEqual(current.InnerPos, k_same_epsilon))
                    {
                        out_index = current.Inner;
                        return true;
                    }
                    if (has_previous)
                    {
                        if (point.NearlyEqual(previous.InnerPos, k_same_epsilon))
                        {
                            out_index = previous.Inner;
                            return true;
                        }
                        if (point.NearlyEqual(previous.OuterPos, k_same_epsilon))
                        {
                            out_index = previous.Outer;
                            return true;
                        }
                    }
                    return false;
                };

            var reuse_aa_index =
                (Offsetf point, ref uint out_index, VGBasicRRectPairedSection current, bool reuse_current_inner) => {
                    if (reuse_current_inner && point.NearlyEqual(current.InnerAaPos, k_same_epsilon))
                    {
                        out_index = current.InnerAa;
                        return true;
                    }
                    if (has_previous)
                    {
                        if (point.NearlyEqual(previous.InnerAaPos, k_same_epsilon))
                        {
                            out_index = previous.InnerAa;
                            return true;
                        }
                        if (point.NearlyEqual(previous.OuterAaPos, k_same_epsilon))
                        {
                            out_index = previous.OuterAa;
                            return true;
                        }
                    }
                    return false;
                };

            EmitPairedBoundary(outer, inner, aa_radius, arc_tolerance, elliptical_tolerance, (Offsetf inner_point, Offsetf outer_point, Offsetf inner_aa_point, Offsetf outer_aa_point) => {
                    VGBasicRRectPairedSection current = new();
                    current.InnerPos = inner_point;
                    if (!reuse_body_index(inner_point, ref current.Inner, current, false))
                    {
                        current.Inner = emitter.PushVertex(inner_point, 1.0f);
                    }

                    current.OuterPos = outer_point;
                    if (!reuse_body_index(outer_point, ref current.Outer, current, true))
                    {
                        current.Outer = emitter.PushVertex(outer_point, 1.0f);
                    }

                    if (has_aa)
                    {
                        current.InnerAaPos = inner_aa_point;
                        if (!reuse_aa_index(inner_aa_point, ref current.InnerAa, current, false))
                        {
                            current.InnerAa = emitter.PushVertex(inner_aa_point, 0.0f);
                        }

                        current.OuterAaPos = outer_aa_point;
                        if (!reuse_aa_index(outer_aa_point, ref current.OuterAa, current, true))
                        {
                            current.OuterAa = emitter.PushVertex(outer_aa_point, 0.0f);
                        }
                    }

                    if (!has_previous)
                    {
                        first = current;
                        previous = current;
                        has_previous = true;
                        return;
                    }

                    ConnectPairedSections(emitter, previous, current, has_aa);
                    previous = current;
                });

            if (has_previous)
            {
                ConnectPairedSections(emitter, previous, first, has_aa);
            }
        }
}
public static class VGBasicPrimitives
{
    // Original line 1562: circle.
    public static bool Circle(VGBackend backend, Circle circle, float pixel_ratio = 1.0f, float tessellation_factor = 1.0f, float aa_radius = 0.0f)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.circle requires a valid VGBackend");
            return false;
        }
        if (!circle.IsValid() || circle.IsEmpty())
        {
            return false;
        }



        float resolved_aa_radius = VGAAHelper.ResolveLogicalRadius(aa_radius, pixel_ratio);
        float tolerance = global::SkrGui.Circle.CalcTolerance(
            tessellation_factor,
            pixel_ratio
        );
        uint segment_count = circle.EstimateSegmentCount(tolerance);
        if (segment_count < 3u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong vertex_count = 1u + (ulong)(segment_count) * (has_aa ? 2u : 1u);
        ulong triangle_count = (ulong)(segment_count) * (has_aa ? 3u : 1u);
        backend.CallReserve(vertex_count, triangle_count);


        VGMeshEmitter emitter = new(backend);
        CircleSampler sampler = circle.CreateSampler(0.0f);
        uint center_index = emitter.PushVertex(circle.Center, 1.0f);
        uint first_body = emitter.PushVertex(
            sampler.SamplePoint(circle.Center, circle.Radius),
            1.0f
        );
        uint first_aa = 0u;
        if (has_aa)
        {
            first_aa = emitter.PushVertex(
                sampler.SamplePoint(circle.Center, circle.Radius + resolved_aa_radius),
                0.0f
            );
        }

        uint previous_body = first_body;
        uint previous_aa = first_aa;



        for (uint i = 1u; i < segment_count; ++i)
        {
            float angle =
                (MathF.PI*2) *
                (float)(i) /
                (float)(segment_count);
            sampler.SetAngle(angle);
            uint current_body = emitter.PushVertex(
                sampler.SamplePoint(circle.Center, circle.Radius),
                1.0f
            );
            uint current_aa = 0u;
            if (has_aa)
            {
                current_aa = emitter.PushVertex(
                    sampler.SamplePoint(circle.Center, circle.Radius + resolved_aa_radius),
                    0.0f
                );
            }

            if (has_aa)
            {
                emitter.PushQuad(previous_body, current_body, current_aa, previous_aa);
            }
            emitter.PushTriangle(center_index, previous_body, current_body);

            previous_body = current_body;
            previous_aa = current_aa;
        }


        if (has_aa)
        {
            emitter.PushQuad(previous_body, first_body, first_aa, previous_aa);
        }
        emitter.PushTriangle(center_index, previous_body, first_body);
        return true;
    }
    public static bool CircleStroke(VGBackend backend, Circle circle, float stroke_width) => CircleStroke(backend,circle,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 1659: circle_stroke.
    public static bool CircleStroke(VGBackend backend, Circle circle, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.circle_stroke requires a valid VGBackend");
            return false;
        }
        if (!circle.IsValid() || circle.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float half_width = stroke_width * 0.5f;
        float inner_body_radius = CppMath.Max(0.0f, circle.Radius - half_width);
        float outer_body_radius = circle.Radius + half_width;
        float tolerance = global::SkrGui.Circle.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count = global::SkrGui.Circle.CenterRadius(
                                           circle.Center,
                                           outer_body_radius
        )
                                           .EstimateSegmentCount(tolerance);
        if (segment_count < 3u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        float outer_aa_radius = outer_body_radius + resolved_aa_radius;
        if (inner_body_radius <= 0.0f)
        {


            ulong vertex_count = 1u + (ulong)(segment_count) * (has_aa ? 2u : 1u);
            ulong triangle_count = (ulong)(segment_count) * (has_aa ? 3u : 1u);
            backend.CallReserve(vertex_count, triangle_count);


            VGMeshEmitter emitter_scope10 = new(backend);
            CircleSampler sampler_scope11 = circle.CreateSampler(0.0f);
            uint center_index = emitter_scope10.PushVertex(circle.Center, 1.0f);
            uint first_body = emitter_scope10.PushVertex(
                sampler_scope11.SamplePoint(circle.Center, outer_body_radius),
                1.0f
            );
            uint first_aa = 0u;
            if (has_aa)
            {
                first_aa = emitter_scope10.PushVertex(
                    sampler_scope11.SamplePoint(circle.Center, outer_aa_radius),
                    0.0f
                );
            }

            uint previous_body = first_body;
            uint previous_aa = first_aa;



            for (uint i_scope17 = 1u; i_scope17 < segment_count; ++i_scope17)
            {
                float angle_scope18 =
                    (MathF.PI*2) *
                    (float)(i_scope17) /
                    (float)(segment_count);
                sampler_scope11.SetAngle(angle_scope18);
                uint current_body = emitter_scope10.PushVertex(
                    sampler_scope11.SamplePoint(circle.Center, outer_body_radius),
                    1.0f
                );
                uint current_aa = 0u;
                if (has_aa)
                {
                    current_aa = emitter_scope10.PushVertex(
                        sampler_scope11.SamplePoint(circle.Center, outer_aa_radius),
                        0.0f
                    );
                }

                if (has_aa)
                {
                    emitter_scope10.PushQuad(previous_body, current_body, current_aa, previous_aa);
                }
                emitter_scope10.PushTriangle(center_index, previous_body, current_body);

                previous_body = current_body;
                previous_aa = current_aa;
            }


            if (has_aa)
            {
                emitter_scope10.PushQuad(previous_body, first_body, first_aa, previous_aa);
            }
            emitter_scope10.PushTriangle(center_index, previous_body, first_body);
            return true;
        }

        float inner_aa_radius = CppMath.Max(0.0f, inner_body_radius - resolved_aa_radius);
        bool has_inner_aa = has_aa && inner_aa_radius > 0.0f;



        ulong section_vertex_count = has_aa ? (has_inner_aa ? 4u : 3u) : 2u;
        ulong section_triangle_count = has_aa ? (has_inner_aa ? 6u : 4u) : 2u;
        backend.CallReserve(
            (ulong)(segment_count) * section_vertex_count,
            (ulong)(segment_count) * section_triangle_count
        );




        VGMeshEmitter emitter_scope25 = new(backend);
        CircleSampler sampler_scope26 = circle.CreateSampler(0.0f);
        VGBasicClosedStrokeSection first_section = new();
        if (has_inner_aa)
        {
            first_section.InnerAa = emitter_scope25.PushVertex(
                sampler_scope26.SamplePoint(circle.Center, inner_aa_radius),
                0.0f
            );
        }
        first_section.InnerBody = emitter_scope25.PushVertex(
            sampler_scope26.SamplePoint(circle.Center, inner_body_radius),
            1.0f
        );
        first_section.OuterBody = emitter_scope25.PushVertex(
            sampler_scope26.SamplePoint(circle.Center, outer_body_radius),
            1.0f
        );
        if (has_aa)
        {
            first_section.OuterAa = emitter_scope25.PushVertex(
                sampler_scope26.SamplePoint(circle.Center, outer_aa_radius),
                0.0f
            );
        }

        VGBasicClosedStrokeSection previous_section = first_section;



        for (uint i_scope29 = 1u; i_scope29 < segment_count; ++i_scope29)
        {
            float angle_scope30 =
                (MathF.PI*2) *
                (float)(i_scope29) /
                (float)(segment_count);
            sampler_scope26.SetAngle(angle_scope30);
            VGBasicClosedStrokeSection current_section = new();
            if (has_inner_aa)
            {
                current_section.InnerAa = emitter_scope25.PushVertex(
                    sampler_scope26.SamplePoint(circle.Center, inner_aa_radius),
                    0.0f
                );
            }
            current_section.InnerBody = emitter_scope25.PushVertex(
                sampler_scope26.SamplePoint(circle.Center, inner_body_radius),
                1.0f
            );
            current_section.OuterBody = emitter_scope25.PushVertex(
                sampler_scope26.SamplePoint(circle.Center, outer_body_radius),
                1.0f
            );
            if (has_aa)
            {
                current_section.OuterAa = emitter_scope25.PushVertex(
                    sampler_scope26.SamplePoint(circle.Center, outer_aa_radius),
                    0.0f
                );
            }

            VGBasicClosedPrimitiveHelper.ConnectStrokeSections(emitter_scope25, previous_section, current_section, has_aa, has_inner_aa);

            previous_section = current_section;
        }


        VGBasicClosedPrimitiveHelper.ConnectStrokeSections(emitter_scope25, previous_section, first_section, has_aa, has_inner_aa);
        return true;
    }
    // Original line 1866: ellipse.
    public static bool Ellipse(VGBackend backend, Ellipse ellipse, float pixel_ratio = 1.0f, float tessellation_factor = 1.0f, float aa_radius = 0.0f)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.ellipse requires a valid VGBackend");
            return false;
        }
        if (!ellipse.IsValid() || ellipse.IsEmpty())
        {
            return false;
        }



        Ellipse body_ellipse = ellipse.Normalized();
        float resolved_aa_radius = VGAAHelper.ResolveLogicalRadius(aa_radius, pixel_ratio);
        Ellipse aa_ellipse = body_ellipse.Inflate(resolved_aa_radius);
        float tolerance = global::SkrGui.Ellipse.CalcTolerance(
            tessellation_factor,
            pixel_ratio
        );
        uint segment_count_hint = body_ellipse.EstimateSegmentCount(tolerance);
        if (segment_count_hint < 3u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong reserve_segment_count = segment_count_hint;
        ulong vertex_count = 1u + reserve_segment_count * (has_aa ? 2u : 1u);
        ulong triangle_count = reserve_segment_count * (has_aa ? 3u : 1u);
        backend.CallReserve(vertex_count, triangle_count);



        VGMeshEmitter emitter = new(backend);
        uint center_index = emitter.PushVertex(body_ellipse.Center, 1.0f);
        bool has_first = false;
        uint first_body = 0u;
        uint first_aa = 0u;
        uint previous_body = 0u;
        uint previous_aa = 0u;

        body_ellipse.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf body_point, float _unused1, EllipseSampler sampler) => {
                uint current_body = emitter.PushVertex(body_point, 1.0f);
                uint current_aa = 0u;
                if (has_aa)
                {
                    current_aa = emitter.PushVertex(
                        sampler.SamplePoint(body_ellipse.Center, aa_ellipse.RadiusX, aa_ellipse.RadiusY),
                        0.0f
                    );
                }

                if (!has_first)
                {
                    has_first = true;
                    first_body = current_body;
                    first_aa = current_aa;
                }
                else
                {
                    if (has_aa)
                    {
                        emitter.PushQuad(previous_body, current_body, current_aa, previous_aa);
                    }
                    emitter.PushTriangle(center_index, previous_body, current_body);
                }

                previous_body = current_body;
                previous_aa = current_aa;
            }
        );


        if (has_first)
        {
            if (has_aa)
            {
                emitter.PushQuad(previous_body, first_body, first_aa, previous_aa);
            }
            emitter.PushTriangle(center_index, previous_body, first_body);
        }
        return true;
    }
    public static bool EllipseStroke(VGBackend backend, Ellipse ellipse, float stroke_width) => EllipseStroke(backend,ellipse,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 1960: ellipse_stroke.
    public static bool EllipseStroke(VGBackend backend, Ellipse ellipse, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.ellipse_stroke requires a valid VGBackend");
            return false;
        }
        if (!ellipse.IsValid() || ellipse.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }



        Ellipse center_ellipse = ellipse.Normalized();
        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float half_width = stroke_width * 0.5f;
        Ellipse inner_body_ellipse = center_ellipse.Deflate(half_width);
        Ellipse outer_body_ellipse = center_ellipse.Inflate(half_width);
        Ellipse outer_aa_ellipse = outer_body_ellipse.Inflate(resolved_aa_radius);
        float tolerance = global::SkrGui.Ellipse.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = outer_body_ellipse.EstimateSegmentCount(tolerance);
        if (segment_count_hint < 3u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong reserve_segment_count = segment_count_hint;
        if (inner_body_ellipse.IsEmpty())
        {


            ulong vertex_count = 1u + reserve_segment_count * (has_aa ? 2u : 1u);
            ulong triangle_count = reserve_segment_count * (has_aa ? 3u : 1u);
            backend.CallReserve(vertex_count, triangle_count);



            VGMeshEmitter emitter_scope12 = new(backend);
            uint center_index = emitter_scope12.PushVertex(center_ellipse.Center, 1.0f);
            bool has_first_scope14 = false;
            uint first_body = 0u;
            uint first_aa = 0u;
            uint previous_body = 0u;
            uint previous_aa = 0u;

            outer_body_ellipse.SampleWithSampler(
                new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
                (Offsetf body_point, float _unused1, EllipseSampler sampler) => {
                    uint current_body = emitter_scope12.PushVertex(body_point, 1.0f);
                    uint current_aa = 0u;
                    if (has_aa)
                    {
                        current_aa = emitter_scope12.PushVertex(
                            sampler.SamplePoint(
                                center_ellipse.Center,
                                outer_aa_ellipse.RadiusX,
                                outer_aa_ellipse.RadiusY
                            ),
                            0.0f
                        );
                    }

                    if (!has_first_scope14)
                    {
                        has_first_scope14 = true;
                        first_body = current_body;
                        first_aa = current_aa;
                    }
                    else
                    {
                        if (has_aa)
                        {
                            emitter_scope12.PushQuad(previous_body, current_body, current_aa, previous_aa);
                        }
                        emitter_scope12.PushTriangle(center_index, previous_body, current_body);
                    }

                    previous_body = current_body;
                    previous_aa = current_aa;
                }
            );


            if (has_first_scope14)
            {
                if (has_aa)
                {
                    emitter_scope12.PushQuad(previous_body, first_body, first_aa, previous_aa);
                }
                emitter_scope12.PushTriangle(center_index, previous_body, first_body);
            }
            return true;
        }

        Ellipse inner_aa_ellipse = inner_body_ellipse.Deflate(resolved_aa_radius);
        bool has_inner_aa = has_aa && !inner_aa_ellipse.IsEmpty();



        ulong section_vertex_count = has_aa ? (has_inner_aa ? 4u : 3u) : 2u;
        ulong section_triangle_count = has_aa ? (has_inner_aa ? 6u : 4u) : 2u;
        backend.CallReserve(
            reserve_segment_count * section_vertex_count,
            reserve_segment_count * section_triangle_count
        );




        VGMeshEmitter emitter_scope25 = new(backend);
        bool has_first_scope26 = false;
        VGBasicClosedStrokeSection first_section = new();
        VGBasicClosedStrokeSection previous_section = new();

        outer_body_ellipse.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf outer_body_point, float _unused1, EllipseSampler sampler) => {
                VGBasicClosedStrokeSection current_section = new();
                if (has_inner_aa)
                {
                    current_section.InnerAa = emitter_scope25.PushVertex(
                        sampler.SamplePoint(center_ellipse.Center, inner_aa_ellipse.RadiusX, inner_aa_ellipse.RadiusY),
                        0.0f
                    );
                }
                current_section.InnerBody = emitter_scope25.PushVertex(
                    sampler.SamplePoint(center_ellipse.Center, inner_body_ellipse.RadiusX, inner_body_ellipse.RadiusY),
                    1.0f
                );
                current_section.OuterBody = emitter_scope25.PushVertex(outer_body_point, 1.0f);
                if (has_aa)
                {
                    current_section.OuterAa = emitter_scope25.PushVertex(
                        sampler.SamplePoint(center_ellipse.Center, outer_aa_ellipse.RadiusX, outer_aa_ellipse.RadiusY),
                        0.0f
                    );
                }

                if (!has_first_scope26)
                {
                    has_first_scope26 = true;
                    first_section = current_section;
                }
                else
                {
                    VGBasicClosedPrimitiveHelper.ConnectStrokeSections(emitter_scope25, previous_section, current_section, has_aa, has_inner_aa);
                }

                previous_section = current_section;
            }
        );


        if (has_first_scope26)
        {
            VGBasicClosedPrimitiveHelper.ConnectStrokeSections(emitter_scope25, previous_section, first_section, has_aa, has_inner_aa);
        }
        return true;
    }
    public static bool Superellipse(VGBackend backend, Superellipse superellipse) => Superellipse(backend,superellipse,new VGBasicPrimitiveOptions());
    // Original line 2143: superellipse.
    public static bool Superellipse(VGBackend backend, Superellipse superellipse, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.superellipse requires a valid VGBackend");
            return false;
        }
        if (!superellipse.IsValid() || superellipse.IsEmpty())
        {
            return false;
        }



        Superellipse body_superellipse = superellipse.Normalized();
        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        Superellipse aa_superellipse = body_superellipse.Inflate(resolved_aa_radius);
        float tolerance = global::SkrGui.Superellipse.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = body_superellipse.EstimateSegmentCount(tolerance);
        if (segment_count_hint < 3u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong reserve_segment_count = segment_count_hint;
        ulong vertex_count = 1u + reserve_segment_count * (has_aa ? 2u : 1u);
        ulong triangle_count = reserve_segment_count * (has_aa ? 3u : 1u);
        backend.CallReserve(vertex_count, triangle_count);



        VGMeshEmitter emitter = new(backend);
        uint center_index = emitter.PushVertex(body_superellipse.Center, 1.0f);
        bool has_first = false;
        uint first_body = 0u;
        uint first_aa = 0u;
        uint previous_body = 0u;
        uint previous_aa = 0u;

        body_superellipse.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf body_point, float _unused1, SuperellipseSampler sampler) => {
                uint current_body = emitter.PushVertex(body_point, 1.0f);
                uint current_aa = 0u;
                if (has_aa)
                {
                    current_aa = emitter.PushVertex(
                        sampler.SamplePoint(
                            body_superellipse.Center,
                            aa_superellipse.RadiusX,
                            aa_superellipse.RadiusY
                        ),
                        0.0f
                    );
                }

                if (!has_first)
                {
                    has_first = true;
                    first_body = current_body;
                    first_aa = current_aa;
                }
                else
                {
                    if (has_aa)
                    {
                        emitter.PushQuad(previous_body, current_body, current_aa, previous_aa);
                    }
                    emitter.PushTriangle(center_index, previous_body, current_body);
                }

                previous_body = current_body;
                previous_aa = current_aa;
            }
        );


        if (has_first)
        {
            if (has_aa)
            {
                emitter.PushQuad(previous_body, first_body, first_aa, previous_aa);
            }
            emitter.PushTriangle(center_index, previous_body, first_body);
        }
        return true;
    }
    public static bool SuperellipseStroke(VGBackend backend, Superellipse superellipse, float stroke_width) => SuperellipseStroke(backend,superellipse,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 2240: superellipse_stroke.
    public static bool SuperellipseStroke(VGBackend backend, Superellipse superellipse, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.superellipse_stroke requires a valid VGBackend");
            return false;
        }
        if (!superellipse.IsValid() || superellipse.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }



        Superellipse center_superellipse = superellipse.Normalized();
        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float half_width = stroke_width * 0.5f;
        Superellipse inner_body_superellipse = center_superellipse.Deflate(half_width);
        Superellipse outer_body_superellipse = center_superellipse.Inflate(half_width);
        Superellipse outer_aa_superellipse = outer_body_superellipse.Inflate(resolved_aa_radius);
        float tolerance = global::SkrGui.Superellipse.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = outer_body_superellipse.EstimateSegmentCount(tolerance);
        if (segment_count_hint < 3u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong reserve_segment_count = segment_count_hint;
        if (inner_body_superellipse.IsEmpty())
        {


            ulong vertex_count = 1u + reserve_segment_count * (has_aa ? 2u : 1u);
            ulong triangle_count = reserve_segment_count * (has_aa ? 3u : 1u);
            backend.CallReserve(vertex_count, triangle_count);



            VGMeshEmitter emitter_scope12 = new(backend);
            uint center_index = emitter_scope12.PushVertex(center_superellipse.Center, 1.0f);
            bool has_first_scope14 = false;
            uint first_body = 0u;
            uint first_aa = 0u;
            uint previous_body = 0u;
            uint previous_aa = 0u;

            outer_body_superellipse.SampleWithSampler(
                new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
                (Offsetf body_point, float _unused1, SuperellipseSampler sampler) => {
                    uint current_body = emitter_scope12.PushVertex(body_point, 1.0f);
                    uint current_aa = 0u;
                    if (has_aa)
                    {
                        current_aa = emitter_scope12.PushVertex(
                            sampler.SamplePoint(
                                center_superellipse.Center,
                                outer_aa_superellipse.RadiusX,
                                outer_aa_superellipse.RadiusY
                            ),
                            0.0f
                        );
                    }

                    if (!has_first_scope14)
                    {
                        has_first_scope14 = true;
                        first_body = current_body;
                        first_aa = current_aa;
                    }
                    else
                    {
                        if (has_aa)
                        {
                            emitter_scope12.PushQuad(previous_body, current_body, current_aa, previous_aa);
                        }
                        emitter_scope12.PushTriangle(center_index, previous_body, current_body);
                    }

                    previous_body = current_body;
                    previous_aa = current_aa;
                }
            );


            if (has_first_scope14)
            {
                if (has_aa)
                {
                    emitter_scope12.PushQuad(previous_body, first_body, first_aa, previous_aa);
                }
                emitter_scope12.PushTriangle(center_index, previous_body, first_body);
            }
            return true;
        }

        Superellipse inner_aa_superellipse = inner_body_superellipse.Deflate(resolved_aa_radius);
        bool has_inner_aa = has_aa && !inner_aa_superellipse.IsEmpty();



        ulong section_vertex_count = has_aa ? (has_inner_aa ? 4u : 3u) : 2u;
        ulong section_triangle_count = has_aa ? (has_inner_aa ? 6u : 4u) : 2u;
        backend.CallReserve(
            reserve_segment_count * section_vertex_count,
            reserve_segment_count * section_triangle_count
        );




        VGMeshEmitter emitter_scope25 = new(backend);
        bool has_first_scope26 = false;
        VGBasicClosedStrokeSection first_section = new();
        VGBasicClosedStrokeSection previous_section = new();

        outer_body_superellipse.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf outer_body_point, float _unused1, SuperellipseSampler sampler) => {
                VGBasicClosedStrokeSection current_section = new();
                if (has_inner_aa)
                {
                    current_section.InnerAa = emitter_scope25.PushVertex(
                        sampler.SamplePoint(
                            center_superellipse.Center,
                            inner_aa_superellipse.RadiusX,
                            inner_aa_superellipse.RadiusY
                        ),
                        0.0f
                    );
                }
                current_section.InnerBody = emitter_scope25.PushVertex(
                    sampler.SamplePoint(
                        center_superellipse.Center,
                        inner_body_superellipse.RadiusX,
                        inner_body_superellipse.RadiusY
                    ),
                    1.0f
                );
                current_section.OuterBody = emitter_scope25.PushVertex(outer_body_point, 1.0f);
                if (has_aa)
                {
                    current_section.OuterAa = emitter_scope25.PushVertex(
                        sampler.SamplePoint(
                            center_superellipse.Center,
                            outer_aa_superellipse.RadiusX,
                            outer_aa_superellipse.RadiusY
                        ),
                        0.0f
                    );
                }

                if (!has_first_scope26)
                {
                    has_first_scope26 = true;
                    first_section = current_section;
                }
                else
                {
                    VGBasicClosedPrimitiveHelper.ConnectStrokeSections(emitter_scope25, previous_section, current_section, has_aa, has_inner_aa);
                }

                previous_section = current_section;
            }
        );


        if (has_first_scope26)
        {
            VGBasicClosedPrimitiveHelper.ConnectStrokeSections(emitter_scope25, previous_section, first_section, has_aa, has_inner_aa);
        }
        return true;
    }
    public static bool Fan(VGBackend backend, Arc arc) => Fan(backend,arc,new VGBasicPrimitiveOptions());
    // Original line 2435: fan.
    public static bool Fan(VGBackend backend, Arc arc, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.fan requires a valid VGBackend");
            return false;
        }
        if (!arc.IsValid() || arc.IsEmpty())
        {
            return false;
        }



        Arc body_arc = arc.Normalized();
        if (MathF.Abs(body_arc.SweepAngle) >= (MathF.PI*2))
        {
            return Circle(backend, global::SkrGui.Circle.CenterRadius(body_arc.Center, body_arc.Radius), options.PixelRatio, options.TessellationFactor, options.AaRadius);
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float tolerance = global::SkrGui.Arc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = body_arc.EstimateSegmentCount(tolerance);
        if (segment_count_hint == 0u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong section_count = (ulong)(segment_count_hint) + 1u;
        ulong boundary_count = section_count + 1u;
        ulong vertex_count = has_aa ? boundary_count * 3u : boundary_count;
        ulong triangle_count = has_aa ?
            (ulong)(segment_count_hint) + boundary_count * 3u :
            (ulong)(segment_count_hint);
        backend.CallReserve(vertex_count, triangle_count);



        VGMeshEmitter emitter = new(backend);
        uint center_index = emitter.PushVertex(body_arc.Center, 1.0f);
        bool has_first = false;
        uint previous_body = 0u;
        if (!has_aa)
        {
            body_arc.SampleWithSampler(
                new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
                (Offsetf body_point, float _unused1, CircleSampler _unused2) => {
                    uint current_body_scope13 = emitter.PushVertex(body_point, 1.0f);
                    if (has_first)
                    {
                        emitter.PushTriangle(center_index, previous_body, current_body_scope13);
                    }
                    else
                    {
                        has_first = true;
                    }

                    previous_body = current_body_scope13;
                }
            );
            return true;
        }




        float signed_aa_radius = body_arc.SweepAngle >= 0.0f ? resolved_aa_radius : -resolved_aa_radius;
        VGFillAANode center_node = VGFillAAHelper.BuildNode(
            body_arc.EndPoint(),
            body_arc.Center,
            body_arc.StartPoint(),
            center_index,
            resolved_aa_radius
        );
        bool has_center_node = false;
        bool has_pending = false;
        bool has_previous_node = false;
        uint pending_body = 0u;
        Offsetf previous_pos = body_arc.Center;
        Offsetf pending_pos = Offsetf.Zero();
        VGFillAANode previous_node = new();
        body_arc.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf body_point, float _unused1, CircleSampler _unused2) => {
                uint current_body_scope21 = emitter.PushVertex(body_point, 1.0f);
                if (!has_center_node)
                {
                    has_center_node = true;
                    VGFillAAHelper.EmitOuterVertices(emitter, ref center_node, signed_aa_radius);
                }

                if (!has_pending)
                {
                    has_pending = true;
                    pending_body = current_body_scope21;
                    pending_pos = body_point;
                    return;
                }


                VGFillAANode pending_node = VGFillAAHelper.BuildNode(
                    previous_pos,
                    pending_pos,
                    body_point,
                    pending_body,
                    resolved_aa_radius
                );
                VGFillAAHelper.EmitOuterVertices(emitter, ref pending_node, signed_aa_radius);
                if (has_previous_node)
                {
                    VGFillAAHelper.ConnectSections(emitter, previous_node, pending_node);
                    VGFillAAHelper.EmitJoinTriangle(emitter, previous_node);
                }
                else
                {
                    VGFillAAHelper.ConnectSections(emitter, center_node, pending_node);
                    VGFillAAHelper.EmitJoinTriangle(emitter, center_node);
                }
                emitter.PushTriangle(center_index, pending_body, current_body_scope21);

                has_previous_node = true;
                previous_node = pending_node;
                previous_pos = pending_pos;
                pending_body = current_body_scope21;
                pending_pos = body_point;
            }
        );



        if (has_pending)
        {
            VGFillAANode last_node = VGFillAAHelper.BuildNode(
                previous_pos,
                pending_pos,
                body_arc.Center,
                pending_body,
                resolved_aa_radius
            );
            VGFillAAHelper.EmitOuterVertices(emitter, ref last_node, signed_aa_radius);
            if (has_previous_node)
            {
                VGFillAAHelper.ConnectSections(emitter, previous_node, last_node);
                VGFillAAHelper.EmitJoinTriangle(emitter, previous_node);
            }
            else
            {
                VGFillAAHelper.ConnectSections(emitter, center_node, last_node);
                VGFillAAHelper.EmitJoinTriangle(emitter, center_node);
            }
            VGFillAAHelper.ConnectSections(emitter, last_node, center_node);
            VGFillAAHelper.EmitJoinTriangle(emitter, last_node);
        }
        return true;
    }
    public static bool ArcStroke(VGBackend backend, Arc arc, float stroke_width) => ArcStroke(backend,arc,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 2608: arc_stroke.
    public static bool ArcStroke(VGBackend backend, Arc arc, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.arc_stroke requires a valid VGBackend");
            return false;
        }
        if (!arc.IsValid() || arc.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }


        Arc center_arc = arc.Normalized();
        if (MathF.Abs(center_arc.SweepAngle) >= (MathF.PI*2))
        {
            return CircleStroke(backend, global::SkrGui.Circle.CenterRadius(center_arc.Center, center_arc.Radius), stroke_width, options);
        }


        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float half_width = stroke_width * 0.5f;
        Arc inner_body_arc = center_arc.Deflate(half_width);
        Arc outer_body_arc = center_arc.Inflate(half_width);
        float tolerance = global::SkrGui.Arc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = outer_body_arc.EstimateSegmentCount(tolerance);
        if (segment_count_hint == 0u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        if (inner_body_arc.IsEmpty())
        {
            return Fan(backend, outer_body_arc, options);
        }

        float inner_aa_radius = CppMath.Max(0.0f, inner_body_arc.Radius - resolved_aa_radius);
        bool has_inner_aa = has_aa && inner_aa_radius > 0.0f;
        ulong section_count = (ulong)(segment_count_hint) + 1u;
        ulong section_vertex_count = has_aa ? (has_inner_aa ? 4u : 3u) : 2u;
        ulong section_triangle_count = has_aa ? (has_inner_aa ? 6u : 4u) : 2u;
        backend.CallReserve(
            section_count * section_vertex_count,
            (ulong)(segment_count_hint) * section_triangle_count +
                (has_aa ? (has_inner_aa ? 4u : 2u) : 0u)
        );




        VGMeshEmitter emitter = new(backend);
        bool has_pending = false;
        bool has_ready_previous = false;
        VGBasicOpenStrokeSection first_section = new();
        VGBasicOpenStrokeSection ready_previous = new();
        VGBasicOpenStrokeSection pending_section = new();
        Offsetf previous_direction = Offsetf.Zero();

        outer_body_arc.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf outer_body_point, float _unused1, CircleSampler sampler) => {
                VGBasicOpenStrokeSection current = new();
                if (has_inner_aa)
                {
                    current.InnerAaPos = sampler.SamplePoint(center_arc.Center, inner_aa_radius);
                }
                current.InnerBodyPos = sampler.SamplePoint(center_arc.Center, inner_body_arc.Radius);
                current.InnerBody = emitter.PushVertex(current.InnerBodyPos, 1.0f);
                current.OuterBodyPos = outer_body_point;
                current.OuterBody = emitter.PushVertex(current.OuterBodyPos, 1.0f);
                if (has_aa)
                {
                    current.OuterAaPos = sampler.SamplePoint(
                        center_arc.Center,
                        outer_body_arc.Radius + resolved_aa_radius
                    );
                }

                if (!has_pending)
                {
                    has_pending = true;
                    pending_section = current;
                    if (!has_aa)
                    {
                        ready_previous = current;
                        has_ready_previous = true;
                    }
                    return;
                }

                Offsetf current_direction = current.OuterBodyPos - pending_section.OuterBodyPos;
                if (!has_aa)
                {
                    VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, current, false, false);
                    ready_previous = current;
                    pending_section = current;
                    previous_direction = current_direction;
                    return;
                }




                if (!has_ready_previous)
                {
                    Offsetf cap_offset_scope22 = current_direction.Normalize() * -resolved_aa_radius;
                    VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, cap_offset_scope22, has_inner_aa);
                    first_section = pending_section;
                    VGBasicOpenPrimitiveHelper.PushFlatCapAa(emitter, first_section, true, has_inner_aa);
                }
                else
                {
                    VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, Offsetf.Zero(), has_inner_aa);
                    VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, pending_section, true, has_inner_aa);
                }

                ready_previous = pending_section;
                has_ready_previous = true;
                pending_section = current;
                previous_direction = current_direction;
            }
        );

        if (has_aa && has_pending && has_ready_previous)
        {


            Offsetf cap_offset_scope23 = previous_direction.Normalize() * resolved_aa_radius;
            VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, cap_offset_scope23, has_inner_aa);
            VGBasicOpenPrimitiveHelper.PushFlatCapAa(emitter, pending_section, false, has_inner_aa);
            VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, pending_section, true, has_inner_aa);
        }
        return true;
    }
    public static bool EllipticalFan(VGBackend backend, EllipticalArc arc) => EllipticalFan(backend,arc,new VGBasicPrimitiveOptions());
    // Original line 2791: elliptical_fan.
    public static bool EllipticalFan(VGBackend backend, EllipticalArc arc, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.elliptical_fan requires a valid VGBackend");
            return false;
        }
        if (!arc.IsValid() || arc.IsEmpty())
        {
            return false;
        }

        EllipticalArc body_arc = arc.Normalized();
        if (MathF.Abs(body_arc.SweepAngle) >= (MathF.PI*2))
        {
            return Ellipse(backend, global::SkrGui.Ellipse.CenterRadius(body_arc.Center, body_arc.RadiusX, body_arc.RadiusY, body_arc.Rotation), options.PixelRatio, options.TessellationFactor, options.AaRadius);
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float tolerance = global::SkrGui.EllipticalArc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = body_arc.EstimateSegmentCount(tolerance);
        if (segment_count_hint == 0u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong section_count = (ulong)(segment_count_hint) + 1u;
        ulong boundary_count = section_count + 1u;
        ulong vertex_count = has_aa ? boundary_count * 3u : boundary_count;
        ulong triangle_count = has_aa ?
            (ulong)(segment_count_hint) + boundary_count * 3u :
            (ulong)(segment_count_hint);
        backend.CallReserve(vertex_count, triangle_count);



        VGMeshEmitter emitter = new(backend);
        uint center_index = emitter.PushVertex(body_arc.Center, 1.0f);
        bool has_first = false;
        uint previous_body = 0u;
        if (!has_aa)
        {
            body_arc.SampleWithSampler(
                new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
                (Offsetf body_point, float _unused1, EllipseSampler _unused2) => {
                    uint current_body_scope13 = emitter.PushVertex(body_point, 1.0f);
                    if (has_first)
                    {
                        emitter.PushTriangle(center_index, previous_body, current_body_scope13);
                    }
                    else
                    {
                        has_first = true;
                    }

                    previous_body = current_body_scope13;
                }
            );
            return true;
        }




        float signed_aa_radius = body_arc.SweepAngle >= 0.0f ? resolved_aa_radius : -resolved_aa_radius;
        VGFillAANode center_node = VGFillAAHelper.BuildNode(
            body_arc.EndPoint(),
            body_arc.Center,
            body_arc.StartPoint(),
            center_index,
            resolved_aa_radius
        );
        bool has_center_node = false;
        bool has_pending = false;
        bool has_previous_node = false;
        uint pending_body = 0u;
        Offsetf previous_pos = body_arc.Center;
        Offsetf pending_pos = Offsetf.Zero();
        VGFillAANode previous_node = new();
        body_arc.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf body_point, float _unused1, EllipseSampler sampler) => {
                uint current_body_scope21 = emitter.PushVertex(body_point, 1.0f);
                if (!has_center_node)
                {
                    has_center_node = true;
                    VGFillAAHelper.EmitOuterVertices(emitter, ref center_node, signed_aa_radius);
                }

                if (!has_pending)
                {
                    has_pending = true;
                    pending_body = current_body_scope21;
                    pending_pos = body_point;
                    return;
                }


                VGFillAANode pending_node = VGFillAAHelper.BuildNode(
                    previous_pos,
                    pending_pos,
                    body_point,
                    pending_body,
                    resolved_aa_radius
                );
                VGFillAAHelper.EmitOuterVertices(emitter, ref pending_node, signed_aa_radius);
                if (has_previous_node)
                {
                    VGFillAAHelper.ConnectSections(emitter, previous_node, pending_node);
                    VGFillAAHelper.EmitJoinTriangle(emitter, previous_node);
                }
                else
                {
                    VGFillAAHelper.ConnectSections(emitter, center_node, pending_node);
                    VGFillAAHelper.EmitJoinTriangle(emitter, center_node);
                }
                emitter.PushTriangle(center_index, pending_body, current_body_scope21);

                has_previous_node = true;
                previous_node = pending_node;
                previous_pos = pending_pos;
                pending_body = current_body_scope21;
                pending_pos = body_point;
            }
        );



        if (has_pending)
        {
            VGFillAANode last_node = VGFillAAHelper.BuildNode(
                previous_pos,
                pending_pos,
                body_arc.Center,
                pending_body,
                resolved_aa_radius
            );
            VGFillAAHelper.EmitOuterVertices(emitter, ref last_node, signed_aa_radius);
            if (has_previous_node)
            {
                VGFillAAHelper.ConnectSections(emitter, previous_node, last_node);
                VGFillAAHelper.EmitJoinTriangle(emitter, previous_node);
            }
            else
            {
                VGFillAAHelper.ConnectSections(emitter, center_node, last_node);
                VGFillAAHelper.EmitJoinTriangle(emitter, center_node);
            }
            VGFillAAHelper.ConnectSections(emitter, last_node, center_node);
            VGFillAAHelper.EmitJoinTriangle(emitter, last_node);
        }
        return true;
    }
    public static bool EllipticalArcStroke(VGBackend backend, EllipticalArc arc, float stroke_width) => EllipticalArcStroke(backend,arc,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 2962: elliptical_arc_stroke.
    public static bool EllipticalArcStroke(VGBackend backend, EllipticalArc arc, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.elliptical_arc_stroke requires a valid VGBackend");
            return false;
        }
        if (!arc.IsValid() || arc.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }

        EllipticalArc center_arc = arc.Normalized();
        if (MathF.Abs(center_arc.SweepAngle) >= (MathF.PI*2))
        {
            return EllipseStroke(backend, global::SkrGui.Ellipse.CenterRadius(center_arc.Center, center_arc.RadiusX, center_arc.RadiusY, center_arc.Rotation), stroke_width, options);
        }


        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float half_width = stroke_width * 0.5f;
        EllipticalArc inner_body_arc = center_arc.Deflate(half_width);
        EllipticalArc outer_body_arc = center_arc.Inflate(half_width);
        EllipticalArc outer_aa_arc = outer_body_arc.Inflate(resolved_aa_radius);
        float tolerance = global::SkrGui.EllipticalArc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = outer_body_arc.EstimateSegmentCount(tolerance);
        if (segment_count_hint == 0u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        if (inner_body_arc.IsEmpty())
        {
            return EllipticalFan(backend, outer_body_arc, options);
        }

        EllipticalArc inner_aa_arc = inner_body_arc.Deflate(resolved_aa_radius);
        bool has_inner_aa = has_aa && !inner_aa_arc.IsEmpty();
        ulong section_count = (ulong)(segment_count_hint) + 1u;
        ulong section_vertex_count = has_aa ? (has_inner_aa ? 4u : 3u) : 2u;
        ulong section_triangle_count = has_aa ? (has_inner_aa ? 6u : 4u) : 2u;
        backend.CallReserve(
            section_count * section_vertex_count,
            (ulong)(segment_count_hint) * section_triangle_count +
                (has_aa ? (has_inner_aa ? 4u : 2u) : 0u)
        );




        VGMeshEmitter emitter = new(backend);
        bool has_pending = false;
        bool has_ready_previous = false;
        VGBasicOpenStrokeSection first_section = new();
        VGBasicOpenStrokeSection ready_previous = new();
        VGBasicOpenStrokeSection pending_section = new();
        Offsetf previous_direction = Offsetf.Zero();

        outer_body_arc.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf outer_body_point, float _unused1, EllipseSampler sampler) => {
                VGBasicOpenStrokeSection current = new();
                if (has_inner_aa)
                {
                    current.InnerAaPos = sampler.SamplePoint(
                        center_arc.Center,
                        inner_aa_arc.RadiusX,
                        inner_aa_arc.RadiusY
                    );
                }
                current.InnerBodyPos = sampler.SamplePoint(
                    center_arc.Center,
                    inner_body_arc.RadiusX,
                    inner_body_arc.RadiusY
                );
                current.InnerBody = emitter.PushVertex(current.InnerBodyPos, 1.0f);
                current.OuterBodyPos = outer_body_point;
                current.OuterBody = emitter.PushVertex(current.OuterBodyPos, 1.0f);
                if (has_aa)
                {
                    current.OuterAaPos = sampler.SamplePoint(
                        center_arc.Center,
                        outer_aa_arc.RadiusX,
                        outer_aa_arc.RadiusY
                    );
                }

                if (!has_pending)
                {
                    has_pending = true;
                    pending_section = current;
                    if (!has_aa)
                    {
                        ready_previous = current;
                        has_ready_previous = true;
                    }
                    return;
                }

                Offsetf current_direction = current.OuterBodyPos - pending_section.OuterBodyPos;
                if (!has_aa)
                {
                    VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, current, false, false);
                    ready_previous = current;
                    pending_section = current;
                    previous_direction = current_direction;
                    return;
                }




                if (!has_ready_previous)
                {
                    Offsetf cap_offset_scope23 = current_direction.Normalize() * -resolved_aa_radius;
                    VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, cap_offset_scope23, has_inner_aa);
                    first_section = pending_section;
                    VGBasicOpenPrimitiveHelper.PushFlatCapAa(emitter, first_section, true, has_inner_aa);
                }
                else
                {
                    VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, Offsetf.Zero(), has_inner_aa);
                    VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, pending_section, true, has_inner_aa);
                }

                ready_previous = pending_section;
                has_ready_previous = true;
                pending_section = current;
                previous_direction = current_direction;
            }
        );

        if (has_aa && has_pending && has_ready_previous)
        {


            Offsetf cap_offset_scope24 = previous_direction.Normalize() * resolved_aa_radius;
            VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, cap_offset_scope24, has_inner_aa);
            VGBasicOpenPrimitiveHelper.PushFlatCapAa(emitter, pending_section, false, has_inner_aa);
            VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, pending_section, true, has_inner_aa);
        }
        return true;
    }
    public static bool SuperellipseFan(VGBackend backend, SuperellipseArc arc) => SuperellipseFan(backend,arc,new VGBasicPrimitiveOptions());
    // Original line 3159: superellipse_fan.
    public static bool SuperellipseFan(VGBackend backend, SuperellipseArc arc, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.superellipse_fan requires a valid VGBackend");
            return false;
        }
        if (!arc.IsValid() || arc.IsEmpty())
        {
            return false;
        }

        SuperellipseArc body_arc = arc.Normalized();
        if (MathF.Abs(body_arc.SweepAngle) >= (MathF.PI*2))
        {
            return Superellipse(backend, global::SkrGui.Superellipse.CenterRadius(
                    body_arc.Center,
                    body_arc.RadiusX,
                    body_arc.RadiusY,
                    body_arc.Rotation,
                    body_arc.Exponent
                ), options);
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float tolerance = global::SkrGui.SuperellipseArc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = body_arc.EstimateSegmentCount(tolerance);
        if (segment_count_hint == 0u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        ulong section_count = (ulong)(segment_count_hint) + 1u;
        ulong boundary_count = section_count + 1u;
        ulong vertex_count = has_aa ? boundary_count * 3u : boundary_count;
        ulong triangle_count = has_aa ?
            (ulong)(segment_count_hint) + boundary_count * 3u :
            (ulong)(segment_count_hint);
        backend.CallReserve(vertex_count, triangle_count);



        VGMeshEmitter emitter = new(backend);
        uint center_index = emitter.PushVertex(body_arc.Center, 1.0f);
        bool has_first = false;
        uint previous_body = 0u;
        if (!has_aa)
        {
            body_arc.SampleWithSampler(
                new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
                (Offsetf body_point, float _unused1, SuperellipseSampler _unused2) => {
                    uint current_body_scope13 = emitter.PushVertex(body_point, 1.0f);
                    if (has_first)
                    {
                        emitter.PushTriangle(center_index, previous_body, current_body_scope13);
                    }
                    else
                    {
                        has_first = true;
                    }

                    previous_body = current_body_scope13;
                }
            );
            return true;
        }




        float signed_aa_radius = body_arc.SweepAngle >= 0.0f ? resolved_aa_radius : -resolved_aa_radius;
        VGFillAANode center_node = VGFillAAHelper.BuildNode(
            body_arc.EndPoint(),
            body_arc.Center,
            body_arc.StartPoint(),
            center_index,
            resolved_aa_radius
        );
        bool has_center_node = false;
        bool has_pending = false;
        bool has_previous_node = false;
        uint pending_body = 0u;
        Offsetf previous_pos = body_arc.Center;
        Offsetf pending_pos = Offsetf.Zero();
        VGFillAANode previous_node = new();
        body_arc.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf body_point, float _unused1, SuperellipseSampler _unused2) => {
                uint current_body_scope21 = emitter.PushVertex(body_point, 1.0f);
                if (!has_center_node)
                {
                    has_center_node = true;
                    VGFillAAHelper.EmitOuterVertices(emitter, ref center_node, signed_aa_radius);
                }

                if (!has_pending)
                {
                    has_pending = true;
                    pending_body = current_body_scope21;
                    pending_pos = body_point;
                    return;
                }


                VGFillAANode pending_node = VGFillAAHelper.BuildNode(
                    previous_pos,
                    pending_pos,
                    body_point,
                    pending_body,
                    resolved_aa_radius
                );
                VGFillAAHelper.EmitOuterVertices(emitter, ref pending_node, signed_aa_radius);
                if (has_previous_node)
                {
                    VGFillAAHelper.ConnectSections(emitter, previous_node, pending_node);
                    VGFillAAHelper.EmitJoinTriangle(emitter, previous_node);
                }
                else
                {
                    VGFillAAHelper.ConnectSections(emitter, center_node, pending_node);
                    VGFillAAHelper.EmitJoinTriangle(emitter, center_node);
                }
                emitter.PushTriangle(center_index, pending_body, current_body_scope21);

                has_previous_node = true;
                previous_node = pending_node;
                previous_pos = pending_pos;
                pending_body = current_body_scope21;
                pending_pos = body_point;
            }
        );



        if (has_pending)
        {
            VGFillAANode last_node = VGFillAAHelper.BuildNode(
                previous_pos,
                pending_pos,
                body_arc.Center,
                pending_body,
                resolved_aa_radius
            );
            VGFillAAHelper.EmitOuterVertices(emitter, ref last_node, signed_aa_radius);
            if (has_previous_node)
            {
                VGFillAAHelper.ConnectSections(emitter, previous_node, last_node);
                VGFillAAHelper.EmitJoinTriangle(emitter, previous_node);
            }
            else
            {
                VGFillAAHelper.ConnectSections(emitter, center_node, last_node);
                VGFillAAHelper.EmitJoinTriangle(emitter, center_node);
            }
            VGFillAAHelper.ConnectSections(emitter, last_node, center_node);
            VGFillAAHelper.EmitJoinTriangle(emitter, last_node);
        }
        return true;
    }
    public static bool SuperellipseArcStroke(VGBackend backend, SuperellipseArc arc, float stroke_width) => SuperellipseArcStroke(backend,arc,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 3334: superellipse_arc_stroke.
    public static bool SuperellipseArcStroke(VGBackend backend, SuperellipseArc arc, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.superellipse_arc_stroke requires a valid VGBackend");
            return false;
        }
        if (!arc.IsValid() || arc.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }

        SuperellipseArc center_arc = arc.Normalized();
        if (MathF.Abs(center_arc.SweepAngle) >= (MathF.PI*2))
        {
            return SuperellipseStroke(backend, global::SkrGui.Superellipse.CenterRadius(
                    center_arc.Center,
                    center_arc.RadiusX,
                    center_arc.RadiusY,
                    center_arc.Rotation,
                    center_arc.Exponent
                ), stroke_width, options);
        }


        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float half_width = stroke_width * 0.5f;
        SuperellipseArc inner_body_arc = center_arc.Deflate(half_width);
        SuperellipseArc outer_body_arc = center_arc.Inflate(half_width);
        SuperellipseArc outer_aa_arc = outer_body_arc.Inflate(resolved_aa_radius);
        float tolerance = global::SkrGui.SuperellipseArc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        uint segment_count_hint = outer_body_arc.EstimateSegmentCount(tolerance);
        if (segment_count_hint == 0u)
        {
            return false;
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        if (inner_body_arc.IsEmpty())
        {
            return SuperellipseFan(backend, outer_body_arc, options);
        }

        SuperellipseArc inner_aa_arc = inner_body_arc.Deflate(resolved_aa_radius);
        bool has_inner_aa = has_aa && !inner_aa_arc.IsEmpty();
        ulong section_count = (ulong)(segment_count_hint) + 1u;
        ulong section_vertex_count = has_aa ? (has_inner_aa ? 4u : 3u) : 2u;
        ulong section_triangle_count = has_aa ? (has_inner_aa ? 6u : 4u) : 2u;
        backend.CallReserve(
            section_count * section_vertex_count,
            (ulong)(segment_count_hint) * section_triangle_count +
                (has_aa ? (has_inner_aa ? 4u : 2u) : 0u)
        );




        VGMeshEmitter emitter = new(backend);
        bool has_pending = false;
        bool has_ready_previous = false;
        VGBasicOpenStrokeSection first_section = new();
        VGBasicOpenStrokeSection ready_previous = new();
        VGBasicOpenStrokeSection pending_section = new();
        Offsetf previous_direction = Offsetf.Zero();

        outer_body_arc.SampleWithSampler(
            new ShapeToleranceSampleDesc{Tolerance=tolerance,Direction=EShapeSampleDirection.Forward,MaxDepth=16u},
            (Offsetf outer_body_point, float _unused1, SuperellipseSampler sampler) => {
                VGBasicOpenStrokeSection current = new();
                if (has_inner_aa)
                {
                    current.InnerAaPos = sampler.SamplePoint(
                        center_arc.Center,
                        inner_aa_arc.RadiusX,
                        inner_aa_arc.RadiusY
                    );
                }
                current.InnerBodyPos = sampler.SamplePoint(
                    center_arc.Center,
                    inner_body_arc.RadiusX,
                    inner_body_arc.RadiusY
                );
                current.InnerBody = emitter.PushVertex(current.InnerBodyPos, 1.0f);
                current.OuterBodyPos = outer_body_point;
                current.OuterBody = emitter.PushVertex(current.OuterBodyPos, 1.0f);
                if (has_aa)
                {
                    current.OuterAaPos = sampler.SamplePoint(
                        center_arc.Center,
                        outer_aa_arc.RadiusX,
                        outer_aa_arc.RadiusY
                    );
                }

                if (!has_pending)
                {
                    has_pending = true;
                    pending_section = current;
                    if (!has_aa)
                    {
                        ready_previous = current;
                        has_ready_previous = true;
                    }
                    return;
                }

                Offsetf current_direction = current.OuterBodyPos - pending_section.OuterBodyPos;
                if (!has_aa)
                {
                    VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, current, false, false);
                    ready_previous = current;
                    pending_section = current;
                    previous_direction = current_direction;
                    return;
                }




                if (!has_ready_previous)
                {
                    Offsetf cap_offset_scope23 = current_direction.Normalize() * -resolved_aa_radius;
                    VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, cap_offset_scope23, has_inner_aa);
                    first_section = pending_section;
                    VGBasicOpenPrimitiveHelper.PushFlatCapAa(emitter, first_section, true, has_inner_aa);
                }
                else
                {
                    VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, Offsetf.Zero(), has_inner_aa);
                    VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, pending_section, true, has_inner_aa);
                }

                ready_previous = pending_section;
                has_ready_previous = true;
                pending_section = current;
                previous_direction = current_direction;
            }
        );

        if (has_aa && has_pending && has_ready_previous)
        {


            Offsetf cap_offset_scope24 = previous_direction.Normalize() * resolved_aa_radius;
            VGBasicOpenPrimitiveHelper.PushStrokeSectionAa(emitter, ref pending_section, cap_offset_scope24, has_inner_aa);
            VGBasicOpenPrimitiveHelper.PushFlatCapAa(emitter, pending_section, false, has_inner_aa);
            VGBasicOpenPrimitiveHelper.ConnectStrokeSections(emitter, ready_previous, pending_section, true, has_inner_aa);
        }
        return true;
    }
    public static bool Rect(VGBackend backend, Rectf rect) => Rect(backend,rect,new VGBasicPrimitiveOptions());
    // Original line 3537: rect.
    public static bool Rect(VGBackend backend, Rectf rect, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.rect requires a valid VGBackend");
            return false;
        }
        if (!rect.IsFinite() || rect.IsEmpty())
        {
            return false;
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        bool has_aa = resolved_aa_radius > 0.0f;
        backend.CallReserve(has_aa ? 8u : 4u, has_aa ? 10u : 2u);


        VGMeshEmitter emitter = new(backend);
        uint body_tl = emitter.PushVertex(rect.TopLeft(), 1.0f);
        uint body_tr = emitter.PushVertex(rect.TopRight(), 1.0f);
        uint body_br = emitter.PushVertex(rect.BottomRight(), 1.0f);
        uint body_bl = emitter.PushVertex(rect.BottomLeft(), 1.0f);

        if (!has_aa)
        {
            emitter.PushQuad(body_tl, body_tr, body_br, body_bl);
            return true;
        }

        Rectf aa_rect = rect.Inflate(resolved_aa_radius);
        uint aa_tl = emitter.PushVertex(aa_rect.TopLeft(), 0.0f);
        uint aa_tr = emitter.PushVertex(aa_rect.TopRight(), 0.0f);
        uint aa_br = emitter.PushVertex(aa_rect.BottomRight(), 0.0f);
        uint aa_bl = emitter.PushVertex(aa_rect.BottomLeft(), 0.0f);


        emitter.PushQuad(aa_tl, aa_tr, body_tr, body_tl);
        emitter.PushQuad(body_tr, aa_tr, aa_br, body_br);
        emitter.PushQuad(body_br, aa_br, aa_bl, body_bl);
        emitter.PushQuad(aa_bl, aa_tl, body_tl, body_bl);
        emitter.PushQuad(body_tl, body_tr, body_br, body_bl);
        return true;
    }
    public static bool RectStroke(VGBackend backend, Rectf rect, float stroke_width) => RectStroke(backend,rect,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 3588: rect_stroke.
    public static bool RectStroke(VGBackend backend, Rectf rect, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.rect_stroke requires a valid VGBackend");
            return false;
        }
        if (!rect.IsFinite() || rect.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float half_width = stroke_width * 0.5f;
        Rectf inner_body_rect = rect.Deflate(half_width);
        Rectf outer_body_rect = rect.Inflate(half_width);
        if (inner_body_rect.IsEmpty())
        {
            return VGBasicPrimitives.Rect(backend, outer_body_rect, options);
        }

        bool has_aa = resolved_aa_radius > 0.0f;
        Rectf outer_aa_rect = outer_body_rect.Inflate(resolved_aa_radius);
        Rectf inner_aa_rect = inner_body_rect.Deflate(resolved_aa_radius);
        bool has_inner_aa = has_aa && !inner_aa_rect.IsEmpty();
        backend.CallReserve(
            has_aa ? (has_inner_aa ? 16u : 12u) : 8u,
            has_aa ? (has_inner_aa ? 24u : 16u) : 8u
        );


        VGMeshEmitter emitter = new(backend);
        VGBasicRectRingHelper.EmitFixedRing(emitter, outer_body_rect, inner_body_rect, outer_aa_rect, inner_aa_rect, has_aa, has_inner_aa);
        return true;
    }
    public static bool DiffRect(VGBackend backend, Rectf outer, Rectf inner) => DiffRect(backend,outer,inner,new VGBasicPrimitiveOptions());
    // Original line 3640: diff_rect.
    public static bool DiffRect(VGBackend backend, Rectf outer, Rectf inner, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.diff_rect requires a valid VGBackend");
            return false;
        }
        if (!outer.IsFinite() || outer.IsEmpty() || !inner.IsFinite())
        {
            return false;
        }



        if (inner.IsEmpty() || !outer.Overlaps(inner))
        {
            return Rect(backend, outer, options);
        }

        Rectf hole = outer.Intersect(inner);
        if (hole.IsEmpty())
        {
            return Rect(backend, outer, options);
        }
        if (hole.Left <= outer.Left && hole.Top <= outer.Top && hole.Right >= outer.Right && hole.Bottom >= outer.Bottom)
        {
            return true;
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        bool has_aa = resolved_aa_radius > 0.0f;
        bool touches_boundary =
            hole.Left <= outer.Left + VGBasicDiffRectGridHelper.k_same_epsilon ||
            hole.Top <= outer.Top + VGBasicDiffRectGridHelper.k_same_epsilon ||
            hole.Right >= outer.Right - VGBasicDiffRectGridHelper.k_same_epsilon ||
            hole.Bottom >= outer.Bottom - VGBasicDiffRectGridHelper.k_same_epsilon;
        if (touches_boundary)
        {



            backend.CallReserve(has_aa ? 48u : 16u, has_aa ? 64u : 16u);
            VGMeshEmitter emitter_scope3 = new(backend);
            VGBasicDiffRectGridHelper.Emit(emitter_scope3, outer, hole, resolved_aa_radius);
            return true;
        }

        Rectf outer_aa_rect = outer.Inflate(resolved_aa_radius);
        Rectf inner_aa_rect = hole.Deflate(resolved_aa_radius);
        bool has_inner_aa = has_aa && !inner_aa_rect.IsEmpty();
        backend.CallReserve(
            has_aa ? (has_inner_aa ? 16u : 12u) : 8u,
            has_aa ? (has_inner_aa ? 24u : 16u) : 8u
        );



        VGMeshEmitter emitter_scope5 = new(backend);
        VGBasicRectRingHelper.EmitFixedRing(emitter_scope5, outer, hole, outer_aa_rect, inner_aa_rect, has_aa, has_inner_aa);
        return true;
    }
    public static bool RRect(VGBackend backend, RRect rrect) => RRect(backend,rrect,new VGBasicPrimitiveOptions());
    // Original line 3718: rrect.
    public static bool RRect(VGBackend backend, RRect rrect, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.rrect requires a valid VGBackend");
            return false;
        }
        if (!rrect.IsFinite() || rrect.IsEmpty())
        {
            return false;
        }


        RRect value = VGBasicRRectHelper.Normalized(rrect);
        if (!VGBasicRRectHelper.HasAnyRadius(value))
        {
            return Rect(backend, value.Rect(), options);
        }
        if (VGBasicRRectHelper.IsFullEllipse(value))
        {
            return Ellipse(backend, global::SkrGui.Ellipse.CenterRadius(value.Center(), value.TlRadius.X, value.TlRadius.Y, 0.0f), options.PixelRatio, options.TessellationFactor, options.AaRadius);
        }



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        bool has_aa = resolved_aa_radius > 0.0f;
        float arc_tolerance = global::SkrGui.Arc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        float elliptical_tolerance = global::SkrGui.EllipticalArc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        ulong section_hint = VGBasicRRectHelper.EstimateBoundarySectionCount(value, arc_tolerance, elliptical_tolerance);
        backend.CallReserve(
            section_hint * (has_aa ? 3u : 1u),
            section_hint * (has_aa ? 4u : 2u)
        );




        VGMeshEmitter emitter = new(backend);
        VGBasicRRectBoundaryRecord record = VGBasicRRectHelper.EmitBoundaryRecord(emitter, value, resolved_aa_radius, arc_tolerance, elliptical_tolerance, has_aa, false);

        var connect_section = (uint previous_top, uint previous_bottom, uint current_top, uint current_bottom) => {
            VGBasicRRectHelper.PushQuadOrTriangle(emitter, previous_top, current_top, current_bottom, previous_bottom);
        };
        var connect_side = (EVGBasicRRectRange top_range, EVGBasicRRectRange bottom_range, bool reverse_top, bool reverse_bottom) => {
            ref VGBasicRRectBoundaryRange top_body_range = ref record.Ranges[VGBasicRRectHelper.RangeIndex(top_range)];
            ref VGBasicRRectBoundaryRange bottom_body_range = ref record.Ranges[VGBasicRRectHelper.RangeIndex(bottom_range)];
            uint section_count = CppMath.Max(top_body_range.Count, bottom_body_range.Count);
            if (section_count < 2u)
            {
                return;
            }

            uint previous_top = VGBasicRRectHelper.BodyIndexAt(record, top_range, reverse_top ? top_body_range.Count - 1u : 0u);
            uint previous_bottom = VGBasicRRectHelper.BodyIndexAt(record, bottom_range, reverse_bottom ? bottom_body_range.Count - 1u : 0u);
            for (uint i = 1u; i < section_count; ++i)
            {
                uint top_i = VGBasicRRectHelper.NearestPointIndex(top_body_range.Count, i, section_count);
                uint bottom_i = VGBasicRRectHelper.NearestPointIndex(bottom_body_range.Count, i, section_count);
                if (reverse_top)
                {
                    top_i = top_body_range.Count - 1u - top_i;
                }
                if (reverse_bottom)
                {
                    bottom_i = bottom_body_range.Count - 1u - bottom_i;
                }

                uint current_top = VGBasicRRectHelper.BodyIndexAt(record, top_range, top_i);
                uint current_bottom = VGBasicRRectHelper.BodyIndexAt(record, bottom_range, bottom_i);
                connect_section(previous_top, previous_bottom, current_top, current_bottom);
                previous_top = current_top;
                previous_bottom = current_bottom;
            }
        };



        connect_side(EVGBasicRRectRange.TLCorner, EVGBasicRRectRange.BLCorner, true, false);


        {
            ref VGBasicRRectBoundaryRange tl_range = ref record.Ranges[VGBasicRRectHelper.RangeIndex(EVGBasicRRectRange.TLCorner)];
            ref VGBasicRRectBoundaryRange tr_range = ref record.Ranges[VGBasicRRectHelper.RangeIndex(EVGBasicRRectRange.TRCorner)];
            ref VGBasicRRectBoundaryRange br_range = ref record.Ranges[VGBasicRRectHelper.RangeIndex(EVGBasicRRectRange.BRCorner)];
            ref VGBasicRRectBoundaryRange bl_range = ref record.Ranges[VGBasicRRectHelper.RangeIndex(EVGBasicRRectRange.BLCorner)];
            connect_section(
                VGBasicRRectHelper.BodyIndexAt(record, EVGBasicRRectRange.TLCorner, tl_range.Count - 1u),
                VGBasicRRectHelper.BodyIndexAt(record, EVGBasicRRectRange.BLCorner, 0u),
                VGBasicRRectHelper.BodyIndexAt(record, EVGBasicRRectRange.TRCorner, 0u),
                VGBasicRRectHelper.BodyIndexAt(record, EVGBasicRRectRange.BRCorner, br_range.Count - 1u)
            );
            _ = tr_range;
            _ = bl_range;
        }



        connect_side(EVGBasicRRectRange.TRCorner, EVGBasicRRectRange.BRCorner, false, true);
        return true;
    }
    public static bool RRectStroke(VGBackend backend, RRect rrect, float stroke_width) => RRectStroke(backend,rrect,stroke_width,new VGBasicPrimitiveOptions());
    // Original line 3863: rrect_stroke.
    public static bool RRectStroke(VGBackend backend, RRect rrect, float stroke_width, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.rrect_stroke requires a valid VGBackend");
            return false;
        }
        if (!rrect.IsFinite() || rrect.IsEmpty() || !float.IsFinite(stroke_width) || stroke_width <= 0.0f)
        {
            return false;
        }


        RRect center = VGBasicRRectHelper.Normalized(rrect);
        float half_width = stroke_width * 0.5f;
        RRect outer = VGBasicRRectHelper.Normalized(center.Inflate(half_width));
        RRect inner = VGBasicRRectHelper.Normalized(center.Deflate(half_width));
        if (inner.IsEmpty())
        {
            return VGBasicPrimitives.RRect(backend, outer, options);
        }

        return DiffRRect(backend, outer, inner, options);
    }
    public static bool DiffRRect(VGBackend backend, RRect outer, RRect inner) => DiffRRect(backend,outer,inner,new VGBasicPrimitiveOptions());
    // Original line 3893: diff_rrect.
    public static bool DiffRRect(VGBackend backend, RRect outer, RRect inner, VGBasicPrimitiveOptions options)
    {

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGBasicPrimitives.diff_rrect requires a valid VGBackend");
            return false;
        }
        if (!outer.IsFinite() || outer.IsEmpty() || !inner.IsFinite())
        {
            return false;
        }


        RRect outer_value = VGBasicRRectHelper.Normalized(outer);
        if (inner.IsEmpty() || !outer_value.Rect().Overlaps(inner.Rect()))
        {
            return RRect(backend, outer_value, options);
        }

        Rectf clamped_inner_rect = outer_value.Rect().Intersect(inner.Rect());
        if (clamped_inner_rect.IsEmpty())
        {
            return RRect(backend, outer_value, options);
        }
        if (
            clamped_inner_rect.Left <= outer_value.Left &&
            clamped_inner_rect.Top <= outer_value.Top &&
            clamped_inner_rect.Right >= outer_value.Right &&
            clamped_inner_rect.Bottom >= outer_value.Bottom
        )
        {
            return true;
        }

        RRect inner_value = VGBasicRRectHelper.Normalized(new RRect(clamped_inner_rect.Left, clamped_inner_rect.Top, clamped_inner_rect.Right, clamped_inner_rect.Bottom, inner.TlRadius, inner.TrRadius, inner.BrRadius, inner.BlRadius));



        float resolved_aa_radius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        bool has_aa = resolved_aa_radius > 0.0f;
        float arc_tolerance = global::SkrGui.Arc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        float elliptical_tolerance = global::SkrGui.EllipticalArc.CalcTolerance(
            options.TessellationFactor,
            options.PixelRatio
        );
        ulong outer_hint = VGBasicRRectHelper.EstimateBoundarySectionCount(outer_value, arc_tolerance, elliptical_tolerance);
        ulong inner_hint = VGBasicRRectHelper.EstimateBoundarySectionCount(inner_value, arc_tolerance, elliptical_tolerance);
        ulong section_hint = CppMath.Max(outer_hint, inner_hint);




        if (VGBasicRRectHelper.RectTouchesBoundary(inner_value.Rect(), outer_value.Rect()))
        {
            backend.CallReserve(
                section_hint * (has_aa ? 4u : 2u),
                section_hint * (has_aa ? 6u : 2u)
            );
            VGMeshEmitter emitter_scope7 = new(backend);
            VGBasicRRectHelper.EmitPairedDiffRrect(emitter_scope7, outer_value, inner_value, resolved_aa_radius, arc_tolerance, elliptical_tolerance, has_aa);
            return true;
        }


        if (!VGBasicRRectHelper.HasAnyRadius(outer_value) && !VGBasicRRectHelper.HasAnyRadius(inner_value))
        {
            return DiffRect(backend, outer_value.Rect(), inner_value.Rect(), options);
        }

        backend.CallReserve(
            section_hint * (has_aa ? 4u : 2u),
            section_hint * (has_aa ? 6u : 2u)
        );




        VGMeshEmitter emitter_scope8 = new(backend);
        VGBasicRRectBoundaryRecord inner_record = VGBasicRRectHelper.EmitBoundaryRecord(emitter_scope8, inner_value, resolved_aa_radius, arc_tolerance, elliptical_tolerance, has_aa, true);
        VGBasicRRectBoundaryRecord outer_record = VGBasicRRectHelper.EmitBoundaryRecord(emitter_scope8, outer_value, resolved_aa_radius, arc_tolerance, elliptical_tolerance, has_aa, false);





        for (uint range_i = 0u; range_i < VGBasicRRectHelper.RangeIndex(EVGBasicRRectRange.Count); ++range_i)
        {
            EVGBasicRRectRange range = (EVGBasicRRectRange)(range_i);
            ref VGBasicRRectBoundaryRange inner_range = ref inner_record.Ranges[range_i];
            ref VGBasicRRectBoundaryRange outer_range = ref outer_record.Ranges[range_i];
            uint section_count = CppMath.Max(inner_range.Count, outer_range.Count);
            if (section_count < 2u)
            {
                continue;
            }

            uint previous_inner = VGBasicRRectHelper.BodyIndexAt(inner_record, range, 0u);
            uint previous_outer = VGBasicRRectHelper.BodyIndexAt(outer_record, range, 0u);
            for (uint i = 1u; i < section_count; ++i)
            {
                uint inner_i = VGBasicRRectHelper.NearestPointIndex(inner_range.Count, i, section_count);
                uint outer_i = VGBasicRRectHelper.NearestPointIndex(outer_range.Count, i, section_count);
                uint current_inner = VGBasicRRectHelper.BodyIndexAt(inner_record, range, inner_i);
                uint current_outer = VGBasicRRectHelper.BodyIndexAt(outer_record, range, outer_i);
                VGBasicRRectHelper.PushQuadOrTriangle(emitter_scope8, previous_inner, previous_outer, current_outer, current_inner);
                previous_inner = current_inner;
                previous_outer = current_outer;
            }
        }
        return true;
    }
}
