using System.Runtime.CompilerServices;
namespace SkrGui.Tests;
// Source: all 10 cases in tests/visual/visual_owner_tests.cpp.
internal static class VisualOwnerTests
{
    private sealed class Fixture:IDisposable
    {
        public TextServices TextServices=TextServices.CreateFallback();
        public VisualTestBackend Backend=new();
        public VisualTestTarget Target;
        public Fixture()=>Target=Backend.MakeTarget(new(64,48));
        public VisualOwner MakeOwner()=>new(TextServices,Backend);
        public VisualRenderDesc RenderDesc()=>new(){Target=Target,Clear=true,ClearColor=new(.1f,.2f,.3f,1)};
        public void Dispose()=>TextServices.Dispose();
    }
    private sealed class TrackingProxy:VisualProxy
    {
        public uint MountCount,AttachOwnerCount,DetachOwnerCount;
        protected override void OnMount(VisualNode parent)=>MountCount++;
        protected override void OnAttachOwner(VisualOwner owner)=>AttachOwnerCount++;
        protected override void OnDetachOwner(VisualOwner owner)=>DetachOwnerCount++;
    }
    private sealed class PipelineLeaf:VisualLeaf
    {
        public uint PaintCount;
        protected override void PerformPaint(PaintContext context)=>PaintCount++;
        protected override void PerformLayout()=>SetSize(Constraints().Biggest());
        protected override float ComputeMinIntrinsicWidth(float height)=>0;
        protected override float ComputeMaxIntrinsicWidth(float height)=>0;
        protected override float ComputeMinIntrinsicHeight(float width)=>0;
        protected override float ComputeMaxIntrinsicHeight(float width)=>0;
        protected override Sizef ComputeDryLayout(BoxConstraints constraints)=>constraints.Biggest();
        protected override bool HitTestSelf(Offsetf position)=>true;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Alive<T>(WeakReference<T> weak)where T:class=>weak.TryGetTarget(out _);
    private static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (List<VisualOwner>,List<RenderTarget>,WeakReference<TextServices>,WeakReference<VisualBackend>) RetentionFixture()
    {
        var services=TextServices.CreateFallback();var backend=new VisualTestBackend();var target=backend.MakeTarget(new(64,48));
        var first=new VisualOwner(services,backend);var second=new VisualOwner(services,backend);Check.Same(services,first.TextServices());Check.Same(backend,second.VisualBackend());
        return (new(){first,second},new(){target},new(services),new(backend));
    }
    [GuiTest("gui/visual-owner/retains-render-environment")]
    private static void RetainsRenderEnvironment()
    {
        var (owners,targets,text,backend)=RetentionFixture();Collect();Check.That(Alive(text));Check.That(Alive(backend));
        owners.RemoveAt(0);Collect();Check.That(Alive(text));owners.Clear();targets.Clear();Collect();Check.False(Alive(text));Check.False(Alive(backend));
    }
    [GuiTest("gui/visual-owner/propagates-owner-and-depth")]
    private static void PropagatesOwnerAndDepth()
    {
        using var f=new Fixture();using var owner=f.MakeOwner();var root=new TrackingProxy();var child=new TrackingProxy();var grandchild=new TrackingProxy();
        child.SetChild(grandchild);root.SetChild(child);owner.SetRoot(root);
        Check.Same(owner,root.Owner());Check.Same(owner,child.Owner());Check.Same(owner,grandchild.Owner());Check.Equal(0u,root.VisualDepth());Check.Equal(1u,child.VisualDepth());Check.Equal(2u,grandchild.VisualDepth());
        Check.Equal(0u,root.MountCount);Check.Equal(1u,child.MountCount);owner.SetRoot(root);Check.Equal(1u,root.AttachOwnerCount);Check.Equal(1u,child.AttachOwnerCount);
    }
    [GuiTest("gui/visual-owner/replaces-removes-and-detaches-root")]
    private static void ReplacesRemovesAndDetachesRoot()
    {
        using var f=new Fixture();using var owner=f.MakeOwner();var first=new TrackingProxy();var child=new TrackingProxy();var second=new TrackingProxy();first.SetChild(child);
        owner.SetRoot(first);owner.SetRoot(second);Check.Null(first.Owner());Check.Null(child.Owner());Check.Equal(1u,first.DetachOwnerCount);Check.Same(owner,second.Owner());
        var removed=owner.RemoveRoot();Check.Same(second,removed);Check.Null(owner.Root());Check.Null(second.Owner());Check.Equal(1u,second.DetachOwnerCount);
    }
    [GuiTest("gui/visual-owner/migrates-detached-subtree")]
    private static void MigratesDetachedSubtree()
    {
        using var f=new Fixture();using var first=f.MakeOwner();using var second=f.MakeOwner();var root=new TrackingProxy();var child=new TrackingProxy();root.SetChild(child);
        first.SetRoot(root);second.SetRoot(first.RemoveRoot());Check.Same(second,root.Owner());Check.Same(second,child.Owner());Check.Equal(2u,root.AttachOwnerCount);Check.Equal(1u,root.DetachOwnerCount);Check.Equal(0u,root.VisualDepth());Check.Equal(1u,child.VisualDepth());
    }
    [GuiTest("gui/visual-owner/layouts-and-hit-tests-root")]
    private static void LayoutsAndHitTestsRoot()
    {
        using var f=new Fixture();using var owner=f.MakeOwner();var empty=new VisualHitTestResult();owner.Layout(BoxConstraints.Tight(new(32,24)));Check.False(owner.HitTest(empty,new(1,1)));
        var leaf=new PipelineLeaf();owner.SetRoot(leaf);owner.Layout(BoxConstraints.Tight(new(32,24)));Check.Equal(new Sizef(32,24),leaf.Size());
        var result=new VisualHitTestResult();Check.That(owner.HitTest(result,new(4,3)));Check.Equal(1,result.Path().Count);Check.Same(leaf,result.Path()[0].Target);
    }
    [GuiTest("gui/visual-owner/renders-complete-frame")]
    private static void RendersCompleteFrame()
    {
        using var f=new Fixture();using var owner=f.MakeOwner();var border=new VisualBorder();border.SetBackgroundColor(new(1,0,0,1));owner.SetRoot(border);owner.Layout(BoxConstraints.Tight(new(16,12)));
        Check.That(owner.Paint(new(){PixelRatio=1.5f,AaRadius=.75f}));Check.That(owner.Render(f.RenderDesc()));
        Check.Equal(1u,f.Backend.BeginCount);Check.Equal(1u,f.Backend.EndCount);Check.Same(f.Target,f.Backend.RenderTarget.Target);Check.Equal(Recti.LTWH(0,0,64,48),f.Backend.RenderTarget.Viewport);
        Check.Equal(new Sizef(16,12),f.Backend.RenderTarget.LogicSize);Check.Equal(1,f.Backend.Commands.Count);Check.Equal(EVisualTestCommand.Mesh,f.Backend.Commands[0].Kind);
        Check.That(f.Backend.Vertices.Length!=0);Check.That(f.Backend.Indices.Length!=0);Check.Equal((ulong)f.Backend.Indices.Length,f.Backend.Commands[0].Range.IndexCount);
    }
    [GuiTest("gui/visual-owner/renders-empty-root")]
    private static void RendersEmptyRoot()
    {using var f=new Fixture();using var owner=f.MakeOwner();Check.That(owner.Paint());Check.That(owner.Render(f.RenderDesc()));Check.Equal(1u,f.Backend.BeginCount);Check.Equal(0,f.Backend.Commands.Count);Check.That(f.Backend.RenderTarget.Clear);}
    [GuiTest("gui/visual-owner/requires-paint-before-render")]
    private static void RequiresPaintBeforeRender()
    {using var f=new Fixture();using var owner=f.MakeOwner();Check.False(owner.Render(f.RenderDesc()));Check.Equal(0u,f.Backend.BeginCount);}
    [GuiTest("gui/visual-owner/rejects-foreign-target")]
    private static void RejectsForeignTarget()
    {
        using var f=new Fixture();using var owner=f.MakeOwner();var foreign=new VisualTestBackend();var desc=f.RenderDesc();desc.Target=foreign.MakeTarget();
        Check.That(owner.Paint());Check.False(owner.Render(desc));Check.Equal(0u,f.Backend.BeginCount);Check.Equal(0u,foreign.BeginCount);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<RenderTarget> RetainedTarget(VisualOwner owner,VisualTestBackend backend)
    {var target=backend.MakeTarget();Check.That(owner.Paint());Check.That(owner.Render(new(){Target=target,Clear=true,ClearColor=new(.1f,.2f,.3f,1)}));return new(target);}
    [GuiTest("gui/visual-owner/frame-retains-target")]
    private static void FrameRetainsTarget()
    {
        using var services=TextServices.CreateFallback();var backend=new VisualTestBackend();using var owner=new VisualOwner(services,backend);
        var weak=RetainedTarget(owner,backend);Collect();Check.That(Alive(weak));backend.RenderTarget=new();Collect();Check.False(Alive(weak));
    }
}
