namespace SkrGui.Tests;
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
 [GuiTest("gui/batch/color-gradient solid and axis sampling")] public static void Case1()
{
    ColorGradient default_gradient = new();
    True(default_gradient.IsSolid());
    True(default_gradient.IsValid());
    check_color(default_gradient.Sample(0.5f), 1.0f, 1.0f, 1.0f, 1.0f);

    SRGBColor solid_color = new(0.2f, 0.4f, 0.6f, 0.8f);
    ColorGradient solid = new ColorGradientSolid(solid_color);

    True(solid.IsSolid());
    True(solid.IsValid());
    False(solid.IsAxis());
    False(solid.IsCurve());
    True(solid.AsSolid());
    False(solid.AsAxis());
    False(solid.AsCurve());

    ColorGradientSolid? solid_data = solid.AsSolid();
    if (solid_data is null)
    {
        return;
    }
    check_color(solid_data.Color(), 0.2f, 0.4f, 0.6f, 0.8f);
    check_color(solid.Sample(0.5f), 0.2f, 0.4f, 0.6f, 0.8f);
    check_color(solid.Sample(new Offsetf(0.75f, 0.25f)), 0.2f, 0.4f, 0.6f, 0.8f);
    solid_data.SetColor(new SRGBColor(0.1f, 0.2f, 0.3f, 0.4f));
    check_color(solid.Sample(0.5f), 0.1f, 0.2f, 0.3f, 0.4f);

    SRGBColor start_color = new(0.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor end_color = new(1.0f, 0.5f, 0.25f, 0.5f);
    Offsetf uv = new(0.25f, 0.75f);
    Rectf bounds = Rectf.LTWH(10.0f, 20.0f, 100.0f, 200.0f);
    Offsetf point = new(35.0f, 170.0f);

    ColorGradient ltr = new ColorGradientAxis(start_color, end_color, EColorGradientAxis.LeftToRight);
    True(ltr.IsAxis());
    True(ltr.IsValid());
    True(ltr.AsAxis());

    ColorGradientAxis? ltr_axis = ltr.AsAxis();
    if (ltr_axis is null)
    {
        return;
    }
    check_color(ltr_axis.StartColor(), 0.0f, 0.0f, 0.0f, 1.0f);
    check_color(ltr_axis.EndColor(), 1.0f, 0.5f, 0.25f, 0.5f);
    True(ltr_axis.Axis() == EColorGradientAxis.LeftToRight);
    check_color(ltr.Sample(uv), 0.25f, 0.125f, 0.0625f, 0.875f);
    check_color(ltr.Sample(bounds, point), 0.25f, 0.125f, 0.0625f, 0.875f);
    ltr_axis.SetStartColor(new SRGBColor(0.5f, 0.0f, 0.0f, 1.0f));
    ltr_axis.SetEndColor(new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f));
    ltr_axis.SetAxis(EColorGradientAxis.TopToBottom);
    check_color(ltr.Sample(new Offsetf(0.0f, 0.5f)), 0.75f, 0.5f, 0.5f, 1.0f);

    ColorGradient rtl = new ColorGradientAxis(start_color, end_color, EColorGradientAxis.RightToLeft);
    check_color(rtl.Sample(uv), 0.75f, 0.375f, 0.1875f, 0.625f);

    ColorGradient ttd = new ColorGradientAxis(start_color, end_color, EColorGradientAxis.TopToBottom);
    check_color(ttd.Sample(uv), 0.75f, 0.375f, 0.1875f, 0.625f);

    ColorGradient dtt = new ColorGradientAxis(start_color, end_color, EColorGradientAxis.BottomToTop);
    check_color(dtt.Sample(uv), 0.25f, 0.125f, 0.0625f, 0.875f);
}
 [GuiTest("gui/batch/color-gradient validity")] public static void Case2()
{
    float inf = float.PositiveInfinity;
    SRGBColor valid_color = new(0.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor invalid_color = new(inf, 0.0f, 0.0f, 1.0f);

    True(new ColorGradientSolid(valid_color).IsValid());
    False(new ColorGradientSolid(invalid_color).IsValid());
    False(new ColorGradient(new ColorGradientSolid(invalid_color)).IsValid());

    True(new ColorGradientAxis(valid_color, valid_color).IsValid());
    False(new ColorGradientAxis(invalid_color, valid_color).IsValid());
    ColorGradientAxis invalid_axis = new(
        valid_color,
        valid_color,
        (EColorGradientAxis)(255u)
    );
    False(invalid_axis.IsValid());

    ColorGradientStop[] valid_stops = {
        new ColorGradientStop(0.0f, valid_color),
        new ColorGradientStop(1.0f, new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)),
    };
    ColorGradientStop[] invalid_stops = {
        new ColorGradientStop(0.0f, valid_color),
        new ColorGradientStop(1.0f, invalid_color),
    };
    True(valid_stops[0].IsValid());
    False(invalid_stops[1].IsValid());

    True(new ColorGradientCurve(valid_stops).IsValid());
    False(new ColorGradientCurve(Array.Empty<ColorGradientStop>()).IsValid());
    False(
        new ColorGradientCurve(
            valid_stops,
            new Offsetf(0.0f, 0.0f),
            new Offsetf(0.0f, 0.0f)
        )
            .IsValid()
    );
    False(new ColorGradientCurve(invalid_stops).IsValid());
}
 [GuiTest("gui/batch/color-gradient curve stop sorting and duplicate override")] public static void Case3()
{
    SRGBColor red = new(1.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor green = new(0.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor yellow = new(1.0f, 1.0f, 0.0f, 1.0f);
    SRGBColor blue = new(0.0f, 0.0f, 1.0f, 1.0f);

    ColorGradientStop[] gradient_stops = {
        new ColorGradientStop(0.5f, green),
        new ColorGradientStop(2.0f, blue),
        new ColorGradientStop(-1.0f, red),
        new ColorGradientStop(0.5f, yellow),
    };
    ColorGradient gradient = new ColorGradientCurve(
        gradient_stops,
        new Offsetf(0.0f, 0.0f),
        new Offsetf(0.0f, 1.0f)
    );

    False(gradient.IsSolid());
    False(gradient.IsAxis());
    True(gradient.IsCurve());
    True(gradient.IsValid());

    ColorGradientCurve? curve = gradient.AsCurve();
    True(curve);
    if (curve is null)
    {
        return;
    }
    var stops = curve.Stops();
    Eq(stops.Length, 3u);
    expect_near(stops[0].Position, 0.0f, kFloatEpsilon);
    expect_near(stops[1].Position, 0.5f, kFloatEpsilon);
    expect_near(stops[2].Position, 1.0f, kFloatEpsilon);
    check_color(stops[1].Color, 1.0f, 1.0f, 0.0f, 1.0f);

    check_color(gradient.Sample(0.25f), 1.0f, 0.5f, 0.0f, 1.0f);
    check_color(gradient.Sample(0.75f), 0.5f, 0.5f, 0.5f, 1.0f);
    check_color(gradient.Sample(new Offsetf(0.0f, 0.75f)), 0.5f, 0.5f, 0.5f, 1.0f);
    check_color(gradient.Sample(float.PositiveInfinity), 1.0f, 0.0f, 0.0f, 1.0f);

    curve.AddStop(0.5f, green);
    stops = curve.Stops();
    Eq(stops.Length, 3u);
    check_color(gradient.Sample(0.5f), 0.0f, 1.0f, 0.0f, 1.0f);

    curve.AddStop(0.25f, blue);
    stops = curve.Stops();
    Eq(stops.Length, 4u);
    expect_near(stops[1].Position, 0.25f, kFloatEpsilon);
    check_color(stops[1].Color, 0.0f, 0.0f, 1.0f, 1.0f);

    ColorGradient empty = new ColorGradientCurve(Array.Empty<ColorGradientStop>());
    False(empty.IsValid());
    check_color(empty.Sample(0.5f), 0.0f, 0.0f, 0.0f, 1.0f);

    ColorGradientStop[] one_stop = { new ColorGradientStop(0.5f, blue) };
    ColorGradient one = new ColorGradientCurve(one_stop);
    True(one.IsValid());
    check_color(one.Sample(0.0f), 0.0f, 0.0f, 1.0f, 1.0f);
    check_color(one.Sample(1.0f), 0.0f, 0.0f, 1.0f, 1.0f);

    ColorGradientStop[] reset_stops = {
        new ColorGradientStop(1.0f, blue),
        new ColorGradientStop(0.0f, green),
    };
    curve.Reset(
        reset_stops,
        new Offsetf(1.0f, 0.0f),
        new Offsetf(0.0f, 0.0f)
    );
    stops = curve.Stops();
    Eq(stops.Length, 2u);
    check_offsetf(curve.Start(), 1.0f, 0.0f);
    check_offsetf(curve.End(), 0.0f, 0.0f);
    check_color(gradient.Sample(new Offsetf(0.5f, 0.0f)), 0.0f, 0.5f, 0.5f, 1.0f);
}
 [GuiTest("gui/batch/color-gradient rect sampling")] public static void Case4()
{
    SRGBColor start_color = new(0.0f, 0.0f, 0.0f, 1.0f);
    SRGBColor end_color = new(1.0f, 1.0f, 1.0f, 0.5f);
    ColorGradient gradient = new ColorGradientAxis(start_color, end_color);

    Rectf bounds = Rectf.LTWH(10.0f, 20.0f, 80.0f, 40.0f);
    check_color(gradient.Sample(bounds, new Offsetf(50.0f, 30.0f)), 0.5f, 0.5f, 0.5f, 0.75f);
    check_color(gradient.Sample(Rectf.Zero(), new Offsetf(50.0f, 30.0f)), 0.0f, 0.0f, 0.0f, 1.0f);
}
 [GuiTest("gui/batch/PaintContext draws mesh with current transform state")] public static void Case5()
{
    var texture = new PaintContextTestTexture();
    var shader = new PaintContextTestShader();
    var mesh = make_paint_context_mesh();

    PaintContext context = new(2.0f, true, 0.75f);
    False(context.BatchRoot());
    expect_near(context.RootPixelRatio(), 2.0f, kFloatEpsilon);
    expect_near(context.PixelRatio(), 2.0f, kFloatEpsilon);
    expect_near(context.AaRadius(), 0.75f, kFloatEpsilon);
    expect_near(context.Opacity(), 1.0f, kFloatEpsilon);
    True(context.RootScaleOffsetOnly());
    True(context.ScaleOffsetOnly());

    PaintTransform transform = new();
    transform.ApplyOffset2D(new Offsetf(2.0f, 3.0f));
    transform.ApplyScale2D(new Offsetf(2.0f, 4.0f));
    context.PushTransform(transform);

    True(context.RootScaleOffsetOnly());
    True(context.ScaleOffsetOnly());
    expect_near(context.PixelRatio(), 8.0f, kFloatEpsilon);
    False(context.BatchRoot());
    check_rectf(context.TransformBound(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)), 4.0f, 11.0f, 10.0f, 27.0f);
    check_rectf(context.ClippedLocalBound(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)), 4.0f, 11.0f, 10.0f, 27.0f);
    True(context.IsLocalBoundVisible(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)));

    var cmd = context.DrawMesh(mesh, texture, shader, Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f));
    True(cmd);
    True(context.BatchRoot());
    Eq(context.BatchRoot().Cmds.Count, 1u);
    True(context.BatchRoot().Cmds[0] == cmd);
    True(cmd.Mesh == mesh);
    True(cmd.Texture == texture);
    True(cmd.Shader == shader);
    check_rectf(cmd.Bound, 4.0f, 11.0f, 10.0f, 27.0f);
}
 [GuiTest("gui/batch/PaintContext resolves uniform device mapping")] public static void Case6()
{
    PaintContext context = new(2.0f);
    float pixel_ratio = 0.0f;
    Offsetf device_offset = new();
    True(context.ResolvePixelMapping(out pixel_ratio,out device_offset));
    expect_near(pixel_ratio, 2.0f, kFloatEpsilon);
    expect_near(device_offset.X, 0.0f, kFloatEpsilon);
    expect_near(device_offset.Y, 0.0f, kFloatEpsilon);

    PaintTransform uniform = new();
    uniform.ApplyOffset2D(new Offsetf(2.0f, 3.0f));
    uniform.ApplyScale2D(new Offsetf(4.0f, 4.0f));
    context.PushTransform(uniform);
    True(context.ResolvePixelMapping(out pixel_ratio,out device_offset));
    expect_near(pixel_ratio, 8.0f, kFloatEpsilon);
    expect_near(device_offset.X, 4.0f, kFloatEpsilon);
    expect_near(device_offset.Y, 6.0f, kFloatEpsilon);
    context.PopTransform();

    PaintTransform non_uniform = new();
    non_uniform.ApplyScale2D(new Offsetf(2.0f, 3.0f));
    context.PushTransform(non_uniform);
    False(context.ResolvePixelMapping(out pixel_ratio,out device_offset));
    context.PopTransform();

    PaintTransform rotated = new();
    rotated.ApplyRotation2D(MathF.PI * 0.25f);
    context.PushTransform(rotated);
    False(context.ResolvePixelMapping(out pixel_ratio,out device_offset));
    context.PopTransform();

    PaintTransform reflected = new();
    reflected.ApplyScale2D(new Offsetf(-1.0f, -1.0f));
    context.PushTransform(reflected);
    False(context.ResolvePixelMapping(out pixel_ratio,out device_offset));
}
 [GuiTest("gui/batch/PaintContext builds clip root tree")] public static void Case7()
{
    PaintContext context = new();
    False(context.HasClipBound());
    check_rectf(context.ClippedBound(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)), 1.0f, 2.0f, 4.0f, 6.0f);
    True(context.IsBoundVisible(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)));

    var clip = context.PushClipRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f));
    True(clip);
    True(context.HasClipBound());
    check_rectf(context.ClipBound(), 0.0f, 0.0f, 10.0f, 10.0f);
    Eq(context.BatchRoot().Cmds.Count, 1u);
    True(clip is BatchCmdClipRect);
    True(clip.SubRoot);
    check_rectf(clip.Bound, 0.0f, 0.0f, 10.0f, 10.0f);

    var texture = new PaintContextTestTexture();
    var shader = new PaintContextTestShader();
    var mesh = make_paint_context_mesh();
    var child_cmd = context.DrawMesh(mesh, texture, shader, Rectf.LTWH(2.0f, 3.0f, 4.0f, 5.0f));

    True(child_cmd);
    Eq(clip.SubRoot.Cmds.Count, 1u);
    True(clip.SubRoot.Cmds[0] == child_cmd);

    context.PopClip();
    False(context.HasClipBound());
    var outer_cmd = context.DrawMesh(mesh, texture, shader, Rectf.LTWH(20.0f, 0.0f, 5.0f, 5.0f));

    True(outer_cmd);
    Eq(context.BatchRoot().Cmds.Count, 2u);
    True(context.BatchRoot().Cmds[1] == outer_cmd);
}
 [GuiTest("gui/batch/PaintContext complex clip rect falls back to clip mesh")] public static void Case8()
{
    PaintContext context = new();
    PaintTransform transform = new();
    transform.ApplyRotation2D(MathF.PI * 0.5f);
    context.PushTransform(transform);

    True(context.RootScaleOffsetOnly());
    False(context.ScaleOffsetOnly());
    var clip = context.PushClipRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 20.0f));

    True(clip);
    Eq(context.BatchRoot().Cmds.Count, 1u);
    True(clip is BatchCmdClipMesh);

    var clip_mesh = (BatchCmdClipMesh)clip;
    True(clip_mesh.Mesh);
    Eq(clip_mesh.Mesh.Vertices.Count, 4u);
    Eq(clip_mesh.Mesh.Indices.Count, 6u);
}
 [GuiTest("gui/batch/PaintContext supports clip mesh backdrop opacity and reset")] public static void Case9()
{
    var mesh = make_paint_context_mesh();

    PaintContext context = new();
    context.PushOpacity(0.5f);
    context.PushOpacity(0.5f);
    expect_near(context.Opacity(), 0.25f, kFloatEpsilon);
    context.PopOpacity();
    expect_near(context.Opacity(), 0.5f, kFloatEpsilon);
    context.PopOpacity();
    expect_near(context.Opacity(), 1.0f, kFloatEpsilon);

    var clip = context.PushClipMesh(mesh, Rectf.LTWH(0.0f, 0.0f, 4.0f, 4.0f));
    True(clip);
    Eq(context.BatchRoot().Cmds.Count, 1u);
    True(clip.SubRoot);

    context.PopClip();
    var backdrop = context.Backdrop(
        new BatchCmdBackdrop(),
        mesh,
        Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)
    );

    True(backdrop);
    Eq(context.BatchRoot().Cmds.Count, 2u);
    True(backdrop is BatchCmdBackdrop);

    context.Reset(3.0f, false, 0.5f);
    False(context.BatchRoot());
    expect_near(context.RootPixelRatio(), 3.0f, kFloatEpsilon);
    expect_near(context.PixelRatio(), 3.0f, kFloatEpsilon);
    expect_near(context.AaRadius(), 0.5f, kFloatEpsilon);
    expect_near(context.Opacity(), 1.0f, kFloatEpsilon);
    False(context.RootScaleOffsetOnly());
    False(context.ScaleOffsetOnly());
}
 [GuiTest("gui/batch/PaintContext root complex transform forces mesh clip")] public static void Case10()
{
    PaintContext context = new(1.0f, false);
    False(context.BatchRoot());
    False(context.RootScaleOffsetOnly());
    False(context.ScaleOffsetOnly());

    var clip = context.PushClipRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 20.0f));

    True(clip);
    Eq(context.BatchRoot().Cmds.Count, 1u);
    True(clip is BatchCmdClipMesh);
}
 [GuiTest("gui/batch/PaintContext clips draw command bounds with clip stack")] public static void Case11()
{
    var texture = new PaintContextTestTexture();
    var shader = new PaintContextTestShader();
    var mesh = make_paint_context_mesh();

    PaintContext context = new();
    var clip = context.PushClipRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f));
    True(clip);
    check_rectf(context.ClipBound(), 0.0f, 0.0f, 10.0f, 10.0f);
    check_rectf(context.ClippedBound(Rectf.LTWH(8.0f, 2.0f, 8.0f, 4.0f)), 8.0f, 2.0f, 10.0f, 6.0f);
    check_rectf(context.ClippedLocalBound(Rectf.LTWH(8.0f, 2.0f, 8.0f, 4.0f)), 8.0f, 2.0f, 10.0f, 6.0f);
    False(context.IsBoundVisible(Rectf.LTWH(20.0f, 20.0f, 4.0f, 4.0f)));
    False(context.IsLocalBoundVisible(Rectf.LTWH(20.0f, 20.0f, 4.0f, 4.0f)));

    var clipped_cmd = context.DrawMesh(mesh, texture, shader, Rectf.LTWH(8.0f, 2.0f, 8.0f, 4.0f));
    True(clipped_cmd);
    check_rectf(clipped_cmd.Bound, 8.0f, 2.0f, 10.0f, 6.0f);

    var skipped_cmd = context.DrawMesh(mesh, texture, shader, Rectf.LTWH(20.0f, 20.0f, 4.0f, 4.0f));
    False(skipped_cmd);
    Eq(clip.SubRoot.Cmds.Count, 1u);

    var nested_clip = context.PushClipRect(Rectf.LTWH(5.0f, 5.0f, 10.0f, 10.0f));
    True(nested_clip);
    check_rectf(context.ClipBound(), 5.0f, 5.0f, 10.0f, 10.0f);

    var nested_cmd = context.Backdrop(
        new BatchCmdBackdrop(),
        mesh,
        Rectf.LTWH(4.0f, 4.0f, 3.0f, 3.0f)
    );
    True(nested_cmd);
    check_rectf(nested_cmd.Bound, 5.0f, 5.0f, 7.0f, 7.0f);

    context.PopClip();
    check_rectf(context.ClipBound(), 0.0f, 0.0f, 10.0f, 10.0f);

    context.PopClip();
    False(context.HasClipBound());
}
 [GuiTest("gui/batch/PaintContext skips empty clipped clip commands")] public static void Case12()
{
    var texture = new PaintContextTestTexture();
    var shader = new PaintContextTestShader();
    var mesh = make_paint_context_mesh();

    PaintContext context = new();
    var outer_clip = context.PushClipRect(Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f));
    True(outer_clip);
    True(outer_clip.SubRoot);

    var empty_rect_clip = context.PushClipRect(Rectf.LTWH(20.0f, 20.0f, 4.0f, 4.0f));
    False(empty_rect_clip);
    True(context.HasClipBound());
    True(context.ClipBound().IsEmpty());
    Eq(outer_clip.SubRoot.Cmds.Count, 0u);

    var skipped_cmd = context.DrawMesh(mesh, texture, shader, Rectf.LTWH(1.0f, 1.0f, 2.0f, 2.0f));
    False(skipped_cmd);
    Eq(outer_clip.SubRoot.Cmds.Count, 0u);

    context.PopClip();
    check_rectf(context.ClipBound(), 0.0f, 0.0f, 10.0f, 10.0f);

    var empty_mesh_clip = context.PushClipMesh(mesh, Rectf.LTWH(20.0f, 20.0f, 4.0f, 4.0f));
    False(empty_mesh_clip);
    True(context.ClipBound().IsEmpty());
    Eq(outer_clip.SubRoot.Cmds.Count, 0u);

    var skipped_backdrop = context.Backdrop(
        new BatchCmdBackdrop(),
        mesh,
        Rectf.LTWH(2.0f, 2.0f, 2.0f, 2.0f)
    );
    False(skipped_backdrop);
    Eq(outer_clip.SubRoot.Cmds.Count, 0u);

    context.PopClip();
    check_rectf(context.ClipBound(), 0.0f, 0.0f, 10.0f, 10.0f);

    var visible_cmd = context.DrawMesh(mesh, texture, shader, Rectf.LTWH(2.0f, 2.0f, 2.0f, 2.0f));
    True(visible_cmd);
    Eq(outer_clip.SubRoot.Cmds.Count, 1u);

    context.PopClip();
    False(context.HasClipBound());
}
}
