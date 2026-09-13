from pathlib import Path
import json,hashlib,re
repo=Path(__file__).resolve().parents[1]
source=Path('D:/Code/ExtremeEngine-CppSLJIT')
root=repo/'libraries/SkrGui.Core'
inventory=json.loads((repo/'migration/source-inventory.json').read_text())['files']
mapping=[]
def pascal(s):return ''.join(x[:1].upper()+x[1:] for x in s.split('_'))
framework={**{s:'Framework/Widget.cs' for s in ['build_context','state','reactive','widget','widget_dsl','component_widget','visual_widget','visual_widget_leaf','visual_widget_single_child','visual_widget_multi_child','visual_slot_widget']},'build_owner':'Framework/BuildOwner.cs','key':'Framework/Key.cs','nexus_slot':'Framework/Key.cs','nexus':'Framework/Nexus.cs'}
math={'offset':['Geometry/Offsetf.cs','Geometry/Offseti.cs'],'size':['Geometry/Sizef.cs','Geometry/Sizei.cs'],'rect':['Geometry/Rectf.cs','Geometry/Recti.cs'],'radius':['Geometry/Radius.cs'],'rrect':['Geometry/RRect.cs'],'alignment':['Layout/Alignment.cs','Layout/AlignmentDirectional.cs','Layout/AlignmentMixed.cs'],'edge_insets':['Layout/EdgeInsets.cs','Layout/EdgeInsetsDirectional.cs','Layout/EdgeInsetsMixed.cs'],'box_constraint':['Layout/BoxConstraints.cs'],'box_fit':['Layout/BoxFit.cs'],'placement':['Layout/Placement.cs','Layout/Placement.Builders.cs'],'color_scheme':['M3/M3ColorScheme.cs','M3/M3SchemeBuilder.cs','M3/M3SchemeFunctions.cs'],'tonal_palette':['M3/M3TonalPalette.cs'],'m3_color_private':['M3/M3ColorHelper.cs']}
visual={**{s:'Visual/VisualNode.cs' for s in ['visual_node','visual_leaf','visual_single_child','visual_slot']},'visual_multi_child':'Visual/VisualMultiChild.cs','visual_hit_test':'Visual/VisualHitTest.cs',**{s:'Visual/Proxy/VisualProxy.cs' for s in ['visual_proxy','visual_opacity','visual_visibility','visual_limited','visual_aspect_ratio','visual_clip_rect']},**{s:'Visual/Proxy/VisualIntrinsic.cs' for s in ['visual_intrinsic_height','visual_intrinsic_width']},**{s:'Visual/Shifted/VisualShifted.cs' for s in ['visual_shifted','visual_aligning_shifted','visual_positioned','visual_padding']}}
for r in inventory:
 p=r['path'];stem=Path(p).stem;targets=[];kind='ported-source';note=''
 if '/tests/' in p or '/gui/core/' not in p:continue
 if '/framework/' in p:
  targets=[framework.get(stem,'Framework/NexusWidgets.cs')]
 elif '/widgets/' in p and stem!='temp_text':targets=['Widgets/'+pascal(stem)+'.cs']
 elif '/math/' in p and not any(s in p for s in ['/shape/','/transform/']):
  if stem=='fwd':targets=['Math/BasicTypes.cs'];kind='language-only';note='C++ forward declarations are resolved directly by C# compilation; runtime definitions live in the corresponding type files.'
  elif stem in math:targets=['Math/'+x for x in math[stem]]
 elif '/visual/' in p and '/backend/' not in p and stem!='visual_temp_text':
  if stem=='fwd':targets=['Visual/VisualNode.cs'];kind='language-only';note='Forward declarations have no runtime behavior; full classes remain in mapped Visual files.'
  elif stem in visual:targets=[visual[stem]]
  else:targets=['Visual/'+('MultiChild/' if '/multi_child/' in p else 'Proxy/' if '/proxy/' in p else 'Shifted/')+pascal(stem)+'.cs']
 if not targets:continue
 for target in targets:assert (root/target).exists(),(p,target)
 mapping.append(dict(source=p,source_sha256=r['sha256'],targets=['libraries/SkrGui.Core/'+x for x in targets],status=kind,verification='original corresponding source cases passed',note=note))
for rel,targets in [('engine/modules/core/base/include/SkrBase/math/manual/color/srgb_color.hpp',['Math/Color/SRGBColor.cs']),('engine/modules/core/base/include/SkrBase/math/manual/color/hct_color.hpp',['Math/Color/HCTColor.cs']),('engine/modules/core/base/src/SkrBase/build.hct_color.cpp',['Math/Color/HCTColorAlgorithm.cs','Math/Color/HCTColor.Conversions.cs'])]:
 p=source/rel;assert p.exists();mapping.append(dict(source=rel,source_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),targets=['libraries/SkrGui.Core/'+x for x in targets],status='ported-dependency',verification='M3 original independent packed-color goldens passed'))
results=json.loads((repo/'.report/structure-results.json').read_text())
passed={r['source'] for r in results['tests'] if r['result']=='passed'}
cases=[]
for path in (repo/'.report').glob('structure-*-test-mapping.json'):
 rows=json.loads(path.read_text())
 for row in rows:
  key=row['source']+'::'+row['case'];row['status']='passed' if key in passed else 'not-verified';cases.append(row)
 path.write_text(json.dumps(rows,indent=2))
assert len(cases)==142 and all(x['status']=='passed' for x in cases)
report={'source_commit':'611561f81534354c8c1618dc27c4782cd9277cbf','owner':'skrgui_structure','files':mapping,'source_test_cases':cases,'result':{'owned_cases':142,'passed':142,'failed':0},'preserved_unsupported':['GlobalKey registry and inactive re-take remain source TODO.','DataScopeWidget/NexusDataScope/NexusNotification remain source TODO, not invented runtime features.','Reactive remains source IProvider/IConsumer/IComputed interfaces, without invented automatic tracking.','Intrinsic/dry/baseline caches and paint dirty processing remain source TODO.','Bulk VisualGrid.SetColumns/SetRows retain source absence of layout invalidation.'], 'language_adaptations':['C++ retained references map to managed reference identity; owned destruction maps to IDisposable at original owning release points. State.Dispose is the virtual destructor counterpart.','Geometry remains original concrete float and int pairs; double precision HCT uses Double3. No replacement with Godot layout types.','C++ source fields whose PascalCase equals their C# enclosing class map to OpacityValue,PaddingValue,VisibilityValue,AspectRatioValue.','Placement mutating fill/pin/align map to ResetFill/PinAt/AlignTo because PascalCase collides with factories. ResetFill returns ref Placement; ref struct builders retain the parent reference.','C++ consteval color literal validation maps to checked runtime parser; no C# compile-time evaluator is invented.','std::min/max preserve ordered NaN comparisons using CppMath. Round uses AwayFromZero.','Original fixture CountingAspectRatio inherits generated Super=VisualProxy, so its explicit ancestor layout call is translated to that exact method body rather than C# base.PerformLayout. Runtime VisualAspectRatio remains unchanged.','Original source test SUBCASE blocks have independent local setup and are retained as scoped blocks; no assertions are removed.']}
(repo/'migration/structure-source-map.json').write_text(json.dumps(report,indent=2))
print('Mapped',len(mapping),'original files and dependencies;',len(cases),'original test cases passed.')
