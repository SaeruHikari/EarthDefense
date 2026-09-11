# Building command thumbnails

Six 256 x 192 PNG images rendered from the actual Earthward building geometry:
`mine.png`, `solar.png`, `lab.png`, `interceptor.png`, `laser.png`, `missile.png`.

- Resource buildings use the original surface construction geometry and the same
  offline mesh batching / PBR atlas path used by planet construction.
- Aircraft factories use the original factory prototypes, including shipped hull
  meshes, real sliding-door assemblies and the production indicator.
- No geometry, silhouette, colors or equipment were invented for these icons.
- All images use a consistent orthographic three-quarter camera, a restrained
  key/fill light setup, the game's HDR reflection environment, and a clean
  dark background. Model bounds determine framing so each building remains
  large and legible at small command-panel tile sizes.
- Godot Forward+ / Vulkan renders a 768 x 576 isolated SubViewport with 8x MSAA
  and FXAA, then downsamples to 256 x 192 using Lanczos. The final bake does not
  use a ground plane, screen-space glow, a full game scene or runtime capture.

These PNGs are retained offline source assets. The one-time pre-C# thumbnail
baker is retired. Current building geometry lives in `assets/managed/factories`
and is animated by `src/Rendering/FacilityVisual.cs`; preserve the recorded
orthographic framing and lighting when authoring replacement images.

Render settings and geometric bounds are recorded in the baker / manifest.
The inspected six-image sheet is `artifacts/build-icons-contact.jpg`.
Final bake log: `artifacts/build-icons-bake.log` (`BUILD_ICONS_PASS`).

These are offline UI assets; using them adds no 3D render passes at runtime.
