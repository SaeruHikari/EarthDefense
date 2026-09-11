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
        string role = C.S(entry, "role", "claw"), kind = C.S(enemy, "kind");
        var catalog = CombatCatalog.Current;
        var tuning = catalog.Values;
        var data = catalog.Enemy(kind, role, wave);
        if (data.BossVariant.Length > 0) enemy["boss_variant_id"] = data.BossVariant;
        var defaults = CatalogData.Load("economy.json").Map("settings");
        double factor = C.N(settings, "enemy_health", defaults.N("enemy_health")) / tuning.HealthSettingReference;
        double hp = tuning.HealthBase * data.Health * Math.Exp(Math.Min(600, Math.Log(1 + C.N(settings, "enemy_health_growth", defaults.N("enemy_health_growth"))) * (wave - 1))) * catalog.StageHealth[stage] * factor;
        double dps = tuning.DamageBase * data.Dps * Math.Exp(Math.Min(600, Math.Log(1 + C.N(settings, "enemy_damage_growth", defaults.N("enemy_damage_growth"))) * (wave - 1))) * catalog.StageDamage[stage];
        bool elite = kind is "scout" or "cruiser" && wave >= tuning.EliteFirstWave && C.Posmod(C.L(entry, "index"), (long)tuning.EliteInterval) == (long)tuning.EliteOrdinal;
        if (elite)
        {
            hp *= tuning.EliteHealthMultiplier;
            dps *= tuning.EliteDamageMultiplier;
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
        enemy["tactical_speed"] = tuning.TacticalSpeedBase * data.Speed * C.N(settings, "enemy_speed_multiplier", defaults.N("enemy_speed_multiplier"));
        enemy["speed"] = enemy["tactical_speed"];
        enemy["base_speed"] = tuning.TacticalSpeedBase * data.Speed;
        enemy["attack_cooldown"] = data.Cooldown;
        enemy["attack_damage"] = dps * data.Cooldown;
        enemy["ground_damage"] = Math.Max(tuning.GroundDamageFloor, dps * tuning.GroundDamageMultiplier * data.Cooldown);
        enemy["defense_stage"] = stage;
        enemy["reward_wave"] = wave;
        enemy["planned_uid"] = C.S(entry, "planned_uid");
        enemy["telegraph"] = 0d;
        enemy["skill_phase"] = "travel";
        enemy["skill_clock"] = 1d;
    }
}

