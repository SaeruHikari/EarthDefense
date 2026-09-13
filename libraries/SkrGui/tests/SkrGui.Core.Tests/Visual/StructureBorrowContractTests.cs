using System.Numerics;
using System.Reflection;
namespace SkrGui.Tests;

// Additional boundary checks for original const references and in/out parameters.
internal static class StructureBorrowContractTests
{
    private static MethodInfo Method(Type t,string name)=>t.GetMethod(name,BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic)!;

    [GuiTest("audit/structure/private-layout-failure-keeps-output")]
    private static void PrivateLayoutFailureKeepsOutput()
    {
        object[] args={0f,100f,0f,0f,0f,EPlacementSizeMode.Tight,123f,456f};
        Check.Equal(false,Method(typeof(Placement),"ResolveAxisConstraints").Invoke(null,args));Check.Equal(123f,args[6]);Check.Equal(456f,args[7]);
        object[] next={0UL,true,123UL};
        Check.Equal(false,Method(typeof(VisualWrap),"NextChildIndex").Invoke(new VisualWrap(),next));Check.Equal(123UL,next[2]);
    }

    [GuiTest("audit/structure/private-hit-failure-keeps-output")]
    private static void PrivateHitFailureKeepsOutput()
    {
        object[] inverse={new Matrix4x4(),Matrix4x4.Identity};
        Check.Equal(false,Method(typeof(VisualHitTestResult),"TryInvert").Invoke(null,inverse));Check.Equal(Matrix4x4.Identity,inverse[1]);
        object[] projection={new Matrix4x4(),new Offsetf(4,7),new Offsetf(123,456)};
        Check.Equal(false,Method(typeof(VisualHitTestResult),"ProjectPosition").Invoke(null,projection));Check.Equal(new Offsetf(123,456),projection[2]);
    }

    [GuiTest("audit/structure/readonly-value-borrows-remain-live")]
    private static void ReadonlyValueBorrowsRemainLive()
    {
        var visual=new VisualTransform();ref readonly var transform=ref visual.Transform();
        var replacement=new PaintTransform();replacement.ApplyOffset2D(new(15,27));visual.SetTransform(replacement);
        Check.Equal(new Offsetf(15,27),transform.Offset());
        var slot=new VisualPlacementSlot();ref readonly var placement=ref slot.Placement();
        slot.SetPlacement(Placement.Pin(new(.25f,.75f),new(5,6),new(7,8)));
        Check.Equal(new Sizef(7,8),placement.SizeDelta);Check.Equal(new Offsetf(5,6),placement.PivotOffset);
        var palette=M3TonalPalette.FromHueAndChroma(20,30);ref readonly var key=ref palette.KeyColor();
        palette=M3TonalPalette.FromHueAndChroma(200,60);Check.Equal(palette.KeyColor(),key);
    }

    [GuiTest("audit/structure/readonly-collection-borrows-remain-live")]
    private static void ReadonlyCollectionBorrowsRemainLive()
    {
        var grid=new VisualGrid();var columns=grid.Columns();var rows=grid.Rows();
        grid.SetColumns(new[]{VisualGridTrackSize.Fixed(20)});grid.SetRows(new[]{VisualGridTrackSize.Fixed(30)});
        Check.Equal(1,columns.Count);Check.Equal(1,rows.Count);
        Check.Throws<NotSupportedException>(()=>((IList<VisualGridTrackSize>)columns).Clear());
        var hit=new VisualHitTestResult();var path=hit.Path();hit.Add(grid,new(1,2));Check.Equal(1,path.Count);
        Check.Throws<NotSupportedException>(()=>((IList<VisualHitTestEntry>)path).Clear());hit.Clear();Check.Equal(0,path.Count);
    }
}
