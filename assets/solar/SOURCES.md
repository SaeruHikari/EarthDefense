# Solar system assets and attribution

## Surface and cloud maps

Mercury, Venus surface, Venus atmosphere, Mars, Sun and Moon maps are by **Solar System Scope / INOVE**, distributed under [Creative Commons Attribution 4.0 International](https://creativecommons.org/licenses/by/4.0/).

Official source: https://www.solarsystemscope.com/textures/

Original downloaded JPG files are retained without rescaling, painting or recompression. See manifest.json for exact official download URLs, byte sizes, dimensions and SHA-256 hashes. Mercury, Venus surface, Mars and Moon are 8192×4096. Venus atmosphere is 4096×2048. The official filename `8k_sun.jpg` contains a 4096×2048 image; this project reports its actual 4K size and does not upscale it.

Rendering adaptations: physically lit surface material, restrained tint and roughness, color-derived shallow microdetail for Mars/Mercury, independent Venus clouds and atmosphere, Mars thin atmosphere, Sun HDR surface emission and an animated corona. These adaptations do not modify the original downloaded image files. Body distances and the Sun/Earth size ratio are compressed for playable presentation, not astronomical ephemerides.

## Actual lunar elevation

Credit: **NASA’s Scientific Visualization Studio / LRO LOLA**, CGI Moon Kit, visualization by Ernie Wright.

Source and documentation: https://svs.gsfc.nasa.gov/4720/

Original data: https://svs.gsfc.nasa.gov/vis/a000000/a004700/a004720/ldem_16_uint.tif

5760×2880 unsigned 16-bit samples, 0.5 metres per unit; sample 20000 is zero elevation relative to a 1737400-metre radius. Original TIFF is retained. The PNG conversion was verified sample-for-sample identical, but Godot’s PNG decoder would quantize it to L8. Runtime therefore uses `moon_lola_height.res`, a 32-bit floating-point height texture with complete mipmaps; its base level was verified to recover every original uint16 value exactly. See moon_lola_manifest.json and moon_lola_height_manifest.json for conversion records. This height texture drives the Moon’s geometric displacement and terrain normals. No albedo brightness is presented as lunar elevation.

NASA reuse guidance is linked from the CGI Moon Kit page. Retain the source credit with redistributed project assets.
