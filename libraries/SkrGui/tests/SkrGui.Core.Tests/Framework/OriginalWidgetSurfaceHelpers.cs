using SkrGui;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;

internal static class OriginalWidgetSurfaceHelpers
{
    public sealed class WidgetTreeHost(Widget widget):ComponentTreeHost(widget);
    // Exact default -> configured create -> same Nexus update -> reset transactions from widget_surface_tests.cpp.
    public static void CheckVisualWidget<W,N,V>(Action<W> configure,Action<V> checkDefault,Action<V> checkUpdated)
        where W:Widget,new() where N:Nexus where V:VisualNode
    {
        W initial=new();using var host=new WidgetTreeHost(initial);
        Nexus initialNexus=host.Root!;VisualNode initialVisual=host.Root!.Visual()!;
        Check.NotNull(initialNexus);Check.NotNull(initialVisual);
        Equal(initialNexus.GetType(),typeof(N));Equal(initialNexus.Widget().GetType(),typeof(W));Equal(initialVisual.GetType(),typeof(V));
        checkDefault((V)initialVisual);
        W configuredForCreate=new();configure(configuredForCreate);using var configuredHost=new WidgetTreeHost(configuredForCreate);
        Equal(configuredHost.Root!.GetType(),typeof(N));Equal(configuredHost.Root.Visual()!.GetType(),typeof(V));checkUpdated((V)configuredHost.Root.Visual()!);
        W updated=new();configure(updated);host.Update(updated);
        Equal(host.Root,initialNexus);Equal(host.Root!.Visual(),initialVisual);Equal(host.Root.Widget().GetType(),typeof(W));checkUpdated((V)initialVisual);
        host.Update(new W());Equal(host.Root,initialNexus);Equal(host.Root.Visual(),initialVisual);checkDefault((V)initialVisual);
    }
    public static void CheckSlotWidget<C,CV,S,SV>(Action<S> configure,Action<SV> checkDefault,Action<SV> checkUpdated)
        where C:VisualWidgetMultiChild,new() where CV:VisualNode where S:VisualSlotWidget,new() where SV:VisualSlot
    {
        C initialContainer=new();S initialSlot=new();initialSlot.Child=new TempText();initialContainer.Children.Add(initialSlot);using var host=new WidgetTreeHost(initialContainer);
        var containerNexus=(NexusVisualMultiChild)host.Root!;Check.NotNull(containerNexus);Equal(host.Root!.GetType(),typeof(NexusVisualMultiChild));Equal(host.Root.Visual()!.GetType(),typeof(CV));
        Nexus initialSlotNexus=containerNexus.ChildAt(0);Nexus initialChildNexus=initialSlotNexus.ChildAt(0);VisualNode initialChildVisual=initialChildNexus.Visual()!;VisualSlot initialVisualSlot=initialChildVisual.Slot()!;
        Check.NotNull(initialVisualSlot);Equal(initialSlotNexus.GetType(),typeof(NexusVisualSlot));Equal(initialSlotNexus.Widget().GetType(),typeof(S));Equal(initialVisualSlot.GetType(),typeof(SV));checkDefault((SV)initialVisualSlot);
        C configuredContainer=new();S configuredSlot=new();configure(configuredSlot);configuredSlot.Child=new TempText();configuredContainer.Children.Add(configuredSlot);using var configuredHost=new WidgetTreeHost(configuredContainer);
        VisualSlot configuredVisualSlot=configuredHost.Root!.ChildAt(0).ChildAt(0).Visual()!.Slot()!;Check.NotNull(configuredVisualSlot);Equal(configuredVisualSlot.GetType(),typeof(SV));checkUpdated((SV)configuredVisualSlot);
        C updatedContainer=new();S updatedSlot=new();configure(updatedSlot);updatedSlot.Child=new TempText();updatedContainer.Children.Add(updatedSlot);host.Update(updatedContainer);
        Equal(containerNexus.ChildAt(0),initialSlotNexus);Equal(initialSlotNexus.ChildAt(0),initialChildNexus);Equal(initialChildNexus.Visual(),initialChildVisual);Equal(initialChildVisual.Slot(),initialVisualSlot);checkUpdated((SV)initialVisualSlot);
        C resetContainer=new();S resetSlot=new();resetSlot.Child=new TempText();resetContainer.Children.Add(resetSlot);host.Update(resetContainer);
        Equal(containerNexus.ChildAt(0),initialSlotNexus);Equal(initialSlotNexus.ChildAt(0),initialChildNexus);Equal(initialChildVisual.Slot(),initialVisualSlot);checkDefault((SV)initialVisualSlot);
    }
}
