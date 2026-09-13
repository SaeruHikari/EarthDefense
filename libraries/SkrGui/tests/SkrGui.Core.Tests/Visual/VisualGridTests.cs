using System.Numerics;
using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualGridTests
{
private class GridTest : VisualLeaf{
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
public float MaxIntrinsicWidth = 20.0f;
public Sizef PreferredSize = new Sizef(20.0f, 10.0f);
protected override void PerformLayout(){
        LastConstraints = Constraints();
        ++LayoutCount;
        SetSize(Constraints().Constrain(PreferredSize));
    }
protected override float ComputeMinIntrinsicWidth(float height){

        return MaxIntrinsicWidth;
    }
protected override float ComputeMaxIntrinsicWidth(float height){

        return MaxIntrinsicWidth;
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
[GuiTest("visual/multi_child/visual_grid_tests.cpp::gui/visual-grid/track-sizing-and-layout")]
public static void Case0(){

    VisualGridTrackSize[] columns = {
        VisualGridTrackSize.Fixed(100.0f),
        VisualGridTrackSize.Auto(),
        VisualGridTrackSize.Star(1.0f),
        VisualGridTrackSize.Percent(0.25f),
    };
    VisualGridTrackSize[] rows = {
        VisualGridTrackSize.Auto(),
        VisualGridTrackSize.Star(1.0f),
    };

    var fixed_child = new GridTest();
    fixed_child.PreferredSize = new Sizef(40.0f, 10.0f);
    fixed_child.MaxIntrinsicWidth = 40.0f;

    var auto_child = new GridTest();
    auto_child.PreferredSize = new Sizef(50.0f, 20.0f);
    auto_child.MaxIntrinsicWidth = 50.0f;

    var star_child = new GridTest();
    star_child.PreferredSize = new Sizef(16.0f, 12.0f);
    star_child.MaxIntrinsicWidth = 16.0f;

    var grid = new VisualGrid();
    grid.SetColumns(columns);
    grid.SetRows(rows);
    grid.SetGap(10.0f, 5.0f);

    grid.AddChild(fixed_child);
    VisualGridSlot fixed_slot = SlotOf<VisualGridSlot>(fixed_child);
    fixed_slot.SetArea(0u, 0u);
    grid.AddChild(auto_child);
    VisualGridSlot auto_slot = SlotOf<VisualGridSlot>(auto_child);
    auto_slot.SetArea(0u, 1u);
    grid.AddChild(star_child);
    VisualGridSlot star_slot = SlotOf<VisualGridSlot>(star_child);
    star_slot.SetArea(1u, 2u);

    VisualTestHelpers.LayoutNode(grid, BoxConstraints.Tight(new Sizef(400.0f, 200.0f)));

    CheckSizef(grid.Size(), 400.0f, 200.0f);
    CheckOffsetf(fixed_slot.Offset(), 0.0f, 0.0f);
    CheckOffsetf(auto_slot.Offset(), 110.0f, 0.0f);
    CheckOffsetf(star_slot.Offset(), 170.0f, 25.0f);

    CheckConstraints(fixed_child.LastConstraints, 100.0f, 100.0f, 20.0f, 20.0f);
    CheckConstraints(auto_child.LastConstraints, 50.0f, 50.0f, 20.0f, 20.0f);
    CheckConstraints(star_child.LastConstraints, 127.5f, 127.5f, 175.0f, 175.0f);

    Equal(fixed_child.LayoutCount, 1u);
    Equal(auto_child.LayoutCount, 1u);
    Equal(star_child.LayoutCount, 1u);
    ExpectFalse(grid.IsOverflowing());

}
[GuiTest("visual/multi_child/visual_grid_tests.cpp::gui/visual-grid/rtl-and-alignment")]
public static void Case1(){

    VisualGridTrackSize[] columns = {
        VisualGridTrackSize.Fixed(50.0f),
        VisualGridTrackSize.Fixed(70.0f),
    };
    VisualGridTrackSize[] rows = {
        VisualGridTrackSize.Fixed(40.0f),
    };

    var child = new GridTest();
    child.PreferredSize = new Sizef(20.0f, 10.0f);
    child.MaxIntrinsicWidth = 20.0f;

    var grid = new VisualGrid();
    grid.SetColumns(columns);
    grid.SetRows(rows);
    grid.SetColumnGap(10.0f);
    grid.SetTextDirection(ETextDirection.RTL);
    grid.SetJustifyContent(EVisualGridContentAlignment.Start);
    grid.SetAlignContent(EVisualGridContentAlignment.End);
    grid.SetJustifyItems(EVisualGridItemAlignment.Start);
    grid.SetAlignItems(EVisualGridItemAlignment.End);

    grid.AddChild(child);
    VisualGridSlot slot = SlotOf<VisualGridSlot>(child);
    slot.SetArea(0u, 0u);
    slot.SetJustifySelf(EVisualGridItemAlignment.Start);
    slot.SetAlignSelf(EVisualGridItemAlignment.End);

    VisualTestHelpers.LayoutNode(grid, BoxConstraints.Tight(new Sizef(200.0f, 80.0f)));

    CheckSizef(grid.Size(), 200.0f, 80.0f);
    CheckConstraints(child.LastConstraints, 0.0f, 50.0f, 0.0f, 40.0f);
    CheckSizef(child.Size(), 20.0f, 10.0f);
    CheckOffsetf(slot.Offset(), 110.0f, 70.0f);

}
[GuiTest("visual/multi_child/visual_grid_tests.cpp::gui/visual-grid/baseline-first-child")]
public static void Case2(){

    VisualGridTrackSize[] columns = {
        VisualGridTrackSize.Fixed(100.0f),
    };
    VisualGridTrackSize[] rows = {
        VisualGridTrackSize.Fixed(40.0f),
        VisualGridTrackSize.Fixed(40.0f),
    };

    var first = new GridTest();
    first.PreferredSize = new Sizef(20.0f, 10.0f);

    var second = new GridTest();
    second.PreferredSize = new Sizef(20.0f, 10.0f);
    second.DryBaseline = 7.0f;
    second.ActualBaseline = 8.0f;

    var grid = new VisualGrid();
    grid.SetColumns(columns);
    grid.SetRows(rows);

    grid.AddChild(first);
    VisualGridSlot first_slot = SlotOf<VisualGridSlot>(first);
    first_slot.SetArea(0u, 0u);
    grid.AddChild(second);
    VisualGridSlot second_slot = SlotOf<VisualGridSlot>(second);
    second_slot.SetArea(1u, 0u);
    second_slot.SetAlignSelf(EVisualGridItemAlignment.Start);

    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(100.0f, 80.0f));
    float? DryBaseline = grid.GetDryBaseline(constraints, ETextBaseline.Alphabetic);
    Expect(DryBaseline);
    ExpectNear(DryBaseline.Value, 47.0f, kFloatEpsilon);

    VisualTestHelpers.LayoutNode(grid, constraints);
    float? ActualBaseline = grid.GetDistanceToActualBaseline(ETextBaseline.Alphabetic);
    Expect(ActualBaseline);
    ExpectNear(ActualBaseline.Value, 48.0f, kFloatEpsilon);
    CheckOffsetf(second_slot.Offset(), 0.0f, 40.0f);
    Equal(first.DryBaselineCount, 1u);
    Equal(second.DryBaselineCount, 1u);

}
}
