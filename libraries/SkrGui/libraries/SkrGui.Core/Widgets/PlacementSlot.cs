namespace SkrGui;
// Source: widgets/multi_child/placement_slot.hpp and matching cpp (611561f8).
public sealed class PlacementSlot : VisualSlotWidget
{
    public Placement Placement=SkrGui.Placement.Fill();
    public EPlacementSizeMode SizeMode=SkrGui.EPlacementSizeMode.Tight;
    protected override void ApplyVisualSlot(VisualSlot slot)
    {
        var result = (VisualPlacementSlot)slot;
        GuiAssert.Require(result!=null,"PlacementSlot requires VisualPlacementSlot");
        result.SetPlacement(Placement);
        result.SetSizeMode(SizeMode);
    }
}
