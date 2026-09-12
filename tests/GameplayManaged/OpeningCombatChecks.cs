using System.Reflection;
using Earthward.Combat;
using Earthward.Domain;

internal static class OpeningCombatChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string label) => check(value, "opening combat: " + label);
        void Near(double actual, double expected, string label) => Check(Math.Abs(actual - expected) < 1e-7, label + $" ({actual}/{expected})");
        var game = new DefenseState();
        var battle = new Battlefield(game);
        var makePlan = typeof(Battlefield).GetMethod("MakeWavePlan", BindingFlags.Instance | BindingFlags.NonPublic)!;
        battle.GetInvasionAnchor();
        long[] counts = { 9, 14, 16, 18, 20 };
        for (int wave = 1; wave <= counts.Length; wave++)
        {
            var plan = (DataMap)makePlan.Invoke(battle, new object[] { (long)wave })!;
            Check(plan.L("planned_count") == counts[wave - 1], "real invasion count is reduced at wave " + wave);
            Near(plan.N("duration"), 45, "deployment is spread across forty-five seconds at wave " + wave);
            Near(plan.N("cycle_duration"), 60, "every opening wave lasts sixty seconds at wave " + wave);
            var entries = Enumerable.Range(0, (int)plan.L("planned_count")).Select(index => DefenseWavePlan.Entry(plan, index)).ToList();
            Check(entries.Count(entry => entry.S("kind") == "small_boss") == 1, "reduced waves still include exactly one resource-core carrier at wave " + wave);
        }
        battle.StartWave();
        battle.AdvanceFixedSchedule(44.9);
        Check(game.Wave == 1 && battle.GetWaveSpawnPlan().L("spawned") == 8, "lower first-wave budget is distributed instead of arriving in a burst");
        battle.AdvanceFixedSchedule(.1);
        Check(battle.GetWaveSpawnPlan().L("spawned") == 9 && battle.WaveRemaining == 0 && game.Wave == 1, "all nine arrive at the end of the deployment window");
        battle.AdvanceFixedSchedule(14.9);
        Check(game.Wave == 1, "reduced count leaves a fifteen-second deployment break");
        battle.AdvanceFixedSchedule(.1);
        Check(game.Wave == 2 && battle.WaveTotal == 14, "second wave starts on its fixed minute boundary");
        Near(game.Science, 0, "actual wave completion does not bypass the satellite income limit");

        var settings = game.CombatSettings;
        var catalog = CombatCatalog.Current;
        Near(catalog.Values.OpeningDamageMultiplier, .35, "opening damage reduction is configurable and leaves a novice response window");
        Near(catalog.Values.OpeningDamageHoldThroughWave, 10, "full opening reduction is held through wave ten");
        Near(catalog.Values.OpeningDamageFullWave, 20, "ordinary threat curve returns at wave twenty");
        foreach (var (wave, ramp) in new[] { (1, .35), (2, .35), (3, .35), (5, .35), (10, .35), (11, .415), (15, .675), (20, 1d), (21, 1d) })
        {
            Near(catalog.Values.DamageMultiplierForWave(wave), ramp, "opening damage ramp boundary at wave " + wave);
            var unit = new DataMap { ["kind"] = "scout" };
            EnemyCatalog.Apply(unit, new DataMap { ["wave"] = wave, ["role"] = "claw", ["index"] = 0 }, settings);
            var data = catalog.Enemy("scout", "claw", wave);
            double opening = wave == 1 ? settings.N("first_wave_enemy_damage_multiplier") : 1;
            double oldDps = 19.5 * data.Dps * Math.Pow(1 + settings.N("enemy_damage_growth"), wave - 1) * catalog.StageDamage[0] * opening;
            Near(unit.N("attack_damage"), oldDps * data.Cooldown * .6 * ramp, "actual ordinary per-shot damage includes the lower base and opening ramp at wave " + wave);
            Near(unit.N("ground_damage"), Math.Max(.75 * ramp, oldDps * .6 * ramp * .12 * data.Cooldown), "Earth bombardment applies the opening ramp to damage and its minimum at wave " + wave);
            Check(unit.N("ground_damage") < Math.Max(1.5, oldDps * .12 * data.Cooldown), "Earth damage reduction is real rather than hidden by old minimum at wave " + wave);
            var elite = new DataMap { ["kind"] = "scout" };
            EnemyCatalog.Apply(elite, new DataMap { ["wave"] = wave, ["role"] = "claw", ["index"] = 12 }, settings);
            Check(elite.B("elite") == (wave >= 5), "elite pressure starts at wave five: " + wave);
            Near(elite.N("attack_damage"), unit.N("attack_damage") * (wave >= 5 ? 1.5 : 1), "elite shot multiplier follows the gentler opening curve at wave " + wave);
        }
        Near(catalog.Values.DamageMultiplierForWave(11) / catalog.Values.DamageMultiplierForWave(10), .415 / .35,
            "wave eleven eases into recovery instead of abruptly removing the damage buffer");
        double previousRamp = catalog.Values.DamageMultiplierForWave(10);
        for (int wave = 11; wave <= 20; wave++)
        {
            double ramp = catalog.Values.DamageMultiplierForWave(wave);
            Near(ramp - previousRamp, .065, "recovery proceeds by a consistent small increment at wave " + wave);
            previousRamp = ramp;
        }
        string tuningCsv = File.ReadAllText("data/domain/combat_tuning.csv");
        var customTuning = new CombatCatalog.Tuning(CsvTable.Parse(tuningCsv
            .Replace("OpeningDamageMultiplier,0.35,", "OpeningDamageMultiplier,0.4,")
            .Replace("OpeningDamageHoldThroughWave,10,", "OpeningDamageHoldThroughWave,8,")
            .Replace("OpeningDamageFullWave,20,", "OpeningDamageFullWave,16,")));
        Near(customTuning.DamageMultiplierForWave(8), .4, "edited multiplier and hold boundary affect the curve");
        Near(customTuning.DamageMultiplierForWave(12), .7, "edited endpoints produce the expected midpoint");
        Near(customTuning.DamageMultiplierForWave(16), 1, "edited full-strength wave is honored");
        foreach (var (from, to) in new[]
        {
            ("OpeningDamageMultiplier,0.35,", "OpeningDamageMultiplier,0,"),
            ("OpeningDamageHoldThroughWave,10,", "OpeningDamageHoldThroughWave,10.5,"),
            ("OpeningDamageFullWave,20,", "OpeningDamageFullWave,10,")
        })
        {
            bool rejected = false;
            try { _ = new CombatCatalog.Tuning(CsvTable.Parse(tuningCsv.Replace(from, to))); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "invalid opening damage configuration is rejected: " + to);
        }
        Near(settings.N("enemy_bullet_growth_interval"), 180, "bullet-count escalation takes three minutes per step");
    }
}
