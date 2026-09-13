using System;
using System.Collections.Generic;
using Earthward.Domain;
namespace Earthward.Combat;

public static class EnemyCatalog
{
    public static void Apply(DataMap enemy, DataMap entry, DataMap settings)
    {
        long wave = Math.Max(1, C.L(entry, "wave", 1));
        int stage = Math.Clamp(C.I(entry, "stage"), 0, 3);
        string kind = C.S(enemy, "kind");
        var catalog = CombatCatalog.Current;
        var tuning = catalog.Values;
        var data = catalog.Enemy(kind, "claw", wave);
        var defaults = CatalogData.Load("economy.json").Map("settings");
        double factor = C.N(settings, "enemy_health", defaults.N("enemy_health")) / tuning.HealthSettingReference;
        double hp = tuning.HealthBase * data.Health * Math.Exp(Math.Min(600, Math.Log(1 + C.N(settings, "enemy_health_growth", defaults.N("enemy_health_growth"))) * (wave - 1))) * catalog.StageHealth[stage] * factor;
        double openingDamage = tuning.DamageMultiplierForWave(wave);
        double dps = tuning.DamageBase * data.Dps * Math.Exp(Math.Min(600, Math.Log(1 + C.N(settings, "enemy_damage_growth", defaults.N("enemy_damage_growth"))) * (wave - 1))) * catalog.StageDamage[stage] * openingDamage;
        // The opening wave is an onboarding beat. Its count and threat are
        // explicit CSV parameters so a fresh run can establish a defense
        // line; the separate opening multiplier eases later new directions
        // into the full curve instead of removing the damage buffer at wave 2.
        if (wave == 1)
        {
            hp *= Math.Clamp(C.N(settings, "first_wave_enemy_health_multiplier", 1), .25, 1);
            dps *= Math.Clamp(C.N(settings, "first_wave_enemy_damage_multiplier", 1), .25, 1);
        }
        enemy["wave"] = wave;
        enemy["name"] = data.Name;
        enemy["enemy_role_id"] = kind == "scout" ? "claw" : "";
        enemy["armor_type"] = data.Armor;
        enemy["body_armor"] = data.Armor;
        enemy["hp"] = hp;
        enemy["max_hp"] = hp;
        enemy["tactical_speed"] = tuning.TacticalSpeedBase * data.Speed * C.N(settings, "enemy_speed_multiplier", defaults.N("enemy_speed_multiplier"));
        enemy["speed"] = enemy["tactical_speed"];
        enemy["base_speed"] = tuning.TacticalSpeedBase * data.Speed;
        enemy["attack_cooldown"] = data.Cooldown;
        enemy["attack_damage"] = dps * data.Cooldown;
        enemy["ground_damage"] = Math.Max(tuning.GroundDamageFloor * openingDamage, dps * tuning.GroundDamageMultiplier * data.Cooldown);
        enemy["defense_stage"] = stage;
        enemy["reward_wave"] = wave;
        enemy["planned_uid"] = C.S(entry, "planned_uid");
    }
}
