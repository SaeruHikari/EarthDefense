namespace SkrGui.Tests;
// Source: tests/text/text_baseline_fixture.hpp.
internal sealed class EmbeddedLatinFontProvider(string sourceKey="embedded://text-baseline/latin/v1") : FontProvider
{
    private string _sourceKey=sourceKey;private uint _queryCount;
    internal void MutateSourceKeySilently()=>_sourceKey="embedded://text-baseline/latin/v2";
    internal uint QueryCount()=>_queryCount;
    public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource output)
    {output=new();_queryCount++;if(family!="Skr Test Latin")return false;output=new(){SourceKey=_sourceKey,Weight=weight,Style=style,Stretch=stretch};return true;}
    public override bool LoadFaceData(FontProviderFaceSource source,List<byte> output)
    {output.Clear();if(source.SourceKey.Bytes.IndexOf("embedded://text-baseline/latin/"u8)<0)return false;output.AddRange(TestFontAssets.Get("Latin"));return true;}
}
internal sealed class TextFixture : IDisposable
{
    internal TextServices Services=null!;
    internal EmbeddedLatinFontProvider Provider=null!;
    public void Dispose()=>Services.Dispose();
}
internal static class TextBaselineFixture
{
    internal static TextFixture MakeTextFixture(bool advanced)
    {var desc=new TextServicesDesc(){AddSystemFontProvider=false,IcuData=TextContractFixture.IcuData()};TextFixture result=new(){Services=advanced?TextServices.CreateAdvanced(desc):TextServices.CreateFallback(desc),Provider=new()};result.Services.AddFontProvider(result.Provider);return result;}
    internal static TextStyle MakeLatinStyle(float size=16)=>new(){FontFamilies="Skr Test Latin",FontSize=size};
    internal static FontFace? PreloadLatin(TextServices services)
    {List<FontFaceQuery> queries=[];return services.QueryFontFaces(MakeLatinStyle(),queries)&&queries.Count!=0?services.PreloadFontFace(queries[0]):null;}
}
