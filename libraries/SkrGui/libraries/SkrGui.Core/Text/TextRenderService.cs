namespace SkrGui;

// Source: text/render/text_render_service.cpp @ 611561f8.
internal struct TextPaintClip { public float Left, Right; public bool HasLeft, HasRight; }
internal enum ETextPaintClipResult : byte { Paint, Skip, StopLine }
internal enum ETextPaintRecordResult : byte { Continue, StopLine, Failure }
internal static partial class TextAlgorithms
{
    internal static TextPaintClip PaintClip(in TextPaintDesc desc, bool clipToFrame, float width)
    {
        TextPaintClip result = new(); if (clipToFrame && width > 0) { result.HasLeft = result.HasRight = true; result.Right = width; }
        if (float.IsFinite(desc.ClipL) && desc.ClipL > 0) { result.Left = result.HasLeft ? Math.Max(result.Left, desc.ClipL) : desc.ClipL; result.HasLeft = true; }
        if (float.IsFinite(desc.ClipR) && desc.ClipR >= 0) { result.Right = result.HasRight ? Math.Min(result.Right, desc.ClipR) : desc.ClipR; result.HasRight = true; }
        return result;
    }
    internal static ETextPaintClipResult PaintClipResult(TextPlacedGlyph glyph, float positionX, in TextPaintClip clip)
    { float pen = positionX - glyph.Glyph.Bearing.X - glyph.XOff; if (clip.HasRight && pen + glyph.Advance > clip.Right) return ETextPaintClipResult.StopLine; if (clip.HasLeft && pen < clip.Left) return ETextPaintClipResult.Skip; return ETextPaintClipResult.Paint; }
    internal static float PaintEllipsisWidth(TextShapedData line) { float result = 0; foreach (var glyph in line.Trim.EllipsisGlyphs) result += GlyphAdvance(glyph); return result; }
    internal static TextRasterGlyph MakeRasterGlyph(TextPlacedGlyph shaped, Offsetf position)
    {
        return new() { Glyph = new() { SourceRange = new(shaped.SourceBegin, shaped.SourceEnd), SpanId = shaped.SpanId, FontFace = shaped.Glyph.Font, Offset = new(shaped.XOff, shaped.YOff), Advance = shaped.Advance, FontSize = shaped.Glyph.FontSize, GlyphIndex = shaped.Glyph.GlyphIndex, Count = (uint)Math.Max(1ul, shaped.SourceEnd - shaped.SourceBegin), Repeat = 1, Flags = shaped.Flags, Kind = shaped.Glyph.Kind, ObjectKey = shaped.ObjectKey }, Position = position, Bounds = Rectf.LTWH(position.X, position.Y, shaped.Glyph.BitmapSize.Width, shaped.Glyph.BitmapSize.Height), Bearing = shaped.Glyph.Bearing, BitmapSize = shaped.Glyph.BitmapSize, SourceSize26_6 = shaped.Glyph.SourceSize26_6 };
    }
}
internal sealed partial class TextServicesImpl
{
    internal bool PaintLines(IReadOnlyList<TextShapedData> lines, ulong visibleLineCount, float frameWidth, ETextHAlign alignment, bool anchorRtlFillToFrameEnd, bool clipToFrame, float lineSpacing, Offsetf origin, TextRenderResult output, TextPaintDesc desc, ulong lineIndex = ulong.MaxValue)
    {
        bool visible = false, result = true; var glyphClip = TextAlgorithms.PaintClip(desc, clipToFrame, frameWidth); AtlasManager.BeginUpdate();
        ETextPaintRecordResult PaintRecord(TextPlacedGlyph glyph, Offsetf position, TextPaintClip clip)
        {
            bool paintable = glyph.Glyph.IsVisible() && !(glyph.Glyph.Kind == ETextGlyphKind.Glyph && !glyph.Glyph.Font.IsValid()); uint repeat = Math.Max(1u, glyph.Repeat);
            for (uint i = 0; i < repeat; i++)
            {
                var instance = position; instance.X += glyph.Advance * i; var clipping = TextAlgorithms.PaintClipResult(glyph, instance.X, clip);
                if (clipping == ETextPaintClipResult.StopLine) return ETextPaintRecordResult.StopLine;
                if (clipping == ETextPaintClipResult.Skip || !paintable) continue;
                visible = true; if (!PaintGlyph(TextAlgorithms.MakeRasterGlyph(glyph, instance), origin, desc, output)) return ETextPaintRecordResult.Failure;
            }
            return ETextPaintRecordResult.Continue;
        }
        ulong first = lineIndex == ulong.MaxValue ? 0 : lineIndex, end = lineIndex == ulong.MaxValue ? Math.Min(visibleLineCount, (ulong)lines.Count) : Math.Min(lineIndex + 1, (ulong)lines.Count);
        float top = 0;
        for (ulong current = first; current < end && result; current++)
        {
            var line = lines[(int)current]; float alignmentOffset = TextAlgorithms.LineAlignmentOffset(line, frameWidth, alignment, anchorRtlFillToFrameEnd), baseline = top + line.Ascent;
            bool rtl = line.InferredDirection == ETextDirection.RTL; ulong visibleBegin = 0, visibleEnd = (ulong)line.Glyphs.Count;
            if (line.Trim.TrimPosition != ulong.MaxValue) { if (rtl) visibleBegin = Math.Min(line.Trim.TrimPosition, visibleEnd); else visibleEnd = Math.Min(line.Trim.TrimPosition, visibleEnd); }
            float pen = rtl ? TextAlgorithms.PaintEllipsisWidth(line) : 0;
            bool PaintEllipsis(float ellipsisPen)
            {
                for (int visual = 0; visual < line.Trim.EllipsisGlyphs.Count; visual++)
                {
                    int index = rtl ? line.Trim.EllipsisGlyphs.Count - visual - 1 : visual; var source = line.Trim.EllipsisGlyphs[index];
                    var position = new Offsetf(alignmentOffset + ellipsisPen + source.XOff + source.Glyph.Bearing.X, baseline + source.YOff - source.Glyph.Bearing.Y);
                    if (PaintRecord(source, position, new()) == ETextPaintRecordResult.Failure) return false;
                    ellipsisPen += source.Advance * Math.Max(1u, source.Repeat);
                }
                return true;
            }
            if (rtl && line.Trim.EllipsisPosition != ulong.MaxValue) result = PaintEllipsis(0);
            bool stopped = false;
            for (ulong i = visibleBegin; i < visibleEnd && result; i++)
            {
                var glyph = line.Glyphs[(int)i]; float x = alignmentOffset + pen + glyph.XOff + glyph.Glyph.Bearing.X;
                float glyphTop = glyph.Glyph.Kind == ETextGlyphKind.Object ? baseline + glyph.YOff : baseline + glyph.YOff - glyph.Glyph.Bearing.Y;
                var outcome = PaintRecord(glyph, new(x, glyphTop), glyphClip);
                if (outcome == ETextPaintRecordResult.Failure) { result = false; break; }
                if (outcome == ETextPaintRecordResult.StopLine) { stopped = true; break; }
                pen += glyph.Advance * Math.Max(1u, glyph.Repeat);
            }
            if (!rtl && !stopped && line.Trim.EllipsisPosition != ulong.MaxValue) result = PaintEllipsis(pen);
            if (lineIndex == ulong.MaxValue) { top += line.LineHeight; if (current + 1 < end) top += lineSpacing; }
        }
        AtlasManager.EndUpdate(); if (!result) { output.Clear(); return false; }
        if (!visible || output.IsEmpty()) output.Bounds = Rectf.Zero(); return true;
    }
}
