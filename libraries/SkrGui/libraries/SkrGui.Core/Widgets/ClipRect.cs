namespace SkrGui;
// Source: widgets/proxy/clip_rect.hpp and matching cpp (611561f8).
public sealed class ClipRect : VisualWidgetSingleChild
{
    public EClipBehavior ClipBehavior=SkrGui.EClipBehavior.HardEdge;
    public Rectf? CustomClipRect=null;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualClipRect();
        result.SetClipBehavior(ClipBehavior);
        result.SetCustomClipRect(CustomClipRect);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualClipRect)visual;
        GuiAssert.Require(result!=null,"ClipRect requires VisualClipRect");
        result.SetClipBehavior(ClipBehavior);
        result.SetCustomClipRect(CustomClipRect);
    }
}
