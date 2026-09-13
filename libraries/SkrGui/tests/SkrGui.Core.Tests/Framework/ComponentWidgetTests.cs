using SkrGui;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class ComponentWidgetTests
{
[GuiTest("framework/widget/component_widget_tests.cpp::gui/component-widget/hosts-state-and-explicitly-rebuilds")]
public static void Case0(){

    ComponentStats stats = new();
    TestComponentWidget initial_widget =
        MakeStatefulComponent(stats, 7);
    using var host = new ComponentTreeHost(initial_widget);

    var root = RttrCast<NexusComponent>(host.Root);
    Expect(root);
    var state = ((TestComponentState)(stats.State));
    var visual =
        ((TestComponentVisual)(host.Root.Visual()));
    Nexus child = root.ChildAt(0);

    Expect(state);
    Equal(root.NumChildren(), 1);
    Equal(stats.CreateStateCount, 1);
    Equal(stats.BuildCount, 1);
    Equal(stats.BuildState, state);
    Equal(stats.BuildContextWidget, initial_widget);
    Equal(stats.BuildContextVisual, null);
    Equal(state.Context().Widget(), initial_widget);
    Equal(visual.Value, 7);

    state.Value = 5;
    state.MarkNeedsRebuild();
    Expect(host.Owner.HasPendingBuild());
    host.Rebuild();

    Equal(stats.BuildCount, 2);
    Equal(root.ChildAt(0), child);
    Equal(host.Root.Visual(), visual);
    Equal(stats.BuildContextVisual, visual);
    Equal(visual.Value, 12);

    TestComponentWidget updated_widget =
        MakeStatefulComponent(stats, 20);
    host.Update(updated_widget);

    Equal(stats.CreateStateCount, 1);
    Equal(stats.ShouldRebuildCount, 1);
    Equal(stats.ComparedOldValue, 7);
    Equal(stats.BuildCount, 3);
    Equal(stats.BuildState, state);
    Equal(stats.BuildContextWidget, updated_widget);
    Equal(state.Context().Widget(), updated_widget);
    Equal(root.ChildAt(0), child);
    Equal(host.Root.Visual(), visual);
    Equal(visual.Value, 25);

    host.Destroy();
    Equal(stats.DestroyStateCount, 1);

}
[GuiTest("framework/widget/component_widget_tests.cpp::gui/component-widget/can-suppress-forced-update-rebuild")]
public static void Case1(){

    ComponentStats stats = new();
    TestComponentWidget initial_widget =
        MakeStatefulComponent(stats, 3);
    using var host = new ComponentTreeHost(initial_widget);

    var state = ((TestComponentState)(stats.State));
    var visual =
        ((TestComponentVisual)(host.Root.Visual()));

    TestComponentWidget updated_widget =
        MakeStatefulComponent(stats, 11, false);
    host.Update(updated_widget);

    Equal(stats.CreateStateCount, 1);
    Equal(stats.ShouldRebuildCount, 1);
    Equal(stats.ComparedOldValue, 3);
    Equal(stats.BuildCount, 1);
    Equal(state.Context().Widget(), updated_widget);
    Equal(visual.Value, 3);

    state.MarkNeedsRebuild();
    host.Rebuild();

    Equal(stats.BuildCount, 2);
    Equal(stats.BuildContextWidget, updated_widget);
    Equal(visual.Value, 11);

}
[GuiTest("framework/widget/component_widget_tests.cpp::gui/component-widget/supports-stateless-components")]
public static void Case2(){

    ComponentStats stats = new();
    StatelessTestComponentWidget initial_widget =
        MakeStatelessComponent(stats, 4);
    using var host = new ComponentTreeHost(initial_widget);

    var visual =
        ((TestComponentVisual)(host.Root.Visual()));
    Equal(stats.CreateStateCount, 0);
    Equal(stats.BuildCount, 1);
    Equal(stats.BuildState, null);
    Equal(stats.BuildContextWidget, initial_widget);
    Equal(visual.Value, 4);

    StatelessTestComponentWidget updated_widget =
        MakeStatelessComponent(stats, 9);
    host.Update(updated_widget);

    Equal(stats.BuildCount, 2);
    Equal(stats.BuildState, null);
    Equal(stats.BuildContextWidget, updated_widget);
    Equal(visual.Value, 9);

}
}
