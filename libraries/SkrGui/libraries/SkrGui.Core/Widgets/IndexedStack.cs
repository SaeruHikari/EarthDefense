namespace SkrGui;
// Source: widgets/multi_child/indexed_stack.hpp and matching cpp (611561f8).
public sealed class IndexedStack : VisualWidgetMultiChild
{
    public AlignmentMixed Alignment=SkrGui.AlignmentDirectional.TopStart();
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public EStackFit Fit=SkrGui.EStackFit.Loose;
    public EClipBehavior ClipBehavior=SkrGui.EClipBehavior.HardEdge;
    public ulong? Index=0u;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualIndexedStack();
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetFit(Fit);
        result.SetClipBehavior(ClipBehavior);
        result.SetIndex(Index);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualIndexedStack)visual;
        GuiAssert.Require(result!=null,"IndexedStack requires VisualIndexedStack");
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetFit(Fit);
        result.SetClipBehavior(ClipBehavior);
        result.SetIndex(Index);
    }
}
