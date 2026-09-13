using static SkrGui.Tests.TextBaselineFixture;
using static SkrGui.Tests.TextSourceTestHelpers;
namespace SkrGui.Tests;
// All cases/loops/inputs from tests/text/text_baseline_common_tests.cpp @ 611561f8.
public static partial class TextBaselineCommonTests {
private static readonly bool[] kBackends = [false,true];
private static void Eq<T>(T a,T b)=>Check.Equal(a,b);
private static void Eq(ulong a,uint b)=>Check.Equal(a,(ulong)b);
private static void Eq(long a,int b)=>Check.Equal(a,(long)b);
private static void Eq(Utf8StringView a,string b)=>Check.Equal(a.ToString(),b);
private static void Eq(uint a,char b)=>Check.Equal(a,(uint)b);
private static void Ne<T>(T a,T b)=>Check.That(!EqualityComparer<T>.Default.Equals(a,b), $"Expected unequal: {a} and {b}");
private static void Near(float a,float b)=>Check.Near(b,a,(1.1920928955078125e-7 * 100) * (1 + Math.Max(Math.Abs(a),Math.Abs(b))));
private static bool FlagAny(ETextGraphemeFlag a,ETextGraphemeFlag b)=>(a&b)!=0;
private static bool finite_rect(Rectf r)=>r.IsFinite();
private static bool finite_size(Sizef s)=>s.IsFinite();

[GuiTest("gui/text-baseline/common/service-and-font-lifetime")]
public static void SourceCase01()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        Check.NotNull(fixture.Services);
        Eq(fixture.Services.ShortName(),advanced ? "advanced" : "fallback");
        Check.That(fixture.Services.HasFeature(ETextFeature.SimpleLayout));
        Check.That(fixture.Services.HasFeature(ETextFeature.FontDynamic));
        Eq(fixture.Services.HasFeature(ETextFeature.Shaping),advanced);
        Eq(fixture.Services.HasFeature(ETextFeature.BidiLayout),advanced);
        Check.False(fixture.Services.HasFeature(ETextFeature.VerticalLayout));
        Check.False(fixture.Services.HasFeature(
                ETextFeature.KashidaJustification
            ));
        Eq(fixture.Services.FontProviderCount(),1u);
        Eq(fixture.Services.FontProviderAt(0u),fixture.Provider);

        List<FontFaceQuery> queries = new();
        Check.That(fixture.Services.QueryFontFaces(
            MakeLatinStyle(),
            queries
        ));
        Eq((ulong)queries.Count,1u);
        Check.That(queries[(int)(0)].IsValid());
        Eq(queries[(int)(0)].Provider,fixture.Provider);

        FontFace? face = fixture.Services.PreloadFontFace(queries[(int)(0)]);
        Check.That(face != null);
        Check.That(face.Id().IsValid());
        Eq(fixture.Services.FontFaceCount(),1u);
        Eq(fixture.Services.FontFace(face.Id()),face);
        Ne(face.GlyphIndex((uint)'A'),0u);
        Check.That((face.Metrics(16.0f).Ascent) > (0.0f));

        TextFontFaceId face_id = face.Id();
        Check.That(fixture.Services.UnloadFontFace(face_id));
        Eq(fixture.Services.FontFaceCount(),0u);
        Eq(fixture.Services.FontFace(face_id),null);
    }
}

