namespace SkrGui;

// Source: text/layout/text_shaped_pipeline.cpp (source shaping, metrics, runs).
internal sealed class TextGlyphFontMetricsCache
{
    public TextFontFaceId Font;
    public float FontSize;
    public TextMetrics Metrics;
    public bool Resolved, Valid;
}
internal readonly record struct TextGlyphGroup(int Begin, int Count, ulong SourceBegin, ulong SourceEnd, float Advance, ETextGraphemeFlag Flags);
internal static partial class TextAlgorithms
{
    internal static bool IsPunctuation(Utf8StringView custom, uint cp, bool advanced)
    {
        if (!custom.IsEmpty()) { for (ulong offset = 0; offset < custom.Size();) { var decoded = DecodeNext(custom, offset); if (decoded.Codepoint == cp) return true; offset += decoded.Bytes; } return false; }
        if (advanced) return cp != '_' && TextNative.u_ispunct((int)cp);
        return cp != '_' && cp != ' ' && (cp >= 0x20 && cp <= 0x2f || cp >= 0x3a && cp <= 0x40 || cp >= 0x5b && cp <= 0x5e || cp == 0x60 || cp >= 0x7b && cp <= 0x7e || cp >= 0x2000 && cp <= 0x206f || cp >= 0x3000 && cp <= 0x303f);
    }
    internal static TextGlyphGroup GlyphGroupAt(IReadOnlyList<TextPlacedGlyph> glyphs, int begin)
    { int count = GlyphGroupCount(glyphs, begin); var head = glyphs[begin]; float advance = 0; var flags = head.Flags; for (int i = 0; i < count; i++) { advance += GlyphAdvance(glyphs[begin + i]); flags |= glyphs[begin + i].Flags; } return new(begin, count, head.SourceBegin, head.SourceEnd, advance, flags); }
    internal static ulong SpanAtSource(IReadOnlyList<TextLayoutSpan> spans, ulong source)
    { int first = 0, last = spans.Count; while (first < last) { int middle = first + (last - first) / 2; if (spans[middle].End <= source) first = middle + 1; else last = middle; } return first < spans.Count && spans[first].Start <= source && source < spans[first].End ? (ulong)first : ulong.MaxValue; }
    internal static TextShapedObjectData? ObjectByKey(IReadOnlyList<TextShapedObjectData> objects, ulong key) { foreach (var obj in objects) if (obj.Key == key) return obj; return null; }
    internal static bool SpanIsObject(IReadOnlyList<TextShapedObjectData> objects, TextLayoutSpanId id) { foreach (var obj in objects) if (obj.SpanId == id) return true; return false; }
    internal static ETextGraphemeFlag SourceFlags(TextShapedData desc, ulong source, bool advanced)
    {
        if (source >= (ulong)desc.Codepoints.Count) return ETextGraphemeFlag.None;
        uint cp = desc.Codepoints[(int)source]; ETextGraphemeFlag flags = 0;
        if (IsWhitespace(cp)) flags |= ETextGraphemeFlag.Space;
        if (cp is '\t' or '\v') flags |= ETextGraphemeFlag.Tab;
        if (cp == 0xad) flags |= ETextGraphemeFlag.SoftHyphen;
        if (cp == 0x640) flags |= ETextGraphemeFlag.Elongation;
        if (IsPunctuation(desc.CustomPunctuation, cp, advanced)) flags |= ETextGraphemeFlag.Punctuation;
        if (cp == '_') flags |= ETextGraphemeFlag.Underscore; return flags;
    }
    internal static void ClearBreakFlags(TextPlacedGlyph glyph)
    { const ETextGraphemeFlag mask = ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakHard | ETextGraphemeFlag.BreakSoft | ETextGraphemeFlag.Tab | ETextGraphemeFlag.Elongation | ETextGraphemeFlag.Punctuation | ETextGraphemeFlag.Underscore | ETextGraphemeFlag.SoftHyphen; glyph.Flags &= ~mask; }
    private static void AppendFallbackObject(TextShapedObjectData obj, List<TextPlacedGlyph> output)
    { output.Add(new() { Glyph = new() { Kind = ETextGlyphKind.Object, Advance = new(obj.Size.Width, 0), BitmapSize = obj.Size }, SpanId = obj.SpanId, Advance = obj.Size.Width, ClusterCount = 1, Flags = ETextGraphemeFlag.EmbeddedObject | ETextGraphemeFlag.Valid, SourceBegin = obj.Start, SourceEnd = obj.End, ObjectKey = obj.Key }); }
    internal static bool ShapeFallback(TextServicesImpl services, TextShapedData desc, TextShapedData shaped, List<TextPlacedGlyph> output)
    {
        output.Clear(); int spanIndex = 0, objectIndex = 0; ulong spacingSpan = ulong.MaxValue, spacingLimit = 0; TextFontFaceId previousFont = new();
        for (ulong source = 0; source <= (ulong)desc.Codepoints.Count;)
        {
            while (objectIndex < desc.Objects.Count && desc.Objects[objectIndex].Start == source && desc.Objects[objectIndex].End == source)
            { AppendFallbackObject(desc.Objects[objectIndex], output); objectIndex++; previousFont = new(); }
            if (source == (ulong)desc.Codepoints.Count) break;
            TextShapedObjectData? obj = null;
            if (objectIndex < desc.Objects.Count && desc.Objects[objectIndex].Start == source && desc.Objects[objectIndex].End > source) obj = desc.Objects[objectIndex++];
            if (obj is not null) { AppendFallbackObject(obj, output); source = obj.End; previousFont = new(); continue; }
            while (spanIndex < desc.Spans.Count && desc.Spans[spanIndex].End <= source) { spanIndex++; previousFont = new(); }
            if (spanIndex >= desc.Spans.Count) { source++; continue; }
            var span = desc.Spans[spanIndex]; var resolved = shaped.ResolvedSpans[spanIndex]; uint original = desc.Codepoints[(int)source], cp = original;
            bool zeroWidth = IsZeroWidth(cp, desc.PreserveControl);
            if (cp is '\t' or '\v' || zeroWidth) cp = ' ';
            if (!desc.PreserveControl && IsControl(cp)) cp = ' ';
            if (spacingSpan != (ulong)spanIndex)
            {
                spacingSpan = (ulong)spanIndex; spacingLimit = desc.SourceRange.End > 0 ? desc.SourceRange.End - 1 : 0;
                if (spanIndex + 1 == desc.Spans.Count)
                { spacingLimit = span.Start; for (ulong trailing = span.End; trailing > span.Start; trailing--) { spacingLimit = trailing - 1; uint last = desc.Codepoints[(int)spacingLimit]; if (!IsControl(last) && !IsZeroWidth(last, desc.PreserveControl)) break; } }
            }
            TextGlyphMetrics glyph = new(); bool hasFont = false, subpixel = false;
            for (ulong candidate = 0; candidate < services.FontCandidateCount(resolved); candidate++)
            { var font = services.ResolveFontCandidate(span.Style, resolved, candidate); if (services.GetExactGlyphMetrics(font, cp, span.Style.FontSize, ref glyph, out subpixel)) { hasFont = true; break; } }
            if (!hasFont && previousFont.IsValid()) hasFont = services.GetExactGlyphMetrics(previousFont, cp, span.Style.FontSize, ref glyph, out subpixel);
            if (!hasFont)
            { var fallback = services.ResolveSystemFallback(span.Style, new uint[] { original }, span.Language); hasFont = fallback.IsValid() && services.GetExactGlyphMetrics(fallback, cp, span.Style.FontSize, ref glyph, out subpixel); }
            previousFont = hasFont ? glyph.Font : new(); float advance = 0;
            if (hasFont)
            {
                if (original != 0 && !IsLinebreak(original)) advance = glyph.Advance.X;
                if (advance != 0 && source < spacingLimit && services.FindFontFace(glyph.Font) is { } face)
                { var spacing = IsWhitespace(original) ? ETextSpacing.Space : ETextSpacing.Glyph; advance += desc.Spacing[(int)spacing] + face.SpacingValues[(int)spacing]; }
                if (output.Count != 0)
                { var previous = output[^1]; if (previous.Glyph.Kind == ETextGlyphKind.Glyph && previous.Glyph.Font == glyph.Font && previous.Glyph.FontSize == glyph.FontSize) previous.Advance += services.KerningX(glyph.Font, previous.Glyph.GlyphIndex, glyph.GlyphIndex, glyph.FontSize); }
                if (!subpixel) advance = MathF.Round(advance, MidpointRounding.AwayFromZero);
            }
            else if (desc.PreserveInvalid || desc.PreserveControl && IsControl(cp)) { glyph = MissingGlyphMetrics(cp, span.Style.FontSize); advance = glyph.Advance.X; }
            else glyph = new() { Codepoint = cp, GlyphIndex = cp, FontSize = span.Style.FontSize };
            if (zeroWidth) { glyph = ZeroWidthGlyphMetrics(glyph, original, span.Style.FontSize); advance = 0; }
            output.Add(new() { Glyph = glyph, SpanId = span.Id, YOff = glyph.Kind == ETextGlyphKind.Glyph && glyph.GlyphIndex != 0 ? services.FontBaselineShift(glyph.Font, glyph.FontSize) : 0, Advance = advance, ClusterCount = 1, SourceBegin = source, SourceEnd = source + 1 }); source++;
        }
        return true;
    }
    private static float SourceMax(float a, float b) => a < b ? b : a;
    internal static void IncludeFontMetrics(TextShapedData line, in TextMetrics metrics, float yOffset)
    { line.Ascent = SourceMax(line.Ascent, SourceMax(metrics.Ascent, -yOffset)); line.Descent = SourceMax(line.Descent, SourceMax(metrics.Descent, yOffset)); line.UnderlinePosition = SourceMax(line.UnderlinePosition, metrics.UnderlinePosition); line.UnderlineThickness = SourceMax(line.UnderlineThickness, metrics.UnderlineThickness); }
    internal static void IncludeGlyphMetrics(TextServicesImpl services, TextShapedData line, TextPlacedGlyph placed, TextGlyphFontMetricsCache cache)
    {
        var glyph = placed.Glyph;
        if (glyph.Kind == ETextGlyphKind.HexBox) { line.Ascent = SourceMax(line.Ascent, glyph.BitmapSize.Height * .85f); line.Descent = SourceMax(line.Descent, glyph.BitmapSize.Height * .15f); return; }
        if (glyph.Kind != ETextGlyphKind.Glyph) return;
        if (!cache.Resolved || cache.Font != glyph.Font || cache.FontSize != glyph.FontSize)
        { cache.Font = glyph.Font; cache.FontSize = glyph.FontSize; cache.Valid = services.FontMetrics(glyph.Font, glyph.FontSize, ref cache.Metrics); cache.Resolved = true; }
        if (cache.Valid) IncludeFontMetrics(line, cache.Metrics, placed.YOff);
    }
    internal static float ObjectRelativeY(TextShapedData line, TextShapedObjectData obj)
    {
        int align = (byte)obj.InlineAlign;
        float value = (align & 0b1100) switch { (int)ETextInlineAlignment.ToCenter => (-line.Ascent + line.Descent) * .5f, (int)ETextInlineAlignment.ToBaseline => 0, (int)ETextInlineAlignment.ToBottom => line.Descent, _ => -line.Ascent };
        switch (align & 0b0011) { case (int)ETextInlineAlignment.CenterTo: value -= obj.Size.Height * .5f; break; case (int)ETextInlineAlignment.BaselineTo: value -= obj.Baseline; break; case (int)ETextInlineAlignment.BottomTo: value -= obj.Size.Height; break; } return value;
    }
    internal static float LineWidth(IReadOnlyList<TextPlacedGlyph> glyphs) { float width = 0; foreach (var glyph in glyphs) width += GlyphAdvance(glyph); return width; }
    internal static void ResetLineMetrics(TextShapedData shaped)
    { shaped.Ascent = shaped.Descent = shaped.LineGap = shaped.LineHeight = shaped.Width = shaped.WidthTrimmed = shaped.UnderlinePosition = shaped.UnderlineThickness = 0; foreach (var obj in shaped.Objects) obj.Rect = new(); }
    internal static void UpdateLineMetrics(TextServicesImpl services, TextShapedData shaped)
    {
        ResetLineMetrics(shaped); var root = shaped.Root(); TextGlyphFontMetricsCache cache = new();
        foreach (var glyph in shaped.Glyphs) { if (glyph.Glyph.Kind != ETextGlyphKind.Object) IncludeGlyphMetrics(services, shaped, glyph, cache); shaped.Width += GlyphAdvance(glyph); }
        using TextShapedData textBox = new() { Ascent = shaped.Ascent, Descent = shaped.Descent }; float pen = 0;
        foreach (var glyph in shaped.Glyphs)
        {
            if (glyph.Glyph.Kind == ETextGlyphKind.Object)
            {
                var obj = ObjectByKey(shaped.Objects, glyph.ObjectKey);
                if (obj is not null) { glyph.YOff = ObjectRelativeY(textBox, obj); obj.Rect = Rectf.LTWH(pen, glyph.YOff, obj.Size.Width, obj.Size.Height); shaped.Ascent = SourceMax(shaped.Ascent, -glyph.YOff); shaped.Descent = SourceMax(shaped.Descent, glyph.YOff + obj.Size.Height); }
            }
            pen += GlyphAdvance(glyph);
        }
        shaped.Ascent += root.Spacing[(int)ETextSpacing.Top]; shaped.Descent += root.Spacing[(int)ETextSpacing.Bottom];
        shaped.LineHeight = SourceMax(shaped.LineHeight, shaped.Ascent + shaped.Descent + shaped.LineGap); shaped.WidthTrimmed = shaped.Width;
    }
    internal static void InvalidateLogicalOrder(TextShapedData shaped) { shaped.LogicalGlyphs.Clear(); shaped.SortValid = false; }
    internal static void UpdateObjectMainAxisPositions(TextShapedData shaped)
    {
        if (shaped.Objects.Count == 0) return; float pen = 0;
        foreach (var glyph in shaped.Glyphs)
        { if (glyph.Glyph.Kind == ETextGlyphKind.Object) foreach (var obj in shaped.Objects) if (obj.Key == glyph.ObjectKey) { obj.Rect = Rectf.LTWH(pen, obj.Rect.Top, obj.Size.Width, obj.Size.Height); break; } pen += GlyphAdvance(glyph); }
    }
}
internal sealed partial class TextServicesImpl
{
    internal bool ShapeText(TextShapedData shaped)
    {
        shaped.UseTrimmedWidth = BackendValue == ETextServicesBackend.Advanced; if (shaped.Valid) return true; if (!IsValid()) return false;
        shaped.Invalidate(false); shaped.SourceRange = new(0, (ulong)shaped.Codepoints.Count); shaped.InferredDirection = shaped.Direction == ETextDirectionMode.RTL ? ETextDirection.RTL : ETextDirection.LTR;
        shaped.ResolvedSpans.Clear(); foreach (var span in shaped.Spans) { var resolved = new TextResolvedSpan(); ResolveTextStyle(span.Style, resolved); shaped.ResolvedSpans.Add(resolved); }
        bool ok = BackendValue == ETextServicesBackend.Advanced ? ShapeAdvanced(shaped) : TextAlgorithms.ShapeFallback(this, shaped, shaped, shaped.Glyphs);
        if (!ok) { shaped.Invalidate(false); return false; }
        CollectFontDependencies(shaped); TextAlgorithms.UpdateLineMetrics(this, shaped);
        shaped.ObservedFontTopologyRevision = FontTopologyRevisionValue; shaped.ObservedFontStateRevision = FontStateRevisionValue;
        shaped.Valid = true; shaped.SortValid = false; shaped.RunsDirty = true; return true;
    }
    internal IReadOnlyList<TextPlacedGlyph> LogicalGlyphs(TextShapedData shaped)
    {
        if (!shaped.SortValid)
        {
            shaped.LogicalGlyphs = shaped.Glyphs.Select(g => g.Copy()).ToList();
            shaped.LogicalGlyphs.Sort((a, b) => a.SourceBegin != b.SourceBegin ? a.SourceBegin.CompareTo(b.SourceBegin) : a.ClusterCount != b.ClusterCount ? b.ClusterCount.CompareTo(a.ClusterCount) : ((a.Flags & ETextGraphemeFlag.Virtual) != 0).CompareTo((b.Flags & ETextGraphemeFlag.Virtual) != 0)); shaped.SortValid = true;
        }
        return shaped.LogicalGlyphs;
    }
    internal IReadOnlyList<TextRunData> Runs(TextShapedData shaped)
    {
        if (!shaped.RunsDirty) return shaped.Runs; shaped.Runs.Clear(); var root = shaped.Root();
        for (int i = 0; i < shaped.Glyphs.Count; i++)
        {
            var glyph = shaped.Glyphs[i]; var direction = (glyph.Flags & ETextGraphemeFlag.Rtl) != 0 ? ETextDirection.RTL : ETextDirection.LTR; bool isObject = glyph.Glyph.Kind == ETextGlyphKind.Object;
            var span = glyph.SpanId.IsValid() && glyph.SpanId.Value < (ulong)root.Spans.Count ? root.Spans[(int)glyph.SpanId.Value] : null;
            var previous = shaped.Runs.Count != 0 ? shaped.Runs[^1] : null;
            if (previous is not null && previous.SpanId == glyph.SpanId && previous.FontFace == glyph.Glyph.Font && previous.FontSize == glyph.Glyph.FontSize && previous.Direction == direction && previous.IsObject == isObject && (!isObject || previous.ObjectKey == glyph.ObjectKey))
            { previous.SourceRange = new(Math.Min(previous.SourceRange.Start, glyph.SourceBegin), Math.Max(previous.SourceRange.End, glyph.SourceEnd)); previous.GlyphRange = new(previous.GlyphRange.Start, (ulong)i + 1); continue; }
            shaped.Runs.Add(new() { SourceRange = new(glyph.SourceBegin, glyph.SourceEnd), GlyphRange = new((ulong)i, (ulong)i + 1), SpanId = glyph.SpanId, FontFace = glyph.Glyph.Font, Language = span is null ? default : new Utf8StringView(span.Language.Bytes.ToArray()), FontSize = glyph.Glyph.FontSize, Direction = direction, ObjectKey = glyph.ObjectKey, IsObject = isObject });
        }
        shaped.RunsDirty = false; return shaped.Runs;
    }
    internal void CharacterBreaks(TextShapedData shaped, List<ulong> output)
    {
        output.Clear(); var root = shaped.Root(); var range = shaped.SourceRange;
        if (BackendValue == ETextServicesBackend.Fallback) { for (ulong source = range.Start; source < range.End; source++) output.Add(source + 1); return; }
        var cache = root.Advanced;
        if (!cache.CharsValid)
        {
            cache.Characters.Clear(); var boundaries = cache.GraphemeBoundaries;
            if (boundaries.Count == root.Codepoints.Count + 1) { for (int source = 1; source < boundaries.Count; source++) if (boundaries[source] != 0) cache.Characters.Add((ulong)source); }
            else for (int source = 0; source < root.Codepoints.Count; source++) cache.Characters.Add((ulong)source + 1);
            cache.CharsValid = true;
        }
        foreach (ulong source in cache.Characters) if (source > range.Start && source <= range.End) output.Add(source);
    }
    internal bool UpdateBreaks(TextShapedData shaped)
    {
        if (!ShapeText(shaped)) return false; if (shaped.LineBreaksValid) return true;
        if (BackendValue == ETextServicesBackend.Advanced) TextAlgorithms.UpdateAdvancedBreaks(shaped.Root(), shaped); else TextAlgorithms.UpdateFallbackBreaks(shaped.Root(), shaped);
        shaped.LineBreaksValid = true; shaped.SortValid = false; shaped.RunsDirty = true; return true;
    }
    internal bool UpdateJustificationOps(TextShapedData shaped) { if (!UpdateBreaks(shaped)) return false; shaped.JustificationOpsValid = true; return true; }
    internal bool LineBreaks(TextShapedData shaped, float width, ulong start, ETextLineBreakFlag flags, List<TextRange> output)
        => TextAlgorithms.BreakGlyphRanges(LogicalGlyphs(shaped), shaped.SourceRange, width, start, flags, glyph => { TextGlyphMetrics metrics=new(); return GetExactGlyphMetrics(glyph.Glyph.Font, 0xad, glyph.Glyph.FontSize, ref metrics) ? metrics.Advance.X : 0; }, output);
}
