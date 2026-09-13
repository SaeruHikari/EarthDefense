from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/src/vg/vg_basic_primitive.cpp').read_text(encoding='utf-8')
def pascal(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
def block(s,a):
 i=a+1;d=1
 while d:
  if s[i]=='{':d+=1
  elif s[i]=='}':d-=1
  i+=1
 return s[a:i]
def split_args(s):
 result=[];depth=0;last=0
 for i,c in enumerate(s):
  if c in '([{<':depth+=1
  if c in ')]}>':depth-=1
  if c==',' and depth==0:result.append(s[last:i].strip());last=i+1
 if s[last:].strip():result.append(s[last:].strip())
 return result
structs={};funcs=[];constants={}
for m in re.finditer(r'^struct (\w+)\s*\{',src,re.M):
 body=block(src,m.end()-1)[1:-1];name=m[1]
 if 'static ' not in body:
  fields=[]
  for f in re.finditer(r'^    (\w+) (\w+)(\[[^\n;]+?\])? = ([^;]+);',body,re.M):fields.append((f[1],f[2],f[3],f[4]))
  structs[name]=fields
 else:
  constants[name]=re.findall(r'static constexpr (\w+) (\w+) = ([^;]+);',body)
  for f in re.finditer(r'    static (\w+) (\w+)\((.*?)\) noexcept\s*\{',body,re.S):funcs.append((f[1],name,f[2],f[3],block(body,f.end()-1),src[:m.start()].count('\n')+body[:f.start()].count('\n')+2))
for m in re.finditer(r'^bool VGBasicPrimitives::(\w+)\((.*?)\)\s*\{',src,re.S|re.M):funcs.append(('bool','VGBasicPrimitives',m[1],m[2],block(src,m.end()-1),src[:m.start()].count('\n')+1))
names={x[2]:pascal(x[2]) for x in funcs};names.update({'rrect':'RRect','rrect_stroke':'RRectStroke','diff_rrect':'DiffRRect'})
fields={x[1]:pascal(x[1]) for fs in structs.values() for x in fs};fields.update({x:pascal(x) for x in ['center','radius','radius_x','radius_y','rotation','start_angle','sweep_angle','exponent','pixel_ratio','aa_radius','tessellation_factor','tl_radius','tr_radius','br_radius','bl_radius','left','top','right','bottom','x','y','pos','converge','tolerance','direction','max_depth']})
# Only pointers/references whose mutation must escape need ref in managed code.
ref_positions={}
for ret,owner,name,args,body,line in funcs:
 ref_positions[names[name]]=[i for i,a in enumerate(split_args(args)) if '&' in a and 'const ' not in a and not any(t in a for t in ['VGMeshEmitter','VGBackend','Func'])]
def types(s):
 for a,b in [('uint32_t','uint'),('uint64_t','ulong'),('int32_t','int'),('uint8_t','byte')]:s=s.replace(a,b)
 return s
def signature(args,name):
 arr=[]
 for i,a in enumerate(split_args(args)):
  if 'Func&&' in a:a='Action<Offsetf, float> func' if name=='sample_corner' else 'Action<Offsetf, Offsetf, Offsetf, Offsetf> func'
  elif '[' in a:a=re.sub(r'(\w+) (\w+)\[[^]]+\]',r'\1[] \2',a)
  elif '&' in a and 'const ' not in a and not any(t in a for t in ['VGMeshEmitter','VGBackend']):a='ref '+a
  a=re.sub(r'\bconst\s+','',a).replace('&','').strip();arr.append(types(a))
 return ', '.join(arr)
# C++ bodies require scope-local names whereas C# forbids nested reuse.
def shadow(s):
 ds=list(re.finditer(r'\b(?:float|uint|ulong|bool|Offsetf|Arc|Ellipse|EllipticalArc|Circle|CircleSampler|EllipseSampler|SuperellipseSampler|Superellipse|SuperellipseArc|VGMeshEmitter|VGBasic\w+)\s+(\w+)\s*=',s));counts={m[1]:sum(n[1]==m[1] for n in ds) for m in ds}
 for serial,m in reversed(list(enumerate(ds))):
  if counts[m[1]]<2:continue
  stack=[]
  for i,c in enumerate(s[:m.start()]):
   if c=='{':stack.append(i)
   elif c=='}' and stack:stack.pop()
  if not stack:continue
  end=stack[-1]+len(block(s,stack[-1]));s=s[:m.start()]+re.sub(r'(?<![.\w])'+m[1]+r'\b',m[1]+'_scope'+str(serial),s[m.start():end])+s[end:]
 return s

def conv(s,ret='void'):
 s=re.sub(r'//[^\n]*','',s);s=types(s)
 s=re.sub(r'static_cast<([^>]+)>\(',r'(\1)(',s)
 for a,b in [('std::fabs','MathF.Abs'),('skr::math::kHalfPi','(MathF.PI/2)'),('std::isfinite','float.IsFinite'),('std::min','CppMath.Min'),('std::max','CppMath.Max'),('std::clamp','CppMath.Clamp'),('std::abs','MathF.Abs'),('std::sqrt','MathF.Sqrt'),('std::cos','MathF.Cos'),('std::sin','MathF.Sin'),('std::round','VgScalar.Round'),('skr::math::kPi2','(MathF.PI*2)'),('skr::math::kPiOver2','(MathF.PI/2)'),('skr::math::kPi','MathF.PI'),('SKR_VERIFY','Debug.Assert'),('SKR_ASSERT','Debug.Assert')]:s=s.replace(a,b)
 s=re.sub(r'&&\s*("[^"\n]*")\s*\)',r', \1)',s)
 # Reference variables that get captured use direct array element access.
 if 'VGBasicRRectBoundaryRange& body_range = record.ranges[range_i];' in s:
  s=s.replace('VGBasicRRectBoundaryRange& body_range = record.ranges[range_i];','');s=re.sub(r'\bbody_range\b','record.ranges[range_i]',s)
 s=re.sub(r'\b(VGBasic\w+)& (\w+) = ([^;]+);',r'ref \1 \2 = ref \3;',s)
 s=re.sub(r'\bconst (\w+)& (\w+)',r'\1 \2',s)
 s=re.sub(r'\bconst\s+','',s)
 # Fixed C arrays keep source capacities and zero initialization.
 s=re.sub(r'(\w+) (\w+)\[([^]\n]+)\] = \{\};',r'\1[] \2 = new \1[\3];',s)
 s=re.sub(r'(\w+) (\w+)\[(\d+)\] = \{',r'\1[] \2 = {',s)
 s=s.replace('= {};','= new();')
 for typ,fs in structs.items():
  # Source aggregate initializers into a full-field managed constructor.
  if ret==typ:s=re.sub(r'\breturn\s*\{', 'return new '+typ+'(',s)
 s=re.sub(r'(return new VGBasic\w+\([^;]*?)\};',r'\1);',s,flags=re.S)
 s=re.sub(r'edges\[edge_count\+\+\] = \{([^}]+)\};',lambda m:'edges[edge_count++] = new VGBasicDiffRectGridEdge('+m[1].strip().rstrip(',')+');',s)
 s=re.sub(r'return ShapeToleranceSampleDesc\{([^}]+)\};',lambda m:'return new ShapeToleranceSampleDesc{Tolerance='+split_args(m[1])[0]+',Direction='+split_args(m[1])[1]+',MaxDepth='+split_args(m[1])[2]+'};',s)

 s=re.sub(r'(?<!new )ShapeToleranceSampleDesc\{([^}]+)\}',lambda m:'new ShapeToleranceSampleDesc{Tolerance='+split_args(m[1])[0]+',Direction='+split_args(m[1])[1]+',MaxDepth='+split_args(m[1])[2]+'}',s)
 s=re.sub(r'(CircleSampler|EllipseSampler|SuperellipseSampler) (\w+)\(([^;]+)\);',r'\1 \2 = new(\3);',s)
 s=re.sub(r',\s*\)',')',s)
 s=re.sub(r'\(void\)(\w+);',r'_ = \1;',s)
 # All sampling lambdas retain typed callback parameters, add names for C++ unnamed params.
 def lam(m):
  args=[]
  for i,a in enumerate(split_args(m[1])):
   a=a.replace('const ','').strip();rf='ref ' if '&' in a and a.startswith('uint') else '';a=a.replace('&','');
   if len(a.split())==1:a+=' _unused'+str(i)
   args.append(rf+a)
  return '('+', '.join(args)+') => {'
 s=re.sub(r'\[[^]\n]*\]\((.*?)\)(?: noexcept)?\s*\{',lam,s,flags=re.S)
 s=re.sub(r'\bauto\b','var',s)
 s=s.replace('VGMeshEmitter emitter{ backend };','VGMeshEmitter emitter = new(backend);')
 # Convert type factories before primitive function names (Circle method vs Circle type).
 s=re.sub(r'\b(Circle|Arc|Ellipse|EllipticalArc|Superellipse|SuperellipseArc)::',r'global::SkrGui.\1.',s)
 s=re.sub(r'(?<!\w)Offsetf\(', 'new Offsetf(',s)
 s=re.sub(r'\.([a-z]\w*)',lambda m:'.'+fields.get(m[1],pascal(m[1])) if m[1] in fields or re.match(r'[a-z].*_',m[1]) else m[0],s)
 # Methods without underscores.
 for n in ['is_valid','is_empty','is_finite','is_rect','is_circle','is_ellipse','width','height','center','shift','inflate','deflate','intersect','contains','contains_rect','sample','normalize','length','cross','dot','lerp']:
  s=re.sub(r'\b'+n+r'\s*\(',pascal(n)+'(',s)
 for a,b in names.items():s=re.sub(r'(?<!\w)'+a+r'\s*\(',b+'(',s)
 s=s.replace('::','.');s=re.sub(r'\.([a-z]\w*)\s*\(',lambda m:'.'+pascal(m[1])+'(',s)
 s=re.sub(r'(?<![.\w])RRect\(', 'new RRect(',s)
 s=s.replace('new RRect(backend,','RRect(backend,')
 # Existing shared shape naming.
 s=s.replace('global.SkrGui.','global::SkrGui.')
 # Reference call sites only; declarations emitted separately.
 for name,poses in (ref_positions | {'EmitOuterVertices':[1], 'reuse_body_index':[1], 'reuse_aa_index':[1]}).items():
  for m in reversed(list(re.finditer(r'\b'+name+r'\(',s))):
   start=m.end();d=1;i=start
   while d:
    if s[i]=='(':d+=1
    if s[i]==')':d-=1
    i+=1
   a=split_args(s[start:i-1])
   for p in poses:
    if p<len(a):a[p]='ref '+a[p]
   s=s[:start]+', '.join(a)+s[i-1:]
 return shadow(s)

out=['// Source: src/vg/vg_basic_primitive.cpp @ 611561f8.','using System.Diagnostics;','namespace SkrGui;','public struct VGBasicPrimitiveOptions { public float PixelRatio=1,TessellationFactor=1,AaRadius; public VGBasicPrimitiveOptions() {} }']
for name,fs in structs.items():
 out+=['internal struct '+name,'{']
 for typ,field,array,init in fs:
  if array:out+=['    public '+typ+'[] '+fields[field]+' = new '+typ+'[(int)EVGBasicRRectRange.Count];']
  else:out+=['    public '+types(typ)+' '+fields[field]+' = '+('new()' if init=='{}' else conv(init))+';']
 out+=['    public '+name+'() {}']
 if not any(f[2] for f in fs):out+=['    public '+name+'('+', '.join(types(t)+' '+f for t,f,_,_ in fs)+') { '+''.join('this.'+fields[f]+'='+f+';' for t,f,_,_ in fs)+' }']
 out+=['}']
out+=['internal enum EVGBasicRRectRange : byte { TLCorner,TopEdge,TRCorner,RightEdge,BRCorner,BottomEdge,BLCorner,LeftEdge,Count }']
for owner in list(constants)+['VGBasicPrimitives']:
 out += [('public' if owner=='VGBasicPrimitives' else 'internal')+' static class '+owner,'{']
 for typ,n,val in constants.get(owner,[]):out+=['    public const '+types(typ)+' '+n+' = '+val+';']
 for ret,cls,name,args,body,line in funcs:
  if cls!=owner:continue
  args=signature(args,name);body=conv(body,ret)
  # Preserve source defaults through an overload, because new options carry member defaults.
  if 'VGBasicPrimitiveOptions options' in args:
   short=args.replace(', VGBasicPrimitiveOptions options','');an=[x.split()[-1] for x in split_args(short)]
   out+=['    public static '+types(ret)+' '+names[name]+'('+short+') => '+names[name]+'('+','.join(an)+',new VGBasicPrimitiveOptions());']
  if name in ['circle','ellipse']:
   args=args.replace('float pixel_ratio','float pixel_ratio = 1.0f').replace('float tessellation_factor','float tessellation_factor = 1.0f').replace('float aa_radius','float aa_radius = 0.0f')
  out+=['    // Original line '+str(line)+': '+name+'.','    public static '+types(ret)+' '+names[name]+'('+args+')']+['    '+r for r in body.splitlines()]
 out+=['}']
(ROOT/'libraries/SkrGui.Core/Vg/VgBasicPrimitive.cs').write_text('\n'.join(out)+'\n');print(len(funcs),'original bodies,',len(structs),'helper data types')
