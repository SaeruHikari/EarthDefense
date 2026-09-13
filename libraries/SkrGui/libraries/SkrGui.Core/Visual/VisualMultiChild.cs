namespace SkrGui;

// Source: SkrGuiCore/visual/visual_multi_child.hpp + src/visual/visual_multi_child.cpp.
public abstract class VisualMultiChild : VisualNode,IDisposable
{
    private sealed class ChildEntry { public required VisualNode Child; public ulong RebuildIndex=ulong.MaxValue; }
    private readonly List<ChildEntry> _children=new();
    public bool IsEmpty()=>_children.Count==0;
    public sealed override ulong NumChildren()=>(ulong)_children.Count;
    public sealed override VisualNode ChildAt(ulong index) { GuiAssert.Require(index<(ulong)_children.Count,"VisualMultiChild index out of range");return _children[(int)index].Child; }
    public void AddChild(VisualNode child)
    {
        GuiAssert.Require(!_needsRebuildFlush&&child!=null,"VisualMultiChild.AddChild requires flushed state and child");
        ulong index=(ulong)_children.Count;_children.Add(new(){Child=child,RebuildIndex=index});MountChild(child,index);ApplySlot(child);MarkNeedsLayout();
    }
    public void InsertChild(ulong index,VisualNode child)
    {
        GuiAssert.Require(!_needsRebuildFlush&&child!=null&&index<=(ulong)_children.Count,"VisualMultiChild.InsertChild requires flushed state, child, and valid index");
        _children.Insert((int)index,new(){Child=child,RebuildIndex=index});
        for(ulong i=index+1;i<(ulong)_children.Count;i++) { var entry=_children[(int)i];SyncChildIndex(entry.Child,i);entry.RebuildIndex=i; }
        MountChild(child,index);ApplySlot(child);MarkNeedsLayout();
    }
    public VisualNode RemoveChildAt(ulong index)
    {
        GuiAssert.Require(!_needsRebuildFlush&&index<(ulong)_children.Count,"VisualMultiChild.RemoveChildAt requires flushed state and valid index");
        var child=_children[(int)index].Child;UnmountChild(child);SetChildSlot(child,null);_children.RemoveAt((int)index);
        for(ulong i=index;i<(ulong)_children.Count;i++) { var entry=_children[(int)i];SyncChildIndex(entry.Child,i);entry.RebuildIndex=i; }
        MarkNeedsLayout();return child;
    }
    public bool RemoveChild(VisualNode child) { GuiAssert.Require(!_needsRebuildFlush&&child!=null,"VisualMultiChild.RemoveChild requires flushed state and child");if(child.Parent()==this) { RemoveChildAt(child.ParentIndex());return true; }return false; }
    public void ClearChildren() { foreach(var entry in _children) { UnmountChild(entry.Child);SetChildSlot(entry.Child,null); }_children.Clear();_needsRebuildFlush=false;MarkNeedsLayout(); }
    public void RebuildAddChild(VisualNode child,ulong rebuildIndex)
    { GuiAssert.Require(child!=null,"VisualMultiChild.RebuildAddChild requires child");_needsRebuildFlush=true;ulong index=(ulong)_children.Count;_children.Add(new(){Child=child,RebuildIndex=rebuildIndex});MountChild(child,index);ApplySlot(child); }
    public void RebuildRemoveChild(VisualNode child,ulong rebuildIndex)
    {
        GuiAssert.Require(child!=null,"VisualMultiChild.RebuildRemoveChild requires child");
        int index=(int)child.ParentIndex();var entry=_children[index];GuiAssert.Require(entry.RebuildIndex==rebuildIndex,"VisualMultiChild rebuild index mismatch");
        _needsRebuildFlush=true;UnmountChild(entry.Child);SetChildSlot(entry.Child,null);
        _children[index]=_children[^1];_children.RemoveAt(_children.Count-1);
        if(index<_children.Count) SyncChildIndex(_children[index].Child,(ulong)index);
    }
    public void RebuildMoveChild(VisualNode child,ulong from,ulong to)
    {
        GuiAssert.Require(child!=null,"VisualMultiChild.RebuildMoveChild requires child");var entry=_children[(int)child.ParentIndex()];GuiAssert.Require(entry.RebuildIndex==from,"VisualMultiChild source rebuild index mismatch");entry.RebuildIndex=to;_needsRebuildFlush=true;
    }
    public void RebuildFlush()
    {
        if(!_needsRebuildFlush) return;
        // LINQ OrderBy is stable, matching the original stable comparison sort.
        var ordered=_children.OrderBy(entry=>entry.RebuildIndex).ToArray();_children.Clear();_children.AddRange(ordered);
        bool consistent=true;ulong index=0;
        foreach(var entry in _children) { SyncChildIndex(entry.Child,index);consistent&=entry.RebuildIndex==index;entry.RebuildIndex=index;++index; }
        _needsRebuildFlush=false;
        GuiAssert.Verify(consistent,"VisualMultiChild.RebuildFlush recovered an invalid rebuild index permutation");
        MarkNeedsLayout();
    }
    protected virtual void ApplySlot(VisualNode child) { }
    public void Dispose()=>ClearChildren();
}
