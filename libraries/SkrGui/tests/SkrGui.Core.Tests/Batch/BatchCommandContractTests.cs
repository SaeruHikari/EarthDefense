using System.Runtime.CompilerServices;
namespace SkrGui.Tests;
// Complete original six batch_cmd_tests.cpp cases, including every source assertion.
public static class BatchCommandContractTests
{
 private sealed class BatchTestTexture:Texture{public override Sizei PixelSize()=>new(1,1);}
 private sealed class BatchTestShader:Shader{}
 private sealed class BatchTestRule:BatchRule
 {
  public override bool Matches(BatchCmd c)=>c is BatchCmdDrawMesh;
  public override bool CanBatch(BatchCmd a,BatchCmd b)=>a is BatchCmdDrawMesh aa&&b is BatchCmdDrawMesh bb&&ReferenceEquals(aa.Shader,bb.Shader);
  public override ulong SortKey(BatchCmd c)=>c is BatchCmdDrawMesh m&&m.Shader!=null?(ulong)(uint)RuntimeHelpers.GetHashCode(m.Shader):0;
 }
 static Mesh make_batch_test_mesh(){var m=new Mesh();for(int i=0;i<3;i++)m.Vertices.Add(new());m.Indices.AddRange(new uint[]{0,1,2});return m;}
 static BatchCmdDrawMesh make_draw_cmd(Texture t,Shader s,Rectf b)=>new(){Texture=t,Shader=s,Bound=b,Mesh=make_batch_test_mesh()};
 static BatchCmdText make_text_cmd(uint atlas,Rectf b,object source)=>new(){AtlasIndex=atlas,PixelMode=ETextPixelMode.Sdf,Bound=b,Mesh=make_batch_test_mesh(),Source=source};
 static bool batch_contains_commands(BatchRoot root,ulong bi,ulong a,ulong b)
 {
  if(bi>=(ulong)root.Batches.Count)return false;bool hasA=false,hasB=false;var batch=root.Batches[(int)bi];ulong end=batch.SortedCmdStart+batch.SortedCmdCount;
  for(ulong i=batch.SortedCmdStart;i<end;i++){hasA|=root.SortedCmds[(int)i].CommandIndex==a;hasB|=root.SortedCmds[(int)i].CommandIndex==b;}return hasA&&hasB;
 }
 static void Eq<T>(T a,T b)=>Check.Equal(a,b);static void Eq(int a,uint b)=>Check.Equal((long)a,(long)b);static void Eq(ulong a,uint b)=>Check.Equal(a,(ulong)b);static void True(bool v)=>Check.That(v);
 [GuiTest("gui/batch/adjacent compatible meshes share one batch")] public static void Case1()
{
    var texture_a = new BatchTestTexture();
    var texture_b = new BatchTestTexture();
    var shader = new BatchTestShader();

    BatchRoot root = new();
    root.Cmds.Add(make_draw_cmd(texture_a, shader, Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f)));
    root.Cmds.Add(make_draw_cmd(texture_a, shader, Rectf.LTWH(12.0f, 0.0f, 10.0f, 10.0f)));
    root.Cmds.Add(make_draw_cmd(texture_b, shader, Rectf.LTWH(24.0f, 0.0f, 10.0f, 10.0f)));

    root.NeighbourBatch();

