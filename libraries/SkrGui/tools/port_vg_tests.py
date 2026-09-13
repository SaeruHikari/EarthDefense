from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[1];base=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/vg')
def pc(s):
 if s in ['rrect','rrect_stroke','diff_rrect']:return {'rrect':'RRect','rrect_stroke':'RRectStroke','diff_rrect':'DiffRRect'}[s]
 return ''.join(x[:1].upper()+x[1:] for x in s.strip('_').split('_'))
def body(s,a):
 i=a+1;d=1
 while d:
  if s[i]=='{':d+=1
  elif s[i]=='}':d-=1
  i+=1
 return s[a:i]
def tr(s):
 s=re.sub(r'\b(\d+)\.f\b',r'\1.0f',s);s=re.sub(r'//[^\n]*','',s);s=s.replace('#if SKR_SHIPPING','#if !DEBUG').replace('#endif','#endif')
 s=re.sub(r'SKR_TEST_SUBCASE\([^\n]*\)', '',s)
 s=s.replace('constexpr','const');s=re.sub(r'\bconst\s+(?!float k_)','',s);s=re.sub(r'\buint(?:32|64)_t\b','uint',s)
 s=re.sub(r'\bstd::numeric_limits<float>::(?:quiet_NaN|signaling_NaN)\(\)','float.NaN',s).replace('std::numeric_limits<float>::infinity()','float.PositiveInfinity')
 s=s.replace('std::isfinite','float.IsFinite').replace('std::fabs','MathF.Abs').replace('std::abs','MathF.Abs').replace('std::max','Math.Max').replace('std::min','Math.Min').replace('std::ceil','MathF.Ceiling').replace('std::sqrt','MathF.Sqrt')
 s=s.replace('skr::math::kPi2','(MathF.PI*2)').replace('skr::math::kHalfPi','(MathF.PI/2)').replace('skr::math::kPi','MathF.PI').replace('skr::kPi','MathF.PI').replace('skr::flag_any','flag_any').replace('skr::flag_all','flag_all')
 s=re.sub(r'static_cast<([^>]+)>\(',r'(\1)(',s)
 s=re.sub(r'VGFillAllocator\{\s*&([\w]+),.*?\}',r'MakeAllocator(\1)',s,flags=re.S)
 s=s.replace('&workspace','workspace').replace('nullptr','null')
 s=re.sub(r'\b(VGPath|VGPathFlatten|VGPathFlattenContour|VGPathFlattenNode|MeshCapture|FillMeshCapture|VGStrokeOptions|VGFillOptions|Circle|Arc|Ellipse|EllipticalArc|Superellipse|SuperellipseArc|RRect|VGBasicPrimitiveOptions|Rectf|VGVertex|MeshBounds|MeshTriangle|FillMeshTriangle|VGMeshTriangle)&',r'\1',s)
 s=re.sub(r'for \((\w+)&? (\w+) : ([^)]+)\)',r'foreach (\1 \2 in \3)',s)
 s=re.sub(r'\bfloat (\w+)\[\]',r'float[] \1',s)
 s=re.sub(r'\b(\w+) (\w+) = \{\};',r'\1 \2 = new();',s)
 s=re.sub(r'VGDashPattern\{\s*(\w+)\s*,\s*([^}]+)\}',r'new VGDashPattern{Values=\1,Offset=\2}',s)

 s=re.sub(r'(?<=[,(])\s*\{\s*(\w+|\{\})\s*,\s*([^{}]+?)\s*\}',lambda m:'new VGDashPattern{Values='+('Array.Empty<float>()' if m[1]=='{}' else m[1])+',Offset='+m[2]+'}',s)
 s=re.sub(r'VGPathFlattenLine (\w+)\[\]',r'VGPathFlattenLine[] \1',s)
 s=re.sub(r'\{ (Offsetf\([^}]+?\)), (Offsetf\([^}]+?\)) \}',r'new VGPathFlattenLine{Position=\1,Direction=\2}',s)
 s=re.sub(r'skr::Span<VGPathFlattenLine>\((\w+),\s*\d+u\)',r'\1',s)

 s=re.sub(r'(?<!new )ShapeToleranceSampleDesc\{([^}]+)\}',lambda m:'new ShapeToleranceSampleDesc{Tolerance='+m[1].split(',')[0]+',Direction='+m[1].split(',')[1]+',MaxDepth='+m[1].split(',')[2]+'}',s)
 s=re.sub(r'\[[^]\n]*\]\((.*?)\)(?: noexcept)?\s*\{',lambda m:'('+', '.join((a.strip().replace('&','') if len(a.strip().replace('&','').split())>1 else a.strip().replace('&','')+' unused'+str(i)) for i,a in enumerate(m[1].split(',')))+') => {',s,flags=re.S)
 s=re.sub(r'(?<![\w.])VGVertex\{([^}]+)\}',lambda m:'new VGVertex('+m[1].strip().rstrip(',')+')',s)
 s=s.replace('::','.').replace('->','.')
 s=re.sub(r'\.(\w+)',lambda m:'.'+pc(m[1]) if m[1][0].islower() else m[0],s).replace('.Size()', '.Count').replace('.AtLast()', '[^1]')
 s=re.sub(r'([\w.]+)\.IsEmpty\(\)',lambda m:m[0] if m[1].split('.')[-1] not in ['Vertices','Triangles','ScratchContourVertices','VertexLookup'] else '('+m[1]+'.Count == 0)',s)
 s=s.replace(', {})',', new())')
 s=s.replace('.CloseData','.Closing').replace('.MoveToData','.Move').replace('.MoveData','.Move').replace('.LineToData','.Line').replace('.LineData','.Line').replace('.QuadToData','.Quad').replace('.QuadData','.Quad').replace('.CubicToData','.Cubic').replace('.CubicData','.Cubic')

 s=re.sub(r'\b(RRect|Radius|Offsetf) (\w+)\(',r'\1 \2 = new(',s)
 s=re.sub(r'(?<![.\w])(Rectf|RRect|Radius)\(',r'new \1(',s)
 s=s.replace(', {},',', new(),')

 s=s.replace('.Nodes().IsEmpty()','.Nodes().Count == 0').replace('.Contours().IsEmpty()','.Contours().Count == 0')
 s=re.sub(r'(count_body_vertices\([^()]+\)) == ([\w.]+)\.Vertices.Count',r'\1 == (ulong)\2.Vertices.Count',s)
 s=s.replace('void* ptr','nint ptr').replace('ptr != null','ptr != 0').replace('ptr == null','ptr == 0')
 s=re.sub(r'\bauto\b','var',s)
 s=s.replace('new VGDashPattern{Values=backend,Offset=', '{ backend, ')
 s=re.sub(r'\buint i =', 'uint i =',s)
 s=re.sub(r'(?<![.\w])(Offsetf|SRGBColor|VGPathFlattenLine|VGDashPattern)\(',r'new \1(',s)
 s=s.replace('Flatten({})','Flatten(new VGPathFlattenOptions())').replace('capture_fill(path, {})','capture_fill(path,new VGFillOptions())').replace('capture_fill(flatten, {})','capture_fill(flatten,new VGFillOptions())')
 s=s.replace('(void)','_ = ')
 s=re.sub(r'(VGBasicPrimitiveOptions|VGStrokeOptions|VGFillOptions) (\w+) = \{',r'\1 \2 = new(){',s)
 s=re.sub(r'(?<=[{,])\s*\.(\w+)\s*=',r' \1 =',s)
 s=re.sub(r'\b(\d+)\.f\b',r'\1.0f',s)
 s=s.replace('SKR_TEST_EXPECT_FALSE','False').replace('SKR_TEST_EXPECT','True').replace('SKR_TEST_CHECK_FALSE','False').replace('SKR_TEST_CHECK_EQ','Eq').replace('SKR_TEST_CHECK','True')
 # Nullable Optional<T> dereference.
 s=re.sub(r'\*(\w+\.CursorPos\(\))',r'\1!.Value',s)
 s=re.sub(r'\*([\w.()\[\]]+\.WindingHint)',r'\1!.Value',s)
 # All C++ integral indexes explicitly map to CLR array/List indexes.
 s=re.sub(r'\[([^]\n]+)\]',lambda m:m[0] if m[1].startswith('^') else '[(int)('+m[1]+')]',s)
 return s
