using System.Security.Cryptography;
namespace SkrGui.Tests;
// Source: tests/text/text_contract_fixture.hpp.
internal sealed class TextContractFixture:IDisposable
{
    public TextServices Services=null!;
    public EmbeddedTextFontProvider Provider=null!;
    private static readonly Lazy<byte[]> IcuBytes=new(()=>{
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir!=null;dir=dir.Parent)
            foreach(string name in new[]{"icudt72l.dat","native/artifacts/win-x64/icudt72l.dat"})
            {string p=Path.Combine(dir.FullName,name);if(File.Exists(p))return File.ReadAllBytes(p);}
        throw new FileNotFoundException("Pinned ICU 72.1 test data is missing.");
    });
    internal static byte[] IcuData()=>IcuBytes.Value;
    internal static EmbeddedTextFontProvider MakeProvider()
    {
        var result=new EmbeddedTextFontProvider();
        void Add(string key,string family,uint index=0)
        {
            byte[] bytes=TestFontAssets.Get(key);string hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string source=key=="ColorBitmap"?"embedded://text-contract/color-bitmap/sbix-7x3":"embedded://text-contract/"+key.ToLowerInvariant()+"/"+hash;
            result.AddFace(new(){Family=family,SourceKey=source,Data=bytes,FaceIndex=index});
        }
        foreach(string key in new[]{"Latin","Hebrew","Arabic","Thai","Emoji"})Add(key,"Skr Test "+key);
        Add("Collection","Skr Test Collection Latin");Add("Collection","Skr Test Collection Hebrew",1);
        Add("Color","Skr Test Color");Add("ColorBitmap","Skr Test Color Bitmap");return result;
    }
    internal static TextContractFixture Make(bool advanced,bool withIcu=true)
    {
        var desc=new TextServicesDesc(){AddSystemFontProvider=false,IcuData=withIcu?IcuData():null};
        var result=new TextContractFixture(){Services=advanced?TextServices.CreateAdvanced(desc):TextServices.CreateFallback(desc),Provider=MakeProvider()};
        result.Services.AddFontProvider(result.Provider);return result;
    }
    internal static TextStyle MakeStyle(string families,float size=16)=>new(){FontFamilies=families,FontSize=size};
    internal static FontFace? PreloadFamily(TextServices services,string family)
    {
        List<FontFaceQuery> queries=[];if(!services.QueryFontFaces(MakeStyle(family),queries)||queries.Count==0)return null;
        return services.PreloadFontFace(queries[0]);
    }
    public void Dispose()=>Services.Dispose();
}
