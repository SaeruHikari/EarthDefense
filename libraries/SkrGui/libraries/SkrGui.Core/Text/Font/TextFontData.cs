using System.Runtime.InteropServices;
namespace SkrGui;

// Source: text/text_private.hpp font data, asset and size-cache records.
internal sealed class TextFontData : IDisposable
{
    internal FontProvider? Provider;
    internal Utf8StringView SourceKey;
    internal byte[]? Bytes;
    private GCHandle _bytesHandle;
    internal nint BytesPointer => _bytesHandle.IsAllocated ? _bytesHandle.AddrOfPinnedObject() : 0;
    internal void SetBytes(byte[] bytes) { Dispose(); Bytes = bytes; if (bytes.Length != 0) _bytesHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned); }
    public void Dispose() { if (_bytesHandle.IsAllocated) _bytesHandle.Free(); Bytes = null; }
}
internal sealed class TextFontAsset
{
    internal TextFontData? Data;
    internal uint FaceIndex;
    internal bool FaceLoadAttempted,FaceLoadable,MetadataReady;
    internal Utf8StringView Family, StyleName;
    internal readonly List<FontOpenTypeName> Names = [];
    internal readonly List<FontVariationAxis> Axes = [];
    internal readonly List<FontOpenTypeFeature> Features = [];
    internal readonly List<Utf8StringView> PaletteNames = [];
    internal readonly List<List<SRGBColor>> Palettes = [];
}
internal sealed class TextFontCachedGlyph
{
    internal TextGlyphMetrics Metrics;
    internal Offsetf? AdvanceOverride,RasterOffsetOverride;
    internal Sizef? RasterSizeOverride;
    internal enum ELoadState : byte { Unloaded,Loaded,Missing }
    internal ELoadState LoadState;
    internal bool HasOverride() => AdvanceOverride.HasValue || RasterOffsetOverride.HasValue || RasterSizeOverride.HasValue;
}
internal readonly record struct TextRenderedGlyphKey(uint GlyphIndex,ETextSubpixelPositioning SubpixelPositioning,byte SubpixelPhase);
internal sealed class TextRenderedGlyph
{
    internal float FontSize;
    internal Offsetf Bearing;
    internal Sizef BitmapSize;
    internal uint AtlasIndex = uint.MaxValue;
    internal Rectf AtlasUv;
    internal ETextPixelMode PixelMode = ETextPixelMode.Gray;
    internal ETextSubpixelPositioning SubpixelPositioning = ETextSubpixelPositioning.Disabled;
}
internal sealed class TextFontSizeCache : IDisposable
{
    internal nint FtSize,ShapingFont;
    internal bool ColorPaint;
    internal uint Size26_6;
    internal float Scale = 1;
    internal TextMetrics Metrics;
    internal readonly Dictionary<uint,TextFontCachedGlyph> Glyphs = [];
    internal readonly Dictionary<ulong,Offsetf> KerningOverrides = [];
    internal readonly Dictionary<TextRenderedGlyphKey,TextRenderedGlyph> RenderedGlyphs = [];
    internal float FontSize() => Size26_6 / 64f;
    internal float RasterFontSize() => Scale > 0 ? FontSize() / Scale : 0;
    public void Dispose()
    {
        if (ShapingFont != 0) { TextNative.hb_font_destroy(ShapingFont); ShapingFont=0; }
        if (FtSize != 0) { TextNative.FT_Done_Size(FtSize); FtSize=0; }
    }
}
internal struct TextLayoutFontSize
{
    internal TextFontSizeCache? Cache;
    internal uint RequestedSize26_6;
    internal bool UsesSdfSource;
    internal bool IsValid() => Cache != null && RequestedSize26_6 > 0 && Cache.Size26_6 > 0 && Cache.Scale > 0;
    internal float RequestedFontSize() => RequestedSize26_6 / 64f;
    internal float SourceToRequestedScale() => IsValid() && UsesSdfSource ? (float)RequestedSize26_6 / Cache!.Size26_6 : 1;
    internal float ShapingScale() => IsValid() ? Cache!.Scale * SourceToRequestedScale() : 1;
    internal bool PreservesHorizontalSubpixel(in TextFontRasterConfig config)
    {
        if (ShapingScale() != 1) return true;
        return config.SubpixelPositioning switch {
            ETextSubpixelPositioning.Half or ETextSubpixelPositioning.Quarter => true,
            ETextSubpixelPositioning.Auto => RequestedSize26_6 <= TextFontRasterConfig.KHalfMaxPpem * 64u, _ => false };
    }
    internal bool PreservesDirectGlyphAdvance(in TextFontRasterConfig config) => UsesSdfSource || PreservesHorizontalSubpixel(config);
}
internal readonly record struct TextNamedSupportOverride(Utf8StringView Name,bool Supported);
