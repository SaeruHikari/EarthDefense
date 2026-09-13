from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/visual/visual_temp_text_tests.cpp')
v={};exec((repo/'tools/port_visual_tests.py').read_text().split('rows=[]')[0].replace('__file__',repr(str(repo/'tools/port_visual_tests.py'))),v)
close=v['close'];text=src.read_text(encoding='utf8')
fixture='''
private sealed class VisualTempTextFixture:IDisposable
{
 public TextServices Service;public EmbeddedTextFontProvider Provider;public string Family="";public bool IsReady;
 public VisualTempTextFixture()
 {
  Service=TextServices.CreateFallback(new TextServicesDesc{AddSystemFontProvider=false});
  Provider=new();Provider.AddFace(new(){Family="Skr Visual Test Latin",SourceKey="embedded://visual-temp-text/latin/v1",Data=TestFontAssets.Get("Latin")});
  IsReady=Service!=null&&Provider!=null;if(!IsReady)return;
  Family="Skr Visual Test Latin";Service!.AddFontProvider(Provider!);
 }
 public VisualTempText MakeText(Utf8StringView value)
 {TextStyle style=new(){FontFamilies=Family,FontSize=13};var result=new VisualTempText();result.SetTextStyle(style);result.SetText(value);return result;}
 public VisualOwner Mount(VisualTempText text,VisualTestBackend? backend=null)
 {backend??=new();var result=new VisualOwner(Service,backend);result.SetRoot(text);return result;}
 public bool Paint(VisualOwner owner,VisualTestBackend backend)
 {VisualRenderDesc desc=new(){Target=backend.MakeTarget(new Sizei(512,256))};return owner.Paint()&&owner.Render(desc);}
 public void Dispose()=>Service.Dispose();
}
private static VisualTestCommand? FindCommand(VisualTestBackend backend,EVisualTestCommand kind)
{foreach(var command in backend.Commands)if(command.Kind==kind)return command;return null;}
private static VisualTestCommand? FindDrawCommand(VisualTestBackend backend)
{foreach(var command in backend.Commands)if(command.Kind==EVisualTestCommand.Text||command.Kind==EVisualTestCommand.Mesh)return command;return null;}
private static Rectf CommandBound(VisualTestBackend backend,VisualTestCommand command)
{
 Check.That(command.Range.IndexCount>0);ulong indexEnd=(ulong)command.Range.IndexStart+command.Range.IndexCount;Check.That(indexEnd<=(ulong)backend.Indices.Length);
 uint firstIndex=backend.Indices[command.Range.IndexStart];Check.That(firstIndex<backend.Vertices.Length);
 Rectf result=Rectf.Points(backend.Vertices[firstIndex].Pos,backend.Vertices[firstIndex].Pos);
 for(ulong i=(ulong)command.Range.IndexStart+1;i<indexEnd;++i){uint vertexIndex=backend.Indices[i];Check.That(vertexIndex<backend.Vertices.Length);result=result.Unite(Rectf.Points(backend.Vertices[vertexIndex].Pos,backend.Vertices[vertexIndex].Pos));}return result;
}
'''
parts=[fixture];rows=[]
def conv(s):
 s=s.replace('u8"','"')
 s=re.sub(r'\*(backend|owner|first_owner|second_owner|visual_owner)\b',r'\1',s)
 s=s.replace('const std::array<EVisualTempTextAlign, 3> alignments{','EVisualTempTextAlign[] alignments={')
 s=s.replace('Span<const float>(&custom_stop, 1u)', 'new[]{custom_stop}')
 s=re.sub(r'second_provider->add_face\(\{[\s\S]*?\}\);', 'second_provider->add_face(new EmbeddedFontSource { Family=fixture.Family, SourceKey="EMBEDDED_SOURCE_V2",Data=TestFontAssets.Get("Latin") });',s)
 s=v['conv'](s)
 s=s.replace('constexpr ', '')
 s=s.replace('kTextDefaultEllipsisCodepoint','TextDefaults.EllipsisCodepoint')
 s=re.sub(r'(?<![\w.])(is_ready|family|service)\b',lambda m:'fixture.'+v['pascal'](m[1]),s)
 s=re.sub(r'(?<![\w.])(mount|paint)\(',lambda m:v['pascal'](m[1])+'(',s)
 for n in ['MakeText','Mount','Paint']:s=re.sub(r'(?<![\w.])'+n+r'\(', 'fixture.'+n+'(',s)
 s=s.replace('Expect_GT(', 'Greater(').replace('Expect_LT(', 'Less(')
 s=re.sub(r'VisualTestCommand (\w+) =',r'VisualTestCommand? \1 =',s)
 s=s.replace('start_command ?','start_command.HasValue ?').replace('if (start_command && end_command)', 'if (start_command.HasValue && end_command.HasValue)').replace('if (!command)','if (!command.HasValue)')
 s=s.replace('alignments.Size()', 'alignments.Length').replace('for (ulong i = 0u;', 'for (int i = 0;')
 return s.replace("EMBEDDED_SOURCE_V2","embedded://visual-temp-text/latin/v2")
for i,m in enumerate(re.finditer(r'SKR_TEST_CASE_FIXTURE\(VisualTempTextFixture, "([^"]+)"\)\s*\{',text)):
 end=close(text,m.end());parts.append('[GuiTest('+json.dumps('visual/visual_temp_text_tests.cpp::'+m[1])+')]\npublic static void Case'+str(i)+'(){using var fixture=new VisualTempTextFixture();\n'+conv(text[m.end():end-1])+'\n}')
 rows.append(dict(source='visual/visual_temp_text_tests.cpp',case=m[1],method='Case'+str(i),status='translated-unverified'))
(repo/'tests/SkrGui.Core.Tests/Visual/VisualTempTextTests.cs').write_text('using SkrGui;\nusing static SkrGui.Tests.OriginalMathHelpers;\nusing static SkrGui.Tests.OriginalWidgetFixtures;\nnamespace SkrGui.Tests;\ninternal static class VisualTempTextTests\n{\n'+'\n'.join(parts)+'\n}\n',encoding='utf8')
(repo/'.report/visual-text-source-test-mapping.json').write_text(json.dumps(rows,indent=2))
