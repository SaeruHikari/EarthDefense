namespace SkrGui;
// Source: widgets/proxy/fitted_box.hpp and matching cpp (611561f8).
public sealed class FittedBox : VisualWidgetSingleChild
{
    public EBoxFit Fit=SkrGui.EBoxFit.Contain;
    public AlignmentMixed Alignment=SkrGui.Alignment.Center();
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public EClipBehavior ClipBehavior=SkrGui.EClipBehavior.None;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualFitted();
        result.SetFit(Fit);
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetClipBehavior(ClipBehavior);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualFitted)visual;
        GuiAssert.Require(result!=null,"FittedBox requires VisualFitted");
        result.SetFit(Fit);
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetClipBehavior(ClipBehavior);
    }
}
