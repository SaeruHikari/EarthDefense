using static SkrGui.Tests.TextBaselineFixture;
using static SkrGui.Tests.TextSourceTestHelpers;
namespace SkrGui.Tests;
// All cases/loops/inputs from tests/text/text_baseline_advanced_tests.cpp @ 611561f8.
public static class TextBaselineAdvancedTests {
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

private sealed record IdentifierCase(Utf8StringView Text,bool Expected);
private sealed record LetterCase(int FailIndex,uint[] Codepoints);
private static void check_uint_ranges(List<ulong> actual,ulong[] expected)=>Check.SequenceEqual(expected,actual);
private static void check_text_ranges(List<TextRange> actual,TextRange[] expected)=>Check.SequenceEqual(expected,actual);

[GuiTest("gui/text-baseline/advanced/visual-and-logical-bidi")]
public static void SourceCase01()
{
    using TextFixture fixture = MakeTextFixture(true);
    TextLine line = fixture.Services.CreateLine();
    line.AddString(
        "abc אבג 123",
        MakeLatinStyle(20.0f),
        "he"
    );
    Check.That(line.Shape());
    Check.That(glyphs_have_flag(
        line.Glyphs(),
        ETextGraphemeFlag.Rtl
    ));
    Check.That(glyph_buffer_is_well_formed(line.Glyphs()));

    List<TextGlyph> visual_copy = new();
    copy_glyphs(line.Glyphs(), visual_copy);
    ReadOnlySpan<TextGlyph> logical = line.SortLogicalGlyphs();
    Eq((ulong)logical.Length,(ulong)visual_copy.Count);

    bool visual_differs = false;
    for (ulong i = 1u; i < (ulong)logical.Length; ++i)
    {
        Check.That((logical[(int)(i - 1u)].SourceRange.Start) <= (logical[(int)(i)].SourceRange.Start));
        visual_differs |=
            visual_copy[(int)(i - 1u)].SourceRange.Start >
            visual_copy[(int)(i)].SourceRange.Start;
    }
    Check.That(visual_differs);

    ReadOnlySpan<TextGlyph> visual_after_sort = line.Glyphs();
    Eq((ulong)visual_after_sort.Length,(ulong)visual_copy.Count);
    for (ulong i = 0u; i < (ulong)visual_copy.Count; ++i)
    {
        Eq(visual_after_sort[(int)(i)].SourceRange,visual_copy[(int)(i)].SourceRange);
        Eq(visual_after_sort[(int)(i)].GlyphIndex,visual_copy[(int)(i)].GlyphIndex);
    }

    Eq(line.RangeDirection(new TextRange( 4u, 7u )),ETextDirection.RTL);
    TextCaretInfo caret = line.Caret(5u);
    bool has_rtl_caret =
        caret.LeadingDirection == ETextDirection.RTL ||
        caret.TrailingDirection == ETextDirection.RTL;
    Check.That(has_rtl_caret);
}

[GuiTest("gui/text-baseline/advanced/forced-direction-and-override")]
public static void SourceCase02()
{
    using TextFixture fixture = MakeTextFixture(true);
    TextLine line = fixture.Services.CreateLine();
    line.SetDirection(ETextDirectionMode.RTL);
    line.AddString("abc 123", MakeLatinStyle(), "en");
    Check.That(line.Shape());
    Eq(line.InferredDirection(),ETextDirection.RTL);

    TextBidiOverride bidiOverride = new() { Range = line.SourceRange(), Direction = ETextDirection.LTR,
    };
    line.SetBidiOverride(
        new TextBidiOverride[]{bidiOverride}
    );
    Check.False(line.IsReady());
    Check.That(line.Shape());
    Eq(line.RangeDirection(new TextRange( 0u, 3u )),ETextDirection.LTR);
}

[GuiTest("gui/text-baseline/advanced/bidi-ranges")]
public static void SourceCase03()
{
    using TextFixture fixture = MakeTextFixture(true);
    TextLine line = fixture.Services.CreateLine();
    line.AddString(
        "Arabic (اَلْعَرَبِيَّةُ, al-ʿarabiyyah)",
        MakeLatinStyle(),
        "ar"
    );
    Check.That(line.Shape());

    foreach (TextGlyph glyph in line.Glyphs())
    {
        if (glyph.Count == 0u)
        {
            continue;
        }
        ulong source = glyph.SourceRange.Start;
        bool rtl = FlagAny(
            glyph.Flags,
            ETextGraphemeFlag.Rtl
        );
        if (source < 7u)
        {
            Check.False(rtl);
        }
        if (source > 8u && source < 23u)
        {
            Check.That(rtl);
        }
        if (source > 26u)
        {
            Check.False(rtl);
        }
    }
}

[GuiTest("gui/text-baseline/advanced/character-breaks")]
public static void SourceCase04()
{
    using TextFixture fixture = MakeTextFixture(true);

    List<ulong> breaks = new();
    fixture.Services.StringCharacterBreaks(
        "Xtestxs",
        "en",
        breaks
    );
    check_uint_ranges(breaks, new ulong[]{1u,2u, 3u, 4u, 5u, 6u, 7u });

    breaks.Clear();
    fixture.Services.StringCharacterBreaks(
        "X❤️‍🔥xs",
        "en",
        breaks
    );
    check_uint_ranges(breaks, new ulong[]{1u,5u, 6u, 7u });

    TextLine line = fixture.Services.CreateLine();
    line.AddString("X❤️‍🔥xs", MakeLatinStyle(), "en");
    Check.That(line.Shape());
    breaks.Clear();
    Check.That(line.CharacterBreaks(breaks));
    check_uint_ranges(breaks, new ulong[]{1u,5u, 6u, 7u });
}

[GuiTest("gui/text-baseline/advanced/word-breaks")]
public static void SourceCase05()
{
    using TextFixture fixture = MakeTextFixture(true);
    List<TextRange> breaks = new();
    fixture.Services.StringWordBreaks(
        "linguistically similar and effectively form",
        "en",
        0u,
        breaks
    );
    check_text_ranges(
        breaks,
        new TextRange[]{
            new TextRange(0u,14u ),
            new TextRange(15u,22u ),
            new TextRange(23u,26u ),
            new TextRange(27u,38u ),
            new TextRange(39u,43u ),
        }
    );

    breaks.Clear();
    fixture.Services.StringWordBreaks(
        "เป็นภาษาราชการและภาษาประจำชาติของประเทศไทย",
        "th",
        0u,
        breaks
    );
    check_text_ranges(
        breaks,
        new TextRange[]{
            new TextRange(0u,4u ),
            new TextRange(4u,8u ),
            new TextRange(8u,14u ),
            new TextRange(14u,17u ),
            new TextRange(17u,21u ),
            new TextRange(21u,26u ),
            new TextRange(26u,30u ),
            new TextRange(30u,33u ),
            new TextRange(33u,42u ),
        }
    );
}

[GuiTest("gui/text-baseline/advanced/long-emoji-breaks")]
public static void SourceCase06()
{
    using TextFixture fixture = MakeTextFixture(true);
    List<ulong> breaks = new();
    fixture.Services.StringCharacterBreaks(
        "U+2764 U+FE0F U+200D U+1F525 ; 13.1 # ❤️‍🔥",
        "en",
        breaks
    );
    Eq((ulong)breaks.Count,39u);
    for (ulong i = 0u; i < 38u; ++i)
    {
        Eq(breaks[(int)(i)],i + 1u);
    }
    Eq(breaks[(int)(38u)],42u);
}

[GuiTest("gui/text-baseline/advanced/unicode-identifiers")]
public static void SourceCase07()
{
    using TextFixture fixture = MakeTextFixture(true);

    IdentifierCase[] kCases = {
        new IdentifierCase("-30",false),
        new IdentifierCase("100",false),
        new IdentifierCase("10.1",false),
        new IdentifierCase("10,1",false),
        new IdentifierCase("1e2",false),
        new IdentifierCase("1e-2",false),
        new IdentifierCase("1e2e3",false),
        new IdentifierCase("0xAB",false),
        new IdentifierCase("AB",true),
        new IdentifierCase("Test1",true),
        new IdentifierCase("1Test",false),
        new IdentifierCase("Test*1",false),
        new IdentifierCase("test_testeT",true),
        new IdentifierCase("test_tes teT",false),
        new IdentifierCase("عَلَيْكُمْ",true),
        new IdentifierCase("عَلَيْكُمْTest",true),
        new IdentifierCase("ӒӖӚӜ",true),
        new IdentifierCase("_test",true),
        new IdentifierCase("ÂÃÄÅĀĂĄÇĆĈĊ",true),
    };

    foreach (IdentifierCase value in kCases)
    {
        Eq(fixture.Services.IsValidIdentifier(value.Text),value.Expected);
    }

    Check.That(fixture.Services.IsValidIdentifier(
        "\u0646\u0627\u0645\u0647\u200C\u0627\u06CC"
    ));
    Check.That(fixture.Services.IsValidIdentifier(
        "\u0D26\u0D43\u0D15\u0D4D\u200C\u0D38\u0D3E\u0D15\u0D4D\u0D37\u0D3F"
    ));
    Check.That(fixture.Services.IsValidIdentifier(
        "\u0DC1\u0DCA\u200D\u0DBB\u0DD3"
    ));
}

[GuiTest("gui/text-baseline/advanced/letter-and-diacritic")]
public static void SourceCase08()
{
    using TextFixture fixture = MakeTextFixture(true);

    LetterCase[] kCases = {
        new LetterCase(0,new uint[]{0x2d, 0x33, 0x30 ,0}),
        new LetterCase(1,new uint[]{0x61, 0x2e, 0x31 ,0}),
        new LetterCase(1,new uint[]{0x61, 0x2c, 0x31 ,0}),
        new LetterCase(0,new uint[]{0x31, 0x65, 0x2d, 0x32 ,0}),
        new LetterCase(0,new uint[]{0xab ,0}),
        new LetterCase(-1,new uint[]{0x41, 0x42 ,0}),
        new LetterCase(4,new uint[]{0x54, 0x65, 0x73, 0x74, 0x31 ,0}),
        new LetterCase(2,new uint[]{0x54, 0x65, 0x2a, 0x73, 0x74 ,0}),
        new LetterCase(4,new uint[]{0x74, 0x65, 0x73, 0x74, 0x5f, 0x74, 0x65, 0x73, 0x74, 0x65 ,0}),
        new LetterCase(4,new uint[]{0x74, 0x65, 0x73, 0x74, 0x20, 0x74, 0x65, 0x73, 0x74 ,0}),
        new LetterCase(-1,new uint[]{0x643, 0x402, 0x716, 0xb05 ,0}),
        new LetterCase(-1,new uint[]{0x643, 0x402, 0x716, 0xb05, 0x54, 0x65, 0x73, 0x74, 0x30aa, 0x4e21 ,0}),
        new LetterCase(-1,new uint[]{0x4d2, 0x4d6, 0x4da, 0x4dc ,0}),
        new LetterCase(-1,new uint[]{0xc2, 0xc3, 0xc4, 0xc5, 0x100, 0x102, 0x104, 0xc7, 0x106, 0x108 ,0}),
    };

    foreach (LetterCase value in kCases)
    {
        int failed_on_index = -1;
        for (ulong i = 0u; i < (ulong)value.Codepoints.Length; ++i)
        {
            uint codepoint = value.Codepoints[(int)(i)];
            if (codepoint == 0u)
            {
                break;
            }
            if (!fixture.Services.IsValidLetter(codepoint))
            {
                failed_on_index = (int)(i);
                break;
            }
        }
        Eq(failed_on_index,value.FailIndex);
    }

    Eq(fixture.Services.StripDiacritics(
            "ٱلسَّلَامُ عَلَيْكُمْ"
        ),"ٱلسلام عليكم");
}

[GuiTest("gui/text-baseline/advanced/context-case-and-security")]
public static void SourceCase09()
{
    using TextFixture fixture = MakeTextFixture(true);
    Check.That(fixture.Services.IsLocaleRightToLeft("ar"));
    Eq(fixture.Services.StringToLower("I", "tr"),"ı");
    Eq(fixture.Services.StringToUpper("i", "tr"),"İ");

    Utf8StringView[] dictionary = { "paypal" };
    Eq(fixture.Services.IsConfusable(
            "pаypal",
            dictionary
        ),0);
    Check.That(fixture.Services.SpoofCheck("pаypal"));
}

[GuiTest("gui/text-baseline/advanced/opentype-cluster-contract")]
public static void SourceCase10()
{
    using TextFixture fixture = MakeTextFixture(true);
    TextStyle ligatures = MakeLatinStyle();
    ligatures.OpenTypeFeatures.Add(new FontOpenTypeFeatureValue { Tag = fixture.Services.OpenTypeNameToTag("dlig"), Value = 1u,
    });
    TextLine enabled = fixture.Services.CreateLine();
    enabled.AddString("!?", ligatures, "en");
    Check.That(enabled.Shape());

    TextStyle disabled = ligatures.Copy();
    disabled.OpenTypeFeatures.Add(new FontOpenTypeFeatureValue { Tag = fixture.Services.OpenTypeNameToTag("dlig"), Value = 0u,
    });
    TextLine no_ligatures = fixture.Services.CreateLine();
    no_ligatures.AddString("!?", disabled, "en");
    Check.That(no_ligatures.Shape());
    Check.That((enabled.GlyphCount()) < (no_ligatures.GlyphCount()));
    Check.That(glyph_buffer_is_well_formed(enabled.Glyphs()));
    Check.That(glyph_buffer_is_well_formed(
        no_ligatures.Glyphs()
    ));
}
}
