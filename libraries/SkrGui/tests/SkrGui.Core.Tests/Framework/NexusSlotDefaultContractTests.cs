using System.Runtime.InteropServices;
namespace SkrGui.Tests;

internal static class NexusSlotDefaultContractTests
{
    [GuiTest("audit/framework/nexus-slot-default-array-is-invalid")]
    private static void DefaultAndArrayAreInvalid()
    {
        NexusSlot slot=default;Check.False(slot.IsValid());Check.Equal(NexusSlot.Invalid(),slot);
        var array=new NexusSlot[3];foreach(var entry in array)Check.False(entry.IsValid());
        var first=new NexusSlot(0);Check.That(first.IsValid());Check.Equal(0UL,first.Index());
        var last=new NexusSlot(ulong.MaxValue-1);Check.That(last.IsValid());Check.Equal(ulong.MaxValue-1,last.Index());
        Check.False(new NexusSlot(ulong.MaxValue).IsValid());Check.That(slot.CompareTo(last)>0);Check.That(first.CompareTo(last)<0);
        Check.Equal(8,Marshal.SizeOf<NexusSlot>());
    }
}
