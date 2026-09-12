# Building command thumbnails

The current construction grid uses six 256 x 192 PNG command thumbnails:
`satellite_launcher.png`, `mine.png`, `solar.png`, `interceptor.png`,
`missile.png`, and `laser.png`. The local shield uses its own command card.

`satellite_launcher.png` is a drawn launchpad-and-rocket schematic. It is not a
render or screenshot of the current seven-cell model. The current free, single
satellite launcher is the first construction option; its in-world geometry has
a central gantry and six terrain-aligned annex platforms, authored in
`src/Rendering/SatelliteLauncherModel.cs`. Its free research satellite uses a
staged launch animation before generating the default 0.08 science per second.
The orbit follows the launch site's position at radius 20.25 (height 4.25).

The original six baked images were rendered from Earthward building geometry:
`mine.png`, `solar.png`, `lab.png`, `interceptor.png`, `laser.png`, and `missile.png`.
`lab.png` is retained as a historical asset; the ground laboratory is absent
from the current construction grid. The following bake notes apply only to
those six original images, not to the drawn satellite launcher icon.

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
