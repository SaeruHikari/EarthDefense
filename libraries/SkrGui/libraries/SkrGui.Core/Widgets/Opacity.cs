namespace SkrGui;
// Source: widgets/proxy/opacity.hpp and matching cpp (611561f8).
public sealed class Opacity : VisualWidgetSingleChild
{
    public float OpacityValue=1.0f;
    public EVisualOpacityMode OpacityMode=SkrGui.EVisualOpacityMode.Multiply;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualOpacity();
        result.SetOpacity(OpacityValue);
        result.SetOpacityMode(OpacityMode);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualOpacity)visual;
        GuiAssert.Require(result!=null,"Opacity requires VisualOpacity");
        result.SetOpacity(OpacityValue);
        result.SetOpacityMode(OpacityMode);
    }
}
