namespace SkrGui;
// Source: widgets/proxy/limited_box.hpp and matching cpp (611561f8).
public sealed class LimitedBox : VisualWidgetSingleChild
{
    public float MaxWidth=float.PositiveInfinity;
    public float MaxHeight=float.PositiveInfinity;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualLimited();
        result.SetMaxWidth(MaxWidth);
        result.SetMaxHeight(MaxHeight);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualLimited)visual;
        GuiAssert.Require(result!=null,"LimitedBox requires VisualLimited");
        result.SetMaxWidth(MaxWidth);
        result.SetMaxHeight(MaxHeight);
    }
}
