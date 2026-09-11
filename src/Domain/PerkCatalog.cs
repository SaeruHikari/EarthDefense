using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace Earthward.Domain;

public static class PerkCatalog
{
	public static IReadOnlyList<DataMap> Entries => CatalogData.Rows("perks.json", "definitions");
	public static string[] LegacyIds => CatalogData.Load("perks.json").List("legacy_ids").Cast<string>().ToArray();
	public static string[] Kinds => new[] { "interceptor", "laser", "missile" };
	public static string[] Layers => new[] { "factory", "aircraft" };
	public static DataMap DefaultSettings => CatalogData.Load("perks.json").Map("settings").DeepClone();
	public static DataMap Definition(string id)
	{
		var row = CatalogData.Definition("perks.json", "definitions", id);
		if (row.Count > 0)
		{
			var tier = PerkEffectRules.Tier(row.I("stage") > 0 ? 1 : 0);
			row["max_level"] = tier.I("max_level");row["upgrade_base_cost"] = tier.I("upgrade_base_cost");row["upgrade_growth"] = tier.N("upgrade_growth");
		}
		return row;
	}
	public static bool Compatible(string id, string layer, string kind, string airframe = "")
	{
		var row = Definition(id);
		if (row.Count == 0 || row.S("layer") != layer || !Kinds.Contains(kind) || (row.List("kinds").Count > 0 && !row.List("kinds").Contains(kind)))
			return false;
		if (layer == "factory" || airframe == "*" || row.List("airframes").Count == 0)
			return true;
		return row.List("airframes").Contains(airframe.Length > 0 ? airframe : AirframeCatalog.BaseFor(kind));
	}
	public static DataMap NeutralModifiers() => CatalogData.Load("perks.json").Map("neutral_modifiers").DeepClone();
	public static DataMap Effects(string id, int level, DataMap settings) => PerkEffectRules.Evaluate(id, level, settings);
	public static void Accumulate(DataMap result, DataMap addition)
	{
		foreach (var (key, value) in addition)
			if (key == "spawn_multiplier")
				result[key] = result.N(key) * DataMap.Number(value);
			else if (key.EndsWith("_multiplier") && key != "slow_boss_multiplier")
				result[key] = result.N(key) + DataMap.Number(value) - 1;
			else if (key == "capacity_add")
				result[key] = result.L(key) + DataMap.Integer(value);
			else
				result[key] = value;
	}
	public static string Percent(double value) => (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
	public static string EffectText(string id, int level, DataMap settings)
	{
		var e = Effects(id, level, settings);
		if (e.Count == 0)
			return "未知 Perk";
		if (e.Count == 1 && e.ContainsKey("damage_multiplier"))
			return "武器伤害 +" + Percent(e.N("damage_multiplier") - 1);
		if (e.Count == 1 && e.ContainsKey("fire_rate_multiplier"))
			return "射速 +" + Percent(e.N("fire_rate_multiplier") - 1);
		string text = id switch
		{
			"expanded_hangar" => $"工厂编制 +{e.I("capacity_add")}",
			"assembly" => $"补造速度 +{Percent(1 / e.N("spawn_multiplier") - 1)} · 间隔 ÷ {1 / e.N("spawn_multiplier"):F2}",
			"armor" => "最大生命 +" + Percent(e.N("health_multiplier") - 1),
			"navigation" => $"巡航范围 +{Percent(e.N("patrol_multiplier") - 1)} · 速度 +{Percent(e.N("speed_multiplier") - 1)}",
			"last_stand" => "自爆伤害 +" + Percent(e.N("death_blast_multiplier") - 1),
			"f_kinetic_mobilize" => $"所属飞机阵亡：缺编补造推进 {e.N("death_spawn_reduction_seconds"):F1} 秒",
			"f_laser_focus" => "对大型目标伤害 +" + Percent(e.N("big_target_damage_multiplier") - 1),
			"f_laser_precharge" => $"出厂首次开火起 {e.N("launch_damage_seconds"):F0} 秒：伤害 +{Percent(e.N("launch_damage_multiplier") - 1)}",
			"f_missile_payload" => "爆炸半径 +" + Percent(e.N("blast_radius_multiplier") - 1),
			"f_missile_recycle" => $"所属导弹击毁：缺编补造推进 {e.N("kill_spawn_reduction_seconds"):F2} 秒 · 每 {e.N("kill_spawn_reduction_cooldown"):F0} 秒最多一次",
			"a_kinetic_pierce" => $"额外贯穿 {e.I("pierce_extra_targets")} 个目标 · 每次保留 {Percent(e.N("pierce_damage_retention"))} 伤害",
			"a_kinetic_ricochet" => $"跳弹 {e.I("ricochet_targets")} 个附近目标 · 副击 {Percent(e.N("ricochet_damage_retention"))} 伤害",
			"a_laser_refraction" => $"额外折射 {e.I("refraction_targets")} 束 · 每束 {Percent(e.N("refraction_damage_retention"))} 伤害",
			"a_laser_erosion" => $"每层易伤 +{Percent(e.N("erosion_damage_per_stack"))} · 最多 {e.I("erosion_max_stacks")} 层 · 持续 {e.N("erosion_duration"):F0} 秒",
			"a_missile_guidance" => $"导弹速度 +{Percent(e.N("projectile_speed_multiplier") - 1)} · 伤害 +{Percent(e.N("damage_multiplier") - 1)}",
			"a_missile_cluster" => $"{e.I("cluster_fragments")} 枚子弹药 · 每枚 {Percent(e.N("cluster_damage_retention"))} 伤害 · 不递归",
			"a_missile_slow" => $"爆炸减速 {Percent(e.N("slow_fraction"))} · {e.N("slow_duration"):F1} 秒 · Boss 效果减半",
			_ => ""
		};
		if (text.Length > 0)
			return text;
		var row = Definition(id);
		double number = e.N(row.S("display_key"));
		string display = row.S("display_mode") switch
		{
			"bonus" => Percent(number - 1),
			"percent" => Percent(number),
			_ => number.ToString("0.##", CultureInfo.InvariantCulture) + "s"
		};
		return row.S("description") + " " + row.S("effect_label") + " " + display;
	}
}
