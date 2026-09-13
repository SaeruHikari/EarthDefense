namespace SkrGui;
// Source: widgets/multi_child/flex_slot.hpp and matching cpp (611561f8).
public sealed class FlexSlot : VisualSlotWidget
{
    public int Flex=0;
    public EFlexFit Fit=SkrGui.EFlexFit.Tight;
    protected override void ApplyVisualSlot(VisualSlot slot)
    {
        var result = (VisualFlexSlot)slot;
        GuiAssert.Require(result!=null,"FlexSlot requires VisualFlexSlot");
        result.SetFlex(Flex);
        result.SetFit(Fit);
    }
}
