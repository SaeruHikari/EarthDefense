namespace SkrGui;

// Source: SkrGuiCore/visual/visual_node.hpp + src/visual/visual_node.cpp (611561f8).
public abstract class VisualNode
{
    private BorrowedReference<VisualNode> _parentBorrow;
    private VisualNode? _parent {get=>_parentBorrow.Value;set=>_parentBorrow.Value=value;}
    private ulong _parentIndex=ulong.MaxValue;
    private BorrowedReference<VisualOwner> _ownerBorrow;
    private VisualOwner? _owner {get=>_ownerBorrow.Value;set=>_ownerBorrow.Value=value;}
    private VisualSlot? _slot;
    private BoxConstraints _constraints=new();
    private Sizef _size=new();
    private bool _needsLayout=true,_isRelayoutBoundary;
    private uint _visualDepth;
    protected bool _needsRebuildFlush;
    public VisualNode? Parent()=>_parent;
    public ulong ParentIndex()=>_parentIndex;
    public VisualSlot? Slot()=>_slot;
    public bool HasChild()=>NumChildren()!=0;
    public abstract ulong NumChildren();
    public abstract VisualNode ChildAt(ulong index);
    public bool NeedsRebuildFlush()=>_needsRebuildFlush;
    public bool NeedsLayout()=>_needsLayout;
    public VisualOwner? Owner()=>_owner;
    public uint VisualDepth()=>_visualDepth;
    // Source intrinsic/dry/baseline caches are TODO; queries still call through.
    public float GetMinIntrinsicWidth(float height)=>ComputeMinIntrinsicWidth(height);
    public float GetMaxIntrinsicWidth(float height)=>ComputeMaxIntrinsicWidth(height);
    public float GetMinIntrinsicHeight(float width)=>ComputeMinIntrinsicHeight(width);
    public float GetMaxIntrinsicHeight(float width)=>ComputeMaxIntrinsicHeight(width);
    public Sizef GetDryLayout(BoxConstraints constraints)=>ComputeDryLayout(constraints);
    public float? GetDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>ComputeDryBaseline(constraints,baseline);
    public float? GetDistanceToBaseline(ETextBaseline baseline,bool onlyReal=false)
    { float? result=GetDistanceToActualBaseline(baseline); return !result.HasValue&&!onlyReal ? Size().Height : result; }
    public float? GetDistanceToActualBaseline(ETextBaseline baseline)=>ComputeDistanceToActualBaseline(baseline);
    public BoxConstraints Constraints()=>_constraints;
    public Sizef Size()=>_size;
    public void MarkNeedsLayout()
    {
        if(_needsLayout) return;
        _needsLayout=true;
        if(_isRelayoutBoundary||_parent==null) { _owner?.ScheduleLayout(this); return; }
        _parent?.MarkNeedsLayout();
    }
    public void MarkNeedsLayoutForSizedByParentChange() { MarkNeedsLayout(); _parent?.MarkNeedsLayout(); }
    internal bool Layout(BoxConstraints constraints,bool parentUsesSize=true)
    {
        GuiAssert.Verify(!NeedsRebuildFlush(),"VisualNode.Layout requires flushed rebuild state");
        _isRelayoutBoundary=!parentUsesSize||SizedByParent()||constraints.IsTight()||_parent==null;
        if(!_needsLayout&&constraints==_constraints&&CanReuseLayout()) return false;
        _constraints=constraints;
        PerformLayout();
        _needsLayout=false;
        return true;
    }
    protected static void LayoutChild(VisualNode? child,BoxConstraints constraints,bool parentUsesSize=true)=>child?.Layout(constraints,parentUsesSize);
    protected virtual bool SizedByParent()=>false;
    protected virtual bool CanReuseLayout()=>true;
    protected abstract void PerformLayout();
    protected abstract float ComputeMinIntrinsicWidth(float height);
    protected abstract float ComputeMaxIntrinsicWidth(float height);
    protected abstract float ComputeMinIntrinsicHeight(float width);
    protected abstract float ComputeMaxIntrinsicHeight(float width);
    protected abstract Sizef ComputeDryLayout(BoxConstraints constraints);
    protected virtual float? ComputeDryBaseline(BoxConstraints constraints,ETextBaseline baseline)=>null;
    protected virtual float? ComputeDistanceToActualBaseline(ETextBaseline baseline)=>null;
    protected void SetSize(Sizef size)=>_size=size;
    public virtual bool HitTest(VisualHitTestResult result,Offsetf position)
    {
        if(!Rectf.OffsetSize(Offsetf.Zero(),Size()).Contains(position)) return false;
        if(HitTestChildren(result,position)||HitTestSelf(position)) { result.Add(this,position); return true; }
        return false;
    }
    protected virtual bool HitTestSelf(Offsetf position)=>false;
    protected virtual bool HitTestChildren(VisualHitTestResult result,Offsetf position)=>false;
    protected bool HitTestChild(VisualHitTestResult result,VisualNode? child,Offsetf position,Offsetf? offset=null)
    { if(child==null) return false; return result.AddWithPaintOffset(offset,position,(r,p)=>child.HitTest(r,p)); }
    protected virtual void OnMount(VisualNode parent) { }
    protected virtual void OnUnmount(VisualNode parent) { }
    protected virtual void OnAttachOwner(VisualOwner owner) { }
    protected virtual void OnDetachOwner(VisualOwner owner) { }
    protected void MountChild(VisualNode child,ulong index)
    {
        GuiAssert.Require(child!=null&&child._parent==null&&child._parentIndex==ulong.MaxValue&&child._owner==null,"VisualNode.MountChild requires a detached child, not an owner root");
        child._parent=this; child._parentIndex=index; child.OnMount(this);
        if(_owner!=null) child.AttachOwnerSubtree(_owner,_visualDepth+1);
    }
    protected void UnmountChild(VisualNode child)
    {
        GuiAssert.Require(child!=null&&child._parent==this,"VisualNode.UnmountChild requires an owned child");
        child.DetachOwnerSubtree(); child.OnUnmount(this); child._parent=null; child._parentIndex=ulong.MaxValue;
    }
    protected void SyncChildIndex(VisualNode child,ulong index)
    { GuiAssert.Require(child!=null&&child._parent==this,"VisualNode.SyncChildIndex requires an owned child"); child._parentIndex=index; }
    internal void Paint(PaintContext context)
    {
        GuiAssert.Require(_owner!=null,"VisualNode.Paint must be driven by VisualOwner");
        GuiAssert.Verify(!NeedsRebuildFlush(),"VisualNode.Paint requires flushed rebuild state");
        PerformPaint(context);
    }
    protected static void PaintChild(VisualNode? child,PaintContext context)=>child?.Paint(context);
    protected static void PaintChild(VisualNode? child,PaintContext context,Offsetf offset)
    {
        if(child==null) return;
        if(offset==Offsetf.Zero()) { child.Paint(context); return; }
        var transform=new PaintTransform(); transform.ApplyOffset2D(offset);
        context.PushTransform(transform); child.Paint(context); context.PopTransform();
    }
    protected virtual void PerformPaint(PaintContext context) { }
    protected void SetChildSlot(VisualNode? child,VisualSlot? slot)=>child?.SetSlot(slot);
    private void SetSlot(VisualSlot? slot)
    {
        if(ReferenceEquals(_slot,slot)) return;
        if(_slot!=null) { GuiAssert.Require(_slot.AttachedNode==this,"Visual slot must be owned"); _slot.AttachedNode=null; }
        _slot=slot;
        if(_slot!=null) { GuiAssert.Require(_slot.AttachedNode==null||_slot.AttachedNode==this,"Visual slot must be detached"); _slot.AttachedNode=this; }
    }
    internal void AttachOwnerSubtree(VisualOwner owner,uint depth)
    {
        GuiAssert.Require(owner!=null,"Visual owner attachment requires an owner");
        bool changed=_owner!=owner;
        if(changed) { if(_owner!=null) { var oldOwner=_owner; _owner=null; OnDetachOwner(oldOwner); } _owner=owner; }
        _visualDepth=depth;
        if(changed) OnAttachOwner(owner);
        for(ulong i=0;i<NumChildren();i++) ChildAt(i).AttachOwnerSubtree(owner,depth+1);
    }
    internal void DetachOwnerSubtree()
    {
        if(_owner!=null) { var oldOwner=_owner; _owner=null; OnDetachOwner(oldOwner); }
        for(ulong i=0;i<NumChildren();i++) ChildAt(i).DetachOwnerSubtree();
    }
}

