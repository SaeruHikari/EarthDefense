using System;
using System.Collections.Generic;
using Earthward.Domain;
namespace Earthward.Combat;

public static class LegacyEnemyCatalog127
{
    private sealed record Definition(string Name, string Armor, double Health, double Dps, double Speed, double Cooldown, double Energy = 0);
    private static readonly Dictionary<string, Definition> Roles = new()
    {
        ["claw"] = new("裂爪突击机", "light", 1.15, 1.1, 1, .7),
        ["needle"] = new("针翼猎杀机", "light", 1.05, .8, 1.18, .9),
        ["rock"] = new("岩甲突进舰", "heavy", 2.4, 1.3, .7, 1.8),
        ["siege"] = new("蚀星攻城舰", "heavy", 2.8, 1.6, .58, 3.8),
        ["prism"] = new("棱镜游猎机", "light", 1.3, 1.2, .85, 1.1, .65),
        ["weaver"] = new("织幕支援舰", "heavy", 2, .35, .62, 2.2, .7),
        ["hatcher"] = new("孵化运输舟", "heavy", 2.5, .6, .7, 2),
        ["jammer"] = new("噬频干扰机", "light", 1.6, .55, 1.05, 1.5)
    };
    public static void Apply(DataMap enemy, DataMap entry, DataMap settings)
    {
        long wave = Math.Max(1, C.L(entry, "wave", 1));
        int stage = Math.Clamp(C.I(entry, "stage"), 0, 3);
        string role = C.S(entry, "role", "claw"), kind = C.S(enemy, "kind");
        var data = Roles.GetValueOrDefault(role, Roles["claw"]);
        if (kind == "small_boss")
            data = new("资源运输虫", wave < 10 ? "light" : "heavy", 4.5, 1, .75, 1.8);
        else if (kind == "boss")
        {
            string variant = wave < 10 ? "brood" : wave < 20 ? "forge" : "prism";
            enemy["boss_variant_id"] = variant;
            data = new(variant == "brood" ? "裂巢督军" : variant == "forge" ? "铸甲督军" : "棱镜审判者", variant == "brood" ? "light" : "heavy", 8, 1.6, .66, 3, variant == "prism" ? .8 : 0);
        }
        else if (kind is "carrier" or "mothership")
            data = new("外星母舰", "heavy", 35, 1.3, .3, 3, wave >= 20 ? .5 : 0);
        double factor = C.N(settings, "enemy_health", 29) / 29, hp = 29.25 * data.Health * Math.Exp(Math.Min(600, Math.Log(1 + C.N(settings, "enemy_health_growth", .035)) * (wave - 1))) * new[] { 1d, 2, 4, 8 }[stage] * factor, dps = 19.5 * data.Dps * Math.Exp(Math.Min(600, Math.Log(1 + C.N(settings, "enemy_damage_growth", .018)) * (wave - 1))) * new[] { 1d, 1.35, 1.8, 2.4 }[stage];
        bool elite = kind is "scout" or "cruiser" && wave >= 3 && C.Posmod(C.L(entry, "index"), 13) == 12;
        if (elite)
        {
            hp *= 2;
            dps *= 2;
        }
        enemy["elite"] = elite;
        enemy["wave"] = wave;
        enemy["name"] = (elite ? "精锐 · " : "") + data.Name;
        enemy["enemy_role_id"] = kind is "scout" or "cruiser" ? role : "";
        enemy["armor_type"] = data.Armor;
        enemy["body_armor"] = data.Armor;
        enemy["hp"] = hp;
        enemy["max_hp"] = hp;
        enemy["energy_hp"] = hp * data.Energy;
        enemy["energy_max_hp"] = hp * data.Energy;
        enemy["shield_broken"] = false;
        enemy["tactical_speed"] = .32 * data.Speed * C.N(settings, "enemy_speed_multiplier", 1.5);
        enemy["speed"] = enemy["tactical_speed"];
        enemy["base_speed"] = .32 * data.Speed;
        enemy["attack_cooldown"] = data.Cooldown;
        enemy["attack_damage"] = dps * data.Cooldown;
        enemy["ground_damage"] = Math.Max(1.5, dps * .12 * data.Cooldown);
        enemy["defense_stage"] = stage;
        enemy["reward_wave"] = wave;
        enemy["planned_uid"] = C.S(entry, "planned_uid");
        enemy["telegraph"] = 0d;
        enemy["skill_phase"] = "travel";
        enemy["skill_clock"] = 1d;
    }
}

