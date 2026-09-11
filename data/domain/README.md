# 数值表编辑说明

正式运行时使用本目录的 UTF-8 CSV。科技、机型、建筑、特性、资源、初始基数与奖励目录不再读取旧的正式 JSON；缺表、重复 ID、无效数值、未知引用或科技循环依赖会报错，并显示文件、行号或对应 ID。修改后重新启动游戏生效。

## 常用表

| 想改的内容 | CSV |
| --- | --- |
| 科技名称、大小、分支、层级 | `deep_technology_nodes.csv` |
| 科技数值效果 | `deep_technology_nodes_values.csv` |
| 科技前置关系 | `deep_technology_nodes_requires.csv` |
| 每个科技的费用 | `deep_technology_nodes_cost.csv` |
| 科技波次和阶段门槛 | `deep_technology_nodes_unlock.csv` |
| 科技说明文字 | `deep_technology_nodes_effects.csv`、`deep_technology_nodes_effect_definition.csv` |
| 科技图位置 | `deep_technology_nodes_draw_position.csv` |
| 9 种飞机的倍率、射程、生产时长与机型能力参数 | `airframes_definitions.csv` |
| 39 种特性的描述和类别 | `perks_definitions.csv` |
| 特性适用工厂、机型 | `perks_definitions_kinds.csv`、`perks_definitions_airframes.csv` |
| 特性实际效果公式 | `perk_effect_rules.csv` |
| 基础与高级特性的升级费用/等级上限 | `perk_tiers.csv`、`perks_settings.csv` |
| 永久特性工程参数的合法范围 | `perk_setting_bounds.csv` |
| 建筑定义与造价 | `economy_buildings.csv`、`economy_buildings_cost.csv` |
| 初始设施数量 | `economy_initial_buildings.csv` |
| 参数界面的默认值、上下限和整数设置 | `economy_settings.csv`、`economy_ranges.csv`、`economy_integer_settings.csv` |
| 初始资源、武器基数、资源产出、修理、波次奖励等 | `domain_balance.csv` |
| 各工厂基础巡航半径、远端范围、速度和生命 | `patrol_bases.csv` |
| 击杀每类敌人的资源、积分、外星点和核心规则 | `kill_rewards.csv` |
| 旧存档科技继续生效的数值系数 | `legacy_effect_coefficients.csv` |
| 后继单级科技的收益、科研与外星点增长曲线 | `successor_research.csv` |
| 旧存档的四层航程基础值 | `legacy_frontiers.csv` |
| 暂停开放的远征系统参数与既有存档权益 | `expedition_*.csv` |

`combat_*.csv` 由战斗系统维护，包含敌人和波次构成等内容；其具体列见该部分说明。

## 编辑规则

- 文件编码为 UTF-8（包含 BOM，方便 Windows 表格软件识别中文）。保存时选择 UTF-8 CSV，保留逗号分隔。包含逗号、换行或双引号的单元格使用标准 CSV 引号规则。
- 小数用点，例如 `0.15`；布尔值用 `true` / `false`；不要填写百分号、千位分隔符或公式。`0.15` 表示比例 15%，具体含义以属性名称与说明为准。
- `id` 是稳定的游戏和存档 ID。`parent_id` 指向所属记录。例如科技效果表的 `parent_id=K_S01` 表示它属于 K_S01。不要只改 ID 而遗漏关联表。
- `position` 是同一父记录下从 0 开始的顺序。新增或删除条目后保持连续；它不是界面坐标。地图坐标在 `draw_position` 关系表中：位置 0 是 x，位置 1 是 y。
- 属性表一行只保存一个标量，`type` 可为 `integer`、`number`、`bool`、`string`。不支持把 JSON 或可执行代码填进单元格。
- `has_` 列表示该记录是否具有对应的子表字段。已有字段通常保留为 `true`；空关系由没有子行表示。根表和 `catalog_tables.csv`、`catalog_columns.csv` 属于结构描述，日常调数值无需改动。
- 科技属性必须出现在 `technology_attribute_schema.csv`，并由对应的战斗或领域逻辑实现。加入新名字不会自动创造新的玩法；拼错名字会被校验拒绝。
- 单级科技 `max=1`；小科技只付科研。三项容量中科技仍合计最多研究 +3；该限制是玩法规则，不会同时限制工厂基数、参数倍率和永久特性。

