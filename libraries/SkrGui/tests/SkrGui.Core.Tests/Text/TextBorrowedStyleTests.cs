namespace SkrGui.Tests;
internal static class TextBorrowedStyleTests
{
    [GuiTest("migration/text/const-style-borrow-observes-reassignment")]
    private static void BorrowedStyle()
    {
        using var fixture=TextContractFixture.Make(false);var style=TextContractFixture.MakeStyle("Skr Test Latin");style.FontFaces.Add(new(17));
        var line=fixture.Services.CreateLine();var span=line.AddString("A",style);var view=line.SpanStyle(span)!;var faces=view.FontFaces;var features=view.OpenTypeFeatures;
        Check.Equal(16f,view.FontSize);Check.Equal(new TextFontFaceId(17),faces[0]);Check.False(faces is IList<TextFontFaceId>);
        var edited=view.Copy();edited.FontSize=24;edited.FontFaces.Clear();edited.FontFaces.Add(new(29));edited.OpenTypeFeatures.Add(new(0x6c696761,0));
        Check.Equal(16f,view.FontSize);Check.Equal(new TextFontFaceId(17),faces[0]);Check.That(line.UpdateSpanStyle(span,edited));Check.Equal(24f,view.FontSize);Check.Equal(new TextFontFaceId(29),faces[0]);Check.Equal(1,features.Count);
        var paragraph=fixture.Services.CreateParagraph();var pspan=paragraph.AddString("A",style);var pview=paragraph.SpanStyle(pspan)!;Check.That(paragraph.UpdateSpanStyle(pspan,edited));Check.Equal(24f,pview.FontSize);Check.Equal(new TextFontFaceId(29),pview.FontFaces[0]);
        var visual=new VisualTempText();visual.SetTextStyle(style);var vview=visual.TextStyle();var vfaces=vview.FontFaces;visual.SetTextStyle(edited);Check.Equal(24f,vview.FontSize);Check.Equal(new TextFontFaceId(29),vfaces[0]);var detachedCopy=vview.Copy();detachedCopy.FontSize=30;Check.Equal(24f,vview.FontSize);
    }
    [GuiTest("migration/text/opentype-tag-preserves-arbitrary-four-bytes")]
    private static void TagBytes()
    {
        using var service=TextServices.CreateFallback(new(){AddSystemFontProvider=false});
        foreach(uint tag in new uint[]{0,0xff80c001,0x61006200,0xf09f9880,0xffffffff})
        {
            var name=service.OpenTypeTagToName(tag);Check.Equal(4UL,name.Size());Check.Equal(tag,service.OpenTypeNameToTag(name));
            Check.Equal((byte)(tag>>24),name.Bytes[0]);Check.Equal((byte)tag,name.Bytes[3]);
        }
    }
}
