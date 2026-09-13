namespace SkrGui.Tests;
internal static class TextFontReferenceAuditTests
{
    [GuiTest("migration/text/font-cache-failure-preserves-reference-output")]
    private static void FailedMetrics()
    {
        using var fixture=TextContractFixture.Make(true);var service=(TextServicesImpl)fixture.Services;
        TextMetrics sentinel=new(){Ascent=123,Descent=31,LineHeight=154,Size=new(19,27)};TextMetrics metrics=sentinel;
        Check.False(service.FontBaseMetrics(new(),16,ref metrics,out float scale));Check.Equal(sentinel,metrics);Check.Equal(1f,scale);
        Check.False(service.FontMetrics(new(),16,ref metrics));Check.Equal(sentinel,metrics);
        TextGlyphMetrics glyphSentinel=new(){Font=new(199),Codepoint=83,GlyphIndex=91,FontSize=22,Advance=new(11,3),BitmapSize=new(23,13)};TextGlyphMetrics glyph=glyphSentinel;
        Check.False(service.GetExactGlyphMetrics(new(),65,16,ref glyph,out bool subpixel));Check.Equal(glyphSentinel,glyph);Check.False(subpixel);
        Check.False(service.GetGlyphMetrics(new(),65,0,16,ref glyph,out subpixel));Check.Equal(glyphSentinel,glyph);Check.False(subpixel);
        var face=(TextFontFaceImpl)TextContractFixture.PreloadFamily(service,"Skr Test Latin")!;
        Check.False(service.GetExactGlyphMetrics(face.Id(),0x10ffff,16,ref glyph));Check.Equal(glyphSentinel,glyph);
        var cache=service.EnsureFontSizeCache(face,16*64)!;Check.NotNull(cache);
        Check.False(service.LoadGlyphMetrics(face,cache,0,uint.MaxValue,ref glyph));Check.Equal(glyphSentinel,glyph);
        Check.False(service.GetGlyphMetrics(face.Id(),0,uint.MaxValue,16,ref glyph));Check.Equal(glyphSentinel,glyph);
        TextShapedData empty=new();TextLayoutSpanId span=new(713);Check.False(service.ResolveTrimGlyph(empty,0x2026,false,ref glyph,ref span));Check.Equal(glyphSentinel,glyph);Check.Equal(new TextLayoutSpanId(713),span);
    }
    [GuiTest("migration/text/bitmap-format-failure-retains-output")]
    private static void BitmapFormatFailure()
    {
        ETextAtlasBitmapFormat format=ETextAtlasBitmapFormat.BGRA;FT_Bitmap bitmap=new(){PixelMode=0xff};Check.False(TextAlgorithms.BitmapFormat(bitmap,ref format));Check.Equal(ETextAtlasBitmapFormat.BGRA,format);
        bitmap.PixelMode=2;Check.That(TextAlgorithms.BitmapFormat(bitmap,ref format));Check.Equal(ETextAtlasBitmapFormat.Gray,format);
    }
    [GuiTest("migration/text/font-source-const-borrow-observes-face-index")]
    private static void SourceBorrow()
    {
        using var fixture=TextContractFixture.Make(true);var face=TextContractFixture.PreloadFamily(fixture.Services,"Skr Test Collection Latin")!;Check.NotNull(face);
        ref readonly var source=ref face.Source();var copied=face.Source();Check.Equal(0u,source.FaceIndex);Check.That(face.SetFaceIndex(1));Check.Equal(1u,source.FaceIndex);Check.Equal(0u,copied.FaceIndex);
    }
    [GuiTest("migration/text/font-name-raw-bytes-and-owned-setters")]
    private static unsafe void RawNameBytes()
    {
        byte[] raw=[0x80,0xff,0,0x41];Utf8StringView decoded;
        fixed(byte* pointer=raw)
        {
            FT_SfntName name=new(){PlatformId=1,String=(nint)pointer,StringLength=(uint)raw.Length};
            decoded=TextFontSupport.DecodeSfntName(name);
        }
        Check.That(decoded.Bytes.SequenceEqual(raw));raw[0]=0x20;Check.Equal((byte)0x80,decoded.Bytes[0]);
        byte[] utf16=[0,0x41,0xd8,0x3d,0xde,0,0xff];
        fixed(byte* pointer=utf16)
        {FT_SfntName name=new(){PlatformId=3,String=(nint)pointer,StringLength=(uint)utf16.Length};Check.Equal((Utf8StringView)"A\U0001f600",TextFontSupport.DecodeSfntName(name));}
        using var fixture=TextContractFixture.Make(true);var face=TextContractFixture.PreloadFamily(fixture.Services,"Skr Test Latin")!;
        byte[] family=[0x41,0x80,0,0xff],style=[0xfe,0x53,0xc0];byte[] expectedFamily=family.ToArray(),expectedStyle=style.ToArray();
        face.SetFamilyName(new Utf8StringView(family));face.SetStyleName(new Utf8StringView(style));family[1]=0x20;style[0]=0x20;
        Check.That(face.FamilyName().Bytes.SequenceEqual(expectedFamily));Check.That(face.StyleName().Bytes.SequenceEqual(expectedStyle));
    }
    [GuiTest("migration/text/font-support-overrides-use-byte-identity")]
    private static void RawOverrideKeys()
    {
        using var fixture=TextContractFixture.Make(true);var face=TextContractFixture.PreloadFamily(fixture.Services,"Skr Test Latin")!;
        byte[] first=[0x80],second=[0xff];face.SetLanguageSupportOverride(new(first),true);face.SetLanguageSupportOverride(new(second),false);
        first[0]=0x20;Check.That(face.LanguageSupportOverride(new Utf8StringView(new byte[]{0x80})));Check.False(face.LanguageSupportOverride(new(second)));
        List<Utf8StringView> names=[];face.LanguageSupportOverrides(names);Check.Equal(2,names.Count);Check.Equal((byte)0x80,names[0].Bytes[0]);Check.Equal((byte)0xff,names[1].Bytes[0]);
        face.SetScriptSupportOverride(new Utf8StringView(new byte[]{0x80}),true);face.SetScriptSupportOverride(new(second),false);face.ScriptSupportOverrides(names);Check.Equal(2,names.Count);
        face.RemoveScriptSupportOverride(new(second));face.ScriptSupportOverrides(names);Check.Equal(1,names.Count);Check.Equal((byte)0x80,names[0].Bytes[0]);
    }
}
