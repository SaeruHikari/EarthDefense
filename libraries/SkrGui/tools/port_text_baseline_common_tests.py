import pathlib,re,json
root=pathlib.Path(__file__).resolve().parents[1]
p=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/text/text_baseline_common_tests.cpp')
source=p.read_text(encoding='utf-8')
def balanced(s,pos,op='(',cl=')'):
 depth=0
 for end in range(pos,len(s)):
  if s[end]==op:depth+=1
  elif s[end]==cl:
   depth-=1
   if depth==0:return end
 raise ValueError((pos,s[pos:pos+90]))
def args_split(s):
 depth=0;args=[];start=0
 for i,c in enumerate(s):
  if c in '({[':depth+=1
  elif c in ')}]':depth-=1
  elif c==',' and depth==0:args.append(s[start:i].strip());start=i+1
 args.append(s[start:].strip());return args
def pascal(n):
 return {'uv':'Uv','rtl':'RTL','ltr':'LTR'}.get(n,''.join(x[:1].upper()+x[1:] for x in n.split('_')))
strings=[]
def mask(m):strings.append(m[0]);return f'STRTOKEN{len(strings)-1}TOKEN'
clean=re.sub(r'(?:u8)?"(?:\\.|[^"\\])*"|(?:U)?\'(?:\\.|[^\'\\])*\'',mask,source)
clean=re.sub(r'//[^\n]*','',clean)
structs=[]
for m in list(re.finditer(r'    struct (\w+)\s*\{',clean))[::-1]:
 end=balanced(clean,m.end()-1,'{','}')
 structs.append((m[1],clean[m.end():end]))
 clean=clean[:m.start()]+clean[end+2:]
