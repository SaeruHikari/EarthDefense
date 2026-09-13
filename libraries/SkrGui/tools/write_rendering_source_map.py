from pathlib import Path
import hashlib,json,re
ROOT=Path(__file__).resolve().parents[1];ENGINE=Path('D:/Code/ExtremeEngine-CppSLJIT');core='engine/modules/gui/core/'
entries=[]
def add(source,targets,status='ported-source',note='Original source cases passed; all branch bodies retained.'):
 path=ENGINE/source
 entries.append({'source':source,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'targets':targets,'status':status,'verification':note})
batch={
'include/SkrGuiCore/batch/batch_cmd.hpp':['Batch/BatchCmd.cs'],
'src/batch/batch_cmd.cpp':['Batch/BatchCmd.cs'],
'include/SkrGuiCore/batch/color_gradient.hpp':['Batch/ColorGradient.cs'],
'include/SkrGuiCore/batch/paint_context.hpp':['Batch/PaintContext.cs'],
'include/SkrGuiCore/batch/mesh.hpp':['Batch/Mesh.cs','Batch/MeshBuilder.cs','Batch/BasicMeshes.cs','Batch/BasicMeshes.Generated.cs'],
'include/SkrGuiCore/math/transform/paint_transform.hpp':['Math/Transform/PaintTransform.cs']}
for source,targets in batch.items():add(core+source,['libraries/SkrGui.Core/'+t for t in targets])
for source,target in [('shape_sample','ShapeSample'),('shape_sampler','ShapeSampler'),('quad_bezier','QuadBezier'),('cubic_bezier','CubicBezier'),('circle','Circle'),('arc','Arc'),('ellipse','Ellipse'),('elliptical_arc','EllipticalArc'),('superellipse','Superellipse'),('superellipse_arc','SuperellipseArc')]:add(core+'include/SkrGuiCore/math/shape/'+source+'.hpp',['libraries/SkrGui.Core/Math/Shape/'+target+'.cs'])
for source,targets in {
'include/SkrGuiCore/vg/vg_utils.hpp':['VgUtils.cs'],
'include/SkrGuiCore/vg/vg_mesh_utils.hpp':['VgMeshUtils.cs'],
'include/SkrGuiCore/vg/vg_basic_primitive.hpp':['VgBasicPrimitive.cs'],
'src/vg/vg_basic_primitive.cpp':['VgBasicPrimitive.cs'],
'include/SkrGuiCore/vg/vg_path.hpp':['VgPath.cs','VgPath.Flatten.cs','VgPath.Shapes.cs'],
'src/vg/vg_path.cpp':['VgPath.cs','VgPath.Flatten.cs','VgPath.Shapes.cs'],
'include/SkrGuiCore/vg/vg_path_flatten.hpp':['VgPathFlatten.Types.cs','VgPathFlatten.Retained.cs','VgPathFlatten.Stroke.cs','VgPathFlatten.StrokeContour.cs','VgPathFlatten.Dash.cs','VgPathFlatten.Fill.cs','TessNative.cs'],
'src/vg/vg_path_flatten.cpp':['VgPathFlatten.Retained.cs'],
'src/vg/vg_path_flatten.stroke.cpp':['VgPathFlatten.Stroke.cs'],
'src/vg/vg_path_flatten.stroke_contour.cpp':['VgPathFlatten.StrokeContour.cs'],
'src/vg/vg_path_flatten.dash.cpp':['VgPathFlatten.Dash.cs'],
'src/vg/vg_path_flatten.fill.cpp':['VgPathFlatten.Fill.cs','TessNative.cs'],
}.items():add(core+source,['libraries/SkrGui.Core/Vg/'+t for t in targets])
add('engine/packages/runtime-libraries/libtess2/1.0.3/Include/tesselator.h',['libraries/SkrGui.Core/Vg/TessNative.cs','native/artifacts/win-x64/skrgui_native.dll'],'third-party-c-abi','Pinned source libtess2 ABI; custom allocator and original tessellation options verified by all original fill cases.')
render='engine/modules/gui/samples/gallery_common/'
add(render+'include/SkrGuiGalleryCommon/gallery_renderer.hpp',['libraries/SkrGui.Godot/GodotVisualBackend.cs','libraries/SkrGui.Godot/GodotRenderTarget.cs'],'godot-renderer-replacement','Actual main Vulkan RD/RTX4090: 12 explicit pixel and state probes, plus CounterDemo interaction/reset in all 4 concrete text AA modes.')
add(render+'src/gallery_renderer.cpp',['libraries/SkrGui.Godot/GodotVisualBackend.cs','libraries/SkrGui.Godot/GodotRenderTarget.cs'],'godot-renderer-replacement','Same frame draw assembly, adjacent merging, bounds scissor, raw/L8-gray views and backdrop order. GPU API/ownership replaced; see backend_contracts below.')
for file in ['include/SkrGuiGalleryCommon/gallery_window.hpp','src/gallery_window.cpp']:
 add(render+file,['godot/SkrGuiHost/SkrGuiHost.cs','godot/SkrGuiHost/project.godot','tools/LaunchSkrGui.ps1'],'godot-window-host-replacement','Window creation/events/presentation/lifetime move to Godot; concrete field/function behavior is mapped in window_contracts.')
