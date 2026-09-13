namespace SkrGui;
// Source: widgets/proxy/aspect_ratio.hpp and matching cpp (611561f8).
public sealed class AspectRatio : VisualWidgetSingleChild
{
    public float AspectRatioValue=1.0f;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualAspectRatio();
        result.SetAspectRatio(AspectRatioValue);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualAspectRatio)visual;
        GuiAssert.Require(result!=null,"AspectRatio requires VisualAspectRatio");
        result.SetAspectRatio(AspectRatioValue);
    }
}
