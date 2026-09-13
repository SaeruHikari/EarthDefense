namespace SkrGui;
// Source: widgets/multi_child/flexible.hpp and matching cpp (611561f8).
public sealed class Flexible : VisualSlotWidget
{
    public int Flex=1;
    protected override void ApplyVisualSlot(VisualSlot slot)
    {
        var result = (VisualFlexSlot)slot;
        GuiAssert.Require(result!=null,"Flexible requires VisualFlexSlot");
        result.SetFlex(Flex);
        result.SetFit(SkrGui.EFlexFit.Loose);
    }
}
