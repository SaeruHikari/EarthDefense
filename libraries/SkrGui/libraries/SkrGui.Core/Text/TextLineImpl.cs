using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SkrGui;

// Source: text/layout/text_line.cpp @ 611561f8. Root shaping and line projection
// remain distinct, including font-revision invalidation and unsupported APIs.
internal sealed class TextLineImpl : TextLine, IDisposable
{
    internal readonly WeakReference<TextServicesImpl> Services;
    internal List<float> TabStops = [];
    internal TextShapedData Shaped = new(), Line = new();
    internal bool Dirty = true;
    internal float MaxWidthValue = -1;
    internal ETextHAlign AlignmentValue = ETextHAlign.Left;
    internal ETextJustificationFlag JustificationFlagsValue = TextDefaults.JustificationFlags;
    internal ETextOverrunBehavior OverrunBehavior = ETextOverrunBehavior.TrimEllipsis;
    internal uint EllipsisCodepointValue = TextDefaults.EllipsisCodepoint;
    private readonly List<TextGlyph> _visualGlyphs = [], _logicalGlyphs = [], _ellipsisGlyphs = [];
    private bool _visualGlyphsValid, _logicalGlyphsValid, _ellipsisGlyphsValid;
    internal TextLineImpl(TextServicesImpl services) => Services = new(services);
    private TextLineImpl(WeakReference<TextServicesImpl> services) => Services = services;
    internal TextServicesImpl? LiveServices() => Services.TryGetTarget(out var service) && !service.IsDisposed ? service : null;
    public void Dispose() { Shaped.Dispose(); Line.Dispose(); GC.SuppressFinalize(this); }
    public override void Clear()
    {
        Shaped.Text.Clear(); Shaped.Codepoints.Clear(); Shaped.ByteOffsets.Clear(); Shaped.Spans.Clear(); Shaped.Objects.Clear(); Shaped.BidiOverrides.Clear(); Shaped.SourceRange = new();
        Shaped.Invalidate(true); Line.Dispose(); Line = new(); Dirty = true; InvalidatePublicProjections();
    }
    public override TextLine Duplicate()
    {
        var result = new TextLineImpl(Services); var root = Shaped.Root();
        result.Shaped.Text = root.Text.Copy(); result.Shaped.Codepoints = [..root.Codepoints]; result.Shaped.ByteOffsets = [..root.ByteOffsets];
        result.Shaped.Spans = root.Spans.Select(s => s.Copy()).ToList(); result.Shaped.Objects = root.Objects.Select(o => o with { }).ToList();
        result.Shaped.BidiOverrides = [..root.BidiOverrides]; result.Shaped.CustomPunctuation = new(root.CustomPunctuation.Bytes.ToArray());
        result.Shaped.Direction = root.Direction; result.Shaped.Orientation = root.Orientation; result.Shaped.PreserveInvalid = root.PreserveInvalid; result.Shaped.PreserveControl = root.PreserveControl;
        for (int i = 0; i < 4; i++) result.Shaped.Spacing[i] = root.Spacing[i];
        result.TabStops = [..TabStops]; result.MaxWidthValue = MaxWidthValue; result.AlignmentValue = AlignmentValue;
        result.JustificationFlagsValue = JustificationFlagsValue; result.OverrunBehavior = OverrunBehavior; result.EllipsisCodepointValue = EllipsisCodepointValue; return result;
    }
    public override TextLayoutSpanId AddString(Utf8StringView text, TextStyle style, Utf8StringView language = default)
    {
        TextLayoutSpanId id = new((ulong)Shaped.Spans.Count); ulong start = (ulong)Shaped.Codepoints.Count, byteBase = (ulong)Shaped.Text.Count;
        Shaped.Text.Append(text.Bytes);
        for (ulong offset = 0; offset < text.Size();)
        { Shaped.ByteOffsets.Add(byteBase + offset); var decoded = TextAlgorithms.DecodeNext(text, offset); if (decoded.Bytes == 0) break; Shaped.Codepoints.Add(decoded.Codepoint); offset += decoded.Bytes; }
        Shaped.Spans.Add(new() { Id = id, Start = start, End = (ulong)Shaped.Codepoints.Count, Style = style.Copy(), Language = new Utf8StringView(language.Bytes.ToArray()) }); MarkDirty(true); return id;
    }
    public override bool AddObject(ulong key, Sizef size, ETextInlineAlignment inline_align = ETextInlineAlignment.Center, ulong length = 1, float baseline = 0)
    {
        if (key == 0 || HasObject(key)) return false;
        ulong start = (ulong)Shaped.Codepoints.Count; TextLayoutSpanId span = new((ulong)Shaped.Spans.Count);
        for (ulong i = 0; i < length; i++) { Shaped.ByteOffsets.Add((ulong)Shaped.Text.Count); Shaped.Text.Append(new byte[] { 0xef, 0xbf, 0xbc }); Shaped.Codepoints.Add(0xfffc); }
        Shaped.Objects.Add(new() { Key = key, Start = start, End = start + length, SpanId = span, Size = size, InlineAlign = inline_align, Baseline = baseline });
        Shaped.Spans.Add(new() { Id = span, Start = start, End = start + length }); MarkDirty(true); return true;
    }
    public override bool ResizeObject(ulong key, Sizef size, ETextInlineAlignment inline_align = ETextInlineAlignment.Center, float baseline = 0)
    {
        if (!EnsureReady() || LiveServices() is not { } service) return false;
        bool hadProjection = Line.Valid; bool resized = service.ResizeShapedObject(Shaped, key, size, inline_align, baseline);
        if (resized) { if (hadProjection) { MarkLineDirty(); EnsureReady(); } else InvalidatePublicProjections(); } return resized;
    }
    public override bool HasObject(ulong key) => FindObject(key) is not null;
    public override Utf8StringView Text() => Shaped.Root().Text.View;
    public override TextRange SourceRange() => new(0, (ulong)Shaped.Codepoints.Count);
    public override ulong SpanCount() => (ulong)Shaped.Root().Spans.Count;
    public override TextLayoutSpanId SpanId(ulong index) => index < SpanCount() ? Shaped.Root().Spans[(int)index].Id : new();
    private Utf8StringView SourceText(TextRange range)
    { var root = Shaped.Root(); ulong begin = TextAlgorithms.SourceByteOffset(root.Text, root.ByteOffsets, range.Start), end = TextAlgorithms.SourceByteOffset(root.Text, root.ByteOffsets, range.End); return root.Text.View.Slice((int)begin, (int)(end - begin)); }
    public override Utf8StringView SpanText(TextLayoutSpanId id) => FindSpan(id) is { } span ? SourceText(new(span.Start, span.End)) : default;
    public override ReadOnlyTextStyle? SpanStyle(TextLayoutSpanId id) => FindSpan(id) is { } span ? new ReadOnlyTextStyle(() => span.Style) : null;
    public override Utf8StringView SpanLanguage(TextLayoutSpanId id) => FindSpan(id) is { } span ? span.Language : default(Utf8StringView);
    public override bool SpanEmbeddedObject(TextLayoutSpanId id, ref ulong out_key)
    { if (FindSpan(id) is null) return false; foreach (var obj in Shaped.Root().Objects) if (obj.SpanId == id) { out_key = obj.Key; return true; } return false; }
    public override bool UpdateSpanStyle(TextLayoutSpanId id, TextStyle style)
    { if (!id.IsValid() || id.Value >= (ulong)Shaped.Spans.Count) return false; Shaped.Spans[(int)id.Value].Style = style.Copy(); MarkDirty(false); return true; }
    public override ulong ObjectCount() => (ulong)Shaped.Objects.Count;
    public override ulong ObjectKey(ulong index) => index < ObjectCount() ? Shaped.Objects[(int)index].Key : 0;
    public override TextRange ObjectRange(ulong key) => FindObject(key) is { } obj ? new(obj.Start, obj.End) : new();
    public override long ObjectGlyphIndex(ulong key)
    { if (!EnsureReady()) return -1; var line = LayoutData(); for (int i = 0; i < line.Glyphs.Count; i++) if (line.Glyphs[i].ObjectKey == key) return i; return -1; }
    public override Rectf ObjectRect(ulong key) => EnsureReady() ? TextAlgorithms.LineObjectRect(LayoutData(), key, FrameWidth(), AlignmentValue, false) : new();
    private float FrameWidth() => MaxWidthValue > 0 ? MaxWidthValue : TextAlgorithms.ShapedWidth(LayoutData());
    public override void SetDirection(ETextDirectionMode value) { if (Shaped.Direction != value) { Shaped.Direction = value; MarkDirty(false); } }
    public override ETextDirectionMode Direction() => Shaped.Direction;
    public override ETextDirection InferredDirection() => EnsureReady() ? LayoutData().InferredDirection : Shaped.Direction == ETextDirectionMode.RTL ? ETextDirection.RTL : ETextDirection.LTR;
    public override void SetBidiOverride(ReadOnlySpan<TextBidiOverride> overrides) { Shaped.BidiOverrides.Clear(); foreach (var value in overrides) Shaped.BidiOverrides.Add(value); MarkDirty(false); }
    public override void SetOrientation(ETextOrientation value) { if (value != ETextOrientation.Horizontal) SourceUnimplemented(); }
    public override ETextOrientation Orientation() => Shaped.Orientation;
    public override void SetPreserveInvalid(bool value) { if (Shaped.PreserveInvalid != value) { Shaped.PreserveInvalid = value; MarkDirty(false); } }
    public override bool PreserveInvalid() => Shaped.PreserveInvalid;
    public override void SetPreserveControl(bool value) { if (Shaped.PreserveControl != value) { Shaped.PreserveControl = value; MarkDirty(false); } }
    public override bool PreserveControl() => Shaped.PreserveControl;
    public override void SetCustomPunctuation(Utf8StringView value) { if (Shaped.CustomPunctuation != value) { Shaped.CustomPunctuation = new(value.Bytes.ToArray()); MarkDirty(false); } }
    public override Utf8StringView CustomPunctuation() => Shaped.CustomPunctuation;
    public override void SetMaxWidth(float value) { if (MaxWidthValue == value) return; MaxWidthValue = value; if (AlignmentValue == ETextHAlign.Fill || OverrunBehavior != ETextOverrunBehavior.NoTrimming) MarkLineDirty(); }
    public override float MaxWidth() => MaxWidthValue;
    public override void SetAlignment(ETextHAlign value) { if (AlignmentValue == value) return; bool changesFill = AlignmentValue == ETextHAlign.Fill || value == ETextHAlign.Fill; AlignmentValue = value; if (changesFill) MarkLineDirty(); }
    public override ETextHAlign Alignment() => AlignmentValue;
    public override void SetJustificationFlags(ETextJustificationFlag value) { if (JustificationFlagsValue != value) { JustificationFlagsValue = value; MarkLineDirty(); } }
    public override ETextJustificationFlag JustificationFlags() => JustificationFlagsValue;
    public override void SetTextOverrunBehavior(ETextOverrunBehavior value) { if (OverrunBehavior != value) { OverrunBehavior = value; MarkLineDirty(); } }
    public override ETextOverrunBehavior TextOverrunBehavior() => OverrunBehavior;
    public override void SetEllipsisCodepoint(uint value) { if (EllipsisCodepointValue != value) { EllipsisCodepointValue = value; MarkLineDirty(); } }
    public override uint EllipsisCodepoint() => EllipsisCodepointValue;
    public override void SetSpacing(ETextSpacing type, float value) { if (Shaped.Spacing[(int)type] != value) { Shaped.Spacing[(int)type] = value; MarkDirty(false); } }
    public override float Spacing(ETextSpacing type) => Shaped.Spacing[(int)type];
    public override float TabAlign(ReadOnlySpan<float> stops)
    { foreach (float stop in stops) if (!float.IsFinite(stop) || stop <= 0) return 0; TabStops.Clear(); foreach (var stop in stops) TabStops.Add(stop); MarkLineDirty(); return EnsureReady() ? TextAlgorithms.ShapedWidth(LayoutData()) : 0; }
    public override bool Shape() => EnsureReady();
    public override bool IsReady()
    {
        var service = LiveServices(); if (service is null || Dirty || !Shaped.Valid || NeedsLineProjection() && !Line.Valid) return false;
        if (Shaped.ObservedFontTopologyRevision != service.FontTopologyRevision()) return false;
        ulong revision = service.FontStateRevision(); if (Shaped.ObservedFontStateRevision == revision) return true;
        if (!service.FontDependenciesValid(Shaped.FontDependencies)) return false; Shaped.ObservedFontStateRevision = revision; return true;
    }
    public override ulong GlyphCount() => EnsureReady() ? (ulong)LayoutData().Glyphs.Count : 0;
    public override ReadOnlySpan<TextGlyph> Glyphs() => EnsureVisualGlyphs() ? CollectionsMarshal.AsSpan(_visualGlyphs) : [];
    public override ReadOnlySpan<TextGlyph> SortLogicalGlyphs() => EnsureLogicalGlyphs() ? CollectionsMarshal.AsSpan(_logicalGlyphs) : [];
    public override ulong VisibleCharacters() => EnsureReady() ? VisibleSourceCount(LayoutData()) : 0;
    internal static ulong VisibleSourceCount(TextShapedData shaped)
    {
        int begin = 0, end = shaped.Glyphs.Count;
        if (shaped.TextTrimmed && shaped.Trim.TrimPosition != ulong.MaxValue) { if (shaped.InferredDirection == ETextDirection.RTL) begin = (int)Math.Min(shaped.Trim.TrimPosition, (ulong)end); else end = (int)Math.Min(shaped.Trim.TrimPosition, (ulong)end); }
        ulong count = 0; for (int i = begin; i < end; i++) { var glyph = shaped.Glyphs[i]; if (glyph.ClusterCount == 0 || (glyph.Flags & ETextGraphemeFlag.Virtual) != 0) continue; count += glyph.SourceEnd - glyph.SourceBegin; } return count;
    }
    public override ulong TrimPosition() => EnsureReady() ? LayoutData().Trim.TrimPosition : ulong.MaxValue;
    public override ulong EllipsisPosition() => EnsureReady() ? LayoutData().Trim.EllipsisPosition : ulong.MaxValue;
    public override ReadOnlySpan<TextGlyph> EllipsisGlyphs() => EnsureEllipsisGlyphs() ? CollectionsMarshal.AsSpan(_ellipsisGlyphs) : [];
    public override ulong EllipsisGlyphCount() => EnsureReady() ? (ulong)LayoutData().Trim.EllipsisGlyphs.Count : 0;
    public override Sizef Size() => EnsureReady() ? TextAlgorithms.ShapedSize(LayoutData()) : Sizef.Zero();
    public override float LineAscent() => EnsureReady() ? LayoutData().Ascent : 0;
    public override float LineDescent() => EnsureReady() ? LayoutData().Descent : 0;
    public override float LineWidth() => EnsureReady() ? TextAlgorithms.ShapedWidth(LayoutData()) : 0;
    public override float LineUnderlinePosition() => EnsureReady() ? LayoutData().UnderlinePosition : 0;
    public override float LineUnderlineThickness() => EnsureReady() ? LayoutData().UnderlineThickness : 0;
    private TextRunData? Run(ulong index) => EnsureRuns() && index < (ulong)LayoutData().Runs.Count ? LayoutData().Runs[(int)index] : null;
    public override ulong RunCount() => EnsureRuns() ? (ulong)LayoutData().Runs.Count : 0;
    public override Utf8StringView RunText(ulong index) => Run(index) is { } run ? SourceText(run.SourceRange) : default;
    public override TextRange RunSourceRange(ulong index) => Run(index)?.SourceRange ?? new();
    public override TextRange RunGlyphRange(ulong index) => Run(index)?.GlyphRange ?? new();
    public override FontFace? RunFontFace(ulong index) => Run(index) is { } run ? LiveServices()?.FontFace(run.FontFace) : null;
    public override float RunFontSize(ulong index) => Run(index)?.FontSize ?? 0;
    public override Utf8StringView RunLanguage(ulong index) => Run(index) is { } run ? run.Language : default(Utf8StringView);
    public override ETextDirection RunDirection(ulong index) => Run(index)?.Direction ?? ETextDirection.LTR;
    public override bool RunObject(ulong index, ref ulong out_key) { if (Run(index) is not { IsObject: true } run) return false; out_key = run.ObjectKey; return true; }
    public override TextLayoutSpanId RunSpanId(ulong index) => Run(index)?.SpanId ?? new();
    public override bool LineBreaks(float width, ulong start, ETextLineBreakFlag flags, List<TextRange> out_ranges)
    { out_ranges.Clear(); if (!EnsureReady() || LiveServices() is not { } service) return false; var line = LayoutData(); return service.UpdateBreaks(line) && service.LineBreaks(line, width, start, flags, out_ranges); }
    public override bool LineBreaks(ReadOnlySpan<float> widths, ulong start, bool once, ETextLineBreakFlag flags, List<TextRange> out_ranges)
    { out_ranges.Clear(); SourceUnimplemented(); return false; }
    public override bool WordBreaks(ETextGraphemeFlag grapheme_flags, ETextGraphemeFlag skip_grapheme_flags, List<TextRange> out_ranges)
    {
        out_ranges.Clear(); var logical = SortLogicalGlyphs(); if (!EnsureReady()) return false; ulong wordStart = SourceRange().Start;
        for (int i = 0; i < logical.Length; i++) { var glyph = logical[i]; if (glyph.Count == 0 || (glyph.Flags & grapheme_flags) == 0 || (glyph.Flags & skip_grapheme_flags) != 0) continue; ulong next = i == 0 ? glyph.SourceRange.Start : logical[i - 1].SourceRange.End; if (wordStart < next) out_ranges.Add(new(wordStart, next)); wordStart = glyph.SourceRange.End; }
        if (wordStart < SourceRange().End) out_ranges.Add(new(wordStart, SourceRange().End)); return true;
    }
    public override float FitToWidth(float width, ETextJustificationFlag flags = TextDefaults.JustificationFlags)
    { JustificationFlagsValue = flags; AlignmentValue = ETextHAlign.Fill; MaxWidthValue = width; MarkLineDirty(); return EnsureReady() ? TextAlgorithms.ShapedWidth(LayoutData()) : 0; }
    public override void OverrunTrimToWidth(float width, ETextOverrunFlag flags)
    { MaxWidthValue = width; OverrunBehavior = TextAlgorithms.OverrunBehaviorFromFlags(flags); MarkLineDirty(); EnsureReady(); }
    public override TextLine? Substring(TextRange range) { SourceUnimplemented(); return null; }
    public override long HitTestPosition(float coords) => EnsureReady() ? TextAlgorithms.LineHitTestPosition(LayoutData(), coords) : 0;
    public override long HitTestGrapheme(float coords) => EnsureReady() ? TextAlgorithms.LineHitTestGrapheme(LayoutData(), coords) : -1;
    public override TextCaretInfo Caret(ulong position) => EnsureReady() ? TextAlgorithms.LineCaret(LayoutData(), position) : new();
    public override bool SelectionRects(TextRange range, List<Rectf> out_rects) { out_rects.Clear(); if (!EnsureReady()) return false; TextAlgorithms.LineSelectionRects(LayoutData(), range, out_rects); return true; }
    public override TextFloatRange GraphemeBounds(ulong position) => EnsureReady() ? TextAlgorithms.LineGraphemeBounds(LayoutData(), position) : new();
    public override long PreviousGraphemePosition(ulong position)
    { if (!EnsureReady() || position == 0) return 0; ulong result = SourceRange().Start; foreach (var glyph in SortLogicalGlyphs()) { if (glyph.SourceRange.Start >= position) break; result = glyph.SourceRange.Start; } return (long)result; }
    public override long NextGraphemePosition(ulong position)
    { if (!EnsureReady()) return (long)position; foreach (var glyph in SortLogicalGlyphs()) if (glyph.SourceRange.End > position) return (long)glyph.SourceRange.End; return (long)SourceRange().End; }
    public override bool CharacterBreaks(List<ulong> out_breaks)
    { out_breaks.Clear(); if (!EnsureReady() || LiveServices() is not { } service) return false; service.CharacterBreaks(LayoutData(), out_breaks); return true; }
    public override long PreviousCharacterPosition(ulong position)
    { List<ulong> breaks = []; if (!CharacterBreaks(breaks)) return (long)(position > 0 ? position - 1 : 0); ulong result = SourceRange().Start; foreach (var value in breaks) { if (value >= position) break; result = value; } return (long)result; }
    public override long NextCharacterPosition(ulong position)
    { List<ulong> breaks = []; if (!CharacterBreaks(breaks)) return (long)Math.Min(SourceRange().End, position + 1); foreach (var value in breaks) if (value > position) return (long)value; return (long)SourceRange().End; }
    public override long ClosestCharacterPosition(ulong position)
    { List<ulong> breaks = []; if (!CharacterBreaks(breaks)) return (long)Math.Min(position, SourceRange().End); ulong previous = SourceRange().Start; foreach (var value in breaks) { if (value == position) return (long)value; if (value > position) return (long)(value - position < position - previous ? value : previous); previous = value; } return (long)previous; }
    public override ETextDirection RangeDirection(TextRange range) => EnsureReady() ? TextAlgorithms.LineRangeDirection(LayoutData(), range) : ETextDirection.LTR;
    public override bool Paint(Offsetf origin, TextRenderResult out_result, TextPaintDesc? desc = null)
    { out_result.Clear(); if (!EnsureReady() || LiveServices() is not { } service) return false; return service.PaintLines(new[] { LayoutData() }, 1, FrameWidth(), AlignmentValue, false, MaxWidthValue > 0, 0, origin, out_result, desc ?? new()); }
    internal void MarkDirty(bool sourceChanged = true) { Shaped.Invalidate(sourceChanged); Line.Dispose(); Line = new(); Dirty = true; InvalidatePublicProjections(); }
    internal void MarkLineDirty() { Line.Dispose(); Line = new(); Dirty = true; InvalidatePublicProjections(); }
    internal bool NeedsLineProjection() => TabStops.Count != 0 || AlignmentValue == ETextHAlign.Fill || MaxWidthValue > 0 && OverrunBehavior != ETextOverrunBehavior.NoTrimming;
    internal TextShapedData LayoutData() => Line.Valid ? Line : Shaped;
    private bool EnsureReady() { var service = LiveServices(); if (service is null) return false; return IsReady() || service.ShapeLine(this); }
    private bool EnsureVisualGlyphs() { if (!EnsureReady()) return false; if (!_visualGlyphsValid) { _visualGlyphs.Clear(); foreach (var glyph in LayoutData().Glyphs) _visualGlyphs.Add(TextAlgorithms.PublicGlyph(glyph)); _visualGlyphsValid = true; } return true; }
    private bool EnsureLogicalGlyphs() { if (!EnsureReady() || LiveServices() is not { } service) return false; if (!_logicalGlyphsValid) { _logicalGlyphs.Clear(); foreach (var glyph in service.LogicalGlyphs(LayoutData())) _logicalGlyphs.Add(TextAlgorithms.PublicGlyph(glyph)); _logicalGlyphsValid = true; } return true; }
    private bool EnsureEllipsisGlyphs() { if (!EnsureReady()) return false; if (!_ellipsisGlyphsValid) { _ellipsisGlyphs.Clear(); foreach (var glyph in LayoutData().Trim.EllipsisGlyphs) _ellipsisGlyphs.Add(TextAlgorithms.PublicGlyph(glyph)); _ellipsisGlyphsValid = true; } return true; }
    private bool EnsureRuns() { if (!EnsureReady() || LiveServices() is not { } service) return false; service.Runs(LayoutData()); return true; }
    internal void InvalidatePublicProjections() { _visualGlyphs.Clear(); _logicalGlyphs.Clear(); _ellipsisGlyphs.Clear(); _visualGlyphsValid = _logicalGlyphsValid = _ellipsisGlyphsValid = false; }
    private TextLayoutSpan? FindSpan(TextLayoutSpanId id) => id.IsValid() && id.Value < (ulong)Shaped.Root().Spans.Count ? Shaped.Root().Spans[(int)id.Value] : null;
    private TextShapedObjectData? FindObject(ulong key) { foreach (var obj in Shaped.Root().Objects) if (obj.Key == key) return obj; return null; }
    [Conditional("DEBUG")] private static void SourceUnimplemented() => Debug.Fail("Unimplemented in SkrGui source at 611561f8.");
}
internal static partial class TextAlgorithms
{
    internal static ETextOverrunBehavior OverrunBehaviorFromFlags(ETextOverrunFlag flags)
    {
        if ((flags & ETextOverrunFlag.Trim) == 0) return ETextOverrunBehavior.NoTrimming;
        bool word = (flags & ETextOverrunFlag.TrimWordOnly) != 0, ellipsis = (flags & ETextOverrunFlag.AddEllipsis) != 0, force = (flags & ETextOverrunFlag.EnforceEllipsis) != 0;
        if (ellipsis || force) return word ? (force ? ETextOverrunBehavior.TrimWordEllipsisForce : ETextOverrunBehavior.TrimWordEllipsis) : (force ? ETextOverrunBehavior.TrimEllipsisForce : ETextOverrunBehavior.TrimEllipsis);
        return word ? ETextOverrunBehavior.TrimWord : ETextOverrunBehavior.TrimChar;
    }
}
internal sealed partial class TextServicesImpl
{
    internal bool ShapeLine(TextLineImpl line)
    {
        ulong topology = FontTopologyRevision(), state = FontStateRevision();
        if (line.Shaped.Valid && (line.Shaped.ObservedFontTopologyRevision != topology || line.Shaped.ObservedFontStateRevision != state && !FontDependenciesValid(line.Shaped.FontDependencies)))
        { line.Shaped.Invalidate(false); line.Line.Dispose(); line.Line = new(); line.Dirty = true; line.InvalidatePublicProjections(); }
        if (!ShapeText(line.Shaped)) return false;
        if (!line.Dirty) { line.Shaped.ObservedFontStateRevision = state; return true; }
        line.Line.Dispose(); line.Line = new(); bool needsBreaks = line.TabStops.Count != 0 || line.AlignmentValue == ETextHAlign.Fill || line.OverrunBehavior != ETextOverrunBehavior.NoTrimming;
        if (needsBreaks && !UpdateBreaks(line.Shaped)) return false;
        if (line.NeedsLineProjection())
        {
            if (!ShapeSubstring(line.Shaped, line.Shaped.SourceRange, line.Line, ETextShapedProjection.FullRange)) return false;
            if (line.TabStops.Count != 0) TabAlign(line.Line, line.TabStops);
            var overrun = TextAlgorithms.OverrunFlagsFromBehavior(line.OverrunBehavior);
            if (line.OverrunBehavior != ETextOverrunBehavior.NoTrimming)
            { if (line.AlignmentValue == ETextHAlign.Fill) { FitToWidth(line.Line, line.MaxWidthValue, line.JustificationFlagsValue); overrun |= ETextOverrunFlag.JustificationAware; } OverrunTrimToWidth(line.Line, line.MaxWidthValue, overrun, line.EllipsisCodepointValue); }
            else if (line.AlignmentValue == ETextHAlign.Fill) FitToWidth(line.Line, line.MaxWidthValue, line.JustificationFlagsValue);
        }
        line.Dirty = false; line.Shaped.ObservedFontTopologyRevision = topology; line.Shaped.ObservedFontStateRevision = state; line.InvalidatePublicProjections(); return true;
    }
}
