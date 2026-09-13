namespace SkrGui.Tests;
public static partial class TextBaselineCommonTests
{
    private sealed record StripCase(Utf8StringView Text,string Expected);
    private sealed record BreakCase(Utf8StringView Text,ETextLineBreakFlag Flags,TextRange[] Expected);
    private sealed class LazyFallbackFontProvider : FontProvider
    {
        internal uint PrimaryQueries,SecondaryQueries,PrimaryLoads,SecondaryLoads;
        public override bool QueryFace(Utf8StringView family,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource output)
        {output=new();string source;if(family=="Primary"){PrimaryQueries++;source="embedded://lazy-fallback/primary";}else if(family=="Secondary"){SecondaryQueries++;source="embedded://lazy-fallback/secondary";}else return false;output=new(){SourceKey=source,Weight=weight,Style=style,Stretch=stretch};return true;}
        public override bool LoadFaceData(FontProviderFaceSource source,List<byte> output)
        {output.Clear();if(source.SourceKey=="embedded://lazy-fallback/primary"){PrimaryLoads++;output.AddRange(TestFontAssets.Get("Latin"));}else if(source.SourceKey=="embedded://lazy-fallback/secondary"){SecondaryLoads++;output.AddRange(TestFontAssets.Get("Hebrew"));}else return false;return true;}
    }
    private sealed class SingleFontProvider(string family,string sourceKey,byte[]? data,ulong size) : FontProvider
    {
        internal uint QueryCount,LoadCount;
        public override bool QueryFace(Utf8StringView query,EFontWeight weight,EFontStyle style,EFontStretch stretch,out FontProviderFaceSource output)
        {QueryCount++;output=new();if(query!=family)return false;output=new(){SourceKey=sourceKey,Weight=weight,Style=style,Stretch=stretch};return true;}
        public override bool LoadFaceData(FontProviderFaceSource source,List<byte> output)
        {output.Clear();if(source.SourceKey!=sourceKey)return false;LoadCount++;if(data is null||size==0)return false;output.AddRange(data.AsSpan(0,(int)size).ToArray());return true;}
    }
    private static void check_line_shape_invariants(TextLine line)
    {Check.That(line.IsReady());Check.Equal(line.GlyphCount(),(ulong)line.Glyphs().Length);Check.That(TextSourceTestHelpers.glyph_buffer_is_well_formed(line.Glyphs()));Check.That(line.Size().Height+.0001f>=line.LineAscent()+line.LineDescent());var logical=line.SortLogicalGlyphs();Check.Equal(logical.Length,line.Glyphs().Length);for(int i=1;i<logical.Length;i++)Check.That(logical[i-1].SourceRange.Start<=logical[i].SourceRange.Start);}
    private static void check_paragraph_line_invariants(TextParagraph paragraph)
    {Check.That(paragraph.IsReady());Check.Equal(paragraph.GlyphCount(),(ulong)paragraph.Glyphs().Length);Check.That(TextSourceTestHelpers.glyph_buffer_is_well_formed(paragraph.Glyphs()));ulong glyphEnd=0,sourceEnd=0;float height=0;for(ulong i=0;i<paragraph.LineCount();i++){var glyph=paragraph.LineGlyphRange(i);var source=paragraph.LineRange(i);Check.Equal(glyphEnd,glyph.Start);Check.That(source.Start>=sourceEnd);Check.That(glyph.End<=paragraph.GlyphCount()&&source.End<=paragraph.SourceRange().End);Check.That(paragraph.LineSize(i).Height+.0001f>=paragraph.LineAscent(i)+paragraph.LineDescent(i));glyphEnd=glyph.End;sourceEnd=source.End;height+=paragraph.LineSize(i).Height;}Check.Equal(paragraph.GlyphCount(),glyphEnd);Check.Equal(paragraph.SourceRange().End,sourceEnd);Check.Near(paragraph.Size().Height,height,.0001f);}
    private static void check_ranges(List<TextRange> actual,TextRange[] expected)=>Check.SequenceEqual(expected,actual);
}
