using System.Numerics;
namespace SkrGui.Tests;
// Source: all tests/math/paint_transform_tests.cpp cases. Numerics vectors/matrices preserve row-vector storage.
public static class PaintTransformContractTests {
const float kPi=MathF.PI,kFloatEpsilon=.0001f;
private static void check_offsetf(Offsetf p,float x,float y){Check.Near(x,p.X,kFloatEpsilon);Check.Near(y,p.Y,kFloatEpsilon);}
private static void check_rectf(Rectf r,float l,float t,float right,float b){Check.Near(l,r.Left,kFloatEpsilon);Check.Near(t,r.Top,kFloatEpsilon);Check.Near(right,r.Right,kFloatEpsilon);Check.Near(b,r.Bottom,kFloatEpsilon);}
private static void check_transformed_point(PaintTransform t,Offsetf p,float x,float y)=>check_offsetf(t.TransformPoint(p),x,y);
private static void expect_near(float a,float b,float e)=>Check.Near(b,a,e);
[GuiTest("gui/math/PaintTransform offset fast path")]
public static void SourceCase01()
{
    PaintTransform transform = new();

    Check.False(transform.HasTransform());
    Check.That(transform.ScaleOffsetOnly());
    check_offsetf(transform.Offset(), 0.0f, 0.0f);
    expect_near(transform.PixelRatioScale(), 1.0f, kFloatEpsilon);

    transform.ApplyOffset2D(new Offsetf(3.0f, 4.0f));

    Check.False(transform.HasTransform());
    Check.That(transform.ScaleOffsetOnly());
    check_offsetf(transform.Offset(), 3.0f, 4.0f);
    check_transformed_point(transform, new Offsetf(5.0f, 6.0f), 8.0f, 10.0f);
    check_offsetf(transform.TransformVector(new Offsetf(5.0f, 6.0f)), 5.0f, 6.0f);
    check_rectf(transform.TransformRect(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)), 4.0f, 6.0f, 7.0f, 10.0f);

    Vector4 projected =
        Vector4.Transform(new Vector4(5,6,0,1),transform.ResolvedTransform());
    expect_near(projected.X, 8.0f, kFloatEpsilon);
    expect_near(projected.Y, 10.0f, kFloatEpsilon);
}

[GuiTest("gui/math/PaintTransform reset restores default state")]
public static void SourceCase02()
{
    PaintTransform transform = new();
    transform.ApplyOffset2D(new Offsetf(2.0f, 3.0f));
    transform.ApplyScale2D(new Offsetf(2.0f, 4.0f));
    transform.ApplyRotation2D(kPi * 0.5f);

    Check.That(transform.HasTransform());
    Check.False(transform.ScaleOffsetOnly());
    expect_near(transform.PixelRatioScale(), 4.0f, kFloatEpsilon);

    transform.Reset();

    Check.False(transform.HasTransform());
    Check.That(transform.ScaleOffsetOnly());
    check_offsetf(transform.Offset(), 0.0f, 0.0f);
    expect_near(transform.PixelRatioScale(), 1.0f, kFloatEpsilon);
    check_transformed_point(transform, new Offsetf(5.0f, 6.0f), 5.0f, 6.0f);
}

[GuiTest("gui/math/PaintTransform scale keeps scale offset state")]
public static void SourceCase03()
{
    PaintTransform transform = new();

    transform.ApplyOffset2D(new Offsetf(2.0f, 3.0f));
    transform.ApplyScale2D(new Offsetf(2.0f, 4.0f));

    Check.That(transform.HasTransform());
    Check.That(transform.ScaleOffsetOnly());
    check_offsetf(transform.Offset(), 0.0f, 0.0f);
    expect_near(transform.PixelRatioScale(), 4.0f, kFloatEpsilon);
    check_transformed_point(transform, new Offsetf(5.0f, 6.0f), 12.0f, 27.0f);
    check_offsetf(transform.TransformVector(new Offsetf(5.0f, 6.0f)), 10.0f, 24.0f);
    check_rectf(transform.TransformRect(Rectf.LTWH(1.0f, 2.0f, 3.0f, 4.0f)), 4.0f, 11.0f, 10.0f, 27.0f);

    transform.ApplyOffset2D(new Offsetf(1.0f, 2.0f));
    Check.That(transform.ScaleOffsetOnly());
    check_transformed_point(transform, new Offsetf(0.0f, 0.0f), 4.0f, 11.0f);
}

[GuiTest("gui/math/PaintTransform complex 2d state")]
public static void SourceCase04()
{
    PaintTransform rotation = new();
    rotation.ApplyRotation2D(kPi * 0.5f);

    Check.That(rotation.HasTransform());
    Check.False(rotation.ScaleOffsetOnly());
    expect_near(rotation.PixelRatioScale(), 1.0f, kFloatEpsilon);
    check_transformed_point(rotation, new Offsetf(1.0f, 0.0f), 0.0f, 1.0f);

    PaintTransform shear = new();
    shear.ApplyShear2D(new Offsetf(2.0f, 0.0f));

    Check.That(shear.HasTransform());
    Check.False(shear.ScaleOffsetOnly());
    expect_near(shear.PixelRatioScale(), 2.4142137f, kFloatEpsilon);
    check_transformed_point(shear, new Offsetf(1.0f, 3.0f), 7.0f, 3.0f);
}

