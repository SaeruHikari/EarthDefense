"""Syntax-port retained VG path algorithms; engine files remain read-only."""
from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
BASE=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core')
src=(BASE/'src/vg/vg_path_flatten.cpp').read_text(encoding='utf-8')
def pascal(name): return ''.join(s[:1].upper()+s[1:] for s in name.strip('_').split('_'))
pattern=re.compile(r'^(.*?)\s+VGPathFlatten::(\w+)\s*\((.*?)\)\s*(?:const\s*)?\{',re.S|re.M)
# Do not let a match swallow includes or preceding functions.
pattern=re.compile(r'^(?:const\s+)?([\w:<>, &]+?)\s+VGPathFlatten::(\w+)\s*\((.*?)\)\s*(?:const\s*)?\{',re.S|re.M)
functions=[]
for m in pattern.finditer(src):
    start=m.end()-1;end=start+1;depth=1
    while depth:
        if src[end]=='{':depth+=1
        elif src[end]=='}':depth-=1
        end+=1
    functions.append((m[1].strip(),m[2],m[3],src[start:end],src[:m.start()].count('\n')+1))
names={name:pascal(name) for _,name,_,_,_ in functions}
fields=['position','next_direction','join_direction','contour_distance','next_length','flags','cache_flags','node_begin','node_count','bevel_count','total_length','closed','winding_hint','convex','direction']
static_names={'_is_pass_through_join','_is_line_valid','_try_calc_segment_line_intersection_t','_find_next_segment_line_intersection_t','_find_previous_segment_line_intersection_t','_count_segment_line_intersections','_make_line_intersection_node'}
out_refs={'_try_calc_segment_line_intersection_t':{'out_t'},'_find_next_segment_line_intersection_t':{'out_t'},'_find_previous_segment_line_intersection_t':{'out_t'},'_update_segment_data':{'has_bounds'}}
ref_contour={'_reverse_contour_nodes','_enforce_winding_hint','_update_segment_data','_update_join_data'}

def convert(text,body=False):
    text=re.sub(r'//[^\n]*','',text)
    text=text.replace('this != &input','!ReferenceEquals(this, input)')
    text=text.replace('std::numeric_limits<uint32_t>::max()','uint.MaxValue')
    text=re.sub(r'static_cast<([^>]+)>\(',r'(\1)(',text)
    text=text.replace('uint32_t','uint').replace('uint64_t','ulong')
    text=text.replace('Span<const VGPathFlattenLine>','ReadOnlySpan<VGPathFlattenLine>')
    text=text.replace('Optional<EVGPathWinding>','EVGPathWinding?')
    text=text.replace('SKR_ASSERT','Debug.Assert').replace('SKR_VERIFY','Debug.Assert')
    text=re.sub(r'&&\s*("[^"\n]*")\s*\)',r', \1)',text)
    text=text.replace('std::isfinite','float.IsFinite').replace('std::fabs','MathF.Abs').replace('std::abs','MathF.Abs').replace('std::max','CppMath.Max')
    text=text.replace('point_equals_tolerance','PointEqualsTolerance').replace('_k_epsilon','KEpsilon')
    for a,b in names.items():text=re.sub(r'\b'+re.escape(a)+r'\b',b,text)
    for f in fields:text=re.sub(r'\.'+f+r'\b','.'+pascal(f),text)
    for a,b in [('is_finite','IsFinite'),('nearly_equal','NearlyEqual'),('unite','Unite'),('hold','Hold'),('length_squared','LengthSquared'),('length','Length'),('cross','Cross'),('dot','Dot'),('size','Size'),('is_empty','IsEmpty'),('reserve','Reserve'),('clear','Clear'),('release','Release'),('at_last','AtLast'),('push_back','PushBack'),('remove_at','RemoveAt'),('stack_pop_unsafe','StackPopUnsafe'),('resize_unsafe','ResizeUnsafe')]:text=text.replace('.'+a+'(','.'+b+'(')
    text=re.sub(r'\.([xy])\b',lambda m:'.'+m[1].upper(),text)
    text=text.replace('::','.')
    text=text.replace('lines.IsEmpty()','lines.IsEmpty')
    text=text.replace('return {};','return default;').replace('= {};','= default;')
    text=re.sub(r'(?<!\w)Offsetf\(', 'new Offsetf(', text)
    text=re.sub(r'for \(const ([\w]+)& (\w+) : ([^\)]+)\)',r'foreach (\1 \2 in \3)',text)
    text=re.sub(r'for \((\w+)& (\w+) : ([^\)]+)\)',r'foreach (ref \1 \2 in \3.AsSpan())',text)
    text=re.sub(r'for \((\w+) (\w+) : ([^\)]+)\)',r'foreach (\1 \2_original in \3)',text)
    # Only value-copy range loop in this file is append_from contour.
    text=text.replace('foreach (VGPathFlattenContour contour_original in input._contours)\n    {','foreach (VGPathFlattenContour contour_original in input._contours)\n    {\n        VGPathFlattenContour contour = contour_original;')
    text=re.sub(r'const (\w+)& (\w+)\s*=',r'\1 \2 =',text)
    text=re.sub(r'(?<!const )(VGPathFlattenNode|VGPathFlattenContour)& (\w+)\s*=',r'ref \1 \2 = ref',text)
    text=text.replace('.add_default().ref()', '.AddDefault()')
    text=re.sub(r'\bconst\s+', '', text)
    text=text.replace('VGPathFlatten& input','VGPathFlatten input').replace('VGPathFlattenContour& contour','VGPathFlattenContour contour').replace('VGPathFlattenLine& line','VGPathFlattenLine line')
    text=text.replace('!contour.WindingHint','!contour.WindingHint.HasValue').replace('*contour.WindingHint','contour.WindingHint.Value')
    text=text.replace('skr.flag_erase(node.Flags, EVGPathFlattenNodeFlags.Left)','node.Flags & ~EVGPathFlattenNodeFlags.Left')
    return text

