namespace Sugoi.Data;

/// <summary>Reference staging geometry: a type row targets 256 KiB, clamped to 256..4096 records per block.</summary>
public sealed class StagingRowLayout
{
    public ComponentLayout Component { get; }
    public int Stride => Component.Size;
    public int Alignment => Math.Max(IntPtr.Size, Component.Alignment);
    public int RecordsPerBlock { get; }
    public int BlockBytes { get; }

    public StagingRowLayout(ComponentLayout component)
    {
        Component = component;
        RecordsPerBlock = component.Size == 0 ? 0 : Math.Clamp(256 * 1024 / component.Size, 256, 4096);
        BlockBytes = LayoutMath.AlignUp(checked(RecordsPerBlock * component.Size), Alignment);
    }
    public int BlockIndex(int record) => RecordsPerBlock > 0 ? record / RecordsPerBlock : throw new InvalidOperationException("Tags have no payload blocks.");
    public int Offset(int record) => checked(record % RecordsPerBlock * Stride);
}
