// Source: SkrGuiCore/text/text_services.hpp @ 611561f8.
namespace SkrGui;

public abstract partial class TextServices
{
    public abstract Utf8StringView Name();
    public abstract Utf8StringView ShortName();
    public abstract ETextFeature Features();
    public abstract bool HasFeature(ETextFeature feature);
    public abstract void AddFontProvider(FontProvider provider);
    public abstract void ClearFontProviders();
    public abstract ulong FontProviderCount();
    public abstract FontProvider? FontProviderAt(ulong index);
    public abstract bool QueryFontFaces(TextStyle style, List<FontFaceQuery> out_queries);
    public abstract FontFace? PreloadFontFace(FontFaceQuery query);
    public abstract bool PreloadFontFaces(TextStyle style, List<FontFace> out_faces);
    public abstract ulong FontFaceCount();
    public abstract FontFace? FontFaceAt(ulong index);
    public abstract FontFace? FontFace(TextFontFaceId id);
    public abstract bool UnloadFontFace(TextFontFaceId id);
    public abstract void UnloadAllFontFaces();
    public abstract TextFontRasterConfig DefaultFontRasterConfig();
    public abstract void SetDefaultFontRasterConfig(TextFontRasterConfig config);
    public abstract TextFontRasterConfig FontRasterConfig(TextFontFaceId font);
    public abstract bool SetFontRasterConfig(TextFontFaceId font, TextFontRasterConfig config);
    public abstract bool ClearFontRasterConfig(TextFontFaceId font);
    public abstract void ClearGlyphCache();
    public abstract Sizei AtlasPageSize(ETextAtlasFormat format);
    public abstract void SetAtlasPageSize(ETextAtlasFormat format, Sizei size);
    public abstract ETextAtlasAllocationAlgorithm AtlasAllocationAlgorithm();
    public abstract bool SetAtlasAllocationAlgorithm(ETextAtlasAllocationAlgorithm algorithm);
    public abstract uint AtlasGlyphPadding();
    public abstract void SetAtlasGlyphPadding(uint padding);
    public abstract uint AtlasCount();
    public abstract TextAtlasView Atlas(uint atlas_index);
    public abstract void ClearAtlases();
    public abstract uint OpenTypeNameToTag(Utf8StringView name);
    public abstract Utf8StringView OpenTypeTagToName(uint tag);
    public abstract bool IsLocaleRightToLeft(Utf8StringView locale);
    public abstract void StringWordBreaks(Utf8StringView text, Utf8StringView language, ulong characters_per_line, List<TextRange> out_breaks);
    public abstract void StringCharacterBreaks(Utf8StringView text, Utf8StringView language, List<ulong> out_breaks);
    public abstract long IsConfusable(Utf8StringView text, ReadOnlySpan<Utf8StringView> dictionary);
    public abstract bool SpoofCheck(Utf8StringView text);
    public abstract Utf8StringView StripDiacritics(Utf8StringView text);
    public abstract bool IsValidIdentifier(Utf8StringView text);
    public abstract bool IsValidLetter(uint codepoint);
    public abstract Utf8StringView StringToUpper(Utf8StringView text, Utf8StringView language = default);
    public abstract Utf8StringView StringToLower(Utf8StringView text, Utf8StringView language = default);
    public abstract Utf8StringView StringToTitle(Utf8StringView text, Utf8StringView language = default);
    public abstract bool LoadSupportData(Utf8StringView path);
    public abstract Utf8StringView SupportDataFilename();
    public abstract Utf8StringView SupportDataInfo();
    public abstract bool SaveSupportData(Utf8StringView path);
    public abstract ReadOnlySpan<byte> SupportData();
    public abstract bool IsLocaleUsingSupportData(Utf8StringView locale);
    public abstract void ParseStructuredText(ETextStructuredTextParser parser, ReadOnlySpan<Utf8StringView> arguments, Utf8StringView text, List<TextBidiOverride> out_overrides);
    public abstract TextParagraph CreateParagraph();
    public abstract TextLine CreateLine();
}
