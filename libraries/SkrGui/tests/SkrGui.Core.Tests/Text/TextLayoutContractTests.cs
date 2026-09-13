using static SkrGui.Tests.TextContractFixture;
namespace SkrGui.Tests;
// All cases/loops/inputs from tests/text/text_layout_contract_tests.cpp @ 611561f8.
public static class TextLayoutContractTests {
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
private sealed class DirectionCase {
        public ETextDirectionMode Direction = ETextDirectionMode.LTR;
        public Utf8StringView Family = new();
        public Utf8StringView Text = new();
     }
private sealed class TextAlignmentCase {
        public ETextDirectionMode Direction = ETextDirectionMode.LTR;
        public Utf8StringView Family = new();
        public Utf8StringView LongText = new();
        public Utf8StringView ShortText = new();
     }
private static bool same_glyph_identity(TextGlyph lhs, TextGlyph rhs)
{
    return lhs.SourceRange == rhs.SourceRange &&
        lhs.SpanId == rhs.SpanId &&
        lhs.FontFace == rhs.FontFace &&
        lhs.Offset == rhs.Offset &&
        lhs.Advance == rhs.Advance &&
        lhs.FontSize == rhs.FontSize &&
        lhs.GlyphIndex == rhs.GlyphIndex &&
        lhs.Count == rhs.Count &&
        lhs.Repeat == rhs.Repeat &&
        lhs.Flags == rhs.Flags &&
        lhs.Kind == rhs.Kind &&
        lhs.ObjectKey == rhs.ObjectKey;
}

private static Rectf selected_bounds(TextLine line)
{
    List<Rectf> rects = new();
    Check.That(line.SelectionRects(line.SourceRange(), rects));
    Check.False((rects.Count == 0));

    Rectf result = rects[(int)(0)];
    for (ulong i = 1u; i < (ulong)rects.Count; ++i)
    {
        result = result.Unite(rects[(int)(i)]);
    }
    return result;
}

private static Rectf selected_line_bounds(TextParagraph paragraph, ulong line)
{
    List<Rectf> rects = new();
    Check.That(paragraph.SelectionRects(
        paragraph.LineRange(line),
        rects
    ));
    Check.False((rects.Count == 0));

    Rectf result = rects[(int)(0)];
    for (ulong i = 1u; i < (ulong)rects.Count; ++i)
    {
        result = result.Unite(rects[(int)(i)]);
    }
    return result;
}

private static Rectf painted_span_bounds(
    TextRenderResult result,
    TextLayoutSpanId span_id
)
{
    bool found = false;
    Rectf bounds = new();
    foreach (TextRenderRect rect in result.Rects)
    {
        if (rect.SpanId != span_id)
        {
            continue;
        }
        bounds = found ? bounds.Unite(rect.Rect) : rect.Rect;
        found = true;
    }
    Check.That(found);
    return bounds;
}

private static float alignment_offset(
    ETextHAlign alignment,
    float paragraph_width,
    float line_width
)
{
    if (alignment == ETextHAlign.Center)
    {
        return MathF.Floor((paragraph_width - line_width) * 0.5f);
    }
    if (alignment == ETextHAlign.Right)
    {
        return paragraph_width - line_width;
    }
    return 0.0f;
}

private static float frame_alignment_offset(
    ETextHAlign alignment,
    float frame_width,
    float line_width,
    ETextDirectionMode direction,
    bool anchor_rtl_fill_to_frame_end
)
{
    bool rtl = direction == ETextDirectionMode.RTL;
    if (alignment == ETextHAlign.Fill)
    {
        return anchor_rtl_fill_to_frame_end && rtl ?
            frame_width - line_width :
            0.0f;
    }
    if (alignment == ETextHAlign.Center)
    {
        return line_width <= frame_width ?
            MathF.Floor((frame_width - line_width) * 0.5f) :
            (rtl ? frame_width - line_width : 0.0f);
    }
    if (alignment == ETextHAlign.Right)
    {
        return frame_width - line_width;
    }
    return 0.0f;
}

private static float glyph_stream_width(ReadOnlySpan<TextGlyph> glyphs)
{
    float result = 0.0f;
    foreach (TextGlyph glyph in glyphs)
    {
        result += glyph.Advance *
            (float)(Math.Max(1u, glyph.Repeat));
    }
    return result;
}

private static float object_pen(ReadOnlySpan<TextGlyph> glyphs, ulong key)
{
    float result = 0.0f;
    foreach (TextGlyph glyph in glyphs)
    {
        if (glyph.Kind == ETextGlyphKind.Object &&
            glyph.ObjectKey == key)
        {
            return result;
        }
        result += glyph.Advance *
            (float)(Math.Max(1u, glyph.Repeat));
    }
    return result;
}

private static void check_actual_size(TextParagraph paragraph)
{
    ulong visible_lines = paragraph.MaxLinesVisible() < 0 ?
        paragraph.LineCount() :
        Math.Min(
            paragraph.LineCount(),
            (ulong)(paragraph.MaxLinesVisible())
        );
    float actual_width = 0.0f;
    for (ulong line = 0u; line < visible_lines; ++line)
    {
        Near(paragraph.LineSize(line).Width,paragraph.LineWidth(line));
        actual_width = Math.Max(actual_width, paragraph.LineWidth(line));
    }
    Near(paragraph.Size().Width,actual_width);
}

private static void check_ascii_character_navigation(TextLine line)
{
    List<ulong> breaks = new();
    Check.That(line.CharacterBreaks(breaks));
    Eq((ulong)breaks.Count,3u);
    if ((ulong)breaks.Count == 3u)
    {
        Eq(breaks[(int)(0)],1u);
        Eq(breaks[(int)(1)],2u);
        Eq(breaks[(int)(2)],3u);
    }
    Eq(line.PreviousCharacterPosition(2u),1);
    Eq(line.NextCharacterPosition(1u),2);
    Eq(line.ClosestCharacterPosition(2u),2);
    Eq(line.RangeDirection(new TextRange(0u,3u )),ETextDirection.LTR);
}

[GuiTest("gui/text-contract/line/max-width-and-actual-size")]
public static void SourceCase01()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
        line.AddString(
            "MMMM",
            MakeStyle("Skr Test Latin")
        );
        Check.That(line.Shape());

        float natural_width = line.LineWidth();
        float glyph_width = glyph_stream_width(line.Glyphs());
        float max_width = natural_width + 100.0f;
        ulong glyph_count = line.GlyphCount();
        ETextHAlign[] alignments = {
            ETextHAlign.Left,
            ETextHAlign.Center,
            ETextHAlign.Right,
        };

        Rectf reference_paint = new();

        for (ulong i = 0u; i < (ulong)alignments.Length; ++i)
        {
            ETextHAlign alignment = alignments[(int)(i)];
            line.SetMaxWidth(max_width);
            line.SetAlignment(alignment);
            Check.That(line.Shape());

            Eq(line.MaxWidth(),max_width);
            Near(line.Size().Width,natural_width);
            Near(line.LineWidth(),natural_width);
            Eq(line.GlyphCount(),glyph_count);

            Rectf selection = selected_bounds(line);
            Near(selection.Left,0.0f);
            Near(selection.Right,glyph_width);
            TextRenderResult paint = new();
            Check.That(line.Paint(Offsetf.Zero(), paint));
            Check.False(paint.IsEmpty());
            if (i == 0u)
            {
                reference_paint = paint.Bounds;
            }
            Near(paint.Bounds.Left - reference_paint.Left,alignment_offset(
                    alignment,
                    max_width,
                    natural_width
                ));
        }
    }
}

[GuiTest("gui/text-contract/line/presentation-projection-is-reversible")]
public static void SourceCase02()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        line.AddString(
            "Alpha beta",
            MakeStyle("Skr Test Latin")
        );
        Check.That(line.Shape());

        float natural_width = line.LineWidth();
        float fill_width = natural_width + 48.0f;
        line.SortLogicalGlyphs();
        line.SetMaxWidth(fill_width);
        line.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.DoNotSkipSingleLine
        );
        line.SetAlignment(ETextHAlign.Fill);
        Check.That(line.Shape());
        Near(line.LineWidth(),fill_width);
        Near(glyph_stream_width(line.Glyphs()),line.LineWidth());
        Near(glyph_stream_width(line.SortLogicalGlyphs()),line.LineWidth());

        line.SetAlignment(ETextHAlign.Left);
        Check.That(line.Shape());
        Near(line.LineWidth(),natural_width);

        line.SetMaxWidth(natural_width * 0.5f);
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        Check.That(line.Shape());
        Ne(line.TrimPosition(),ulong.MaxValue);

        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        Check.That(line.Shape());
        Eq(line.TrimPosition(),ulong.MaxValue);
        Near(line.LineWidth(),natural_width);
    }
}

