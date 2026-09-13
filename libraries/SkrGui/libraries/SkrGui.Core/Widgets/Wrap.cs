namespace SkrGui;
// Source: widgets/multi_child/wrap.hpp and matching cpp (611561f8).
public sealed class Wrap : VisualWidgetMultiChild
{
    public EAxis Direction=SkrGui.EAxis.Horizontal;
    public EWrapAlignment Alignment=SkrGui.EWrapAlignment.Start;
    public float Spacing=0.0f;
    public EWrapAlignment RunAlignment=SkrGui.EWrapAlignment.Start;
    public float RunSpacing=0.0f;
    public EWrapCrossAlignment CrossAxisAlignment=SkrGui.EWrapCrossAlignment.Start;
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public EVerticalDirection VerticalDirection=SkrGui.EVerticalDirection.Down;
    public EClipBehavior ClipBehavior=SkrGui.EClipBehavior.None;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualWrap();
        result.SetDirection(Direction);
        result.SetAlignment(Alignment);
        result.SetSpacing(Spacing);
        result.SetRunAlignment(RunAlignment);
        result.SetRunSpacing(RunSpacing);
        result.SetCrossAxisAlignment(CrossAxisAlignment);
        result.SetTextDirection(TextDirection);
        result.SetVerticalDirection(VerticalDirection);
        result.SetClipBehavior(ClipBehavior);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualWrap)visual;
        GuiAssert.Require(result!=null,"Wrap requires VisualWrap");
        result.SetDirection(Direction);
        result.SetAlignment(Alignment);
        result.SetSpacing(Spacing);
        result.SetRunAlignment(RunAlignment);
        result.SetRunSpacing(RunSpacing);
        result.SetCrossAxisAlignment(CrossAxisAlignment);
        result.SetTextDirection(TextDirection);
        result.SetVerticalDirection(VerticalDirection);
        result.SetClipBehavior(ClipBehavior);
    }
}
