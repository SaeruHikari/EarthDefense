using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
namespace SkrGui.Tests;
internal static class ShapeTests
{

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

private static float SignedPowForTest(float value, float power){
    float magnitude = MathF.Pow(MathF.Abs(value), power);
    return value < 0.0f ? -magnitude : magnitude;
}
private static float SuperellipseAngleFromPoint(
    Superellipse shape,
    Offsetf point
){
    Superellipse value = shape.Normalized();
    Offsetf delta = point - value.Center;
    float cos_rotation = MathF.Cos(value.Rotation);
    float sin_rotation = MathF.Sin(value.Rotation);
    Offsetf local = new Offsetf(
        delta.X * cos_rotation + delta.Y * sin_rotation,
        -delta.X * sin_rotation + delta.Y * cos_rotation
    );
    float power = value.Exponent * 0.5f;
    float cos_angle = SignedPowForTest(local.X / value.RadiusX, power);
    float sin_angle = SignedPowForTest(local.Y / value.RadiusY, power);
    return MathF.Atan2(sin_angle, cos_angle);
}
private static float UnwrapAngleAfter(float angle, float previous){
    while (angle <= previous + 1.0e-5f)
    {
        angle += (MathF.PI * 2);
    }
    return angle;
}
private static float UnwrapAngleBefore(float angle, float previous){
    while (angle >= previous - 1.0e-5f)
    {
        angle -= (MathF.PI * 2);
    }
    return angle;
}
private static void ExpectShapeSampleMatch(
    List<CollectedShapeSample> lhs,
    List<CollectedShapeSample> rhs
){
    Check.That(lhs.Size() == rhs.Size());
    for (int i = 0; i < lhs.Size() && i < rhs.Size(); ++i)
    {
        CheckOffsetf(lhs[i].Point, rhs[i].Point.X, rhs[i].Point.Y);
        ExpectNear(lhs[i].T, rhs[i].T, 0.001f);
    }
}
private static List<CollectedShapeSample> ReverseShapeSamples(
    List<CollectedShapeSample> samples
){
    List<CollectedShapeSample> result = new();
    for (int i = samples.Size(); i > 0u; --i)
    {
        result.Add(samples[i - 1]);
    }
    return result;
}
private static void ExpectNoShapeSamples(
    dynamic shape,
    dynamic desc
){
    List<CollectedShapeSample> samples = CollectShapeSamples(shape, desc);
    Check.That(samples.Size() == 0u);
}
private static void ExpectShapeSamplerSamplesMatch(
    dynamic shape,
    dynamic desc
){
    ExpectShapeSampleMatch(
        CollectShapeSamples(shape, desc),
        CollectShapeSamplerSamples(shape, desc)
    );
}
private static void ExpectNoCubicBeziers(dynamic shape){
    uint callback_count = 0;

    Check.That(shape.CubicBezierCountFast() == 0u);
    shape.SplitToCubicBeziersFast((Action<CubicBezier>)(unused=>++callback_count));
    Check.That(callback_count == 0u);

    callback_count = 0u;
    Check.That(shape.CubicBezierCount(0.1f) == 0u);
    shape.SplitToCubicBeziers(
        0.1f,
        (Action<CubicBezier>)(unused=>++callback_count)
    );
    Check.That(callback_count == 0u);
}
private static void ExpectNoToleranceCubicBeziers(dynamic shape){
    uint callback_count = 0;

    Check.That(shape.CubicBezierCount(0.1f) == 0u);
    shape.SplitToCubicBeziers(
        0.1f,
        (Action<CubicBezier>)(unused=>++callback_count)
    );
    Check.That(callback_count == 0u);
}
private static void ExpectToleranceCubicBezierCountMonotonic(dynamic shape){
    uint loose_count = shape.CubicBezierCount(1.0f);
    uint dense_count = shape.CubicBezierCount(0.05f);
    List<CubicBezier> loose_cubics = CollectCubicBeziers(shape, 1.0f);
    List<CubicBezier> dense_cubics = CollectCubicBeziers(shape, 0.05f);

    Check.That(dense_count >= loose_count);
    Check.That(loose_cubics.Size() == loose_count);
    Check.That(dense_cubics.Size() == dense_count);
}
private static void ExpectInvalidToleranceNoCubicBeziers(dynamic shape){





}
private static void ExpectCalcToleranceScaleResponse<T>()
{float Calc(float a,float b)=>(float)typeof(T).GetMethod("CalcTolerance")!.Invoke(null,new object[]{a,b})!;
 float value=Calc(1,1),higherTessellation=Calc(2,1),higherPixelRatio=Calc(1,2),higherBoth=Calc(2,2);
 Check.That(value>0);Check.That(higherTessellation<value);Check.That(higherPixelRatio<value);Check.That(higherBoth<higherTessellation);Check.That(higherBoth<higherPixelRatio);ExpectNear(Calc(0,1),value,kFloatEpsilon);}

[GuiTest("math/shape_tests.cpp::gui/math/Circle normalization relations and measures")]
public static void Case0(){

    Circle circle = new Circle(new Offsetf(1.0f, -2.0f), -3.0f);
    Check.False(circle.IsValid());

    Circle normalized = circle.Normalized();
    Check.That(normalized.IsValid());
    Check.That(normalized.IsNormalized());
    ExpectNear(normalized.Diameter(), 6.0f, kFloatEpsilon);
    ExpectNear(normalized.Length(), 6.0f * kPi, 0.001f);
    ExpectNear(normalized.Area(), 9.0f * kPi, 0.001f);
    Check.That(normalized.Contains(new Offsetf(1.0f, -2.0f)));
    CheckOffsetf(normalized.ClosestPoint(new Offsetf(1.0f, -2.0f)), 4.0f, -2.0f);
    ExpectNear(normalized.SignedDistance(new Offsetf(1.0f, -2.0f)), -3.0f, 0.001f);
    ExpectNear(normalized.Distance(new Offsetf(10.0f, -2.0f)), 6.0f, 0.001f);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape Invalid factories are consistently invalid")]
public static void Case1(){

    Circle circle = Circle.Invalid();
    Ellipse ellipse = Ellipse.Invalid();
    Arc arc = Arc.Invalid();
    EllipticalArc elliptical_arc = EllipticalArc.Invalid();
    QuadBezier quad = QuadBezier.Invalid();
    CubicBezier cubic = CubicBezier.Invalid();

    Check.False(circle.IsValid());
    Check.False(ellipse.IsValid());
    Check.False(arc.IsValid());
    Check.False(elliptical_arc.IsValid());
    Check.False(quad.IsValid());
    Check.False(cubic.IsValid());

    Check.False(circle.Normalized().IsValid());
    Check.False(ellipse.Normalized().IsValid());
    Check.False(arc.Normalized().IsValid());
    Check.False(elliptical_arc.Normalized().IsValid());

}
[GuiTest("math/shape_tests.cpp::gui/math/Ellipse normalization relations and measures")]
public static void Case2(){

    Ellipse ellipse = Ellipse.CenterRadius(
        Offsetf.Zero(),
        -4.0f,
        2.0f,
        (MathF.PI * 2) + 0.25f * kPi
    );
    Check.False(ellipse.IsValid());

    Ellipse normalized = ellipse.Normalized();
    Check.That(normalized.IsValid());
    Check.That(normalized.IsNormalized());
    ExpectNear(normalized.Area(), 8.0f * kPi, 0.001f);
    ExpectNear(normalized.Eccentricity(), MathF.Sqrt(0.75f), 0.001f);

    Ellipse axis = Ellipse.CenterRadius(Offsetf.Zero(), 4.0f, 2.0f, 0.0f);
    Check.That(axis.Contains(new Offsetf(3.0f, 0.0f)));
    Check.False(axis.Contains(new Offsetf(5.0f, 0.0f)));
    CheckOffsetf(axis.ClosestPoint(new Offsetf(10.0f, 0.0f), 0.001f), 4.0f, 0.0f);
    ExpectNear(axis.Distance(new Offsetf(10.0f, 0.0f), 0.001f), 6.0f, 0.01f);
    ExpectNear(axis.SignedDistance(Offsetf.Zero(), 0.001f), -2.0f, 0.05f);

}
[GuiTest("math/shape_tests.cpp::gui/math/Arc normalization length and closest point")]
public static void Case3(){

    Arc invalid = Arc.CenterRadius(
        Offsetf.Zero(),
        -10.0f,
        0.0f,
        0.5f * kPi
    );
    Check.False(invalid.IsValid());

    Arc normalized = invalid.Normalized();
    Check.That(normalized.IsValid());
    Check.That(normalized.IsNormalized());
    ExpectNear(normalized.Length(), 5.0f * kPi, 0.001f);

    Arc arc = Arc.CenterRadius(
        Offsetf.Zero(),
        10.0f,
        0.0f,
        0.5f * kPi
    );
    Check.That(arc.ContainsAngle(0.25f * kPi));
    Check.False(arc.ContainsAngle(kPi));
    CheckOffsetf(arc.ClosestPoint(new Offsetf(20.0f, -1.0f)), 10.0f, 0.0f);
    ExpectNear(arc.Distance(new Offsetf(20.0f, -1.0f)), MathF.Sqrt(101.0f), 0.01f);

}
[GuiTest("math/shape_tests.cpp::gui/math/EllipticalArc normalization length and closest point")]
public static void Case4(){

    EllipticalArc circular = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        5.0f,
        5.0f,
        0.0f,
        0.0f,
        0.5f * kPi
    );
    ExpectNear(circular.Length(0.001f), 2.5f * kPi, 0.05f);

    EllipticalArc arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        8.0f,
        4.0f,
        0.0f,
        0.0f,
        0.5f * kPi
    );
    Check.That(arc.ContainsAngle(0.25f * kPi));
    Check.False(arc.ContainsAngle(kPi));
    CheckOffsetf(arc.ClosestPoint(new Offsetf(10.0f, 0.0f), 0.001f), 8.0f, 0.0f);
    ExpectNear(arc.Distance(new Offsetf(10.0f, 0.0f), 0.001f), 2.0f, 0.01f);

