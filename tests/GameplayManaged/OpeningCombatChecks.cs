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
        Near(catalog.Values.DamageBase, 9, "campaign enemy base DPS remains nine");
        Near(catalog.Values.GroundDamageFloor, .03, "ground impact floor stays independently configurable");
        Near(catalog.Values.GroundDamageMultiplier, .0075, "only the configured fraction of enemy shot damage reaches Earth");
        Near(settings.N("enemy_health_growth"), .025, "enemy health grows by two and a half percent per wave");
        Near(settings.N("enemy_damage_growth"), .012, "enemy damage grows by one point two percent per wave");
        Near(catalog.Values.OpeningDamageMultiplier, .35, "opening damage reduction is configurable and leaves a novice response window");
        Near(catalog.Values.OpeningDamageHoldThroughWave, 20, "full opening reduction is held through wave twenty");
        Near(catalog.Values.OpeningDamageFullWave, 40, "ordinary threat curve returns at wave forty");
        foreach (var (wave, ramp) in new[] { (1, .35), (2, .35), (3, .35), (5, .35), (10, .35), (19, .35), (20, .35), (21, .3825), (30, .675), (39, .9675), (40, 1d), (41, 1d), (60, 1d) })
        {
            Near(catalog.Values.DamageMultiplierForWave(wave), ramp, "opening damage ramp boundary at wave " + wave);
            var unit = new DataMap { ["kind"] = "scout" };
            EnemyCatalog.Apply(unit, new DataMap { ["wave"] = wave, ["role"] = "claw", ["index"] = 0 }, settings);
            var data = catalog.Enemy("scout", "claw", wave);
            double opening = wave == 1 ? settings.N("first_wave_enemy_damage_multiplier") : 1;
            double shotDamage = 9 * data.Dps * Math.Pow(1.012, wave - 1) * catalog.StageDamage[0] * opening * data.Cooldown * ramp;
            Near(unit.N("attack_damage"), shotDamage, "actual ordinary per-shot damage includes the lower base and opening ramp at wave " + wave);
            Near(unit.N("ground_damage"), Math.Max(.03 * ramp, shotDamage * .0075), "Earth bombardment applies the opening ramp to damage and its minimum at wave " + wave);
            double legacyRamp = wave <= 10 ? .35 : wave >= 20 ? 1 : .35 + .065 * (wave - 10);
            double legacyShot = 11.7 * data.Dps * Math.Pow(1.018, wave - 1) * catalog.StageDamage[0] * opening * data.Cooldown * legacyRamp;
            Check(unit.N("ground_damage") < Math.Max(.75 * legacyRamp, legacyShot * .12), "actual Earth damage is below the previous campaign at wave " + wave);
            double health = 29.25 * data.Health * Math.Pow(1.025, wave - 1) * catalog.StageHealth[0]
                * settings.N("enemy_health") / 29 * (wave == 1 ? settings.N("first_wave_enemy_health_multiplier") : 1);
            Near(unit.N("max_hp"), health, "actual enemy health follows the slower campaign growth at wave " + wave);
            if (wave == 1)
            {
                Near(unit.N("attack_damage"), 1.819125, "first ordinary claw shot retains its independent damage regression value");
                Near(unit.N("ground_damage"), .0136434375, "first claw Earth impact is small but nonzero");
            }
            var elite = new DataMap { ["kind"] = "scout" };
            EnemyCatalog.Apply(elite, new DataMap { ["wave"] = wave, ["role"] = "claw", ["index"] = 12 }, settings);
            Check(elite.B("elite") == (wave >= 5), "elite pressure starts at wave five: " + wave);
            Near(elite.N("attack_damage"), unit.N("attack_damage") * (wave >= 5 ? 1.5 : 1), "elite shot multiplier follows the gentler opening curve at wave " + wave);
        }
        Near(catalog.Values.DamageMultiplierForWave(21) / catalog.Values.DamageMultiplierForWave(20), .3825 / .35,
            "wave twenty-one eases into recovery instead of abruptly removing the damage buffer");
        double previousRamp = catalog.Values.DamageMultiplierForWave(20);
        for (int wave = 21; wave <= 40; wave++)
        {
            double ramp = catalog.Values.DamageMultiplierForWave(wave);
            Near(ramp - previousRamp, .0325, "recovery proceeds by a consistent small increment at wave " + wave);
            previousRamp = ramp;
        }
        string tuningCsv = File.ReadAllText("data/domain/combat_tuning.csv");
        var customTuning = new CombatCatalog.Tuning(CsvTable.Parse(tuningCsv
            .Replace("OpeningDamageMultiplier,0.35,", "OpeningDamageMultiplier,0.4,")
            .Replace("OpeningDamageHoldThroughWave,20,", "OpeningDamageHoldThroughWave,8,")
            .Replace("OpeningDamageFullWave,40,", "OpeningDamageFullWave,16,")));
        Near(customTuning.DamageMultiplierForWave(8), .4, "edited multiplier and hold boundary affect the curve");
        Near(customTuning.DamageMultiplierForWave(12), .7, "edited endpoints produce the expected midpoint");
        Near(customTuning.DamageMultiplierForWave(16), 1, "edited full-strength wave is honored");
        foreach (var (from, to) in new[]
        {
            ("OpeningDamageMultiplier,0.35,", "OpeningDamageMultiplier,0,"),
            ("OpeningDamageHoldThroughWave,20,", "OpeningDamageHoldThroughWave,20.5,"),
            ("OpeningDamageFullWave,40,", "OpeningDamageFullWave,20,")
        })
        {
            bool rejected = false;
            try { _ = new CombatCatalog.Tuning(CsvTable.Parse(tuningCsv.Replace(from, to))); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "invalid opening damage configuration is rejected: " + to);
        }
        string root = Path.GetFullPath("data/domain");
        string floorOnlyCsv = tuningCsv.Replace("GroundDamageMultiplier,0.0075,", "GroundDamageMultiplier,0,");
        Check(floorOnlyCsv != tuningCsv, "floor fixture edits the real ground-conversion parameter");
        try
        {
            CatalogData.Configure(file => file == "combat_tuning.csv" ? floorOnlyCsv : File.ReadAllText(Path.Combine(root, file)));
            foreach (var (wave, expected) in new[] { (1, .0105), (20, .0105), (30, .02025), (40, .03), (60, .03) })
            {
                var unit = new DataMap { ["kind"] = "scout" };
                EnemyCatalog.Apply(unit, new DataMap { ["wave"] = wave, ["role"] = "claw", ["index"] = 0 }, settings);
                Near(unit.N("ground_damage"), expected, "actual enemy ground minimum honors ramp boundaries when conversion is zero at wave " + wave);
                Check(unit.N("attack_damage") > unit.N("ground_damage"), "ground-only tuning preserves aircraft combat damage at wave " + wave);
            }
        }
        finally { CatalogData.Configure(root); }
        Near(settings.N("enemy_bullet_growth_interval"), 180, "bullet-count escalation takes three minutes per step");
    }
}
