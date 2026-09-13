namespace SkrGui;
// Source: widgets/multi_child/positioned.hpp and matching cpp (611561f8).
public sealed class Positioned : VisualSlotWidget
{
    public float? Left=null;
    public float? Top=null;
    public float? Right=null;
    public float? Bottom=null;
    public float? Width=null;
    public float? Height=null;
    protected override void ApplyVisualSlot(VisualSlot slot)
    {
        var result = (VisualStackSlot)slot;
        GuiAssert.Require(result!=null,"Positioned requires VisualStackSlot");
        result.SetPosition(Left, Top, Right, Bottom, Width, Height);
    }
}
