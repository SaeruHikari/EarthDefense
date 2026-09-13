namespace SkrGui;
// Source: widgets/proxy/transform.hpp and matching cpp (611561f8).
public sealed class Transform : VisualWidgetSingleChild
{
    public PaintTransform PaintTransform=new();
    public Offsetf Origin=new();
    public AlignmentMixed Alignment=SkrGui.Alignment.TopLeft();
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public bool TransformHitTests=true;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualTransform();
        result.SetTransform(PaintTransform);
        result.SetOrigin(Origin);
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetTransformHitTests(TransformHitTests);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualTransform)visual;
        GuiAssert.Require(result!=null,"Transform requires VisualTransform");
        result.SetTransform(PaintTransform);
        result.SetOrigin(Origin);
        result.SetAlignment(Alignment);
        result.SetTextDirection(TextDirection);
        result.SetTransformHitTests(TransformHitTests);
    }
}
