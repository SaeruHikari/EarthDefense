using System.Runtime.CompilerServices;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;

// C++ raw-pointer ownership boundaries, independently of destructor timing.
internal static class RawBorrowGcContractTests
{
    [MethodImpl(MethodImplOptions.NoInlining)] private static bool Alive<T>(WeakReference<T> weak)where T:class=>weak.TryGetTarget(out _);
    private static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (VisualNode,WeakReference<VisualNode>,WeakReference<VisualOwner>) VisualBorrow()
    {
        var root=new VisualProxy();var child=new TestComponentVisual();root.SetChild(child);
        var owner=VisualTestHelpers.MakeVisualOwner();owner.SetRoot(root);
        return(child,new(root),new(owner));
    }
    [GuiTest("audit/ownership/visual-child-does-not-own-parent-or-owner")]
    private static void VisualChildDoesNotOwnParentOrOwner()
    {
        var(child,parent,owner)=VisualBorrow();Collect();
        Check.False(Alive(parent));Check.False(Alive(owner));Check.Null(child.Parent());Check.Null(child.Owner());GC.KeepAlive(child);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (VisualSlot,WeakReference<VisualNode>) SlotBorrow()
    {
        var root=new VisualStack();var child=new TestComponentVisual();root.AddChild(child);
        return(child.Slot()!,new(child));
    }
    [GuiTest("audit/ownership/slot-does-not-own-attached-node")]
    private static void SlotDoesNotOwnAttachedNode()
    {
        var(slot,node)=SlotBorrow();Collect();Check.False(Alive(node));Check.Null(slot.Node());GC.KeepAlive(slot);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (State,WeakReference<Nexus>) StateBorrow()
    {
        var stats=new ComponentStats();var nexus=MakeStatefulComponent(stats,1).CreateNexus();
        return(stats.State!,new(nexus));
    }
    [GuiTest("audit/ownership/state-does-not-own-attached-nexus")]
    private static void StateDoesNotOwnAttachedNexus()
    {
        var(state,nexus)=StateBorrow();Collect();Check.False(Alive(nexus));Check.Null(state.AttachedNexus);GC.KeepAlive(state);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Nexus,WeakReference<Nexus>,WeakReference<BuildOwner>) NexusBorrow()
    {
        var widget=new TestSingleWidget{Stats=new(),Child=MakeLeaf(new(),7)};
        var root=widget.CreateNexus();var owner=new BuildOwner();owner.AddRoot(root);owner.BuildScope(root);
        return(root.ChildAt(0),new(root),new(owner));
    }
    [GuiTest("audit/ownership/nexus-child-does-not-own-ancestor-or-build-owner")]
    private static void NexusChildDoesNotOwnAncestorOrBuildOwner()
    {
        var(child,parent,owner)=NexusBorrow();Collect();
        Check.False(Alive(parent));Check.False(Alive(owner));Check.Null(child.Parent());Check.Null(child.BuildScope()!.Owner());GC.KeepAlive(child);
    }

    [GuiTest("audit/ownership/active-tree-retains-owned-children-and-state")]
    private static void ActiveTreeRetainsOwnedChildrenAndState()
    {
        var stats=new ComponentStats();var widget=MakeStatefulComponent(stats,3);var root=widget.CreateNexus();
        using var owner=new BuildOwner();owner.AddRoot(root);owner.BuildScope(root);var child=new WeakReference<Nexus>(root.ChildAt(0));var state=new WeakReference<State>(stats.State!);stats.State=null;
        Collect();Check.That(Alive(child));Check.That(Alive(state));Check.Same(root,root.ChildAt(0).Parent());
        owner.RemoveRoot(root);owner.FinalizeNexus();Check.Equal(ENexusLifeCycle.Destroyed,root.LifeCycle());GC.KeepAlive(root);
        using var visualOwner=VisualTestHelpers.MakeVisualOwner();var visualRoot=new VisualProxy();var visualChild=new TestComponentVisual();visualRoot.SetChild(visualChild);visualOwner.SetRoot(visualRoot);
        Collect();Check.Same(visualRoot,visualChild.Parent());Check.Same(visualOwner,visualChild.Owner());
        visualOwner.ClearRoot();Check.Null(visualChild.Owner());GC.KeepAlive(visualRoot);
    }
}