[GuiTest("gui/text-contract/layout/trimmed-width-follows-backend-contract")]
public static void SourceCase03()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextStyle style = MakeStyle("Skr Test Latin");

        TextLine line = fixture.Services.CreateLine();
        line.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
        line.AddString("ABCDEFGHIJKLMNOPQRSTUVWXYZ", style);
        Check.That(line.Shape());
        float line_natural_width = line.LineWidth();
        line.SetMaxWidth(line_natural_width * 0.5f);
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        Check.That(line.Shape());
        Ne(line.TrimPosition(),ulong.MaxValue);
        if (advanced)
        {
            Check.That((line.LineWidth()) < (line_natural_width));
        }
        else
        {
            Near(line.LineWidth(),line_natural_width);
        }
        Near(line.Size().Width,line.LineWidth());
        Eq(line.HitTestPosition(line.LineWidth()),(long)(line.SourceRange().End));
        Eq(line.HitTestPosition(line.LineWidth() - 0.25f),(long)(line.SourceRange().End));
        if (!advanced)
        {
            float full_width_probe =
                (line.MaxWidth() + line.LineWidth()) * 0.5f;
            Check.That((line.HitTestPosition(full_width_probe)) < ((long)(line.SourceRange().End)));
        }

        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString("ABCDEFGHIJKLMNOPQRSTUVWXYZ\nA", style);
        Check.That(paragraph.Shape());
        float paragraph_natural_width = paragraph.LineWidth(0u);
        paragraph.SetMaxWidth(paragraph_natural_width * 0.5f);
        paragraph.SetMaxLinesVisible(1);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        Check.That(paragraph.Shape());
        Ne(paragraph.TrimPosition(),ulong.MaxValue);
        if (advanced)
        {
            Check.That((paragraph.LineWidth(0u)) < (paragraph_natural_width));
        }
        else
        {
            Near(paragraph.LineWidth(0u),paragraph_natural_width);
        }
        Near(paragraph.LineSize(0u).Width,paragraph.LineWidth(0u));
        Near(paragraph.Size().Width,paragraph.LineWidth(0u));
    }
}

[GuiTest("gui/text-contract/line/object-follows-advance-projection")]
public static void SourceCase04()
{
    ulong kObject = 900u;
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        TextStyle style = MakeStyle("Skr Test Latin");
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        line.AddString("A\tB", style);
        Check.That(line.AddObject(
            kObject,
            new Sizef(20.0f, 16.0f),
            ETextInlineAlignment.BaselineTo,
            0u,
            12.0f
        ));
        line.AddString(" C", style);

        float[] first_stops = { 24.0f, 48.0f };
        line.TabAlign(first_stops);
        Check.That(line.Shape());
        line.SortLogicalGlyphs();
        Near(line.ObjectRect(kObject).Left,object_pen(line.Glyphs(), kObject));

        float[] second_stops = { 72.0f };
        line.TabAlign(second_stops);
        Check.That(line.Shape());
        Near(line.ObjectRect(kObject).Left,object_pen(line.Glyphs(), kObject));
        Near(MathF.Ceiling(glyph_stream_width(line.SortLogicalGlyphs())),line.LineWidth());

        line.SetMaxWidth(line.LineWidth() + 40.0f);
        line.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.DoNotSkipSingleLine
        );
        line.SetAlignment(ETextHAlign.Fill);
        Check.That(line.Shape());
        Near(line.ObjectRect(kObject).Left,object_pen(line.Glyphs(), kObject));

        TextLine compact = fixture.Services.CreateLine();
        compact.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        compact.AddString("A\tB", style);
        List<TextRange> compact_ranges = new();
        Check.That(compact.LineBreaks(
            60.0f,
            0u,
            TextDefaults.LineBreakFlags,
            compact_ranges
        ));
        Eq((ulong)compact_ranges.Count,1u);

        float[] break_stops = { 72.0f };
        compact.TabAlign(break_stops);
        List<TextRange> projected_ranges = new();
        Check.That(compact.LineBreaks(
            60.0f,
            0u,
            TextDefaults.LineBreakFlags,
            projected_ranges
        ));
        Check.That(((ulong)projected_ranges.Count) > ((ulong)compact_ranges.Count));
    }
}

[GuiTest("gui/text-contract/line/full-range-projection-preserves-root")]
public static void SourceCase05()
{
    ulong kObject = 901u;
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
        line.AddString(
            "אב",
            MakeStyle("Skr Test Hebrew")
        );
        Check.That(line.AddObject(
            kObject,
            new Sizef(12.0f, 16.0f),
            ETextInlineAlignment.Center,
            0u
        ));
        line.AddString(
            "ab\u00AD",
            MakeStyle("Skr Test Latin")
        );
        Check.That(line.Shape());
        List<TextRange> ranges = new();
        Check.That(line.LineBreaks(
            1000.0f,
            0u,
            ETextLineBreakFlag.Mandatory,
            ranges
        ));

        List<TextGlyph> original = new();
        ulong original_object_count = 0u;
        foreach (TextGlyph glyph in line.Glyphs())
        {
            original.Add(glyph);
            if (glyph.Kind == ETextGlyphKind.Object &&
                glyph.ObjectKey == kObject)
            {
                ++original_object_count;
            }
        }
        Rectf original_object = line.ObjectRect(kObject);
        Sizef original_size = line.Size();
        Eq(original_object_count,1u);

        float[] tab_stops = { 100.0f };
        line.TabAlign(tab_stops);
        Check.That(line.Shape());
        Eq(line.Size(),original_size);
        Eq(line.ObjectRect(kObject),original_object);
        ReadOnlySpan<TextGlyph> projected = line.Glyphs();
        Eq((ulong)projected.Length,(ulong)original.Count);
        ulong projected_object_count = 0u;
        for (ulong i = 0u; i < (ulong)projected.Length; ++i)
        {
            Check.That(same_glyph_identity(projected[(int)(i)], original[(int)(i)]),string.Concat(new object?[]{"backend=",advanced ? "advanced" : "fallback"," glyph=",i," original advance=",original[(int)(i)].Advance," projected advance=",projected[(int)(i)].Advance," original offset=",original[(int)(i)].Offset.X,",",original[(int)(i)].Offset.Y," projected offset=",projected[(int)(i)].Offset.X,",",projected[(int)(i)].Offset.Y," original index=",original[(int)(i)].GlyphIndex," projected index=",projected[(int)(i)].GlyphIndex}));
            if (projected[(int)(i)].Kind == ETextGlyphKind.Object &&
                projected[(int)(i)].ObjectKey == kObject)
            {
                ++projected_object_count;
            }
        }
        Eq(projected_object_count,1u);
    }
}

[GuiTest("gui/text-contract/layout/root-and-substring-share-line-metrics")]
public static void SourceCase06()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextStyle style = MakeStyle("Skr Test Latin");

        TextLine line = fixture.Services.CreateLine();
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        line.AddString("Ag", style);
        Check.That(line.Shape());

        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(1000.0f);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString("Ag", style);
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),1u);
        Near(line.LineAscent(),paragraph.LineAscent(0u));
        Near(line.LineDescent(),paragraph.LineDescent(0u));
        Near(line.Size().Height,paragraph.LineSize(0u).Height);

        TextLine empty_line = fixture.Services.CreateLine();
        empty_line.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        empty_line.AddString(default, style);
        Check.That(empty_line.Shape());
        Eq(empty_line.Size().Height,0.0f);
    }
}

[GuiTest("gui/text-contract/layout/ligature-hit-exposes-source-boundaries")]
public static void SourceCase07()
{
    using TextContractFixture fixture = Make(true);
    TextStyle style = MakeStyle("Skr Test Latin");
    style.OpenTypeFeatures.Add(new FontOpenTypeFeatureValue { Tag = fixture.Services.OpenTypeNameToTag("dlig"), Value = 1u,
    });

    TextLine line = fixture.Services.CreateLine();
    line.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
    line.AddString("!?", style, "en");
    Check.That(line.Shape());

    TextRange ligature_range = new();
    foreach (TextGlyph glyph in line.Glyphs())
    {
        if (glyph.Count > 0u && glyph.SourceRange.Length() > 1u)
        {
            ligature_range = glyph.SourceRange;
            break;
        }
    }
    Check.That((ligature_range.Length()) > (1u));
    TextFloatRange line_bounds = line.GraphemeBounds(
        ligature_range.Start
    );
    Check.That((line_bounds.Length()) > (0.0f));
    ulong source_count = ligature_range.Length();
    float source_width = line_bounds.Length() /
        (float)(source_count);
    for (ulong i = 0u; i < source_count; ++i)
    {
        Eq(line.HitTestPosition(
                line_bounds.Start + source_width * ((float)(i) + 0.25f)
            ),(long)(ligature_range.Start + i));
        Eq(line.HitTestPosition(
                line_bounds.Start + source_width * ((float)(i) + 0.75f)
            ),(long)(ligature_range.Start + i + 1u));
    }

    TextParagraph paragraph = fixture.Services.CreateParagraph();
    paragraph.SetMaxWidth(1000.0f);
    paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
    paragraph.SetTextOverrunBehavior(
        ETextOverrunBehavior.NoTrimming
    );
    paragraph.AddString("X\n!?", style, "en");
    Check.That(paragraph.Shape());
    Eq(paragraph.LineCount(),2u);

    TextRange second_glyphs = paragraph.LineGlyphRange(1u);
    TextRange paragraph_ligature_range = new();
    for (ulong i = second_glyphs.Start; i < second_glyphs.End; ++i)
    {
        TextGlyph glyph = paragraph.Glyphs()[(int)(i)];
        if (glyph.Count > 0u && glyph.SourceRange.Length() > 1u)
        {
            paragraph_ligature_range = glyph.SourceRange;
            break;
        }
    }
    Check.That((paragraph_ligature_range.Length()) > (1u));
    TextFloatRange paragraph_bounds = paragraph.GraphemeBounds(
        paragraph_ligature_range.Start
    );
    Check.That((paragraph_bounds.Length()) > (0.0f));
    float paragraph_source_width = paragraph_bounds.Length() /
        (float)(paragraph_ligature_range.Length());
    float second_line_y = paragraph.LineSize(0u).Height +
        paragraph.LineSpacing() +
        paragraph.LineSize(1u).Height * 0.5f;
    Eq(paragraph.HitTestPosition(new Offsetf(
            paragraph_bounds.Start + paragraph_source_width * 1.75f,
            second_line_y
        )),(long)(paragraph_ligature_range.Start + 2u));
}

