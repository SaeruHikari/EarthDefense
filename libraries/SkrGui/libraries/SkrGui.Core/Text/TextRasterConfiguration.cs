using System.Text;
namespace SkrGui;

// Source: text/render/text_glyph_raster.cpp:704-735,844-1050;
// text_render_service.cpp:136-192; text_font_registry.cpp:691-715 @ 611561f8.
internal readonly record struct TextRasterConfigTransition(bool ClearsSizeCache, bool InvalidatesLayout);
internal static partial class TextAlgorithms
{
    internal static uint SanitizeSdfPpem(uint value) { value = Math.Clamp(value, 8u, 256u); uint remainder = value % 4; return remainder == 0 ? value : value + 4 - remainder; }
    internal static uint SanitizeSdfSpread(uint value) => value == 0 ? 4u : Math.Clamp(value, 2u, 32u);
    internal static TextFontRasterConfig SanitizeRasterConfig(TextFontRasterConfig config)
    {
        if (config.Mode != ETextRasterMode.Bitmap && config.Mode != ETextRasterMode.Sdf) config.Mode = ETextRasterMode.Sdf;
        if (config.Hinting != ETextHinting.None && config.Hinting != ETextHinting.Light && config.Hinting != ETextHinting.Normal) config.Hinting = ETextHinting.Light;
        if (config.SubpixelPositioning != ETextSubpixelPositioning.Disabled && config.SubpixelPositioning != ETextSubpixelPositioning.Auto && config.SubpixelPositioning != ETextSubpixelPositioning.Half && config.SubpixelPositioning != ETextSubpixelPositioning.Quarter) config.SubpixelPositioning = ETextSubpixelPositioning.Auto;
        config.SdfPpem = SanitizeSdfPpem(config.SdfPpem); config.SdfSpread = SanitizeSdfSpread(config.SdfSpread); return config;
    }
    internal static TextRasterConfigTransition RasterConfigTransition(in TextFontRasterConfig previous, in TextFontRasterConfig next)
    {
        bool clears = previous.Mode != next.Mode || previous.LcdMode != next.LcdMode || previous.Hinting != next.Hinting || previous.DisableEmbeddedBitmaps != next.DisableEmbeddedBitmaps || previous.SdfPpem != next.SdfPpem || previous.SdfSpread != next.SdfSpread;
        return new(clears, clears || previous.SubpixelPositioning != next.SubpixelPositioning || previous.KeepRoundingRemainders != next.KeepRoundingRemainders);
    }
}
internal sealed partial class TextServicesImpl
{
    public override TextFontRasterConfig DefaultFontRasterConfig() => DefaultFontRasterConfigValue;
    public override void SetDefaultFontRasterConfig(TextFontRasterConfig config)
    {
        var canonical = TextAlgorithms.SanitizeRasterConfig(config); if (DefaultFontRasterConfigValue == canonical) return;
        var previous = DefaultFontRasterConfigValue; DefaultFontRasterConfigValue = canonical;
        var transition = TextAlgorithms.RasterConfigTransition(previous, canonical); bool affected = false, cleared = false;
        foreach (var face in FontFaces)
        {
            if (face.RasterConfigOverridden) continue;
            if (transition.ClearsSizeCache) { face.ClearSizeCaches(); cleared = true; }
            if (transition.InvalidatesLayout) { face.Revision++; affected = true; }
        }
        if (affected) FontStateRevisionValue++; if (cleared) ClearAtlases();
    }
    public override TextFontRasterConfig FontRasterConfig(TextFontFaceId font) => FindFontFace(font) is { } face ? EffectiveFontRasterConfig(face) : DefaultFontRasterConfigValue;
    public override bool SetFontRasterConfig(TextFontFaceId font, TextFontRasterConfig config)
    {
        var face = FindFontFace(font); if (face is null) return false; var canonical = TextAlgorithms.SanitizeRasterConfig(config);
        if (face.RasterConfigOverridden && face.RasterConfig == canonical) return true;
        var previous = EffectiveFontRasterConfig(face); face.RasterConfig = canonical; face.RasterConfigOverridden = true;
        if (previous != canonical)
        {
            var transition = TextAlgorithms.RasterConfigTransition(previous, canonical);
            if (transition.ClearsSizeCache) { face.ClearSizeCaches(); ClearAtlases(); }
            if (transition.InvalidatesLayout) { face.Revision++; FontStateRevisionValue++; }
        }
        return true;
    }
    public override bool ClearFontRasterConfig(TextFontFaceId font)
    {
        var face = FindFontFace(font); if (face is null) return false;
        if (face.RasterConfigOverridden)
        {
            var previous = face.RasterConfig; face.RasterConfigOverridden = false;
            if (previous != DefaultFontRasterConfigValue)
            {
                var transition = TextAlgorithms.RasterConfigTransition(previous, DefaultFontRasterConfigValue);
                if (transition.ClearsSizeCache) { face.ClearSizeCaches(); ClearAtlases(); }
                if (transition.InvalidatesLayout) { face.Revision++; FontStateRevisionValue++; }
            }
        }
        return true;
    }
    public override void ClearGlyphCache() { foreach (var face in FontFaces) ClearFontGlyphCache(face); }
    internal void ClearFontGlyphCache(TextFontFaceImpl font) { foreach (var entry in font.SizeCaches.Values) entry.RenderedGlyphs.Clear(); }
    internal TextFontRasterConfig EffectiveFontRasterConfig(TextFontFaceImpl font) => font.RasterConfigOverridden ? font.RasterConfig : DefaultFontRasterConfigValue;
    public override Sizei AtlasPageSize(ETextAtlasFormat format) => AtlasManager.AtlasPageSize(format);
    public override void SetAtlasPageSize(ETextAtlasFormat format, Sizei size) { if (AtlasManager.SetAtlasPageSize(format, size)) ClearAtlases(); }
    public override ETextAtlasAllocationAlgorithm AtlasAllocationAlgorithm() => AtlasManager.AtlasAllocationAlgorithm();
    public override bool SetAtlasAllocationAlgorithm(ETextAtlasAllocationAlgorithm algorithm) => AtlasManager.SetAtlasAllocationAlgorithm(algorithm);
    public override uint AtlasGlyphPadding() => AtlasManager.AtlasGlyphPadding();
    public override void SetAtlasGlyphPadding(uint padding) { if (AtlasManager.SetAtlasGlyphPadding(padding)) ClearAtlases(); }
    public override uint AtlasCount() => AtlasManager.AtlasCount();
    public override TextAtlasView Atlas(uint atlas_index) => AtlasManager.Atlas(atlas_index);
    public override void ClearAtlases() { AtlasManager.Clear(); ClearGlyphCache(); }
    public override uint OpenTypeNameToTag(Utf8StringView name)
    { if (name.Size() != 4) return 0; var data = name.Bytes; return (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | data[3]; }
    public override Utf8StringView OpenTypeTagToName(uint tag) => new(new byte[] { (byte)(tag >> 24), (byte)(tag >> 16), (byte)(tag >> 8), (byte)tag });
}
