using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime.ExceptionServices;
namespace SkrGui;

// Sources: framework/widget/*.hpp, widget/*.cpp, state.hpp, build_context.hpp.
public abstract class Widget
{
    public Key Key=new();
    public abstract Nexus CreateNexus();
    public static bool CanUpdateWidget(Widget oldWidget,Widget newWidget)=>oldWidget.Key==newWidget.Key&&oldWidget.GetType()==newWidget.GetType();
}
public abstract class ComponentWidget : Widget
{
    public sealed override Nexus CreateNexus()=>new NexusComponent(this);
    protected virtual State? CreateState()=>null;
    protected abstract Widget Build(BuildContext context,State? state);
    protected virtual bool ShouldRebuild(ComponentWidget oldWidget)=>true;
    internal State? CreateStateForNexus()=>CreateState();
    internal Widget BuildForNexus(BuildContext context,State? state)=>Build(context,state);
    internal bool ShouldRebuildForNexus(ComponentWidget oldWidget)=>ShouldRebuild(oldWidget);
}
public abstract class VisualWidget : Widget
{
    protected abstract VisualNode CreateVisual(BuildContext context);
    protected virtual void UpdateVisual(BuildContext context,VisualNode visual) { }
    protected virtual void DidUnmountVisual(VisualNode visual) { }
    internal VisualNode CreateVisualForNexus(BuildContext context)=>CreateVisual(context);
    internal void UpdateVisualForNexus(BuildContext context,VisualNode visual)=>UpdateVisual(context,visual);
    internal void DidUnmountVisualForNexus(VisualNode visual)=>DidUnmountVisual(visual);
}
public abstract class VisualWidgetLeaf : VisualWidget { public sealed override Nexus CreateNexus()=>new NexusVisualLeaf(this); }
public abstract class VisualWidgetSingleChild : VisualWidget { public Widget? Child;public sealed override Nexus CreateNexus()=>new NexusVisualSingleChild(this); }
public abstract class VisualWidgetMultiChild : VisualWidget { public List<Widget> Children=new();public sealed override Nexus CreateNexus()=>new NexusVisualMultiChild(this); }
public abstract class VisualSlotWidget : Widget
{
    public Widget? Child;
    public sealed override Nexus CreateNexus()=>new NexusVisualSlot(this);
    protected abstract void ApplyVisualSlot(VisualSlot slot);
    internal void ApplyVisualSlotForNexus(VisualSlot slot)=>ApplyVisualSlot(slot);
}
public class State : IDisposable
{
    private BorrowedReference<Nexus> _attachedNexusBorrow;
    internal Nexus? AttachedNexus {get=>_attachedNexusBorrow.Value;set=>_attachedNexusBorrow.Value=value;}
    // Deterministic counterpart of the C++ State virtual destructor.
    public virtual void Dispose() { }
    public BuildContext Context(){GuiAssert.Require(AttachedNexus!=null,"State is not attached to a NexusComponent");return new(AttachedNexus!);}
    public void MarkNeedsRebuild(){GuiAssert.Require(AttachedNexus!=null,"State is not attached to a NexusComponent");AttachedNexus!.MarkNeedsBuild();}
}
public readonly struct BuildContext
{
    private readonly Nexus _nexus;
    public BuildContext(Nexus nexus){_nexus=nexus;}
    public Widget Widget()=>_nexus.Widget();
    public VisualNode? Visual()=>_nexus.Visual();
}
public interface IPreConstruct { void PreConstruct(); }
public interface IPostConstruct { void PostConstruct(); }
public readonly struct WidgetBuilder<T> where T:Widget,new()
{
    public readonly string File;
    public readonly int Line;
    public WidgetBuilder(string file,int line){File=file;Line=line;}
    public T Configure(Action<T> configure)
    {
        var widget=new T();
        InvokeConstructionHook(widget,"PreConstruct");
        configure(widget);
        InvokeConstructionHook(widget,"PostConstruct");
        return widget;
    }
    private static void InvokeConstructionHook(T widget,string name)
    {
        // Source requires { w->hook() } -> same_as<void>; no interface is required.
        var method=typeof(T).GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static)
            .FirstOrDefault(m=>m.Name==name&&!m.ContainsGenericParameters&&m.ReturnType==typeof(void)&&m.GetParameters().Length==0);
        if(method==null)return;
        try { method.Invoke(method.IsStatic?null:widget,null); }
        catch(TargetInvocationException error) when(error.InnerException!=null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }
}
public static class Gui
{
    public static T Widget<T>(Action<T> configure,[CallerFilePath]string file="",[CallerLineNumber]int line=0)where T:Widget,new()=>new WidgetBuilder<T>(file,line).Configure(configure);
}
// Source reactive.hpp defines interfaces only, without automatic dependency tracking.
public interface IProvider { void Subscribe(IConsumer consumer);void Unsubscribe(IConsumer consumer); }
public interface IConsumer { void NotifyProviderChanged(IProvider provider);void NotifyProviderDead(IProvider provider); }
public interface IComputed : IProvider,IConsumer { }
