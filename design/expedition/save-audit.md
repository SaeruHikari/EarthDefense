# 二阶段远征：现有存档审计、自动保护备份与迁移方案

审计日期：2026-09-10（Asia/Shanghai，UTC+08:00）。本文件记录**已完成的保护动作**，并提出后续开发方案；本轮未实现二阶段玩法、存档库或迁移程序，未修改生产代码及 Build，也未暂停、停止、重开、读取进游戏或切换玩家当前游戏。

## 1. 已确认并保护的通关记录

已找到 **1 个真实玩家候选目录、1 份完整通关 checkpoint**。备份中 `destroyed_fronts` 恰好包含无重复的 `front_01` 至 `front_08`，与当前生产代码的胜利判定一致；`wave=125`、`completed_waves=125`、`started=true`。

**自动保护备份目录：**

`D:\MyGame\EarthDefense\saves\backups\20260910-003354-827-pre-expedition-design\`

- [备份清单 manifest.json](../../saves/backups/20260910-003354-827-pre-expedition-design/manifest.json)
- [原样 checkpoint 副本](../../saves/backups/20260910-003354-827-pre-expedition-design/profile-01/capture-01/earthward_checkpoint.json)
- [原样战斗参数副本](../../saves/backups/20260910-003354-827-pre-expedition-design/profile-01/capture-01/earthward_combat_settings.json)

此名称为本次**自动保护备份**，不是玩家命名存档。备份于本地时间 **00:33:54–00:33:55** 完成；玩家源文件仍留在原目录，并可继续被正在运行的游戏正常保存。保护对象是以下明确时间点的磁盘记录，不声称包含其后内存中的操作。

| 文件 | 源文件最后修改时间（UTC+08:00） | 原始字节数 | SHA256（副本相同） |
|---|---|---:|---|
| earthward_checkpoint.json | 2026-09-10 00:31:46.5415080 | 18,761 | `f89816a036cda95ba75393d620ed49eeeed42d4335246140b1e74bc69b363fd0` |
| earthward_combat_settings.json | 2026-09-10 00:31:01.9142256 | 805 | `5f153d2950f8ffc9bace89f49cb9f94d4f4afbfb19c153c6372ad38ad9a2ea2e` |

两份 JSON 都能解析。复制采用原始字节读取与新文件创建，没有重新排版、重编码或修改数值。复制后逐文件重新读取源 SHA256、源 mtime 和副本 SHA256；均与捕获时一致。本次第 1 次捕获即稳定，不需要重试。独立参数文件与 checkpoint 内嵌的 28 项参数逐键一致，因此本次两文件配套一致；现有程序本身没有跨两文件事务保证。

离线复核还确认：255 个槽位与 255 个方向配对，243 个已占用槽位与六种建筑数量相符；方向向量最大单位长度偏差约 `7.59e-8`，小于当前 0.01 容差；核心强化的建筑 ID、类型和正整数等级均对应存在的槽位。未启动引擎执行 `game.restore()`；这里的验证边界是原始字节、JSON、结构证据和当前保存代码审计，不能替代未来隔离环境的完整载入验收。

### 通关记录概要

| 项目 | 备份中的值 |
|---|---:|
| 已毁母舰 | 8 / 8 |
| 波次 / 已完成波次 | 125 / 125 |
| 地球生命 / 护盾 | 100 / 240 |
| 防御时间 | 6495.93792821973 秒 |
| 矿物 | 204928.897300775 |
| 能源 | 203602.924908144 |
| 科研 | 74075.8176792645 |
| 资源核心 / 外星点 | 378 / 24 |
| 击杀 / 分数 | 33493 / 1404520 |
| 科技 | 56 节点，44 个已研究，等级总和 209 |
| 矿场 / 太阳能 / 实验室 | 42 / 58 / 74 |
| 拦截机厂 / 激光机厂 / 导弹机厂 | 38 / 18 / 13 |
| 核心强化 | 实验室 ID 127：21 级；实验室 ID 128：2 级；太阳能 ID 161：53 级 |

完整科技等级、建筑计数、资源值、参数项数、进程命令行及审计代码指纹均在 manifest 中。当前工程/启动器版本为 1.15；**旧 JSON 只保存 envelope `version=2` 和 game `version=1`，没有构建版本或内容版本**，因此不能只凭这个文件断言它由某一精确 EXE 构建产生。报告中的“1.15 通关档”指本次按当前 1.15 保存/载入合同识别的真实通关记录。

## 2. 存档位置与测试档排除

真实源目录为：

`C:\Users\30453\AppData\Roaming\Godot\app_userdata\EARTHWARD · 地球守望\`

检查覆盖当前 Windows 用户的 Roaming/Local Godot `app_userdata` 中名称含 EARTHWARD 的目录，以及 `Launch.cmd` 指定的 `.runtime\AppData` / `.runtime\LocalAppData` 下相应目录。本机实际候选结果：

| 位置 | 发现 | 处理 |
|---|---|---|
| Windows Roaming EARTHWARD 目录 | checkpoint + settings，完整 8 母舰 | 两文件均原样备份为 profile-01 |
| `.runtime\AppData\Godot\app_userdata\EARTHWARD · 地球守望` | 日志/渲染缓存，无 checkpoint/settings | 记录目录存在，不制造存档 |
| Windows Local 与 `.runtime\LocalAppData` 的 Godot app_userdata | 未发现相应根目录 | 记录未发现 |
| `.runtime\integration-profile` | 老集成测试 checkpoint/settings | 排除；日志明确含 `INTEGRATION_PASS: 39 checks ... save/load and restart.` |
| `.runtime-tests` | 测试沙盒 | 明确排除，未扫描、未备份其内容 |

另对 `.runtime` 做了按两个存档文件名的发现检查，仅命中上述 `integration-profile` 测试文件；未把基准/回归目录当作玩家档。没有扫描其它 Windows 用户、浏览器或无关目录。

`Launch.cmd:4–5` 将 APPDATA/LOCALAPPDATA 重定向到项目 `.runtime`；直接从编辑器运行则使用其继承的用户目录。审计看到两个 `--editor` 进程（PID 42916、48404），以及编辑器子游戏进程 PID 43732，命令行指向本项目 `res://main.tscn`，没有发现独立 Earthward.exe。这与当前真实用户 Roaming 目录及其最新保存时间相符。命令行本身不证明整个进程环境，实际文件与内容是本次确认的主要证据。

