using SkrGui;
namespace SkrGui.Tests;

public static class TextPrimitiveTests
{
    [GuiTest("text/text_types_tests.cpp; text_raster_contract_tests.cpp:2339")]
    public static void TypesAndRenderCoalescing()
    {
        Check.False(new TextFontFaceId().IsValid()); Check.False(new TextLayoutSpanId().IsValid()); Check.That(new TextLayoutSpanId(0).IsValid());
        Check.Equal(0ul, new TextRange(9, 3).Length()); Check.False(new TextRange(1, 3).Contains(3));
        TextRenderResult result = new();
        result.BuildAppendRect(2, ETextPixelMode.Sdf, Rectf.Largest(), new(0), Rectf.LTWH(1, 2, 3, 4), Rectf.LTWH(0, 0, .1f, .1f));
        result.BuildAppendRect(2, ETextPixelMode.Sdf, Rectf.Largest(), new(1), Rectf.LTWH(4, 2, 3, 4), Rectf.LTWH(0, 0, .1f, .1f));
        Check.Equal(1, result.Commands.Count); Check.Equal(2ul, result.Commands[0].RectCount); Check.Equal(Rectf.LTWH(1, 2, 6, 4), result.Bounds);
        result.BuildAppendBox(new(2), Rectf.LTWH(2, 1, 1, 1)); Check.Equal(2, result.Commands.Count); Check.That(result.Commands[1].IsHexBoxFallback());
        result.Clear(); Check.That(result.IsEmpty());
    }
    [GuiTest("src/text/layout/text_layout_builder.cpp:439")]
    public static void SourceScalarAndMalformedBytes()
    {
        Utf8StringView text = "A\U0001f600Z"; ulong pos = 0; List<uint> scalars = [];
        while (pos < text.Size()) { var decoded = TextAlgorithms.DecodeNext(text, pos); scalars.Add(decoded.Codepoint); pos += decoded.Bytes; }
        Check.SequenceEqual(new uint[] { 65, 0x1f600, 90 }, scalars); Check.Equal(6ul, pos);
        var malformed = new Utf8StringView(new byte[] { 0xe0, 0x80, 0x80, 0x41 });
        var first = TextAlgorithms.DecodeNext(malformed, 0); Check.Equal(0xfffdu, first.Codepoint); Check.Equal(1ul, first.Bytes);
        Check.Equal(0xfffdu, TextAlgorithms.DecodeNext(malformed, 1).Codepoint); Check.Equal(65u, TextAlgorithms.DecodeNext(malformed, 3).Codepoint);
        Check.Equal(0ul, TextAlgorithms.DecodeNext(malformed, 4).Bytes);
    }
    [GuiTest("text/text_layout_contract_tests.cpp:640; src/text/layout/text_layout_builder.cpp:133")]
    public static void LigatureHitsAndSelection()
    {
        using TextShapedData line = new() { Width = 30, LineHeight = 12, SourceRange = new(0, 3) };
        line.Glyphs.Add(new() { SourceBegin = 0, SourceEnd = 3, Advance = 30, Glyph = new() { GlyphIndex = 1 } });
        Check.Equal(0L, TextAlgorithms.LineHitTestPosition(line, 4)); Check.Equal(1L, TextAlgorithms.LineHitTestPosition(line, 6));
        Check.Equal(2L, TextAlgorithms.LineHitTestPosition(line, 24)); Check.Equal(3L, TextAlgorithms.LineHitTestPosition(line, 26));
        Check.Equal(Rectf.LTWH(10, 0, 1, 12), TextAlgorithms.LineCaret(line, 1).LeadingCaret);
        List<Rectf> rects = []; TextAlgorithms.LineSelectionRects(line, new(1, 2), rects); Check.Equal(Rectf.LTWH(10, 0, 10, 12), rects[0]);
        line.Glyphs[0].Flags = ETextGraphemeFlag.Rtl; Check.Equal(3L, TextAlgorithms.LineHitTestPosition(line, 4)); Check.Equal(2L, TextAlgorithms.LineHitTestPosition(line, 6));
        Check.Equal(0f, TextAlgorithms.LineAlignmentOffset(line, 20, ETextHAlign.Center, false));
        line.InferredDirection = ETextDirection.RTL; Check.Equal(-10f, TextAlgorithms.LineAlignmentOffset(line, 20, ETextHAlign.Center, false));
    }
    [GuiTest("text/text_raster_contract_tests.cpp:2243; src/text/render/text_atlas_manager.cpp")]
    public static unsafe void AtlasAllocationAndPixelContracts()
    {
        foreach (var algorithm in new[] { ETextAtlasAllocationAlgorithm.ShelfLinear, ETextAtlasAllocationAlgorithm.ShelfBestFit, ETextAtlasAllocationAlgorithm.GuillotineSplit })
        {
            TextAtlasManager atlas = new(); atlas.SetAtlasPageSize(ETextAtlasFormat.RGBA8, new(1, 127));
            Check.Equal(new Sizei(128, 128), atlas.AtlasPageSize(ETextAtlasFormat.RGBA8)); atlas.SetAtlasAllocationAlgorithm(algorithm);
            atlas.BeginUpdate(); Check.That(atlas.Allocate(ETextAtlasFormat.RGBA8, 16, 16, out var a)); Check.Equal(2u, a.X); Check.Equal(2u, a.Y);
            Check.That(atlas.Allocate(ETextAtlasFormat.RGBA8, 16, 16, out var b));
            Check.Equal(algorithm == ETextAtlasAllocationAlgorithm.GuillotineSplit ? 2u : 22u, b.X);
            Check.Equal(algorithm == ETextAtlasAllocationAlgorithm.GuillotineSplit ? 22u : 2u, b.Y);
            Check.That(atlas.Allocate(ETextAtlasFormat.RGBA8, 1, 1, out var color));
            byte* bgra = stackalloc byte[] { 10, 20, 30, 40 }; Check.That(atlas.WriteBitmap(color, bgra, 4, ETextAtlasBitmapFormat.BGRA));
            var before = atlas.Atlas(0); Check.Equal(1ul, before.Generation); atlas.EndUpdate(); var after = atlas.Atlas(0); Check.Equal(2ul, after.Generation);
            int offset = (int)((color.Y * after.Width + color.X) * 4);
            Check.SequenceEqual(new byte[] { 30, 20, 10, 40 }, after.Pixels.Span.Slice(offset, 4).ToArray());
            int padding = (int)((color.Y * after.Width + color.X - 1) * 4); Check.SequenceEqual(new byte[] { 0, 0, 0, 0 }, after.Pixels.Span.Slice(padding, 4).ToArray());
            atlas.BeginUpdate(); atlas.EndUpdate(); Check.Equal(2ul, atlas.Atlas(0).Generation); atlas.Clear(); Check.Equal(uint.MaxValue, atlas.Atlas(0).AtlasIndex);
        }
    }
}
