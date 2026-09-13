namespace SkrGui;
// Source: widgets/proxy/constrained_box.hpp and matching cpp (611561f8).
public sealed class ConstrainedBox : VisualWidgetSingleChild
{
    public BoxConstraints AdditionalConstraints=new();
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualConstrained();
        result.SetAdditionalConstraints(AdditionalConstraints);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualConstrained)visual;
        GuiAssert.Require(result!=null,"ConstrainedBox requires VisualConstrained");
        result.SetAdditionalConstraints(AdditionalConstraints);
    }
}
