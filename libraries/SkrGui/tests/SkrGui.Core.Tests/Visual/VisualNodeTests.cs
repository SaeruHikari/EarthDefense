using System.Numerics;
using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualNodeTests
{
private class TestLeaf : VisualLeaf{
public ETextBaseline LastDryBaseline = ETextBaseline.Alphabetic;
public BoxConstraints LastDryBaselineConstraints = new();
public Offsetf LastHitTestPosition = new();
public ulong DryBaselineCount = 0;
public ulong ActualBaselineCount = 0;
public ulong HitTestSelfCount = 0;
public ulong PaintCount = 0;
public ulong LayoutCount = 0;
public bool HitTestSelfResult = false;
public float? DryBaseline = null;
public float? ActualBaseline = null;
public Rectf LastPaintClipRect = Rectf.Largest();
public Offsetf LastPaintTransformedOrigin = new();
public Offsetf LastPaintScale = new Offsetf(1.0f, 1.0f);
public Offsetf LastPaintOrigin = new();
public BoxConstraints LastConstraints = new();
public Sizef DrySize = new Sizef(21.0f, 11.0f);
public Sizef LayoutSize = new Sizef(24.0f, 12.0f);
protected override void PerformLayout(){
        LastConstraints = Constraints();
        ++LayoutCount;
        SetSize(LayoutSize);
    }
protected override float ComputeMinIntrinsicWidth(float height){
        return height + 1.0f;
    }
protected override float ComputeMaxIntrinsicWidth(float height){
        return height + 2.0f;
    }
protected override float ComputeMinIntrinsicHeight(float width){
        return width + 3.0f;
    }
protected override float ComputeMaxIntrinsicHeight(float width){
        return width + 4.0f;
    }
protected override Sizef ComputeDryLayout(BoxConstraints constraints){
        return constraints.Constrain(DrySize);
    }
protected override float? ComputeDryBaseline(BoxConstraints constraints, ETextBaseline baseline){
        ++DryBaselineCount;
        LastDryBaselineConstraints = constraints;
        LastDryBaseline = baseline;
        return DryBaseline;
    }
protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){

        ++ActualBaselineCount;
        return ActualBaseline;
    }
protected override bool HitTestSelf(Offsetf position){
        ++HitTestSelfCount;
        LastHitTestPosition = position;
        return HitTestSelfResult;
    }
}
private class TestShifted : VisualShifted{
public Offsetf ObservedLayoutOffset = new();
public Offsetf LayoutOffset = new Offsetf(7.0f, 9.0f);
protected override void PerformLayout(){
        if (Child() is {} child)
        {
            LayoutChild(child, Constraints());
            _childOffset = LayoutOffset;
            ObservedLayoutOffset = ChildOffset();
            SetSize(child.Size());
            return;
        }

        _childOffset = Offsetf.Zero();
        ObservedLayoutOffset = ChildOffset();
        SetSize(Constraints().Smallest());
    }
}
private class TestIntrinsicLeaf : VisualLeaf{
public float LastMaxIntrinsicHeightWidth = -1.0f;
public float LastMinIntrinsicHeightWidth = -1.0f;
public float LastMaxIntrinsicWidthHeight = -1.0f;
public float LastMinIntrinsicWidthHeight = -1.0f;
public float MaxIntrinsicHeightResult = 17.0f;
public float MinIntrinsicHeightResult = 7.0f;
public float MaxIntrinsicWidthResult = 23.0f;
public float MinIntrinsicWidthResult = 11.0f;
public ETextBaseline LastDryBaseline = ETextBaseline.Alphabetic;
public BoxConstraints LastDryBaselineConstraints = new();
public ulong DryBaselineCount = 0;
public ulong ActualBaselineCount = 0;
public ulong DryLayoutCount = 0;
public ulong LayoutCount = 0;
public float? DryBaseline = null;
public float? ActualBaseline = null;
public BoxConstraints LastDryConstraints = new();
public BoxConstraints LastConstraints = new();
public Sizef PreferredSize = new Sizef(40.0f, 30.0f);
protected override void PerformLayout(){
        LastConstraints = Constraints();
        ++LayoutCount;
        SetSize(Constraints().Constrain(PreferredSize));
    }
protected override float ComputeMinIntrinsicWidth(float height){
        LastMinIntrinsicWidthHeight = height;
        return MinIntrinsicWidthResult;
    }
protected override float ComputeMaxIntrinsicWidth(float height){
        LastMaxIntrinsicWidthHeight = height;
        return MaxIntrinsicWidthResult;
    }
protected override float ComputeMinIntrinsicHeight(float width){
        LastMinIntrinsicHeightWidth = width;
        return MinIntrinsicHeightResult;
    }
protected override float ComputeMaxIntrinsicHeight(float width){
        LastMaxIntrinsicHeightWidth = width;
        return MaxIntrinsicHeightResult;
    }
protected override Sizef ComputeDryLayout(BoxConstraints constraints){
        LastDryConstraints = constraints;
        ++DryLayoutCount;
        return constraints.Constrain(PreferredSize);
    }
protected override float? ComputeDryBaseline(BoxConstraints constraints, ETextBaseline baseline){
        ++DryBaselineCount;
        LastDryBaselineConstraints = constraints;
        LastDryBaseline = baseline;
        return DryBaseline;
    }
protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){

        ++ActualBaselineCount;
        return ActualBaseline;
    }
}
private static T SlotOf<T>(VisualNode node)where T:VisualSlot {Check.NotNull(node);Check.NotNull(node.Slot());return (T)node.Slot()!;}
private static void CheckHitTestTransform(Matrix4x4 transform,Offsetf position,float x,float y){var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);if(point.W!=1){point.X/=point.W;point.Y/=point.W;}CheckOffsetf(new(point.X,point.Y),x,y);}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test self and bounds")]
public static void Case0(){

    var leaf = new TestLeaf();
    leaf.LayoutSize = new Sizef(24.0f, 12.0f);
    VisualTestHelpers.LayoutNode(leaf, BoxConstraints.Tight(leaf.LayoutSize));

    VisualHitTestResult result = new();
    ExpectFalse(leaf.HitTest(result, new Offsetf(5.0f, 6.0f)));
    Expect(result.IsEmpty());
    Equal(leaf.HitTestSelfCount, 1u);

    leaf.HitTestSelfResult = true;
    Expect(leaf.HitTest(result, new Offsetf(5.0f, 6.0f)));
    Equal(result.NumEntries(), 1u);
    Expect(result.Path()[0].Target == leaf);
    CheckOffsetf(result.Path()[0].LocalPosition, 5.0f, 6.0f);
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(5.0f, 6.0f), 5.0f, 6.0f);

    result.Clear();
    ExpectFalse(leaf.HitTest(result, new Offsetf(24.0f, 12.0f)));
    Expect(result.IsEmpty());

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test shifted child coordinates")]
public static void Case1(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(10.0f, 8.0f);
    child.HitTestSelfResult = true;

    var shifted = new TestShifted();
    shifted.LayoutOffset = new Offsetf(2.0f, 2.0f);
    shifted.SetChild(child);
    VisualTestHelpers.LayoutNode(shifted, BoxConstraints.Tight(new Sizef(30.0f, 30.0f)));

    VisualHitTestResult result = new();
    Expect(shifted.HitTest(result, new Offsetf(5.0f, 4.0f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == child);
    CheckOffsetf(result.Path()[0].LocalPosition, 3.0f, 2.0f);
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(5.0f, 4.0f), 3.0f, 2.0f);
    Expect(result.Path()[1].Target == shifted);
    CheckOffsetf(result.Path()[1].LocalPosition, 5.0f, 4.0f);
    CheckHitTestTransform(result.Path()[1].Transform, new Offsetf(5.0f, 4.0f), 5.0f, 4.0f);
    CheckOffsetf(child.LastHitTestPosition, 3.0f, 2.0f);

    result.Clear();
    ExpectFalse(shifted.HitTest(result, new Offsetf(1.0f, 1.0f)));

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test stack uses reverse paint order")]
public static void Case2(){

    var bottom = new TestLeaf();
    bottom.LayoutSize = new Sizef(20.0f, 20.0f);
    bottom.HitTestSelfResult = true;

    var top = new TestLeaf();
    top.LayoutSize = new Sizef(20.0f, 20.0f);
    top.HitTestSelfResult = true;

    var stack = new VisualStack();
    stack.AddChild(bottom);
    stack.AddChild(top);
    VisualTestHelpers.LayoutNode(stack, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));

    VisualHitTestResult result = new();
    Expect(stack.HitTest(result, new Offsetf(5.0f, 5.0f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == top);
    Expect(result.Path()[1].Target == stack);
    Equal(bottom.HitTestSelfCount, 0u);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test indexed stack only tests displayed child")]
public static void Case3(){

    var hidden = new TestLeaf();
    hidden.LayoutSize = new Sizef(20.0f, 20.0f);
    hidden.HitTestSelfResult = true;

    var displayed = new TestLeaf();
    displayed.LayoutSize = new Sizef(20.0f, 20.0f);
    displayed.HitTestSelfResult = true;

    var stack = new VisualIndexedStack();
    stack.AddChild(hidden);
    stack.AddChild(displayed);
    stack.SetIndex(1u);
    VisualTestHelpers.LayoutNode(stack, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));

    VisualHitTestResult result = new();
    Expect(stack.HitTest(result, new Offsetf(5.0f, 5.0f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == displayed);
    Expect(result.Path()[1].Target == stack);
    Equal(hidden.HitTestSelfCount, 0u);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test visibility hidden skips subtree")]
public static void Case4(){

    var child = new TestLeaf();
    child.HitTestSelfResult = true;

    var visibility = new VisualVisibility();
    visibility.SetMaintainSize(true);
    visibility.SetVisibility(false);
    visibility.SetChild(child);
    VisualTestHelpers.LayoutNode(visibility, BoxConstraints.Tight(new Sizef(24.0f, 12.0f)));

    VisualHitTestResult result = new();
    ExpectFalse(visibility.HitTest(result, new Offsetf(5.0f, 5.0f)));
    Expect(result.IsEmpty());
    Equal(child.HitTestSelfCount, 0u);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test clip rect filters child")]
public static void Case5(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(20.0f, 20.0f);
    child.HitTestSelfResult = true;

    var clip = new VisualClipRect();
    clip.SetChild(child);
    clip.SetCustomClipRect(Rectf.LTWH(0.0f, 0.0f, 8.0f, 8.0f));
    VisualTestHelpers.LayoutNode(clip, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));

    VisualHitTestResult result = new();
    Expect(clip.HitTest(result, new Offsetf(4.0f, 4.0f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == child);
    Expect(result.Path()[1].Target == clip);

    result.Clear();
    ExpectFalse(clip.HitTest(result, new Offsetf(12.0f, 4.0f)));
    Expect(result.IsEmpty());

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test fitted node transforms child position")]
public static void Case6(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(100.0f, 50.0f);
    child.HitTestSelfResult = true;

    var fitted = new VisualFitted();
    fitted.SetFit(EBoxFit.Contain);
    fitted.SetChild(child);
    VisualTestHelpers.LayoutNode(fitted, BoxConstraints.Tight(new Sizef(50.0f, 25.0f)));

    VisualHitTestResult result = new();
    Expect(fitted.HitTest(result, new Offsetf(25.0f, 12.5f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == child);
    CheckOffsetf(result.Path()[0].LocalPosition, 50.0f, 25.0f);
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(25.0f, 12.5f), 50.0f, 25.0f);
    Expect(result.Path()[1].Target == fitted);
    CheckOffsetf(child.LastHitTestPosition, 50.0f, 25.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test proxy forwards child")]
public static void Case7(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(24.0f, 12.0f);
    child.HitTestSelfResult = true;

    var proxy = new VisualProxy();
    proxy.SetChild(child);
    VisualTestHelpers.LayoutNode(proxy, BoxConstraints.Tight(new Sizef(24.0f, 12.0f)));

    VisualHitTestResult result = new();
    Expect(proxy.HitTest(result, new Offsetf(6.0f, 7.0f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == child);
    CheckOffsetf(result.Path()[0].LocalPosition, 6.0f, 7.0f);
    Expect(result.Path()[1].Target == proxy);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test transform maps child position")]
public static void Case8(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(20.0f, 10.0f);
    child.HitTestSelfResult = true;

    var transform = new VisualTransform();
    PaintTransform paint_transform = new();
    paint_transform.ApplyOffset2D(new Offsetf(30.0f, 40.0f));
    transform.SetTransform(paint_transform);
    transform.SetChild(child);
    VisualTestHelpers.LayoutNode(transform, BoxConstraints.Tight(new Sizef(20.0f, 10.0f)));

    VisualHitTestResult result = new();
    Expect(transform.HitTest(result, new Offsetf(35.0f, 45.0f)));
    Equal(result.NumEntries(), 1u);
    Expect(result.Path()[0].Target == child);
    CheckOffsetf(result.Path()[0].LocalPosition, 5.0f, 5.0f);
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(35.0f, 45.0f), 5.0f, 5.0f);
    CheckOffsetf(child.LastHitTestPosition, 5.0f, 5.0f);

    result.Clear();
    ExpectFalse(transform.HitTest(result, new Offsetf(25.0f, 45.0f)));
    Expect(result.IsEmpty());

    transform.SetTransformHitTests(false);
    Expect(transform.HitTest(result, new Offsetf(5.0f, 5.0f)));
    Equal(result.NumEntries(), 1u);
    CheckOffsetf(result.Path()[0].LocalPosition, 5.0f, 5.0f);
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(5.0f, 5.0f), 5.0f, 5.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test transform stack composes nested child transforms")]
public static void Case9(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(10.0f, 8.0f);
    child.HitTestSelfResult = true;

    var shifted = new TestShifted();
    shifted.LayoutOffset = new Offsetf(2.0f, 3.0f);
    shifted.SetChild(child);

    var transform = new VisualTransform();
    PaintTransform paint_transform = new();
    paint_transform.ApplyOffset2D(new Offsetf(30.0f, 40.0f));
    transform.SetTransform(paint_transform);
    transform.SetChild(shifted);
    VisualTestHelpers.LayoutNode(transform, BoxConstraints.Tight(new Sizef(10.0f, 8.0f)));

    VisualHitTestResult result = new();
    Expect(transform.HitTest(result, new Offsetf(35.0f, 46.0f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == child);
    CheckOffsetf(result.Path()[0].LocalPosition, 3.0f, 3.0f);
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(35.0f, 46.0f), 3.0f, 3.0f);
    Expect(result.Path()[1].Target == shifted);
    CheckOffsetf(result.Path()[1].LocalPosition, 5.0f, 6.0f);
    CheckHitTestTransform(result.Path()[1].Transform, new Offsetf(35.0f, 46.0f), 5.0f, 6.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/hit-test raw transform records entry transform")]
public static void Case10(){

    var target = new TestLeaf();
    VisualNode target_ptr = target;

    VisualHitTestResult result = new();
    Matrix4x4 raw_transform = Matrix4x4.CreateScale(new Vector3(2.0f, 3.0f, 1.0f));
    bool is_hit = result.AddWithRawTransform(
        raw_transform,
        new Offsetf(4.0f, 5.0f),
        (VisualHitTestResult child_result, Offsetf transformed_position) => {
            CheckOffsetf(transformed_position, 8.0f, 15.0f);
            child_result.Add(target_ptr, transformed_position);
            return true;
        }
    );

    Expect(is_hit);
    Equal(result.NumEntries(), 1u);
    Expect(result.Path()[0].Target == target);
    CheckOffsetf(result.Path()[0].LocalPosition, 8.0f, 15.0f);
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(4.0f, 5.0f), 8.0f, 15.0f);

    result.Clear();
    result.Add(target_ptr, new Offsetf(1.0f, 2.0f));
    CheckHitTestTransform(result.Path()[0].Transform, new Offsetf(1.0f, 2.0f), 1.0f, 2.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/proxy no child")]
public static void Case11(){

    var proxy = new VisualProxy();
    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 40.0f);

    VisualTestHelpers.LayoutNode(proxy, constraints);

    Expect(proxy.Child() == null);
    CheckSizef(proxy.Size(), 10.0f, 5.0f);
    CheckSizef(proxy.GetDryLayout(constraints), 10.0f, 5.0f);
    ExpectNear(proxy.GetMinIntrinsicWidth(10.0f), 0.0f, kFloatEpsilon);
    ExpectNear(proxy.GetMaxIntrinsicHeight(10.0f), 0.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/baseline base fallback and proxy forwarding")]
public static void Case12(){

    var child = new TestLeaf();
    child.ActualBaseline = 8.0f;
    child.DryBaseline = 7.0f;

    var proxy = new VisualProxy();
    proxy.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 40.0f);
    VisualTestHelpers.LayoutNode(proxy, constraints);

    float? ActualBaseline = proxy.GetDistanceToActualBaseline(ETextBaseline.Alphabetic);
    float? distance_to_baseline = proxy.GetDistanceToBaseline(ETextBaseline.Alphabetic);
    float? DryBaseline = proxy.GetDryBaseline(constraints, ETextBaseline.Ideographic);

    Expect(ActualBaseline);
    Expect(distance_to_baseline);
    Expect(DryBaseline);
    ExpectNear(ActualBaseline.Value, 8.0f, kFloatEpsilon);
    ExpectNear(distance_to_baseline.Value, 8.0f, kFloatEpsilon);
    ExpectNear(DryBaseline.Value, 7.0f, kFloatEpsilon);
    CheckConstraints(child.LastDryBaselineConstraints, 10.0f, 50.0f, 5.0f, 40.0f);
    Expect(child.LastDryBaseline == ETextBaseline.Ideographic);

    var no_baseline = new TestLeaf();
    no_baseline.LayoutSize = new Sizef(24.0f, 12.0f);
    VisualTestHelpers.LayoutNode(no_baseline, constraints);

    ExpectFalse(no_baseline.GetDistanceToActualBaseline(ETextBaseline.Alphabetic));
    ExpectFalse(no_baseline.GetDistanceToBaseline(ETextBaseline.Alphabetic, true));
    ExpectFalse(no_baseline.GetDryBaseline(constraints, ETextBaseline.Alphabetic));
    ExpectNear(no_baseline.GetDistanceToBaseline(ETextBaseline.Alphabetic).Value, 12.0f, kFloatEpsilon);

    proxy.ClearChild();
    VisualTestHelpers.LayoutNode(proxy, constraints);
    ExpectFalse(proxy.GetDistanceToActualBaseline(ETextBaseline.Alphabetic));
    ExpectNear(proxy.GetDistanceToBaseline(ETextBaseline.Alphabetic).Value, 5.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/aspect-ratio bounded width layouts child tightly")]
public static void Case13(){

    var child = new TestLeaf();
    var aspect = new VisualAspectRatio();
    aspect.SetAspectRatio(2.0f);
    aspect.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 100.0f);
    VisualTestHelpers.LayoutNode(aspect, constraints);

    Equal(child.LayoutCount, 1u);
    Expect(child.LastConstraints == BoxConstraints.Tight(new Sizef(100.0f, 50.0f)));
    CheckSizef(aspect.Size(), 100.0f, 50.0f);
    CheckSizef(aspect.GetDryLayout(constraints), 100.0f, 50.0f);
    ExpectNear(aspect.AspectRatio(), 2.0f, kFloatEpsilon);
    ExpectNear(aspect.GetMinIntrinsicWidth(10.0f), 20.0f, kFloatEpsilon);
    ExpectNear(aspect.GetMaxIntrinsicWidth(10.0f), 20.0f, kFloatEpsilon);
    ExpectNear(aspect.GetMinIntrinsicHeight(30.0f), 15.0f, kFloatEpsilon);
    ExpectNear(aspect.GetMaxIntrinsicHeight(30.0f), 15.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/aspect-ratio bounded height and constraint iteration")]
public static void Case14(){

    var child = new TestLeaf();
    var aspect = new VisualAspectRatio();
    aspect.SetAspectRatio(0.5f);
    aspect.SetChild(child);

    BoxConstraints bounded = new BoxConstraints(0.0f, 100.0f, 0.0f, 100.0f);
    VisualTestHelpers.LayoutNode(aspect, bounded);

    Equal(child.LayoutCount, 1u);
    Expect(child.LastConstraints == BoxConstraints.Tight(new Sizef(50.0f, 100.0f)));
    CheckSizef(aspect.Size(), 50.0f, 100.0f);
    CheckSizef(aspect.GetDryLayout(bounded), 50.0f, 100.0f);

    aspect.SetAspectRatio(16.0f / 9.0f);
    BoxConstraints height_tight = new BoxConstraints(0.0f, float.PositiveInfinity, 100.0f, 100.0f);
    VisualTestHelpers.LayoutNode(aspect, height_tight);

    Equal(child.LayoutCount, 2u);
    CheckConstraints(child.LastConstraints, 1600.0f / 9.0f, 1600.0f / 9.0f, 100.0f, 100.0f);
    CheckSizef(aspect.Size(), 1600.0f / 9.0f, 100.0f);
    CheckSizef(aspect.GetDryLayout(height_tight), 1600.0f / 9.0f, 100.0f);

    aspect.SetAspectRatio(2.0f);
    BoxConstraints conflicting = new BoxConstraints(80.0f, 100.0f, 80.0f, 100.0f);
    VisualTestHelpers.LayoutNode(aspect, conflicting);

    Equal(child.LayoutCount, 3u);
    Expect(child.LastConstraints == BoxConstraints.Tight(new Sizef(100.0f, 80.0f)));
    CheckSizef(aspect.Size(), 100.0f, 80.0f);
    CheckSizef(aspect.GetDryLayout(conflicting), 100.0f, 80.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/aspect-ratio no child uses ratio size")]
public static void Case15(){

    var aspect = new VisualAspectRatio();
    aspect.SetAspectRatio(4.0f);

    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 40.0f);
    VisualTestHelpers.LayoutNode(aspect, constraints);

    Expect(aspect.Child() == null);
    CheckSizef(aspect.Size(), 50.0f, 12.5f);
    CheckSizef(aspect.GetDryLayout(constraints), 50.0f, 12.5f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/intrinsic-width steps child constraints")]
public static void Case16(){

    var child = new TestIntrinsicLeaf();
    var intrinsic = new VisualIntrinsicWidth();
    intrinsic.SetStepWidth((float?)(8.0f));
    intrinsic.SetStepHeight((float?)(10.0f));
    intrinsic.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(intrinsic, constraints);

    Expect(intrinsic.StepWidth());
    Expect(intrinsic.StepHeight());
    ExpectNear(intrinsic.StepWidth().Value, 8.0f, kFloatEpsilon);
    ExpectNear(intrinsic.StepHeight().Value, 10.0f, kFloatEpsilon);
    Equal(child.LayoutCount, 1u);
    CheckConstraints(child.LastConstraints, 24.0f, 24.0f, 20.0f, 20.0f);
    CheckSizef(intrinsic.Size(), 24.0f, 20.0f);

    CheckSizef(intrinsic.GetDryLayout(constraints), 24.0f, 20.0f);
    Equal(child.DryLayoutCount, 1u);
    CheckConstraints(child.LastDryConstraints, 24.0f, 24.0f, 20.0f, 20.0f);

    ExpectNear(intrinsic.GetMinIntrinsicWidth(50.0f), 24.0f, kFloatEpsilon);
    ExpectNear(intrinsic.GetMaxIntrinsicWidth(50.0f), 24.0f, kFloatEpsilon);
    ExpectNear(intrinsic.GetMinIntrinsicHeight(30.0f), 10.0f, kFloatEpsilon);
    ExpectNear(intrinsic.GetMaxIntrinsicHeight(30.0f), 20.0f, kFloatEpsilon);

    VisualTestHelpers.LayoutNode(intrinsic, BoxConstraints.TightWidth(40.0f));

    Equal(child.LayoutCount, 2u);
    CheckConstraints(child.LastConstraints, 40.0f, 40.0f, 20.0f, 20.0f);

    intrinsic.ClearStepWidth();
    intrinsic.ClearStepHeight();
    ExpectFalse(intrinsic.StepWidth());
    ExpectFalse(intrinsic.StepHeight());

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/intrinsic-width no child")]
public static void Case17(){

    var intrinsic = new VisualIntrinsicWidth();
    BoxConstraints constraints = new BoxConstraints(5.0f, 100.0f, 6.0f, 80.0f);

    VisualTestHelpers.LayoutNode(intrinsic, constraints);

    CheckSizef(intrinsic.Size(), 5.0f, 6.0f);
    CheckSizef(intrinsic.GetDryLayout(constraints), 5.0f, 6.0f);
    ExpectNear(intrinsic.GetMinIntrinsicWidth(10.0f), 0.0f, kFloatEpsilon);
    ExpectNear(intrinsic.GetMaxIntrinsicHeight(10.0f), 0.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/intrinsic-height tightens child height")]
public static void Case18(){

    var child = new TestIntrinsicLeaf();
    child.PreferredSize = new Sizef(32.0f, 99.0f);

    var intrinsic = new VisualIntrinsicHeight();
    intrinsic.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(intrinsic, constraints);

    Equal(child.LayoutCount, 1u);
    CheckConstraints(child.LastConstraints, 0.0f, 100.0f, 17.0f, 17.0f);
    CheckSizef(intrinsic.Size(), 32.0f, 17.0f);

    CheckSizef(intrinsic.GetDryLayout(constraints), 32.0f, 17.0f);
    Equal(child.DryLayoutCount, 1u);
    CheckConstraints(child.LastDryConstraints, 0.0f, 100.0f, 17.0f, 17.0f);

    ExpectNear(intrinsic.GetMinIntrinsicHeight(20.0f), 17.0f, kFloatEpsilon);
    ExpectNear(intrinsic.GetMaxIntrinsicHeight(20.0f), 17.0f, kFloatEpsilon);
    ExpectNear(intrinsic.GetMinIntrinsicWidth(10.0f), 11.0f, kFloatEpsilon);
    ExpectNear(intrinsic.GetMaxIntrinsicWidth(10.0f), 23.0f, kFloatEpsilon);

    ExpectNear(intrinsic.GetMinIntrinsicWidth(float.PositiveInfinity), 11.0f, kFloatEpsilon);
    ExpectNear(child.LastMinIntrinsicWidthHeight, 17.0f, kFloatEpsilon);

    VisualTestHelpers.LayoutNode(intrinsic, BoxConstraints.TightHeight(40.0f));

    Equal(child.LayoutCount, 2u);
    CheckConstraints(child.LastConstraints, 0.0f, float.PositiveInfinity, 40.0f, 40.0f);

    intrinsic.ClearChild();
    VisualTestHelpers.LayoutNode(intrinsic, new BoxConstraints(5.0f, 100.0f, 6.0f, 80.0f));

    CheckSizef(intrinsic.Size(), 5.0f, 6.0f);
    CheckSizef(intrinsic.GetDryLayout(new BoxConstraints(5.0f, 100.0f, 6.0f, 80.0f)), 5.0f, 6.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/baseline wrappers forward dry constraints")]
public static void Case19(){

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);

    var constrained_child = new TestLeaf();
    constrained_child.DryBaseline = 5.0f;
    var constrained = new VisualConstrained();
    constrained.SetWidth(30.0f);
    constrained.SetChild(constrained_child);
    ExpectNear(constrained.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 5.0f, kFloatEpsilon);
    CheckConstraints(constrained_child.LastDryBaselineConstraints, 30.0f, 30.0f, 0.0f, 80.0f);

    var aspect_child = new TestLeaf();
    aspect_child.DryBaseline = 6.0f;
    var aspect = new VisualAspectRatio();
    aspect.SetAspectRatio(2.0f);
    aspect.SetChild(aspect_child);
    ExpectNear(aspect.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 6.0f, kFloatEpsilon);
    CheckConstraints(aspect_child.LastDryBaselineConstraints, 100.0f, 100.0f, 50.0f, 50.0f);

    var width_child = new TestIntrinsicLeaf();
    width_child.DryBaseline = 7.0f;
    var intrinsic_width = new VisualIntrinsicWidth();
    intrinsic_width.SetStepWidth((float?)(8.0f));
    intrinsic_width.SetStepHeight((float?)(10.0f));
    intrinsic_width.SetChild(width_child);
    ExpectNear(intrinsic_width.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 7.0f, kFloatEpsilon);
    CheckConstraints(width_child.LastDryBaselineConstraints, 24.0f, 24.0f, 20.0f, 20.0f);

    var height_child = new TestIntrinsicLeaf();
    height_child.DryBaseline = 8.0f;
    var intrinsic_height = new VisualIntrinsicHeight();
    intrinsic_height.SetChild(height_child);
    ExpectNear(intrinsic_height.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 8.0f, kFloatEpsilon);
    CheckConstraints(height_child.LastDryBaselineConstraints, 0.0f, 100.0f, 17.0f, 17.0f);

    var fitted_child = new TestLeaf();
    fitted_child.DryBaseline = 9.0f;
    var fitted = new VisualFitted();
    fitted.SetChild(fitted_child);
    ExpectNear(fitted.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 9.0f, kFloatEpsilon);
    CheckConstraints(
        fitted_child.LastDryBaselineConstraints,
        0.0f,
        float.PositiveInfinity,
        0.0f,
        float.PositiveInfinity
    );

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/flex horizontal distributes flex and spacing")]
public static void Case20(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(20.0f, 10.0f);
    var tight = new TestIntrinsicLeaf();
    tight.PreferredSize = new Sizef(10.0f, 12.0f);
    var loose = new TestIntrinsicLeaf();
    loose.PreferredSize = new Sizef(5.0f, 8.0f);

    var flex = new VisualFlex();
    flex.SetSpacing(5.0f);
    flex.AddChild(first);
    flex.AddChild(tight);
    flex.AddChild(loose);
    VisualFlexSlot first_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(0));
    VisualFlexSlot tight_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(1));
    VisualFlexSlot loose_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(2));
    tight_slot.SetFlex(1);
    loose_slot.SetFlex(2);
    loose_slot.SetFit(EFlexFit.Loose);

    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(100.0f, 40.0f));
    VisualTestHelpers.LayoutNode(flex, constraints);

    Expect(flex.Direction() == EAxis.Horizontal);
    Expect(flex.MainAxisSize() == EMainAxisSize.Max);
    Expect(flex.MainAxisAlignment() == EMainAxisAlignment.Start);
    Expect(flex.CrossAxisAlignment() == ECrossAxisAlignment.Center);
    ExpectNear(flex.Spacing(), 5.0f, kFloatEpsilon);
    Equal(flex.NumChildren(), 3u);
    Expect(flex.ChildAt(0) == first);
    VisualNode flex_node = flex;
    Expect(flex_node.ChildAt(1) == tight);
    Expect(tight.Parent() == flex);
    Equal(tight.ParentIndex(), 1u);
    Expect(first_slot.Flex() == 0);
    Expect(tight_slot.Flex() == 1);
    Expect(loose_slot.Flex() == 2);
    Expect(tight_slot.Fit() == EFlexFit.Tight);
    Expect(loose_slot.Fit() == EFlexFit.Loose);

    CheckSizef(flex.Size(), 100.0f, 40.0f);
    CheckSizef(first.Size(), 20.0f, 10.0f);
    CheckSizef(tight.Size(), 70.0f / 3.0f, 12.0f);
    CheckSizef(loose.Size(), 5.0f, 8.0f);
    CheckConstraints(first.LastConstraints, 0.0f, float.PositiveInfinity, 0.0f, 40.0f);
    CheckConstraints(tight.LastConstraints, 70.0f / 3.0f, 70.0f / 3.0f, 0.0f, 40.0f);
    CheckConstraints(loose.LastConstraints, 0.0f, 140.0f / 3.0f, 0.0f, 40.0f);
    CheckOffsetf(first_slot.Offset(), 0.0f, 15.0f);
    CheckOffsetf(tight_slot.Offset(), 25.0f, 14.0f);
    CheckOffsetf(loose_slot.Offset(), 160.0f / 3.0f, 16.0f);
    ExpectNear(flex.Overflow(), 0.0f, kFloatEpsilon);
    ExpectFalse(flex.IsOverflowing());

    CheckSizef(flex.GetDryLayout(constraints), 100.0f, 40.0f);
    Equal(first.DryLayoutCount, 1u);
    Equal(tight.DryLayoutCount, 1u);
    Equal(loose.DryLayoutCount, 1u);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/flex rtl and vertical direction flip offsets")]
public static void Case21(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(10.0f, 10.0f);
    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(20.0f, 10.0f);

    var flex = new VisualFlex();
    flex.SetSpacing(5.0f);
    flex.SetTextDirection(ETextDirection.RTL);
    flex.SetVerticalDirection(EVerticalDirection.Up);
    flex.SetMainAxisAlignment(EMainAxisAlignment.SpaceBetween);
    flex.SetCrossAxisAlignment(ECrossAxisAlignment.Start);
    flex.AddChild(first);
    flex.AddChild(second);
    VisualFlexSlot first_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(0));
    VisualFlexSlot second_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(1));

    VisualTestHelpers.LayoutNode(flex, BoxConstraints.Tight(new Sizef(100.0f, 40.0f)));

    CheckSizef(flex.Size(), 100.0f, 40.0f);
    CheckOffsetf(second_slot.Offset(), 0.0f, 30.0f);
    CheckOffsetf(first_slot.Offset(), 90.0f, 30.0f);

    flex.SetMainAxisAlignment(EMainAxisAlignment.End);
    flex.SetTextDirection(ETextDirection.LTR);
    flex.SetVerticalDirection(EVerticalDirection.Down);
    VisualTestHelpers.LayoutNode(flex, BoxConstraints.Tight(new Sizef(100.0f, 40.0f)));

    CheckOffsetf(first_slot.Offset(), 65.0f, 0.0f);
    CheckOffsetf(second_slot.Offset(), 80.0f, 0.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/flex vertical shrink wraps and stretches cross axis")]
public static void Case22(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(20.0f, 10.0f);
    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(30.0f, 15.0f);

    var flex = new VisualFlex();
    flex.SetDirection(EAxis.Vertical);
    flex.SetMainAxisSize(EMainAxisSize.Min);
    flex.SetCrossAxisAlignment(ECrossAxisAlignment.Stretch);
    flex.SetSpacing(4.0f);
    flex.AddChild(first);
    flex.AddChild(second);
    VisualFlexSlot first_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(0));
    VisualFlexSlot second_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(1));

    VisualTestHelpers.LayoutNode(flex, new BoxConstraints(0.0f, 60.0f, 0.0f, 100.0f));

    CheckSizef(flex.Size(), 60.0f, 29.0f);
    CheckSizef(first.Size(), 60.0f, 10.0f);
    CheckSizef(second.Size(), 60.0f, 15.0f);
    CheckConstraints(first.LastConstraints, 60.0f, 60.0f, 0.0f, float.PositiveInfinity);
    CheckConstraints(second.LastConstraints, 60.0f, 60.0f, 0.0f, float.PositiveInfinity);
    CheckOffsetf(first_slot.Offset(), 0.0f, 0.0f);
    CheckOffsetf(second_slot.Offset(), 0.0f, 14.0f);
    CheckSizef(flex.GetDryLayout(new BoxConstraints(0.0f, 60.0f, 0.0f, 100.0f)), 60.0f, 29.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/flex baseline alignment")]
public static void Case23(){

    var tall = new TestIntrinsicLeaf();
    tall.PreferredSize = new Sizef(20.0f, 20.0f);
    tall.ActualBaseline = 15.0f;
    tall.DryBaseline = 15.0f;

    var low = new TestIntrinsicLeaf();
    low.PreferredSize = new Sizef(20.0f, 20.0f);
    low.ActualBaseline = 5.0f;
    low.DryBaseline = 5.0f;

    var flex = new VisualFlex();
    flex.SetMainAxisSize(EMainAxisSize.Min);
    flex.SetCrossAxisAlignment(ECrossAxisAlignment.Baseline);
    flex.SetTextBaseline(ETextBaseline.Alphabetic);
    flex.AddChild(tall);
    flex.AddChild(low);

    VisualTestHelpers.LayoutNode(flex, new BoxConstraints(0.0f, 100.0f, 0.0f, 100.0f));

    Expect(flex.TextBaseline());
    Expect(flex.TextBaseline().Value == ETextBaseline.Alphabetic);
    CheckSizef(flex.Size(), 40.0f, 30.0f);
    CheckOffsetf(SlotOf<VisualFlexSlot>(flex.ChildAt(0)).Offset(), 0.0f, 0.0f);
    CheckOffsetf(SlotOf<VisualFlexSlot>(flex.ChildAt(1)).Offset(), 20.0f, 10.0f);
    ExpectNear(flex.GetDistanceToActualBaseline(ETextBaseline.Alphabetic).Value, 15.0f, kFloatEpsilon);
    ExpectNear(flex.GetDistanceToBaseline(ETextBaseline.Alphabetic).Value, 15.0f, kFloatEpsilon);
    ExpectNear(flex.GetDryBaseline(new BoxConstraints(0.0f, 100.0f, 0.0f, 100.0f), ETextBaseline.Alphabetic).Value, 15.0f, kFloatEpsilon);

    flex.ClearTextBaseline();
    ExpectFalse(flex.TextBaseline());

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/flex vertical baseline uses first child baseline")]
public static void Case24(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(20.0f, 10.0f);

    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(30.0f, 15.0f);
    second.ActualBaseline = 6.0f;
    second.DryBaseline = 6.0f;

    var flex = new VisualFlex();
    flex.SetDirection(EAxis.Vertical);
    flex.SetMainAxisSize(EMainAxisSize.Min);
    flex.SetSpacing(4.0f);
    flex.AddChild(first);
    flex.AddChild(second);

    BoxConstraints constraints = new BoxConstraints(0.0f, 60.0f, 0.0f, 100.0f);
    VisualTestHelpers.LayoutNode(flex, constraints);

    CheckSizef(flex.Size(), 30.0f, 29.0f);
    CheckOffsetf(SlotOf<VisualFlexSlot>(flex.ChildAt(0)).Offset(), 5.0f, 0.0f);
    CheckOffsetf(SlotOf<VisualFlexSlot>(flex.ChildAt(1)).Offset(), 0.0f, 14.0f);
    ExpectNear(flex.GetDistanceToActualBaseline(ETextBaseline.Alphabetic).Value, 20.0f, kFloatEpsilon);
    ExpectNear(flex.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 20.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/flex intrinsic main axis follows flex fractions")]
public static void Case25(){

    var inflexible = new TestIntrinsicLeaf();
    inflexible.MinIntrinsicWidthResult = 5.0f;
    inflexible.MaxIntrinsicWidthResult = 10.0f;
    var flex_two = new TestIntrinsicLeaf();
    flex_two.MinIntrinsicWidthResult = 20.0f;
    flex_two.MaxIntrinsicWidthResult = 40.0f;
    var flex_one = new TestIntrinsicLeaf();
    flex_one.MinIntrinsicWidthResult = 9.0f;
    flex_one.MaxIntrinsicWidthResult = 12.0f;

    var flex = new VisualFlex();
    flex.SetSpacing(2.0f);
    flex.AddChild(inflexible);
    flex.AddChild(flex_two);
    flex.AddChild(flex_one);
    VisualFlexSlot two_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(1));
    VisualFlexSlot one_slot = SlotOf<VisualFlexSlot>(flex.ChildAt(2));
    two_slot.SetFlex(2);
    one_slot.SetFlex(1);

    ExpectNear(flex.GetMinIntrinsicWidth(30.0f), 39.0f, kFloatEpsilon);
    ExpectNear(flex.GetMaxIntrinsicWidth(30.0f), 74.0f, kFloatEpsilon);

    VisualNode removed_child = flex.RemoveChildAt(1);
    Expect(removed_child == flex_two);
    Expect(flex_two.Parent() == null);
    Equal(flex_one.ParentIndex(), 1u);
    Expect(flex.RemoveChild(flex_one));
    ExpectFalse(flex.RemoveChild(flex_one));
    flex.ClearChildren();
    Equal(flex.NumChildren(), 0u);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/wrap horizontal wraps and aligns runs")]
public static void Case26(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(30.0f, 10.0f);
    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(40.0f, 20.0f);
    var third = new TestIntrinsicLeaf();
    third.PreferredSize = new Sizef(50.0f, 15.0f);
    var fourth = new TestIntrinsicLeaf();
    fourth.PreferredSize = new Sizef(20.0f, 8.0f);

    var wrap = new VisualWrap();
    wrap.SetSpacing(5.0f);
    wrap.SetRunSpacing(7.0f);
    wrap.AddChild(first);
    wrap.AddChild(second);
    wrap.AddChild(third);
    wrap.AddChild(fourth);

    VisualTestHelpers.LayoutNode(wrap, BoxConstraints.Tight(new Sizef(100.0f, 80.0f)));

    Expect(wrap.Direction() == EAxis.Horizontal);
    Expect(wrap.Alignment() == EWrapAlignment.Start);
    Expect(wrap.RunAlignment() == EWrapAlignment.Start);
    Expect(wrap.CrossAxisAlignment() == EWrapCrossAlignment.Start);
    ExpectNear(wrap.Spacing(), 5.0f, kFloatEpsilon);
    ExpectNear(wrap.RunSpacing(), 7.0f, kFloatEpsilon);
    Equal(wrap.NumChildren(), 4u);
    Expect(wrap.ChildAt(1) == second);
    VisualNode wrap_node = wrap;
    Expect(wrap_node.ChildAt(2) == third);
    Expect(second.Parent() == wrap);
    Equal(second.ParentIndex(), 1u);

    CheckSizef(wrap.Size(), 100.0f, 80.0f);
    CheckConstraints(first.LastConstraints, 0.0f, 100.0f, 0.0f, float.PositiveInfinity);
    CheckConstraints(second.LastConstraints, 0.0f, 100.0f, 0.0f, float.PositiveInfinity);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(0)).Offset(), 0.0f, 0.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(1)).Offset(), 35.0f, 0.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(2)).Offset(), 0.0f, 27.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(3)).Offset(), 55.0f, 27.0f);
    CheckSizef(wrap.Overflow(), 0.0f, 0.0f);
    ExpectFalse(wrap.IsOverflowing());
    CheckSizef(wrap.GetDryLayout(new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f)), 75.0f, 42.0f);

    ExpectNear(wrap.GetMinIntrinsicWidth(80.0f), 11.0f, kFloatEpsilon);
    ExpectNear(wrap.GetMaxIntrinsicWidth(80.0f), 92.0f, kFloatEpsilon);
    ExpectNear(wrap.GetMinIntrinsicHeight(100.0f), 42.0f, kFloatEpsilon);
    ExpectNear(wrap.GetMaxIntrinsicHeight(100.0f), 42.0f, kFloatEpsilon);

    VisualNode removed_child = wrap.RemoveChildAt(1);
    Expect(removed_child == second);
    Expect(second.Parent() == null);
    Equal(third.ParentIndex(), 1u);
    Expect(wrap.RemoveChild(third));
    ExpectFalse(wrap.RemoveChild(third));
    wrap.ClearChildren();
    Equal(wrap.NumChildren(), 0u);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/wrap distributes child and run alignment")]
public static void Case27(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(30.0f, 10.0f);
    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(40.0f, 20.0f);
    var third = new TestIntrinsicLeaf();
    third.PreferredSize = new Sizef(50.0f, 15.0f);
    var fourth = new TestIntrinsicLeaf();
    fourth.PreferredSize = new Sizef(20.0f, 8.0f);

    var wrap = new VisualWrap();
    wrap.SetSpacing(5.0f);
    wrap.SetRunSpacing(7.0f);
    wrap.SetAlignment(EWrapAlignment.Center);
    wrap.SetRunAlignment(EWrapAlignment.SpaceBetween);
    wrap.SetCrossAxisAlignment(EWrapCrossAlignment.End);
    wrap.AddChild(first);
    wrap.AddChild(second);
    wrap.AddChild(third);
    wrap.AddChild(fourth);

    VisualTestHelpers.LayoutNode(wrap, BoxConstraints.Tight(new Sizef(100.0f, 80.0f)));

    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(0)).Offset(), 12.5f, 10.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(1)).Offset(), 47.5f, 0.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(2)).Offset(), 12.5f, 65.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(3)).Offset(), 67.5f, 72.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/wrap dry layout uses strict flutter line break")]
public static void Case28(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(50.00003f, 10.0f);
    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(50.00003f, 10.0f);

    var wrap = new VisualWrap();
    wrap.AddChild(first);
    wrap.AddChild(second);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.00001f, 0.0f, 100.0f);
    CheckSizef(wrap.GetDryLayout(constraints), 50.00003f, 20.0f);

    VisualTestHelpers.LayoutNode(wrap, constraints);

    CheckSizef(wrap.Size(), 50.00003f, 20.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(0)).Offset(), 0.0f, 0.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(1)).Offset(), 0.0f, 10.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/flex and wrap use flutter overflow precision")]
public static void Case29(){

    var flex_first = new TestLeaf();
    flex_first.LayoutSize = new Sizef(1.0e-9f, 1.0e-9f);
    var flex_second = new TestLeaf();
    flex_second.LayoutSize = new Sizef(1.0e-9f, 1.0e-9f);

    var flex = new VisualFlex();
    flex.AddChild(flex_first);
    flex.AddChild(flex_second);
    VisualTestHelpers.LayoutNode(flex, BoxConstraints.Tight(new Sizef(1.95e-9f, 1.0e-9f)));

    Expect(flex.Overflow() > 0.0f);
    Expect(flex.Overflow() < GuiConstants.FlutterPrecisionErrorTolerance);
    ExpectFalse(flex.IsOverflowing());

    var wrap_first = new TestLeaf();
    wrap_first.LayoutSize = new Sizef(1.0e-9f, 1.0e-9f);
    var wrap_second = new TestLeaf();
    wrap_second.LayoutSize = new Sizef(1.0e-9f, 1.0e-9f);

    var wrap = new VisualWrap();
    wrap.AddChild(wrap_first);
    wrap.AddChild(wrap_second);
    VisualTestHelpers.LayoutNode(wrap, BoxConstraints.Tight(new Sizef(1.0e-9f, 1.95e-9f)));

    Expect(wrap.Overflow().Height > 0.0f);
    Expect(wrap.Overflow().Height < GuiConstants.FlutterPrecisionErrorTolerance);
    Expect(wrap.IsOverflowing());

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/wrap rtl and vertical direction flip offsets")]
public static void Case30(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(30.0f, 10.0f);
    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(40.0f, 20.0f);
    var third = new TestIntrinsicLeaf();
    third.PreferredSize = new Sizef(50.0f, 15.0f);
    var fourth = new TestIntrinsicLeaf();
    fourth.PreferredSize = new Sizef(20.0f, 8.0f);

    var wrap = new VisualWrap();
    wrap.SetSpacing(5.0f);
    wrap.SetRunSpacing(7.0f);
    wrap.SetTextDirection(ETextDirection.RTL);
    wrap.SetVerticalDirection(EVerticalDirection.Up);
    wrap.AddChild(first);
    wrap.AddChild(second);
    wrap.AddChild(third);
    wrap.AddChild(fourth);

    VisualTestHelpers.LayoutNode(wrap, BoxConstraints.Tight(new Sizef(100.0f, 80.0f)));

    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(0)).Offset(), 70.0f, 70.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(1)).Offset(), 25.0f, 60.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(2)).Offset(), 50.0f, 38.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(3)).Offset(), 25.0f, 45.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/wrap vertical wraps columns and uses highest baseline")]
public static void Case31(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(10.0f, 30.0f);
    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(20.0f, 20.0f);
    second.ActualBaseline = 10.0f;
    second.DryBaseline = 10.0f;
    var third = new TestIntrinsicLeaf();
    third.PreferredSize = new Sizef(15.0f, 25.0f);
    third.ActualBaseline = 4.0f;
    third.DryBaseline = 4.0f;

    var wrap = new VisualWrap();
    wrap.SetDirection(EAxis.Vertical);
    wrap.SetSpacing(5.0f);
    wrap.SetRunSpacing(7.0f);
    wrap.AddChild(first);
    wrap.AddChild(second);
    wrap.AddChild(third);

    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(80.0f, 60.0f));
    VisualTestHelpers.LayoutNode(wrap, constraints);

    CheckSizef(wrap.Size(), 80.0f, 60.0f);
    CheckConstraints(first.LastConstraints, 0.0f, float.PositiveInfinity, 0.0f, 60.0f);
    CheckConstraints(second.LastConstraints, 0.0f, float.PositiveInfinity, 0.0f, 60.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(0)).Offset(), 0.0f, 0.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(1)).Offset(), 0.0f, 35.0f);
    CheckOffsetf(SlotOf<VisualWrapSlot>(wrap.ChildAt(2)).Offset(), 27.0f, 0.0f);
    ExpectNear(wrap.GetDistanceToActualBaseline(ETextBaseline.Alphabetic).Value, 4.0f, kFloatEpsilon);
    ExpectNear(wrap.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 4.0f, kFloatEpsilon);
    CheckSizef(wrap.GetDryLayout(new BoxConstraints(0.0f, 80.0f, 0.0f, 60.0f)), 42.0f, 55.0f);

    ExpectNear(wrap.GetMinIntrinsicWidth(60.0f), 42.0f, kFloatEpsilon);
    ExpectNear(wrap.GetMaxIntrinsicWidth(60.0f), 42.0f, kFloatEpsilon);
    ExpectNear(wrap.GetMinIntrinsicHeight(80.0f), 7.0f, kFloatEpsilon);
    ExpectNear(wrap.GetMaxIntrinsicHeight(80.0f), 51.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/stack fit controls non-positioned child constraints")]
public static void Case32(){

    var child = new TestIntrinsicLeaf();
    child.PreferredSize = new Sizef(10.0f, 10.0f);

    var stack = new VisualStack();
    stack.AddChild(child);

    BoxConstraints constraints = new BoxConstraints(20.0f, 100.0f, 15.0f, 80.0f);
    VisualTestHelpers.LayoutNode(stack, constraints);

    Expect(stack.Fit() == EStackFit.Loose);
    Expect(stack.ClipBehavior() == EClipBehavior.HardEdge);
    Expect(stack.Alignment() == AlignmentDirectional.TopStart());
    Expect(stack.ResolvedAlignment() == Alignment.TopLeft());
    CheckConstraints(child.LastConstraints, 0.0f, 100.0f, 0.0f, 80.0f);
    CheckSizef(child.Size(), 10.0f, 10.0f);
    CheckSizef(stack.Size(), 20.0f, 15.0f);
    CheckOffsetf(SlotOf<VisualStackSlot>(stack.ChildAt(0)).Offset(), 0.0f, 0.0f);

    stack.SetFit(EStackFit.Expand);
    VisualTestHelpers.LayoutNode(stack, constraints);

    CheckConstraints(child.LastConstraints, 100.0f, 100.0f, 80.0f, 80.0f);
    CheckSizef(child.Size(), 100.0f, 80.0f);
    CheckSizef(stack.Size(), 100.0f, 80.0f);

    stack.SetFit(EStackFit.Passthrough);
    VisualTestHelpers.LayoutNode(stack, constraints);

    CheckConstraints(child.LastConstraints, 20.0f, 100.0f, 15.0f, 80.0f);
    CheckSizef(child.Size(), 20.0f, 15.0f);
    CheckSizef(stack.Size(), 20.0f, 15.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/stack aligns non-positioned children")]
public static void Case33(){

    var first = new TestIntrinsicLeaf();
    first.PreferredSize = new Sizef(20.0f, 10.0f);
    first.MinIntrinsicWidthResult = 12.0f;
    first.MaxIntrinsicWidthResult = 22.0f;
    first.MinIntrinsicHeightResult = 7.0f;
    first.MaxIntrinsicHeightResult = 17.0f;

    var second = new TestIntrinsicLeaf();
    second.PreferredSize = new Sizef(40.0f, 30.0f);
    second.MinIntrinsicWidthResult = 18.0f;
    second.MaxIntrinsicWidthResult = 28.0f;
    second.MinIntrinsicHeightResult = 9.0f;
    second.MaxIntrinsicHeightResult = 19.0f;

    var stack = new VisualStack();
    stack.SetAlignment(Alignment.Center());
    stack.AddChild(first);
    stack.AddChild(second);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(stack, constraints);

    CheckSizef(stack.Size(), 40.0f, 30.0f);
    CheckOffsetf(SlotOf<VisualStackSlot>(stack.ChildAt(0)).Offset(), 10.0f, 10.0f);
    CheckOffsetf(SlotOf<VisualStackSlot>(stack.ChildAt(1)).Offset(), 0.0f, 0.0f);
    ExpectNear(stack.GetMinIntrinsicWidth(10.0f), 18.0f, kFloatEpsilon);
    ExpectNear(stack.GetMaxIntrinsicWidth(10.0f), 28.0f, kFloatEpsilon);
    ExpectNear(stack.GetMinIntrinsicHeight(10.0f), 9.0f, kFloatEpsilon);
    ExpectNear(stack.GetMaxIntrinsicHeight(10.0f), 19.0f, kFloatEpsilon);

    stack.SetAlignment(AlignmentDirectional.TopStart());
    stack.SetTextDirection(ETextDirection.RTL);
    VisualTestHelpers.LayoutNode(stack, constraints);

    Expect(stack.ResolvedAlignment() == Alignment.TopRight());
    CheckOffsetf(SlotOf<VisualStackSlot>(stack.ChildAt(0)).Offset(), 20.0f, 0.0f);
    CheckOffsetf(SlotOf<VisualStackSlot>(stack.ChildAt(1)).Offset(), 0.0f, 0.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/stack lays out positioned children")]
public static void Case34(){

    var @base = new TestIntrinsicLeaf();
    @base.PreferredSize = new Sizef(50.0f, 40.0f);

    var fill = new TestIntrinsicLeaf();
    fill.PreferredSize = new Sizef(10.0f, 10.0f);

    var overflow = new TestIntrinsicLeaf();
    overflow.PreferredSize = new Sizef(20.0f, 10.0f);

    var stack = new VisualStack();
    stack.AddChild(@base);
    stack.AddChild(fill);
    VisualStackSlot fill_slot = SlotOf<VisualStackSlot>(fill);
    fill_slot.SetPosition(10.0f, 3.0f, 5.0f, 7.0f);
    stack.AddChild(overflow);
    VisualStackSlot overflow_slot = SlotOf<VisualStackSlot>(overflow);
    overflow_slot.SetPosition(-5.0f, 35.0f, null, null, 20.0f, 10.0f);

    VisualTestHelpers.LayoutNode(stack, new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f));

    Expect(fill_slot.IsPositioned());
    Expect(overflow_slot.IsPositioned());
    CheckSizef(stack.Size(), 50.0f, 40.0f);
    CheckConstraints(fill.LastConstraints, 35.0f, 35.0f, 30.0f, 30.0f);
    CheckSizef(fill.Size(), 35.0f, 30.0f);
    CheckOffsetf(fill_slot.Offset(), 10.0f, 3.0f);
    CheckConstraints(overflow.LastConstraints, 20.0f, 20.0f, 10.0f, 10.0f);
    CheckOffsetf(overflow_slot.Offset(), -5.0f, 35.0f);
    Expect(stack.IsOverflowing());

    fill_slot.ClearPosition();
    ExpectFalse(fill_slot.IsPositioned());

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/stack baseline uses highest child baseline")]
public static void Case35(){

    var @base = new TestIntrinsicLeaf();
    @base.PreferredSize = new Sizef(20.0f, 10.0f);
    @base.ActualBaseline = 8.0f;
    @base.DryBaseline = 8.0f;

    var top = new TestIntrinsicLeaf();
    top.PreferredSize = new Sizef(20.0f, 10.0f);
    top.ActualBaseline = 5.0f;
    top.DryBaseline = 5.0f;

    var bottom = new TestIntrinsicLeaf();
    bottom.PreferredSize = new Sizef(20.0f, 10.0f);
    bottom.ActualBaseline = 6.0f;
    bottom.DryBaseline = 6.0f;

    var stack = new VisualStack();
    stack.SetAlignment(Alignment.Center());
    stack.AddChild(@base);
    stack.AddChild(top);
    VisualStackSlot top_slot = SlotOf<VisualStackSlot>(top);
    top_slot.SetTop(30.0f);
    stack.AddChild(bottom);
    VisualStackSlot bottom_slot = SlotOf<VisualStackSlot>(bottom);
    bottom_slot.SetBottom(10.0f);

    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(100.0f, 80.0f));
    VisualTestHelpers.LayoutNode(stack, constraints);

    CheckOffsetf(SlotOf<VisualStackSlot>(stack.ChildAt(0)).Offset(), 40.0f, 35.0f);
    CheckOffsetf(top_slot.Offset(), 40.0f, 30.0f);
    CheckOffsetf(bottom_slot.Offset(), 40.0f, 60.0f);
    ExpectNear(stack.GetDistanceToActualBaseline(ETextBaseline.Alphabetic).Value, 35.0f, kFloatEpsilon);
    ExpectNear(stack.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 35.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/shifted no child")]
public static void Case36(){

    var shifted = new TestShifted();
    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 40.0f);

    VisualTestHelpers.LayoutNode(shifted, constraints);

    Expect(shifted.Child() == null);
    CheckOffsetf(shifted.ChildOffset(), 0.0f, 0.0f);
    CheckOffsetf(shifted.ObservedLayoutOffset, 0.0f, 0.0f);
    CheckSizef(shifted.Size(), 10.0f, 5.0f);
    CheckSizef(shifted.GetDryLayout(constraints), 10.0f, 5.0f);
    ExpectNear(shifted.GetMinIntrinsicWidth(10.0f), 0.0f, kFloatEpsilon);
    ExpectNear(shifted.GetMaxIntrinsicHeight(10.0f), 0.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/baseline shifted padding and positioned offsets")]
public static void Case37(){

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);

    var shifted_child = new TestLeaf();
    shifted_child.ActualBaseline = 6.0f;
    shifted_child.DryBaseline = 7.0f;
    var shifted = new TestShifted();
    shifted.SetChild(shifted_child);
    VisualTestHelpers.LayoutNode(shifted, constraints);

    ExpectNear(shifted.GetDistanceToActualBaseline(ETextBaseline.Alphabetic).Value, 15.0f, kFloatEpsilon);
    ExpectNear(shifted.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 7.0f, kFloatEpsilon);

    var padding_child = new TestLeaf();
    padding_child.LayoutSize = new Sizef(20.0f, 10.0f);
    padding_child.DrySize = new Sizef(21.0f, 11.0f);
    padding_child.ActualBaseline = 6.0f;
    padding_child.DryBaseline = 7.0f;
    var padding = new VisualPadding();
    padding.SetPadding(EdgeInsets.FromLTRB(1.0f, 2.0f, 3.0f, 4.0f));
    padding.SetChild(padding_child);
    VisualTestHelpers.LayoutNode(padding, constraints);

    ExpectNear(padding.GetDistanceToActualBaseline(ETextBaseline.Alphabetic).Value, 8.0f, kFloatEpsilon);
    ExpectNear(padding.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 9.0f, kFloatEpsilon);
    CheckConstraints(padding_child.LastDryBaselineConstraints, 0.0f, 96.0f, 0.0f, 74.0f);

    var positioned_child = new TestLeaf();
    positioned_child.LayoutSize = new Sizef(20.0f, 10.0f);
    positioned_child.DrySize = new Sizef(20.0f, 10.0f);
    positioned_child.ActualBaseline = 6.0f;
    positioned_child.DryBaseline = 4.0f;
    var positioned = new VisualPositioned();
    positioned.SetChild(positioned_child);
    VisualTestHelpers.LayoutNode(positioned, constraints);

    CheckOffsetf(positioned.ChildOffset(), 40.0f, 35.0f);
    ExpectNear(positioned.GetDistanceToActualBaseline(ETextBaseline.Alphabetic).Value, 41.0f, kFloatEpsilon);
    ExpectNear(positioned.GetDryBaseline(constraints, ETextBaseline.Alphabetic).Value, 39.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/positioned factors shrink wrap and scale intrinsic")]
public static void Case38(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(20.0f, 10.0f);
    child.DrySize = new Sizef(21.0f, 11.0f);

    var positioned = new VisualPositioned();
    positioned.SetWidthFactor((float?)(2.0f));
    positioned.SetHeightFactor((float?)(3.0f));
    positioned.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(positioned, constraints);

    Expect(positioned.WidthFactor());
    Expect(positioned.HeightFactor());
    ExpectNear(positioned.WidthFactor().Value, 2.0f, kFloatEpsilon);
    ExpectNear(positioned.HeightFactor().Value, 3.0f, kFloatEpsilon);
    CheckSizef(positioned.Size(), 40.0f, 30.0f);
    CheckOffsetf(positioned.ChildOffset(), 10.0f, 10.0f);
    CheckSizef(positioned.GetDryLayout(constraints), 42.0f, 33.0f);
    ExpectNear(positioned.GetMinIntrinsicWidth(10.0f), 22.0f, kFloatEpsilon);
    ExpectNear(positioned.GetMaxIntrinsicWidth(10.0f), 24.0f, kFloatEpsilon);
    ExpectNear(positioned.GetMinIntrinsicHeight(10.0f), 39.0f, kFloatEpsilon);
    ExpectNear(positioned.GetMaxIntrinsicHeight(10.0f), 42.0f, kFloatEpsilon);

    positioned.ClearWidthFactor();
    positioned.ClearHeightFactor();
    ExpectFalse(positioned.WidthFactor());
    ExpectFalse(positioned.HeightFactor());

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/positioned unbounded axes and directional alignment")]
public static void Case39(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(20.0f, 10.0f);

    var positioned = new VisualPositioned();
    positioned.SetAlignment(AlignmentDirectional.CenterEnd());
    positioned.SetChild(child);

    BoxConstraints mixed = new BoxConstraints(0.0f, float.PositiveInfinity, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(positioned, mixed);

    CheckSizef(positioned.Size(), 20.0f, 80.0f);
    CheckOffsetf(positioned.ChildOffset(), 0.0f, 35.0f);
    Expect(positioned.ResolvedAlignment() == Alignment.CenterRight());

    BoxConstraints bounded = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(positioned, bounded);

    CheckSizef(positioned.Size(), 100.0f, 80.0f);
    CheckOffsetf(positioned.ChildOffset(), 80.0f, 35.0f);
    Expect(positioned.ResolvedAlignment() == Alignment.CenterRight());

    positioned.SetTextDirection(ETextDirection.RTL);
    VisualTestHelpers.LayoutNode(positioned, bounded);

    CheckSizef(positioned.Size(), 100.0f, 80.0f);
    CheckOffsetf(positioned.ChildOffset(), 0.0f, 35.0f);
    Expect(positioned.ResolvedAlignment() == Alignment.CenterLeft());

    positioned.ClearChild();
    VisualTestHelpers.LayoutNode(positioned, new BoxConstraints(5.0f, 100.0f, 6.0f, 80.0f));

    CheckSizef(positioned.Size(), 100.0f, 80.0f);
    CheckOffsetf(positioned.ChildOffset(), 0.0f, 0.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/fitted scale-down layout")]
public static void Case40(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(20.0f, 10.0f);
    child.DrySize = new Sizef(20.0f, 10.0f);

    var fitted = new VisualFitted();
    fitted.SetFit(EBoxFit.ScaleDown);
    fitted.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(30.0f, 100.0f, 0.0f, 100.0f);
    VisualTestHelpers.LayoutNode(fitted, constraints);

    CheckSizef(fitted.Size(), 30.0f, 10.0f);
    CheckSizef(fitted.GetDryLayout(constraints), 30.0f, 10.0f);

    fitted.SetFit(EBoxFit.Contain);
    VisualTestHelpers.LayoutNode(fitted, constraints);

    CheckSizef(fitted.Size(), 30.0f, 15.0f);
    CheckSizef(fitted.GetDryLayout(constraints), 30.0f, 15.0f);

    fitted.ClearChild();
    VisualTestHelpers.LayoutNode(fitted, new BoxConstraints(5.0f, 100.0f, 6.0f, 80.0f));
    CheckSizef(fitted.Size(), 5.0f, 6.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/padding directional resolve and no child")]
public static void Case41(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(20.0f, 10.0f);

    var padding = new VisualPadding();
    padding.SetPadding(EdgeInsetsDirectional.FromSTEB(1.0f, 2.0f, 3.0f, 4.0f));
    padding.SetTextDirection(ETextDirection.RTL);
    padding.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(padding, constraints);

    CheckInsets(padding.ResolvedPadding(), 3.0f, 2.0f, 1.0f, 4.0f);
    CheckOffsetf(padding.ChildOffset(), 3.0f, 2.0f);
    CheckSizef(padding.Size(), 24.0f, 16.0f);

    padding.ClearChild();
    VisualTestHelpers.LayoutNode(padding, new BoxConstraints(10.0f, 100.0f, 8.0f, 80.0f));

    CheckSizef(padding.Size(), 10.0f, 8.0f);
    CheckOffsetf(padding.ChildOffset(), 0.0f, 0.0f);
    ExpectNear(padding.GetMinIntrinsicWidth(20.0f), 4.0f, kFloatEpsilon);
    ExpectNear(padding.GetMaxIntrinsicHeight(20.0f), 6.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/constrained fixed size no child")]
public static void Case42(){

    var constrained = new VisualConstrained();
    constrained.SetSized(new Sizef(30.0f, 12.0f));

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 100.0f);
    VisualTestHelpers.LayoutNode(constrained, constraints);

    Expect(constrained.Child() == null);
    Expect(constrained.AdditionalConstraints() == BoxConstraints.Tight(new Sizef(30.0f, 12.0f)));
    CheckSizef(constrained.Size(), 30.0f, 12.0f);
    CheckSizef(constrained.GetDryLayout(constraints), 30.0f, 12.0f);
    ExpectNear(constrained.GetMinIntrinsicWidth(10.0f), 30.0f, kFloatEpsilon);
    ExpectNear(constrained.GetMaxIntrinsicWidth(10.0f), 30.0f, kFloatEpsilon);
    ExpectNear(constrained.GetMinIntrinsicHeight(10.0f), 12.0f, kFloatEpsilon);
    ExpectNear(constrained.GetMaxIntrinsicHeight(10.0f), 12.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/constrained constrains child")]
public static void Case43(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(30.0f, 20.0f);
    child.DrySize = new Sizef(18.0f, 20.0f);

    var constrained = new VisualConstrained();
    constrained.SetAdditionalConstraints(VisualConstrained.Sized((float?)(30.0f), null));
    constrained.SetChild(child);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 100.0f);
    VisualTestHelpers.LayoutNode(constrained, constraints);

    Equal(child.LayoutCount, 1u);
    Expect(child.LastConstraints == new BoxConstraints(30.0f, 30.0f, 0.0f, 100.0f));
    CheckSizef(constrained.Size(), 30.0f, 20.0f);
    CheckSizef(constrained.GetDryLayout(constraints), 30.0f, 20.0f);
    ExpectNear(constrained.GetMinIntrinsicWidth(10.0f), 30.0f, kFloatEpsilon);
    ExpectNear(constrained.GetMaxIntrinsicWidth(10.0f), 30.0f, kFloatEpsilon);
    ExpectNear(constrained.GetMinIntrinsicHeight(10.0f), 13.0f, kFloatEpsilon);
    ExpectNear(constrained.GetMaxIntrinsicHeight(10.0f), 14.0f, kFloatEpsilon);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/constrained expand and shrink helpers")]
public static void Case44(){

    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 40.0f);

    var expanded = new VisualConstrained();
    expanded.SetExpand();
    VisualTestHelpers.LayoutNode(expanded, constraints);
    Expect(expanded.AdditionalConstraints() == VisualConstrained.Expand());
    CheckSizef(expanded.Size(), 50.0f, 40.0f);
    CheckSizef(expanded.GetDryLayout(constraints), 50.0f, 40.0f);
    ExpectNear(expanded.GetMinIntrinsicWidth(10.0f), 0.0f, kFloatEpsilon);
    ExpectNear(expanded.GetMaxIntrinsicHeight(10.0f), 0.0f, kFloatEpsilon);

    var shrunk = new VisualConstrained();
    shrunk.SetShrink();
    VisualTestHelpers.LayoutNode(shrunk, constraints);
    Expect(shrunk.AdditionalConstraints() == VisualConstrained.Shrink());
    CheckSizef(shrunk.Size(), 10.0f, 5.0f);
    CheckSizef(shrunk.GetDryLayout(constraints), 10.0f, 5.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/constrained width and height helpers")]
public static void Case45(){

    BoxConstraints constraints = new BoxConstraints(10.0f, 50.0f, 5.0f, 40.0f);

    var width_only = new VisualConstrained();
    width_only.SetAdditionalConstraints(VisualConstrained.Width(25.0f));
    VisualTestHelpers.LayoutNode(width_only, constraints);
    Expect(width_only.AdditionalConstraints() == BoxConstraints.TightWidth(25.0f));
    CheckSizef(width_only.Size(), 25.0f, 5.0f);
    Expect(width_only.AdditionalConstraints() == VisualConstrained.Sized((float?)(25.0f), null));

    var height_only = new VisualConstrained();
    height_only.SetAdditionalConstraints(VisualConstrained.Height(16.0f));
    VisualTestHelpers.LayoutNode(height_only, constraints);
    Expect(height_only.AdditionalConstraints() == BoxConstraints.TightHeight(16.0f));
    CheckSizef(height_only.Size(), 10.0f, 16.0f);
    Expect(height_only.AdditionalConstraints() == VisualConstrained.Sized(null, (float?)(16.0f)));

    var expand_width = new VisualConstrained();
    expand_width.SetExpand((float?)(25.0f), null);
    VisualTestHelpers.LayoutNode(expand_width, constraints);
    Expect(expand_width.AdditionalConstraints() == new BoxConstraints(25.0f, 25.0f, float.PositiveInfinity, float.PositiveInfinity));
    CheckSizef(expand_width.Size(), 25.0f, 40.0f);

    var expand_axis = new VisualConstrained();
    expand_axis.SetWidth(30.0f);
    expand_axis.SetHeight(18.0f);
    expand_axis.SetWidthExpand();
    VisualTestHelpers.LayoutNode(expand_axis, constraints);
    Expect(expand_axis.AdditionalConstraints() == new BoxConstraints(float.PositiveInfinity, float.PositiveInfinity, 18.0f, 18.0f));
    CheckSizef(expand_axis.Size(), 50.0f, 18.0f);

    expand_axis.SetHeightExpand();
    VisualTestHelpers.LayoutNode(expand_axis, constraints);
    Expect(expand_axis.AdditionalConstraints() == VisualConstrained.Expand());
    CheckSizef(expand_axis.Size(), 50.0f, 40.0f);

    var constrained = new VisualConstrained();
    constrained.SetWidth(30.0f);
    VisualTestHelpers.LayoutNode(constrained, constraints);
    Expect(constrained.AdditionalConstraints() == BoxConstraints.TightWidth(30.0f));
    CheckSizef(constrained.Size(), 30.0f, 5.0f);

    constrained.SetHeight(18.0f);
    VisualTestHelpers.LayoutNode(constrained, constraints);
    Expect(constrained.AdditionalConstraints() == BoxConstraints.Tight(new Sizef(30.0f, 18.0f)));
    CheckSizef(constrained.Size(), 30.0f, 18.0f);

    constrained.ClearWidth();
    VisualTestHelpers.LayoutNode(constrained, constraints);
    Expect(constrained.AdditionalConstraints() == BoxConstraints.TightHeight(18.0f));
    CheckSizef(constrained.Size(), 10.0f, 18.0f);

    constrained.ClearHeight();
    VisualTestHelpers.LayoutNode(constrained, constraints);
    Expect(constrained.AdditionalConstraints() == new BoxConstraints());
    CheckSizef(constrained.Size(), 10.0f, 5.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/limited limits only unbounded axes")]
public static void Case46(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(24.0f, 18.0f);
    child.DrySize = new Sizef(80.0f, 90.0f);

    var limited = new VisualLimited();
    limited.SetMaxWidth(30.0f);
    limited.SetMaxHeight(40.0f);
    limited.SetChild(child);

    BoxConstraints unbounded = new();
    VisualTestHelpers.LayoutNode(limited, unbounded);

    Equal(child.LayoutCount, 1u);
    Expect(child.LastConstraints == new BoxConstraints(0.0f, 30.0f, 0.0f, 40.0f));
    CheckSizef(limited.Size(), 24.0f, 18.0f);
    CheckSizef(limited.GetDryLayout(unbounded), 30.0f, 40.0f);
    ExpectNear(limited.MaxWidth(), 30.0f, kFloatEpsilon);
    ExpectNear(limited.MaxHeight(), 40.0f, kFloatEpsilon);

    BoxConstraints bounded = new BoxConstraints(5.0f, 100.0f, 6.0f, 90.0f);
    VisualTestHelpers.LayoutNode(limited, bounded);

    Equal(child.LayoutCount, 2u);
    Expect(child.LastConstraints == bounded);
    CheckSizef(limited.Size(), 24.0f, 18.0f);
    CheckSizef(limited.GetDryLayout(bounded), 80.0f, 90.0f);

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/limited mixed constraints and no child")]
public static void Case47(){

    var child = new TestLeaf();
    child.LayoutSize = new Sizef(75.0f, 50.0f);
    child.DrySize = new Sizef(75.0f, 50.0f);

    var limited = new VisualLimited();
    limited.SetMaxWidth(30.0f);
    limited.SetMaxHeight(40.0f);
    limited.SetChild(child);

    BoxConstraints mixed = new BoxConstraints(10.0f, 60.0f, 5.0f, float.PositiveInfinity);
    VisualTestHelpers.LayoutNode(limited, mixed);

    Expect(child.LastConstraints == new BoxConstraints(10.0f, 60.0f, 5.0f, 40.0f));
    CheckSizef(limited.Size(), 60.0f, 50.0f);
    CheckSizef(limited.GetDryLayout(mixed), 60.0f, 40.0f);

    limited.ClearChild();
    VisualTestHelpers.LayoutNode(limited, new BoxConstraints(8.0f, float.PositiveInfinity, 6.0f, float.PositiveInfinity));

    Expect(limited.Child() == null);
    CheckSizef(limited.Size(), 8.0f, 6.0f);
    CheckSizef(
        limited.GetDryLayout(new BoxConstraints(8.0f, float.PositiveInfinity, 6.0f, float.PositiveInfinity)),
        8.0f,
        6.0f
    );

}
[GuiTest("visual/visual_node_tests.cpp::gui/visual-node/limited keeps proxy intrinsic behavior")]
public static void Case48(){

    var child = new TestLeaf();

    var limited = new VisualLimited();
    limited.SetMaxWidth(1.0f);
    limited.SetMaxHeight(1.0f);
    limited.SetChild(child);

    ExpectNear(limited.GetMinIntrinsicWidth(10.0f), 11.0f, kFloatEpsilon);
    ExpectNear(limited.GetMaxIntrinsicWidth(10.0f), 12.0f, kFloatEpsilon);
    ExpectNear(limited.GetMinIntrinsicHeight(10.0f), 13.0f, kFloatEpsilon);
    ExpectNear(limited.GetMaxIntrinsicHeight(10.0f), 14.0f, kFloatEpsilon);

}
}
