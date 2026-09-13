using System.Numerics;
using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualChildManagementTests
{
private class TestNode : VisualLeaf{
public bool ParentTreeConsistentOnUnmount = false;
public bool ParentTreeConsistentOnMount = false;
public bool InspectParentTreeOnLifecycle = false;
public uint DetachOwnerCount = 0;
public uint AttachOwnerCount = 0;
public uint UnmountCount = 0;
public uint MountCount = 0;
public List<VisualNode> PaintLog = null;
public List<VisualNode> LayoutLog = null;
public bool HitTestSelfResult = false;
public Sizef LayoutSize = new Sizef(20.0f, 20.0f);
protected override void OnMount(VisualNode parent){
        if (InspectParentTreeOnLifecycle)
        {
            ParentTreeConsistentOnMount = IsParentTreeConsistent(parent);
        }
        ++MountCount;
    }
protected override void OnUnmount(VisualNode parent){
        if (InspectParentTreeOnLifecycle)
        {
            ParentTreeConsistentOnUnmount = IsParentTreeConsistent(parent);
        }
        ++UnmountCount;
    }
protected override void OnAttachOwner(VisualOwner owner){

        ++AttachOwnerCount;
    }
protected override void OnDetachOwner(VisualOwner owner){

        ++DetachOwnerCount;
    }
protected override void PerformPaint(PaintContext context){

        if (PaintLog!=null)
        {
            PaintLog.Add(this);
        }
    }
protected override void PerformLayout(){
        if (LayoutLog!=null)
        {
            LayoutLog.Add(this);
        }
        SetSize(Constraints().Constrain(LayoutSize));
    }
protected override float ComputeMinIntrinsicWidth(float height){

        return 0.0f;
    }
protected override float ComputeMaxIntrinsicWidth(float height){

        return 0.0f;
    }
protected override float ComputeMinIntrinsicHeight(float width){

        return 0.0f;
    }
protected override float ComputeMaxIntrinsicHeight(float width){

        return 0.0f;
    }
protected override Sizef ComputeDryLayout(BoxConstraints constraints){
        return constraints.Constrain(LayoutSize);
    }
protected override bool HitTestSelf(Offsetf position){

        return HitTestSelfResult;
    }
public bool IsParentTreeConsistent(VisualNode parent){
        if (parent==null)
        {
            return false;
        }

        for (ulong index = 0; index < parent.NumChildren(); ++index)
        {
            VisualNode Child = parent.ChildAt(index);
            if (Child==null || Child.Parent() != parent || Child.ParentIndex() != index)
            {
                return false;
            }
        }
        return true;
    }
}
private static T SlotOf<T>(VisualNode node)where T:VisualSlot {Check.NotNull(node);Check.NotNull(node.Slot());return (T)node.Slot()!;}
private static void CheckHitTestTransform(Matrix4x4 transform,Offsetf position,float x,float y){var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);if(point.W!=1){point.X/=point.W;point.Y/=point.W;}CheckOffsetf(new(point.X,point.Y),x,y);}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-node/single-child-tree-api")]
public static void Case0(){

    var owner = new VisualOpacity();
    var Child = new TestNode();

    ExpectFalse(owner.HasChild());
    owner.SetChild(Child);

    VisualNode base_owner = owner;
    Expect(base_owner.HasChild());
    Equal(base_owner.NumChildren(), 1u);
    Expect(base_owner.ChildAt(0) == Child);
    ExpectFalse(base_owner.NeedsRebuildFlush());
    Expect(owner.Child() == Child);
    Expect(Child.Slot() == null);
    Expect(Child.Parent() == owner);
    Equal(Child.ParentIndex(), 0u);

    VisualOpacity const_owner = owner;
    VisualNode child_from_const_owner = const_owner.Child();
    Expect(child_from_const_owner == Child);

    VisualNode const_base_owner = owner;
    VisualNode child_from_const_base = const_base_owner.ChildAt(0);
    Expect(child_from_const_base == Child);

    VisualNode removed_child = owner.RemoveChild();
    Expect(removed_child == Child);
    ExpectFalse(base_owner.HasChild());
    Equal(base_owner.NumChildren(), 0u);
    Expect(Child.Parent() == null);
    Equal(Child.ParentIndex(), ulong.MaxValue);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-node/leaf-tree-api")]
public static void Case1(){

    var Child = new TestNode();

    VisualNode leaf = Child;
    ExpectFalse(leaf.HasChild());
    Equal(leaf.NumChildren(), 0u);
    ExpectFalse(leaf.NeedsRebuildFlush());

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-node/multi-child-tree-api")]
public static void Case2(){

    var owner = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var third = new TestNode();

    ExpectFalse(owner.HasChild());
    owner.AddChild(first);
    owner.AddChild(third);
    owner.InsertChild(1, second);

    Expect(owner.HasChild());
    ExpectFalse(owner.NeedsRebuildFlush());
    owner.RebuildFlush();
    ExpectFalse(owner.NeedsRebuildFlush());
    Equal(owner.NumChildren(), 3u);
    Expect(owner.ChildAt(0) == first);
    Expect(owner.ChildAt(1) == second);
    Expect(owner.ChildAt(2) == third);
    Expect(first.Parent() == owner);
    Expect(second.Parent() == owner);
    Expect(third.Parent() == owner);
    Equal(first.ParentIndex(), 0u);
    Equal(second.ParentIndex(), 1u);
    Equal(third.ParentIndex(), 2u);

    VisualNode base_owner = owner;
    Equal(base_owner.NumChildren(), 3u);
    Expect(base_owner.ChildAt(1) == second);

    VisualNode removed_child = owner.RemoveChildAt(1);
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(removed_child == second);
    Expect(second.Parent() == null);
    Equal(third.ParentIndex(), 1u);

    Expect(owner.RemoveChild(third));
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(third.Parent() == null);
    Equal(owner.NumChildren(), 1u);

    owner.ClearChildren();
    ExpectFalse(owner.HasChild());
    Expect(owner.IsEmpty());
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(first.Parent() == null);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-node/owner-destruction-clears-child-state")]
public static void Case3(){

    var single_child = new TestNode();
    {
        using var owner = new VisualOpacity();
        owner.SetChild(single_child);
    }
    Expect(single_child.Parent() == null);
    Equal(single_child.ParentIndex(), ulong.MaxValue);
    Equal(single_child.UnmountCount, 1u);

    var multi_first = new TestNode();
    var multi_second = new TestNode();
    {
        using var owner = new VisualStack();
        owner.AddChild(multi_first);
        owner.AddChild(multi_second);
        owner.RebuildMoveChild(multi_first, 0, 1);
        Expect(owner.NeedsRebuildFlush());
        Expect(multi_first.Slot() != null);
        Expect(multi_second.Slot() != null);
    }
    Expect(multi_first.Parent() == null);
    Expect(multi_second.Parent() == null);
    Equal(multi_first.ParentIndex(), ulong.MaxValue);
    Equal(multi_second.ParentIndex(), ulong.MaxValue);
    Equal(multi_first.UnmountCount, 1u);
    Equal(multi_second.UnmountCount, 1u);
    Expect(multi_first.Slot() == null);
    Expect(multi_second.Slot() == null);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/rebuild-aligned-mutations-require-flush")]
public static void Case4(){

    var owner = new VisualStack();
    var Child = new TestNode();

    owner.RebuildAddChild(Child, 0);
    Expect(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == Child);
    Expect(Child.Parent() == owner);
    Equal(Child.ParentIndex(), 0u);
    Expect(Child.Slot() != null);

    owner.RebuildFlush();
    ExpectFalse(owner.NeedsRebuildFlush());

    owner.RebuildMoveChild(Child, 0, 0);
    Expect(owner.NeedsRebuildFlush());
    owner.RebuildFlush();
    ExpectFalse(owner.NeedsRebuildFlush());

    owner.RebuildRemoveChild(Child, 0);
    Expect(owner.NeedsRebuildFlush());
    Expect(owner.IsEmpty());
    Expect(Child.Parent() == null);
    Equal(Child.ParentIndex(), ulong.MaxValue);
    Equal(Child.UnmountCount, 1u);
    Expect(Child.Slot() == null);

    owner.RebuildFlush();
    ExpectFalse(owner.NeedsRebuildFlush());

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/rebuild-remove-swaps-physical-tail")]
public static void Case5(){

    var owner = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var third = new TestNode();
    var fourth = new TestNode();

    owner.AddChild(first);
    owner.AddChild(second);
    owner.AddChild(third);
    owner.AddChild(fourth);

    owner.RebuildRemoveChild(second, 1);

    Expect(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == first);
    Expect(owner.ChildAt(1) == fourth);
    Expect(owner.ChildAt(2) == third);
    Equal(first.ParentIndex(), 0u);
    Equal(fourth.ParentIndex(), 1u);
    Equal(third.ParentIndex(), 2u);
    Expect(second.Parent() == null);
    Equal(second.ParentIndex(), ulong.MaxValue);
    Expect(second.Slot() == null);

    owner.RebuildMoveChild(fourth, 3, 2);
    owner.RebuildMoveChild(third, 2, 1);
    owner.RebuildFlush();

    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == first);
    Expect(owner.ChildAt(1) == third);
    Expect(owner.ChildAt(2) == fourth);
    Equal(first.ParentIndex(), 0u);
    Equal(third.ParentIndex(), 1u);
    Equal(fourth.ParentIndex(), 2u);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/rebuild-move-and-flush")]
public static void Case6(){

    var owner = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var third = new TestNode();
    var fourth = new TestNode();

    owner.AddChild(first);
    owner.AddChild(second);
    owner.AddChild(third);
    owner.AddChild(fourth);

    owner.RebuildMoveChild(first, 0, 0);
    Expect(owner.NeedsRebuildFlush());

    owner.RebuildMoveChild(first, 0, 3);
    owner.RebuildMoveChild(first, 3, 0);
    Expect(owner.NeedsRebuildFlush());
    owner.RebuildMoveChild(first, 0, 2);
    owner.RebuildMoveChild(second, 1, 0);
    owner.RebuildMoveChild(third, 2, 3);
    owner.RebuildMoveChild(fourth, 3, 1);

    VisualNode base_owner = owner;
    Expect(base_owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == first);
    Expect(owner.ChildAt(1) == second);
    Expect(owner.ChildAt(2) == third);
    Expect(owner.ChildAt(3) == fourth);
    Equal(first.ParentIndex(), 0u);
    Equal(second.ParentIndex(), 1u);
    Equal(third.ParentIndex(), 2u);
    Equal(fourth.ParentIndex(), 3u);

    owner.RebuildFlush();

    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == second);
    Expect(owner.ChildAt(1) == fourth);
    Expect(owner.ChildAt(2) == first);
    Expect(owner.ChildAt(3) == third);
    Equal(second.ParentIndex(), 0u);
    Equal(fourth.ParentIndex(), 1u);
    Equal(first.ParentIndex(), 2u);
    Equal(third.ParentIndex(), 3u);
    Equal(first.MountCount, 1u);
    Equal(second.MountCount, 1u);
    Equal(third.MountCount, 1u);
    Equal(fourth.MountCount, 1u);
    Equal(first.UnmountCount, 0u);
    Equal(second.UnmountCount, 0u);
    Equal(third.UnmountCount, 0u);
    Equal(fourth.UnmountCount, 0u);

    owner.RebuildMoveChild(second, 0, 0);
    owner.RebuildMoveChild(fourth, 1, 1);
    owner.RebuildMoveChild(first, 2, 2);
    owner.RebuildMoveChild(third, 3, 3);
    Expect(owner.NeedsRebuildFlush());

    owner.RebuildFlush();
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == second);
    Expect(owner.ChildAt(1) == fourth);
    Expect(owner.ChildAt(2) == first);
    Expect(owner.ChildAt(3) == third);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/rebuild-add-remove-and-move")]
public static void Case7(){

    var visual_owner = VisualTestHelpers.MakeVisualOwner();
    var owner = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var third = new TestNode();
    var fourth = new TestNode();

    second.InspectParentTreeOnLifecycle = true;
    visual_owner.SetRoot(owner);
    owner.AddChild(first);
    owner.AddChild(second);
    owner.AddChild(third);
    Expect(second.ParentTreeConsistentOnMount);
    Equal(first.AttachOwnerCount, 1u);
    Equal(second.AttachOwnerCount, 1u);
    Equal(third.AttachOwnerCount, 1u);
    VisualSlot first_slot = first.Slot();
    VisualSlot third_slot = third.Slot();

    owner.RebuildMoveChild(third, 2, 1);
    owner.RebuildMoveChild(third, 1, 0);
    owner.RebuildMoveChild(first, 0, 2);
    owner.RebuildMoveChild(second, 1, 3);
    owner.RebuildRemoveChild(second, 3);
    Equal(third.ParentIndex(), 1u);
    owner.RebuildMoveChild(third, 0, 0);
    owner.RebuildAddChild(fourth, 1);
    VisualSlot fourth_slot = fourth.Slot();

    Expect(owner.NeedsRebuildFlush());
    Expect(second.Parent() == null);
    Equal(second.ParentIndex(), ulong.MaxValue);
    Equal(second.UnmountCount, 1u);
    Expect(second.ParentTreeConsistentOnUnmount);
    Equal(second.DetachOwnerCount, 1u);
    Expect(second.Slot() == null);
    Equal(fourth.AttachOwnerCount, 1u);
    Expect(fourth_slot != null);
    Expect(owner.ChildAt(0) == first);
    Expect(owner.ChildAt(1) == third);
    Expect(owner.ChildAt(2) == fourth);
    for (ulong index = 0; index < owner.NumChildren(); ++index)
    {
        Equal(owner.ChildAt(index).ParentIndex(), index);
    }

    owner.RebuildFlush();

    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == third);
    Expect(owner.ChildAt(1) == fourth);
    Expect(owner.ChildAt(2) == first);
    for (ulong index = 0; index < owner.NumChildren(); ++index)
    {
        TestNode Child = ((TestNode)(owner.ChildAt(index)));
        Equal(Child.ParentIndex(), index);
        Equal(Child.MountCount, 1u);
        Equal(Child.UnmountCount, 0u);
    }
    Expect(first.Slot() == first_slot);
    Expect(third.Slot() == third_slot);
    Expect(fourth.Slot() == fourth_slot);
    Equal(first.AttachOwnerCount, 1u);
    Equal(third.AttachOwnerCount, 1u);
    Equal(fourth.AttachOwnerCount, 1u);
    Equal(first.DetachOwnerCount, 0u);
    Equal(third.DetachOwnerCount, 0u);
    Equal(fourth.DetachOwnerCount, 0u);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/rebuild-clear-resets-state")]
public static void Case8(){

    var owner = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var fresh = new TestNode();

    owner.AddChild(first);
    owner.AddChild(second);
    owner.RebuildMoveChild(first, 0, 1);
    Expect(owner.NeedsRebuildFlush());
    owner.ClearChildren();

    Expect(owner.IsEmpty());
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(first.Parent() == null);
    Expect(second.Parent() == null);
    Equal(first.ParentIndex(), ulong.MaxValue);
    Equal(second.ParentIndex(), ulong.MaxValue);
    Expect(first.Slot() == null);
    Expect(second.Slot() == null);

    owner.AddChild(fresh);
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == fresh);
    Expect(fresh.Parent() == owner);
    Equal(fresh.ParentIndex(), 0u);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/mounted-structural-mutations")]
public static void Case9(){

    var visual_owner = VisualTestHelpers.MakeVisualOwner();
    var owner = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var third = new TestNode();

    second.InspectParentTreeOnLifecycle = true;
    visual_owner.SetRoot(owner);
    owner.AddChild(first);
    owner.AddChild(third);
    owner.InsertChild(1, second);
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(second.ParentTreeConsistentOnMount);

    Expect(first.Parent() == owner);
    Expect(second.Parent() == owner);
    Expect(third.Parent() == owner);
    Equal(first.ParentIndex(), 0u);
    Equal(second.ParentIndex(), 1u);
    Equal(third.ParentIndex(), 2u);
    Equal(first.AttachOwnerCount, 1u);
    Equal(second.AttachOwnerCount, 1u);
    Equal(third.AttachOwnerCount, 1u);
    Expect(first.Slot() != null);
    Expect(second.Slot() != null);
    Expect(third.Slot() != null);

    VisualNode removed = owner.RemoveChildAt(1);
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(removed == second);
    Expect(second.Parent() == null);
    Equal(second.UnmountCount, 1u);
    Expect(second.ParentTreeConsistentOnUnmount);
    Equal(second.DetachOwnerCount, 1u);
    Expect(second.Slot() == null);
    Equal(third.ParentIndex(), 1u);
    Equal(third.MountCount, 1u);
    Equal(third.AttachOwnerCount, 1u);

    owner.ClearChildren();
    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(first.Parent() == null);
    Expect(third.Parent() == null);
    Equal(first.UnmountCount, 1u);
    Equal(third.UnmountCount, 1u);
    Equal(first.DetachOwnerCount, 1u);
    Equal(third.DetachOwnerCount, 1u);
    Expect(first.Slot() == null);
    Expect(third.Slot() == null);

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/rebuild-invalid-permutation-recovers")]
public static void Case10(){

    var owner = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var third = new TestNode();
    var fourth = new TestNode();

    owner.AddChild(first);
    owner.AddChild(second);
    owner.AddChild(third);
    owner.AddChild(fourth);

    owner.RebuildMoveChild(first, 0, 5);
    owner.RebuildMoveChild(second, 1, 1);
    owner.RebuildMoveChild(third, 2, 1);
    owner.RebuildMoveChild(fourth, 3, 99);
    owner.RebuildFlush();

    ExpectFalse(owner.NeedsRebuildFlush());
    Expect(owner.ChildAt(0) == second);
    Expect(owner.ChildAt(1) == third);
    Expect(owner.ChildAt(2) == first);
    Expect(owner.ChildAt(3) == fourth);
    for (ulong index = 0; index < owner.NumChildren(); ++index)
    {
        Equal(owner.ChildAt(index).ParentIndex(), index);
        owner.RebuildMoveChild(owner.ChildAt(index), index, index);
    }
    Expect(owner.NeedsRebuildFlush());
    owner.RebuildFlush();
    ExpectFalse(owner.NeedsRebuildFlush());

}
[GuiTest("visual/visual_child_management_tests.cpp::gui/visual-multi-child/rebuild-visual-integration")]
public static void Case11(){

    VisualTestBackend backend = null;
    var visual_owner = VisualTestHelpers.MakeVisualOwner(out backend);
    var stack = new VisualStack();
    var first = new TestNode();
    var second = new TestNode();
    var third = new TestNode();
    List<VisualNode> LayoutLog = new();
    List<VisualNode> PaintLog = new();

    first.HitTestSelfResult = true;
    second.HitTestSelfResult = true;
    third.HitTestSelfResult = true;
    first.LayoutLog = LayoutLog;
    second.LayoutLog = LayoutLog;
    third.LayoutLog = LayoutLog;
    first.PaintLog = PaintLog;
    second.PaintLog = PaintLog;
    third.PaintLog = PaintLog;

    stack.AddChild(first);
    stack.AddChild(second);
    stack.AddChild(third);
    VisualSlot first_slot = first.Slot();
    VisualSlot second_slot = second.Slot();
    VisualSlot third_slot = third.Slot();
    visual_owner.SetRoot(stack);

    stack.RebuildMoveChild(first, 0, 2);
    stack.RebuildMoveChild(second, 1, 0);
    stack.RebuildMoveChild(third, 2, 1);
    Expect(stack.NeedsRebuildFlush());
    stack.RebuildFlush();

    ExpectFalse(stack.NeedsRebuildFlush());
    Expect(stack.ChildAt(0) == second);
    Expect(stack.ChildAt(1) == third);
    Expect(stack.ChildAt(2) == first);
    Expect(first.Slot() == first_slot);
    Expect(second.Slot() == second_slot);
    Expect(third.Slot() == third_slot);
    Equal(first.MountCount, 1u);
    Equal(second.MountCount, 1u);
    Equal(third.MountCount, 1u);
    Equal(first.UnmountCount, 0u);
    Equal(second.UnmountCount, 0u);
    Equal(third.UnmountCount, 0u);
    Equal(first.AttachOwnerCount, 1u);
    Equal(second.AttachOwnerCount, 1u);
    Equal(third.AttachOwnerCount, 1u);
    Equal(first.DetachOwnerCount, 0u);
    Equal(second.DetachOwnerCount, 0u);
    Equal(third.DetachOwnerCount, 0u);

    visual_owner.Layout(BoxConstraints.Tight(new Sizef(20.0f, 20.0f)));
    Equal(LayoutLog.Size(), 3u);
    Expect(LayoutLog[0] == second);
    Expect(LayoutLog[1] == third);
    Expect(LayoutLog[2] == first);

    VisualHitTestResult result = new();
    Expect(stack.HitTest(result, new Offsetf(5.0f, 5.0f)));
    Equal(result.NumEntries(), 2u);
    Expect(result.Path()[0].Target == first);
    Expect(result.Path()[1].Target == stack);

    Expect(visual_owner.Paint());
    Expect(visual_owner.Render(VisualTestHelpers.MakeRenderDesc(backend)));
    Equal(PaintLog.Size(), 3u);
    Expect(PaintLog[0] == second);
    Expect(PaintLog[1] == third);
    Expect(PaintLog[2] == first);

    visual_owner.ClearRoot();
    Expect(first.Parent() == stack);
    Expect(second.Parent() == stack);
    Expect(third.Parent() == stack);
    Expect(first.Slot() == first_slot);
    Expect(second.Slot() == second_slot);
    Expect(third.Slot() == third_slot);
    Equal(first.DetachOwnerCount, 1u);
    Equal(second.DetachOwnerCount, 1u);
    Equal(third.DetachOwnerCount, 1u);

}
}
