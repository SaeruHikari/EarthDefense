using System.Runtime.InteropServices;
namespace SkrGui;

// Standard C ABI only. No C++ GUI code is called by this binding.
internal static unsafe partial class TextNative
{
    private const string Library = "skrgui_native";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_buffer_destroy(nint buffer);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ubidi_close_72")] internal static extern void ubidi_close(nint bidi);
}
internal sealed class TextUtf8Buffer
{
    private byte[] _storage = [];
    public int Count { get; private set; }
    public ReadOnlySpan<byte> Span => _storage.AsSpan(0, Count);
    public Utf8StringView View => new(_storage.AsMemory(0, Count));
    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (_storage.Length < Count + bytes.Length) Array.Resize(ref _storage, Math.Max(Count + bytes.Length, Math.Max(16, _storage.Length * 2)));
        bytes.CopyTo(_storage.AsSpan(Count)); Count += bytes.Length;
    }
    public void Clear() => Count = 0;
    public TextUtf8Buffer Copy() { var result = new TextUtf8Buffer(); result.Append(Span); return result; }
}
internal sealed class TextLayoutSpan
{
    public TextLayoutSpanId Id = new();
    public ulong Start, End;
    public TextStyle Style = new();
    public Utf8StringView Language;
    public TextLayoutSpan Copy() => new() { Id = Id, Start = Start, End = End, Style = Style.Copy(), Language = new Utf8StringView(Language.Bytes.ToArray()) };
}
internal record struct TextMetrics
{
    public Sizef Size;
    public Rectf Bounds;
    public float Ascent, Descent, LineGap, LineHeight, UnderlinePosition, UnderlineThickness;
    public ulong LineCount, GlyphCount;
    public bool IsTruncated;
}
internal record struct TextGlyphMetrics
{
    public TextFontFaceId Font;
    public uint Codepoint, GlyphIndex;
    public ETextGlyphKind Kind = ETextGlyphKind.Glyph;
    public float FontSize;
    public uint SourceSize26_6;
    public Offsetf Advance, Bearing;
    public Sizef BitmapSize;
    public TextGlyphMetrics() { }
    public bool IsVisible() => Kind == ETextGlyphKind.HexBox ? GlyphIndex != 0 && BitmapSize.Width > 0 && BitmapSize.Height > 0 : Kind == ETextGlyphKind.Glyph && GlyphIndex != 0;
}
internal sealed record TextPlacedGlyph
{
    public TextGlyphMetrics Glyph = new();
    public TextLayoutSpanId SpanId = new();
    public float XOff, YOff, Advance;
    public uint Repeat = 1, ClusterCount = 1;
    public ETextGraphemeFlag Flags;
    public ulong SourceBegin, SourceEnd, ObjectKey;
    public TextPlacedGlyph Copy() => this with { };
}
internal sealed class TextResolvedSpan
{
    internal sealed class FontCandidate { public TextFontFaceId Font; public Utf8StringView Family; public bool Resolved; }
    public List<FontCandidate> Fonts = [];
}
internal readonly record struct TextFontDependency(TextFontFaceId Font, ulong Revision);
internal sealed record TextRunData
{
    public TextRange SourceRange, GlyphRange;
    public TextLayoutSpanId SpanId = new();
    public TextFontFaceId FontFace;
    public Utf8StringView Language;
    public float FontSize;
    public ETextDirection Direction = ETextDirection.LTR;
    public ulong ObjectKey;
    public bool IsObject;
}
internal sealed class TextAdvancedSourceCache : IDisposable
{
    public List<ushort> Utf16 = [];
    public List<int> SourceToUtf16 = [];
    public List<byte> GraphemeBoundaries = [];
    public List<uint> Scripts = [];
    public List<nint> BidiIterators = [];
    public Dictionary<ulong, bool> Breaks = [];
    public List<ulong> Characters = [];
    public ulong BreakInserts;
    private GCHandle _utf16Pin;
    private ushort[]? _pinnedUtf16;
    internal unsafe ushort* PinnedUtf16()
    {
        if (!_utf16Pin.IsAllocated) { _pinnedUtf16 = Utf16.ToArray(); _utf16Pin = GCHandle.Alloc(_pinnedUtf16, GCHandleType.Pinned); }
        return (ushort*)_utf16Pin.AddrOfPinnedObject();
    }
    private void ReleaseUtf16Pin() { if (_utf16Pin.IsAllocated) _utf16Pin.Free(); _pinnedUtf16 = null; }
    public nint HbBuffer;
    public byte ParagraphLevel;
    public bool BreakOpsValid, CharsValid, ScriptsValid;
    public void InvalidateShape()
    {
        Utf16.Clear(); SourceToUtf16.Clear();
        foreach (var bidi in BidiIterators) if (bidi != 0) TextNative.ubidi_close(bidi);
        BidiIterators.Clear(); ReleaseUtf16Pin(); ParagraphLevel = 0;
    }
    public void InvalidateSource()
    {
        GraphemeBoundaries.Clear(); Scripts.Clear(); Breaks.Clear(); Characters.Clear(); BreakInserts = 0;
        BreakOpsValid = CharsValid = ScriptsValid = false;
    }
    public void Dispose()
    {
        foreach (var bidi in BidiIterators) if (bidi != 0) TextNative.ubidi_close(bidi);
        BidiIterators.Clear(); ReleaseUtf16Pin();
        if (HbBuffer != 0) { TextNative.hb_buffer_destroy(HbBuffer); HbBuffer = 0; }
        GC.SuppressFinalize(this);
    }
    ~TextAdvancedSourceCache() { Dispose(); }
}
internal sealed class TextShapedTrimData
{
    public ulong TrimPosition = ulong.MaxValue, EllipsisPosition = ulong.MaxValue;
    public List<TextPlacedGlyph> EllipsisGlyphs = [];
    public void Clear() { TrimPosition = EllipsisPosition = ulong.MaxValue; EllipsisGlyphs.Clear(); }
}
internal sealed record TextShapedObjectData
{
    public ulong Key, Start, End;
    public TextLayoutSpanId SpanId = new();
    public Sizef Size;
    public ETextInlineAlignment InlineAlign = ETextInlineAlignment.Center;
    public float Baseline;
    public Rectf Rect;
}
internal sealed class TextShapedData : IDisposable
{
    public TextShapedData? Parent;
    public TextUtf8Buffer Text = new();
    public List<uint> Codepoints = [];
    public List<ulong> ByteOffsets = [];
    public List<TextLayoutSpan> Spans = [];
    public List<TextBidiOverride> BidiOverrides = [];
    public Utf8StringView CustomPunctuation;
    public TextRange SourceRange;
    public List<TextPlacedGlyph> Glyphs = [], LogicalGlyphs = [];
    public List<TextResolvedSpan> ResolvedSpans = [];
    public List<TextFontDependency> FontDependencies = [];
    public List<TextRunData> Runs = [];
    public List<TextShapedObjectData> Objects = [];
    public TextShapedTrimData Trim = new();
    public TextAdvancedSourceCache Advanced = new();
    public ETextDirectionMode Direction = ETextDirectionMode.Auto;
    public ETextOrientation Orientation = ETextOrientation.Horizontal;
    public ETextDirection InferredDirection = ETextDirection.LTR;
    public bool PreserveInvalid = true, PreserveControl;
    public float[] Spacing = new float[4];
    public float Ascent, Descent, LineGap, LineHeight, Width, WidthTrimmed, UnderlinePosition, UnderlineThickness;
    public ulong ObservedFontTopologyRevision, ObservedFontStateRevision;
    public bool Valid, LineBreaksValid, JustificationOpsValid, SortValid, RunsDirty = true, TextTrimmed, UseTrimmedWidth, FitWidthMinimumReached;
    public TextShapedData Root() => Parent ?? this;
    public void Invalidate(bool sourceChanged)
    {
        Glyphs.Clear(); LogicalGlyphs.Clear(); ResolvedSpans.Clear(); FontDependencies.Clear(); Runs.Clear();
        foreach (var obj in Objects) obj.Rect = default;
        Trim.Clear(); InferredDirection = ETextDirection.LTR;
        Ascent = Descent = LineGap = LineHeight = Width = WidthTrimmed = UnderlinePosition = UnderlineThickness = 0;
        ObservedFontTopologyRevision = ObservedFontStateRevision = 0;
        Valid = LineBreaksValid = JustificationOpsValid = SortValid = false; RunsDirty = true; TextTrimmed = FitWidthMinimumReached = false;
        Advanced.InvalidateShape(); if (sourceChanged) Advanced.InvalidateSource();
    }
    public void Clear()
    {
        Invalidate(true); Parent = null; Text.Clear(); Codepoints.Clear(); ByteOffsets.Clear(); Spans.Clear(); Objects.Clear(); BidiOverrides.Clear();
        CustomPunctuation = default; SourceRange = default; Direction = ETextDirectionMode.Auto; Orientation = ETextOrientation.Horizontal;
        PreserveInvalid = true; PreserveControl = false; Array.Clear(Spacing);
    }
    public void Dispose() { Advanced.Dispose(); GC.SuppressFinalize(this); }
}
internal static partial class TextAlgorithms
{
    public static ulong SourceByteOffset(TextUtf8Buffer text, List<ulong> byteOffsets, ulong source) => source < (ulong)byteOffsets.Count ? byteOffsets[(int)source] : (ulong)text.Count;
    public static TextGlyph PublicGlyph(TextPlacedGlyph glyph) => new()
    {
        SourceRange = new(glyph.SourceBegin, glyph.SourceEnd), SpanId = glyph.SpanId, FontFace = glyph.Glyph.Font,
        Offset = new(glyph.XOff, glyph.YOff), Advance = glyph.Advance, FontSize = glyph.Glyph.FontSize, GlyphIndex = glyph.Glyph.GlyphIndex,
        Count = glyph.ClusterCount, Repeat = glyph.Repeat, Flags = glyph.Flags, Kind = glyph.Glyph.Kind, ObjectKey = glyph.ObjectKey
    };
    public static TextGlyphMetrics MissingGlyphMetrics(uint codepoint, float fontSize)
    {
        uint groups = codepoint <= 0xff ? 1u : codepoint <= 0xffff ? 2u : 3u;
        float scale = MathF.Max(1, MathF.Round(fontSize / 15f, MidpointRounding.AwayFromZero));
        float width = (4 + 3 * groups + (groups - 1) + 1) * scale, height = 15 * scale;
        return new() { Codepoint = codepoint, GlyphIndex = codepoint, Kind = ETextGlyphKind.HexBox, FontSize = fontSize,
            Advance = new(width, 0), Bearing = new(0, height * .85f), BitmapSize = new(width, height) };
    }
    public static TextGlyphMetrics ZeroWidthGlyphMetrics(in TextGlyphMetrics glyph, uint codepoint, float fontSize)
    {
        if (glyph.Kind == ETextGlyphKind.HexBox) { var result = MissingGlyphMetrics(0, fontSize); result.Advance = default; return result; }
        return new() { Font = glyph.Font, Codepoint = codepoint, FontSize = fontSize };
    }
}
