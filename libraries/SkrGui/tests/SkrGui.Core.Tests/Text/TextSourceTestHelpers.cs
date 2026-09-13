namespace SkrGui.Tests;
// Source: shared predicates from tests/text/text_baseline_fixture.hpp / text_contract_fixture.hpp.
internal static class TextSourceTestHelpers
{
    internal static bool finite_offset(Offsetf v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y);
    internal static bool glyphs_have_flag(ReadOnlySpan<TextGlyph> glyphs,ETextGraphemeFlag flag){foreach(var glyph in glyphs)if((glyph.Flags&flag)!=0)return true;return false;}
    internal static bool glyphs_have_kind(ReadOnlySpan<TextGlyph> glyphs,ETextGlyphKind kind){foreach(var glyph in glyphs)if(glyph.Kind==kind)return true;return false;}
    internal static bool glyph_buffer_is_well_formed(ReadOnlySpan<TextGlyph> glyphs)
    {for(int i=0;i<glyphs.Length;){var head=glyphs[i];if(head.Count==0||i+head.Count>glyphs.Length)return false;for(int j=1;j<head.Count;j++){var glyph=glyphs[i+j];if(glyph.Count!=0||glyph.SourceRange!=head.SourceRange)return false;}i+=(int)head.Count;}return true;}
    internal static void copy_glyphs(ReadOnlySpan<TextGlyph> source,List<TextGlyph> output){output.Clear();foreach(var glyph in source)output.Add(glyph);}
    internal static ulong source_glyph_count(ReadOnlySpan<TextGlyph> glyphs){ulong count=0;foreach(var glyph in glyphs)if(glyph.Count>0&&(glyph.Flags&ETextGraphemeFlag.Virtual)==0)count++;return count;}
    internal static bool atlas_has_coverage(TextAtlasView atlas)
    {var pixels=atlas.Pixels.Span;if(atlas.Format==ETextAtlasFormat.RGBA8){for(int i=3;i<pixels.Length;i+=4)if(pixels[i]!=0)return true;return false;}foreach(byte value in pixels)if(value!=0)return true;return false;}
}
