namespace SkrGui.Tests;
// Source: tests/text/text_no_icu_fixture.hpp and all text_without_icu_*_tests.cpp @ 611561f8.
internal sealed class NoIcuFontProvider : FontProvider
{
    private string _sourceKey="embedded://text-no-icu/latin/v1";private uint _queries;
    public void MutateSourceKeySilently()=>_sourceKey="embedded://text-no-icu/latin/v2";
    public uint QueryCount()=>_queries;
    public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource output)
    {output=new();_queries++;if(family!="Skr Test Latin")return false;output=new(){SourceKey=_sourceKey,Weight=weight,Style=style,Stretch=stretch};return true;}
    public override bool LoadFaceData(FontProviderFaceSource source,List<byte> output)
    {output.Clear();if(source.SourceKey.Bytes.IndexOf("embedded://text-no-icu/latin/"u8)<0)return false;output.AddRange(TestFontAssets.Get("Latin"));return true;}
}
internal sealed class NoIcuTextFixture : IDisposable
{
    public readonly TextServices Services=TextServices.CreateAdvanced(new(){AddSystemFontProvider=false,IcuData=null});
    public readonly NoIcuFontProvider Provider=new();
    public NoIcuTextFixture()=>Services.AddFontProvider(Provider);
    public void Dispose()=>Services.Dispose();
}
internal static class TextWithoutIcuTests
{
    private static TextStyle Style(float size=16)=>new(){FontFamilies="Skr Test Latin",FontSize=size};
    private static bool GlyphsWellFormed(ReadOnlySpan<TextGlyph> glyphs)
    {
        for(int i=0;i<glyphs.Length;)
        {var head=glyphs[i];if(head.Count==0||i+head.Count>glyphs.Length||head.SourceRange.Start>head.SourceRange.End||!float.IsFinite(head.Advance)||!float.IsFinite(head.Offset.X)||!float.IsFinite(head.Offset.Y))return false;for(int j=1;j<head.Count;j++){var g=glyphs[i+j];if(g.Count!=0||g.SourceRange!=head.SourceRange)return false;}i+=(int)head.Count;}return true;
    }
    private static void CheckLine(TextLine line)
    {Check.That(line.IsReady());Check.Equal(line.GlyphCount(),(ulong)line.Glyphs().Length);Check.That(GlyphsWellFormed(line.Glyphs()));Check.That(float.IsFinite(line.Size().Width)&&float.IsFinite(line.Size().Height));Check.That(line.Size().Width>=0&&line.Size().Height>=0);var logical=line.SortLogicalGlyphs();Check.Equal(logical.Length,line.Glyphs().Length);for(int i=1;i<logical.Length;i++)Check.That(logical[i-1].SourceRange.Start<=logical[i].SourceRange.Start);}
    private static void CheckParagraph(TextParagraph paragraph)
    {
        Check.That(paragraph.IsReady());Check.Equal(paragraph.GlyphCount(),(ulong)paragraph.Glyphs().Length);Check.That(GlyphsWellFormed(paragraph.Glyphs()));Check.That(paragraph.LineCount()>=1);Check.That(float.IsFinite(paragraph.Size().Width)&&float.IsFinite(paragraph.Size().Height));ulong previousGlyphEnd=0,previousSourceEnd=0;
        for(ulong i=0;i<paragraph.LineCount();i++)
        {var glyphs=paragraph.LineGlyphRange(i);var source=paragraph.LineRange(i);Check.Equal(previousGlyphEnd,glyphs.Start);Check.That(source.Start>=previousSourceEnd);Check.That(glyphs.End<=paragraph.GlyphCount()&&source.End<=paragraph.SourceRange().End);Check.That(float.IsFinite(paragraph.LineWidth(i))&&float.IsFinite(paragraph.LineSize(i).Height));previousGlyphEnd=glyphs.End;previousSourceEnd=source.End;}
        Check.Equal(paragraph.GlyphCount(),previousGlyphEnd);Check.Equal(paragraph.SourceRange().End,previousSourceEnd);
    }
    private static bool RangesOrdered(List<TextRange> ranges,ulong length)
    {ulong previous=0;foreach(var range in ranges){if(range.Start>range.End||range.End>length||range.Start<previous)return false;previous=range.End;}return true;}
    private static bool BoundariesOrdered(List<ulong> values,ulong length)
    {ulong previous=0;foreach(ulong value in values){if(value<=previous||value>length)return false;previous=value;}return true;}
    [GuiTest("gui/text-no-icu/service/capabilities-and-factories")]
    private static void Capabilities()
    {
        using var f=new NoIcuTextFixture();var s=f.Services;Check.NotNull(s);Check.False(s.Name().IsEmpty());Check.Equal("advanced",s.ShortName());
        foreach(var feature in new[]{ETextFeature.SimpleLayout,ETextFeature.Shaping,ETextFeature.FontBitmap,ETextFeature.FontDynamic,ETextFeature.FontVariable})Check.That(s.HasFeature(feature));
        foreach(var feature in new[]{ETextFeature.BidiLayout,ETextFeature.BreakIterators,ETextFeature.ContextSensitiveCaseConversion,ETextFeature.UnicodeIdentifiers,ETextFeature.UnicodeSecurity,ETextFeature.FontSystem})Check.False(s.HasFeature(feature));
        Check.NotNull(s.CreateLine());Check.NotNull(s.CreateParagraph());
    }
    [GuiTest("gui/text-no-icu/service/font-query-preload-and-invalidation")]
    private static void FontQuery()
    {
        using var f=new NoIcuTextFixture();var s=f.Services;Check.Equal(1UL,s.FontProviderCount());Check.Same(f.Provider,s.FontProviderAt(0));List<FontFaceQuery> queries=[];Check.That(s.QueryFontFaces(Style(),queries));Check.That(queries.Count!=0);Check.That(queries[0].IsValid());var face=s.PreloadFontFace(queries[0]);Check.NotNull(face);Check.That(face!.Id().IsValid());Check.That(face.GlyphIndex('A')!=0);Check.That(face.Metrics(16).Ascent>0);
        var line=s.CreateLine();line.AddString("Font generation",Style());Check.That(line.Shape());Check.That(line.RunCount()>0);Utf8StringView first=line.RunFontFace(0)!.Source().SourceKey;uint queriesBefore=f.Provider.QueryCount();f.Provider.MutateSourceKeySilently();Check.That(line.IsReady());Check.False(line.Glyphs().IsEmpty);Check.That(line.Shape());Check.That(line.RunCount()>0);Check.Equal(first,line.RunFontFace(0)!.Source().SourceKey);Check.Equal(queriesBefore,f.Provider.QueryCount());CheckLine(line);s.ClearFontProviders();Check.Equal(0UL,s.FontProviderCount());Check.False(line.IsReady());Check.That(line.Shape());CheckLine(line);
    }
    [GuiTest("gui/text-no-icu/service/repeated-independent-lifetimes")]
    private static void Lifetimes()
    {for(uint i=0;i<8;i++){using var f=new NoIcuTextFixture();var line=f.Services.CreateLine();line.AddString("Repeated service",Style());Check.That(line.Shape());CheckLine(line);}}
    [GuiTest("gui/text-no-icu/line/shaping-scenario-matrix")]
    private static void ShapingMatrix()
    {
        using var f=new NoIcuTextFixture();
        foreach(var row in new (string Text,string Language)[]{("","en"),("Plain ASCII text","en"),("office affinity","en"),("Cafe\u0301","fr"),("A\tB","en"),("A\nB\r\nC","en"),("abc \u05e9\u05dc\u05d5\u05dd \u0627\u0644\u0639\u0631\u0628\u064a\u0629","ar"),("\U0001f469\u200d\U0001f4bb + \u2764\ufe0f","und"),("soft\u00adhyphen","en")})
        {var line=f.Services.CreateLine();line.AddString(row.Text,Style(),row.Language);line.TabAlign(new float[]{24,48});Check.That(line.Shape());CheckLine(line);var copy=line.Duplicate();Check.NotNull(copy);Check.That(copy.Shape());CheckLine(copy);Check.Equal(line.SourceRange(),copy.SourceRange());}
    }
    [GuiTest("gui/text-no-icu/line/direction-controls-and-missing-glyphs")]
    private static void DirectionControls()
    {using var f=new NoIcuTextFixture();foreach(var direction in new[]{ETextDirectionMode.Auto,ETextDirectionMode.LTR,ETextDirectionMode.RTL}){var line=f.Services.CreateLine();line.SetDirection(direction);line.SetPreserveControl(true);line.SetPreserveInvalid(true);line.AddString("Latin \u05e9\u05dc\u05d5\u05dd \U0001f600\u200d\U0001f4bb",Style(),"und");Check.That(line.Shape());Check.Equal(direction,line.Direction());Check.False(line.Glyphs().IsEmpty);CheckLine(line);}}
    [GuiTest("gui/text-no-icu/line/layout-objects-overrun-and-editing")]
    private static void ObjectsOverrunEditing()
    {
        using var f=new NoIcuTextFixture();var line=f.Services.CreateLine();line.AddString("Alpha\tBeta ",Style(),"en");Check.That(line.AddObject(42,new(12,16),ETextInlineAlignment.BaselineTo,0,11));line.AddString(" Gamma Delta",Style(),"en");Check.That(line.TabAlign(new float[]{32,64})>0);Check.That(line.Shape());Check.That(line.HasObject(42));Check.That(line.ObjectGlyphIndex(42)>=0);Check.Equal(new Sizef(12,16),line.ObjectRect(42).Size());
        line.SetMaxWidth(80);line.SetTextOverrunBehavior(ETextOverrunBehavior.TrimEllipsis);Check.That(line.Shape());CheckLine(line);Check.That(line.HasObject(42));Check.That(line.ObjectGlyphIndex(42)>=0);Check.That(line.VisibleCharacters()<=line.SourceRange().End);
        if(line.TrimPosition()!=ulong.MaxValue){Check.That(line.TrimPosition()<=line.GlyphCount());if(line.EllipsisPosition()!=ulong.MaxValue)Check.False(line.EllipsisGlyphs().IsEmpty);}
        List<TextRange> ranges=[];Check.That(line.LineBreaks(60,0,ETextLineBreakFlag.Mandatory|ETextLineBreakFlag.WordBound|ETextLineBreakFlag.GraphemeBound,ranges));Check.That(ranges.Count!=0);foreach(var range in ranges)Check.That(range.Start<=range.End&&range.End<=line.SourceRange().End);
        List<ulong> breaks=[];Check.That(line.CharacterBreaks(breaks));foreach(ulong boundary in breaks)Check.That(boundary<=line.SourceRange().End);var first=line.GraphemeBounds(0);Check.That(first.End>=first.Start);Check.That(line.HitTestPosition(first.Start)>=0);Check.That(line.HitTestGrapheme(first.Start)>=0);var caret=line.Caret(1);Check.That(caret.LeadingCaret.Height()>=0&&caret.TrailingCaret.Height()>=0);List<Rectf> rects=[];Check.That(line.SelectionRects(new(0,5),rects));Check.That(rects.Count!=0);
    }
    [GuiTest("gui/text-no-icu/paragraph/empty-wrap-lines-and-objects")]
    private static void ParagraphObjects()
    {
        using var f=new NoIcuTextFixture();var empty=f.Services.CreateParagraph();Check.That(empty.Shape());CheckParagraph(empty);Check.Equal(1UL,empty.LineCount());Check.Equal(0UL,empty.GlyphCount());var paragraph=f.Services.CreateParagraph();paragraph.SetMaxWidth(72);paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory|ETextLineBreakFlag.WordBound|ETextLineBreakFlag.GraphemeBound);paragraph.AddString("First line with words\nSecond ",Style(),"en");Check.That(paragraph.AddObject(7,new(10,14),ETextInlineAlignment.Center,0));paragraph.AddString(" line with \u05e9\u05dc\u05d5\u05dd and emoji \U0001f600",Style(),"und");Check.That(paragraph.Shape());CheckParagraph(paragraph);Check.That(paragraph.LineCount()>1);Check.That(paragraph.HasObject(7));Check.That(paragraph.ObjectGlyphIndex(7)>=0);
        bool found=false;for(ulong i=0;i<paragraph.LineCount();i++){List<ulong> objects=[];Check.That(paragraph.LineObjects(i,objects));foreach(ulong key in objects)found|=key==7;}Check.That(found);var copy=paragraph.Duplicate();Check.NotNull(copy);Check.That(copy.Shape());CheckParagraph(copy);
    }
    [GuiTest("gui/text-no-icu/unicode/local-degradation-remains-usable")]
    private static void LocalDegradation()
    {
        using var f=new NoIcuTextFixture();var s=f.Services;List<TextRange> words=[];s.StringWordBreaks("Hello, world\nagain","en",0,words);Check.That(words.Count!=0);Check.That(RangesOrdered(words,18));List<TextRange> wrapped=[];s.StringWordBreaks("one two three four","en",6,wrapped);Check.That(wrapped.Count>1);Check.That(RangesOrdered(wrapped,18));List<ulong> chars=[];s.StringCharacterBreaks("A\u0301B\U0001f600","und",chars);Check.That(chars.Count!=0);Check.That(BoundariesOrdered(chars,4));Check.Equal("HELLO 42",s.StringToUpper("Hello 42"));Check.Equal("hello 42",s.StringToLower("Hello 42"));Utf8StringView title=s.StringToTitle("hello world");Check.False(title.IsEmpty());Check.Equal(((Utf8StringView)"hello world").Size(),title.Size());Check.Equal((byte)'H',title.Bytes[0]);Check.Equal("Cafe",s.StripDiacritics("Caf\u00e9"));Check.That(s.IsValidLetter('A'));Check.False(s.IsValidLetter('7'));
    }
    [GuiTest("gui/text-no-icu/unicode/data-required-features-fail-safely")]
    private static void DataRequired()
    {
        using var f=new NoIcuTextFixture();var s=f.Services;Check.That(s.IsLocaleRightToLeft("ar"));Check.False(s.IsLocaleRightToLeft("en"));long confusable=s.IsConfusable("p\u0430ypal",new Utf8StringView[]{"paypal","example"});Check.That(confusable>=-1&&confusable<2);_=s.SpoofCheck("p\u0430ypal");Check.False(s.SpoofCheck("plain_ascii"));Check.That(s.IsValidIdentifier("valid_name"));Check.False(s.IsValidIdentifier("9invalid"));List<TextRange> words=[new(1,2)];s.StringWordBreaks(default,default,0,words);Check.That(words.Count==0);List<ulong> chars=[1];s.StringCharacterBreaks(default,default,chars);Check.That(chars.Count==0);Check.Equal(-1L,s.IsConfusable(default,[]));Check.False(s.SpoofCheck(default));Check.False(s.IsValidIdentifier(default));
    }
    [GuiTest("gui/text-no-icu/paint/atlas-text-and-missing-glyph")]
    private static void PaintMissingGlyph()
    {
        using var f=new NoIcuTextFixture();var s=f.Services;var paragraph=s.CreateParagraph();paragraph.SetMaxWidth(100);paragraph.AddString("Paint text\nmissing \U0001f600",Style(20),"und");Check.That(paragraph.Shape());CheckParagraph(paragraph);TextRenderResult result=new();Check.That(paragraph.Paint(new(3,5),result));Check.False(result.IsEmpty());Check.That(result.Commands.Count!=0&&result.Rects.Count!=0);bool atlasCommand=false,missing=false;foreach(var command in result.Commands){atlasCommand|=command.AtlasIndex!=uint.MaxValue;missing|=command.IsHexBoxFallback();}Check.That(atlasCommand&&missing);Check.That(s.AtlasCount()>0);bool covered=false;for(uint i=0;i<s.AtlasCount();i++)covered|=TextSourceTestHelpers.atlas_has_coverage(s.Atlas(i));Check.That(covered);TextRenderResult last=new();Check.That(paragraph.PaintLine(paragraph.LineCount()-1,Offsetf.Zero(),last));Check.False(last.IsEmpty());s.ClearAtlases();Check.Equal(0u,s.AtlasCount());TextRenderResult rebuilt=new();Check.That(paragraph.Paint(Offsetf.Zero(),rebuilt));Check.False(rebuilt.IsEmpty());
    }
}
