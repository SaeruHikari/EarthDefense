namespace SkrGui;

// Source: text/layout/text_shaped_pipeline.cpp:2507-end @ 611561f8.
internal sealed partial class TextServicesImpl
{
    private static float PlacedAdvance(TextPlacedGlyph glyph) => glyph.Advance * Math.Max(1u, glyph.Repeat);
    internal float FitToWidth(TextShapedData shaped, float width, ETextJustificationFlag flags)
    {
        if (!UpdateJustificationOps(shaped) || shaped.Glyphs.Count == 0) return shaped.Width;
        bool advanced = BackendValue == ETextServicesBackend.Advanced; if (advanced) shaped.FitWidthMinimumReached = false;
        int begin = 0, end = shaped.Glyphs.Count - 1;
        if ((flags & ETextJustificationFlag.AfterLastTab) != 0)
        {
            if (shaped.InferredDirection == ETextDirection.LTR) { for (int i = end; i >= 0; i--) if ((shaped.Glyphs[i].Flags & ETextGraphemeFlag.Tab) != 0) { begin = i; break; } }
            else { for (int i = 0; i <= end; i++) if ((shaped.Glyphs[i].Flags & ETextGraphemeFlag.Tab) != 0) { end = i; break; } }
        }
        float result = shaped.Width;
        if ((flags & ETextJustificationFlag.ConstrainEllipsis) != 0)
        {
            if (shaped.Trim.TrimPosition == ulong.MaxValue) return MathF.Ceiling(shaped.Width);
            if (advanced && shaped.InferredDirection == ETextDirection.RTL) begin = (int)shaped.Trim.TrimPosition;
            else end = Math.Min((int)shaped.Trim.TrimPosition, end);
            result = shaped.WidthTrimmed;
        }
        static bool IsTrimEdge(TextPlacedGlyph g) => (g.Flags & ETextGraphemeFlag.SoftHyphen) == 0 && (g.Flags & (ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakHard | ETextGraphemeFlag.BreakSoft)) != 0;
        static bool IsBreakEdge(TextPlacedGlyph g) => (g.Flags & ETextGraphemeFlag.SoftHyphen) == 0 && (g.Flags & (ETextGraphemeFlag.BreakHard | ETextGraphemeFlag.BreakSoft)) != 0;
        if ((flags & ETextJustificationFlag.TrimEdgeSpaces) != 0)
        {
            while (begin < end && IsTrimEdge(shaped.Glyphs[begin])) { var glyph = shaped.Glyphs[begin]; result -= PlacedAdvance(glyph); glyph.Advance = 0; begin += (int)glyph.ClusterCount; }
            while (begin < end && IsTrimEdge(shaped.Glyphs[end])) { var glyph = shaped.Glyphs[end]; result -= PlacedAdvance(glyph); glyph.Advance = 0; end -= (int)glyph.ClusterCount; }
        }
        else
        {
            while (begin < end && IsBreakEdge(shaped.Glyphs[begin])) begin += (int)shaped.Glyphs[begin].ClusterCount;
            while (begin < end && IsBreakEdge(shaped.Glyphs[end])) end -= (int)shaped.Glyphs[end].ClusterCount;
        }
        ulong spaces = 0;
        for (int i = begin; i <= end; i++)
        { var glyph = shaped.Glyphs[i]; if (glyph.ClusterCount > 0 && (glyph.Flags & ETextGraphemeFlag.Space) != 0 && (glyph.Flags & (ETextGraphemeFlag.SoftHyphen | ETextGraphemeFlag.Punctuation)) == 0) spaces++; }
        if (spaces > 0 && (flags & ETextJustificationFlag.WordBound) != 0)
        {
            float delta = (width - result) / spaces;
            for (int i = begin; i <= end; i++)
            {
                var glyph = shaped.Glyphs[i]; if (glyph.ClusterCount == 0 || (glyph.Flags & ETextGraphemeFlag.Space) == 0 || (glyph.Flags & (ETextGraphemeFlag.SoftHyphen | ETextGraphemeFlag.Punctuation)) != 0) continue;
                float old = glyph.Advance;
                float minimum = advanced ? ((glyph.Flags & ETextGraphemeFlag.Virtual) != 0 ? 0 : glyph.Glyph.FontSize * .1f) : MathF.Round(glyph.Glyph.FontSize * .1f, MidpointRounding.AwayFromZero);
                glyph.Advance = TextFontSupport.Max(glyph.Advance + delta, minimum); result += glyph.Advance - old;
            }
        }
        shaped.FitWidthMinimumReached = MathF.Floor(width) < MathF.Floor(result);
        if ((flags & ETextJustificationFlag.ConstrainEllipsis) == 0) shaped.Width = result;
        TextAlgorithms.InvalidateLogicalOrder(shaped); TextAlgorithms.UpdateObjectMainAxisPositions(shaped); return MathF.Ceiling(result);
    }
    internal void OverrunTrimToWidth(TextShapedData shaped, float width, ETextOverrunFlag flags, uint ellipsisCodepoint)
    {
        if (!UpdateBreaks(shaped)) return;
        shaped.TextTrimmed = false; shaped.Trim.EllipsisGlyphs.Clear();
        bool add = (flags & ETextOverrunFlag.AddEllipsis) != 0, word = (flags & ETextOverrunFlag.TrimWordOnly) != 0,
            enforce = (flags & ETextOverrunFlag.EnforceEllipsis) != 0, shortString = (flags & ETextOverrunFlag.ShortStringEllipsis) != 0,
            justification = (flags & ETextOverrunFlag.JustificationAware) != 0;
        if ((flags & ETextOverrunFlag.Trim) == 0 || shaped.Glyphs.Count == 0 || width <= 0 || !(shaped.Width > width || enforce))
        { shaped.Trim.TrimPosition = ulong.MaxValue; shaped.Trim.EllipsisPosition = ulong.MaxValue; return; }
        if (justification && !shaped.FitWidthMinimumReached) return;
        TextGlyphMetrics em = new(); TextLayoutSpanId es = new(); bool foundCharacter = false;
        if (add || enforce || shortString)
        { foundCharacter = ResolveTrimGlyph(shaped, ellipsisCodepoint, true, ref em, ref es); if (!foundCharacter) ResolveTrimGlyph(shaped, '.', true, ref em, ref es); }
        TextGlyphMetrics wm=new();TextLayoutSpanId ws=new();bool foundWhitespace = ResolveTrimGlyph(shaped, ' ', false, ref wm, ref ws);
        int ew = 0;
        if (add && foundWhitespace && em.Font.IsValid()) { uint repeat = foundCharacter ? 1u : 3u; ew = (int)(repeat * em.Advance.X + TrimGlyphSpacing(shaped, em.Font) + (word ? wm.Advance.X : 0)); }
        const int minimumCharacters = 6; float remaining = shaped.Width, withoutEllipsis = remaining;
        bool rtl = BackendValue == ETextServicesBackend.Advanced && shaped.InferredDirection == ETextDirection.RTL;
        int count = shaped.Glyphs.Count, trim = rtl ? count : 0, ellipsis = enforce || shortString ? 0 : -1, lastCut = -1, lastCutWithout = -1;
        int from = rtl ? 0 : count - 1, to = rtl ? count - 1 : -1, step = rtl ? 1 : -1;
        if ((enforce || shortString) && remaining + ew <= width) { trim = -1; ellipsis = rtl ? 0 : count; }
        else
        {
            for (int i = from; i != to; i += step)
            {
                var glyph = shaped.Glyphs[i]; if (!rtl) remaining -= PlacedAdvance(glyph);
                if (glyph.ClusterCount > 0)
                {
                    bool above = (rtl ? count - 1 - i : i) >= minimumCharacters;
                    if (!above && lastCutWithout != -1) { trim = lastCutWithout; ellipsis = -1; remaining = withoutEllipsis; break; }
                    if (!(enforce || shortString) && remaining <= width && lastCutWithout == -1)
                    {
                        if (word && above) { if ((glyph.Flags & ETextGraphemeFlag.BreakSoft) != 0) { lastCutWithout = i; withoutEllipsis = remaining; } }
                        else { lastCutWithout = i; withoutEllipsis = remaining; }
                    }
                    float reserved = above && add || enforce || shortString ? ew : 0;
                    if (remaining + reserved <= width)
                    {
                        if (word && above) { if ((glyph.Flags & ETextGraphemeFlag.BreakSoft) != 0) lastCut = i; } else lastCut = i;
                        if (lastCut != -1) { trim = lastCut; if (add && (above || enforce || shortString) && remaining - ew <= width) ellipsis = trim; break; }
                    }
                }
                if (rtl) remaining -= PlacedAdvance(glyph);
            }
        }
        shaped.Trim.TrimPosition = trim >= 0 ? (ulong)trim : ulong.MaxValue; shaped.Trim.EllipsisPosition = ellipsis >= 0 ? (ulong)ellipsis : ulong.MaxValue;
        if (trim == 0 && (enforce || shortString) && add) shaped.Trim.EllipsisPosition = 0;
        if (trim >= 0 && shaped.Width > width || enforce || shortString)
        {
            if (add && (ellipsis > 0 || enforce || shortString))
            {
                var direction = rtl ? ETextGraphemeFlag.Rtl : ETextGraphemeFlag.None;
                if (word && ellipsis > 0 && foundWhitespace)
                    shaped.Trim.EllipsisGlyphs.Add(new() { Glyph = wm, SpanId = ws, Advance = wm.Advance.X, ClusterCount = 1, Flags = ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakSoft | ETextGraphemeFlag.Virtual | direction });
                if (em.GlyphIndex != 0)
                    shaped.Trim.EllipsisGlyphs.Add(new() { Glyph = em, SpanId = es, Advance = em.Advance.X, Repeat = foundCharacter ? 1u : 3u, ClusterCount = 1, Flags = ETextGraphemeFlag.Punctuation | ETextGraphemeFlag.Virtual | direction });
            }
            shaped.TextTrimmed = true; shaped.WidthTrimmed = remaining + (ellipsis != -1 ? ew : 0);
        }
    }
}