[GuiTest("gui/text-baseline/common/source-span-object-invalidation")]
public static void SourceCase02()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextStyle style = MakeLatinStyle();
        TextLine line = fixture.Services.CreateLine();

        TextLayoutSpanId first =
            line.AddString("T", style, "en");
        Check.That(first.IsValid());
        Check.That(line.Shape());
        Eq(source_glyph_count(line.Glyphs()),1u);

        Check.That(line.AddObject(
            17u,
            new Sizef(20.0f, 12.0f),
            ETextInlineAlignment.Center,
            1u
        ));
        Check.That(line.Shape());
        Eq(source_glyph_count(line.Glyphs()),2u);

        TextLayoutSpanId last =
            line.AddString("B", style, "en");
        Check.That(last.IsValid());
        Check.That(line.Shape());
        Eq(source_glyph_count(line.Glyphs()),3u);
        Eq(line.SourceRange(),(new TextRange( 0u, 3u )));
        Eq(line.SpanCount(),3u);
        Eq(line.SpanText(first),"T");
        Eq(line.SpanText(last),"B");
        Eq(line.SpanLanguage(first),"en");
        Check.That(line.HasObject(17u));
        Eq(line.ObjectCount(),1u);
        Eq(line.ObjectKey(0u),17u);
        Eq(line.ObjectRange(17u),(new TextRange( 1u, 2u )));
        Check.That((line.ObjectGlyphIndex(17u)) >= (0));
        Eq(line.ObjectRect(17u).Size(),new Sizef(20.0f, 12.0f));
        check_line_shape_invariants(line);

        TextStyle larger = style.Copy();
        larger.FontSize = 24.0f;
        Check.That(line.UpdateSpanStyle(first, larger));
        Check.False(line.IsReady());
        Check.That(line.Shape());
        Eq(line.SpanStyle(first).FontSize,24.0f);

        TextLine copy = line.Duplicate();
        Check.NotNull(copy);
        Eq(copy.Text(),line.Text());
        Check.That(copy.Shape());
        Eq(copy.GlyphCount(),line.GlyphCount());

        line.Clear();
        Eq(line.SpanCount(),0u);
        Eq(line.ObjectCount(),0u);
        Check.That(line.SourceRange().IsEmpty());
        Check.That(line.Shape());
        Eq(line.GlyphCount(),0u);
    }
}

[GuiTest("gui/text-baseline/common/break-flags-and-hard-breaks")]
public static void SourceCase03()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextStyle style = MakeLatinStyle();
        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.AddString(
            "A\r\nB\vC\fD\rE\u0085F\u2028G\u2029H",
            style,
            "en"
        );
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),8u);
        Eq(paragraph.LineRange(0u),(new TextRange( 0u, 3u )));
        Eq(paragraph.LineRange(7u).End,paragraph.SourceRange().End);
        check_paragraph_line_invariants(paragraph);

        TextLine flags = fixture.Services.CreateLine();
        flags.AddString(
            "Test test long text long text\n",
            style,
            "en"
        );
        Check.That(flags.Shape());
        ulong glyph_heads = 0u;
        foreach (TextGlyph glyph in flags.Glyphs())
        {
            if (glyph.Count == 0u)
            {
                continue;
            }
            ++glyph_heads;
            bool is_space = FlagAny(
                glyph.Flags,
                ETextGraphemeFlag.Space
            );
            bool is_soft = FlagAny(
                glyph.Flags,
                ETextGraphemeFlag.BreakSoft
            );
            bool is_hard = FlagAny(
                glyph.Flags,
                ETextGraphemeFlag.BreakHard
            );
            ulong source = glyph.SourceRange.Start;
            if (source == 29u)
            {
                Check.That(is_space);
                Check.That(is_hard);
                Check.False(is_soft);
                Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual));
            }
            else if (
                source == 4u ||
                source == 9u ||
                source == 14u ||
                source == 19u ||
                source == 24u
            )
            {
                Check.That(is_space);
                Check.That(is_soft);
                Check.False(is_hard);
            }
            else
            {
                Check.False(is_space);
                Check.False(is_soft);
                Check.False(is_hard);
            }
        }
        Eq(glyph_heads,30u);
    }
}

