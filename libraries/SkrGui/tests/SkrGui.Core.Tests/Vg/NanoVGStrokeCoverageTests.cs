using System.Runtime.CompilerServices;
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
 private static double cross(PointD a, PointD b, PointD c)
{
    return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
}
 private static double distance_to_segment(PointD p, PointD a, PointD b)
{
    double vx = b.X - a.X;
    double vy = b.Y - a.Y;
    double len_sq = vx * vx + vy * vy;
    if (len_sq <= 0.0)
    {
        double dx_zero = p.X - a.X;
        double dy_zero = p.Y - a.Y;
        return Math.Sqrt(dx_zero * dx_zero + dy_zero * dy_zero);
    }

    double t = Math.Max(0.0, Math.Min(1.0, ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len_sq));
    double cx = a.X + vx * t;
    double cy = a.Y + vy * t;
    double dx = p.X - cx;
    double dy = p.Y - cy;
    return Math.Sqrt(dx * dx + dy * dy);
}
 private static bool is_degenerate_triangle(PointD a, PointD b, PointD c)
{
    return Math.Abs(cross(a, b, c)) <= 0.000000001;
}
 private static bool is_point_inside_triangle(PointD p, PointD a, PointD b, PointD c)
{
    double d0 = cross(a, b, p);
    double d1 = cross(b, c, p);
    double d2 = cross(c, a, p);
    return (d0 >= 0.0 && d1 >= 0.0 && d2 >= 0.0) ||
        (d0 <= 0.0 && d1 <= 0.0 && d2 <= 0.0);
}
 private static bool is_point_near_triangle_edge(PointD p, PointD a, PointD b, PointD c, double epsilon)
{
    return distance_to_segment(p, a, b) <= epsilon ||
        distance_to_segment(p, b, c) <= epsilon ||
        distance_to_segment(p, c, a) <= epsilon;
}
 private static void add_mesh_triangle(TriangleMesh mesh, PointD a, PointD b, PointD c)
{
    uint vertexBase = (uint)(mesh.Vertices.Count);
    mesh.Vertices.Add(a);
    mesh.Vertices.Add(b);
    mesh.Vertices.Add(c);
    mesh.Triangles.Add(new Triangle(vertexBase, vertexBase + 1u, vertexBase + 2u));
}
 private static void extend_bounds(MeshBounds bounds, PointD p)
{
    if (!bounds.Valid)
    {
        bounds.MinX = p.X;
        bounds.MinY = p.Y;
        bounds.MaxX = p.X;
        bounds.MaxY = p.Y;
        bounds.Valid = true;
        return;
    }

    bounds.MinX = Math.Min(bounds.MinX, p.X);
    bounds.MinY = Math.Min(bounds.MinY, p.Y);
    bounds.MaxX = Math.Max(bounds.MaxX, p.X);
    bounds.MaxY = Math.Max(bounds.MaxY, p.Y);
}
 private static MeshBounds calc_mesh_bounds(TriangleMesh mesh)
{
    MeshBounds bounds = new();
    foreach (PointD vertex in mesh.Vertices)
    {
        extend_bounds(bounds, vertex);
    }
    return bounds;
}
 private static MeshBounds calc_union_bounds(TriangleMesh a, TriangleMesh b)
{
    MeshBounds bounds = calc_mesh_bounds(a);
    MeshBounds b_bounds = calc_mesh_bounds(b);
    if (b_bounds.Valid)
    {
        extend_bounds(bounds, new PointD(b_bounds.MinX, b_bounds.MinY));
        extend_bounds(bounds, new PointD(b_bounds.MaxX, b_bounds.MaxY));
    }
    return bounds;
}
 private static ESampleState sample_mesh(TriangleMesh mesh, PointD p, double boundary_epsilon)
{
    bool near_edge = false;
    foreach (Triangle triangle in mesh.Triangles)
    {
        PointD a = mesh.Vertices[(int)(triangle.I0)];
        PointD b = mesh.Vertices[(int)(triangle.I1)];
        PointD c = mesh.Vertices[(int)(triangle.I2)];
        if (is_degenerate_triangle(a, b, c))
        {
            continue;
        }

        bool inside = is_point_inside_triangle(p, a, b, c);
        bool near = is_point_near_triangle_edge(p, a, b, c, boundary_epsilon);
        near_edge |= near;
        if (inside && near)
        {
            return ESampleState.Ambiguous;
        }
        if (inside)
        {
            return ESampleState.Inside;
        }
    }
    return near_edge ? ESampleState.Ambiguous : ESampleState.Outside;
}
 private static CoverageStats compare_coverage(
    TriangleMesh vg_mesh,
    TriangleMesh nanovg_mesh,
    StrokeCoverageCase test_case
)
{
    CoverageStats stats = new();
    MeshBounds bounds = calc_union_bounds(vg_mesh, nanovg_mesh);
    if (!bounds.Valid)
    {
        return stats;
    }

    double padding = Math.Max(test_case.RequestedStep, test_case.BoundaryEpsilon * 2.0);
    bounds.MinX -= padding;
    bounds.MinY -= padding;
    bounds.MaxX += padding;
    bounds.MaxY += padding;

    double width = Math.Max(bounds.MaxX - bounds.MinX, test_case.RequestedStep);
    double height = Math.Max(bounds.MaxY - bounds.MinY, test_case.RequestedStep);
    double max_extent = Math.Max(width, height);
    stats.Step = test_case.RequestedStep;
    if (test_case.MaxAxisSamples > 0u)
    {
        stats.Step = Math.Max(stats.Step, max_extent / (double)(test_case.MaxAxisSamples));
    }

    uint x_count = Math.Max(1u, (uint)(Math.Ceiling(width / stats.Step)));
    uint y_count = Math.Max(1u, (uint)(Math.Ceiling(height / stats.Step)));
    for (uint y = 0u; y < y_count; ++y)
    {
        for (uint x = 0u; x < x_count; ++x)
        {
            PointD sample = new(bounds.MinX + ((double)(x) + 0.5) * stats.Step,
                bounds.MinY + ((double)(y) + 0.5) * stats.Step);

            ESampleState vg_state = sample_mesh(vg_mesh, sample, test_case.BoundaryEpsilon);
            ESampleState nanovg_state = sample_mesh(nanovg_mesh, sample, test_case.BoundaryEpsilon);
            if (vg_state == ESampleState.Ambiguous || nanovg_state == ESampleState.Ambiguous)
            {
                ++stats.AmbiguousCount;
                continue;
            }

            ++stats.StrictSampleCount;
            bool vg_inside = vg_state == ESampleState.Inside;
            bool nanovg_inside = nanovg_state == ESampleState.Inside;
            if (vg_inside)
            {
                ++stats.VgInsideCount;
            }
            if (nanovg_inside)
            {
                ++stats.NanovgInsideCount;
            }
            if (vg_inside || nanovg_inside)
            {
                ++stats.UnionCount;
            }
            if (vg_inside != nanovg_inside)
            {
                ++stats.XorCount;
            }
        }
    }

    if (stats.UnionCount > 0u)
    {
        stats.XorRatio = (double)(stats.XorCount) / (double)(stats.UnionCount);
        ulong area_delta = stats.VgInsideCount > stats.NanovgInsideCount ?
            stats.VgInsideCount - stats.NanovgInsideCount :
            stats.NanovgInsideCount - stats.VgInsideCount;
        stats.AreaRatio = (double)(area_delta) / (double)(stats.UnionCount);
    }
    return stats;
}
 private static void build_vg_path(VGPath path, EPathShape shape)
{
    switch (shape)
    {
    case EPathShape.HorizontalLine:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(96.0f, 0.0f));
        break;
    case EPathShape.DiagonalLine:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(78.0f, 54.0f));
        break;
    case EPathShape.LeftTurnPolyline:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(60.0f, 0.0f));
        path.LineTo(new Offsetf(60.0f, 44.0f));
        break;
    case EPathShape.RightTurnPolyline:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(60.0f, 0.0f));
        path.LineTo(new Offsetf(60.0f, -44.0f));
        break;
    case EPathShape.ClosedRectangle:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(86.0f, 0.0f));
        path.LineTo(new Offsetf(86.0f, 46.0f));
        path.LineTo(new Offsetf(0.0f, 46.0f));
        path.Close();
        break;
    case EPathShape.ClosedTriangle:
        path.MoveTo(new Offsetf(4.0f, 52.0f));
        path.LineTo(new Offsetf(48.0f, 0.0f));
        path.LineTo(new Offsetf(94.0f, 48.0f));
        path.Close();
        break;
    case EPathShape.AcuteMiterFallback:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(64.0f, 0.0f));
        path.LineTo(new Offsetf(67.0f, 42.0f));
        break;
    case EPathShape.NearCollinearJoin:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(54.0f, 0.0f));
        path.LineTo(new Offsetf(108.0f, 1.4f));
        break;
    case EPathShape.MultipleOpenContours:
        path.MoveTo(new Offsetf(0.0f, 0.0f));
        path.LineTo(new Offsetf(48.0f, 0.0f));
        path.MoveTo(new Offsetf(12.0f, 28.0f));
        path.LineTo(new Offsetf(70.0f, 42.0f));
        break;
    }
}
 private static void build_nanovg_path(nint ctx, EPathShape shape)
{
    nvgBeginPath(ctx);
    switch (shape)
    {
    case EPathShape.HorizontalLine:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 96.0f, 0.0f);
        break;
    case EPathShape.DiagonalLine:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 78.0f, 54.0f);
        break;
    case EPathShape.LeftTurnPolyline:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 60.0f, 0.0f);
        nvgLineTo(ctx, 60.0f, 44.0f);
        break;
    case EPathShape.RightTurnPolyline:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 60.0f, 0.0f);
        nvgLineTo(ctx, 60.0f, -44.0f);
        break;
    case EPathShape.ClosedRectangle:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 86.0f, 0.0f);
        nvgLineTo(ctx, 86.0f, 46.0f);
        nvgLineTo(ctx, 0.0f, 46.0f);
        nvgClosePath(ctx);
        break;
    case EPathShape.ClosedTriangle:
        nvgMoveTo(ctx, 4.0f, 52.0f);
        nvgLineTo(ctx, 48.0f, 0.0f);
        nvgLineTo(ctx, 94.0f, 48.0f);
        nvgClosePath(ctx);
        break;
    case EPathShape.AcuteMiterFallback:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 64.0f, 0.0f);
        nvgLineTo(ctx, 67.0f, 42.0f);
        break;
    case EPathShape.NearCollinearJoin:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 54.0f, 0.0f);
        nvgLineTo(ctx, 108.0f, 1.4f);
        break;
    case EPathShape.MultipleOpenContours:
        nvgMoveTo(ctx, 0.0f, 0.0f);
        nvgLineTo(ctx, 48.0f, 0.0f);
        nvgMoveTo(ctx, 12.0f, 28.0f);
        nvgLineTo(ctx, 70.0f, 42.0f);
        break;
    }
}
 [GuiTest("gui/vg/VGPathFlatten stroke no-AA coverage matches NanoVG cap cases")] public static void Case1()
{
    StrokeCoverageCase[] cases = {
        new("horizontal butt cap", EPathShape.HorizontalLine, 8.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter),
        new("horizontal square cap", EPathShape.HorizontalLine, 8.0f, EVGStrokeCap.Square, EVGStrokeJoin.Miter),
        new("horizontal round cap", EPathShape.HorizontalLine, 8.0f, EVGStrokeCap.Round, EVGStrokeJoin.Miter, 4.0f, 2.0f, 0.45, 180u, 0.04, 0.012, 0.006),
        new("diagonal round cap", EPathShape.DiagonalLine, 8.0f, EVGStrokeCap.Round, EVGStrokeJoin.Miter, 4.0f, 2.0f, 0.45, 180u, 0.04, 0.012, 0.006),
        new("thin stroke", EPathShape.DiagonalLine, 1.25f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter, 4.0f, 1.0f, 0.18, 180u, 0.018, 0.012, 0.008),
        new("wide stroke", EPathShape.ClosedRectangle, 22.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter, 4.0f, 1.0f, 0.65, 180u, 0.05, 0.006, 0.004),
    };

    run_stroke_coverage_cases(cases);
}
 [GuiTest("gui/vg/VGPathFlatten stroke no-AA coverage matches NanoVG join cases")] public static void Case2()
{
    StrokeCoverageCase[] cases = {
        new("left turn miter join", EPathShape.LeftTurnPolyline, 10.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter),
        new("left turn bevel join", EPathShape.LeftTurnPolyline, 10.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Bevel),
        new("left turn round join", EPathShape.LeftTurnPolyline, 10.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Round, 4.0f, 2.0f, 0.45, 180u, 0.04, 0.012, 0.006),
        new("right turn miter join", EPathShape.RightTurnPolyline, 10.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter),
        new("right turn bevel join", EPathShape.RightTurnPolyline, 10.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Bevel),
        new("right turn round join", EPathShape.RightTurnPolyline, 10.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Round, 4.0f, 2.0f, 0.45, 180u, 0.04, 0.012, 0.006),
        new("acute miter fallback", EPathShape.AcuteMiterFallback, 14.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter, 1.2f, 1.0f, 0.45, 180u, 0.04, 0.014, 0.008),
        new("near collinear join", EPathShape.NearCollinearJoin, 8.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter, 4.0f, 1.0f, 0.45, 180u, 0.04, 0.006, 0.004),
    };

    run_stroke_coverage_cases(cases);
}
 [GuiTest("gui/vg/VGPathFlatten stroke no-AA coverage matches NanoVG contour cases")] public static void Case3()
{
    StrokeCoverageCase[] cases = {
        new("closed rectangle", EPathShape.ClosedRectangle, 8.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter),
        new("closed triangle", EPathShape.ClosedTriangle, 8.0f, EVGStrokeCap.Butt, EVGStrokeJoin.Miter),
        new("multiple open contours", EPathShape.MultipleOpenContours, 7.0f, EVGStrokeCap.Square, EVGStrokeJoin.Miter, 4.0f, 1.0f, 0.45, 180u, 0.04, 0.006, 0.004),
    };

    run_stroke_coverage_cases(cases);
}
}