[GuiTest("gui/text-contract/paragraph/draw-frame-and-local-editing")]
public static void SourceCase08()
{

    TextAlignmentCase[] cases = {
        new TextAlignmentCase { Direction = ETextDirectionMode.LTR, Family = "Skr Test Latin", LongText = "MMMM\n", ShortText = "M",
        },
        new TextAlignmentCase { Direction = ETextDirectionMode.RTL, Family = "Skr Test Hebrew", LongText = "שששש\n", ShortText = "ש",
        },
    };
    ETextHAlign[] alignments = {
        ETextHAlign.Left,
        ETextHAlign.Center,
        ETextHAlign.Right,
    };

    foreach (bool advanced in kBackends)
    {
        foreach (TextAlignmentCase test_case in cases)
        {
            using TextContractFixture fixture = Make(advanced);
            TextParagraph paragraph = fixture.Services.CreateParagraph();
            paragraph.SetDirection(test_case.Direction);
            paragraph.SetMaxWidth(200.0f);
            paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
            paragraph.SetTextOverrunBehavior(
                ETextOverrunBehavior.NoTrimming
            );
            TextStyle style = MakeStyle(test_case.Family.ToString());
            paragraph.AddString(test_case.LongText, style);
            paragraph.AddString(test_case.ShortText, style);

            Sizef reference_size = new();
            Rectf reference_long_selection = new();
            Rectf reference_short_selection = new();
            Rectf reference_long_paint = new();
            Rectf reference_short_paint = new();
            Rectf reference_paint_line = new();
            for (ulong i = 0u; i < (ulong)alignments.Length; ++i)
            {
                ETextHAlign alignment = alignments[(int)(i)];
                paragraph.SetAlignment(alignment);
                Check.That(paragraph.Shape());
                Eq(paragraph.LineCount(),2u);
                check_actual_size(paragraph);

                float long_width = paragraph.LineWidth(0u);
                float short_width = paragraph.LineWidth(1u);
                Check.That((long_width) > (short_width));
                Check.That((paragraph.Size().Width) < (200.0f));

                Rectf long_selection =
                    selected_line_bounds(paragraph, 0u);
                Rectf short_selection =
                    selected_line_bounds(paragraph, 1u);
                TextRenderResult paint = new();
                Check.That(paragraph.Paint(Offsetf.Zero(), paint));
                Rectf long_paint = painted_span_bounds(
                    paint,
                    paragraph.SpanId(0u)
                );
                Rectf short_paint = painted_span_bounds(
                    paint,
                    paragraph.SpanId(1u)
                );
                TextRenderResult paint_line = new();
                Check.That(paragraph.PaintLine(
                    1u,
                    Offsetf.Zero(),
                    paint_line
                ));
                Rectf short_line_paint = painted_span_bounds(
                    paint_line,
                    paragraph.SpanId(1u)
                );

                if (i == 0u)
                {
                    reference_size = paragraph.Size();
                    reference_long_selection = long_selection;
                    reference_short_selection = short_selection;
                    reference_long_paint = long_paint;
                    reference_short_paint = short_paint;
                    reference_paint_line = short_line_paint;
                }
                else
                {
                    Eq(paragraph.Size(),reference_size);
                }
                Near(long_selection.Left - reference_long_selection.Left,0.0f);
                Near(short_selection.Left - reference_short_selection.Left,0.0f);
                Near(long_paint.Left - reference_long_paint.Left,alignment_offset(
                        alignment,
                        200.0f,
                        long_width
                    ));
                Near(short_paint.Left - reference_short_paint.Left,alignment_offset(
                        alignment,
                        200.0f,
                        short_width
                    ));
                Near(short_line_paint.Left - reference_paint_line.Left,0.0f);
            }
        }
    }
}

[GuiTest("gui/text-contract/paragraph/equal-lines-ignore-hard-break-geometry")]
public static void SourceCase09()
{
    ETextHAlign[] alignments = {
        ETextHAlign.Left,
        ETextHAlign.Center,
        ETextHAlign.Right,
    };
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(200.0f);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString(
            "MM\nMM",
            MakeStyle("Skr Test Latin")
        );

        foreach (ETextHAlign alignment in alignments)
        {
            paragraph.SetAlignment(alignment);
            Check.That(paragraph.Shape());
            Eq(paragraph.LineCount(),2u);
            Near(paragraph.LineWidth(0u),paragraph.LineWidth(1u));

            Rectf first = selected_line_bounds(paragraph, 0u);
            Rectf second = selected_line_bounds(paragraph, 1u);
            Near(first.Left,second.Left);
            Near(first.Right,second.Right);
        }
    }
}

[GuiTest("gui/text-contract/paragraph/explicit-empty-lines-keep-font-metrics")]
public static void SourceCase10()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? face = PreloadFamily(fixture.Services,
            "Skr Test Latin"
        );
        Check.That(face != null);

        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString(
            "\n\nM",
            MakeStyle("Skr Test Latin")
        );
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),3u);
        Eq(paragraph.LineRange(0u),(new TextRange( 0u, 1u )));
        Eq(paragraph.LineRange(1u),(new TextRange( 1u, 2u )));
        Eq(paragraph.LineRange(2u),(new TextRange( 2u, 3u )));
        Eq(paragraph.LineWidth(0u),0.0f);
        Eq(paragraph.LineWidth(1u),0.0f);
        Check.That((paragraph.LineWidth(2u)) > (0.0f));

        float line_height = paragraph.LineSize(2u).Height;
        Check.That((line_height) > (0.0f));
        Near(paragraph.LineSize(0u).Height,line_height);
        Near(paragraph.LineSize(1u).Height,line_height);

        for (ulong line = 0u; line < 2u; ++line)
        {
            TextRange glyph_range =
                paragraph.LineGlyphRange(line);
            Check.That((glyph_range.Start) < (glyph_range.End));
            if (glyph_range.Start >= glyph_range.End)
            {
                continue;
            }
            TextGlyph glyph =
                paragraph.Glyphs()[(int)(glyph_range.Start)];
            Eq(glyph.FontFace,face.Id());
            Eq(glyph.FontSize,16.0f);
            Check.False(FlagAny(glyph.Flags, ETextGraphemeFlag.Virtual));
        }

        TextParagraph trailing_break =
            fixture.Services.CreateParagraph();
        trailing_break.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        trailing_break.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        trailing_break.AddString(
            "A\n",
            MakeStyle("Skr Test Latin")
        );
        Check.That(trailing_break.Shape());
        Eq(trailing_break.LineCount(),1u);
        Eq(trailing_break.LineRange(0u),(new TextRange( 0u, 2u )));
    }
}

[GuiTest("gui/text-contract/line/control-editing-geometry")]
public static void SourceCase11()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            "A\n\u200B\u200C",
            MakeStyle("Skr Test Latin")
        );
        Check.That(line.Shape());

        List<Rectf> selection = new();
        Check.That(line.SelectionRects(new TextRange(1u,2u ), selection));
        Check.False((selection.Count == 0));
        if (!(selection.Count == 0))
        {
            Eq(selection[(int)(0)].Width(),0.0f);
        }
        TextFloatRange shared_boundary =
            line.GraphemeBounds(1u);
        Eq(shared_boundary,line.GraphemeBounds(0u));
        Check.That((shared_boundary.Length()) > (0.0f));

        selection.Clear();
        Check.That(line.SelectionRects(new TextRange(2u,3u ), selection));
        Check.False((selection.Count == 0));
        TextFloatRange zero_width_space =
            line.GraphemeBounds(2u);
        Check.That((zero_width_space.Start) > (0.0f));
        Eq(zero_width_space.Start,zero_width_space.End);

        selection.Clear();
        Check.That(line.SelectionRects(new TextRange(3u,4u ), selection));
        Check.That((selection.Count == 0));

        TextCaretInfo caret = line.Caret(1u);
        Check.That((caret.LeadingCaret.Left) > (0.0f));
        Check.That((caret.TrailingCaret.Left) > (0.0f));

        TextLine no_geometry = fixture.Services.CreateLine();
        no_geometry.AddString(
            "\u200C",
            MakeStyle("Missing Family")
        );
        Check.That(no_geometry.Shape());
        Eq((ulong)no_geometry.Glyphs().Length,1u);
        if (!no_geometry.Glyphs().IsEmpty)
        {
            TextGlyph glyph = no_geometry.Glyphs()[(int)(0u)];
            Eq(glyph.Kind,ETextGlyphKind.HexBox);
            Eq(glyph.GlyphIndex,0u);
            Eq(glyph.Advance,0.0f);
            Check.False(glyph.IsVisible());
        }
        selection.Clear();
        Check.That(no_geometry.SelectionRects(
            new TextRange(0u,1u ),
            selection
        ));
        Check.That((selection.Count == 0));
        Eq(no_geometry.GraphemeBounds(0u),new TextFloatRange());
        TextRenderResult missing_result = new();
        Check.That(no_geometry.Paint(
            Offsetf.Zero(),
            missing_result
        ));
        Check.That(missing_result.IsEmpty());
    }
}

