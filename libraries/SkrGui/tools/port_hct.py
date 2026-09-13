"""Syntax translation of the complete fixed HCT source solver and its constants.
No substitutions of approximation, iteration counts, matrices, or coefficients.
"""
from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
source=Path(r'D:/Code/ExtremeEngine-CppSLJIT/engine/modules/core/base/src/SkrBase/build.hct_color.cpp')
text=source.read_text(encoding='utf-8')
def pascal(x):return ''.join(w[:1].upper()+w[1:] for w in x.split('_'))
type_map={'double3':'Double3','float3':'Vector3','uint32_t':'uint','int32_t':'int','size_t':'int'}
def split_args(s):
 parts=[];start=0;depth=0
 for i,c in enumerate(s):
  if c in '([{':depth+=1
  elif c in ')]}':depth-=1
  elif c==',' and depth==0:parts.append(s[start:i].strip());start=i+1
 parts.append(s[start:].strip());return [p for p in parts if p]
pattern=re.compile(r'^(?:constexpr\s+)?(?P<ret>HCTColor|Double3|double3|SRGBColor|double|bool|uint32_t|int32_t)\s+(?P<cls>HCTColorAlgorithm|HCTColor)::(?P<fn>\w+)\s*\((?P<params>[^;{}]*?)\)\s*(?:const\s*)?noexcept\s*\{',re.M)
functions=[]
for m in pattern.finditer(text):
 i=m.end();depth=1
 while depth:
  if text[i]=='{':depth+=1
  elif text[i]=='}':depth-=1
  i+=1
 params=[];refs=[]
 for index,p in enumerate(split_args(m['params'])):
  array=re.match(r'const\s+double3\s*\(&\s*(\w+)\)\[3\]',p)
  if array:params.append('Double3[] '+array[1]);continue
  isref='&' in p and not p.strip().startswith('const ')
  p=re.sub(r'\bconst\s+','',p).replace('&',' ');p=' '.join(p.split())
  for k,v in type_map.items():p=re.sub(r'\b'+k+r'\b',v,p)
  if isref:p='ref '+p;refs.append(index)
  params.append(p)
 functions.append({'cls':m['cls'],'ret':type_map.get(m['ret'],m['ret']),'fn':m['fn'],'params':params,'refs':refs,'body':text[m.end():i-1],'line':text.count('\n',0,m.start())+1})
fn_names={f['fn']:pascal(f['fn']) for f in functions}
refmap={f['fn']:f['refs'] for f in functions if f['refs']}
def refs_in_calls(body):
 # Start at rightmost calls so nested expressions retain offsets.
 hits=list(re.finditer(r'\b('+ '|'.join(re.escape(k) for k in refmap)+r')\s*\(',body))
 for m in reversed(hits):
  i=m.end();depth=1
  while depth:
   if body[i]=='(':depth+=1
   elif body[i]==')':depth-=1
   i+=1
  args=split_args(body[m.end():i-1])
  for index in refmap[m[1]]:args[index]='ref '+args[index]
  body=body[:m.end()]+','.join(args)+body[i-1:]
 return body
