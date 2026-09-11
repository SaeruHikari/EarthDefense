from pathlib import Path
p=Path('design/expedition/proposal.md');s=p.read_text(encoding='utf-8')
a='当前通关等于 `destroyed_fronts`'
s=s.replace(a,'迁移字段明确分开：`stage1_complete=true`、`stage2_unlocked=true`、`telescope_researched=false`、`stage2_started=false`、`stage2_complete=false`。这里 unlocked 只代表有资格研究二阶段，既不自动扣费，也不自动揭图。只有六个合法唯一占领 ID 齐全才判 stage2_complete；旧 campaign_won 不能继续作为全游戏停止开关。\n\n'+a,1)
s=s.replace('## 11. 实施顺序与验收门槛','''## 11. 资产与表现交付清单

首发完整范围：六套独立母巢、六位独立将军；母舰三代舰体和甲板模块；三类 Mk II 舰载机、三类 Mk III 进化基础机体、三类 Mk IV 旗舰级基础机体；六组协议武器组件、发射与命中特效；木卫二、土卫六两个新天体与四个既有天体近战细化。

主协议决定较大的机翼、炮口与核心形状，辅助协议通过局部设备和可辨光效表达；不为 90 个搭配各维护一套完全独立的材质。模型采用统一尺度、炮口／引擎／升降机 socket、LOD、碰撞包围和可复用 PBR 贴图。可在 Blender 离线完成细模、法线／AO 烘焙和资产扫掠，游戏运行时只使用已经准备好的资源。

每套将军至少交付：进入、三种攻击的蓄力／发射／恢复、部件受损、低血量与败亡演出。工厂交付出厂开门／升降／离舰、返修入坞、生产工作动画；所有持续动画由统一时钟驱动。优先完成母巢与将军大剪影，再细化面板接缝和发光槽，不用高频噪声覆盖整个模型。

文字、预警和资源行保持原有简洁设计；每个危险区域同时有形状、时间与声音线索。UI 在 1024×640 到常见宽屏检查点击面积和详情边界。特殊机型与高级弹幕在缩小视角仍可分辨，血条和伤害数值不遮住将军弱点。

## 12. 实施顺序与验收门槛''')
s=s.replace('## 12. 本案最重要的验收体验','## 13. 本案最重要的验收体验')
p.write_text(s,encoding='utf-8')
p=Path('design/expedition/world-and-combat.md');s=p.read_text(encoding='utf-8').replace('撤离即使旧 run 失效','撤离即令旧 run 失效');p.write_text(s,encoding='utf-8')
print('Stage flags, asset deliverables and controller boundaries specified.')