**后续载入 UI 必须显示存档实际目录/档案来源。** 同一工程的 Launch 与编辑器当前可能显示不同“继续游戏”进度；迁移器应发现并列出两处真实候选，不能以最新文件名覆盖另一处，也不能把测试沙盒加入自动发现。当前真实通关档在 Roaming，而 Launch 指向 .runtime 且没有 checkpoint，所以用户下次改用 Launch 可能误以为进度丢失；本轮没有偷偷同步这两个活跃路径。后续应统一 profile 选择：提供明确的“使用/导入已发现真实存档”，在新库创建带来源的独立条目，记录所选 profile；启动器和编辑器使用同一选择合同。导入不覆盖任何原位置，存在多份时让玩家按通关概要/时间选，不能仅凭路径或最新时间擅自合并。

## 3. 当前保存合同与恢复边界

依据 [main.gd](../../scripts/main.gd) 的 `_save_if_safe`（855）、`_save_checkpoint`（859）、`_load_checkpoint`（867）、`_validate_checkpoint`（905），以及 [game_state.gd](../../scripts/game_state.gd) 的 `serialize`（477）、`restore`（491）：

- 文件固定为 `user://earthward_checkpoint.json` 和 `user://earthward_combat_settings.json`，每类一个文件，没有命名槽、历史索引、修订号或校验和。
- checkpoint 外层保存 `version/game/slots/site_directions/started/invasion_anchor/destroyed_fronts`。game 保存资源、HP/护盾、波次/完成波次、击杀/分数、防御时间、核心/外星点、建筑、科技、战斗参数、按建筑 ID 索引的核心强化。
- 普通 `_save_if_safe()` 仅在非战斗、未失败、允许覆盖时保存；完成一波、全部母舰摧毁会直接保存。全部母舰摧毁时先设 `campaign_won=true`、关闭自动下一波，再存 checkpoint。
- `campaign_won` 没有独立写入 JSON，载入后从合法 `destroyed_fronts.size()==8` 推导；旧档缺 `destroyed_fronts` 时按 `[]`，不能因高波次或满科技推定通关。
- `_validate_checkpoint()` 校验版本、槽位与数量、单位方向、母舰 ID、强化 ID/kind 等；`DefenseState.restore()` 先校验/构造临时字典，再一次性赋值。非法输入不会在该 state 对象上留下部分恢复结果。
- **内存状态恢复的原子校验不等于磁盘写入原子性。** 两文件当前都直接 `FileAccess.WRITE` 覆盖，没有“临时文件 → 校验 → 提交”流程；磁盘错误时只返回，尚无备份回退或完整错误呈现。
- 载入 checkpoint 会调用 `battle.reset_battle()`；它清除单位、弹体、波次窗口、工厂计时/活动、目标索引、特效和 EMP 冷却，再重建机群/预览。载入时战略相机也复位。普通已开始但未胜利的存档恢复为 16 秒补给后下一波；胜利档 `next_wave=-1`。
- 配套 preferences 主要服务新局参数；读取 checkpoint 后其内嵌参数会写回 preferences。未来导入必须明确以 checkpoint 参数为历史战役合同，不能悄悄套用电脑当前 preferences。

