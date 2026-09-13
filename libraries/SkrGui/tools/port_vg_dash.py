from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
env={'__file__':str(ROOT/'tools/port_vg_stroke.py')};exec((ROOT/'tools/port_vg_stroke.py').read_text(encoding='utf-8-sig').split("out=['// Source:")[0],env)
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/src/vg/vg_path_flatten.dash.cpp').read_text(encoding='utf-8')
block=env['block'];pascal=env['pascal'];base_conv=env['conv']
fs=[]
for m in re.finditer(r'^(?:inline )?(\w+) (VGDashPatternState|VGDashCursor|VGDashContourCursor|VGDashBuilder|VGDashNeedTest|VGPathFlatten)::(\w+)\((.*?)\)\s*(?:const\s*)?\{',src,re.S|re.M):fs.append((m[1],m[2],m[3],m[4],block(src,m.end()-1),src[:m.start()].count('\n')+1))
fs=[f for f in fs if f[1]!=f[2]]
env['names']={n:pascal(n) for _,_,n,_,_,_ in fs}
env['fields'] += ['total_length','contour_distance','winding_hint','values','source_count','virtual_count','non_zero_on_count','non_zero_off_count','pattern_length','pattern','index','remaining','flatten','contour','segment_count','segment_local_index','segment_start_distance','segment_end_distance','offset','node_count','contour_count','position','flags','point_equals_tolerance']
env['methods'] += ['size','at_last','remove_at','is_empty','add_node','next_contour','close_contour','finalize','clear','reserve']
def conv(s):
 s=re.sub(r'\bout\b','Output',s).replace('&Output == this','ReferenceEquals(Output, this)').replace('*this','this')
 s=s.replace('->','.').replace('pattern = &in_pattern','pattern = in_pattern')
 s=s.replace('k_dash_epsilon','0.000001f').replace('std::fmod','CppMath.Fmod').replace('std::ceil','VgScalar.Ceiling').replace('skr::flag_erase','VgFlags.Erase')
 s=re.sub(r'for \(const VGPathFlattenContour& (\w+) : (\w+)\._contours\)',r'foreach (VGPathFlattenContour \1 in \2._contours)',s)
 s=s.replace('VGDashCursor dash_cursor;','VGDashCursor dash_cursor = new();')
 s=base_conv(s).replace('VGPathFlatten&','VGPathFlatten').replace('VGDashPattern&','VGDashPattern').replace('VGDashPatternState&','VGDashPatternState').replace('VGDashContourCursor&','VGDashContourCursor')
 s=s.replace('Values.Size()','((ulong)Values.Length)').replace('Values.IsEmpty()','Values.IsEmpty')
 s=re.sub(r'Values\[([^]]+)\]',r'Values.Span[(int)(\1)]',s)
 s=s.replace('(SourceCount & 1u) ?', '(SourceCount & 1u) != 0 ?').replace('if (SourceCount & 1u)','if ((SourceCount & 1u) != 0)')
 s=re.sub(r'return\s*\{([^{}]*)\};',lambda m:'return new VGDashSample('+m[1].strip().rstrip(',')+');',s)
 for t in ['VGDashBuilder','VGDashNeedTest','VGDashContourCursor']:
  s=re.sub(t+r' (\w+)\{([^}]+)\};',lambda m:t+' '+m[1]+' = new('+m[2].strip().rstrip(',')+');',s)
 return s
out=['// Source: src/vg/vg_path_flatten.dash.cpp @ 611561f8.','using System.Diagnostics;','namespace SkrGui;', 'internal struct VGDashReserveEstimate { public ulong NodeCount,ContourCount; }', 'internal struct VGDashSample(Offsetf position,EVGPathFlattenNodeFlags flags) { public Offsetf Position=position; public EVGPathFlattenNodeFlags Flags=flags; }']
headers={
'VGDashPatternState':['internal sealed class VGDashPatternState','{','    public ReadOnlyMemory<float> Values; public ulong SourceCount,VirtualCount,NonZeroOnCount,NonZeroOffCount; public float PatternLength;'],
'VGDashCursor':['internal sealed class VGDashCursor','{','    public VGDashPatternState Pattern=null!; public ulong Index; public float Remaining;'],
'VGDashContourCursor':['internal sealed class VGDashContourCursor','{','    public readonly VGPathFlatten Flatten; public readonly VGPathFlattenContour Contour; public uint SegmentCount,SegmentLocalIndex; public float SegmentStartDistance,SegmentEndDistance;', '    public VGDashContourCursor(VGPathFlatten flatten,VGPathFlattenContour contour) { Flatten=flatten;Contour=contour;SegmentCount=contour.Closed?contour.NodeCount:contour.NodeCount-1;Reset(); }'],
'VGDashBuilder':['internal sealed class VGDashBuilder(VGPathFlatten flatten,VGPathFlatten output,VGDashPatternState pattern,float offset)','{','    public readonly VGPathFlatten Flatten=flatten,Output=output; public readonly VGDashPatternState Pattern=pattern;public float Offset=offset;'],
'VGDashNeedTest':['internal sealed class VGDashNeedTest(VGPathFlatten flatten,VGDashPatternState pattern,float offset)','{','    public readonly VGPathFlatten Flatten=flatten;public readonly VGDashPatternState Pattern=pattern;public float Offset=offset;'],
'VGPathFlatten':['public sealed partial class VGPathFlatten','{']}
for owner,header in headers.items():
 out+=header
 for ret,cls,name,args,body,line in fs:
  if cls!=owner:continue
  out += [f'    // Original line {line}: {name}.','    public '+ret.replace('uint32_t','uint').replace('uint64_t','ulong')+' '+pascal(name)+'('+conv(args).strip()+')']+['    '+row for row in conv(body).splitlines()]
 out+=['}']
(ROOT/'libraries/SkrGui.Core/Vg/VgPathFlatten.Dash.cs').write_text('\n'.join(out)+'\n',encoding='utf-8');print(len(fs),'dash algorithm bodies')
