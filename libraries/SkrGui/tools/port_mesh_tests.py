import pathlib,re,json
root=pathlib.Path(__file__).resolve().parents[1]
source_path=pathlib.Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/batch/mesh_tests.cpp')
source=source_path.read_text()
cases=[]
for m in re.finditer(r'SKR_TEST_CASE\("([^"]+)"\)\s*\{',source):
 start=m.end()-1;depth=0
 for end in range(start,len(source)):
  if source[end]=='{':depth+=1
  elif source[end]=='}':
   depth-=1
   if depth==0:break
 cases.append((m.group(1),source[start:end+1],source[:m.start()].count('\n')+1))
def pascal(n):
 return {'rrect':'RRect','rrect_fill':'RRectFill','rrect_stroke':'RRectStroke','rrect_border':'RRectBorder','diff_rrect':'DiffRRect'}.get(n,''.join(x[:1].upper()+x[1:] for x in n.split('_')))
def tr(s):
 s=re.sub(r'\bconst\s+','',s)
 s=s.replace('skr::math::kHalfPi','(MathF.PI * .5f)').replace('skr::math::kPi','MathF.PI')
 s=re.sub(r'skr::Span<(?:TextRenderRect|ColorGradientStop)>\((\w+),\s*\d+u\)',r'\1',s)
 s=re.sub(r'skr::Span<(TextRenderRect|ColorGradientStop)>\(\)',r'Array.Empty<\1>()',s)
 s=s.replace('::','.')
 s=re.sub(r'\.(\w+)',lambda m:'.'+pascal(m[1]) if m[1][0].islower() else m[0],s)
 s=s.replace('.Size()', '.Count')
 s=re.sub(r'([\w.]+)\.IsEmpty\(\)',r'(\1.Count == 0)',s)
 s=re.sub(r'\b(Mesh|VGFillWorkspace|BasicMeshOptions) (\w+)(?: = \{\})?;',r'\1 \2 = new();',s)
 s=re.sub(r'\b(MeshBuilder|SRGBColor|ColorGradient|ColorGradientSolid|Rectf) (\w+)\(',r'\1 \2 = new(',s)
 s=re.sub(r'(?<![\w.])(ColorGradientAxis|ColorGradientCurve|ColorGradientSolid|SRGBColor|Rectf|Offsetf|TextLayoutSpanId)\(',r'new \1(',s)
 s=re.sub(r'\b(TextRenderRect|ColorGradientStop|EdgeInsets) (\w+)\[\]',r'\1[] \2',s)
 s=s.replace('TextRenderRect{','new TextRenderRect {')
 s=re.sub(r'(?<=[{,])\s*\.(\w+)\s*=',lambda m:' '+pascal(m[1])+' =',s)
 s=re.sub(r'\{ (\d+\.\d+f), ([^\n]+?) \}',r'new ColorGradientStop(\1, \2)',s)
 s=s.replace('{}','new BasicMeshOptions()').replace('&uv_rect','uv_rect').replace('&gradient','gradient').replace('&workspace','workspace').replace('nullptr','null')
 s=s.replace('MeshIndex','uint').replace('uint64_t','int')
 s=re.sub(r'for \(EdgeInsets& (\w+) : (\w+)\)',r'foreach (EdgeInsets \1 in \2)',s)
 s=re.sub(r'\.Vertices\[(left|right|straight|fallback)\]',r'.Vertices[(int)\1]',s)
 s=s.replace('SKR_TEST_CHECK_FALSE','Check.False').replace('SKR_TEST_CHECK_EQ','Eq').replace('SKR_TEST_CHECK','Check.That')
 return s
