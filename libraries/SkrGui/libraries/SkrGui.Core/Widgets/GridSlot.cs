namespace SkrGui;
// Source: widgets/multi_child/grid_slot.hpp and matching cpp (611561f8).
public sealed class GridSlot : VisualSlotWidget
{
    public uint RowStart=0u;
    public uint ColumnStart=0u;
    public uint RowSpan=1u;
    public uint ColumnSpan=1u;
    public EVisualGridItemAlignment? JustifySelf=null;
    public EVisualGridItemAlignment? AlignSelf=null;
    protected override void ApplyVisualSlot(VisualSlot slot)
    {
        var result = (VisualGridSlot)slot;
        GuiAssert.Require(result!=null,"GridSlot requires VisualGridSlot");
        result.SetArea(RowStart, ColumnStart, RowSpan, ColumnSpan);
        result.SetJustifySelf(JustifySelf);
        result.SetAlignSelf(AlignSelf);
    }
}
