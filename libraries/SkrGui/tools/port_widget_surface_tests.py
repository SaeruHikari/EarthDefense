from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/tests/widgets/widget_surface_tests.cpp')
f={};exec((repo/'tools/port_framework_tests.py').read_text().split('rows=[]')[0].replace('__file__',repr(str(repo/'tools/port_framework_tests.py'))),f)
v={};exec((repo/'tools/port_visual_tests.py').read_text().split('rows=[]')[0].replace('__file__',repr(str(repo/'tools/port_visual_tests.py'))),v)
pascal=f['pascal'];close=f['close']
def conv(s):
 s=s.replace('u8"','"').replace('d5::SRGBColor','SRGBColor').replace('d5::String','string').replace('skr::type_id_of<','typeof(')
 s=re.sub(r'typeof\((\w+)>\(\)',r'typeof(\1)',s)
 for widget,field in [('Opacity','opacity'),('Padding','padding'),('Visibility','visibility'),('AspectRatio','aspect_ratio')]:
  for m in reversed(list(re.finditer(r'\[\]\('+widget+r'& (\w+)\)\s*\{',s))):
   end=close(s,m.end());body=s[m.end():end-1].replace(m[1]+'.'+field,m[1]+'.'+field+'_value');s=s[:m.end()]+body+s[end-1:]
 s=s.replace('$w.padding =','$w.padding_value =')
 s=f['conv'](s);s=v['conv'](s)
 s=re.sub(r'\[\]\((\w+)\)\s*\{',r'(\1 unused) => {',s)
 s=re.sub(r'\[[^]]*\]\((\w+)&? (\w+)\)\s*\{',r'(\1 \2) => {',s)
 s=re.sub(r'SRGBColor (\w+)\(([^;]+)\);',r'SRGBColor \1 = new SRGBColor(\2);',s)
 s=s.replace('WidgetTreeHost host(', 'WidgetTreeHost host(')
 s=re.sub(r'WidgetTreeHost (\w+)\(([^;]+)\);',r'using var \1 = new WidgetTreeHost(\2);',s)
 s=s.replace('std.Move(', 'Identity(').replace('std.Isinf(', 'double.IsInfinity(')
 for name in ['Key','SRGBColor']:
  s=re.sub(r'(?<![\w.])'+name+r'\(',r'new '+name+'(',s)
 s=s.replace('.RttrGetTypeid()', '.GetType()')
 # Nullable scalar dereference in original assertions.
 s=re.sub(r'\*((?:visual|slot)\.\w+\(\))',r'\1.Value',s)
 s=re.sub(r'(\.(?:ColumnTrack|RowTrack)\([^)]*\))\.Value(?!\()',r'\1.Value()',s)
 s=s.replace('kTextDefaultEllipsisCodepoint','TextDefaults.EllipsisCodepoint').replace('OpacityValueMode','OpacityMode').replace('widget.Padding =','widget.PaddingValue =')
 s=s.replace('border_color','BorderColor').replace('background_color','BackgroundColor').replace('Expect_NE(','NotEqualNumeric(')
 s=re.sub(r'\*\(\(VisualStackSlot\)\(positioned_slot\)\)\.(Left|Top)\(\)',r'((VisualStackSlot)(positioned_slot)).\1().Value',s)
 return s.replace("new new ","new ").replace("long(","(long)(")
text=src.read_text();parts=[];rows=[]
for m in re.finditer(r'RC<(TempText|PlacementLayout)> (\w+)\(([^)]+)\)\s*\{',text):
 end=close(text,m.end());parts.append('private static '+m[1]+' '+pascal(m[2])+'('+conv(m[3])+'){'+conv(text[m.end():end-1])+'}')
for i,m in enumerate(re.finditer(r'SKR_TEST_CASE\("([^"]+)"\)\s*\{',text)):
 end=close(text,m.end());parts.append('[GuiTest('+json.dumps('widgets/widget_surface_tests.cpp::'+m[1])+')]\npublic static void Case'+str(i)+'(){'+conv(text[m.end():end-1])+'}')
 rows.append(dict(source='widgets/widget_surface_tests.cpp',case=m[1],target='WidgetSurfaceTests.cs',method='Case'+str(i),status='translated-unverified'))
p=repo/'tests/SkrGui.Core.Tests/Widgets';p.mkdir(exist_ok=True)
(p/'WidgetSurfaceTests.cs').write_text('using SkrGui;\nusing static SkrGui.Tests.OriginalWidgetFixtures;\nusing static SkrGui.Tests.OriginalMathHelpers;\nusing static SkrGui.Tests.OriginalWidgetSurfaceHelpers;\nnamespace SkrGui.Tests;\ninternal static class WidgetSurfaceTests\n{\n'+'\n'.join(parts)+'\n}\n')
(repo/'.report/structure-widget-test-mapping.json').write_text(json.dumps(rows,indent=2))