| 可恢复 | 未保存，不能承诺原样恢复 |
|---|---|
| 地球资源、护盾/HP、科技、已建设施及球面位置、核心强化 | 每架机的 UID、位置/速度/机头、HP、返修/殉爆状态与任务 |
| 已完成波次、历史击杀、分数、防御时间 | 工厂剩余制造时间、舱门/起飞/维修进度、飞机编制中的具体个体 |
| 固定入侵锚点、已摧毁母舰集合 | 尚存母舰的部分损伤、普通敌群、导弹/子弹/激光、命中事件 |
| 战役参数 | 当前刷怪窗口的 elapsed/spawned/planned 快照、战斗 RNG、效果计时 |
| 是否已开始、通关资格的可推导证据 | 精确暂停/UI/相机、第一人称观战目标、EMP 冷却、补给倒计时 |

因此现档应称为**战略 checkpoint / 结算点存档**。即使当前胜利档能够继续建设，也不能把同一 schema 包装成任意战斗时刻的完整快照。

## 4. 二阶段存档库：建议数据结构

以下全部属于开发提案，本轮没有创建这些运行时目录或 schema 文件。

建议正式 SaveRepository 使用 `user://dev_save_library/`，与 `res://` 游戏资源分离；主索引可重建，原始导入与玩家命名槽分开：

```text
user://dev_save_library/
  index.json                         # 索引；损坏可扫描 committed manifest 重建
  imports/<import_id>/               # 原始导入字节，永不覆写、永不自动清理
    source-checkpoint.json
    source-settings.json
    manifest.json                    # 来源、捕获时间、hash、通关证据
  campaigns/<campaign_id>/
    slots/<slot_id>/
      revisions/<revision_id>/       # 每次提交的新快照
        campaign.json
        manifest.json
      current.json                   # 指向最后已提交且校验成功的修订
  recovery/                          # 可诊断的未提交事务；不自动载入
```

`save_id/slot_id/campaign_id/revision_id` 使用稳定唯一标识，不能把用户输入的存档名直接作为路径。界面名支持玩家自定义，与路径分离。每个存档需显示名称、阶段、实际游戏时长、时间、母舰进度/战区、版本和恢复点类型；重命名只改展示元数据，不重写世界状态。

建议 envelope 的核心字段如下；确切 schema 版本号在开发时冻结，不能与旧 `version=2` 混用：

```json
{
  "format": "earthward.save_library",
  "schema_version": 1,
  "revision_id": "uuid",
  "campaign_id": "uuid",
  "slot_id": "uuid",
  "display_name": "玩家可命名；未命名时自动生成",
  "build_version": "具体构建号",
  "content_version": "科技/地图/规则版本",
  "created_at_utc": "ISO-8601",
  "save_kind": "strategic_checkpoint",
  "parent_revision_id": "uuid-or-null",
  "origin": {
    "import_id": "uuid",
    "source_checkpoint_sha256": "原始字节哈希",
    "migration_id": "legacy_v2_to_expedition_v1"
  },
  "progression": {
    "stage1_complete": true,
    "stage2_unlocked": true,
    "telescope_researched": false,
    "stage2_started": false,
    "stage2_complete": false,
    "current_stage": "earth_defense"
  },
  "earth": {},
  "expedition": null,
  "rules_snapshot": {},
  "transaction": {"sequence": 1}
}
```

