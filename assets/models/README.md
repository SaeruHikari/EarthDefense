# Earthward PBR assets

Original assets authored for this project. The shipped geometry, surface maps and HDR orbital reflection lighting are retained as editable source assets; no external aircraft or factory asset pack is required.

Runtime fleet scenes use standard Godot ORMMaterial3D with vertex albedo, authored UVs and orthogonal tangent frames. Texture atlas channels follow glTF metallic-roughness conventions: ORM R=occlusion, G=roughness, B=metallic. Normal maps use tangent space. Emission maps contain calibrated colored radiance encoded for color sampling.

Six fleet .tscn scenes retain live Godot engine exhaust materials. Their .glb companions contain the rigid physically based surfaces for editing in other 3D software. Three factory .res files contain cached rigid hulls; their .glb companions are editable rigid assets. Current runtime models are the packed scenes in `assets/managed`; `src/Rendering/FacilityVisual.cs` animates their door and production components. Edit the packed scenes in Godot or the retained GLB companions in a modeling application.

Surface-map authoring: `tools/generate_pbr_atlas.py`. The one-time GDScript migration generators have been retired after the C# migration; their historical source remains in the protected development backup. `manifest.json` records model triangle counts and rigid draw counts. Validate geometry changes with `tests/managed_combat_assets.tscn` and `tests/managed_rendering.tscn` so muzzle and hangar-clearance contracts remain valid.