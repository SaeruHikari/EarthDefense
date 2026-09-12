using Earthward.Domain;

internal static class AchievementChecks
{
    internal static void Run(Action<bool, string> check)
    {
        const string id = "first_defeat";
        void Check(bool value, string label) => check(value, "achievements: " + label);
        string folder = Path.GetFullPath(Path.Combine(".runtime-tests", "domain-achievements-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        string profile = Path.Combine(folder, "achievements.json");

        Check(AchievementCatalog.Entries.Count == 1 && AchievementCatalog.Entries.Single().S("id") == id,
            "initial achievement catalog contains only the requested first-defeat entry");
        var game = new DefenseState();
        Check(game.Achievements.ProfilePath.Length == 0 && !File.Exists(profile), "new domain instance performs no implicit profile IO");
        Check(game.Achievements.LoadProfile(profile), "bind explicit isolated permanent profile");
        Check(!game.Achievements.HasUnlocked(id) && game.LocalShieldBuildLimit == 0, "new profile starts locked without granting research or capacity");
        Check(!game.RecordDefeat().B("ok") && !game.Achievements.HasUnlocked(id), "living Earth cannot claim first-defeat experience");
        Check(game.PurchaseGroup("D_N4") && game.LocalShieldBuildLimit == 1, "unearned profile retains ordinary one-generator opening");
        var beforeDefeat = game.Serialize();
        int unlockEvents = 0;
        game.Achievements.Unlocked += unlocked => { if (unlocked == id) unlockEvents++; };
        _ = game.TechEffects(); // Exercise cache invalidation when a permanent reward arrives mid-run.
        game.EarthHp = 0;
        var reward = game.RecordDefeat();
        Check(reward.B("ok") && reward.B("unlocked") && reward.S("id") == id, "first actual defeat automatically unlocks the only achievement");
        Check(game.Achievements.HasUnlocked(id) && File.Exists(profile), "first defeat is durably saved immediately");
        Check(game.LocalShieldBuildLimit == 2 && game.TechEffects().I("shield_building_limit_add") == 2,
            "reward invalidates cached effects and adds one to the already researched opening shield grant");
        string earnedBytes = File.ReadAllText(profile);
        var duplicate = game.RecordDefeat();
        Check(duplicate.B("ok") && duplicate.B("duplicate") && !duplicate.B("unlocked"), "repeated defeat notification is idempotent");
        Check(game.LocalShieldBuildLimit == 2 && File.ReadAllText(profile) == earnedBytes, "duplicate defeat cannot stack reward or rewrite the profile");
        Check(unlockEvents == 1 && game.Achievements.UnlockedCount == 1, "first and repeated defeat emit exactly one unlock and retain one achievement");

        Check(game.Restore(beforeDefeat) && game.EarthHp > 0 && game.Achievements.HasUnlocked(id) && game.LocalShieldBuildLimit == 2,
            "restoring a pre-defeat run preserves external permanent achievement and recalculates its reward");
        string previousRun = game.RunId;
        Check(game.Reset() && game.RunId != previousRun && game.Achievements.HasUnlocked(id), "new deployment preserves first-defeat unlock");
        Check(!game.HasResearch("D_N4") && game.LocalShieldBuildLimit == 0 && !game.BuildingUnlocked("shield"),
            "permanent experience never grants shield technology or construction before research");
        Check(game.PurchaseGroup("D_N4") && game.LocalShieldBuildLimit == 2, "new run opening research grants one plus one generators");
        Check(game.Build("shield", 301) && !game.CanBuild("shield") && game.LocalShieldBuildCooldownRemaining == 3,
            "bonus capacity preserves the three-wave construction cooldown");
        game.Wave = 3;
        Check(game.Build("shield", 302) && !game.CanBuild("shield") && game.Buildings.L("shield") == 2,
            "second earned slot is actually buildable and the two-generator cap still applies");

        game.Science = 1e9;
        game.AlienPoints = 1000000;
        game.Wave = game.CompletedWaves = 120;
        game.SetDefenseReachStage(3);
        game.RewardKill("boss", 120, 3);
        void Prepare(string research)
        {
            if (game.HasResearch(research)) return;
            foreach (string parent in DeepTechnology.Definition(research).List("requires").Cast<string>()) Prepare(parent);
            Check(game.PurchaseGroup(research), "actual required research purchase " + research);
        }
        foreach (string research in new[] { "D_N1", "D_N2", "D_N3", "D_G1", "D_G2" })
        {
            foreach (string parent in DeepTechnology.Definition(research).List("requires").Cast<string>()) Prepare(parent);
            int before = game.LocalShieldBuildLimit;
            int baseIncrement = DeepTechnology.Definition(research).Map("values").I("shield_building_limit_add");
            Check(game.PurchaseGroup(research), "later shield capacity node remains purchasable " + research);
            Check(game.LocalShieldBuildLimit == before + baseIncrement + 1,
                "each shield-capacity research adds its base value plus exactly one: " + research);
        }
        Check(game.LocalShieldBuildLimit == 14, "six eligible researches total ordinary eight plus six permanent bonus slots");
        int capacity = game.LocalShieldBuildLimit;
        Check(!game.PurchaseGroup("D_N4") && game.LocalShieldBuildLimit == capacity, "clicking owned research cannot reapply permanent increment");
        string priorProfile = File.ReadAllText(profile);
        Check(game.PurchaseGroup("D_S41") && game.LocalShieldBuildLimit == capacity, "non-capacity shield research does not add an extra slot");
        Check(File.ReadAllText(profile) == priorProfile, "ordinary research purchases do not mutate permanent achievement progress");

        var reopened = new DefenseState();
        Check(reopened.Achievements.LoadProfile(profile) && reopened.Achievements.HasUnlocked(id), "separate process domain instance reloads earned experience");
        Check(reopened.Restore(game.Serialize()) && reopened.LocalShieldBuildLimit == 14 && reopened.Buildings.L("shield") == 2,
            "post-achievement save reload restores built extra slots without duplicating earned effects");
        Check(reopened.Reset() && reopened.PurchaseGroup("D_N4") && reopened.LocalShieldBuildLimit == 2,
            "reload then new deployment retains exactly the same first-research bonus");

        var cheat = new DefenseState();
        Check(cheat.Achievements.LoadProfile(Path.Combine(folder, "cheat.json")) && cheat.UnlockAllTechnologyCheat(), "isolated all-research cheat fixture");
        Check(!cheat.Achievements.HasUnlocked(id) && cheat.LocalShieldBuildLimit == 8, "unlock-all research cheat never awards first-defeat experience");

        var noStorage = new DefenseState { EarthHp = 0 };
        Check(!noStorage.RecordDefeat().B("ok") && !noStorage.Achievements.HasUnlocked(id),
            "unbound profile cannot claim an unsaved permanent reward");
        string failedWritePath = Path.Combine(folder, "blocked-write.json");
        var failedWrite = new DefenseState { EarthHp = 0 };
        Check(failedWrite.Achievements.LoadProfile(failedWritePath), "bind independently blocked storage fixture");
        Directory.CreateDirectory(failedWritePath + ".lock");
        Check(!failedWrite.RecordDefeat().B("ok") && !failedWrite.Achievements.HasUnlocked(id) && !File.Exists(failedWritePath),
            "failed permanent write does not unlock in memory or leave a partial profile");
        Directory.Delete(failedWritePath + ".lock");
        Check(failedWrite.RecordDefeat().B("unlocked") && failedWrite.Achievements.HasUnlocked(id),
            "original defeat can be retried after fixing storage and then activates its reward");
        string badProfile = Path.Combine(folder, "corrupt.json");
        File.WriteAllText(badProfile, "invalid achievement profile");
        File.WriteAllText(badProfile + ".bak", "invalid backup");
        var corrupt = new DefenseState { EarthHp = 0 };
        Check(!corrupt.Achievements.LoadProfile(badProfile) && !corrupt.RecordDefeat().B("ok") && !corrupt.Achievements.HasUnlocked(id),
            "corrupt permanent storage cannot silently issue an unsaved reward");
        Check(File.ReadAllText(badProfile) == "invalid achievement profile" && File.ReadAllText(badProfile + ".bak") == "invalid backup",
            "failed profile load preserves both corrupt sources for recovery");
    }
}
