using static SkrGui.Tests.TextSourceTestHelpers;
using static SkrGui.Tests.TextContractFixture;
namespace SkrGui.Tests;
// All cases/loops/inputs from tests/text/text_shaping_contract_tests.cpp @ 611561f8.
public static class TextShapingContractTests {
private static readonly bool[] kBackends = [false,true];
private static void Eq<T>(T a,T b)=>Check.Equal(a,b);
private static void Eq(ulong a,uint b)=>Check.Equal(a,(ulong)b);
private static void Eq(long a,int b)=>Check.Equal(a,(long)b);
private static void Eq(Utf8StringView a,string b)=>Check.Equal(a.ToString(),b);
private static void Eq(uint a,char b)=>Check.Equal(a,(uint)b);
private static void Ne<T>(T a,T b)=>Check.That(!EqualityComparer<T>.Default.Equals(a,b), $"Expected unequal: {a} and {b}");
private static void Near(float a,float b)=>Check.Near(b,a,(1.1920928955078125e-7 * 100) * (1 + Math.Max(Math.Abs(a),Math.Abs(b))));
private static bool FlagAll(ETextGraphemeFlag a,ETextGraphemeFlag b)=>(a&b)==b;
private static bool FlagAny(ETextGraphemeFlag a,ETextGraphemeFlag b)=>(a&b)!=0;
private static float Round(float value)=>MathF.Round(value,MidpointRounding.AwayFromZero);
private static bool finite_rect(Rectf r)=>r.IsFinite();
private static bool finite_size(Sizef s)=>s.IsFinite();
private sealed record ControlCase(Utf8StringView Text,uint Codepoint,bool IsSpace,bool IsHardBreak,bool IsTab,bool IsZeroWidth);
private sealed record BreakCase(Utf8StringView Text,uint Codepoint,bool FallbackHexBox);
private sealed record ScriptCase(Utf8StringView Text,string Family,Utf8StringView Language,bool RTL);
private sealed record FixedBreakCase(Utf8StringView Text,TextRange[] Ranges,ulong RangeCount);
private delegate void ConfigMutation(ref TextFontRasterConfig config);
private sealed class GlyphRef(TextGlyph value) { public TextRange SourceRange=>value.SourceRange;public TextFontFaceId FontFace=>value.FontFace; public uint GlyphIndex=>value.GlyphIndex;public ETextGlyphKind Kind=>value.Kind;public ETextGraphemeFlag Flags=>value.Flags; public float Advance=>value.Advance;public float FontSize=>value.FontSize;public Offsetf Offset=>value.Offset;public bool IsVisible()=>value.IsVisible(); }
private static bool same_glyph_identity(TextGlyph lhs, TextGlyph rhs)
{
    return lhs.SourceRange == rhs.SourceRange &&
        lhs.FontFace == rhs.FontFace &&
        lhs.GlyphIndex == rhs.GlyphIndex &&
        lhs.Count == rhs.Count && lhs.Kind == rhs.Kind;
}

private static void check_visual_logical_identity(TextLine line)
{
    ReadOnlySpan<TextGlyph> visual = line.Glyphs();
    ReadOnlySpan<TextGlyph> logical = line.SortLogicalGlyphs();
    Eq((ulong)visual.Length,(ulong)logical.Length);
    List<bool> used = new();
    for(int i=0;i<logical.Length;i++)used.Add(false);
    foreach (TextGlyph glyph in visual)
    {
        bool found = false;
        for (ulong index = 0u; index < (ulong)logical.Length; ++index)
        {
            if (!used[(int)(index)] && same_glyph_identity(glyph, logical[(int)(index)]))
            {
                used[(int)(index)] = true;
                found = true;
                break;
            }
        }
        Check.That(found);
    }
}

private static void check_ranges(
    ReadOnlySpan<TextRange> actual,
    ReadOnlySpan<TextRange> expected
)
{
    Eq((ulong)actual.Length,(ulong)expected.Length);
    if ((ulong)actual.Length != (ulong)expected.Length)
    {
        return;
    }
    for (ulong index = 0u; index < (ulong)actual.Length; ++index)
    {
        Eq(actual[(int)(index)],expected[(int)(index)]);
    }
}

private static GlyphRef? source_glyph_at(
    ReadOnlySpan<TextGlyph> glyphs,
    ulong source
)
{
    foreach (TextGlyph glyph in glyphs)
    {
        if (glyph.Count > 0u &&
            glyph.SourceRange.Start == source &&
            !FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual))
        {
            return new GlyphRef(glyph);
        }
    }
    return null;
}

[GuiTest("gui/text-contract/shaping/fractional-size-changes-advance")]
public static void SourceCase01()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    uint glyph_index = face.GlyphIndex((uint)'A');
    Ne(glyph_index,0u);

    var shape_at_size = (float font_size) => {
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", font_size);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());
        Eq((ulong)line.Glyphs().Length,1u);
        Check.That((line.Glyphs()[(int)(0)].Advance) > (0.0f));
        return line.Glyphs()[(int)(0)].Advance;
    };

    float smaller = shape_at_size(16.1f);
    float larger = shape_at_size(16.2f);
    Check.That((larger) > (smaller));
}

