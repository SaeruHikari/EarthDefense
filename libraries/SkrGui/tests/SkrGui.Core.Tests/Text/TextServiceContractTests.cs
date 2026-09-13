namespace SkrGui.Tests;
// Source: tests/text/text_service_contract_tests.cpp and text_types_tests.cpp.
internal static class TextServiceContractTests
{
    [GuiTest("gui/text-contract/service/features-and-font-provider-registry")]
    private static void FeaturesAndRegistry()
    {
        foreach(bool advanced in new[]{false,true})
        {
            using var f=TextContractFixture.Make(advanced);var s=f.Services;var features=s.Features();
            foreach(var bit in new[]{ETextFeature.SimpleLayout,ETextFeature.BidiLayout,ETextFeature.VerticalLayout,ETextFeature.Shaping,ETextFeature.KashidaJustification,ETextFeature.BreakIterators,ETextFeature.FontBitmap,ETextFeature.FontDynamic,ETextFeature.FontMSDF,ETextFeature.FontSystem,ETextFeature.FontVariable,ETextFeature.ContextSensitiveCaseConversion,ETextFeature.SupportData,ETextFeature.UnicodeIdentifiers,ETextFeature.UnicodeSecurity})Check.Equal((features&bit)!=0,s.HasFeature(bit));
            Check.Equal(1UL,s.FontProviderCount());Check.Same(f.Provider,s.FontProviderAt(0));var second=TextContractFixture.MakeProvider();s.AddFontProvider(second);Check.Equal(2UL,s.FontProviderCount());Check.Same(second,s.FontProviderAt(1));s.ClearFontProviders();Check.Equal(0UL,s.FontProviderCount());
        }
    }
    [GuiTest("gui/text-contract/service/provider-precedence-and-affinity")]
    private static void ProviderPrecedence()
    {
        using var s=TextServices.CreateAdvanced(new(){AddSystemFontProvider=false,IcuData=TextContractFixture.IcuData()});var first=new EmbeddedTextFontProvider();var second=new EmbeddedTextFontProvider();
        first.AddFace(new(){Family="Shared Test Family",SourceKey="embedded://precedence/first",Data=TestFontAssets.Get("Latin")});second.AddFace(new(){Family="Shared Test Family",SourceKey="embedded://precedence/second",Data=TestFontAssets.Get("Hebrew")});
        s.AddFontProvider(first);s.AddFontProvider(second);List<FontFaceQuery> queries=[];Check.That(s.QueryFontFaces(TextContractFixture.MakeStyle("Shared Test Family"),queries));Check.That(queries.Count!=0);Check.Same(first,queries[0].Provider);Check.Equal("embedded://precedence/first",queries[0].Source.SourceKey);var face=s.PreloadFontFace(queries[0]);Check.NotNull(face);Check.Same(first,face!.Provider());Check.That(face.HasCodepoint('A'));
        s.UnloadAllFontFaces();s.ClearFontProviders();s.AddFontProvider(second);s.AddFontProvider(first);queries.Clear();Check.That(s.QueryFontFaces(TextContractFixture.MakeStyle("Shared Test Family"),queries));Check.That(queries.Count!=0);Check.Same(second,queries[0].Provider);Check.Equal("embedded://precedence/second",queries[0].Source.SourceKey);
    }
    [GuiTest("gui/text-contract/service/preload-deduplicate-and-unload")]
    private static void PreloadDeduplicate()
    {
        foreach(bool advanced in new[]{false,true})
        {
            using var f=TextContractFixture.Make(advanced);var s=f.Services;var latin=TextContractFixture.PreloadFamily(s,"Skr Test Latin");Check.NotNull(latin);var style=TextContractFixture.MakeStyle("Skr Test Latin,Skr Test Hebrew");style.FontFaces.Add(latin!.Id());List<FontFace> faces=[];Check.That(s.PreloadFontFaces(style,faces));Check.Equal(2,faces.Count);Check.That(faces.Contains(latin));Check.Equal(2UL,s.FontFaceCount());Check.Equal(s.FontFaceAt(0)!.Id(),s.FontFace(s.FontFaceAt(0)!.Id())!.Id());List<FontFaceQuery> queries=[];Check.That(s.QueryFontFaces(TextContractFixture.MakeStyle("Skr Test Latin"),queries));Check.That(queries.Count!=0);Check.Same(latin,s.PreloadFontFace(queries[0]));var line=s.CreateLine();line.AddString("AB",TextContractFixture.MakeStyle("Skr Test Latin"));Check.That(line.Shape());s.UnloadAllFontFaces();Check.Equal(0UL,s.FontFaceCount());Check.False(line.IsReady());Check.That(line.Shape());Check.That(s.FontFaceCount()>0);
        }
    }
    [GuiTest("gui/text-contract/service/opentype-tag-roundtrip")]
    private static void OpenTypeTags(){using var f=TextContractFixture.Make(true);foreach(string name in new[]{"liga","kern","wght"}){uint tag=f.Services.OpenTypeNameToTag(name);Check.That(tag!=0);Check.Equal(name,f.Services.OpenTypeTagToName(tag));}}
    [GuiTest("gui/text-style/basic-fields")]
    private static void StyleFields(){TextStyle s=new();Check.Equal("",s.FontFamilies);Check.Equal(14f,s.FontSize);Check.Equal(EFontWeight.Regular,s.FontWeight);Check.Equal(EFontStyle.Normal,s.FontStyle);Check.Equal(EFontStretch.Normal,s.FontStretch);}
    [GuiTest("gui/text-style/font-family-fallback-chain")]
    private static void StyleFamily(){TextStyle s=new(){FontFamilies="Inter,Noto Sans CJK,Segoe UI Emoji"};Check.Equal("Inter,Noto Sans CJK,Segoe UI Emoji",s.FontFamilies);}
    [GuiTest("gui/text-types/layout-id-validity")]
    private static void SpanId(){TextLayoutSpanId invalid=new(),span=new(0);Check.False(invalid.IsValid());Check.That(span.IsValid());Check.Equal(ulong.MaxValue,invalid.Value);Check.Equal(0UL,span.Value);}
}