    Eq(root.Batches.Count, 2u);
    Eq(root.Batches[0].Kind, EBatchRegionKind.Mesh);
    Eq(root.Batches[0].SortedCmdCount, 2u);
    Eq(root.Batches[1].SortedCmdCount, 1u);
}
 [GuiTest("gui/batch/clip commands remain isolated")] public static void Case2()
{
    BatchRoot root = new();
    var first = new BatchCmdClipRect();
    var second = new BatchCmdClipRect();
    first.Bound = Rectf.LTWH(0.0f, 0.0f, 10.0f, 10.0f);
    second.Bound = Rectf.LTWH(12.0f, 0.0f, 10.0f, 10.0f);
    root.Cmds.Add(first);
    root.Cmds.Add(second);

    root.NeighbourBatch();

    Eq(root.Batches.Count, 2u);
    Eq(root.Batches[0].Kind, EBatchRegionKind.ClipRect);
    Eq(root.Batches[1].Kind, EBatchRegionKind.ClipRect);
}
 [GuiTest("gui/batch/text from one layout may cross within that layout")] public static void Case3()
{
    TextServices services = TextServices.CreateFallback();
    var source_a = services.CreateLine();
    var source_b = services.CreateLine();

    BatchRoot same_source = new();
    same_source.Cmds.Add(make_text_cmd(
        0u,
        Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f),
        source_a
    ));
    same_source.Cmds.Add(make_text_cmd(
        1u,
        Rectf.LTWH(8.0f, 0.0f, 20.0f, 20.0f),
        source_a
    ));
    same_source.Cmds.Add(make_text_cmd(
        0u,
        Rectf.LTWH(16.0f, 0.0f, 20.0f, 20.0f),
        source_a
    ));
    same_source.ResortBatch();
    Eq(same_source.Batches.Count, 2u);
    True(batch_contains_commands(same_source, 0u, 0u, 2u));

    BatchRoot different_source = new();
    different_source.Cmds.Add(make_text_cmd(
        0u,
        Rectf.LTWH(0.0f, 0.0f, 20.0f, 20.0f),
        source_a
    ));
    different_source.Cmds.Add(make_text_cmd(
        1u,
        Rectf.LTWH(8.0f, 0.0f, 20.0f, 20.0f),
        source_b
    ));
    different_source.Cmds.Add(make_text_cmd(
        0u,
        Rectf.LTWH(16.0f, 0.0f, 20.0f, 20.0f),
        source_a
    ));
    different_source.ResortBatch();
    Eq(different_source.Batches.Count, 3u);
}
 [GuiTest("gui/batch/lookahead combines compatible non-overlapping meshes")] public static void Case4()
{
    var texture_a = new BatchTestTexture();
    var texture_b = new BatchTestTexture();
    var shader = new BatchTestShader();

    BatchRoot root = new();
    root.Cmds.Add(make_draw_cmd(texture_a, shader, Rectf.LTWH(0.0f, 0.0f, 8.0f, 8.0f)));
    root.Cmds.Add(make_draw_cmd(texture_b, shader, Rectf.LTWH(20.0f, 0.0f, 8.0f, 8.0f)));
    root.Cmds.Add(make_draw_cmd(texture_a, shader, Rectf.LTWH(40.0f, 0.0f, 8.0f, 8.0f)));

    root.GroupBatch(2u);

    Eq(root.Batches.Count, 2u);
    True(batch_contains_commands(root, 0u, 0u, 2u));
}
 [GuiTest("gui/batch/lookahead preserves overlapping draw order")] public static void Case5()
{
    var texture_a = new BatchTestTexture();
    var texture_b = new BatchTestTexture();
    var shader = new BatchTestShader();

    BatchRoot root = new();
    root.Cmds.Add(make_draw_cmd(texture_a, shader, Rectf.LTWH(0.0f, 0.0f, 8.0f, 8.0f)));
    root.Cmds.Add(make_draw_cmd(texture_b, shader, Rectf.LTWH(18.0f, 0.0f, 12.0f, 8.0f)));
    root.Cmds.Add(make_draw_cmd(texture_a, shader, Rectf.LTWH(24.0f, 0.0f, 8.0f, 8.0f)));

    root.GroupBatch(2u);

    Eq(root.Batches.Count, 3u);
    Eq(root.SortedCmds[0].CommandIndex, 0u);
    Eq(root.SortedCmds[1].CommandIndex, 1u);
    Eq(root.SortedCmds[2].CommandIndex, 2u);
}
 [GuiTest("gui/batch/external rule reads command data")] public static void Case6()
{
    var texture_a = new BatchTestTexture();
    var texture_b = new BatchTestTexture();
    var shader = new BatchTestShader();
    var rule = new BatchTestRule();

    var first = make_draw_cmd(texture_a, shader, Rectf.LTWH(0.0f, 0.0f, 8.0f, 8.0f));
    var second = make_draw_cmd(texture_b, shader, Rectf.LTWH(12.0f, 0.0f, 8.0f, 8.0f));
    BatchRoot root = new();
    root.Cmds.Add(first);
    root.Cmds.Add(second);
    List<BatchRule> rules = new();
    rules.Add(rule);
    root.NeighbourBatch(rules);

    Eq(root.Batches.Count, 1u);
    Eq(root.Batches[0].SortedCmdCount, 2u);
}
}