[GuiTest("gui/text-contract/line/fill-is-not-paragraph-last-line-policy")]
public static void SourceCase12()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.SetMaxWidth(160.0f);
        line.SetAlignment(ETextHAlign.Fill);
        line.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
        line.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.SkipLastLine
        );
        line.AddString(
            "Alpha beta",
            MakeStyle("Skr Test Latin")
        );
        Check.That(line.Shape());
        Near(line.LineWidth(),line.MaxWidth());




        line.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.DoNotSkipSingleLine
        );
        Check.That(line.Shape());
        Near(line.LineWidth(),line.MaxWidth());
        Near(line.Size().Width,line.MaxWidth());

        TextLine no_gap = fixture.Services.CreateLine();
        no_gap.SetMaxWidth(160.0f);
        no_gap.SetAlignment(ETextHAlign.Fill);
        no_gap.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
        no_gap.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.DoNotSkipSingleLine
        );
        no_gap.AddString(
            "MMMM",
            MakeStyle("Skr Test Latin")
        );
        Check.That(no_gap.Shape());
        Check.That((no_gap.LineWidth()) < (no_gap.MaxWidth()));
        Near(no_gap.Size().Width,no_gap.LineWidth());
    }
}

[GuiTest("gui/text-contract/paragraph/zero-index-box-is-not-visible-text")]
public static void SourceCase13()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(180.0f);
        paragraph.SetAlignment(ETextHAlign.Fill);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.SkipLastLineWithVisibleChars
        );
        paragraph.AddString(
            "Alpha beta\n",
            MakeStyle("Skr Test Latin")
        );
        paragraph.AddString(
            "\u200C",
            MakeStyle("Missing Family")
        );

        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),2u);
        Check.That((paragraph.LineWidth(0u)) < (180.0f));
        Eq(paragraph.LineWidth(1u),0.0f);
        TextRange last_glyphs = paragraph.LineGlyphRange(1u);
        Check.That((last_glyphs.Start) < (last_glyphs.End));
        if (last_glyphs.Start < last_glyphs.End)
        {
            TextGlyph glyph = paragraph.Glyphs()[(int)(last_glyphs.Start)];
            Eq(glyph.Kind,ETextGlyphKind.HexBox);
            Eq(glyph.GlyphIndex,0u);
            Check.False(glyph.IsVisible());
        }
    }
}

[GuiTest("gui/text-contract/paragraph/alignment-moves-objects-with-glyphs")]
public static void SourceCase14()
{
    ulong kObjectKey = 42u;
    ETextHAlign[] alignments = {
        ETextHAlign.Left,
        ETextHAlign.Center,
        ETextHAlign.Right,
    };

    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(240.0f);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString(
            "MMMMMMMM\n",
            MakeStyle("Skr Test Latin")
        );
        Check.That(paragraph.AddObject(
            kObjectKey,
            new Sizef(12.0f, 16.0f),
            ETextInlineAlignment.Center,
            1u
        ));
        paragraph.AddString(
            "A",
            MakeStyle("Skr Test Latin")
        );

        float left_object_x = 0.0f;
        Sizef reference_size = new();
        for (ulong i = 0u; i < (ulong)alignments.Length; ++i)
        {
            ETextHAlign alignment = alignments[(int)(i)];
            paragraph.SetAlignment(alignment);
            Check.That(paragraph.Shape());
            Eq(paragraph.LineCount(),2u);
            check_actual_size(paragraph);

            float short_width = paragraph.LineWidth(1u);
            float expected_offset = alignment_offset(
                alignment,
                240.0f,
                short_width
            );
            Rectf object_rect = paragraph.LineObjectRect(
                1u,
                kObjectKey
            );
            Rectf paragraph_object = paragraph.ObjectRect(
                kObjectKey
            );
            Near(object_rect.Width(),12.0f);
            Near(object_rect.Height(),16.0f);
            Eq(paragraph_object,object_rect);

            if (i == 0u)
            {
                left_object_x = object_rect.Left;
                reference_size = paragraph.Size();
            }
            else
            {
                Eq(paragraph.Size(),reference_size);
            }
            Near(object_rect.Left - left_object_x,expected_offset);
        }
    }
}

[GuiTest("gui/text-contract/layout/overflow-frame-alignment")]
public static void SourceCase15()
{
    ulong kLineObject = 1001u;
    ulong kParagraphObject = 1002u;

    DirectionCase[] directions = {
        new DirectionCase { Direction = ETextDirectionMode.LTR, Family = "Skr Test Latin", Text = "MMMMMMMM",
        },
        new DirectionCase { Direction = ETextDirectionMode.RTL, Family = "Skr Test Hebrew", Text = "שששששששש",
        },
    };
    ETextHAlign[] alignments = {
        ETextHAlign.Fill,
        ETextHAlign.Center,
        ETextHAlign.Right,
    };

    foreach (bool advanced in kBackends)
    {
        foreach (DirectionCase test_case in directions)
        {
            using TextContractFixture fixture = Make(advanced);
            TextStyle style = MakeStyle(test_case.Family.ToString());

            TextLine line = fixture.Services.CreateLine();
            line.SetDirection(test_case.Direction);
            line.SetTextOverrunBehavior(
                ETextOverrunBehavior.NoTrimming
            );
            line.AddString(test_case.Text, style);
            Check.That(line.AddObject(
                kLineObject,
                new Sizef(12.0f, 16.0f),
                ETextInlineAlignment.Center,
                0u
            ));
            Check.That(line.Shape());
            float line_frame = line.LineWidth() * 0.55f;
            line.SetMaxWidth(line_frame);
            line.SetAlignment(ETextHAlign.Left);
            Check.That(line.Shape());
            float line_left = line.ObjectRect(kLineObject).Left;
            foreach (ETextHAlign alignment in alignments)
            {
                line.SetAlignment(alignment);
                Check.That(line.Shape());
                Check.That((line.LineWidth()) > (line_frame));
                float actual_offset =
                    line.ObjectRect(kLineObject).Left - line_left;
                float expected_offset = frame_alignment_offset(
                    alignment,
                    line_frame,
                    line.LineWidth(),
                    test_case.Direction,
                    false
                );
                Check.That(MathF.Abs(actual_offset - expected_offset) <= 0.001f,string.Concat(new object?[]{"backend=",advanced ? "advanced" : "fallback"," direction=",(uint)(test_case.Direction)," alignment=",(uint)(alignment)," actual=",actual_offset," expected=",expected_offset}));
            }

            TextParagraph paragraph =
                fixture.Services.CreateParagraph();
            paragraph.SetDirection(test_case.Direction);
            paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
            paragraph.SetTextOverrunBehavior(
                ETextOverrunBehavior.NoTrimming
            );
            paragraph.AddString(test_case.Text, style);
            Check.That(paragraph.AddObject(
                kParagraphObject,
                new Sizef(12.0f, 16.0f),
                ETextInlineAlignment.Center,
                1u
            ));
            Check.That(paragraph.Shape());
            Eq(paragraph.LineCount(),1u);
            float paragraph_frame =
                paragraph.LineWidth(0u) * 0.55f;
            paragraph.SetMaxWidth(paragraph_frame);
            paragraph.SetAlignment(ETextHAlign.Left);
            Check.That(paragraph.Shape());
            float paragraph_left = paragraph.LineObjectRect(
                                                      0u,
                                                      kParagraphObject
            )
                                             .Left;
            foreach (ETextHAlign alignment in alignments)
            {
                paragraph.SetAlignment(alignment);
                Check.That(paragraph.Shape());
                Eq(paragraph.LineCount(),1u);
                Check.That((paragraph.LineWidth(0u)) > (paragraph_frame));
                Near(paragraph.LineObjectRect(
                                 0u,
                                 kParagraphObject
                    )
                            .Left -
                        paragraph_left,frame_alignment_offset(
                        alignment,
                        paragraph_frame,
                        paragraph.LineWidth(0u),
                        test_case.Direction,
                        true
                    ));
            }
        }
    }
}

