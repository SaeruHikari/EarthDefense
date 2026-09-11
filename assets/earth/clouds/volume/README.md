# Periodic volumetric cloud density data

This asset is generated entirely offline by `tools/bake_cloud_volume_noise.py`.
No external image or noise assets are used. Deterministic seed: 261214.

`cloud_noise_3d.res` is a native Godot `ImageTexture3D`, 96 x 96 x 96,
RGBA8, linear scalar data, no mipmaps. GPU base payload is 3,538,944 bytes
(3.375 MiB). Binary resource disk compression is Zstandard, not GPU texture
block compression. The resulting file is 3,458,682 bytes on Godot 4.6.1.

| Channel | Content | Mean / standard deviation |
| --- | --- | --- |
| R | Large volumetric body: periodic gradient Perlin fBm and rounded smooth unions of Worley feature points | 0.522 / 0.192 |
| G | Finer Worley erosion, blended at two frequencies | 0.488 / 0.194 |
| B | Independent fine Perlin/cellular detail | 0.493 / 0.190 |
| A | Independent low frequency variation / domain-warp potential | 0.506 / 0.201 |

Every channel varies independently through X, Y AND Z. R is a density shaping
field, not a surface height. All axes are periodic. Do not duplicate first and
last voxels: adjacent cell centres straddle the repeat seam at the normal
one-voxel spacing. `NOISE_STATS.json` reports seam differences relative to
ordinary interior voxel differences and channel distribution statistics.

Use a shader sampler such as:

```glsl
uniform sampler3D volume_noise : filter_linear, repeat_enable;
```

Do not add `source_color`; this is linear numeric data, not albedo. A reasonable
initial coordinate scale is planet-local 3D position times 0.8 to 1.8. The
texture itself already contains about 4-6 primary shape features per period
and 12-24 erosion features. Tune scale in conjunction with the cloud vertical
profile and ray step spacing. Excessively high frequency relative to ray
steps causes aliasing; a very large erosion coefficient produces perforated
cotton instead of solid cloud interiors. Apply strongest erosion near the
cloud boundary and retain smoothly accumulated density in its interior.

The weather map defines cloud-system coverage; radial height defines the
permitted cloud band. Sample this 3D field inside that band to vary density
with depth, rather than extruding the weather map unchanged between two
surfaces. Advect the 3D sampling frame with the same wind used by the weather
and shadow calculation.

## Rebuild

1. Run `tools/bake_cloud_volume_noise.py` with NumPy and Pillow. It writes
   `artifacts/cloud_noise_3d.rgba8`, this directory's statistics, and
   `artifacts/cloud-volume-noise-slices.png` (8 Z slices of each channel).
2. Run `powershell -ExecutionPolicy Bypass -File tools/bake_cloud_volume_texture.ps1`.
   This builds and runs the standalone C# tool scene with a real renderer,
   an isolated test profile, and an immediately hidden window. It never loads
   the game scene. `--headless` is rejected
   because Godot's Dummy renderer cannot read back ImageTexture3D data for
   ResourceSaver serialization.
3. The baker creates RGBA8 Image slices, calls ImageTexture3D.create with
   mipmaps false, and saves a native `.res` with FLAG_COMPRESS. It reloads the
   file, verifies its dimensions and an exact byte round trip for every slice,
   then exits. The saved resource needs no runtime helper script.

Verified on Godot 4.6.1, OpenGL Compatibility renderer / RTX 4090. Verification
output: the `CLOUD_VOLUME_BAKE_PASS` console message. Runtime can load the native Texture3D
in Forward+ as usual; no OpenGL dependency is embedded in the saved asset.

API references:
- https://docs.godotengine.org/en/4.6/classes/class_imagetexture3d.html
- https://docs.godotengine.org/en/4.6/classes/class_resourcesaver.html
