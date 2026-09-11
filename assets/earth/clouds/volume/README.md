# Periodic volumetric cloud density data

This asset is generated entirely offline by `tools/bake_cloud_volume_noise.py` and repacked by
`tools/bake_cloud_volume_texture.ps1`. No external image or noise assets are used.
Deterministic seed: 261214.

## Shipped texture

`cloud_noise_3d.res` is a native Godot `ImageTexture3D`, 96 x 96 x 96, **RG8**, linear scalar
data, no mipmaps. GPU base payload is 1,769,472 bytes (1.6875 MiB); the file is 1,713,055 bytes
on Godot 4.7.2 (Zstandard, not GPU block compression).

| Channel | Content |
| --- | --- |
| R | Large volumetric body, unchanged from the generator: periodic gradient Perlin fBm and rounded smooth unions of Worley feature points |
| G | The generator's finer Worley erosion and independent fine detail, folded into one channel as `(G * 0.035 + B * 0.012) / 0.047` |

The generator still writes four channels (R shape, G erosion, B detail, A low frequency
variation / domain-warp potential) and `NOISE_STATS.json` describes exactly that RGBA8 output.
The packer folds G and B into the single gain the shader applies
(`shaders/cloud_volume_density.gdshaderinc`, `volume_noise_erosion_gain = 0.047`), so the density
field is reproduced exactly up to one 8-bit step; the unused A channel is dropped. Packing is
pixel-exact: a frozen-scene A/B against the RGBA8 build differs in 11 of 3,360,000 sampled pixels,
which is the sub-quantization rounding of the fold. It halves the volume's VRAM footprint and
per-sample bandwidth. Measured cost on M3 Max: the whole level-0 3D fetch is worth 1.4-1.8 ms at
1x cloud resolution, and the byte-width reduction alone is below the 0.15 ms `--print-fps` floor,
so treat the win as memory, not frame time.

Mipmaps are deliberately not generated: `ImageTexture3D` does not survive a mipmapped save in
Godot 4.7.2 (the reloaded resource is 1x1x1 with no slices), and the volume march samples level 0
only. Re-verify before changing this.

Every channel varies independently through X, Y AND Z. R is a density shaping field, not a surface
height. All axes are periodic. Do not duplicate first and last voxels: adjacent cell centres
straddle the repeat seam at the normal one-voxel spacing. `NOISE_STATS.json` reports seam
differences relative to ordinary interior voxel differences and channel distribution statistics.

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
3. The packer folds the raw Z,Y,X RGBA8 cube into RG8 slices, calls
   ImageTexture3D.create with mipmaps false, and saves a native `.res` with FLAG_COMPRESS.
   It reloads the file, verifies its dimensions, format and an exact byte round trip for
   every slice, then exits. The saved resource needs no runtime helper script.
4. To change the generator's erosion/detail balance without re-running the Python bake, repack
   an existing volume in place with
   `-Source res://assets/earth/clouds/volume/cloud_noise_3d.res`; the weights live in
   `CloudVolumeBake.PackSlice` and must stay in sync with the shader's
   `volume_noise_erosion_gain`.

Verified on Godot 4.6.1 / OpenGL Compatibility (original RGBA8 bake) and on Godot 4.7.2 mono /
macOS Metal Forward+ (RG8 packing). Verification output: the `CLOUD_VOLUME_BAKE_PASS` console
message. Runtime can load the native Texture3D in Forward+ as usual; no OpenGL dependency is
embedded in the saved asset.

API references:
- https://docs.godotengine.org/en/4.6/classes/class_imagetexture3d.html
- https://docs.godotengine.org/en/4.6/classes/class_resourcesaver.html
