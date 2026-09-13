from pathlib import Path
import re
p=Path('tests/SkrGui.Core.Tests/Text/TextRasterContractTests.cs');s=p.read_text(encoding='utf-8')
s='using static SkrGui.Tests.TextSourceTestHelpers;\n'+s
s=s.replace('std.NumericLimits<uint>.Max()','uint.MaxValue').replace('float3x3','Float3x3')
s=re.sub(r'\b(Rectf) (\w+)\(',r'\1 \2 = new(',s)
s=re.sub(r'(?<![\w.])(Sizei|TextLayoutSpanId|SRGBColor)\(',r'new \1(',s)
s=s.replace('TextPaintDesc{','new TextPaintDesc{')
s=re.sub(r'\b(SRGBColor|RasterRoute|BitmapRasterRoute|StrikeCase) (\w+)\[\]\s*\{',r'\1[] \2 = {',s)
s=re.sub(r'\{\s*(true|false),\s*(true|false),\s*(true|false)\s*\}',r'new RasterRoute(\1,\2,\3)',s)
s=re.sub(r'\{\s*(true|false),\s*(true|false)\s*\}',r'new BitmapRasterRoute(\1,\2)',s)
s=re.sub(r'\{\s*(\d+\.\d+f),\s*(\d+\.\d+f),\s*(\d+\.\d+f),\s*(\d+\.\d+f)\s*\}',r'new StrikeCase(\1,\2,\3,\4)',s)
s=re.sub(r'std\.Array<(\w+),\s*(\d+)u>\s+(\w+) = new\(\)',r'\1[] \3 = new \1[\2]',s)
s=re.sub(r'std\.Array<byte, 4u>new TextRange\(([^()]+)\)',lambda m:'new byte[]{'+re.sub(r'(\d+)u',r'\1',m[1])+'}',s)
s=re.sub(r'\breturn std.Pair\{([^{}]+)\};',r'return (\1);',s)
s=s.replace('ReadOnlySpan<SRGBColor>{}','Array.Empty<SRGBColor>()')
s=s.replace('{}','null').replace('if (!face)','if (face is null)')
s=re.sub(r'([\w.]+)\.Commands.Size\(\)',r'(ulong)\1.Commands.Count',s)
s=s.replace('atlas.Pixels.IsEmpty()','atlas.Pixels.IsEmpty').replace('width_deltas.Size()','(ulong)width_deltas.Length')
s=s.replace('var raster_signatures = (','RasterSignaturesDelegate raster_signatures = (')
# Original output signature is a uint64_t reference: retain caller initialization and failure behavior.
def balanced(t,start):
 d=1;i=start+1
 while d:
  if t[i]=='(':d+=1
  if t[i]==')':d-=1
  i+=1
 return i-1
pos=0
while True:
 m=re.search(r'atlas_rect_signature\(',s[pos:])
 if not m:break
 begin=pos+m.start();end=balanced(s,begin+len('atlas_rect_signature'))
 part=s[begin:end];comma=part.rfind(',');part=part[:comma+1]+' ref '+part[comma+1:].strip();s=s[:begin]+part+s[end:];pos=begin+len(part)+1
p.write_text(s,encoding='utf-8')
p=Path('tests/SkrGui.Core.Tests/Text/TextRasterTestHelpers.cs');s=p.read_text(encoding='utf-8-sig').replace('    private sealed class HorizontalCoverage','''    private delegate List<ulong> RasterSignaturesDelegate(bool lcd,ETextSubpixelPositioning positioning,ReadOnlySpan<float> positions);
    private sealed record RasterRoute(bool Advanced,bool Lcd,bool TransformsColorLayers);
    private sealed record BitmapRasterRoute(bool Advanced,bool Lcd);
    private sealed record StrikeCase(float FontSize,float NativeWidth,float NativeHeight,float SelectedWidth);
    private static void Eq(byte[] a,byte[] b)=>Check.SequenceEqual(a,b);
    private sealed class HorizontalCoverage''');s=s.replace('private static TextRenderResult paint_glyph(TextLine line,Offsetf origin,float ratio=1,Offsetf? offset=default)','''private static TextRenderResult paint_glyph(TextLine line,Offsetf origin,float ratio=1)=>paint_glyph(line,origin,ratio,Offsetf.Zero());
    private static TextRenderResult paint_glyph(TextLine line,Offsetf origin,float ratio,Offsetf? offset)''');p.write_text(s,encoding='utf-8')
