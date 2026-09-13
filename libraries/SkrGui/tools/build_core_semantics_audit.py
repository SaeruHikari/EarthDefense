from pathlib import Path
import json, hashlib, re

ws=Path('.')
prefix='engine/modules/gui/core/'
inventory=json.loads((ws/'migration/source-inventory.json').read_text())
source_root=Path(inventory['source_root'])
files=[f for f in inventory['files'] if f['path'].startswith(prefix+'include/') or f['path'].startswith(prefix+'src/')]
sources={f['path']:f for f in files if Path(f['path']).suffix in ('.hpp','.cpp')}
mapped={}
for path,key in [('migration/structure-source-map.json','files'),('migration/root-source-map.json','entries'),('libraries/SkrGui.Core/Text/source-map.json','entries')]:
    for entry in json.loads((ws/path).read_text())[key]:
        source=entry['source']
        if not source.startswith('engine/'): source=prefix+source
        if source in sources:
            mapped.setdefault(source,[])
            for target in entry.get('targets',[]):
                if target not in mapped[source]:mapped[source].append(target)

extra={
 'include/SkrGuiCore/basic_types.hpp':['Math/BasicTypes.cs'],
 'include/SkrGuiCore/math.hpp':['Math/BasicTypes.cs'],
 'include/SkrGuiCore/batch/batch_cmd.hpp':['Batch/BatchCmd.cs'],
 'src/batch/batch_cmd.cpp':['Batch/BatchCmd.cs'],
 'include/SkrGuiCore/batch/color_gradient.hpp':['Batch/ColorGradient.cs'],
 'include/SkrGuiCore/batch/mesh.hpp':['Batch/Mesh.cs','Batch/MeshBuilder.cs','Batch/BasicMeshes.cs','Batch/BasicMeshes.Generated.cs'],
 'include/SkrGuiCore/batch/paint_context.hpp':['Batch/PaintContext.cs'],
 'src/batch/paint_context.cpp':['Batch/PaintContext.cs'],
 'include/SkrGuiCore/math/transform/paint_transform.hpp':['Math/Transform/PaintTransform.cs'],
 'include/SkrGuiCore/vg/vg_basic_primitive.hpp':['Vg/VgBasicPrimitive.cs'],
 'src/vg/vg_basic_primitive.cpp':['Vg/VgBasicPrimitive.cs'],
 'include/SkrGuiCore/vg/vg_mesh_utils.hpp':['Vg/VgMeshUtils.cs'],
 'include/SkrGuiCore/vg/vg_path.hpp':['Vg/VgPath.cs','Vg/VgPath.Shapes.cs','Vg/VgPath.Flatten.cs'],
 'src/vg/vg_path.cpp':['Vg/VgPath.cs','Vg/VgPath.Shapes.cs','Vg/VgPath.Flatten.cs'],
 'include/SkrGuiCore/vg/vg_path_flatten.hpp':['Vg/VgPathFlatten.Types.cs','Vg/VgPathFlatten.Retained.cs'],
 'src/vg/vg_path_flatten.cpp':['Vg/VgPathFlatten.Retained.cs'],
 'src/vg/vg_path_flatten.dash.cpp':['Vg/VgPathFlatten.Dash.cs'],
 'src/vg/vg_path_flatten.fill.cpp':['Vg/VgPathFlatten.Fill.cs'],
 'src/vg/vg_path_flatten.stroke.cpp':['Vg/VgPathFlatten.Stroke.cs'],
 'src/vg/vg_path_flatten.stroke_contour.cpp':['Vg/VgPathFlatten.StrokeContour.cs'],
 'include/SkrGuiCore/vg/vg_utils.hpp':['Vg/VgUtils.cs','Vg/VgScalar.cs'],
 'src/visual/backend/texture.cpp':['Backend/Resources.cs'],
 'src/gui_core_module.cpp':['SkrGui.Core.csproj'],
}
for source in sources:
    rel=source[len(prefix):]
    if rel.startswith('include/SkrGuiCore/math/shape/'):
        stem=Path(rel).stem
        name=''.join(s.capitalize() for s in stem.split('_'))
        extra[rel]=[f'Math/Shape/{name}.cs']