[GuiTest("gui/text-baseline/common/fixed-line-breaking")]
public static void SourceCase04()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextStyle style = MakeLatinStyle(16.0f);
        TextLine line = fixture.Services.CreateLine();
        line.AddString("test test test", style, "en");
        Check.That(line.Shape());

        List<TextRange> ranges = new();
        Check.That(line.LineBreaks(
            1.0f,
            0u,
            ETextLineBreakFlag.WordBound |
                ETextLineBreakFlag.Mandatory,
            ranges
        ));
        check_ranges(
            ranges,
            new TextRange[]{
                new TextRange(0u,5u ),
                new TextRange(5u,10u ),
                new TextRange(10u,14u ),
            }
        );

        ranges.Clear();
        Check.That(line.LineBreaks(
            35.0f,
            0u,
            ETextLineBreakFlag.WordBound |
                ETextLineBreakFlag.Mandatory |
                ETextLineBreakFlag.TrimStartEdgeSpaces |
                ETextLineBreakFlag.TrimEndEdgeSpaces,
            ranges
        ));
        check_ranges(
            ranges,
            new TextRange[]{
                new TextRange(0u,4u ),
                new TextRange(5u,9u ),
                new TextRange(10u,14u ),
            }
        );

        line.Clear();
        line.AddString("Word Wrap", style, "en");
        Check.That(line.Shape());

        ranges.Clear();
        Check.That(line.LineBreaks(
            43.0f,
            0u,
            TextDefaults.LineBreakFlags,
            ranges
        ));
        check_ranges(ranges, new TextRange[]{ new TextRange(0u,5u ), new TextRange(5u,9u ) });

        ranges.Clear();
        Check.That(line.LineBreaks(
            43.0f,
            0u,
            ETextLineBreakFlag.WordBound |
                ETextLineBreakFlag.Mandatory |
                ETextLineBreakFlag.TrimStartEdgeSpaces |
                ETextLineBreakFlag.TrimEndEdgeSpaces,
            ranges
        ));
        check_ranges(ranges, new TextRange[]{ new TextRange(0u,4u ), new TextRange(5u,9u ) });

        ranges.Clear();
        Check.That(line.LineBreaks(
            43.0f,
            0u,
            ETextLineBreakFlag.WordBound |
                ETextLineBreakFlag.Adaptive |
                ETextLineBreakFlag.Mandatory |
                ETextLineBreakFlag.TrimStartEdgeSpaces |
                ETextLineBreakFlag.TrimEndEdgeSpaces,
            ranges
        ));
        check_ranges(ranges, new TextRange[]{ new TextRange(0u,4u ), new TextRange(5u,9u ) });

        ranges.Clear();
        Check.That(line.LineBreaks(
            43.0f,
            0u,
            ETextLineBreakFlag.WordBound |
                ETextLineBreakFlag.Adaptive |
                ETextLineBreakFlag.Mandatory,
            ranges
        ));
        check_ranges(
            ranges,
            new TextRange[]{
                new TextRange(0u,4u ),
                new TextRange(4u,5u ),
                new TextRange(5u,9u ),
            }
        );

        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.AddString("Word Wrap", style, "en");
        paragraph.SetMaxWidth(43.0f);
        paragraph.SetBreakFlags(
            ETextLineBreakFlag.WordBound |
            ETextLineBreakFlag.Adaptive |
            ETextLineBreakFlag.Mandatory |
            ETextLineBreakFlag.TrimStartEdgeSpaces |
            ETextLineBreakFlag.TrimEndEdgeSpaces
        );
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),2u);
        Eq(paragraph.LineRange(0u),(new TextRange( 0u, 4u )));
        Eq(paragraph.LineRange(1u),(new TextRange( 5u, 9u )));
    }
}

