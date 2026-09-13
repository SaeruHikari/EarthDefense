using SkrGui;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualWidgetTests
{
[GuiTest("framework/widget/visual_widget_tests.cpp::gui/visual-widget/creates-once-and-updates-existing-visual")]
public static void Case0(){

    VisualLifecycleStats stats = new();

    TestLeafWidget initial_widget = MakeLeaf(stats, 7);
    using var host = new VisualTreeHost(initial_widget);

    TestLeafVisual initial_visual =
        ((TestLeafVisual)(host.Root.Visual()));
    Expect(initial_visual);
    Equal(initial_visual.Value, 7);
    Equal(stats.CreateCount, 1);
    Equal(stats.UpdateCount, 0);
    Equal(stats.CreateContextWidget, initial_widget);
    Equal(stats.CreateContextVisual, null);

    TestLeafWidget updated_widget = MakeLeaf(stats, 19);
    host.Update(updated_widget);

    Equal(host.Root.Visual(), initial_visual);
    Equal(initial_visual.Value, 19);
    Equal(stats.CreateCount, 1);
    Equal(stats.UpdateCount, 1);
    Equal(stats.UpdateContextWidget, updated_widget);
    Equal(stats.UpdateContextVisual, initial_visual);

    host.Destroy();
    Equal(stats.UnmountCount, 1);

}
[GuiTest("framework/widget/visual_widget_tests.cpp::gui/visual-widget/single-child-reuses-nexus-and-visual")]
public static void Case1(){

    VisualLifecycleStats parent_stats = new();
    VisualLifecycleStats child_stats = new();

    TestSingleWidget initial_widget = new TestSingleWidget();
    initial_widget.Stats = parent_stats;
    initial_widget.Child = MakeLeaf(child_stats, 3);
    using var host = new VisualTreeHost(initial_widget);

    var root = RttrCast<NexusVisualSingleChild>(host.Root);
    Expect(root);
    Equal(root.NumChildren(), 1);

    Nexus child_nexus = root.ChildAt(0);
    VisualNode child_visual = child_nexus.Visual();
    var root_visual = ((VisualProxy)(root.Visual()));
    Equal(root_visual.Child(), child_visual);

    TestSingleWidget updated_widget = new TestSingleWidget();
    updated_widget.Stats = parent_stats;
    updated_widget.Child = MakeLeaf(child_stats, 11);
    host.Update(updated_widget);

    Equal(root.ChildAt(0), child_nexus);
    Equal(root.ChildAt(0).Visual(), child_visual);
    Equal(root_visual.Child(), child_visual);
    Equal(
        ((TestLeafVisual)(child_visual)).Value,
        11
    );
    Equal(parent_stats.UpdateCount, 1);
    Equal(child_stats.UpdateCount, 1);

    TestSingleWidget removed_widget = new TestSingleWidget();
    removed_widget.Stats = parent_stats;
    host.Update(removed_widget);

    Equal(root.NumChildren(), 0);
    Equal(root_visual.Child(), null);
    Equal(child_stats.UnmountCount, 0);

    host.Owner.FinalizeNexus();
    Equal(child_stats.UnmountCount, 1);

}
[GuiTest("framework/widget/visual_widget_tests.cpp::gui/visual-widget/reorders-keyed-slot-widgets")]
public static void Case2(){

    VisualLifecycleStats root_stats = new();
    VisualLifecycleStats first_stats = new();
    VisualLifecycleStats second_stats = new();
    var first_slot_apply_count = new Counter();
    var second_slot_apply_count = new Counter();

    TestMultiWidget initial_widget = new TestMultiWidget();
    initial_widget.Stats = root_stats;
    initial_widget.Children.Add(MakeSlot(
        1,
        MakeLeaf(first_stats, 10),
        first_slot_apply_count,
        12.0f
    ));
    initial_widget.Children.Add(MakeSlot(
        2,
        MakeLeaf(second_stats, 20),
        second_slot_apply_count,
        24.0f
    ));
    using var host = new VisualTreeHost(initial_widget);

    var root = RttrCast<NexusVisualMultiChild>(host.Root);
    var root_visual = ((VisualStack)(host.Root.Visual()));
    Expect(root);
    Equal(root.NumChildren(), 2);
    Equal(root_visual.NumChildren(), 2);

    Nexus first_nexus = root.ChildAt(0);
    Nexus second_nexus = root.ChildAt(1);
    VisualNode first_visual = root_visual.ChildAt(0);
    VisualNode second_visual = root_visual.ChildAt(1);
    VisualSlot first_visual_slot = first_visual.Slot();
    VisualSlot second_visual_slot = second_visual.Slot();
    Equal(first_nexus.Visual(), first_visual);
    Equal(second_nexus.Visual(), second_visual);
    Equal(first_slot_apply_count, 1);
    Equal(second_slot_apply_count, 1);
    Equal(
        ((VisualStackSlot)(first_visual.Slot())).Left(),
        12.0f
    );
    Equal(
        ((VisualStackSlot)(second_visual.Slot())).Left(),
        24.0f
    );

    TestMultiWidget updated_widget = new TestMultiWidget();
    updated_widget.Stats = root_stats;
    updated_widget.Children.Add(MakeSlot(
        2,
        MakeLeaf(second_stats, 21),
        second_slot_apply_count,
        48.0f
    ));
    updated_widget.Children.Add(MakeSlot(
        1,
        MakeLeaf(first_stats, 11),
        first_slot_apply_count,
        36.0f
    ));
    host.Update(updated_widget);

    Equal(root.ChildAt(0), second_nexus);
    Equal(root.ChildAt(1), first_nexus);
    Equal(root.ChildAt(0).ParentIndex(), 0);
    Equal(root.ChildAt(1).ParentIndex(), 1);
    Equal(root_visual.ChildAt(0), second_visual);
    Equal(root_visual.ChildAt(1), first_visual);
    Check.False(root_visual.NeedsRebuildFlush());
    Equal(second_nexus.Visual(), second_visual);
    Equal(first_nexus.Visual(), first_visual);
    Equal(second_visual.Slot(), second_visual_slot);
    Equal(first_visual.Slot(), first_visual_slot);
    Equal(first_slot_apply_count, 2);
    Equal(second_slot_apply_count, 2);
    Equal(
        ((VisualStackSlot)(second_visual.Slot())).Left(),
        48.0f
    );
    Equal(
        ((VisualStackSlot)(first_visual.Slot())).Left(),
        36.0f
    );
    Equal(
        ((TestLeafVisual)(second_visual)).Value,
        21
    );
    Equal(
        ((TestLeafVisual)(first_visual)).Value,
        11
    );

}
}
