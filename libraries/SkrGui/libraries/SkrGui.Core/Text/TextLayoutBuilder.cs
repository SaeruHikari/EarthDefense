namespace SkrGui;

// Source: text/layout/text_layout_builder.cpp @ 611561f8.
internal readonly record struct TextDecodeResult(uint Codepoint, ulong Bytes);
internal static partial class TextAlgorithms
{
    private static float GlyphAdvance(TextPlacedGlyph glyph) => glyph.Advance * Math.Max(1u, glyph.Repeat);
    internal static int GlyphGroupCount(IReadOnlyList<TextPlacedGlyph> glyphs, int begin) => (int)Math.Min(Math.Max(1u, glyphs[begin].ClusterCount), (uint)(glyphs.Count - begin));
    internal static float GlyphGroupAdvance(IReadOnlyList<TextPlacedGlyph> glyphs, int begin)
    { float result = 0; int count = GlyphGroupCount(glyphs, begin); for (int i = 0; i < count; i++) result += GlyphAdvance(glyphs[begin + i]); return result; }
    internal static bool GlyphIsRtl(TextPlacedGlyph glyph) => (glyph.Flags & ETextGraphemeFlag.Rtl) != 0;
    private static bool GlyphHasGraphemeGeometry(TextPlacedGlyph glyph) => glyph.Glyph.GlyphIndex != 0 || (glyph.Flags & ETextGraphemeFlag.Space) != 0;
    internal static float ShapedWidth(TextShapedData shaped) => MathF.Ceiling(shaped.TextTrimmed && shaped.Root().UseTrimmedWidth ? shaped.WidthTrimmed : shaped.Width);
    internal static Sizef ShapedSize(TextShapedData shaped) => new(ShapedWidth(shaped), MathF.Ceiling(shaped.LineHeight));
    internal static float LineAlignmentOffset(TextShapedData line, float frameWidth, ETextHAlign alignment, bool anchorRtlFillToFrameEnd)
    {
        float width = ShapedWidth(line); if (!(frameWidth > 0)) return 0;
        if (alignment == ETextHAlign.Fill) return anchorRtlFillToFrameEnd && line.InferredDirection == ETextDirection.RTL ? frameWidth - width : 0;
        if (alignment == ETextHAlign.Center)
        { if (width <= frameWidth) return MathF.Floor((frameWidth - width) * .5f); return line.InferredDirection == ETextDirection.RTL ? frameWidth - width : 0; }
        return alignment == ETextHAlign.Right ? frameWidth - width : 0;
    }
    internal static Rectf LineObjectRect(TextShapedData line, ulong key, float frameWidth, ETextHAlign alignment, bool anchorRtlFillToFrameEnd)
    {
        foreach (var obj in line.Objects)
            if (obj.Key == key) return obj.Rect.Shift(new(LineAlignmentOffset(line, frameWidth, alignment, anchorRtlFillToFrameEnd), line.Ascent));
        return new();
    }
    internal static long LineHitTestPosition(TextShapedData line, float position)
    {
        var glyphs = line.Glyphs; if (glyphs.Count == 0) return (long)line.SourceRange.Start;
        float pen = 0;
        if (MathF.Floor(position) <= 0) return (long)(GlyphIsRtl(glyphs[0]) ? glyphs[0].SourceEnd : glyphs[0].SourceBegin);
        if (MathF.Ceiling(position) >= ShapedWidth(line)) { var last = glyphs[^1]; return (long)(GlyphIsRtl(last) ? last.SourceBegin : last.SourceEnd); }
        for (int i = 0; i < glyphs.Count;)
        {
            var glyph = glyphs[i]; float advance = GlyphGroupAdvance(glyphs, i);
            if (position < pen + advance)
            {
                bool rtl = GlyphIsRtl(glyph);
                if ((glyph.Flags & ETextGraphemeFlag.Virtual) != 0) return (long)(rtl ? glyph.SourceEnd : glyph.SourceBegin);
                ulong count = glyph.SourceEnd - glyph.SourceBegin;
                if (count > 1)
                {
                    float characterAdvance = advance / count;
                    ulong slot = Math.Min(count, (ulong)MathF.Floor((position - pen) / characterAdvance + .5f));
                    return (long)(rtl ? glyph.SourceEnd - slot : glyph.SourceBegin + slot);
                }
                return (long)(position < pen + advance * .5f ? (rtl ? glyph.SourceEnd : glyph.SourceBegin) : (rtl ? glyph.SourceBegin : glyph.SourceEnd));
            }
            pen += advance; i += GlyphGroupCount(glyphs, i);
        }
        return -1;
    }
    internal static long LineHitTestGrapheme(TextShapedData line, float position)
    {
        var glyphs = line.Glyphs; float pen = 0;
        for (int i = 0; i < glyphs.Count;)
        { float advance = GlyphGroupAdvance(glyphs, i); if (position >= pen && position < pen + advance) return i; pen += advance; i += GlyphGroupCount(glyphs, i); }
        return -1;
    }
    internal static TextCaretInfo LineCaret(TextShapedData line, ulong position)
    {
        TextCaretInfo result = new(); bool hasLeading = false, hasTrailing = false; float pen = 0; var glyphs = line.Glyphs;
        for (int i = 0; i < glyphs.Count;)
        {
            var glyph = glyphs[i]; int count = GlyphGroupCount(glyphs, i); float advance = GlyphGroupAdvance(glyphs, i);
            if ((glyph.Flags & ETextGraphemeFlag.Virtual) == 0)
            {
                bool rtl = GlyphIsRtl(glyph); var direction = rtl ? ETextDirection.RTL : ETextDirection.LTR;
                if (position == glyph.SourceBegin) { float x = rtl ? pen + advance : pen; result.TrailingCaret = Rectf.LTWH(x, 0, 1, line.LineHeight); result.TrailingDirection = direction; hasTrailing = true; }
                if (position == glyph.SourceEnd) { float x = rtl ? pen : pen + advance; result.LeadingCaret = Rectf.LTWH(x, 0, 1, line.LineHeight); result.LeadingDirection = direction; hasLeading = true; }
                if (position > glyph.SourceBegin && position < glyph.SourceEnd)
                {
                    float denominator = Math.Max(1ul, glyph.SourceEnd - glyph.SourceBegin);
                    float progress = (position - glyph.SourceBegin) / denominator;
                    float x = rtl ? pen + advance * (1 - progress) : pen + advance * progress;
                    result.LeadingCaret = Rectf.LTWH(x, 0, 1, line.LineHeight); result.TrailingCaret = result.LeadingCaret;
                    result.LeadingDirection = result.TrailingDirection = direction; return result;
                }
            }
            pen += advance; i += count;
        }
        if (!hasLeading && hasTrailing) { result.LeadingCaret = result.TrailingCaret; result.LeadingDirection = result.TrailingDirection; }
        else if (hasLeading && !hasTrailing) { result.TrailingCaret = result.LeadingCaret; result.TrailingDirection = result.LeadingDirection; }
        else if (!hasLeading && !hasTrailing) { result.LeadingCaret = Rectf.LTWH(ShapedWidth(line), 0, 1, line.LineHeight); result.TrailingCaret = result.LeadingCaret; }
        return result;
    }
    internal static void LineSelectionRects(TextShapedData line, TextRange range, List<Rectf> output)
    {
        if (range.Start == range.End) return; if (range.End < range.Start) range = new(range.End, range.Start);
        float pen = 0; var glyphs = line.Glyphs;
        for (int i = 0; i < glyphs.Count;)
        {
            var glyph = glyphs[i]; int count = GlyphGroupCount(glyphs, i); float advance = GlyphGroupAdvance(glyphs, i);
            if (glyph.SourceBegin < range.End && glyph.SourceEnd > range.Start && GlyphHasGraphemeGeometry(glyph))
            {
                ulong sourceCount = Math.Max(1ul, glyph.SourceEnd - glyph.SourceBegin);
                ulong begin = Math.Max(range.Start, glyph.SourceBegin), end = Math.Min(range.End, glyph.SourceEnd);
                float bf = (float)(begin - glyph.SourceBegin) / sourceCount, ef = (float)(end - glyph.SourceBegin) / sourceCount;
                bool rtl = GlyphIsRtl(glyph);
                float left = rtl ? pen + advance * (1 - ef) : pen + advance * bf, right = rtl ? pen + advance * (1 - bf) : pen + advance * ef;
                if (output.Count != 0 && MathF.Abs(output[^1].Right - left) <= .0001f && MathF.Abs(output[^1].Top) <= .0001f)
                { var previous = output[^1]; previous.Right = right; output[^1] = previous; }
                else output.Add(Rectf.LTWH(left, 0, right - left, line.LineHeight));
            }
            pen += advance; i += count;
        }
    }
    internal static TextFloatRange LineGraphemeBounds(TextShapedData line, ulong position)
    {
        float pen = 0; var glyphs = line.Glyphs;
        for (int i = 0; i < glyphs.Count;)
        {
            var glyph = glyphs[i]; float advance = GlyphGroupAdvance(glyphs, i);
            if (position >= glyph.SourceBegin && position <= glyph.SourceEnd && GlyphHasGraphemeGeometry(glyph)) return new(pen, pen + advance);
            pen += advance; i += GlyphGroupCount(glyphs, i);
        }
        return new();
    }
    internal static ETextDirection LineRangeDirection(TextShapedData line, TextRange range)
    {
        ulong ltr = 0, rtl = 0; var glyphs = line.Glyphs;
        for (int i = 0; i < glyphs.Count;)
        { var glyph = glyphs[i]; if (glyph.SourceBegin < range.End && glyph.SourceEnd > range.Start) { if (GlyphIsRtl(glyph)) rtl++; else ltr++; } i += GlyphGroupCount(glyphs, i); }
        return rtl == ltr ? line.InferredDirection : rtl > ltr ? ETextDirection.RTL : ETextDirection.LTR;
    }
    internal static TextDecodeResult DecodeNext(Utf8StringView text, ulong offset)
    {
        if (offset >= text.Size()) return new(); var bytes = text.Bytes; byte first = bytes[(int)offset]; if (first < 0x80) return new(first, 1);
        uint cp, minimum; ulong length;
        if ((first & 0xe0) == 0xc0) { cp = (uint)(first & 0x1f); length = 2; minimum = 0x80; }
        else if ((first & 0xf0) == 0xe0) { cp = (uint)(first & 0x0f); length = 3; minimum = 0x800; }
        else if ((first & 0xf8) == 0xf0) { cp = (uint)(first & 0x07); length = 4; minimum = 0x10000; }
        else return new(0xfffd, 1);
        if (offset + length > text.Size()) return new(0xfffd, 1);
        for (ulong i = 1; i < length; i++)
        { byte continuation = bytes[(int)(offset + i)]; if ((continuation & 0xc0) != 0x80) return new(0xfffd, 1); cp = (cp << 6) | (uint)(continuation & 0x3f); }
        if (cp < minimum || cp > 0x10ffff || cp >= 0xd800 && cp <= 0xdfff) return new(0xfffd, 1);
        return new(cp, length);
    }
    internal static ETextOverrunFlag OverrunFlagsFromBehavior(ETextOverrunBehavior behavior) => behavior switch
    {
        ETextOverrunBehavior.TrimChar => ETextOverrunFlag.Trim,
        ETextOverrunBehavior.TrimWord => ETextOverrunFlag.Trim | ETextOverrunFlag.TrimWordOnly,
        ETextOverrunBehavior.TrimEllipsis => ETextOverrunFlag.Trim | ETextOverrunFlag.AddEllipsis,
        ETextOverrunBehavior.TrimWordEllipsis => ETextOverrunFlag.Trim | ETextOverrunFlag.TrimWordOnly | ETextOverrunFlag.AddEllipsis,
        ETextOverrunBehavior.TrimEllipsisForce => ETextOverrunFlag.Trim | ETextOverrunFlag.AddEllipsis | ETextOverrunFlag.EnforceEllipsis,
        ETextOverrunBehavior.TrimWordEllipsisForce => ETextOverrunFlag.Trim | ETextOverrunFlag.TrimWordOnly | ETextOverrunFlag.AddEllipsis | ETextOverrunFlag.EnforceEllipsis,
        _ => ETextOverrunFlag.None
    };
}
