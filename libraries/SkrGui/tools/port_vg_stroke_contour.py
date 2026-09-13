from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
env={"__file__":str(ROOT/"tools/port_vg_stroke.py")};exec((ROOT/'tools/port_vg_stroke.py').read_text(encoding='utf-8-sig').split("out=['// Source:")[0],env)
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/src/vg/vg_path_flatten.stroke_contour.cpp').read_text(encoding='utf-8')
block=env['block'];pascal=env['pascal'];base_conv=env['conv']
fs=[]
for m in re.finditer(r'^(?:inline )?(\w+) (StrokeContourBuilder|VGPathFlatten)::(\w+)\((.*?)\)\s*(?:const\s*)?\{',src,re.S|re.M):fs.append((m[1],m[2],m[3],m[4],block(src,m.end()-1),src[:m.start()].count('\n')+1))
env['names']={n:pascal(n) for _,_,n,_,_,_ in fs}
env['fields'] += ['source','options','point_equals_tolerance']
env['methods'] += ['nodes','contours','add_node','next_contour','close_contour','finalize','clear','reserve']
def conv(s):
 s=re.sub(r'\bout\b','Output',s).replace('&Output == this','ReferenceEquals(Output, this)').replace('*this','this')
 s=s.replace('source.nodes()','source._nodes').replace('source.contours()','source._contours')
 s=s.replace('for (const VGPathFlattenContour& contour : source._contours)','foreach (ref VGPathFlattenContour contour in source._contours.AsSpan())')
 s=base_conv(s).replace('VGPathFlatten&','VGPathFlatten')
 s=re.sub(r'StrokeContourBuilder builder\{([^}]+)\};',lambda m:'StrokeContourBuilder builder = new('+m[1].strip().rstrip(',')+');',s)
 return s
out=['// Source: src/vg/vg_path_flatten.stroke_contour.cpp @ 611561f8.','using System.Diagnostics;','namespace SkrGui;','internal enum EStrokeContourSide : byte { Left, Right }']
for owner in ['StrokeContourBuilder','VGPathFlatten']:
 if owner=='StrokeContourBuilder':out += ['internal sealed class StrokeContourBuilder(VGPathFlatten source,VGPathFlatten output,VGStrokeOptions options,float bodyRadius,float roundTolerance,uint roundDivisions)','{','    public readonly VGPathFlatten Source=source,Output=output; public readonly VGStrokeOptions Options=options; public float BodyRadius=bodyRadius,RoundTolerance=roundTolerance; public uint RoundDivisions=roundDivisions;']
 else:out += ['public sealed partial class VGPathFlatten','{']
 for ret,cls,name,args,body,line in fs:
  if cls!=owner:continue
  static=name in ['calc_round_divisions','direction_from_angle','next_node','previous_node']
  out += [f'    // Original line {line}: {name}.','    public '+('static ' if static else '')+ret.replace('uint32_t','uint').replace('uint64_t','ulong')+' '+pascal(name)+'('+conv(args).strip()+')']+['    '+row for row in conv(body).splitlines()]
 out += ['}']
(ROOT/'libraries/SkrGui.Core/Vg/VgPathFlatten.StrokeContour.cs').write_text('\n'.join(out)+'\n',encoding='utf-8');print(len(fs),'stroke-contour bodies')