[GuiTest("gui/text-baseline/common/strip-diacritics")]
public static void SourceCase05()
{

    StripCase[] kCases = {
        new StripCase("pêches épinards tomates fraises","peches epinards tomates fraises"),
        new StripCase("ΆΈΉΊΌΎΏΪΫϓϔ","ΑΕΗΙΟΥΩΙΥΥΥ"),
        new StripCase("άέήίΐϊΰϋόύώ","αεηιιιυυουω"),
        new StripCase("ЀЁЃ ЇЌЍӢӤЙ ЎӮӰӲ ӐӒӖӚӜӞ ӦӪ Ӭ Ӵ Ӹ","ЕЕГ ІКИИИИ УУУУ ААЕӘЖЗ ОӨ Э Ч Ы"),
        new StripCase("ѐёѓ їќѝӣӥй ўӯӱӳ ӑӓӗӛӝӟ ӧӫ ӭ ӵ ӹ","еег ікииии уууу ааеәжз оө э ч ы"),
        new StripCase("ÀÁÂÃÄÅĀĂĄÇĆĈĊČĎÈÉÊËĒĔĖĘĚĜĞĠĢĤÌÍÎÏĨĪĬĮİĴĶĹĻĽÑŃŅŇŊÒÓÔÕÖØŌŎŐƠŔŖŘŚŜŞŠŢŤÙÚÛÜŨŪŬŮŰŲƯŴÝŶŹŻŽ","AAAAAAAAACCCCCDEEEEEEEEEGGGGHIIIIIIIIIJKLLLNNNNŊOOOOOØOOOORRRSSSSTTUUUUUUUUUUUWYYZZZ"),
        new StripCase("àáâãäåāăąçćĉċčďèéêëēĕėęěĝğġģĥìíîïĩīĭįĵķĺļľñńņňŋòóôõöøōŏőơŕŗřśŝşšţťùúûüũūŭůűųưŵýÿŷźżž","aaaaaaaaacccccdeeeeeeeeegggghiiiiiiiijklllnnnnŋoooooøoooorrrssssttuuuuuuuuuuuwyyyzzz"),
        new StripCase("ǍǏȈǑǪǬȌȎȪȬȮȰǓǕǗǙǛȔȖǞǠǺȀȂȦǢǼǦǴǨǸȆȐȒȘȚȞȨ Ḁ ḂḄḆ Ḉ ḊḌḎḐḒ ḔḖḘḚḜ Ḟ Ḡ ḢḤḦḨḪ ḬḮ ḰḲḴ ḶḸḺḼ ḾṀṂ ṄṆṈṊ ṌṎṐṒ ṔṖ ṘṚṜṞ ṠṢṤṦṨ ṪṬṮṰ ṲṴṶṸṺ","AIIOOOOOOOOOUUUUUUUAAAAAAÆÆGGKNERRSTHE A BBB C DDDDD EEEEE F G HHHHH II KKK LLLL MMM NNNN OOOO PP RRRR SSSSS TTTT UUUUU"),
        new StripCase("ǎǐȉȋǒǫǭȍȏȫȭȯȱǔǖǘǚǜȕȗǟǡǻȁȃȧǣǽǧǵǩǹȇȑȓșțȟȩ ḁ ḃḅḇ ḉ ḋḍḏḑḓ ḟ ḡ ḭḯ ḱḳḵ ḷḹḻḽ ḿṁṃ ṅṇṉṋ ṍṏṑṓ ṗṕ ṙṛṝṟ ṡṣṥṧṩ ṫṭṯṱ ṳṵṷṹṻ","aiiiooooooooouuuuuuuaaaaaaææggknerrsthe a bbb c ddddd f g ii kkk llll mmm nnnn oooo pp rrrr sssss tttt uuuuu"),
        new StripCase("ṼṾ ẀẂẄẆẈ ẊẌ Ẏ ẐẒẔ","VV WWWWW XX Y ZZZ"),
        new StripCase("ṽṿ ẁẃẅẇẉ ẋẍ ẏ ẑẓẕ ẖ ẗẘẙẛ","vv wwwww xx y zzz h twys"),
    };

    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        foreach (StripCase value in kCases)
        {
            Eq(fixture.Services.StripDiacritics(value.Text),value.Expected);
        }
    }
}