for source,targets in extra.items():
    target=[f'libraries/SkrGui.Core/{t}' for t in targets]
    mapped.setdefault(prefix+source,[])
    mapped[prefix+source]=list(dict.fromkeys(mapped[prefix+source]+target))

def group(source):
    rel=source[len(prefix):]
    if '/text/' in rel or rel.endswith(('visual_temp_text.hpp','visual_temp_text.cpp','temp_text.hpp','temp_text.cpp')):return 'text'
    if '/framework/' in rel:return 'framework'
    if '/backend/' in rel:return 'backend'
    if '/visual/' in rel:return 'visual'
    if '/widgets/' in rel:return 'widgets'
    if '/vg/' in rel:return 'vg'
    if '/batch/' in rel:return 'batch'
    if '/shape/' in rel:return 'shape'
    if '/transform/' in rel:return 'transform'
    if '/math/' in rel:return 'math'
    return 'registration-or-aggregate'
focus={
 'framework':'Nexus ownership/lifecycle; keyed diff; BuildOwner original sorting and lock phases; virtual hooks; source requires-expression and inherited Super.',
 'visual':'Original layout/dirty propagation; const borrowed getters; failed ref outputs; defaults; source getter and setter boundaries.',
 'widgets':'All source fields/defaults, CreateVisual/UpdateVisual and Slot application; by-value input copies; language-only name collisions.',
 'math':'Concrete integer/float geometry; double HCT/M3; source constants; AwayFromZero rounding; C++ min/max NaN ordering; ref builder/const-ref semantics.',
 'shape':'Original samplers are values and evaluator receives sampler&; sample counts/extrema/splits; all 50 original shape cases.',
 'transform':'Original 3x3/4x4 matrix storage and default zeros vs explicit identity; row-vector multiplication; const borrowed values.',
 'batch':'Reference-owned commands/resources versus ordinary value records; gradient copies; default alpha; batching order and boundaries; const transform getter.',
 'vg':'Original curve/path records, flatten/fill/stroke/dash algorithms; container borrows; sampler ref state; libtess2 retained as native dependency.',
 'text':'UTF-8 byte positions and owned string snapshots; FreeType/HarfBuzz/ICU adapter boundary; readonly live styles; ref failure outputs; native lifetimes/defaults.',
 'backend':'VisualOwner scheduling and resources preserved; source descriptor defaults; only Godot renderer/platform host substitutes native backend.',
 'registration-or-aggregate':'Source include-only/forward declarations and no-op module hooks; no GUI algorithm omitted.'}
rows=[]
for source,info in sources.items():
    targets=mapped.get(source,[])
    raw=(source_root/source).read_bytes();actual=hashlib.sha256(raw).hexdigest()
    assert actual==info['sha256'],source
    for target in targets:assert (ws/target).is_file(),(source,target)
    rows.append({'source':source,'source_sha256':actual,'source_lines':info['lines'],'targets':targets,'source_group':group(source),'coverage_status':'mapped-existing-implementation' if targets else 'unmapped','audit_focus':focus[group(source)]})
missing=[r['source'] for r in rows if not r['targets']]
result={'source_commit':inventory['commit'],'scope':'Every .hpp/.cpp under core/include and core/src; file coverage plus focused source-language semantic audit, not a formal equivalence proof. Original TODO behavior remains unsupported.','file_count':len(rows),'include_headers':sum('/include/' in r['source'] for r in rows),'source_and_private_files':sum('/src/' in r['source'] for r in rows),'unmapped':missing,'files':rows}
(ws/'migration/core-semantics-audit.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'files':len(rows),'unmapped':missing},indent=2))
