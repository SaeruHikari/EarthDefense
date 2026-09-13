namespace SkrGui;
// Source: widgets/shifted/align.hpp and matching cpp (611561f8).
public sealed class Align : VisualWidgetSingleChild
{
    public AlignmentMixed Alignment=SkrGui.Alignment.Center();
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public float? WidthFactor=null;
    public float? HeightFactor=null;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualPositioned();
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetWidthFactor(WidthFactor);
        result.SetHeightFactor(HeightFactor);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualPositioned)visual;
        GuiAssert.Require(result!=null,"Align requires VisualPositioned");
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetWidthFactor(WidthFactor);
        result.SetHeightFactor(HeightFactor);
    }
}
