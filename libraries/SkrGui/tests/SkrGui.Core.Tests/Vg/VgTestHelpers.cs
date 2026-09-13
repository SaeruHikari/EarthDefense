using System.Runtime.InteropServices;
namespace SkrGui.Tests;
public class VgMeshCapture
{
 public readonly List<VGVertex> Vertices=[];public readonly List<VgMeshTriangle> Triangles=[];
 public ulong ReservedVertices,ReservedTriangles,ReserveCallCount;public bool FiniteVertices=true,LegalIndices=true;
 public void Reserve(ulong v,ulong t){ReserveCallCount++;ReservedVertices+=v;ReservedTriangles+=t;Vertices.EnsureCapacity(Vertices.Count+(int)v);Triangles.EnsureCapacity(Triangles.Count+(int)t);}
 public void PushVertex(VGVertex v){FiniteVertices&=v.Pos.IsFinite()&&float.IsFinite(v.Converge);Vertices.Add(v);}
 public void PushTriangle(uint a,uint b,uint c){LegalIndices&=a<Vertices.Count&&b<Vertices.Count&&c<Vertices.Count;Triangles.Add(new(a,b,c));}
 public VGBackend Backend()=>new(){Reserve=Reserve,PushVertex=PushVertex,PushTriangle=PushTriangle};
 public void CheckValid(){Check.That(FiniteVertices);Check.That(LegalIndices);foreach(var t in Triangles)Check.That(t.I0<Vertices.Count&&t.I1<Vertices.Count&&t.I2<Vertices.Count);}
 public void CheckReservedOnce(){Check.Equal(1UL,ReserveCallCount);Check.That(ReservedVertices>=(ulong)Vertices.Count);Check.That(ReservedTriangles>=(ulong)Triangles.Count);}
 public void CheckReservedHintOnce(){Check.Equal(1UL,ReserveCallCount);Check.That(Vertices.Count==0||ReservedVertices>0);Check.That(Triangles.Count==0||ReservedTriangles>0);}
}
public readonly record struct VgMeshTriangle(uint I0,uint I1,uint I2);
public readonly record struct VgFlattenSnapshot(int NodeCount,int ContourCount);
public struct VgMeshBounds { public float MinX,MinY,MaxX,MaxY; }
public sealed class VgFillAllocatorCounter { public ulong AllocCount,ReallocCount,FreeCount; }
public abstract class VgTestHelpers
{
 protected const float kFloatEpsilon=.0001f,kPi=MathF.PI;
 protected static void True(bool v)=>Check.That(v);protected static void True(object? v)=>Check.NotNull(v);
 protected static void False(bool v)=>Check.False(v);protected static void False(object? v)=>Check.Null(v);
 protected static void Eq<T>(T a,T b)=>Check.Equal(a,b);protected static void Eq(int a,uint b)=>Check.Equal((long)a,(long)b);
 protected static void expect_near(float a,float b,float e)=>Check.Near(b,a,e);
 protected static void check_near(float actual,float expected,float tolerance=.001f)=>Check.That(MathF.Abs(actual-expected)<=tolerance);
 protected static void check_offsetf(Offsetf p,float x,float y){Check.Near(x,p.X,kFloatEpsilon);Check.Near(y,p.Y,kFloatEpsilon);}
 protected static void check_rectf(Rectf r,float l,float t,float rr,float b){Check.Near(l,r.Left,kFloatEpsilon);Check.Near(t,r.Top,kFloatEpsilon);Check.Near(rr,r.Right,kFloatEpsilon);Check.Near(b,r.Bottom,kFloatEpsilon);}
 protected static bool flag_any<T>(T value,T flags) where T:Enum=>(Convert.ToUInt64(value)&Convert.ToUInt64(flags))!=0;
 protected static bool flag_all<T>(T value,T flags) where T:Enum=>(Convert.ToUInt64(value)&Convert.ToUInt64(flags))==Convert.ToUInt64(flags);
 protected static void check_contour(VGPathFlattenContour c,uint begin,uint count,float length,bool closed,bool convex=false){Check.Equal(begin,c.NodeBegin);Check.Equal(count,c.NodeCount);Check.Equal(0u,c.BevelCount);expect_near(c.TotalLength,length,kFloatEpsilon);Check.Equal(closed,c.Closed);Check.Equal(convex,c.Convex);}
 protected static void check_node(VGPathFlattenNode n,float x,float y,float distance,float length,EVGPathFlattenNodeFlags flags){check_offsetf(n.Position,x,y);expect_near(n.ContourDistance,distance,kFloatEpsilon);expect_near(n.NextLength,length,kFloatEpsilon);Check.Equal(flags,n.Flags);}
 protected static void check_node_direction(VGPathFlattenNode n,float x,float y,float jx,float jy){check_offsetf(n.NextDirection,x,y);check_offsetf(n.JoinDirection,jx,jy);}
 protected static VgFlattenSnapshot snapshot_flatten(VGPathFlatten flat)=>new(flat.Nodes().Count,flat.Contours().Count);
 protected static void check_flatten_snapshot(VGPathFlatten flat,VgFlattenSnapshot s){Check.Equal(s.NodeCount,flat.Nodes().Count);Check.Equal(s.ContourCount,flat.Contours().Count);}
 protected static VgMeshCapture capture_fill(VGPath path,VGFillOptions options,VGFillWorkspace? workspace=null)=>capture_fill(path.Flatten(new()),options,workspace);
 protected static VgMeshCapture capture_fill(VGPathFlatten flat,VGFillOptions options,VGFillWorkspace? workspace=null){var c=new VgMeshCapture();Check.That(flat.Fill(c.Backend(),options,workspace));c.CheckValid();return c;}
 protected static VgMeshCapture capture_stroke(VGPath path,VGStrokeOptions options)=>capture_stroke(path.Flatten(new()),options);
 protected static VgMeshCapture capture_stroke(VGPathFlatten flat,VGStrokeOptions options){var c=new VgMeshCapture();Check.That(flat.Stroke(c.Backend(),options));c.CheckValid();return c;}
 protected static unsafe VGFillAllocator MakeAllocator(VgFillAllocatorCounter c)=>new(){Alloc=(user,n)=>{c.AllocCount++;return (nint)NativeMemory.Alloc((nuint)n);},Realloc=(user,p,n)=>{c.ReallocCount++;return (nint)NativeMemory.Realloc((void*)p,(nuint)n);},Free=(user,p)=>{c.FreeCount++;NativeMemory.Free((void*)p);}};
 protected static float triangle_edge_value(Offsetf a,Offsetf b,Offsetf p)=>(b-a).Cross(p-a);
 protected static bool is_point_in_triangle(Offsetf p,Offsetf a,Offsetf b,Offsetf c){float ab=triangle_edge_value(a,b,p),bc=triangle_edge_value(b,c,p),ca=triangle_edge_value(c,a,p);bool neg=ab<-.0001f||bc<-.0001f||ca<-.0001f,pos=ab>.0001f||bc>.0001f||ca>.0001f;return !(neg&&pos);}
 protected static bool mesh_covers_point(VgMeshCapture m,Offsetf p){foreach(var t in m.Triangles)if(is_point_in_triangle(p,m.Vertices[(int)t.I0].Pos,m.Vertices[(int)t.I1].Pos,m.Vertices[(int)t.I2].Pos))return true;return false;}
 protected static bool mesh_has_aa_vertex(VgMeshCapture m)=>m.Vertices.Any(v=>v.Converge<.5f);
 protected static bool triangle_has_aa_vertex(VgMeshCapture m,VgMeshTriangle t)=>m.Vertices[(int)t.I0].Converge<.5f||m.Vertices[(int)t.I1].Converge<.5f||m.Vertices[(int)t.I2].Converge<.5f;
 protected static ulong count_body_vertices(VgMeshCapture m)=>(ulong)m.Vertices.Count(v=>v.Converge>=.5f);
 protected static ulong count_aa_vertices_in_rect(VgMeshCapture m,Rectf r)=>(ulong)m.Vertices.Count(v=>v.Converge<.5f&&r.Contains(v.Pos));
 protected static ulong count_aa_vertices_outside_rect(VgMeshCapture m,Rectf r)=>(ulong)m.Vertices.Count(v=>v.Converge<.5f&&!r.Contains(v.Pos));
 protected static VGFillOptions fill_aa_options(float aa_radius=1,float aa_miter_limit=2.4f)=>new(){AaRadius=aa_radius,AaMiterLimit=aa_miter_limit};
 protected static VgMeshBounds calc_bounds(VgMeshCapture c){Check.That(c.Vertices.Count>0);var b=new VgMeshBounds{MinX=c.Vertices[0].Pos.X,MinY=c.Vertices[0].Pos.Y,MaxX=c.Vertices[0].Pos.X,MaxY=c.Vertices[0].Pos.Y};foreach(var v in c.Vertices){b.MinX=v.Pos.X<b.MinX?v.Pos.X:b.MinX;b.MinY=v.Pos.Y<b.MinY?v.Pos.Y:b.MinY;b.MaxX=v.Pos.X>b.MaxX?v.Pos.X:b.MaxX;b.MaxY=v.Pos.Y>b.MaxY?v.Pos.Y:b.MaxY;}return b;}
}
