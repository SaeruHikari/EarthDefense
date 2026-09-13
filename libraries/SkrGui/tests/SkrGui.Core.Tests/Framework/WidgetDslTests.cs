using SkrGui;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class WidgetDslTests
{

 private abstract class WidgetDslTestBase:Widget {public override Nexus CreateNexus()=>null!;}
 private sealed class PlainWidget:WidgetDslTestBase {public int Value;}
 private sealed class PreConstructWidget:WidgetDslTestBase,IPreConstruct {public int Phase;public bool ConfiguredAfterPre;public void PreConstruct()=>Phase=1;}
 private sealed class PostConstructWidget:WidgetDslTestBase,IPostConstruct {public int Phase;public bool ConfiguredBeforePost;public void PostConstruct(){ConfiguredBeforePost=Phase==1;Phase=2;}}
 private sealed class PrePostConstructWidget:WidgetDslTestBase,IPreConstruct,IPostConstruct {public int Phase;public bool ConfigObservedPre,PostObservedConfiguration;public void PreConstruct()=>Phase=1;public void PostConstruct(){PostObservedConfiguration=Phase==2;Phase=3;}}
 private sealed class ParentWidget:WidgetDslTestBase {public Widget? Child;public int Value;}
 private struct NodeParams {public int Id,Inset;public bool Enabled=true;public NodeParams(){}}
 private sealed class ParameterizedLeafWidget:WidgetDslTestBase {public NodeParams Params=new();public int Value;}
 private sealed class InnerBranchWidget:WidgetDslTestBase {public NodeParams Params=new();public ParameterizedLeafWidget? First,Second;}
 private sealed class ParameterizedBranchWidget:WidgetDslTestBase {public NodeParams Params=new();public ParameterizedLeafWidget? Leading;public InnerBranchWidget? Nested;}
 private sealed class ParameterizedRootWidget:WidgetDslTestBase {public NodeParams Params=new();public ParameterizedLeafWidget? Header;public ParameterizedBranchWidget? Content;}

[GuiTest("framework/widget/widget_dsl_tests.cpp::gui/widget-dsl/configures-widget-and-returns-typed-rc")]
public static void Case0(){

    int configured_value = 42;

    PlainWidget widget = Gui.Widget<PlainWidget>(w0 => {
        w0.Value = configured_value;
    });
    Widget base_widget = widget;

    Expect(widget);
    Equal(widget.Value, configured_value);
    Equal(base_widget, widget);

}
[GuiTest("framework/widget/widget_dsl_tests.cpp::gui/widget-dsl/runs-optional-construction-hooks")]
public static void Case1(){

    // source subcase: pre-only
{
        PreConstructWidget widget = Gui.Widget<PreConstructWidget>(w0 => {
            w0.ConfiguredAfterPre = w0.Phase == 1;
            w0.Phase = 2;
        });

        Expect(widget.ConfiguredAfterPre);
        Equal(widget.Phase, 2);
    }

    // source subcase: post-only
{
        PostConstructWidget widget = Gui.Widget<PostConstructWidget>(w0 => {
            w0.Phase = 1;
        });

        Expect(widget.ConfiguredBeforePost);
        Equal(widget.Phase, 2);
    }

    // source subcase: pre-and-post
{
        PrePostConstructWidget widget = Gui.Widget<PrePostConstructWidget>(w0 => {
            w0.ConfigObservedPre = w0.Phase == 1;
            w0.Phase = 2;
        });

        Expect(widget.ConfigObservedPre);
        Expect(widget.PostObservedConfiguration);
        Equal(widget.Phase, 3);
    }

}
[GuiTest("framework/widget/widget_dsl_tests.cpp::gui/widget-dsl/composes-nested-widgets")]
public static void Case2(){

    PlainWidget child_ptr = null;

    ParentWidget parent = Gui.Widget<ParentWidget>(w0 => {
        w0.Value = 7;
        w0.Child = Gui.Widget<PlainWidget>(w1 => {
            w1.Value = 11;
            child_ptr = w1;
        });
    });

    Expect(parent.Child);
    Equal(parent.Value, 7);
    Equal(parent.Child, child_ptr);
    Equal(child_ptr.Value, 11);

}
[GuiTest("framework/widget/widget_dsl_tests.cpp::gui/widget-dsl/composes-complex-widget-and-param-tree")]
public static void Case3(){

    ParameterizedRootWidget root = Gui.Widget<ParameterizedRootWidget>(w0 => {
        w0.Params = new NodeParams { Id = 1, Inset = 4 };
        w0.Header = Gui.Widget<ParameterizedLeafWidget>(w1 => {
            w1.Params = new NodeParams { Id = 2, Enabled = false };
            w1.Value = 20;
        });
        w0.Content = Gui.Widget<ParameterizedBranchWidget>(w1 => {
            w1.Params = new NodeParams { Id = 3, Inset = 8 };
            w1.Leading = Gui.Widget<ParameterizedLeafWidget>(w2 => {
                w2.Params = new NodeParams { Id = 4 };
                w2.Value = 40;
            });
            w1.Nested = Gui.Widget<InnerBranchWidget>(w2 => {
                w2.Params = new NodeParams { Id = 5, Enabled = false };
                w2.First = Gui.Widget<ParameterizedLeafWidget>(w3 => {
                    w3.Params = new NodeParams { Id = 6, Inset = 12 };
                    w3.Value = 60;
                });
                w2.Second = Gui.Widget<ParameterizedLeafWidget>(w3 => {
                    w3.Params = new NodeParams { Id = 7, Inset = 16, Enabled = false };
                    w3.Value = 70;
                });
            });
        });
    });

    Expect(root);
    Expect(root.Header);
    Expect(root.Content);
    Expect(root.Content.Leading);
    Expect(root.Content.Nested);
    Expect(root.Content.Nested.First);
    Expect(root.Content.Nested.Second);

    Equal(root.Params.Id, 1);
    Equal(root.Params.Inset, 4);
    Expect(root.Params.Enabled);
    Equal(root.Header.Params.Id, 2);
    Check.False(root.Header.Params.Enabled);
    Equal(root.Header.Value, 20);

    Equal(root.Content.Params.Id, 3);
    Equal(root.Content.Params.Inset, 8);
    Equal(root.Content.Leading.Params.Id, 4);
    Equal(root.Content.Leading.Value, 40);
    Equal(root.Content.Nested.Params.Id, 5);
    Check.False(root.Content.Nested.Params.Enabled);

    Equal(root.Content.Nested.First.Params.Id, 6);
    Equal(root.Content.Nested.First.Params.Inset, 12);
    Equal(root.Content.Nested.First.Value, 60);
    Equal(root.Content.Nested.Second.Params.Id, 7);
    Equal(root.Content.Nested.Second.Params.Inset, 16);
    Check.False(root.Content.Nested.Second.Params.Enabled);
    Equal(root.Content.Nested.Second.Value, 70);

}
}
