using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SkrGui;

// Source: text/layout/text_paragraph.cpp @ 611561f8.
internal sealed class TextParagraphImpl : TextParagraph, IDisposable
{
    internal readonly WeakReference<TextServicesImpl> Services;
    internal List<float> TabStops = [];
    internal TextShapedData Shaped = new();
    internal List<TextShapedData> Lines = [];
    internal bool LinesDirty = true;
    internal float MaxWidthValue = -1, LineSpacingValue;
    internal int MaxLinesVisibleValue = -1;
    internal ETextHAlign AlignmentValue = ETextHAlign.Left;
    internal ETextLineBreakFlag BreakFlagsValue = TextDefaults.LineBreakFlags;
    internal ETextJustificationFlag JustificationFlagsValue = TextDefaults.JustificationFlags | ETextJustificationFlag.SkipLastLine | ETextJustificationFlag.DoNotSkipSingleLine;
    internal ETextOverrunBehavior OverrunBehavior = ETextOverrunBehavior.NoTrimming;
    internal uint EllipsisCodepointValue = TextDefaults.EllipsisCodepoint;
    private readonly List<TextGlyph> _visualGlyphs = [], _logicalGlyphs = [], _ellipsisGlyphs = [];
    private readonly List<TextRunData> _runs = [];
    private bool _visualGlyphsValid, _logicalGlyphsValid, _ellipsisGlyphsValid, _runsValid;
    internal TextParagraphImpl(TextServicesImpl services) => Services = new(services);
    private TextParagraphImpl(WeakReference<TextServicesImpl> services) => Services = services;
    internal TextServicesImpl? LiveServices() => Services.TryGetTarget(out var service) && !service.IsDisposed ? service : null;
    private void ClearLines() { foreach (var line in Lines) line.Dispose(); Lines.Clear(); }
    public void Dispose() { Shaped.Dispose(); ClearLines(); GC.SuppressFinalize(this); }
    public override void Clear()
    {
        Shaped.Text.Clear(); Shaped.Codepoints.Clear(); Shaped.ByteOffsets.Clear(); Shaped.Spans.Clear(); Shaped.Objects.Clear(); Shaped.BidiOverrides.Clear();
        Shaped.Invalidate(true); ClearLines(); LinesDirty = true; InvalidateLineProjections();
    }
    public override TextParagraph Duplicate()
    {
        var result = new TextParagraphImpl(Services);
        result.Shaped.Text = Shaped.Text.Copy(); result.Shaped.Codepoints = [..Shaped.Codepoints]; result.Shaped.ByteOffsets = [..Shaped.ByteOffsets];
        result.Shaped.Spans = Shaped.Spans.Select(s => s.Copy()).ToList(); result.Shaped.Objects = Shaped.Objects.Select(o => o with { }).ToList();
        result.Shaped.BidiOverrides = [..Shaped.BidiOverrides]; result.Shaped.CustomPunctuation = new(Shaped.CustomPunctuation.Bytes.ToArray());
        result.Shaped.Direction = Shaped.Direction; result.Shaped.Orientation = Shaped.Orientation; result.Shaped.PreserveInvalid = Shaped.PreserveInvalid; result.Shaped.PreserveControl = Shaped.PreserveControl;
        for (int i = 0; i < 4; i++) result.Shaped.Spacing[i] = Shaped.Spacing[i];
        result.TabStops = [..TabStops]; result.MaxWidthValue = MaxWidthValue; result.MaxLinesVisibleValue = MaxLinesVisibleValue; result.LineSpacingValue = LineSpacingValue; result.AlignmentValue = AlignmentValue;
        result.BreakFlagsValue = BreakFlagsValue; result.JustificationFlagsValue = JustificationFlagsValue; result.OverrunBehavior = OverrunBehavior; result.EllipsisCodepointValue = EllipsisCodepointValue; return result;
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
    public override bool HasObject(ulong key) => FindObject(key) is not null;
    public override Utf8StringView Text() => Shaped.Text.View;
    public override TextRange SourceRange() => new(0, (ulong)Shaped.Codepoints.Count);
    public override ulong SpanCount() => (ulong)Shaped.Spans.Count;
    public override TextLayoutSpanId SpanId(ulong index) => index < SpanCount() ? Shaped.Spans[(int)index].Id : new();
    private Utf8StringView SourceText(TextRange range)
    { var root = Shaped; ulong begin = TextAlgorithms.SourceByteOffset(root.Text, root.ByteOffsets, range.Start), end = TextAlgorithms.SourceByteOffset(root.Text, root.ByteOffsets, range.End); return root.Text.View.Slice((int)begin, (int)(end - begin)); }
    public override Utf8StringView SpanText(TextLayoutSpanId id) => FindSpan(id) is { } span ? SourceText(new(span.Start, span.End)) : default;
    public override ReadOnlyTextStyle? SpanStyle(TextLayoutSpanId id) => FindSpan(id) is { } span ? new ReadOnlyTextStyle(() => span.Style) : null;
    public override Utf8StringView SpanLanguage(TextLayoutSpanId id) => FindSpan(id) is { } span ? span.Language : default(Utf8StringView);
    public override bool SpanEmbeddedObject(TextLayoutSpanId id, ref ulong out_key)
    { if (FindSpan(id) is null) return false; foreach (var obj in Shaped.Objects) if (obj.SpanId == id) { out_key = obj.Key; return true; } return false; }
    public override bool UpdateSpanStyle(TextLayoutSpanId id, TextStyle style)
    { if (!id.IsValid() || id.Value >= (ulong)Shaped.Spans.Count) return false; Shaped.Spans[(int)id.Value].Style = style.Copy(); MarkDirty(false); return true; }
    public override ulong ObjectCount() => (ulong)Shaped.Objects.Count;
    public override ulong ObjectKey(ulong index) => index < ObjectCount() ? Shaped.Objects[(int)index].Key : 0;
    public override TextRange ObjectRange(ulong key) => FindObject(key) is { } obj ? new(obj.Start, obj.End) : new();
    public override void SetDirection(ETextDirectionMode value) { if (Shaped.Direction != value) { Shaped.Direction = value; MarkDirty(false); } }
    public override ETextDirectionMode Direction() => Shaped.Direction;
    public override void SetBidiOverride(ReadOnlySpan<TextBidiOverride> overrides) { Shaped.BidiOverrides.Clear(); foreach (var value in overrides) Shaped.BidiOverrides.Add(value); MarkDirty(false); }
    public override void SetOrientation(ETextOrientation value) { if (value != ETextOrientation.Horizontal) SourceUnimplemented(); }
    public override ETextOrientation Orientation() => Shaped.Orientation;
    public override void SetPreserveInvalid(bool value) { if (Shaped.PreserveInvalid != value) { Shaped.PreserveInvalid = value; MarkDirty(false); } }
    public override bool PreserveInvalid() => Shaped.PreserveInvalid;
    public override void SetPreserveControl(bool value) { if (Shaped.PreserveControl != value) { Shaped.PreserveControl = value; MarkDirty(false); } }
    public override bool PreserveControl() => Shaped.PreserveControl;
    public override void SetCustomPunctuation(Utf8StringView value) { if (Shaped.CustomPunctuation != value) { Shaped.CustomPunctuation = new(value.Bytes.ToArray()); MarkDirty(false); } }
    public override Utf8StringView CustomPunctuation() => Shaped.CustomPunctuation;
    public override void SetAlignment(ETextHAlign value) { if (AlignmentValue == value) return; bool changesFill = AlignmentValue == ETextHAlign.Fill || value == ETextHAlign.Fill; AlignmentValue = value; if (changesFill) MarkLinesDirty(); }
    public override ETextHAlign Alignment() => AlignmentValue;
    public override void SetJustificationFlags(ETextJustificationFlag value) { if (JustificationFlagsValue != value) { JustificationFlagsValue = value; MarkLinesDirty(); } }
    public override ETextJustificationFlag JustificationFlags() => JustificationFlagsValue;
    public override void SetTextOverrunBehavior(ETextOverrunBehavior value) { if (OverrunBehavior != value) { OverrunBehavior = value; MarkLinesDirty(); } }
    public override ETextOverrunBehavior TextOverrunBehavior() => OverrunBehavior;
    public override void SetEllipsisCodepoint(uint value) { if (EllipsisCodepointValue != value) { EllipsisCodepointValue = value; MarkLinesDirty(); } }
    public override uint EllipsisCodepoint() => EllipsisCodepointValue;
    public override void SetSpacing(ETextSpacing type, float value) { if (Shaped.Spacing[(int)type] != value) { Shaped.Spacing[(int)type] = value; MarkDirty(false); } }
    public override float Spacing(ETextSpacing type) => Shaped.Spacing[(int)type];
    public override float TabAlign(ReadOnlySpan<float> stops)
    { foreach (float stop in stops) if (!float.IsFinite(stop) || stop <= 0) return 0; TabStops.Clear(); foreach (var stop in stops) TabStops.Add(stop); MarkLinesDirty(); return EnsureReady() ? TextAlgorithms.ShapedWidth(Shaped) : 0; }
    public override bool Shape() => EnsureReady();
    public override ReadOnlySpan<TextGlyph> Glyphs() => EnsureVisualGlyphs() ? CollectionsMarshal.AsSpan(_visualGlyphs) : [];
    public override ReadOnlySpan<TextGlyph> SortLogicalGlyphs() => EnsureLogicalGlyphs() ? CollectionsMarshal.AsSpan(_logicalGlyphs) : [];
    public override ReadOnlySpan<TextGlyph> EllipsisGlyphs() => EnsureEllipsisGlyphs() ? CollectionsMarshal.AsSpan(_ellipsisGlyphs) : [];
    public override Utf8StringView RunText(ulong index) => Run(index) is { } run ? SourceText(run.SourceRange) : default;
    public override TextRange RunSourceRange(ulong index) => Run(index)?.SourceRange ?? new();
    public override TextRange RunGlyphRange(ulong index) => Run(index)?.GlyphRange ?? new();
    public override FontFace? RunFontFace(ulong index) => Run(index) is { } run ? LiveServices()?.FontFace(run.FontFace) : null;
    public override float RunFontSize(ulong index) => Run(index)?.FontSize ?? 0;
    public override Utf8StringView RunLanguage(ulong index) => Run(index) is { } run ? run.Language : default(Utf8StringView);
    public override ETextDirection RunDirection(ulong index) => Run(index)?.Direction ?? ETextDirection.LTR;
    public override bool RunObject(ulong index, ref ulong out_key) { if (Run(index) is not { IsObject: true } run) return false; out_key = run.ObjectKey; return true; }
    public override TextLayoutSpanId RunSpanId(ulong index) => Run(index)?.SpanId ?? new();
    public override bool WordBreaks(ETextGraphemeFlag grapheme_flags, ETextGraphemeFlag skip_grapheme_flags, List<TextRange> out_ranges)
    {
        out_ranges.Clear(); var logical = SortLogicalGlyphs(); if (!EnsureReady()) return false; ulong wordStart = SourceRange().Start;
        for (int i = 0; i < logical.Length; i++) { var glyph = logical[i]; if (glyph.Count == 0 || (glyph.Flags & grapheme_flags) == 0 || (glyph.Flags & skip_grapheme_flags) != 0) continue; ulong next = i == 0 ? glyph.SourceRange.Start : logical[i - 1].SourceRange.End; if (wordStart < next) out_ranges.Add(new(wordStart, next)); wordStart = glyph.SourceRange.End; }
        if (wordStart < SourceRange().End) out_ranges.Add(new(wordStart, SourceRange().End)); return true;
    }
    public override void OverrunTrimToWidth(float width, ETextOverrunFlag flags)
    { MaxWidthValue = width; OverrunBehavior = TextAlgorithms.OverrunBehaviorFromFlags(flags); MarkLinesDirty(); EnsureReady(); }
    public override long PreviousGraphemePosition(ulong position)
    { if (!EnsureReady() || position == 0) return 0; ulong result = SourceRange().Start; foreach (var glyph in SortLogicalGlyphs()) { if (glyph.SourceRange.Start >= position) break; result = glyph.SourceRange.Start; } return (long)result; }
    public override long NextGraphemePosition(ulong position)
    { if (!EnsureReady()) return (long)position; foreach (var glyph in SortLogicalGlyphs()) if (glyph.SourceRange.End > position) return (long)glyph.SourceRange.End; return (long)SourceRange().End; }
    public override bool CharacterBreaks(List<ulong> out_breaks)
    { out_breaks.Clear(); if (!EnsureReady() || LiveServices() is not { } service) return false; service.CharacterBreaks(Shaped, out_breaks); return true; }
    public override long PreviousCharacterPosition(ulong position)
    { List<ulong> breaks = []; if (!CharacterBreaks(breaks)) return (long)(position > 0 ? position - 1 : 0); ulong result = SourceRange().Start; foreach (var value in breaks) { if (value >= position) break; result = value; } return (long)result; }
    public override long NextCharacterPosition(ulong position)
    { List<ulong> breaks = []; if (!CharacterBreaks(breaks)) return (long)Math.Min(SourceRange().End, position + 1); foreach (var value in breaks) if (value > position) return (long)value; return (long)SourceRange().End; }
    public override long ClosestCharacterPosition(ulong position)
    { List<ulong> breaks = []; if (!CharacterBreaks(breaks)) return (long)Math.Min(position, SourceRange().End); ulong previous = SourceRange().Start; foreach (var value in breaks) { if (value == position) return (long)value; if (value > position) return (long)(value - position < position - previous ? value : previous); previous = value; } return (long)previous; }
    public override ETextDirection RangeDirection(TextRange range) => EnsureReady() ? TextAlgorithms.LineRangeDirection(Shaped, range) : ETextDirection.LTR;
    private TextLayoutSpan? FindSpan(TextLayoutSpanId id) => id.IsValid() && id.Value < (ulong)Shaped.Spans.Count ? Shaped.Spans[(int)id.Value] : null;
    private TextShapedObjectData? FindObject(ulong key) { foreach (var obj in Shaped.Objects) if (obj.Key == key) return obj; return null; }
    [Conditional("DEBUG")] private static void SourceUnimplemented() => Debug.Fail("Unimplemented in SkrGui source at 611561f8.");
    public override bool ResizeObject(ulong key, Sizef size, ETextInlineAlignment inline_align = ETextInlineAlignment.Center, float baseline = 0)
    { if (LiveServices() is not { } service) return false; bool resized = service.ResizeShapedObject(Shaped, key, size, inline_align, baseline); if (resized) MarkLinesDirty(); return resized; }
    public override long ObjectGlyphIndex(ulong key)
    { if (!EnsureReady()) return -1; ulong offset = 0; foreach (var line in Lines) { for (int i = 0; i < line.Glyphs.Count; i++) if (line.Glyphs[i].ObjectKey == key) return (long)offset + i; offset += (ulong)line.Glyphs.Count; } return -1; }
    public override Rectf ObjectRect(ulong key)
    { if (!EnsureReady()) return new(); float frame = FrameWidth(); for (int i = 0; i < Lines.Count; i++) { var rect = TextAlgorithms.LineObjectRect(Lines[i], key, frame, AlignmentValue, true); if (rect != new Rectf()) return rect.Shift(new(0, LineTop((ulong)i))); } return new(); }
    public override ETextDirection InferredDirection() => EnsureReady() ? Shaped.InferredDirection : Shaped.Direction == ETextDirectionMode.RTL ? ETextDirection.RTL : ETextDirection.LTR;
    public override void SetMaxWidth(float value) { if (MaxWidthValue == value) return; MaxWidthValue = value; MarkLinesDirty(); }
    public override float MaxWidth() => MaxWidthValue;
    public override void SetMaxLinesVisible(int value) { if (MaxLinesVisibleValue == value) return; MaxLinesVisibleValue = value; MarkLinesDirty(); }
    public override int MaxLinesVisible() => MaxLinesVisibleValue;
    public override void SetLineSpacing(float value) { if (LineSpacingValue == value) return; LineSpacingValue = value; MarkLinesDirty(); }
    public override float LineSpacing() => LineSpacingValue;
    public override void SetBreakFlags(ETextLineBreakFlag value) { if (BreakFlagsValue == value) return; BreakFlagsValue = value; MarkLinesDirty(); }
    public override ETextLineBreakFlag BreakFlags() => BreakFlagsValue;
    public override bool IsReady()
    {
        var service = LiveServices(); if (service is null || LinesDirty || !Shaped.Valid) return false;
        if (Shaped.ObservedFontTopologyRevision != service.FontTopologyRevision()) return false;
        ulong revision = service.FontStateRevision(); if (Shaped.ObservedFontStateRevision == revision) return true;
        if (!service.FontDependenciesValid(Shaped.FontDependencies)) return false; Shaped.ObservedFontStateRevision = revision; return true;
    }
    internal ulong VisibleLineCount() => MaxLinesVisibleValue >= 0 ? Math.Min((ulong)Lines.Count, (ulong)MaxLinesVisibleValue) : (ulong)Lines.Count;
    private float FrameWidth()
    { if (MaxWidthValue > 0) return MaxWidthValue; float width = 0; for (ulong i = 0; i < VisibleLineCount(); i++) width = TextFontSupport.Max(width, TextAlgorithms.ShapedWidth(Lines[(int)i])); return width; }
    private float LineTop(ulong line)
    { float top = 0; for (ulong i = 0; i < Math.Min(line, (ulong)Lines.Count); i++) top += Lines[(int)i].LineHeight + LineSpacingValue; return top; }
    private ulong LineAtY(float y)
    { float top = 0; for (int i = 0; i < Lines.Count; i++) { float bottom = top + TextAlgorithms.ShapedSize(Lines[i]).Height; if (y >= top && y <= bottom) return (ulong)i; top = bottom + LineSpacingValue; } return ulong.MaxValue; }
    private ulong LineAtSource(ulong position)
    { for (int i = 0; i < Lines.Count; i++) { var range = Lines[i].SourceRange; if (range.Start <= position && position < range.End || i + 1 == Lines.Count && position == range.End) return (ulong)i; } return ulong.MaxValue; }
    public override ulong GlyphCount() { if (!EnsureReady()) return 0; ulong total = 0; foreach (var line in Lines) total += (ulong)line.Glyphs.Count; return total; }
    public override ulong VisibleCharacters()
    { if (!EnsureReady()) return 0; ulong total = 0; for (ulong i = 0; i < VisibleLineCount(); i++) total += TextLineImpl.VisibleSourceCount(Lines[(int)i]); return total; }
    private ulong GlobalTrimPosition(bool ellipsis)
    { if (!EnsureReady()) return ulong.MaxValue; ulong visible = VisibleLineCount(); if (visible == 0) return ulong.MaxValue; var trim = Lines[(int)visible - 1].Trim; ulong local = ellipsis ? trim.EllipsisPosition : trim.TrimPosition; if (local == ulong.MaxValue) return local; ulong prefix = 0; for (ulong i = 0; i + 1 < visible; i++) prefix += (ulong)Lines[(int)i].Glyphs.Count; return prefix + local; }
    public override ulong TrimPosition() => GlobalTrimPosition(false);
    public override ulong EllipsisPosition() => GlobalTrimPosition(true);
    public override ulong EllipsisGlyphCount() => (ulong)EllipsisGlyphs().Length;
    public override Sizef NonWrappedSize() => EnsureReady() ? TextAlgorithms.ShapedSize(Shaped) : Sizef.Zero();
    public override Sizef Size()
    { if (!EnsureReady()) return Sizef.Zero(); Sizef result = new(); ulong visible = VisibleLineCount(); for (ulong i = 0; i < visible; i++) { result.Width = TextFontSupport.Max(result.Width, TextAlgorithms.ShapedWidth(Lines[(int)i])); result.Height += TextAlgorithms.ShapedSize(Lines[(int)i]).Height; if (i + 1 < visible) result.Height += LineSpacingValue; } return result; }
    private TextRunData? Run(ulong index) => EnsureRuns() && index < (ulong)_runs.Count ? _runs[(int)index] : null;
    public override ulong RunCount() => EnsureRuns() ? (ulong)_runs.Count : 0;
    private TextShapedData? Line(ulong index) => index < (ulong)Lines.Count ? Lines[(int)index] : null;
    private TextShapedData? ReadyLine(ulong index) => EnsureReady() ? Line(index) : null;
    public override ulong LineCount() => EnsureReady() ? (ulong)Lines.Count : 0;
    public override Sizef LineSize(ulong line) => ReadyLine(line) is { } value ? TextAlgorithms.ShapedSize(value) : Sizef.Zero();
    public override float LineWidth(ulong line) => ReadyLine(line) is { } value ? TextAlgorithms.ShapedWidth(value) : 0;
    public override float LineAscent(ulong line) => ReadyLine(line)?.Ascent ?? 0;
    public override float LineDescent(ulong line) => ReadyLine(line)?.Descent ?? 0;
    public override TextRange LineRange(ulong line) => ReadyLine(line)?.SourceRange ?? new();
    public override TextRange LineGlyphRange(ulong line)
    { if (!EnsureReady() || line >= (ulong)Lines.Count) return new(); ulong begin = 0; for (ulong i = 0; i < line; i++) begin += (ulong)Lines[(int)i].Glyphs.Count; return new(begin, begin + (ulong)Lines[(int)line].Glyphs.Count); }
    public override bool LineObjects(ulong line, List<ulong> out_keys)
    { out_keys.Clear(); if (ReadyLine(line) is not { } value) return false; foreach (var obj in value.Objects) out_keys.Add(obj.Key); return true; }
    public override Rectf LineObjectRect(ulong line, ulong key)
    { var value = ReadyLine(line); float frame = FrameWidth(); if (value is null) return new(); var result = TextAlgorithms.LineObjectRect(value, key, frame, AlignmentValue, true); return result == new Rectf() ? new() : result.Shift(new(0, LineTop(line))); }
    public override float LineUnderlinePosition(ulong line) => ReadyLine(line)?.UnderlinePosition ?? 0;
    public override float LineUnderlineThickness(ulong line) => ReadyLine(line)?.UnderlineThickness ?? 0;
    public override bool LineBreaks(float width, ulong start, ETextLineBreakFlag flags, List<TextRange> out_ranges)
    { out_ranges.Clear(); return EnsureReady() && LiveServices() is { } service && service.LineBreaks(Shaped, width, start, flags, out_ranges); }
    public override bool LineBreaks(ReadOnlySpan<float> widths, ulong start, bool once, ETextLineBreakFlag flags, List<TextRange> out_ranges)
    { out_ranges.Clear(); SourceUnimplemented(); return false; }
    public override float FitToWidth(float width, ETextJustificationFlag flags = TextDefaults.JustificationFlags)
    { JustificationFlagsValue = flags; AlignmentValue = ETextHAlign.Fill; MaxWidthValue = width; MarkLinesDirty(); return EnsureReady() ? Size().Width : 0; }
    public override TextParagraph? Substring(TextRange range) { SourceUnimplemented(); return null; }
    public override long HitTestPosition(Offsetf coords)
    { if (!EnsureReady()) return 0; ulong line = LineAtY(coords.Y); return line == ulong.MaxValue ? (long)SourceRange().End : TextAlgorithms.LineHitTestPosition(Lines[(int)line], coords.X); }
    public override long HitTestGrapheme(Offsetf coords)
    { if (!EnsureReady()) return -1; ulong line = LineAtY(coords.Y); if (line == ulong.MaxValue) return -1; long local = TextAlgorithms.LineHitTestGrapheme(Lines[(int)line], coords.X); if (local < 0) return -1; ulong offset = 0; for (ulong i = 0; i < line; i++) offset += (ulong)Lines[(int)i].Glyphs.Count; return (long)offset + local; }
    public override TextCaretInfo Caret(ulong position)
    { if (!EnsureReady() || Lines.Count == 0) return new(); ulong line = LineAtSource(position); if (line == ulong.MaxValue) line = position < Lines[0].SourceRange.Start ? 0 : (ulong)Lines.Count - 1; var result = TextAlgorithms.LineCaret(Lines[(int)line], position); float top = LineTop(line); result.LeadingCaret = result.LeadingCaret.Shift(new(0, top)); result.TrailingCaret = result.TrailingCaret.Shift(new(0, top)); return result; }
    public override bool SelectionRects(TextRange range, List<Rectf> out_rects)
    { out_rects.Clear(); if (!EnsureReady()) return false; float top = 0; foreach (var line in Lines) { List<Rectf> local = []; TextAlgorithms.LineSelectionRects(line, range, local); foreach (var rect in local) out_rects.Add(rect.Shift(new(0, top))); top += line.LineHeight + LineSpacingValue; } return true; }
    public override TextFloatRange GraphemeBounds(ulong position)
    { if (!EnsureReady()) return new(); ulong line = LineAtSource(position); return line == ulong.MaxValue ? new() : TextAlgorithms.LineGraphemeBounds(Lines[(int)line], position); }
    public override void SetDropCap(Utf8StringView text, TextStyle style, uint lines, Rectf margins = default, Utf8StringView language = default) => SourceUnimplemented();
    public override void ClearDropCap() { }
    public override uint DropCapLines() => 0;
    public override Sizef DropCapSize() => new();
    public override ReadOnlySpan<TextGlyph> DropCapGlyphs() => [];
    public override bool PaintDropCap(Offsetf origin, TextRenderResult out_result, TextPaintDesc? desc = null) { out_result.Clear(); SourceUnimplemented(); return false; }
    public override bool Paint(Offsetf origin, TextRenderResult out_result, TextPaintDesc? desc = null)
    { out_result.Clear(); if (!EnsureReady() || LiveServices() is not { } service) return false; return service.PaintLines(Lines, VisibleLineCount(), FrameWidth(), AlignmentValue, true, MaxWidthValue > 0, LineSpacingValue, origin, out_result, desc ?? new()); }
    public override bool PaintLine(ulong line, Offsetf origin, TextRenderResult out_result, TextPaintDesc? desc = null)
    { out_result.Clear(); if (!EnsureReady() || LiveServices() is not { } service || line >= (ulong)Lines.Count) return false; return service.PaintLines(Lines, (ulong)Lines.Count, TextAlgorithms.ShapedWidth(Lines[(int)line]), ETextHAlign.Left, false, false, LineSpacingValue, origin, out_result, desc ?? new(), line); }
    internal void MarkDirty(bool sourceChanged = true) { Shaped.Invalidate(sourceChanged); MarkLinesDirty(); }
    internal void MarkLinesDirty() { LinesDirty = true; InvalidateLineProjections(); }
    private bool EnsureReady() => LiveServices() is { } service && (IsReady() || service.ShapeParagraph(this));
    private bool EnsureVisualGlyphs()
    { if (!EnsureReady()) return false; if (!_visualGlyphsValid) { _visualGlyphs.Clear(); foreach (var line in Lines) foreach (var glyph in line.Glyphs) _visualGlyphs.Add(TextAlgorithms.PublicGlyph(glyph)); _visualGlyphsValid = true; } return true; }
    private bool EnsureLogicalGlyphs()
    { if (!EnsureReady() || LiveServices() is not { } service) return false; if (!_logicalGlyphsValid) { _logicalGlyphs.Clear(); foreach (var line in Lines) foreach (var glyph in service.LogicalGlyphs(line)) _logicalGlyphs.Add(TextAlgorithms.PublicGlyph(glyph)); _logicalGlyphsValid = true; } return true; }
    private bool EnsureEllipsisGlyphs()
    { if (!EnsureReady()) return false; if (!_ellipsisGlyphsValid) { _ellipsisGlyphs.Clear(); for (ulong i = 0; i < VisibleLineCount(); i++) foreach (var glyph in Lines[(int)i].Trim.EllipsisGlyphs) _ellipsisGlyphs.Add(TextAlgorithms.PublicGlyph(glyph)); _ellipsisGlyphsValid = true; } return true; }
    private bool EnsureRuns()
    {
        if (!EnsureReady() || LiveServices() is not { } service) return false; if (_runsValid) return true;
        _runs.Clear(); ulong offset = 0; foreach (var line in Lines) { foreach (var item in service.Runs(line)) { var run = item with { Language = new Utf8StringView(item.Language.Bytes.ToArray()) }; run.GlyphRange = new(run.GlyphRange.Start + offset, run.GlyphRange.End + offset); _runs.Add(run); } offset += (ulong)line.Glyphs.Count; } _runsValid = true; return true;
    }
    internal void InvalidateLineProjections()
    { _visualGlyphsValid = _logicalGlyphsValid = _ellipsisGlyphsValid = _runsValid = false; _visualGlyphs.Clear(); _logicalGlyphs.Clear(); _ellipsisGlyphs.Clear(); _runs.Clear(); }
    private TextPlacedGlyph? Grapheme(ulong position)
    { foreach (var line in Lines) foreach (var glyph in line.Glyphs) if (position >= glyph.SourceBegin && position < glyph.SourceEnd) return glyph; return null; }
}

internal sealed partial class TextServicesImpl
{
    private static bool LineHasVisibleCharacters(TextShapedData line)
    { foreach (var glyph in line.Glyphs) if (glyph.Glyph.GlyphIndex != 0 && (glyph.Flags & ETextGraphemeFlag.Virtual) == 0) return true; return false; }
    internal bool ShapeParagraph(TextParagraphImpl paragraph)
    {
        ulong topology = FontTopologyRevision(), state = FontStateRevision();
        if (paragraph.Shaped.Valid && paragraph.Shaped.ObservedFontTopologyRevision != topology)
        { paragraph.Shaped.Invalidate(false); paragraph.MarkLinesDirty(); }
        else if (paragraph.Shaped.Valid && paragraph.Shaped.ObservedFontStateRevision != state && !FontDependenciesValid(paragraph.Shaped.FontDependencies))
        { paragraph.Shaped.Invalidate(false); paragraph.MarkLinesDirty(); }
        if (!ShapeText(paragraph.Shaped)) return false;
        if (!paragraph.LinesDirty) { paragraph.Shaped.ObservedFontStateRevision = state; return true; }
        foreach (var line in paragraph.Lines) line.Dispose(); paragraph.Lines.Clear();
        if (paragraph.TabStops.Count != 0) TabAlign(paragraph.Shaped, paragraph.TabStops);
        if (!UpdateBreaks(paragraph.Shaped)) return false;
        List<TextRange> ranges = [];
        if (!LineBreaks(paragraph.Shaped, paragraph.MaxWidthValue, paragraph.Shaped.SourceRange.Start, paragraph.BreakFlagsValue, ranges)) return false;
        if (ranges.Count == 0) ranges.Add(new(0, 0));
        foreach (var range in ranges)
        {
            TextShapedData line = new(); paragraph.Lines.Add(line);
            if (!ShapeSubstring(paragraph.Shaped, range, line, ETextShapedProjection.LineBreak)) { paragraph.Lines.RemoveAt(paragraph.Lines.Count - 1); line.Dispose(); return false; }
            if (paragraph.TabStops.Count != 0) TabAlign(line, paragraph.TabStops);
        }
        var overrun = TextAlgorithms.OverrunFlagsFromBehavior(paragraph.OverrunBehavior);
        bool autowrap = (paragraph.BreakFlagsValue & ETextLineBreakFlag.WordBound) != 0 || (paragraph.BreakFlagsValue & ETextLineBreakFlag.GraphemeBound) != 0;
        ulong visible = paragraph.VisibleLineCount(); bool hidden = visible > 0 && visible < (ulong)paragraph.Lines.Count;
        if (autowrap)
        {
            if (hidden) overrun |= ETextOverrunFlag.EnforceEllipsis;
            if (paragraph.AlignmentValue == ETextHAlign.Fill)
            {
                ulong justifyTo = visible;
                if (paragraph.Lines.Count == 1 && (paragraph.JustificationFlagsValue & ETextJustificationFlag.DoNotSkipSingleLine) != 0) justifyTo = (ulong)paragraph.Lines.Count;
                else
                {
                    if ((paragraph.JustificationFlagsValue & ETextJustificationFlag.SkipLastLine) != 0) justifyTo = visible > 0 ? visible - 1 : 0;
                    if ((paragraph.JustificationFlagsValue & ETextJustificationFlag.SkipLastLineWithVisibleChars) != 0)
                        for (ulong i = visible; i > 0; i--) if (LineHasVisibleCharacters(paragraph.Lines[(int)i - 1])) { justifyTo = i - 1; break; }
                }
                for (ulong i = 0; i < (ulong)paragraph.Lines.Count; i++)
                {
                    if (i < justifyTo) FitToWidth(paragraph.Lines[(int)i], paragraph.MaxWidthValue, paragraph.JustificationFlagsValue);
                    else if (visible > 0 && i == visible - 1) OverrunTrimToWidth(paragraph.Lines[(int)i], paragraph.MaxWidthValue, overrun, paragraph.EllipsisCodepointValue);
                }
            }
            else if (hidden) OverrunTrimToWidth(paragraph.Lines[(int)visible - 1], paragraph.MaxWidthValue, overrun, paragraph.EllipsisCodepointValue);
        }
        else
        {
            ulong justifyTo = (ulong)paragraph.Lines.Count;
            if (paragraph.Lines.Count != 1 || (paragraph.JustificationFlagsValue & ETextJustificationFlag.DoNotSkipSingleLine) == 0)
            {
                if ((paragraph.JustificationFlagsValue & ETextJustificationFlag.SkipLastLine) != 0 && justifyTo > 0) justifyTo--;
                if ((paragraph.JustificationFlagsValue & ETextJustificationFlag.SkipLastLineWithVisibleChars) != 0)
                    for (ulong i = (ulong)paragraph.Lines.Count; i > 0; i--) if (LineHasVisibleCharacters(paragraph.Lines[(int)i - 1])) { justifyTo = i - 1; break; }
            }
            for (ulong i = 0; i < (ulong)paragraph.Lines.Count; i++)
            {
                var line = paragraph.Lines[(int)i];
                if (i < justifyTo && paragraph.AlignmentValue == ETextHAlign.Fill)
                {
                    FitToWidth(line, paragraph.MaxWidthValue, paragraph.JustificationFlagsValue);
                    OverrunTrimToWidth(line, paragraph.MaxWidthValue, overrun | ETextOverrunFlag.JustificationAware, paragraph.EllipsisCodepointValue);
                    FitToWidth(line, paragraph.MaxWidthValue, paragraph.JustificationFlagsValue | ETextJustificationFlag.ConstrainEllipsis);
                }
                else OverrunTrimToWidth(line, paragraph.MaxWidthValue, overrun, paragraph.EllipsisCodepointValue);
            }
        }
        paragraph.LinesDirty = false; paragraph.Shaped.ObservedFontTopologyRevision = topology; paragraph.Shaped.ObservedFontStateRevision = state; return true;
    }
}
