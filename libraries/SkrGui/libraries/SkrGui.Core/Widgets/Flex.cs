namespace SkrGui;
// Source: widgets/multi_child/flex.hpp and matching cpp (611561f8).
public sealed class Flex : VisualWidgetMultiChild
{
    public EAxis Direction=SkrGui.EAxis.Horizontal;
    public EMainAxisSize MainAxisSize=SkrGui.EMainAxisSize.Max;
    public EMainAxisAlignment MainAxisAlignment=SkrGui.EMainAxisAlignment.Start;
    public ECrossAxisAlignment CrossAxisAlignment=SkrGui.ECrossAxisAlignment.Center;
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public EVerticalDirection VerticalDirection=SkrGui.EVerticalDirection.Down;
    public ETextBaseline? TextBaseline=null;
    public EClipBehavior ClipBehavior=SkrGui.EClipBehavior.None;
    public float Spacing=0.0f;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualFlex();
        result.SetDirection(Direction);
        result.SetMainAxisSize(MainAxisSize);
        result.SetMainAxisAlignment(MainAxisAlignment);
        result.SetCrossAxisAlignment(CrossAxisAlignment);
        result.SetTextDirection(TextDirection);
        result.SetVerticalDirection(VerticalDirection);
        result.SetTextBaseline(TextBaseline);
        result.SetClipBehavior(ClipBehavior);
        result.SetSpacing(Spacing);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualFlex)visual;
        GuiAssert.Require(result!=null,"Flex requires VisualFlex");
        result.SetDirection(Direction);
        result.SetMainAxisSize(MainAxisSize);
        result.SetMainAxisAlignment(MainAxisAlignment);
        result.SetCrossAxisAlignment(CrossAxisAlignment);
        result.SetTextDirection(TextDirection);
        result.SetVerticalDirection(VerticalDirection);
        result.SetTextBaseline(TextBaseline);
        result.SetClipBehavior(ClipBehavior);
        result.SetSpacing(Spacing);
    }
}
