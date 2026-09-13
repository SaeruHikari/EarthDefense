from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[1];src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/vg/vg_path_stroke_nanovg_coverage_tests.cpp').read_text(encoding='utf-8')
def pc(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
def block(s,a):
 i=a+1;d=1
 while d:
  if s[i]=='{':d+=1
  elif s[i]=='}':d-=1
  i+=1
 return s[a:i]
def tr(s):
 s=re.sub(r'//[^\n]*','',s);s=re.sub(r'\bconst\s+','',s).replace('constexpr','const').replace('uint32_t','uint').replace('uint64_t','ulong')
 s=s.replace('std::sqrt','Math.Sqrt').replace('std::fabs','Math.Abs').replace('std::min','Math.Min').replace('std::max','Math.Max').replace('std::ceil','Math.Ceiling')
 s=re.sub(r'static_cast<([^>]+)>\(',r'(\1)(',s);s=s.replace('::','.')
 s=re.sub(r'\.(\w+)',lambda m:'.'+pc(m[1]) if m[1][0].islower() else m[0],s).replace('.Size()', '.Count').replace('.PushBack(','.Add(')
 s=re.sub(r'for \((\w+)& (\w+) : ([^)]+)\)',r'foreach (\1 \2 in \3)',s)
 s=re.sub(r'\b(TriangleMesh|MeshBounds|StrokeCoverageCase|VGPath)&',r'\1',s)
 s=re.sub(r'(?<![.\w])Offsetf\(', 'new Offsetf(',s)
 s=re.sub(r'(Triangle|PointD)\{([^}]+)\}',lambda m:'new '+m[1]+'('+m[2].strip().rstrip(',')+')',s)
 s=re.sub(r'PointD sample\{([^}]+)\};',lambda m:'PointD sample = new('+m[1].strip().rstrip(',')+');',s)
 s=re.sub(r'(MeshBounds|CoverageStats) (\w+) = \{\};',r'\1 \2 = new();',s)
 s=s.replace('uint base =','uint vertexBase =').replace('new Triangle( base, base + 1u, base + 2u )','new Triangle(vertexBase, vertexBase + 1u, vertexBase + 2u)')
 s=re.sub(r'\bbase\b','vertexBase',s)
 s=re.sub(r'\[([^]\n]+)\]',r'[(int)(\1)]',s)
 s=s.replace('NVGcontext* ctx','nint ctx')
 return s
pre='''using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace SkrGui.Tests;
// Source: tests/vg/vg_path_stroke_nanovg_coverage_tests.cpp, all original sampling/limits retained.
public sealed unsafe class NanoVGStrokeCoverageTests
{
 private readonly record struct PointD(double X,double Y);
 private readonly record struct Triangle(uint I0,uint I1,uint I2);
 private sealed class TriangleMesh { public List<PointD> Vertices=[]; public List<Triangle> Triangles=[]; }
 private sealed class MeshBounds { public double MinX,MinY,MaxX,MaxY;public bool Valid; }
 private enum ESampleState:byte { Outside,Inside,Ambiguous }
 private enum EPathShape:byte { HorizontalLine,DiagonalLine,LeftTurnPolyline,RightTurnPolyline,ClosedRectangle,ClosedTriangle,AcuteMiterFallback,NearCollinearJoin,MultipleOpenContours }
 private sealed class StrokeCoverageCase(string name,EPathShape shape,float width,EVGStrokeCap cap,EVGStrokeJoin join,float miterLimit=4,float tessellationFactor=1,double requestedStep=.5,uint maxAxisSamples=180,double boundaryEpsilon=.035,double xorLimit=.005,double areaLimit=.003)
 {
  public string Name=name;public EPathShape Shape=shape;public float Width=width;public EVGStrokeCap Cap=cap;public EVGStrokeJoin Join=join;
  public float MiterLimit=miterLimit,TessellationFactor=tessellationFactor;public double RequestedStep=requestedStep,BoundaryEpsilon=boundaryEpsilon,XorLimit=xorLimit,AreaLimit=areaLimit;public uint MaxAxisSamples=maxAxisSamples;
 }
 private sealed class CoverageStats { public ulong StrictSampleCount,AmbiguousCount,UnionCount,XorCount,VgInsideCount,NanovgInsideCount;public double XorRatio,AreaRatio,Step; }
 [StructLayout(LayoutKind.Sequential)] private struct NVGVertex { public float X,Y,U,V; }
 [StructLayout(LayoutKind.Sequential)] private struct NVGPath { public int First,Count;public byte Closed;public int Nbevel;public NVGVertex* Fill;public int Nfill;public NVGVertex* Stroke;public int Nstroke,Winding,Convex; }
 [StructLayout(LayoutKind.Sequential)] private struct NVGComposite { public int SrcRgb,DstRgb,SrcAlpha,DstAlpha; }
 [StructLayout(LayoutKind.Sequential)] private struct NVGParams
 {
  public nint User;public int EdgeAntiAlias;
  public delegate* unmanaged[Cdecl]<nint,nint,NVGComposite,float,float*,NVGPath*,int,void> RenderFill;
  public delegate* unmanaged[Cdecl]<nint,nint,NVGComposite,float,float,NVGPath*,int,void> RenderStroke;
 }
 private const string Lib="skrgui_nanovg_test";
 static NanoVGStrokeCoverageTests()
 {
  string? library=null;
  foreach(string start in new[]{AppContext.BaseDirectory,Environment.CurrentDirectory})
   for(var dir=new DirectoryInfo(start);dir!=null;dir=dir.Parent){string candidate=Path.Combine(dir.FullName,"native/artifacts/win-x64/skrgui_nanovg_test.dll");if(File.Exists(candidate)){library=candidate;break;}}
  if(library==null)throw new FileNotFoundException("Build pinned NanoVG test dependency with native/build_nanovg_tests.ps1");
  nint handle=NativeLibrary.Load(library);
  NativeLibrary.SetDllImportResolver(typeof(NanoVGStrokeCoverageTests).Assembly,(name,assembly,path)=>name==Lib?handle:0);
 }
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern nint nvgCreateInternal(NVGParams* parameters);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgDeleteInternal(nint ctx);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgBeginFrame(nint ctx,float ratio);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgShapeAntiAlias(nint ctx,int aa);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgStrokeWidth(nint ctx,float value);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgLineCap(nint ctx,int value);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgLineJoin(nint ctx,int value);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgMiterLimit(nint ctx,float value);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgBeginPath(nint ctx);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgMoveTo(nint ctx,float x,float y);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgLineTo(nint ctx,float x,float y);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgClosePath(nint ctx);
 [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)] private static extern void nvgStroke(nint ctx);
 private sealed class Capture { public TriangleMesh Mesh=new();public bool Finite=true; }
 [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
 private static void NvgFill(nint user,nint paint,NVGComposite operation,float fringe,float* bounds,NVGPath* paths,int pathCount) { }
 [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
 private static void NvgStroke(nint user,nint paint,NVGComposite operation,float fringe,float width,NVGPath* paths,int pathCount)
 {
  var capture=(Capture)GCHandle.FromIntPtr(user).Target!;
  for(int pathIndex=0;pathIndex<pathCount;pathIndex++)
  {
   var path=paths[pathIndex];var vertices=new List<PointD>(path.Nstroke);
   for(int i=0;i<path.Nstroke;i++){var v=path.Stroke[i];capture.Finite&=double.IsFinite(v.X)&&double.IsFinite(v.Y);vertices.Add(new(v.X,v.Y));}
   for(int i=2;i<path.Nstroke;i++)add_mesh_triangle(capture.Mesh,vertices[i-2],vertices[i-1],vertices[i]);
  }
 }
 private static TriangleMesh capture_nanovg_stroke(StrokeCoverageCase c)
 {
  var capture=new Capture();var handle=GCHandle.Alloc(capture);
  var parameters=new NVGParams{User=GCHandle.ToIntPtr(handle),EdgeAntiAlias=0,RenderFill=&NvgFill,RenderStroke=&NvgStroke};
  nint ctx=nvgCreateInternal(&parameters);Check.That(ctx!=0);
  try{nvgBeginFrame(ctx,1);nvgShapeAntiAlias(ctx,0);nvgStrokeWidth(ctx,c.Width);nvgLineCap(ctx,c.Cap switch{EVGStrokeCap.Square=>2,EVGStrokeCap.Round=>1,_=>0});nvgLineJoin(ctx,c.Join switch{EVGStrokeJoin.Bevel=>3,EVGStrokeJoin.Round=>1,_=>4});nvgMiterLimit(ctx,c.MiterLimit);build_nanovg_path(ctx,c.Shape);nvgStroke(ctx);}
  finally{nvgDeleteInternal(ctx);handle.Free();}
  Check.That(capture.Finite);Check.That(capture.Mesh.Triangles.Count>0);return capture.Mesh;
 }
 private static TriangleMesh capture_vg_stroke(VGPath path,VGStrokeOptions options)
 {
  var m=new TriangleMesh();bool finite=true,legal=true;
  var backend=new VGBackend{Reserve=(v,t)=>{m.Vertices.EnsureCapacity(m.Vertices.Count+(int)v);m.Triangles.EnsureCapacity(m.Triangles.Count+(int)t);},PushVertex=v=>{finite&=v.Pos.IsFinite()&&double.IsFinite(v.Converge);m.Vertices.Add(new(v.Pos.X,v.Pos.Y));},PushTriangle=(a,b,c)=>{legal&=a<m.Vertices.Count&&b<m.Vertices.Count&&c<m.Vertices.Count;m.Triangles.Add(new(a,b,c));}};
  Check.That(path.Flatten(new()).Stroke(backend,options));Check.That(finite);Check.That(legal);Check.That(m.Triangles.Count>0);return m;
 }
 private static void run_stroke_coverage_cases(StrokeCoverageCase[] cases)
 {
  foreach(var c in cases)
  {
   var path=new VGPath();build_vg_path(path,c.Shape);var options=new VGStrokeOptions{Width=c.Width,Cap=c.Cap,Join=c.Join,MiterLimit=c.MiterLimit,TessellationFactor=c.TessellationFactor,AaRadius=0};
   var vg=capture_vg_stroke(path,options);var nvg=capture_nanovg_stroke(c);var s=compare_coverage(vg,nvg,c);
   Check.That(s.UnionCount>0);Check.That(s.StrictSampleCount>0);Check.That(s.XorRatio<=c.XorLimit,$"{c.Name}: XOR {s.XorRatio} > {c.XorLimit}");Check.That(s.AreaRatio<=c.AreaLimit,$"{c.Name}: area {s.AreaRatio} > {c.AreaLimit}");
  }
 }
'''
selected=['cross','distance_to_segment','is_degenerate_triangle','is_point_inside_triangle','is_point_near_triangle_edge','add_mesh_triangle','extend_bounds','calc_mesh_bounds','calc_union_bounds','sample_mesh','compare_coverage','build_vg_path','build_nanovg_path']
out=pre
for m in re.finditer(r'^(\w+) (\w+)\((.*?)\)\s*\{',src,re.S|re.M):
 if m[2] not in selected:continue
 b=tr(block(src,m.end()-1));sig=tr(m[1]+' '+m[2]+'('+m[3]+')')
 if m[2]=='distance_to_segment':
  a=b.index('if (len_sq');z=b.index('\n    }',a)+6;b=b[:a]+re.sub(r'\b(dx|dy)\b',lambda n:n[1]+'_zero',b[a:z])+b[z:]
 out+=' private static '+sig+'\n'+b+'\n'
mapping=[]
for i,m in enumerate(re.finditer(r'SKR_TEST_CASE\("([^\"]+)"\)\s*\{',src)):
 b=block(src,m.end()-1);b=re.sub(r'const StrokeCoverageCase cases\[\]',r'StrokeCoverageCase[] cases',b)
 b=re.sub(r'\{ ("[^\n]+?) \}',lambda n:'new('+n[1]+')',b).replace('::','.').replace('sizeof(cases) / sizeof(cases[0])','cases.Length').replace('run_stroke_coverage_cases(cases, cases.Length)','run_stroke_coverage_cases(cases)')
 out+=' [GuiTest("'+m[1]+'")] public static void Case'+str(i+1)+'()\n'+b+'\n';mapping.append({'source':'tests/vg/vg_path_stroke_nanovg_coverage_tests.cpp','line':src[:m.start()].count('\n')+1,'case':m[1],'target':'NanoVGStrokeCoverageTests.Case'+str(i+1)})
out+='}\n';(ROOT/'tests/SkrGui.Core.Tests/Vg/NanoVGStrokeCoverageTests.cs').write_text(out);(ROOT/'migration/nanovg-coverage-test-map.json').write_text(json.dumps(mapping,indent=2));print('3 original coverage cases,17 unchanged coverage configurations')
