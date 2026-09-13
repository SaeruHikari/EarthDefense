namespace SkrGui;

// Source: src/text/font/text_font_size.cpp and text_font_cache.cpp.
internal static unsafe partial class TextFontSupport
{
    internal static bool IsFixedSdfSource(TextFontFaceImpl f,in TextFontRasterConfig c) =>
        f.Face!=0&&c.Mode==ETextRasterMode.Sdf&&(TextNative.Face(f.Face).FaceFlags&TextNative.FT_FACE_FLAG_SCALABLE)!=0&&
        (TextNative.Face(f.Face).FaceFlags&(TextNative.FT_FACE_FLAG_TRICKY|TextNative.FT_FACE_FLAG_COLOR))==0;
    internal static ulong GlyphPairKey(uint left,uint right)=>((ulong)left<<32)|right;
    internal static bool ChooseColorFixedStrike(nint face,uint requested,out int index,out uint strikeSize)
    {
        index=-1;strikeSize=0;if(face==0||requested==0)return false;
        ref var f=ref TextNative.Face(face);
        if((f.FaceFlags&TextNative.FT_FACE_FLAG_COLOR)==0||(f.FaceFlags&TextNative.FT_FACE_FLAG_FIXED_SIZES)==0||f.NumFixedSizes<=0)return false;
        int best=int.MaxValue;
        for(int i=0;i<f.NumFixedSizes;i++)
        {
            int width=((FT_Bitmap_Size*)f.AvailableSizes)[i].Width;if(width<=0)continue;
            int distance=(int)Math.Abs(requested/64.0-width);
            if(distance<best){best=distance;index=i;strikeSize=(uint)width*64;}
        }
        return index>=0;
    }
    internal static bool ApplySize(nint face,uint requested,out float scale)
    {
        scale=1;if(face==0||requested==0)return false;
        if(ChooseColorFixedStrike(face,requested,out int strike,out uint strikeSize))
        {
            if(TextNative.FT_Select_Size(face,strike)!=0)return false;
            scale=(float)requested/strikeSize;return float.IsFinite(scale)&&scale>0;
        }
        FT_Size_Request request=new(){Type=0,Width=(int)Math.Min(requested,2048u*64),Height=(int)Math.Min(requested,2048u*64)};
        if(TextNative.FT_Request_Size(face,ref request)!=0)return false;
        ushort ppem=TextNative.Size(TextNative.Face(face).Size).Metrics.YPpem;
        if(ppem!=0)scale=(requested/64f)/ppem;
        return float.IsFinite(scale)&&scale>0;
    }
    internal static TextMetrics ReadSizeMetrics(TextFontFaceImpl font,TextFontSizeCache cache)
    {
        ref var face=ref TextNative.Face(font.Face);ref var m=ref TextNative.Size(face.Size).Metrics;
        TextMetrics r=new(){Ascent=m.Ascender/64f*cache.Scale,Descent=-m.Descender/64f*cache.Scale,LineHeight=m.Height/64f*cache.Scale};
        if(r.LineHeight<=0)r.LineHeight=r.Ascent+r.Descent;
        if(r.LineHeight<=0)r.LineHeight=cache.FontSize();
        r.LineGap=Max(0,r.LineHeight-r.Ascent-r.Descent);
        r.UnderlinePosition=-TextNative.FT_MulFix(face.UnderlinePosition,m.YScale)/64f*cache.Scale;
        r.UnderlineThickness=TextNative.FT_MulFix(face.UnderlineThickness,m.YScale)/64f*cache.Scale;
        return r;
    }
    internal static float Max(float a,float b)=>a<b?b:a;
}
internal unsafe partial class TextServicesImpl
{
    internal TextFontSizeCache? EnsureFontSizeCache(TextFontFaceImpl font,uint size26_6)
    {
        if(font.Face==0||size26_6==0)return null;
        if(font.SizeCaches.TryGetValue(size26_6,out var existing))
            return existing.FtSize!=0&&TextNative.FT_Activate_Size(existing.FtSize)==0?existing:null;
        var cache=new TextFontSizeCache();if(TextNative.FT_New_Size(font.Face,out nint size)!=0)return null;
        cache.FtSize=size;cache.Size26_6=size26_6;
        if(TextNative.FT_Activate_Size(size)!=0||!TextFontSupport.ApplySize(font.Face,size26_6,out cache.Scale)){cache.Dispose();return null;}
        cache.Metrics=TextFontSupport.ReadSizeMetrics(font,cache);
        if(BackendValue==ETextServicesBackend.Advanced)
        {
            if(font.RasterPaint==0)font.RasterPaint=TextNative.hb_raster_paint_create_or_fail();
            if(font.RasterDraw==0)font.RasterDraw=TextNative.hb_raster_draw_create_or_fail();
            cache.ShapingFont=TextNative.hb_ft_font_create_referenced(font.Face);
            if(cache.ShapingFont==0){cache.Dispose();return null;}
            nint face=TextNative.hb_font_get_face(cache.ShapingFont);
            cache.ColorPaint=face!=0&&(TextNative.hb_ot_color_has_paint(face)!=0||TextNative.hb_ot_color_has_layers(face)!=0);
            if(font.EmboldenValue!=0)TextNative.hb_font_set_synthetic_bold(cache.ShapingFont,font.EmboldenValue/16f,font.EmboldenValue/16f,1);
            if(TextFontSupport.IsSimpleSlantTransform(font.TransformValue))TextNative.hb_font_set_synthetic_slant(cache.ShapingFont,font.TransformValue.M10);
            if(font.Coordinates.Count!=0)
            {
                var variations=font.Coordinates.Select(v=>new hb_variation_t{Tag=v.Tag,Value=v.Value}).ToArray();
                fixed(hb_variation_t* p=variations)TextNative.hb_font_set_variations(cache.ShapingFont,p,(uint)variations.Length);
            }
        }
        font.SizeCaches.Add(size26_6,cache);return cache;
    }
    internal uint FontSourceSize26_6(TextFontFaceImpl font,float size)
    {
        uint requested=PixelSize26_6(size);if(requested==0)return 0;
        var config=EffectiveFontRasterConfig(font);
        return TextFontSupport.IsFixedSdfSource(font,config)?config.SdfPpem*64u:requested;
    }
    internal bool ResolveLayoutFontSize(TextFontFaceImpl font,float size,out TextLayoutFontSize result)
    {
        result=default;uint requested=PixelSize26_6(size);if(requested==0)return false;
        var config=EffectiveFontRasterConfig(font);bool sdf=TextFontSupport.IsFixedSdfSource(font,config);
        var cache=EnsureFontSizeCache(font,sdf?config.SdfPpem*64u:requested);if(cache==null)return false;
        result=new(){Cache=cache,RequestedSize26_6=requested,UsesSdfSource=sdf};return result.IsValid();
    }
    internal bool FontBaseMetrics(TextFontFaceId id,float size,ref TextMetrics result)=>FontBaseMetrics(id,size,ref result,out _);
    internal bool FontBaseMetrics(TextFontFaceId id,float size,ref TextMetrics result,out float outScale)
    {
        outScale=1;var font=FindFontFace(id);
        if(font==null||!ResolveLayoutFontSize(font,size,out var r))return false;
        result=r.Cache!.Metrics;float scale=r.SourceToRequestedScale();result.Size*=scale;
        result.Bounds=Rectf.LTWH(result.Bounds.Left*scale,result.Bounds.Top*scale,result.Bounds.Width()*scale,result.Bounds.Height()*scale);
        result.Ascent*=scale;result.Descent*=scale;result.LineGap*=scale;result.LineHeight*=scale;
        result.UnderlinePosition*=scale;result.UnderlineThickness*=scale;
        outScale=r.Cache.Scale*scale;return true;
    }
    internal float FontBaselineShift(TextFontFaceId id,float size)
    {
        TextMetrics m=new();var f=FindFontFace(id);return f==null||f.BaselineOffsetValue==0||!FontBaseMetrics(id,size,ref m)?0:f.BaselineOffsetValue*(m.Ascent+m.Descent);
    }
    internal bool FontMetrics(TextFontFaceId id,float size,ref TextMetrics result)
    {
        var f=FindFontFace(id);if(f==null||!FontBaseMetrics(id,size,ref result))return false;
        result.Ascent+=f.SpacingValues[(int)ETextSpacing.Top];result.Descent+=f.SpacingValues[(int)ETextSpacing.Bottom];
        result.LineHeight=TextFontSupport.Max(result.LineHeight,result.Ascent+result.Descent+result.LineGap);return true;
    }
    internal bool GetExactGlyphMetrics(TextFontFaceId id,uint cp,float size,ref TextGlyphMetrics result)=>GetExactGlyphMetrics(id,cp,size,ref result,out _);
    internal bool GetExactGlyphMetrics(TextFontFaceId id,uint cp,float size,ref TextGlyphMetrics result,out bool subpixel)
    {
        subpixel=false;var f=FindFontFace(id);if(f==null||f.Face==0||PixelSize26_6(size)==0)return false;
        uint glyph=TextNative.FT_Get_Char_Index(f.Face,cp);return glyph!=0&&GetGlyphMetrics(id,cp,glyph,size,ref result,out subpixel);
    }
    internal bool GetGlyphMetrics(TextFontFaceId id,uint cp,uint glyph,float size,ref TextGlyphMetrics result)=>GetGlyphMetrics(id,cp,glyph,size,ref result,out _);
    internal bool GetGlyphMetrics(TextFontFaceId id,uint cp,uint glyph,float size,ref TextGlyphMetrics result,out bool subpixel)
    {
        subpixel=false;var f=FindFontFace(id);
        if(f==null||f.Face==0||glyph==0||!ResolveLayoutFontSize(f,size,out var r)||!LoadGlyphMetrics(f,r.Cache!,cp,glyph,ref result))return false;
        var cache=r.Cache!;
        if(f.GlyphOverrideCount>0&&cache.Glyphs.TryGetValue(glyph,out var cached)&&cached.AdvanceOverride.HasValue)result.Advance=cached.AdvanceOverride.Value;
        var config=EffectiveFontRasterConfig(f);subpixel=r.PreservesHorizontalSubpixel(config);
        result.Advance.X+=f.EmboldenValue*cache.Size26_6/4096f;
        result.FontSize=r.RequestedFontSize();result.SourceSize26_6=cache.Size26_6;
        float scale=r.SourceToRequestedScale();result.Advance*=scale;result.Bearing*=scale;result.BitmapSize*=scale;
        if(!r.PreservesDirectGlyphAdvance(config))result.Advance.X=MathF.Round(result.Advance.X,MidpointRounding.AwayFromZero);
        return true;
    }
    internal bool LoadGlyphMetrics(TextFontFaceImpl f,TextFontSizeCache cache,uint cp,uint glyph,ref TextGlyphMetrics result)
    {
        if(!cache.Glyphs.TryGetValue(glyph,out var c)){c=new();cache.Glyphs.Add(glyph,c);}
        if(c.LoadState==TextFontCachedGlyph.ELoadState.Unloaded)
        {
            var config=EffectiveFontRasterConfig(f);
            if(TextNative.FT_Load_Glyph(f.Face,glyph,TextFontSupport.LoadFlags(config.Hinting,(TextNative.Face(f.Face).FaceFlags&TextNative.FT_FACE_FLAG_COLOR)!=0,config.DisableEmbeddedBitmaps||TextFontSupport.IsFixedSdfSource(f,config)))==0)
            {
                ref var s=ref TextNative.Slot(TextNative.Face(f.Face).Glyph);ref var m=ref s.Metrics;
                c.Metrics.Advance=new(s.Advance.X/64f*cache.Scale,s.Advance.Y/64f*cache.Scale);
                c.Metrics.Bearing=new(m.HoriBearingX/64f*cache.Scale,m.HoriBearingY/64f*cache.Scale);
                c.Metrics.BitmapSize=new(m.Width/64f*cache.Scale,m.Height/64f*cache.Scale);c.LoadState=TextFontCachedGlyph.ELoadState.Loaded;
            }
            else c.LoadState=TextFontCachedGlyph.ELoadState.Missing;
        }
        if(c.LoadState==TextFontCachedGlyph.ELoadState.Loaded)result=c.Metrics;else if(!c.HasOverride())return false;else result=new();
        result.Font=f.IdValue;result.Codepoint=cp;result.GlyphIndex=glyph;result.FontSize=cache.FontSize();return true;
    }
    internal float KerningX(TextFontFaceId id,uint left,uint right,float size)
    {
        if(left==0||right==0)return 0;var f=FindFontFace(id);if(f==null||!ResolveLayoutFontSize(f,size,out var r))return 0;
        var c=r.Cache!;if(c.KerningOverrides.TryGetValue(TextFontSupport.GlyphPairKey(left,right),out var v))return v.X*r.SourceToRequestedScale();
        if((TextNative.Face(f.Face).FaceFlags&TextNative.FT_FACE_FLAG_KERNING)==0||TextNative.FT_Get_Kerning(f.Face,left,right,0,out var k)!=0)return 0;
        return k.X/64f*c.Scale*r.SourceToRequestedScale();
    }
    internal uint PixelSize26_6(float size)=>size<=0||!float.IsFinite(size)?0:unchecked((uint)TextFontSupport.Max(1,MathF.Round(size*64f,MidpointRounding.AwayFromZero)));
    internal bool IsFinitePositive(float value)=>float.IsFinite(value)&&value>0;
}
