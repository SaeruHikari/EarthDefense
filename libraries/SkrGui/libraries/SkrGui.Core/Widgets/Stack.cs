namespace SkrGui;
// Source: widgets/multi_child/stack.hpp and matching cpp (611561f8).
public sealed class Stack : VisualWidgetMultiChild
{
    public AlignmentMixed Alignment=SkrGui.AlignmentDirectional.TopStart();
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public EStackFit Fit=SkrGui.EStackFit.Loose;
    public EClipBehavior ClipBehavior=SkrGui.EClipBehavior.HardEdge;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualStack();
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetFit(Fit);
        result.SetClipBehavior(ClipBehavior);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualStack)visual;
        GuiAssert.Require(result!=null,"Stack requires VisualStack");
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetFit(Fit);
        result.SetClipBehavior(ClipBehavior);
    }
}
