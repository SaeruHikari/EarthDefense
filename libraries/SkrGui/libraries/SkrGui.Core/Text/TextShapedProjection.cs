using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SkrGui;
internal static unsafe partial class TextNative
{
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubidi_openSized_72")] internal static extern nint ubidi_openSized(int length, int maxRuns, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubidi_setLine_72")] internal static extern void ubidi_setLine(nint parent, int start, int limit, nint line, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubidi_setPara_72")] internal static extern void ubidi_setPara(nint bidi, char* text, int length, byte level, byte* embeddingLevels, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubidi_countRuns_72")] internal static extern int ubidi_countRuns(nint bidi, ref int status);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubidi_getVisualRun_72")] internal static extern int ubidi_getVisualRun(nint bidi, int run, out int logicalStart, out int length);
}
// Source: text/layout/text_shaped_pipeline.cpp:1580-1698,1983-2505 @ 611561f8.
internal sealed partial class TextServicesImpl
{
    internal bool ResolveExactGlyph(TextShapedData root, TextFontFaceId font, uint codepoint, float size, ref TextGlyphMetrics metrics)
    { AppendFontDependency(root, font); return GetExactGlyphMetrics(font, codepoint, size, ref metrics); }
    internal bool ResolveTrimGlyph(TextShapedData shaped, uint cp, bool allowSystemFallback, ref TextGlyphMetrics metrics, ref TextLayoutSpanId spanId)
    {
        var root = shaped.Root(); var edge = shaped.Glyphs.Count == 0 ? null : shaped.Glyphs[^1];
        if (root.Spans.Count == 0 || root.ResolvedSpans.Count < root.Spans.Count) return false;
        int index = root.Spans.Count - 1; var span = root.Spans[index]; var resolved = root.ResolvedSpans[index]; float size = edge?.Glyph.FontSize ?? span.Style.FontSize;
        spanId = edge is not null && edge.SpanId.IsValid() ? edge.SpanId : span.Id;
        if (edge is not null && edge.Glyph.Font.IsValid() && ResolveExactGlyph(root, edge.Glyph.Font, cp, size, ref metrics)) return true;
        for (ulong candidate = 0; candidate < FontCandidateCount(resolved); candidate++)
        { var font = ResolveFontCandidate(span.Style, resolved, candidate); if (ResolveExactGlyph(root, font, cp, size, ref metrics)) return true; }
        if (!allowSystemFallback || resolved.Fonts.Count == 0) return false;
        var system = ResolveSystemFallback(span.Style, new uint[] { cp }, span.Language); return ResolveExactGlyph(root, system, cp, size, ref metrics);
    }
    internal float TrimGlyphSpacing(TextShapedData shaped, TextFontFaceId font)
    { float spacing = shaped.Root().Spacing[(int)ETextSpacing.Glyph]; if (FindFontFace(font) is { } face) spacing += face.SpacingValues[(int)ETextSpacing.Glyph]; return spacing; }
    internal unsafe bool ShapeSubstring(TextShapedData parent, TextRange range, TextShapedData output, ETextShapedProjection projection)
    {
        var root = parent.Root(); Debug.Assert(root.Valid && root.LineBreaksValid);
        ulong begin = Math.Clamp(range.Start, root.SourceRange.Start, root.SourceRange.End); range = new(begin, Math.Clamp(range.End, begin, root.SourceRange.End));
        if (projection == ETextShapedProjection.FullRange) Debug.Assert(range == root.SourceRange);
        Debug.Assert(!output.Valid && output.Parent is null);
        output.Parent = root; output.SourceRange = range; output.Direction = root.Direction; output.Orientation = root.Orientation;
        output.CustomPunctuation = root.CustomPunctuation; output.InferredDirection = root.InferredDirection; output.PreserveInvalid = root.PreserveInvalid; output.PreserveControl = root.PreserveControl;
        for (int i = 0; i < 4; i++) output.Spacing[i] = root.Spacing[i];
        var glyphs = root.Glyphs;
        void ResolveTerminalSoftHyphen(TextPlacedGlyph head)
        {
            if (head.SourceEnd != range.End || (head.Flags & ETextGraphemeFlag.SoftHyphen) == 0) return;
            TextGlyphMetrics metrics = new(); bool resolved = head.Glyph.Font.IsValid() && ResolveExactGlyph(root, head.Glyph.Font, 0xad, head.Glyph.FontSize, ref metrics);
            ulong spanIndex = head.SpanId.IsValid() ? head.SpanId.Value : ulong.MaxValue;
            if (!resolved && spanIndex < (ulong)root.Spans.Count && spanIndex < (ulong)root.ResolvedSpans.Count)
            {
                var span = root.Spans[(int)spanIndex]; var candidates = root.ResolvedSpans[(int)spanIndex];
                for (ulong candidate = 0; candidate < FontCandidateCount(candidates); candidate++)
                { var font = ResolveFontCandidate(span.Style, candidates, candidate); if (ResolveExactGlyph(root, font, 0xad, span.Style.FontSize, ref metrics)) { resolved = true; break; } }
            }
            if (!resolved && BackendValue == ETextServicesBackend.Advanced && head.Glyph.Font.IsValid() && spanIndex < (ulong)root.Spans.Count)
            { var span = root.Spans[(int)spanIndex]; var font = ResolveSystemFallback(span.Style, new uint[] { 0xad }, span.Language); resolved = ResolveExactGlyph(root, font, 0xad, head.Glyph.FontSize, ref metrics); }
            if (resolved) { head.Glyph = metrics; head.Advance = metrics.Advance.X; }
        }
        void AppendSourceRun(TextRange sourceRun)
        {
            TextGlyphFontMetricsCache cache = new();
            foreach (var sourceGlyph in glyphs)
            {
                ulong emptyOffset = BackendValue == ETextServicesBackend.Advanced && projection == ETextShapedProjection.LineBreak && sourceGlyph.SourceBegin == sourceGlyph.SourceEnd ? 1ul : 0;
                if (sourceGlyph.SourceBegin < sourceRun.Start || sourceGlyph.SourceEnd + emptyOffset > sourceRun.End) continue;
                var glyph = sourceGlyph.Copy();
                if (projection == ETextShapedProjection.LineBreak && glyph.ClusterCount > 0) ResolveTerminalSoftHyphen(glyph);
                if (projection == ETextShapedProjection.LineBreak && output.Glyphs.Count == 0 && glyph.Glyph.Kind != ETextGlyphKind.Object && glyph.XOff < 0)
                { glyph.Advance -= glyph.XOff; glyph.XOff = 0; }
                if (glyph.Glyph.Kind == ETextGlyphKind.Object)
                {
                    var obj = TextAlgorithms.ObjectByKey(root.Objects, glyph.ObjectKey);
                    if (obj is not null && TextAlgorithms.ObjectByKey(output.Objects, glyph.ObjectKey) is null) output.Objects.Add(obj with { Rect = new() });
                }
                else TextAlgorithms.IncludeGlyphMetrics(this, output, glyph, cache);
                output.Width += glyph.Advance * Math.Max(1u, glyph.Repeat); output.Glyphs.Add(glyph);
            }
        }
        if (projection == ETextShapedProjection.LineBreak && BackendValue == ETextServicesBackend.Advanced && !range.IsEmpty() && root.Advanced.SourceToUtf16.Count == root.Codepoints.Count + 1)
        {
            var offsets = root.Advanced.SourceToUtf16;
            ulong SourceAtUtf16(int offset) { int lo = 0, hi = offsets.Count; while (lo < hi) { int middle = lo + (hi - lo) / 2; if (offsets[middle] < offset) lo = middle + 1; else hi = middle; } return (ulong)lo; }
            int count = root.BidiOverrides.Count == 0 ? 1 : root.BidiOverrides.Count;
            for (int index = 0; index < count; index++)
            {
                var overrideRange = root.SourceRange; byte level = root.Advanced.ParagraphLevel;
                if (root.BidiOverrides.Count != 0)
                { var value = root.BidiOverrides[index]; ulong start = Math.Clamp(value.Range.Start, root.SourceRange.Start, root.SourceRange.End); overrideRange = new(start, Math.Clamp(value.Range.End, start, root.SourceRange.End)); level = value.Direction == ETextDirection.RTL ? (byte)1 : (byte)0; }
                TextRange intersection = new(Math.Max(range.Start, overrideRange.Start), Math.Min(range.End, overrideRange.End)); if (intersection.IsEmpty()) continue;
                int overrideUtf16 = offsets[(int)overrideRange.Start], lineUtf16 = offsets[(int)intersection.Start], lineEnd = offsets[(int)intersection.End];
                nint lineBidi = 0, parentBidi = index < root.Advanced.BidiIterators.Count ? root.Advanced.BidiIterators[index] : 0;
                if (parentBidi != 0)
                {
                    int localStatus = 0; lineBidi = TextNative.ubidi_openSized(lineEnd - lineUtf16, 0, ref localStatus);
                    if (localStatus <= 0 && lineBidi != 0)
                    {
                        TextNative.ubidi_setLine(parentBidi, lineUtf16 - overrideUtf16, lineEnd - overrideUtf16, lineBidi, ref localStatus);
                        if (localStatus > 0)
                        { localStatus = 0; TextNative.ubidi_setPara(lineBidi, (char*)root.Advanced.PinnedUtf16() + lineUtf16, lineEnd - lineUtf16, level, null, ref localStatus); }
                    }
                    if (localStatus > 0) { if (lineBidi != 0) TextNative.ubidi_close(lineBidi); lineBidi = 0; }
                }
                int status = 0, runCount = lineBidi != 0 ? TextNative.ubidi_countRuns(lineBidi, ref status) : 1;
                int safeCount = status <= 0 && runCount > 0 ? runCount : 1;
                for (int visualRun = 0; visualRun < safeCount; visualRun++)
                {
                    var sourceRun = intersection;
                    if (lineBidi != 0 && status <= 0)
                    { TextNative.ubidi_getVisualRun(lineBidi, visualRun, out int logicalStart, out int logicalLength); sourceRun = new(SourceAtUtf16(lineUtf16 + logicalStart), SourceAtUtf16(lineUtf16 + logicalStart + logicalLength)); }
                    AppendSourceRun(sourceRun);
                }
                if (lineBidi != 0) TextNative.ubidi_close(lineBidi);
            }
        }
        else AppendSourceRun(range);
        output.LineBreaksValid = root.LineBreaksValid; output.JustificationOpsValid = root.JustificationOpsValid; output.Valid = true;
        if (output.Objects.Count != 0)
        {
            using TextShapedData textBox = new() { Ascent = output.Ascent, Descent = output.Descent }; float pen = 0;
            foreach (var glyph in output.Glyphs)
            {
                if (glyph.Glyph.Kind == ETextGlyphKind.Object)
                    foreach (var obj in output.Objects)
                    { if (obj.Key != glyph.ObjectKey) continue; glyph.YOff = TextAlgorithms.ObjectRelativeY(textBox, obj); obj.Rect = Rectf.LTWH(pen, glyph.YOff, obj.Size.Width, obj.Size.Height); output.Ascent = TextFontSupport.Max(output.Ascent, -glyph.YOff); output.Descent = TextFontSupport.Max(output.Descent, glyph.YOff + obj.Size.Height); break; }
                pen += glyph.Advance * Math.Max(1u, glyph.Repeat);
            }
        }
        output.Ascent += root.Spacing[(int)ETextSpacing.Top]; output.Descent += root.Spacing[(int)ETextSpacing.Bottom];
        output.LineHeight = TextFontSupport.Max(output.LineHeight, output.Ascent + output.Descent + output.LineGap); output.WidthTrimmed = output.Width;
        output.UnderlinePosition = root.UnderlinePosition; output.UnderlineThickness = root.UnderlineThickness; return true;
    }
    internal bool ResizeShapedObject(TextShapedData shaped, ulong key, Sizef size, ETextInlineAlignment inlineAlign, float baseline)
    {
        var obj = TextAlgorithms.ObjectByKey(shaped.Objects, key); if (obj is null) return false;
        obj.Size = size; obj.InlineAlign = inlineAlign; obj.Baseline = baseline; if (!shaped.Valid) return true;
        foreach (var glyph in shaped.Glyphs) if (glyph.Glyph.Kind == ETextGlyphKind.Object && glyph.ObjectKey == key) { glyph.Advance = size.Width; glyph.Glyph.Advance = new(size.Width, 0); glyph.Glyph.BitmapSize = size; }
        shaped.LogicalGlyphs.Clear(); shaped.SortValid = false; TextAlgorithms.UpdateLineMetrics(this, shaped); return true;
    }
    internal float TabAlign(TextShapedData shaped, IReadOnlyList<float> tabStops)
    {
        if (!UpdateBreaks(shaped) || tabStops.Count == 0) return shaped.Width;
        foreach (float stop in tabStops) if (!float.IsFinite(stop) || stop <= 0) return shaped.Width;
        int tabIndex = 0; float runOffset = 0; bool changed = false, rtl = shaped.InferredDirection == ETextDirection.RTL;
        for (int visual = 0; visual < shaped.Glyphs.Count; visual++)
        {
            var glyph = shaped.Glyphs[rtl ? shaped.Glyphs.Count - visual - 1 : visual];
            if ((glyph.Flags & ETextGraphemeFlag.Tab) != 0)
            {
                float offset = 0; while (offset <= runOffset) { offset += tabStops[tabIndex++]; if (tabIndex == tabStops.Count) tabIndex = 0; }
                float previous = glyph.Advance * Math.Max(1u, glyph.Repeat); glyph.Advance = offset - runOffset;
                float current = glyph.Advance * Math.Max(1u, glyph.Repeat); shaped.Width += current - previous; changed |= current != previous; runOffset = 0;
            }
            else runOffset += glyph.Advance * Math.Max(1u, glyph.Repeat);
        }
        if (!shaped.TextTrimmed) shaped.WidthTrimmed = shaped.Width;
        if (changed) { TextAlgorithms.InvalidateLogicalOrder(shaped); TextAlgorithms.UpdateObjectMainAxisPositions(shaped); } return shaped.Width;
    }
}