method_records=[]
for target in sorted({t for e in entries for t in e['targets'] if t.endswith('.cs')}):
 text=(ROOT/target).read_text(encoding='utf-8-sig');lines=text.splitlines()
 for i,line in enumerate(lines):
  m=re.search(r'// Original line (\d+): (\w+)\.',line)
  if m:method_records.append({'target':target,'target_line':i+2,'source_line':int(m[1]),'source_method':m[2]})
result={'source_commit':'611561f81534354c8c1618dc27c4782cd9277cbf','owner':'skrgui_rendering','files':entries,'generated_method_locations':method_records,
'language_adaptations':[
'Public names retain original types and PascalCase members. MeshIndex remains uint; CPU transforms/sorting/geometry are not moved to a native GUI implementation.',
'BatchRoot preserves O(N^2) overlap-depth/order checks. Native pointer-address sort keys become stable managed reference identities; independent-resource physical address order has no portable C# identity.',
'SortedBatchCmd and BatchRegion are value structs; source auto& writes use ref over CollectionsMarshal.AsSpan, including VisualOwner combined index ranges.',
'Sampler caches are value structs. Mutable eval callbacks use ref TSampler; readonly sample callback values are snapshots. Source reference cache updates persist to callers.',
'PaintTransform borrowed getters and PaintContext.Transform use ref readonly. VgBuffer exposes only IReadOnlyList publicly; internal ref indexers preserve original mutable-node aliases.',
'ColorGradient boxed alternatives retain mutable AsSolid/AsAxis/AsCurve references; explicit copy constructors deep-copy source value copies. CLR default(T) differs from C++ member-initialized {}; translated source uses new T() where member defaults apply.',
'VGFillAllocator.UserData is a managed context object, with all three callbacks receiving it. A GCHandle bridges this context to pinned libtess2 native allocator ABI; default diagnostic tag preserves the source name.',
'VGPath and VGPathFlatten expose explicit deep-copy constructors corresponding to implicit C++ copies.',
'Test-only original NanoVG 0.1.0 C API is used as the independent coverage oracle; all coverage sampling, cases and assertions are C#.'
],
'backend_contracts':[
{'source':'begin_frame / end_frame','mapping':'Recording guards and retry state retained. Missing RD returns false. EndFrame always closes a valid completed recording, retains the submitted frame and strong resource snapshots. RD resource creation errors return false when executed inline and are reported via LastRenderError on a queued rendering-thread execution; the host checks that result. This asynchronous hardware-error timing is an explicit Godot-host boundary, not claimed to match CGPU synchronously.'},
{'source':'create_image_render_target / begin_render_target','mapping':'Nonpositive target size returns null. Empty viewport falls back to full target. Logic dimensions use ceil then max(1). A clear-only frame still clears with empty vertices/indices.'},
{'source':'begin_clip / set_draw_scissor','mapping':'Every clip intersects the prior logical bound. Original ClipMesh remains AABB-only. Logical bounds intersect viewport, then physical scissor; floor minimum/ceil maximum clamp to target.'},
{'source':'create_frame_resources / text_atlas','mapping':'20-byte vertices: position float2, UV float2, source RGBA-packed color converted to little-endian RGBA8. TextService+atlas id identity and generation/size/stride/format changes recreate GPU storage. R8 gray-alpha shared view maps R to all RGBA channels for direct textured/gray mode; SDF/LCD uses raw view.'},
{'source':'create_render_program_for_shader','mapping':'RGBA8 UNORM single-sample target, premultiplied ONE/ONE_MINUS_SRC_ALPHA. GrayLCD/SDFLCD preserve ONE/ONE_MINUS_SRC1_COLOR and independent alpha dual-source blend.'},
{'source':'record_backdrop_blur','mapping':'End draw pass, copy previously rendered complete background, original crop/downsample4 and H/V Gaussian compute, glass draw with original 208-byte parameter fields, resume LOAD draw pass. Original 40 bytes of blur fields use a 48-byte RD push-constant allocation because Godot rounds the SPIR-V block alignment.'},
{'source':'destroy_frame_resources','mapping':'Godot owns fences and deferred GPU deletion. Dependent vertex/index arrays and uniform sets release before buffers; invalidated texture-dependent sets are checked. Frames retain their mesh bytes/textures/text services until consumed.'},
{'source':'readback','mapping':'Original RGBA pack compute runs on the shared main RD; buffer readback completes asynchronously. No private/local RD and no GUI-to-bitmap approximation are used for drawing.'}
],
'window_contracts':[
{'source':'GalleryOptions.backend / create_swapchain','mapping':'Godot process rendering-method mobile (or Forward+) and rendering-driver vulkan select the shared device before the C# scene. Godot owns platform surface/swapchain, image count, queue/fences and recreation; no CGPU backend handles survive.'},
{'source':'GalleryOptions.window_title / window_create','mapping':'project.godot config/name plus Godot Window.Title replaces native title; viewport and window width/height overrides create the CounterDemo logical window. project.godot window/size/resizable=false establishes source is_resizable=false before native window creation, preserving the 960x720 client area. The Ready-time GetWindow().Unresizable=true assignment is then idempotent and does not resize the Win32 client area.'},
{'source':'GalleryOptions.window_scale / pixel_ratio','mapping':'Godot window/viewport resolution supplies equivalent concrete sizing. Host maps input by viewport dimensions to CounterDemo logical extent, and computes paint pixel ratio from physical target/logical extent. Generic native window_scale/pixel_ratio knobs are replaced by Godot host configuration; there is no claim that a separate CGPU GalleryOptions object is still instantiated.'},
{'source':'GalleryOptions.enable_vsync','mapping':'project.godot window/vsync/vsync_mode=0 preserves the original default false; Godot command-line/project/display server settings own alternate modes.'},
{'source':'window_current_pixel_ratio / sync_physical_size','mapping':'GetViewportRect().Size is the render target pixel extent; SizeChanged updates target size. LogicPosition converts position to CounterDemo.DemoWidth/Height using this extent; text raster pixel ratio is recalculated by the original CounterDemo render logic.'},
{'source':'GalleryWindowEventHandler.handle_event','mapping':'_Input is scoped to the Godot scene viewport/window. Pressed&&!Echo retains repeat suppression. Escape calls SceneTree.Quit; mouse move/left down/up/leave forward to the original CounterDemo handlers.'},
{'source':'pump_events / should_close / destroy','mapping':'Godot runs the event pump and close lifecycle. _Process calls the original render path; close/Escape terminates SceneTree; _ExitTree disposes demo, target and backend and releases resources on the render thread.'}
],
'verification_artifacts':['artifacts/tests/full-current.json','artifacts/tests/vg-complete.json','artifacts/tests/nanovg-coverage.json','artifacts/tests/rendering-owned.json','artifacts/gpu-probe/results.json','artifacts/godot/host-verification.json','artifacts/godot-gray/host-verification.json','artifacts/godot-gray-lcd/host-verification.json','artifacts/godot-sdf-lcd/host-verification.json'],
'limits':['Source behavioral cases and analytic pixel expectations are verified. An original CGPU gallery screenshot baseline was not executed, so cross-driver bit-exact full-image parity is not asserted.','Independent draw/layout gallery pages and developer HTML/performance/estimator tools are tracked by root separately; this map does not claim they are migrated by the rendering subagent.']}
for e in entries:
 for target in e['targets']:
  if not (ROOT/target).exists():raise FileNotFoundError(target)
(ROOT/'migration/rendering-source-map.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(len(entries),'source files;',len(method_records),'generated method locations; all targets verified')