[GuiTest("gui/text-contract/shaping/horizontal-rounding-policy")]
public static void SourceCase02()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    var shape = (Utf8StringView text) => {
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString(text, style);
        Check.That(line.Shape());
        List<TextGlyph> glyphs = new();

        foreach (TextGlyph glyph in line.Glyphs())
        {
            glyphs.Add(glyph);
        }
        return glyphs;
    };

    List<TextGlyph> raw = shape("AAAA");
    Eq((ulong)raw.Count,4u);

    config.SubpixelPositioning = ETextSubpixelPositioning.Disabled;
    config.KeepRoundingRemainders = false;
    fixture.Services.SetDefaultFontRasterConfig(config);
    List<TextGlyph> independently_rounded = shape("AAAA");
    Eq((ulong)independently_rounded.Count,(ulong)raw.Count);
    float raw_width = 0.0f;
    float independent_width = 0.0f;
    for (ulong index = 0u; index < (ulong)raw.Count; ++index)
    {
        raw_width += raw[(int)(index)].Advance;
        independent_width += independently_rounded[(int)(index)].Advance;
        Check.Near(independently_rounded[(int)(index)].Advance, Round(independently_rounded[(int)(index)].Advance), 0.0001f);
        Check.Near(independently_rounded[(int)(index)].Offset.X, Round(independently_rounded[(int)(index)].Offset.X), 0.0001f);
    }

    config.KeepRoundingRemainders = true;
    fixture.Services.SetDefaultFontRasterConfig(config);
    List<TextGlyph> accumulated = shape("AAAA");
    Eq((ulong)accumulated.Count,(ulong)raw.Count);
    float accumulated_width = 0.0f;
    for (ulong index = 0u; index < (ulong)raw.Count; ++index)
    {
        accumulated_width += accumulated[(int)(index)].Advance;
        Check.Near(accumulated[(int)(index)].Advance, Round(accumulated[(int)(index)].Advance), 0.0001f);
    }
    Check.That((MathF.Abs(accumulated_width - raw_width)) <= (MathF.Abs(independent_width - raw_width)));

    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);
    List<TextGlyph> raw_mark = shape("x́");
    Check.False((raw_mark.Count == 0));
    bool has_horizontal_mark_offset = false;
    foreach (TextGlyph glyph in raw_mark)
    {
        has_horizontal_mark_offset |= MathF.Abs(glyph.Offset.X) > 0.0001f;
    }
    Check.That(has_horizontal_mark_offset);

    config.SubpixelPositioning = ETextSubpixelPositioning.Disabled;
    fixture.Services.SetDefaultFontRasterConfig(config);
    List<TextGlyph> rounded_mark = shape("x́");
    Eq((ulong)rounded_mark.Count,(ulong)raw_mark.Count);
    bool horizontal_mark_offset_changed = false;
    for (ulong index = 0u; index < (ulong)raw_mark.Count; ++index)
    {
        Check.Near(
            rounded_mark[(int)(index)].Offset.X,
            Round(rounded_mark[(int)(index)].Offset.X),
            0.0001f
        );
        Check.Near(
            rounded_mark[(int)(index)].Offset.Y,
            Round(raw_mark[(int)(index)].Offset.Y),
            0.0001f
        );
        horizontal_mark_offset_changed |=
            MathF.Abs(
                rounded_mark[(int)(index)].Offset.X - raw_mark[(int)(index)].Offset.X
            ) > 0.0001f;
    }
    Check.That(horizontal_mark_offset_changed);
}

[GuiTest("gui/text-contract/shaping/automatic-subpixel-rounding-by-size")]
public static void SourceCase03()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.SubpixelPositioning = ETextSubpixelPositioning.Auto;
        fixture.Services.SetDefaultFontRasterConfig(config);

        var shape_advance = (
                                       float font_size,
                                       ETextSubpixelPositioning positioning
                                   ) => {
            config.SubpixelPositioning = positioning;
            fixture.Services.SetDefaultFontRasterConfig(config);
            TextLine line = fixture.Services.CreateLine();
            TextStyle style = MakeStyle("", font_size);
            style.FontFaces.Add(face.Id());
            line.AddString("A", style);
            Check.That(line.Shape());
            Eq((ulong)line.Glyphs().Length,1u);
            return line.Glyphs()[(int)(0)].Advance;
        };

        Check.Near(
            shape_advance(16.0f, ETextSubpixelPositioning.Auto),
            shape_advance(16.0f, ETextSubpixelPositioning.Quarter),
            0.0001f
        );
        Check.Near(
            shape_advance(18.0f, ETextSubpixelPositioning.Auto),
            shape_advance(18.0f, ETextSubpixelPositioning.Half),
            0.0001f
        );
        Check.Near(
            shape_advance(24.0f, ETextSubpixelPositioning.Auto),
            shape_advance(24.0f, ETextSubpixelPositioning.Disabled),
            0.0001f
        );
    }
}

[GuiTest("gui/text-contract/shaping/glyph-advance-overrides-follow-raster-mode")]
public static void SourceCase04()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    uint glyph = face.GlyphIndex((uint)'A');
    Ne(glyph,0u);
    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);
    face.SetGlyphAdvance(
        glyph,
        16.0f,
        new Offsetf(7.25f, 0.0f)
    );
    Check.Near(face.GlyphAdvance(glyph, 16.0f).X, 7.25f, 0.0001f);

    config.SubpixelPositioning = ETextSubpixelPositioning.Disabled;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Check.Near(face.GlyphAdvance(glyph, 16.0f).X, 7.0f, 0.0001f);

    config.Mode = ETextRasterMode.Sdf;
    config.SdfPpem = 32u;
    fixture.Services.SetDefaultFontRasterConfig(config);
    face.SetGlyphAdvance(
        glyph,
        16.0f,
        new Offsetf(14.5f, 0.0f)
    );
    Check.Near(face.GlyphAdvance(glyph, 16.0f).X, 7.25f, 0.0001f);
    Check.Near(face.GlyphAdvance(glyph, 32.0f).X, 14.5f, 0.0001f);
}

[GuiTest("gui/text-contract/shaping/embolden-increases-advance")]
public static void SourceCase05()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    uint glyph = face.GlyphIndex((uint)'A');
    Ne(glyph,0u);

    var shaped_advance = () => {
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());
        Eq((ulong)line.Glyphs().Length,1u);
        return line.Glyphs()[(int)(0)].Advance;
    };
    var measure_delta = () => {
        face.SetEmbolden(0.0f);
        float direct_before = face.GlyphAdvance(glyph, 16.0f).X;
        float shaped_before = shaped_advance();
        face.SetEmbolden(1.0f);
        float direct_after = face.GlyphAdvance(glyph, 16.0f).X;
        float shaped_after = shaped_advance();
        return new float[] {
            direct_after - direct_before,
            shaped_after - shaped_before,
        };
    };

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);
    float[] bitmap_delta = measure_delta();
    Check.That((bitmap_delta[(int)(0)]) > (0.0f));
    Check.That((bitmap_delta[(int)(1)]) > (0.0f));

    config.Mode = ETextRasterMode.Sdf;
    config.SdfPpem = 32u;
    fixture.Services.SetDefaultFontRasterConfig(config);
    float[] sdf_delta = measure_delta();
    Check.That((sdf_delta[(int)(0)]) > (0.0f));
    Check.That((sdf_delta[(int)(1)]) > (0.0f));
}