[GuiTest("gui/math/PaintTransform 3d rotation overloads and projection")]
public static void SourceCase05()
{
    Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ,kPi * .5f);
    RotatorF rotator = new RotatorF(0,0,MathF.Atan2(2*(rotation.W*rotation.Z+rotation.X*rotation.Y),1-2*(rotation.Y*rotation.Y+rotation.Z*rotation.Z)));

    PaintTransform rotator_transform = new();
    rotator_transform.ApplyRotation3D(rotator);

    PaintTransform quat_transform = new();
    quat_transform.ApplyRotation3D(rotation);

    Check.That(rotator_transform.HasTransform());
    Check.That(quat_transform.HasTransform());
    Check.False(rotator_transform.ScaleOffsetOnly());
    Check.False(quat_transform.ScaleOffsetOnly());
    check_offsetf(rotator_transform.TransformPoint(new Offsetf(1.0f, 0.0f)), 0.0f, 1.0f);
    check_offsetf(quat_transform.TransformPoint(new Offsetf(1.0f, 0.0f)), 0.0f, 1.0f);

    PaintTransform offset = new();
    offset.ApplyOffset3D(new Vector3(2.0f, 3.0f, 0.0f));
    Check.False(offset.HasTransform());
    check_offsetf(offset.Offset(), 2.0f, 3.0f);

    PaintTransform scale = new();
    scale.ApplyScale3D(new Vector3(2.0f, 3.0f, 4.0f));
    Check.That(scale.HasTransform());
    Check.False(scale.ScaleOffsetOnly());
    expect_near(scale.PixelRatioScale(), 1.0f, kFloatEpsilon);

    PaintTransform perspective = new();
    perspective.ApplyPerspective(32.0f, 24.0f, 1.0f, 100.0f);
    Check.That(perspective.HasTransform());
    Check.False(perspective.ScaleOffsetOnly());
}

[GuiTest("gui/math/PaintTransform matrix apply")]
public static void SourceCase06()
{
    PaintTransform matrix_2d = new();
    matrix_2d.Apply(new Float3x3( 2.0f, 0.0f, 0.0f, 0.0f, 3.0f, 0.0f, 100.0f, 200.0f, 1.0f ));

    Check.That(matrix_2d.HasTransform());
    Check.That(matrix_2d.ScaleOffsetOnly());
    expect_near(matrix_2d.PixelRatioScale(), 3.0f, kFloatEpsilon);
    check_transformed_point(matrix_2d, new Offsetf(1.0f, 2.0f), 102.0f, 206.0f);

    PaintTransform shear_2d = new();
    shear_2d.Apply(new Float3x3( 1.0f, 0.0f, 0.0f, 2.0f, 1.0f, 0.0f, 100.0f, 0.0f, 1.0f ));

    Check.That(shear_2d.HasTransform());
    Check.False(shear_2d.ScaleOffsetOnly());
    expect_near(shear_2d.PixelRatioScale(), 2.4142137f, kFloatEpsilon);
    check_transformed_point(shear_2d, new Offsetf(1.0f, 3.0f), 107.0f, 3.0f);

    PaintTransform matrix_4d = new();
    matrix_4d.Apply(
        Matrix4x4.CreateRotationZ(kPi * 0.5f) *
        Matrix4x4.CreateTranslation(new Vector3(5.0f, 0.0f, 0.0f))
    );

    Check.That(matrix_4d.HasTransform());
    Check.False(matrix_4d.ScaleOffsetOnly());
    expect_near(matrix_4d.PixelRatioScale(), 1.0f, kFloatEpsilon);
    check_transformed_point(matrix_4d, new Offsetf(1.0f, 0.0f), 5.0f, 1.0f);
}

[GuiTest("gui/math/PaintTransform can apply another PaintTransform")]
public static void SourceCase07()
{
    PaintTransform target = new();
    target.ApplyOffset2D(new Offsetf(10.0f, 0.0f));

    PaintTransform local = new();
    local.ApplyOffset2D(new Offsetf(2.0f, 3.0f));
    local.ApplyScale2D(new Offsetf(2.0f, 3.0f));

    target.Apply(local);

    Check.That(target.HasTransform());
    Check.That(target.ScaleOffsetOnly());
    expect_near(target.PixelRatioScale(), 3.0f, kFloatEpsilon);
    check_transformed_point(target, new Offsetf(1.0f, 1.0f), 14.0f, 6.0f);

    PaintTransform complex = new();
    complex.ApplyRotation2D(kPi * 0.5f);

    target.Apply(complex);

    Check.False(target.ScaleOffsetOnly());
    expect_near(target.PixelRatioScale(), 3.0f, kFloatEpsilon);
    check_transformed_point(target, new Offsetf(1.0f, 1.0f), 10.0f, 6.0f);
}

[GuiTest("gui/math/PaintTransform components apply in semantic order")]
public static void SourceCase08()
{
    PaintTransform2D transform_2d = new();
    transform_2d.Offset = new Offsetf(5.0f, 0.0f);
    transform_2d.Rotation = kPi * 0.5f;
    transform_2d.Scale = new Offsetf(2.0f, 1.0f);
    transform_2d.Pivot = new Offsetf(10.0f, 0.0f);

    PaintTransform target_2d = new();
    target_2d.Apply(transform_2d);

    Check.That(target_2d.HasTransform());
    Check.False(target_2d.ScaleOffsetOnly());
    check_transformed_point(target_2d, new Offsetf(11.0f, 0.0f), 15.0f, 2.0f);

    PaintTransform3D transform_3d = new();
    transform_3d.Transform = new TransformF { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ,kPi*.5f),Position=new Vector3(5,0,0),Scale=new Vector3(2,1,1) };
    transform_3d.Pivot = new Vector3(10.0f, 0.0f, 0.0f);

    PaintTransform target_3d = new();
    target_3d.Apply(transform_3d);

    Check.That(target_3d.HasTransform());
    Check.False(target_3d.ScaleOffsetOnly());
    check_transformed_point(target_3d, new Offsetf(11.0f, 0.0f), 15.0f, 2.0f);
}

}
