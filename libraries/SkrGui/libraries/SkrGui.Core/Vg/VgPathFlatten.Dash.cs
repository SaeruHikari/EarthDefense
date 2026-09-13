// Source: src/vg/vg_path_flatten.dash.cpp @ 611561f8.
using System.Diagnostics;
namespace SkrGui;
internal struct VGDashReserveEstimate { public ulong NodeCount,ContourCount; }
internal struct VGDashSample(Offsetf position,EVGPathFlattenNodeFlags flags) { public Offsetf Position=position; public EVGPathFlattenNodeFlags Flags=flags; }
internal sealed class VGDashPatternState
{
    public ReadOnlyMemory<float> Values; public ulong SourceCount,VirtualCount,NonZeroOnCount,NonZeroOffCount; public float PatternLength;
    // Original line 145: value_at.
    public float ValueAt(ulong Index)
    {
        return Values.Span[(int)(Index % SourceCount)];
    }
    // Original line 149: is_on.
    public bool IsOn(ulong Index)
    {
        return (Index & 1u) == 0u;
    }
    // Original line 153: build.
    public bool Build(VGDashPattern dash)
    {


        Values = dash.Values;
        SourceCount = ((ulong)Values.Length);
        VirtualCount = (SourceCount & 1u) != 0 ? SourceCount * 2u : SourceCount;
        NonZeroOnCount = 0u;
        NonZeroOffCount = 0u;
        PatternLength = 0.0f;

        if (!float.IsFinite(dash.Offset))
        {
            Debug.Assert(false , "VGPathFlatten.DashTo requires a finite dash Offset");
            return false;
        }

        for (ulong i_scope0 = 0u; i_scope0 < SourceCount; ++i_scope0)
        {
            float value = Values.Span[(int)(i_scope0)];
            if (!float.IsFinite(value) || value < 0.0f)
            {
                Debug.Assert(false , "VGPathFlatten.DashTo requires finite non-negative dash Values");
                return false;
            }
            PatternLength += value;
        }

        if (SourceCount == 0u || PatternLength <= 0.000001f)
        {
            return true;
        }
        if ((SourceCount & 1u) != 0)
        {
            PatternLength *= 2.0f;
        }



        for (ulong i_scope2 = 0u; i_scope2 < VirtualCount; ++i_scope2)
        {
            if (IsOn(i_scope2) && ValueAt(i_scope2) > 0.000001f)
            {
                ++NonZeroOnCount;
            }
            else if (!IsOn(i_scope2) && ValueAt(i_scope2) > 0.000001f)
            {
                ++NonZeroOffCount;
            }
        }
        return true;
    }
}
internal sealed class VGDashCursor
{
    public VGDashPatternState Pattern=null!; public ulong Index; public float Remaining;
    // Original line 210: is_on.
    public bool IsOn()
    {
        return Pattern.IsOn(Index);
    }
    // Original line 214: reset.
    public void Reset(VGDashPatternState in_pattern, float Offset)
    {

        Pattern = in_pattern;
        Index = 0u;

        float phase = CppMath.Fmod(Offset, Pattern.PatternLength);
        if (phase < 0.0f)
        {
            phase += Pattern.PatternLength;
        }

        while (Index < Pattern.VirtualCount)
        {
            float value = Pattern.ValueAt(Index);
            if (phase < value)
            {
                Remaining = value - phase;
                if (Remaining <= 0.000001f)
                {
                    SkipEmptyValues();
                }
                return;
            }
            phase -= value;
            ++Index;
        }

        Index = 0u;
        Remaining = Pattern.ValueAt(Index);
        if (Remaining <= 0.000001f)
        {
            SkipEmptyValues();
        }
    }
    // Original line 249: advance.
    public void Advance(float distance)
    {


        Remaining -= distance;
        if (Remaining <= 0.000001f)
        {
            ++Index;
            if (Index == Pattern.VirtualCount)
            {
                Index = 0u;
            }
            Remaining = Pattern.ValueAt(Index);
            SkipEmptyValues();
        }
    }
    // Original line 265: skip_empty_values.
    public void SkipEmptyValues()
    {


        for (ulong i = 0u; i < Pattern.VirtualCount; ++i)
        {
            if (Index == Pattern.VirtualCount)
            {
                Index = 0u;
            }

            Remaining = Pattern.ValueAt(Index);
            if (Remaining > 0.000001f)
            {
                return;
            }
            ++Index;
        }
        Remaining = 0.0f;
    }
}
internal sealed class VGDashContourCursor
{
    public readonly VGPathFlatten Flatten; public readonly VGPathFlattenContour Contour; public uint SegmentCount,SegmentLocalIndex; public float SegmentStartDistance,SegmentEndDistance;
    public VGDashContourCursor(VGPathFlatten flatten,VGPathFlattenContour contour) { Flatten=flatten;Contour=contour;SegmentCount=contour.Closed?contour.NodeCount:contour.NodeCount-1;Reset(); }
    // Original line 300: reset.
    public void Reset()
    {
        SegmentLocalIndex = 0u;
        RefreshSegmentDistance();
    }
    // Original line 305: move_to.
    public void MoveTo(float distance)
    {


        while (
            SegmentLocalIndex + 1u < SegmentCount &&
            distance > SegmentEndDistance - 0.000001f
        )
        {
            ++SegmentLocalIndex;
            RefreshSegmentDistance();
        }
    }
    // Original line 318: segment_node_index.
    public uint SegmentNodeIndex()
    {
        return Contour.NodeBegin + SegmentLocalIndex;
    }
    // Original line 322: segment_next_node_index.
    public uint SegmentNextNodeIndex()
    {
        uint next = SegmentLocalIndex + 1u;
        if (next < Contour.NodeCount)
        {
            return Contour.NodeBegin + next;
        }
        return Contour.NodeBegin;
    }
    // Original line 331: sample.
    public VGDashSample Sample(float distance)
    {

        if (distance <= 0.000001f)
        {
            VGPathFlattenNode node_scope0 = Flatten._nodes[Contour.NodeBegin];
            return new VGDashSample(node_scope0.Position, node_scope0.Flags);
        }
        if (distance >= Contour.TotalLength - 0.000001f)
        {
            uint node_index = Contour.Closed ?
                Contour.NodeBegin :
                Contour.NodeBegin + Contour.NodeCount - 1u;
            VGPathFlattenNode node_scope2 = Flatten._nodes[node_index];
            return new VGDashSample(node_scope2.Position, node_scope2.Flags);
        }



        MoveTo(distance);

        VGPathFlattenNode node_scope3 = Flatten._nodes[SegmentNodeIndex()];
        if (MathF.Abs(distance - SegmentStartDistance) <= 0.000001f)
        {
            return new VGDashSample(node_scope3.Position, node_scope3.Flags);
        }

        VGPathFlattenNode next_node = Flatten._nodes[SegmentNextNodeIndex()];
        if (MathF.Abs(distance - SegmentEndDistance) <= 0.000001f)
        {
            return new VGDashSample(next_node.Position, next_node.Flags);
        }



        float local_distance = distance - SegmentStartDistance;
        return new VGDashSample(node_scope3.Position + node_scope3.NextDirection * local_distance,
            EVGPathFlattenNodeFlags.None);
    }
    // Original line 372: refresh_segment_distance.
    public void RefreshSegmentDistance()
    {
        if (SegmentCount == 0u)
        {
            SegmentStartDistance = 0.0f;
            SegmentEndDistance = 0.0f;
            return;
        }

        VGPathFlattenNode node = Flatten._nodes[SegmentNodeIndex()];
        SegmentStartDistance = node.ContourDistance;
        SegmentEndDistance = SegmentStartDistance + node.NextLength;
    }
}
internal sealed class VGDashBuilder(VGPathFlatten flatten,VGPathFlatten output,VGDashPatternState pattern,float offset)
{
    public readonly VGPathFlatten Flatten=flatten,Output=output; public readonly VGDashPatternState Pattern=pattern;public float Offset=offset;
    // Original line 390: reserve.
    public void Reserve()
    {


        VGDashReserveEstimate estimate = new();
        estimate.NodeCount = Flatten._nodes.Size();

        foreach (VGPathFlattenContour Contour in Flatten._contours)
        {
            if (Contour.TotalLength <= 0.000001f)
            {
                continue;
            }

            double cycle_count = VgScalar.Ceiling(
                (double)(Contour.TotalLength) /
                (double)(Pattern.PatternLength)
            );
            ulong visible_runs = (ulong)(cycle_count) * Pattern.NonZeroOnCount + 2u;
            estimate.ContourCount += visible_runs;
            estimate.NodeCount += visible_runs * 2u;
        }

        Output.Reserve(estimate.NodeCount, estimate.ContourCount);
    }
    // Original line 415: copy_all.
    public void CopyAll()
    {

        Output.Clear();
        Output.Reserve(Flatten._nodes.Size(), Flatten._contours.Size());
        foreach (VGPathFlattenContour Contour in Flatten._contours)
        {
            CopyContour(Contour);
        }
        Output.Finalize();
    }
    // Original line 426: copy_contour.
    public void CopyContour(VGPathFlattenContour Contour)
    {
        Output.NextContour();
        for (uint i = 0u; i < Contour.NodeCount; ++i)
        {
            VGPathFlattenNode node = Flatten._nodes[Contour.NodeBegin + i];
            Output.AddNode(node.Position, node.Flags);
        }
        if (Contour.Closed)
        {
            Output.CloseContour(Contour.WindingHint);
        }
    }
    // Original line 439: emit.
    public bool Emit()
    {


        Output.Clear();
        if (Pattern.NonZeroOnCount == 0u)
        {
            return true;
        }

        Reserve();

        foreach (VGPathFlattenContour Contour in Flatten._contours)
        {
            EmitContour(Contour);
        }

        Output.Finalize();
        return true;
    }
    // Original line 459: emit_contour.
    public void EmitContour(VGPathFlattenContour Contour)
    {

        VGDashCursor dash_cursor = new();
        dash_cursor.Reset(Pattern, Offset);
        if (dash_cursor.Remaining <= 0.000001f)
        {
            return;
        }

        if (dash_cursor.Remaining >= Contour.TotalLength - 0.000001f)
        {
            if (dash_cursor.IsOn())
            {
                CopyContour(Contour);
            }
            return;
        }




        ulong first_contour_index = Output._contours.Size();
        bool first_interval_starts_at_zero = false;
        bool last_interval_ends_at_total = false;
        float distance = 0.0f;

        VGDashContourCursor contour_cursor = new(Flatten, Contour);
        while (distance < Contour.TotalLength - 0.000001f)
        {
            if (dash_cursor.Remaining <= 0.000001f)
            {
                break;
            }

            float step = dash_cursor.Remaining;
            float path_remaining = Contour.TotalLength - distance;
            if (step > path_remaining)
            {
                step = path_remaining;
            }

            float next_distance = distance + step;
            if (dash_cursor.IsOn() && step > 0.000001f)
            {
                ulong before_count = Output._contours.Size();
                EmitRange(Contour, contour_cursor, distance, next_distance);
                if (Output._contours.Size() > before_count)
                {
                    if (before_count == first_contour_index && distance <= 0.000001f)
                    {
                        first_interval_starts_at_zero = true;
                    }
                    last_interval_ends_at_total =
                        next_distance >= Contour.TotalLength - 0.000001f;
                }
            }

            distance = next_distance;
            dash_cursor.Advance(step);
        }

        if (
            Contour.Closed &&
            first_interval_starts_at_zero &&
            last_interval_ends_at_total &&
            Output._contours.Size() > first_contour_index + 1u
        )
        {
            MergeClosedWrap(first_contour_index);
        }
    }
    // Original line 531: emit_range.
    public void EmitRange(VGPathFlattenContour Contour,
    VGDashContourCursor cursor,
    float start_distance,
    float end_distance)
    {


        if (end_distance - start_distance <= 0.000001f)
        {
            return;
        }

        Output.NextContour();

        VGDashSample start = cursor.Sample(start_distance);
        start.Flags |= EVGPathFlattenNodeFlags.DashCutStart;
        Output.AddNode(start.Position, start.Flags);



        cursor.MoveTo(start_distance);
        while (cursor.SegmentEndDistance < end_distance - 0.000001f)
        {
            VGPathFlattenNode next_node = Flatten._nodes[cursor.SegmentNextNodeIndex()];
            Output.AddNode(next_node.Position, next_node.Flags);
            ++cursor.SegmentLocalIndex;
            cursor.RefreshSegmentDistance();
        }

        VGDashSample end = cursor.Sample(end_distance);
        end.Flags |= EVGPathFlattenNodeFlags.DashCutEnd;
        Output.AddNode(end.Position, end.Flags);
    }
    // Original line 566: merge_closed_wrap.
    public void MergeClosedWrap(ulong first_contour_index)
    {



        VGPathFlattenContour first_contour = Output._contours[first_contour_index];
        VGPathFlattenContour last_contour = Output._contours.AtLast();
        uint seam_index = last_contour.NodeBegin + last_contour.NodeCount - 1u;

        for (uint i = 0u; i < first_contour.NodeCount; ++i)
        {
            VGPathFlattenNode node = Output._nodes[first_contour.NodeBegin + i];
            EVGPathFlattenNodeFlags Flags = node.Flags;
            if (i == 0u)
            {
                Flags = VgFlags.Erase(
                    Flags,
                    EVGPathFlattenNodeFlags.DashCutStart | EVGPathFlattenNodeFlags.DashCutEnd
                );
            }
            Output.AddNode(node.Position, Flags);
        }

        Output._nodes[seam_index].Flags = VgFlags.Erase(
            Output._nodes[seam_index].Flags,
            EVGPathFlattenNodeFlags.DashCutStart | EVGPathFlattenNodeFlags.DashCutEnd
        );

        RemoveContour(first_contour_index);
    }
    // Original line 596: remove_contour.
    public void RemoveContour(ulong contour_index)
    {


        if (contour_index >= Output._contours.Size())
        {
            return;
        }

        VGPathFlattenContour Contour = Output._contours[contour_index];
        if (Contour.NodeCount > 0u)
        {
            Output._nodes.RemoveAt(Contour.NodeBegin, Contour.NodeCount);
            for (ulong i = contour_index + 1u; i < Output._contours.Size(); ++i)
            {
                Output._contours[i].NodeBegin -= Contour.NodeCount;
            }
        }
        Output._contours.RemoveAt(contour_index, 1u);
    }
}
internal sealed class VGDashNeedTest(VGPathFlatten flatten,VGDashPatternState pattern,float offset)
{
    public readonly VGPathFlatten Flatten=flatten;public readonly VGDashPatternState Pattern=pattern;public float Offset=offset;
    // Original line 621: need.
    public bool Need()
    {
        if (Flatten._contours.IsEmpty())
        {
            return false;
        }


        if (Pattern.NonZeroOnCount == 0u)
        {
            return true;
        }

        foreach (VGPathFlattenContour Contour in Flatten._contours)
        {
            if (NeedContour(Contour))
            {
                return true;
            }
        }
        return false;
    }
    // Original line 643: need_contour.
    public bool NeedContour(VGPathFlattenContour Contour)
    {
        if (Contour.TotalLength <= 0.000001f)
        {
            return false;
        }




        VGDashCursor dash_cursor = new();
        dash_cursor.Reset(Pattern, Offset);
        if (dash_cursor.Remaining <= 0.000001f)
        {
            return true;
        }
        if (!dash_cursor.IsOn())
        {
            return true;
        }
        return dash_cursor.Remaining < Contour.TotalLength - 0.000001f;
    }
}
public sealed partial class VGPathFlatten
{
    // Original line 670: need_dash.
    public bool NeedDash(VGDashPattern dash)
    {
        Debug.Assert(_finalized , "VGPathFlatten.NeedDash requires finalized contours");

        VGDashPatternState Pattern = new();
        if (!Pattern.Build(dash))
        {
            return false;
        }

        if (
            dash.Values.IsEmpty ||
            Pattern.PatternLength <= 0.000001f ||
            Pattern.NonZeroOffCount == 0u
        )
        {
            return false;
        }

        VGDashNeedTest test = new(this,
            Pattern,
            dash.Offset);
        return test.Need();
    }
    // Original line 696: dash_to.
    public bool DashTo(VGPathFlatten Output, VGDashPattern dash)
    {
        Debug.Assert(_finalized , "VGPathFlatten.DashTo requires finalized contours");



        if (ReferenceEquals(Output, this))
        {
            Debug.Assert(false , "VGPathFlatten.DashTo does not support in-place output");
            return false;
        }

        Output.PointEqualsTolerance = PointEqualsTolerance;



        VGDashPatternState Pattern = new();
        if (!Pattern.Build(dash))
        {
            Output.Clear();
            return false;
        }

        VGDashBuilder builder = new(this,
            Output,
            Pattern,
            dash.Offset);



        if (dash.Values.IsEmpty)
        {
            builder.CopyAll();
            return true;
        }

        if (Pattern.PatternLength <= 0.000001f || Pattern.NonZeroOffCount == 0u)
        {
            builder.CopyAll();
            return true;
        }


        if (Pattern.NonZeroOnCount == 0u)
        {
            Output.Clear();
            Output.Finalize();
            return true;
        }

        return builder.Emit();
    }
}
