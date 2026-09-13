using System.Reflection;
namespace SkrGui.Gallery;

public struct GalleryPalette
{
    public SRGBColor Background=new(),Surface=new(),SurfaceSubtle=new(),Border=new(),GridLine=new(),Ink=new(),Muted=new(),Blue=new(),Green=new(),Red=new(),Amber=new(),Violet=new(),Cyan=new();
    public GalleryPalette(){}
}
public struct GalleryTextAtlasConfig {public Sizei L8PageSize,Rgba8PageSize;public uint GlyphPadding=1;public GalleryTextAtlasConfig(){}}
public sealed class GalleryTextServices {public TextServices? Services;public float PaintPixelRatio;}
public struct GalleryMeshDrawOptions {public float TessellationFactor=1;public GalleryMeshDrawOptions(){}}
public static class GalleryShared
{
    public const float AutoPixelRatio=0,GeometryAaRadius=.75f;
    public const string RasterProbeFontFamily="Skr Mini Sans Raster Probe";
    public static float DefaultWindowScale=>OperatingSystem.IsMacOS()?.8f:1;
    internal static float Luminance(SRGBColor c)=>c.R*.2126f+c.G*.7152f+c.B*.0722f;
    public static bool IsAutoPixelRatio(float ratio)=>ratio<=0||!float.IsFinite(ratio);
    public static float SanitizePixelRatio(float ratio)=>IsAutoPixelRatio(ratio)?1:Math.Clamp(ratio,.25f,8);
    public static uint ScaledDimension(uint size,float ratio)=>Math.Max(1u,(uint)MathF.Round(size*SanitizePixelRatio(ratio),MidpointRounding.AwayFromZero));
    public static string TextAaModeName(EGalleryTextAAMode mode)=>mode switch{EGalleryTextAAMode.Gray=>"gray",EGalleryTextAAMode.GrayLcd=>"gray-lcd",EGalleryTextAAMode.Sdf=>"sdf",EGalleryTextAAMode.SdfLcd=>"sdf-lcd",_=>"auto"};
    // Source const char* inputs are byte views terminated by the first NUL.
    internal static Utf8StringView CString(Utf8StringView value)
    {int end=value.Bytes.IndexOf((byte)0);return end<0?value:value.Slice(0,end);}
    public static bool ParseTextAaMode(string? name,ref EGalleryTextAAMode mode)=>name!=null&&ParseTextAaMode((Utf8StringView)name,ref mode);
    public static bool ParseTextAaMode(Utf8StringView name,ref EGalleryTextAAMode mode)
    {
        name=CString(name);
        if(name=="auto")mode=EGalleryTextAAMode.Auto;
        else if(name=="gray")mode=EGalleryTextAAMode.Gray;
        else if(name=="gray-lcd")mode=EGalleryTextAAMode.GrayLcd;
        else if(name=="sdf")mode=EGalleryTextAAMode.Sdf;
        else if(name=="sdf-lcd")mode=EGalleryTextAAMode.SdfLcd;
        else return false;
        return true;
    }
    public static EGalleryTextAAMode EffectiveTextAaMode(EGalleryTextAAMode mode)=>mode==EGalleryTextAAMode.Auto?EGalleryTextAAMode.Sdf:mode;
    public static string DefaultFontFamilies()=>OperatingSystem.IsWindows()?
        "Skr Mini Sans,Source Han Sans SC,Source Han Sans CN,Noto Sans CJK SC,Noto Sans SC,Segoe UI,Segoe UI Emoji,Arial,SimSun":
        OperatingSystem.IsMacOS()?"Skr Mini Sans,Helvetica Neue,PingFang SC,Apple Color Emoji,Arial Unicode MS":
        "Skr Mini Sans,Noto Sans,Noto Sans CJK SC,Noto Color Emoji,DejaVu Sans,Arial";
    public static GalleryPalette DefaultPalette()=>new(){
        Background=Rgba(17,18,22),Surface=Rgba(27,29,35),SurfaceSubtle=Rgba(37,40,48),Border=Rgba(67,74,87),GridLine=Rgba(55,62,74),
        Ink=Rgba(230,234,242),Muted=Rgba(150,160,176),Blue=Rgba(83,132,205),Green=Rgba(67,159,116),Red=Rgba(207,91,82),
        Amber=Rgba(205,139,48),Violet=Rgba(153,107,205),Cyan=Rgba(59,162,187)};
    public static GalleryTextAtlasConfig DefaultTextAtlasConfig()=>new(){L8PageSize=new(1024,1024),Rgba8PageSize=new(512,512),GlyphPadding=1};
    public static void ApplyTextAtlasConfig(TextServices services,GalleryTextAtlasConfig c)
    {services.SetAtlasPageSize(ETextAtlasFormat.L8,c.L8PageSize);services.SetAtlasPageSize(ETextAtlasFormat.RGBA8,c.Rgba8PageSize);services.SetAtlasGlyphPadding(c.GlyphPadding);}
    internal static TextFontRasterConfig TextRasterConfig(EGalleryTextAAMode mode)
    {
        var c=new TextFontRasterConfig();
        switch(EffectiveTextAaMode(mode)){case EGalleryTextAAMode.GrayLcd:c.Mode=ETextRasterMode.Bitmap;c.LcdMode=true;break;
            case EGalleryTextAAMode.Gray:c.Mode=ETextRasterMode.Bitmap;break;case EGalleryTextAAMode.SdfLcd:c.Mode=ETextRasterMode.Sdf;c.LcdMode=true;break;default:c.Mode=ETextRasterMode.Sdf;break;}
        return c;
    }
    internal static bool TextAtlasConfigEqual(TextServices s,GalleryTextAtlasConfig c)=>s.AtlasPageSize(ETextAtlasFormat.L8)==c.L8PageSize&&s.AtlasPageSize(ETextAtlasFormat.RGBA8)==c.Rgba8PageSize&&s.AtlasGlyphPadding()==c.GlyphPadding;
    internal static bool TextPixelRatioEqual(float a,float b)=>a>0&&b>0&&MathF.Abs(SanitizePixelRatio(a)-SanitizePixelRatio(b))<=.001f;
    private static readonly Lazy<byte[]?> IcuData=new(LoadIcuData);
    private static byte[]? LoadIcuData()
    {
        // Same immutable ICU payload; only executable-relative asset location belongs to the Godot host.
        foreach(string start in new[]{AppContext.BaseDirectory,Environment.CurrentDirectory,Path.GetDirectoryName(typeof(GalleryShared).Assembly.Location)??""})
            for(var directory=new DirectoryInfo(start);directory!=null;directory=directory.Parent)
                foreach(string relative in new[]{"icudt72l.dat","native/artifacts/win-x64/icudt72l.dat"})
                {string file=Path.Combine(directory.FullName,relative);if(File.Exists(file))return File.ReadAllBytes(file);}
        System.Diagnostics.Trace.TraceError("Gallery failed to resolve ICU data from the binary directory.");return null;
    }
    public static bool PrepareTextServices(GalleryTextServices context,EGalleryTextAAMode mode)
    {
        var atlas=DefaultTextAtlasConfig();var config=TextRasterConfig(mode);
        if(context.Services==null)
        {
            byte[]? data=IcuData.Value;if(data==null)return false;
            var services=TextServices.CreateAdvanced(new(){AddSystemFontProvider=false,DefaultFontRasterConfig=config,IcuData=data});
            if(!services.HasFeature(ETextFeature.BreakIterators)){services.Dispose();System.Diagnostics.Trace.TraceError("Gallery failed to initialize ICU break iteration data.");return false;}
            ApplyTextAtlasConfig(services,atlas);services.AddFontProvider(new EmbeddedGalleryFontProvider());
            if(FontProvider.CreateSystem() is {} provider)services.AddFontProvider(provider);
            context.Services=services;context.PaintPixelRatio=0;
        }
        else if(context.Services.DefaultFontRasterConfig()!=config)
        {context.Services.SetDefaultFontRasterConfig(config);context.Services.ClearAtlases();context.PaintPixelRatio=0;}
        if(!TextAtlasConfigEqual(context.Services,atlas))ApplyTextAtlasConfig(context.Services,atlas);
        return context.Services!=null;
    }
    public static bool PrepareTextServices(GalleryTextServices context,EGalleryTextAAMode mode,float ratio)
    {
        if(!PrepareTextServices(context,mode))return false;float resolved=SanitizePixelRatio(ratio);
        if(!TextPixelRatioEqual(context.PaintPixelRatio,resolved)){context.Services!.ClearAtlases();context.PaintPixelRatio=resolved;}return true;
    }
    public static SRGBColor Rgba(byte r,byte g,byte b,byte a=255)=>new(r/255f,g/255f,b/255f,a/255f);
    public static uint PackColor(SRGBColor c){c=c.Clamped();return PackRgba8(c.Red8(),c.Green8(),c.Blue8(),c.Alpha8());}
    public static uint PackRgba8(byte r,byte g,byte b,byte a)=>(uint)r|((uint)g<<8)|((uint)b<<16)|((uint)a<<24);
    public static uint PackMeshColor(uint rgba,bool premultiply)
    {
        byte r=(byte)(rgba>>24),g=(byte)(rgba>>16),b=(byte)(rgba>>8),a=(byte)rgba;
        if(premultiply){r=(byte)(((uint)r*a+127)/255);g=(byte)(((uint)g*a+127)/255);b=(byte)(((uint)b*a+127)/255);}
        return PackRgba8(r,g,b,a);
    }
    public static string DefaultAtlasPngPath(string frame)
    {
        if(frame.Length==0)return "gallery.atlas.png";
        // Same lexical Path::basename/set_filename rules: preserve separators and hidden names.
        bool windows=OperatingSystem.IsWindows();bool rootNeedsSeparator=false;
        int body=windows?WindowsPathBody(frame,out rootNeedsSeparator):0;
        if(!windows)while(body<frame.Length&&frame[body]=='/')body++;
        bool IsSeparator(char c)=>c=='/'||(windows&&c=='\\');
        int filenameStart=frame.Length;
        if(body<frame.Length&&!IsSeparator(frame[^1]))
        {filenameStart=body;for(int i=body;i<frame.Length;i++)if(IsSeparator(frame[i]))filenameStart=i+1;}
        string stem=frame[filenameStart..];
        if(stem.Length!=0&&stem!="."&&stem!="..")
        {int dot=stem.LastIndexOf('.');if(dot>=(stem[0]=='.'?1:0))stem=stem[..dot];}
        if(stem.Length==0)stem="gallery";
        if(body==frame.Length&&rootNeedsSeparator&&!IsSeparator(frame[^1]))return frame+"\\"+stem+".atlas.png";
        return frame[..filenameStart]+stem+".atlas.png";
    }
    // Root/body extraction from PathWin::_parse(..., false); validation is not used by basename.
    private static int WindowsPathBody(string path,out bool rootNeedsSeparator)
    {
        rootNeedsSeparator=false;int length=path.Length;
        bool Separator(char c)=>c=='/'||c=='\\';
        bool Alpha(char c)=>(c>='A'&&c<='Z')||(c>='a'&&c<='z');
        int Skip(int start,bool canonical){while(start<length&&(canonical?path[start]=='\\':Separator(path[start])))start++;return start;}
        int Network(int start,bool canonical)
        {
            bool Sep(char c)=>canonical?c=='\\':Separator(c);
            int end=start;while(end<length&&!Sep(path[end]))end++;
            if(end==start||end==length)return length;
            int share=end+1;if(share>=length||Sep(path[share]))return length;
            end=share;while(end<length&&!Sep(path[end]))end++;
            return Skip(end,canonical);
        }
        if(path.StartsWith("\\\\?\\",StringComparison.Ordinal)||path.StartsWith("\\\\.\\",StringComparison.Ordinal))
        {
            const int start=4;
            if(start>=length){rootNeedsSeparator=true;return length;}
            if(length>=start+4&&path.AsSpan(start,3).Equals("UNC",StringComparison.OrdinalIgnoreCase)&&path[start+3]=='\\')
            {rootNeedsSeparator=true;return Network(start+4,true);}
            if(length>=start+2&&Alpha(path[start])&&path[start+1]==':')
                return length>start+2&&path[start+2]=='\\'?Skip(start+3,true):start+2;
            rootNeedsSeparator=true;int end=start;while(end<length&&path[end]!='\\')end++;return Skip(end,true);
        }
        if(path.StartsWith("\\Device\\",StringComparison.OrdinalIgnoreCase)||path.StartsWith("\\GLOBAL??\\",StringComparison.OrdinalIgnoreCase))
        {
            rootNeedsSeparator=true;int end=path.StartsWith("\\Device\\",StringComparison.OrdinalIgnoreCase)?8:10;
            while(end<length&&path[end]!='\\')end++;return Skip(end,true);
        }
        if(length>=2&&Separator(path[0])&&Separator(path[1])){rootNeedsSeparator=true;return Network(2,false);}
        if(Separator(path[0]))return 1;
        if(length>=2&&Alpha(path[0])&&path[1]==':')return length>=3&&Separator(path[2])?Skip(3,false):2;
        return 0;
    }

}
internal sealed class EmbeddedGalleryFontProvider:FontProvider
{
    private const string Source="embedded://skr-mini-sans/inter-v4.1/cfcb9c61a9a36bf8";
    private const string ProbeSource="embedded://skr-mini-sans-raster-probe/inter-v4.1/cfcb9c61a9a36bf8";
    private static readonly Lazy<byte[]> Font=new(()=>{
        using var stream=typeof(EmbeddedGalleryFontProvider).Assembly.GetManifestResourceStream("SkrGui.Godot.Assets.SkrMiniSans.ttf")??throw new InvalidOperationException("Missing original embedded Gallery font.");
        using var memory=new MemoryStream();stream.CopyTo(memory);return memory.ToArray();});
    public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource source)
    {
        source=new();
        if(family=="Skr Mini Sans")source.SourceKey=Source;else if(family==GalleryShared.RasterProbeFontFamily)source.SourceKey=ProbeSource;else return false;
        source.FaceIndex=0;source.Weight=weight;source.Style=style;source.Stretch=stretch;return true;
    }
    public override bool LoadFaceData(FontProviderFaceSource source,List<byte> data)
    {data.Clear();if(source.SourceKey!=Source&&source.SourceKey!=ProbeSource)return false;data.AddRange(Font.Value);return true;}
}