[GuiTest("gui/text-contract/paragraph/hidden-lines-do-not-expand-visible-size")]
public static void SourceCase16()
{
    ETextHAlign[] alignments = {
        ETextHAlign.Center,
        ETextHAlign.Right,
    };
    foreach (bool advanced in kBackends)
    {
        foreach (ETextHAlign alignment in alignments)
        {
            using TextContractFixture fixture = Make(advanced);
            TextParagraph paragraph = fixture.Services.CreateParagraph();
            paragraph.SetMaxWidth(240.0f);
            paragraph.SetMaxLinesVisible(1);
            paragraph.SetAlignment(alignment);
            paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
            paragraph.SetTextOverrunBehavior(
                ETextOverrunBehavior.NoTrimming
            );
            paragraph.AddString(
                "M\nMMMMMMMM",
                MakeStyle("Skr Test Latin")
            );

            Check.That(paragraph.Shape());
            Eq(paragraph.LineCount(),2u);
            check_actual_size(paragraph);
            Near(paragraph.Size().Width,paragraph.LineWidth(0u));
            Check.That((paragraph.LineWidth(1u)) > (paragraph.Size().Width));
        }
    }
}

[GuiTest("gui/text-contract/line/configuration-and-navigation")]
public static void SourceCase17()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.SetDirection(ETextDirectionMode.LTR);
        line.SetPreserveInvalid(true);
        line.SetPreserveControl(true);
        line.SetCustomPunctuation("!?;");
        line.SetMaxWidth(128.0f);
        line.SetAlignment(ETextHAlign.Center);
        line.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.ConstrainEllipsis
        );
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimWordEllipsis
        );
        line.SetEllipsisCodepoint((uint)'…');
        line.SetSpacing(ETextSpacing.Glyph, 1.0f);
        line.SetSpacing(ETextSpacing.Space, 2.0f);
        line.SetSpacing(ETextSpacing.Top, 3.0f);
        line.SetSpacing(ETextSpacing.Bottom, 4.0f);

        Eq(line.Direction(),ETextDirectionMode.LTR);
        Check.That(line.PreserveInvalid());
        Check.That(line.PreserveControl());
        Eq(line.CustomPunctuation(),"!?;");
        Eq(line.MaxWidth(),128.0f);
        Eq(line.Alignment(),ETextHAlign.Center);
        Eq(line.JustificationFlags(),ETextJustificationFlag.WordBound |
                ETextJustificationFlag.ConstrainEllipsis);
        Eq(line.TextOverrunBehavior(),ETextOverrunBehavior.TrimWordEllipsis);
        Eq(line.EllipsisCodepoint(),(uint)'…');
        Eq(line.Spacing(ETextSpacing.Glyph),1.0f);
        Eq(line.Spacing(ETextSpacing.Space),2.0f);
        Eq(line.Spacing(ETextSpacing.Top),3.0f);
        Eq(line.Spacing(ETextSpacing.Bottom),4.0f);

        line.AddString(
            "ABC",
            MakeStyle("Skr Test Latin")
        );
        Check.That(line.Shape());
        Eq(line.Orientation(),ETextOrientation.Horizontal);
        Eq(line.InferredDirection(),ETextDirection.LTR);
        Eq(line.EllipsisGlyphCount(),(ulong)line.EllipsisGlyphs().Length);
        Check.That(float.IsFinite(line.LineUnderlinePosition()));
        Check.That((line.LineUnderlineThickness()) > (0.0f));
        check_ascii_character_navigation(line);

        for (ulong position = 0u; position < 3u; ++position)
        {
            TextFloatRange bounds = line.GraphemeBounds(position);
            Check.That((bounds.End) > (bounds.Start));
        }
        TextFloatRange first_bounds = line.GraphemeBounds(0u);
        Eq(line.HitTestGrapheme(
                (first_bounds.Start + first_bounds.End) * 0.5f
            ),0);
        List<Rectf> selection = new();
        Check.That(line.SelectionRects(new TextRange(0u,3u ), selection));
        Check.False((selection.Count == 0));
        foreach (Rectf rect in selection)
        {
            Check.That(finite_rect(rect));
            Check.False(rect.IsEmpty());
        }

        TextLine duplicate = line.Duplicate();
        Check.That(duplicate != null);
        Eq(duplicate.Direction(),line.Direction());
        Eq(duplicate.Alignment(),line.Alignment());
        Eq(duplicate.MaxWidth(),line.MaxWidth());
        Eq(duplicate.Text(),line.Text());
        Check.That(duplicate.Shape());
    }
}

[GuiTest("gui/text-contract/line/configuration-invalidates-shaped-state")]
public static void SourceCase18()
{
    using TextContractFixture fixture = Make(true);
    TextLine line = fixture.Services.CreateLine();
    line.AddString(
        "A B C",
        MakeStyle("Skr Test Latin")
    );
    Check.That(line.Shape());

    line.SetMaxWidth(line.LineWidth() * 0.6f);
    Check.False(line.IsReady());
    line.SetAlignment(ETextHAlign.Right);
    line.SetTextOverrunBehavior(
        ETextOverrunBehavior.TrimEllipsisForce
    );
    Check.That(line.Shape());
    Ne(line.TrimPosition(),ulong.MaxValue);
    Ne(line.EllipsisPosition(),ulong.MaxValue);
    Check.False(line.EllipsisGlyphs().IsEmpty);
}

[GuiTest("gui/text-contract/paint/ellipsis-follows-raw-glyph-pen")]
public static void SourceCase19()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextStyle style = MakeStyle("Skr Test Latin");
        TextLine line = fixture.Services.CreateLine();
        line.AddString("MMMMMMMMMMMM", style);
        Check.That(line.Shape());
        float natural_width = glyph_stream_width(line.Glyphs());

        line.SetMaxWidth(natural_width * 0.55f);
        line.SetEllipsisCodepoint((uint)'.');
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        Check.That(line.Shape());
        Ne(line.TrimPosition(),ulong.MaxValue);
        Eq(line.EllipsisGlyphCount(),1u);

        float visible_pen = 0.0f;
        ulong visible_end = Math.Min(
            line.TrimPosition(),
            (ulong)line.Glyphs().Length
        );
        for (ulong i = 0u; i < visible_end; ++i)
        {
            visible_pen += line.Glyphs()[(int)(i)].Advance *
                (float)(Math.Max(1u, line.Glyphs()[(int)(i)].Repeat));
        }

        TextRenderResult trimmed = new();
        Check.That(line.Paint(Offsetf.Zero(), trimmed));
        Check.False((trimmed.Rects.Count == 0));

        TextLine period = fixture.Services.CreateLine();
        period.AddString(".", style);
        Check.That(period.Shape());
        TextRenderResult isolated = new();
        Check.That(period.Paint(Offsetf.Zero(), isolated));
        Check.False((isolated.Rects.Count == 0));
        if (!(trimmed.Rects.Count == 0) && !(isolated.Rects.Count == 0))
        {
            Near(trimmed.Rects[^1].Rect.Left -
                    isolated.Rects[(int)(0)].Rect.Left,visible_pen);
        }
    }
}

[GuiTest("gui/text-contract/paragraph/ellipsis-uses-final-logical-span")]
public static void SourceCase20()
{
    using TextContractFixture fixture = Make(true);
    FontFace? emoji_face = PreloadFamily(fixture.Services,
        "Skr Test Emoji"
    );
    Check.That(emoji_face != null);

    TextParagraph paragraph = fixture.Services.CreateParagraph();
    paragraph.SetDirection(ETextDirectionMode.RTL);
    paragraph.SetMaxWidth(96.0f);
    paragraph.SetMaxLinesVisible(1);
    paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
    paragraph.SetTextOverrunBehavior(
        ETextOverrunBehavior.TrimEllipsisForce
    );
    paragraph.SetEllipsisCodepoint(0x1f600u);
    paragraph.AddString(
        "אבגדהוזחטיכלמ\n",
        MakeStyle("Skr Test Hebrew")
    );
    paragraph.AddString(
        "A",
        MakeStyle(
            "Skr Test Latin,Skr Test Emoji"
        )
    );

    Check.That(paragraph.Shape());
    Eq(paragraph.LineCount(),2u);
    Eq(paragraph.EllipsisGlyphCount(),1u);
    Check.False(paragraph.EllipsisGlyphs().IsEmpty);
    Eq(paragraph.EllipsisGlyphs()[(int)(0u)].FontFace,emoji_face.Id());
    Eq(paragraph.EllipsisGlyphs()[(int)(0u)].Repeat,1u);

    emoji_face.SetSpacing(
        ETextSpacing.Glyph,
        emoji_face.Spacing(ETextSpacing.Glyph) + 1.0f
    );
    Check.False(paragraph.IsReady());
    Check.That(paragraph.Shape());
    Eq(paragraph.EllipsisGlyphs()[(int)(0u)].FontFace,emoji_face.Id());
}