## 特性公式

`perk_effect_rules.csv` 按 `perk_id + attribute` 唯一匹配。一种特性可以有多行，每行输出一个属性。

- `linear`：`base + per_level × (等级 - level_offset)`。
- `reciprocal`：`1 / [base + per_level × (等级 - level_offset)]`，用于生产/锁定时间缩短。
- `floor_step`：`base + per_level × floor[(等级 - level_offset) / step]`，用于分阶段增加穿透目标数。
- `threshold`：等级达到 `threshold` 时用 `high`，否则用 `base`。
- `maximum` 非空时取上限。`value_type=integer` 表示整数输出。
- `coefficient_setting` 非空时，`per_level` 系数取永久档的对应设置。这保留了既有玩家的工程参数，而不是覆盖永久档。

界面说明与实际效果分别有明确的表。改变数值后同时核对说明，不要只改文字。

## 兼容与校验

`technology-migration-127.json` 是冻结的历史迁移映射，不是可调的当前数值源；保留它是为了读取已购买的旧科技和进行一次性容量退款。`golden-*.json` 是测试参考。旧正式 JSON 的只读参考保存在 `tests/DomainManaged/Fixtures/pre-csv`，运行时不会因为 CSV 失败而读取它们。

性能或历史测试若必须加载旧 JSON，需要显式调用 `CatalogData.ConfigureLegacyFixture(path)`；这是专用测试入口。正常游戏使用 `Configure`，只加载 CSV。每次重新配置会增加 Revision 并清除缓存。当前数据一次完成解析和引用校验，运行过程中使用缓存，不逐帧读取 CSV。

修改后执行独立领域校验：

```powershell
dotnet run --project tests/DomainManaged/DomainManaged.csproj -v quiet
```

该套测试覆盖 CSV 引号/换行/数值格式、全部目录与迁移前数值逐字段相同、科技可达、特性实际公式、缓存失效、坏表拒绝、旧存档与永久进度。存档测试写入 `.runtime-tests`，不会写玩家目录。

## 修改何时生效

游戏启动时读取并校验表格，之后使用缓存。当前没有运行中热重载：先保存 CSV，通过检查，再关闭并重启游戏。

| 改动 | 已有存档与新局的行为 |
| --- | --- |
| 科技/特性公式、基础机型倍率 | 重启并载入后按新表编译效果；已购买节点、永久特性所有权和等级保持。 |
| `economy_settings.csv` 的默认参数 | 新局与旧档缺少的参数使用新默认；旧档已保存的工程参数优先。要改变当前档的倍率，使用游戏内参数页修改对应项。 |
| 永久特性设置 | 永久档已保存的设置优先，不能靠修改默认表覆盖玩家已有配置。 |
| 初始资源、初始建筑 | 只影响新局，不重置或增加现有存档中的资源与建筑。 |
| 敌人造型类别、波次权重、生成基数与技能参数 | 重启后新计划/新生成单位使用表值；战斗快照已保存的单位状态仍保留。 |
| 击杀/波次奖励 | 重启后的后续结算读取新表，历史已领取奖励不会重新发放。 |

修改当前数值表会改变对应的平衡。旧存档兼容承诺保留的是 ID、所有权、进度与已发生的交易，不是将旧数值永远锁死。

## 两个科技编辑例子

**把 K_S01 的动能伤害从 +4% 改成 +5%。** 在 `deep_technology_nodes_values.csv` 找到：

```csv
parent_id,position,attribute,type,value
K_S01,0,kinetic_damage_bonus,number,0.04
```

把末尾改为 `0.05`；同步 `deep_technology_nodes_effects.csv` 的说明以及 `deep_technology_nodes_effect_definition.csv` 的显示数值。伴随节点 K_S21 是另一个独立购买节点，不会自动跟着修改。如果希望整对仍均分收益，要明确修改两项。

