using System.Runtime.InteropServices;
namespace SkrGui;

// Source: font_face.cpp metadata preload, variations, palettes.
internal sealed unsafe partial class TextFontFaceImpl
{
    internal bool PreloadMetadata()
    {
        uint previous=SelectedPalette;SelectedPalette=0;if(Face==0||Asset==null)return false;
        TextNative.FT_Select_Charmap(Face,0x756e6963);
        if(Asset.MetadataReady)
        {
            Family=Asset.Family;StyleNameValue=Asset.StyleName;
            if(Asset.Palettes.Count!=0){SelectedPalette=Math.Min(previous,(uint)Asset.Palettes.Count-1);ApplyPalette();}
            return true;
        }
        Asset.Names.Clear();Asset.Axes.Clear();Asset.Features.Clear();Asset.PaletteNames.Clear();Asset.Palettes.Clear();
        Family=TextFontSupport.CopyNativeString(TextNative.Face(Face).FamilyName);StyleNameValue=TextFontSupport.CopyNativeString(TextNative.Face(Face).StyleName);
        Asset.Family=Family;Asset.StyleName=StyleNameValue;
        uint nameCount=TextNative.FT_Get_Sfnt_Name_Count(Face);
        for(uint i=0;i<nameCount;i++)
        {
            if(TextNative.FT_Get_Sfnt_Name(Face,i,out var n)!=0)continue;Utf8StringView value=TextFontSupport.DecodeSfntName(n);if(value.IsEmpty())continue;
            Asset.Names.Add(new(){Id=n.NameId,Language=$"{n.PlatformId}:{n.LanguageId:x4}",Value=value});
        }
        nint hbFace=TextNative.hb_ft_face_create_referenced(Face);
        if(hbFace!=0)
        {
            foreach(uint table in new uint[]{TextFontSupport.Gsub,TextFontSupport.Gpos})
            {
                uint total=TextNative.hb_ot_layout_table_get_feature_tags_raw(hbFace,table,0,null,null);
                var tags=new uint[total];uint count=total;
                fixed(uint* p=tags)TextNative.hb_ot_layout_table_get_feature_tags(hbFace,table,0,ref count,p);
                for(int i=0;i<count;i++)
                {
                    bool duplicate=false;foreach(var f in Asset.Features)duplicate|=f.Tag==tags[i];if(duplicate)continue;
                    Asset.Features.Add(new(){Tag=tags[i],Name=Services!.OpenTypeTagToName(tags[i]),DefaultValue=1,IsHidden=false});
                }
            }
            TextNative.hb_face_destroy(hbFace);
        }
        if(TextNative.FT_Get_MM_Var(Face,out nint axes)==0&&axes!=0)
        {
            var mm=(FT_MM_Var*)axes;var a=(FT_Var_Axis*)mm->Axis;
            for(uint i=0;i<mm->NumAxis;i++)Asset.Axes.Add(new(){Tag=a[i].Tag,Name=TextFontSupport.CopyNativeString(a[i].Name),
                MinValue=TextFontSupport.FixedToFloat(a[i].Minimum),MaxValue=TextFontSupport.FixedToFloat(a[i].Maximum),DefaultValue=TextFontSupport.FixedToFloat(a[i].Default)});
            TextNative.FT_Done_MM_Var(Services!.Library,axes);
        }
        if(TextNative.FT_Palette_Data_Get(Face,out var data)==0)
        {
            for(ushort i=0;i<data.NumPalettes;i++)
            {
                Utf8StringView name=default;
                if(data.PaletteNameIds!=0&&((ushort*)data.PaletteNameIds)[i]!=0xffff)
                {ushort id=((ushort*)data.PaletteNameIds)[i];foreach(var n in Asset.Names)if(n.Id==id){name=n.Value;break;}}
                Asset.PaletteNames.Add(name);List<SRGBColor> palette=[];
                if(TextNative.FT_Palette_Select(Face,i,out nint colors)==0&&colors!=0)
                {var p=(FT_Color*)colors;for(ushort j=0;j<data.NumPaletteEntries;j++)palette.Add(new(p[j].Red/255f,p[j].Green/255f,p[j].Blue/255f,p[j].Alpha/255f));}
                Asset.Palettes.Add(palette);
            }
            if(Asset.Palettes.Count!=0){SelectedPalette=Math.Min(previous,(uint)Asset.Palettes.Count-1);ApplyPalette();}
        }
        Asset.MetadataReady=true;return true;
    }
    internal bool ApplyVariationCoordinates(nint target=0)
    {
        if(target==0)target=Face;if(target==0||Services==null||Services.Library==0)return false;
        if(TextNative.FT_Get_MM_Var(target,out nint pointer)!=0||pointer==0)return true;
        var mm=(FT_MM_Var*)pointer;var axes=(FT_Var_Axis*)mm->Axis;var values=new int[mm->NumAxis];
        uint weightTag=TextFontSupport.AxisTag('w','g','h','t'),widthTag=TextFontSupport.AxisTag('w','d','t','h'),
            italicTag=TextFontSupport.AxisTag('i','t','a','l'),slantTag=TextFontSupport.AxisTag('s','l','n','t');
        bool hasItalic=false;for(uint i=0;i<mm->NumAxis;i++)hasItalic|=axes[i].Tag==italicTag;
        for(uint i=0;i<mm->NumAxis;i++)
        {
            var a=axes[i];double value=TextFontSupport.FixedToFloat(a.Default);
            if(VariationCoordinatesOverridden)
            {foreach(var c in Coordinates)if(c.Tag==a.Tag){value=c.Value;break;}}
            else if(a.Tag==weightTag)value=(int)SourceData.Weight;
            else if(a.Tag==widthTag)value=TextFontSupport.StretchAxisValue(SourceData.Stretch);
            else if(a.Tag==italicTag&&SourceData.Style==EFontStyle.Italic)value=1;
            else if(a.Tag==slantTag&&(SourceData.Style==EFontStyle.Oblique||(SourceData.Style==EFontStyle.Italic&&!hasItalic)))value=-12;
            double minimum=TextFontSupport.FixedToFloat(a.Minimum),maximum=TextFontSupport.FixedToFloat(a.Maximum);
            // std::clamp's ordered comparisons preserve a NaN value.
            value=value<minimum?minimum:maximum<value?maximum:value;values[i]=TextFontSupport.FontFixed(value);
        }
        int error;fixed(int* p=values)error=TextNative.FT_Set_Var_Design_Coordinates(target,mm->NumAxis,p);
        TextNative.FT_Done_MM_Var(Services.Library,pointer);return error==0;
    }
    internal bool ApplyPalette()
    {
        if(Face==0||Asset==null||Asset.Palettes.Count==0||SelectedPalette>=Asset.Palettes.Count)return CustomPalette.Count==0;
        if(TextNative.FT_Palette_Select(Face,(ushort)SelectedPalette,out nint pointer)!=0)return false;
        if(pointer==0)return CustomPalette.Count==0;
        var target=(FT_Color*)pointer;var selected=Asset.Palettes[(int)SelectedPalette];
        static FT_Color Convert(SRGBColor c)=>new(){Blue=c.Blue8(),Green=c.Green8(),Red=c.Red8(),Alpha=c.Alpha8()};
        for(int i=0;i<selected.Count;i++)target[i]=Convert(selected[i]);
        int count=Math.Min(CustomPalette.Count,selected.Count);
        for(int i=0;i<count;i++)
        {
            var c=CustomPalette[i];if(c.Red8()==0&&c.Green8()==0&&c.Blue8()==0&&c.Alpha8()==0)continue;target[i]=Convert(c);
        }
        return true;
    }
    public override bool HasColorGlyphs()=>Face!=0&&(TextNative.Face(Face).FaceFlags&TextNative.FT_FACE_FLAG_COLOR)!=0;
    public override uint PaletteCount()=>(uint)(Asset?.Palettes.Count??0);
    public override Utf8StringView PaletteName(uint index)=>Asset!=null&&index<Asset.PaletteNames.Count?Asset.PaletteNames[(int)index]:"";
    public override ReadOnlySpan<SRGBColor> PaletteColors(uint index)=>Asset!=null&&index<Asset.Palettes.Count?CollectionsMarshal.AsSpan(Asset.Palettes[(int)index]):[];
    public override uint UsedPalette()=>SelectedPalette;
    public override bool SetUsedPalette(uint index)
    {
        if(Asset==null||index>=Asset.Palettes.Count||Face==0)return false;
        if(TextNative.FT_Palette_Select(Face,(ushort)index,out _)!=0)return false;
        SelectedPalette=index;ApplyPalette();NotifyRasterModified();return true;
    }
    public override ReadOnlySpan<SRGBColor> CustomPaletteColors()=>CollectionsMarshal.AsSpan(CustomPalette);
    public override void SetCustomPaletteColors(ReadOnlySpan<SRGBColor> colors)
    {CustomPalette.Clear();foreach(var c in colors)CustomPalette.Add(c);ApplyPalette();NotifyRasterModified();}
}
