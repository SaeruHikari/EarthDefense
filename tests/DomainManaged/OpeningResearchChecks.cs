using Earthward.Domain;

internal static class OpeningResearchChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string label) => check(value, "opening research: " + label);
        void Near(double actual, double expected, string label) => Check(Math.Abs(actual - expected) < 1e-7, label + $" ({actual}/{expected})");
        var game = new DefenseState();
        Near(game.Science, 0, "new deployment has no prepaid science");
        Check(!game.CanResearch("K_S01") && !game.PurchaseGroup("K_S01"), "paid technology cannot bypass the empty initial wallet");
        Check(game.PurchaseGroup("D_N4") && game.LocalShieldBuildLimit == 2, "free shield onboarding still unlocks two towers without waiting for science");
        game.Tick(60);
        Near(game.Science, 0, "waiting without a deployed satellite creates no science");
        Check(game.Build("satellite_launcher", 12) && game.BeginResearchSatelliteLaunch(), "ordinary free launcher and satellite flow starts from initial resources");
        game.Tick(30);
        Near(game.Science, 0, "launch-in-progress does not earn orbital science");
        Check(game.CompleteResearchSatelliteLaunch(), "orbital insertion activates the actual domain satellite");
        Near(game.Rates().N("science"), .08, "base satellite supplies 4.8 science per minute");

        // Exercise actual sequential purchases and income without a funded wallet,
        // free research cheat, or an assumed constant pace after the purchases.
        string[] route =
        {
            "K_S01", "K_S21", "K_S02", "K_S22", "K_S04", "K_S24", "K_S05", "K_S25",
            "C_S01", "C_S21", "C_S02", "C_S22", "M_N1", "M_S01", "M_S05"
        };
        double deployedAt = game.DefenseTime, previousPurchase = deployedAt, totalSpent = 0;
        foreach (string id in route)
        {
            double cost = DeepTechnology.Definition(id).Map("cost").N("science");
            int waited = 0;
            while (!game.CanResearch(id) && waited < 600)
            {
                game.Tick(.25);
                waited++;
            }
            Check(game.PurchaseGroup(id), "real income pays accessible technology " + id);
            double interval = game.DefenseTime - previousPurchase;
            Check(interval >= 49.5 && interval <= 100.5, "one opening upgrade takes 50-100 seconds: " + id + " / " + interval);
            previousPurchase = game.DefenseTime;
            totalSpent += cost;
        }
        Near(totalSpent, 72, "fifteen meaningful opening upgrades cost seventy-two science");
        Check(game.DefenseTime - deployedAt >= 900 && game.DefenseTime - deployedAt <= 900.5,
            "fifteen upgrades take fifteen minutes after deployment");
        Check(game.PurchasedResearchCount == 16, "free shield plus exactly fifteen purchased upgrades");
        Near(game.Science, (game.DefenseTime - deployedAt) * .08 - totalSpent, "income minus actual payments explains the entire science wallet");
        Near(DeepTechnology.Definition("M_N1").Map("cost").N("science"), 8, "first missile milestone costs about one hundred seconds");
        Near(DeepTechnology.Definition("L_N1").Map("cost").N("science"), 16, "laser unlock remains a saved-for milestone");

        var rewards = new DefenseState();
        for (int wave = 1; wave <= 3; wave++)
        {
            rewards.Wave = wave;
            for (int enemy = 0; enemy < 20; enemy++) rewards.RegisterEnemyLoot(new() { ["kind"] = "scout", ["hp"] = 0d, ["reward_event_id"] = $"opening:{wave}:{enemy}", ["wave"] = wave });
            Check(rewards.CollectLootBatch(rewards.PendingEnemyLoot.Select(row => row.S("id")).ToArray()), "explicit arrival settles opening enemy resources");
            rewards.RewardWave(wave);
            Near(rewards.Science, 0, "kills and wave completion cannot accelerate science in opening wave " + wave);
        }
        foreach (string kind in new[] { "carrier", "mothership" })
        {
            rewards.RegisterEnemyLoot(new() { ["kind"] = kind, ["hp"] = 0d, ["reward_event_id"] = "later:" + kind, ["wave"] = 10L });
            Check(rewards.CollectLootBatch(rewards.PendingEnemyLoot.Select(row => row.S("id")).ToArray()), "explicit arrival settles later enemy resources");
            Near(rewards.Science, 0, "later enemy reward also keeps science on its controlled income clock: " + kind);
        }
        Check(rewards.Kills == 62 && rewards.PendingEnemyLoot.Count == 0, "uniform aircraft and strategic vessels settle without any special boss kills");
        var restored = new DefenseState();
        Check(restored.Restore(game.Serialize()), "legitimately earned slow research progress restores");
        Near(restored.Science, game.Science, "restore neither gifts nor loses fractional science");
        game.EarthHp = 0;
        double atDefeat = game.Science, timeAtDefeat = game.DefenseTime;
        game.Tick(120);
        Near(game.Science, atDefeat, "defeat cannot continue farming research");
        Near(game.DefenseTime, timeAtDefeat, "defeat stops the same displayed play clock");
        Check(game.Reset(), "ordinary restart succeeds");
        Near(game.Science, 0, "new run resets science back to zero");
        Check(!game.ResearchSatelliteDeployed && game.PurchasedResearchCount == 0, "restart resets orbital production and local research together");

        var preferenceGame = new DefenseState { Science = 37 };
        var customValues = new Dictionary<string, double>
        {
            ["drone_damage"] = 13, ["enemy_count_multiplier"] = 3,
            ["enemy_wave_duration"] = 90, ["enemy_spawn_duration"] = 30,
            ["enemy_wave_base_count"] = 10, ["enemy_wave_growth"] = 2,
            ["enemy_bullet_growth_interval"] = 90,
            ["enemy_health_growth"] = .035, ["enemy_damage_growth"] = .018
        };
        foreach (var (key, value) in customValues)
            Check(preferenceGame.SetCombatSetting(key, value), "current preference accepts custom value " + key);
        var currentPreferences = preferenceGame.SerializeCombatPreferences();
        var currentRun = preferenceGame.Serialize();
        string preferencesBefore = currentPreferences.ToJson(), runBefore = currentRun.ToJson();
        var readPreferences = preferenceGame.ReadCombatPreferences(DataMap.Parse(preferencesBefore));
        var currentCopy = new DefenseState();
        Check(readPreferences != null && currentCopy.Restore(DataMap.Parse(runBefore)), "complete current preferences and run both restore");
        foreach (var (key, value) in customValues)
        {
            Near(readPreferences!.N(key), value, "current preferences preserve explicit custom value without migration " + key);
            Near(currentCopy.CombatSettings.N(key), value, "current checkpoint preserves explicit custom value without migration " + key);
        }
        Near(currentCopy.Science, 37, "restoring current parameters preserves earned resources");
        Check(currentPreferences.ToJson() == preferencesBefore && currentRun.ToJson() == runBefore,
            "current preference and run validation leaves both inputs unchanged");
        foreach (object? invalid in new object?[] { null, 0L, 1L, -.1, .5, DefenseState.CombatBalanceRevision + 1, "2", true })
        {
            var invalidPreferences = currentPreferences.DeepClone();
            var invalidRun = currentRun.DeepClone();
            if (invalid == null)
            {
                invalidPreferences.Remove("balance_revision");
                invalidRun.Remove("balance_revision");
            }
            else
            {
                invalidPreferences["balance_revision"] = invalid;
                invalidRun["balance_revision"] = invalid;
            }
            Check(preferenceGame.ReadCombatPreferences(invalidPreferences) == null,
                "missing, historical or malformed preference revision is rejected: " + (invalid ?? "missing"));
            var before = currentCopy.Serialize();
            Check(!currentCopy.Restore(invalidRun) && DataMap.Equivalent(before, currentCopy.Serialize()),
                "missing, historical or malformed run revision rejects atomically: " + (invalid ?? "missing"));
        }
        foreach (string mutation in new[] { "missing_parameter", "unknown_parameter", "invalid_value" })
        {
            var invalidPreferences = currentPreferences.DeepClone();
            var invalidRun = currentRun.DeepClone();
            foreach (var values in new[] { invalidPreferences.Map("settings"), invalidRun.Map("combat_settings") })
            {
                if (mutation == "missing_parameter") values.Remove("enemy_health");
                else if (mutation == "unknown_parameter") values["obsolete_parameter"] = 1d;
                else values["enemy_health"] = double.NaN;
            }
            var before = currentCopy.Serialize();
            Check(preferenceGame.ReadCombatPreferences(invalidPreferences) == null
                && !currentCopy.Restore(invalidRun) && DataMap.Equivalent(before, currentCopy.Serialize()),
                "current schema requires every parameter to be known, present and valid: " + mutation);
        }
        var extraPreference = currentPreferences.DeepClone(); extraPreference["unexpected"] = true;
        Check(preferenceGame.ReadCombatPreferences(extraPreference) == null, "unknown preference envelope keys remain invalid");
    }
}