**给某节点增加第二个前置。** 在 `deep_technology_nodes_requires.csv`，同一 `parent_id` 的 position 应依次为 0、1、2。第一列是“需要前置的节点”，value 才是前置 ID。例如：

```csv
parent_id,position,type,value
K_S21,0,string,K_S01
```

表示 K_S21 需要 K_S01。不要将两者写反，不要产生循环，也不要使用不存在的 ID。小节点仍是科研费用单项、单级购买。新增真正的节点需同时建立主记录、费用、属性和必要的关系行；仅在描述里写能力不会产生实际效果。

## 一个特性公式编辑例子

在 `perk_effect_rules.csv` 查找 `perk_id=f_kinetic_quality` 与 `attribute=damage_multiplier`。当前 `operation=linear`、`base=1`、`per_level=0.16`、`level_offset=0`，因此等级 1 为 ×1.16、等级 5 为 ×1.80。

若把 `per_level` 改为 `0.20`，对应变为 ×1.20 和 ×2.00。ID、等级和装备归属不变。该行的空 maximum 表示没有额外硬上限。穿透等阶梯能力应保留其 floor_step / threshold 操作，不能直接照搬成连续的小数目标数。

## 敌人与波次表

| 文件 | 用途 |
| --- | --- |
| `combat_enemies.csv` | 普通角色、Boss 变体、护甲、生命/火力/速度倍率和冷却。 |
| `combat_enemy_kinds.csv` | 单位类别尺寸、投弹与攻击节奏等。 |
| `combat_wave_composition.csv` | 各波次区间的角色权重；区间必须连续，权重不能为负。 |
| `combat_fronts.csv` | 母舰方位、颜色、方向与开放波次。 |
| `combat_defense_stages.csv` | 每次外推对应的数值倍率。 |
| `combat_armor.csv` | 武器对护甲/能量层的伤害关系。 |
| `combat_tuning.csv` | 波次、数量、生成范围、精锐、EMP 及特殊技能等公共系数。 |

修改一类敌人的生命倍率可从 `combat_enemies.csv` 的对应 health 列开始，而不是把每个波次都复制一份敌人记录。各参数名称、单位和范围以表头与 tuning 表说明为准。

## Excel 保存与 ID

建议用 Excel 的“数据 → 从文本/CSV”导入，编码选 UTF-8、分隔符选逗号；包含 ID 的列设为文本，避免软件改成日期或科学计数法。保存选 **CSV UTF-8（逗号分隔）**，不要选系统 ANSI CSV。多张关系表分别保存成文件，不能只保存工作簿的当前一页后假设其它 CSV 已更新。

CSV 的双引号是文件格式的一部分，编辑器会替你转义。不要手工删除包住逗号和换行的引号。文件的 `.csv.import` 保留配置用于让 Godot 将表格作为原始文本打包，不属于数值内容。

节点 ID、机型 ID、Perk ID、建筑 kind、敌人 role 都是跨表和存档契约。对已有 ID 更名或删除可能使存档失效；应使用带迁移代码的版本升级，而不是只在 Excel 中替换。不要修改历史迁移 JSON、冻结性能夹具或玩家存档来“修好”校验错误。

## 统一检查入口

双击项目根目录 `ValidateData.bat`；命令行也可执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate_data.ps1
```

它调用 `tests/BalanceManaged` 的 `--validate-only`，只做短时真实 Domain + Combat 数据校验，不跑 5000 架压力测试，也不构建完整游戏项目。失败时根据输出的表名、行号或 ID 修正，详细日志在 `artifacts/validate-data.log`。

导出预设显式包含 `data/domain/*.csv` 原始文件。JSON 存档格式不在本次替换范围内；数值目录改为 CSV，不代表玩家进度文件也要改成 CSV。

### 攻坚测距（C_N1）

该节点现对所有目标提供最终武器射程 ×1.20，属性为 `all_target_range_multiplier`。原有 C_S21 / C_S22 研究前置保留；已研究的存档无需重新购买。它与针对大型目标的旧字段不同，正式表已经取消目标体型限制。冻结性能夹具仍保留旧大目标限定值，只用于保持测试基准。