def convert(body,ret):
 body=re.sub(r'//[^\n]*','',body)
 body=refs_in_calls(body)
 body=re.sub(r'SKR_VERIFY\((.*?)&&\s*"([^"]+)"\);',r'GuiAssert.Verify(\1,"\2");',body,flags=re.S)
 body=body.replace('SKR_UNREACHABLE_CODE();','GuiAssert.Require(false,"Unreachable HCT branch");')
 body=re.sub(r'\b(?:const|constexpr)\s+','',body)
 body=body.replace('std::swap(left_j, right_j);','(left_j,right_j)=(right_j,left_j);').replace('std::swap(left_residual, right_residual);','(left_residual,right_residual)=(right_residual,left_residual);')
 for name in ['min','max','clamp','fmod']:body=body.replace('std::'+name,'CppMath.'+pascal(name))
 for name,cname in [('isfinite','double.IsFinite'),('isnan','double.IsNaN'),('isinf','double.IsInfinity'),('pow','Math.Pow'),('abs','Math.Abs'),('sqrt','Math.Sqrt'),('atan2','Math.Atan2'),('cos','Math.Cos'),('sin','Math.Sin'),('copysign','Math.CopySign'),('ceil','Math.Ceiling'),('floor','Math.Floor'),('round','HctMath.Round')]:body=body.replace('std::'+name,cname)
 body=re.sub(r'static_cast<([^>]+)>\(',lambda m:'('+type_map.get(m[1],m[1])+')(',body)
 for k,v in type_map.items():body=re.sub(r'\b'+k+r'\b',v,body)
 body=body.replace('::','.').replace('*this','this')
 for name,target in sorted(fn_names.items(),key=lambda v:-len(v[0])):body=re.sub(r'\b'+name+r'(?=\s*\()',target,body)
 for name in ['x','y','z','r','g','b','a','hue','chroma','tone']:body=re.sub(r'\.'+name+r'\b','.'+pascal(name),body)
 body=re.sub(r'\bbase\b','@base',body)
 # Local array initializers, retaining literal order.
 body=re.sub(r'\b(Double3|double|bool)\s+(\w+)\s*\[[^]]*\]\s*=\s*\{([\s\S]*?)\};',lambda m:m[1]+'[] '+m[2]+' = new '+m[1]+'[]{'+(re.sub(r'\{([^{}]*)\}',lambda n:'new Double3('+n[1].strip().rstrip(',')+')',m[3]) if m[1]=='Double3' else m[3])+'};',body)
 body=re.sub(r'\b(Double3|Vector3)\s+(\w+)\s*=\s*\{([^{}]*)\};',lambda m:m[1]+' '+m[2]+' = new '+m[1]+'('+m[3].strip().rstrip(',')+');',body)
 body=re.sub(r'\b(Double3|double)\s+(\w+)\s*;',r'\1 \2 = new();',body)
 body=re.sub(r'\b(Double3|Vector3)\s*\{([^{}]*)\}',lambda m:'new '+m[1]+'('+m[2].strip().rstrip(',')+')',body)
 body=re.sub(r'(\b(?:scaled|linear|result))\s*=\s*\{([^{}]*)\};',lambda m:m[1]+' = new Double3('+m[2].strip().rstrip(',')+');',body)
 body=re.sub(r'FromLinear\(\s*\{([^{}]*)\}\s*\)',lambda m:'FromLinear(new Double3('+m[1].strip().rstrip(',')+'))',body)
 body=re.sub(r'return\s*\{([^{}]*)\};',lambda m:'return new '+ret+'('+m[1].strip().rstrip(',')+');',body)
 # C# forbids inner/outer local name reuse allowed by C++.
 if 'double r = coordinate_a;' in body and 'if (index < 4)' in body:
  marker=body.rfind('double r = coordinate_a;');body=body[:marker]+re.sub(r'\b([rgb])\b',lambda m:'final_'+m[1],body[marker:])
 # No-argument value returns construct source defaults, including opaque black.
 body=body.replace('return new bool();','return false;')
 return body.strip()
license=text[:text.index('#include')]
constants=[]
head=text[:text.index('// impl HCTColorAlgorithm')]
for m in re.finditer(r'inline static constexpr (double3|double)\s+(\w+)(\[[^]]*\])?\s*=\s*([^;]+);',head):
 t,name,array,val=m.groups();val=re.sub(r'//[^\n]*','',val).strip()
 if t=='double3':
  if array:
   inside=val.strip()[1:-1];inside=re.sub(r'\{([^{}]*)\}',lambda n:'new Double3('+n[1].strip().rstrip(',')+')',inside);constants.append('    private static readonly Double3[] '+name+' = new Double3[]{'+inside+'};')
  else:constants.append('    private static readonly Double3 '+name+' = new('+val[1:-1].strip().rstrip(',')+');')
 elif array:constants.append('    private static readonly double[] '+name+' = new double[]'+val+';')
 else:constants.append('    private const double '+name+' = '+val+';')
for cls in ['HCTColorAlgorithm','HCTColor']:
 out=[license,'using System.Numerics;','namespace SkrGui;',f'// Source: {source.name} at 611561f8; expression/control-flow translation.',('internal static class '+cls if cls.endswith('Algorithm') else 'public partial struct '+cls),'{']
 if cls.endswith('Algorithm'):out+=constants
 for f in functions:
  if f['cls']!=cls:continue
  static=cls.endswith('Algorithm') or f['fn'].startswith('From')
  out += [f'    // source line {f["line"]}',f'    public '+('static ' if static else '')+f['ret']+' '+pascal(f['fn'])+'('+','.join(f['params'])+')','    {',convert(f['body'],f['ret']),'    }']
 out+=['}']
 path=repo/'libraries/SkrGui.Core/Math/Color'/('HCTColorAlgorithm.cs' if cls.endswith('Algorithm') else 'HCTColor.Conversions.cs')
 path.write_text('\n'.join(out)+'\n',encoding='utf-8')
(repo/'.report/structure-hct-mapping.json').write_text(json.dumps([{k:v for k,v in f.items() if k not in ['body']}|{'status':'translated-unverified'} for f in functions],indent=2),encoding='utf-8')