[GuiTest("gui/text-contract/paragraph/ellipsis-tracks-late-fallback-dependencies")]
public static void SourceCase21()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        FontFace? late_fallback = PreloadFamily(fixture.Services,
            "Skr Test Collection Latin"
        );
        Check.That(late_fallback != null);
        if (late_fallback == null)
        {
            continue;
        }

        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(64.0f);
        paragraph.SetMaxLinesVisible(1);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        paragraph.SetEllipsisCodepoint(0x05d0u);
        paragraph.AddString(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ\n",
            MakeStyle("Skr Test Latin")
        );
        paragraph.AddString(
            "A",
            MakeStyle(
                "Skr Test Latin,Skr Test Collection Latin"
            )
        );

        Check.That(paragraph.Shape());
        Check.That(paragraph.IsReady());
        Eq(paragraph.EllipsisGlyphCount(),1u);
        Check.False(paragraph.EllipsisGlyphs().IsEmpty);
        if (!paragraph.EllipsisGlyphs().IsEmpty)
        {
            Ne(paragraph.EllipsisGlyphs()[(int)(0u)].FontFace,late_fallback.Id());
        }

        Check.That(late_fallback.SetFaceIndex(1u));
        Check.That(late_fallback.HasCodepoint(0x05d0u));
        Check.False(paragraph.IsReady());
    }
}

[GuiTest("gui/text-contract/paint/frame-culls-whole-glyphs")]
public static void SourceCase22()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextStyle style = MakeStyle("Skr Test Latin");

        TextLine line = fixture.Services.CreateLine();
        line.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
        line.AddString("MMMMMMMM", style);
        Check.That(line.Shape());
        TextRenderResult complete_line = new();
        Check.That(line.Paint(Offsetf.Zero(), complete_line));
        Check.False(complete_line.IsEmpty());
        float frame_width = line.LineWidth() * 0.45f;
        line.SetMaxWidth(frame_width);
        Check.That(line.Shape());
        TextRenderResult clipped_line = new();
        Check.That(line.Paint(Offsetf.Zero(), clipped_line));
        Check.False(clipped_line.IsEmpty());
        ulong expected_rects = 0u;
        float pen = 0.0f;
        bool stopped = false;
        foreach (TextGlyph glyph in line.Glyphs())
        {
            for (uint repeat = 0u;
                 repeat < Math.Max(1u, glyph.Repeat);
                 ++repeat)
            {
                if (pen + glyph.Advance > frame_width)
                {
                    stopped = true;
                    break;
                }
                if (glyph.IsVisible())
                {
                    ++expected_rects;
                }
                pen += glyph.Advance;
            }
            if (stopped)
            {
                break;
            }
        }
        Eq((ulong)clipped_line.Rects.Count,expected_rects);
        Check.That((expected_rects) < ((ulong)complete_line.Rects.Count));
        for (ulong i = 0u; i < (ulong)clipped_line.Rects.Count; ++i)
        {
            Eq(clipped_line.Rects[(int)(i)].Rect,complete_line.Rects[(int)(i)].Rect);
            Eq(clipped_line.Rects[(int)(i)].Uv,complete_line.Rects[(int)(i)].Uv);
        }

        Offsetf shifted_origin = new(37.0f, 19.0f);
        TextRenderResult shifted_line = new();
        Check.That(line.Paint(shifted_origin, shifted_line));
        Eq((ulong)shifted_line.Rects.Count,(ulong)clipped_line.Rects.Count);
        for (ulong i = 0u; i < (ulong)shifted_line.Rects.Count; ++i)
        {
            Eq(shifted_line.Rects[(int)(i)].Rect,clipped_line.Rects[(int)(i)].Rect.Shift(shifted_origin));
            Eq(shifted_line.Rects[(int)(i)].Uv,clipped_line.Rects[(int)(i)].Uv);
        }

        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(frame_width);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString("MMMMMMMM", style);
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),1u);
        TextRenderResult clipped_paragraph = new();
        TextRenderResult local_line = new();
        Check.That(paragraph.Paint(
            Offsetf.Zero(),
            clipped_paragraph
        ));
        Check.That(paragraph.PaintLine(
            0u,
            Offsetf.Zero(),
            local_line
        ));
        Check.False(clipped_paragraph.IsEmpty());
        Check.False(local_line.IsEmpty());
        Eq((ulong)clipped_paragraph.Rects.Count,expected_rects);
        Check.That((expected_rects) < ((ulong)local_line.Rects.Count));
        for (ulong i = 0u; i < (ulong)clipped_paragraph.Rects.Count; ++i)
        {
            Eq(clipped_paragraph.Rects[(int)(i)].Rect,local_line.Rects[(int)(i)].Rect);
            Eq(clipped_paragraph.Rects[(int)(i)].Uv,local_line.Rects[(int)(i)].Uv);
        }

        TextParagraph multiline =
            fixture.Services.CreateParagraph();
        multiline.SetMaxWidth(frame_width);
        multiline.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        multiline.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        multiline.AddString("MMMMMMMM\n", style);
        TextLayoutSpanId second_span =
            multiline.AddString("M", style);
        Check.That(multiline.Shape());
        Eq(multiline.LineCount(),2u);
        TextRenderResult multiline_result = new();
        Check.That(multiline.Paint(
            Offsetf.Zero(),
            multiline_result
        ));
        Rectf second_bounds = painted_span_bounds(
            multiline_result,
            second_span
        );
        Check.False(second_bounds.IsEmpty());
    }
}

[GuiTest("gui/text-contract/paint/rtl-fill-and-overflow-frame")]
public static void SourceCase23()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextStyle style = MakeStyle("Skr Test Hebrew");

        TextLine line = fixture.Services.CreateLine();
        line.SetDirection(ETextDirectionMode.RTL);
        line.SetTextOverrunBehavior(ETextOverrunBehavior.NoTrimming);
        line.AddString("אבגדהוזח", style);
        Check.That(line.Shape());
        float line_frame = line.LineWidth() + 80.0f;
        line.SetMaxWidth(line_frame);
        line.SetAlignment(ETextHAlign.Left);
        Check.That(line.Shape());
        TextRenderResult line_left = new();
        Check.That(line.Paint(Offsetf.Zero(), line_left));
        line.SetAlignment(ETextHAlign.Fill);
        Check.That(line.Shape());
        TextRenderResult line_fill = new();
        Check.That(line.Paint(Offsetf.Zero(), line_fill));
        Eq(line_fill.Bounds,line_left.Bounds);

        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.SetDirection(ETextDirectionMode.RTL);
        paragraph.SetMaxWidth(line_frame);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString("אבגדהוזח", style);
        paragraph.SetAlignment(ETextHAlign.Left);
        Check.That(paragraph.Shape());
        float paragraph_width = paragraph.LineWidth(0u);
        TextRenderResult paragraph_left = new();
        Check.That(paragraph.Paint(
            Offsetf.Zero(),
            paragraph_left
        ));
        paragraph.SetAlignment(ETextHAlign.Fill);
        Check.That(paragraph.Shape());
        TextRenderResult paragraph_fill = new();
        Check.That(paragraph.Paint(
            Offsetf.Zero(),
            paragraph_fill
        ));
        Near(paragraph_fill.Bounds.Left - paragraph_left.Bounds.Left,line_frame - paragraph_width);

        float overflow_frame = paragraph_width * 0.55f;
        paragraph.SetMaxWidth(overflow_frame);
        paragraph.SetAlignment(ETextHAlign.Center);
        Check.That(paragraph.Shape());
        Check.That((paragraph.LineWidth(0u)) > (overflow_frame));
        TextRenderResult overflow = new();
        TextRenderResult local = new();
        Check.That(paragraph.Paint(Offsetf.Zero(), overflow));
        Check.That(paragraph.PaintLine(
            0u,
            Offsetf.Zero(),
            local
        ));
        Check.False(overflow.IsEmpty());
        Check.That(((ulong)overflow.Rects.Count) < ((ulong)local.Rects.Count));
        float overflow_offset = frame_alignment_offset(
            ETextHAlign.Center,
            overflow_frame,
            paragraph.LineWidth(0u),
            ETextDirectionMode.RTL,
            true
        );
        foreach (TextRenderRect actual in overflow.Rects)
        {
            bool found = false;
            foreach (TextRenderRect source in local.Rects)
            {
                if (source.Uv != actual.Uv)
                {
                    continue;
                }
                found = true;
                Near(actual.Rect.Left - source.Rect.Left,overflow_offset);
                Eq(actual.Rect.Top,source.Rect.Top);
                Eq(actual.Rect.Size(),source.Rect.Size());
                break;
            }
            Check.That(found);
        }
    }
}

[GuiTest("gui/text-contract/paint/frame-does-not-cull-ellipsis")]
public static void SourceCase24()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            "MMMM",
            MakeStyle("Skr Test Latin")
        );
        line.SetMaxWidth(1.0f);
        line.SetEllipsisCodepoint((uint)'.');
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        Check.That(line.Shape());
        Eq(line.EllipsisGlyphCount(),1u);
        Check.That((line.EllipsisGlyphs()[(int)(0u)].Advance) > (1.0f));

        TextRenderResult result = new();
        TextPaintDesc desc = new() { ClipR = 1.0f,
        };
        Check.That(line.Paint(Offsetf.Zero(), result, desc));
        Check.False(result.IsEmpty());
    }
}

