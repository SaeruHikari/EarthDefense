from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/math/shape_tests.cpp')
out=repo/'tests/SkrGui.Core.Tests/Math'
def pascal(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
def close(s,i,left='{',right='}'):
 d=1
 while d:d+=(s[i]==left)-(s[i]==right);i+=1
 return i
types='Offsetf Sizef Rectf Circle Ellipse Arc EllipticalArc QuadBezier CubicBezier Superellipse SuperellipseArc ShapeSegmentCountSampleDesc ShapeStepLengthSampleDesc ShapeToleranceSampleDesc CircleSampler EllipseSampler SuperellipseSampler'.split()
def conv(s):
 s=re.sub(r'//[^\n]*','',s)
 # Original Debug harness does not execute assertion-triggering invalid-tolerance branch.
 s=re.sub(r'#if SKR_SHIPPING([\s\S]*?)#else([\s\S]*?)#endif',r'\2',s)
 s=re.sub(r'#if SKR_SHIPPING[\s\S]*?#endif','',s)
 s=re.sub(r'\b(?:const|constexpr)\s+','',s);s=re.sub(r'\(void\)\w+;','',s)
 s=s.replace('skr::math::kHalfPi','(MathF.PI * 0.5f)')
 s=s.replace('skr::math::kPi2','(MathF.PI * 2)').replace('skr::Vector<','List<')
 s=re.sub(r'(?<=[\w>])&','',s)
 s=re.sub(r'std::numeric_limits<float>::infinity\(\)','float.PositiveInfinity',s)
 s=re.sub(r'std::numeric_limits<float>::quiet_NaN\(\)','float.NaN',s)
 for a,b in [('fabs','MathF.Abs'),('abs','MathF.Abs'),('pow','MathF.Pow'),('sqrt','MathF.Sqrt'),('atan2','MathF.Atan2'),('cos','MathF.Cos'),('sin','MathF.Sin'),('isfinite','float.IsFinite'),('isinf','float.IsInfinity'),('min','CppMath.Min'),('max','CppMath.Max')]:s=s.replace('std::'+a,b)
 for m in reversed(list(re.finditer(r'static_cast<([^>]+)>\(',s))):
  end=close(s,m.end(),'(',')');s=s[:m.start()]+'(('+m[1]+')('+s[m.end():end-1]+'))'+s[end:]
 for a,b in [('uint32_t','uint'),('uint64_t','int'),('size_t','int'),('auto','var')]:s=re.sub(r'\b'+a+r'\b',b,s)
 s=s.replace('::','.')
 s=re.sub(r'(?<!\w)(\d+)\.f',r'\1.0f',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\s+(\w+)\(([^;]+)\);',r'\1 \2 = new \1(\3);',s)
 s=re.sub(r'(?<![\w.])('+ '|'.join(types)+r')\(',r'new \1(',s).replace('new new ','new ')
 s=re.sub(r'\b('+ '|'.join(types)+r')\s+(\w+)\s*=\s*\{([^;]+)\};',r'\1 \2 = new \1(\3);',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\s+(\w+)\s*\{([^{}]*)\};',lambda m:m[1]+' '+m[2]+' = new '+m[1]+'('+m[3].strip().rstrip(',')+');',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\s*\{([^{}]*)\}',r'new \1(\2)',s)
 s=re.sub(r'\b(\w+)\s+(\w+)\[\]\s*=',r'\1[] \2 =',s)
 s=re.sub(r'for\s*\(\s*(\w+)\s+(\w+)\s*:\s*([^\n]+)\)',r'foreach (\1 \2 in \3)',s)
 s=re.sub(r'\[([^]]*)\]\(([^)]+)\)\s*(?:noexcept\s*)?\{',r'(\2) => {',s)
 s=re.sub(r'\bbase\b','@base',s)
 s=s.replace('.add({ point, t });','.add(new CollectedShapeSample(point,t));')
 s=re.sub(r'\.([a-z_]\w*)',lambda m:'.'+pascal(m[1]),s)
 for n in set(re.findall(r'\b([a-z_]\w*)\s*(?:<[^>]+>)?\s*\(',s)):
  if '_' in n:s=re.sub(r'\b'+n+r'(?=\s*(?:<[^>]+>)?\s*\()',pascal(n),s)
 for a,b in [('SKR_TEST_EXPECT_FALSE','Check.False'),('SKR_TEST_EXPECT_EQ','EqualNumeric'),('SKR_TEST_EXPECT','Check.That'),('SKR_TEST_CHECK','Check.That')]:s=s.replace(a,b)
 # List indexing has int indices; source uint samples stay uint only in geometry APIs.
 s=re.sub(r'\[([^]\n]+)\]',lambda m:'['+re.sub(r'(\d+)u',r'\1',m[1])+']',s)
 s=re.sub(r'\b(List<[^>]+>)\s+(\w+)\s*=\s*\{\};',r'\1 \2 = new();',s)
 s=re.sub(r'int (\w+) = (\d+)u;',r'int \1 = \2;',s)
 s=re.sub(r'sizeof\((\w+)\) / sizeof\(\1\[0\]\)',r'\1.Length',s)
 s=s.replace('var check_sample =', 'var CheckSample =').replace('var expect_same_points =','var ExpectSamePoints =')
 s=re.sub(r'var (\w+) = CollectShapeSamples\(',r'List<CollectedShapeSample> \1 = CollectShapeSamples(',s)
 s=re.sub(r'var (\w+) = CollectCubicBeziers\(',r'List<CubicBezier> \1 = CollectCubicBeziers(',s)
 s=s.replace('var eval = (CircleSampler sampler, float t) =>', 'ShapeSampleEvaluator<CircleSampler> eval = (ref CircleSampler sampler, float t) =>')
 s=re.sub(r'(?m)^(\s*)(\w+_sampler),\s*\n(\s*)eval,',r'\1ref \2,\n\3eval,',s)
 return s
text=src.read_text();prefix=text[:text.index('SKR_TEST_CASE')]
helpers=['''
private readonly record struct CollectedShapeSample(Offsetf Point,float T);
private static List<CollectedShapeSample> CollectShapeSamples(dynamic shape,dynamic desc)
{var result=new List<CollectedShapeSample>();shape.Sample(desc,(Action<Offsetf,float>)((point,t)=>result.Add(new(point,t))));return result;}
private static List<CollectedShapeSample> CollectShapeSamplerSamples(dynamic shape,dynamic desc)
{
 var result=new List<CollectedShapeSample>();
 if(shape is Circle || shape is Arc)shape.SampleWithSampler(desc,(Action<Offsetf,float,CircleSampler>)((point,t,sampler)=>result.Add(new(point,t))));
 else if(shape is Ellipse || shape is EllipticalArc)shape.SampleWithSampler(desc,(Action<Offsetf,float,EllipseSampler>)((point,t,sampler)=>result.Add(new(point,t))));
 else shape.SampleWithSampler(desc,(Action<Offsetf,float,SuperellipseSampler>)((point,t,sampler)=>result.Add(new(point,t))));
 return result;
}
private static List<CubicBezier> CollectCubicBeziersFast(dynamic shape)
{var result=new List<CubicBezier>();shape.SplitToCubicBeziersFast((Action<CubicBezier>)(cubic=>result.Add(cubic)));return result;}
private static List<CubicBezier> CollectCubicBeziersFast(dynamic shape,EShapeSampleDirection direction)
{var result=new List<CubicBezier>();shape.SplitToCubicBeziersFast(direction,(Action<CubicBezier>)(cubic=>result.Add(cubic)));return result;}
private static List<CubicBezier> CollectCubicBeziers(dynamic shape,float tolerance)
{var result=new List<CubicBezier>();shape.SplitToCubicBeziers(tolerance,(Action<CubicBezier>)(cubic=>result.Add(cubic)));return result;}
private static List<CubicBezier> CollectCubicBeziers(dynamic shape,float tolerance,EShapeSampleDirection direction)
{var result=new List<CubicBezier>();shape.SplitToCubicBeziers(tolerance,direction,(Action<CubicBezier>)(cubic=>result.Add(cubic)));return result;}
private static uint CountSplitCubicBeziersFast(dynamic shape)=> (uint)CollectCubicBeziersFast(shape).Count;
private static uint CountSplitCubicBeziersFast(dynamic shape,EShapeSampleDirection direction)=>(uint)CollectCubicBeziersFast(shape,direction).Count;
private static uint CountSplitCubicBeziers(dynamic shape,float tolerance)=>(uint)CollectCubicBeziers(shape,tolerance).Count;
private static uint CountSplitCubicBeziers(dynamic shape,float tolerance,EShapeSampleDirection direction)=>(uint)CollectCubicBeziers(shape,tolerance,direction).Count;
private static uint CountSplitCubicBeziers(dynamic shape,float tolerance,uint maxDepth)
{uint count=0;shape.SplitToCubicBeziers(tolerance,(Action<CubicBezier>)(cubic=>++count),maxDepth);return count;}
private static uint CountSplitCubicBeziers(dynamic shape,float tolerance,EShapeSampleDirection direction,uint maxDepth)
{uint count=0;shape.SplitToCubicBeziers(tolerance,direction,(Action<CubicBezier>)(cubic=>++count),maxDepth);return count;}
''']
# Translate remaining original helper bodies; generic duck-typed shape arguments remain dynamic in tests only.
for m in re.finditer(r'^(float|void|skr::Vector<CollectedShapeSample>)\s+(\w+)\(([^;{}]*?)\)\s*\{',prefix,re.M):
 name=m[2]
 if name in ['expect_calc_tolerance_scale_response','collect_shape_samples','collect_shape_sampler_samples']:continue
 end=close(prefix,m.end());rt=conv(m[1]);args=conv(m[3]).replace('Shape shape','dynamic shape').replace('Desc desc','dynamic desc')
 body=conv(prefix[m.end():end-1]);body=re.sub(r'\(CubicBezier\) =>',r'(CubicBezier unused) =>',body)
 body=body.replace('shape.SplitToCubicBeziersFast(\n        (CubicBezier unused) => {\n            ++callback_count;\n        }\n    );','shape.SplitToCubicBeziersFast((Action<CubicBezier>)(unused=>++callback_count));')
 body=re.sub(r'\(CubicBezier unused\) => \{\s*\+\+callback_count;\s*\}',r'(Action<CubicBezier>)(unused=>++callback_count)',body)
 helpers.append('private static '+rt+' '+pascal(name)+'('+args+'){'+body+'}')
helpers.append('''private static void ExpectCalcToleranceScaleResponse<T>()
{float Calc(float a,float b)=>(float)typeof(T).GetMethod("CalcTolerance")!.Invoke(null,new object[]{a,b})!;
 float value=Calc(1,1),higherTessellation=Calc(2,1),higherPixelRatio=Calc(1,2),higherBoth=Calc(2,2);
 Check.That(value>0);Check.That(higherTessellation<value);Check.That(higherPixelRatio<value);Check.That(higherBoth<higherTessellation);Check.That(higherBoth<higherPixelRatio);ExpectNear(Calc(0,1),value,kFloatEpsilon);}
''')
parts=[];rows=[]
for i,m in enumerate(re.finditer(r'SKR_TEST_CASE\("([^"]+)"\)\s*\{',text)):
 end=close(text,m.end());parts.append('[GuiTest('+json.dumps('math/shape_tests.cpp::'+m[1])+')]\npublic static void Case'+str(i)+'(){\n'+conv(text[m.end():end-1])+'\n}')
 rows.append(dict(source='math/shape_tests.cpp',case=m[1],method='Case'+str(i),status='translated-unverified'))
(out/'ShapeTests.cs').write_text('using SkrGui;\nusing static SkrGui.Tests.OriginalMathHelpers;\nnamespace SkrGui.Tests;\ninternal static class ShapeTests\n{\n'+'\n'.join(helpers+parts)+'\n}\n')
(repo/'.report/shape-source-test-mapping.json').write_text(json.dumps(rows,indent=2))
