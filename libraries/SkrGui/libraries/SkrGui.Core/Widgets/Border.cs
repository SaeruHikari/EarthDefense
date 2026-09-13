namespace SkrGui;
// Source: widgets/shifted/border.hpp and matching cpp (611561f8).
public sealed class Border : VisualWidgetSingleChild
{
    public VisualBorderRadius Radius=new();
    public EdgeInsets BorderThickness=new();
    public SRGBColor BorderColor=new SRGBColor(0.0f, 0.0f, 0.0f, 0.0f);
    public SRGBColor BackgroundColor=new SRGBColor(0.0f, 0.0f, 0.0f, 0.0f);
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualBorder();
        result.SetRadius(Radius);
        result.SetBorderThickness(BorderThickness);
        result.SetBorderColor(BorderColor);
        result.SetBackgroundColor(BackgroundColor);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualBorder)visual;
        GuiAssert.Require(result!=null,"Border requires VisualBorder");
        result.SetRadius(Radius);
        result.SetBorderThickness(BorderThickness);
        result.SetBorderColor(BorderColor);
        result.SetBackgroundColor(BackgroundColor);
    }
}