    EllipticalArc invalid = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        8.0f,
        -4.0f,
        0.0f,
        0.0f,
        0.5f * kPi
    );
    Check.False(invalid.IsValid());
    Check.That(invalid.Normalized().IsValid());

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape cubic bezier fast split preserves full contour endpoints")]
public static void Case5(){

    Circle circle = Circle.CenterRadius(new Offsetf(2.0f, -3.0f), 5.0f);
    var circle_cubics = CollectCubicBeziersFast(circle);
    Check.That(circle.CubicBezierCountFast() == 4u);
    Check.That(circle_cubics.Size() == 4u);
    CheckOffsetf(
        circle_cubics.Front().StartPoint(),
        circle.PointAtAngle(0.0f).X,
        circle.PointAtAngle(0.0f).Y
    );
    CheckOffsetf(
        circle_cubics.Back().EndPoint(),
        circle.PointAtAngle(0.0f).X,
        circle.PointAtAngle(0.0f).Y
    );

    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(-2.0f, 4.0f),
        8.0f,
        3.0f,
        0.25f * kPi
    );
    var ellipse_cubics = CollectCubicBeziersFast(ellipse);
    Check.That(ellipse.CubicBezierCountFast() == 4u);
    Check.That(ellipse_cubics.Size() == 4u);
    CheckOffsetf(
        ellipse_cubics.Front().StartPoint(),
        ellipse.PointAtAngle(0.0f).X,
        ellipse.PointAtAngle(0.0f).Y
    );
    CheckOffsetf(
        ellipse_cubics[0].EndPoint(),
        ellipse.PointAtAngle(0.5f * kPi).X,
        ellipse.PointAtAngle(0.5f * kPi).Y
    );
    CheckOffsetf(
        ellipse_cubics[1].EndPoint(),
        ellipse.PointAtAngle(kPi).X,
        ellipse.PointAtAngle(kPi).Y
    );
    CheckOffsetf(
        ellipse_cubics[2].EndPoint(),
        ellipse.PointAtAngle(1.5f * kPi).X,
        ellipse.PointAtAngle(1.5f * kPi).Y
    );
    CheckOffsetf(
        ellipse_cubics.Back().EndPoint(),
        ellipse.PointAtAngle(0.0f).X,
        ellipse.PointAtAngle(0.0f).Y
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape cubic bezier counts match emitted segments")]
public static void Case6(){

    float[] tolerances = { 2.0f, 1.0f, 0.5f, 0.1f, 0.025f };
    uint[] max_depths = { 4u, 10u, 16u };


    {
        Circle[] shapes = {
            Circle.CenterRadius(new Offsetf(2.0f, -3.0f), 5.0f),
            Circle.CenterRadius(Offsetf.Zero(), 512.0f),
            Circle.Zero(),
            Circle.Invalid(),
        };
        foreach (Circle shape in shapes)
        {
            uint count = shape.CubicBezierCountFast();
            Check.That(CountSplitCubicBeziersFast(shape) == count);
            Check.That(
                CountSplitCubicBeziersFast(shape, EShapeSampleDirection.Forward) ==
                count
            );
            Check.That(
                CountSplitCubicBeziersFast(shape, EShapeSampleDirection.Reverse) ==
                count
            );
        }
    }

    {
        Ellipse[] shapes = {
            Ellipse.CenterRadius(new Offsetf(-2.0f, 4.0f), 8.0f, 3.0f, 0.25f * kPi),
            Ellipse.CenterRadius(Offsetf.Zero(), 512.0f, 48.0f, -0.15f * kPi),
            Ellipse.Zero(),
            Ellipse.Invalid(),
        };
        foreach (Ellipse shape in shapes)
        {
            uint count = shape.CubicBezierCountFast();
            Check.That(CountSplitCubicBeziersFast(shape) == count);
            Check.That(
                CountSplitCubicBeziersFast(shape, EShapeSampleDirection.Forward) ==
                count
            );
            Check.That(
                CountSplitCubicBeziersFast(shape, EShapeSampleDirection.Reverse) ==
                count
            );
        }
    }

    {
        Arc[] shapes = {
            Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.5f * kPi),
            Arc.CenterRadius(Offsetf.Zero(), 24.0f, 0.25f * kPi, -1.25f * kPi),
            Arc.CenterRadius(Offsetf.Zero(), 32.0f, -0.25f * kPi, 3.0f * kPi),
            Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.0f),
            Arc.Invalid(),
        };
        foreach (Arc shape in shapes)
        {
            uint count = shape.CubicBezierCountFast();
            Check.That(CountSplitCubicBeziersFast(shape) == count);
        }
    }

    {
        EllipticalArc[] shapes = {
            EllipticalArc.CenterRadius(Offsetf.Zero(), 8.0f, 4.0f, 0.0f, 0.0f, 0.5f * kPi),
            EllipticalArc.CenterRadius(
                Offsetf.Zero(),
                24.0f,
                7.0f,
                0.2f * kPi,
                0.25f * kPi,
                -1.25f * kPi
            ),
            EllipticalArc.CenterRadius(Offsetf.Zero(), 16.0f, 6.0f, 0.0f, 0.0f, 0.0f),
            EllipticalArc.Invalid(),
        };
        foreach (EllipticalArc shape in shapes)
        {
            uint count = shape.CubicBezierCountFast();
            Check.That(CountSplitCubicBeziersFast(shape) == count);
        }
    }


    {
        Circle[] shapes = {
            Circle.CenterRadius(new Offsetf(2.0f, -3.0f), 5.0f),
            Circle.CenterRadius(Offsetf.Zero(), 512.0f),
            Circle.Zero(),
            Circle.Invalid(),
        };
        foreach (Circle shape in shapes)
        {
            foreach (float tolerance in tolerances)
            {
                uint count = shape.CubicBezierCount(tolerance);
                Check.That(CountSplitCubicBeziers(shape, tolerance) == count);
                Check.That(
                    CountSplitCubicBeziers(
                        shape,
                        tolerance,
                        EShapeSampleDirection.Forward
                    ) == count
                );
                Check.That(
                    CountSplitCubicBeziers(
                        shape,
                        tolerance,
                        EShapeSampleDirection.Reverse
                    ) == count
                );
            }
        }
    }

    {
        Ellipse[] shapes = {
            Ellipse.CenterRadius(new Offsetf(-2.0f, 4.0f), 8.0f, 3.0f, 0.25f * kPi),
            Ellipse.CenterRadius(Offsetf.Zero(), 512.0f, 48.0f, -0.15f * kPi),
            Ellipse.Zero(),
            Ellipse.Invalid(),
        };
        foreach (Ellipse shape in shapes)
        {
            foreach (float tolerance in tolerances)
            {
                uint count = shape.CubicBezierCount(tolerance);
                Check.That(CountSplitCubicBeziers(shape, tolerance) == count);
                Check.That(
                    CountSplitCubicBeziers(
                        shape,
                        tolerance,
                        EShapeSampleDirection.Forward
                    ) == count
                );
                Check.That(
                    CountSplitCubicBeziers(
                        shape,
                        tolerance,
                        EShapeSampleDirection.Reverse
                    ) == count
                );
            }
        }
    }

    {
        Arc[] shapes = {
            Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.5f * kPi),
            Arc.CenterRadius(Offsetf.Zero(), 24.0f, 0.25f * kPi, -1.25f * kPi),
            Arc.CenterRadius(Offsetf.Zero(), 32.0f, -0.25f * kPi, 3.0f * kPi),
            Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.0f),
            Arc.Invalid(),
        };
        foreach (Arc shape in shapes)
        {
            foreach (float tolerance in tolerances)
            {
                uint count = shape.CubicBezierCount(tolerance);
                Check.That(CountSplitCubicBeziers(shape, tolerance) == count);
            }
        }
    }

    {
        EllipticalArc[] shapes = {
            EllipticalArc.CenterRadius(Offsetf.Zero(), 8.0f, 4.0f, 0.0f, 0.0f, 0.5f * kPi),
            EllipticalArc.CenterRadius(
                Offsetf.Zero(),
                24.0f,
                7.0f,
                0.2f * kPi,
                0.25f * kPi,
                -1.25f * kPi
            ),
            EllipticalArc.CenterRadius(Offsetf.Zero(), 16.0f, 6.0f, 0.0f, 0.0f, 0.0f),
            EllipticalArc.Invalid(),
        };
        foreach (EllipticalArc shape in shapes)
        {
            foreach (float tolerance in tolerances)
            {
                uint count = shape.CubicBezierCount(tolerance);
                Check.That(CountSplitCubicBeziers(shape, tolerance) == count);
            }
        }
    }


    {
        Superellipse[] shapes = {
            Superellipse.CenterRadius(new Offsetf(-2.0f, 4.0f), 8.0f, 3.0f, 0.25f * kPi, 4.0f),
            Superellipse.CenterRadius(new Offsetf(3.0f, -1.0f), 6.0f, 5.0f, -0.15f * kPi, 0.75f),
            Superellipse.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 1.0f),
            Superellipse.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 2.0f),
            Superellipse.Zero(),
            Superellipse.Invalid(),
        };
        foreach (Superellipse shape in shapes)
        {
            foreach (float tolerance in tolerances)
            {
                uint count = shape.CubicBezierCount(tolerance);
                Check.That(CountSplitCubicBeziers(shape, tolerance) == count);
                Check.That(
                    CountSplitCubicBeziers(
                        shape,
                        tolerance,
                        EShapeSampleDirection.Forward
                    ) == count
                );
                Check.That(
                    CountSplitCubicBeziers(
                        shape,
                        tolerance,
                        EShapeSampleDirection.Reverse
                    ) == count
                );

                foreach (uint max_depth in max_depths)
                {
                    uint depth_count = shape.CubicBezierCount(tolerance, max_depth);
                    Check.That(
                        CountSplitCubicBeziers(shape, tolerance, max_depth) == depth_count
                    );
                    Check.That(
                        CountSplitCubicBeziers(
                            shape,
                            tolerance,
                            EShapeSampleDirection.Forward,
                            max_depth
                        ) == depth_count
                    );
                    Check.That(
                        CountSplitCubicBeziers(
                            shape,
                            tolerance,
                            EShapeSampleDirection.Reverse,
                            max_depth
                        ) == depth_count
                    );
                }
            }
        }
    }

    {
        SuperellipseArc[] shapes = {
            SuperellipseArc.CenterRadius(
                new Offsetf(3.0f, -2.0f),
                12.0f,
                5.0f,
                0.25f * kPi,
                0.15f * kPi,
                1.35f * kPi,
                4.0f
            ),
            SuperellipseArc.CenterRadius(
                new Offsetf(-4.0f, 1.0f),
                9.0f,
                2.0f,
                -0.2f * kPi,
                0.25f * kPi,
                0.95f * kPi,
                0.75f
            ),
            SuperellipseArc.CenterRadius(
                new Offsetf(2.0f, 3.0f),
                7.0f,
                4.0f,
                0.1f * kPi,
                0.75f * kPi,
                -1.1f * kPi,
                3.5f
            ),
            SuperellipseArc.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 0.0f, kPi, 1.0f),
            SuperellipseArc.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 0.0f, kPi, 2.0f),
            SuperellipseArc.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 0.0f, 0.0f, 4.0f),
            SuperellipseArc.Invalid(),
        };
        foreach (SuperellipseArc shape in shapes)
        {
            foreach (float tolerance in tolerances)
            {
                uint count = shape.CubicBezierCount(tolerance);
                Check.That(CountSplitCubicBeziers(shape, tolerance) == count);

                foreach (uint max_depth in max_depths)
                {
                    uint depth_count = shape.CubicBezierCount(tolerance, max_depth);
                    Check.That(
                        CountSplitCubicBeziers(shape, tolerance, max_depth) == depth_count
                    );
                }
            }
        }
    }


    {
        Superellipse[] shapes = {
            Superellipse.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 0.75f),
            Superellipse.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 1.0f),
            Superellipse.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 2.0f),
            Superellipse.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 4.0f),
            Superellipse.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 4.5f),
            Superellipse.Zero(),
            Superellipse.Invalid(),
        };
        uint[] expected_counts = { 8u, 4u, 4u, 12u, 12u, 0u, 0u };
        for (uint i = 0; i < shapes.Length; ++i)
        {
            uint count = shapes[i].CubicBezierCountFast();
            Check.That(count == expected_counts[i]);
            Check.That(CountSplitCubicBeziersFast(shapes[i]) == count);
            Check.That(
                CountSplitCubicBeziersFast(shapes[i], EShapeSampleDirection.Forward) ==
                count
            );
            Check.That(
                CountSplitCubicBeziersFast(shapes[i], EShapeSampleDirection.Reverse) ==
                count
            );
        }
    }

    {
        SuperellipseArc[] shapes = {
            SuperellipseArc.CenterRadius(
                Offsetf.Zero(),
                145.0f,
                90.0f,
                -0.12f * kPi,
                0.10f * kPi,
                1.25f * kPi,
                0.75f
            ),
            SuperellipseArc.CenterRadius(
                Offsetf.Zero(),
                145.0f,
                90.0f,
                -0.12f * kPi,
                0.10f * kPi,
                1.25f * kPi,
                1.0f
            ),
            SuperellipseArc.CenterRadius(
                Offsetf.Zero(),
                145.0f,
                90.0f,
                -0.12f * kPi,
                0.10f * kPi,
                1.25f * kPi,
                2.0f
            ),
            SuperellipseArc.CenterRadius(
                Offsetf.Zero(),
                145.0f,
                90.0f,
                -0.12f * kPi,
                0.10f * kPi,
                1.25f * kPi,
                4.0f
            ),
            SuperellipseArc.CenterRadius(Offsetf.Zero(), 9.0f, 6.0f, 0.0f, 0.0f, 0.0f, 4.0f),
            SuperellipseArc.Invalid(),
        };
        foreach (SuperellipseArc shape in shapes)
        {
            uint count = shape.CubicBezierCountFast();
            Check.That(CountSplitCubicBeziersFast(shape) == count);
        }
    }

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape samplers match point sampling")]
public static void Case7(){

    Circle circle = Circle.CenterRadius(new Offsetf(2.0f, -3.0f), 5.0f);
    CircleSampler circle_sampler = circle.CreateSampler();
    CheckOffsetf(circle_sampler.SamplePoint(circle.Center, 7.0f), 9.0f, -3.0f);
    circle_sampler.SetAngle(0.5f * kPi);
    CheckOffsetf(circle_sampler.SamplePoint(circle.Center, 7.0f), 2.0f, 4.0f);
    CheckOffsetf(
        circle.PointAtAngle(0.5f * kPi),
        circle_sampler.SamplePoint(circle.Center, circle.Radius).X,
        circle_sampler.SamplePoint(circle.Center, circle.Radius).Y
    );

    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(1.0f, 2.0f),
        3.0f,
        5.0f,
        0.5f * kPi
    );
    EllipseSampler ellipse_sampler = ellipse.CreateSampler();
    CheckOffsetf(ellipse_sampler.SamplePoint(ellipse.Center, 3.0f, 5.0f), 1.0f, 5.0f);
    ellipse_sampler.SetAngle(0.5f * kPi);
    CheckOffsetf(ellipse_sampler.SamplePoint(ellipse.Center, 3.0f, 5.0f), -4.0f, 2.0f);
    CheckOffsetf(
        ellipse.PointAtAngle(0.5f * kPi),
        ellipse_sampler.SamplePoint(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY).X,
        ellipse_sampler.SamplePoint(ellipse.Center, ellipse.RadiusX, ellipse.RadiusY).Y
    );

    Arc arc = Arc.CenterRadius(new Offsetf(1.0f, 2.0f), 3.0f, 0.0f, kPi);
    CircleSampler arc_sampler = arc.CreateSampler();
    CheckOffsetf(arc_sampler.SamplePoint(arc.Center, 4.0f), 5.0f, 2.0f);
    arc_sampler.SetAngle(0.5f * kPi);
    CheckOffsetf(arc_sampler.SamplePoint(arc.Center, 4.0f), 1.0f, 6.0f);
    CheckOffsetf(
        arc.PointAtAngle(0.5f * kPi),
        arc_sampler.SamplePoint(arc.Center, arc.Radius).X,
        arc_sampler.SamplePoint(arc.Center, arc.Radius).Y
    );

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        new Offsetf(1.0f, 2.0f),
        3.0f,
        5.0f,
        0.5f * kPi,
        0.0f,
        kPi
    );
    EllipseSampler elliptical_arc_sampler = elliptical_arc.CreateSampler();
    CheckOffsetf(elliptical_arc_sampler.SamplePoint(elliptical_arc.Center, 3.0f, 5.0f), 1.0f, 5.0f);
    elliptical_arc_sampler.SetAngle(0.5f * kPi);
    CheckOffsetf(elliptical_arc_sampler.SamplePoint(elliptical_arc.Center, 3.0f, 5.0f), -4.0f, 2.0f);
    CheckOffsetf(
        elliptical_arc.PointAtAngle(0.5f * kPi),
        elliptical_arc_sampler.SamplePoint(elliptical_arc.Center, elliptical_arc.RadiusX, elliptical_arc.RadiusY).X,
        elliptical_arc_sampler.SamplePoint(elliptical_arc.Center, elliptical_arc.RadiusX, elliptical_arc.RadiusY).Y
    );

    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(1.0f, 2.0f),
        3.0f,
        5.0f,
        0.5f * kPi,
        4.0f
    );
    SuperellipseSampler superellipse_sampler = superellipse.CreateSampler();
    CheckOffsetf(superellipse_sampler.SamplePoint(superellipse.Center, 3.0f, 5.0f), 1.0f, 5.0f);
    superellipse_sampler.SetAngle(0.5f * kPi);
    CheckOffsetf(superellipse_sampler.SamplePoint(superellipse.Center, 3.0f, 5.0f), -4.0f, 2.0f);
    CheckOffsetf(
        superellipse.PointAtAngle(0.5f * kPi),
        superellipse_sampler.SamplePoint(superellipse.Center, superellipse.RadiusX, superellipse.RadiusY).X,
        superellipse_sampler.SamplePoint(superellipse.Center, superellipse.RadiusX, superellipse.RadiusY).Y
    );

    SuperellipseArc superellipse_arc = SuperellipseArc.CenterRadius(
        new Offsetf(1.0f, 2.0f),
        3.0f,
        5.0f,
        0.5f * kPi,
        0.0f,
        kPi,
        4.0f
    );
    SuperellipseSampler superellipse_arc_sampler = superellipse_arc.CreateSampler();
    CheckOffsetf(superellipse_arc_sampler.SamplePoint(superellipse_arc.Center, 3.0f, 5.0f), 1.0f, 5.0f);
    superellipse_arc_sampler.SetAngle(0.5f * kPi);
    CheckOffsetf(superellipse_arc_sampler.SamplePoint(superellipse_arc.Center, 3.0f, 5.0f), -4.0f, 2.0f);
    CheckOffsetf(
        superellipse_arc.PointAtAngle(0.5f * kPi),
        superellipse_arc_sampler.SamplePoint(superellipse_arc.Center, superellipse_arc.RadiusX, superellipse_arc.RadiusY).X,
        superellipse_arc_sampler.SamplePoint(superellipse_arc.Center, superellipse_arc.RadiusX, superellipse_arc.RadiusY).Y
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/ShapeSampleHelper sampler callbacks expose synced sampler")]
public static void Case8(){

    Circle circle = Circle.CenterRadius(new Offsetf(2.0f, -3.0f), 8.0f);
    ShapeSampleEvaluator<CircleSampler> eval = (ref CircleSampler sampler, float t) => {
        sampler.SetAngle(t * (MathF.PI * 2));
        return sampler.SamplePoint(circle.Center, circle.Radius);
    };
    var CheckSample = (Offsetf point, float t, CircleSampler sampler) => {
        ExpectNear(sampler.Angle(), t * (MathF.PI * 2), 0.001f);
        CheckOffsetf(
            point,
            sampler.SamplePoint(circle.Center, circle.Radius).X,
            sampler.SamplePoint(circle.Center, circle.Radius).Y
        );
    };

    CircleSampler uniform_sampler = circle.CreateSampler();
    uint uniform_count = 0;
    ShapeSampleHelper.SampleClosedUniformWithSampler(
        4u,
        EShapeSampleDirection.Forward,
        ref uniform_sampler,
        eval,
        (Offsetf point, float t, CircleSampler sampler) => {
            CheckSample(point, t, sampler);
            ++uniform_count;
        }
    );
    Check.That(uniform_count == 4u);

    CircleSampler open_uniform_sampler = circle.CreateSampler();
    uint open_uniform_count = 0;
    ShapeSampleHelper.SampleOpenUniformWithSampler(
        4u,
        EShapeSampleDirection.Forward,
        ref open_uniform_sampler,
        eval,
        (Offsetf point, float t, CircleSampler sampler) => {
            CheckSample(point, t, sampler);
            ++open_uniform_count;
        }
    );
    Check.That(open_uniform_count == 5u);

    CircleSampler adaptive_sampler = circle.CreateSampler();
    uint adaptive_count = 0;
    ShapeSampleHelper.SampleClosedAdaptiveWithSampler(
        0.1f,
        8u,
        EShapeSampleDirection.Forward,
        ref adaptive_sampler,
        eval,
        (Offsetf point, float t, CircleSampler sampler) => {
            CheckSample(point, t, sampler);
            ++adaptive_count;
        }
    );
    Check.That(adaptive_count > 4u);

    CircleSampler open_adaptive_sampler = circle.CreateSampler();
    uint open_adaptive_count = 0;
    ShapeSampleHelper.SampleOpenAdaptiveWithSampler(
        0.1f,
        8u,
        EShapeSampleDirection.Forward,
        ref open_adaptive_sampler,
        eval,
        (Offsetf point, float t, CircleSampler sampler) => {
            CheckSample(point, t, sampler);
            ++open_adaptive_count;
        }
    );
    Check.That(open_adaptive_count > 4u);

    CircleSampler step_sampler = circle.CreateSampler();
    uint step_count = 0;
    ShapeSampleHelper.SampleClosedByStepLengthWithSampler(
        4.0f,
        0.1f,
        8u,
        EShapeSampleDirection.Forward,
        ref step_sampler,
        eval,
        (Offsetf point, float t, CircleSampler sampler) => {
            CheckSample(point, t, sampler);
            ++step_count;
        }
    );
    Check.That(step_count > 4u);

    CircleSampler open_step_sampler = circle.CreateSampler();
    uint open_step_count = 0;
    ShapeSampleHelper.SampleOpenByStepLengthWithSampler(
        4.0f,
        0.1f,
        8u,
        EShapeSampleDirection.Forward,
        ref open_step_sampler,
        eval,
        (Offsetf point, float t, CircleSampler sampler) => {
            CheckSample(point, t, sampler);
            ++open_step_count;
        }
    );
    Check.That(open_step_count > 4u);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape sampler sampling entries match regular sampling")]
public static void Case9(){

    ShapeSegmentCountSampleDesc closed_segment_desc = new ShapeSegmentCountSampleDesc(8u,
        EShapeSampleDirection.Forward);
    ShapeSegmentCountSampleDesc open_segment_desc = new ShapeSegmentCountSampleDesc(8u,
        EShapeSampleDirection.Forward);
    ShapeToleranceSampleDesc tolerance_desc = new ShapeToleranceSampleDesc(0.1f,
        EShapeSampleDirection.Forward,
        8u);
    ShapeStepLengthSampleDesc step_desc = new ShapeStepLengthSampleDesc(2.0f,
        EShapeSampleDirection.Forward,
        8u);

    Circle circle = Circle.CenterRadius(new Offsetf(2.0f, -3.0f), 5.0f);
    ExpectShapeSamplerSamplesMatch(circle, closed_segment_desc);
    ExpectShapeSamplerSamplesMatch(circle, tolerance_desc);
    ExpectShapeSamplerSamplesMatch(circle, step_desc);

    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(1.0f, 2.0f),
        6.0f,
        3.0f,
        0.25f * kPi
    );
    ExpectShapeSamplerSamplesMatch(ellipse, closed_segment_desc);
    ExpectShapeSamplerSamplesMatch(ellipse, tolerance_desc);
    ExpectShapeSamplerSamplesMatch(ellipse, step_desc);

    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(-2.0f, 4.0f),
        6.0f,
        3.0f,
        0.25f * kPi,
        4.0f
    );
    ExpectShapeSamplerSamplesMatch(superellipse, closed_segment_desc);
    ExpectShapeSamplerSamplesMatch(superellipse, tolerance_desc);
    ExpectShapeSamplerSamplesMatch(superellipse, step_desc);

    Arc arc = Arc.CenterRadius(
        new Offsetf(1.0f, 2.0f),
        5.0f,
        0.1f * kPi,
        0.75f * kPi
    );
    ExpectShapeSamplerSamplesMatch(arc, open_segment_desc);
    ExpectShapeSamplerSamplesMatch(arc, tolerance_desc);
    ExpectShapeSamplerSamplesMatch(arc, step_desc);

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        new Offsetf(1.0f, -2.0f),
        6.0f,
        3.0f,
        0.2f * kPi,
        0.1f * kPi,
        0.75f * kPi
    );
    ExpectShapeSamplerSamplesMatch(elliptical_arc, open_segment_desc);
    ExpectShapeSamplerSamplesMatch(elliptical_arc, tolerance_desc);
    ExpectShapeSamplerSamplesMatch(elliptical_arc, step_desc);

    SuperellipseArc superellipse_arc = SuperellipseArc.CenterRadius(
        new Offsetf(-1.0f, 3.0f),
        6.0f,
        3.0f,
        0.2f * kPi,
        0.1f * kPi,
        0.75f * kPi,
        4.0f
    );
    ExpectShapeSamplerSamplesMatch(superellipse_arc, open_segment_desc);
    ExpectShapeSamplerSamplesMatch(superellipse_arc, tolerance_desc);
    ExpectShapeSamplerSamplesMatch(superellipse_arc, step_desc);

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse tolerance sampling splits closed contour by quadrants")]
public static void Case10(){

    Superellipse shape = Superellipse.CenterRadius(
        new Offsetf(-2.0f, 4.0f),
        96.0f,
        48.0f,
        0.12f * kPi,
        4.0f
    );
    ShapeToleranceSampleDesc forward_desc = new ShapeToleranceSampleDesc(0.5f,
        EShapeSampleDirection.Forward,
        10u);
    ShapeToleranceSampleDesc reverse_desc = new ShapeToleranceSampleDesc(0.5f,
        EShapeSampleDirection.Reverse,
        10u);

    List<CollectedShapeSample> forward_samples = CollectShapeSamples(shape, forward_desc);
    List<CollectedShapeSample> reverse_samples = CollectShapeSamples(shape, reverse_desc);
    Check.That(forward_samples.Size() > 4u);
    Check.That(reverse_samples.Size() == forward_samples.Size());


    bool has_quarter = false;
    bool has_half = false;
    bool has_three_quarter = false;
    for (int i = 0; i < forward_samples.Size(); ++i)
    {
        float t = forward_samples[i].T;
        Check.That(t >= 0.0f);
        Check.That(t < 1.0f);
        if (i > 0u)
        {
            Check.That(t > forward_samples[i - 1].T);
        }

        has_quarter = has_quarter || MathF.Abs(t - 0.25f) <= 0.0001f;
        has_half = has_half || MathF.Abs(t - 0.5f) <= 0.0001f;
        has_three_quarter = has_three_quarter || MathF.Abs(t - 0.75f) <= 0.0001f;
    }
    Check.That(has_quarter);
    Check.That(has_half);
    Check.That(has_three_quarter);


    for (int i = 0; i < reverse_samples.Size(); ++i)
    {
        float t = reverse_samples[i].T;
        Check.That(t >= 0.0f);
        Check.That(t < 1.0f);
        if (i > 0u)
        {
            Check.That(t < reverse_samples[i - 1].T);
        }
    }
    ExpectShapeSampleMatch(ReverseShapeSamples(forward_samples), reverse_samples);


    int sampler_count = 0;
    shape.SampleWithSampler(
        forward_desc,
        (Offsetf point, float t, SuperellipseSampler sampler) => {
            ExpectNear(sampler.Angle(), t * (MathF.PI * 2), 0.001f);
            CheckOffsetf(
                point,
                sampler.SamplePoint(shape.Center, shape.RadiusX, shape.RadiusY).X,
                sampler.SamplePoint(shape.Center, shape.RadiusX, shape.RadiusY).Y
            );
            ++sampler_count;
        }
    );
    Check.That(sampler_count == forward_samples.Size());

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse tolerance sampling handles diamond exponent")]
public static void Case11(){

    Superellipse shape = Superellipse.CenterRadius(
        new Offsetf(3.0f, -2.0f),
        80.0f,
        48.0f,
        0.2f * kPi,
        1.0f
    );
    ShapeToleranceSampleDesc forward_desc = new ShapeToleranceSampleDesc(0.01f,
        EShapeSampleDirection.Forward,
        10u);
    ShapeToleranceSampleDesc reverse_desc = new ShapeToleranceSampleDesc(0.01f,
        EShapeSampleDirection.Reverse,
        10u);

    List<CollectedShapeSample> forward_samples = CollectShapeSamples(shape, forward_desc);
    List<CollectedShapeSample> reverse_samples = CollectShapeSamples(shape, reverse_desc);
    float[] expected_t = { 0.0f, 0.25f, 0.5f, 0.75f };


    Check.That(shape.EstimateSegmentCount(forward_desc.Tolerance) == 4u);
    Check.That(forward_samples.Size() == 4u);
    Check.That(reverse_samples.Size() == 4u);
    for (int i = 0; i < forward_samples.Size(); ++i)
    {
        ExpectNear(forward_samples[i].T, expected_t[i], kFloatEpsilon);
        CheckOffsetf(
            forward_samples[i].Point,
            shape.PointAtT(expected_t[i]).X,
            shape.PointAtT(expected_t[i]).Y
        );
    }
    ExpectShapeSampleMatch(ReverseShapeSamples(forward_samples), reverse_samples);

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc tolerance sampling splits by half quadrants")]
public static void Case12(){

    SuperellipseArc arc = SuperellipseArc.CenterRadius(
        new Offsetf(-2.0f, 4.0f),
        124.0f,
        72.0f,
        0.25f,
        -0.45f,
        1.45f * kPi,
        4.0f
    );
    ShapeToleranceSampleDesc forward_desc = new ShapeToleranceSampleDesc(0.5f,
        EShapeSampleDirection.Forward,
        10u);
    ShapeToleranceSampleDesc reverse_desc = new ShapeToleranceSampleDesc(0.5f,
        EShapeSampleDirection.Reverse,
        10u);

    List<CollectedShapeSample> forward_samples = CollectShapeSamples(arc, forward_desc);
    List<CollectedShapeSample> reverse_samples = CollectShapeSamples(arc, reverse_desc);
    Check.That(forward_samples.Size() > 4u);
    Check.That(forward_samples.Size() <= 96u);
    Check.That(reverse_samples.Size() == forward_samples.Size());
    CheckOffsetf(forward_samples.Front().Point, arc.StartPoint().X, arc.StartPoint().Y);
    CheckOffsetf(forward_samples.Back().Point, arc.EndPoint().X, arc.EndPoint().Y);



    bool has_zero_axis = false;
    bool has_half_pi_axis = false;
    bool has_pi_axis = false;
    for (int i = 0; i < forward_samples.Size(); ++i)
    {
        float t = forward_samples[i].T;
        Check.That(t >= 0.0f);
        Check.That(t <= 1.0f);
        if (i > 0u)
        {
            Check.That(t > forward_samples[i - 1].T);
        }

        has_zero_axis = has_zero_axis || MathF.Abs(t - ((0.0f - arc.StartAngle) / arc.SweepAngle)) <= 0.0001f;
        has_half_pi_axis = has_half_pi_axis || MathF.Abs(t - ((0.5f * kPi - arc.StartAngle) / arc.SweepAngle)) <= 0.0001f;
        has_pi_axis = has_pi_axis || MathF.Abs(t - ((kPi - arc.StartAngle) / arc.SweepAngle)) <= 0.0001f;
    }
    Check.That(has_zero_axis);
    Check.That(has_half_pi_axis);
    Check.That(has_pi_axis);
    ExpectShapeSampleMatch(ReverseShapeSamples(forward_samples), reverse_samples);

    int sampler_count = 0;
    arc.SampleWithSampler(
        forward_desc,
        (Offsetf point, float t, SuperellipseSampler sampler) => {
            ExpectNear(sampler.Angle(), arc.StartAngle + arc.SweepAngle * t, 0.001f);
            Offsetf sampler_point = sampler.SamplePoint(
                arc.Center,
                arc.RadiusX,
                arc.RadiusY
            );
            ExpectNear(point.X, sampler_point.X, 0.001f);
            ExpectNear(point.Y, sampler_point.Y, 0.001f);
            ++sampler_count;
        }
    );
    Check.That(sampler_count == forward_samples.Size());

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc tolerance sampling handles diamond exponent")]
public static void Case13(){

    SuperellipseArc arc = SuperellipseArc.CenterRadius(
        new Offsetf(-2.0f, 4.0f),
        124.0f,
        72.0f,
        0.25f,
        -0.45f,
        1.45f * kPi,
        1.0f
    );
    ShapeToleranceSampleDesc forward_desc = new ShapeToleranceSampleDesc(0.01f,
        EShapeSampleDirection.Forward,
        10u);
    ShapeToleranceSampleDesc reverse_desc = new ShapeToleranceSampleDesc(0.01f,
        EShapeSampleDirection.Reverse,
        10u);

    List<CollectedShapeSample> forward_samples = CollectShapeSamples(arc, forward_desc);
    List<CollectedShapeSample> reverse_samples = CollectShapeSamples(arc, reverse_desc);


    Check.That(arc.EstimateSegmentCount(forward_desc.Tolerance) == 4u);
    Check.That(forward_samples.Size() == 5u);
    Check.That(reverse_samples.Size() == 5u);
    CheckOffsetf(forward_samples.Front().Point, arc.StartPoint().X, arc.StartPoint().Y);
    CheckOffsetf(forward_samples.Back().Point, arc.EndPoint().X, arc.EndPoint().Y);
    ExpectNear(forward_samples.Front().T, 0.0f, kFloatEpsilon);
    ExpectNear(forward_samples.Back().T, 1.0f, kFloatEpsilon);

    bool has_zero_axis = false;
    bool has_half_pi_axis = false;
    bool has_pi_axis = false;
    for (int i = 0; i < forward_samples.Size(); ++i)
    {
        float t = forward_samples[i].T;
        if (i > 0u)
        {
            Check.That(t > forward_samples[i - 1].T);
        }

        has_zero_axis = has_zero_axis || MathF.Abs(t - ((0.0f - arc.StartAngle) / arc.SweepAngle)) <= 0.0001f;
        has_half_pi_axis = has_half_pi_axis || MathF.Abs(t - ((0.5f * kPi - arc.StartAngle) / arc.SweepAngle)) <= 0.0001f;
        has_pi_axis = has_pi_axis || MathF.Abs(t - ((kPi - arc.StartAngle) / arc.SweepAngle)) <= 0.0001f;
    }
    Check.That(has_zero_axis);
    Check.That(has_half_pi_axis);
    Check.That(has_pi_axis);
    ExpectShapeSampleMatch(ReverseShapeSamples(forward_samples), reverse_samples);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape cubic bezier split supports closed shape direction")]
public static void Case14(){

    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 5.0f);
    var circle_cw = CollectCubicBeziersFast(circle, EShapeSampleDirection.Forward);
    var circle_ccw = CollectCubicBeziersFast(circle, EShapeSampleDirection.Reverse);
    Check.That(circle_cw.Size() == circle_ccw.Size());
    Check.That(circle_cw.Size() == 4u);
    CheckOffsetf(circle_cw.Front().StartPoint(), 5.0f, 0.0f);
    CheckOffsetf(circle_ccw.Front().StartPoint(), 5.0f, 0.0f);
    Check.That(circle_cw.Front().ControlPoint1().Y > 0.0f);
    Check.That(circle_ccw.Front().ControlPoint1().Y < 0.0f);

    Ellipse ellipse = Ellipse.CenterRadius(Offsetf.Zero(), 8.0f, 4.0f, 0.0f);
    List<CubicBezier> ellipse_cw = CollectCubicBeziers(ellipse, 0.1f, EShapeSampleDirection.Forward);
    List<CubicBezier> ellipse_ccw = CollectCubicBeziers(ellipse, 0.1f, EShapeSampleDirection.Reverse);
    Check.That(ellipse_cw.Size() == ellipse_ccw.Size());
    Check.That(ellipse_cw.Size() > 0u);
    CheckOffsetf(ellipse_cw.Front().StartPoint(), 8.0f, 0.0f);
    CheckOffsetf(ellipse_ccw.Front().StartPoint(), 8.0f, 0.0f);
    Check.That(ellipse_cw.Front().ControlPoint1().Y > 0.0f);
    Check.That(ellipse_ccw.Front().ControlPoint1().Y < 0.0f);

}
[GuiTest("math/shape_tests.cpp::gui/math/Arc cubic bezier fast split respects sweep bounds")]
public static void Case15(){

    Arc half_arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, kPi);
    var half_cubics = CollectCubicBeziersFast(half_arc);
    Check.That(half_arc.CubicBezierCountFast() == 2u);
    Check.That(half_cubics.Size() == 2u);
    CheckOffsetf(
        half_cubics.Front().StartPoint(),
        half_arc.StartPoint().X,
        half_arc.StartPoint().Y
    );
    CheckOffsetf(
        half_cubics.Back().EndPoint(),
        half_arc.EndPoint().X,
        half_arc.EndPoint().Y
    );

    Arc negative_arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, -kPi);
    var negative_cubics = CollectCubicBeziersFast(negative_arc);
    Check.That(negative_arc.CubicBezierCountFast() == 2u);
    Check.That(negative_cubics.Size() == 2u);
    CheckOffsetf(
        negative_cubics.Front().StartPoint(),
        negative_arc.StartPoint().X,
        negative_arc.StartPoint().Y
    );
    CheckOffsetf(
        negative_cubics.Front().EndPoint(),
        negative_arc.PointAtT(0.5f).X,
        negative_arc.PointAtT(0.5f).Y
    );
    CheckOffsetf(
        negative_cubics.Back().EndPoint(),
        negative_arc.EndPoint().X,
        negative_arc.EndPoint().Y
    );

    Arc over_full_arc = Arc.CenterRadius(
        Offsetf.Zero(),
        10.0f,
        0.0f,
        3.0f * kPi
    );
    var over_full_cubics = CollectCubicBeziersFast(over_full_arc);
    Check.That(over_full_arc.CubicBezierCountFast() <= 4u);
    Check.That(over_full_cubics.Size() == over_full_arc.CubicBezierCountFast());

}
[GuiTest("math/shape_tests.cpp::gui/math/EllipticalArc cubic bezier fast split preserves endpoints")]
public static void Case16(){

    EllipticalArc quarter_arc = EllipticalArc.CenterRadius(
        new Offsetf(3.0f, -2.0f),
        12.0f,
        5.0f,
        0.25f * kPi,
        0.0f,
        0.5f * kPi
    );
    var quarter_cubics = CollectCubicBeziersFast(quarter_arc);
    Check.That(quarter_arc.CubicBezierCountFast() == 1u);
    Check.That(quarter_cubics.Size() == 1u);
    CheckOffsetf(
        quarter_cubics.Front().StartPoint(),
        quarter_arc.StartPoint().X,
        quarter_arc.StartPoint().Y
    );
    CheckOffsetf(
        quarter_cubics.Back().EndPoint(),
        quarter_arc.EndPoint().X,
        quarter_arc.EndPoint().Y
    );

    EllipticalArc half_arc = EllipticalArc.CenterRadius(
        new Offsetf(-4.0f, 1.0f),
        9.0f,
        2.0f,
        -0.2f * kPi,
        0.25f * kPi,
        kPi
    );
    var half_cubics = CollectCubicBeziersFast(half_arc);
    Check.That(half_arc.CubicBezierCountFast() == 2u);
    Check.That(half_cubics.Size() == 2u);
    CheckOffsetf(
        half_cubics.Front().StartPoint(),
        half_arc.StartPoint().X,
        half_arc.StartPoint().Y
    );
    CheckOffsetf(
        half_cubics.Back().EndPoint(),
        half_arc.EndPoint().X,
        half_arc.EndPoint().Y
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape cubic bezier tolerance split handles density and invalid input")]
public static void Case17(){

    ExpectToleranceCubicBezierCountMonotonic(
        Circle.CenterRadius(Offsetf.Zero(), 10.0f)
    );
    ExpectToleranceCubicBezierCountMonotonic(
        Ellipse.CenterRadius(Offsetf.Zero(), 12.0f, 4.0f, 0.25f * kPi)
    );
    ExpectToleranceCubicBezierCountMonotonic(
        Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.75f * kPi)
    );
    ExpectToleranceCubicBezierCountMonotonic(
        EllipticalArc.CenterRadius(
            Offsetf.Zero(),
            12.0f,
            4.0f,
            0.25f * kPi,
            0.0f,
            0.75f * kPi
        )
    );

    ExpectInvalidToleranceNoCubicBeziers(
        Circle.CenterRadius(Offsetf.Zero(), 10.0f)
    );
    ExpectInvalidToleranceNoCubicBeziers(
        Ellipse.CenterRadius(Offsetf.Zero(), 12.0f, 4.0f, 0.25f * kPi)
    );
    ExpectInvalidToleranceNoCubicBeziers(
        Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.75f * kPi)
    );
    ExpectInvalidToleranceNoCubicBeziers(
        EllipticalArc.CenterRadius(
            Offsetf.Zero(),
            12.0f,
            4.0f,
            0.25f * kPi,
            0.0f,
            0.75f * kPi
        )
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape cubic bezier split skips empty and invalid primitives")]
public static void Case18(){

    ExpectNoCubicBeziers(Circle.Zero());
    ExpectNoCubicBeziers(Circle.Invalid());
    ExpectNoCubicBeziers(Ellipse.Zero());
    ExpectNoCubicBeziers(Ellipse.Invalid());
    ExpectNoCubicBeziers(Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.0f));
    ExpectNoCubicBeziers(Arc.Invalid());
    ExpectNoCubicBeziers(
        EllipticalArc.CenterRadius(Offsetf.Zero(), 8.0f, 4.0f, 0.0f, 0.0f, 0.0f)
    );
    ExpectNoCubicBeziers(EllipticalArc.Invalid());

}
[GuiTest("math/shape_tests.cpp::gui/math/QuadBezier length and closest point")]
public static void Case19(){

    QuadBezier line = QuadBezier.Line(new Offsetf(0.0f, 0.0f), new Offsetf(10.0f, 0.0f));
    Check.That(line.IsValid());
    ExpectNear(line.Length(0.001f), 10.0f, 0.01f);
    CheckOffsetf(line.ClosestPoint(new Offsetf(3.0f, 4.0f), 0.001f), 3.0f, 0.0f);
    ExpectNear(line.Distance(new Offsetf(3.0f, 4.0f), 0.001f), 4.0f, 0.01f);

    QuadBezier curve = new QuadBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(5.0f, 10.0f),
        new Offsetf(10.0f, 0.0f)
    );
    Check.That(curve.Length(0.001f) > 10.0f);
    Offsetf midpoint = curve.PointAt(0.5f);
    Offsetf closest = curve.ClosestPoint(midpoint, 0.001f);
    ExpectNear((closest - midpoint).Length(), 0.0f, 0.05f);

}
[GuiTest("math/shape_tests.cpp::gui/math/Bezier path-style factories match direct construction")]
public static void Case20(){

    QuadBezier quad = QuadBezier.QuadTo(
        new Offsetf(1.0f, 2.0f),
        new Offsetf(3.0f, 4.0f),
        new Offsetf(5.0f, 6.0f)
    );
    Check.That(
        quad ==
        new QuadBezier(
            new Offsetf(1.0f, 2.0f),
            new Offsetf(3.0f, 4.0f),
            new Offsetf(5.0f, 6.0f)
        )
    );

    CubicBezier cubic = CubicBezier.CubicTo(
        new Offsetf(1.0f, 2.0f),
        new Offsetf(3.0f, 4.0f),
        new Offsetf(5.0f, 6.0f),
        new Offsetf(7.0f, 8.0f)
    );
    Check.That(
        cubic ==
        new CubicBezier(
            new Offsetf(1.0f, 2.0f),
            new Offsetf(3.0f, 4.0f),
            new Offsetf(5.0f, 6.0f),
            new Offsetf(7.0f, 8.0f)
        )
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Arc ArcTo builds tangent corner arc and rejects invalid input")]
public static void Case21(){

    Arc arc = Arc.ArcTo(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(10.0f, 0.0f),
        new Offsetf(10.0f, 10.0f),
        2.0f
    );
    Check.That(arc.IsValid());
    CheckOffsetf(arc.Center, 8.0f, 2.0f);
    CheckOffsetf(arc.StartPoint(), 8.0f, 0.0f);
    CheckOffsetf(arc.EndPoint(), 10.0f, 2.0f);
    ExpectNear(arc.Radius, 2.0f, 0.001f);
    ExpectNear(arc.SweepAngle, 0.5f * kPi, 0.001f);

    Check.False(
        Arc.ArcTo(
            new Offsetf(0.0f, 0.0f),
            new Offsetf(0.0f, 0.0f),
            new Offsetf(1.0f, 1.0f),
            2.0f
        )
            .IsValid()
    );
    Check.False(
        Arc.ArcTo(
            new Offsetf(0.0f, 0.0f),
            new Offsetf(1.0f, 0.0f),
            new Offsetf(2.0f, 0.0f),
            2.0f
        )
            .IsValid()
    );
    Check.False(
        Arc.ArcTo(
            new Offsetf(0.0f, 0.0f),
            new Offsetf(1.0f, 0.0f),
            new Offsetf(1.0f, 1.0f),
            0.0f
        )
            .IsValid()
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/EllipticalArc ArcTo supports flags scaling and invalid input")]
public static void Case22(){

    EllipticalArc small_cw = EllipticalArc.ArcTo(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(100.0f, 0.0f),
        new Offsetf(80.0f, 40.0f),
        0.0f,
        EEllipticalArcSize.Small,
        EEllipticalArcSweep.CW
    );
    EllipticalArc large_cw = EllipticalArc.ArcTo(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(100.0f, 0.0f),
        new Offsetf(80.0f, 40.0f),
        0.0f,
        EEllipticalArcSize.Large,
        EEllipticalArcSweep.CW
    );
    EllipticalArc small_ccw = EllipticalArc.ArcTo(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(100.0f, 0.0f),
        new Offsetf(80.0f, 40.0f),
        0.0f,
        EEllipticalArcSize.Small,
        EEllipticalArcSweep.CCW
    );
    EllipticalArc large_ccw = EllipticalArc.ArcTo(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(100.0f, 0.0f),
        new Offsetf(80.0f, 40.0f),
        0.0f,
        EEllipticalArcSize.Large,
        EEllipticalArcSweep.CCW
    );

    Check.That(small_cw.IsValid());
    Check.That(large_cw.IsValid());
    Check.That(small_ccw.IsValid());
    Check.That(large_ccw.IsValid());
    CheckOffsetf(small_cw.StartPoint(), 0.0f, 0.0f);
    CheckOffsetf(small_cw.EndPoint(), 100.0f, 0.0f);
    Check.That(small_cw.SweepAngle > 0.0f);
    Check.That(large_cw.SweepAngle > small_cw.SweepAngle);
    Check.That(small_ccw.SweepAngle < 0.0f);
    Check.That(MathF.Abs(large_ccw.SweepAngle) > MathF.Abs(small_ccw.SweepAngle));

    EllipticalArc scaled = EllipticalArc.ArcTo(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(100.0f, 0.0f),
        new Offsetf(20.0f, 10.0f),
        0.0f,
        EEllipticalArcSize.Small,
        EEllipticalArcSweep.CW
    );
    Check.That(scaled.IsValid());
    CheckOffsetf(scaled.StartPoint(), 0.0f, 0.0f);
    CheckOffsetf(scaled.EndPoint(), 100.0f, 0.0f);
    ExpectNear(scaled.RadiusX, 50.0f, 0.01f);
    ExpectNear(scaled.RadiusY, 25.0f, 0.01f);

    Check.False(
        EllipticalArc.ArcTo(
            new Offsetf(0.0f, 0.0f),
            new Offsetf(0.0f, 0.0f),
            new Offsetf(80.0f, 40.0f),
            0.0f,
            EEllipticalArcSize.Small,
            EEllipticalArcSweep.CW
        )
            .IsValid()
    );
    Check.False(
        EllipticalArc.ArcTo(
            new Offsetf(0.0f, 0.0f),
            new Offsetf(100.0f, 0.0f),
            new Offsetf(0.0f, 40.0f),
            0.0f,
            EEllipticalArcSize.Small,
            EEllipticalArcSweep.CW
        )
            .IsValid()
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape calc_tolerance scale response")]
public static void Case23(){

    ExpectCalcToleranceScaleResponse<Circle>();
    ExpectCalcToleranceScaleResponse<Ellipse>();
    ExpectCalcToleranceScaleResponse<Arc>();
    ExpectCalcToleranceScaleResponse<EllipticalArc>();
    ExpectCalcToleranceScaleResponse<QuadBezier>();
    ExpectCalcToleranceScaleResponse<CubicBezier>();
    ExpectNear(
        QuadBezier.CalcTolerance(1.0f, 0.0f),
        QuadBezier.CalcTolerance(1.0f, 1.0f),
        kFloatEpsilon
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape sample tolerance is monotonic across primitives")]
public static void Case24(){

    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 10.0f);
    Ellipse ellipse = Ellipse.CenterRadius(Offsetf.Zero(), 20.0f, 4.0f, 0.0f);
    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.5f * kPi);
    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        8.0f,
        4.0f,
        0.0f,
        0.0f,
        0.5f * kPi
    );

    List<CollectedShapeSample> circle_loose = CollectShapeSamples(
        circle,
        new ShapeToleranceSampleDesc( 1.0f, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> circle_dense = CollectShapeSamples(
        circle,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> ellipse_loose = CollectShapeSamples(
        ellipse,
        new ShapeToleranceSampleDesc( 1.0f, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> ellipse_dense = CollectShapeSamples(
        ellipse,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> arc_loose = CollectShapeSamples(
        arc,
        new ShapeToleranceSampleDesc( 1.0f, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> arc_dense = CollectShapeSamples(
        arc,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> elliptical_arc_loose = CollectShapeSamples(
        elliptical_arc,
        new ShapeToleranceSampleDesc( 1.0f, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> elliptical_arc_dense = CollectShapeSamples(
        elliptical_arc,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward )
    );

    Check.That(circle_dense.Size() >= circle_loose.Size());
    Check.That(ellipse_dense.Size() >= ellipse_loose.Size());
    Check.That(arc_dense.Size() >= arc_loose.Size());
    Check.That(elliptical_arc_dense.Size() >= elliptical_arc_loose.Size());

}
[GuiTest("math/shape_tests.cpp::gui/math/Arc shape estimate_segment_count handles ellipse and superellipse arcs")]
public static void Case25(){

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        6.0f,
        0.2f,
        -0.25f * kPi,
        0.75f * kPi
    );
    SuperellipseArc superellipse_arc = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        6.0f,
        -0.1f,
        -0.25f * kPi,
        0.75f * kPi,
        4.0f
    );

    uint elliptical_loose = elliptical_arc.EstimateSegmentCount(1.0f);
    uint elliptical_dense = elliptical_arc.EstimateSegmentCount(0.1f);
    uint superellipse_loose = superellipse_arc.EstimateSegmentCount(1.0f);
    uint superellipse_dense = superellipse_arc.EstimateSegmentCount(0.1f);
    Check.That(elliptical_loose >= 2u);
    Check.That(elliptical_dense >= elliptical_loose);
    Check.That(superellipse_loose >= 2u);
    Check.That(superellipse_dense >= superellipse_loose);
    Check.That(EllipticalArc.Invalid().EstimateSegmentCount(1.0f) == 0u);
    Check.That(SuperellipseArc.Invalid().EstimateSegmentCount(1.0f) == 0u);
    Check.That(elliptical_arc.EstimateSegmentCount(0.0f) == 0u);
    Check.That(superellipse_arc.EstimateSegmentCount(0.0f) == 0u);

    EllipticalArc rotated_elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        6.0f,
        1.1f,
        elliptical_arc.StartAngle,
        elliptical_arc.SweepAngle
    );
    SuperellipseArc rotated_superellipse_arc = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        6.0f,
        1.1f,
        superellipse_arc.StartAngle,
        superellipse_arc.SweepAngle,
        superellipse_arc.Exponent
    );
    Check.That(rotated_elliptical_arc.EstimateSegmentCount(0.5f) == elliptical_arc.EstimateSegmentCount(0.5f));
    Check.That(rotated_superellipse_arc.EstimateSegmentCount(0.5f) == superellipse_arc.EstimateSegmentCount(0.5f));

    EllipticalArc shifted_elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        6.0f,
        elliptical_arc.Rotation,
        0.375f * kPi,
        elliptical_arc.SweepAngle
    );
    SuperellipseArc shifted_superellipse_arc = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        6.0f,
        superellipse_arc.Rotation,
        0.375f * kPi,
        superellipse_arc.SweepAngle,
        superellipse_arc.Exponent
    );
    Check.That(shifted_elliptical_arc.EstimateSegmentCount(0.5f) >= 2u);
    Check.That(shifted_superellipse_arc.EstimateSegmentCount(0.5f) >= 2u);

    Ellipse ellipse = Ellipse.CenterRadius(
        elliptical_arc.Center,
        elliptical_arc.RadiusX,
        elliptical_arc.RadiusY,
        elliptical_arc.Rotation
    );
    EllipticalArc full_elliptical_arc = EllipticalArc.CenterRadius(
        elliptical_arc.Center,
        elliptical_arc.RadiusX,
        elliptical_arc.RadiusY,
        1.1f,
        -0.375f * kPi,
        (MathF.PI * 2)
    );
    Check.That(full_elliptical_arc.EstimateSegmentCount(0.5f) == ellipse.EstimateSegmentCount(0.5f));

    EllipticalArc swapped_axis_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        6.0f,
        24.0f,
        -0.7f,
        (MathF.PI * 0.5f) - elliptical_arc.StartAngle,
        -elliptical_arc.SweepAngle
    );
    Check.That(swapped_axis_arc.EstimateSegmentCount(0.5f) == elliptical_arc.EstimateSegmentCount(0.5f));

    EllipticalArc negative_sweep_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        24.0f,
        6.0f,
        0.3f,
        0.4f * kPi,
        -1.2f * kPi
    );
    Check.That(negative_sweep_arc.EstimateSegmentCount(1.0f) >= 2u);
    Check.That(negative_sweep_arc.EstimateSegmentCount(0.1f) >= negative_sweep_arc.EstimateSegmentCount(1.0f));

    float[] aspects = {
        1.0f,
        1.249f,
        1.25f,
        1.749f,
        1.75f,
        2.499f,
        2.5f,
        3.999f,
        4.0f,
        7.999f,
        8.0f,
        15.999f,
        16.0f,
        24.0f,
    };
    foreach (float aspect in aspects)
    {
        Ellipse segmented_ellipse = Ellipse.CenterRadius(
            Offsetf.Zero(),
            96.0f,
            96.0f / aspect,
            0.6f
        );
        Ellipse swapped_ellipse = Ellipse.CenterRadius(
            Offsetf.Zero(),
            96.0f / aspect,
            96.0f,
            -0.2f
        );
        uint loose = segmented_ellipse.EstimateSegmentCount(1.0f);
        uint dense = segmented_ellipse.EstimateSegmentCount(0.1f);
        Check.That(loose >= 4u);


        Check.That(dense >= loose);
        Check.That(swapped_ellipse.EstimateSegmentCount(1.0f) == loose);
    }

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse estimate_segment_count handles exponent segments")]
public static void Case26(){

    float[] exponents = {
        0.25f,
        0.75f,
        1.249f,
        1.25f,
        1.749f,
        1.75f,
        2.0f,
        2.25f,
        3.75f,
        4.75f,
        8.0f,
        16.0f,
    };
    foreach (float exponent in exponents)
    {
        Superellipse shape = Superellipse.CenterRadius(
            Offsetf.Zero(),
            64.0f,
            17.0f,
            0.2f,
            exponent
        );
        uint loose = shape.EstimateSegmentCount(1.0f);
        uint dense = shape.EstimateSegmentCount(0.1f);
        Check.That(loose >= 3u);
        Check.That(dense >= loose);
    }

    Superellipse diamond = Superellipse.CenterRadius(
        Offsetf.Zero(),
        64.0f,
        17.0f,
        0.2f,
        1.0f
    );
    Check.That(diamond.EstimateSegmentCount(1.0f) == 4u);
    Check.That(diamond.EstimateSegmentCount(0.01f) == 4u);

    Ellipse ellipse = Ellipse.CenterRadius(Offsetf.Zero(), 64.0f, 17.0f, 0.2f);
    Superellipse exponent_two = Superellipse.CenterRadius(
        ellipse.Center,
        ellipse.RadiusX,
        ellipse.RadiusY,
        ellipse.Rotation,
        2.0f
    );
    Check.That(exponent_two.EstimateSegmentCount(0.5f) == ellipse.EstimateSegmentCount(0.5f));

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc estimate_segment_count handles exponent segments")]
public static void Case27(){

    float[] exponents = {
        0.25f,
        0.75f,
        1.249f,
        1.25f,
        1.749f,
        1.75f,
        2.0f,
        2.25f,
        3.75f,
        4.75f,
        8.0f,
        16.0f,
    };
    foreach (float exponent in exponents)
    {
        SuperellipseArc arc = SuperellipseArc.CenterRadius(
            Offsetf.Zero(),
            64.0f,
            17.0f,
            -0.3f,
            -0.35f * kPi,
            1.4f * kPi,
            exponent
        );
        uint loose = arc.EstimateSegmentCount(1.0f);
        uint dense = arc.EstimateSegmentCount(0.1f);
        Check.That(loose >= 2u);
        Check.That(dense >= loose);
    }

    SuperellipseArc diamond = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        64.0f,
        17.0f,
        -0.3f,
        -0.35f * kPi,
        1.4f * kPi,
        1.0f
    );
    Check.That(diamond.EstimateSegmentCount(1.0f) == diamond.EstimateSegmentCount(0.01f));

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        64.0f,
        17.0f,
        -0.3f,
        -0.35f * kPi,
        1.4f * kPi
    );
    SuperellipseArc exponent_two = SuperellipseArc.CenterRadius(
        elliptical_arc.Center,
        elliptical_arc.RadiusX,
        elliptical_arc.RadiusY,
        elliptical_arc.Rotation,
        elliptical_arc.StartAngle,
        elliptical_arc.SweepAngle,
        2.0f
    );
    Check.That(exponent_two.EstimateSegmentCount(0.5f) == elliptical_arc.EstimateSegmentCount(0.5f));

    Superellipse full_shape = Superellipse.CenterRadius(
        Offsetf.Zero(),
        64.0f,
        17.0f,
        0.7f,
        4.0f
    );
    SuperellipseArc full_arc = SuperellipseArc.CenterRadius(
        full_shape.Center,
        full_shape.RadiusX,
        full_shape.RadiusY,
        -0.4f,
        0.37f * kPi,
        (MathF.PI * 2),
        full_shape.Exponent
    );
    Check.That(full_arc.EstimateSegmentCount(0.5f) == full_shape.EstimateSegmentCount(0.5f));

    SuperellipseArc canonical_axis = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        64.0f,
        17.0f,
        0.5f,
        -0.35f * kPi,
        1.4f * kPi,
        4.0f
    );
    SuperellipseArc swapped_axis = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        17.0f,
        64.0f,
        0.5f,
        (MathF.PI * 0.5f) - canonical_axis.StartAngle,
        -canonical_axis.SweepAngle,
        canonical_axis.Exponent
    );
    Check.That(swapped_axis.EstimateSegmentCount(0.5f) == canonical_axis.EstimateSegmentCount(0.5f));

    SuperellipseArc negative_sweep = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        64.0f,
        17.0f,
        0.1f,
        0.4f * kPi,
        -1.2f * kPi,
        4.0f
    );
    Check.That(negative_sweep.EstimateSegmentCount(1.0f) >= 2u);
    Check.That(negative_sweep.EstimateSegmentCount(0.1f) >= negative_sweep.EstimateSegmentCount(1.0f));

    SuperellipseArc near_axis = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        128.0f,
        8.0f,
        0.0f,
        0.0f,
        0.35f * kPi,
        4.0f
    );
    SuperellipseArc near_diagonal = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        128.0f,
        8.0f,
        0.0f,
        0.45f * kPi,
        0.35f * kPi,
        4.0f
    );
    Check.That(near_axis.EstimateSegmentCount(0.05f) != near_diagonal.EstimateSegmentCount(0.05f));

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape sample step length is supported across primitives")]
public static void Case28(){

    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 10.0f);
    List<CollectedShapeSample> circle_samples = CollectShapeSamples(
        circle,
        new ShapeStepLengthSampleDesc( 10.0f, EShapeSampleDirection.Forward, 1u )
    );
    Check.That(circle_samples.Size() == 7u);

    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.5f * kPi);
    List<CollectedShapeSample> arc_samples = CollectShapeSamples(
        arc,
        new ShapeStepLengthSampleDesc( 4.0f, EShapeSampleDirection.Forward, 1u )
    );
    Check.That(arc_samples.Size() == 5u);
    CheckOffsetf(arc_samples.Front().Point, arc.StartPoint().X, arc.StartPoint().Y);
    CheckOffsetf(arc_samples.Back().Point, arc.EndPoint().X, arc.EndPoint().Y);

    Ellipse ellipse = Ellipse.CenterRadius(Offsetf.Zero(), 20.0f, 4.0f, 0.0f);
    List<CollectedShapeSample> ellipse_samples = CollectShapeSamples(
        ellipse,
        new ShapeStepLengthSampleDesc( 6.0f, EShapeSampleDirection.Forward, 8u )
    );
    Check.That(ellipse_samples.Size() >= 3u);

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        8.0f,
        4.0f,
        0.0f,
        0.0f,
        0.5f * kPi
    );
    List<CollectedShapeSample> elliptical_arc_samples = CollectShapeSamples(
        elliptical_arc,
        new ShapeStepLengthSampleDesc( 3.0f, EShapeSampleDirection.Forward, 8u )
    );
    CheckOffsetf(
        elliptical_arc_samples.Front().Point,
        elliptical_arc.StartPoint().X,
        elliptical_arc.StartPoint().Y
    );
    CheckOffsetf(
        elliptical_arc_samples.Back().Point,
        elliptical_arc.EndPoint().X,
        elliptical_arc.EndPoint().Y
    );

    QuadBezier quad = new QuadBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(10.0f, 20.0f),
        new Offsetf(20.0f, 0.0f)
    );
    List<CollectedShapeSample> quad_samples = CollectShapeSamples(
        quad,
        new ShapeStepLengthSampleDesc( 4.0f, EShapeSampleDirection.Forward, 8u )
    );
    CheckOffsetf(quad_samples.Front().Point, quad.Start.X, quad.Start.Y);
    CheckOffsetf(quad_samples.Back().Point, quad.End.X, quad.End.Y);

    CubicBezier cubic = new CubicBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(10.0f, 30.0f),
        new Offsetf(20.0f, -30.0f),
        new Offsetf(30.0f, 0.0f)
    );
    List<CollectedShapeSample> cubic_samples = CollectShapeSamples(
        cubic,
        new ShapeStepLengthSampleDesc( 4.0f, EShapeSampleDirection.Forward, 8u )
    );
    CheckOffsetf(cubic_samples.Front().Point, cubic.Start.X, cubic.Start.Y);
    CheckOffsetf(cubic_samples.Back().Point, cubic.End.X, cubic.End.Y);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape sample max depth controls adaptive refinement")]
