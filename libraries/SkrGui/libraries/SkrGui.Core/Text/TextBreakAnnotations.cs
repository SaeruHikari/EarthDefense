using System.Runtime.InteropServices;
namespace SkrGui;

internal static unsafe partial class TextNative
{
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubrk_getRuleStatus_72")]
    internal static extern int ubrk_getRuleStatus(nint iterator);
}
// Source: text/layout/text_shaped_pipeline.cpp:216-569 @ 611561f8.
internal static partial class TextAlgorithms
{
    internal static unsafe void EnsureAdvancedBreaks(TextShapedData desc)
    {
        var cache = desc.Advanced; if (cache.BreakOpsValid) return;
        cache.Breaks.Clear(); cache.BreakInserts = 0;
        if (desc.Codepoints.Count == 0 || cache.SourceToUtf16.Count != desc.Codepoints.Count + 1) { cache.BreakOpsValid = true; return; }
        ulong SourceAtUtf16(int offset)
        { int first = 0, last = cache.SourceToUtf16.Count; while (first < last) { int mid = first + (last - first) / 2; if (cache.SourceToUtf16[mid] < offset) first = mid + 1; else last = mid; } return (ulong)first; }
        int spanIndex = 0;
        while (spanIndex < desc.Spans.Count)
        {
            var span = desc.Spans[spanIndex]; if (span.Start == span.End) { spanIndex++; continue; }
            Utf8StringView language = span.Language; ulong begin = span.Start, end = span.End; bool startsObject = SpanIsObject(desc.Objects, span.Id);
            while (spanIndex + 1 < desc.Spans.Count)
            {
                var next = desc.Spans[spanIndex + 1]; bool empty = next.Start == next.End, obj = SpanIsObject(desc.Objects, next.Id);
                if (language != next.Language && !startsObject && !obj && !empty) break;
                if (startsObject && !obj) { language = next.Language; startsObject = false; }
                spanIndex++; end = next.End;
            }
            begin = Math.Min(begin, (ulong)desc.Codepoints.Count); end = Math.Clamp(end, begin, (ulong)desc.Codepoints.Count);
            int status = 0; nint iterator;
            fixed (byte* locale = NullTerminated(language))
            fixed (ushort* data = CollectionsMarshal.AsSpan(cache.Utf16))
            {
                iterator = TextNative.ubrk_open(2, language.IsEmpty() ? null : locale, (char*)data + cache.SourceToUtf16[(int)begin], cache.SourceToUtf16[(int)end] - cache.SourceToUtf16[(int)begin], ref status);
                if (status <= 0 && iterator != 0)
                {
                    for (int boundary = TextNative.ubrk_first(iterator); boundary != -1; boundary = TextNative.ubrk_next(iterator))
                    {
                        ulong source = SourceAtUtf16(cache.SourceToUtf16[(int)begin] + boundary); int rule = TextNative.ubrk_getRuleStatus(iterator);
                        if (rule >= 100 && rule < 200) cache.Breaks[source] = true;
                        else if (rule >= 0 && rule < 100) cache.Breaks[source] = false;
                        if (source > begin && source < (ulong)desc.Codepoints.Count)
                        { uint previous = desc.Codepoints[(int)source - 1]; if (!IsWhitespace(previous) && previous != 0xfffc) cache.BreakInserts++; }
                    }
                }
                else
                {
                    for (ulong source = begin; source < end; source++)
                    { uint cp = desc.Codepoints[(int)source]; if (IsHardBreakAt(desc.Codepoints, source)) cache.Breaks[source + 1] = true; else if (IsWhitespace(cp)) cache.Breaks[source + 1] = false; }
                }
                if (iterator != 0) TextNative.ubrk_close(iterator);
            }
            spanIndex++;
        }
        cache.BreakOpsValid = true;
    }
    internal static void UpdateFallbackBreaks(TextShapedData desc, TextShapedData shaped)
    {
        for (int i = 0; i < shaped.Glyphs.Count; i++)
        {
            var glyph = shaped.Glyphs[i]; if (glyph.ClusterCount == 0) continue;
            ClearBreakFlags(glyph); glyph.Flags |= SourceFlags(desc, glyph.SourceBegin, false);
            if (glyph.Glyph.Kind == ETextGlyphKind.Object || glyph.SourceBegin >= (ulong)desc.Codepoints.Count) continue;
            uint cp = desc.Codepoints[(int)glyph.SourceBegin];
            if (IsWhitespace(cp) && !IsLinebreak(cp))
            { glyph.Flags |= ETextGraphemeFlag.Space; if (cp != 0xa0 && cp != 0x202f && cp != 0x2060 && cp != 0x2007) glyph.Flags |= ETextGraphemeFlag.BreakSoft; }
            if (IsLinebreak(cp)) { glyph.Flags |= ETextGraphemeFlag.Space; if (IsHardBreakAt(desc.Codepoints, glyph.SourceBegin)) glyph.Flags |= ETextGraphemeFlag.BreakHard; }
            if (cp is '\t' or '\v') glyph.Flags |= ETextGraphemeFlag.Tab;
            if (cp == 0xad) glyph.Flags |= ETextGraphemeFlag.SoftHyphen;
            i += (int)glyph.ClusterCount - 1;
        }
    }
    internal static void UpdateAdvancedBreaks(TextShapedData desc, TextShapedData shaped)
    {
        EnsureAdvancedBreaks(desc); var glyphs = shaped.Glyphs; List<TextPlacedGlyph> rewritten = [];
        void AppendVirtualBreak(TextPlacedGlyph head, bool rtl)
        {
            var flags = ETextGraphemeFlag.Virtual | ETextGraphemeFlag.BreakSoft | ETextGraphemeFlag.Space;
            if (rtl) flags |= ETextGraphemeFlag.Rtl; if ((head.Flags & ETextGraphemeFlag.Punctuation) != 0) flags |= ETextGraphemeFlag.Punctuation;
            rewritten.Add(new() { Glyph = new() { Font = head.Glyph.Font, FontSize = head.Glyph.FontSize }, SpanId = head.SpanId, ClusterCount = 1, Flags = flags, SourceBegin = head.SourceBegin, SourceEnd = head.SourceEnd });
        }
        for (int begin = 0; begin < glyphs.Count;)
        {
            var group = GlyphGroupAt(glyphs, begin); begin += group.Count; var head = glyphs[group.Begin].Copy();
            ClearBreakFlags(head); head.Flags |= SourceFlags(desc, head.SourceBegin, true);
            if (head.SourceBegin >= (ulong)desc.Codepoints.Count || desc.Codepoints[(int)head.SourceBegin] == 0xfffc)
            { rewritten.Add(head); for (int i = 1; i < group.Count; i++) rewritten.Add(glyphs[group.Begin + i].Copy()); continue; }
            uint cp = desc.Codepoints[(int)head.SourceBegin]; bool rtl = (head.Flags & ETextGraphemeFlag.Rtl) != 0, insert = false;
            if (desc.Advanced.Breaks.TryGetValue(group.SourceEnd, out bool hard))
            {
                if (hard && IsLinebreak(cp)) head.Flags |= ETextGraphemeFlag.BreakHard;
                else if (IsWhitespace(cp) || cp == 0xad) head.Flags |= ETextGraphemeFlag.BreakSoft;
                else
                {
                    insert = true;
                    if (group.SourceEnd == desc.SourceRange.End) insert = false;
                    else if (rtl)
                    { int next = group.Begin + group.Count; const ETextGraphemeFlag both = ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakSoft; if (next < glyphs.Count - 1 && (glyphs[next].Flags & both) == both) insert = false; }
                    else { const ETextGraphemeFlag both = ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakSoft; if ((head.Flags & both) == both) insert = false; }
                    bool headBoundaryObject = head.Glyph.Kind == ETextGlyphKind.Object && head.SourceBegin == head.SourceEnd;
                    bool nextBoundaryObject = group.Begin + 1 < glyphs.Count && glyphs[group.Begin + 1].Glyph.Kind == ETextGlyphKind.Object && glyphs[group.Begin + 1].SourceBegin == glyphs[group.Begin + 1].SourceEnd;
                    if (headBoundaryObject || nextBoundaryObject) insert = false;
                }
            }
            if (rtl && insert) AppendVirtualBreak(head, true);
            rewritten.Add(head); for (int i = 1; i < group.Count; i++) rewritten.Add(glyphs[group.Begin + i].Copy());
            if (!rtl && insert) AppendVirtualBreak(rewritten[rewritten.Count - group.Count], false);
        }
        shaped.Glyphs = rewritten; shaped.LogicalGlyphs.Clear(); shaped.SortValid = false; shaped.Runs.Clear(); shaped.RunsDirty = true;
    }
}
