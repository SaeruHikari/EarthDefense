from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[1];env={'__file__':str(ROOT/'tools/port_batch_paint_gradient_tests.py')};exec((ROOT/'tools/port_batch_paint_gradient_tests.py').read_text(encoding='utf-8-sig').split("prefix='''")[0],env)
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/batch/batch_cmd_tests.cpp').read_text(encoding='utf-8');tr=env['tr'];block=env['body']
out='''using System.Runtime.CompilerServices;
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
''';mapping=[]
for i,m in enumerate(re.finditer(r'SKR_TEST_CASE\("([^\"]+)"\)\s*\{',src)):
 s=block(src,m.end()-1);s=s.replace('RC<TextServices>','TextServices').replace('Vector<RC<BatchRule>>','List<BatchRule>').replace('Span<const RC<BatchRule>>(rules.data(), rules.size())','rules');s=tr(s).replace('.Get()','').replace('BatchRoot root = {};','BatchRoot root = new();');out+=' [GuiTest("'+m[1]+'")] public static void Case'+str(i+1)+'()\n'+s+'\n';mapping.append({'source':'tests/batch/batch_cmd_tests.cpp','line':src[:m.start()].count('\n')+1,'case':m[1],'target':'BatchCommandContractTests.Case'+str(i+1)})
out+='}\n';(ROOT/'tests/SkrGui.Core.Tests/Batch/BatchCommandContractTests.cs').write_text(out);(ROOT/'migration/batch-command-test-map.json').write_text(json.dumps(mapping,indent=2));print('six complete original cases')
