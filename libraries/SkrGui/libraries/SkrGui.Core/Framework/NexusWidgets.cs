namespace SkrGui;

// Sources: src/framework/nexus/nexus_component.cpp and nexus_visual*.cpp (611561f8).
public class NexusComponent : Nexus
{
    private State? _state;private Nexus? _child;
    public NexusComponent(ComponentWidget widget):base(widget)
    { _state=widget.CreateStateForNexus();if(_state!=null){GuiAssert.Require(_state.AttachedNexus==null,"CreateState must return unattached State");_state.AttachedNexus=this;} }
    public sealed override ulong NumChildren()=>_child!=null?1UL:0;
    public sealed override Nexus ChildAt(ulong index){GuiAssert.Require(index==0&&_child!=null,"NexusComponent index out of range");return _child!;}
    protected ComponentWidget ComponentWidget()=>(ComponentWidget)Widget();
    protected override void OnDestroy(){_child=null;if(_state!=null){_state.AttachedNexus=null;_state.Dispose();_state=null;}base.OnDestroy();}
    protected override void UpdateWidget(Widget newWidget)
    { var oldWidget=ComponentWidget();var updated=(ComponentWidget)newWidget;bool shouldRebuild=updated.ShouldRebuildForNexus(oldWidget);base.UpdateWidget(newWidget);if(shouldRebuild)Rebuild(true); }
    protected override void PerformRebuild()
    {
        var context=new BuildContext(this);var built=ComponentWidget().BuildForNexus(context,_state);GuiAssert.Require(built!=null,"Component.Build must return a valid Widget");base.PerformRebuild();UpdateChild(ref _child,built,Slot(),0);
    }
}
public abstract class NexusVisual : Nexus
{
    private VisualNode? _visual;
    private BorrowedReference<NexusVisual> _ancestorVisualBorrow;
    private NexusVisual? _ancestorVisualNexus {get=>_ancestorVisualBorrow.Value;set=>_ancestorVisualBorrow.Value=value;}
    private bool _isFirstBuild=true;
    protected NexusVisual(VisualWidget widget):base(widget){}
    public sealed override VisualNode? Visual()=>_visual;
    protected VisualWidget VisualWidget()=>(VisualWidget)Widget();
    protected override Nexus? VisualAttachingChild()=>null;
    protected override void AttachVisual(NexusSlot slot)
    {
        GuiAssert.Require(_visual!=null&&_ancestorVisualNexus==null,"Cannot attach Visual twice");base.UpdateSlot(slot);_ancestorVisualNexus=FindAncestorVisualNexus();if(_ancestorVisualNexus==null)return;
        _ancestorVisualNexus.InsertVisualChild(_visual!,slot);ApplyVisualSlotToAncestors(_visual!,_ancestorVisualNexus);
    }
    protected override void DetachVisual()
    { GuiAssert.Require(_visual!=null,"NexusVisual requires Visual");if(_ancestorVisualNexus!=null){_ancestorVisualNexus.RemoveVisualChild(_visual!,Slot());_ancestorVisualNexus=null;}base.UpdateSlot(NexusSlot.Invalid()); }
    protected override void UpdateSlot(NexusSlot newSlot)
    {var oldSlot=Slot();base.UpdateSlot(newSlot);_ancestorVisualNexus?.MoveVisualChild(_visual!,oldSlot,newSlot);}
    protected override void OnInitialActivate(){base.OnInitialActivate();_visual=VisualWidget().CreateVisualForNexus(new BuildContext(this));GuiAssert.Require(_visual!=null,"CreateVisual must return valid Visual");}
    protected override void OnDestroy(){VisualWidget().DidUnmountVisualForNexus(_visual!);_ancestorVisualNexus=null;_visual=null;base.OnDestroy();}
    protected override void UpdateWidget(Widget newWidget){base.UpdateWidget(newWidget);_isFirstBuild=false;Rebuild(true);}
    protected override void PerformRebuild(){if(_isFirstBuild)_isFirstBuild=false;else VisualWidget().UpdateVisualForNexus(new BuildContext(this),_visual!);base.PerformRebuild();}
    protected abstract void InsertVisualChild(VisualNode child,NexusSlot slot);
    protected abstract void MoveVisualChild(VisualNode child,NexusSlot oldSlot,NexusSlot newSlot);
    protected abstract void RemoveVisualChild(VisualNode child,NexusSlot slot);
    private NexusVisual? FindAncestorVisualNexus(){for(var ancestor=Parent();ancestor!=null;ancestor=ancestor.Parent())if(ancestor is NexusVisual visual)return visual;return null;}
}
public class NexusVisualLeaf : NexusVisual
{
    public NexusVisualLeaf(VisualWidgetLeaf widget):base(widget){}
    public sealed override ulong NumChildren()=>0;
    public sealed override Nexus ChildAt(ulong index)=>throw new InvalidOperationException("NexusVisualLeaf has no children");
    protected override void InsertVisualChild(VisualNode child,NexusSlot slot)=>throw new InvalidOperationException("Leaf cannot insert child");
    protected override void MoveVisualChild(VisualNode child,NexusSlot oldSlot,NexusSlot newSlot)=>throw new InvalidOperationException("Leaf cannot move child");
    protected override void RemoveVisualChild(VisualNode child,NexusSlot slot)=>throw new InvalidOperationException("Leaf cannot remove child");
}
public class NexusVisualSingleChild : NexusVisual
{
    private Nexus? _child;
    public NexusVisualSingleChild(VisualWidgetSingleChild widget):base(widget){}
    private VisualSingleChild SingleVisual()=>(VisualSingleChild)Visual()!;
    public sealed override ulong NumChildren()=>_child!=null?1UL:0;
    public sealed override Nexus ChildAt(ulong index){GuiAssert.Require(index==0&&_child!=null,"NexusVisualSingleChild index out of range");return _child!;}
    protected override void OnInitialActivate(){base.OnInitialActivate();_=SingleVisual();}
    protected override void OnDestroy(){_child=null;base.OnDestroy();}
    protected override void PerformRebuild(){base.PerformRebuild();UpdateChild(ref _child,((VisualWidgetSingleChild)Widget()).Child,NexusSlot.Invalid(),0);}
    protected override void InsertVisualChild(VisualNode child,NexusSlot slot){GuiAssert.Require(!slot.IsValid()&&SingleVisual().Child()==null,"Single Visual child insertion requires empty child and invalid slot");SingleVisual().SetChild(child);}
    protected override void MoveVisualChild(VisualNode child,NexusSlot oldSlot,NexusSlot newSlot)=>throw new InvalidOperationException("VisualSingleChild cannot move child");
    protected override void RemoveVisualChild(VisualNode child,NexusSlot slot){GuiAssert.Require(!slot.IsValid()&&SingleVisual().Child()==child,"Can remove only current single child");SingleVisual().RemoveChild();}
}
public class NexusVisualMultiChild : NexusVisual
{
    private readonly List<Nexus?> _children=new();
    public NexusVisualMultiChild(VisualWidgetMultiChild widget):base(widget){}
    private VisualMultiChild MultiVisual()=>(VisualMultiChild)Visual()!;
    public sealed override ulong NumChildren()=>(ulong)_children.Count;
    public sealed override Nexus ChildAt(ulong index){GuiAssert.Require(index<(ulong)_children.Count,"NexusVisualMultiChild index out of range");return _children[(int)index]!;}
    protected override void OnInitialActivate(){base.OnInitialActivate();_=MultiVisual();}
    protected override void OnDestroy(){_children.Clear();base.OnDestroy();}
    protected override void PerformRebuild()
    {base.PerformRebuild();UpdateChildren(_children,((VisualWidgetMultiChild)Widget()).Children);var visual=MultiVisual();visual.RebuildFlush();GuiAssert.Require(visual.NumChildren()==(ulong)_children.Count,"Nexus and Visual count must match");for(int i=0;i<_children.Count;i++)GuiAssert.Require(visual.ChildAt((ulong)i)==_children[i]!.Visual(),"Nexus and Visual child order must match");}
    protected override void InsertVisualChild(VisualNode child,NexusSlot slot){GuiAssert.Require(slot.IsValid(),"Multi Visual requires valid slot");MultiVisual().RebuildAddChild(child,slot.Index());}
    protected override void MoveVisualChild(VisualNode child,NexusSlot oldSlot,NexusSlot newSlot){GuiAssert.Require(oldSlot.IsValid()&&newSlot.IsValid(),"Multi Visual move requires valid slots");MultiVisual().RebuildMoveChild(child,oldSlot.Index(),newSlot.Index());}
    protected override void RemoveVisualChild(VisualNode child,NexusSlot slot){GuiAssert.Require(slot.IsValid(),"Multi Visual requires valid slot");MultiVisual().RebuildRemoveChild(child,slot.Index());}
}
public class NexusVisualSlot : Nexus
{
    private Nexus? _child;
    public NexusVisualSlot(VisualSlotWidget widget):base(widget){}
    private VisualSlotWidget SlotWidget()=>(VisualSlotWidget)Widget();
    public sealed override ulong NumChildren()=>_child!=null?1UL:0;
    public sealed override Nexus ChildAt(ulong index){GuiAssert.Require(index==0&&_child!=null,"NexusVisualSlot index out of range");return _child!;}
    protected override void ApplyVisualSlot(VisualNode visual){GuiAssert.Require(visual.Slot()!=null,"VisualSlotWidget requires ancestor container slot");SlotWidget().ApplyVisualSlotForNexus(visual.Slot()!);}
    protected override void OnDestroy(){_child=null;base.OnDestroy();}
    protected override void UpdateWidget(Widget newWidget){base.UpdateWidget(newWidget);if(Visual() is {} attaching)ApplyVisualSlot(attaching);Rebuild(true);}
    protected override void PerformRebuild(){base.PerformRebuild();GuiAssert.Require(SlotWidget().Child!=null,"VisualSlotWidget requires child");UpdateChild(ref _child,SlotWidget().Child,Slot(),0);}
}