[GuiTest("gui/text-baseline/common/hard-break-space-trimming")]
public static void SourceCase06()
{


    ETextLineBreakFlag kBaseFlags =
        ETextLineBreakFlag.Mandatory |
        ETextLineBreakFlag.WordBound;
    BreakCase[] cases = {
        new BreakCase("test \rtest",kBaseFlags |
                ETextLineBreakFlag.TrimStartEdgeSpaces |
                ETextLineBreakFlag.TrimEndEdgeSpaces,new TextRange[]{new TextRange(0u,4u ), new TextRange(6u,10u ) }),
        new BreakCase("test \rtest",kBaseFlags |
                ETextLineBreakFlag.TrimStartEdgeSpaces,new TextRange[]{new TextRange(0u,6u ), new TextRange(6u,10u ) }),
        new BreakCase("test\r test",kBaseFlags |
                ETextLineBreakFlag.TrimStartEdgeSpaces |
                ETextLineBreakFlag.TrimEndEdgeSpaces,new TextRange[]{new TextRange(0u,4u ), new TextRange(6u,10u ) }),
        new BreakCase("test\r test",kBaseFlags |
                ETextLineBreakFlag.TrimEndEdgeSpaces,new TextRange[]{new TextRange(0u,4u ), new TextRange(5u,10u ) }),
        new BreakCase("test\r test \r test",kBaseFlags |
                ETextLineBreakFlag.TrimStartEdgeSpaces |
                ETextLineBreakFlag.TrimEndEdgeSpaces,new TextRange[]{new TextRange(0u,4u ), new TextRange(6u,10u ), new TextRange(13u,17u ) }),
        new BreakCase("test\r test \r test",kBaseFlags |
                ETextLineBreakFlag.TrimStartEdgeSpaces,new TextRange[]{new TextRange(0u,5u ), new TextRange(6u,12u ), new TextRange(13u,17u ) }),
        new BreakCase("test\r test \r test",kBaseFlags |
                ETextLineBreakFlag.TrimEndEdgeSpaces,new TextRange[]{new TextRange(0u,4u ), new TextRange(5u,10u ), new TextRange(12u,17u ) }),
        new BreakCase("test\r test \r test",kBaseFlags,new TextRange[]{new TextRange(0u,5u ), new TextRange(5u,12u ), new TextRange(12u,17u ) }),
    };

    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        foreach (BreakCase value in cases)
        {
            TextLine line = fixture.Services.CreateLine();
            line.AddString(
                value.Text,
                MakeLatinStyle(16.0f),
                "en"
            );
            Check.That(line.Shape());

            List<TextRange> ranges = new();
            Check.That(line.LineBreaks(
                90.0f,
                0u,
                value.Flags,
                ranges
            ));
            check_ranges(ranges, value.Expected);
        }
    }
}

[GuiTest("gui/text-baseline/common/tabs-objects-and-line-views")]
public static void SourceCase07()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextStyle style = MakeLatinStyle();

        TextLine line = fixture.Services.CreateLine();
        line.AddString("A\tB", style);
        float[] invalid_stops = { 0.0f, -1.0f, float.NaN };
        Eq(line.TabAlign(
                invalid_stops
            ),0.0f);
        float[] stops = { 24.0f, 48.0f };
        Check.That((line.TabAlign(stops)) > (0.0f));
        Check.That(line.Shape());
        Check.That(glyphs_have_flag(
            line.Glyphs(),
            ETextGraphemeFlag.Tab
        ));

        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.AddString("A", style);
        Check.That(paragraph.AddObject(
            42u,
            new Sizef(12.0f, 16.0f),
            ETextInlineAlignment.BaselineTo,
            0u,
            11.0f
        ));
        paragraph.AddString("\nB", style);
        Check.That(paragraph.Shape());
        Eq(paragraph.ObjectRange(42u),(new TextRange( 1u, 1u )));
        Eq(paragraph.LineCount(),2u);
        List<ulong> objects = new();
        Check.That(paragraph.LineObjects(0u, objects));
        Eq((ulong)objects.Count,1u);
        Eq(objects[(int)(0)],42u);
        Eq(paragraph.LineObjectRect(0u, 42u).Size(),new Sizef(12.0f, 16.0f));
        check_paragraph_line_invariants(paragraph);
    }
}

