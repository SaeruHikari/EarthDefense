using System;
using Earthward.Domain;
namespace Earthward.Combat;

public readonly record struct DamageResult(double Total, double Energy, double Hull, bool Broke, bool Immune);
public static class EnemyArmor
{
    public static string Family(string kind) => kind == "laser" ? "beam" : kind == "missile" ? "explosive" : "kinetic";
    public static string Layer(DataMap enemy) => C.N(enemy, "energy_hp") > 0 ? "energy" : C.S(enemy, "armor_type", "legacy");
    public static double Multiplier(DataMap enemy, string family, DataMap? context = null, double clock = 0)
    {
        context ??= C.Empty;
        string layer = Layer(enemy);
        int layerIndex = CombatCatalog.LayerIndex(layer), familyIndex = CombatCatalog.FamilyIndex(family);
        double result = layerIndex < 0 ? 1 : familyIndex < 0 ? 0 : CombatCatalog.Current.Armor[layerIndex, familyIndex];
        if (result <= 0)
            return 0;
        var tech = C.M(context, "tech_abilities");
        if (layer == "heavy" && family == "kinetic" && clock < C.N(enemy, "armor_breach_until", -1))
            result = C.N(enemy, "armor_breach_coefficient", .35);
        if (layer == "light")
        {
            result *= Math.Max(0, C.N(context, "light_damage_multiplier", 1));
            if (family == "kinetic")
                result *= 1 + Math.Max(0, C.N(context, "kinetic_light_damage_bonus"));
        }
        if (layer == "energy" && family == "beam")
        {
            result *= Math.Max(0, C.N(context, "energy_damage_multiplier", 1)) * (1 + Math.Max(0, C.N(context, "laser_energy_damage_bonus")));
            result *= C.Packet(context, "laser_energy_multiplier", 1);
        }
        if (layer != "energy" && family == "kinetic" && C.B(tech, "K_A3") && clock < C.N(enemy, "energy_broken_at", -10) + C.Packet(context, "post_shield_seconds", 3))
            result *= C.Packet(context, "post_shield_kinetic_multiplier", 1.4);
        if (C.N(enemy, "perk_erosion_remaining") > 0 && C.S(enemy, "perk_erosion_layer", layer) == layer)
            result *= 1 + C.N(enemy, "perk_erosion_per_stack") * C.I(enemy, "perk_erosion_stacks");
        return result;
    }
    public static DamageResult Resolve(DataMap enemy, double raw, string family, DataMap? context = null, double clock = 0)
    {
        if (raw <= 0 || !double.IsFinite(raw) || C.N(enemy, "hp") <= 0)
            return default;
        double remaining = raw, energy = 0, hull = 0;
        bool broke = false;
        if (C.N(enemy, "energy_hp") > 0)
        {
            double factor = Multiplier(enemy, family, context, clock);
            if (factor <= 0)
                return new(0, 0, 0, false, true);
            energy = Math.Min(C.N(enemy, "energy_hp"), remaining * factor);
            enemy["energy_hp"] = Math.Max(0, C.N(enemy, "energy_hp") - energy);
            remaining = Math.Max(0, remaining - energy / factor);
            if (C.N(enemy, "energy_hp") <= .000001)
            {
                enemy["energy_hp"] = 0d;
                enemy["shield_broken"] = true;
                enemy["energy_broken_at"] = clock;
                broke = true;
                foreach (var k in new[] { "perk_erosion_stacks", "perk_erosion_remaining", "perk_erosion_per_stack", "perk_erosion_layer" })
                    enemy.Remove(k);
            }
        }
        if (remaining > 0)
        {
            hull = Math.Min(C.N(enemy, "hp"), remaining * Multiplier(enemy, family, context, clock));
            enemy["hp"] = Math.Max(0, C.N(enemy, "hp") - hull);
        }
        return new(energy + hull, energy, hull, broke, false);
    }
}
