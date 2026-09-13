using System.Reflection;
namespace SkrGui.Tests;
internal static class TestFontAssets
{
    private static readonly Dictionary<string,byte[]> Cache=[];
    internal static byte[] Get(string name)
    {
        if(Cache.TryGetValue(name,out var bytes))return bytes;
        using var stream=typeof(TestFontAssets).Assembly.GetManifestResourceStream("SkrGui.Tests.Text.Assets."+name+".font")??throw new InvalidOperationException("Missing original fixture font: "+name);
        using var memory=new MemoryStream();stream.CopyTo(memory);bytes=memory.ToArray();Cache.Add(name,bytes);return bytes;
    }
}
internal sealed class EmbeddedFontSource
{
    public Utf8StringView Family,SourceKey;public byte[] Data=[];public uint FaceIndex;
    public EFontWeight Weight=EFontWeight.Regular;public EFontStyle Style=EFontStyle.Normal;public EFontStretch Stretch=EFontStretch.Normal;
}
internal sealed class EmbeddedTextFontProvider:FontProvider
{
    private readonly List<EmbeddedFontSource> _sources=[];
    internal void AddFace(EmbeddedFontSource source)=>_sources.Add(source);
    public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource result)
    {
        result=new();foreach(var s in _sources)if(s.Family==family){result=new(){SourceKey=s.SourceKey,FaceIndex=s.FaceIndex,Weight=s.Weight,Style=s.Style,Stretch=s.Stretch};return true;}return false;
    }
    public override bool LoadFaceData(FontProviderFaceSource source,List<byte> output)
    {output.Clear();foreach(var s in _sources)if(s.SourceKey==source.SourceKey){output.AddRange(s.Data);return true;}return false;}
}
