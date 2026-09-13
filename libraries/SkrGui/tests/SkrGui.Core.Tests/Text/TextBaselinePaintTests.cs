using static SkrGui.Tests.TextBaselineFixture;
using static SkrGui.Tests.TextSourceTestHelpers;
namespace SkrGui.Tests;
// Source: tests/text/text_baseline_paint_tests.cpp @ 611561f8.
internal static class TextBaselinePaintTests
{
    [GuiTest("gui/text-baseline/paint/sdf-atlas-and-cache-reset")]
    private static void SdfAtlasReset()
    {
        using var f=MakeTextFixture(false);var paragraph=f.Services.CreateParagraph();paragraph.AddString("SDF text",MakeLatinStyle(20),"en");Check.That(paragraph.Shape());TextRenderResult first=new();Check.That(paragraph.Paint(new(4,6),first));Check.Equal(1,first.Commands.Count);Check.That(first.Rects.Count!=0);Check.Equal(ETextPixelMode.Sdf,first.Commands[0].PixelMode);Check.That(first.Commands[0].AtlasIndex!=uint.MaxValue);Check.Equal((ulong)first.Rects.Count,first.Commands[0].RectCount);var atlas=f.Services.Atlas(first.Commands[0].AtlasIndex);Check.Equal(ETextAtlasFormat.L8,atlas.Format);Check.That(atlas_has_coverage(atlas));f.Services.ClearAtlases();Check.Equal(0u,f.Services.AtlasCount());TextRenderResult second=new();Check.That(paragraph.Paint(Offsetf.Zero(),second));Check.False(second.IsEmpty());Check.That(f.Services.AtlasCount()>0);
    }
    [GuiTest("gui/text-baseline/paint/empty-glyph-is-a-noop")]
    private static void EmptyGlyph()
    {using var f=MakeTextFixture(false);var line=f.Services.CreateLine();line.AddString(" \t",MakeLatinStyle());line.TabAlign(new float[]{24});Check.That(line.Shape());TextRenderResult result=new();Check.That(line.Paint(Offsetf.Zero(),result));Check.That(result.IsEmpty());}
    [GuiTest("gui/text-baseline/paint/missing-glyph-solid-box")]
    private static void MissingGlyph()
    {using var f=MakeTextFixture(false);var line=f.Services.CreateLine();line.AddString("\U0001F600",MakeLatinStyle(20));Check.That(line.Shape());Check.Equal(1UL,line.GlyphCount());Check.Equal(ETextGlyphKind.HexBox,line.Glyphs()[0].Kind);TextRenderResult result=new();Check.That(line.Paint(Offsetf.Zero(),result));Check.Equal(1,result.Commands.Count);Check.Equal(1,result.Rects.Count);Check.That(result.Commands[0].IsHexBoxFallback());Check.Equal(uint.MaxValue,result.Commands[0].AtlasIndex);Check.Near(16,result.Rects[0].Rect.Width(),.0001);Check.Near(15,result.Rects[0].Rect.Height(),.0001);}
    [GuiTest("gui/text-baseline/paint/paragraph-line-local-origin")]
    private static void LineOrigin()
    {foreach(bool advanced in new[]{false,true}){using var f=MakeTextFixture(advanced);var paragraph=f.Services.CreateParagraph();paragraph.AddString("A\nB",MakeLatinStyle(20),"en");Check.That(paragraph.Shape());Check.Equal(2UL,paragraph.LineCount());TextRenderResult result=new();Check.That(paragraph.PaintLine(1,Offsetf.Zero(),result));Check.False(result.IsEmpty());Check.That(result.Bounds.Top>=0);Check.That(result.Bounds.Bottom<=paragraph.LineSize(1).Height+.0001);}}
}
