namespace SkrGui.Tests;
// Source: tests/text/font_provider_tests.cpp, plus native-host boundary checks.
internal static class FontProviderTests
{
    private sealed class TestFontProvider:FontProvider
    {
        internal string Family="";internal FontProviderFaceSource Source=new();
        public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource result)
        {if(family!=Family){result=new();return false;}result=Source;return true;}
    }
    private static string PrepareFont()
    {
        string folder=Path.Combine(Path.GetTempPath(),"skrgui-migration-tests");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"skr_gui_text_contract_latin_74545838.ttf");File.WriteAllBytes(path,TestFontAssets.Get("Latin"));return path;
    }
    [GuiTest("gui/font-provider/abstract-query-and-load")]
    private static void AbstractQueryAndLoad()
    {
        string path=PrepareFont();
        try
        {
            var p=new TestFontProvider(){Family="Unit Test Family",Source=new(){SourceKey=path,FaceIndex=0}};
            Check.That(p.QueryFace("Unit Test Family",EFontWeight.Medium,EFontStyle.Normal,EFontStretch.Normal,out var found));
            Check.Equal(p.Source,found);List<byte> data=[];Check.That(p.LoadFaceData(found,data));Check.That(data.Count!=0);
        }
        finally{File.Delete(path);}
    }
    [GuiTest("gui/font-provider/files-provider-indexes-font-file")]
    private static void IndexesFontFile()
    {
        string path=PrepareFont();
        try
        {
            var p=new FilesFontProvider();Check.That(p.AddFontFile(path));Check.Equal(1,p.FontFaces().Count);
            var f=p.FontFaces()[0];Check.That(f.IsValid());Check.That(f.Families.Count!=0);Check.Equal(path,f.Source.SourceKey);Check.Equal(0u,f.Source.FaceIndex);
            Check.That(p.QueryFace(f.Families[0],f.Weight,f.Style,f.Stretch,out var found));Check.That(found.IsValid());Check.That(found.SourceKey!=f.Source.SourceKey);
            Check.Equal(f.Source.FaceIndex,found.FaceIndex);Check.Equal(f.Weight,found.Weight);Check.Equal(f.Style,found.Style);Check.Equal(f.Stretch,found.Stretch);
            Check.That(p.QueryFace(f.Families[0],EFontWeight.UltraBlack,EFontStyle.Italic,EFontStretch.UltraExpanded,out found));
            Check.That(found.IsValid());Check.Equal(f.Source.FaceIndex,found.FaceIndex);Check.Equal(EFontWeight.UltraBlack,found.Weight);Check.Equal(EFontStyle.Italic,found.Style);Check.Equal(EFontStretch.UltraExpanded,found.Stretch);
            Check.False(p.QueryFace("Missing Family",EFontWeight.Regular,EFontStyle.Normal,EFontStretch.Normal,out found));Check.False(found.IsValid());
            p.ClearFontFiles();Check.Equal(0,p.FontFaces().Count);
        }
        finally{File.Delete(path);}
    }
    [GuiTest("host/windows-directwrite/full-cluster-fallback")]
    private static void WindowsFallback()
    {
        if(!OperatingSystem.IsWindows())return;using var p=new WindowsFontProvider();
        Check.That(p.QueryFace("Segoe UI",EFontWeight.Bold,EFontStyle.Italic,EFontStretch.Normal,out var face));Check.That(File.Exists(face.SourceKey.ToString()));Check.Equal(EFontWeight.Bold,face.Weight);
        Check.That(p.QueryFallback("Segoe UI",new uint[]{0x1f600},"en-US",EFontWeight.Regular,EFontStyle.Normal,EFontStretch.Normal,out var emoji));
        Check.That(File.Exists(emoji.SourceKey.ToString()));
        Check.False(p.QueryFallback("Segoe UI",ReadOnlySpan<uint>.Empty,"en-US",EFontWeight.Regular,EFontStyle.Normal,EFontStretch.Normal,out _));
        Check.False(p.QueryFace("SkrGui___NoSuchFamily_1281024",EFontWeight.Regular,EFontStyle.Normal,EFontStretch.Normal,out _));
    }
    [GuiTest("host/font-directory/malformed-and-collection")]
    private static void MalformedDirectory()
    {
        List<FilesFontProviderFace> faces=[];
        Check.False(FilesFontProviderParseHelper.ParseFile("bad",new byte[]{0,0,0,0},faces));Check.Equal(0,faces.Count);
        Check.That(FilesFontProviderParseHelper.ParseFile("collection",TestFontAssets.Get("Collection"),faces));Check.Equal(2,faces.Count);
        Check.Equal(0u,faces[0].Source.FaceIndex);Check.Equal(1u,faces[1].Source.FaceIndex);
        Check.That(faces[0].HasFamily("Skr Test Tiny Latin"));Check.That(faces[1].HasFamily("Skr Test Tiny Hebrew"));
        var broken=TestFontAssets.Get("Latin").ToArray();Array.Fill(broken,(byte)255,4,2);Check.False(FilesFontProviderParseHelper.ParseFile("bad",broken,faces));
    }
}
