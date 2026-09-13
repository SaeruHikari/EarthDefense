using System.Numerics;
using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class LayoutSchedulingTests
{
private class LayoutCountLeaf : VisualLeaf{
public Sizef FixedSize = new Sizef(10.0f, 10.0f);
public ulong LayoutCount = 0u;
protected override void PerformLayout(){
        ++LayoutCount;
        SetSize(Constraints().Constrain(FixedSize));
    }
protected override float ComputeMinIntrinsicWidth(float unused){ return 0.0f; }
protected override float ComputeMaxIntrinsicWidth(float unused){ return 0.0f; }
protected override float ComputeMinIntrinsicHeight(float unused){ return 0.0f; }
protected override float ComputeMaxIntrinsicHeight(float unused){ return 0.0f; }
protected override Sizef ComputeDryLayout(BoxConstraints constraints){
        return constraints.Constrain(FixedSize);
    }
}
private class CountingPair : VisualMultiChild{
public LayoutCountLeaf Rhs = null;
public LayoutCountLeaf Lhs = null;
public ulong LayoutCount = 0u;
protected override void PerformLayout(){
        ++LayoutCount;
        float Width = 0.0f;
        float Height = 0.0f;
        for (ulong i = 0u; i < NumChildren(); ++i)
        {
            LayoutChild(ChildAt(i), Constraints());
            Sizef child_size = ChildAt(i).Size();
            Width += child_size.Width;
            Height = CppMath.Max(Height, child_size.Height);
        }
        SetSize(Constraints().Constrain(new Sizef(Width, Height)));
    }
protected override float ComputeMinIntrinsicWidth(float h){
        float Total = 0.0f;
        for (ulong i = 0u; i < NumChildren(); ++i) { Total += ChildAt(i).GetMinIntrinsicWidth(h); }
        return Total;
    }
protected override float ComputeMaxIntrinsicWidth(float h){
        float Total = 0.0f;
        for (ulong i = 0u; i < NumChildren(); ++i) { Total += ChildAt(i).GetMaxIntrinsicWidth(h); }
        return Total;
    }
protected override float ComputeMinIntrinsicHeight(float w){
        float MaxValue = 0.0f;
        for (ulong i = 0u; i < NumChildren(); ++i) { MaxValue = CppMath.Max(MaxValue, ChildAt(i).GetMinIntrinsicHeight(w)); }
        return MaxValue;
    }
protected override float ComputeMaxIntrinsicHeight(float w){
        float MaxValue = 0.0f;
        for (ulong i = 0u; i < NumChildren(); ++i) { MaxValue = CppMath.Max(MaxValue, ChildAt(i).GetMaxIntrinsicHeight(w)); }
        return MaxValue;
    }
protected override Sizef ComputeDryLayout(BoxConstraints constraints){
        return constraints.Biggest();
    }
protected override bool HitTestChildren(VisualHitTestResult result, Offsetf position){


        return false;
    }
public CountingPair(){
        Lhs = new LayoutCountLeaf();
        Rhs = new LayoutCountLeaf();
        RebuildAddChild(Lhs, 0u);
        RebuildAddChild(Rhs, 1u);
        RebuildFlush();
    }
}
private class CountingVisibility : VisualVisibility{
public ulong LayoutCount = 0u;
protected override void PerformLayout(){
        ++LayoutCount;


        base.PerformLayout();
    }
}
private class CountingAspectRatio : VisualAspectRatio{
public ulong LayoutCount = 0u;
protected override void PerformLayout(){
        ++LayoutCount;
        if (Child() is {} child) { LayoutChild(child,Constraints()); SetSize(child.Size()); return; } SetSize(ComputeSizeForNoChild(Constraints()));
    }
}
private class LayoutSchedulingFixture{
public VisualTestBackend Backend = new VisualTestBackend();
public TextServices TextServices = TextServices.CreateFallback();
public VisualOwner MakeOwner(){
        return new VisualOwner(TextServices, Backend);
    }
}
private class CountingProxy : VisualProxy{
public ulong LayoutCount = 0u;
protected override void PerformLayout(){
        ++LayoutCount;


        base.PerformLayout();
    }
}
private static T SlotOf<T>(VisualNode node)where T:VisualSlot {Check.NotNull(node);Check.NotNull(node.Slot());return (T)node.Slot()!;}
private static void CheckHitTestTransform(Matrix4x4 transform,Offsetf position,float x,float y){var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);if(point.W!=1){point.X/=point.W;point.Y/=point.W;}CheckOffsetf(new(point.X,point.Y),x,y);}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/same-constraints-short-circuits")]
public static void Case0(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var leaf = new LayoutCountLeaf();
    owner.SetRoot(leaf);

    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(20.0f, 20.0f));
    VisualTestHelpers.LayoutNode(owner, constraints);
    Equal(leaf.LayoutCount, 1u);


    VisualTestHelpers.LayoutNode(owner, constraints);
    Equal(leaf.LayoutCount, 1u);

}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/constraint-change-relayouts")]
public static void Case1(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var leaf = new LayoutCountLeaf();
    owner.SetRoot(leaf);

    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));
    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(30.0f, 20.0f)));
    Equal(leaf.LayoutCount, 2u);

}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/set-child-dirty-only-relayouts-its-path")]
public static void Case2(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var pair = new CountingPair();
    owner.SetRoot(pair);

    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(60.0f, 20.0f)));
    Equal(pair.Lhs.LayoutCount, 1u);
    Equal(pair.Rhs.LayoutCount, 1u);
    Equal(pair.LayoutCount, 1u);



    pair.Lhs.FixedSize = new Sizef(12.0f, 10.0f);
    pair.Lhs.MarkNeedsLayout();
    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(60.0f, 20.0f)));

    Equal(pair.Lhs.LayoutCount, 2u);
    Equal(pair.Rhs.LayoutCount, 1u);

}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/first-layout-always-runs")]
public static void Case3(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var leaf = new LayoutCountLeaf();
    owner.SetRoot(leaf);

    Expect(leaf.NeedsLayout());
    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));
    Equal(leaf.LayoutCount, 1u);
    ExpectFalse(leaf.NeedsLayout());

}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/visibility-boundary-isolates-child")]
public static void Case4(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var visibility = new CountingVisibility();
    var leaf = new LayoutCountLeaf();
    visibility.SetChild(leaf);
    owner.SetRoot(visibility);


    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));
    Equal(visibility.LayoutCount, 1u);
    Equal(leaf.LayoutCount, 1u);


    leaf.MarkNeedsLayout();
    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));
    Equal(visibility.LayoutCount, 1u);
    Equal(leaf.LayoutCount, 2u);


    visibility.SetMaintain(true);
    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));
    leaf.MarkNeedsLayout();
    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));
    Equal(visibility.LayoutCount, 2u);
    Equal(leaf.LayoutCount, 3u);

}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/dirty-ancestor-lays-out-descendants-once")]
public static void Case5(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var pair = new CountingPair();
    owner.SetRoot(pair);

    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(60.0f, 20.0f)));
    Equal(pair.LayoutCount, 1u);





    pair.MarkNeedsLayout();
    pair.Lhs.MarkNeedsLayout();
    VisualTestHelpers.LayoutNode(owner, BoxConstraints.Tight(new Sizef(60.0f, 20.0f)));

    Equal(pair.LayoutCount, 2u);
    Equal(pair.Lhs.LayoutCount, 2u);
    Equal(pair.Rhs.LayoutCount, 1u);

}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/sized-by-parent-isolates-parent")]
public static void Case6(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var proxy = new CountingProxy();
    var aspect = new CountingAspectRatio();
    var leaf = new LayoutCountLeaf();
    aspect.SetChild(leaf);
    proxy.SetChild(aspect);
    owner.SetRoot(proxy);



    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(owner, constraints);
    Equal(proxy.LayoutCount, 1u);
    Equal(aspect.LayoutCount, 1u);
    Equal(leaf.LayoutCount, 1u);



    leaf.MarkNeedsLayout();
    VisualTestHelpers.LayoutNode(owner, constraints);
    Equal(leaf.LayoutCount, 2u);
    Equal(aspect.LayoutCount, 2u);
    Equal(proxy.LayoutCount, 1u);

}
[GuiTest("visual/layout_scheduling_tests.cpp::gui/layout-scheduling/sized-by-parent-attribute-change-relayouts-parent")]
public static void Case7(){
var fixture=new LayoutSchedulingFixture();

    var owner = fixture.MakeOwner();
    var proxy = new CountingProxy();
    var aspect = new CountingAspectRatio();
    var leaf = new LayoutCountLeaf();
    aspect.SetChild(leaf);
    proxy.SetChild(aspect);
    owner.SetRoot(proxy);

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(owner, constraints);
    Equal(proxy.LayoutCount, 1u);
    Equal(aspect.LayoutCount, 1u);



    aspect.SetAspectRatio(0.5f);
    VisualTestHelpers.LayoutNode(owner, constraints);
    Equal(aspect.LayoutCount, 2u);
    Equal(proxy.LayoutCount, 2u);

}
}
