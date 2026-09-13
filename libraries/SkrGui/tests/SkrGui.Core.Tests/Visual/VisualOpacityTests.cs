using System.Numerics;
using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualOpacityTests
{
private class OpacityProbe : VisualLeaf{
public ulong LayoutCount = 0u;
public Sizef PreferredSize = new Sizef(40.0f, 20.0f);
protected override void PerformLayout(){
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
        return constraints.Constrain(PreferredSize);
    }
}
private static T SlotOf<T>(VisualNode node)where T:VisualSlot {Check.NotNull(node);Check.NotNull(node.Slot());return (T)node.Slot()!;}
private static void CheckHitTestTransform(Matrix4x4 transform,Offsetf position,float x,float y){var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);if(point.W!=1){point.X/=point.W;point.Y/=point.W;}CheckOffsetf(new(point.X,point.Y),x,y);}
[GuiTest("visual/proxy/visual_opacity_tests.cpp::gui/visual-opacity/layout-proxy")]
public static void Case0(){

    var child = new OpacityProbe();

    var opacity = new VisualOpacity();
    opacity.SetChild(child);
    VisualTestHelpers.LayoutNode(opacity, new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f));

    Equal(child.LayoutCount, 1u);
    CheckSizef(opacity.Size(), 40.0f, 20.0f);
    CheckSizef(opacity.GetDryLayout(new BoxConstraints(0.0f, 100.0f, 0.0f, 80.0f)), 40.0f, 20.0f);

}
}