[GuiTest("gui/text-contract/shaping/fallback-is-stable-across-remainder-policy")]
public static void SourceCase06()
{
    using TextContractFixture fixture = Make(false);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.SubpixelPositioning = ETextSubpixelPositioning.Disabled;
    fixture.Services.SetDefaultFontRasterConfig(config);

    var shape = (bool keep_remainders) => {
        config.KeepRoundingRemainders = keep_remainders;
        fixture.Services.SetDefaultFontRasterConfig(config);
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString("AVAV", style);
        Check.That(line.Shape());
        List<float> advances = new();
        foreach (TextGlyph glyph in line.Glyphs())
        {
            advances.Add(glyph.Advance);
        }
        return advances;
    };

    List<float> without_remainders = shape(false);
    List<float> with_remainders = shape(true);
    Eq((ulong)without_remainders.Count,(ulong)with_remainders.Count);
    for (ulong index = 0u; index < (ulong)without_remainders.Count; ++index)
    {
        Check.Near(
            without_remainders[(int)(index)],
            with_remainders[(int)(index)],
            0.0001f
        );
    }
    Eq((ulong)without_remainders.Count,4u);
}

[GuiTest("gui/text-contract/shaping/raster-config-refreshes-dependent-lines")]
public static void SourceCase07()
{
    Action<ConfigMutation> run = (ConfigMutation mutate) => {
        using TextContractFixture fixture = Make(true);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);
        if (face is null)
        {
            return;
        }

        TextFontRasterConfig config =
            fixture.Services.DefaultFontRasterConfig();
        config.Mode = ETextRasterMode.Bitmap;
        config.LcdMode = false;
        config.Hinting = ETextHinting.Light;
        config.DisableEmbeddedBitmaps = true;
        config.SdfPpem = TextFontRasterConfig.KLowPpem;
        config.SdfSpread = 4u;
        fixture.Services.SetDefaultFontRasterConfig(config);

        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString("A", style);
        Check.That(line.Shape());
        TextRenderResult rendered = new();
        Check.That(line.Paint(Offsetf.Zero(), rendered));
        Check.False(rendered.IsEmpty());

        TextFontRasterConfig next = config;
        mutate(ref next);
        fixture.Services.SetDefaultFontRasterConfig(next);

        Check.False(line.IsReady());
        Check.That(line.Shape());
        rendered.Clear();
        Check.That(line.Paint(Offsetf.Zero(), rendered));
        Check.False(rendered.IsEmpty());
    };


    {
        run((ref TextFontRasterConfig config) => {
            config.Mode = ETextRasterMode.Sdf;
        });
    }

    {
        run((ref TextFontRasterConfig config) => {
            config.LcdMode = true;
        });
    }

    {
        run((ref TextFontRasterConfig config) => {
            config.Hinting = ETextHinting.Normal;
        });
    }

    {
        run((ref TextFontRasterConfig config) => {
            config.DisableEmbeddedBitmaps = false;
        });
    }

    {
        run((ref TextFontRasterConfig config) => {
            config.SdfPpem = TextFontRasterConfig.KMediumPpem;
        });
    }

    {
        run((ref TextFontRasterConfig config) => {
            ++config.SdfSpread;
        });
    }

    {
        run((ref TextFontRasterConfig config) => {
            config.SubpixelPositioning = ETextSubpixelPositioning.Half;
        });
    }

    {
        run((ref TextFontRasterConfig config) => {
            config.KeepRoundingRemainders = false;
        });
    }
}

[GuiTest("gui/text-contract/shaping/default-raster-config-respects-face-override")]
public static void SourceCase08()
{
    using TextContractFixture fixture = Make(true);
    FontFace? latin = PreloadFamily(fixture.Services, "Skr Test Latin");
    FontFace? hebrew = PreloadFamily(fixture.Services,
        "Skr Test Hebrew"
    );
    Check.That(latin != null);
    Check.That(hebrew != null);
    if (latin is null || hebrew is null)
    {
        return;
    }

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Check.That(fixture.Services.SetFontRasterConfig(
        latin.Id(),
        config
    ));

    var shape_and_paint = (FontFace face, Utf8StringView text) => {
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("", 16.0f);
        style.FontFaces.Add(face.Id());
        line.AddString(text, style);
        Check.That(line.Shape());
        TextRenderResult rendered = new();
        Check.That(line.Paint(Offsetf.Zero(), rendered));
        return line;
    };
    TextLine latin_line = shape_and_paint(latin, "Latin");
    TextLine hebrew_line = shape_and_paint(hebrew, "שלום");

    ++config.SdfSpread;
    fixture.Services.SetDefaultFontRasterConfig(config);

    Check.That(latin_line.IsReady());
    Check.False(hebrew_line.IsReady());
    Check.That(hebrew_line.Shape());
}

[GuiTest("gui/text-contract/shaping/raster-config-change-discards-glyph-overrides")]
public static void SourceCase09()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    uint glyph = face.GlyphIndex((uint)'A');
    Ne(glyph,0u);

    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Bitmap;
    config.Hinting = ETextHinting.Light;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    face.SetGlyphAdvance(glyph, 16.0f, new Offsetf(77.0f, 0.0f));
    Eq(face.GlyphAdvance(glyph, 16.0f),new Offsetf(77.0f, 0.0f));

    config.Hinting = ETextHinting.Normal;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Ne(face.GlyphAdvance(glyph, 16.0f),new Offsetf(77.0f, 0.0f));
    config.Hinting = ETextHinting.Light;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Ne(face.GlyphAdvance(glyph, 16.0f),new Offsetf(77.0f, 0.0f));

    face.SetGlyphAdvance(glyph, 16.0f, new Offsetf(66.0f, 0.0f));
    config.Mode = ETextRasterMode.Sdf;
    config.SdfPpem = TextFontRasterConfig.KLowPpem;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Ne(face.GlyphAdvance(glyph, 16.0f),new Offsetf(66.0f, 0.0f));
    config.Mode = ETextRasterMode.Bitmap;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Ne(face.GlyphAdvance(glyph, 16.0f),new Offsetf(66.0f, 0.0f));
}