[GuiTest("gui/text-baseline/common/trim-justify-and-editing")]
public static void SourceCase08()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextStyle style = MakeLatinStyle();
        TextLine line = fixture.Services.CreateLine();
        line.AddString("test test", style, "en");
        Check.That(line.Shape());

        float original_width = line.LineWidth();
        float fitted = line.FitToWidth(
            original_width + 20.0f,
            ETextJustificationFlag.WordBound
        );
        Check.That((fitted) > (original_width));
        Check.That((fitted) <= (original_width + 20.0f));

        line.SetMaxWidth(38.0f);
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        Check.That(line.Shape());
        Ne(line.TrimPosition(),ulong.MaxValue);
        Ne(line.EllipsisPosition(),ulong.MaxValue);
        Check.False(line.EllipsisGlyphs().IsEmpty);
        Check.That(glyphs_have_flag(
            line.EllipsisGlyphs(),
            ETextGraphemeFlag.Virtual
        ));

        TextFloatRange first = line.GraphemeBounds(0u);
        Check.That((first.End) > (first.Start));
        Eq(line.HitTestGrapheme(
                (first.Start + first.End) * 0.5f
            ),0);
        Check.That((line.HitTestPosition(first.End)) >= (0));
        TextCaretInfo caret = line.Caret(1u);
        bool has_caret_geometry =
            caret.LeadingCaret.Height() > 0.0f ||
            caret.TrailingCaret.Height() > 0.0f;
        Check.That(has_caret_geometry);
        List<Rectf> selection = new();
        Check.That(line.SelectionRects(
            new TextRange( 1u, 4u ),
            selection
        ));
        Check.False((selection.Count == 0));
        Eq(line.PreviousGraphemePosition(1u),0);
        Check.That((line.NextGraphemePosition(0u)) > (0));
    }
}

[GuiTest("gui/text-baseline/common/stable-provider-is-not-polled")]
public static void SourceCase09()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.AddString("AB", MakeLatinStyle());
        Check.That(line.Shape());
        Check.That((line.RunCount()) > (0u));
        FontFace? first = line.RunFontFace(0u);
        Check.That(first != null);
        Utf8StringView first_source = first.Source().SourceKey;
        uint query_count = fixture.Provider.QueryCount();

        fixture.Provider.MutateSourceKeySilently();
        Check.That(line.IsReady());
        Check.False(line.Glyphs().IsEmpty);
        Check.That(line.Shape());
        FontFace? second = line.RunFontFace(0u);
        Eq(second,first);
        Eq(second.Source().SourceKey,first_source);
        Eq(fixture.Provider.QueryCount(),query_count);
    }
}

[GuiTest("gui/text-baseline/common/provider-topology-invalidates")]
public static void SourceCase10()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.AddString("AB", MakeLatinStyle());
        Check.That(line.Shape());
        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.AddString("AB", MakeLatinStyle());
        Check.That(paragraph.Shape());
        Check.That((line.RunCount()) > (0u));
        Eq(line.RunFontFace(0u).Provider(),fixture.Provider);

        EmbeddedLatinFontProvider second =
            new EmbeddedLatinFontProvider(
                "embedded://text-baseline/latin/second"
            );
        fixture.Services.AddFontProvider(second);
        Check.False(line.IsReady());
        Check.False(paragraph.IsReady());
        Check.That(line.Shape());
        Check.That(paragraph.Shape());
        Eq(line.RunFontFace(0u).Provider(),fixture.Provider);

        fixture.Services.ClearFontProviders();
        Check.False(line.IsReady());
        Check.False(paragraph.IsReady());
        fixture.Services.AddFontProvider(second);
        Check.That(line.Shape());
        Check.That(paragraph.Shape());
        Check.That((line.RunCount()) > (0u));
        Eq(line.RunFontFace(0u).Provider(),second);
    }
}

