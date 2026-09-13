"""Syntax-only mapping for reviewed Alignment/EdgeInsets inline value functions.
Source expression/control-flow order is retained. Generated output is compiled and
checked against the source tests separately; generation is not a passing test.
"""
from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
source=Path(r'D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/include/SkrGuiCore')
target=repo/'libraries/SkrGui.Core/Math/Layout'
target.mkdir(parents=True,exist_ok=True)
fields={'Alignment':['x','y'],'AlignmentDirectional':['start','y'],'AlignmentMixed':['x','start','y'],'EdgeInsets':['left','top','right','bottom'],'EdgeInsetsDirectional':['start','top','end','bottom'],'EdgeInsetsMixed':['left','right','start','end','top','bottom']}
types=list(fields)+['Offsetf','Sizef','Rectf','Radius','RRect']
def pascal(x):return ''.join(p[:1].upper()+p[1:] for p in x.split('_'))
allfields=set(sum(fields.values(),[])+['width','height','tl_radius','tr_radius','bl_radius','br_radius'])
mapping=[]
for header in ['alignment.hpp','edge_insets.hpp']:
 text=(source/'math/layout'/header).read_text(encoding='utf-8')
 classes={name:[] for name in fields if ('struct '+name+'\n') in text}
 names=set(re.findall(r'::(\w+)\s*\(',text))|set(re.findall(r'\b(\w+)\s*\([^;{}]*\)\s*(?:const\s*)?noexcept\s*;',text))
 def translate(body, ret):
  body=re.sub(r'//[^\n]*','',body)
  body=body.replace('std::numeric_limits<float>::infinity()','float.PositiveInfinity')
  body=body.replace('std::min','CppMath.Min').replace('std::max','CppMath.Max').replace('std::clamp','CppMath.Clamp').replace('std::fmod','CppMath.Fmod')
  body=re.sub(r'\bconst\s+','',body).replace('auto ','var ').replace('*this','this').replace('->','.')
  body=re.sub(r'([A-Za-z_]\w*)\.value_or\(([^()]*)\)',r'\1.GetValueOrDefault(\2)',body)
  body=re.sub(r'\b(\w+)\s*\?\s*\*\1\s*:',r'\1.HasValue ? \1.Value :',body)
  body=body.replace('::','.')
  body=body.replace('var clamp_radius = [](Radius radius) noexcept', 'Func<Radius,Radius> clamp_radius = (Radius radius) =>')
  for name in sorted(names,key=len,reverse=True):
   if name not in types:body=re.sub(r'\b'+name+r'(?=\s*\()',pascal(name),body)
  for f in sorted(allfields,key=len,reverse=True):body=re.sub(r'\b'+f+r'\b',pascal(f),body)
  for name in types:body=re.sub(r'(?<![\w.])'+name+r'\s*\(',r'new '+name+'(',body)
  body=re.sub(r'return\s*\{\s*\};','return new '+ret+'();',body)
  body=re.sub(r'return\s*\{([\s\S]*?)\};',lambda m:'return new '+ret+'('+m[1].strip().rstrip(',')+');',body)
  body=re.sub(r'(?<=\d)\.f\b','.0f',body)
  return body.strip()
 pattern=re.compile(r'inline\s+(?:constexpr\s+)?(?P<ret>[\w:&<>]+)\s+(?:(?P<cls>\w+)::)?(?P<func>operator[+*/%!=\-]+|\w+)\s*\((?P<params>[^)]*)\)\s*(?:const\s*)?noexcept\s*\{')
 for m in pattern.finditer(text):
  ret=m['ret']; cls=m['cls'];fn=m['func'];rawparams=m['params']
  if ret=='constexpr' or fn==cls:continue
  if cls and cls not in classes:continue
  if not cls and (not fn.startswith('operator') or ret not in classes):continue
  if ret.endswith('&') or fn in ['operator=','operator+=','operator-=','operator*=','operator/=','operator%=']:continue
  cls=cls or ret
  depth=1;end=m.end()
  while depth:
   if text[end]=='{':depth+=1
   elif text[end]=='}':depth-=1
   end+=1
  body=text[m.end():end-1]
  params=[]
  for i,p in enumerate(rawparams.split(',')):
   p=p.strip()
   if not p:continue
   p=re.sub(r'\bconst\s+','',p).replace('&','')
   p=p.replace('Optional<float>','float?')
   if ' ' not in p:p+=' direction'
   ptype,pname=p.rsplit(' ',1)
   if pname in allfields:pname=pascal(pname)
   default='=null' if ptype=='float?' else '=0' if fn in ('Symmetric','Only') else ''
   params.append(ptype+' '+pname+default)
  op=fn.startswith('operator')
  if op:
   if m['cls']:
    # C# operators are static; qualify the original receiver without reordering.
    body=body.replace('*this','lhs')
    for f in fields[cls]:body=re.sub(r'(?<![\w.>])\b'+f+r'\b','lhs.'+f,body)
    params.insert(0,cls+' lhs')
   static=True;name=fn
  else:
   # All named factories are upper-case in the source. lerp has one member overload.
   static=fn[:1].isupper() or (fn=='lerp' and len(params)==3)
   name=pascal(fn)
  body=translate(body,ret)
  signature='public '+('static ' if static else '')+ret+' '+name+'('+','.join(params)+')'
  classes[cls].append('    '+signature+'\n    {\n'+ '\n'.join('        '+x.strip() for x in body.splitlines() if x.strip())+'\n    }')
  mapping.append({'source':'math/layout/'+header,'line':text.count('\n',0,m.start())+1,'symbol':cls+'::'+fn,'target':cls+'.cs','status':'translated-unverified'})
 for cls,methods in classes.items():
  fs=fields[cls]
  init='; '.join(pascal(f)+'='+f for f in fs)+';'
  lines=['namespace SkrGui;',f'// Source: SkrGuiCore/math/layout/{header} (611561f8).',f'public partial struct {cls} : IEquatable<{cls}>','{','    public float '+','.join(pascal(f) for f in fs)+';',f'    public {cls}('+','.join('float '+f for f in fs)+') { '+init+' }']
  if cls=='AlignmentMixed':lines += ['    public AlignmentMixed(Alignment value):this(value.X,0,value.Y) {}','    public AlignmentMixed(AlignmentDirectional value):this(0,value.Start,value.Y) {}','    public static implicit operator AlignmentMixed(Alignment value)=>new(value);','    public static implicit operator AlignmentMixed(AlignmentDirectional value)=>new(value);']
  if cls=='EdgeInsetsMixed':lines += ['    public EdgeInsetsMixed(EdgeInsets value):this(value.Left,value.Right,0,0,value.Top,value.Bottom) {}','    public EdgeInsetsMixed(EdgeInsetsDirectional value):this(0,0,value.Start,value.End,value.Top,value.Bottom) {}','    public static implicit operator EdgeInsetsMixed(EdgeInsets value)=>new(value);','    public static implicit operator EdgeInsetsMixed(EdgeInsetsDirectional value)=>new(value);']
  lines+=methods
  lines += [f'    public bool Equals({cls} rhs)=>this==rhs;',f'    public override bool Equals(object? value)=>value is {cls} rhs && this==rhs;','    public override int GetHashCode()=>HashCode.Combine('+','.join(pascal(f) for f in fs)+');','}']
  (target/(cls+'.cs')).write_text('\n'.join(lines)+'\n',encoding='utf-8')
(repo/'.report/structure-value-mapping.json').write_text(json.dumps(mapping,indent=2),encoding='utf-8')
