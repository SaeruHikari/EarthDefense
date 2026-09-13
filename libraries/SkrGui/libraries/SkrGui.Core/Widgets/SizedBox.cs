namespace SkrGui;
// Source: widgets/proxy/sized_box.hpp and matching cpp (611561f8).
public sealed class SizedBox : VisualWidgetSingleChild
{
    public float? Width=null;
    public float? Height=null;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualConstrained();
        result.SetSized(Width, Height);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualConstrained)visual;
        GuiAssert.Require(result!=null,"SizedBox requires VisualConstrained");
        result.SetSized(Width, Height);
    }
}
