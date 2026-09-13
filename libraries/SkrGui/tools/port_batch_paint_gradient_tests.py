from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[1]
base=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/batch')
def pc(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
def body(s,a):
 i=a+1;d=1
 while d:
  if s[i]=='{':d+=1
  if s[i]=='}':d-=1
  i+=1
 return s[a:i]
def tr(s):
 s=re.sub(r'\bconst\s+','',s);s=s.replace('std::numeric_limits<float>::infinity()','float.PositiveInfinity').replace('kPi','MathF.PI')
 s=re.sub(r'RC<(\w+)>::New\(\)',r'new \1()',s)
 s=re.sub(r'skr::Span<(?:ColorGradientStop)>\((\w+),\s*\d+u\)',r'\1',s).replace('skr::Span<ColorGradientStop>()','Array.Empty<ColorGradientStop>()')
 s=re.sub(r'static_cast<(\w+)\*>\((\w+).get\(\)\)',r'(\1)\2',s)
 s=re.sub(r'static_cast<(\w+)>\(',r'(\1)(',s)
 s=re.sub(r'(\w+)->rttr_cast<(\w+)>\(\) != nullptr',r'\1 is \2',s)
 s=s.replace('->','.').replace('::','.').replace('nullptr','null')
 s=re.sub(r'\.(\w+)',lambda m:'.'+pc(m[1]) if m[1][0].islower() else m[0],s).replace('.Size()', '.Count')
 s=re.sub(r'\bauto\b','var',s)
 s=re.sub(r'\b(ColorGradientSolid|ColorGradientAxis|ColorGradientCurve)\*',r'\1?',s)
 s=re.sub(r'\b(ColorGradient|PaintContext|PaintTransform) (\w+)(?: = \{\})?;',r'\1 \2 = new();',s)
 s=re.sub(r'\b(SRGBColor|Offsetf|ColorGradientAxis|PaintContext) (\w+)\(',r'\1 \2 = new(',s)
 s=re.sub(r'(?<![.\w])(ColorGradient|ColorGradientSolid|ColorGradientAxis|ColorGradientCurve|SRGBColor|Offsetf)\(',r'new \1(',s)
 s=re.sub(r'\bColorGradientStop (\w+)\[\]',r'ColorGradientStop[] \1',s)
 s=re.sub(r'\{ ([\d.-]+f), ([^\n]+?) \}',r'new ColorGradientStop(\1, \2)',s)
 s=s.replace('SKR_TEST_CHECK_FALSE','False').replace('SKR_TEST_CHECK_EQ','Eq').replace('SKR_TEST_CHECK','True')
 s=re.sub(r'if \(!(\w+)\)',r'if (\1 is null)',s)
 s=re.sub(r'ResolvePixelMapping\(\s*pixel_ratio,\s*device_offset\s*\)',r'ResolvePixelMapping(out pixel_ratio,out device_offset)',s)
 s=s.replace('stops.Count','stops.Length')
 s=s.replace('2d(', '2D(').replace('3d(', '3D(').replace('= {};','= new();')
 return s
prefix='''namespace SkrGui.Tests;
public static class BatchPaintGradientContractTests
{
 const float kFloatEpsilon=.0001f;
 static void True(bool value)=>Check.That(value);static void True(object? value)=>Check.NotNull(value);
 static void False(bool value)=>Check.False(value);static void False(object? value)=>Check.Null(value);
 static void Eq<T>(T a,T b)=>Check.Equal(a,b);static void Eq(int a,uint b)=>Check.Equal((long)a,(long)b);
 static void expect_near(float a,float b,float epsilon)=>Check.Near(b,a,epsilon);
 static void check_color(SRGBColor c,float r,float g,float b,float a){Check.Near(r,c.R,kFloatEpsilon);Check.Near(g,c.G,kFloatEpsilon);Check.Near(b,c.B,kFloatEpsilon);Check.Near(a,c.A,kFloatEpsilon);}
 static void check_offsetf(Offsetf p,float x,float y){Check.Near(x,p.X,kFloatEpsilon);Check.Near(y,p.Y,kFloatEpsilon);}
 static void check_rectf(Rectf r,float l,float t,float rr,float b){Check.Near(l,r.Left,kFloatEpsilon);Check.Near(t,r.Top,kFloatEpsilon);Check.Near(rr,r.Right,kFloatEpsilon);Check.Near(b,r.Bottom,kFloatEpsilon);}
 sealed class PaintContextTestTexture:Texture { public override Sizei PixelSize()=>new(1,1); }
 sealed class PaintContextTestShader:Shader {}
 static Mesh make_paint_context_mesh(){var m=new Mesh();m.Vertices.Add(new(new(0,0),Offsetf.Zero(),uint.MaxValue));m.Vertices.Add(new(new(1,0),Offsetf.Zero(),uint.MaxValue));m.Vertices.Add(new(new(0,1),Offsetf.Zero(),uint.MaxValue));m.Indices.AddRange(new uint[]{0,1,2});return m;}
'''
out=prefix;mapping=[];i=0
for name in ['color_gradient_tests','paint_context_tests']:
 s=(base/(name+'.cpp')).read_text(encoding='utf-8')
 for m in re.finditer(r'SKR_TEST_CASE\("([^\"]+)"\)\s*\{',s):
  i+=1;out+=f' [GuiTest("{m[1]}")] public static void Case{i}()\n'+tr(body(s,m.end()-1))+'\n';mapping.append({'source':'tests/batch/'+name+'.cpp','line':s[:m.start()].count('\n')+1,'case':m[1],'target':f'BatchPaintGradientContractTests.Case{i}'})
out+='}\n';(ROOT/'tests/SkrGui.Core.Tests/Batch/BatchPaintGradientContractTests.cs').write_text(out);(ROOT/'migration/batch-paint-gradient-test-map.json').write_text(json.dumps(mapping,indent=2));print(i,'complete source cases')
