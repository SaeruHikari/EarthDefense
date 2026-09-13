using System.Diagnostics;
using System.Runtime.InteropServices;
namespace SkrGui;

[StructLayout(LayoutKind.Sequential)] internal struct hb_raster_extents_t { public int XOrigin, YOrigin; public uint Width, Height, Stride; }
internal static unsafe partial class TextNative
{
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int hb_raster_image_get_format(nint image);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_image_get_extents(nint image, out hb_raster_extents_t extents);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern byte* hb_raster_image_get_buffer(nint image);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_paint_recycle_image(nint paint, nint image);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_draw_recycle_image(nint draw, nint image);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_paint_reset(nint paint);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_draw_reset(nint draw);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_paint_set_scale_factor(nint paint, float x, float y);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_draw_set_scale_factor(nint draw, float x, float y);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_paint_set_transform(nint paint, float xx, float yx, float xy, float yy, float dx, float dy);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_draw_set_transform(nint draw, float xx, float yx, float xy, float yy, float dx, float dy);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_paint_clear_custom_palette_colors(nint paint);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_paint_set_palette(nint paint, uint palette);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int hb_raster_paint_set_custom_palette_color(nint paint, uint index, uint color);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int hb_raster_paint_glyph_or_fail(nint paint, nint font, uint glyph);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void hb_raster_draw_glyph(nint draw, nint font, uint glyph);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint hb_raster_paint_render(nint paint);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern nint hb_raster_draw_render(nint draw);
}
internal record struct TextRasterGlyph
{
    public TextGlyph Glyph = new();
    public Offsetf Position, Bearing;
    public Rectf Bounds;
    public Sizef BitmapSize;
    public uint SourceSize26_6;
    public TextRasterGlyph() { }
    public bool IsVisible() => Glyph.Kind == ETextGlyphKind.HexBox ? Glyph.GlyphIndex != 0 && BitmapSize.Width > 0 && BitmapSize.Height > 0 : Glyph.Kind == ETextGlyphKind.Glyph && Glyph.GlyphIndex != 0;
}
internal record struct TextGlyphRasterRequest
{
    public ETextPixelMode PixelMode = ETextPixelMode.Gray;
    public ETextHinting Hinting = ETextHinting.Light;
    public uint SdfPpem = 32, SdfSpread = 4;
    public ETextSubpixelPositioning SubpixelPositioning = ETextSubpixelPositioning.Disabled;
    public byte SubpixelPhase;
    public bool LoadColor;
    public bool DisableEmbeddedBitmaps = true;
    public TextGlyphRasterRequest() { }
}
internal record struct TextGlyphRasterPlan
{
    public TextFontFaceImpl? Font;
    public uint GlyphIndex, Size26_6;
    public TextGlyphRasterRequest Request = new();
    public TextGlyphRasterPlan() { }
    public TextRenderedGlyphKey CacheKey() => new(GlyphIndex, Request.SubpixelPositioning, Request.SubpixelPhase);
}
internal unsafe struct TextGlyphAtlasBitmap { public byte* Pixels; public int Pitch; public uint Width, Height; public ETextAtlasBitmapFormat Format; }
internal sealed class TextHBRasterImageLease : IDisposable
{
    public nint Paint, Draw, Image;
    public void AcquirePaint(nint owner, nint image) { Debug.Assert(Image == 0); Paint = owner; Image = image; }
    public void AcquireDraw(nint owner, nint image) { Debug.Assert(Image == 0); Draw = owner; Image = image; }
    public void Dispose() { if (Paint != 0 && Image != 0) TextNative.hb_raster_paint_recycle_image(Paint, Image); else if (Draw != 0 && Image != 0) TextNative.hb_raster_draw_recycle_image(Draw, Image); Image = 0; }
}
internal record struct TextHBRasterTransform
{
    public float Xx = 1, Yx, Xy, Yy = -1, Dx, Dy;
    public TextHBRasterTransform() { }
}
internal static unsafe partial class TextAlgorithms
{
    internal static bool IsSdfPixelMode(ETextPixelMode mode) => mode is ETextPixelMode.Sdf or ETextPixelMode.SdfLcd or ETextPixelMode.Msdf or ETextPixelMode.MsdfLcd;
    internal static bool SetSdfSpread(nint library, uint value)
    {
        if (library == 0) return false; int spread = (int)SanitizeSdfSpread(value);
        fixed (byte* property = "spread\0"u8) fixed (byte* sdf = "sdf\0"u8) fixed (byte* bsdf = "bsdf\0"u8)
            return TextNative.FT_Property_Set(library, sdf, property, &spread) == 0 && TextNative.FT_Property_Set(library, bsdf, property, &spread) == 0;
    }
    internal static bool IsSdfFaceEligible(nint face)
    { if (face == 0) return false; ref var f = ref TextNative.Face(face); return (f.FaceFlags & TextNative.FT_FACE_FLAG_TRICKY) == 0 && (f.FaceFlags & TextNative.FT_FACE_FLAG_SCALABLE) != 0 && ((f.FaceFlags & TextNative.FT_FACE_FLAG_FIXED_SIZES) == 0 || f.NumGlyphs > 0); }
    internal static bool IsSdfGlyphEligible(nint slot) => slot != 0 && TextNative.Slot(slot).Format == TextNative.FT_GLYPH_FORMAT_OUTLINE && TextNative.Slot(slot).Metrics.Width > 0 && TextNative.Slot(slot).Metrics.Height > 0;
    internal static bool IsEmptyGlyph(nint slot) => slot != 0 && (TextNative.Slot(slot).Metrics.Width <= 0 || TextNative.Slot(slot).Metrics.Height <= 0);
    internal static int RasterLoadFlags(ETextPixelMode mode, ETextHinting hinting, bool color, bool disableBitmaps)
    {
        if (IsSdfPixelMode(mode)) return TextFontSupport.LoadFlags(hinting, false, true) | 1 << 11 | TextNative.FT_LOAD_NO_BITMAP;
        if (mode == ETextPixelMode.GrayLcd) return TextFontSupport.LoadFlags(hinting, color, disableBitmaps);
        return TextFontSupport.LoadFlags(hinting, color || mode == ETextPixelMode.Colored, disableBitmaps);
    }
    internal static bool RenderGlyph(nint slot, ETextPixelMode mode)
    { if (slot == 0) return false; if (IsSdfPixelMode(mode)) return TextNative.FT_Render_Glyph(slot, 0) == 0 && TextNative.FT_Render_Glyph(slot, 5) == 0; return TextNative.FT_Render_Glyph(slot, mode == ETextPixelMode.GrayLcd ? 3 : 0) == 0; }
    internal static bool BitmapFormat(in FT_Bitmap bitmap, ref ETextAtlasBitmapFormat output)
    { switch (bitmap.PixelMode) { case 1: output = ETextAtlasBitmapFormat.Mono; return true; case 7: output = ETextAtlasBitmapFormat.BGRA; return true; case 5: output = ETextAtlasBitmapFormat.LCD; return true; case 2: output = ETextAtlasBitmapFormat.Gray; return true; default: return false; } }
    internal static TextHBRasterTransform HBRasterTransform(Float3x3 transform, float xshift)
    {
        TextHBRasterTransform result = new() { Dx = xshift };
        if (TextFontSupport.IsSimpleSlantTransform(transform)) return result;
        if (!float.IsFinite(transform.M00) || !float.IsFinite(transform.M01) || !float.IsFinite(transform.M10) || !float.IsFinite(transform.M11)) return result;
        result.Xx = transform.M00; result.Yx = -transform.M01; result.Xy = transform.M10; result.Yy = -transform.M11; return result;
    }
    internal static bool PrepareHBAtlasBitmap(nint image, int expectedFormat, out TextGlyphAtlasBitmap output, out hb_raster_extents_t extents)
    {
        output = new(); extents = new(); if (image == 0 || TextNative.hb_raster_image_get_format(image) != expectedFormat) return false;
        TextNative.hb_raster_image_get_extents(image, out extents); if (extents.Width == 0 || extents.Height == 0) return true;
        if (extents.Stride == 0 || extents.Stride > int.MaxValue || TextNative.hb_raster_image_get_buffer(image) == null) return false;
        output.Pixels = TextNative.hb_raster_image_get_buffer(image); output.Pitch = (int)extents.Stride; output.Width = extents.Width; output.Height = extents.Height;
        output.Format = expectedFormat == 1 ? ETextAtlasBitmapFormat.BGRA : ETextAtlasBitmapFormat.Gray; return true;
    }
    internal static uint SubpixelDivisor(ETextSubpixelPositioning mode) => mode == ETextSubpixelPositioning.Quarter ? 4u : mode == ETextSubpixelPositioning.Half ? 2u : 1u;
    internal static bool PrepareAtlasBitmap(in FT_Bitmap bitmap, uint width, uint height, out TextGlyphAtlasBitmap output)
    {
        output = new(); if (bitmap.Buffer == 0 || width == 0 || height == 0) return false;
        ETextAtlasBitmapFormat format=ETextAtlasBitmapFormat.Gray;if (!BitmapFormat(bitmap, ref format)) return false;
        output.Pixels = (byte*)bitmap.Buffer; output.Pitch = bitmap.Pitch; output.Width = width; output.Height = height; output.Format = format; return true;
    }
    internal static TextGlyphRasterRequest RasterRequest(in TextFontRasterConfig config, TextFontFaceImpl font, bool fallback = false)
    {
        bool color = font.Face != 0 && (TextNative.Face(font.Face).FaceFlags & TextNative.FT_FACE_FLAG_COLOR) != 0;
        if (!fallback && TextFontSupport.IsFixedSdfSource(font, config)) return new() { PixelMode = config.LcdMode ? ETextPixelMode.SdfLcd : ETextPixelMode.Sdf, Hinting = config.Hinting, SdfPpem = config.SdfPpem, SdfSpread = config.SdfSpread, SubpixelPositioning = config.SubpixelPositioning, DisableEmbeddedBitmaps = true };
        return new() { PixelMode = config.LcdMode ? ETextPixelMode.GrayLcd : ETextPixelMode.Gray, Hinting = config.Hinting, SdfPpem = 0, SdfSpread = 0, SubpixelPositioning = config.SubpixelPositioning, LoadColor = color, DisableEmbeddedBitmaps = config.DisableEmbeddedBitmaps };
    }
    internal static float PaintPixelRatio(in TextPaintDesc desc) => float.IsFinite(desc.PixelRatio) && desc.PixelRatio > 0 ? desc.PixelRatio : 1;
    internal static float RasterPixelRatio(in TextPaintDesc desc) => Math.Max(1u, (uint)(Math.Clamp(PaintPixelRatio(desc), .1f, 100f) * 64)) / 64f;
    internal static ETextSubpixelPositioning RasterSubpixelPositioning(ETextSubpixelPositioning configured, ETextPixelMode mode, uint size26_6)
    { if (IsSdfPixelMode(mode)) return ETextSubpixelPositioning.Disabled; if (configured != ETextSubpixelPositioning.Auto) return configured; if (size26_6 <= 16 * 64) return ETextSubpixelPositioning.Quarter; if (size26_6 <= 20 * 64) return ETextSubpixelPositioning.Half; return ETextSubpixelPositioning.Disabled; }
    internal static float SubpixelBias(ETextSubpixelPositioning mode) { uint divisor = SubpixelDivisor(mode); return divisor > 1 ? .5f / divisor : 0; }
    internal static byte SubpixelPhase(ETextSubpixelPositioning mode, float x)
    { uint divisor = SubpixelDivisor(mode); if (divisor == 1 || !float.IsFinite(x)) return 0; float biased = x + SubpixelBias(mode), b = MathF.Floor(biased), phase = MathF.Floor(divisor * biased) - divisor * b; return (byte)Math.Clamp((int)phase, 0, (int)divisor - 1); }
}