public static void Case29(){

    Ellipse ellipse = Ellipse.CenterRadius(
        Offsetf.Zero(),
        30.0f,
        4.0f,
        0.0f
    );
    List<CollectedShapeSample> ellipse_shallow = CollectShapeSamples(
        ellipse,
        new ShapeStepLengthSampleDesc( 5.0f, EShapeSampleDirection.Forward, 1u )
    );
    List<CollectedShapeSample> ellipse_deep = CollectShapeSamples(
        ellipse,
        new ShapeStepLengthSampleDesc( 5.0f, EShapeSampleDirection.Forward, 8u )
    );

    bool ellipse_differs = false;
    for (int i = 0; i < ellipse_shallow.Size() && i < ellipse_deep.Size(); ++i)
    {
        if (
            (ellipse_shallow[i].Point - ellipse_deep[i].Point).Length() > 0.001f ||
            MathF.Abs(ellipse_shallow[i].T - ellipse_deep[i].T) > 0.001f
        )
        {
            ellipse_differs = true;
            break;
        }
    }
    Check.That(ellipse_differs);

    CubicBezier cubic = new CubicBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(10.0f, 30.0f),
        new Offsetf(20.0f, -30.0f),
        new Offsetf(30.0f, 0.0f)
    );
    List<CollectedShapeSample> cubic_shallow = CollectShapeSamples(
        cubic,
        new ShapeToleranceSampleDesc( 0.01f, EShapeSampleDirection.Forward, 1u )
    );
    List<CollectedShapeSample> cubic_deep = CollectShapeSamples(
        cubic,
        new ShapeToleranceSampleDesc( 0.01f, EShapeSampleDirection.Forward, 8u )
    );
    List<CollectedShapeSample> cubic_default = CollectShapeSamples(
        cubic,
        new ShapeToleranceSampleDesc( 0.01f, EShapeSampleDirection.Forward, 0u )
    );
    List<CollectedShapeSample> cubic_explicit_default = CollectShapeSamples(
        cubic,
        new ShapeToleranceSampleDesc( 0.01f, EShapeSampleDirection.Forward, 16u )
    );

    Check.That(cubic_deep.Size() > cubic_shallow.Size());
    ExpectShapeSampleMatch(cubic_default, cubic_explicit_default);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape sampling direction preserves native t values")]
