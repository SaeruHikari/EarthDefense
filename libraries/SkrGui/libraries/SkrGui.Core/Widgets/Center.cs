namespace SkrGui;
// Source: widgets/shifted/center.hpp and matching cpp (611561f8).
public sealed class Center : VisualWidgetSingleChild
{
    public float? WidthFactor=null;
    public float? HeightFactor=null;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualPositioned();
        result.SetAlignment(SkrGui.Alignment.Center());
        result.SetTextDirection(SkrGui.ETextDirection.LTR);
        result.SetWidthFactor(WidthFactor);
        result.SetHeightFactor(HeightFactor);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualPositioned)visual;
        GuiAssert.Require(result!=null,"Center requires VisualPositioned");
        result.SetAlignment(SkrGui.Alignment.Center());
        result.SetTextDirection(SkrGui.ETextDirection.LTR);
        result.SetWidthFactor(WidthFactor);
        result.SetHeightFactor(HeightFactor);
    }
}