[GuiTest("gui/text-contract/editing/trim-keeps-source-geometry")]
public static void SourceCase25()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            "MMMMMMMMMMMM",
            MakeStyle("Skr Test Latin")
        );
        Check.That(line.Shape());
        line.SetMaxWidth(line.LineWidth() * 0.45f);
        line.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsisForce
        );
        Check.That(line.Shape());
        Ne(line.TrimPosition(),ulong.MaxValue);

        ReadOnlySpan<TextGlyph> glyphs = line.Glyphs();
        ulong first_hidden = Math.Min(
            line.TrimPosition(),
            (ulong)glyphs.Length
        );
        ulong hidden = ulong.MaxValue;
        for (ulong i = first_hidden; i < (ulong)glyphs.Length; ++i)
        {
            if (glyphs[(int)(i)].Count > 0u && glyphs[(int)(i)].IsVisible())
            {
                hidden = i;
                break;
            }
        }
        Ne(hidden,ulong.MaxValue);
        if (hidden == ulong.MaxValue)
        {
            continue;
        }

        TextRange hidden_source = glyphs[(int)(hidden)].SourceRange;
        List<Rectf> selection = new();
        Check.That(line.SelectionRects(hidden_source, selection));
        Check.False((selection.Count == 0));
        Check.That((line.GraphemeBounds(hidden_source.End).Length()) > (0.0f));
    }
}

[GuiTest("gui/text-contract/cache/non-fill-alignment-keeps-line-ready")]
public static void SourceCase26()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextLine line = fixture.Services.CreateLine();
        line.AddString(
            "Alignment reuses this shaped glyph stream.",
            MakeStyle("Skr Test Latin")
        );
        line.SetMaxWidth(480.0f);
        Check.That(line.Shape());

        List<TextGlyph> original = new();
        foreach (TextGlyph glyph in line.Glyphs())
        {
            original.Add(glyph);
        }
        Sizef original_size = line.Size();
        float original_width = line.LineWidth();

        foreach (ETextHAlign alignment in new ETextHAlign[] {
                 ETextHAlign.Center,
                 ETextHAlign.Right,
                 ETextHAlign.Left,
             })
        {
            line.SetAlignment(alignment);
            Check.That(line.IsReady());
            Eq(line.Size(),original_size);
            Eq(line.LineWidth(),original_width);
            ReadOnlySpan<TextGlyph> current = line.Glyphs();
            Eq((ulong)current.Length,(ulong)original.Count);
            for (ulong i = 0u; i < (ulong)current.Length; ++i)
            {
                Check.That(same_glyph_identity(
                    current[(int)(i)],
                    original[(int)(i)]
                ));
                Eq(current[(int)(i)].Advance,original[(int)(i)].Advance);
            }
        }

        line.SetAlignment(ETextHAlign.Fill);
        Check.False(line.IsReady());
        Check.That(line.Shape());
    }
}

[GuiTest("gui/text-contract/cache/non-fill-alignment-keeps-paragraph-ready")]
public static void SourceCase27()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(420.0f);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.AddString(
            "MMMMMMMM\n",
            MakeStyle("Skr Test Latin")
        );
        Check.That(paragraph.AddObject(
            91u,
            new Sizef(12.0f, 16.0f),
            ETextInlineAlignment.Center,
            1u
        ));
        paragraph.AddString(
            "M",
            MakeStyle("Skr Test Latin")
        );
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),2u);

        Sizef original_size = paragraph.Size();
        TextRange first_range = paragraph.LineRange(0u);
        TextRange second_range = paragraph.LineRange(1u);
        float first_width = paragraph.LineWidth(0u);
        float second_width = paragraph.LineWidth(1u);
        float previous_object_x = paragraph.LineObjectRect(1u, 91u).Left;

        foreach (ETextHAlign alignment in new ETextHAlign[] {
                 ETextHAlign.Center,
                 ETextHAlign.Right,
                 ETextHAlign.Left,
             })
        {
            paragraph.SetAlignment(alignment);
            Check.That(paragraph.IsReady());
            Eq(paragraph.Size(),original_size);
            Eq(paragraph.LineRange(0u),first_range);
            Eq(paragraph.LineRange(1u),second_range);
            Eq(paragraph.LineWidth(0u),first_width);
            Eq(paragraph.LineWidth(1u),second_width);

            Rectf object_rect = paragraph.LineObjectRect(1u, 91u);
            Eq(object_rect.Size(),new Sizef(12.0f, 16.0f));
            if (alignment != ETextHAlign.Left)
            {
                Ne(object_rect.Left,previous_object_x);
            }
            previous_object_x = object_rect.Left;
        }

        paragraph.SetAlignment(ETextHAlign.Fill);
        Check.False(paragraph.IsReady());
        Check.That(paragraph.Shape());
    }
}

[GuiTest("gui/text-contract/paragraph/configuration-lines-and-editing")]
public static void SourceCase28()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(
            advanced,
            true
        );
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetDirection(ETextDirectionMode.LTR);
        paragraph.SetPreserveInvalid(true);
        paragraph.SetPreserveControl(true);
        paragraph.SetCustomPunctuation("!?;");
        paragraph.SetMaxWidth(72.0f);
        paragraph.SetMaxLinesVisible(2);
        paragraph.SetLineSpacing(5.0f);
        paragraph.SetAlignment(ETextHAlign.Left);
        paragraph.SetBreakFlags(
            ETextLineBreakFlag.Mandatory |
            ETextLineBreakFlag.WordBound |
            ETextLineBreakFlag.TrimStartEdgeSpaces |
            ETextLineBreakFlag.TrimEndEdgeSpaces
        );
        paragraph.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.SkipLastLine
        );
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimWordEllipsis
        );
        paragraph.SetEllipsisCodepoint((uint)'…');
        paragraph.SetSpacing(ETextSpacing.Glyph, 1.0f);
        paragraph.SetSpacing(ETextSpacing.Space, 2.0f);
        paragraph.SetSpacing(ETextSpacing.Top, 3.0f);
        paragraph.SetSpacing(ETextSpacing.Bottom, 4.0f);

        paragraph.AddString(
            "Alpha beta gamma delta epsilon",
            MakeStyle("Skr Test Latin"),
            "en"
        );
        Check.That(paragraph.Shape());
        Eq(paragraph.Orientation(),ETextOrientation.Horizontal);
        Eq(paragraph.InferredDirection(),ETextDirection.LTR);
        Check.That(paragraph.PreserveInvalid());
        Check.That(paragraph.PreserveControl());
        Eq(paragraph.CustomPunctuation(),"!?;");
        Eq(paragraph.MaxWidth(),72.0f);
        Eq(paragraph.MaxLinesVisible(),2);
        Eq(paragraph.LineSpacing(),5.0f);
        Eq(paragraph.Alignment(),ETextHAlign.Left);
        Eq(paragraph.BreakFlags(),ETextLineBreakFlag.Mandatory |
                ETextLineBreakFlag.WordBound |
                ETextLineBreakFlag.TrimStartEdgeSpaces |
                ETextLineBreakFlag.TrimEndEdgeSpaces);
        Eq(paragraph.TextOverrunBehavior(),ETextOverrunBehavior.TrimWordEllipsis);
        Eq(paragraph.EllipsisCodepoint(),(uint)'…');
        Eq(paragraph.Spacing(ETextSpacing.Glyph),1.0f);
        Eq(paragraph.Spacing(ETextSpacing.Space),2.0f);
        Eq(paragraph.Spacing(ETextSpacing.Top),3.0f);
        Eq(paragraph.Spacing(ETextSpacing.Bottom),4.0f);


        Check.That((paragraph.LineCount()) > (2u));
        Check.That((paragraph.NonWrappedSize().Width) >= (paragraph.Size().Width));
        Eq(paragraph.EllipsisGlyphCount(),(ulong)paragraph.EllipsisGlyphs().Length);

        TextRange previous_range = new();
        for (ulong line = 0u; line < paragraph.LineCount(); ++line)
        {
            TextRange source = paragraph.LineRange(line);
            TextRange glyphs = paragraph.LineGlyphRange(line);
            Check.That((previous_range.End) <= (source.Start));
            Check.That((source.End) <= (paragraph.SourceRange().End));
            Check.That((glyphs.End) <= (paragraph.GlyphCount()));
            Check.That(finite_size(paragraph.LineSize(line)));
            Check.That(float.IsFinite(paragraph.LineWidth(line)));
            Check.That((paragraph.LineAscent(line)) > (0.0f));
            Check.That((paragraph.LineDescent(line)) >= (0.0f));
            Check.That(float.IsFinite(paragraph.LineUnderlinePosition(line)));
            Check.That((paragraph.LineUnderlineThickness(line)) > (0.0f));

            TextRenderResult line_result = new();
            Check.That(paragraph.PaintLine(
                line,
                Offsetf.Zero(),
                line_result
            ));
            Check.False(line_result.IsEmpty());
            previous_range = source;
        }

        List<ulong> breaks = new();
        Check.That(paragraph.CharacterBreaks(breaks));
        Check.False((breaks.Count == 0));
        Eq(breaks[^1],paragraph.SourceRange().End);
        Eq(paragraph.PreviousCharacterPosition(2u),1);
        Eq(paragraph.NextCharacterPosition(1u),2);
        Eq(paragraph.ClosestCharacterPosition(2u),2);
        Eq(paragraph.RangeDirection(new TextRange(0u,5u )),ETextDirection.LTR);

        List<Rectf> selection = new();
        Check.That(paragraph.SelectionRects(
            new TextRange(0u,paragraph.SourceRange().End ),
            selection
        ));
        Check.False((selection.Count == 0));

        TextParagraph duplicate = paragraph.Duplicate();
        Check.That(duplicate != null);
        Eq(duplicate.MaxWidth(),paragraph.MaxWidth());
        Eq(duplicate.MaxLinesVisible(),paragraph.MaxLinesVisible());
        Eq(duplicate.Text(),paragraph.Text());
        Check.That(duplicate.Shape());
    }
}