public class VisualSlot
{
    private BorrowedReference<VisualNode> _nodeBorrow;
    internal VisualNode? AttachedNode {get=>_nodeBorrow.Value;set=>_nodeBorrow.Value=value;}
    public VisualNode? Node()=>AttachedNode;
}

public abstract class VisualLeaf : VisualNode
{
    public sealed override ulong NumChildren()=>0;
    public sealed override VisualNode ChildAt(ulong index)=>throw new InvalidOperationException("VisualLeaf has no children");
}

public abstract class VisualSingleChild : VisualNode,IDisposable
{
    private VisualNode? _child;
    public VisualNode? Child()=>_child;
    public sealed override ulong NumChildren()=>_child!=null?1UL:0;
    public sealed override VisualNode ChildAt(ulong index) { GuiAssert.Require(index==0&&_child!=null,"VisualSingleChild index out of range"); return _child!; }
    public void SetChild(VisualNode? child) { ClearChild(); _child=child; if(_child!=null) MountChild(_child,0); MarkNeedsLayout(); }
    public VisualNode? RemoveChild() { var child=_child; _child=null; if(child!=null) { UnmountChild(child); SetChildSlot(child,null); MarkNeedsLayout(); } return child; }
    public void ClearChild()=>RemoveChild();
    public void Dispose()=>ClearChild();
}