- `stage2_unlocked` 只表示具备研究二阶段内容的资格，不等于已购买望远镜、揭示星图或完成远征。迁移时 `telescope_researched=false`、`stage2_started=false`、`stage2_complete=false`；仅当六个合法、唯一战区 ID 的占领事务全部提交后，才可设置 `stage2_complete=true`。旧 `campaign_won` 只映射第一阶段完成；退出 Earth 波次调度，不得让旧 `_invasion_won` 短路新的 SectorDirector。
- `earth` 保留旧档所有可恢复字段；建筑必须有稳定 ID 与球面法向，旧槽位 ID 映射写入迁移表，核心强化不能因排序变化转移到另一栋建筑。
- `rules_snapshot` 保存战役数值规则；显示、音量、按键和渲染设置属于独立用户偏好。载入历史战役不得被最新偏好静默改写。
- `expedition` 首次为 null；玩家执行远征起航后才创建。为六战区及首站自由选择保存稳定 `sector_id`、发现/侦察/完成标记、进入序号与挑战等级、远征舰 ID/蓝图等级/编制/货舱、已扣成本/已领奖事务 ID、当前停靠点/在途目的地等。不能仅从“当前第几站”反推出地点或重复发奖。
- 科技/建筑/战区使用稳定内容 ID，显示中文名不作为存档键。内容升级使用明确 ID 映射；未知 ID 不直接丢弃，返回可理解的兼容错误并保留原件。
- 数值继续遵守有限值与范围校验；整数货币、计数和升级不接受小数或超出精确表示范围。当前上限为整数 `2^53-1`、资源 `1e300`。若后续经济需要突破该数值模型，应单独升级 schema 和显示/运算库，不在迁移中将超界值截断或变成无穷。
- 建议初版仍只承诺战略安全点：地球结算/建设后、合法出发前后、战区结算、返航入库。战斗期间点击保存须显示“最近安全点”，不能显示“已保存当前战斗”。未来若要求任意时刻恢复，另加显式 `save_kind=battle_snapshot` 与全部单位/弹体/计时/RNG/碰撞任务合同，并做确定性恢复测试。

## 5. 1.15 通关 → 二阶段：只生成新副本

迁移顺序必须固定：

1. **识别来源并保护原件。** 发现真实候选，展示来源；先保存原字节和 paired settings、hash、mtime。无论是否通关，都不改写现有 `earthward_checkpoint.json`。使用已导入 source hash 去重，不能重复授予解锁奖励。
2. **严格验证旧格式。** envelope、state、科技前置、建筑/强化 ID、方向、参数、整数/有限值全部通过。只有完整合法的 8 母舰证据才能设置 `stage1_complete=true`；缺字段仍是 `[]`，失败则中止，不能靠波次 70/125、科技满级或玩家文件名替代。
3. **在临时内存对象中构建新战役。** 原矿物/能源/科研/核心/外星点、科技、建筑及强化完整继承；保留旧地球胜利，关闭旧入侵调度。设置 `stage1_complete=true`、`stage2_unlocked=true`、`telescope_researched=false`、`stage2_started=false`、`stage2_complete=false`。只解锁入口，不自动扣望远镜/远征舰成本、不自动选择首站、不重置资源、不重放母舰击杀奖励。
4. **创建独立 slot/campaign。** 记录 parent/import hash 与迁移版本。玩家以后可以继续原版胜利世界，也可进入新远征分支，两者独立保存。旧档原件及本次保护备份永久保留，不被新自动存档滚动轮换。
5. **验证并提交新修订。** 新 schema 载入隔离对象成功后，才提交磁盘并更新索引。只有这一过程完整成功，UI 才显示迁移完成；遇到错误保留旧档及原进度，没有半解锁状态。
6. **幂等恢复。** 同一 source hash + migration_id 再次导入可定位已有分支或让玩家明确创建新分支，不能偷偷叠加资源/货物/奖励。

本次备份的 8 母舰证据符合迁移的通关资格条件，但**尚未迁移、尚未解锁实际二阶段游戏**。

## 6. 原子提交、异常回退与复原策略

### 开发时的原子规则

每个槽位单写者，提交必须携带预期的上一修订 ID，拒绝过期窗口的覆盖。先在目标同一卷的新修订临时目录写 payload → flush/close → 重读 hash 与结构 → 写 manifest → flush/close，再以同卷重命名完成提交；最后更新 `current` 指针及可重建索引。避免跨卷 move、先删旧档再写新档或复用上个 revision 文件。Windows 替换 API 的具体可靠实现需实测，不能把语言层 `flush` 宣传成绝对断电保证。

新修订提交后保留至少最后 3 个已验证修订；手工命名、阶段里程碑、原始导入和迁移前保护档不参加自动轮换。索引不是世界状态的唯一来源，必须能由 committed manifests 重建。SHA256 用来发现损坏或复制错误，不是防作弊签名，也不能证明存档来自某个未经修改的游戏构建。

### 恢复矩阵