helper='''namespace SkrGui.Tests;
// Source: core/tests/batch/mesh_tests.cpp. All original inputs, predicates and case names retained.
public static class BasicMeshesContractTests
{
 private const float kFloatEpsilon = .0001f;
 private static void Eq<T>(T a,T b) => Check.Equal(a,b);
 private static void Eq(int a,uint b) => Check.Equal((long)a,(long)b);
 private static void Eq(ulong a,uint b) => Check.Equal(a,(ulong)b);
 private static SRGBColor unpack_color(uint p)=>SRGBColor.FromRGBA32(p);
 private static void check_offsetf(Offsetf v,float x,float y){Check.Near(x,v.X,kFloatEpsilon);Check.Near(y,v.Y,kFloatEpsilon);}
 private static void check_color(SRGBColor v,float r,float g,float b,float a){Check.Near(r,v.R,kFloatEpsilon);Check.Near(g,v.G,kFloatEpsilon);Check.Near(b,v.B,kFloatEpsilon);Check.Near(a,v.A,kFloatEpsilon);}
 private static bool has_vertex_at(Mesh mesh,float x,float y)=>mesh.Vertices.Any(v=>MathF.Abs(v.Pos.X-x)<=kFloatEpsilon&&MathF.Abs(v.Pos.Y-y)<=kFloatEpsilon);
 private static bool has_vertex_uv_at(Mesh mesh,float x,float y,float u,float v)=>mesh.Vertices.Any(p=>MathF.Abs(p.Pos.X-x)<=kFloatEpsilon&&MathF.Abs(p.Pos.Y-y)<=kFloatEpsilon&&MathF.Abs(p.Uv.X-u)<=kFloatEpsilon&&MathF.Abs(p.Uv.Y-v)<=kFloatEpsilon);
 private static bool has_vertex_color_at(Mesh mesh,float x,float y,SRGBColor color)=>mesh.Vertices.Any(v=>MathF.Abs(v.Pos.X-x)<=kFloatEpsilon&&MathF.Abs(v.Pos.Y-y)<=kFloatEpsilon&&unpack_color(v.PackedColor).NearlyEqual(color,1f/255+kFloatEpsilon));
 private static bool has_duplicate_vertices(Mesh mesh){for(int i=0;i<mesh.Vertices.Count;i++)for(int j=i+1;j<mesh.Vertices.Count;j++){var a=mesh.Vertices[i];var b=mesh.Vertices[j];if(a.Pos==b.Pos&&a.Uv==b.Uv&&a.PackedColor==b.PackedColor)return true;}return false;}
 private static bool is_mesh_partitioned_at_x(Mesh mesh,float x){if(mesh.Indices.Count%3!=0)return false;for(int i=0;i<mesh.Indices.Count;i+=3){bool left=false,right=false;for(int j=0;j<3;j++){uint index=mesh.Indices[i+j];if(index>=mesh.Vertices.Count)return false;float vx=mesh.Vertices[(int)index].Pos.X;left|=vx<x-kFloatEpsilon;right|=vx>x+kFloatEpsilon;}if(left&&right)return false;}return true;}
 private static void check_mesh_equal(Mesh a,Mesh b){Check.Equal(a.Vertices.Count,b.Vertices.Count);Check.Equal(a.Indices.Count,b.Indices.Count);for(int i=0;i<a.Vertices.Count;i++){Check.Equal(a.Vertices[i].Pos,b.Vertices[i].Pos);Check.Equal(a.Vertices[i].Uv,b.Vertices[i].Uv);Check.Equal(a.Vertices[i].PackedColor,b.Vertices[i].PackedColor);}Check.SequenceEqual(a.Indices,b.Indices);}
'''
output=helper
mapping=[]
for i,(name,body,line) in enumerate(cases):
 output+=f'\n [GuiTest("{name}")]\n public static void SourceCase{i+1:02}()\n'+tr(body)+'\n'
 mapping.append({'source':'tests/batch/mesh_tests.cpp','line':line,'case':name,'target':f'BasicMeshesContractTests.SourceCase{i+1:02}'})
output+='}\n'
(root/'tests/SkrGui.Core.Tests/Batch').mkdir(exist_ok=True)
(root/'tests/SkrGui.Core.Tests/Batch/BasicMeshesContractTests.cs').write_text(output)
(root/'migration/mesh-test-map.json').write_text(json.dumps(mapping,indent=2))
print('Mapped all',len(cases),'mesh cases.')