[GuiTest("gui/text-contract/paragraph/max-lines-limits-visibility-only")]
public static void SourceCase29()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(
            advanced,
            true
        );
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(64.0f);
        paragraph.SetBreakFlags(
            ETextLineBreakFlag.Mandatory |
            ETextLineBreakFlag.WordBound
        );
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.TrimEllipsis
        );
        paragraph.AddString(
            "Alpha beta gamma delta epsilon",
            MakeStyle("Skr Test Latin")
        );

        Check.That(paragraph.Shape());
        ulong complete_line_count = paragraph.LineCount();
        Check.That((complete_line_count) > (1u));

        paragraph.SetMaxLinesVisible(1);
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),complete_line_count);
        Eq(paragraph.Size().Height,paragraph.LineSize(0u).Height);
        Ne(paragraph.EllipsisPosition(),ulong.MaxValue);
        Check.False(paragraph.EllipsisGlyphs().IsEmpty);
        Eq(paragraph.EllipsisGlyphCount(),(ulong)paragraph.EllipsisGlyphs().Length);
        TextRenderResult visible = new();
        Check.That(paragraph.Paint(Offsetf.Zero(), visible));
        Check.False(visible.IsEmpty());
        TextRenderResult hidden_line = new();
        Check.That(paragraph.PaintLine(
            complete_line_count - 1u,
            Offsetf.Zero(),
            hidden_line
        ));
        Check.False(hidden_line.IsEmpty());

        paragraph.SetMaxLinesVisible(0);
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),complete_line_count);
        Eq(paragraph.Size(),Sizef.Zero());
        Eq(paragraph.TrimPosition(),ulong.MaxValue);
        Eq(paragraph.EllipsisPosition(),ulong.MaxValue);
        Check.That(paragraph.EllipsisGlyphs().IsEmpty);
        Eq(paragraph.EllipsisGlyphCount(),0u);
        TextRenderResult none_visible = new();
        Check.That(paragraph.Paint(
            Offsetf.Zero(),
            none_visible
        ));
        Check.That(none_visible.IsEmpty());
        TextRenderResult first_line = new();
        Check.That(paragraph.PaintLine(
            0u,
            Offsetf.Zero(),
            first_line
        ));
        Check.False(first_line.IsEmpty());
    }
}

[GuiTest("gui/text-contract/layout/public-size-rounds-line-height")]
public static void SourceCase30()
{
    float kFontSize = 15.3f;
    float kLineSpacing = 2.0f;
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextStyle style = MakeStyle(
            "Skr Test Latin",
            kFontSize
        );

        TextLine line = fixture.Services.CreateLine();
        line.AddString("A", style);
        Check.That(line.Shape());
        float raw_line_height =
            line.LineAscent() + line.LineDescent();
        Near(line.Size().Height,MathF.Ceiling(raw_line_height));

        TextParagraph paragraph =
            fixture.Services.CreateParagraph();
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetLineSpacing(kLineSpacing);
        paragraph.AddString("A\nB", style);
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),2u);
        for (ulong i = 0u; i < paragraph.LineCount(); ++i)
        {
            float raw_height = paragraph.LineAscent(i) +
                paragraph.LineDescent(i);
            Near(paragraph.LineSize(i).Height,MathF.Ceiling(raw_height));
        }
        Near(paragraph.Size().Height,
                paragraph.LineSize(0u).Height + kLineSpacing +
                paragraph.LineSize(1u).Height
            );

        float first_raw_height = paragraph.LineAscent(0u) +
            paragraph.LineDescent(0u);
        float first_public_height = paragraph.LineSize(0u).Height;
        float rounding_fringe =
            first_public_height - first_raw_height;
        Check.That((rounding_fringe) > (0.001f));
        TextFloatRange first_bounds =
            paragraph.GraphemeBounds(0u);
        Offsetf fringe_position = new(
            first_bounds.Start + first_bounds.Length() * 0.25f,
            first_raw_height + rounding_fringe * 0.5f
        );
        Eq(paragraph.HitTestPosition(fringe_position),0);
    }
}

[GuiTest("gui/text-contract/paragraph/hit-test-uses-real-line-boxes")]
public static void SourceCase31()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(1000.0f);
        paragraph.SetLineSpacing(10.0f);
        paragraph.SetBreakFlags(ETextLineBreakFlag.Mandatory);
        paragraph.SetTextOverrunBehavior(
            ETextOverrunBehavior.NoTrimming
        );
        paragraph.AddString(
            "A\nB",
            MakeStyle("Skr Test Latin")
        );
        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),2u);

        ReadOnlySpan<TextGlyph> glyphs = paragraph.Glyphs();
        TextRange first_glyphs = paragraph.LineGlyphRange(0u);
        TextRange second_glyphs = paragraph.LineGlyphRange(1u);
        ulong first = ulong.MaxValue;
        ulong second = ulong.MaxValue;
        for (ulong i = first_glyphs.Start; i < first_glyphs.End; ++i)
        {
            if (glyphs[(int)(i)].Count > 0u && glyphs[(int)(i)].IsVisible())
            {
                first = i;
                break;
            }
        }
        for (ulong i = second_glyphs.Start; i < second_glyphs.End; ++i)
        {
            if (glyphs[(int)(i)].Count > 0u && glyphs[(int)(i)].IsVisible())
            {
                second = i;
                break;
            }
        }
        Ne(first,ulong.MaxValue);
        Ne(second,ulong.MaxValue);

        TextFloatRange first_bounds = paragraph.GraphemeBounds(
            glyphs[(int)(first)].SourceRange.Start
        );
        TextFloatRange second_bounds = paragraph.GraphemeBounds(
            glyphs[(int)(second)].SourceRange.Start
        );
        TextFloatRange terminal_bounds = paragraph.GraphemeBounds(
            paragraph.SourceRange().End
        );
        Eq(terminal_bounds,second_bounds);
        float first_x = first_bounds.Start +
            first_bounds.Length() * 0.25f;
        float second_x = second_bounds.Start +
            second_bounds.Length() * 0.25f;
        ulong second_source_start =
            glyphs[(int)(second)].SourceRange.Start;
        float first_hit_bottom = paragraph.LineSize(0u).Height;
        float second_hit_top =
            first_hit_bottom + paragraph.LineSpacing();
        float gap_y =
            first_hit_bottom + paragraph.LineSpacing() * 0.5f;
        float second_y =
            second_hit_top + paragraph.LineSize(1u).Height * 0.5f;
        float below_y =
            second_hit_top + paragraph.LineSize(1u).Height + 1.0f;
        long source_end = (long)(
            paragraph.SourceRange().End
        );

        Eq(paragraph.HitTestGrapheme(new Offsetf(first_x, first_hit_bottom)),(long)(first));
        Eq(paragraph.HitTestPosition(new Offsetf(first_x, gap_y)),source_end);
        Eq(paragraph.HitTestGrapheme(new Offsetf(first_x, gap_y)),-1);

        foreach (int visible_lines in new int[] { 1, 0 })
        {
            paragraph.SetMaxLinesVisible(visible_lines);
            Check.That(paragraph.Shape());
            Eq(paragraph.HitTestPosition(new Offsetf(second_x, second_y)),(long)(second_source_start));
            Eq(paragraph.HitTestGrapheme(new Offsetf(second_x, second_y)),(long)(second));
        }

        Eq(paragraph.HitTestPosition(new Offsetf(second_x, below_y)),source_end);
        Eq(paragraph.HitTestGrapheme(new Offsetf(second_x, below_y)),-1);
        Eq(paragraph.HitTestPosition(new Offsetf(first_x, -1.0f)),source_end);
        Eq(paragraph.HitTestGrapheme(new Offsetf(first_x, -1.0f)),-1);
    }
}

[GuiTest("gui/text-contract/paragraph/hidden-single-line-fill")]
public static void SourceCase32()
{
    foreach (bool advanced in kBackends)
    {
        using TextContractFixture fixture = Make(advanced);
        TextParagraph paragraph = fixture.Services.CreateParagraph();
        paragraph.SetMaxWidth(128.0f);
        paragraph.SetMaxLinesVisible(0);
        paragraph.SetAlignment(ETextHAlign.Fill);
        paragraph.SetBreakFlags(
            ETextLineBreakFlag.Mandatory |
            ETextLineBreakFlag.WordBound
        );
        paragraph.SetJustificationFlags(
            ETextJustificationFlag.WordBound |
            ETextJustificationFlag.DoNotSkipSingleLine
        );
        paragraph.AddString(
            "Alpha beta",
            MakeStyle("Skr Test Latin")
        );

        Check.That(paragraph.Shape());
        Eq(paragraph.LineCount(),1u);
        Eq(paragraph.LineWidth(0u),128.0f);
        Eq(paragraph.Size(),Sizef.Zero());
    }
}
}