[GuiTest("gui/text-contract/shaping/sdf-size-change-refreshes-layout")]
public static void SourceCase10()
{
    using TextContractFixture fixture = Make(true);
    FontFace? face = PreloadFamily(fixture.Services, "Skr Test Latin");
    Check.That(face != null);
    TextFontRasterConfig config =
        fixture.Services.DefaultFontRasterConfig();
    config.Mode = ETextRasterMode.Sdf;
    config.SdfPpem = TextFontRasterConfig.KLowPpem;
    config.SubpixelPositioning = ETextSubpixelPositioning.Quarter;
    fixture.Services.SetDefaultFontRasterConfig(config);

    TextLine line = fixture.Services.CreateLine();
    TextStyle style = MakeStyle("", 16.0f);
    style.FontFaces.Add(face.Id());
    line.AddString("AAAA", style);
    Check.That(line.Shape());
    Eq((ulong)line.Glyphs().Length,4u);
    TextRenderResult rendered = new();
    Check.That(line.Paint(Offsetf.Zero(), rendered));
    Check.False(rendered.IsEmpty());

    config.SdfPpem = 16u;
    fixture.Services.SetDefaultFontRasterConfig(config);
    Check.False(line.IsReady());
    Check.That(line.Shape());
    Eq((ulong)line.Glyphs().Length,4u);
    rendered.Clear();
    Check.That(line.Paint(Offsetf.Zero(), rendered));
    Check.False(rendered.IsEmpty());
}

[GuiTest("gui/text-contract/shaping/source-control-glyph-semantics")]
public static void SourceCase11()
{

    ControlCase[] cases = {
        new ControlCase( "\n", (uint)'\n', true, true, false, false ),
        new ControlCase( "\t", (uint)'\t', true, false, true, false ),
        new ControlCase( "\v", (uint)'\v', true, true, true, false ),
        new ControlCase( "\u200B", 0x200bu, true, false, false, true ),
        new ControlCase( "\u200C", 0x200cu, false, false, false, true ),
        new ControlCase( "\u200D", 0x200du, false, false, false, true ),
        new ControlCase( "\u2060", 0x2060u, false, false, false, true ),
        new ControlCase( "\uFEFF", 0xfeffu, false, false, false, true ),
    };

    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);

        foreach (ControlCase value in cases)
        {
            TextLine line = fixture.Services.CreateLine();
            line.AddString(
                value.Text,
                MakeStyle("Skr Test Latin")
            );
            Check.That(line.Shape());

            GlyphRef? glyph = source_glyph_at(line.Glyphs(), 0u);
            Check.That(glyph != null);
            if (glyph is null)
            {
                continue;
            }
            Eq(glyph.FontFace,face.Id());
            Eq(glyph.FontSize,16.0f);
            Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual));
            Eq(FlagAny(
                    glyph.Flags,
                    ETextGraphemeFlag.Space
                ),value.IsSpace);
            Eq(FlagAny(
                    glyph.Flags,
                    ETextGraphemeFlag.BreakHard
                ),value.IsHardBreak);
            Eq(FlagAny(
                    glyph.Flags,
                    ETextGraphemeFlag.Tab
                ),value.IsTab);

            if (value.IsZeroWidth)
            {
                Eq(glyph.GlyphIndex,0u);
                Eq(glyph.Advance,0.0f);
                Eq(glyph.Offset,new Offsetf());
                Check.False(glyph.IsVisible());
            }
            if (value.IsHardBreak)
            {
                Eq(glyph.Advance,0.0f);
            }
            if (advanced &&
                (value.IsHardBreak || value.IsTab))
            {
                Eq(face.GlyphIndex(value.Codepoint),0u);
                Eq(glyph.GlyphIndex,0u);
                Eq(glyph.Offset,new Offsetf());
            }
            if (value.Codepoint == (uint)'\t')
            {
                if (advanced)
                {
                    Eq(glyph.Advance,0.0f);
                }
                else
                {
                    Check.That((glyph.Advance) > (0.0f));
                }
            }
        }

        TextLine vertical_tab = fixture.Services.CreateLine();
        vertical_tab.AddString(
            "\v",
            MakeStyle("Skr Test Latin")
        );
        float tab_stop = 24.0f;
        Eq(vertical_tab.TabAlign(new float[]{tab_stop}),24.0f);
        GlyphRef? aligned_tab = source_glyph_at(
            vertical_tab.Glyphs(),
            0u
        );
        Check.That(aligned_tab != null);
        if (aligned_tab is not null)
        {
            Eq(aligned_tab.Advance,24.0f);
            Check.That(FlagAll(aligned_tab.Flags, ETextGraphemeFlag.Tab | ETextGraphemeFlag.BreakHard));
        }
    }
}

