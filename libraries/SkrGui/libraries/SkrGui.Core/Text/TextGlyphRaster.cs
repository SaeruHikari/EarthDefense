using System.Diagnostics;
namespace SkrGui;

// Source: text/render/text_glyph_raster.cpp:397-694 @ 611561f8.
internal static unsafe partial class TextAlgorithms
{
    internal static bool PrepareRenderedGlyph(nint library, ETextServicesBackend backend, TextFontFaceImpl font, TextFontSizeCache cache, in TextGlyphRasterPlan plan, uint padding, out TextRenderedGlyph output, out TextGlyphAtlasBitmap bitmap, TextHBRasterImageLease lease)
    {
        output = new(); bitmap = new(); font.ApplyPalette(); Debug.Assert(cache.Size26_6 == plan.Size26_6);
        float rasterSize = cache.RasterFontSize(); bool sdf = IsSdfPixelMode(plan.Request.PixelMode);
        if (sdf && !SetSdfSpread(library, plan.Request.SdfSpread)) return false;
        uint divisor = SubpixelDivisor(plan.Request.SubpixelPositioning); int shift = (int)(plan.Request.SubpixelPhase * 64u / divisor);
        if (TextNative.FT_Load_Glyph(font.Face, plan.GlyphIndex, RasterLoadFlags(plan.Request.PixelMode, plan.Request.Hinting, plan.Request.LoadColor, plan.Request.DisableEmbeddedBitmaps)) != 0) return false;
        nint slotPointer = TextNative.Face(font.Face).Glyph; ref var slot = ref TextNative.Slot(slotPointer);
        if (plan.Request.SubpixelPhase != 0 && divisor > 1 && !sdf && slot.Format == TextNative.FT_GLYPH_FORMAT_OUTLINE) TextNative.FT_Outline_Translate(ref slot.Outline, shift, 0);
        if (font.EmboldenValue != 0 && slot.Format == TextNative.FT_GLYPH_FORMAT_OUTLINE) TextNative.FT_Outline_Embolden(ref slot.Outline, (int)(font.EmboldenValue * plan.Size26_6 / 16));
        if (slot.Format == TextNative.FT_GLYPH_FORMAT_OUTLINE) TextFontSupport.ApplyOutlineTransform(ref slot.Outline, font.TransformValue);
        bool requiresColorRenderer = slot.Format == 0x53564720;
        output.FontSize = rasterSize; output.SubpixelPositioning = plan.Request.SubpixelPositioning;
        if (sdf && IsEmptyGlyph(slotPointer)) { output.PixelMode = plan.Request.PixelMode; return true; }
        if (sdf && !IsSdfGlyphEligible(slotPointer)) return false;
        bool useHb = backend == ETextServicesBackend.Advanced && plan.Request.PixelMode == ETextPixelMode.Gray && slot.Format != TextNative.FT_GLYPH_FORMAT_BITMAP && cache.ShapingFont != 0 && font.RasterPaint != 0 && font.RasterDraw != 0;
        if (useHb)
        {
            float xshift = shift + font.EmboldenValue * plan.Size26_6 / 64;
            var transform = HBRasterTransform(font.TransformValue, xshift); bool rasterized = false, color = false; hb_raster_extents_t extents = new();
            if (plan.Request.LoadColor && cache.ColorPaint)
            {
                TextNative.hb_raster_paint_reset(font.RasterPaint); TextNative.hb_raster_paint_set_scale_factor(font.RasterPaint, 64, 64);
                TextNative.hb_raster_paint_set_transform(font.RasterPaint, transform.Xx, transform.Yx, transform.Xy, transform.Yy, transform.Dx, transform.Dy);
                TextNative.hb_raster_paint_clear_custom_palette_colors(font.RasterPaint); TextNative.hb_raster_paint_set_palette(font.RasterPaint, font.SelectedPalette);
                for (int index = 0; index < font.CustomPalette.Count; index++)
                {
                    var c = font.CustomPalette[index]; byte b = c.Blue8(), g = c.Green8(), r = c.Red8(), a = c.Alpha8();
                    if (b == 0 && g == 0 && r == 0 && a == 0) continue;
                    TextNative.hb_raster_paint_set_custom_palette_color(font.RasterPaint, (uint)index, (uint)b << 24 | (uint)g << 16 | (uint)r << 8 | a);
                }
                if (TextNative.hb_raster_paint_glyph_or_fail(font.RasterPaint, cache.ShapingFont, plan.GlyphIndex) != 0)
                {
                    rasterized = true; color = true; nint image = TextNative.hb_raster_paint_render(font.RasterPaint);
                    if (image != 0) { lease.AcquirePaint(font.RasterPaint, image); if (!PrepareHBAtlasBitmap(image, 1, out bitmap, out extents)) return false; }
                }
            }
            if (!rasterized && !requiresColorRenderer && plan.Request.Hinting == ETextHinting.None)
            {
                TextNative.hb_raster_draw_reset(font.RasterDraw); TextNative.hb_raster_draw_set_scale_factor(font.RasterDraw, 64, 64);
                TextNative.hb_raster_draw_set_transform(font.RasterDraw, transform.Xx, transform.Yx, transform.Xy, transform.Yy, transform.Dx, transform.Dy);
                TextNative.hb_raster_draw_glyph(font.RasterDraw, cache.ShapingFont, plan.GlyphIndex); nint image = TextNative.hb_raster_draw_render(font.RasterDraw);
                if (image != 0) { rasterized = true; lease.AcquireDraw(font.RasterDraw, image); if (!PrepareHBAtlasBitmap(image, 0, out bitmap, out extents)) return false; }
            }
            if (rasterized)
            {
                output.PixelMode = color ? ETextPixelMode.Colored : ETextPixelMode.Gray; if (extents.Width == 0 || extents.Height == 0) return true;
                output.Bearing = new(extents.XOrigin - (float)padding, -extents.YOrigin + (float)padding);
                output.BitmapSize = new(bitmap.Width + padding * 2, bitmap.Height + padding * 2); return true;
            }
        }
        if (!RenderGlyph(slotPointer, plan.Request.PixelMode)) return false;
        uint displayWidth = slot.Bitmap.Width, height = slot.Bitmap.Rows;
        if (slot.Bitmap.PixelMode == 5) { if (displayWidth % 3 != 0) return false; displayWidth /= 3; }
        bool empty = displayWidth == 0 || height == 0 || slot.Bitmap.Buffer == 0;
        if (!empty && !PrepareAtlasBitmap(slot.Bitmap, displayWidth, height, out bitmap)) return false;
        output.PixelMode = sdf ? plan.Request.PixelMode : bitmap.Format == ETextAtlasBitmapFormat.BGRA ? ETextPixelMode.Colored : bitmap.Format == ETextAtlasBitmapFormat.LCD ? ETextPixelMode.GrayLcd : ETextPixelMode.Gray;
        if (empty) return true;
        output.Bearing = new(slot.BitmapLeft - (float)padding, slot.BitmapTop + (float)padding);
        output.BitmapSize = new(bitmap.Width + padding * 2, bitmap.Height + padding * 2); return true;
    }
}
// Source: text_glyph_raster.cpp:1051-end. Font source overrides and rendered
// variants have separate lifetimes; atlas writes happen inside an update batch.
internal sealed partial class TextServicesImpl
{
    internal void FontGlyphRasterRectOverride(TextFontFaceImpl font, uint glyphIndex, uint sourceSize26_6, float size, out Offsetf? offset, out Sizef? extent)
    {
        offset = null; extent = null;
        if (font.GlyphOverrideCount == 0 || sourceSize26_6 == 0 || !font.SizeCaches.TryGetValue(sourceSize26_6, out var cache) || !cache.Glyphs.TryGetValue(glyphIndex, out var glyph) || !glyph.HasOverride()) return;
        float scale = size * 64 / sourceSize26_6;
        if (glyph.RasterOffsetOverride is { } o) offset = o * scale;
        if (glyph.RasterSizeOverride is { } s) extent = s * scale;
    }
    internal bool FontGlyphRasterRect(TextFontFaceImpl font, uint glyphIndex, float size, out Offsetf offset, out Sizef extent)
    {
        offset = new(); extent = new(); TextGlyphMetrics metrics=new();
        if (font.Face == 0 || glyphIndex == 0 || !GetGlyphMetrics(font.IdValue, 0, glyphIndex, size, ref metrics)) return false;
        FontGlyphRasterRectOverride(font, glyphIndex, metrics.SourceSize26_6, metrics.FontSize, out var overrideOffset, out var overrideSize);
        TextRasterGlyph glyph = new() { Glyph = new() { FontFace = font.IdValue, FontSize = size, GlyphIndex = glyphIndex, Kind = ETextGlyphKind.Glyph }, Bearing = metrics.Bearing, BitmapSize = metrics.BitmapSize, SourceSize26_6 = metrics.SourceSize26_6 };
        var config = EffectiveFontRasterConfig(font); var request = TextAlgorithms.RasterRequest(config, font);
        bool canRasterize = font.SizeCaches.TryGetValue(metrics.SourceSize26_6, out var cache) && cache.Glyphs.TryGetValue(glyphIndex, out var cachedGlyph) && cachedGlyph.LoadState == TextFontCachedGlyph.ELoadState.Loaded;
        TextRenderedGlyph? rendered = null;
        if (canRasterize)
        {
            bool ownsUpdate = !AtlasManager.UpdateActive(); if (ownsUpdate) AtlasManager.BeginUpdate();
            rendered = EnsureRenderedGlyph(glyph, request, 1, null);
            if (rendered is null && TextAlgorithms.IsSdfPixelMode(request.PixelMode)) { request = TextAlgorithms.RasterRequest(config, font, true); rendered = EnsureRenderedGlyph(glyph, request, 1, null); }
            if (ownsUpdate) AtlasManager.EndUpdate();
        }
        if (rendered is not null) { float scale = rendered.FontSize > 0 ? size / rendered.FontSize : 1; offset = new(rendered.Bearing.X * scale, -rendered.Bearing.Y * scale); extent = rendered.BitmapSize * scale; }
        if (overrideOffset.HasValue) offset = overrideOffset.Value; if (overrideSize.HasValue) extent = overrideSize.Value;
        return rendered is not null || overrideOffset.HasValue || overrideSize.HasValue;
    }
    internal bool PaintGlyph(in TextRasterGlyph glyph, Offsetf origin, in TextPaintDesc desc, TextRenderResult output)
    {
        if (!glyph.IsVisible()) return true;
        if (glyph.Glyph.Kind == ETextGlyphKind.HexBox) { var rect = glyph.Bounds.Shift(origin); if (!rect.IsEmpty()) output.BuildAppendBox(glyph.Glyph.SpanId, rect); return true; }
        var font = FindFontFace(glyph.Glyph.FontFace); if (font is null) return false;
        var config = EffectiveFontRasterConfig(font); var request = TextAlgorithms.RasterRequest(config, font);
        float ratio = TextAlgorithms.PaintPixelRatio(desc), rasterRatio = TextAlgorithms.RasterPixelRatio(desc); float? deviceX = null;
        if (desc.DeviceOffset is { } device && device.IsFinite()) deviceX = (origin.X + glyph.Position.X - glyph.Bearing.X) * ratio + device.X;
        var rendered = EnsureRenderedGlyph(glyph, request, rasterRatio, deviceX);
        if (rendered is null && TextAlgorithms.IsSdfPixelMode(request.PixelMode)) { request = TextAlgorithms.RasterRequest(config, font, true); rendered = EnsureRenderedGlyph(glyph, request, rasterRatio, deviceX); }
        if (rendered is null) return false; if (rendered.AtlasIndex == uint.MaxValue) return true;
        output.BuildAppendRect(rendered.AtlasIndex, rendered.PixelMode, Rectf.Largest(), glyph.Glyph.SpanId, RenderedGlyphRect(glyph, rendered, origin, desc), rendered.AtlasUv); return true;
    }
    internal bool ResolveGlyphRasterPlan(in TextRasterGlyph glyph, in TextGlyphRasterRequest request, float rasterRatio, float? deviceX, out TextGlyphRasterPlan output)
    {
        output = new(); var font = FindFontFace(glyph.Glyph.FontFace); if (font is null || font.Face == 0) return false;
        bool sdf = TextAlgorithms.IsSdfPixelMode(request.PixelMode); if (sdf && !TextAlgorithms.IsSdfFaceEligible(font.Face)) return false;
        uint size = sdf ? request.SdfPpem * 64 : PixelSize26_6(glyph.Glyph.FontSize * rasterRatio); if (size == 0) return false;
        var resolved = request; resolved.SubpixelPositioning = TextAlgorithms.RasterSubpixelPositioning(request.SubpixelPositioning, request.PixelMode, size);
        resolved.SubpixelPhase = deviceX.HasValue ? TextAlgorithms.SubpixelPhase(resolved.SubpixelPositioning, deviceX.Value) : (byte)0;
        output = new() { Font = font, GlyphIndex = glyph.Glyph.GlyphIndex, Size26_6 = size, Request = resolved }; return true;
    }
    internal TextRenderedGlyph? EnsureRenderedGlyph(in TextRasterGlyph glyph, in TextGlyphRasterRequest request, float rasterRatio, float? deviceX)
    {
        if (!ResolveGlyphRasterPlan(glyph, request, rasterRatio, deviceX, out var plan)) return null;
        if (plan.Font!.SizeCaches.TryGetValue(plan.Size26_6, out var cache) && cache.RenderedGlyphs.TryGetValue(plan.CacheKey(), out var found)) return found;
        return RasterizeGlyph(plan);
    }
    internal unsafe TextRenderedGlyph? RasterizeGlyph(in TextGlyphRasterPlan plan)
    {
        var font = plan.Font; if (font is null || font.Face == 0) return null;
        var cache = EnsureFontSizeCache(font, plan.Size26_6); if (cache is null) return null;
        if (cache.RenderedGlyphs.TryGetValue(plan.CacheKey(), out var found)) return found;
        using TextHBRasterImageLease lease = new();
        if (!TextAlgorithms.PrepareRenderedGlyph(Library, BackendValue, font, cache, plan, AtlasManager.AtlasGlyphPadding(), out var rendered, out var bitmap, lease)) return null;
        if (bitmap.Width > 0 && bitmap.Height > 0)
        {
            var format = bitmap.Format is ETextAtlasBitmapFormat.BGRA or ETextAtlasBitmapFormat.LCD ? ETextAtlasFormat.RGBA8 : ETextAtlasFormat.L8;
            if (!AtlasManager.Allocate(format, bitmap.Width, bitmap.Height, out var allocation) || !AtlasManager.WriteBitmap(allocation, bitmap.Pixels, bitmap.Pitch, bitmap.Format)) return null;
            uint padding = AtlasManager.AtlasGlyphPadding(); rendered.AtlasIndex = allocation.AtlasIndex;
            var atlasSize = AtlasManager.AtlasPageSize(format); float iw = 1f / atlasSize.Width, ih = 1f / atlasSize.Height;
            rendered.AtlasUv = Rectf.LTWH((allocation.X - padding) * iw, (allocation.Y - padding) * ih, (allocation.Width + padding * 2) * iw, (allocation.Height + padding * 2) * ih);
        }
        cache.RenderedGlyphs.TryAdd(plan.CacheKey(), rendered); return cache.RenderedGlyphs[plan.CacheKey()];
    }
    internal Rectf RenderedGlyphRect(in TextRasterGlyph glyph, TextRenderedGlyph rendered, Offsetf origin, in TextPaintDesc desc)
    {
        float rasterRatio = TextAlgorithms.RasterPixelRatio(desc);
        float scale = float.IsFinite(rendered.FontSize) && rendered.FontSize > 0 ? glyph.Glyph.FontSize / rendered.FontSize : 1 / rasterRatio;
        float penX = glyph.Position.X - glyph.Bearing.X, baseline = glyph.Position.Y + glyph.Bearing.Y;
        Offsetf offset = new(rendered.Bearing.X * scale, -rendered.Bearing.Y * scale); Sizef size = rendered.BitmapSize * scale;
        if (FindFontFace(glyph.Glyph.FontFace) is { } font)
        { FontGlyphRasterRectOverride(font, glyph.Glyph.GlyphIndex, glyph.SourceSize26_6, glyph.Glyph.FontSize, out var overrideOffset, out var overrideSize); if (overrideOffset.HasValue) offset = overrideOffset.Value; if (overrideSize.HasValue) size = overrideSize.Value; }
        if (!TextAlgorithms.IsSdfPixelMode(rendered.PixelMode) && desc.DeviceOffset is { } device && device.IsFinite())
        {
            float ratio = TextAlgorithms.PaintPixelRatio(desc), deviceScale = scale * ratio;
            if (float.IsFinite(deviceScale) && deviceScale > 0)
            {
                Offsetf pen = new((origin.X + penX) * ratio + device.X, (origin.Y + baseline) * ratio + device.Y);
                pen.X += TextAlgorithms.SubpixelBias(rendered.SubpixelPositioning);
                if (deviceScale == 1) { pen.X = MathF.Floor(pen.X); pen.Y = MathF.Floor(pen.Y); }
                var rect = Rectf.LTWH(pen.X + offset.X * ratio, pen.Y + offset.Y * ratio, size.Width * ratio, size.Height * ratio);
                return Rectf.LTWH((rect.Left - device.X) / ratio, (rect.Top - device.Y) / ratio, rect.Width() / ratio, rect.Height() / ratio);
            }
        }
        return Rectf.LTWH(origin.X + penX + offset.X, origin.Y + baseline + offset.Y, size.Width, size.Height);
    }
}
