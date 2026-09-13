from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/src/vg/vg_path_flatten.stroke.cpp').read_text(encoding='utf-8')
def pascal(s):return ''.join(p[:1].upper()+p[1:] for p in s.strip('_').split('_'))
def block(s,a):
 i=a+1;d=1
 while d:
  if s[i]=='{':d+=1
  elif s[i]=='}':d-=1
  i+=1
 return s[a:i]
fs=[]
for m in re.finditer(r'^(?:inline )?(\w+) (StrokeBuildContext|StrokeReserveEstimate|VGPathFlatten)::(\w+)\((.*?)\)\s*(?:const\s*)?\{',src,re.S|re.M):fs.append((m[1],m[2],m[3],m[4],block(src,m.end()-1),src[:m.start()].count('\n')+1))
names={name:pascal(name) for _,_,name,_,_,_ in fs}
fields=['emitter','body_radius','aa_radius','round_tolerance','round_divisions','has_aa','left_body','right_body','left_aa','right_aa','incoming','outgoing','first_body','first_aa','last_body','last_aa','extra_begin','extra_count','vertex_count','triangle_count']
methods=['radians','resolve_logical_radius','push_vertex','push_triangle','push_quad','cross','dot','is_finite','is_valid','estimate_segment_count','calc_tolerance','length_squared','length','normalize','ccw_normal','cw_normal','call_reserve','is_empty','_is_pass_through_join','_previous_node']
def rename_shadowed(text):
 ds=list(re.finditer(r'\b(?:float|uint|ulong|Offsetf|StrokeSectionIndices|StrokeJoinSections|VGPathFlattenNode|VGFillAANode)\s+(\w+)\s*=',text));repeated={m[1] for m in ds if sum(n[1]==m[1] for n in ds)>1}
 for serial,m in reversed(list(enumerate(ds))):
  if m[1] not in repeated:continue
  stack=[]
  for i,c in enumerate(text[:m.start()]):
   if c=='{':stack.append(i)
   elif c=='}' and stack:stack.pop()
  if not stack:continue
  end=stack[-1]+len(block(text,stack[-1]));text=text[:m.start()]+re.sub(r'(?<!\.)\b'+m[1]+r'\b',m[1]+'_scope'+str(serial),text[m.start():end])+text[end:]
 return text

def conv(s):
 s=re.sub(r'//[^\n]*','',s);s=s.replace('uint32_t','uint').replace('uint64_t','ulong')
 s=re.sub(r'static_cast<([^>]+)>\(',r'(\1)(',s)
 s=s.replace('std::isfinite','float.IsFinite').replace('std::abs','MathF.Abs').replace('std::cos','MathF.Cos').replace('std::sin','MathF.Sin')
 s=s.replace('skr::math::kPi2','(MathF.PI*2)').replace('skr::kPi2','(MathF.PI*2)').replace('skr::kPi','MathF.PI')
 s=s.replace('skr::flag_all','VgFlags.All').replace('skr::flag_any','VgFlags.Any')
 s=s.replace('SKR_ASSERT','Debug.Assert').replace('SKR_VERIFY','Debug.Assert');s=re.sub(r'&&\s*("[^"\n]*")\s*\)',r', \1)',s)
 for a,b in names.items():s=re.sub(r'\b'+a+r'\b',b,s)
 for f in fields:s=re.sub(r'\b'+f+r'\b',pascal(f),s)
 for m in methods:s=re.sub(r'\b'+m+r'\b',pascal(m),s)
 for f in ['position','next_direction','join_direction','flags','cache_flags','node_begin','node_count','bevel_count','next_length','closed','join','cap','width','tessellation_factor','pixel_ratio','miter_limit','x','y']:s=re.sub(r'\.'+f+r'\b','.'+pascal(f),s)
 s=s.replace('::','.')
 s=s.replace('_k_epsilon','KEpsilon')
 s=re.sub(r'for \(const VGPathFlattenContour& (\w+) : _contours\)',r'foreach (ref VGPathFlattenContour \1 in _contours.AsSpan())',s)
 s=re.sub(r'const (VGPathFlattenNode)& (\w+) = _nodes\[([^;]+)\];',r'ref \1 \2 = ref _nodes[\3];',s)
 s=re.sub(r'\bconst\s+','',s)
 for t in ['VGPathFlattenNode','VGPathFlattenContour','VGBackend','VGStrokeOptions','VGMeshEmitter','StrokeSectionIndices','StrokeRoundArcIndices','StrokeReserveEstimate']:s=s.replace(t+'&',t)
 s=s.replace('= {};','= new();')
 s=re.sub(r'(?<!\w)Offsetf\(', 'new Offsetf(',s)
 for m in reversed(list(re.finditer(r'\b(StrokeJoinSections|StrokeRoundArcIndices)\s*\{',s))):
  b=block(s,m.end()-1);s=s[:m.start()]+'new '+m[1]+'('+b[1:-1].strip().rstrip(',')+')'+s[m.end()-1+len(b):]
 s=re.sub(r'VGMeshEmitter Emitter\{ backend \};','VGMeshEmitter Emitter = new(backend);',s)
 s=re.sub(r'StrokeBuildContext build\{([^}]+)\};',lambda m:'StrokeBuildContext build = new('+m[1].strip().rstrip(',')+');',s)
 return rename_shadowed(s)

out=['// Source: src/vg/vg_path_flatten.stroke.cpp @ 611561f8.','using System.Diagnostics;','namespace SkrGui;',
' internal struct StrokeSectionIndices { public uint LeftBody,RightBody,LeftAa,RightAa; }',
' internal struct StrokeJoinSections(StrokeSectionIndices incoming,StrokeSectionIndices outgoing) { public StrokeSectionIndices Incoming=incoming,Outgoing=outgoing; }',
' internal struct StrokeRoundArcIndices { public uint FirstBody,FirstAa,LastBody,LastAa,ExtraBegin,ExtraCount; }']
for owner in ['StrokeBuildContext','StrokeReserveEstimate','VGPathFlatten']:
 if owner=='StrokeBuildContext':out += ['internal sealed class StrokeBuildContext(VGMeshEmitter emitter,float bodyRadius,float aaRadius,float roundTolerance,uint roundDivisions,bool hasAa)','{','    public VGMeshEmitter Emitter=emitter; public float BodyRadius=bodyRadius,AaRadius=aaRadius,RoundTolerance=roundTolerance; public uint RoundDivisions=roundDivisions; public bool HasAa=hasAa;']
 elif owner=='StrokeReserveEstimate':out += ['internal sealed class StrokeReserveEstimate','{','    public ulong VertexCount,TriangleCount;']
 else:out += ['public sealed partial class VGPathFlatten','{']
 for ret,cls,name,args,body,line in fs:
  if cls!=owner:continue
  args=conv(args);body=conv(body)
  static=owner=='StrokeReserveEstimate' or name in ['line_intersection','calc_bevel_aa_corner','calc_round_divisions']
  out += [f'    // Original line {line}: {name}.','    public '+('static ' if static else '')+ret.replace('uint32_t','uint').replace('uint64_t','ulong')+' '+names[name]+'('+args.strip()+')']+['    '+row for row in body.splitlines()]
 out += ['}']
(ROOT/'libraries/SkrGui.Core/Vg/VgPathFlatten.Stroke.cs').write_text('\n'.join(out)+'\n',encoding='utf-8');print(len(fs),'stroke algorithm bodies')
