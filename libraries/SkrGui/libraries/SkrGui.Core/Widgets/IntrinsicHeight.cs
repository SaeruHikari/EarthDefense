namespace SkrGui;
// Source: widgets/proxy/intrinsic_height.hpp and matching cpp (611561f8).
public sealed class IntrinsicHeight : VisualWidgetSingleChild
{
    protected override VisualNode CreateVisual(BuildContext context)
    {
        return new VisualIntrinsicHeight();
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        GuiAssert.Require(visual is VisualIntrinsicHeight,"IntrinsicHeight requires VisualIntrinsicHeight");
    }
}
