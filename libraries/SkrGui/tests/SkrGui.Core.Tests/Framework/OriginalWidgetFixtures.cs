using SkrGui;
namespace SkrGui.Tests;

// Direct counterparts of fixtures in framework/widget/{component,visual}_widget_tests.cpp.
internal static class OriginalWidgetFixtures
{
    public sealed class ComponentStats
    {
        public ulong CreateStateCount,DestroyStateCount,BuildCount,ShouldRebuildCount;
        public State? State,BuildState;
        public Widget? BuildContextWidget;
        public VisualNode? BuildContextVisual;
        public int ComparedOldValue;
    }
    public sealed class TestComponentState(ComponentStats stats):State
    {
        public int Value;
        public override void Dispose(){++stats.DestroyStateCount;}
    }
    public class TestComponentVisual:VisualLeaf
    {
        public int Value;
        protected override void PerformLayout()=>SetSize(Constraints().Smallest());
        protected override float ComputeMinIntrinsicWidth(float height)=>0;
        protected override float ComputeMaxIntrinsicWidth(float height)=>0;
        protected override float ComputeMinIntrinsicHeight(float width)=>0;
        protected override float ComputeMaxIntrinsicHeight(float width)=>0;
        protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>constraints.Smallest();
    }
    public sealed class TestComponentLeafWidget:VisualWidgetLeaf
    {
        public int Value;
        protected override VisualNode CreateVisual(BuildContext context)=>new TestComponentVisual{Value=Value};
        protected override void UpdateVisual(BuildContext context,VisualNode visual)=>((TestComponentVisual)visual).Value=Value;
    }
    public sealed class TestComponentWidget:ComponentWidget
    {
        public ComponentStats Stats=null!;public int Value;public bool RebuildOnUpdate=true;
        protected override State CreateState(){++Stats.CreateStateCount;var result=new TestComponentState(Stats);Stats.State=result;return result;}
        protected override Widget Build(BuildContext context,State? state)
        {++Stats.BuildCount;Stats.BuildState=state;Stats.BuildContextWidget=context.Widget();Stats.BuildContextVisual=context.Visual();var typedState=(TestComponentState)state!;return new TestComponentLeafWidget{Value=Value+typedState.Value};}
        protected override bool ShouldRebuild(ComponentWidget oldWidget)
        {++Stats.ShouldRebuildCount;Stats.ComparedOldValue=((TestComponentWidget)oldWidget).Value;return RebuildOnUpdate;}
    }
    public sealed class StatelessTestComponentWidget:ComponentWidget
    {
        public ComponentStats Stats=null!;public int Value;
        protected override Widget Build(BuildContext context,State? state)
        {++Stats.BuildCount;Stats.BuildState=state;Stats.BuildContextWidget=context.Widget();return new TestComponentLeafWidget{Value=Value};}
    }
    public class ComponentTreeHost:IDisposable
    {
        public BuildOwner Owner;public Nexus? Root;
        public ComponentTreeHost(Widget widget){Owner=new();Root=widget.CreateNexus();Owner.AddRoot(Root);Owner.BuildScope(Root);}
        public void Update(Widget widget)=>Owner.UpdateRoot(Root!,widget);
        public void Rebuild()=>Owner.BuildScope(Root!);
        public void Destroy(){if(Root==null)return;Owner.RemoveRoot(Root);Owner.FinalizeNexus();Root=null;Owner=null!;}
        public void Dispose()=>Destroy();
    }
    public sealed class VisualTreeHost(Widget widget):ComponentTreeHost(widget);
    public static TestComponentWidget MakeStatefulComponent(ComponentStats stats,int value,bool rebuildOnUpdate=true)=>new(){Stats=stats,Value=value,RebuildOnUpdate=rebuildOnUpdate};
    public static StatelessTestComponentWidget MakeStatelessComponent(ComponentStats stats,int value)=>new(){Stats=stats,Value=value};
    public sealed class VisualLifecycleStats
    {public ulong CreateCount,UpdateCount,UnmountCount;public Widget? CreateContextWidget,UpdateContextWidget;public VisualNode? CreateContextVisual,UpdateContextVisual;}
    public sealed class TestLeafVisual:TestComponentVisual { }
    public sealed class TestLeafWidget:VisualWidgetLeaf
    {
        public VisualLifecycleStats Stats=null!;public int Value;
        protected override VisualNode CreateVisual(BuildContext context)
        {Check.NotNull(Stats);++Stats.CreateCount;Stats.CreateContextWidget=context.Widget();Stats.CreateContextVisual=context.Visual();return new TestLeafVisual{Value=Value};}
        protected override void UpdateVisual(BuildContext context,VisualNode visual)
        {Check.NotNull(Stats);++Stats.UpdateCount;Stats.UpdateContextWidget=context.Widget();Stats.UpdateContextVisual=context.Visual();((TestLeafVisual)visual).Value=Value;}
        protected override void DidUnmountVisual(VisualNode visual){Check.NotNull(Stats);Check.NotNull(visual);++Stats.UnmountCount;}
    }
    public sealed class TestSingleWidget:VisualWidgetSingleChild
    {
        public VisualLifecycleStats Stats=null!;
        protected override VisualNode CreateVisual(BuildContext context){Check.NotNull(Stats);++Stats.CreateCount;return new VisualProxy();}
        protected override void UpdateVisual(BuildContext context,VisualNode visual){Check.NotNull(Stats);Check.NotNull(visual);++Stats.UpdateCount;}
        protected override void DidUnmountVisual(VisualNode visual){Check.NotNull(Stats);Check.NotNull(visual);++Stats.UnmountCount;}
    }
    public sealed class TestMultiWidget:VisualWidgetMultiChild
    {
        public VisualLifecycleStats Stats=null!;
        protected override VisualNode CreateVisual(BuildContext context){Check.NotNull(Stats);++Stats.CreateCount;return new VisualStack();}
        protected override void UpdateVisual(BuildContext context,VisualNode visual){Check.NotNull(Stats);Check.NotNull(visual);++Stats.UpdateCount;}
        protected override void DidUnmountVisual(VisualNode visual){Check.NotNull(Stats);Check.NotNull(visual);++Stats.UnmountCount;}
    }
    public sealed class Counter { public ulong Value; }
    public sealed class TestStackSlotWidget:VisualSlotWidget
    {
        public Counter ApplyCount=null!;public float Left;
        protected override void ApplyVisualSlot(VisualSlot slot){Check.NotNull(ApplyCount);++ApplyCount.Value;((VisualStackSlot)slot).SetLeft(Left);}
    }
    public static TestLeafWidget MakeLeaf(VisualLifecycleStats stats,int value)=>new(){Stats=stats,Value=value};
    public static TestStackSlotWidget MakeSlot(long key,Widget child,Counter applyCount,float left)=>new(){Key=new Key(key),Child=child,ApplyCount=applyCount,Left=left};
    public static T? RttrCast<T>(object? value)where T:class=>value as T;
    public static void ExpectFalse(object? value)=>Check.That(value is bool b?!b:value==null);
    public static void Expect(object? value)=>Check.That(value is bool b?b:value!=null);
    public static void Equal(object? a,object? b)
    {if(a is Counter c)a=c.Value;if(b is Counter d)b=d.Value;if(a is IConvertible&&b is IConvertible)Check.That(Convert.ToDouble(a)==Convert.ToDouble(b),$"{a} != {b}");else Check.That(Equals(a,b),$"{a} != {b}");}
}
