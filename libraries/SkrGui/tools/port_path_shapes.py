from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
src=Path('D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/src/vg/vg_path.cpp').read_text(encoding='utf-8')
names=['arc_to','elliptical_arc_to','add_circle','add_ellipse','add_superellipse','add_rrect','add_smooth_rrect']
def pascal(s):return ''.join(p[:1].upper()+p[1:] for p in s.split('_'))
methods=names+['move_to','line_to','quad_to','cubic_to','close','cursor_pos','start_point','split_to_cubic_beziers_fast','point_at_angle','normalized','is_valid','is_empty','is_finite','nearly_equal','scale_radii','rect','add_rect']
outputs=['// Source: src/vg/vg_path.cpp:176-623 @ 611561f8.','using System.Diagnostics;','namespace SkrGui;','public sealed partial class VGPath','{']
for name in names:
 m=re.search(r'void VGPath::'+name+r'\((.*?)\)\s*\{',src,re.S);start=m.end()-1;i=start+1;depth=1
 while depth:
  if src[i]=='{':depth+=1
  elif src[i]=='}':depth-=1
  i+=1
 args=m[1];body=src[start:i]
 def conv(s):
  s=re.sub(r'//[^\n]*','',s);s=re.sub(r'\bconst\s+','',s)
  s=s.replace('Optional<Offsetf>','Offsetf?').replace('point_equals_tolerance','PointEqualsTolerance')
  s=re.sub(r'!cursor\b(?!->)', '!cursor.HasValue',s).replace('*cursor','cursor.Value').replace('cursor->','cursor.Value.')
  s=s.replace('std::isfinite','float.IsFinite').replace('std::cos','MathF.Cos').replace('std::sin','MathF.Sin')
  s=s.replace('skr::math::kHalfPi','(MathF.PI * .5f)').replace('skr::math::kPi','MathF.PI')
  s=s.replace('SKR_VERIFY','Debug.Assert');s=re.sub(r'&&\s*("[^"\n]*")\s*\)',r', \1)',s)
  for f in methods:s=re.sub(r'\b'+f+r'\b',pascal(f),s)
  for f in ['x','y','left','top','right','bottom','tl_radius','tr_radius','br_radius','bl_radius','control_1','control_2','end']:s=re.sub(r'\.'+f+r'\b','.'+pascal(f),s)
  s=s.replace('::','.')
  s=re.sub(r'\bauto\b','var',s)
  s=re.sub(r'\[[^\]]*\]\s*\((.*?)\)\s*(?:noexcept\s*)?\{',lambda m:'('+m[1].replace('&','')+') => {',s,flags=re.S)
  s=re.sub(r'Offsetf (\w+)\(([^;]+)\);',r'Offsetf \1 = new Offsetf(\2);',s)
  s=re.sub(r'(?<!\w)(Offsetf|RRect)\(',r'new \1(',s).replace('new new Offsetf','new Offsetf')
  s=s.replace('RRect&','RRect').replace('Circle&','Circle').replace('Ellipse&','Ellipse').replace('Superellipse&','Superellipse')
  return s
 args=conv(args);body=conv(body)
 if name.startswith('add_'):args=args.replace('EVGPathWinding winding','EVGPathWinding winding = EVGPathWinding.CW')
 outputs += ['    public void '+pascal(name)+'('+args.strip()+')']+['    '+line for line in body.splitlines()]
outputs.append('}')
(ROOT/'libraries/SkrGui.Core/Vg/VgPath.Shapes.cs').write_text('\n'.join(outputs)+'\n',encoding='utf-8')
print(len(names),'path shape method bodies')
