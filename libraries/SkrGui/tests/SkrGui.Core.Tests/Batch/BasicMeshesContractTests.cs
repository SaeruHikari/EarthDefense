namespace SkrGui.Tests;
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

 [GuiTest("gui/batch/mesh-builder maps vertex attributes")]
 public static void SourceCase01()
{
    Mesh mesh = new();
    Rectf bounds = Rectf.LTWH(10.0f, 20.0f, 100.0f, 50.0f);
    Rectf uv_rect = Rectf.LTWH(0.25f, 0.50f, 0.50f, 0.25f);
    ColorGradient gradient = new(
        new ColorGradientAxis(
            new SRGBColor(0.0f, 0.0f, 0.0f, 1.0f),
            new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)
        )
    );
    MeshBuilder builder = new(mesh, bounds, uv_rect, gradient);

    uint left = builder.PushVertex(bounds.TopLeft());
    uint right = builder.PushVertex(bounds.TopRight(), 0.5f);
    builder.PushLocalTriangle(0u, 1u, 0u);

    Eq(builder.VertexStart(), 0u);
    Eq(builder.IndexStart(), 0u);
    Eq(mesh.Vertices.Count, 2u);
    Eq(mesh.Indices.Count, 3u);
    Eq(mesh.Indices[0], left);
    Eq(mesh.Indices[1], right);
    check_offsetf(mesh.Vertices[(int)left].Uv, 0.25f, 0.50f);
    check_offsetf(mesh.Vertices[(int)right].Uv, 0.75f, 0.50f);
    check_color(unpack_color(mesh.Vertices[(int)left].PackedColor), 0.0f, 0.0f, 0.0f, 1.0f);
    check_color(unpack_color(mesh.Vertices[(int)right].PackedColor), 128.0f / 255.0f, 128.0f / 255.0f, 128.0f / 255.0f, 128.0f / 255.0f);

    builder.RefreshStart();
    Eq(builder.VertexStart(), 2u);
    Eq(builder.IndexStart(), 3u);

    builder.SetUvRect(null);
    builder.SetGradient(null);
    builder.SetPremultiplyColor(false);
    uint straight = builder.PushVertex(new Offsetf(35.0f, 35.0f), new SRGBColor(1.0f, 0.5f, 0.25f, 0.5f));
    check_color(unpack_color(mesh.Vertices[(int)straight].PackedColor), 1.0f, 128.0f / 255.0f, 64.0f / 255.0f, 128.0f / 255.0f);

    builder.SetPremultiplyColor(true);
    uint fallback = builder.PushVertex(new Offsetf(40.0f, 40.0f));
    builder.PushTriangle(fallback, fallback, fallback);
    check_offsetf(mesh.Vertices[(int)fallback].Uv, 0.0f, 0.0f);
    check_color(unpack_color(mesh.Vertices[(int)fallback].PackedColor), 1.0f, 1.0f, 1.0f, 1.0f);

    builder.RollbackToStart();
    Eq(mesh.Vertices.Count, 2u);
    Eq(mesh.Indices.Count, 3u);
}

 [GuiTest("gui/batch/basic-meshes text builds rect span mesh")]
 public static void SourceCase02()
{
    TextRenderRect[] rects = {
        new TextRenderRect { SpanId = new TextLayoutSpanId(1u), Rect = Rectf.LTWH(10.0f, 20.0f, 4.0f, 8.0f), Uv = Rectf.LTWH(0.10f, 0.20f, 0.30f, 0.40f),
        },
        new TextRenderRect { SpanId = new TextLayoutSpanId(2u), Rect = Rectf.LTWH(50.0f, 60.0f, 7.0f, 9.0f), Uv = Rectf.LTWH(0.00f, 0.25f, 0.25f, 0.50f),
        },
    };
    SRGBColor color = new(1.0f, 0.0f, 0.0f, 0.50f);

    Mesh mesh = new();
    Check.That(BasicMeshes.Text(mesh, rects, color));
    Eq(mesh.Vertices.Count, 8u);
    Eq(mesh.Indices.Count, 12u);

    check_offsetf(mesh.Vertices[0].Pos, 10.0f, 20.0f);
    check_offsetf(mesh.Vertices[1].Pos, 14.0f, 20.0f);
    check_offsetf(mesh.Vertices[2].Pos, 14.0f, 28.0f);
    check_offsetf(mesh.Vertices[3].Pos, 10.0f, 28.0f);
    check_offsetf(mesh.Vertices[0].Uv, 0.10f, 0.20f);
    check_offsetf(mesh.Vertices[2].Uv, 0.40f, 0.60f);
    check_color(unpack_color(mesh.Vertices[0].PackedColor), 128.0f / 255.0f, 0.0f, 0.0f, 128.0f / 255.0f);

    check_offsetf(mesh.Vertices[4].Pos, 50.0f, 60.0f);
    check_offsetf(mesh.Vertices[6].Pos, 57.0f, 69.0f);
    check_offsetf(mesh.Vertices[4].Uv, 0.00f, 0.25f);
    check_offsetf(mesh.Vertices[6].Uv, 0.25f, 0.75f);
    Eq(mesh.Indices[0], 0u);
    Eq(mesh.Indices[5], 3u);
    Eq(mesh.Indices[6], 4u);
    Eq(mesh.Indices[11], 7u);

    int vertex_count = mesh.Vertices.Count;
    int index_count = mesh.Indices.Count;
    Check.False(BasicMeshes.Text(mesh, Array.Empty<TextRenderRect>(), color));
    Eq(mesh.Vertices.Count, vertex_count);
    Eq(mesh.Indices.Count, index_count);
}

 [GuiTest("gui/batch/basic-meshes text emits every rect in span")]
 public static void SourceCase03()
{
    Mesh mesh = new();
    TextRenderRect[] rects = {
        new TextRenderRect { SpanId = new TextLayoutSpanId(1u), Rect = Rectf.Zero(), Uv = Rectf.Zero(),
        },
    };

    Check.That(BasicMeshes.Text(mesh, rects, new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)));
    Eq(mesh.Vertices.Count, 4u);
    Eq(mesh.Indices.Count, 6u);
    check_offsetf(mesh.Vertices[0].Pos, 0.0f, 0.0f);
    check_offsetf(mesh.Vertices[2].Pos, 0.0f, 0.0f);
}

 [GuiTest("gui/batch/basic-meshes premultiply color option")]
 public static void SourceCase04()
{
    SRGBColor color = new(1.0f, 0.5f, 0.25f, 0.5f);
    ColorGradient gradient = new ColorGradientSolid(color);
    Rectf rect = Rectf.LTWH(0.0f, 0.0f, 8.0f, 8.0f);

    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;

    Mesh premultiplied = new();
    Check.That(BasicMeshes.RectFill(premultiplied, rect, gradient, options));
    check_color(
        unpack_color(premultiplied.Vertices[0].PackedColor),
        128.0f / 255.0f,
        64.0f / 255.0f,
        32.0f / 255.0f,
        128.0f / 255.0f
    );

    options.PremultiplyColor = false;

    Mesh straight = new();
    Check.That(BasicMeshes.RectFill(straight, rect, gradient, options));
    check_color(
        unpack_color(straight.Vertices[0].PackedColor),
        1.0f,
        128.0f / 255.0f,
        64.0f / 255.0f,
        128.0f / 255.0f
    );
}

 [GuiTest("gui/batch/basic-meshes rect fill axis gradient")]
 public static void SourceCase05()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;
    options.UvRect = Rectf.LTWH(0.0f, 0.0f, 1.0f, 1.0f);

    bool emitted = BasicMeshes.RectFill(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 20.0f),
        new ColorGradientAxis(new SRGBColor(0.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 4u);
    Eq(mesh.Indices.Count, 6u);
    check_offsetf(mesh.Vertices[0].Pos, 0.0f, 0.0f);
    check_offsetf(mesh.Vertices[1].Pos, 10.0f, 0.0f);
    check_offsetf(mesh.Vertices[2].Pos, 10.0f, 20.0f);
    check_offsetf(mesh.Vertices[3].Pos, 0.0f, 20.0f);
    check_offsetf(mesh.Vertices[0].Uv, 0.0f, 0.0f);
    check_offsetf(mesh.Vertices[2].Uv, 1.0f, 1.0f);
    check_color(unpack_color(mesh.Vertices[0].PackedColor), 0.0f, 0.0f, 0.0f, 1.0f);
    check_color(unpack_color(mesh.Vertices[1].PackedColor), 1.0f, 1.0f, 1.0f, 1.0f);
}

 [GuiTest("gui/batch/basic-meshes rect stroke axis gradient uses vg fast path")]
 public static void SourceCase06()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);

    bool emitted = BasicMeshes.RectStroke(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        2.0f,
        new ColorGradientAxis(red, blue),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 8u);
    Eq(mesh.Indices.Count, 24u);
    Check.That(has_vertex_at(mesh, -1.0f, -1.0f));
    Check.That(has_vertex_at(mesh, 11.0f, -1.0f));
    Check.That(has_vertex_color_at(mesh, -1.0f, -1.0f, red));
    Check.That(has_vertex_color_at(mesh, 11.0f, -1.0f, blue));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rect fill colored corners")]
 public static void SourceCase07()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;

    bool emitted = BasicMeshes.RectFillColored(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 8.0f, 8.0f),
        new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f),
        new SRGBColor(0.0f, 1.0f, 0.0f, 1.0f),
        new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f),
        new SRGBColor(1.0f, 1.0f, 0.0f, 1.0f),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 4u);
    Eq(mesh.Indices.Count, 6u);
    check_color(unpack_color(mesh.Vertices[0].PackedColor), 1.0f, 0.0f, 0.0f, 1.0f);
    check_color(unpack_color(mesh.Vertices[1].PackedColor), 0.0f, 1.0f, 0.0f, 1.0f);
    check_color(unpack_color(mesh.Vertices[2].PackedColor), 0.0f, 0.0f, 1.0f, 1.0f);
    check_color(unpack_color(mesh.Vertices[3].PackedColor), 1.0f, 1.0f, 0.0f, 1.0f);
}

 [GuiTest("gui/batch/basic-meshes ninepatch emits inset grid")]
 public static void SourceCase08()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;
    options.UvRect = new Rectf(0.0f, 0.0f, 1.0f, 1.0f);

    bool emitted = BasicMeshes.Ninepatch(
        mesh,
        Rectf.LTWH(10.0f, 20.0f, 100.0f, 80.0f),
        EdgeInsets.FromLTRB(10.0f, 20.0f, 30.0f, 15.0f),
        EdgeInsets.FromLTRB(0.10f, 0.20f, 0.30f, 0.15f),
        new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 16u);
    Eq(mesh.Indices.Count, 54u);
    Check.That(has_vertex_uv_at(mesh, 10.0f, 20.0f, 0.0f, 0.0f));
    Check.That(has_vertex_uv_at(mesh, 20.0f, 40.0f, 0.10f, 0.20f));
    Check.That(has_vertex_uv_at(mesh, 80.0f, 85.0f, 0.70f, 0.85f));
    Check.That(has_vertex_uv_at(mesh, 110.0f, 100.0f, 1.0f, 1.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes ninepatch rejects invalid inset")]
 public static void SourceCase09()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.UvRect = new Rectf(0.0f, 0.0f, 1.0f, 1.0f);

    bool emitted = BasicMeshes.Ninepatch(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f),
        EdgeInsets.FromLTRB(12.0f, 4.0f, 12.0f, 4.0f),
        EdgeInsets.All(0.25f),
        new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f),
        options
    );
    Check.False(emitted);
    Check.That((mesh.Vertices.Count == 0));
    Check.That((mesh.Indices.Count == 0));
}

 [GuiTest("gui/batch/basic-meshes rrect fill uses delaunay when curve split is required")]
 public static void SourceCase10()
{
    Mesh mesh = new();
    VGFillWorkspace workspace = new();
    BasicMeshOptions options = new();
    options.ReuseFillWorkspace = workspace;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, new SRGBColor(0.0f, 1.0f, 0.0f, 1.0f)),
        new ColorGradientStop(1.0f, blue),
    };

    bool emitted = BasicMeshes.RRectFill(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 80.0f, 48.0f), 14.0f, 14.0f),
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 40.0f, 0.0f));
    Check.That(has_vertex_at(mesh, 40.0f, 48.0f));
    Check.That(is_mesh_partitioned_at_x(mesh, 40.0f));
    Check.False((workspace.ScratchContourVertices.Count == 0));
    Check.False((workspace.VertexLookup.Count == 0));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rrect stroke uses vg path")]
 public static void SourceCase11()
{
    Mesh mesh = new();

    bool emitted = BasicMeshes.RRectStroke(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 64.0f, 40.0f), 10.0f, 10.0f),
        6.0f,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes diff rrect uses vg path")]
 public static void SourceCase12()
{
    Mesh mesh = new();

    bool emitted = BasicMeshes.DiffRRect(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 80.0f, 56.0f), 16.0f, 16.0f),
        RRect.FromRectXY(Rectf.LTWH(18.0f, 14.0f, 44.0f, 28.0f), 8.0f, 8.0f),
        new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/rrect/square-corners-use-rect-primitives")]
 public static void SourceCase13()
{
    Rectf outer_rect = Rectf.LTWH(0.0f, 0.0f, 96.0f, 64.0f);
    Rectf inner_rect = Rectf.LTWH(12.0f, 10.0f, 68.0f, 40.0f);
    RRect outer_rrect = RRect.FromRectXY(outer_rect, 0.0f, 0.0f);
    RRect inner_rrect = RRect.FromRectXY(inner_rect, 0.0f, 0.0f);
    ColorGradient gradient = new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f));

    Mesh rrect_fill_mesh = new();
    Mesh rect_fill_mesh = new();
    Check.That(BasicMeshes.RRectFill(rrect_fill_mesh, outer_rrect, gradient, new BasicMeshOptions()));
    Check.That(BasicMeshes.RectFill(rect_fill_mesh, outer_rect, gradient, new BasicMeshOptions()));
    check_mesh_equal(rrect_fill_mesh, rect_fill_mesh);

    Mesh rrect_stroke_mesh = new();
    Mesh rect_stroke_mesh = new();
    Check.That(BasicMeshes.RRectStroke(rrect_stroke_mesh, outer_rrect, 6.0f, gradient, new BasicMeshOptions()));
    Check.That(BasicMeshes.RectStroke(rect_stroke_mesh, outer_rect, 6.0f, gradient, new BasicMeshOptions()));
    check_mesh_equal(rrect_stroke_mesh, rect_stroke_mesh);

    Mesh diff_rrect_mesh = new();
    Mesh diff_rect_mesh = new();
    Check.That(BasicMeshes.DiffRRect(diff_rrect_mesh, outer_rrect, inner_rrect, gradient, new BasicMeshOptions()));
    Check.That(BasicMeshes.DiffRect(diff_rect_mesh, outer_rect, inner_rect, gradient, new BasicMeshOptions()));
    check_mesh_equal(diff_rrect_mesh, diff_rect_mesh);
}

 [GuiTest("gui/batch/basic-meshes fill workspace can be reused")]
 public static void SourceCase14()
{
    Mesh first_mesh = new();
    Mesh second_mesh = new();
    VGFillWorkspace workspace = new();
    BasicMeshOptions options = new();
    options.AaRadius = 1.0f;
    options.ReuseFillWorkspace = workspace;

    RRect outer = RRect.FromRectXY(
        Rectf.LTWH(0.0f, 0.0f, 80.0f, 56.0f),
        16.0f,
        16.0f
    );
    RRect inner = RRect.FromRectXY(
        Rectf.LTWH(18.0f, 14.0f, 44.0f, 28.0f),
        8.0f,
        8.0f
    );
    ColorGradientSolid gradient = new(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f));

    bool first_emitted = BasicMeshes.DiffRRect(
        first_mesh,
        outer,
        inner,
        gradient,
        options
    );
    bool second_emitted = BasicMeshes.DiffRRect(
        second_mesh,
        outer,
        inner,
        gradient,
        options
    );

    Check.That(first_emitted);
    Check.That(second_emitted);
    check_mesh_equal(first_mesh, second_mesh);
    Check.False((workspace.ScratchContourVertices.Count == 0));
    Check.False((workspace.VertexLookup.Count == 0));
}

 [GuiTest("gui/batch/basic-meshes rrect border uses diff path when all insets are nonzero")]
 public static void SourceCase15()
{
    Mesh mesh = new();

    bool emitted = BasicMeshes.RRectBorder(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 80.0f, 56.0f), 16.0f, 16.0f),
        EdgeInsets.FromLTRB(18.0f, 14.0f, 18.0f, 14.0f),
        new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rrect border skips zero inset side")]
 public static void SourceCase16()
{
    Mesh mesh = new();

    bool emitted = BasicMeshes.RRectBorder(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 80.0f, 56.0f), 16.0f, 16.0f),
        EdgeInsets.FromLTRB(12.0f, 0.0f, 14.0f, 10.0f),
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_vertex_at(mesh, 40.0f, 0.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rrect border handles opposite zero inset sides")]
 public static void SourceCase17()
{
    Mesh mesh = new();

    bool emitted = BasicMeshes.RRectBorder(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 96.0f, 64.0f), 18.0f, 18.0f),
        EdgeInsets.FromLTRB(0.0f, 8.0f, 0.0f, 8.0f),
        new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_vertex_at(mesh, 0.0f, 32.0f));
    Check.False(has_vertex_at(mesh, 96.0f, 32.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rrect border handles single inset side")]
 public static void SourceCase18()
{
    Mesh mesh = new();

    bool emitted = BasicMeshes.RRectBorder(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 96.0f, 64.0f), 18.0f, 18.0f),
        EdgeInsets.Only(8.0f, 0.0f, 0.0f, 0.0f),
        new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 12u);
    Check.False(has_vertex_at(mesh, 48.0f, 0.0f));
    Check.False(has_vertex_at(mesh, 48.0f, 64.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rrect border rejects zero inset")]
 public static void SourceCase19()
{
    Mesh mesh = new();

    bool emitted = BasicMeshes.RRectBorder(
        mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 80.0f, 56.0f), 16.0f, 16.0f),
        EdgeInsets.Zero(),
        new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.False(emitted);

    Check.That((mesh.Vertices.Count == 0));
    Check.That((mesh.Indices.Count == 0));
}

 [GuiTest("gui/batch/rrect-border/square-corners-use-diff-rect")]
 public static void SourceCase20()
{
    Rectf outer = Rectf.LTWH(0.0f, 0.0f, 96.0f, 64.0f);
    RRect square_rrect = RRect.FromRectXY(outer, 0.0f, 0.0f);
    ColorGradient gradient = new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f));
    EdgeInsets[] insets = {
        EdgeInsets.FromLTRB(8.0f, 6.0f, 10.0f, 12.0f),
        EdgeInsets.Only(8.0f, 0.0f, 0.0f, 0.0f),
    };

    foreach (EdgeInsets inset in insets)
    {
        Mesh border_mesh = new();
        Mesh diff_mesh = new();

        Check.That(BasicMeshes.RRectBorder(border_mesh, square_rrect, inset, gradient, new BasicMeshOptions()));
        Check.That(BasicMeshes.DiffRect(diff_mesh, outer, inset.DeflateRect(outer), gradient, new BasicMeshOptions()));
        check_mesh_equal(border_mesh, diff_mesh);
    }
}

 [GuiTest("gui/batch/basic-meshes superellipse fill uses delaunay when curve split is required")]
 public static void SourceCase21()
{
    Mesh mesh = new();
    VGFillWorkspace workspace = new();
    BasicMeshOptions options = new();
    options.ReuseFillWorkspace = workspace;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, new SRGBColor(0.0f, 1.0f, 0.0f, 1.0f)),
        new ColorGradientStop(1.0f, blue),
    };

    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(40.0f, 24.0f),
        40.0f,
        24.0f,
        0.0f,
        4.0f
    );

    bool emitted = BasicMeshes.SuperellipseFill(
        mesh,
        superellipse,
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 40.0f, 0.0f));
    Check.That(has_vertex_at(mesh, 40.0f, 48.0f));
    Check.That(is_mesh_partitioned_at_x(mesh, 40.0f));
    Check.False((workspace.ScratchContourVertices.Count == 0));
    Check.False((workspace.VertexLookup.Count == 0));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes superellipse stroke uses vg path")]
 public static void SourceCase22()
{
    Mesh mesh = new();

    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(32.0f, 20.0f),
        32.0f,
        20.0f,
        0.15f,
        3.2f
    );

    bool emitted = BasicMeshes.SuperellipseStroke(
        mesh,
        superellipse,
        6.0f,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes circle filled uses vg primitive without curve split")]
 public static void SourceCase23()
{
    Mesh mesh = new();

    Circle circle = Circle.CenterRadius(new Offsetf(20.0f, 20.0f), 20.0f);

    bool emitted = BasicMeshes.CircleFilled(
        mesh,
        circle,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 40.0f, 20.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes circle filled uses delaunay when curve split is required")]
 public static void SourceCase24()
{
    Mesh mesh = new();
    VGFillWorkspace workspace = new();
    BasicMeshOptions options = new();
    options.ReuseFillWorkspace = workspace;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor green = new(0.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, green),
        new ColorGradientStop(1.0f, blue),
    };

    Circle circle = Circle.CenterRadius(new Offsetf(20.0f, 20.0f), 20.0f);

    bool emitted = BasicMeshes.CircleFilled(
        mesh,
        circle,
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 20.0f, 0.0f));
    Check.That(has_vertex_at(mesh, 20.0f, 40.0f));
    Check.That(has_vertex_color_at(mesh, 20.0f, 0.0f, green));
    Check.That(has_vertex_color_at(mesh, 20.0f, 40.0f, green));
    Check.That(is_mesh_partitioned_at_x(mesh, 20.0f));
    Check.False((workspace.ScratchContourVertices.Count == 0));
    Check.False((workspace.VertexLookup.Count == 0));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes circle stroke uses vg path")]
 public static void SourceCase25()
{
    Mesh mesh = new();

    Circle circle = Circle.CenterRadius(new Offsetf(24.0f, 24.0f), 18.0f);

    bool emitted = BasicMeshes.CircleStroke(
        mesh,
        circle,
        6.0f,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes ellipse filled uses vg primitive without curve split")]
 public static void SourceCase26()
{
    Mesh mesh = new();

    Ellipse ellipse = Ellipse.CenterRadius(new Offsetf(30.0f, 20.0f), 30.0f, 16.0f);

    bool emitted = BasicMeshes.EllipseFilled(
        mesh,
        ellipse,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 60.0f, 20.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes ellipse filled uses delaunay when curve split is required")]
 public static void SourceCase27()
{
    Mesh mesh = new();
    VGFillWorkspace workspace = new();
    BasicMeshOptions options = new();
    options.ReuseFillWorkspace = workspace;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor green = new(0.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, green),
        new ColorGradientStop(1.0f, blue),
    };

    Ellipse ellipse = Ellipse.CenterRadius(new Offsetf(30.0f, 20.0f), 30.0f, 16.0f);

    bool emitted = BasicMeshes.EllipseFilled(
        mesh,
        ellipse,
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 30.0f, 4.0f));
    Check.That(has_vertex_at(mesh, 30.0f, 36.0f));
    Check.That(has_vertex_color_at(mesh, 30.0f, 4.0f, green));
    Check.That(has_vertex_color_at(mesh, 30.0f, 36.0f, green));
    Check.That(is_mesh_partitioned_at_x(mesh, 30.0f));
    Check.False((workspace.ScratchContourVertices.Count == 0));
    Check.False((workspace.VertexLookup.Count == 0));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes ellipse stroke uses vg path")]
 public static void SourceCase28()
{
    Mesh mesh = new();

    Ellipse ellipse = Ellipse.CenterRadius(new Offsetf(32.0f, 24.0f), 30.0f, 16.0f, 0.18f);

    bool emitted = BasicMeshes.EllipseStroke(
        mesh,
        ellipse,
        6.0f,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes fan uses vg primitive without curve split")]
 public static void SourceCase29()
{
    Mesh mesh = new();

    Arc arc = Arc.CenterRadius(new Offsetf(20.0f, 20.0f), 20.0f, 0.0f, (MathF.PI * .5f));

    bool emitted = BasicMeshes.Fan(
        mesh,
        arc,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 20.0f, 20.0f));
    Check.That(has_vertex_at(mesh, 40.0f, 20.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes fan uses vg path when curve split is required")]
 public static void SourceCase30()
{
    Mesh mesh = new();
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor green = new(0.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, green),
        new ColorGradientStop(1.0f, blue),
    };

    Arc arc = Arc.CenterRadius(new Offsetf(20.0f, 20.0f), 20.0f, 0.0f, (MathF.PI * .5f));

    bool emitted = BasicMeshes.Fan(
        mesh,
        arc,
        new ColorGradientCurve(stops),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 30.0f, 20.0f));
    Check.That(has_vertex_color_at(mesh, 30.0f, 20.0f, green));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes arc stroke uses vg path")]
 public static void SourceCase31()
{
    Mesh mesh = new();

    Arc arc = Arc.CenterRadius(new Offsetf(24.0f, 24.0f), 18.0f, -0.25f, 0.75f * MathF.PI);

    bool emitted = BasicMeshes.ArcStroke(
        mesh,
        arc,
        6.0f,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes elliptical fan uses vg primitive without curve split")]
 public static void SourceCase32()
{
    Mesh mesh = new();

    EllipticalArc arc = EllipticalArc.CenterRadius(
        new Offsetf(30.0f, 20.0f),
        30.0f,
        16.0f,
        0.0f,
        0.0f,
        (MathF.PI * .5f)
    );

    bool emitted = BasicMeshes.EllipticalFan(
        mesh,
        arc,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 30.0f, 20.0f));
    Check.That(has_vertex_at(mesh, 60.0f, 20.0f));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes elliptical fan uses vg path when curve split is required")]
 public static void SourceCase33()
{
    Mesh mesh = new();
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor green = new(0.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, green),
        new ColorGradientStop(1.0f, blue),
    };

    EllipticalArc arc = EllipticalArc.CenterRadius(
        new Offsetf(30.0f, 20.0f),
        30.0f,
        16.0f,
        0.0f,
        0.0f,
        (MathF.PI * .5f)
    );

    bool emitted = BasicMeshes.EllipticalFan(
        mesh,
        arc,
        new ColorGradientCurve(stops),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count > 6u);
    Check.That(has_vertex_at(mesh, 45.0f, 20.0f));
    Check.That(has_vertex_color_at(mesh, 45.0f, 20.0f, green));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes elliptical arc stroke uses vg path")]
 public static void SourceCase34()
{
    Mesh mesh = new();

    EllipticalArc arc = EllipticalArc.CenterRadius(
        new Offsetf(34.0f, 24.0f),
        30.0f,
        16.0f,
        0.18f,
        -0.25f,
        0.75f * MathF.PI
    );

    bool emitted = BasicMeshes.EllipticalArcStroke(
        mesh,
        arc,
        6.0f,
        new ColorGradientAxis(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f), new SRGBColor(0.0f, 0.0f, 1.0f, 1.0f)),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rect fill uses delaunay when curve split is required")]
 public static void SourceCase35()
{
    Mesh mesh = new();
    VGFillWorkspace workspace = new();
    BasicMeshOptions options = new();
    options.ReuseFillWorkspace = workspace;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor green = new(0.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, green),
        new ColorGradientStop(1.0f, blue),
    };

    bool emitted = BasicMeshes.RectFill(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 4u);
    Check.That(mesh.Indices.Count >= 12u);
    Check.That(has_vertex_at(mesh, 5.0f, 0.0f));
    Check.That(has_vertex_at(mesh, 5.0f, 10.0f));
    Check.That(has_vertex_color_at(mesh, 5.0f, 0.0f, green));
    Check.That(has_vertex_color_at(mesh, 5.0f, 10.0f, green));
    Check.That(is_mesh_partitioned_at_x(mesh, 5.0f));
    Check.False((workspace.ScratchContourVertices.Count == 0));
    Check.False((workspace.VertexLookup.Count == 0));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rect fill curve gradient without inner stop uses vg rect")]
 public static void SourceCase36()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(1.0f, blue),
    };

    bool emitted = BasicMeshes.RectFill(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 4u);
    Eq(mesh.Indices.Count, 6u);
    Check.That(has_vertex_color_at(mesh, 0.0f, 0.0f, red));
    Check.That(has_vertex_color_at(mesh, 10.0f, 0.0f, blue));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes curve gradient splits rect stroke before fill")]
 public static void SourceCase37()
{
    Mesh mesh = new();
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor green = new(0.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(0.5f, green),
        new ColorGradientStop(1.0f, blue),
    };

    bool emitted = BasicMeshes.RectStroke(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        2.0f,
        new ColorGradientCurve(stops),
        new BasicMeshOptions()
    );
    Check.That(emitted);

    Check.That(mesh.Vertices.Count > 8u);
    Check.That(mesh.Indices.Count > 24u);
    Check.That(has_vertex_at(mesh, 5.0f, -1.0f));
    Check.That(has_vertex_at(mesh, 5.0f, 1.0f));
    Check.That(has_vertex_color_at(mesh, 5.0f, -1.0f, green));
    Check.That(has_vertex_color_at(mesh, 5.0f, 1.0f, green));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rect stroke curve gradient without inner stop uses vg ring")]
 public static void SourceCase38()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(1.0f, blue),
    };

    bool emitted = BasicMeshes.RectStroke(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        2.0f,
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 8u);
    Eq(mesh.Indices.Count, 24u);
    Check.That(has_vertex_color_at(mesh, -1.0f, -1.0f, red));
    Check.That(has_vertex_color_at(mesh, 11.0f, -1.0f, blue));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes diff rect curve gradient without inner stop uses vg ring")]
 public static void SourceCase39()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 0.0f;
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);
    ColorGradientStop[] stops = {
        new ColorGradientStop(0.0f, red),
        new ColorGradientStop(1.0f, blue),
    };

    bool emitted = BasicMeshes.DiffRect(
        mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        Rectf.LTWH(2.0f, 2.0f, 6.0f, 6.0f),
        new ColorGradientCurve(stops),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 8u);
    Eq(mesh.Indices.Count, 24u);
    Check.That(has_vertex_color_at(mesh, 0.0f, 0.0f, red));
    Check.That(has_vertex_color_at(mesh, 10.0f, 0.0f, blue));
    Check.False(has_duplicate_vertices(mesh));
}

 [GuiTest("gui/batch/basic-meshes rejects invalid gradient before geometry")]
 public static void SourceCase40()
{
    Mesh fill_mesh = new();
    Mesh stroke_mesh = new();
    Mesh diff_mesh = new();
    Mesh rrect_fill_mesh = new();
    Mesh rrect_stroke_mesh = new();
    Mesh rrect_diff_mesh = new();
    Mesh rrect_border_mesh = new();
    Mesh superellipse_fill_mesh = new();
    Mesh superellipse_stroke_mesh = new();
    Mesh circle_filled_mesh = new();
    Mesh circle_stroke_mesh = new();
    Mesh ellipse_filled_mesh = new();
    Mesh ellipse_stroke_mesh = new();
    Mesh fan_mesh = new();
    Mesh arc_stroke_mesh = new();
    Mesh elliptical_fan_mesh = new();
    Mesh elliptical_arc_stroke_mesh = new();

    ColorGradient invalid_gradient = new ColorGradientCurve(Array.Empty<ColorGradientStop>());

    bool fill_emitted = BasicMeshes.RectFill(
        fill_mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool stroke_emitted = BasicMeshes.RectStroke(
        stroke_mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        2.0f,
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool diff_emitted = BasicMeshes.DiffRect(
        diff_mesh,
        Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f),
        Rectf.LTWH(2.0f, 2.0f, 6.0f, 6.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool rrect_fill_emitted = BasicMeshes.RRectFill(
        rrect_fill_mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f), 2.0f, 2.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool rrect_stroke_emitted = BasicMeshes.RRectStroke(
        rrect_stroke_mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f), 2.0f, 2.0f),
        2.0f,
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool rrect_diff_emitted = BasicMeshes.DiffRRect(
        rrect_diff_mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f), 2.0f, 2.0f),
        RRect.FromRectXY(Rectf.LTWH(2.0f, 2.0f, 6.0f, 6.0f), 1.0f, 1.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool rrect_border_emitted = BasicMeshes.RRectBorder(
        rrect_border_mesh,
        RRect.FromRectXY(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f), 2.0f, 2.0f),
        EdgeInsets.All(2.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool superellipse_fill_emitted = BasicMeshes.SuperellipseFill(
        superellipse_fill_mesh,
        Superellipse.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 5.0f, 0.0f, 4.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool superellipse_stroke_emitted = BasicMeshes.SuperellipseStroke(
        superellipse_stroke_mesh,
        Superellipse.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 5.0f, 0.0f, 4.0f),
        2.0f,
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool circle_filled_emitted = BasicMeshes.CircleFilled(
        circle_filled_mesh,
        Circle.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool circle_stroke_emitted = BasicMeshes.CircleStroke(
        circle_stroke_mesh,
        Circle.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f),
        2.0f,
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool ellipse_filled_emitted = BasicMeshes.EllipseFilled(
        ellipse_filled_mesh,
        Ellipse.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 4.0f),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool ellipse_stroke_emitted = BasicMeshes.EllipseStroke(
        ellipse_stroke_mesh,
        Ellipse.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 4.0f),
        2.0f,
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool fan_emitted = BasicMeshes.Fan(
        fan_mesh,
        Arc.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 0.0f, (MathF.PI * .5f)),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool arc_stroke_emitted = BasicMeshes.ArcStroke(
        arc_stroke_mesh,
        Arc.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 0.0f, (MathF.PI * .5f)),
        2.0f,
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool elliptical_fan_emitted = BasicMeshes.EllipticalFan(
        elliptical_fan_mesh,
        EllipticalArc.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 4.0f, 0.0f, 0.0f, (MathF.PI * .5f)),
        invalid_gradient,
        new BasicMeshOptions()
    );
    bool elliptical_arc_stroke_emitted = BasicMeshes.EllipticalArcStroke(
        elliptical_arc_stroke_mesh,
        EllipticalArc.CenterRadius(new Offsetf(5.0f, 5.0f), 5.0f, 4.0f, 0.0f, 0.0f, (MathF.PI * .5f)),
        2.0f,
        invalid_gradient,
        new BasicMeshOptions()
    );

    Check.False(fill_emitted);
    Check.False(stroke_emitted);
    Check.False(diff_emitted);
    Check.False(rrect_fill_emitted);
    Check.False(rrect_stroke_emitted);
    Check.False(rrect_diff_emitted);
    Check.False(rrect_border_emitted);
    Check.False(superellipse_fill_emitted);
    Check.False(superellipse_stroke_emitted);
    Check.False(circle_filled_emitted);
    Check.False(circle_stroke_emitted);
    Check.False(ellipse_filled_emitted);
    Check.False(ellipse_stroke_emitted);
    Check.False(fan_emitted);
    Check.False(arc_stroke_emitted);
    Check.False(elliptical_fan_emitted);
    Check.False(elliptical_arc_stroke_emitted);

    Check.That((fill_mesh.Vertices.Count == 0));
    Check.That((fill_mesh.Indices.Count == 0));
    Check.That((stroke_mesh.Vertices.Count == 0));
    Check.That((stroke_mesh.Indices.Count == 0));
    Check.That((diff_mesh.Vertices.Count == 0));
    Check.That((diff_mesh.Indices.Count == 0));
    Check.That((rrect_fill_mesh.Vertices.Count == 0));
    Check.That((rrect_fill_mesh.Indices.Count == 0));
    Check.That((rrect_stroke_mesh.Vertices.Count == 0));
    Check.That((rrect_stroke_mesh.Indices.Count == 0));
    Check.That((rrect_diff_mesh.Vertices.Count == 0));
    Check.That((rrect_diff_mesh.Indices.Count == 0));
    Check.That((rrect_border_mesh.Vertices.Count == 0));
    Check.That((rrect_border_mesh.Indices.Count == 0));
    Check.That((superellipse_fill_mesh.Vertices.Count == 0));
    Check.That((superellipse_fill_mesh.Indices.Count == 0));
    Check.That((superellipse_stroke_mesh.Vertices.Count == 0));
    Check.That((superellipse_stroke_mesh.Indices.Count == 0));
    Check.That((circle_filled_mesh.Vertices.Count == 0));
    Check.That((circle_filled_mesh.Indices.Count == 0));
    Check.That((circle_stroke_mesh.Vertices.Count == 0));
    Check.That((circle_stroke_mesh.Indices.Count == 0));
    Check.That((ellipse_filled_mesh.Vertices.Count == 0));
    Check.That((ellipse_filled_mesh.Indices.Count == 0));
    Check.That((ellipse_stroke_mesh.Vertices.Count == 0));
    Check.That((ellipse_stroke_mesh.Indices.Count == 0));
    Check.That((fan_mesh.Vertices.Count == 0));
    Check.That((fan_mesh.Indices.Count == 0));
    Check.That((arc_stroke_mesh.Vertices.Count == 0));
    Check.That((arc_stroke_mesh.Indices.Count == 0));
    Check.That((elliptical_fan_mesh.Vertices.Count == 0));
    Check.That((elliptical_fan_mesh.Indices.Count == 0));
    Check.That((elliptical_arc_stroke_mesh.Vertices.Count == 0));
    Check.That((elliptical_arc_stroke_mesh.Indices.Count == 0));
}

 [GuiTest("gui/batch/basic-meshes diff rect follows vg aa coverage")]
 public static void SourceCase41()
{
    Mesh mesh = new();
    BasicMeshOptions options = new();
    options.AaRadius = 1.0f;

    bool emitted = BasicMeshes.DiffRect(
        mesh,
        Rectf.LTWH(-1.0f, -1.0f, 12.0f, 12.0f),
        Rectf.LTWH(1.0f, 1.0f, 8.0f, 8.0f),
        new ColorGradientSolid(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
        options
    );
    Check.That(emitted);

    Eq(mesh.Vertices.Count, 16u);
    Eq(mesh.Indices.Count, 72u);
    check_color(unpack_color(mesh.Vertices[0].PackedColor), 1.0f, 1.0f, 1.0f, 1.0f);
    check_color(unpack_color(mesh.Vertices[8].PackedColor), 0.0f, 0.0f, 0.0f, 0.0f);
    Check.That(has_vertex_at(mesh, -2.0f, -2.0f));
    Check.That(has_vertex_at(mesh, 2.0f, 2.0f));
    Check.False(has_duplicate_vertices(mesh));
}
}
