from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests')
out=repo/'tests/SkrGui.Core.Tests/Visual';out.mkdir(parents=True,exist_ok=True)
def pascal(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
def close(s,start,left='{',right='}'):
 i=start;depth=1
 while depth:depth+=(s[i]==left)-(s[i]==right);i+=1
 return i
def blocks(s,pat):
 for m in re.finditer(pat,s,re.M):
  end=close(s,m.end());yield m,s[m.end():end-1],end
types='Offsetf Sizef Sizei Rectf Radius RRect Alignment AlignmentMixed EdgeInsets EdgeInsetsMixed BoxConstraints PaintTransform PaintTransform2D VisualHitTestResult VisualGridTrackSize VisualPlacementAxisSize'.split()
fields={}
def conv(s,ret=None):
 s=re.sub(r'//[^\n]*','',s);s=re.sub(r'\b(?:const|mutable|inline)\s+','',s)
 s=re.sub(r'\(void\)\w+;','',s)
 s=s.replace('::skr::','').replace('skr::','').replace('test::','')
 s=s.replace('Vector<VisualNode*>*','List<VisualNode>').replace('Vector<VisualNode*>','List<VisualNode>')
 s=re.sub(r'RC<(\w+)>::New\(',r'new \1(',s);s=re.sub(r'RC<(\w+)>',r'\1',s)
 s=re.sub(r'Optional<(\w+)>',r'\1?',s)
 for m in reversed(list(re.finditer(r'static_cast<([^>]+)>\(',s))):
  end=close(s,m.end(),'(',')');t=m[1].replace('*','');s=s[:m.start()]+'(('+t+')('+s[m.end():end-1]+'))'+s[end:]
 s=re.sub(r'\b(\w+)\*\s+',r'\1 ',s);s=re.sub(r'(?<=[\w>])&','',s)
 s=s.replace('->','.').replace('::','.').replace('.get()','').replace('nullptr','null')
 s=re.sub(r'\bstd\.numeric_limits<(float|double)>\.infinity\(\)',r'\1.PositiveInfinity',s)
 s=re.sub(r'\bstd\.numeric_limits<(float|double)>\.quiet_NaN\(\)',r'\1.NaN',s)
 for a,b in [('std.max','CppMath.Max'),('std.min','CppMath.Min'),('std.isinf','double.IsInfinity'),('uint64_t','ulong'),('int64_t','long'),('uint32_t','uint'),('int32_t','int'),('auto','var')]:s=re.sub(r'\b'+re.escape(a)+r'\b',b,s)
 s=re.sub(r'(\w+) = \{\};',r'\1 = new();',s)
 s=re.sub(r'float\? (\w+) = new\(\);',r'float? \1 = null;',s)
 s=re.sub(r'(?<![\w.])('+ '|'.join(types)+r')\(',r'new \1(',s)
 s=re.sub(r'\b('+ '|'.join(types)+r')\s+(\w+)\(([^;]+)\);',r'\1 \2 = new \1(\3);',s)
 s=re.sub(r'\b(\w+)\s+(\w+)\[\]\s*=',r'\1[] \2 =',s)
 s=re.sub(r'if \(VisualNode (\w+) = ([^)]+\(\))\)',r'if (\2 is {} \1)',s)
 s=re.sub(r'(?<![\w.])(constraints|child|size)\(',lambda m:pascal(m[1])+'(',s)
 s=s.replace('this.child()','Child()').replace('_child_offset','_childOffset')
 s=s.replace('Super.','base.').replace('VisualProxy.perform_layout()', 'base.PerformLayout()')
 s=re.sub(r'\.([a-z_]\w*)',lambda m:'.'+pascal(m[1]),s)
 # Source fixture fields need identical access from their own methods and cases.
 for name,target in sorted(fields.items(),key=lambda x:-len(x[0])):
  s=re.sub(r'\b'+name+r'\b(?!\s*\()',target,s)
 for name in set(re.findall(r'\b([a-z_]\w*)\s*(?:<[^>]+>)?\s*\(',s)):
  if '_' in name:s=re.sub(r'\b'+name+r'(?=\s*(?:<[^>]+>)?\s*\()',pascal(name),s)
 for a,b in [('SKR_TEST_CHECK_EQ','Equal'),('SKR_TEST_EXPECT_EQ','Equal'),('SKR_TEST_CHECK_FALSE','Check.False'),('SKR_TEST_EXPECT_FALSE','Check.False'),('SKR_TEST_EXPECT','Expect'),('SKR_TEST_CHECK','Expect')]:s=s.replace(a,b)
 s=re.sub(r'SKR_TEST_SUBCASE\("([^"]+)"\)\s*',r'// subcase \1\n',s)
 s=re.sub(r'\bSlotOf<([^>]+)>',r'SlotOf<\1>',s)
 s=s.replace('.HasValue()', '.HasValue').replace('.Value()', '.Value')
 s=re.sub(r'\*([\w.]+(?:\([^;]*?\))?)',r'\1.Value',s) if False else s
 s=re.sub(r'\[([^]]*)\]\((VisualHitTestResult) (\w+), (Offsetf) (\w+)\)\s*\{',r'(\2 \3, \4 \5) => {',s)
 s=s.replace('float4x4.FromScale(', 'Matrix4x4.CreateScale(').replace('float4x4','Matrix4x4').replace('float3(', 'new Vector3(')
 s=s.replace('test.','')
 s=s.replace('!Child ||', 'Child==null ||')
 s=s.replace('&LayoutLog','LayoutLog').replace('&PaintLog','PaintLog')
 s=s.replace('SRGBColor(', 'new SRGBColor(').replace('new new ', 'new ')
 s=s.replace('2d(', '2D(').replace('3d(', '3D(')
 s=s.replace('kFlutterPrecisionErrorTolerance','GuiConstants.FlutterPrecisionErrorTolerance')
 s=re.sub(r'(?<![\w.])(Placement|BoxConstraints) (\w+);',r'\1 \2 = new();',s)
 s=re.sub(r'(?<!\w)float\?\(', '(float?)(',s)
 s=re.sub(r'(?<!\w)base\b(?!\.PerformLayout)', '@base',s)
 s=re.sub(r'for \((\w+) (\w+) : ([^)]+)\)',r'foreach (\1 \2 in \3)',s)
 s=s.replace('({},', '(null,')
 s=s.replace(', {},', ', null,').replace(', {})', ', null)').replace(', {},', ', null,')
 s=s.replace('NposOf<ulong>', 'ulong.MaxValue').replace('npos_of<ulong>', 'ulong.MaxValue')
 s=s.replace('&layout_log','layout_log').replace('&paint_log','paint_log')
 s=re.sub(r'if \((!?)([Ll]ayout[Ll]og|[Pp]aint[Ll]og|parent)\)',lambda m:'if ('+m[2]+('==null' if m[1] else '!=null')+')',s)
 s=s.replace('!child ||', 'child==null ||')
 s=s.replace('LayoutNode(', 'VisualTestHelpers.LayoutNode(')
 s=re.sub(r'MakeVisualOwner\(backend\)', 'MakeVisualOwner(out backend)',s)
 s=s.replace('MakeVisualOwner(', 'VisualTestHelpers.MakeVisualOwner(').replace('MakeRenderDesc(', 'VisualTestHelpers.MakeRenderDesc(')
 if ret=='float?':s=s.replace('return {};','return null;')
 s=s.replace('Check.False(', 'ExpectFalse(')
 # C++ optional dereference, with balanced calls.
 for m in reversed(list(re.finditer(r'(?<![\w)])\*(?=[A-Za-z_])',s))):
  i=m.end()
  while i<len(s):
   name=re.match(r'[A-Za-z_]\w*',s[i:])
   if not name:break
   i+=len(name[0])
   if i<len(s) and s[i]=='(':i=close(s,i+1,'(',')')
   if i<len(s) and s[i]=='.':i+=1;continue
   break
  s=s[:m.start()]+s[m.end():i]+'.Value'+s[i:]
 return s
rows=[]
for rel in ['visual/visual_child_management_tests.cpp','visual/visual_node_tests.cpp','visual/layout_scheduling_tests.cpp','visual/multi_child/visual_grid_tests.cpp','visual/multi_child/visual_placement_tests.cpp','visual/proxy/visual_opacity_tests.cpp','visual/shifted/visual_border_tests.cpp']:
 text=(src/rel).read_text();prefix=text[:text.index('SKR_TEST_CASE')];cls=pascal(Path(rel).stem);parts=[];fields={}
 structs=list(blocks(text,r'struct (\w+)\s*(?::\s*(?:public\s+)?(\w+))?\s*\{'))
 for m,b,end in structs:
  for f in re.finditer(r'^\s*(?:mutable\s+)?[\w<>:*]+\s+(\w+)\s*=',b,re.M):fields[f[1]]=pascal(f[1])
 for m,b,end in structs:
  name,base=m.groups();members=[]
  # Fields occur at class top level; isolate method regions first.
  methodspans=[]
  pat=r'^\s*(?:inline\s+)?([\w<>:*]+)\s+(\w+)\s*\(([^;{}]*?)\)\s*(?:const\s*)?(override\s*)?\{'
  for mm,bb,ee in blocks(b,pat):
   rt,fn,args,over=mm.groups();methodspans.append((mm.start(),ee))
   args=re.sub(r'\bfloat\s*(?=,|$)', 'float unused',args)
   accessibility='protected override' if over else 'public'
   if name=='CountingAspectRatio' and fn=='perform_layout':
    bb=bb.replace('Super::perform_layout();', 'if (Child() is {} child) { LayoutChild(child,Constraints()); SetSize(child.Size()); return; } SetSize(ComputeSizeForNoChild(Constraints()));')
   members.append(accessibility+' '+conv(rt)+' '+pascal(fn)+'('+conv(args)+'){'+conv(bb,conv(rt))+'}')
  for mm,bb,ee in blocks(b,r'^\s*'+name+r'\(\)\s*\{'):
   methodspans.append((mm.start(),ee));members.append('public '+name+'(){'+conv(bb)+'}')
  fieldbody=b
  for a,z in sorted(methodspans,reverse=True):fieldbody=fieldbody[:a]+fieldbody[z:]
  for f in re.finditer(r'^\s*(?:mutable\s+)?([\w<>:*]+)\s+(\w+)\s*=\s*([^;]+);',fieldbody,re.M):
   t,n,value=f.groups();members.insert(0,'public '+conv(t)+' '+pascal(n)+' = '+conv(value)+';')
  members=[x.replace('= {};','= new();').replace('float? DryBaseline = new();','float? DryBaseline = null;').replace('float? ActualBaseline = new();','float? ActualBaseline = null;') for x in members]
  parts.append('private class '+name+(' : '+base if base else '')+'{\n'+'\n'.join(members)+'\n}')
 # Original slot and transform helpers; managed casts replace explicit C++ pointer casts.
 parts+=['private static T SlotOf<T>(VisualNode node)where T:VisualSlot {Check.NotNull(node);Check.NotNull(node.Slot());return (T)node.Slot()!;}',
 'private static void CheckHitTestTransform(Matrix4x4 transform,Offsetf position,float x,float y){var point=Vector4.Transform(new Vector4(position.X,position.Y,0,1),transform);if(point.W!=1){point.X/=point.W;point.Y/=point.W;}CheckOffsetf(new(point.X,point.Y),x,y);}']
 for index,m in enumerate(re.finditer(r'SKR_TEST_CASE(?:_FIXTURE\((\w+),\s*|\(\s*)"([^"]+)"\s*\)\s*\{',text)):
  end=close(text,m.end());body=conv(text[m.end():end-1]);fixture=m[1]
  if m[2].endswith('owner-destruction-clears-child-state'):body=body.replace('var owner =', 'using var owner =')
  if fixture:body='var fixture=new '+fixture+'();\n'+body.replace('MakeOwner()', 'fixture.MakeOwner()')
  parts.append('[GuiTest('+json.dumps(rel+'::'+m[2])+')]\npublic static void Case'+str(index)+'(){\n'+body+'\n}')
  rows.append(dict(source=rel,case=m[2],target=cls+'.cs',method='Case'+str(index),status='translated-unverified'))
 (out/(cls+'.cs')).write_text('using System.Numerics;\nusing SkrGui;\nusing static SkrGui.Tests.OriginalMathHelpers;\nusing static SkrGui.Tests.OriginalWidgetFixtures;\nnamespace SkrGui.Tests;\ninternal static class '+cls+'\n{\n'+'\n'.join(parts)+'\n}\n')
(repo/'.report/structure-visual-test-mapping.json').write_text(json.dumps(rows,indent=2))
