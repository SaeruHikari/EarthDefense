namespace SkrGui;
// Source: widgets/multi_child/placement_layout.hpp and matching cpp (611561f8).
public sealed class PlacementLayout : VisualWidgetMultiChild
{
    public VisualPlacementAxisSize WidthRule=SkrGui.VisualPlacementAxisSize.Expand();
    public VisualPlacementAxisSize HeightRule=SkrGui.VisualPlacementAxisSize.Expand();
    public EClipBehavior ClipBehavior=SkrGui.EClipBehavior.None;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualPlacement();
        result.SetWidthRule(WidthRule);
        result.SetHeightRule(HeightRule);
        result.SetClipBehavior(ClipBehavior);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualPlacement)visual;
        GuiAssert.Require(result!=null,"PlacementLayout requires VisualPlacement");
        result.SetWidthRule(WidthRule);
        result.SetHeightRule(HeightRule);
        result.SetClipBehavior(ClipBehavior);
    }
}
