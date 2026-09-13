namespace SkrGui;
// Source: widgets/shifted/padding.hpp and matching cpp (611561f8).
public sealed class Padding : VisualWidgetSingleChild
{
    public EdgeInsetsMixed PaddingValue=new();
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualPadding();
        result.SetPadding(PaddingValue);
        result.SetTextDirection(TextDirection);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualPadding)visual;
        GuiAssert.Require(result!=null,"Padding requires VisualPadding");
        result.SetPadding(PaddingValue);
        result.SetTextDirection(TextDirection);
    }
}
