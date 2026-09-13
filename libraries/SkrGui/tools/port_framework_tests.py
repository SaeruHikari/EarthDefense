from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/framework/widget')
out=repo/'tests/SkrGui.Core.Tests/Framework';out.mkdir(parents=True,exist_ok=True)
def pascal(x):return ''.join(w[:1].upper()+w[1:] for w in x.split('_'))
def close(s,start,left='{',right='}'):
 depth=1;i=start
 while depth:depth+=(s[i]==left)-(s[i]==right);i+=1
 return i
def dsl(s,depth=0):
 while (m:=re.search(r'\$SkrGuiWidget\((\w+)\)\s*\{',s)):
  end=close(s,m.end());body=dsl(s[m.end():end-1],depth+1).replace('$w','w'+str(depth));s=s[:m.start()]+'Gui.Widget<'+m[1]+'>(w'+str(depth)+' => {'+body+'})'+s[end:]
 return s
def conv(s):
 s=re.sub(r'//[^\n]*','',s);s=dsl(s)
 s=re.sub(r'\bconst\s+','',s)
 s=re.sub(r'RC<(\w+)>::New\(',r'new \1(',s)
 s=re.sub(r'RC<(\w+)>',r'\1',s)
 for m in reversed(list(re.finditer(r'static_cast<([^>]+)>\(',s))):
  end=close(s,m.end(),'(',')');t=m[1].replace('const ','').replace('*','');s=s[:m.start()]+'(('+t+')('+s[m.end():end-1]+'))'+s[end:]
 s=re.sub(r'([\w.]+(?:->\w+)*)->rttr_cast<(\w+)>\(\)',r'RttrCast<\2>(\1)',s)
 s=s.replace('->','.').replace('::','.').replace('.get()','').replace('nullptr','null')
 s=re.sub(r'\b(\w+)\*\s+',r'\1 ',s)
 s=re.sub(r'&(\w+)',r'\1',s)
 for a,b in [('uint64_t','ulong'),('int64_t','long'),('int32_t','int'),('auto','var')]:s=re.sub(r'\b'+a+r'\b',b,s)
 s=re.sub(r'(ComponentTreeHost|VisualTreeHost) host\(([^;]+)\);',r'using var host = new \1(\2);',s)
 s=re.sub(r'ulong (\w+slot_apply_count) = 0;',r'var \1 = new Counter();',s)
 s=re.sub(r'(\w+) = \{\};',r'\1 = new();',s)
 s=re.sub(r'\.params = \{([^}]+)\}',lambda m:'.Params = new NodeParams {'+re.sub(r'\.(\w+)',lambda n:pascal(n[1]),m[1])+'}',s)
 s=re.sub(r'\.([a-z_]\w*)',lambda m:'.'+pascal(m[1]),s)
 for name in set(re.findall(r'\b([a-z_]\w*)\s*\(',s)):
  if '_' in name:s=re.sub(r'\b'+name+r'(?=\s*\()',pascal(name),s)
 s=re.sub(r'SKR_TEST_SUBCASE\("([^"]+)"\)\s*',r'// source subcase: \1\n',s)
 for a,b in [('SKR_TEST_CHECK_EQ','Equal'),('SKR_TEST_CHECK_FALSE','Check.False'),('SKR_TEST_CHECK','Expect')]:s=s.replace(a,b)
 return s
rows=[]
for p in src.glob('*.cpp'):
 text=p.read_text();cls=pascal(p.stem);parts=[]
 if p.stem=='widget_dsl_tests':
  parts.append('''
 private abstract class WidgetDslTestBase:Widget {public override Nexus CreateNexus()=>null!;}
 private sealed class PlainWidget:WidgetDslTestBase {public int Value;}
 private sealed class PreConstructWidget:WidgetDslTestBase,IPreConstruct {public int Phase;public bool ConfiguredAfterPre;public void PreConstruct()=>Phase=1;}
 private sealed class PostConstructWidget:WidgetDslTestBase,IPostConstruct {public int Phase;public bool ConfiguredBeforePost;public void PostConstruct(){ConfiguredBeforePost=Phase==1;Phase=2;}}
 private sealed class PrePostConstructWidget:WidgetDslTestBase,IPreConstruct,IPostConstruct {public int Phase;public bool ConfigObservedPre,PostObservedConfiguration;public void PreConstruct()=>Phase=1;public void PostConstruct(){PostObservedConfiguration=Phase==2;Phase=3;}}
 private sealed class ParentWidget:WidgetDslTestBase {public Widget? Child;public int Value;}
 private struct NodeParams {public int Id,Inset;public bool Enabled=true;public NodeParams(){}}
 private sealed class ParameterizedLeafWidget:WidgetDslTestBase {public NodeParams Params=new();public int Value;}
 private sealed class InnerBranchWidget:WidgetDslTestBase {public NodeParams Params=new();public ParameterizedLeafWidget? First,Second;}
 private sealed class ParameterizedBranchWidget:WidgetDslTestBase {public NodeParams Params=new();public ParameterizedLeafWidget? Leading;public InnerBranchWidget? Nested;}
 private sealed class ParameterizedRootWidget:WidgetDslTestBase {public NodeParams Params=new();public ParameterizedLeafWidget? Header;public ParameterizedBranchWidget? Content;}
''')
 for index,m in enumerate(re.finditer(r'SKR_TEST_CASE\("([^"]+)"\)\s*\{',text)):
  end=close(text,m.end());parts.append('[GuiTest('+json.dumps('framework/widget/'+p.name+'::'+m[1])+')]\npublic static void Case'+str(index)+'(){\n'+conv(text[m.end():end-1])+'\n}')
  rows.append(dict(source='framework/widget/'+p.name,case=m[1],target=cls+'.cs',method='Case'+str(index),status='translated-unverified'))
 (out/(cls+'.cs')).write_text('using SkrGui;\nusing static SkrGui.Tests.OriginalWidgetFixtures;\nnamespace SkrGui.Tests;\ninternal static class '+cls+'\n{\n'+'\n'.join(parts)+'\n}\n')
(repo/'.report/structure-framework-test-mapping.json').write_text(json.dumps(rows,indent=2))
