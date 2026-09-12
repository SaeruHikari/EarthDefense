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
            for (int enemy = 0; enemy < 20; enemy++) rewards.RewardKill("scout", wave);
            rewards.RewardKill("small_boss", wave);
            if (wave == 3) rewards.RewardKill("boss", wave);
            rewards.RewardWave(wave);
            Near(rewards.Science, 0, "kills and wave completion cannot accelerate science in opening wave " + wave);
        }
        foreach (string kind in new[] { "cruiser", "carrier", "mothership" })
        {
            rewards.RewardKill(kind, 10);
            Near(rewards.Science, 0, "later enemy reward also keeps science on its controlled income clock: " + kind);
        }
        Check(rewards.AlienPoints > 0 && rewards.ResourceCores > 0, "ordinary boss progression currencies are still awarded");
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

        var preferenceGame = new DefenseState();
        Check(preferenceGame.SetCombatSetting("drone_damage", 13) && preferenceGame.SetCombatSetting("enemy_count_multiplier", 3),
            "fixture stores unrelated custom damage and count multiplier");
        var oldValues = new Dictionary<string, double>
        {
            ["enemy_wave_base_count"] = 10, ["enemy_wave_growth"] = 2,
            ["enemy_wave_duration"] = 45, ["enemy_spawn_duration"] = 30,
            ["enemy_bullet_growth_interval"] = 90
        };
        var oldGrowth = new Dictionary<string, double>
        {
            ["enemy_health_growth"] = .035, ["enemy_damage_growth"] = .018
        };
        var oldPreferences = preferenceGame.SerializeCombatPreferences();
        oldPreferences.Remove("balance_revision");
        foreach (var (key, value) in oldValues.Concat(oldGrowth)) oldPreferences.Map("settings")[key] = value;
        string oldPreferencesBefore = oldPreferences.ToJson();
        var readPreferences = preferenceGame.ReadCombatPreferences(oldPreferences);
        Check(readPreferences != null, "previous preference shape loads with the new balance revision");
        foreach (string key in oldValues.Keys)
            Near(readPreferences!.N(key), DefenseState.DefaultCombatSettings.N(key), "old saved default refreshes only pacing parameter " + key);
        foreach (string key in oldGrowth.Keys)
            Near(readPreferences!.N(key), DefenseState.DefaultCombatSettings.N(key), "unversioned preferences also adopt updated default growth " + key);
        Near(readPreferences!.N("drone_damage"), 13, "old preferences retain custom friendly damage");
        Near(readPreferences.N("enemy_count_multiplier"), 3, "old preferences retain chosen enemy quantity multiplier");
        Check(oldPreferences.ToJson() == oldPreferencesBefore, "reading old preferences never mutates input data");
        var oldRun = preferenceGame.Serialize();
        oldRun.Remove("balance_revision");
        oldRun["science"] = 37d;
        foreach (var (key, value) in oldValues.Concat(oldGrowth)) oldRun.Map("combat_settings")[key] = value;
        var runCopy = new DefenseState();
        Check(runCopy.Restore(oldRun), "previous live run upgrades the same five pacing defaults");
        foreach (string key in oldValues.Keys)
            Near(runCopy.CombatSettings.N(key), DefenseState.DefaultCombatSettings.N(key), "old checkpoint refreshes pacing parameter " + key);
        foreach (string key in oldGrowth.Keys)
            Near(runCopy.CombatSettings.N(key), DefenseState.DefaultCombatSettings.N(key), "unversioned checkpoint also adopts updated default growth " + key);
        Near(runCopy.CombatSettings.N("drone_damage"), 13, "checkpoint preserves custom damage");
        Near(runCopy.CombatSettings.N("enemy_count_multiplier"), 3, "checkpoint preserves custom multiplier");
        Near(runCopy.Science, 37, "balance refresh does not erase science already earned in the saved run");
        Check(runCopy.Serialize().I("balance_revision") == DefenseState.CombatBalanceRevision, "re-saved checkpoint records the completed balance update");

        Check(preferenceGame.SetCombatSetting("enemy_wave_duration", 90), "revision-one fixture has a custom ninety-second cycle");
        foreach (bool customGrowth in new[] { false, true })
        {
            var revisionOnePreferences = preferenceGame.SerializeCombatPreferences();
            revisionOnePreferences["balance_revision"] = 1L;
            var revisionOneRun = preferenceGame.Serialize();
            revisionOneRun["balance_revision"] = 1L;
            revisionOneRun["science"] = 37d;
            foreach (var (key, previousDefault) in oldGrowth)
            {
                double storedGrowth = customGrowth ? previousDefault + .01 : previousDefault;
                revisionOnePreferences.Map("settings")[key] = storedGrowth;
                revisionOneRun.Map("combat_settings")[key] = storedGrowth;
            }
            string preferencesBefore = revisionOnePreferences.ToJson(), runBefore = revisionOneRun.ToJson();
            var loadedPreferences = DataMap.Parse(preferencesBefore);
            var loadedRun = DataMap.Parse(runBefore);
            var revisionTwoValues = preferenceGame.ReadCombatPreferences(loadedPreferences);
            var revisionTwoRun = new DefenseState();
            Check(revisionTwoValues != null && revisionTwoRun.Restore(loadedRun),
                "revision-one preferences and checkpoint restore with " + (customGrowth ? "custom" : "default") + " growth");
            foreach (var (key, previousDefault) in oldGrowth)
            {
                double expected = customGrowth ? previousDefault + .01 : DefenseState.DefaultCombatSettings.N(key);
                Near(revisionTwoValues!.N(key), expected, "revision-one preferences distinguish default from custom growth " + key);
                Near(revisionTwoRun.CombatSettings.N(key), expected, "revision-one checkpoint distinguishes default from custom growth " + key);
            }
            Near(revisionTwoValues!.N("enemy_wave_duration"), 90, "growth update preserves revision-one custom preference pacing");
            Near(revisionTwoRun.CombatSettings.N("enemy_wave_duration"), 90, "growth update preserves revision-one custom checkpoint pacing");
            Near(revisionTwoValues.N("drone_damage"), 13, "growth update preserves unrelated custom damage preferences");
            Near(revisionTwoRun.CombatSettings.N("enemy_count_multiplier"), 3, "growth update preserves unrelated custom enemy count");
            Near(revisionTwoRun.Science, 37, "growth update preserves earned checkpoint science");
            Check(loadedPreferences.ToJson() == preferencesBefore && loadedRun.ToJson() == runBefore,
                "growth migration leaves both source documents unchanged");
            Check(revisionTwoRun.Serialize().I("balance_revision") == DefenseState.CombatBalanceRevision,
                "revision-one checkpoint re-saves with the current balance marker");
        }

        Check(runCopy.SetCombatSetting("enemy_wave_duration", 90), "player can explicitly select a different cycle after the update");
        foreach (var (key, previousDefault) in oldGrowth)
            Check(runCopy.SetCombatSetting(key, previousDefault), "player may explicitly choose former growth after the update " + key);
        var currentPreferences = runCopy.SerializeCombatPreferences();
        Near(runCopy.ReadCombatPreferences(currentPreferences)!.N("enemy_wave_duration"), 90, "current preference revision retains ninety-second custom cycle");
        var currentCopy = new DefenseState();
        Check(currentCopy.Restore(runCopy.Serialize()), "current revision checkpoint restores");
        Near(currentCopy.CombatSettings.N("enemy_wave_duration"), 90, "current checkpoint retains ninety-second custom cycle");
        foreach (var (key, previousDefault) in oldGrowth)
        {
            Near(runCopy.ReadCombatPreferences(currentPreferences)!.N(key), previousDefault, "current preferences do not re-migrate an intentionally chosen former growth " + key);
            Near(currentCopy.CombatSettings.N(key), previousDefault, "current checkpoint does not re-migrate an intentionally chosen former growth " + key);
        }
        foreach (object invalid in new object[] { -.1, .5, DefenseState.CombatBalanceRevision + 1, "1", true })
        {
            var invalidPreferences = currentPreferences.DeepClone();
            invalidPreferences["balance_revision"] = invalid;
            Check(runCopy.ReadCombatPreferences(invalidPreferences) == null, "invalid preference balance marker rejected: " + invalid);
            var invalidRun = runCopy.Serialize();
            invalidRun["balance_revision"] = invalid;
            var before = currentCopy.Serialize();
            Check(!currentCopy.Restore(invalidRun) && DataMap.Equivalent(before, currentCopy.Serialize()), "invalid checkpoint revision rejects atomically: " + invalid);
        }
        var extraPreference = currentPreferences.DeepClone(); extraPreference["unexpected"] = true;
        Check(runCopy.ReadCombatPreferences(extraPreference) == null, "unknown preference envelope keys remain invalid");
    }
}
