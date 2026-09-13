"""Translate the original 29 non-text Widget configuration/adaptor classes.
Each CreateVisual/UpdateVisual/ApplyVisualSlot statement remains in source order.
"""
from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
src=Path(r'D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core')
out=repo/'libraries/SkrGui.Core/Widgets';out.mkdir(parents=True,exist_ok=True)
def pascal(x):return ''.join(w[:1].upper()+w[1:] for w in x.split('_'))
rows=[]
for header in (src/'include/SkrGuiCore/widgets').rglob('*.hpp'):
 if header.stem=='temp_text':continue
 text=header.read_text(encoding='utf-8');match=re.search(r'SKR_GUI_CORE_API\s+(\w+)\s+final\s*:\s*(\w+)',text)
 name,base=match.groups();cpp=src/'src/widgets'/header.relative_to(src/'include/SkrGuiCore/widgets').with_suffix('.cpp');impl=cpp.read_text(encoding='utf-8')
 declarations=[];fields={}
 section=text.split('SKR_GENERATE_BODY('+name+')')[1].split('protected:')[0]
 for t,f,value in re.findall(r'^\s*([\w<>]+)\s+(\w+)\s*=\s*([^;]+);',section,re.M):
  fields[f]=pascal(f)+'Value' if pascal(f)==name else pascal(f);ct=t.replace('int32_t','int').replace('uint32_t','uint').replace('uint64_t','ulong').replace('Vector<','List<')
  optional=ct.startswith('Optional<');ct=ct[9:-1]+'?' if optional else ct
  value=value.strip().replace('std::numeric_limits<float>::infinity()', 'float.PositiveInfinity').replace('::','.')
  value=re.sub(r'(?<!\w)(\w+)\.(?=[A-Z])',r'SkrGui.\1.',value)
  value=value.replace('SkrGui.float.', 'float.')
  if value=='{}':value='null' if optional else 'new()'
  value=re.sub(r'\b(SRGBColor|Offsetf|Sizef|Radius)\(',r'new \1(',value)
  if value.startswith('{'):value='new(){'+value[1:-1]+'}'
  declarations.append(f'    public {ct} {fields[f]}={value};')
 def body_convert(body):
  body=re.sub(r'\(void\)context;','',body)
  body=re.sub(r'RC<(\w+)>::New\(\)',r'new \1()',body)
  body=re.sub(r'SKR_ASSERT\(\s*visual->rttr_cast<(\w+)>\(\)\s*&&\s*"([^"]+)"\s*\);',r'GuiAssert.Require(visual is \1,"\2");',body)
  body=body.replace('auto result = new ', 'var result = new ')
  body=re.sub(r'auto\* result = (visual|slot)->rttr_cast<(\w+)>\(\);',r'var result = (\2)\1;',body)
  body=re.sub(r'SKR_ASSERT\(result && "([^"]+)"\);',r'GuiAssert.Require(result!=null,"\1");',body)
  body=body.replace('->','.').replace('::','.')
  body=re.sub(r'\.([a-z_]\w*)\(',lambda m:'.'+pascal(m[1])+'(',body)
  for f,p in fields.items():body=re.sub(r'\b'+f+r'\b',p,body)
  body=re.sub(r'(?<![\w.])(Alignment|Radius|BoxConstraints|EAxis|ETextDirection|EVerticalDirection|EFlexFit)\.',r'SkrGui.\1.',body)
  return body.strip()
 methods=[]
 for m in re.finditer(r'(?:RC<VisualNode>|void)\s+'+name+r'::(\w+)\([^)]*\)\s*\{',impl):
  depth=1;i=m.end()
  while depth:
   if impl[i]=='{':depth+=1
   elif impl[i]=='}':depth-=1
   i+=1
  method=m[1];signature={'create_visual':'protected override VisualNode CreateVisual(BuildContext context)','update_visual':'protected override void UpdateVisual(BuildContext context,VisualNode visual)','apply_visual_slot':'protected override void ApplyVisualSlot(VisualSlot slot)'}[method]
  body=body_convert(impl[m.end():i-1]);methods.append('    '+signature+'\n    {\n'+ '\n'.join('        '+line.strip() for line in body.splitlines() if line.strip())+'\n    }')
 (out/(name+'.cs')).write_text('\n'.join(['namespace SkrGui;',f'// Source: widgets/{header.relative_to(src/"include/SkrGuiCore/widgets").as_posix()} and matching cpp (611561f8).',f'public sealed class {name} : {base}','{']+declarations+methods+['}'])+'\n',encoding='utf-8')
 rows.append({'source':str(header.relative_to(src)),'implementation':str(cpp.relative_to(src)),'target':f'Widgets/{name}.cs','functions':len(methods),'status':'translated-unverified'})
(repo/'.report/structure-widget-mapping.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
