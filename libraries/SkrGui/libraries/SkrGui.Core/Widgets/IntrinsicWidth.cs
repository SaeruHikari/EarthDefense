namespace SkrGui;
// Source: widgets/proxy/intrinsic_width.hpp and matching cpp (611561f8).
public sealed class IntrinsicWidth : VisualWidgetSingleChild
{
    public float? StepWidth=null;
    public float? StepHeight=null;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualIntrinsicWidth();
        result.SetStepWidth(StepWidth);
        result.SetStepHeight(StepHeight);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualIntrinsicWidth)visual;
        GuiAssert.Require(result!=null,"IntrinsicWidth requires VisualIntrinsicWidth");
        result.SetStepWidth(StepWidth);
        result.SetStepHeight(StepHeight);
    }
}