[GuiTest("gui/text-baseline/common/family-fallback-is-lazy")]
public static void SourceCase11()
{
    foreach (bool advanced in kBackends)
    {
        TextServicesDesc desc = new() { AddSystemFontProvider = false, IcuData = TextContractFixture.IcuData(),
        };
        TextServices services = advanced ?
            TextServices.CreateAdvanced(desc) :
            TextServices.CreateFallback(desc);
        LazyFallbackFontProvider provider =
            new LazyFallbackFontProvider();
        services.AddFontProvider(provider);

        TextStyle style = new();
        style.FontFamilies = "Primary, Secondary";
        style.FontSize = 16.0f;
        TextLine latin = services.CreateLine();
        latin.AddString("AB", style);
        Check.That(latin.Shape());
        FontFace? regular_face = latin.RunFontFace(0u);
        Check.That(regular_face != null);
        Eq(provider.PrimaryQueries,1u);
        Eq(provider.PrimaryLoads,1u);
        Eq(provider.SecondaryQueries,0u);
        Eq(provider.SecondaryLoads,0u);

        TextLine missing = services.CreateLine();
        missing.AddString("א", style);
        Check.That(missing.Shape());
        Eq(provider.SecondaryQueries,1u);
        Eq(provider.SecondaryLoads,1u);
        Eq(provider.PrimaryQueries,1u);
        Eq(provider.PrimaryLoads,1u);
        Check.False(missing.Glyphs().IsEmpty);
        Eq(missing.Glyphs()[(int)(0)].Kind,ETextGlyphKind.Glyph);
        FontFace? fallback_face = missing.RunFontFace(0u);
        Check.That(fallback_face != null);
        Eq(fallback_face.Source().SourceKey,"embedded://lazy-fallback/secondary");

        TextStyle bold_style = style.Copy();
        bold_style.FontFamilies = "Primary";
        bold_style.FontWeight = EFontWeight.Bold;
        TextLine bold = services.CreateLine();
        bold.AddString("AB", bold_style);
        Check.That(bold.Shape());
        FontFace? bold_face = bold.RunFontFace(0u);
        Check.That(bold_face != null);
        Ne(bold_face,regular_face);
        Eq(bold_face.Source().Weight,EFontWeight.Bold);
        Eq(provider.PrimaryQueries,2u);
        Eq(provider.PrimaryLoads,1u);
    }
}

[GuiTest("gui/text-baseline/common/provider-negative-cache-is-topology-scoped")]
public static void SourceCase12()
{
    foreach (bool advanced in kBackends)
    {
        TextServicesDesc desc = new() { AddSystemFontProvider = false, IcuData = TextContractFixture.IcuData(),
        };
        TextServices services = advanced ?
            TextServices.CreateAdvanced(desc) :
            TextServices.CreateFallback(desc);
        SingleFontProvider first = new SingleFontProvider(
            "Other",
            "embedded://negative-cache/other",
            TestFontAssets.Get("Latin"),
            (ulong)TestFontAssets.Get("Latin").Length
        );
        services.AddFontProvider(first);

        TextStyle late_style = new();
        late_style.FontFamilies = "Late";
        List<FontFaceQuery> queries = new();
        Check.False(services.QueryFontFaces(late_style, queries));
        Check.False(services.QueryFontFaces(late_style, queries));
        Eq(first.QueryCount,1u);

        SingleFontProvider late = new SingleFontProvider(
            "Late",
            "embedded://negative-cache/late",
            TestFontAssets.Get("Latin"),
            (ulong)TestFontAssets.Get("Latin").Length
        );
        services.AddFontProvider(late);
        Check.That(services.QueryFontFaces(late_style, queries));
        Eq((ulong)queries.Count,1u);
        Eq(queries[(int)(0)].Provider,late);
        Eq(first.QueryCount,2u);
        Eq(late.QueryCount,1u);
    }
}

