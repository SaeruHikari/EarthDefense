// Source: src/vg/vg_path_flatten.cpp @ 611561f8.
// Syntax mapping preserves retained-node loops, mutation and winding rules.
using System.Diagnostics;
namespace SkrGui;
public sealed partial class VGPathFlatten
{
    // Original line 12: nodes.
    public IReadOnlyList<VGPathFlattenNode> Nodes()
    {
        return _nodes;
    }
    // Original line 17: contours.
    public IReadOnlyList<VGPathFlattenContour> Contours()
    {
        return _contours;
    }
    // Original line 22: bounds.
    public Rectf Bounds()
    {
        return _bounds;
    }
    // Original line 27: is_empty.
    public bool IsEmpty()
    {
        return _nodes.IsEmpty() && _contours.IsEmpty();
    }
    // Original line 32: is_finalized.
    public bool IsFinalized()
    {
        return _finalized;
    }
    // Original line 37: reserve.
    public void Reserve(ulong node_capacity, ulong contour_capacity)
    {
        _nodes.Reserve(node_capacity);
        _contours.Reserve(contour_capacity);
    }
    // Original line 43: clear.
    public void Clear()
    {
        _nodes.Clear();
        _contours.Clear();
        _bounds = default;
        _finalized = true;
    }
    // Original line 51: release.
    public void Release(ulong node_capacity = 0, ulong contour_capacity = 0)
    {
        _nodes.Release(node_capacity);
        _contours.Release(contour_capacity);
        _bounds = default;
        _finalized = true;
    }
    // Original line 59: append_from.
    public void AppendFrom(VGPathFlatten input)
    {
        Debug.Assert(!ReferenceEquals(this, input) , "VGPathFlatten.AppendFrom cannot append self");
        Debug.Assert(input._finalized , "VGPathFlatten.AppendFrom requires finalized input");
        if (input.IsEmpty())
        {
            return;
        }




        uint node_base = (uint)(_nodes.Size());
        bool was_empty = IsEmpty();
        bool was_finalized = _finalized;
        _nodes.Reserve(_nodes.Size() + input._nodes.Size());
        _contours.Reserve(_contours.Size() + input._contours.Size());

        foreach (VGPathFlattenNode node in input._nodes)
        {
            _nodes.PushBack(node);
        }
        foreach (VGPathFlattenContour contour_original in input._contours)
        {
            VGPathFlattenContour contour = contour_original;
            contour.NodeBegin += node_base;
            _contours.PushBack(contour);
        }


        _bounds = was_empty ? input._bounds : _bounds.Unite(input._bounds);
        _finalized = was_empty ? input._finalized : (was_finalized && input._finalized);
    }
    // Original line 92: next_contour.
    public void NextContour()
    {
        _finalized = false;


        CleanupTailContour();



        ref VGPathFlattenContour contour = ref _contours.AddDefault();
        contour.NodeBegin = (uint)(_nodes.Size());
    }
    // Original line 105: add_node.
    public void AddNode(Offsetf position, EVGPathFlattenNodeFlags flags)
    {
        _finalized = false;

        if (_contours.IsEmpty())
        {
            Debug.Assert(false , "VGPathFlatten.AddNode requires an active contour");
            return;
        }

        ref VGPathFlattenContour contour = ref _contours.AtLast();
        if (contour.Closed)
        {
            Debug.Assert(false , "VGPathFlatten.AddNode cannot append to a closed contour");
            return;
        }

        if (!position.IsFinite())
        {
            Debug.Assert(false , "VGPathFlatten.AddNode requires a finite point");
            return;
        }



        if (contour.NodeCount > 0u)
        {
            ref VGPathFlattenNode tail = ref _nodes[contour.NodeBegin + contour.NodeCount - 1u];
            if (tail.Position.NearlyEqual(position, PointEqualsTolerance))
            {
                tail.Flags |= flags;
                return;
            }
        }


        ref VGPathFlattenNode node = ref _nodes.AddDefault();
        node.Position = position;
        node.Flags = flags;
        ++contour.NodeCount;
    }
    // Original line 147: close_contour.
    public void CloseContour(EVGPathWinding? winding_hint = null)
    {
        _finalized = false;

        if (_contours.IsEmpty())
        {
            Debug.Assert(false , "VGPathFlatten.CloseContour requires an active contour");
            return;
        }

        ref VGPathFlattenContour contour = ref _contours.AtLast();
        if (contour.Closed)
        {
            Debug.Assert(false , "VGPathFlatten.CloseContour cannot close an already closed contour");
            return;
        }




        if (contour.NodeCount > 1u)
        {
            uint first_index = contour.NodeBegin;
            uint tail_index = contour.NodeBegin + contour.NodeCount - 1u;
            if (_nodes[tail_index].Position.NearlyEqual(_nodes[first_index].Position, PointEqualsTolerance))
            {
                _nodes.RemoveAt(tail_index, 1u);
                --contour.NodeCount;
            }
        }


        contour.Closed = true;
        contour.WindingHint = winding_hint;
        if (contour.NodeCount < 3u)
        {
            RemoveContour(_contours.Size() - 1u);
        }
    }
    // Original line 187: finalize.
    public void Finalize(bool force = false)
    {
        if (_finalized && !force)
        {
            return;
        }



        CleanupContours();
        if (_nodes.IsEmpty())
        {
            _finalized = true;
            return;
        }


        _bounds = default;




        bool has_bounds = false;
        foreach (ref VGPathFlattenContour contour in _contours.AsSpan())
        {
            EnforceWindingHint(ref contour);
            UpdateSegmentData(ref contour, ref has_bounds);
            UpdateJoinData(ref contour);
        }

        _finalized = true;
    }
    // Original line 220: remove_collinear_segments.
    public uint RemoveCollinearSegments(float tolerance = 0)
    {
        if (!float.IsFinite(tolerance) || tolerance < 0.0f)
        {
            Debug.Assert(false , "VGPathFlatten.RemoveCollinearSegments requires finite non-negative tolerance");
            return 0u;
        }

        Finalize();
        if (_nodes.IsEmpty())
        {
            return 0u;
        }

        uint removed_count = 0u;
        uint write_node_index = 0u;





        foreach (ref VGPathFlattenContour contour in _contours.AsSpan())
        {
            uint read_begin = contour.NodeBegin;
            uint read_count = contour.NodeCount;
            uint write_begin = write_node_index;
            uint write_count = 0u;
            uint contour_removed_count = 0u;

            if (contour.Closed)
            {
                VGPathFlattenNode first_node = _nodes[read_begin];
                for (uint endpoint_index = 0u; endpoint_index < read_count; ++endpoint_index)
                {
                    uint previous_index = endpoint_index == 0u ? read_count - 1u : endpoint_index - 1u;
                    uint next_index = endpoint_index + 1u == read_count ? 0u : endpoint_index + 1u;
                    Offsetf previous = _nodes[read_begin + previous_index].Position;
                    Offsetf current = _nodes[read_begin + endpoint_index].Position;
                    Offsetf next = next_index == 0u ? first_node.Position : _nodes[read_begin + next_index].Position;
                    bool can_remove = read_count - contour_removed_count > 3u &&
                        IsCollinearPoint(previous, current, next, tolerance);
                    if (can_remove)
                    {
                        ++contour_removed_count;
                        continue;
                    }

                    _nodes[write_begin + write_count] = _nodes[read_begin + endpoint_index];
                    ++write_count;
                }
            }
            else
            {
                for (uint endpoint_index = 0u; endpoint_index < read_count; ++endpoint_index)
                {
                    bool can_remove = false;
                    if (endpoint_index > 0u && endpoint_index + 1u < read_count && read_count - contour_removed_count > 2u)
                    {
                        can_remove = IsCollinearPoint(
                            _nodes[read_begin + endpoint_index - 1u].Position,
                            _nodes[read_begin + endpoint_index].Position,
                            _nodes[read_begin + endpoint_index + 1u].Position,
                            tolerance
                        );
                    }
                    if (can_remove)
                    {
                        ++contour_removed_count;
                        continue;
                    }

                    _nodes[write_begin + write_count] = _nodes[read_begin + endpoint_index];
                    ++write_count;
                }
            }

            contour.NodeBegin = write_begin;
            contour.NodeCount = write_count;
            removed_count += contour_removed_count;
            write_node_index += write_count;
        }

        if (write_node_index < _nodes.Size())
        {
            _nodes.StackPopUnsafe(_nodes.Size() - write_node_index);
        }

        if (removed_count > 0u)
        {
            Finalize(true);
        }
        return removed_count;
    }
    // Original line 314: add_line_intersections.
    public uint AddLineIntersections(ReadOnlySpan<VGPathFlattenLine> lines, float tolerance = 0)
    {
        if (!float.IsFinite(tolerance) || tolerance < 0.0f)
        {
            Debug.Assert(false , "VGPathFlatten.AddLineIntersections requires finite non-negative tolerance");
            return 0u;
        }

        Finalize();
        if (_nodes.IsEmpty() || lines.IsEmpty)
        {
            return 0u;
        }




        ulong insert_count = 0u;
        foreach (VGPathFlattenContour contour in _contours)
        {
            uint segment_count = CalcSegmentCount(contour);
            for (uint i = 0u; i < segment_count; ++i)
            {
                VGPathFlattenNode node = _nodes[contour.NodeBegin + i];
                VGPathFlattenNode next_node = _nodes[NextNode(contour, i)];
                insert_count += CountSegmentLineIntersections(node.Position, next_node.Position, lines, tolerance);
            }
        }

        if (insert_count == 0u)
        {
            return 0u;
        }

        ulong new_node_count = _nodes.Size() + insert_count;
        if (new_node_count > uint.MaxValue)
        {
            Debug.Assert(false , "VGPathFlatten.AddLineIntersections node count overflow");
            return 0u;
        }

        _nodes.ResizeUnsafe(new_node_count);
        uint write_index = (uint)(new_node_count);

        for (ulong contour_index = _contours.Size(); contour_index > 0u; --contour_index)
        {
            ref VGPathFlattenContour contour = ref _contours[contour_index - 1u];
            uint read_begin = contour.NodeBegin;
            uint read_count = contour.NodeCount;
            uint segment_count = CalcSegmentCount(contour);

            uint contour_insert_count = 0u;
            for (uint i = 0u; i < segment_count; ++i)
            {
                VGPathFlattenNode node = _nodes[read_begin + i];
                VGPathFlattenNode next_node = _nodes[NextNode(contour, i)];
                contour_insert_count += CountSegmentLineIntersections(
                    node.Position,
                    next_node.Position,
                    lines,
                    tolerance
                );
            }

            uint write_count = read_count + contour_insert_count;
            write_index -= write_count;
            uint write_begin = write_index;
            uint write_cursor = write_begin + write_count;
            Offsetf next_position = contour.Closed ? _nodes[read_begin].Position : Offsetf.Zero();

            for (uint reverse_index = read_count; reverse_index > 0u; --reverse_index)
            {
                uint node_local_index = reverse_index - 1u;
                VGPathFlattenNode node = _nodes[read_begin + node_local_index];
                if (node_local_index < segment_count)
                {
                    float next_t = 1.0f;
                    float hit_t = 0.0f;
                    while (FindPreviousSegmentLineIntersectionT(
                        node.Position,
                        next_position,
                        lines,
                        next_t,
                        tolerance,
                        ref hit_t
                    ))
                    {
                        Offsetf position = node.Position + (next_position - node.Position) * hit_t;
                        _nodes[--write_cursor] = MakeLineIntersectionNode(position);
                        next_t = hit_t;
                    }
                }

                _nodes[--write_cursor] = node;
                next_position = node.Position;
            }

            Debug.Assert(write_cursor == write_begin , "VGPathFlatten.AddLineIntersections write count mismatch");
            contour.NodeBegin = write_begin;
            contour.NodeCount = write_count;
        }

        Debug.Assert(write_index == 0u , "VGPathFlatten.AddLineIntersections contour rebuild mismatch");
        Finalize(true);
        return (uint)(insert_count);
    }
    // Original line 426: _cleanup_tail_contour.
    private void CleanupTailContour()
    {
        if (_contours.IsEmpty())
        {
            return;
        }

        if (!IsContourValid(_contours.AtLast()))
        {
            RemoveContour(_contours.Size() - 1u);
        }
    }
    // Original line 439: _cleanup_contours.
    private void CleanupContours()
    {
        for (ulong i = _contours.Size(); i > 0u; --i)
        {
            ulong contour_index = i - 1u;
            if (!IsContourValid(_contours[contour_index]))
            {
                RemoveContour(contour_index);
            }
        }
    }
    // Original line 451: _is_contour_valid.
    private bool IsContourValid(VGPathFlattenContour contour)
    {
        if (contour.NodeCount < 2u)
        {
            return false;
        }
        if (contour.Closed && contour.NodeCount < 3u)
        {
            return false;
        }
        return true;
    }
    // Original line 464: _remove_contour.
    private void RemoveContour(ulong contour_index)
    {
        if (contour_index >= _contours.Size())
        {
            return;
        }


        VGPathFlattenContour contour = _contours[contour_index];
        if (contour.NodeCount > 0u)
        {
            _nodes.RemoveAt(contour.NodeBegin, contour.NodeCount);
            for (ulong i = contour_index + 1u; i < _contours.Size(); ++i)
            {
                _contours[i].NodeBegin -= contour.NodeCount;
            }
        }
        _contours.RemoveAt(contour_index, 1u);
    }
    // Original line 484: _is_pass_through_join.
    private static bool IsPassThroughJoin(Offsetf previous_direction, Offsetf next_direction, float tolerance = 0)
    {
        if (!previous_direction.IsFinite() || !next_direction.IsFinite())
        {
            return false;
        }




        float collinear_epsilon = CppMath.Max(KEpsilon, tolerance);
        return MathF.Abs(previous_direction.Cross(next_direction)) <= collinear_epsilon &&
            previous_direction.Dot(next_direction) > 0.0f;
    }
    // Original line 499: _is_collinear_point.
    private bool IsCollinearPoint(Offsetf previous,
    Offsetf current,
    Offsetf next,
    float tolerance)
    {
        Offsetf previous_delta = current - previous;
        Offsetf next_delta = next - current;
        float previous_length = previous_delta.Length();
        float next_length = next_delta.Length();
        if (!float.IsFinite(previous_length) || !float.IsFinite(next_length) ||
            previous_length <= KEpsilon || next_length <= KEpsilon)
        {
            return false;
        }

        Offsetf previous_direction = previous_delta / previous_length;
        Offsetf next_direction = next_delta / next_length;
        if (!previous_direction.IsFinite() || !next_direction.IsFinite())
        {
            return false;
        }

        return IsPassThroughJoin(previous_direction, next_direction, tolerance);
    }
    // Original line 526: _is_line_valid.
    private static bool IsLineValid(VGPathFlattenLine line)
    {
        if (!line.Position.IsFinite() || !line.Direction.IsFinite())
        {
            return false;
        }

        float direction_length = line.Direction.Length();
        return float.IsFinite(direction_length) && direction_length > KEpsilon;
    }
    // Original line 537: _try_calc_segment_line_intersection_t.
    private static bool TryCalcSegmentLineIntersectionT(Offsetf segment_start,
    Offsetf segment_end,
    VGPathFlattenLine line,
    float tolerance,
    ref float out_t)
    {
        if (!IsLineValid(line))
        {
            return false;
        }

        Offsetf segment_delta = segment_end - segment_start;
        float segment_length = segment_delta.Length();
        float line_length = line.Direction.Length();
        if (!float.IsFinite(segment_length) || !float.IsFinite(line_length) ||
            segment_length <= KEpsilon || line_length <= KEpsilon)
        {
            return false;
        }

        float tolerance_value = CppMath.Max(KEpsilon, tolerance);
        float denominator = segment_delta.Cross(line.Direction);
        float parallel_epsilon = tolerance_value * segment_length * line_length;
        if (!float.IsFinite(denominator) || MathF.Abs(denominator) <= parallel_epsilon)
        {


            return false;
        }

        float t = (line.Position - segment_start).Cross(line.Direction) / denominator;
        if (!float.IsFinite(t) || t <= tolerance_value || t >= 1.0f - tolerance_value)
        {
            return false;
        }

        out_t = t;
        return true;
    }
    // Original line 579: _find_next_segment_line_intersection_t.
    private static bool FindNextSegmentLineIntersectionT(Offsetf segment_start,
    Offsetf segment_end,
    ReadOnlySpan<VGPathFlattenLine> lines,
    float previous_t,
    float tolerance,
    ref float out_t)
    {
        float tolerance_value = CppMath.Max(KEpsilon, tolerance);
        bool found = false;
        float best_t = 1.0f;
        foreach (VGPathFlattenLine line in lines)
        {
            float t = 0.0f;
            if (!TryCalcSegmentLineIntersectionT(segment_start, segment_end, line, tolerance, ref t))
            {
                continue;
            }
            if (t <= previous_t + tolerance_value || t >= best_t - tolerance_value)
            {
                continue;
            }

            best_t = t;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        out_t = best_t;
        return true;
    }
    // Original line 616: _find_previous_segment_line_intersection_t.
    private static bool FindPreviousSegmentLineIntersectionT(Offsetf segment_start,
    Offsetf segment_end,
    ReadOnlySpan<VGPathFlattenLine> lines,
    float next_t,
    float tolerance,
    ref float out_t)
    {
        float tolerance_value = CppMath.Max(KEpsilon, tolerance);
        bool found = false;
        float best_t = 0.0f;
        foreach (VGPathFlattenLine line in lines)
        {
            float t = 0.0f;
            if (!TryCalcSegmentLineIntersectionT(segment_start, segment_end, line, tolerance, ref t))
            {
                continue;
            }
            if (t >= next_t - tolerance_value || t <= best_t + tolerance_value)
            {
                continue;
            }

            best_t = t;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        out_t = best_t;
        return true;
    }
    // Original line 653: _count_segment_line_intersections.
    private static uint CountSegmentLineIntersections(Offsetf segment_start,
    Offsetf segment_end,
    ReadOnlySpan<VGPathFlattenLine> lines,
    float tolerance)
    {
        uint count = 0u;
        float previous_t = 0.0f;
        float hit_t = 0.0f;
        while (FindNextSegmentLineIntersectionT(segment_start, segment_end, lines, previous_t, tolerance, ref hit_t))
        {
            ++count;
            previous_t = hit_t;
        }
        return count;
    }
    // Original line 671: _make_line_intersection_node.
    private static VGPathFlattenNode MakeLineIntersectionNode(Offsetf position)
    {
        VGPathFlattenNode node = default;
        node.Position = position;
        node.Flags = EVGPathFlattenNodeFlags.None;
        return node;
    }
    // Original line 683: _calc_contour_area.
    private float CalcContourArea(VGPathFlattenContour contour)
    {
        if (!contour.Closed || contour.NodeCount < 3u)
        {
            return 0.0f;
        }

        float area = 0.0f;
        Offsetf origin = _nodes[contour.NodeBegin].Position;
        for (uint i = 2u; i < contour.NodeCount; ++i)
        {
            Offsetf b = _nodes[contour.NodeBegin + i - 1u].Position;
            Offsetf c = _nodes[contour.NodeBegin + i].Position;
            area += (b - origin).Cross(c - origin);
        }
        return area;
    }
    // Original line 701: _reverse_contour_nodes.
    private void ReverseContourNodes(ref VGPathFlattenContour contour)
    {
        uint first = 0u;
        uint last = contour.NodeCount - 1u;
        while (first < last)
        {
            VGPathFlattenNode tmp = _nodes[contour.NodeBegin + first];
            _nodes[contour.NodeBegin + first] = _nodes[contour.NodeBegin + last];
            _nodes[contour.NodeBegin + last] = tmp;
            ++first;
            --last;
        }
    }
    // Original line 715: _enforce_winding_hint.
    private void EnforceWindingHint(ref VGPathFlattenContour contour)
    {
        if (!contour.Closed || !contour.WindingHint.HasValue || contour.NodeCount < 3u)
        {
            return;
        }

        float area = CalcContourArea(contour);
        if (MathF.Abs(area) <= KEpsilon)
        {
            return;
        }




        if ((contour.WindingHint.Value == EVGPathWinding.CW && area < 0.0f) ||
            (contour.WindingHint.Value == EVGPathWinding.CCW && area > 0.0f))
        {
            ReverseContourNodes(ref contour);
        }
    }
    // Original line 738: _update_segment_data.
    private void UpdateSegmentData(ref VGPathFlattenContour contour, ref bool has_bounds)
    {


        uint segment_count = CalcSegmentCount(contour);
        float contour_distance = 0.0f;

        for (uint i = 0u; i < contour.NodeCount; ++i)
        {

            ref VGPathFlattenNode node = ref _nodes[contour.NodeBegin + i];
            node.ContourDistance = contour_distance;
            node.NextDirection = default;
            node.JoinDirection = default;
            node.NextLength = 0.0f;
            node.CacheFlags = EVGPathFlattenNodeCacheFlags.None;
            node.Flags = node.Flags & ~EVGPathFlattenNodeFlags.Left;



            if (!has_bounds)
            {
                _bounds = Rectf.Points(node.Position, node.Position);
                has_bounds = true;
            }
            else
            {
                _bounds = _bounds.Hold(node.Position);
            }

            if (i >= segment_count)
            {
                continue;
            }


            uint next_index = NextNode(contour, i);
            Offsetf delta = _nodes[next_index].Position - node.Position;
            float length = delta.Length();
            if (!float.IsFinite(length) || length <= KEpsilon)
            {
                continue;
            }

            node.NextDirection = delta / length;
            node.NextLength = length;
            contour_distance += length;
        }

        contour.TotalLength = contour_distance;
    }
    // Original line 790: _update_join_data.
    private void UpdateJoinData(ref VGPathFlattenContour contour)
    {


        uint segment_count = CalcSegmentCount(contour);
        if (segment_count == 0u)
        {
            contour.Convex = false;
            return;
        }

        uint left_count = 0u;
        for (uint i = 0u; i < contour.NodeCount; ++i)
        {
            uint node_index = contour.NodeBegin + i;
            ref VGPathFlattenNode node = ref _nodes[node_index];


            if (!contour.Closed && (i == 0u || i + 1u == contour.NodeCount))
            {
                node.JoinDirection = default;
                continue;
            }



            uint previous_node_index = PreviousNode(contour, i);
            VGPathFlattenNode previous_node = _nodes[previous_node_index];
            Offsetf previous_normal = new Offsetf(previous_node.NextDirection.Y, -previous_node.NextDirection.X);
            Offsetf next_normal = new Offsetf(node.NextDirection.Y, -node.NextDirection.X);
            Offsetf join_direction = (previous_normal + next_normal) * 0.5f;
            float join_length_squared = join_direction.LengthSquared();
            if (join_length_squared > KEpsilon)
            {
                float scale = 1.0f / join_length_squared;
                if (scale > 600.0f)
                {
                    scale = 600.0f;
                }
                join_direction *= scale;
            }
            node.JoinDirection = join_direction;


            float cross = node.NextDirection.Cross(previous_node.NextDirection);
            if (cross > 0.0f)
            {
                node.Flags |= EVGPathFlattenNodeFlags.Left;
                ++left_count;
            }
        }

        contour.Convex = contour.Closed && (left_count == contour.NodeCount || left_count == 0u);
    }
    // Original line 845: _calc_segment_count.
    private uint CalcSegmentCount(VGPathFlattenContour contour)
    {

        if (contour.NodeCount < 2u)
        {
            return 0u;
        }
        return contour.Closed ? contour.NodeCount : contour.NodeCount - 1u;
    }
    // Original line 855: _next_node.
    private uint NextNode(VGPathFlattenContour contour,
    uint node_local_index)
    {


        uint next = node_local_index + 1u;
        if (next < contour.NodeCount)
        {
            return contour.NodeBegin + next;
        }
        return contour.NodeBegin;
    }
    // Original line 870: _previous_node.
    private uint PreviousNode(VGPathFlattenContour contour,
    uint node_local_index)
    {


        if (node_local_index > 0u)
        {
            return contour.NodeBegin + node_local_index - 1u;
        }
        return contour.NodeBegin + contour.NodeCount - 1u;
    }
}
