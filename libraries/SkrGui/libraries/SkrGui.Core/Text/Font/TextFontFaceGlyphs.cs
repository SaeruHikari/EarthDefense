using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
namespace SkrGui;

// Source: font_face.cpp glyph queries, synthetic settings and per-size overrides.
internal sealed unsafe partial class TextFontFaceImpl
{
    public override bool HasCodepoint(uint cp)=>GlyphIndex(cp,0)!=0;
    public override uint GlyphIndex(uint cp,uint selector=0)=>Face==0?0:selector!=0?TextNative.FT_Face_GetCharVariantIndex(Face,cp,selector):TextNative.FT_Get_Char_Index(Face,cp);
    public override uint CodepointFromGlyphIndex(uint glyph)
    {
        if(Face==0||glyph==0)return 0;uint cp=TextNative.FT_Get_First_Char(Face,out uint index),result=0;
        while(index!=0){if(index==glyph)result=cp;cp=TextNative.FT_Get_Next_Char(Face,cp,out index);}return result;
    }
    public override void SupportedCodepoints(List<uint> values)
    {
        values.Clear();if(Face==0)return;uint cp=TextNative.FT_Get_First_Char(Face,out uint glyph);
        while(glyph!=0){values.Add(cp);cp=TextNative.FT_Get_Next_Char(Face,cp,out glyph);}
    }
    public override void SupportedGlyphs(List<uint> values)
    {
        values.Clear();if(Face==0)return;uint cp=TextNative.FT_Get_First_Char(Face,out uint glyph);
        while(glyph!=0){values.Add(glyph);cp=TextNative.FT_Get_Next_Char(Face,cp,out glyph);}
    }
    public override Offsetf GlyphAdvance(uint glyph,float size)
    {
        TextGlyphMetrics metrics=new();if(Services==null||!Services.GetGlyphMetrics(IdValue,0,glyph,size,ref metrics))return new();
        Services.FontGlyphRasterRect(this,glyph,size,out _,out _);return metrics.Advance;
    }
    public override Offsetf GlyphOffset(uint glyph,float size)=>Services!=null&&Services.FontGlyphRasterRect(this,glyph,size,out var o,out _)?o:new();
    public override Sizef GlyphSize(uint glyph,float size)=>Services!=null&&Services.FontGlyphRasterRect(this,glyph,size,out _,out var s)?s:new();
    public override Offsetf Kerning(uint left,uint right,float size)=>Services!=null?new(Services.KerningX(IdValue,left,right,size),0):Offsetf.Zero();
    private sealed class ContourContext(VGPath path,float scale)
    {internal VGPath Path=path;internal float Scale=scale;internal bool HasContour;}
    private static ContourContext Contour(nint handle)=>(ContourContext)GCHandle.FromIntPtr(handle).Target!;
    private static Offsetf OutlinePoint(FT_Vector* p,float scale)=>new(p->X/64f*scale,-p->Y/64f*scale);
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static int ContourMove(FT_Vector* p,nint handle)
    {var c=Contour(handle);if(c.HasContour)c.Path.Close();c.Path.MoveTo(OutlinePoint(p,c.Scale));c.HasContour=true;return 0;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static int ContourLine(FT_Vector* p,nint handle)
    {var c=Contour(handle);c.Path.LineTo(OutlinePoint(p,c.Scale));return 0;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static int ContourConic(FT_Vector* control,FT_Vector* p,nint handle)
    {var c=Contour(handle);c.Path.QuadTo(OutlinePoint(control,c.Scale),OutlinePoint(p,c.Scale));return 0;}
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static int ContourCubic(FT_Vector* control0,FT_Vector* control1,FT_Vector* p,nint handle)
    {var c=Contour(handle);c.Path.CubicTo(OutlinePoint(control0,c.Scale),OutlinePoint(control1,c.Scale),OutlinePoint(p,c.Scale));return 0;}
    public override bool GlyphContour(uint glyph,float size,VGPath path)
    {
        path.Clear();if(Face==0||Services==null)return false;
        if(!Services.ResolveLayoutFontSize(this,size,out var r)||TextNative.FT_Load_Glyph(Face,glyph,TextNative.FT_LOAD_NO_BITMAP)!=0||TextNative.Slot(TextNative.Face(Face).Glyph).Format!=TextNative.FT_GLYPH_FORMAT_OUTLINE)return false;
        ref var outline=ref TextNative.Slot(TextNative.Face(Face).Glyph).Outline;
        if(EmboldenValue!=0)TextNative.FT_Outline_Embolden(ref outline,unchecked((int)(EmboldenValue*r.Cache!.Size26_6/16f)));
        TextFontSupport.ApplyOutlineTransform(ref outline,TransformValue);
        var context=new ContourContext(path,r.Cache!.Scale*r.SourceToRequestedScale());var handle=GCHandle.Alloc(context);
        FT_Outline_Funcs functions=new(){
            MoveTo=(nint)(delegate* unmanaged[Cdecl]<FT_Vector*,nint,int>)&ContourMove,
            LineTo=(nint)(delegate* unmanaged[Cdecl]<FT_Vector*,nint,int>)&ContourLine,
            ConicTo=(nint)(delegate* unmanaged[Cdecl]<FT_Vector*,FT_Vector*,nint,int>)&ContourConic,
            CubicTo=(nint)(delegate* unmanaged[Cdecl]<FT_Vector*,FT_Vector*,FT_Vector*,nint,int>)&ContourCubic};
        int error;try{error=TextNative.FT_Outline_Decompose(ref outline,in functions,GCHandle.ToIntPtr(handle));}finally{handle.Free();}
        if(error!=0){path.Clear();return false;}
        if(context.HasContour)path.Close();return context.HasContour;
    }
    public override FontFaceMetrics Metrics(float size)
    {
        TextMetrics m=new();if(Services==null||!Services.FontBaseMetrics(IdValue,size,ref m,out var scale))return new();
        return new(){Ascent=m.Ascent,Descent=m.Descent,LineGap=m.LineGap,UnderlinePosition=m.UnderlinePosition,UnderlineThickness=m.UnderlineThickness,Scale=scale};
    }
    public override float Embolden()=>EmboldenValue;
    public override void SetEmbolden(float v){if(EmboldenValue!=v){EmboldenValue=v;NotifyModified();}}
    public override float Spacing(ETextSpacing type)=>SpacingValues[(int)type];
    public override void SetSpacing(ETextSpacing type,float v){int i=(int)type;if(SpacingValues[i]!=v){SpacingValues[i]=v;NotifyLayoutModified();}}
    public override float BaselineOffset()=>BaselineOffsetValue;
    public override void SetBaselineOffset(float v){if(BaselineOffsetValue!=v){BaselineOffsetValue=v;NotifyModified();}}
    public override Float3x3 Transform()=>TransformValue;
    public override void SetTransform(Float3x3 v)
    {
        var t=TransformValue;
        // Component comparisons retain C++ NaN semantics rather than record equality.
        if(t.M00!=v.M00||t.M01!=v.M01||t.M02!=v.M02||t.M10!=v.M10||t.M11!=v.M11||t.M12!=v.M12||t.M20!=v.M20||t.M21!=v.M21||t.M22!=v.M22)
        {TransformValue=v;NotifyModified();}
    }
    private TextFontSizeCache? SizeCache(float size)=>Services?.EnsureFontSizeCache(this,Services.FontSourceSize26_6(this,size));
    private TextFontCachedGlyph? EnsureOverride(TextFontSizeCache cache,uint glyph)
    {
        if(glyph==0)return null;if(!cache.Glyphs.TryGetValue(glyph,out var c)){c=new();cache.Glyphs.Add(glyph,c);}
        if(!c.HasOverride())GlyphOverrideCount++;return c;
    }
    public override void SetMetrics(float size,FontFaceMetrics value)
    {
        var c=SizeCache(size);if(c==null)return;
        c.Metrics.Ascent=value.Ascent;c.Metrics.Descent=value.Descent;c.Metrics.LineGap=value.LineGap;
        c.Metrics.LineHeight=value.Ascent+value.Descent+value.LineGap;c.Metrics.UnderlinePosition=value.UnderlinePosition;c.Metrics.UnderlineThickness=value.UnderlineThickness;
        NotifyLayoutModified();
    }
    public override void SetGlyphAdvance(uint glyph,float size,Offsetf value)
    {var cache=SizeCache(size);if(cache==null)return;var c=EnsureOverride(cache,glyph);if(c==null)return;c.AdvanceOverride=value;NotifyLayoutModified();}
    public override void ClearGlyphs(float size)
    {
        var cache=SizeCache(size);if(cache==null)return;bool invalidates=cache.Glyphs.Count!=0;
        foreach(var c in cache.Glyphs.Values)if(c.HasOverride()){GuiAssert.Require(GlyphOverrideCount>0,"glyph_override_count > 0");GlyphOverrideCount--;}
        cache.Glyphs.Clear();Services!.ClearAtlases();if(invalidates)NotifyLayoutModified();
    }
    public override void RemoveGlyph(uint glyph,float size)
    {
        var cache=SizeCache(size);if(cache==null)return;bool invalidates=false;
        if(cache.Glyphs.TryGetValue(glyph,out var c)){invalidates=true;if(c.HasOverride()){GuiAssert.Require(GlyphOverrideCount>0,"glyph_override_count > 0");GlyphOverrideCount--;}}
        cache.Glyphs.Remove(glyph);Services!.ClearAtlases();if(invalidates)NotifyLayoutModified();
    }
    public override void SetGlyphOffset(uint glyph,float size,Offsetf value)
    {var cache=SizeCache(size);if(cache==null)return;var c=EnsureOverride(cache,glyph);if(c!=null)c.RasterOffsetOverride=value;}
    public override void SetGlyphSize(uint glyph,float size,Sizef value)
    {var cache=SizeCache(size);if(cache==null)return;var c=EnsureOverride(cache,glyph);if(c!=null)c.RasterSizeOverride=value;}
    public override void SetKerning(uint left,uint right,float size,Offsetf value)
    {var c=SizeCache(size);if(c==null)return;c.KerningOverrides[TextFontSupport.GlyphPairKey(left,right)]=value;NotifyLayoutModified();}
}
