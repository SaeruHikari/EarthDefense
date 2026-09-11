# 地球纹理来源、许可与接入说明

核对及下载日期：**2026-09-08**。本目录保存官方下载原件与供 Godot 使用的无损格式转换副本。没有对原图进行缩放、翻转、修图或重新绘制。

## Solar System Scope / INOVE

官方 [Solar Textures 页面](https://www.solarsystemscope.com/textures/) 将这些资源列为等距柱状投影的真实行星地图，并明确采用 [Creative Commons Attribution 4.0 International（CC BY 4.0）](https://creativecommons.org/licenses/by/4.0/)。此许可允许复制、改编、分享和商业使用；分发时保留作者归属、来源及许可链接，并标注修改。

| 用途 | 下载原件 | Godot 使用文件 | 尺寸 / 数据 |
|---|---|---|---|
| 白昼地表颜色 | [8k_earth_daymap.jpg](https://www.solarsystemscope.com/textures/download/8k_earth_daymap.jpg) | `8k_earth_daymap.jpg` | 8192 × 4096，RGB8 JPEG |
| 夜间城市灯光 | [8k_earth_nightmap.jpg](https://www.solarsystemscope.com/textures/download/8k_earth_nightmap.jpg) | `8k_earth_nightmap.jpg` | 8192 × 4096，RGB8 JPEG |
| 云层覆盖 | [8k_earth_clouds.jpg](https://www.solarsystemscope.com/textures/download/8k_earth_clouds.jpg) | `8k_earth_clouds.jpg` | 8192 × 4096，RGB8 JPEG；三个通道数值相同 |
| 地形切线法线 | [8k_earth_normal_map.tif](https://www.solarsystemscope.com/textures/download/8k_earth_normal_map.tif) | `8k_earth_normal_map.png` | 8192 × 4096，RGB8；TIFF 原件保留 |
| 海陆反射遮罩 | [8k_earth_specular_map.tif](https://www.solarsystemscope.com/textures/download/8k_earth_specular_map.tif) | `8k_earth_specular_map.png` | 8192 × 4096，RGB8；TIFF 原件保留 |

建议随游戏保留的归属文字：

> Earth texture maps by Solar System Scope / INOVE. Licensed under CC BY 4.0. Normal and specular maps were converted losslessly from TIFF to PNG; pixel values were preserved.

源文件像素保持不变；游戏中的渲染改编包括 PBR 明暗、海水颜色与粗糙度调整、云层阴影，以及夜间灯光的暖色处理。

游戏中的渲染改编包括 PBR 光照、海水颜色与粗糙度调整、云层投影和夜灯暖色处理；这些处理在材质着色器中完成，原始地图文件的像素保持不变。

这套地图的官方说明提到，其地球图像整合了地理数据和 NASA Blue Marble 等影像，并为可视化做过色彩整理。它是地球可视化素材，不代表本游戏制作方重新拍摄的卫星影像。

## NASA Earth Observatory / GEBCO 高程

官方 [Blue Marble: Next Generation — Topography and Bathymetry Maps](https://science.nasa.gov/earth/earth-observatory/blue-marble-next-generation/topography-bathymetry-maps/) 提供 5400 × 2700 的全球地形图，并注明高程数据映射范围为 **0–6400 米**。本项目从高程数据生成顶点起伏，地表颜色不用于推测山体高度。

| 文件 | 来源与处理 |
|---|---|
| `earth_height_5400.tif` | [NASA 官方 GeoTIFF 原件](https://assets.science.nasa.gov/content/dam/science/esd/eo/images/bmng/topography/gebco_08_rev_elev_5400x2700.tif)，5400 × 2700，8 位单通道 |
| `earth_height_5400.png` | 上述 GeoTIFF 的无损 PNG 副本，推荐用于 Godot；全部像素与 TIFF 一致 |
| `earth_height_5400.jpg` | [NASA 官方 JPEG 原件](https://assets.science.nasa.gov/content/dam/science/esd/eo/images/bmng/topography/gebco_08_rev_elev_5400x2700.jpg)，保留作原始参考 |

高程影像归属：**Jesse Allen / NASA Earth Observatory**；底层地形数据：**GEBCO，British Oceanographic Data Centre**。

NASA 高程资源按 [NASA Earth Science FAQ 的影像使用说明](https://science.nasa.gov/earth/faq/)及 [NASA 媒体使用指南](https://www.nasa.gov/nasa-brand-center/images-and-media/)使用。官方来源页未标注另行限制的第三方图像版权。GEBCO 的 [官方使用条款](https://www.gebco.net/data-products/gridded-bathymetry/terms-of-use)将网格及衍生信息产品置于公共领域，允许商业使用，并要求说明来源、避免误述或暗示机构背书。上述机构提供了数据来源，未参与或背书本游戏。

## 地理方向与材质数据

- 所有地图均为 2:1 的经纬度展开图。采用 `u = (longitude + 180) / 360`、`v = (90 - latitude) / 180`：左上角为西经 180°、北纬 90°；格林尼治经线在图像中线，北极在上，南极在下。
- NASA GeoTIFF 元数据明确为 **WGS 84 / EPSG:4326**，左上角坐标 `(-180, +90)`，像素步长约 `0.0666666°`。太平洋、撒哈拉、亚马逊、格林尼治、青藏高原和澳大利亚的像素抽样也与该方向一致。
- Specular 图中海洋为白色、陆地为黑色，可作为水体反射遮罩。Cloud 图的三个 RGB 通道逐像素一致，可取单通道构造云层覆盖度。
- 法线原件的 R 范围为 `14..242`、G 为 `6..237`、B 恒为 `255`。读取后应对 `RGB * 2 - 1` 进行归一化。
- 对 **239,276 个陆地坡度样本**的独立比较显示：R 与向东高程梯度的相关系数为 `-0.8634`，G 与向南高程梯度为 `-0.8501`。据此推断，该图的 R 正向为东，G 正向为南，即随图像 v 增长。此方向是数据验证结果，官方页面没有单独声明法线约定。使用东向 tangent、南向 bitangent 时保留 G；若接收方 bitangent 指北，则需要在着色器中反转 G。
- Day / Night 作为颜色纹理读取；Normal / Specular / Height / Cloud 作为数值数据读取。生产导入开启 mipmaps，使高分辨率纹理在缩小时稳定。原始文件保持不变，PNG 转换和 Godot mipmap 导入均不包含人工图像修改。

## 完整性与复现

`ASSET_MANIFEST.json` 记录每个文件的实际 URL、字节数、SHA-256、尺寸、通道和转换验证；`MAP_VALIDATION.json` 保存地理抽样、GeoTIFF 元数据与法线方向统计。

- `artifacts/prepare_earth_assets.py`：下载原件，生成 PNG 副本，并逐像素比较转换前后的解码数据。
- `artifacts/validate_earth_maps.py`：执行只读的地理方向、通道与法线梯度验证；临时数值分析不覆盖生产图像。
