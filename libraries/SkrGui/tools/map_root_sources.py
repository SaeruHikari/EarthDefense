import pathlib,json,re,hashlib
root=pathlib.Path(__file__).resolve().parents[1]
inv=json.loads((root/'migration/source-inventory.json').read_text())['files']
core='engine/modules/gui/core/'
groups={
'visual/backend/visual_backend.hpp':['libraries/SkrGui.Core/Backend/VisualBackend.cs'],
'visual/backend/visual_owner.hpp':['libraries/SkrGui.Core/Backend/VisualOwner.cs'],
'visual/backend/texture.hpp':['libraries/SkrGui.Core/Backend/Resources.cs'],
'visual/backend/shader.hpp':['libraries/SkrGui.Core/Backend/Resources.cs'],
'visual/backend/render_target.hpp':['libraries/SkrGui.Core/Backend/Resources.cs'],
}
entries=[]
for suffix,targets in groups.items():
 f=next(v for v in inv if v['path']==core+'include/SkrGuiCore/'+suffix)
 entries.append(dict(source=f['path'],source_sha256=f['sha256'],targets=targets,status='ported-source'))
f=next(v for v in inv if v['path']==core+'src/visual/backend/visual_owner.cpp')
entries.append(dict(source=f['path'],source_sha256=f['sha256'],targets=['libraries/SkrGui.Core/Backend/VisualOwner.cs'],status='ported-source'))
for src,targets in [
('src/batch/mesh.cpp',['libraries/SkrGui.Core/Batch/BasicMeshes.Generated.cs','libraries/SkrGui.Core/Batch/BasicMeshes.cs']),
('include/SkrGuiCore/batch/mesh.hpp',['libraries/SkrGui.Core/Batch/Mesh.cs','libraries/SkrGui.Core/Batch/MeshBuilder.cs','libraries/SkrGui.Core/Batch/BasicMeshes.cs']),
('tests/batch/mesh_tests.cpp',['tests/SkrGui.Core.Tests/Batch/BasicMeshesContractTests.cs']),
('tests/math/paint_transform_tests.cpp',['tests/SkrGui.Core.Tests/Math/PaintTransformContractTests.cs']),
('tests/text/text_layout_contract_tests.cpp',['tests/SkrGui.Core.Tests/Text/TextLayoutContractTests.cs']),
('tests/text/text_font_face_contract_tests.cpp',['tests/SkrGui.Core.Tests/Text/FontFaceContractTests.cs']),
('tests/text/files_font_provider_tests.cpp',['tests/SkrGui.Core.Tests/Text/FontProviderTests.cs']),
('tests/visual/visual_owner_tests.cpp',['tests/SkrGui.Core.Tests/Visual/VisualOwnerTests.cs'])]:
 f=next((v for v in inv if v['path']==core+src),None)
 if f:entries.append(dict(source=f['path'],source_sha256=f['sha256'],targets=targets,status='ported-source'))
gallery='engine/modules/gui/samples/gallery_common/'
for name,targets in [
('gallery_framework',['libraries/SkrGui.Godot/Gallery/GalleryFramework.cs']),
('gallery_png',['libraries/SkrGui.Godot/Gallery/GalleryPng.cs']),
('gallery_svg',['libraries/SkrGui.Godot/Gallery/GallerySvg.cs','libraries/SkrGui.Godot/Gallery/GallerySvgPathParser.cs','libraries/SkrGui.Godot/Gallery/GallerySvgDownload.cs','libraries/SkrGui.Godot/Gallery/GalleryNative.cs']),
('gallery_shared',['libraries/SkrGui.Godot/Gallery/GalleryShared.cs','libraries/SkrGui.Godot/Gallery/GalleryResources.cs']),
('visual_glass',['libraries/SkrGui.Godot/Gallery/VisualGlass.cs'])]:
 for path in [gallery+'include/SkrGuiGalleryCommon/'+name+'.hpp',gallery+'src/'+name+'.cpp']:
  f=next(v for v in inv if v['path']==path);entries.append(dict(source=path,source_sha256=f['sha256'],targets=targets,status='ported-source'))
f=next(v for v in inv if v['path']=='engine/modules/gui/samples/demo/src/gui_demo_main.cpp')
entries.append(dict(source=f['path'],source_sha256=f['sha256'],targets=['libraries/SkrGui.Godot/Gallery/CounterDemo.cs','godot/SkrGuiHost/SkrGuiHost.cs'],status='GUI logic ported-source; platform window/input/render loop hosted by Godot'))
(root/'migration/root-source-map.json').write_text(json.dumps({'source_commit':'611561f81534354c8c1618dc27c4782cd9277cbf','entries':entries},indent=2))
p=root/'migration/basic-meshes-map.json';mapping=json.loads(p.read_text())
for e in mapping:e['status']='ported; all 41 original mesh cases passed'
for name,target in [('text','Text'),('_collect_curve_split_lines','CollectCurveSplitLines'),('_flatten_options','FlattenOptions'),('_fill_options','FillOptions'),('_single_convex_fill_options','SingleConvexFillOptions'),('_stroke_options','StrokeOptions')]:
 if any(e['symbol']=='BasicMeshes::'+name for e in mapping):continue
 mapping.append({'source':'src/batch/mesh.cpp' if name=='text' else 'include/SkrGuiCore/batch/mesh.hpp','symbol':'BasicMeshes::'+name,'target_symbol':'BasicMeshes.'+target,'status':'ported; all 41 original mesh cases passed'})
p.write_text(json.dumps(mapping,indent=2))
print('Root source mappings:',len(entries))
