namespace SkrGui.Tests;
// Source: tests/visual/visual_test_backend.hpp.
internal sealed class VisualTestTarget:RenderTarget
{
    internal VisualBackend Backend=null!;internal Sizei Size=new(64,64);
    public override VisualBackend VisualBackend()=>Backend;
    public override Texture? Texture()=>null;
    public override Sizei PixelSize()=>Size;
}
internal enum EVisualTestCommand:byte{BeginClip,EndClip,Mesh,Text,Atlas,Backdrop}
internal struct VisualTestCommand
{
    public EVisualTestCommand Kind;public VisualDrawRange Range;public VisualClipDraw Clip;public VisualMeshDraw Mesh;
    public VisualTextDraw Text;public VisualAtlasDraw Atlas;public VisualBackdropDraw Backdrop;
}
internal sealed class VisualTestBackend:VisualBackend
{
    public bool BeginResult=true,EndResult=true;
    public uint BeginCount,EndCount,RenderTargetCount;
    public MeshVertex[] Vertices=[];public uint[] Indices=[];
    public VisualRenderTargetDesc RenderTarget;
    public readonly List<VisualTestCommand> Commands=[];
    public override bool BeginFrame(){BeginCount++;Vertices=[];Indices=[];RenderTarget=new();Commands.Clear();return BeginResult;}
    public override void UpdateVertexBuffer(ReadOnlySpan<MeshVertex> value)=>Vertices=value.ToArray();
    public override void UpdateIndexBuffer(ReadOnlySpan<uint> value)=>Indices=value.ToArray();
    public override void BeginRenderTarget(in VisualRenderTargetDesc desc){RenderTargetCount++;RenderTarget=desc;}
    public override void BeginClip(in VisualClipDraw draw)=>Commands.Add(new(){Kind=EVisualTestCommand.BeginClip,Range=draw.Range,Clip=draw});
    public override void EndClip()=>Commands.Add(new(){Kind=EVisualTestCommand.EndClip});
    public override void DrawMesh(in VisualMeshDraw draw)=>Commands.Add(new(){Kind=EVisualTestCommand.Mesh,Range=draw.Range,Mesh=draw});
    public override void DrawText(in VisualTextDraw draw)=>Commands.Add(new(){Kind=EVisualTestCommand.Text,Range=draw.Range,Text=draw});
    public override void DrawAtlas(in VisualAtlasDraw draw)=>Commands.Add(new(){Kind=EVisualTestCommand.Atlas,Range=draw.Range,Atlas=draw});
    public override void DrawBackdrop(in VisualBackdropDraw draw)=>Commands.Add(new(){Kind=EVisualTestCommand.Backdrop,Range=draw.Range,Backdrop=draw});
    public override void EndRenderTarget(){}
    public override bool EndFrame(){EndCount++;return EndResult;}
    public VisualTestTarget MakeTarget(Sizei? size=null)=>new(){Backend=this,Size=size??new(64,64)};
}
internal static class VisualTestHelpers
{
    public static VisualOwner MakeVisualOwner(out VisualTestBackend backend,TextServices? services=null)
    {backend=new();return new(services??TextServices.CreateFallback(),backend);}
    public static VisualOwner MakeVisualOwner()=>MakeVisualOwner(out _);
    public static VisualRenderDesc MakeRenderDesc(VisualTestBackend backend,Sizei? size=null)=>new(){Target=backend.MakeTarget(size)};
    public static void LayoutNode(VisualNode node,BoxConstraints constraints){using var owner=MakeVisualOwner();owner.SetRoot(node);owner.Layout(constraints);}
    public static void LayoutNode(VisualOwner owner,BoxConstraints constraints)=>owner.Layout(constraints);
}
