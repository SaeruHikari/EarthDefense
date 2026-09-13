namespace SkrGui;
// Source: widgets/multi_child/expanded.hpp and matching cpp (611561f8).
public sealed class Expanded : VisualSlotWidget
{
    public int Flex=1;
    protected override void ApplyVisualSlot(VisualSlot slot)
    {
        var result = (VisualFlexSlot)slot;
        GuiAssert.Require(result!=null,"Expanded requires VisualFlexSlot");
        result.SetFlex(Flex);
        result.SetFit(SkrGui.EFlexFit.Tight);
    }
}