def tr(s):
 s=re.sub(r'\bconstexpr\b','const',s);s=re.sub(r'\bconst\s+','',s)
 s=s.replace('StringView','Utf8StringView').replace('nullptr','null').replace('->','.')
 s=re.sub(r'RC<([^>]+)>',r'\1',s).replace('FontFace*','FontFace?')
 s=re.sub(r'\b(Vector|Span)<([^>]+)>',lambda m:('List' if m[1]=='Vector' else 'ReadOnlySpan')+'<'+m[2]+'>',s)
 # Array local variables and inline foreach initializers.
 s=re.sub(r'std::array<(\w+),\s*\d+>\s+(\w+)\s*\{',r'\1[] \2 = {',s)
 s=re.sub(r'\b(float) (\w+)\[\]\s*\{',r'\1[] \2 = {',s)
 s=re.sub(r'for\s*\((\w+)&?\s+(\w+)\s*:\s*\{(.*?)\}\s*\)',r'foreach (\1 \2 in new \1[] {\3})',s,flags=re.S)
 s=re.sub(r'for\s*\(([\w<>]+)&?\s+(\w+)\s*:\s*([^;\n]+)\)',r'foreach (\1 \2 in \3)',s)
 s=re.sub(r'([\w<>]+)&\s+',r'\1 ',s)
 s=s.replace('npos_of<uint64_t>','ulong.MaxValue')
 s=s.replace('uint64_t','ulong').replace('int64_t','long').replace('uint32_t','uint').replace('int32_t','int')
 s=re.sub(r'std::(min|max)<[^>]+>',r'Math.\1',s).replace('Math.min','Math.Min').replace('Math.max','Math.Max')
 for a,b in [('std::max','Math.Max'),('std::min','Math.Min'),('std::floor','MathF.Floor'),('std::ceil','MathF.Ceiling'),('std::abs','MathF.Abs'),('std::isfinite','float.IsFinite'),('skr::flag_any','FlagAny')]:s=s.replace(a,b)
 s=re.sub(r'static_cast<([^>]+)>\s*\(',r'(\1)(',s)
 s=s.replace('::','.')
 s=re.sub(r'(?<=[(,])\s*\*([\w.]+)',r'\1',s)
 s=re.sub(r'!\s*late_fallback\b','late_fallback == null',s)
 s=re.sub(r'(?<![\w])object(?![\w])','object_rect',s)
 s=re.sub(r'\.(\w+)',lambda m:'.'+pascal(m[1]) if m[1][0].islower() else m[0],s)
 # Source helper names are intentionally retained for easy side by side comparison.
 s=s.replace('make_contract_fixture','Make').replace('make_contract_style','MakeStyle').replace('preload_family','PreloadFamily')
 s=re.sub(r'TextContractFixture fixture\s*=',r'using TextContractFixture fixture =',s)
 s=s.replace('kTextDefaultLineBreakFlags','TextDefaults.LineBreakFlags')
 # Convert container span wrappers, zero init, positional aggregates.
 s=re.sub(r'ReadOnlySpan<(\w+)>\((\w+),\s*\d+u\)',r'\2',s)
 s=re.sub(r'([\w<>?]+) (\w+)\s*=\s*\{\};',r'\1 \2 = new();',s)
 s=re.sub(r'\b(Offsetf|Sizef) (\w+)\(',r'\1 \2 = new(',s)
 s=re.sub(r'(?<![\w.])(Offsetf|Sizef|TextRange|TextFloatRange)\(',r'new \1(',s)
 s=re.sub(r'TextRange\s*\{([^{}]+)\}',r'new TextRange(\1)',s)
 s=s.replace('TextFloatRange{}','new TextFloatRange()')
 s=re.sub(r'\{\s*(\d+u),\s*([^{};\n]+)\s*\}',r'new TextRange(\1,\2)',s)
 s=re.sub(r'\b(TextAlignmentCase|DirectionCase)\s*\{',r'new \1 {',s)
 s=re.sub(r'TextPaintDesc (\w+)\s*\{',r'TextPaintDesc \1 = new() {',s)
 s=re.sub(r'(?<=[{,])\s*\.(\w+)\s*=',lambda m:' '+pascal(m[1])+' =',s)
 s=s.replace('.AddString({},', '.AddString(default,')
 s=s.replace('style.OpenTypeFeatures.Add({','style.OpenTypeFeatures.Add(new FontOpenTypeFeatureValue {')
 # Lists/arrays/spans get native CLR indexing; same index values.
 arrays=set(re.findall(r'\b\w+\[\]\s+(\w+)',s))
 spans=set(re.findall(r'ReadOnlySpan<\w+>\s+(\w+)',s))
 for v in sorted(arrays|spans,key=len,reverse=True):
  s=s.replace(v+'.Size()', '(ulong)'+v+'.Length')
  s=s.replace(v+'.IsEmpty()', '('+v+'.Length == 0)')
 s=s.replace('.Glyphs().Size()', '.Glyphs().Length').replace('.EllipsisGlyphs().Size()', '.EllipsisGlyphs().Length')
 s=re.sub(r'([\w.]+\.(?:Glyphs|EllipsisGlyphs)\(\))\.IsEmpty\(\)',r'\1.IsEmpty',s)
 s=re.sub(r'([\w.]+)\.Size\(\)',lambda m:m[0] if m[1].endswith(('Pos','Rect')) else m[0],s)
 # .Size() on paragraph/line and rect is a method; containers identified by source declarations.
 lists=set(re.findall(r'List<\w+>\s+(\w+)',s))
 for v in lists:
  s=s.replace(v+'.Size()', '(ulong)'+v+'.Count')
  s=s.replace(v+'.IsEmpty()', '('+v+'.Count == 0)')
  s=s.replace(v+'.Front()',v+'[0]').replace(v+'.Back()',v+'[^1]')
 s=re.sub(r'([\w.]+\.Rects)\.Size\(\)',r'(ulong)\1.Count',s)
 s=re.sub(r'([\w.]+\.Rects)\.IsEmpty\(\)',r'(\1.Count == 0)',s)
 s=s.replace('.Rects.Front()', '.Rects[0]').replace('.Rects.Back()', '.Rects[^1]')
 s=re.sub(r'\.Glyphs\(\)\.Length',r'.Glyphs().Length',s)
 # Span length result needs size_t conversion.
 s=re.sub(r'([\w.]+\.(?:Glyphs|EllipsisGlyphs)\(\))\.Length',r'(ulong)\1.Length',s)
 s=re.sub(r'\[(?!\^)([\w.]+(?:\s*[+\-]\s*[\w.]+)*)\]',r'[(int)(\1)]',s)
 # Source CHECK / EXPECT comparisons, preserve all predicates; custom messages concatenate.
 while True:
  m=re.search(r'SKR_TEST_(CHECK|EXPECT)(?:_([A-Z]+))?\s*\(',s)
  if not m:break
  end=balanced(s,m.end()-1);args=args_split(s[m.end():end]);kind=m[2]
  if kind in ('EQ','NE','LT','GT','LE','GE'):
   a,b=args
   if kind=='EQ' and b.startswith('SKR_TEST_APPROX('): call='Near('+a+','+b[len('SKR_TEST_APPROX('):-1]+')'
   elif kind=='EQ':call='Eq('+a+','+b+')'
   elif kind=='NE':call='Ne('+a+','+b+')'
   else:call='Check.That(('+a+') '+{'LT':'<','GT':'>','LE':'<=','GE':'>='}[kind]+' ('+b+'))'
  elif kind=='FALSE':call='Check.False('+args[0]+')'
  elif kind=='MESSAGE':call='Check.That('+args[0]+',string.Concat(new object?[]{'+','.join(args[1:])+'}))'
  else:call='Check.That('+args[0]+')'
  s=s[:m.start()]+call+s[end+1:]
 s=s.replace('SKR_TEST_APPROX','Approx')
 s=s.replace('make_text_fixture','MakeTextFixture').replace('make_latin_style','MakeLatinStyle').replace('preload_latin','PreloadLatin')
 s=s.replace('TextFixture fixture =','using TextFixture fixture =')
 s=s.replace('TextStyle larger = style;', 'TextStyle larger = style.Copy();').replace('TextStyle bold_style = style;', 'TextStyle bold_style = style.Copy();')
 s=s.replace('expect_near(', 'Check.Near(').replace('kFloatEpsilon', '0.0001f').replace('NAN','float.NaN')
 s=s.replace('text_test_icu_data()', 'TextContractFixture.IcuData()')
 s=s.replace('test_fonts.KLatinSize', '(ulong)TestFontAssets.Get("Latin").Length').replace('test_fonts.KLatin','TestFontAssets.Get("Latin")')
 s=s.replace('test_fonts.KHebrewSize', '(ulong)TestFontAssets.Get("Hebrew").Length').replace('test_fonts.KHebrew','TestFontAssets.Get("Hebrew")')
 s=s.replace('TextServicesDesc desc{','TextServicesDesc desc = new() {')
 s=re.sub(r'(EmbeddedLatinFontProvider|LazyFallbackFontProvider|SingleFontProvider)\.New\(',r'new \1(',s)
 s=s.replace('.Get()', '')


 s=s.replace('MakeStyle(test_case.Family)', 'MakeStyle(test_case.Family.ToString())')
 # Parameters/ref class fields etc.
 for i,value in enumerate(strings):
  value=re.sub(r'^u8','',value);value=re.sub(r"^U'", "(uint)'",value)
  s=s.replace(f'STRTOKEN{i}TOKEN',value)
 return s
helper=''
extra='''using static SkrGui.Tests.TextBaselineFixture;
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
'''
output=extra+helper
cases=[]
for i,m in enumerate(re.finditer(r'SKR_TEST_CASE\((STRTOKEN\d+TOKEN)\)\s*\{',clean)):
 end=balanced(clean,m.end()-1,'{','}');name=strings[int(re.search(r'\d+',m[1])[0])][1:-1]
 body=tr(clean[m.end()-1:end+1])
 output+=f'\n[GuiTest("{name}")]\npublic static void SourceCase{i+1:02}()\n'+body+'\n'
 cases.append({'source':'tests/text/text_baseline_common_tests.cpp','case':name,'target':f'TextBaselineCommonTests.SourceCase{i+1:02}'})
output+='}\n'
(root/'tests/SkrGui.Core.Tests/Text/TextBaselineCommonTests.cs').write_text(output,encoding='utf-8')
(root/'migration/text-baseline-common-test-map.json').write_text(json.dumps(cases,indent=2),encoding='utf-8')
print('Translated',len(cases),'complete layout cases.')
