using SkrGui;
namespace SkrGui.Tests;
public static class BatchAndPathTests
{
    private sealed class TestTexture : Texture { public override Sizei PixelSize() => new(1, 1); }
    private sealed class TestShader : Shader { }
    private sealed class TestRule : BatchRule
    {
        public override bool Matches(BatchCmd command) => command is BatchCmdDrawMesh;
        public override bool CanBatch(BatchCmd lhs, BatchCmd rhs) => lhs is BatchCmdDrawMesh a && rhs is BatchCmdDrawMesh b && ReferenceEquals(a.Shader, b.Shader);
        public override ulong SortKey(BatchCmd command) => 0;
    }
    private static BatchCmdDrawMesh Draw(Texture texture, Shader shader, Rectf bounds) => new() { Texture = texture, Shader = shader, Bound = bounds, Mesh = new Mesh() };
    private static BatchCmdText Text(uint atlas, float x, object source) => new() { AtlasIndex = atlas, PixelMode = ETextPixelMode.Sdf, Bound = Rectf.LTWH(x, 0, 20, 20), Source = source, Mesh = new Mesh() };
    [GuiTest("extra/smoke/BatchAndPathTests/1")]
    public static void Adjacent()
    {
        using var a = new TestTexture(); using var b = new TestTexture(); using var shader = new TestShader(); var root = new BatchRoot();
        root.Cmds.Add(Draw(a, shader, Rectf.LTWH(0, 0, 10, 10))); root.Cmds.Add(Draw(a, shader, Rectf.LTWH(12, 0, 10, 10))); root.Cmds.Add(Draw(b, shader, Rectf.LTWH(24, 0, 10, 10)));
        root.NeighbourBatch(); Check.Equal(2, root.Batches.Count); Check.Equal(EBatchRegionKind.Mesh, root.Batches[0].Kind); Check.Equal(2UL, root.Batches[0].SortedCmdCount); Check.Equal(1UL, root.Batches[1].SortedCmdCount);
    }
    [GuiTest("extra/smoke/BatchAndPathTests/2")]
    public static void ClipIsolation()
    {
        var root = new BatchRoot(); root.Cmds.Add(new BatchCmdClipRect { Bound = Rectf.LTWH(0, 0, 10, 10) }); root.Cmds.Add(new BatchCmdClipRect { Bound = Rectf.LTWH(12, 0, 10, 10) });
        root.NeighbourBatch(); Check.Equal(2, root.Batches.Count); Check.Equal(EBatchRegionKind.ClipRect, root.Batches[0].Kind); Check.Equal(EBatchRegionKind.ClipRect, root.Batches[1].Kind);
    }
    [GuiTest("extra/smoke/BatchAndPathTests/3")]
    public static void TextIdentity()
    {
        var sourceA = new object(); var sourceB = new object();
        var same = new BatchRoot(); same.Cmds.Add(Text(0, 0, sourceA)); same.Cmds.Add(Text(1, 8, sourceA)); same.Cmds.Add(Text(0, 16, sourceA)); same.ResortBatch();
        Check.Equal(2, same.Batches.Count); Check.Equal(0UL, same.SortedCmds[0].CommandIndex); Check.Equal(2UL, same.SortedCmds[1].CommandIndex);
        var different = new BatchRoot(); different.Cmds.Add(Text(0, 0, sourceA)); different.Cmds.Add(Text(1, 8, sourceB)); different.Cmds.Add(Text(0, 16, sourceA)); different.ResortBatch(); Check.Equal(3, different.Batches.Count);
    }
    [GuiTest("extra/smoke/BatchAndPathTests/4")]
    public static void NonOverlap()
    {
        using var a = new TestTexture(); using var b = new TestTexture(); using var shader = new TestShader(); var root = new BatchRoot();
        root.Cmds.Add(Draw(a, shader, Rectf.LTWH(0, 0, 8, 8))); root.Cmds.Add(Draw(b, shader, Rectf.LTWH(20, 0, 8, 8))); root.Cmds.Add(Draw(a, shader, Rectf.LTWH(40, 0, 8, 8)));
        root.GroupBatch(2); Check.Equal(2, root.Batches.Count); Check.Equal(0UL, root.SortedCmds[0].CommandIndex); Check.Equal(2UL, root.SortedCmds[1].CommandIndex);
    }
    [GuiTest("extra/smoke/BatchAndPathTests/5")]
    public static void Overlap()
    {
        using var a = new TestTexture(); using var b = new TestTexture(); using var shader = new TestShader(); var root = new BatchRoot();
        root.Cmds.Add(Draw(a, shader, Rectf.LTWH(0, 0, 8, 8))); root.Cmds.Add(Draw(b, shader, Rectf.LTWH(18, 0, 12, 8))); root.Cmds.Add(Draw(a, shader, Rectf.LTWH(24, 0, 8, 8)));
        root.GroupBatch(2); Check.Equal(3, root.Batches.Count); Check.SequenceEqual(new ulong[] { 0, 1, 2 }, root.SortedCmds.Select(c => c.CommandIndex));
    }
    [GuiTest("extra/smoke/BatchAndPathTests/6")]
    public static void ExternalRule()
    {
        using var a = new TestTexture(); using var b = new TestTexture(); using var shader = new TestShader(); var root = new BatchRoot();
        root.Cmds.Add(Draw(a, shader, Rectf.LTWH(0, 0, 8, 8))); root.Cmds.Add(Draw(b, shader, Rectf.LTWH(12, 0, 8, 8)));
        root.NeighbourBatch(new[] { new TestRule() }); Check.Equal(1, root.Batches.Count); Check.Equal(2UL, root.Batches[0].SortedCmdCount);
    }
    [GuiTest("extra/smoke/BatchAndPathTests/7")]
    public static void WindingHint()
    {
        var path = new VGPath(); path.AddRect(Rectf.LTWH(0, 0, 10, 20), EVGPathWinding.CCW);
        Check.Equal(EVGPathCommandState.Rest, path.State()); Check.Null(path.CursorPos()); Check.Equal(EVGPathWinding.CCW, path.Commands()[^1].Closing.WindingHint!.Value);
        var flat = path.Flatten(new VGPathFlattenOptions()); Check.Equal(4, flat.Nodes().Count); Check.Equal(1, flat.Contours().Count); Check.That(flat.IsFinalized()); Check.That(flat.Contours()[0].Closed); Check.Near(60, flat.Contours()[0].TotalLength);
    }
    [GuiTest("extra/smoke/BatchAndPathTests/8")]
    public static void ClipRoots()
    {
        var context = new PaintContext(); var first = context.PushClipRect(Rectf.LTWH(0, 0, 10, 10)); Check.NotNull(first);
        var mesh = new Mesh(); var cmd = context.DrawMesh(mesh, null, null, Rectf.LTWH(0, 0, 20, 20)); Check.NotNull(cmd); Check.Equal(Rectf.LTWH(0, 0, 10, 10), cmd!.Bound);
        context.PushClipRect(Rectf.LTWH(30, 30, 10, 10)); Check.Null(context.DrawMesh(mesh, null, null, Rectf.LTWH(0, 0, 20, 20))); context.PopClip(); context.PopClip();
        Check.Equal(1, context.BatchRoot()!.Cmds.Count); Check.Equal(1, first!.SubRoot!.Cmds.Count);
    }
}
