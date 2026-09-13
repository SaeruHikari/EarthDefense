namespace SkrGui.Tests;

internal static class WidgetDslSourceContractTests
{
    private abstract class Leaf:Widget {public override Nexus CreateNexus()=>null!;}
    private sealed class MethodOnlyWidget:Leaf
    {
        public int Phase;
        public bool PreObserved,PostObserved;
        public void PreConstruct(){Phase=1;}
        public void PostConstruct(){PostObserved=Phase==2;Phase=3;}
    }
    private sealed class NonVoidHookWidget:Leaf
    {
        public int Phase;
        public int PreConstruct()=>throw new InvalidOperationException("A non-void hook does not satisfy original same_as<void>.");
        public void PostConstruct(){++Phase;}
    }
    private sealed class ThrowingHookWidget:Leaf
    {
        public static readonly InvalidOperationException OriginalError=new("original hook error");
        public void PreConstruct()=>throw OriginalError;
    }
    [GuiTest("audit/framework/widget_dsl.hpp::public-method-hooks-do-not-require-an-interface")]
    public static void PublicMethodHooks()
    {
        var widget=Gui.Widget<MethodOnlyWidget>(value=>{value.PreObserved=value.Phase==1;value.Phase=2;});
        Check.That(widget.PreObserved);Check.That(widget.PostObserved);Check.Equal(3,widget.Phase);
    }
    [GuiTest("audit/framework/widget_dsl.hpp::same_as_void-and-original-hook-exception")]
    public static void SignatureAndException()
    {
        var widget=Gui.Widget<NonVoidHookWidget>(value=>value.Phase=5);Check.Equal(6,widget.Phase);
        Exception? observed=null;try{Gui.Widget<ThrowingHookWidget>(_=>{});}catch(Exception error){observed=error;}
        Check.Same(ThrowingHookWidget.OriginalError,observed!);
    }
}