public static void Case30(){

    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 10.0f);
    List<CollectedShapeSample> circle_forward = CollectShapeSamples(
        circle,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> circle_reverse = CollectShapeSamples(
        circle,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Reverse )
    );
    Check.That(circle_forward.Size() == 4u);
    Check.That(circle_reverse.Size() == 4u);
    ExpectNear(circle_forward.Front().T, 0.0f, kFloatEpsilon);
    ExpectNear(circle_reverse.Front().T, 0.75f, kFloatEpsilon);
    {
        var expected_reverse = ReverseShapeSamples(circle_forward);
        ExpectShapeSampleMatch(circle_reverse, expected_reverse);
    }

    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(2.0f, -1.0f),
        12.0f,
        4.0f,
        0.25f * kPi
    );
    List<CollectedShapeSample> ellipse_forward = CollectShapeSamples(
        ellipse,
        new ShapeToleranceSampleDesc( 0.25f, EShapeSampleDirection.Forward, 6u )
    );
    List<CollectedShapeSample> ellipse_reverse = CollectShapeSamples(
        ellipse,
        new ShapeToleranceSampleDesc( 0.25f, EShapeSampleDirection.Reverse, 6u )
    );
    {
        var expected_reverse = ReverseShapeSamples(ellipse_forward);
        ExpectShapeSampleMatch(ellipse_reverse, expected_reverse);
    }

    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.5f * kPi);
    List<CollectedShapeSample> arc_forward = CollectShapeSamples(
        arc,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Forward )
    );
    List<CollectedShapeSample> arc_reverse = CollectShapeSamples(
        arc,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Reverse )
    );
    Check.That(arc_forward.Size() == 5u);
    Check.That(arc_reverse.Size() == 5u);
    ExpectNear(arc_forward.Front().T, 0.0f, kFloatEpsilon);
    ExpectNear(arc_forward.Back().T, 1.0f, kFloatEpsilon);
    ExpectNear(arc_reverse.Front().T, 1.0f, kFloatEpsilon);
    ExpectNear(arc_reverse.Back().T, 0.0f, kFloatEpsilon);
    {
        var expected_reverse = ReverseShapeSamples(arc_forward);
        ExpectShapeSampleMatch(arc_reverse, expected_reverse);
    }

    CubicBezier cubic = new CubicBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(10.0f, 30.0f),
        new Offsetf(20.0f, -30.0f),
        new Offsetf(30.0f, 0.0f)
    );
    List<CollectedShapeSample> cubic_forward = CollectShapeSamples(
        cubic,
        new ShapeStepLengthSampleDesc( 4.0f, EShapeSampleDirection.Forward, 8u )
    );
    List<CollectedShapeSample> cubic_reverse = CollectShapeSamples(
        cubic,
        new ShapeStepLengthSampleDesc( 4.0f, EShapeSampleDirection.Reverse, 8u )
    );
    {
        var expected_reverse = ReverseShapeSamples(cubic_forward);
        ExpectShapeSampleMatch(cubic_reverse, expected_reverse);
    }

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape sampling distinguishes open and closed endpoint contracts")]
public static void Case31(){

    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 10.0f);
    List<CollectedShapeSample> circle_uniform = CollectShapeSamples(
        circle,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Forward )
    );
    Check.That(circle_uniform.Size() == 4u);
    CheckOffsetf(
        circle_uniform.Front().Point,
        circle.PointAtT(0.0f).X,
        circle.PointAtT(0.0f).Y
    );
    CheckOffsetf(
        circle_uniform.Back().Point,
        circle.PointAtT(0.75f).X,
        circle.PointAtT(0.75f).Y
    );
    ExpectNear(circle_uniform.Back().T, 0.75f, kFloatEpsilon);

    List<CollectedShapeSample> circle_step = CollectShapeSamples(
        circle,
        new ShapeStepLengthSampleDesc( 1000.0f, EShapeSampleDirection.Forward, 1u )
    );
    Check.That(circle_step.Size() == 3u);
    ExpectNear(circle_step.Back().T, 2.0f / 3.0f, 0.001f);

    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.5f * kPi);
    List<CollectedShapeSample> arc_uniform = CollectShapeSamples(
        arc,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Forward )
    );
    Check.That(arc_uniform.Size() == 5u);
    CheckOffsetf(arc_uniform.Front().Point, arc.StartPoint().X, arc.StartPoint().Y);
    CheckOffsetf(arc_uniform.Back().Point, arc.EndPoint().X, arc.EndPoint().Y);
    ExpectNear(arc_uniform.Back().T, 1.0f, kFloatEpsilon);

    List<CollectedShapeSample> arc_step = CollectShapeSamples(
        arc,
        new ShapeStepLengthSampleDesc( 1000.0f, EShapeSampleDirection.Forward, 1u )
    );
    Check.That(arc_step.Size() == 2u);
    CheckOffsetf(arc_step.Front().Point, arc.StartPoint().X, arc.StartPoint().Y);
    CheckOffsetf(arc_step.Back().Point, arc.EndPoint().X, arc.EndPoint().Y);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape sampling skips degenerate primitives")]
