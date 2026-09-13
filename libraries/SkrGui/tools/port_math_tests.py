"""Translate original independent GUI math assertions and M3 golden fixtures."""
from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests')
out=repo/'tests/SkrGui.Core.Tests/Math';out.mkdir(parents=True,exist_ok=True)
def pascal(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
def blocks(s,pattern):
 result=[]
 for m in re.finditer(pattern,s,re.M):
  i=m.end();depth=1
  while depth:
   depth+=(s[i]=='{')-(s[i]=='}');i+=1
  result.append((m,s[m.end():i-1],i))
 return result
types='Offsetf Offseti Sizef Sizei Rectf Recti Radius RRect Alignment AlignmentDirectional AlignmentMixed EdgeInsets EdgeInsetsDirectional EdgeInsetsMixed BoxConstraints Placement HCTColor SRGBColor M3ColorScheme M3TonalPalette FittedSizes'.split()
typemap={'size_t':'int','uint8_t':'byte','uint32_t':'uint','uint16_t':'ushort','int32_t':'int','auto':'var'}
records=[]
def conv(s,ret='void'):
 s=re.sub(r'//[^\n]*','',s)
 s=re.sub(r'\b(?:const|constexpr|inline)\s+','',s)
 # Lift plain local fixture records because C# has no local struct declaration.
 def record(m):
  name=m[1];fields=re.findall(r'([\w*]+)\s+(\w+);',m[2]);params=[]
  for t,n in fields:params.append(t.replace('*','')+' '+pascal(n))
  records.append('private readonly record struct '+name+'('+','.join(params)+');')
  return ''
 s=re.sub(r'struct (\w+)\s*\{([^}]+)\};',record,s)
 s=re.sub(r'(?:::)?skr::Optional<(\w+)>',r'\1?',s)
 s=re.sub(r'std::array<(\w+),\s*\d+>',r'\1[]',s)
 s=s.replace('char*','string').replace('M3ColorScheme*','M3ColorScheme')
 s=re.sub(r'(?<=\w)&(?=\s)', '',s)
 s=s.replace('Placement reset_result = reset.fill();','ref Placement reset_result = ref reset.ResetFill();')
 s=s.replace('&reset_result == &reset','System.Runtime.CompilerServices.Unsafe.AreSame(ref reset_result,ref reset)')
 s=s.replace('*scheme.tertiary_source_color','scheme.tertiary_source_color.Value').replace('tertiary_source_color->','tertiary_source_color.Value.')
 s=s.replace('*result','result.Value').replace('*scheme','scheme')
 s=re.sub(r'&(?=scheme\.|invalid_)','',s)
 s=s.replace('std::move(','Identity(')
 s=re.sub(r'std::numeric_limits<(float|double)>::infinity\(\)',r'\1.PositiveInfinity',s)
 s=re.sub(r'std::numeric_limits<(float|double)>::quiet_NaN\(\)',r'\1.NaN',s)
 s=re.sub(r'std::size\((\w+)\)',r'\1.Length',s)
 for a,b in [('fabs','Math.Abs'),('abs','Math.Abs'),('isinf','double.IsInfinity'),('isnan','double.IsNaN'),('isfinite','double.IsFinite'),('atan2','MathF.Atan2'),('fmod','CppMath.Fmod'),('pow','Math.Pow'),('round','HctMath.Round')]:s=s.replace('std::'+a,b)
 s=re.sub(r'static_cast<([^>]+)>\(',lambda m:'('+typemap.get(m[1],m[1])+')(',s)
 for k,v in typemap.items():s=re.sub(r'\b'+k+r'\b',v,s)
 s=s.replace('->','.').replace('::','.')
 s=s.replace('[]&', '[]')
 s=re.sub(r'\bfixed\b', '@fixed',s)
 s=re.sub(r'(?<!\w)(\d+)\.f',r'\1.0f',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\s+(\w+)\(([^;]+)\);',r'\1 \2 = new \1(\3);',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\s+(\w+)\s*;',r'\1 \2 = new();',s)
 s=re.sub(r'(?<![\w.])('+ '|'.join(types)+r')\(',r'new \1(',s).replace('new new ','new ')
 s=re.sub(r'\b('+ '|'.join(types)+r')\{\}',r'new \1()',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\{([^{}]+)\}',r'new \1(\2)',s)
 s=re.sub(r'for\s*\(\s*([\w]+)\s+(\w+)\s*:\s*([^\n]+)\)',r'foreach (\1 \2 in \3)',s)
 s=re.sub(r'foreach \(([^)]+) in \{([^}]+)\}\)',r'foreach (\1 in new[]{\2})',s)
 s=re.sub(r'\b(\w+)\s+(\w+)\[\]\s*=',r'\1[] \2 =',s)
 for recordname in ['PaletteCase','BoundaryCase']:
  pat=r'('+recordname+r'\[\]\s+\w+\s*=\s*)\{([\s\S]*?)\};'
  s=re.sub(pat,lambda m:m[1]+'new '+recordname+'[]{'+re.sub(r'\{([^{}]*)\}',r'new '+recordname+r'(\1)',m[2])+'};',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\s+(\w+)\{\};',r'\1 \2 = new();',s)
 s=s.replace('{}','null')
 s=s.replace('.has_value()','.HasValue')
 s=re.sub(r'\b(actual|lhs_colors|kContrastLevels)\.size\(\)',r'\1.Length',s)
 # PascalCase member and function mapping, with source constructors left intact.
 s=re.sub(r'\.([a-z_]\w*)',lambda m:'.'+pascal(m[1]),s)
 for name in set(re.findall(r'\b([a-z_]\w*)\s*\(',s)):
  if '_' in name:s=re.sub(r'\b'+name+r'(?=\s*\()',pascal(name),s)
 s=s.replace('.Fill()', '.ResetFill()') # static Fill remains untouched below
 s=s.replace('Placement.ResetFill()', 'Placement.Fill()')
 s=s.replace('.Pin(','.PinAt(').replace('Placement.PinAt(', 'Placement.Pin(')
 s=s.replace('.Align(','.AlignTo(').replace('Placement.AlignTo(', 'Placement.Align(')
 for a,b in [('SKR_TEST_EXPECT_FALSE','Check.False'),('SKR_TEST_CHECK_FALSE','Check.False'),('SKR_TEST_EXPECT','Check.That'),('SKR_TEST_CHECK_EQ','EqualNumeric'),('SKR_TEST_CHECK_NE','NotEqualNumeric'),('SKR_TEST_CHECK_LE','LessEqual'),('SKR_TEST_CHECK_LT','Less'),('SKR_TEST_CHECK_GT','Greater'),('SKR_TEST_CHECK','Check.That')]:s=s.replace(a,b)
 s=s.replace('return {','return new '+ret+'{')
 s=s.replace('ApplyBoxFit(', 'GuiMath.ApplyBoxFit(')
 s=s.replace('Check.That_EQ(', 'EqualNumeric(').replace('.@fixed','.Fixed')
 s=s.replace('new Placement(pin_factory)', 'pin_factory').replace('new Placement(Identity(copied))','Identity(copied)')
 s=re.sub(r'\(bool\)\((none_to_value|value_to_none|value_to_value)\)',r'\1.HasValue',s)
 s=re.sub(r'\*(none_to_value|value_to_none|value_to_value)',r'\1.Value',s)
 s=s.replace('(bool)(Rectf.Lerp(none, none, 0.5f))','Rectf.Lerp(none, none, 0.5f).HasValue')
 return s
helpertext=(src/'test_common.hpp').read_text()
helpers=[]
for m,b,i in blocks(helpertext,r'^inline void (\w+)\(([^;{}]*?)\)\s*\{'):
 if m[1]=='expect_near':continue
 helpers.append('public static void '+pascal(m[1])+'('+conv(m[2])+'){'+conv(b)+'}')
helperhead='''using SkrGui;
namespace SkrGui.Tests;
internal static class OriginalMathHelpers
{
 public const float kFloatEpsilon=0.0001f,kPi=3.14159265358979323846f;
 public static void ExpectNear(double a,double b,double epsilon)=>Check.That(Math.Abs(a-b)<=epsilon,$"{a} != {b} within {epsilon}");
 public static T Identity<T>(T value)=>value;
 public static void EqualNumeric<T,U>(T a,U b) { if(a is IConvertible&&b is IConvertible)Check.That(Convert.ToDouble(a)==Convert.ToDouble(b),$"{a} != {b}");else Check.That(Equals(a,b),$"{a} != {b}"); }
 public static void NotEqualNumeric<T,U>(T a,U b)=>Check.False(Equals(a,b));
 public static void LessEqual(double a,double b)=>Check.That(a<=b);
 public static void Less(double a,double b)=>Check.That(a<b);
 public static void Greater(double a,double b)=>Check.That(a>b);
'''
(out/'OriginalMathHelpers.cs').write_text(helperhead+'\n'.join(helpers)+'}\n')
rows=[]
for rel in ['math/geometry_tests.cpp','math/layout_tests.cpp','math/placement_tests.cpp','math/m3/m3_color_test.cpp']:
 text=(src/rel).read_text();cls=pascal(Path(rel).stem);records=[];parts=[]
 pre=text[:text.find('SKR_TEST_CASE')]
 if 'placement' in rel or '/m3/' in rel:
  # Preserve all original local numerical/check helpers.
  for m,b,i in blocks(pre,r'^([\w:<> ,*&]+?)\s+(\w+)\(([^;{}]*?)\)\s*\{'):
   rt=conv(m[1]).strip();params=conv(m[3]);parts.append('private static '+rt+' '+pascal(m[2])+'('+params+'){'+conv(b,rt)+'}')
 for index,(m,b,i) in enumerate(blocks(text,r'SKR_TEST_CASE\(\s*"([^"]+)"\s*\)\s*\{')):
  parts.append('[GuiTest('+json.dumps(rel+'::'+m[1])+')]\npublic static void Case'+str(index)+'(){\n'+conv(b)+'\n}')
  rows.append(dict(source=rel,case=m[1],target=cls+'.cs',method='Case'+str(index),status='translated-unverified'))
 convertedrecords=[conv(r) for r in records]
 (out/(cls+'.cs')).write_text('using SkrGui;\nusing static SkrGui.Tests.OriginalMathHelpers;\nnamespace SkrGui.Tests;\ninternal static class '+cls+'\n{\n'+'\n'.join(convertedrecords+parts)+'\n}\n')
(repo/'.report/structure-math-test-mapping.json').write_text(json.dumps(rows,indent=2))
