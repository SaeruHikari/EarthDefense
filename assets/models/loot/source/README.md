# Enemy loot pickup models

Six original models authored and exported through Blender 5.0.1. Game-ready
GLBs are in the parent directory, `assets/models/loot/`. Each GLB
contains one mesh with four opaque PBR material surfaces, no textures, no
animation, no lights, and no presentation props. The mesh has an identity
transform, an origin at the center of its bounds, and a maximum vertex radius
of **0.48 world units**. glTF and Godot use **+Y up**.

| Resource | Export | Silhouette | Triangles |
| --- | --- | --- | ---: |
| Minerals | [loot_minerals.glb](../loot_minerals.glb) | Cyan crystal cluster in a hexagonal cradle | 184 |
| Energy | [loot_energy.glb](../loot_energy.glb) | Amber armored cylindrical cell | 544 |
| Research/science | [loot_science.glb](../loot_science.glb) | Blue faceted nucleus with three orbital rings | 812 |
| Resource core | [loot_resource_cores.glb](../loot_resource_cores.glb) | Orange cube suspended in a titanium cage | 876 |
| Alien technology points | [loot_alien_points.glb](../loot_alien_points.glb) | Violet triangular seal with three projecting thorns | 292 |
| Alien chip | [loot_alien_chips.glb](../loot_alien_chips.glb) | Green circuit wafer with eight silver contacts | 716 |

All models use a dark metallic frame, satin titanium edges, a colored alloy,
and a colored luminous inlay. Inlay emission strength is deliberately **0.38**;
pickup halos and bloom remain controlled by the game renderer. All geometry
is opaque so rendering does not depend on transparent material ordering.

[loot_pickups.blend](loot_pickups.blend) contains six independent editable source scenes and a
separate presentation scene. [loot_contact_sheet.png](loot_contact_sheet.png) is rendered from that
presentation scene using Blender Cycles. Presentation cards, typography,
lights, and cameras are excluded from the GLB files.

This `source/` directory contains `.gdignore`, so Godot does not import the
editable Blender library, preview image or presentation scene. Rebuilding
preserves that separation: the six parent GLBs become game resources, together
with the separate transparent HUD icons described below.

Rebuild from the project root:

```powershell
& 'D:\Program Files\Blender Foundation\Blender 5.0\blender.exe' --background --factory-startup --python 'tools\build_loot_models.py'
```

The generator verifies each exported GLB's scene/node/mesh count, triangles,
finite vertex positions, centered bounds, and maximum radius before rendering
the preview. [manifest.json](manifest.json) records the measured bounds, sizes
and colors; model file paths are relative to this source directory.

HUD pickup-flight animations use six **192 x 192 RGBA PNGs** under
[`assets/ui/loot/`](../../../ui/loot/), named `loot_{currency}.png`. These are
Blender Cycles renders of the exact same source meshes and PBR materials as
the world pickups, with a transparent background, soft frontal lighting, no
floor or border, and a centered silhouette occupying about 78% of the image.
They are normal Godot UI assets, outside this ignored source directory.

The full rebuild also renders the HUD icons. To regenerate only those PNGs
from the existing `.blend` library while leaving all GLBs unchanged:

```powershell
& 'D:\Program Files\Blender Foundation\Blender 5.0\blender.exe' --background --factory-startup --python 'tools\build_loot_models.py' -- --icons-only
```

The icon-only pass verifies that all six GLB SHA-256 hashes stay unchanged. It
also reads each saved PNG back into Blender and checks RGBA dimensions,
transparent corners, and a longest occupied dimension between 70% and 82%.