[GuiTest("gui/text-baseline/common/font-data-cache-includes-provider-identity")]
public static void SourceCase13()
{
    foreach (bool advanced in kBackends)
    {
        TextServicesDesc desc = new() { AddSystemFontProvider = false, IcuData = TextContractFixture.IcuData(),
        };
        TextServices services = advanced ?
            TextServices.CreateAdvanced(desc) :
            TextServices.CreateFallback(desc);
        Utf8StringView kFamily = "Shared Family";
        Utf8StringView kSource = "embedded://shared-source";
        SingleFontProvider latin = new SingleFontProvider(
            kFamily.ToString(),
            kSource.ToString(),
            TestFontAssets.Get("Latin"),
            (ulong)TestFontAssets.Get("Latin").Length
        );
        services.AddFontProvider(latin);

        TextStyle style = new();
        style.FontFamilies = kFamily.ToString();
        TextLine first = services.CreateLine();
        first.AddString("A", style);
        Check.That(first.Shape());
        Eq(latin.LoadCount,1u);

        services.ClearFontProviders();
        SingleFontProvider hebrew = new SingleFontProvider(
            kFamily.ToString(),
            kSource.ToString(),
            TestFontAssets.Get("Hebrew"),
            (ulong)TestFontAssets.Get("Hebrew").Length
        );
        services.AddFontProvider(hebrew);

        TextLine second = services.CreateLine();
        second.AddString("א", style);
        Check.That(second.Shape());
        Check.False(second.Glyphs().IsEmpty);
        Eq(second.Glyphs()[(int)(0)].Kind,ETextGlyphKind.Glyph);
        Eq(second.RunFontFace(0u).Provider(),hebrew);
        Eq(hebrew.LoadCount,1u);
    }
}

[GuiTest("gui/text-baseline/common/font-data-load-failure-is-cached")]
public static void SourceCase14()
{
    foreach (bool advanced in kBackends)
    {
        TextServicesDesc desc = new() { AddSystemFontProvider = false, IcuData = TextContractFixture.IcuData(),
        };
        TextServices services = advanced ?
            TextServices.CreateAdvanced(desc) :
            TextServices.CreateFallback(desc);
        SingleFontProvider provider =
            new SingleFontProvider(
                "Broken",
                "embedded://broken-font",
                null,
                0u
            );
        services.AddFontProvider(provider);

        TextStyle style = new();
        style.FontFamilies = "Broken";
        for (uint i = 0u; i < 2u; ++i)
        {
            TextLine line = services.CreateLine();
            line.AddString("A", style);
            Check.That(line.Shape());
        }
        Eq(provider.QueryCount,1u);
        Eq(provider.LoadCount,1u);

        services.UnloadAllFontFaces();
        TextLine retried = services.CreateLine();
        retried.AddString("A", style);
        Check.That(retried.Shape());
        Eq(provider.QueryCount,1u);
        Eq(provider.LoadCount,2u);
    }
}

[GuiTest("gui/text-baseline/common/empty-paragraph-contract")]
public static void SourceCase15()
{
    foreach (bool advanced in kBackends)
    {
        using TextFixture fixture = MakeTextFixture(advanced);
        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),1u);
        Eq(paragraph.LineRange(0u),(new TextRange( 0u, 0u )));
        Eq(paragraph.LineGlyphRange(0u),(new TextRange( 0u, 0u )));
        Eq(paragraph.GlyphCount(),0u);
    }
}
}
