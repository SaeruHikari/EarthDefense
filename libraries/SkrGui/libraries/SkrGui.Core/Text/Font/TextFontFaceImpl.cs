using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
namespace SkrGui;

[StructLayout(LayoutKind.Sequential)] internal struct hb_variation_t {public uint Tag;public float Value;}
internal static unsafe partial class TextNative
{
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Done_Face(nint face);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Select_Charmap(nint face,uint encoding);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int FT_Outline_Embolden(ref FT_Outline outline,int strength);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_raster_paint_create_or_fail();
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern nint hb_raster_draw_create_or_fail();
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_raster_paint_destroy(nint paint);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_raster_draw_destroy(nint draw);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_font_set_synthetic_bold(nint font,float x,float y,int inPlace);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_font_set_synthetic_slant(nint font,float slant);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern void hb_font_set_variations(nint font,hb_variation_t* variations,uint count);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern uint hb_ot_layout_table_get_script_tags(nint face,uint table,uint start,uint* count,uint* tags);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] internal static extern int hb_ot_layout_script_select_language(nint face,uint table,uint scriptIndex,uint count,uint* tags,out uint languageIndex);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl,EntryPoint="hb_ot_layout_table_get_feature_tags")] internal static extern uint hb_ot_layout_table_get_feature_tags_raw(nint face,uint table,uint start,uint* count,uint* tags);
}
internal static unsafe partial class TextFontSupport
{
    internal const uint Gsub=0x47535542,Gpos=0x47504f53;
    internal static uint AxisTag(char a,char b,char c,char d)=>((uint)a<<24)|((uint)b<<16)|((uint)c<<8)|d;
    internal static float FixedToFloat(int x)=>x/65536f;
    internal static int FontFixed(double x)=>unchecked((int)Math.Round(x*65536.0,MidpointRounding.AwayFromZero));
    internal static double StretchAxisValue(EFontStretch s)=>s switch{
        EFontStretch.UltraCondensed=>50,EFontStretch.ExtraCondensed=>62.5,EFontStretch.Condensed=>75,EFontStretch.SemiCondensed=>87.5,
        EFontStretch.SemiExpanded=>112.5,EFontStretch.Expanded=>125,EFontStretch.ExtraExpanded=>150,EFontStretch.UltraExpanded=>200,_=>100};
    internal static bool EqualApprox(float a,float b)=>a==b||MathF.Abs(a-b)<Max(0.00001f,0.00001f*MathF.Abs(a));
    internal static bool IsSimpleSlantTransform(Float3x3 t)=>float.IsFinite(t.M10)&&EqualApprox(t.M00,1)&&EqualApprox(t.M01,0)&&EqualApprox(t.M11,1);
    internal static bool LinearTransformMatrix(Float3x3 t,out FT_Matrix m)
    {
        m=new(){Xx=0x10000,Yy=0x10000};
        if(!float.IsFinite(t.M00)||!float.IsFinite(t.M01)||!float.IsFinite(t.M10)||!float.IsFinite(t.M11))return false;
        m=new(){Xx=FontFixed(t.M00),Xy=FontFixed(t.M10),Yx=FontFixed(t.M01),Yy=FontFixed(t.M11)};return true;
    }
    internal static void ApplyOutlineTransform(ref FT_Outline outline,Float3x3 transform)
    {
        if(transform.M00==1&&transform.M01==0&&transform.M10==0&&transform.M11==1)return;
        if(LinearTransformMatrix(transform,out var m))TextNative.FT_Outline_Transform(ref outline,in m);
    }
    internal static void ApplyOutlineTransform(nint outline,Float3x3 transform)
    {if(outline!=0)ApplyOutlineTransform(ref *(FT_Outline*)outline,transform);}
    internal static int LoadFlags(ETextHinting hinting,bool color,bool disableBitmaps)
    {
        int flags=0;if(color)flags|=TextNative.FT_LOAD_COLOR;else if(disableBitmaps)flags|=TextNative.FT_LOAD_NO_BITMAP;
        flags|=hinting switch {ETextHinting.None=>TextNative.FT_LOAD_NO_HINTING,ETextHinting.Normal=>TextNative.FT_LOAD_TARGET_NORMAL,_=>TextNative.FT_LOAD_TARGET_LIGHT};return flags;
    }
    internal static Utf8StringView CopyNativeString(nint pointer)
    {
        if(pointer==0)return default;byte* p=(byte*)pointer;int length=0;while(p[length]!=0)length++;
        return new Utf8StringView(new ReadOnlySpan<byte>(p,length).ToArray());
    }
    internal static Utf8StringView DecodeSfntName(in FT_SfntName name)
    {
        if(name.String==0||name.StringLength==0)return default;
        if(name.PlatformId is 0 or 3 or 2)
        {
            var chars=new char[name.StringLength/2];byte* source=(byte*)name.String;
            for(int i=0;i<chars.Length;i++)chars[i]=(char)((source[i*2]<<8)|source[i*2+1]);
            if(OperatingSystem.IsWindows())
            {
                // SkrBase code_conv_utf16_to_utf8 uses these exact Win32 calls.
                fixed(char* text=chars)
                {
                    int length=DWriteNative.WideCharToMultiByte(65001,0,text,chars.Length,null,0,0,0);
                    if(length<=0)return default;var bytes=new byte[length];
                    fixed(byte* output=bytes)DWriteNative.WideCharToMultiByte(65001,0,text,chars.Length,output,length,0,0);
                    return new Utf8StringView(bytes);
                }
            }
            return new Utf8StringView(Encoding.UTF8.GetBytes(chars));
        }
        return new Utf8StringView(new ReadOnlySpan<byte>((void*)name.String,(int)name.StringLength).ToArray());
    }
    internal static bool HasScript(TextFontFaceImpl font,uint script)
    {
        if(font.Face==0)return false;nint face=TextNative.hb_ft_face_create_referenced(font.Face);
        uint* tags=stackalloc uint[3];new Span<uint>(tags,3).Clear();uint count=3,languages=0;
        TextNative.hb_ot_tags_from_script_and_language(script,0,ref count,tags,ref languages,null);
        bool supported=false;
        foreach(uint table in new uint[]{Gsub,Gpos})
        {for(uint i=0;i<count;i++)if(TextNative.hb_ot_layout_table_find_script(face,table,tags[i],out _)!=0){supported=true;break;}if(supported)break;}
        TextNative.hb_face_destroy(face);return supported;
    }
    internal static bool HasLanguage(TextFontFaceImpl font,nint language)
    {
        if(font.Face==0||language==0)return false;nint face=TextNative.hb_ft_face_create_referenced(font.Face);
        uint* tags=stackalloc uint[3];new Span<uint>(tags,3).Clear();uint count=3,scripts=0;
        TextNative.hb_ot_tags_from_script_and_language(AxisTag('Z','z','z','z'),language,ref scripts,null,ref count,tags);
        bool supported=false;
        foreach(uint table in new uint[]{Gsub,Gpos})
        {
            uint total=TextNative.hb_ot_layout_table_get_script_tags(face,table,0,null,null);
            for(uint i=0;i<total;i++)if(TextNative.hb_ot_layout_script_select_language(face,table,i,count,tags,out _)!=0){supported=true;break;}
            if(supported)break;
        }
        TextNative.hb_face_destroy(face);return supported;
    }
}
internal sealed unsafe partial class TextFontFaceImpl : FontFace,IDisposable
{
    internal TextServicesImpl? Services;
    internal TextFontFaceId IdValue;
    internal FontProvider? ProviderRef;
    internal FontProviderFaceSource SourceData,QueryIdentity;
    internal TextFontAsset? Asset;
    internal nint Face,RasterDraw,RasterPaint;
    internal readonly Dictionary<uint,TextFontSizeCache> SizeCaches=[];
    internal Utf8StringView Family,StyleNameValue;
    internal List<FontVariationCoordinate> Coordinates=[];
    internal bool VariationCoordinatesOverridden;
    internal readonly List<FontOpenTypeFeatureValue> FeatureOverrides=[];
    internal readonly List<TextNamedSupportOverride> LanguageOverrides=[],ScriptOverrides=[];
    internal readonly List<SRGBColor> CustomPalette=[];
    internal uint SelectedPalette;
    internal float EmboldenValue,BaselineOffsetValue;
    internal readonly float[] SpacingValues=new float[4];
    internal Float3x3 TransformValue=Float3x3.Identity();
    internal EFontWeight WeightValue;
    internal EFontStyle FontStyleValue;
    internal EFontStretch StretchValue;
    internal TextFontRasterConfig RasterConfig=new();
    internal bool RasterConfigOverridden;
    internal ulong GlyphOverrideCount,Revision=1;
    internal TextFontFaceImpl(TextServicesImpl services,TextFontFaceId id,FontProvider provider,FontProviderFaceSource source,TextFontAsset asset,nint face)
    {Services=services;IdValue=id;ProviderRef=provider;SourceData=QueryIdentity=source;Asset=asset;Face=face;WeightValue=source.Weight;FontStyleValue=source.Style;StretchValue=source.Stretch;}
    public void Dispose()
    {
        ClearSizeCaches();
        if(RasterPaint!=0){TextNative.hb_raster_paint_destroy(RasterPaint);RasterPaint=0;}
        if(RasterDraw!=0){TextNative.hb_raster_draw_destroy(RasterDraw);RasterDraw=0;}
        if(Face!=0){TextNative.FT_Done_Face(Face);Face=0;}
    }
    public override bool IsValid()=>IdValue.IsValid()&&ProviderRef!=null&&SourceData.IsValid()&&Face!=0;
    public override TextFontFaceId Id()=>IdValue;
    public override FontProvider Provider()=>ProviderRef!;
    public override ref readonly FontProviderFaceSource Source()=>ref SourceData;
    public override uint FaceIndex()=>SourceData.FaceIndex;
    public override bool SetFaceIndex(uint value)
    {
        if(Services==null||value==SourceData.FaceIndex||Asset?.Data?.Bytes==null)return value==SourceData.FaceIndex;
        var query=new FontFaceQuery(ProviderRef,SourceData with{FaceIndex=value});
        var asset=Services.LoadFontAsset(query);if(asset?.Data?.Bytes==null)return false;
        if(TextNative.FT_New_Memory_Face(Services.Library,(byte*)asset.Data.BytesPointer,asset.Data.Bytes.Length,(int)value,out nint face)!=0)return false;
        if(!ApplyVariationCoordinates(face)){TextNative.FT_Done_Face(face);return false;}
        ClearSizeCaches();TextNative.FT_Done_Face(Face);Face=face;Asset=asset;SourceData.FaceIndex=value;
        QueryIdentity=new();Services.SystemFallbackCache.Clear();PreloadMetadata();NotifyModified();return true;
    }
    public override uint FaceCount()=>Face!=0?(uint)Math.Max(0,TextNative.Face(Face).NumFaces):0;
    public override Utf8StringView FamilyName()=>Family;
    public override void SetFamilyName(Utf8StringView v)=>Family=new(v.Bytes.ToArray());
    public override Utf8StringView StyleName()=>StyleNameValue;
    public override void SetStyleName(Utf8StringView v)=>StyleNameValue=new(v.Bytes.ToArray());
    public override EFontWeight Weight()=>WeightValue;
    public override void SetWeight(EFontWeight v)=>WeightValue=v;
    public override EFontStyle Style()=>FontStyleValue;
    public override void SetStyle(EFontStyle v)=>FontStyleValue=v;
    public override EFontStretch Stretch()=>StretchValue;
    public override void SetStretch(EFontStretch v)=>StretchValue=v;
    public override bool IsFixedWidth()=>Face!=0&&(TextNative.Face(Face).FaceFlags&TextNative.FT_FACE_FLAG_FIXED_WIDTH)!=0;
    public override ReadOnlySpan<FontOpenTypeName> OpenTypeNames()=>Asset==null?[]:CollectionsMarshal.AsSpan(Asset.Names);
    public override ReadOnlySpan<FontVariationAxis> VariationAxes()=>Asset==null?[]:CollectionsMarshal.AsSpan(Asset.Axes);
    public override ReadOnlySpan<FontVariationCoordinate> VariationCoordinates()=>CollectionsMarshal.AsSpan(Coordinates);
    public override bool SetVariationCoordinates(ReadOnlySpan<FontVariationCoordinate> values)
    {
        if(Face==0)return false;List<FontVariationCoordinate> candidate=[];
        foreach(var v in values)
        {
            bool replaced=false;
            for(int i=0;i<candidate.Count;i++)if(candidate[i].Tag==v.Tag){candidate[i]=v;replaced=true;break;}
            if(!replaced)candidate.Add(v);
        }
        if(candidate.Count>1)candidate.Sort((a,b)=>a.Tag.CompareTo(b.Tag));
        // C++ float equality treats NaN as unequal, including in coordinate arrays.
        bool equal=Coordinates.Count==candidate.Count;
        for(int i=0;equal&&i<Coordinates.Count;i++)equal=Coordinates[i].Tag==candidate[i].Tag&&Coordinates[i].Value==candidate[i].Value;
        if(VariationCoordinatesOverridden&&equal)return true;
        var previous=Coordinates;bool wasOverridden=VariationCoordinatesOverridden;Coordinates=candidate;VariationCoordinatesOverridden=true;
        if(!ApplyVariationCoordinates()){Coordinates=previous;VariationCoordinatesOverridden=wasOverridden;ApplyVariationCoordinates();return false;}
        NotifyModified();return true;
    }
    public override ReadOnlySpan<FontOpenTypeFeature> OpenTypeFeatures()=>Asset==null?[]:CollectionsMarshal.AsSpan(Asset.Features);
    public override ReadOnlySpan<FontOpenTypeFeatureValue> OpenTypeFeatureOverrides()=>CollectionsMarshal.AsSpan(FeatureOverrides);
    public override void SetOpenTypeFeatureOverrides(ReadOnlySpan<FontOpenTypeFeatureValue> values)
    {FeatureOverrides.Clear();foreach(var v in values)FeatureOverrides.Add(v);NotifyLayoutModified();}
    private static int FindSupport(List<TextNamedSupportOverride> values,Utf8StringView name)=>values.FindIndex(v=>v.Name==name);
    private static TextNamedSupportOverride? FindSupportDefault(List<TextNamedSupportOverride> values,Utf8StringView name)
    {int i=FindSupport(values,name);if(i<0)i=FindSupport(values,"*");return i<0?null:values[i];}
    private void SetSupport(List<TextNamedSupportOverride> values,Utf8StringView name,bool supported)
    {int i=FindSupport(values,name);if(i<0)values.Add(new(new Utf8StringView(name.Bytes.ToArray()),supported));else values[i]=new(new Utf8StringView(name.Bytes.ToArray()),supported);NotifyLayoutModified();}
    private void RemoveSupport(List<TextNamedSupportOverride> values,Utf8StringView name)
    {int i=FindSupport(values,name);if(i>=0){values.RemoveAt(i);NotifyLayoutModified();}}
    public override bool IsLanguageSupported(Utf8StringView language)
    {
        if(FindSupportDefault(LanguageOverrides,language) is {} value)return value.Supported;
        fixed(byte* p=language.Bytes)return TextFontSupport.HasLanguage(this,TextNative.hb_language_from_string(p,(int)language.Size()));
    }
    public override bool LanguageSupportOverride(Utf8StringView language)
    {int i=FindSupport(LanguageOverrides,language);return i>=0&&LanguageOverrides[i].Supported;}
    public override void SetLanguageSupportOverride(Utf8StringView v,bool b)=>SetSupport(LanguageOverrides,v,b);
    public override void RemoveLanguageSupportOverride(Utf8StringView v)=>RemoveSupport(LanguageOverrides,v);
    public override void LanguageSupportOverrides(List<Utf8StringView> values){values.Clear();foreach(var v in LanguageOverrides)values.Add(new Utf8StringView(v.Name.Bytes.ToArray()));}
    public override bool IsScriptSupported(Utf8StringView script)
    {
        if(FindSupportDefault(ScriptOverrides,script) is {} value)return value.Supported;
        fixed(byte* p=script.Bytes)return TextFontSupport.HasScript(this,TextNative.hb_script_from_string(p,(int)script.Size()));
    }
    public override bool ScriptSupportOverride(Utf8StringView script)
    {int i=FindSupport(ScriptOverrides,script);return i>=0&&ScriptOverrides[i].Supported;}
    public override void SetScriptSupportOverride(Utf8StringView v,bool b)=>SetSupport(ScriptOverrides,v,b);
    public override void RemoveScriptSupportOverride(Utf8StringView v)=>RemoveSupport(ScriptOverrides,v);
    public override void ScriptSupportOverrides(List<Utf8StringView> values){values.Clear();foreach(var v in ScriptOverrides)values.Add(new Utf8StringView(v.Name.Bytes.ToArray()));}
    internal void ClearSizeCaches(){foreach(var c in SizeCaches.Values)c.Dispose();SizeCaches.Clear();GlyphOverrideCount=0;}
    internal void NotifyModified(){if(Services==null)return;ClearSizeCaches();Revision++;Services.FontStateRevisionValue++;Services.ClearAtlases();}
    internal void NotifyLayoutModified(){if(Services==null)return;Revision++;Services.FontStateRevisionValue++;}
    internal void NotifyRasterModified(){Services?.ClearAtlases();}
}