| 异常点 | 预期行为 |
|---|---|
| 临时 payload 写到一半 / 磁盘满 | current 仍指向旧修订；新槽不出现“成功” |
| payload 完整但 manifest 未提交 | 不当作有效存档；留 recovery 供诊断 |
| 新修订已提交但索引损坏 | 扫描并校验 manifest 重建索引 |
| current 指向文件缺失 / 哈希错误 | 选择前一已验证修订；告知回退到哪个时间点 |
| migration 校验/映射失败 | 新分支不发布；旧档和原始导入不变 |
| 旧程序读取新格式 | 使用独立命名空间避免读到新格式；新程序保留原版载入出口 |
| 玩家误覆盖命名槽 | 提供历史修订恢复为新槽；不再覆盖当前槽 |
| 返回地球结算重放 | 通过任务/奖励事务 ID 拒绝重复加资源 |

### 本次备份将来如何复原

**现在不执行复原，不影响正在运行的游戏。** 需要恢复时先确认启动方式所用的真正 `user://`，将当时当前档另外备份；优先在独立用户目录加载本次副本验证资源/槽位/通关状态，再由玩家明确选择替换目标活跃档。还原时成对使用本次 checkpoint/settings，先校验上述两个 SHA256，使用临时文件+提交，不能在活跃游戏持续写盘时直接覆盖。恢复运行版本应兼容 envelope 2/game 1；原版没有记录构建号，不能自动挑选一个猜测的 EXE。

如果只想进入二阶段，应导入生成新分支，完全不需要把本次保护档覆盖回玩家正在玩的目录。本次备份位于项目盘，是独立文件副本；跨设备/离线保存可作为之后的导出功能，不把单盘副本等同于磁盘故障容灾。

## 7. 开发验收门槛

- 用本次副本的**再复制件**做隔离迁移测试，生产玩家路径和保护备份只读；测试夹具单独明确标注，不允许被自动发现列入玩家档。
- 迁移后 8 母舰、125 完成波、243 建筑、56 科技字段、209 等级总和、全部原始资源和三个强化记录逐项一致；参数 28 项不能按旧 20/21/27 项硬编码裁剪。
- 同一文件重复迁移、载入全毁胜利、开始新远征再返回、取消创建、载入旧缺省字段、非法前线/重复 ID/小数核心/未知科技等覆盖成功与失败分支。
- 在写 payload、写 manifest、更新 current、更新 index 的每个提交边界注入中断；重新打开后旧修订或新修订必须有且只有一个可恢复版本，不能出现部分扣款、部分解锁。
- 提交前后重新计算本次保护目录 checkpoint/settings SHA256，必须完全不变。任何新增玩法的存档测试不得直接调用本机玩家 `user://`。

本轮交付范围：真实通关记录的两份原始文件副本、manifest 和本文；未开发命名存档 UI、未创建远征运行时状态、未修改现有存档。
## 8. 打包隔离与原版回放构建

当前 `export_presets.cfg` 使用 `all_resources`，已有 tests/artifacts/.runtime 等排除项，但没有显式排除新增 saves/design。按本次保护要求已创建 **`saves/.gdignore`**，阻止本地玩家备份作为 Godot 资源导入；设计目录的 `.gdignore` 由主任务负责。本轮没有改 export preset、没有导入/导出或构建游戏。正式 SaveRepository 位于 `user://dev_save_library`，不得混入 `res://`，发布验收必须检查 PCK 目录中不含 `earthward_checkpoint.json`、玩家 settings、备份 manifest 或用户存档库。未来发布脚本应另加显式排除并验证导出清单，不能只依赖文件扩展名。

为保障原版回放，建议把当前 **1.15 EXE + PCK 作为同一版本对长期保留**，下一版输出新版本目录，不覆盖这两个文件。它们不是本次 checkpoint 的创建程序身份证明，特别是审计时玩家通过编辑器运行；哈希只是固定现有可用构建候选。已把下列指纹加入备份 manifest，未把合计约 570 MB 的二进制再次复制进存档备份：

| 当前构建文件 | 字节数 | SHA256 |
|---|---:|---|
| `builds/Earthward-1.15/Earthward.exe` | 104,576,512 | `6a0266cb7571aa4d437a32094acd353f020c77dcf7ff5a3305ae45d0609e5c20` |
| `builds/Earthward-1.15/Earthward.pck` | 465,618,088 | `1ac864112f60520653ff4404dfc7ffe06debbbf404bb80d25bebbd7c90e0b8d1` |

将来回放时先确认这对文件未变化，再用隔离 profile 读取保护档的再复制件；不把旧 EXE 启动到当前新战役 user:// 并任其覆盖。当前这些二进制仍在原构建目录，**记录指纹不等于已经做了独立二进制归档**。