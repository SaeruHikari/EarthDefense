// Source: SkrGuiCore/text/text_types.hpp @ 611561f81534354c8c1618dc27c4782cd9277cbf.
// Source positions are Unicode scalar indices, not UTF-16 code-unit positions.
namespace SkrGui;

public readonly record struct TextFontFaceId(ulong Value)
{
    public bool IsValid() => Value != 0;
    public static explicit operator bool(TextFontFaceId value) => value.IsValid();
}
public readonly record struct TextLayoutSpanId
{
    // CLR zero initialization must represent the source all-ones invalid ID.
    private readonly ulong _encodedValue;
    public ulong Value => unchecked(_encodedValue - 1);
    public TextLayoutSpanId() => _encodedValue = 0;
    public TextLayoutSpanId(ulong value) => _encodedValue = unchecked(value + 1);
    public bool IsValid() => Value != ulong.MaxValue;
    public static explicit operator bool(TextLayoutSpanId value) => value.IsValid();
}
public readonly record struct TextRange(ulong Start, ulong End)
{
    public ulong Length() => End > Start ? End - Start : 0;
    public bool IsEmpty() => Start >= End;
    public bool Contains(ulong position) => Start <= position && position < End;
}
public readonly record struct TextFloatRange(float Start, float End)
{
    public bool Equals(TextFloatRange other) => Start == other.Start && End == other.End;
    public override int GetHashCode() => HashCode.Combine(Start, End);
    public float Length() => End > Start ? End - Start : 0;
    public bool IsEmpty() => Start >= End;
}
public record struct TextFontRasterConfig
{
    public const uint KLowPpem = 32, KMediumPpem = 48, KHighPpem = 64;
    public const uint KQuarterMaxPpem = 16, KHalfMaxPpem = 20;
    public ETextRasterMode Mode = ETextRasterMode.Sdf;
    public bool LcdMode;
    public ETextHinting Hinting = ETextHinting.Light;
    public bool DisableEmbeddedBitmaps = true;
    public uint SdfPpem = KLowPpem, SdfSpread = 4;
    public ETextSubpixelPositioning SubpixelPositioning = ETextSubpixelPositioning.Auto;
    public bool KeepRoundingRemainders = true;
    public TextFontRasterConfig() { }
}
public readonly record struct FontOpenTypeFeatureValue(uint Tag, uint Value);
public record struct FontOpenTypeFeature
{
    public uint Tag, DefaultValue;
    public Utf8StringView Name;
    public bool IsHidden;
    public FontOpenTypeFeature() { }
}
public record struct FontOpenTypeName
{
    public ushort Id;
    public Utf8StringView Language, Value;
    public FontOpenTypeName() { }
}
public record struct FontVariationAxis
{
    public uint Tag;
    public Utf8StringView Name;
    public float MinValue, MaxValue, DefaultValue;
    public FontVariationAxis() { }
}
public readonly record struct FontVariationCoordinate(uint Tag, float Value);
public record struct FontFaceMetrics
{
    public float Ascent, Descent, LineGap, UnderlinePosition, UnderlineThickness;
    public float Scale = 1;
    public FontFaceMetrics() { }
}
public sealed class TextStyle
{
    public List<TextFontFaceId> FontFaces = [];
    private Utf8StringView _fontFamilies;
    public Utf8StringView FontFamilies { get=>_fontFamilies; set=>_fontFamilies=new(value.Bytes.ToArray()); }
    public List<FontOpenTypeFeatureValue> OpenTypeFeatures = [];
    public float FontSize = 14;
    public EFontWeight FontWeight = EFontWeight.Regular;
    public EFontStyle FontStyle = EFontStyle.Normal;
    public EFontStretch FontStretch = EFontStretch.Normal;
    internal TextStyle Copy() => new() { FontFaces = [..FontFaces], FontFamilies = FontFamilies,
        OpenTypeFeatures = [..OpenTypeFeatures], FontSize = FontSize, FontWeight = FontWeight,
        FontStyle = FontStyle, FontStretch = FontStretch };
}
public record struct TextBidiOverride(TextRange Range, ETextDirection Direction)
{
    public TextBidiOverride() : this(default, ETextDirection.LTR) { }
}
public record struct TextCaretInfo
{
    public Rectf LeadingCaret, TrailingCaret;
    public ETextDirection LeadingDirection = ETextDirection.LTR, TrailingDirection = ETextDirection.LTR;
    public TextCaretInfo() { }
}
public record struct TextGlyph
{
    public TextRange SourceRange;
    public TextLayoutSpanId SpanId = new();
    public TextFontFaceId FontFace;
    public Offsetf Offset;
    public float Advance, FontSize;
    public uint GlyphIndex;
    public uint Count = 1, Repeat = 1;
    public ETextGraphemeFlag Flags;
    public ETextGlyphKind Kind = ETextGlyphKind.Glyph;
    public ulong ObjectKey;
    public TextGlyph() { }
    public bool IsVisible() => (Kind == ETextGlyphKind.HexBox || Kind == ETextGlyphKind.Glyph) && GlyphIndex != 0;
}
public record struct TextAtlasView
{
    public uint AtlasIndex = uint.MaxValue;
    public uint Width, Height, Stride;
    public ReadOnlyMemory<byte> Pixels;
    public ulong Generation;
    public ETextAtlasFormat Format = ETextAtlasFormat.L8;
    public TextAtlasView() { }
}
public record struct TextPaintDesc
{
    public float PixelRatio = 1;
    public Offsetf? DeviceOffset = Offsetf.Zero();
    public float ClipL = -1, ClipR = -1;
    public TextPaintDesc() { }
}
public readonly record struct TextRenderRect(TextLayoutSpanId SpanId, Rectf Rect, Rectf Uv);
public record struct TextRenderCommand
{
    public uint AtlasIndex = uint.MaxValue;
    public ETextPixelMode PixelMode = ETextPixelMode.Sdf;
    public Rectf ClipRect = Rectf.Largest();
    public ulong RectCount, RectOffset;
    public TextRenderCommand() { }
    public bool IsHexBoxFallback() => AtlasIndex == uint.MaxValue;
}
public sealed class TextRenderResult
{
    public List<TextRenderCommand> Commands = [];
    public List<TextRenderRect> Rects = [];
    public Rectf Bounds;
    public void Clear() { Commands.Clear(); Rects.Clear(); Bounds = default; }
    public bool IsEmpty() => Commands.Count == 0 || Rects.Count == 0;
    public void BuildAppendBox(TextLayoutSpanId spanId, Rectf rect)
    {
        if (rect.IsEmpty()) return;
        BuildAppendRect(uint.MaxValue, ETextPixelMode.Gray, Rectf.Largest(), spanId, rect, Rectf.Zero());
    }
    public void BuildAppendRect(uint atlasIndex, ETextPixelMode pixelMode, Rectf clipRect, in TextRenderRect rect)
    {
        if (rect.Rect.IsEmpty()) return;
        ulong offset = (ulong)Rects.Count;
        Rects.Add(rect);
        Bounds = Rects.Count == 1 ? rect.Rect : Bounds.Unite(rect.Rect);
        if (Commands.Count != 0)
        {
            var last = Commands[^1];
            if (last.AtlasIndex == atlasIndex && last.PixelMode == pixelMode && last.ClipRect == clipRect && last.RectOffset + last.RectCount == offset)
            { last.RectCount++; Commands[^1] = last; return; }
        }
        Commands.Add(new() { AtlasIndex = atlasIndex, PixelMode = pixelMode, ClipRect = clipRect, RectCount = 1, RectOffset = offset });
    }
    public void BuildAppendRect(uint atlasIndex, ETextPixelMode pixelMode, Rectf clipRect, TextLayoutSpanId spanId, Rectf rect, Rectf uv)
        => BuildAppendRect(atlasIndex, pixelMode, clipRect, new TextRenderRect(spanId, rect, uv));
}
public static class TextDefaults
{
    public const uint EllipsisCodepoint = 0x2026;
    public const ETextLineBreakFlag LineBreakFlags = ETextLineBreakFlag.Mandatory | ETextLineBreakFlag.WordBound;
    public const ETextJustificationFlag JustificationFlags = ETextJustificationFlag.WordBound | ETextJustificationFlag.Kashida | ETextJustificationFlag.SkipLastLine | ETextJustificationFlag.DoNotSkipSingleLine;
}