[GuiTest("gui/text-contract/shaping/preserved-default-ignorables-use-explicit-fallback")]
public static void SourceCase12()
{
    Utf8StringView[] cases = {
        "\u200C",
        "\u200D",
        "\u2060",
    };
    Utf8StringView[] zero_width_cases = {
        "\u200B",
        "\uFEFF",
    };
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        foreach (Utf8StringView text in cases)
        {
            TextLine line = fixture.Services.CreateLine();
            line.SetPreserveControl(true);
            line.AddString(
                text,
                MakeStyle("Skr Test Latin")
            );
            Check.That(line.Shape());

            GlyphRef? glyph = source_glyph_at(line.Glyphs(), 0u);
            Check.That(glyph != null);
            if (glyph is null)
            {
                continue;
            }
            Eq(glyph.Kind,ETextGlyphKind.HexBox);
            Check.That((glyph.Advance) > (0.0f));
            Check.That(glyph.IsVisible());
            Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual));
        }

        foreach (Utf8StringView text in zero_width_cases)
        {
            TextLine line = fixture.Services.CreateLine();
            line.SetPreserveControl(true);
            line.AddString(
                text,
                MakeStyle("Skr Test Latin")
            );
            Check.That(line.Shape());

            GlyphRef? glyph = source_glyph_at(line.Glyphs(), 0u);
            Check.That(glyph != null);
            if (glyph is null)
            {
                continue;
            }
            Eq(glyph.Kind,ETextGlyphKind.Glyph);
            Eq(glyph.GlyphIndex,0u);
            Eq(glyph.Advance,0.0f);
            Check.False(glyph.IsVisible());
        }
    }
}

[GuiTest("gui/text-contract/shaping/hard-break-family-backend-semantics")]
public static void SourceCase13()
{

    BreakCase[] cases = {
        new BreakCase( "\n", (uint)'\n', false ),
        new BreakCase( "\v", (uint)'\v', false ),
        new BreakCase( "\f", (uint)'\f', false ),
        new BreakCase( "\r", (uint)'\r', false ),
        new BreakCase( "\u0085", 0x0085u, false ),
        new BreakCase( "\u2028", 0x2028u, true ),
        new BreakCase( "\u2029", 0x2029u, true ),
    };

    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);
        uint space = face.GlyphIndex((uint)' ');
        Ne(space,0u);

        foreach (BreakCase value in cases)
        {
            Eq(face.GlyphIndex(value.Codepoint),0u);
            TextLine line = fixture.Services.CreateLine();
            line.AddString(
                value.Text,
                MakeStyle("Skr Test Latin")
            );
            Check.That(line.Shape());
            GlyphRef? glyph = source_glyph_at(line.Glyphs(), 0u);
            Check.That(glyph != null);
            if (glyph is null)
            {
                continue;
            }
            Check.That(FlagAll(glyph.Flags, ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakHard));
            Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.BreakSoft | ETextGraphemeFlag.Virtual));

            if (advanced)
            {
                Eq(glyph.FontFace,face.Id());
                Eq(glyph.GlyphIndex,0u);
                Eq(glyph.Offset,new Offsetf());
                Eq(glyph.Advance,0.0f);
            }
            else if (value.FallbackHexBox)
            {
                Check.False(glyph.FontFace.IsValid());
                Eq(glyph.Kind,ETextGlyphKind.HexBox);
                Eq(glyph.GlyphIndex,value.Codepoint);
                Check.That((glyph.Advance) > (0.0f));
            }
            else
            {
                Eq(glyph.FontFace,face.Id());
                Eq(glyph.GlyphIndex,space);
                Eq(glyph.Advance,0.0f);
            }
        }

        TextLine crlf = fixture.Services.CreateLine();
        crlf.AddString(
            "\r\n",
            MakeStyle("Skr Test Latin")
        );
        Check.That(crlf.Shape());
        GlyphRef? cr = source_glyph_at(crlf.Glyphs(), 0u);
        GlyphRef? lf = source_glyph_at(crlf.Glyphs(), 1u);
        Check.That(cr != null);
        Check.That(lf != null);
        if (cr is not null && lf is not null)
        {
            Check.That(FlagAny(cr.Flags, ETextGraphemeFlag.Space));
            Check.False(FlagAny(cr.Flags, ETextGraphemeFlag.BreakHard));
            Check.That(FlagAll(lf.Flags, ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakHard));
        }
    }
}

[GuiTest("gui/text-contract/shaping/terminal-fallback-preserves-source-semantics")]
public static void SourceCase14()
{
    {
        using TextContractFixture fixture = Make(false);
        TextLine line = fixture.Services.CreateLine();
        line.SetPreserveInvalid(false);
        line.AddString(
            "\n\u200CX",
            MakeStyle("Missing Family")
        );
        Check.That(line.Shape());
        Eq((ulong)line.Glyphs().Length,3u);

        GlyphRef? hard_break = source_glyph_at(
            line.Glyphs(),
            0u
        );
        GlyphRef? zero_width = source_glyph_at(
            line.Glyphs(),
            1u
        );
        GlyphRef? missing = source_glyph_at(
            line.Glyphs(),
            2u
        );
        Check.That(hard_break != null);
        Check.That(zero_width != null);
        Check.That(missing != null);
        if (hard_break is not null)
        {
            Eq(hard_break.GlyphIndex,(uint)' ');
            Eq(hard_break.Advance,0.0f);
            Check.That(FlagAll(hard_break.Flags, ETextGraphemeFlag.Space | ETextGraphemeFlag.BreakHard));
        }
        if (zero_width is not null)
        {
            Eq(zero_width.GlyphIndex,0u);
            Eq(zero_width.Advance,0.0f);
            Check.False(zero_width.IsVisible());
        }
        if (missing is not null)
        {
            Check.False(missing.FontFace.IsValid());
            Eq(missing.GlyphIndex,(uint)'X');
            Eq(missing.Advance,0.0f);
        }
        TextRenderResult paint_result = new();
        Check.That(line.Paint(Offsetf.Zero(), paint_result));
        Check.That(paint_result.IsEmpty());
    }

    {
        using TextContractFixture fixture = Make(false);
        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            "\n",
            MakeStyle("Missing Family")
        );
        Check.That(line.Shape());
        GlyphRef? glyph = source_glyph_at(line.Glyphs(), 0u);
        Check.That(glyph != null);
        if (glyph is not null)
        {
            Eq(glyph.Kind,ETextGlyphKind.HexBox);
            Eq(glyph.GlyphIndex,(uint)' ');
            Check.That((glyph.Advance) > (0.0f));
            Check.That(FlagAny(glyph.Flags, ETextGraphemeFlag.BreakHard));
        }
    }

    {
        using TextContractFixture fixture = Make(true);
        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            "\u200C",
            MakeStyle("Missing Family")
        );
        Check.That(line.Shape());
        GlyphRef? glyph = source_glyph_at(line.Glyphs(), 0u);
        Check.That(glyph != null);
        if (glyph is not null)
        {
            Check.False(glyph.FontFace.IsValid());
            Eq(glyph.Kind,ETextGlyphKind.HexBox);
            Eq(glyph.GlyphIndex,0u);
            Eq(glyph.Advance,0.0f);
            Check.False(glyph.IsVisible());
        }
        TextRenderResult paint_result = new();
        Check.That(line.Paint(Offsetf.Zero(), paint_result));
        Check.That(paint_result.IsEmpty());
    }
}