out=['// Source: src/vg/vg_path_flatten.cpp @ 611561f8.','// Syntax mapping preserves retained-node loops, mutation and winding rules.','using System.Diagnostics;','namespace SkrGui;','public sealed partial class VGPathFlatten','{']
for ret,name,args,body,line in functions:
    ret=ret.replace('uint32_t','uint').replace('const ','').replace('&','').strip()
    ret=ret.replace('Vector<VGPathFlattenNode>','IReadOnlyList<VGPathFlattenNode>').replace('Vector<VGPathFlattenContour>','IReadOnlyList<VGPathFlattenContour>')
    args=convert(args)
    for arg in out_refs.get(name,[]):args=args.replace('float& '+arg,'ref float '+arg).replace('bool& '+arg,'ref bool '+arg)
    if name in ref_contour:args=args.replace('VGPathFlattenContour contour','ref VGPathFlattenContour contour')
    args=args.replace('Offsetf&','Offsetf')
    if name=='finalize':args='bool force = false'
    if name in {'remove_collinear_segments','add_line_intersections','_is_pass_through_join'}:args=args.replace('float tolerance','float tolerance = 0')
    if name=='close_contour':args='EVGPathWinding? winding_hint = null'
    if name=='release':args='ulong node_capacity = 0, ulong contour_capacity = 0'
    body=convert(body,True)
    # Convert explicit C++ output references to C# ref calls.
    for fn in ['TryCalcSegmentLineIntersectionT','FindNextSegmentLineIntersectionT','FindPreviousSegmentLineIntersectionT']:
        body=re.sub(r'('+fn+r'\([\s\S]*?)(\b(?:hit_t|t)\s*)(\)\))',r'\1ref \2\3',body)
    for fn in ['EnforceWindingHint','UpdateJoinData','ReverseContourNodes']:body=body.replace(fn+'(contour)',fn+'(ref contour)')
    body=body.replace('UpdateSegmentData(contour, has_bounds)','UpdateSegmentData(ref contour, ref has_bounds)')
    out += [f'    // Original line {line}: {name}.','    '+('private' if name.startswith('_') else 'public')+(' static' if name in static_names else '')+' '+ret+' '+names[name]+'('+args.strip()+')']
    out += ['    '+row for row in body.splitlines()]
out.append('}')
target=ROOT/'libraries/SkrGui.Core/Vg/VgPathFlatten.Retained.cs'
target.write_text('\n'.join(out)+'\n',encoding='utf-8')
print(f'{len(functions)} original method bodies -> {target}')
