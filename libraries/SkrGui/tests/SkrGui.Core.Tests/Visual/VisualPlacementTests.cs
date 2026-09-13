using System.Numerics;
using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualPlacementTests
{
private class PlacementTest : VisualLeaf{
public ulong DryBaselineCount = 0u;
public ulong DryLayoutCount = 0u;
public ulong PaintCount = 0u;
public ulong LayoutCount = 0u;
public Rectf LastPaintClipRect = Rectf.Largest();
public Offsetf LastPaintOrigin = new();
public BoxConstraints LastDryConstraints = new();
public BoxConstraints LastConstraints = new();
public float? ActualBaseline = null;
public float? DryBaseline = null;
public Sizef PreferredSize = new Sizef(20.0f, 10.0f);
protected override void PerformLayout(){
        LastConstraints = Constraints();
        ++LayoutCount;
        SetSize(Constraints().Constrain(PreferredSize));
    }
protected override float ComputeMinIntrinsicWidth(float height){

        return PreferredSize.Width;
    }
protected override float ComputeMaxIntrinsicWidth(float height){

        return PreferredSize.Width;
    }
protected override float ComputeMinIntrinsicHeight(float width){

        return PreferredSize.Height;
    }
protected override float ComputeMaxIntrinsicHeight(float width){

        return PreferredSize.Height;
    }
protected override Sizef ComputeDryLayout(BoxConstraints constraints){
        LastDryConstraints = constraints;
        ++DryLayoutCount;
        return constraints.Constrain(PreferredSize);
    }
protected override float? ComputeDryBaseline(BoxConstraints constraints, ETextBaseline baseline){

        LastDryConstraints = constraints;
        ++DryBaselineCount;
        return DryBaseline;
    }
protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){

        return ActualBaseline;
    }
}
private static T SlotOf<T>(VisualNode node)where T:VisualSlot {Check.NotNull(node);Check.NotNull(node.Slot());return (T)node.Slot()!;}
private static void CheckHitTestTransform(Matrix4x4 transform,Offsetf position,float x,float y){var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);if(point.W!=1){point.X/=point.W;point.Y/=point.W;}CheckOffsetf(new(point.X,point.Y),x,y);}
[GuiTest("visual/multi_child/visual_placement_tests.cpp::gui/visual-placement/axis-size-rules-and-default-fill")]
public static void Case0(){

    var child = new PlacementTest();

    var placement = new VisualPlacement();
    placement.SetWidth(90.0f);
    placement.SetHeight(60.0f);
    placement.SetWidthBounds((float?)(100.0f), (float?)(150.0f));
    placement.AddChild(child);

    BoxConstraints constraints = new BoxConstraints(0.0f, 200.0f, 0.0f, 120.0f);
    CheckSizef(placement.GetDryLayout(constraints), 100.0f, 60.0f);
    Equal(child.DryLayoutCount, 0u);

    VisualTestHelpers.LayoutNode(placement, constraints);

    CheckSizef(placement.Size(), 100.0f, 60.0f);
    CheckConstraints(child.LastConstraints, 100.0f, 100.0f, 60.0f, 60.0f);
    CheckOffsetf(SlotOf<VisualPlacementSlot>(child).Offset(), 0.0f, 0.0f);
    CheckSizef(child.Size(), 100.0f, 60.0f);
    ExpectFalse(placement.IsOverflowing());
    ExpectNear(placement.GetMinIntrinsicWidth(0.0f), 100.0f, kFloatEpsilon);
    ExpectNear(placement.GetMaxIntrinsicHeight(0.0f), 60.0f, kFloatEpsilon);

    Equal(placement.WidthRule().Kind(), EVisualPlacementAxisSizeKind.Sized);
    ExpectNear(placement.WidthRule().ValuePx(), 90.0f, kFloatEpsilon);
    Equal(placement.WidthRule().MinValuePx().Value, 100.0f);
    Equal(placement.WidthRule().MaxValuePx().Value, 150.0f);

    placement.SetWidthExpand();
    placement.SetHeightShrink();
    CheckSizef(
        placement.GetDryLayout(new BoxConstraints(12.0f, 220.0f, 8.0f, 140.0f)),
        220.0f,
        8.0f
    );

}
[GuiTest("visual/multi_child/visual_placement_tests.cpp::gui/visual-placement/inset-pin-and-align-placement")]
public static void Case1(){

    var inset_child = new PlacementTest();
    var pinned_child = new PlacementTest();
    var aligned_child = new PlacementTest();

    var placement = new VisualPlacement();

    Placement inset = Placement.Fill();
    inset.Inset().LeftPx(10.0f).RightPx(20.0f).TopPx(5.0f).BottomPx(15.0f);
    placement.AddChild(inset_child);
    VisualPlacementSlot inset_slot = SlotOf<VisualPlacementSlot>(inset_child);
    inset_slot.SetPlacement(inset);

    Placement pinned = new();
    pinned.PinRBPx(10.0f, 5.0f).SizePx(new Sizef(40.0f, 20.0f));
    placement.AddChild(pinned_child);
    VisualPlacementSlot pinned_slot = SlotOf<VisualPlacementSlot>(pinned_child);
    pinned_slot.SetPlacement(pinned);

    Placement aligned = new();
    aligned.AlignCenter().SizePx(new Sizef(50.0f, 20.0f));
    placement.AddChild(aligned_child);
    VisualPlacementSlot aligned_slot = SlotOf<VisualPlacementSlot>(aligned_child);
    aligned_slot.SetPlacement(aligned);

    VisualTestHelpers.LayoutNode(placement, BoxConstraints.Tight(new Sizef(200.0f, 100.0f)));

    CheckSizef(placement.Size(), 200.0f, 100.0f);
    CheckConstraints(inset_child.LastConstraints, 170.0f, 170.0f, 80.0f, 80.0f);
    CheckOffsetf(inset_slot.Offset(), 10.0f, 5.0f);
    CheckConstraints(pinned_child.LastConstraints, 40.0f, 40.0f, 20.0f, 20.0f);
    CheckOffsetf(pinned_slot.Offset(), 150.0f, 75.0f);
    CheckConstraints(aligned_child.LastConstraints, 50.0f, 50.0f, 20.0f, 20.0f);
    CheckOffsetf(aligned_slot.Offset(), 75.0f, 40.0f);
    ExpectFalse(placement.IsOverflowing());

}
[GuiTest("visual/multi_child/visual_placement_tests.cpp::gui/visual-placement/loose-placement-uses-measured-child-size")]
public static void Case2(){

    var child = new PlacementTest();

    var placement = new VisualPlacement();
    placement.AddChild(child);

    Placement child_placement = new();
    child_placement.Inset(Alignment.BottomRight()).AllPx(10.0f);
    VisualPlacementSlot slot = SlotOf<VisualPlacementSlot>(child);
    slot.SetPlacement(child_placement);
    slot.SetSizeMode(EPlacementSizeMode.Loose);

    VisualTestHelpers.LayoutNode(placement, BoxConstraints.Tight(new Sizef(200.0f, 100.0f)));

    Equal(slot.SizeMode(), EPlacementSizeMode.Loose);
    CheckConstraints(child.LastConstraints, 0.0f, 180.0f, 0.0f, 80.0f);
    CheckSizef(child.Size(), 20.0f, 10.0f);
    CheckOffsetf(slot.Offset(), 170.0f, 80.0f);
    ExpectFalse(placement.IsOverflowing());

}
[GuiTest("visual/multi_child/visual_placement_tests.cpp::gui/visual-placement/children-do-not-inflate-container")]
public static void Case3(){

    var child = new PlacementTest();

    var placement = new VisualPlacement();
    placement.SetShrink();

    Placement child_placement = new();
    child_placement.PinLTPx(0.0f, 0.0f).SizePx(new Sizef(50.0f, 40.0f));
    placement.AddChild(child);
    VisualPlacementSlot slot = SlotOf<VisualPlacementSlot>(child);
    slot.SetPlacement(child_placement);

    VisualTestHelpers.LayoutNode(placement, new BoxConstraints(10.0f, 100.0f, 5.0f, 80.0f));

    CheckSizef(placement.Size(), 10.0f, 5.0f);
    CheckSizef(child.Size(), 50.0f, 40.0f);
    CheckOffsetf(slot.Offset(), 0.0f, 0.0f);
    Expect(placement.IsOverflowing());

}
[GuiTest("visual/multi_child/visual_placement_tests.cpp::gui/visual-placement/baseline-uses-first-child-with-baseline")]
public static void Case4(){

    var first = new PlacementTest();
    var second = new PlacementTest();
    second.DryBaseline = 3.0f;
    second.ActualBaseline = 4.0f;
    var third = new PlacementTest();
    third.DryBaseline = 1.0f;
    third.ActualBaseline = 1.0f;

    var placement = new VisualPlacement();

    Placement first_placement = new();
    first_placement.PinLTPx(0.0f, 0.0f).SizePx(new Sizef(10.0f, 10.0f));
    placement.AddChild(first);
    SlotOf<VisualPlacementSlot>(first).SetPlacement(first_placement);

    Placement second_placement = new();
    second_placement.PinLTPx(10.0f, 20.0f).SizePx(new Sizef(20.0f, 10.0f));
    placement.AddChild(second);
    VisualPlacementSlot second_slot = SlotOf<VisualPlacementSlot>(second);
    second_slot.SetPlacement(second_placement);

    Placement third_placement = new();
    third_placement.PinLTPx(0.0f, 0.0f).SizePx(new Sizef(20.0f, 10.0f));
    placement.AddChild(third);
    SlotOf<VisualPlacementSlot>(third).SetPlacement(third_placement);

    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(100.0f, 80.0f));
    float? DryBaseline = placement.GetDryBaseline(constraints, ETextBaseline.Alphabetic);
    Expect(DryBaseline);
    ExpectNear(DryBaseline.Value, 23.0f, kFloatEpsilon);
    Equal(first.DryBaselineCount, 1u);
    Equal(second.DryBaselineCount, 1u);
    Equal(third.DryBaselineCount, 0u);

    VisualTestHelpers.LayoutNode(placement, constraints);
    float? ActualBaseline = placement.GetDistanceToActualBaseline(ETextBaseline.Alphabetic);
    Expect(ActualBaseline);
    ExpectNear(ActualBaseline.Value, 24.0f, kFloatEpsilon);
    CheckOffsetf(second_slot.Offset(), 10.0f, 20.0f);

}
}
