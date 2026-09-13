using System.Runtime.InteropServices;
namespace SkrGui;

// Source: framework/nexus/nexus.hpp + src/framework/nexus/nexus.cpp (611561f8).
public enum ENexusLifeCycle : byte { Initial,Active,Inactive,Destroyed }
public abstract class Nexus
{
    private BorrowedReference<Nexus> _parentBorrow;
    internal Nexus? ParentNode {get=>_parentBorrow.Value;set=>_parentBorrow.Value=value;}
    internal ulong ParentPhysicalIndex=ulong.MaxValue;
    internal bool Root;
    internal NexusSlot CurrentSlot=NexusSlot.Invalid();
    internal Widget? CurrentWidget;
    internal BuildScope? Scope;
    internal uint Depth;
    internal bool Dirty=true,InDirtyList;
    internal ENexusLifeCycle Lifecycle;
    protected Nexus(Widget widget){GuiAssert.Require(widget!=null,"Nexus requires a Widget");CurrentWidget=widget;}
    public Widget Widget()=>CurrentWidget!;
    public Nexus? Parent()=>ParentNode;
    public ulong ParentIndex()=>ParentPhysicalIndex;
    public NexusSlot Slot()=>CurrentSlot;
    public bool IsRoot()=>Root;
    public bool HasChild()=>NumChildren()!=0;
    public abstract ulong NumChildren();
    public abstract Nexus ChildAt(ulong index);
    public ENexusLifeCycle LifeCycle()=>Lifecycle;
    public uint BuildDepth()=>Depth;
    public BuildScope? BuildScope()=>Scope;
    public bool IsDirty()=>Dirty;
    public virtual VisualNode? Visual()=>VisualAttachingChild()?.Visual();
    public void MarkNeedsBuild()
    {
        if(Lifecycle!=ENexusLifeCycle.Active||Dirty)return;
        GuiAssert.Require(Scope?.Owner()!=null,"Active Nexus requires BuildOwner");Dirty=true;Scope!.Owner()!.ScheduleBuildFor(this);
    }
    public void Rebuild(bool force=false)
    {
        GuiAssert.Require(Lifecycle!=ENexusLifeCycle.Initial&&Lifecycle!=ENexusLifeCycle.Destroyed,"Cannot rebuild Initial or Destroyed Nexus");
        if(Lifecycle!=ENexusLifeCycle.Active||(!Dirty&&!force))return;
        var owner=Scope?.Owner();GuiAssert.Require(owner!=null&&owner.IsBuilding(),"Nexus.Rebuild requires active build");
        var previous=owner!.CurrentBuildTarget;owner.CurrentBuildTarget=this;PerformRebuild();owner.CurrentBuildTarget=previous;
        GuiAssert.Require(!Dirty,"PerformRebuild override must call base exactly once");
    }
    protected void UpdateChild(ref Nexus? child,Widget? newWidget,NexusSlot newSlot,ulong newParentIndex)
    {
        RequireBuilding();
        if(newWidget==null){if(child!=null)DeactivateChild(ref child);return;}
        if(child!=null)
        {
            GuiAssert.Require(child.ParentNode==this&&child.Lifecycle==ENexusLifeCycle.Active&&!child.Root&&child.ParentPhysicalIndex==newParentIndex&&ChildAt(newParentIndex)==child,"UpdateChild requires committed parent entry");
            if(ReferenceEquals(child.CurrentWidget,newWidget)){if(child.CurrentSlot!=newSlot)UpdateSlotForChild(child,newSlot);return;}
            if(SkrGui.Widget.CanUpdateWidget(child.Widget(),newWidget)){if(child.CurrentSlot!=newSlot)UpdateSlotForChild(child,newSlot);child.UpdateWidget(newWidget);return;}
            DeactivateChild(ref child);
        }
        InflateWidget(ref child,newWidget,newSlot,newParentIndex);
    }
    protected void UpdateChildren(List<Nexus?> children,IReadOnlyList<Widget> newWidgets)
    {
        RequireBuilding();
        for(int i=0;i<children.Count;i++){var child=children[i];GuiAssert.Require(child!=null&&child.ParentNode==this&&child.Lifecycle==ENexusLifeCycle.Active&&!child.Root&&child.ParentPhysicalIndex==(ulong)i&&ChildAt((ulong)i)==child,"Old children require committed Active parent entries");}
        foreach(var widget in newWidgets)GuiAssert.Require(widget!=null,"New Widgets cannot contain null");
        int oldSize=children.Count,newSize=newWidgets.Count;children.EnsureCapacity(newSize);
        var plan=new Nexus?[newSize];
        bool CanUpdate(int oldIndex,int newIndex)=>SkrGui.Widget.CanUpdateWidget(children[oldIndex]!.Widget(),newWidgets[newIndex]);
        void RetainChild(int oldIndex,int newIndex)=>plan[newIndex]=children[oldIndex];
        int oldTop=0,newTop=0,oldBottom=oldSize-1,newBottom=newSize-1;
        while(oldTop<=oldBottom&&newTop<=newBottom){if(!CanUpdate(oldTop,newTop))break;RetainChild(oldTop,newTop);++oldTop;++newTop;}
        while(oldTop<=oldBottom&&newTop<=newBottom){if(!CanUpdate(oldBottom,newBottom))break;RetainChild(oldBottom,newBottom);--oldBottom;--newBottom;}
        int prefixCount=newTop;
        var oldUnkeyed=new List<Nexus>();var oldKeyed=new Dictionary<Key,Nexus>();
        if(oldTop<=oldBottom)
        {
            oldUnkeyed.EnsureCapacity(oldBottom-oldTop+1);oldKeyed.EnsureCapacity(oldBottom-oldTop+1);
            while(oldTop<=oldBottom){var oldChild=children[oldTop]!;var oldKey=oldChild.Widget().Key;if(oldKey.IsNone())oldUnkeyed.Add(oldChild);else GuiAssert.Require(oldKeyed.TryAdd(oldKey,oldChild),"Sibling Widgets must have unique keys");++oldTop;}
        }
        while(newTop<=newBottom)
        {
            var newWidget=newWidgets[newTop];
            if(!newWidget.Key.IsNone()&&oldKeyed.TryGetValue(newWidget.Key,out var found)&&SkrGui.Widget.CanUpdateWidget(found.Widget(),newWidget)){oldKeyed.Remove(newWidget.Key);plan[newTop]=found;}
            ++newTop;
        }
        void DeactivateAndRemove(Nexus child)
        {
            int index=(int)child.ParentPhysicalIndex;DeactivateChild(ref CollectionsMarshal.AsSpan(children)[index]);
            children[index]=children[^1];children.RemoveAt(children.Count-1);
            if(index<children.Count)SyncChildIndex(children[index]!, (ulong)index);
        }
        void UpdateAt(int newIndex)
        {
            var planned=plan[newIndex];
            if(planned!=null)
            {
                int oldIndex=(int)planned.ParentPhysicalIndex;
                if(oldIndex!=newIndex){(children[oldIndex],children[newIndex])=(children[newIndex],children[oldIndex]);SyncChildIndex(planned,(ulong)newIndex);SyncChildIndex(children[oldIndex]!, (ulong)oldIndex);}
                UpdateChild(ref CollectionsMarshal.AsSpan(children)[newIndex],newWidgets[newIndex],new NexusSlot((ulong)newIndex),(ulong)newIndex);
            }
            else
            {
                children.Add(newWidgets[newIndex].CreateNexus());int physical=children.Count-1;
                InflateWidget(ref CollectionsMarshal.AsSpan(children)[physical],newWidgets[newIndex],new NexusSlot((ulong)newIndex),(ulong)physical);
                if(physical!=newIndex){(children[physical],children[newIndex])=(children[newIndex],children[physical]);SyncChildIndex(children[newIndex]!, (ulong)newIndex);SyncChildIndex(children[physical]!, (ulong)physical);}
            }
        }
        for(int i=0;i<prefixCount;i++)UpdateAt(i);
        foreach(var child in oldUnkeyed)DeactivateAndRemove(child);
        for(int i=prefixCount;i<newSize;i++)UpdateAt(i);
        foreach(var child in oldKeyed.Values)DeactivateAndRemove(child);
        GuiAssert.Require(children.Count==newSize,"Reconciliation child count mismatch");
    }
    protected void InflateWidget(ref Nexus? child,Widget newWidget,NexusSlot newSlot,ulong parentIndex)
    {
        RequireBuilding();
        // Source GlobalKey registry/retake is still TODO.
        child??=newWidget.CreateNexus();
        GuiAssert.Require(child!=null&&parentIndex<NumChildren()&&ReferenceEquals(child.CurrentWidget,newWidget)&&child.Lifecycle==ENexusLifeCycle.Initial&&ChildAt(parentIndex)==child,"Widget.CreateNexus must bind Initial Nexus in committed parent entry");
        child!.Initialize(this,parentIndex,newSlot,Scope!,false);
        GuiAssert.Require(child.Lifecycle==ENexusLifeCycle.Active,"Initialize must activate child");
        if(child.Dirty)Scope!.Owner()!.ScheduleBuildFor(child);
    }
    protected void DeactivateChild(ref Nexus? child)
    {
        RequireBuilding();GuiAssert.Require(child!=null&&child.ParentNode==this&&child.Lifecycle==ENexusLifeCycle.Active&&!child.Root&&child.ParentPhysicalIndex<NumChildren()&&ChildAt(child.ParentPhysicalIndex)==child,"DeactivateChild requires committed Active child");
        child!.DetachVisual();Scope!.Owner()!.DeactivateNexus(child);child=null;
    }
    protected void ActivateChild(ref Nexus? child,Nexus inactiveChild,NexusSlot newSlot,ulong parentIndex)
    {
        RequireBuilding();GuiAssert.Require(child==null&&inactiveChild.Lifecycle==ENexusLifeCycle.Inactive&&inactiveChild.ParentNode==null&&!inactiveChild.Root&&inactiveChild.Scope==Scope,"ActivateChild requires inactive same-scope non-root");
        var owner=Scope!.Owner()!;child=owner.TakeInactiveNexus(inactiveChild);
        GuiAssert.Require(parentIndex<NumChildren()&&ChildAt(parentIndex)==inactiveChild,"Activation requires committed parent entry");
        inactiveChild.ParentNode=this;inactiveChild.ParentPhysicalIndex=parentIndex;owner.ActivateNexus(inactiveChild);child.AttachVisual(newSlot);child.ScheduleDirtySubtree();
    }
    protected void SyncChildIndex(Nexus child,ulong index)
    { GuiAssert.Require(child.ParentNode==this&&index<NumChildren()&&ChildAt(index)==child,"Sync index requires committed parent entry");child.ParentPhysicalIndex=index; }
    protected void UpdateSlotForChild(Nexus child,NexusSlot newSlot)
    { GuiAssert.Require(child.ParentNode==this,"Slot update requires owned child");for(Nexus? nexus=child;nexus!=null;){nexus.UpdateSlot(newSlot);nexus=nexus.VisualAttachingChild();} }
    protected virtual Nexus? VisualAttachingChild(){ulong count=NumChildren();GuiAssert.Require(count<=1,"Non-Visual Nexus exposes at most one Visual-attaching child");return count==1?ChildAt(0):null;}
    protected virtual void AttachVisual(NexusSlot slot){ulong count=NumChildren();for(ulong i=0;i<count;i++)ChildAt(i).AttachVisual(slot);CurrentSlot=slot;}
    protected virtual void DetachVisual(){ulong count=NumChildren();for(ulong i=0;i<count;i++)ChildAt(i).DetachVisual();CurrentSlot=NexusSlot.Invalid();}
    protected virtual void UpdateSlot(NexusSlot newSlot)=>CurrentSlot=newSlot;
    protected virtual void ApplyVisualSlot(VisualNode visual) { }
    protected void ApplyVisualSlotToAncestors(VisualNode visual,Nexus ancestorVisualNexus){for(var ancestor=ParentNode;ancestor!=ancestorVisualNexus;ancestor=ancestor!.ParentNode)ancestor!.ApplyVisualSlot(visual);}
    protected virtual void OnInitialActivate() { }
    protected virtual void OnActivate() { }
    protected virtual void OnDeactivate() { }
    protected virtual void OnDestroy() { }
    protected virtual void UpdateWidget(Widget newWidget)
    { GuiAssert.Require(Lifecycle==ENexusLifeCycle.Active&&CurrentWidget!=null&&newWidget!=null&&!ReferenceEquals(CurrentWidget,newWidget)&&SkrGui.Widget.CanUpdateWidget(CurrentWidget,newWidget),"UpdateWidget requires a distinct compatible Widget on Active Nexus");CurrentWidget=newWidget; }
    protected virtual void PerformRebuild()=>Dirty=false;
    internal void UpdateWidgetInternal(Widget widget)=>UpdateWidget(widget);
    internal void DetachVisualInternal()=>DetachVisual();
    internal void Initialize(Nexus? parent,ulong parentIndex,NexusSlot slot,BuildScope scope,bool root)
    {
        GuiAssert.Require(Lifecycle==ENexusLifeCycle.Initial&&CurrentWidget!=null&&ParentNode==null&&Scope==null&&ParentPhysicalIndex==ulong.MaxValue,"Nexus initialization may happen only once");
        GuiAssert.Require(root==(parent==null)&&root==(parentIndex==ulong.MaxValue),"Only root initialization may omit parent/index");
        GuiAssert.Require(parent==null||(parent.Lifecycle==ENexusLifeCycle.Active&&parent.Scope==scope&&parentIndex<parent.NumChildren()&&parent.ChildAt(parentIndex)==this),"Child initialization requires committed Active parent in same scope");
        GuiAssert.Require(scope.Owner()!=null,"Nexus initialization requires BuildOwner");
        ParentNode=parent;ParentPhysicalIndex=parentIndex;CurrentSlot=slot;Root=root;Scope=scope;Depth=parent!=null?parent.Depth+1:1;Lifecycle=ENexusLifeCycle.Active;OnInitialActivate();AttachVisual(CurrentSlot);
        if(scope.Owner()!.IsBuilding())Rebuild();
    }
    internal void DeactivateInternal(){GuiAssert.Require(Lifecycle==ENexusLifeCycle.Active,"Deactivate requires Active Nexus");OnDeactivate();Lifecycle=ENexusLifeCycle.Inactive;}
    internal void ActivateInternal(){GuiAssert.Require(Lifecycle==ENexusLifeCycle.Inactive,"Activate requires Inactive Nexus");Lifecycle=ENexusLifeCycle.Active;OnActivate();}
    internal void DestroyInternal()
    {
        GuiAssert.Require(Lifecycle==ENexusLifeCycle.Inactive&&!InDirtyList,"Destroy requires inactive and dequeued Nexus");OnDestroy();ParentNode=null;ParentPhysicalIndex=ulong.MaxValue;CurrentSlot=NexusSlot.Invalid();CurrentWidget=null;Scope=null;Dirty=false;Lifecycle=ENexusLifeCycle.Destroyed;
    }
    private void ScheduleDirtySubtree()
    { GuiAssert.Require(Lifecycle==ENexusLifeCycle.Active,"Dirty scheduling requires Active subtree");if(Dirty){GuiAssert.Require(Scope?.Owner()?.IsBuilding()==true,"Reactivation scheduling requires active build");Scope!.Owner()!.ScheduleBuildFor(this);}ulong count=NumChildren();for(ulong i=0;i<count;i++)ChildAt(i).ScheduleDirtySubtree(); }
    private void RequireBuilding()=>GuiAssert.Require(Lifecycle==ENexusLifeCycle.Active&&Scope?.Owner()?.IsBuilding()==true,"Tree reconciliation requires Active parent and build transaction");
}
