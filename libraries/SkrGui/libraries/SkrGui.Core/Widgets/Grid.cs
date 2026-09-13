namespace SkrGui;
// Source: widgets/multi_child/grid.hpp and matching cpp (611561f8).
public sealed class Grid : VisualWidgetMultiChild
{
    public List<VisualGridTrackSize> Columns=new(){ SkrGui.VisualGridTrackSize.Star() };
    public List<VisualGridTrackSize> Rows=new(){ SkrGui.VisualGridTrackSize.Star() };
    public float ColumnGap=0.0f;
    public float RowGap=0.0f;
    public EVisualGridContentAlignment JustifyContent=SkrGui.EVisualGridContentAlignment.Stretch;
    public EVisualGridContentAlignment AlignContent=SkrGui.EVisualGridContentAlignment.Stretch;
    public EVisualGridItemAlignment JustifyItems=SkrGui.EVisualGridItemAlignment.Stretch;
    public EVisualGridItemAlignment AlignItems=SkrGui.EVisualGridItemAlignment.Stretch;
    public ETextDirection TextDirection=SkrGui.ETextDirection.LTR;
    public bool ClipToBounds=false;
    protected override VisualNode CreateVisual(BuildContext context)
    {
        var result = new VisualGrid();
        result.SetColumns(Columns);
        result.SetRows(Rows);
        result.SetColumnGap(ColumnGap);
        result.SetRowGap(RowGap);
        result.SetJustifyContent(JustifyContent);
        result.SetAlignContent(AlignContent);
        result.SetJustifyItems(JustifyItems);
        result.SetAlignItems(AlignItems);
        result.SetTextDirection(TextDirection);
        result.SetClipToBounds(ClipToBounds);
        return result;
    }
    protected override void UpdateVisual(BuildContext context,VisualNode visual)
    {
        var result = (VisualGrid)visual;
        GuiAssert.Require(result!=null,"Grid requires VisualGrid");
        result.SetColumns(Columns);
        result.SetRows(Rows);
        result.SetColumnGap(ColumnGap);
        result.SetRowGap(RowGap);
        result.SetJustifyContent(JustifyContent);
        result.SetAlignContent(AlignContent);
        result.SetJustifyItems(JustifyItems);
        result.SetAlignItems(AlignItems);
        result.SetTextDirection(TextDirection);
        result.SetClipToBounds(ClipToBounds);
    }
}
