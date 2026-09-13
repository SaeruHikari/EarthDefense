from pathlib import Path
import re
p=Path('tests/SkrGui.Core.Tests/Text/TextShapingContractTests.cs');s=p.read_text(encoding='utf-8')
a=s.index('private sealed class BreakCase');b=s.index('private static bool same_glyph_identity');s=s[:a]+'''private sealed record ControlCase(Utf8StringView Text,uint Codepoint,bool IsSpace,bool IsHardBreak,bool IsTab,bool IsZeroWidth);
private sealed record BreakCase(Utf8StringView Text,uint Codepoint,bool FallbackHexBox);
private sealed record ScriptCase(Utf8StringView Text,string Family,Utf8StringView Language,bool RTL);
private sealed record FixedBreakCase(Utf8StringView Text,TextRange[] Ranges,ulong RangeCount);
private delegate void ConfigMutation(ref TextFontRasterConfig config);
private sealed class GlyphRef(TextGlyph value) { public TextRange SourceRange=>value.SourceRange;public TextFontFaceId FontFace=>value.FontFace; public uint GlyphIndex=>value.GlyphIndex;public ETextGlyphKind Kind=>value.Kind;public ETextGraphemeFlag Flags=>value.Flags; public float Advance=>value.Advance;public Offsetf Offset=>value.Offset;public bool IsVisible()=>value.IsVisible(); }
'''+s[b:]
s=s.replace('TextGlyph?','GlyphRef?').replace('return glyph;','return new GlyphRef(glyph);')
# Last local BreakCase has a different original shape.
a=s.index('public static void SourceCase20');s=s[:a]+s[a:].replace('BreakCase','FixedBreakCase')
s=re.sub(r'std\.Array<(\w+),\s*([^>]+)>\s*(\w+)\s*\{\}',r'\1[] \3 = new \1[(int)(\2)]',s)
s=re.sub(r'std\.Array<(\w+),\s*\d+>\{',r'new \1[]{',s)
s=re.sub(r'std\.Array<(\w+),\s*\d+>',r'\1[]',s)
s=re.sub(r'\b(\w+) (\w+)\[\]\s*\{',r'\1[] \2 = {',s)
# Token protection preserves commas/braces inside source strings.
strings=[]
def mask(m):strings.append(m[0]);return 'LIT'+str(len(strings)-1)+'LIT'
s=re.sub(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',mask,s)
def balanced(t,pos):
 depth=1;i=pos+1
 while depth:
  if t[i]=='{':depth+=1
  if t[i]=='}':depth-=1
  i+=1
 return i-1
for name in ['ControlCase','BreakCase','FixedBreakCase']:
 while True:
  m=re.search(r'\b'+name+r'\s*\{',s)
  if not m:break
  end=balanced(s,m.end()-1);body=s[m.end():end]
  if name=='FixedBreakCase':body=body.replace('{','new TextRange[]{',1)
  s=s[:m.start()]+'new '+name+'('+body+')'+s[end+1:]
# ScriptCase source aggregate has no named type.
a=s.index('public static void SourceCase18');b=s.index('public static void SourceCase19');segment=s[a:b];segment=re.sub(r'\{\s*(LIT\d+LIT),\s*(LIT\d+LIT),\s*(LIT\d+LIT),\s*(true|false)\s*\}',r'new ScriptCase(\1,\2,\3,\4)',segment);s=s[:a]+segment+s[b:]
s=s.replace('Offsetf{}','new Offsetf()').replace('auto run = [](auto mutate) {','Action<ConfigMutation> run = (ConfigMutation mutate) => {').replace('mutate(next);','mutate(ref next);')
s=s.replace('[](TextFontRasterConfig config) {','(ref TextFontRasterConfig config) => {')
s=re.sub(r'SKR_TEST_SUBCASE\(LIT\d+LIT\)','',s)
s=s.replace('TextFontRasterConfig.KLowPpem','TextFontRasterConfig.LowPpem').replace('TextFontRasterConfig.KMediumPpem','TextFontRasterConfig.MediumPpem')
s=s.replace('ReadOnlySpan<float>(tab_stop, 1u)','new float[]{tab_stop}')
s=s.replace('skr.FlagAll(','FlagAll(')
s=s.replace('private static bool FlagAny','private static bool FlagAll(ETextGraphemeFlag a,ETextGraphemeFlag b)=>(a&b)==b;\nprivate static bool FlagAny')
for name in ['glyph','aligned_tab','cr','lf','hard_break','zero_width','missing','face']:
 s=re.sub(r'if \(!'+name+r'\)',r'if ('+name+' is null)',s)
 s=re.sub(r'if \('+name+r'\)',r'if ('+name+' is not null)',s)
s=s.replace('if (cr && lf)','if (cr is not null && lf is not null)').replace('if (hard_break && zero_width && missing)','if (hard_break is not null && zero_width is not null && missing is not null)')
s=s.replace('used.ResizeZeroed((ulong)logical.Length);','for(int i=0;i<logical.Length;i++)used.Add(false);')
s=re.sub(r'^\s*glyphs.Reserve\([^;]+;','',s,flags=re.M)
s=s.replace('ReadOnlySpan<TextRange>(value.Ranges.Data(), value.RangeCount)','value.Ranges.AsSpan(0,(int)value.RangeCount)')
for i,v in enumerate(strings):s=s.replace('LIT'+str(i)+'LIT',v)
p.write_text(s,encoding='utf-8')
