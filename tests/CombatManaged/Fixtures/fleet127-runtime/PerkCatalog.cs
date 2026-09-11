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
            bool advanced = row.I("stage") > 0;
            row["max_level"] = advanced ? 10 : 25;
            row["upgrade_base_cost"] = advanced ? 5 : 2;
            row["upgrade_growth"] = advanced ? 1.5 : 1.35;
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
    public static DataMap Effects(string id, int level, DataMap settings)
    {
        if (level <= 0)
            return new();
        double l = level;
        return id switch
        {
            "expanded_hangar" => new() { ["capacity_add"] = level * settings.I("expanded_hangar_per_level") },
            "assembly" => new() { ["spawn_multiplier"] = 1.0 / (1.0 + l * settings.N("assembly_per_level")) },
            "targeting" => new() { ["damage_multiplier"] = 1.0 + l * settings.N("targeting_per_level") },
            "armor" => new() { ["health_multiplier"] = 1.0 + l * settings.N("armor_per_level") },
            "navigation" => new() { ["patrol_multiplier"] = 1.0 + l * settings.N("navigation_patrol_per_level"), ["speed_multiplier"] = 1.0 + l * settings.N("navigation_speed_per_level") },
            "last_stand" => new() { ["death_blast_multiplier"] = 1.0 + l * settings.N("last_stand_per_level") },
            "f_kinetic_quality" => new() { ["damage_multiplier"] = 1.0 + .16 * l },
            "f_kinetic_feed" => new() { ["fire_rate_multiplier"] = 1.0 + .08 * l },
            "f_kinetic_mobilize" => new() { ["death_spawn_reduction_seconds"] = 1.0 + .1 * l },
            "f_laser_lattice" => new() { ["damage_multiplier"] = 1.0 + .20 * l },
            "f_laser_focus" => new() { ["big_target_damage_multiplier"] = 1.0 + .10 * l },
            "f_laser_precharge" => new() { ["launch_damage_multiplier"] = 1.50 + .04 * l, ["launch_damage_seconds"] = 8.0 },
            "f_missile_warhead" => new() { ["damage_multiplier"] = 1.0 + .22 * l },
            "f_missile_payload" => new() { ["blast_radius_multiplier"] = 1.0 + .08 * l },
            "f_missile_recycle" => new() { ["kill_spawn_reduction_seconds"] = Math.Min(.85, .35 + .02 * l), ["kill_spawn_reduction_cooldown"] = 1.0 },
            "a_kinetic_core" => new() { ["damage_multiplier"] = 1.0 + .24 * l },
            "a_kinetic_cycle" => new() { ["fire_rate_multiplier"] = 1.0 + .08 * l },
            "a_kinetic_pierce" => new() { ["pierce_extra_targets"] = Math.Min(3, 1 + (long)Math.Floor((l - 1.0) / 4.0)), ["pierce_damage_retention"] = Math.Min(.80, .65 + .015 * (l - 1.0)) },
            "a_kinetic_ricochet" => new() { ["ricochet_targets"] = (level >= 6 ? 2L : 1L), ["ricochet_damage_retention"] = Math.Min(.60, .35 + .01 * l) },
            "a_laser_crystal" => new() { ["damage_multiplier"] = 1.0 + .28 * l },
            "a_laser_oscillator" => new() { ["fire_rate_multiplier"] = 1.0 + .07 * l },
            "a_laser_refraction" => new() { ["refraction_targets"] = 2, ["refraction_damage_retention"] = Math.Min(.60, .35 + .01 * l) },
            "a_laser_erosion" => new() { ["erosion_damage_per_stack"] = Math.Min(.06, .03 + .002 * (l - 1.0)), ["erosion_max_stacks"] = 10, ["erosion_duration"] = 3.0 },
            "a_missile_charge" => new() { ["damage_multiplier"] = 1.0 + .30 * l },
            "a_missile_guidance" => new() { ["projectile_speed_multiplier"] = 1.0 + .10 * l, ["damage_multiplier"] = 1.0 + .08 * l },
            "a_missile_cluster" => new() { ["cluster_fragments"] = 3, ["cluster_damage_retention"] = Math.Min(.25, .15 + .005 * l) },
            "a_missile_slow" => new() { ["slow_fraction"] = Math.Min(.60, .20 + .04 * l), ["slow_duration"] = 2.5, ["slow_boss_multiplier"] = .5 },
            "a_k2_dense_fire" => new() { ["k2_dense_hits_required"] = 3, ["k2_dense_fire_rate_multiplier"] = 1.10 + .02 * l, ["k2_dense_duration"] = 3.0 },
            "a_k2_recovery" => new() { ["k2_reload_reduction_seconds"] = .10 + .015 * l, ["k2_reload_cooldown"] = 1.0 },
            "a_k3_marked_shot" => new() { ["k3_support_first_multiplier"] = 1.30 + .04 * l },
            "a_k3_mobile_gun" => new() { ["k3_mobile_warmup_retention"] = .4 + .03 * l, ["k3_peak_damage_multiplier"] = .9 },
            "a_m2_dual_zone" => new() { ["m2_delayed_fraction"] = .35 + .01 * l, ["m2_delayed_seconds"] = .35 },
            "a_m2_shock_charge" => new() { ["m2_shock_required"] = 3, ["m2_next_damage_multiplier"] = 1.2 + .03 * l, ["m2_shock_cooldown"] = 4.0 },
            "a_m3_drill_charge" => new() { ["m3_drill_heavy_multiplier"] = 1.35 + .05 * l },
            "a_m3_escort_guidance" => new() { ["m3_escort_lock_multiplier"] = 1.0 / (1.0 + .12 * l), ["m3_escort_radius"] = 1.8 },
            "a_l2_charge_memory" => new() { ["l2_memory_seconds"] = .4 + .1 * l, ["l2_memory_fraction"] = .35 + .04 * l },
            "a_l2_resonance" => new() { ["l2_energy_resonance_per_second"] = .03 + .006 * l, ["l2_resonance_cap"] = .20 + .03 * l },
            "a_l3_wide_pulse" => new() { ["l3_pulse_radius_multiplier"] = 1.20 + .025 * l, ["l3_damage_multiplier"] = .8 },
            "a_l3_afterpulse" => new() { ["l3_afterpulse_seconds"] = .3 + .10 * l },
            _ => new()
        };
    }
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