public static void Case32(){

    ExpectNoShapeSamples(
        Circle.Zero(),
        new ShapeSegmentCountSampleDesc( 8u, EShapeSampleDirection.Forward )
    );
    ExpectNoShapeSamples(
        Ellipse.CenterRadius(Offsetf.Zero(), 0.0f, 4.0f, 0.0f),
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward, 4u )
    );
    ExpectNoShapeSamples(
        Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.0f),
        new ShapeStepLengthSampleDesc( 1.0f, EShapeSampleDirection.Forward, 4u )
    );
    ExpectNoShapeSamples(
        EllipticalArc.CenterRadius(Offsetf.Zero(), 8.0f, 4.0f, 0.0f, 0.0f, 0.0f),
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward, 4u )
    );
    ExpectNoShapeSamples(
        QuadBezier.Point(new Offsetf(1.0f, 2.0f)),
        new ShapeSegmentCountSampleDesc( 8u, EShapeSampleDirection.Forward )
    );
    ExpectNoShapeSamples(
        CubicBezier.Point(new Offsetf(1.0f, 2.0f)),
        new ShapeStepLengthSampleDesc( 1.0f, EShapeSampleDirection.Forward, 4u )
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/CubicBezier length and closest point")]
public static void Case33(){

    CubicBezier line = CubicBezier.Line(new Offsetf(0.0f, 0.0f), new Offsetf(12.0f, 0.0f));
    Check.That(line.IsValid());
    ExpectNear(line.Length(0.001f), 12.0f, 0.01f);
    CheckOffsetf(line.ClosestPoint(new Offsetf(5.0f, -3.0f), 0.001f), 5.0f, 0.0f);
    ExpectNear(line.Distance(new Offsetf(5.0f, -3.0f), 0.001f), 3.0f, 0.01f);

    CubicBezier curve = new CubicBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(4.0f, 12.0f),
        new Offsetf(8.0f, -12.0f),
        new Offsetf(12.0f, 0.0f)
    );
    Check.That(curve.Length(0.001f) > (curve.EndPoint() - curve.StartPoint()).Length());
    Offsetf midpoint = curve.PointAt(0.5f);
    Offsetf closest = curve.ClosestPoint(midpoint, 0.001f);
    ExpectNear((closest - midpoint).Length(), 0.0f, 0.05f);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape project APIs expose semantic parameters")]
public static void Case34(){

    Circle circle = Circle.CenterRadius(Offsetf.Zero(), 3.0f);
    var circle_project = circle.Project(new Offsetf(6.0f, 0.0f));
    Check.That(circle_project.IsValid());
    CheckOffsetf(circle_project.Point, 3.0f, 0.0f);
    ExpectNear(circle_project.T, 0.0f, kFloatEpsilon);
    ExpectNear(circle_project.Distance, 3.0f, kFloatEpsilon);

    Ellipse ellipse = Ellipse.CenterRadius(Offsetf.Zero(), 4.0f, 2.0f, 0.0f);
    var ellipse_project = ellipse.Project(Offsetf.Zero(), 0.001f);
    Check.That(ellipse_project.IsValid());
    CheckOffsetf(ellipse_project.Point, 0.0f, 2.0f);
    ExpectNear(ellipse_project.Distance, 2.0f, 0.05f);

    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, 0.5f * kPi);
    var arc_project = arc.Project(new Offsetf(10.0f, 10.0f));
    Check.That(arc_project.IsValid());
    CheckOffsetf(
        arc_project.Point,
        MathF.Sqrt(50.0f),
        MathF.Sqrt(50.0f)
    );
    ExpectNear(arc_project.T, 0.5f, 0.001f);

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        8.0f,
        4.0f,
        0.0f,
        0.0f,
        0.5f * kPi
    );
    var elliptical_project = elliptical_arc.Project(new Offsetf(10.0f, 0.0f), 0.001f);
    Check.That(elliptical_project.IsValid());
    CheckOffsetf(elliptical_project.Point, 8.0f, 0.0f);
    ExpectNear(elliptical_project.T, 0.0f, kFloatEpsilon);

    QuadBezier quad = QuadBezier.Line(new Offsetf(0.0f, 0.0f), new Offsetf(10.0f, 0.0f));
    var quad_project = quad.Project(new Offsetf(3.0f, 4.0f), 0.001f);
    Check.That(quad_project.IsValid());
    CheckOffsetf(quad_project.Point, 3.0f, 0.0f);
    ExpectNear(quad_project.T, 0.3f, 0.001f);

    CubicBezier cubic = CubicBezier.Line(new Offsetf(0.0f, 0.0f), new Offsetf(12.0f, 0.0f));
    var cubic_project = cubic.Project(new Offsetf(9.0f, -2.0f), 0.001f);
    Check.That(cubic_project.IsValid());
    CheckOffsetf(cubic_project.Point, 9.0f, 0.0f);
    ExpectNear(cubic_project.T, 0.75f, 0.001f);

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape trim and subcurve APIs preserve endpoints")]
public static void Case35(){

    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, kPi);
    Arc trimmed_arc = arc.Trim(0.25f, 0.75f);
    CheckOffsetf(
        trimmed_arc.StartPoint(),
        arc.PointAtT(0.25f).X,
        arc.PointAtT(0.25f).Y
    );
    CheckOffsetf(
        trimmed_arc.EndPoint(),
        arc.PointAtT(0.75f).X,
        arc.PointAtT(0.75f).Y
    );

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        8.0f,
        4.0f,
        0.25f * kPi,
        0.0f,
        kPi
    );
    EllipticalArc trimmed_elliptical_arc = elliptical_arc.Trim(0.2f, 0.8f);
    CheckOffsetf(
        trimmed_elliptical_arc.StartPoint(),
        elliptical_arc.PointAtT(0.2f).X,
        elliptical_arc.PointAtT(0.2f).Y
    );
    CheckOffsetf(
        trimmed_elliptical_arc.EndPoint(),
        elliptical_arc.PointAtT(0.8f).X,
        elliptical_arc.PointAtT(0.8f).Y
    );

    QuadBezier quad = new QuadBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(5.0f, 10.0f),
        new Offsetf(10.0f, 0.0f)
    );
    QuadBezier quad_subcurve = quad.Subcurve(0.25f, 0.75f);
    CheckOffsetf(
        quad_subcurve.StartPoint(),
        quad.PointAt(0.25f).X,
        quad.PointAt(0.25f).Y
    );
    CheckOffsetf(
        quad_subcurve.EndPoint(),
        quad.PointAt(0.75f).X,
        quad.PointAt(0.75f).Y
    );

    CubicBezier cubic = new CubicBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(4.0f, 12.0f),
        new Offsetf(8.0f, -12.0f),
        new Offsetf(12.0f, 0.0f)
    );
    CubicBezier cubic_subcurve = cubic.Subcurve(0.25f, 0.75f);
    CheckOffsetf(
        cubic_subcurve.StartPoint(),
        cubic.PointAt(0.25f).X,
        cubic.PointAt(0.25f).Y
    );
    CheckOffsetf(
        cubic_subcurve.EndPoint(),
        cubic.PointAt(0.75f).X,
        cubic.PointAt(0.75f).Y
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Arc normalization preserves geometry under radius sign flips")]
public static void Case36(){

    Arc arc = Arc.CenterRadius(
        new Offsetf(2.0f, -3.0f),
        -5.0f,
        0.25f * kPi,
        -0.5f * kPi
    );
    Arc normalized = arc.Normalized();
    Check.That(normalized.IsNormalized());
    ExpectNear(normalized.Radius, 5.0f, kFloatEpsilon);

    float[] sample_ts = { 0.0f, 0.25f, 0.5f, 1.0f };
    foreach (float t in sample_ts)
    {
        CheckOffsetf(
            normalized.PointAtT(t),
            arc.PointAtT(t).X,
            arc.PointAtT(t).Y
        );
    }

}
[GuiTest("math/shape_tests.cpp::gui/math/EllipticalArc normalization preserves geometry under radius sign flips")]
public static void Case37(){

    var ExpectSamePoints = (EllipticalArc shape) => {
        EllipticalArc normalized = shape.Normalized();
        Check.That(normalized.IsNormalized());

        float[] sample_ts = { 0.0f, 0.25f, 0.5f, 1.0f };
        foreach (float t in sample_ts)
        {
            CheckOffsetf(
                normalized.PointAtT(t),
                shape.PointAtT(t).X,
                shape.PointAtT(t).Y
            );
        }
    };

    ExpectSamePoints(EllipticalArc.CenterRadius(new Offsetf(1.0f, 2.0f), -6.0f, 4.0f, 0.25f * kPi, 0.1f * kPi, -0.75f * kPi));
    ExpectSamePoints(EllipticalArc.CenterRadius(new Offsetf(-3.0f, 5.0f), 6.0f, -4.0f, -0.15f * kPi, -0.2f * kPi, 0.6f * kPi));
    ExpectSamePoints(EllipticalArc.CenterRadius(new Offsetf(4.0f, -1.0f), -6.0f, -4.0f, 0.3f * kPi, 0.4f * kPi, 0.5f * kPi));

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape trim and subcurve clamp and reverse out-of-range inputs")]
public static void Case38(){

    Arc arc = Arc.CenterRadius(Offsetf.Zero(), 10.0f, 0.0f, kPi);
    Arc reversed_trimmed_arc = arc.Trim(0.8f, 0.2f);
    CheckOffsetf(
        reversed_trimmed_arc.StartPoint(),
        arc.PointAtT(0.8f).X,
        arc.PointAtT(0.8f).Y
    );
    CheckOffsetf(
        reversed_trimmed_arc.EndPoint(),
        arc.PointAtT(0.2f).X,
        arc.PointAtT(0.2f).Y
    );
    Check.That(reversed_trimmed_arc.SweepAngle < 0.0f);

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        Offsetf.Zero(),
        8.0f,
        4.0f,
        0.25f * kPi,
        0.0f,
        kPi
    );
    EllipticalArc reversed_trimmed_elliptical_arc =
        elliptical_arc.Trim(0.9f, 0.1f);
    CheckOffsetf(
        reversed_trimmed_elliptical_arc.StartPoint(),
        elliptical_arc.PointAtT(0.9f).X,
        elliptical_arc.PointAtT(0.9f).Y
    );
    CheckOffsetf(
        reversed_trimmed_elliptical_arc.EndPoint(),
        elliptical_arc.PointAtT(0.1f).X,
        elliptical_arc.PointAtT(0.1f).Y
    );
    Check.That(reversed_trimmed_elliptical_arc.SweepAngle < 0.0f);

    QuadBezier quad = new QuadBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(5.0f, 10.0f),
        new Offsetf(10.0f, 0.0f)
    );
    Check.That(quad.Subcurve(-1.0f, 2.0f) == quad);
    QuadBezier reversed_quad = quad.Subcurve(0.8f, 0.2f);
    CheckOffsetf(
        reversed_quad.StartPoint(),
        quad.PointAt(0.8f).X,
        quad.PointAt(0.8f).Y
    );
    CheckOffsetf(
        reversed_quad.EndPoint(),
        quad.PointAt(0.2f).X,
        quad.PointAt(0.2f).Y
    );

    CubicBezier cubic = new CubicBezier(
        new Offsetf(0.0f, 0.0f),
        new Offsetf(4.0f, 12.0f),
        new Offsetf(8.0f, -12.0f),
        new Offsetf(12.0f, 0.0f)
    );
    Check.That(cubic.Subcurve(-1.0f, 2.0f) == cubic);
    CubicBezier reversed_cubic = cubic.Subcurve(0.9f, 0.1f);
    CheckOffsetf(
        reversed_cubic.StartPoint(),
        cubic.PointAt(0.9f).X,
        cubic.PointAt(0.9f).Y
    );
    CheckOffsetf(
        reversed_cubic.EndPoint(),
        cubic.PointAt(0.1f).X,
        cubic.PointAt(0.1f).Y
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/Shape project stays valid for degenerate inputs")]
public static void Case39(){

    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(2.0f, 3.0f),
        0.0f,
        4.0f,
        0.25f * kPi
    );
    var ellipse_project = ellipse.Project(new Offsetf(10.0f, -7.0f), 0.001f);
    Check.That(ellipse_project.IsValid());
    CheckOffsetf(ellipse_project.Point, ellipse.Center.X, ellipse.Center.Y);
    ExpectNear(ellipse_project.T, 0.0f, kFloatEpsilon);

    Arc arc = Arc.CenterRadius(
        new Offsetf(1.0f, -2.0f),
        5.0f,
        0.25f * kPi,
        0.0f
    );
    var arc_project = arc.Project(new Offsetf(9.0f, 9.0f));
    Check.That(arc_project.IsValid());
    CheckOffsetf(arc_project.Point, arc.StartPoint().X, arc.StartPoint().Y);
    ExpectNear(arc_project.T, 0.0f, kFloatEpsilon);

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        new Offsetf(-3.0f, 4.0f),
        8.0f,
        4.0f,
        0.25f * kPi,
        0.4f * kPi,
        0.0f
    );
    var elliptical_project =
        elliptical_arc.Project(new Offsetf(8.0f, -6.0f), 0.001f);
    Check.That(elliptical_project.IsValid());
    CheckOffsetf(
        elliptical_project.Point,
        elliptical_arc.StartPoint().X,
        elliptical_arc.StartPoint().Y
    );
    ExpectNear(elliptical_project.T, 0.0f, kFloatEpsilon);

    var quad_project =
        QuadBezier.Point(new Offsetf(3.0f, -5.0f)).Project(new Offsetf(9.0f, 1.0f), 0.001f);
    Check.That(quad_project.IsValid());
    CheckOffsetf(quad_project.Point, 3.0f, -5.0f);
    ExpectNear(quad_project.T, 0.0f, kFloatEpsilon);

    var cubic_project =
        CubicBezier.Point(new Offsetf(-7.0f, 6.0f)).Project(new Offsetf(2.0f, 4.0f), 0.001f);
    Check.That(cubic_project.IsValid());
    CheckOffsetf(cubic_project.Point, -7.0f, 6.0f);
    ExpectNear(cubic_project.T, 0.0f, kFloatEpsilon);

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse normalization invalid and exponent two parity")]
public static void Case40(){

    Superellipse invalid = Superellipse.Invalid();
    Check.False(invalid.IsValid());
    Check.False(invalid.Normalized().IsValid());

    Superellipse bad_exponent = Superellipse.CenterRadius(
        Offsetf.Zero(),
        4.0f,
        2.0f,
        0.0f,
        0.0f
    );
    Check.False(bad_exponent.IsValid());

    Superellipse unnormalized = Superellipse.CenterRadius(
        new Offsetf(1.0f, -2.0f),
        -4.0f,
        2.0f,
        (MathF.PI * 2) + 0.25f * kPi,
        4.0f
    );
    Check.False(unnormalized.IsValid());

    Superellipse normalized = unnormalized.Normalized();
    Check.That(normalized.IsValid());
    Check.That(normalized.IsNormalized());
    ExpectNear(normalized.RadiusX, 4.0f, kFloatEpsilon);
    ExpectNear(normalized.RadiusY, 2.0f, kFloatEpsilon);
    ExpectNear(normalized.Exponent, 4.0f, kFloatEpsilon);

    Ellipse ellipse = Ellipse.CenterRadius(
        new Offsetf(3.0f, -5.0f),
        8.0f,
        4.0f,
        0.2f * kPi
    );
    Superellipse superellipse = Superellipse.CenterRadius(
        ellipse.Center,
        ellipse.RadiusX,
        ellipse.RadiusY,
        ellipse.Rotation,
        2.0f
    );

    float[] angles = { 0.0f, 0.25f * kPi, 0.5f * kPi, kPi, 1.5f * kPi };
    foreach (float angle in angles)
    {
        CheckOffsetf(
            superellipse.PointAtAngle(angle),
            ellipse.PointAtAngle(angle).X,
            ellipse.PointAtAngle(angle).Y
        );
    }

    float[] ts = { 0.0f, 0.125f, 0.25f, 0.5f, 0.75f };
    foreach (float t in ts)
    {
        CheckOffsetf(
            superellipse.PointAtT(t),
            ellipse.PointAtT(t).X,
            ellipse.PointAtT(t).Y
        );
    }

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse geometry bounds sampling and direction")]
public static void Case41(){

    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(2.0f, 1.0f),
        5.0f,
        3.0f,
        0.0f,
        4.0f
    );

    CheckRectf(superellipse.Bounds(), -3.0f, -2.0f, 7.0f, 4.0f);
    CheckOffsetf(superellipse.PointAtAngle(0.0f), 7.0f, 1.0f);
    CheckOffsetf(superellipse.PointAtAngle(0.5f * kPi), 2.0f, 4.0f);
    Check.That(superellipse.Contains(new Offsetf(6.0f, 3.0f)));
    Check.False(superellipse.Contains(new Offsetf(8.0f, 1.0f)));

    List<CollectedShapeSample> segment_samples = CollectShapeSamples(
        superellipse,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Forward )
    );
    Check.That(segment_samples.Size() == 4u);
    CheckOffsetf(segment_samples.Front().Point, superellipse.PointAtT(0.0f).X, superellipse.PointAtT(0.0f).Y);
    CheckOffsetf(segment_samples.Back().Point, superellipse.PointAtT(0.75f).X, superellipse.PointAtT(0.75f).Y);

    List<CollectedShapeSample> tolerance_forward = CollectShapeSamples(
        superellipse,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward, 8u )
    );
    List<CollectedShapeSample> tolerance_reverse = CollectShapeSamples(
        superellipse,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Reverse, 8u )
    );
    Check.That(tolerance_forward.Size() > 0u);
    ExpectShapeSampleMatch(
        tolerance_reverse,
        ReverseShapeSamples(tolerance_forward)
    );

    List<CollectedShapeSample> step_samples = CollectShapeSamples(
        superellipse,
        new ShapeStepLengthSampleDesc( 2.0f, EShapeSampleDirection.Forward, 8u )
    );
    Check.That(step_samples.Size() > segment_samples.Size());
    CheckOffsetf(step_samples.Front().Point, superellipse.PointAtT(0.0f).X, superellipse.PointAtT(0.0f).Y);

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse cubic bezier splits preserve endpoints")]
public static void Case42(){

    Superellipse superellipse = Superellipse.CenterRadius(
        new Offsetf(-2.0f, 4.0f),
        8.0f,
        3.0f,
        0.25f * kPi,
        4.0f
    );

    List<CubicBezier> forward = CollectCubicBeziers(
        superellipse,
        0.1f,
        EShapeSampleDirection.Forward
    );
    List<CubicBezier> reverse = CollectCubicBeziers(
        superellipse,
        0.1f,
        EShapeSampleDirection.Reverse
    );
    Check.That(forward.Size() == superellipse.CubicBezierCount(0.1f));
    Check.That(reverse.Size() == forward.Size());
    CheckOffsetf(forward.Front().StartPoint(), superellipse.PointAtT(0.0f).X, superellipse.PointAtT(0.0f).Y);
    CheckOffsetf(reverse.Front().StartPoint(), superellipse.PointAtT(0.0f).X, superellipse.PointAtT(0.0f).Y);
    Check.That(forward.Front().ControlPoint1().Y > superellipse.PointAtT(0.0f).Y);
    Check.That(reverse.Front().ControlPoint1().Y < superellipse.PointAtT(0.0f).Y);

    ExpectToleranceCubicBezierCountMonotonic(superellipse);
    ExpectInvalidToleranceNoCubicBeziers(superellipse);

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse cubic bezier split obeys sampled tolerance")]
public static void Case43(){

    float kTolerance = 0.1f;
    Superellipse[] shapes = {
        Superellipse.CenterRadius(new Offsetf(-2.0f, 4.0f), 8.0f, 3.0f, 0.25f * kPi, 4.0f),
        Superellipse.CenterRadius(new Offsetf(3.0f, -1.0f), 6.0f, 5.0f, -0.15f * kPi, 0.75f),
    };
    float[] samples = { 0.125f, 0.25f, 0.5f, 0.75f, 0.875f };

    foreach (Superellipse shape in shapes)
    {
        List<CubicBezier> cubics = CollectCubicBeziers(shape, kTolerance);
        Check.That(cubics.Size() == shape.CubicBezierCount(kTolerance));
        Check.That(cubics.Size() > 0u);

        float start_angle = 0.0f;
        for (int i = 0; i < cubics.Size(); ++i)
        {
            CubicBezier cubic = cubics[i];
            float end_angle = SuperellipseAngleFromPoint(shape, cubic.EndPoint());
            end_angle = UnwrapAngleAfter(end_angle, start_angle);

            foreach (float sample in samples)
            {
                Offsetf expected = shape.PointAtAngle(
                    start_angle + (end_angle - start_angle) * sample
                );
                Check.That(cubic.Distance(expected, kTolerance * 0.25f) <= kTolerance + 0.001f);
            }

            start_angle = end_angle;
        }
    }

}
[GuiTest("math/shape_tests.cpp::gui/math/Superellipse skips empty and invalid callbacks")]
public static void Case44(){

    ExpectNoShapeSamples(
        Superellipse.Zero(),
        new ShapeSegmentCountSampleDesc( 8u, EShapeSampleDirection.Forward )
    );
    ExpectNoShapeSamples(
        Superellipse.Invalid(),
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward, 4u )
    );
    ExpectNoShapeSamples(
        Superellipse.CenterRadius(Offsetf.Zero(), 5.0f, 0.0f, 0.0f, 4.0f),
        new ShapeStepLengthSampleDesc( 1.0f, EShapeSampleDirection.Forward, 4u )
    );

    ExpectNoToleranceCubicBeziers(Superellipse.Zero());
    ExpectNoToleranceCubicBeziers(Superellipse.Invalid());
    ExpectNoToleranceCubicBeziers(
        Superellipse.CenterRadius(Offsetf.Zero(), 5.0f, 0.0f, 0.0f, 4.0f)
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc normalization invalid and exponent two parity")]
public static void Case45(){

    SuperellipseArc invalid = SuperellipseArc.Invalid();
    Check.False(invalid.IsValid());
    Check.False(invalid.Normalized().IsValid());

    SuperellipseArc bad_exponent = SuperellipseArc.CenterRadius(
        Offsetf.Zero(),
        4.0f,
        2.0f,
        0.0f,
        0.0f,
        kPi,
        0.0f
    );
    Check.False(bad_exponent.IsValid());

    SuperellipseArc unnormalized = SuperellipseArc.CenterRadius(
        new Offsetf(1.0f, 2.0f),
        -6.0f,
        4.0f,
        (MathF.PI * 2) + 0.25f * kPi,
        0.1f * kPi,
        -0.75f * kPi,
        4.0f
    );
    Check.False(unnormalized.IsValid());

    SuperellipseArc normalized = unnormalized.Normalized();
    Check.That(normalized.IsValid());
    Check.That(normalized.IsNormalized());
    ExpectNear(normalized.RadiusX, 6.0f, kFloatEpsilon);
    ExpectNear(normalized.RadiusY, 4.0f, kFloatEpsilon);
    ExpectNear(normalized.Exponent, 4.0f, kFloatEpsilon);

    EllipticalArc elliptical_arc = EllipticalArc.CenterRadius(
        new Offsetf(3.0f, -5.0f),
        8.0f,
        4.0f,
        0.2f * kPi,
        -0.25f * kPi,
        0.75f * kPi
    );
    SuperellipseArc superellipse_arc = SuperellipseArc.CenterRadius(
        elliptical_arc.Center,
        elliptical_arc.RadiusX,
        elliptical_arc.RadiusY,
        elliptical_arc.Rotation,
        elliptical_arc.StartAngle,
        elliptical_arc.SweepAngle,
        2.0f
    );

    float[] ts = { 0.0f, 0.25f, 0.5f, 1.0f };
    foreach (float t in ts)
    {
        CheckOffsetf(
            superellipse_arc.PointAtT(t),
            elliptical_arc.PointAtT(t).X,
            elliptical_arc.PointAtT(t).Y
        );
    }
    CheckOffsetf(
        superellipse_arc.StartPoint(),
        elliptical_arc.StartPoint().X,
        elliptical_arc.StartPoint().Y
    );
    CheckOffsetf(
        superellipse_arc.EndPoint(),
        elliptical_arc.EndPoint().X,
        elliptical_arc.EndPoint().Y
    );

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc bounds sampling reverse and trim")]
public static void Case46(){

    SuperellipseArc superellipse_arc = SuperellipseArc.CenterRadius(
        new Offsetf(1.0f, -2.0f),
        8.0f,
        4.0f,
        0.0f,
        0.0f,
        0.5f * kPi,
        4.0f
    );

    CheckRectf(superellipse_arc.Bounds(), 1.0f, -2.0f, 9.0f, 2.0f);
    Check.That(superellipse_arc.ContainsAngle(0.25f * kPi));
    Check.False(superellipse_arc.ContainsAngle(kPi));
    CheckOffsetf(superellipse_arc.PointAtAngle(0.0f), 9.0f, -2.0f);
    CheckOffsetf(superellipse_arc.PointAtT(1.0f), 1.0f, 2.0f);

    List<CollectedShapeSample> segment_samples = CollectShapeSamples(
        superellipse_arc,
        new ShapeSegmentCountSampleDesc( 4u, EShapeSampleDirection.Forward )
    );
    Check.That(segment_samples.Size() == 5u);
    CheckOffsetf(segment_samples.Front().Point, superellipse_arc.StartPoint().X, superellipse_arc.StartPoint().Y);
    CheckOffsetf(segment_samples.Back().Point, superellipse_arc.EndPoint().X, superellipse_arc.EndPoint().Y);

    List<CollectedShapeSample> tolerance_forward = CollectShapeSamples(
        superellipse_arc,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward, 8u )
    );
    List<CollectedShapeSample> tolerance_reverse = CollectShapeSamples(
        superellipse_arc,
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Reverse, 8u )
    );
    ExpectShapeSampleMatch(
        tolerance_reverse,
        ReverseShapeSamples(tolerance_forward)
    );

    List<CollectedShapeSample> step_samples = CollectShapeSamples(
        superellipse_arc,
        new ShapeStepLengthSampleDesc( 2.0f, EShapeSampleDirection.Forward, 8u )
    );
    Check.That(step_samples.Size() > 2u);
    CheckOffsetf(step_samples.Front().Point, superellipse_arc.StartPoint().X, superellipse_arc.StartPoint().Y);
    CheckOffsetf(step_samples.Back().Point, superellipse_arc.EndPoint().X, superellipse_arc.EndPoint().Y);

    SuperellipseArc trimmed = superellipse_arc.Trim(0.25f, 0.75f);
    CheckOffsetf(
        trimmed.StartPoint(),
        superellipse_arc.PointAtT(0.25f).X,
        superellipse_arc.PointAtT(0.25f).Y
    );
    CheckOffsetf(
        trimmed.EndPoint(),
        superellipse_arc.PointAtT(0.75f).X,
        superellipse_arc.PointAtT(0.75f).Y
    );

    SuperellipseArc reversed = superellipse_arc.Reversed();
    CheckOffsetf(reversed.StartPoint(), superellipse_arc.EndPoint().X, superellipse_arc.EndPoint().Y);
    CheckOffsetf(reversed.EndPoint(), superellipse_arc.StartPoint().X, superellipse_arc.StartPoint().Y);
    Check.That(reversed.SweepAngle < 0.0f);

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc cubic bezier splits preserve endpoints")]
public static void Case47(){

    SuperellipseArc quarter_arc = SuperellipseArc.CenterRadius(
        new Offsetf(3.0f, -2.0f),
        12.0f,
        5.0f,
        0.25f * kPi,
        0.0f,
        0.5f * kPi,
        4.0f
    );
    List<CubicBezier> quarter_cubics = CollectCubicBeziers(quarter_arc, 0.1f);
    Check.That(quarter_cubics.Size() == quarter_arc.CubicBezierCount(0.1f));
    Check.That(quarter_cubics.Size() >= 1u);
    CheckOffsetf(
        quarter_cubics.Front().StartPoint(),
        quarter_arc.StartPoint().X,
        quarter_arc.StartPoint().Y
    );
    CheckOffsetf(
        quarter_cubics.Back().EndPoint(),
        quarter_arc.EndPoint().X,
        quarter_arc.EndPoint().Y
    );

    SuperellipseArc half_arc = SuperellipseArc.CenterRadius(
        new Offsetf(-4.0f, 1.0f),
        9.0f,
        2.0f,
        -0.2f * kPi,
        0.25f * kPi,
        kPi,
        4.0f
    );
    List<CubicBezier> half_cubics = CollectCubicBeziers(half_arc, 0.1f);
    Check.That(half_cubics.Size() == half_arc.CubicBezierCount(0.1f));
    Check.That(half_cubics.Size() >= 2u);
    CheckOffsetf(
        half_cubics.Front().StartPoint(),
        half_arc.StartPoint().X,
        half_arc.StartPoint().Y
    );
    CheckOffsetf(
        half_cubics.Back().EndPoint(),
        half_arc.EndPoint().X,
        half_arc.EndPoint().Y
    );

    ExpectToleranceCubicBezierCountMonotonic(half_arc);
    ExpectInvalidToleranceNoCubicBeziers(half_arc);

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc cubic bezier split obeys sampled tolerance")]
public static void Case48(){

    float kTolerance = 0.1f;
    SuperellipseArc[] arcs = {
        SuperellipseArc.CenterRadius(
            new Offsetf(3.0f, -2.0f),
            12.0f,
            5.0f,
            0.25f * kPi,
            0.15f * kPi,
            1.35f * kPi,
            4.0f
        ),
        SuperellipseArc.CenterRadius(
            new Offsetf(-4.0f, 1.0f),
            9.0f,
            2.0f,
            -0.2f * kPi,
            0.25f * kPi,
            0.95f * kPi,
            0.75f
        ),
        SuperellipseArc.CenterRadius(
            new Offsetf(2.0f, 3.0f),
            7.0f,
            4.0f,
            0.1f * kPi,
            0.75f * kPi,
            -1.1f * kPi,
            3.5f
        ),
    };
    float[] samples = { 0.125f, 0.25f, 0.5f, 0.75f, 0.875f };

    foreach (SuperellipseArc arc in arcs)
    {
        SuperellipseArc value = arc.Normalized();
        Superellipse shape = Superellipse.CenterRadius(
            value.Center,
            value.RadiusX,
            value.RadiusY,
            value.Rotation,
            value.Exponent
        );
        List<CubicBezier> cubics = CollectCubicBeziers(value, kTolerance);
        Check.That(cubics.Size() == value.CubicBezierCount(kTolerance));
        Check.That(cubics.Size() > 0u);

        float start_angle = value.StartAngle;
        for (int i = 0; i < cubics.Size(); ++i)
        {
            CubicBezier cubic = cubics[i];
            float end_angle = SuperellipseAngleFromPoint(shape, cubic.EndPoint());
            end_angle = value.SweepAngle >= 0.0f ?
                UnwrapAngleAfter(end_angle, start_angle) :
                UnwrapAngleBefore(end_angle, start_angle);



            foreach (float sample in samples)
            {
                Offsetf expected = value.PointAtAngle(
                    start_angle + (end_angle - start_angle) * sample
                );
                Check.That(cubic.Distance(expected, kTolerance * 0.25f) <= kTolerance + 0.001f);
            }

            start_angle = end_angle;
        }
    }

}
[GuiTest("math/shape_tests.cpp::gui/math/SuperellipseArc skips empty and invalid callbacks")]
public static void Case49(){

    ExpectNoShapeSamples(
        SuperellipseArc.Zero(),
        new ShapeSegmentCountSampleDesc( 8u, EShapeSampleDirection.Forward )
    );
    ExpectNoShapeSamples(
        SuperellipseArc.Invalid(),
        new ShapeToleranceSampleDesc( 0.1f, EShapeSampleDirection.Forward, 4u )
    );
    ExpectNoShapeSamples(
        SuperellipseArc.CenterRadius(Offsetf.Zero(), 5.0f, 3.0f, 0.0f, 0.0f, 0.0f, 4.0f),
        new ShapeStepLengthSampleDesc( 1.0f, EShapeSampleDirection.Forward, 4u )
    );

    ExpectNoToleranceCubicBeziers(SuperellipseArc.Zero());
    ExpectNoToleranceCubicBeziers(SuperellipseArc.Invalid());
    ExpectNoToleranceCubicBeziers(
        SuperellipseArc.CenterRadius(Offsetf.Zero(), 5.0f, 3.0f, 0.0f, 0.0f, 0.0f, 4.0f)
    );

}
}
