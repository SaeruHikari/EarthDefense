from pathlib import Path
import re,json
repo=Path(__file__).resolve().parents[1]
source=Path(r'D:/Code/ExtremeEngine-CppSLJIT/engine/modules/gui/core/include/SkrGuiCore/math/layout/placement.hpp')
text=source.read_text(encoding='utf-8')
def pascal(x):return ''.join(w[:1].upper()+w[1:] for w in x.split('_'))
ctors={
'PinBuilder':'''internal PinBuilder(ref Placement placement,Offsetf anchorPct,Offsetf pivotOffsetPx,Alignment pivot)
{ _placement=ref placement;_placement.Anchor=new(0,0,0,0);_placement.SizeDelta=Sizef.Zero();_placement.Pivot=Alignment.TopLeft();_placement.PivotOffset=Offsetf.Zero();bool valid=anchorPct.IsFinite()&&pivotOffsetPx.IsFinite()&&float.IsFinite(pivot.X)&&float.IsFinite(pivot.Y);if(!valid){GuiAssert.Verify(valid,"Pin anchor offset and pivot must be finite");return;}_placement.Anchor=new(anchorPct.X,anchorPct.Y,anchorPct.X,anchorPct.Y);_placement.Pivot=pivot;_placement.PivotOffset=pivotOffsetPx; }''',
'AlignBuilder':'''internal AlignBuilder(ref Placement placement,Alignment alignment)
{ _placement=ref placement;_placement.Anchor=new(0,0,0,0);_placement.SizeDelta=Sizef.Zero();_placement.Pivot=Alignment.TopLeft();_placement.PivotOffset=Offsetf.Zero();bool valid=float.IsFinite(alignment.X)&&float.IsFinite(alignment.Y);if(!valid){GuiAssert.Verify(valid,"Alignment must be finite");return;}var anchor=AlignmentToAnchorPct(alignment);_placement.Anchor=new(anchor.X,anchor.Y,anchor.X,anchor.Y);_placement.Pivot=alignment; }''',
'InsetBuilder':'''private EdgeInsets _value_px;
internal InsetBuilder(ref Placement placement,Alignment pivot)
{ _placement=ref placement;_value_px=EdgeInsets.Zero();_placement.ResetFill();bool valid=float.IsFinite(pivot.X)&&float.IsFinite(pivot.Y);if(!valid){GuiAssert.Verify(valid,"Inset pivot must be finite");return;}_placement.Pivot=pivot; }'''}
out=['namespace SkrGui;','// Source: math/layout/placement.hpp nested builders; syntax-only function mapping.','public partial struct Placement','{']
mapping=[]
for cls in ctors:
 out += [f'    public ref struct {cls}','    {','        private ref Placement _placement;',ctors[cls]]
 for m in re.finditer(r'inline Placement::'+cls+r'& Placement::'+cls+r'::(\w+)\(([^)]*)\) noexcept\s*\{',text):
  fn,params=m.groups();i=m.end();depth=1
  while depth:
   if text[i]=='{':depth+=1
   elif text[i]=='}':depth-=1
   i+=1
  body=text[m.end():i-1]
  body=re.sub(r'\bconst\s+','',body).replace('*this','this')
  body=re.sub(r'SKR_VERIFY\((.*?)&&\s*u8"([^"]+)"\);',r'GuiAssert.Verify(\1,"\2");',body,flags=re.S)
  body=body.replace('std::isfinite','float.IsFinite').replace('std::isnan','float.IsNaN').replace('Placement::_alignment_to_anchor_pct','AlignmentToAnchorPct').replace('::','.')
  for f in ['anchor','size_delta','pivot_offset','pivot','left','top','right','bottom','width','height','x','y']:
   body=re.sub(r'\.'+f+r'\b','.'+pascal(f),body)
  body=re.sub(r'(?<![\w.])(Rectf|Offsetf|Sizef)\(',r'new \1(',body)
  body=body.replace('.is_finite()','.IsFinite()')
  body=re.sub(r'(?<=\d)\.f\b','.0f',body)
  out += [f'        public {cls} {pascal(fn)}({params})','        {',body,'        }']
  mapping.append({'source':'math/layout/placement.hpp','line':text.count('\n',0,m.start())+1,'function':cls+'::'+fn,'status':'translated-unverified'})
 out+=['    }']
out+=['}']
(repo/'libraries/SkrGui.Core/Math/Layout/Placement.Builders.cs').write_text('\n'.join(out)+'\n',encoding='utf-8')
(repo/'.report/structure-placement-builder-mapping.json').write_text(json.dumps(mapping,indent=2),encoding='utf-8')
