using SkrGui;
namespace SkrGui.Tests;
public static class VgGeometryTests
{
 private sealed class Capture
 {
  public List<VGVertex> Vertices=[]; public List<(uint A,uint B,uint C)> Triangles=[]; public int ReserveCalls;
  public VGBackend Backend => new(){Reserve=(v,t)=>ReserveCalls++,PushVertex=v=>Vertices.Add(v),PushTriangle=(a,b,c)=>Triangles.Add((a,b,c))};
  public bool Covers(Offsetf p) => Triangles.Any(t=>Inside(Vertices[(int)t.A].Pos,Vertices[(int)t.B].Pos,Vertices[(int)t.C].Pos,p));
  static bool Inside(Offsetf a,Offsetf b,Offsetf c,Offsetf p){float ab=(b-a).Cross(p-a),bc=(c-b).Cross(p-b),ca=(a-c).Cross(p-c);return (ab>=0&&bc>=0&&ca>=0)||(ab<=0&&bc<=0&&ca<=0);}
 }
 [GuiTest("extra/smoke/VgGeometryTests/1")]
 public static unsafe void NativeHole()
 {
  var path=new VGPath();path.AddRect(Rectf.LTWH(0,0,20,20),EVGPathWinding.CW);path.AddRect(Rectf.LTWH(6,6,8,8),EVGPathWinding.CCW);
  int alloc=0,free=0;var source=VGFillAllocator.Default();var ws=new VGFillWorkspace{Allocator=new(){Alloc=(user,n)=>{alloc++;return source.Alloc!(user,n);},Realloc=source.Realloc,Free=(user,p)=>{free++;source.Free!(user,p);}}};
  var result=new Capture();Check.That(path.Flatten(new()).Fill(result.Backend,new(){FillRule=EVGFillRule.NonZero,OptimizeForSingleConvex=true},ws));
  Check.That(alloc>0);Check.That(free>0);Check.That(result.Covers(new(3,10)));Check.False(result.Covers(new(10,10)));
 }
 [GuiTest("extra/smoke/VgGeometryTests/2")]
 public static void ConvexAA()
 {
  var path=new VGPath();path.AddRect(Rectf.LTWH(0,0,12,8),EVGPathWinding.CW);int alloc=0;var source=VGFillAllocator.Default();var ws=new VGFillWorkspace{Allocator=new(){Alloc=(user,n)=>{alloc++;return source.Alloc!(user,n);},Realloc=source.Realloc,Free=source.Free}};
  var result=new Capture();Check.That(path.Flatten(new()).Fill(result.Backend,new(){AaRadius=1,AaMiterLimit=100,OptimizeForSingleConvex=true},ws));
  Check.Equal(0,alloc);Check.Equal(8,result.Vertices.Count);Check.Equal(10,result.Triangles.Count);Check.Equal(0,ws.VertexLookup.Count);Check.Equal(0,ws.ScratchContourVertices.Count);Check.That(result.Covers(new(6,4)));
 }
 [GuiTest("extra/smoke/VgGeometryTests/3")]
 public static void StrokeBodyAA()
 {
  var path=new VGPath();path.MoveTo(new(0,0));path.LineTo(new(100,0));var flat=path.Flatten(new());
  foreach(var (aa,ratio,nv,nt,x0,y0,x1,y1) in new[]{(0f,1f,4,2,0f,-5f,100f,5f),(2f,1f,8,10,-2f,-7f,102f,7f),(4f,2f,8,10,-2f,-7f,102f,7f)})
  {var r=new Capture();Check.That(flat.Stroke(r.Backend,new(){Width=10,Cap=EVGStrokeCap.Butt,Join=EVGStrokeJoin.Miter,AaRadius=aa,PixelRatio=ratio}));Check.Equal(nv,r.Vertices.Count);Check.Equal(nt,r.Triangles.Count);Check.Equal(1,r.ReserveCalls);Check.Near(x0,r.Vertices.Min(v=>v.Pos.X));Check.Near(y0,r.Vertices.Min(v=>v.Pos.Y));Check.Near(x1,r.Vertices.Max(v=>v.Pos.X));Check.Near(y1,r.Vertices.Max(v=>v.Pos.Y));}
 }
 [GuiTest("extra/smoke/VgGeometryTests/4")]
 public static void DashDistances()
 {
  var p=new VGPath();p.MoveTo(new(0,0));p.LineTo(new(10,0));var result=new VGPathFlatten();Check.That(p.Flatten(new()).DashTo(result,new(){Values=new float[]{3,2}}));
  Check.Equal(2,result.Contours().Count);Check.SequenceEqual(new float[]{0,3,5,8},result.Nodes().Select(n=>n.Position.X));Check.SequenceEqual(new float[]{0,3,0,3},result.Nodes().Select(n=>n.ContourDistance));
  Check.SequenceEqual(new float[]{3,0,3,0},result.Nodes().Select(n=>n.NextLength));
 }
}
