namespace SkrGui;
// Source: widgets/proxy/visibility.hpp and matching cpp (611561f8).
public sealed class Visibility : VisualWidgetSingleChild
{
    public bool VisibilityValue=true;
    public bool MaintainSize=false;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualVisibility();
        result.SetVisibility(VisibilityValue);
        result.SetMaintainSize(MaintainSize);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualVisibility)visual;
        GuiAssert.Require(result!=null,"Visibility requires VisualVisibility");
        result.SetVisibility(VisibilityValue);
        result.SetMaintainSize(MaintainSize);
    }
}