[GuiTest("gui/text-contract/shaping/trailing-control-spacing-and-fallback-kerning")]
public static void SourceCase15()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        var width = (Utf8StringView text) => {
            TextLine line = fixture.Services.CreateLine();
            line.SetSpacing(ETextSpacing.Glyph, 5.0f);
            line.AddString(
                text,
                MakeStyle("Skr Test Latin")
            );
            Check.That(line.Shape());
            return line.LineWidth();
        };
        Near(width("A\u200C"),width("A"));
        if (!advanced)
        {
            Near(width("A\n"),width("A"));
        }

        TextLine multi_span = fixture.Services.CreateLine();
        multi_span.SetSpacing(ETextSpacing.Glyph, 5.0f);
        multi_span.AddString(
            "A",
            MakeStyle("Skr Test Latin")
        );
        multi_span.AddString(
            "\n",
            MakeStyle("Skr Test Latin")
        );
        Check.That(multi_span.Shape());
        Near(multi_span.LineWidth(),width("A") + 5.0f);
    }

    using TextContractFixture fallbackFixture = Make(false);
    FontFace? face = PreloadFamily(fallbackFixture.Services, "Skr Test Latin");
    Check.That(face != null);
    byte[] nul_text = { 0 };
    TextLine nul = fallbackFixture.Services.CreateLine();
    nul.AddString(
        new Utf8StringView(nul_text),
        MakeStyle("Skr Test Latin")
    );
    Check.That(nul.Shape());
    GlyphRef? nul_glyph = source_glyph_at(nul.Glyphs(), 0u);
    Check.That(nul_glyph != null);
    if (nul_glyph is not null)
    {
        Eq(nul_glyph.FontFace,face.Id());
        Eq(nul_glyph.GlyphIndex,face.GlyphIndex((uint)' '));
        Eq(nul_glyph.Advance,0.0f);
        Check.False(FlagAny(nul_glyph.Flags, ETextGraphemeFlag.Space));
    }

    TextFontRasterConfig bitmap_config =
        fallbackFixture.Services.DefaultFontRasterConfig();
    bitmap_config.Mode = ETextRasterMode.Bitmap;
    fallbackFixture.Services.SetDefaultFontRasterConfig(bitmap_config);

    uint a = face.GlyphIndex((uint)'A');
    uint v = face.GlyphIndex((uint)'V');
    Ne(a,0u);
    Ne(v,0u);
    face.SetKerning(a, v, 16.0f, new Offsetf(-2.5f, 0.0f));
    float pair_kerning = face.Kerning(a, v, 16.0f).X;
    Eq(pair_kerning,-2.5f);

    TextLine line = fallbackFixture.Services.CreateLine();
    line.AddString(
        "AV",
        MakeStyle("Skr Test Latin")
    );
    Check.That(line.Shape());
    GlyphRef? first = source_glyph_at(line.Glyphs(), 0u);
    GlyphRef? second = source_glyph_at(line.Glyphs(), 1u);
    Check.That(first != null);
    Check.That(second != null);
    if (first is not null && second is not null)
    {
        Near(first.Advance,
                face.GlyphAdvance(a, 16.0f).X + pair_kerning
            );
        Near(second.Advance,face.GlyphAdvance(v, 16.0f).X);
    }
}

[GuiTest("gui/text-contract/shaping/deterministic-font-fallback")]
public static void SourceCase16()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? latin = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        FontFace? thai = PreloadFamily(fixture.Services,
            "Skr Test Thai"
        );
        FontFace? hebrew = PreloadFamily(fixture.Services,
            "Skr Test Hebrew"
        );
        Check.That(latin != null);
        Check.That(thai != null);
        Check.That(hebrew != null);

        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            "Aกא中",
            MakeStyle(
                "Skr Test Latin,Skr Test Thai,Skr Test Hebrew"
            )
        );
        Check.That(line.Shape());
        Check.That(glyph_buffer_is_well_formed(line.Glyphs()));

        bool saw_latin = false;
        bool saw_thai = false;
        bool saw_hebrew = false;
        bool saw_missing = false;
        foreach (TextGlyph glyph in line.Glyphs())
        {
            if (glyph.Count == 0u ||
                FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual))
            {
                continue;
            }
            switch (glyph.SourceRange.Start)
            {
            case 0u:
                saw_latin = true;
                Eq(glyph.FontFace,latin.Id());
                Eq(glyph.Kind,ETextGlyphKind.Glyph);
                break;
            case 1u:
                saw_thai = true;
                Eq(glyph.FontFace,thai.Id());
                Eq(glyph.Kind,ETextGlyphKind.Glyph);
                break;
            case 2u:
                saw_hebrew = true;
                Eq(glyph.FontFace,hebrew.Id());
                Eq(glyph.Kind,ETextGlyphKind.Glyph);
                break;
            case 3u:
                saw_missing = true;
                Eq(glyph.Kind,ETextGlyphKind.HexBox);
                break;
            default:
                break;
            }
        }
        Check.That(saw_latin);
        Check.That(saw_thai);
        Check.That(saw_hebrew);
        Check.That(saw_missing);
    }
}

