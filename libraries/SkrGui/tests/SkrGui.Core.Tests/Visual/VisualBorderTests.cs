using System.Numerics;
using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualBorderTests
{
private class BorderTest : VisualLeaf{
public ulong DryBaselineCount = 0u;
public ulong DryLayoutCount = 0u;
public ulong PaintCount = 0u;
public ulong LayoutCount = 0u;
public float? ActualBaseline = null;
public float? DryBaseline = null;
public Offsetf LastPaintOrigin = new();
public BoxConstraints LastDryBaselineConstraints = new();
public BoxConstraints LastDryConstraints = new();
public BoxConstraints LastConstraints = new();
public Sizef DrySize = new Sizef(30.0f, 10.0f);
public Sizef PreferredSize = new Sizef(40.0f, 20.0f);
protected override void PerformLayout(){
        LastConstraints = Constraints();
        ++LayoutCount;
        SetSize(Constraints().Constrain(PreferredSize));
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
        LastDryConstraints = constraints;
        ++DryLayoutCount;
        return constraints.Constrain(DrySize);
    }
protected override float? ComputeDryBaseline(BoxConstraints constraints, ETextBaseline baseline){

        LastDryBaselineConstraints = constraints;
        ++DryBaselineCount;
        return DryBaseline;
    }
protected override float? ComputeDistanceToActualBaseline(ETextBaseline baseline){

        return ActualBaseline;
    }
}
private static T SlotOf<T>(VisualNode node)where T:VisualSlot {Check.NotNull(node);Check.NotNull(node.Slot());return (T)node.Slot()!;}
private static void CheckHitTestTransform(Matrix4x4 transform,Offsetf position,float x,float y){var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);if(point.W!=1){point.X/=point.W;point.Y/=point.W;}CheckOffsetf(new(point.X,point.Y),x,y);}
[GuiTest("visual/shifted/visual_border_tests.cpp::gui/visual-border/radius")]
public static void Case0(){

    VisualBorderRadius radius = VisualBorderRadius.Only(
        Radius.Circular(4.0f),
        Radius.Circular(8.0f),
        Radius.Circular(12.0f),
        Radius.Circular(16.0f)
    );
    ExpectFalse(radius.IsZero());
    Expect(radius.IsNonNegative());

    RRect rrect = radius.ToRrect(Rectf.LTWH(0.0f, 0.0f, 100.0f, 50.0f));
    CheckRrect(
        rrect,
        0.0f,
        0.0f,
        100.0f,
        50.0f,
        Radius.Circular(4.0f),
        Radius.Circular(8.0f),
        Radius.Circular(12.0f),
        Radius.Circular(16.0f)
    );

}
[GuiTest("visual/shifted/visual_border_tests.cpp::gui/visual-border/layout-insets-child")]
public static void Case1(){

    var child = new BorderTest();
    child.DryBaseline = 7.0f;
    child.ActualBaseline = 6.0f;

    var border = new VisualBorder();
    border.SetChild(child);
    border.SetBorderThickness(EdgeInsets.FromLTRB(2.0f, 3.0f, 4.0f, 5.0f));

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(border, constraints);

    Equal(child.LayoutCount, 1u);
    CheckConstraints(child.LastConstraints, 0.0f, 94.0f, 0.0f, 72.0f);
    CheckOffsetf(border.ChildOffset(), 2.0f, 3.0f);
    CheckSizef(border.Size(), 46.0f, 28.0f);

    CheckSizef(border.GetDryLayout(constraints), 36.0f, 18.0f);
    CheckConstraints(child.LastDryConstraints, 0.0f, 94.0f, 0.0f, 72.0f);

    ExpectNear(border.GetMinIntrinsicWidth(50.0f), 49.0f, kFloatEpsilon);
    ExpectNear(border.GetMaxIntrinsicWidth(50.0f), 50.0f, kFloatEpsilon);
    ExpectNear(border.GetMinIntrinsicHeight(50.0f), 55.0f, kFloatEpsilon);
    ExpectNear(border.GetMaxIntrinsicHeight(50.0f), 56.0f, kFloatEpsilon);

    float? DryBaseline = border.GetDryBaseline(constraints, ETextBaseline.Alphabetic);
    Expect(DryBaseline);
    ExpectNear(DryBaseline.Value, 10.0f, kFloatEpsilon);
    CheckConstraints(child.LastDryBaselineConstraints, 0.0f, 94.0f, 0.0f, 72.0f);

    float? ActualBaseline = border.GetDistanceToActualBaseline(ETextBaseline.Alphabetic);
    Expect(ActualBaseline);
    ExpectNear(ActualBaseline.Value, 9.0f, kFloatEpsilon);

}
[GuiTest("visual/shifted/visual_border_tests.cpp::gui/visual-border/no-child")]
public static void Case2(){

    var border = new VisualBorder();
    border.SetBorderThickness(EdgeInsets.FromLTRB(2.0f, 3.0f, 4.0f, 5.0f));

    BoxConstraints constraints = new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f);
    VisualTestHelpers.LayoutNode(border, constraints);

    CheckSizef(border.Size(), 6.0f, 8.0f);
    CheckSizef(border.GetDryLayout(constraints), 6.0f, 8.0f);
    CheckOffsetf(border.ChildOffset(), 0.0f, 0.0f);

}
[GuiTest("visual/shifted/visual_border_tests.cpp::gui/visual-border/paint-single-side")]
public static void Case3(){

    VisualTestBackend backend = null;
    var visual_owner = VisualTestHelpers.MakeVisualOwner(out backend);
    var border = new VisualBorder();

    border.SetBorderColor(new SRGBColor(1.0f, 0.0f, 0.0f, 1.0f));
    border.SetBorderThickness(EdgeInsets.Only(8.0f, 0.0f, 0.0f, 0.0f));
    border.SetRadius(Radius.Circular(18.0f));
    visual_owner.SetRoot(border);
    visual_owner.Layout(BoxConstraints.Tight(new Sizef(96.0f, 64.0f)));

    Expect(visual_owner.Paint());
    Expect(visual_owner.Render(VisualTestHelpers.MakeRenderDesc(backend)));
    Equal(backend.Commands.Size(), 1u);
    Equal(backend.Commands[0].Kind, EVisualTestCommand.Mesh);
    ExpectFalse(backend.Vertices.IsEmpty());
    foreach (MeshVertex vertex in backend.Vertices)
    {
        Expect(vertex.Pos.X < 48.0f);
    }

}
}
