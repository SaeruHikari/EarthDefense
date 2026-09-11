using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;
namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private static readonly string[] TechPacketKeys = "missile_large_heavy_multiplier saturation_hit_count saturation_damage_multiplier saturation_cooldown linked_factory_count linked_factory_window linked_damage_multiplier linked_cooldown armor_breach_seconds breached_kinetic_heavy_coefficient kinetic_suppress_seconds laser_regen_delay shield_break_stagger_seconds shield_break_field_radius shield_break_field_duration laser_first_energy_multiplier laser_first_energy_cooldown laser_energy_multiplier post_shield_kinetic_multiplier post_shield_seconds proximity_fuse_radius".Split(' ');
    private static readonly string[] PerkEffectKeys = "big_target_damage_multiplier launch_damage_multiplier launch_damage_seconds death_spawn_reduction_seconds kill_spawn_reduction_seconds kill_spawn_reduction_cooldown pierce_extra_targets pierce_damage_retention ricochet_targets ricochet_damage_retention refraction_targets refraction_damage_retention erosion_damage_per_stack erosion_max_stacks erosion_duration cluster_fragments cluster_damage_retention slow_fraction slow_duration slow_boss_multiplier k2_dense_hits_required k2_dense_fire_rate_multiplier k2_dense_duration k2_reload_reduction_seconds k2_reload_cooldown k3_support_first_multiplier k3_mobile_warmup_retention k3_peak_damage_multiplier m2_delayed_fraction m2_delayed_seconds m2_shock_required m2_next_damage_multiplier m2_shock_cooldown m3_drill_heavy_multiplier m3_escort_lock_multiplier m3_escort_radius l2_memory_seconds l2_memory_fraction l2_energy_resonance_per_second l2_resonance_cap l3_pulse_radius_multiplier l3_damage_multiplier l3_afterpulse_seconds".Split(' ');
    private static DataMap Capture(DataMap stats, string[] keys)
    {
        var result = new DataMap();
        foreach (var k in keys)
            if (stats.TryGetValue(k, out var value))
                result[k] = value;
        return result;
    }
    private DataMap WeaponSource(DataMap d, DataMap stats) => new() { ["ability_values"] = stats.TryGetValue("_combat_tech_values", out var values) ? values : Capture(stats, TechPacketKeys), ["aoe_budget"] = C.N(stats, "aoe_budget"), ["actor_uid"] = C.L(d, "uid"), ["primary_target_uid"] = C.L(d, "aim_target_uid", -1), ["damage_type"] = EnemyArmor.Family(C.S(d, "kind")), ["airframe_id"] = C.S(d, "airframe_id", C.S(stats, "airframe_id")), ["tech_abilities"] = C.M(stats, "tech_abilities"), ["kinetic_light_damage_bonus"] = C.N(stats, "kinetic_light_damage_bonus"), ["laser_energy_damage_bonus"] = C.N(stats, "laser_energy_damage_bonus"), ["light_damage_multiplier"] = C.N(stats, "light_damage_multiplier", 1), ["energy_damage_multiplier"] = C.N(stats, "energy_damage_multiplier", 1), ["factory_id"] = C.L(d, "factory_site_id", int.MinValue), ["berth"] = C.I(d, "patrol_slot"), ["kind"] = C.S(d, "kind"), ["effects"] = stats.TryGetValue("_combat_effects", out var effects) ? effects : Capture(stats, PerkEffectKeys) };
    private static DataMap SecondarySource(DataMap source)
    {
        var result = C.Shallow(source);
        result["primary"] = false;
        result["effects"] = Capture(C.M(source, "effects"), new[] { "big_target_damage_multiplier", "kill_spawn_reduction_seconds", "kill_spawn_reduction_cooldown" });
        return result;
    }
    private double LaunchDamage(DataMap d, DataMap stats, double damage)
    {
        d.TryAdd("first_fire_at", Clock);
        return Clock - C.N(d, "first_fire_at") < C.N(stats, "launch_damage_seconds") ? damage * Math.Max(1, C.N(stats, "launch_damage_multiplier", 1)) : damage;
    }
    public double ApplyDroneDamage(DataMap d, double damage)
    {
        if (damage <= 0 || C.N(d, "hp") <= 0)
            return 0;
        if (HasTech("D_A1") && !C.B(d, "reactive_layer_used"))
        {
            d["reactive_layer_used"] = true;
            damage *= .4;
        }
        var stats = GetProfile(d).Weapons;
        double reduced = damage * (1 - C.Clamp(C.N(stats, "drone_armor"), 0, .8)), actual = Math.Min(C.N(d, "hp"), reduced);
        d["hp"] = Math.Max(0, C.N(d, "hp") - reduced);
        if (C.N(d, "hp") > 0 && C.N(d, "hp") <= C.N(d, "max_hp") * .3 && HasTech("D_A2") && !C.B(d, "triage_used"))
        {
            d["triage_used"] = true;
            d["hp"] = Math.Min(C.N(d, "max_hp"), C.N(d, "hp") + C.N(d, "max_hp") * .15);
        }
        d["hit"] = .18;
        var at = GetDroneWorldPosition(d);
        AddDamageNumber(C.L(d, "uid"), at, actual, CombatScale.Coral);
        if (C.N(d, "hp") <= 0)
        {
            Drones.Remove(d);
            _droneById.Remove(C.L(d, "uid"));
            _actorProfiles.Remove(C.L(d, "uid"));
            DestroyedDrones++;
            long id = C.L(d, "factory_site_id");
            if (Factories.TryGetValue(id, out var f))
            {
                bool full = C.I(f, "active") >= C.I(f, "capacity");
                f["active"] = Math.Max(0, C.I(f, "active") - 1);
                if (full)
                    f["timer"] = 0d;
                CreditFactoryProduction(id, C.N(stats, "death_spawn_reduction_seconds"), false);
            }
            DetonateDroneDeath(at, stats, WeaponSource(d, stats));
        }
        return actual;
    }
    public void DetonateDroneDeath(Vector3 at, DataMap? stats = null, DataMap? source = null)
    {
        int epoch = _epoch;
        stats ??= _globalWeapons;
        double radius = Math.Max(0, C.N(stats, "death_blast_radius", .55)), damage = Math.Max(0, C.N(stats, "death_blast_damage", 35)), total = 0;
        var context = (source ?? new()).DeepClone();
        context.TryAdd("damage_type", "kinetic");
        context["primary"] = false;
        AddBurst(at, CombatScale.Cyan, radius * CombatScale.PlanetPixelRadius * .5);
        foreach (var e in Neighbors(at, radius, null, 0))
        {
            total += ApplyEnemyDamage(e, damage, CombatScale.Cyan, context);
            if (InvasionWon || epoch != _epoch)
                break;
        }
        if (HasTech("D_A3"))
            Game.ApplySacrificeRecovery(total);
    }
    public void DetonateMissile(Vector3 at, double damage, double radius = -1, DataMap? context = null)
    {
        int epoch = _epoch;
        if (radius < 0)
            radius = C.N(_globalWeapons, "missile_blast_radius", 96 / CombatScale.PlanetPixelRadius);
        AddBurst(at, CombatScale.Gold, 48);
        context = C.Shallow(context ?? C.Empty);
        context["damage_type"] = "explosive";
        var effects = C.M(context, "effects");
        if (C.S(context, "airframe_id") == "M2" && C.B(context, "primary") && effects.ContainsKey("m2_delayed_fraction"))
        {
            double fraction = C.Clamp(C.N(effects, "m2_delayed_fraction"), 0, .6);
            QueueDelayedBlast(at, radius, damage * fraction, C.N(effects, "m2_delayed_seconds", .35), SecondarySource(context));
            damage *= 1 - fraction;
        }
        var victims = Neighbors(at, radius, null, 0);
        double budget = C.N(context, "aoe_budget");
        if (budget > 0)
            damage *= Math.Min(1, budget / Math.Max(1, victims.Count));
        foreach (var e in victims)
        {
            if (ApplyEnemyDamage(e, damage, CombatScale.Gold, context) > 0)
                ApplySlow(e, effects);
            if (epoch != _epoch)
                return;
        }
    }
    public double ApplyEnemyDamage(DataMap e, double damage, Color? color = null, DataMap? context = null)
    {
        context ??= C.Empty;
        if (C.S(e, "kind") == "mothership" && !C.B(_globalWeapons, "mothership_assault_unlocked"))
            return 0;
        if (C.N(e, "hp") <= 0 || damage <= 0)
            return 0;
        var effects = C.M(context, "effects");
        if (C.Large(e))
            damage *= Math.Max(1, C.N(effects, "big_target_damage_multiplier", 1));
        var packet = C.Shallow(context);
        string type = C.S(packet, "damage_type", "kinetic");
        damage = DamageAbilityBonus(e, damage, type, packet);
        var result = EnemyArmor.Resolve(e, damage, type, packet, Clock);
        if (result.Total <= 0)
        {
            e["shield_flash"] = .12;
            return 0;
        }
        e["hit"] = .14;
        AddDamageNumber(C.L(e, "uid"), C.V(e, "space_position"), result.Total, color ?? CombatScale.Cyan);
        AfterAbilityHit(e, type, packet, result.Broke);
        if (C.N(e, "hp") <= 0)
        {
            CreditSourceKill(context);
            DestroyEnemy(e);
        }
        return result.Total;
    }
    private void DestroyEnemy(DataMap e)
    {
        if (C.S(e, "kind") == "mothership")
        {
            DestroyMothership(e);
            return;
        }
        if (!Enemies.Remove(e))
            return;
        _enemyById.Remove(C.L(e, "uid"));
        _targets.Remove(e);
        _sectorsDirty = true;
        foreach (var id in C.A(e, "energy_links"))
        {
            var ward = EnemyByUid(DataMap.Integer(id));
            if (ward != null && C.L(ward, "energy_owner_uid") == C.L(e, "uid"))
            {
                ward["energy_hp"] = 0d;
                ward["shield_broken"] = true;
                ward["energy_broken_at"] = Clock;
            }
        }
        if (C.S(e, "kind") == "boss")
            Game.ClaimFactoryBossReward(C.S(e, "reward_event_id", $"earth:wave:{C.L(e, "wave", Game.Wave)}:medium:0"), C.L(e, "wave", Game.Wave), C.I(e, "defense_stage"));
        Game.RewardEnemy(e);
        if (C.S(e, "kind") == "boss")
            EventNotice?.Invoke("中型 Boss 已击毁 · 外星科技与永久研究已回收");
        else if (C.S(e, "kind") == "small_boss" || C.B(e, "resource_core_carrier"))
            EventNotice?.Invoke($"小 Boss 已击毁 · 资源核心 +{Game.CombatSettings.I("resource_core_drop_count", 5)}");
        AddBurst(C.V(e, "space_position"), C.S(e, "kind") == "meteor" ? CombatScale.Gold : CombatScale.Coral, C.N(e, "size") * 1.6);
        if (C.B(e, "post_carrier") && _postSpawned >= C.I(_postPlan, "carrier_count", 1) && FrontierCohortStatus().I("alive") == 0)
        {
            _cohortComplete = true;
            CancelSpawnWindow();
        }
    }
    private void DamageEarth(double amount, Vector3 at, bool shieldAlreadyChecked = false)
    {
        if (Dead || amount <= 0 || Game.EarthHp <= 0)
            return;
        if (!shieldAlreadyChecked)
            amount = AbsorbPlanetaryImpact(amount, at);
        if (amount <= 0)
            return;
        double previous = Game.EarthHp;
        Game.TakeDamage(amount);
        double actual = previous - Game.EarthHp;
        if (actual > 0)
        {
            AddDamageNumber(-1, at, actual, CombatScale.Coral);
            EarthDamaged?.Invoke(actual, at);
        }
        AddBurst(at, CombatScale.Cyan, 25);
        if (Game.EarthHp <= 0)
        {
            Dead = true;
            WaveRunning = false;
            EarthDestroyed?.Invoke();
        }
    }
    private double DamageAbilityBonus(DataMap e, double damage, string type, DataMap source)
    {
        var tech = C.M(source, "tech_abilities");
        var actor = GetDroneByUid(C.L(source, "actor_uid", -1));
        var effects = C.M(source, "effects");
        string frame = C.S(source, "airframe_id");
        bool primary = C.B(source, "primary");
        if (type == "beam" && primary && EnemyArmor.Layer(e) == "energy" && C.B(tech, "L_A3") && actor != null && Clock >= C.N(actor, "first_energy_ready") && C.L(actor, "first_energy_uid", -1) != C.L(e, "uid"))
        {
            damage *= C.Packet(source, "laser_first_energy_multiplier", 1.6);
            actor["first_energy_ready"] = Clock + C.Packet(source, "laser_first_energy_cooldown", 3);
            actor["first_energy_uid"] = C.L(e, "uid");
        }
        if (frame == "K3" && primary && C.S(e, "enemy_role_id") is "weaver" or "hatcher" or "jammer" && actor != null)
        {
            var marked = C.A(e, "support_marked_by");
            if (!marked.Any(v => DataMap.Integer(v) == C.L(actor, "uid")) && marked.Count < 64)
            {
                damage *= C.N(effects, "k3_support_first_multiplier", 1);
                marked.Add(C.L(actor, "uid"));
                e["support_marked_by"] = marked;
            }
        }
        if (frame == "M3" && EnemyArmor.Layer(e) == "heavy")
            damage *= C.N(effects, "m3_drill_heavy_multiplier", 1);
        bool direct = primary && C.L(source, "impact_target_uid", C.L(e, "uid")) == C.L(e, "uid");
        if (type != "explosive" || !direct || EnemyArmor.Layer(e) != "heavy")
            return damage;
        if (C.B(tech, "M_G1") && C.Large(e))
            damage *= C.Packet(source, "missile_large_heavy_multiplier", 2);
        if (C.B(tech, "M_A3") && Clock >= C.N(e, "saturation_ready"))
        {
            e["saturation_hits"] = C.I(e, "saturation_hits") + 1;
            if (C.I(e, "saturation_hits") >= Math.Max(1, (int)C.Packet(source, "saturation_hit_count", 3)))
            {
                damage *= C.Packet(source, "saturation_damage_multiplier", 1.6);
                e["saturation_hits"] = 0;
                e["saturation_ready"] = Clock + C.Packet(source, "saturation_cooldown", 3);
            }
        }
        if (C.B(tech, "M_G2") && C.Large(e) && Clock >= C.N(e, "chain_ready"))
        {
            var ledger = e.TryGetValue("missile_chain", out var v) && v is DataMap map ? map : new DataMap();
            foreach (var key in ledger.Keys.ToArray())
                if (Clock - C.N(ledger, key) > C.Packet(source, "linked_factory_window", 2))
                    ledger.Remove(key);
            ledger[C.L(source, "factory_id", -1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = Clock;
            e["missile_chain"] = ledger;
            if (ledger.Count >= Math.Max(1, (int)C.Packet(source, "linked_factory_count", 3)))
            {
                damage *= C.Packet(source, "linked_damage_multiplier", 3);
                ledger.Clear();
                e["chain_ready"] = Clock + C.Packet(source, "linked_cooldown", 8);
            }
        }
        return damage;
    }
    private void AfterAbilityHit(DataMap e, string type, DataMap source, bool broke)
    {
        var tech = C.M(source, "tech_abilities");
        var actor = GetDroneByUid(C.L(source, "actor_uid", -1));
        var effects = C.M(source, "effects");
        if (actor != null && C.B(source, "primary"))
        {
            if (C.S(source, "airframe_id") == "K2" && effects.ContainsKey("k2_dense_hits_required"))
            {
                actor["dense_hits"] = C.I(actor, "dense_hits") + 1;
                if (C.I(actor, "dense_hits") >= C.I(effects, "k2_dense_hits_required"))
                {
                    actor["dense_until"] = Clock + C.N(effects, "k2_dense_duration", 3);
                    actor["dense_hits"] = 0;
                }
            }
            if (C.S(source, "airframe_id") == "M2" && EnemyArmor.Layer(e) == "heavy" && effects.ContainsKey("m2_shock_required"))
                actor["shock_hits"] = C.I(actor, "shock_hits") + 1;
        }
        if (type == "explosive" && C.B(source, "primary") && C.B(tech, "M_A2") && EnemyArmor.Layer(e) == "heavy")
        {
            e["armor_breach_until"] = Clock + C.Packet(source, "armor_breach_seconds", 2);
            e["armor_breach_coefficient"] = C.Packet(source, "breached_kinetic_heavy_coefficient", .35);
        }
        if (type == "kinetic" && EnemyArmor.Layer(e) == "light" && C.B(tech, "K_A2") && Clock >= C.N(e, "suppress_ready"))
        {
            e["fire"] = C.N(e, "fire") + C.Packet(source, "kinetic_suppress_seconds", .2) * (C.Large(e) ? .5 : 1);
            e["suppress_ready"] = Clock + 1;
        }
        if (type == "beam" && C.B(tech, "L_A2"))
            e["energy_recovery_blocked_until"] = Clock + C.Packet(source, "laser_regen_delay", 2);
        if (broke)
        {
            AddBurst(C.V(e, "space_position"), new("83cfff"), 24);
            if (type == "beam" && C.B(tech, "L_A1") && !C.B(e, "shield_unloaded"))
            {
                e["shield_unloaded"] = true;
                e["stagger_until"] = Clock + C.Packet(source, "shield_break_stagger_seconds", .4) * (C.Large(e) ? .5 : 1);
            }
            if (type == "beam" && C.B(tech, "L_G2") && !C.B(e, "shield_field_used"))
            {
                e["shield_field_used"] = true;
                CreateSuppressionField(C.V(e, "space_position"), C.Packet(source, "shield_break_field_radius", 1.4), C.Packet(source, "shield_break_field_duration", 2));
            }
        }
    }
    private void CreditSourceKill(DataMap source)
    {
        var actor = GetDroneByUid(C.L(source, "actor_uid", -1));
        var perk = C.M(source, "effects");
        if (actor != null && perk.ContainsKey("k2_reload_reduction_seconds") && Clock >= C.N(actor, "reload_ready"))
        {
            actor["fire"] = Math.Max(.02, C.N(actor, "fire") - C.N(perk, "k2_reload_reduction_seconds"));
            actor["reload_ready"] = Clock + Math.Max(1, C.N(perk, "k2_reload_cooldown", 1));
        }
        long site = C.L(source, "factory_id", int.MinValue);
        if (Factories.TryGetValue(site, out var f) && C.S(f, "kind") == C.S(source, "kind"))
            CreditFactoryProduction(site, C.Clamp(C.N(perk, "kill_spawn_reduction_seconds"), 0, .85), true, C.N(perk, "kill_spawn_reduction_cooldown", 1));
    }
    private void ApplyErosion(DataMap e, DataMap effects)
    {
        double strength = C.Clamp(C.N(effects, "erosion_damage_per_stack"), 0, .1);
        if (strength <= 0 || C.N(e, "hp") <= 0)
            return;
        int previous = C.N(e, "perk_erosion_remaining") > 0 ? C.I(e, "perk_erosion_stacks") : 0;
        e["perk_erosion_layer"] = EnemyArmor.Layer(e);
        e["perk_erosion_stacks"] = Math.Min(Math.Clamp(C.I(effects, "erosion_max_stacks", 10), 1, 10), previous + 1);
        e["perk_erosion_per_stack"] = Math.Max(strength, previous > 0 ? C.N(e, "perk_erosion_per_stack") : 0);
        e["perk_erosion_remaining"] = C.Clamp(C.N(effects, "erosion_duration", 3), .1, 10);
        if (previous == 0)
            AddBurst(C.V(e, "space_position"), CombatScale.Violet, 8);
    }
    private void ApplySlow(DataMap e, DataMap effects)
    {
        double fraction = C.Clamp(C.N(effects, "slow_fraction"), 0, .6);
        if (fraction <= 0 || C.N(e, "hp") <= 0)
            return;
        if (C.S(e, "kind") is "small_boss" or "boss" or "carrier" or "mothership")
            fraction *= C.Clamp(C.N(effects, "slow_boss_multiplier", .5), 0, 1);
        if (C.N(e, "perk_slow_remaining") <= 0)
            AddBurst(C.V(e, "space_position"), new("82d6f2"), 9);
        e["perk_slow_fraction"] = Math.Max(fraction, C.N(e, "perk_slow_fraction"));
        e["perk_slow_remaining"] = C.Clamp(C.N(effects, "slow_duration", 2.5), .1, 10);
    }
    private double MovementMultiplier(DataMap e) => C.N(e, "perk_slow_remaining") > 0 ? 1 - C.Clamp(C.N(e, "perk_slow_fraction"), 0, .6) : 1;
    private void UpdatePerkStatus(DataMap e, double dt)
    {
        foreach (var prefix in new[] { "perk_slow", "perk_erosion" })
        {
            string key = prefix + "_remaining";
            if (!e.ContainsKey(key))
                continue;
            e[key] = Math.Max(0, C.N(e, key) - Math.Max(dt, 0));
            if (C.N(e, key) > 0)
                continue;
            e.Remove(key);
            if (prefix == "perk_slow")
                e.Remove("perk_slow_fraction");
            else
            {
                e.Remove("perk_erosion_stacks");
                e.Remove("perk_erosion_per_stack");
            }
        }
    }
    private void AddBeam(Vector3 origin, Vector3 destination, Color color)
    {
        if (Beams.Count < 256)
            Beams.Add(new()
            {
                ["from_space"] = origin,
                ["to_space"] = destination,
                ["life"] = .17,
                ["max_life"] = .17,
                ["color"] = color
            });
    }
    private void AddDamageNumber(long uid, Vector3 at, double amount, Color color)
    {
        for (int i = DamageNumbers.Count - 1; i > Math.Max(-1, DamageNumbers.Count - 16); i--)
        {
            var n = DamageNumbers[i];
            if (C.L(n, "uid") == uid && C.N(n, "age") < .08 && C.V(n, "space_position").DistanceSquaredTo(at) < .04)
            {
                n["amount"] = C.N(n, "amount") + amount;
                return;
            }
        }
        NumberSequence++;
        DamageNumbers.Add(new()
        {
            ["uid"] = uid,
            ["space_position"] = at,
            ["amount"] = amount,
            ["color"] = color,
            ["age"] = 0d,
            ["life"] = 1d,
            ["offset"] = (NumberSequence % 3 - 1) * 8d
        });
        if (DamageNumbers.Count > 180)
            DamageNumbers.RemoveAt(0);
    }
    private void AddBurst(Vector3 at, Color color, double radius)
    {
        Bursts.Add(new()
        {
            ["uid"] = NewUid(),
            ["space_position"] = at,
            ["color"] = color,
            ["radius"] = radius,
            ["radius_world"] = radius * 2 / CombatScale.PlanetPixelRadius,
            ["age"] = 0d,
            ["life"] = .5
        });
        if (Bursts.Count > 120)
            Bursts.RemoveAt(0);
    }
    private void UpdateEffects(double dt)
    {
        foreach (var list in new[] { DamageNumbers, Bursts })
            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i]["age"] = C.N(list[i], "age") + dt;
                if (C.N(list[i], "age") >= C.N(list[i], "life"))
                    list.RemoveAt(i);
            }
        for (int i = Beams.Count - 1; i >= 0; i--)
        {
            Beams[i]["life"] = C.N(Beams[i], "life") - dt;
            if (C.N(Beams[i], "life") <= 0)
                Beams.RemoveAt(i);
        }
    }
}

