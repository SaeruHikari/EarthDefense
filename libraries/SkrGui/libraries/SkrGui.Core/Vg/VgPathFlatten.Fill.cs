// Source: src/vg/vg_path_flatten.fill.cpp @ 611561f8.
using System.Diagnostics;
using static SkrGui.TessNative;
namespace SkrGui;
internal static unsafe partial class FillTessContext
{
    // Original line 248: to_tess_winding_rule.
    public static int ToTessWindingRule(EVGFillRule FillRule)
    {
        switch (FillRule)
        {
        case EVGFillRule.EvenOdd:
            return TESS_WINDING_ODD;
        case EVGFillRule.NonZero:
        default:
            return TESS_WINDING_NONZERO;
        }
    }
    // Original line 404: is_tess_point_valid.
    public static bool IsTessPointValid(Offsetf point)
    {
        return point.IsFinite() &&
            point.X >= TESS_MIN_VALID_INPUT_VALUE &&
            point.X <= TESS_MAX_VALID_INPUT_VALUE &&
            point.Y >= TESS_MIN_VALID_INPUT_VALUE &&
            point.Y <= TESS_MAX_VALID_INPUT_VALUE;
    }
    // Original line 413: is_tess_point_equal.
    public static bool IsTessPointEqual(TessContour contour, Offsetf point)
    {
        if (contour.Vertices.IsEmpty())
        {
            return false;
        }

        ulong last = contour.Vertices.Size() - 2u;
        return contour.Vertices[last] == (float)(point.X) &&
            contour.Vertices[last + 1u] == (float)(point.Y);
    }
    // Original line 425: add_point.
    public static void AddPoint(TessContour contour, Offsetf point)
    {
        contour.Vertices.Add((float)(point.X));
        contour.Vertices.Add((float)(point.Y));
    }
    // Original line 431: remove_repeated_closing_point.
    public static void RemoveRepeatedClosingPoint(TessContour contour)
    {
        if (contour.VertexCount() < 2u)
        {
            return;
        }

        ulong last = contour.Vertices.Size() - 2u;
        if (contour.Vertices[0] == contour.Vertices[last] &&
            contour.Vertices[1] == contour.Vertices[last + 1u])
        {
            contour.Vertices.RemoveAt(last, 2u);
        }
    }
    // Original line 446: analyze_contour.
    public static ContourAnalysis AnalyzeContour(TessContour contour)
    {
        ContourAnalysis analysis = new();

        ulong VertexCount = contour.VertexCount();
        if (VertexCount < 3u)
        {
            return analysis;
        }

        double anchor_x = (double)(contour.Vertices[0]);
        double anchor_y = (double)(contour.Vertices[1]);
        bool has_second_point = false;
        double second_x = 0.0;
        double second_y = 0.0;

        for (ulong i = 0u; i < VertexCount; ++i)
        {
            ulong next = (i + 1u) % VertexCount;
            double x0 = (double)(contour.Vertices[i * 2u]);
            double y0 = (double)(contour.Vertices[i * 2u + 1u]);
            double x1 = (double)(contour.Vertices[next * 2u]);
            double y1 = (double)(contour.Vertices[next * 2u + 1u]);
            analysis.SignedArea += x0 * y1 - x1 * y0;

            if (!has_second_point)
            {
                if (VgScalar.Abs(x0 - anchor_x) <= KAreaEpsilon &&
                    VgScalar.Abs(y0 - anchor_y) <= KAreaEpsilon)
                {
                    continue;
                }
                second_x = x0;
                second_y = y0;
                has_second_point = true;
                continue;
            }

            if (!analysis.HasNonCollinearArea)
            {
                double Cross =
                    (second_x - anchor_x) * (y0 - anchor_y) -
                    (second_y - anchor_y) * (x0 - anchor_x);
                if (VgScalar.Abs(Cross) > KAreaEpsilon)
                {
                    analysis.HasNonCollinearArea = true;
                }
            }
        }

        analysis.SignedArea *= 0.5;
        return analysis;
    }
    // Original line 500: reverse.
    public static void Reverse(TessContour contour)
    {
        ulong VertexCount = contour.VertexCount();
        for (ulong left = 0u, right = VertexCount - 1u; left < right; ++left, --right)
        {
            (contour.Vertices[left * 2u],contour.Vertices[right * 2u])=(contour.Vertices[right * 2u],contour.Vertices[left * 2u]);
            (contour.Vertices[left * 2u + 1u],contour.Vertices[right * 2u + 1u])=(contour.Vertices[right * 2u + 1u],contour.Vertices[left * 2u + 1u]);
        }
    }
    // Original line 510: build_contour.
    public static bool BuildContour(VgBuffer<VGPathFlattenNode> nodes,
    VGPathFlattenContour contour,
    TessContour output_contour)
    {
        output_contour.Vertices.Clear();
        if (contour.NodeCount < 3u)
        {
            return false;
        }



        output_contour.Vertices.Reserve((ulong)(contour.NodeCount) * 2u);
        for (uint i = 0u; i < contour.NodeCount; ++i)
        {
            VGPathFlattenNode node = nodes[contour.NodeBegin + i];
            if (!IsTessPointValid(node.Position))
            {
                continue;
            }

            if (IsTessPointEqual(output_contour, node.Position))
            {
                continue;
            }
            AddPoint(output_contour, node.Position);
        }
        RemoveRepeatedClosingPoint(output_contour);

        ContourAnalysis analysis = AnalyzeContour(output_contour);
        if (!analysis.HasNonCollinearArea)
        {
            output_contour.Vertices.Clear();
            return false;
        }
        return true;
    }
    // Original line 561: add_input_contours.
    public static ulong AddInputContours(void* Tess,
    VgBuffer<VGPathFlattenNode> nodes,
    VgBuffer<VGPathFlattenContour> contours,
    EVGFillRule FillRule,
    TessContour scratch_contour)
    {
        ulong contour_count = 0u;
        foreach (VGPathFlattenContour contour in contours)
        {
            if (BuildContour(nodes, contour, scratch_contour))
            {
                AddContour(Tess, scratch_contour);
                ++contour_count;
            }
        }
        return contour_count;
    }
    // Original line 581: boundary_contour_area.
    public static double BoundaryContourArea(float* Vertices, int basis, int count)
    {
        double result = 0.0;
        for (int i = 0; i < count; ++i)
        {
            int next = (i + 1) % count;
            int current_index = basis + i;
            int next_index = basis + next;
            double x0 = (double)(Vertices[current_index * 2]);
            double y0 = (double)(Vertices[current_index * 2 + 1]);
            double x1 = (double)(Vertices[next_index * 2]);
            double y1 = (double)(Vertices[next_index * 2 + 1]);
            result += x0 * y1 - x1 * y0;
        }
        return result * 0.5;
    }
    // Original line 598: is_boundary_contour_valid.
    public static bool IsBoundaryContourValid(float* Vertices,
    int VertexCount,
    int basis,
    int count)
    {
        if (Vertices == null || VertexCount <= 0)
        {
            return false;
        }
        if (basis < 0 || count < 3 || count > VertexCount || basis > VertexCount - count)
        {
            return false;
        }
        return VgScalar.Abs(BoundaryContourArea(Vertices, basis, count)) > KAreaEpsilon;
    }
    // Original line 616: vertex_key.
    public static VGFillVertexKey VertexKey(Offsetf point)
    {
        return new VGFillVertexKey(point.X == 0.0f ? 0.0f : point.X,
            point.Y == 0.0f ? 0.0f : point.Y);
    }
    // Original line 642: add_boundary_contours.
    public static ulong AddBoundaryContours(void* Tess, void* boundary_tess)
    {
        int VertexCount = TessNative.GetVertexCount(boundary_tess);
        float* Vertices = TessNative.GetVertices(boundary_tess);
        int element_count = TessNative.GetElementCount(boundary_tess);
        int* elements = TessNative.GetElements(boundary_tess);
        if (VertexCount <= 0 || Vertices == null || element_count <= 0 || elements == null)
        {
            return 0u;
        }

        ulong contour_count = 0u;
        for (int element_index = 0; element_index < element_count; ++element_index)
        {
            int basis = elements[element_index * 2];
            int count = elements[element_index * 2 + 1];
            if (!IsBoundaryContourValid(Vertices, VertexCount, basis, count))
            {
                continue;
            }

            TessNative.AddContour(
                Tess,
                2,
                Vertices + basis * 2,
                sizeof(float) * 2,
                count
            );
            ++contour_count;
        }
        return contour_count;
    }
    // Original line 675: boundary_vertex.
    public static Offsetf BoundaryVertex(float* Vertices, int index)
    {
        return new Offsetf(
            (float)(Vertices[index * 2]),
            (float)(Vertices[index * 2 + 1])
        );
    }
    // Original line 683: build_aa_node.
    public static VGFillAANode BuildAaNode(float* Vertices,
    int basis,
    int count,
    int point_index,
    float AaRadius,
    float AaMiterLimit)
    {
        int previous = point_index == 0 ? count - 1 : point_index - 1;
        int next = (point_index + 1) % count;
        Offsetf previous_pos = BoundaryVertex(Vertices, basis + previous);
        Offsetf current_pos = BoundaryVertex(Vertices, basis + point_index);
        Offsetf next_pos = BoundaryVertex(Vertices, basis + next);

        return VGFillAAHelper.BuildNode(
            previous_pos,
            current_pos,
            next_pos,
            0u,
            AaRadius,
            AaMiterLimit
        );
    }
    // Original line 708: calc_aa_reserve.
    public static FillReserveEstimate CalcAaReserve(void* boundary_tess,
    Dictionary<VGFillVertexKey,uint> VertexLookup,
    float AaRadius,
    float AaMiterLimit)
    {
        FillReserveEstimate estimate = new();
        if (AaRadius <= KGeometryEpsilon)
        {
            return estimate;
        }

        int VertexCount = TessNative.GetVertexCount(boundary_tess);
        float* Vertices = TessNative.GetVertices(boundary_tess);
        int element_count = TessNative.GetElementCount(boundary_tess);
        int* elements = TessNative.GetElements(boundary_tess);
        if (VertexCount <= 0 || Vertices == null || element_count <= 0 || elements == null)
        {
            return estimate;
        }

        for (int element_index = 0; element_index < element_count; ++element_index)
        {
            int basis = elements[element_index * 2];
            int count = elements[element_index * 2 + 1];
            if (!IsBoundaryContourValid(Vertices, VertexCount, basis, count))
            {
                continue;
            }

            ulong bevel_count = 0u;
            ulong missing_inner_count = 0u;
            for (int i = 0; i < count; ++i)
            {
                VGFillAANode node = BuildAaNode(Vertices, basis, count, i, AaRadius, AaMiterLimit);
                bevel_count += node.IsBevel() ? 1u : 0u;
                if (!VertexLookup.ContainsKey(VertexKey(node.Position)))
                {
                    ++missing_inner_count;
                }
            }
            estimate.VertexCount += (ulong)(count) + bevel_count + missing_inner_count;
            estimate.TriangleCount += (ulong)(count) * 2u + bevel_count;
        }
        return estimate;
    }
    // Original line 756: emit_aa_ring.
    public static void EmitAaRing(VGMeshEmitter Emitter,
    void* boundary_tess,
    Dictionary<VGFillVertexKey,uint> VertexLookup,
    float AaRadius,
    float AaMiterLimit)
    {
        if (AaRadius <= KGeometryEpsilon)
        {
            return;
        }

        int VertexCount = TessNative.GetVertexCount(boundary_tess);
        float* Vertices = TessNative.GetVertices(boundary_tess);
        int element_count = TessNative.GetElementCount(boundary_tess);
        int* elements = TessNative.GetElements(boundary_tess);
        if (VertexCount <= 0 || Vertices == null || element_count <= 0 || elements == null)
        {
            return;
        }

        for (int element_index = 0; element_index < element_count; ++element_index)
        {
            int basis = elements[element_index * 2];
            int count = elements[element_index * 2 + 1];
            if (!IsBoundaryContourValid(Vertices, VertexCount, basis, count))
            {
                continue;
            }

            VGFillAAHelper.EmitRingNodes(
                Emitter,
                (uint)(count),
                (uint point_index) => {
                    VGFillAANode node = BuildAaNode(
                        Vertices,
                        basis,
                        count,
                        (int)(point_index),
                        AaRadius,
                        AaMiterLimit
                    );
                    node.InnerIndex = FindOrPushInnerVertex(Emitter, VertexLookup, node.Position);
                    return node;
                },
                AaRadius
            );
        }
    }
    // Original line 807: convex_point.
    public static Offsetf ConvexPoint(VgBuffer<VGPathFlattenNode> nodes,
    VGPathFlattenContour contour,
    uint point_index,
    bool Reverse)
    {
        uint node_index =
            Reverse && point_index != 0u ? contour.NodeCount - point_index : point_index;
        return nodes[contour.NodeBegin + node_index].Position;
    }
    // Original line 819: build_convex_aa_node.
    public static VGFillAANode BuildConvexAaNode(VgBuffer<VGPathFlattenNode> nodes,
    VGPathFlattenContour contour,
    uint point_index,
    uint InnerIndex,
    bool Reverse,
    float AaRadius,
    float AaMiterLimit)
    {
        uint previous_index =
            point_index == 0u ? contour.NodeCount - 1u : point_index - 1u;
        uint next_index =
            point_index + 1u == contour.NodeCount ? 0u : point_index + 1u;
        return VGFillAAHelper.BuildNode(
            ConvexPoint(nodes, contour, previous_index, Reverse),
            ConvexPoint(nodes, contour, point_index, Reverse),
            ConvexPoint(nodes, contour, next_index, Reverse),
            InnerIndex,
            AaRadius,
            AaMiterLimit
        );
    }
    // Original line 843: calc_convex_reserve.
    public static FillReserveEstimate CalcConvexReserve(VgBuffer<VGPathFlattenNode> nodes,
    VGPathFlattenContour contour,
    bool Reverse,
    float AaRadius,
    float AaMiterLimit)
    {
        FillReserveEstimate estimate = new(contour.NodeCount,
            (ulong)(contour.NodeCount) - 2u);
        if (AaRadius <= KGeometryEpsilon)
        {
            return estimate;
        }

        ulong bevel_count = 0u;
        for (uint i = 0u; i < contour.NodeCount; ++i)
        {
            VGFillAANode node = BuildConvexAaNode(
                nodes,
                contour,
                i,
                0u,
                Reverse,
                AaRadius,
                AaMiterLimit
            );
            bevel_count += node.IsBevel() ? 1u : 0u;
        }

        estimate.VertexCount += (ulong)(contour.NodeCount) + bevel_count;
        estimate.TriangleCount += (ulong)(contour.NodeCount) * 2u + bevel_count;
        return estimate;
    }
    // Original line 880: emit_convex.
    public static bool EmitConvex(VGBackend backend,
    VgBuffer<VGPathFlattenNode> nodes,
    VGPathFlattenContour contour,
    float AaRadius,
    float AaMiterLimit)
    {
        Debug.Assert(contour.Closed && contour.Convex && contour.NodeCount >= 3u);



        bool Reverse = VgFlags.All(
            nodes[contour.NodeBegin].Flags,
            EVGPathFlattenNodeFlags.Left
        );
        bool HasAa = AaRadius > KGeometryEpsilon;
        FillReserveEstimate reserve = CalcConvexReserve(
            nodes,
            contour,
            Reverse,
            AaRadius,
            AaMiterLimit
        );
        if (reserve.VertexCount > uint.MaxValue)
        {
            Debug.Assert(false , "Fill emitted more Vertices than uint32 indices can address");
            return false;
        }

        backend.CallReserve(reserve.VertexCount, reserve.TriangleCount);

        VGMeshEmitter Emitter = new(backend);
        var emit_node = (uint point_index) => {
            Offsetf position = ConvexPoint(nodes, contour, point_index, Reverse);
            uint InnerIndex = Emitter.PushVertex(position, 1.0f);
            if (!HasAa)
            {
                VGFillAANode node_scope2 = new();
                node_scope2.Position = position;
                node_scope2.InnerIndex = InnerIndex;
                return node_scope2;
            }

            VGFillAANode node_scope3 = BuildConvexAaNode(
                nodes,
                contour,
                point_index,
                InnerIndex,
                Reverse,
                AaRadius,
                AaMiterLimit
            );
            VGFillAAHelper.EmitOuterVertices(Emitter, ref node_scope3, AaRadius);
            return node_scope3;
        };
        var connect_aa = (VGFillAANode previous, VGFillAANode current) => {
            if (!HasAa)
            {
                return;
            }



            VGFillAAHelper.ConnectSections(Emitter, previous, current, true);
        };
        var emit_join = (VGFillAANode node) => {
            if (HasAa)
            {
                VGFillAAHelper.EmitJoinTriangle(Emitter, node);
            }
        };

        VGFillAANode start = emit_node(0u);
        ConvexFillSection previous = new(emit_node(1u),
            emit_node(contour.NodeCount - 1u));

        connect_aa(start, previous.Forward);
        emit_join(start);
        connect_aa(previous.Backward, start);
        emit_join(previous.Backward);

        uint Forward = 2u;
        uint Backward = contour.NodeCount - 2u;
        if (Forward > Backward)
        {
            connect_aa(previous.Forward, previous.Backward);
            emit_join(previous.Forward);
        }
        Emitter.PushTriangle(
            start.InnerIndex,
            previous.Forward.InnerIndex,
            previous.Backward.InnerIndex
        );

        while (Forward < Backward)
        {
            ConvexFillSection current = new(emit_node(Forward),
                emit_node(Backward));

            connect_aa(previous.Forward, current.Forward);
            emit_join(previous.Forward);
            connect_aa(current.Backward, previous.Backward);
            emit_join(current.Backward);

            ++Forward;
            --Backward;
            if (Forward > Backward)
            {
                connect_aa(current.Forward, current.Backward);
                emit_join(current.Forward);
            }

            Emitter.PushQuad(
                previous.Backward.InnerIndex,
                previous.Forward.InnerIndex,
                current.Forward.InnerIndex,
                current.Backward.InnerIndex
            );
            previous = current;
        }

        if (Forward == Backward)
        {
            VGFillAANode center = emit_node(Forward);
            connect_aa(previous.Forward, center);
            emit_join(previous.Forward);
            connect_aa(center, previous.Backward);
            emit_join(center);
            Emitter.PushTriangle(
                previous.Backward.InnerIndex,
                previous.Forward.InnerIndex,
                center.InnerIndex
            );
        }
        return true;
    }
    // Original line 1022: build_boundary_contours.
    public static bool BuildBoundaryContours(void* Tess,
    EVGFillRule FillRule)
    {
        int ok = TessNative.Tesselate(
            Tess,
            ToTessWindingRule(FillRule),
            TESS_BOUNDARY_CONTOURS,
            0,
            2,
            null
        );
        if (ok == 0)
        {
            Debug.Assert(false , "VGPathFlatten.Fill failed to build libtess2 boundary contours");
            return false;
        }
        return true;
    }
    // Original line 1043: emit_triangles.
    public static bool EmitTriangles(VGBackend backend,
    void* boundary_tess,
    EVGFillRule FillRule,
    float AaRadius,
    float AaMiterLimit,
    bool UseDelaunay,
    VGFillWorkspace workspace,
    VGFillAllocator allocator)
    {
        int boundary_vertex_count = TessNative.GetVertexCount(boundary_tess);
        using TessHandle Tess = MakeTess(
            boundary_vertex_count > 0 ? (ulong)(boundary_vertex_count) : 0u,
            allocator
        );
        if (!Tess.IsValid())
        {
            Debug.Assert(false , "VGPathFlatten.Fill failed to create libtess2 triangle tesselator");
            return false;
        }

        ulong contour_count = AddBoundaryContours(Tess.Tess, boundary_tess);
        if (contour_count == 0u)
        {
            return true;
        }

        TessNative.SetOption(
            Tess.Tess,
            TESS_CONSTRAINED_DELAUNAY_TRIANGULATION,
            UseDelaunay ? 1 : 0
        );

        int ok = TessNative.Tesselate(
            Tess.Tess,
            ToTessWindingRule(FillRule),
            TESS_POLYGONS,
            3,
            2,
            null
        );
        if (ok == 0)
        {
            Debug.Assert(false , "VGPathFlatten.Fill failed to triangulate libtess2 boundary contours");
            return false;
        }

        int VertexCount = TessNative.GetVertexCount(Tess.Tess);
        float* Vertices = TessNative.GetVertices(Tess.Tess);
        int element_count = TessNative.GetElementCount(Tess.Tess);
        int* elements = TessNative.GetElements(Tess.Tess);

        ulong TriangleCount = 0u;
        for (int element_index = 0; element_index < element_count; ++element_index)
        {
            int* triangle = &elements[element_index * 3];
            if (triangle[0] == TESS_UNDEF ||
                triangle[1] == TESS_UNDEF ||
                triangle[2] == TESS_UNDEF)
            {
                continue;
            }
            if (triangle[0] < 0 || triangle[0] >= VertexCount ||
                triangle[1] < 0 || triangle[1] >= VertexCount ||
                triangle[2] < 0 || triangle[2] >= VertexCount)
            {
                continue;
            }
            ++TriangleCount;
        }

        if (TriangleCount == 0u)
        {
            return true;
        }

        Dictionary<VGFillVertexKey,uint> VertexLookup = workspace.VertexLookup;
        FillReserveEstimate aa_reserve = new();
        if (AaRadius > KGeometryEpsilon)
        {


            VertexLookup.EnsureCapacity((int)(VertexCount));
            for (int vertex_index = 0; vertex_index < VertexCount; ++vertex_index)
            {
                Offsetf position_scope2 = new Offsetf(
                    (float)(Vertices[vertex_index * 2]),
                    (float)(Vertices[vertex_index * 2 + 1])
                );
                VertexLookup.TryAdd(VertexKey(position_scope2), (uint)(vertex_index));
            }
            aa_reserve = CalcAaReserve(boundary_tess, VertexLookup, AaRadius, AaMiterLimit);
        }

        ulong total_vertex_count = (ulong)(VertexCount) + aa_reserve.VertexCount;
        if (total_vertex_count > uint.MaxValue)
        {
            Debug.Assert(false , "Fill emitted more Vertices than uint32 indices can address");
            return false;
        }

        backend.CallReserve(total_vertex_count, TriangleCount + aa_reserve.TriangleCount);

        VGMeshEmitter Emitter = new(backend);
        for (int vertex_index = 0; vertex_index < VertexCount; ++vertex_index)
        {
            Offsetf position_scope4 = new Offsetf(
                (float)(Vertices[vertex_index * 2]),
                (float)(Vertices[vertex_index * 2 + 1])
            );
            Emitter.PushVertex(position_scope4, 1.0f);
        }

        EmitAaRing(Emitter, boundary_tess, VertexLookup, AaRadius, AaMiterLimit);

        for (int element_index = 0; element_index < element_count; ++element_index)
        {
            int* triangle = &elements[element_index * 3];
            if (triangle[0] == TESS_UNDEF ||
                triangle[1] == TESS_UNDEF ||
                triangle[2] == TESS_UNDEF)
            {
                continue;
            }
            if (triangle[0] < 0 || triangle[0] >= VertexCount ||
                triangle[1] < 0 || triangle[1] >= VertexCount ||
                triangle[2] < 0 || triangle[2] >= VertexCount)
            {
                continue;
            }
            Emitter.PushTriangle(
                (uint)(triangle[0]),
                (uint)(triangle[1]),
                (uint)(triangle[2])
            );
        }
        return true;
    }
}
public sealed unsafe partial class VGPathFlatten
{
    // Original line 1187: fill.
    public bool Fill(VGBackend backend, VGFillOptions options, VGFillWorkspace? input_workspace = null)
    {
        Debug.Assert(_finalized , "VGPathFlatten.Fill requires finalized contours");

        if (!backend.IsValid())
        {
            Debug.Assert(false , "VGPathFlatten.Fill requires a valid VGBackend");
            return false;
        }

        VGFillWorkspace local_workspace = new();
        VGFillWorkspace workspace = input_workspace != null ? input_workspace : local_workspace;
        workspace.Clear();



        float AaRadius =
            VGAAHelper.ResolveLogicalRadius(options.AaRadius, options.PixelRatio);
        float AaMiterLimit =
            (float.IsFinite(options.AaMiterLimit) && options.AaMiterLimit > KEpsilon) ?
            options.AaMiterLimit :
            2.4f;

        if (_contours.IsEmpty())
        {
            return true;
        }



        if (options.OptimizeForSingleConvex && _contours.Size() == 1u)
        {
            VGPathFlattenContour contour = _contours[0];
            if (contour.Closed &&
                contour.Convex &&
                contour.NodeCount >= 3u &&
                (ulong)(contour.NodeBegin) + contour.NodeCount <= _nodes.Size())
            {
                return FillTessContext.EmitConvex(
                    backend,
                    _nodes,
                    contour,
                    AaRadius,
                    AaMiterLimit
                );
            }
        }

        bool UseDelaunay =
            options.OptimizeForSingleConvex ? false : options.UseDelaunay;
        VGFillAllocator allocator = FillTessContext.ResolveAllocator(workspace);
        using FillTessContext.TessHandle boundary_tess = FillTessContext.MakeTess(_nodes.Size(), allocator);
        if (!boundary_tess.IsValid())
        {
            Debug.Assert(false , "VGPathFlatten.Fill failed to create libtess2 boundary tesselator");
            return false;
        }

        FillTessContext.TessContour scratch_contour = new(workspace.ScratchContourVertices);
        ulong contour_count = FillTessContext.AddInputContours(
            boundary_tess.Tess,
            _nodes,
            _contours,
            options.FillRule,
            scratch_contour
        );
        if (contour_count == 0u)
        {
            return true;
        }

        if (!FillTessContext.BuildBoundaryContours(
                boundary_tess.Tess,
                options.FillRule
            ))
        {
            return false;
        }

        return FillTessContext.EmitTriangles(
            backend,
            boundary_tess.Tess,
            options.FillRule,
            AaRadius,
            AaMiterLimit,
            UseDelaunay,
            workspace,
            allocator
        );
    }
}