aliases='''using MeshCapture=SkrGui.Tests.VgMeshCapture;
using FillMeshCapture=SkrGui.Tests.VgMeshCapture;
using MeshBounds=SkrGui.Tests.VgMeshBounds;
using MeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillMeshTriangle=SkrGui.Tests.VgMeshTriangle;
using FillAllocatorCounter=SkrGui.Tests.VgFillAllocatorCounter;
using VGFlattenSnapshot=SkrGui.Tests.VgFlattenSnapshot;
namespace SkrGui.Tests;
'''
mapping=[]
files=['vg_path_command_tests','vg_path_flatten_tests','vg_path_dash_tests','vg_path_shape_tests','vg_path_fill_tests','vg_path_stroke_tests','vg_basic_primitive_tests']
for name in files:
 s=(base/(name+'.cpp')).read_text(encoding='utf-8');cls=pc(name);out=aliases+'public sealed class '+cls+' : VgTestHelpers\n{\n'
 first=s.index('SKR_TEST_CASE');pre=s[:first]
 for m in re.finditer(r'^(\w+) (\w+)\((.*?)\)\s*\{',pre,re.S|re.M):
  args=m[3];opts=re.search(r',\s*(VGBasicPrimitiveOptions) options = \{\}',args)
  if opts:
   short=args[:opts.start()];an=[a.split('=')[0].strip().split()[-1] for a in short.split(',')]
   out+=' static '+tr(m[1]+' '+m[2]+'('+short+')')+' => '+m[2]+'('+','.join(an)+',new '+opts[1]+'());\n';args=re.sub(r' = [^,\n]+','',args)
  out+=' static '+tr(m[1]+' '+m[2]+'('+args+')')+'\n'+tr(body(pre,m.end()-1))+'\n'
 for i,m in enumerate(re.finditer(r'SKR_TEST_CASE\("([^\"]+)"\)\s*\{',s)):
  b=tr(body(s,m.end()-1));out+=' [GuiTest("'+m[1]+'")] public static void Case'+str(i+1)+'()\n'+b+'\n';mapping.append({'source':'tests/vg/'+name+'.cpp','line':s[:m.start()].count('\n')+1,'case':m[1],'target':cls+'.Case'+str(i+1)})
 out+='}\n';(ROOT/'tests/SkrGui.Core.Tests/Vg'/ (cls+'.cs')).write_text(out)
(ROOT/'migration/vg-path-test-map.json').write_text(json.dumps(mapping,indent=2));print(len(mapping),'full source cases')