[GuiTest("gui/text-contract/shaping/whole-cluster-font-fallback")]
public static void SourceCase17()
{
    using TextContractFixture fixture = Make(true);
    FontFace? arabic = PreloadFamily(fixture.Services,
        "Skr Test Arabic"
    );
    FontFace? emoji = PreloadFamily(fixture.Services,
        "Skr Test Emoji"
    );
    Check.That(arabic != null);
    Check.That(emoji != null);

    TextLine mark_cluster = fixture.Services.CreateLine();
    mark_cluster.AddString(
        "aَ",
        MakeStyle("Skr Test Latin,Skr Test Arabic")
    );
    Check.That(mark_cluster.Shape());
    Check.False(glyphs_have_kind(
        mark_cluster.Glyphs(),
        ETextGlyphKind.HexBox
    ));
    foreach (TextGlyph glyph in mark_cluster.Glyphs())
    {
        Eq(glyph.SourceRange,(new TextRange( 0u, 2u )));
        Eq(glyph.FontFace,arabic.Id());
    }

    TextLine keycap_cluster = fixture.Services.CreateLine();
    keycap_cluster.AddString(
        "1️⃣",
        MakeStyle("Skr Test Color,Skr Test Emoji")
    );
    Check.That(keycap_cluster.Shape());
    Check.False(glyphs_have_kind(
        keycap_cluster.Glyphs(),
        ETextGlyphKind.HexBox
    ));
    bool saw_keycap_cluster = false;
    foreach (TextGlyph glyph in keycap_cluster.Glyphs())
    {
        if (glyph.Count == 0u ||
            FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual))
        {
            continue;
        }
        saw_keycap_cluster = true;
        Eq(glyph.SourceRange,(new TextRange( 0u, 3u )));
        Eq(glyph.FontFace,emoji.Id());
    }
    Check.That(saw_keycap_cluster);

    TextLine monochrome_first = fixture.Services.CreateLine();
    monochrome_first.AddString(
        "1️⃣",
        MakeStyle("Skr Test Emoji,Skr Test Color")
    );
    Check.That(monochrome_first.Shape());
    bool saw_monochrome_first_cluster = false;
    foreach (TextGlyph glyph in monochrome_first.Glyphs())
    {
        if (glyph.Count == 0u ||
            FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual))
        {
            continue;
        }
        saw_monochrome_first_cluster = true;
        Eq(glyph.SourceRange,(new TextRange( 0u, 3u )));
        Eq(glyph.FontFace,emoji.Id());
    }
    Check.That(saw_monochrome_first_cluster);
}

[GuiTest("gui/text-contract/shaping/advanced-script-clusters")]
public static void SourceCase18()
{

    ScriptCase[] cases = {
        new ScriptCase("العربية","Skr Test Arabic","ar",true),
        new ScriptCase("שלום","Skr Test Hebrew","he",true),
        new ScriptCase("ภาษาไทย","Skr Test Thai","th",false),
        new ScriptCase("👩‍💻","Skr Test Emoji","und",false),
        new ScriptCase("1️⃣","Skr Test Emoji","und",false),
    };

    using TextContractFixture fixture = Make(true);
    foreach (ScriptCase value in cases)
    {
        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            value.Text,
            MakeStyle(value.Family),
            value.Language
        );
        Check.That(line.Shape());
        Check.That(glyph_buffer_is_well_formed(line.Glyphs()));
        Check.False(line.Glyphs().IsEmpty);
        Check.False(glyphs_have_kind(
            line.Glyphs(),
            ETextGlyphKind.HexBox
        ));
        foreach (TextGlyph glyph in line.Glyphs())
        {
            Check.That(float.IsFinite(glyph.Advance));
            Check.That(finite_offset(glyph.Offset));
        }
        Eq(glyphs_have_flag(line.Glyphs(), ETextGraphemeFlag.Rtl),value.RTL);
        check_visual_logical_identity(line);
    }
}

[GuiTest("gui/text-contract/shaping/thai-break-glyph-flags")]
public static void SourceCase19()
{
    using TextContractFixture fixture = Make(true);
    TextStyle style = MakeStyle("Skr Test Thai");

    TextLine spaced = fixture.Services.CreateLine();
    spaced.AddString("เป็น ภาษา ราชการ และ ภาษา", style, "th");
    Check.That(spaced.Shape());
    Eq(spaced.VisibleCharacters(),spaced.SourceRange().Length());
    ulong spaced_breaks = 0u;
    foreach (TextGlyph glyph in spaced.Glyphs())
    {
        if (glyph.Count == 0u ||
            !FlagAny(glyph.Flags, ETextGraphemeFlag.BreakSoft))
        {
            continue;
        }
        ++spaced_breaks;
        Check.That(FlagAny(glyph.Flags, ETextGraphemeFlag.Space));
        Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual));
        Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.BreakHard));
        Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.Elongation));
    }
    Eq(spaced_breaks,4u);

    TextLine unspaced = fixture.Services.CreateLine();
    unspaced.AddString("เป็นภาษาราชการและภาษา", style, "th");
    Check.That(unspaced.Shape());
    Eq(unspaced.VisibleCharacters(),unspaced.SourceRange().Length());
    ulong[] expected_boundaries = {
        4u,
        8u,
        14u,
        17u,
    };
    bool[] seen = new bool[(int)((ulong)expected_boundaries.Length)];
    ulong virtual_breaks = 0u;
    foreach (TextGlyph glyph in unspaced.Glyphs())
    {
        if (glyph.Count == 0u ||
            !FlagAny(glyph.Flags, ETextGraphemeFlag.BreakSoft))
        {
            continue;
        }
        ++virtual_breaks;
        Check.That(FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual));
        Check.That(FlagAny(glyph.Flags, ETextGraphemeFlag.Space));
        Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.BreakHard));
        Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.Elongation));
        for (ulong i = 0u; i < (ulong)expected_boundaries.Length; ++i)
        {
            if (glyph.SourceRange.End == expected_boundaries[(int)(i)])
            {
                seen[(int)(i)] = true;
            }
        }
    }
    Eq(virtual_breaks,(ulong)expected_boundaries.Length);
    foreach (bool found in seen)
    {
        Check.That(found);
    }

    TextParagraph paragraph = fixture.Services.CreateParagraph();
    paragraph.SetMaxWidth(unspaced.LineWidth() * 0.45f);
    paragraph.AddString("เป็นภาษาราชการและภาษา", style, "th");
    Check.That(paragraph.Shape());
    Check.That((paragraph.LineCount()) > (1u));
    Eq(paragraph.VisibleCharacters(),paragraph.SourceRange().Length());

    float original_width = unspaced.LineWidth();
    float fitted_width = unspaced.FitToWidth(
        original_width + 24.0f,
        ETextJustificationFlag.WordBound
    );
    Check.That((fitted_width) > (original_width));
    Check.That((fitted_width) <= (original_width + 24.0f));
}

