from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
env={'__file__':str(ROOT/'tools/port_vg_stroke.py')};exec((ROOT/'tools/port_vg_stroke.py').read_text(encoding='utf-8-sig').split("out=['// Source:")[0],env)
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/src/vg/vg_path_flatten.fill.cpp').read_text(encoding='utf-8')
block=env['block'];pascal=env['pascal'];base_conv=env['conv']
fs=[]
exclude={'default_memalloc','default_memrealloc','default_memfree','resolve_allocator','tess_memalloc','tess_memrealloc','tess_memfree','clamp_extra_vertices','calc_alloc_buckets','make_alloc','make_tess','add_contour','find_or_push_inner_vertex'}
for m in re.finditer(r'^(?:inline )?([\w:*]+) (FillTessContext|VGPathFlatten)::(\w+)\((.*?)\)\s*(?:const\s*)?\{',src,re.S|re.M):
 if m[3] in exclude:continue
 fs.append((m[1],m[2],m[3],m[4],block(src,m.end()-1),src[:m.start()].count('\n')+1))
env['names']={n:pascal(n) for _,_,n,_,_,_ in fs};env['names'].update({n:pascal(n) for n in exclude})
env['fields'] += ['vertices','vertex_lookup','scratch_contour_vertices','signed_area','has_non_collinear_area','inner_index','incoming_outer_index','outgoing_outer_index','forward','backward','tess','fill_rule','aa_miter_limit','use_delaunay','optimize_for_single_convex','convex','winding_hint']
env['methods'] += ['clear','size','is_empty','is_valid','is_bevel','emit_ring_nodes','emit_outer_vertices','connect_sections','emit_join_triangle','build_node','resolve_logical_radius','find_or_push_inner_vertex','try_emplace']
funcmap={'tessGetVertexCount':'GetVertexCount','tessGetVertices':'GetVertices','tessGetElementCount':'GetElementCount','tessGetElements':'GetElements','tessAddContour':'AddContour','tessSetOption':'SetOption','tessTesselate':'Tesselate'}
def conv(s):
 s=re.sub(r'\bbase\b','basis',s)
 s=s.replace('std::numeric_limits<uint32_t>::max()','uint.MaxValue').replace('static_cast<size_t>','static_cast<int>')
 s=s.replace('std::abs','VgScalar.Abs')
 s=s.replace('TESStesselator*','void*').replace('TESSreal','float').replace('TESSindex','int').replace('nullptr','null')
 s=s.replace('FillTessContext::FillAANode','VGFillAANode').replace('FillAANode','VGFillAANode')
 s=s.replace('Vector<VGPathFlattenNode>&','VgBuffer<VGPathFlattenNode>').replace('Vector<VGPathFlattenContour>&','VgBuffer<VGPathFlattenContour>')
 s=s.replace('Map<VGFillVertexKey, uint32_t>&','Dictionary<VGFillVertexKey,uint>')
 for typ in ['VGFillOptions','TessContour','VGFillWorkspace','VGFillAllocator','VGFillAANode']:s=s.replace(typ+'&',typ)
 s=s.replace('VGFillWorkspace* input_workspace','VGFillWorkspace? input_workspace').replace('*input_workspace','input_workspace')
 s=s.replace('k_geometry_epsilon','KGeometryEpsilon').replace('k_area_epsilon','KAreaEpsilon')
 s=re.sub(r'for \(const VGPathFlattenContour& (\w+) : (\w+)\)',r'foreach (VGPathFlattenContour \1 in \2)',s)
 for old,new in funcmap.items():s=s.replace(old,'TessNative.'+new)
 s=re.sub(r'std::swap\(([^,]+),\s*([^;]+)\);',r'(\1,\2)=(\2,\1);',s)
 s=base_conv(s)
 s=s.replace('.Vertices.add(','.Vertices.Add(').replace('.Vertices.reserve(','.Vertices.Reserve(').replace('.Vertices.remove_at(','.Vertices.RemoveAt(')
 s=s.replace('vertex_count()', 'VertexCount()')
 s=s.replace('VertexLookup.TryEmplace(', 'VertexLookup.TryAdd(').replace('VertexLookup.reserve(', 'VertexLookup.EnsureCapacity(')
 s=re.sub(r'!VertexLookup.find\((VertexKey\(node.Position\))\).IsValid\(\)',r'!VertexLookup.ContainsKey(\1)',s)
 s=s.replace('auto emit_node','var emit_node').replace('auto connect_aa','var connect_aa').replace('auto emit_join','var emit_join')
 s=re.sub(r'\[&\]\((.*?)\)\s*(?:noexcept\s*)?\{',lambda m:'('+m[1]+') => {',s,flags=re.S)
 for typ in ['VGFillVertexKey','FillReserveEstimate','ConvexFillSection']:
  for m in reversed(list(re.finditer(r'\b'+typ+r'\s*\{',s))):
   b=block(s,m.end()-1);s=s[:m.start()]+'new '+typ+'('+b[1:-1].strip().rstrip(',')+')'+s[m.end()-1+len(b):]
  s=re.sub(typ+r' (\w+)\s*=\s*\{([^{}]+)\};',lambda m:typ+' '+m[1]+' = new('+m[2].strip().rstrip(',')+');',s)
 s=re.sub(r'VGFillAAHelper\.EmitOuterVertices\(Emitter, (\w+),',r'VGFillAAHelper.EmitOuterVertices(Emitter, ref \1,',s)
 s=re.sub(r'(?<!using )TessHandle (\w+) = MakeTess\(',r'using TessHandle \1 = MakeTess(',s)
 s=s.replace('FillTessContext.TessHandle boundary_tess =','using FillTessContext.TessHandle boundary_tess =')
 s=re.sub(r'FillTessContext.TessContour scratch_contour\{([^}]+)\};',lambda m:'FillTessContext.TessContour scratch_contour = new('+m[1].strip()+');',s)
 return s
out=['// Source: src/vg/vg_path_flatten.fill.cpp @ 611561f8.','using System.Diagnostics;','using static SkrGui.TessNative;','namespace SkrGui;']
for owner in ['FillTessContext','VGPathFlatten']:
 out += ['internal static unsafe partial class FillTessContext' if owner=='FillTessContext' else 'public sealed unsafe partial class VGPathFlatten','{']
 for ret,cls,name,args,body,line in fs:
  if cls!=owner:continue
  args=conv(args);body=conv(body)
  if name=='fill':args=args.replace('VGFillWorkspace? input_workspace','VGFillWorkspace? input_workspace = null')
  ret=ret.replace('FillTessContext::FillAANode','VGFillAANode').replace('FillTessContext::','').replace('uint32_t','uint').replace('uint64_t','ulong')
  out += [f'    // Original line {line}: {name}.','    public '+('static ' if owner=='FillTessContext' else '')+ret+' '+pascal(name)+'('+args.strip()+')']+['    '+row for row in body.splitlines()]
 out += ['}']
(ROOT/'libraries/SkrGui.Core/Vg/VgPathFlatten.Fill.cs').write_text('\n'.join(out)+'\n',encoding='utf-8');print(len(fs),'fill algorithm bodies plus exact native allocator adapter')
