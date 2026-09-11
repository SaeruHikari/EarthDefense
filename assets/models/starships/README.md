# 星舰模型 / 1.18

> Historical 1.18 asset specification. The old GDScript authoring/runtime interfaces have been retired. Packed meshes, textures and thumbnails remain; the current C# game loads the retained silo scene through `FacilityVisual`. Expedition gameplay is currently disabled.
本目录是项目原创程序几何的离线烘焙网格，没有导入 EVE 游戏网格或贴图。

## 美术参考

外观偏好不存在权威统一排名。本次选择社区反复提及的 Oracle 中型舰和 Nyx 旗舰作为轮廓参考，分别映射为曙光级驱逐舰、星穹级母舰。游戏内舰种定位及尺寸沿用本项目规则。

- 官方 Oracle 介绍：https://www.eveonline.com/news/view/ship-troduction-the-amarr-oracle
- 官方 Oracle 舰体渲染：https://images.evetech.net/types/4302/render?size=1024
- 官方 Nyx 舰体渲染：https://images.evetech.net/types/23913/render?size=1024
- 官方 Nyx 外观介绍：https://www.eveonline.com/news/view/federation-day-nyx-skin-now-available
- 玩家外观讨论：https://www.reddit.com/r/Eve/comments/1gw0gdo （Nyx 获较多赞同）
- 玩家外观讨论：https://www.reddit.com/r/Eve/comments/oa5h4t （Oracle 和 Nyx 均反复出现）
- 官方论坛外观讨论：https://forums.eveonline.com/t/best-looking-ship-in-eve-online/429567?page=3

已实际查看官方渲染。参考页面截图仅保存在 artifacts/starship-oracle-reference.png 和 artifacts/starship-nyx-reference.png，不作为游戏贴图。实现保留 Oracle 修长白金双脊、内凹轴向结构、侧置重炮，以及 Nyx 中央环形开孔、宽翼、多层长轴、集束推进器这些有辨识度的轮廓；配色与细部统一到 EARTHWARD 的金属、深蓝和青色光源。

## 接口及尺度

- 所有物理舰体最长轴严格归一化为 **1.0**，中心归零。前方 **-Z**，上方 **+Y**。
- `reference_length(kind)` 返回 1.0，世界层按当前拦截机实际包围盒乘 5 / 8 缩放。
- 每舰 3 个共享网格组：PBR 舰体、导航/喷口灯、透明尾焰。`set_engine_power(node,power)` 使用实例 shader 参数，避免复制材质。
- 驱逐舰总计 17,784 顶点 / 5,928 三角面；母舰 49,008 顶点 / 16,336 三角面。网格在同类舰只之间共享。

## 发射井


- `configure(materials={})` 创建默认建筑，向上为 +Y，名义占地半径 **1.0**（默认十九格地基约 0.96）。固定设备顶高约 0.62，完全开启的翻门顶高约 0.90。
- `configure_footprint(cells)` 接收 19 个当地坐标 `PackedVector3Array` 格子边界，可将十九块地基对齐到实际球面六边形地块。建筑机械部件保持相同尺度。
- `set_launch_progress(0..1)` 表示六瓣舱门开度，0 关闭、1 向外翻开 107°。净开口半径约 0.6174，按实测十九格半径 0.786 缩放后净开口直径约 0.971 世界单位。动画周期由世界层控制。
- `update_visuals(delta)` 驱动服务雷达待机旋转。开放井口有真实下沉内壁、底部电梯、环形导向灯，外围六个服务岛包含冷却片、压力罐、滑轨和交替龙门架。

## 输出与验证

- Godot 原生 Forward+ 模型展示：`artifacts/starship118-model-review.png`。
- UI 缩略图：`assets/ui/starship_destroyer.png`、`assets/ui/starship_carrier.png`（384×220）；`assets/ui/build_icons/starship_silo.png`（256×192）。由真实模型渲染生成。
