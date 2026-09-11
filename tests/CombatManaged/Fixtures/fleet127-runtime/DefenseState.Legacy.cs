using System;
using System.Collections.Generic;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    private static DataMap MigrateLegacyResearch(DataMap legacy)
    {
        double damage = 1 + legacy.N("damage") * .3, industry = (1 + legacy.N("mining") * .25) * (1 + legacy.N("industrial_synergy") * .12) * Math.Pow(1.12, legacy.N("industrial_mastery"));
        var values = new Dictionary<string, double[]>();
        values["K"] = new double[] { damage * (1.0 + legacy.N("kinetic_coils") * .16) * (1.0 + legacy.N("kinetic_mastery") * .12) - 1.0, (1.0 + legacy.N("rapid") * .22) * (1.0 + legacy.N("fire_control") * .12) * (1.0 + legacy.N("ammo_packing") * .08) - 1.0, legacy.N("projectile_drive") * .12 + legacy.N("ammo_packing") * .03, 0.0, legacy.N("alien_kinetic_range") * .18 };
        values["M"] = new double[] { damage * (1.0 + Math.Max(0.0, legacy.N("missile") - 1.0) * .35) * (1.0 + legacy.N("warhead") * .18) * (1.0 + legacy.N("missile_mastery") * .12) - 1.0, (1.0 + legacy.N("missile_feed") * .12) * (1.0 + legacy.N("missile_mastery") * .06) - 1.0, legacy.N("missile_blast") * .045 / .22, legacy.N("missile_guidance") * .35 / 3.5, legacy.N("missile_engines") * .12 };
        values["L"] = new double[] { damage * (1.0 + Math.Max(0.0, legacy.N("laser") - 1.0) * .35) * (1.0 + legacy.N("laser_focus") * .18) * (1.0 + legacy.N("laser_capacitors") * .10) * (1.0 + legacy.N("photonic_mastery") * .12) - 1.0, (1.0 + legacy.N("laser_cycling") * .14) * (1.0 + legacy.N("laser_cooling") * .09) * (1.0 + legacy.N("photonic_mastery") * .06) - 1.0, (1.0 + legacy.N("laser_range") * .08) * (1.0 + legacy.N("alien_laser_range") * .12) - 1.0, 0.0, 0.0 };
        values["I"] = new double[] { industry * (1.0 + legacy.N("mineral_processing") * .18) - 1.0, industry * (1.0 + legacy.N("energy_grid") * .18) * (1.0 + legacy.N("laser_capacitors") * .04) * (1.0 + legacy.N("photonic_mastery") * .04) - 1.0, industry * (1.0 + legacy.N("research_methods") * .20) - 1.0, legacy.N("hangar_capacity"), legacy.N("factory_automation") * .12 + legacy.N("combat_logistics") * .08 };
        values["D"] = new double[] { (1.0 + legacy.N("drone_armor") * .12) * (1.0 + legacy.N("defense_network") * .05) * (1.0 + legacy.N("alien_field_support") * .08) - 1.0, (legacy.N("shield") * 35.0 + legacy.N("defense_network") * 25.0) / 100.0, (1.0 + legacy.N("field_repair") * .18 + legacy.N("combat_logistics") * .08) * (1.0 + legacy.N("alien_field_support") * .12) - 1.0, legacy.N("death_blast") * .40, 0.0 };
        values["C"] = new double[] { (1.0 + legacy.N("patrol_radius") * .10 + legacy.N("orbit_navigation") * .04 + legacy.N("patrol_command") * .06) * (1.0 + legacy.N("alien_navigation") * .12) - 1.0, (1.0 + legacy.N("patrol_outer") * .12 + legacy.N("orbit_navigation") * .05 + legacy.N("patrol_command") * .08) * (1.0 + legacy.N("alien_navigation") * .20) - 1.0, (1.0 + legacy.N("patrol_speed") * .1 + legacy.N("patrol_command") * .04) * (1.0 + legacy.N("alien_propulsion") * .1) - 1.0, 0.0, 0.0 };
        var nodes = new DataMap();
        foreach (var (branch, budget) in values)
        {
            var totals = new double[5];
            for (int i = 0; i < 20; i++)
            {
                string id = $"{branch}_S{i + 1:00}";
                double amount = DataMap.Number(DeepTechnology.Definition(id).Map("values").Values.First());
                int slot = i % 5;
                if (totals[slot] + amount <= budget[slot] + .000001)
                {
                    nodes[id] = 1L;
                    totals[slot] += amount;
                }
            }
        }
        var direct = new Dictionary<string, bool> { ["M_N1"] = legacy.L("missile") > 0, ["L_N1"] = legacy.L("laser") > 0, ["C_G1"] = legacy.L("mothership_assault") > 0, ["C_A2"] = legacy.L("frontier_range_1") > 0, ["C_A3"] = legacy.L("frontier_range_2") > 0, ["C_G2"] = legacy.L("frontier_range_3") > 0, ["D_N1"] = legacy.L("drone_armor") >= 2, ["D_N2"] = legacy.L("field_repair") >= 2, ["I_N2"] = legacy.L("research_methods") >= 2, ["I_N3"] = legacy.L("factory_automation") >= 2 };
        foreach (var (id, owned) in direct)
            if (owned)
                nodes[id] = 1L;
        return new()
        {
            ["version"] = 2L,
            ["nodes"] = nodes,
            ["credited"] = nodes.Keys.Cast<object?>().ToList(),
            ["successor_levels"] = new DataMap()
        };
    }
}