[GuiTest("gui/text-contract/shaping/thai-arabic-fixed-line-breaking")]
public static void SourceCase20()
{

    FixedBreakCase[] cases = {
        new FixedBreakCase( "            เมาส์ตัวนี้", new TextRange[]{ new TextRange( 0u, 17u ), new TextRange( 17u, 23u ) }, 2u ),
        new FixedBreakCase( "              กู้ไฟล์", new TextRange[]{ new TextRange( 0u, 17u ), new TextRange( 17u, 21u ) }, 2u ),
        new FixedBreakCase( "             ไม่มีคำ", new TextRange[]{ new TextRange( 0u, 18u ), new TextRange( 18u, 20u ) }, 2u ),
        new FixedBreakCase( "             ไม่มีคำพูด", new TextRange[]{ new TextRange( 0u, 18u ), new TextRange( 18u, 23u ) }, 2u ),
        new FixedBreakCase( "            ไม่มีคำ", new TextRange[]{ new TextRange( 0u, 17u ), new TextRange( 17u, 19u ) }, 2u ),
        new FixedBreakCase( "         มีอุปกรณ์\nนี้", new TextRange[]{ new TextRange( 0u, 11u ), new TextRange( 11u, 19u ), new TextRange( 19u, 22u ) }, 3u ),
        new FixedBreakCase( "الحمدا لحمدا لحمـــد", new TextRange[]{ new TextRange( 0u, 13u ), new TextRange( 13u, 20u ) }, 2u ),
        new FixedBreakCase( "         الحمد test", new TextRange[]{ new TextRange( 0u, 15u ), new TextRange( 15u, 19u ) }, 2u ),
        new FixedBreakCase( "الحمـد الرياضي العربي", new TextRange[]{ new TextRange( 0u, 7u ), new TextRange( 7u, 15u ), new TextRange( 15u, 21u ) }, 3u ),
    };

    using TextContractFixture fixture = Make(true);
    TextStyle style = MakeStyle(
        "Skr Test Latin,Skr Test Thai,Skr Test Arabic"
    );
    foreach (FixedBreakCase value in cases)
    {
        TextLine line = fixture.Services.CreateLine();
        line.AddString(value.Text, style);
        Check.That(line.Shape());

        List<TextRange> ranges = new();
        Check.That(line.LineBreaks(
            90.0f,
            0u,
            TextDefaults.LineBreakFlags,
            ranges
        ));
        check_ranges(
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(ranges),
            value.Ranges.AsSpan(0,(int)value.RangeCount)
        );
    }
}

[GuiTest("gui/text-contract/shaping/span-object-run-projection")]
public static void SourceCase21()
{
    using TextContractFixture fixture = Make(true);
    TextLine line = fixture.Services.CreateLine();
    TextLayoutSpanId latin = line.AddString(
        "AB",
        MakeStyle("Skr Test Latin", 16.0f),
        "en"
    );
    Check.That(line.AddObject(
        77u,
        new Sizef(12.0f, 18.0f),
        ETextInlineAlignment.Center,
        1u
    ));
    TextLayoutSpanId object_rect = line.SpanId(1u);
    TextLayoutSpanId hebrew = line.AddString(
        "אב",
        MakeStyle("Skr Test Hebrew", 20.0f),
        "he"
    );
    Check.That(line.Shape());
    Check.That((line.RunCount()) >= (3u));

    bool saw_latin = false;
    bool saw_object = false;
    bool saw_hebrew = false;
    for (ulong run = 0u; run < line.RunCount(); ++run)
    {
        TextRange source = line.RunSourceRange(run);
        TextRange glyphs = line.RunGlyphRange(run);
        Check.That((source.End) <= (line.SourceRange().End));
        Check.That((glyphs.End) <= (line.GlyphCount()));
        Check.That((source.Start) <= (source.End));
        Check.That((glyphs.Start) <= (glyphs.End));

        ulong object_key = 0u;
        if (line.RunSpanId(run) == latin)
        {
            saw_latin = true;
            Eq(line.RunText(run),"AB");
            Eq(line.RunFontSize(run),16.0f);
            Eq(line.RunLanguage(run),"en");
            Check.False(line.RunObject(run, ref object_key));
            Check.That(line.RunFontFace(run) != null);
        }
        else if (line.RunSpanId(run) == object_rect)
        {
            saw_object = true;
            Check.That(line.RunObject(run, ref object_key));
            Eq(object_key,77u);
        }
        else if (line.RunSpanId(run) == hebrew)
        {
            saw_hebrew = true;
            Eq(line.RunText(run),"אב");
            Eq(line.RunFontSize(run),20.0f);
            Eq(line.RunLanguage(run),"he");
            Eq(line.RunDirection(run),ETextDirection.RTL);
            Check.False(line.RunObject(run, ref object_key));
            Check.That(line.RunFontFace(run) != null);
        }
    }
    Check.That(saw_latin);
    Check.That(saw_object);
    Check.That(saw_hebrew);

    Check.That(line.ResizeObject(
        77u,
        new Sizef(20.0f, 24.0f),
        ETextInlineAlignment.BaselineTo,
        17.0f
    ));
    Check.That(line.IsReady());
    Eq(line.ObjectRect(77u).Size(),new Sizef(20.0f, 24.0f));
}
}
