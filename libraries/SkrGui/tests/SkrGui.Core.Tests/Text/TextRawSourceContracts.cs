namespace SkrGui.Tests;
internal static class TextRawSourceContracts
{
    private sealed class RawFamilyProvider : FontProvider
    {
        public uint QueryCount;
        public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource source)
        {QueryCount++;source=new();if(family.Size()!=1||(family.Bytes[0]!=0x80&&family.Bytes[0]!=0xff))return false;source=new(){SourceKey=family,Weight=weight,Style=style,Stretch=stretch};return true;}
        public override bool LoadFaceData(FontProviderFaceSource source,List<byte> bytes){bytes.Clear();bytes.AddRange(TestFontAssets.Get("Latin"));return true;}
    }
    [GuiTest("migration/text/scalar-id-default-and-float-range-source-equality")]
    private static void TextScalarDefaultsAndEquality()
    {
        TextLayoutSpanId implicitDefault=default;
        Check.Equal(ulong.MaxValue,implicitDefault.Value);Check.That(!implicitDefault.IsValid());
        foreach(var value in new TextLayoutSpanId[3])Check.That(!value.IsValid());
        Check.Equal(new TextLayoutSpanId(),implicitDefault);Check.Equal(new TextLayoutSpanId(ulong.MaxValue),implicitDefault);
        Check.That(new TextLayoutSpanId(0).IsValid());Check.Equal(0UL,new TextLayoutSpanId(0).Value);
        Check.Equal(ulong.MaxValue-1,new TextLayoutSpanId(ulong.MaxValue-1).Value);
        TextFloatRange nanStart=new(float.NaN,1),nanStartCopy=nanStart,nanEnd=new(0,float.NaN),nanEndCopy=nanEnd;
        Check.That(nanStart!=nanStartCopy);Check.That(nanEnd!=nanEndCopy);Check.That(!nanStart.Equals(nanStartCopy));
        Check.That(new TextFloatRange(-0.0f,1)==new TextFloatRange(0.0f,1));
        Check.That(new TextFloatRange(float.PositiveInfinity,1)==new TextFloatRange(float.PositiveInfinity,1));
    }
    [GuiTest("migration/text/raw-family-chain-language-and-source-ownership")]
    private static void RawFamilyLanguage()
    {
        foreach(bool advanced in new[]{false,true})
        {
            using var services=advanced?TextServices.CreateAdvanced(new(){AddSystemFontProvider=false,IcuData=TextContractFixture.IcuData()}):TextServices.CreateFallback(new(){AddSystemFontProvider=false,IcuData=TextContractFixture.IcuData()});
            RawFamilyProvider provider=new();services.AddFontProvider(provider);
            byte[] families=[(byte)' ',(byte)'\t',0x80,(byte)',',(byte)' ',0xff,(byte)'\t'];TextStyle style=new(){FontFamilies=new(families),FontSize=16};families[2]=0x81;
            List<FontFaceQuery> queries=[];Check.That(services.QueryFontFaces(style,queries));Check.Equal(2,queries.Count);Check.Equal((byte)0x80,queries[0].Source.SourceKey.Bytes[0]);Check.Equal((byte)0xff,queries[1].Source.SourceKey.Bytes[0]);Check.Equal(2u,provider.QueryCount);
            Check.That(services.QueryFontFaces(style,queries));Check.Equal(2u,provider.QueryCount);
            byte[] language=[(byte)'x',0x80,0xff];byte[] expected=language.ToArray();var line=services.CreateLine();var id=line.AddString("A",style,new(language));language[1]=0x20;
            Check.That(line.SpanLanguage(id).Bytes.SequenceEqual(expected));Check.That(line.Shape());Check.That(line.RunLanguage(0).Bytes.SequenceEqual(expected));
            var duplicate=line.Duplicate();Check.That(duplicate.SpanLanguage(id).Bytes.SequenceEqual(expected));Check.That(duplicate.Shape());Check.That(duplicate.RunLanguage(0).Bytes.SequenceEqual(expected));
            var paragraph=services.CreateParagraph();var span=paragraph.AddString("A",style,new(expected));expected[1]=0x21;Check.Equal((byte)0x80,paragraph.SpanLanguage(span).Bytes[1]);Check.That(paragraph.Shape());Check.Equal((byte)0x80,paragraph.RunLanguage(0).Bytes[1]);
            var visual=new VisualTempText();byte[] visualFamily=[0x80];visual.SetFontFamilies(new(visualFamily));visualFamily[0]=0xff;Check.Equal((byte)0x80,visual.FontFamilies().Bytes[0]);
            byte[] widgetText=[0xff];var widget=new TempText(){Text=new(widgetText)};widgetText[0]=0x80;Check.Equal((byte)0xff,widget.Text.Bytes[0]);
            widget.TextStyle=style;style.FontSize=27;style.FontFaces.Add(new(613));Check.Equal(16f,widget.TextStyle.FontSize);Check.Equal(0,widget.TextStyle.FontFaces.Count);widget.TextStyle.FontSize=18;Check.Equal(18f,widget.TextStyle.FontSize);
        }
    }
    [GuiTest("migration/text/unicode-service-keeps-utf8-result-and-dictionary-boundaries")]
    private static void UnicodeRawBoundaries()
    {
        using var fixture=TextContractFixture.Make(true);var service=fixture.Services;
        Utf8StringView upper=service.StringToUpper("\u00e9");Check.Equal(2UL,upper.Size());Check.That(upper.Bytes.SequenceEqual(new byte[]{0xc3,0x89}));
        // ICU 72.1 skeleton conversion replaces malformed UTF8 here (observed original C ABI), unlike strict semantic decoding.
        Check.Equal(0L,service.IsConfusable("\ufffd",new Utf8StringView[]{new(new byte[]{0xff})}));
        Check.Equal(0L,service.IsConfusable("\ufffd",new Utf8StringView[]{"\ufffd"}));
    }
}
