# 完整数值表索引

共 **50 张 CSV**：43 张领域表与 7 张战斗表。行数不包含表头。

返回 [数据入口](README.md)；具体规则和编辑示例见 [CSV 编辑说明](domain/README.md)。日常修改不需要手动修改结构映射表。

| 分类 | 文件 | 数据行 | 主要列 |
| --- | --- | ---: | --- |
| 永久成就 | [achievements.csv](domain/achievements.csv) | 1 | id, name, condition, event, reward_attribute, reward_per_research, icon |
| 机型 | [airframes.csv](domain/airframes.csv) | 1 | parent_id, position, record_id, has_base, has_definitions |
| 机型 | [airframes_base.csv](domain/airframes_base.csv) | 3 | parent_id, position, key, type, value |
| 机型 | [airframes_definitions.csv](domain/airframes_definitions.csv) | 9 | parent_id, position, aoe_budget, blast_radius_multiplier, burst_target_count, capacity_cost, damage_multiplier … |
| 结构映射 | [catalog_columns.csv](domain/catalog_columns.csv) | 61 | table, column, type, required |
| 结构映射 | [catalog_tables.csv](domain/catalog_tables.csv) | 30 | table, catalog, parent_table, parent_field, shape, id_column, key_column … |
| 战斗 / 敌人与波次 | [combat_armor.csv](domain/combat_armor.csv) | 9 | layer, weapon_family, damage_multiplier |
| 战斗 / 敌人与波次 | [combat_defense_stages.csv](domain/combat_defense_stages.csv) | 4 | stage, health_multiplier, damage_multiplier |
| 战斗 / 敌人与波次 | [combat_enemies.csv](domain/combat_enemies.csv) | 15 | id, kind, role_id, boss_variant_id, name, armor, minimum_wave … |
| 战斗 / 敌人与波次 | [combat_enemy_kinds.csv](domain/combat_enemy_kinds.csv) | 6 | id, base_size, bombard_seconds, legacy_speed_multiplier, drone_fire_cooldown, earth_fire_cooldown |
| 战斗 / 敌人与波次 | [combat_fronts.csv](domain/combat_fronts.csv) | 8 | id, ordinal, name, color, direction_x, direction_y, direction_z … |
| 战斗 / 敌人与波次 | [combat_tuning.csv](domain/combat_tuning.csv) | 40 | id, value, minimum, maximum, description |
| 战斗 / 敌人与波次 | [combat_wave_composition.csv](domain/combat_wave_composition.csv) | 11 | from_wave, through_wave, claw, needle, rock, siege, prism … |
| 科技树 | [deep_technology.csv](domain/deep_technology.csv) | 1 | parent_id, position, record_id, has_branches, has_nodes |
| 科技树 | [deep_technology_branches.csv](domain/deep_technology_branches.csv) | 6 | parent_id, position, color, id, name |
| 科技树 | [deep_technology_nodes.csv](domain/deep_technology_nodes.csv) | 248 | parent_id, position, alien, branch, icon, id, importance … |
| 科技树 | [deep_technology_nodes_cost.csv](domain/deep_technology_nodes_cost.csv) | 286 | parent_id, position, currency, type, value |
| 科技树 | [deep_technology_nodes_draw_position.csv](domain/deep_technology_nodes_draw_position.csv) | 496 | parent_id, position, type, value |
| 科技树 | [deep_technology_nodes_effects.csv](domain/deep_technology_nodes_effects.csv) | 253 | parent_id, position, type, value |
| 科技树 | [deep_technology_nodes_requires.csv](domain/deep_technology_nodes_requires.csv) | 287 | parent_id, position, type, value |
| 科技树 | [deep_technology_nodes_unlock.csv](domain/deep_technology_nodes_unlock.csv) | 514 | parent_id, position, key, type, value |
| 科技树 | [deep_technology_nodes_values.csv](domain/deep_technology_nodes_values.csv) | 266 | parent_id, position, attribute, type, value |
| 经济 / 基础 / 奖励 | [domain_balance.csv](domain/domain_balance.csv) | 41 | key, value, minimum, maximum, unit, description |
| 经济 / 基础 / 奖励 | [economy.csv](domain/economy.csv) | 1 | parent_id, position, record_id, has_buildings, has_initial_buildings, has_integer_settings, has_local_shield … |
| 经济 / 基础 / 奖励 | [economy_buildings.csv](domain/economy_buildings.csv) | 7 | parent_id, position, color, description, id, name, unlock_research, has_cost, max_count |
| 经济 / 基础 / 奖励 | [economy_buildings_cost.csv](domain/economy_buildings_cost.csv) | 10 | parent_id, position, currency, type, value |
| 经济 / 基础 / 奖励 | [economy_initial_buildings.csv](domain/economy_initial_buildings.csv) | 7 | parent_id, position, key, type, value |
| 经济 / 基础 / 奖励 | [economy_integer_settings.csv](domain/economy_integer_settings.csv) | 11 | parent_id, position, type, value |
| 经济 / 基础 / 奖励 | [economy_local_shield.csv](domain/economy_local_shield.csv) | 2 | parent_id, position, key, type, value |
| 经济 / 基础 / 奖励 | [economy_ranges.csv](domain/economy_ranges.csv) | 86 | parent_id, position, key, type, value |
| 经济 / 基础 / 奖励 | [economy_settings.csv](domain/economy_settings.csv) | 43 | parent_id, position, parameter, type, value |
| 历史与远征兼容 | [expedition.csv](domain/expedition.csv) | 1 | parent_id, position, record_id, has_default_settings, has_sector_ids, has_setting_definitions |
| 历史与远征兼容 | [expedition_default_settings.csv](domain/expedition_default_settings.csv) | 34 | parent_id, position, parameter, type, value |
| 历史与远征兼容 | [expedition_sector_ids.csv](domain/expedition_sector_ids.csv) | 6 | parent_id, position, type, value |
| 历史与远征兼容 | [expedition_setting_definitions.csv](domain/expedition_setting_definitions.csv) | 34 | parent_id, position, default, id, integer, max, min … |
| 经济 / 基础 / 奖励 | [kill_rewards.csv](domain/kill_rewards.csv) | 6 | kind, minerals, energy, science, score, alien_points, wave_alien_reward … |
| 历史与远征兼容 | [legacy_frontiers.csv](domain/legacy_frontiers.csv) | 4 | stage, action_radius_base, weapon_range_bonus, patrol_outer_bonus |
| 机型 | [patrol_bases.csv](domain/patrol_bases.csv) | 3 | kind, patrol_radius, patrol_outer_range, patrol_speed, health |
| 特性 | [perk_effect_rules.csv](domain/perk_effect_rules.csv) | 62 | perk_id, attribute, operation, base, per_level, level_offset, maximum … |
| 特性 | [perk_economy.csv](domain/perk_economy.csv) | 1 | key, value |
| 特性 | [perk_setting_bounds.csv](domain/perk_setting_bounds.csv) | 10 | key, minimum, maximum, integer |
| 特性 | [perk_tiers.csv](domain/perk_tiers.csv) | 2 | stage, max_level, purchase_cost, upgrade_base_cost, upgrade_growth |
| 特性 | [perks.csv](domain/perks.csv) | 1 | parent_id, position, record_id, has_definitions, has_neutral_modifiers, has_settings |
| 特性 | [perks_definitions.csv](domain/perks_definitions.csv) | 39 | parent_id, position, color, description, display_key, display_mode, effect_label … |
| 特性 | [perks_definitions_airframes.csv](domain/perks_definitions_airframes.csv) | 18 | parent_id, position, type, value |
| 特性 | [perks_definitions_kinds.csv](domain/perks_definitions_kinds.csv) | 33 | parent_id, position, type, value |
| 特性 | [perks_neutral_modifiers.csv](domain/perks_neutral_modifiers.csv) | 53 | parent_id, position, key, type, value |
| 特性 | [perks_settings.csv](domain/perks_settings.csv) | 10 | parent_id, position, parameter, type, value |
| 经济 / 基础 / 奖励 | [successor_research.csv](domain/successor_research.csv) | 6 | branch, attribute, display_name, increment, science_base, science_growth, alien_base … |
| 科技树 | [technology_attribute_schema.csv](domain/technology_attribute_schema.csv) | 79 | attribute, type |

同一实体的费用、属性、前置与文案各自成表；单元格内没有整段 JSON。所有表按启动缓存读取。position 保持同一父记录下从 0 起连续，parent_id 指向实体 ID。

卫星发射中心 `satellite_launcher` 位于建设栏首项，免费、限建一座；`economy_buildings.csv` 使用 `has_cost=false` 与 `max_count=1`，费用表不含该设施。七格蜂窝占地、对应实体模型、分级发射动画及按发射位置确定的轨道属于渲染与建造代码。科研卫星免费发射，入轨后默认科研为 0.08/秒；轨道地心半径为 20.25，相对地球高度为 4.25。具体参数入口见 [CSV 编辑说明](domain/README.md)。

已有存档中的工程参数覆盖对应默认值；保存后的表格需重启才生效。先运行项目根目录 ValidateData.bat，确认格式和引用均正确。
