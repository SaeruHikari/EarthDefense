# Loot flight thumbnails

Six 192 x 192 transparent RGBA PNGs, rendered in Blender Cycles from the same
editable PBR meshes used by the world's 3D pickups. Names match the currency
identifiers: `loot_minerals.png`, `loot_energy.png`, `loot_science.png`,
`loot_resource_cores.png`, `loot_alien_points.png`, and `loot_alien_chips.png`.

Use these textures directly for the animation from the world pickup to the
HUD resource counter. The image already contains the full metallic frame and
colored core; avoid applying a currency tint over the entire texture. The
silhouette occupies about 78% of the square, with transparent margins and no
floor shadow or border.

The generator is `tools/build_loot_models.py`. Its `--icons-only` mode renders
these PNGs from `assets/models/loot/source/loot_pickups.blend` without changing
the GLBs. See the source library's README for the complete Blender command.
