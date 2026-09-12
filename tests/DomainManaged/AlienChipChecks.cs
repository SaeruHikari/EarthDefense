using Earthward.Domain;

internal static class AlienChipChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool ok, string label) => check(ok, "Alien chips: " + label);
        var perks = new FactoryPerks();
        Check(perks.AlienChips == 0 && PerkCatalog.Entries.All(row => perks.GetLevel(row.S("id")) == 0), "new profile starts without chips or purchased perks");
        var empty = perks.Snapshot();
        Check(!perks.Purchase("a_kinetic_core") && !perks.Upgrade("a_kinetic_core") && DataMap.Equivalent(empty, perks.Snapshot()), "insufficient chips and unowned upgrade reject without mutating profile");
        Check(perks.PurchaseCost("missing") == 0 && !perks.Purchase("missing"), "unknown perk cannot be bought");
        Check(!perks.CreditAlienChips(0) && !perks.CreditAlienChips(-1), "invalid chip credit rejected");
        Check(perks.CreditAlienChips(100000), "isolated economy fixture credits actual currency API");
        long before = perks.AlienChips;
        Check(perks.PurchaseCost("a_kinetic_core") == 3 && perks.Purchase("a_kinetic_core") && perks.GetLevel("a_kinetic_core") == 1 && perks.AlienChips == before - 3, "chosen normal perk purchase costs three chips and creates level one");
        var owned = perks.Snapshot();
        Check(perks.PurchaseCost("a_kinetic_core") == 0 && !perks.Purchase("a_kinetic_core") && DataMap.Equivalent(owned, perks.Snapshot()), "duplicate purchase neither charges nor increases level");
        Check(perks.SlotsFor("interceptor", -1, "aircraft").All(id => id.Length == 0), "purchase does not silently overwrite equipment");
        Check(perks.Equip("interceptor", -1, 0, "a_kinetic_core", "aircraft", -1, "K1"), "purchased level-one perk equips compatible aircraft");
        Check(!perks.Equip("laser", -1, 0, "a_kinetic_core", "aircraft", -1, "L1") && !perks.Equip("interceptor", -1, 0, "a_kinetic_core", "factory"), "family and layer incompatibilities remain enforced");
        long[] normalCosts = { 2, 3, 4, 5, 7, 9 };
        foreach (long expected in normalCosts)
        {
            before = perks.AlienChips;
            int level = perks.GetLevel("a_kinetic_core");
            Check(perks.UpgradeCost("a_kinetic_core") == expected && perks.Upgrade("a_kinetic_core") && perks.GetLevel("a_kinetic_core") == level + 1 && perks.AlienChips == before - expected, "normal permanent upgrade follows exact curve at level " + level);
        }
        Check(perks.Purchase("a_laser_crystal") && !perks.Equip("interceptor", -1, 1, "a_laser_crystal", "aircraft", -1, "K1"), "later family may be prepurchased but cannot bypass compatible equipment");
        var beforeAdvanced = perks.Snapshot();
        Check(perks.PurchaseCost("a_kinetic_pierce") == 10 && !perks.Purchase("a_kinetic_pierce") && DataMap.Equivalent(beforeAdvanced, perks.Snapshot()), "advanced price does not bypass permanent frontier milestone");
        Check(perks.MarkDefenseStage(1) && perks.AdvancedUnlocked, "real defense milestone opens advanced purchases");
        before = perks.AlienChips;
        Check(perks.Purchase("a_kinetic_pierce") && perks.AlienChips == before - 10 && perks.GetLevel("a_kinetic_pierce") == 1, "advanced purchase costs ten chips");
        long[] advancedCosts = { 5, 8, 12, 17, 26, 38 };
        foreach (long expected in advancedCosts)
        {
            before = perks.AlienChips;
            int level = perks.GetLevel("a_kinetic_pierce");
            Check(perks.UpgradeCost("a_kinetic_pierce") == expected && perks.Upgrade("a_kinetic_pierce") && perks.GetLevel("a_kinetic_pierce") == level + 1 && perks.AlienChips == before - expected, "advanced permanent upgrade follows exact curve at level " + level);
        }
        Check(perks.Purchase("a_k2_dense_fire") && !perks.Equip("interceptor", -1, 1, "a_k2_dense_fire", "aircraft", -1, "K1") && perks.Equip("interceptor", -1, 1, "a_k2_dense_fire", "aircraft", -1, "K2"), "advanced model-specific perk still requires its correct airframe");
        foreach (var row in PerkCatalog.Entries)
        {
            string id = row.S("id");
            if (!perks.IsUnlocked(id)) Check(perks.Purchase(id), "all 39 catalog perks have a valid explicit purchase path: " + id);
            Check(perks.GetLevel(id) > 0 && perks.UpgradeCost(id) > 0 && perks.EffectText(id).Length > 0, "purchased perk exposes upgrade cost and real effects: " + id);
        }
        before = perks.AlienChips;
        int invested = perks.GetLevel("a_kinetic_core");
        Check(perks.ResetRunSites("chip-new-run") && perks.AlienChips == before && perks.GetLevel("a_kinetic_core") == invested && perks.SlotsFor("interceptor", -1, "aircraft")[0] == "a_kinetic_core", "new run preserves chip balance, investment, and default loadout");
        var claims = new FactoryPerks();
        int combatConfigurationChanges = 0, currencyChanges = 0;
        claims.Changed += () => combatConfigurationChanges++;
        claims.CurrencyChanged += () => currencyChanges++;
        var batch = claims.ClaimAlienChips("run-a", new[] { "enemy-1", "enemy-2", "enemy-1" });
        Check(batch.B("ok") && batch.B("claimed") && batch.L("alien_chips") == 2 && claims.AlienChips == 2, "one atomic batch awards one chip per unique destroyed aircraft");
        Check(currencyChanges == 1 && combatConfigurationChanges == 0, "chip receipt refreshes currency once without invalidating thousands of aircraft profiles");
        var claimState = claims.Snapshot();
        batch = claims.ClaimAlienChips("run-a", new[] { "enemy-2", "enemy-1" });
        Check(batch.B("ok") && batch.B("duplicate") && !batch.B("claimed") && batch.L("alien_chips") == 0 && DataMap.Equivalent(claimState, claims.Snapshot()), "replayed batch has no economic or storage mutation");
        batch = claims.ClaimAlienChips("run-a", new[] { "enemy-3", "" });
        Check(!batch.B("ok") && DataMap.Equivalent(claimState, claims.Snapshot()) && !claims.HasClaimed("run-a", "enemy-3"), "invalid item atomically rejects whole batch and leaves valid item retryable");
        Check(claims.ClaimAlienChip("run-a", "enemy-3").B("claimed") && claims.ClaimAlienChip("run-b", "enemy-1").B("claimed") && claims.AlienChips == 4, "new event or new run receives its own single chip");
        Check(claims.Purchase("a_kinetic_core") && combatConfigurationChanges == 1, "real perk investment still invalidates combat stats exactly once");
        string folder = Path.GetFullPath(Path.Combine(".runtime-tests", "alien-chip-write-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "chips.json");
        var failing = new FactoryPerks();
        Check(failing.LoadProfile(path) && failing.CreditAlienChips(20), "real chip profile bound for transaction failures");
        string diskBefore = File.ReadAllText(path);
        var memoryBefore = failing.Snapshot();
        Directory.CreateDirectory(path + ".tmp");
        Check(!failing.Purchase("a_kinetic_core") && DataMap.Equivalent(memoryBefore, failing.Snapshot()) && File.ReadAllText(path) == diskBefore, "failed purchase write preserves currency, ownership, and disk");
        var rejectedBatch = failing.ClaimAlienChips("disk-run", new[] { "aircraft-a", "aircraft-b" });
        Check(!rejectedBatch.B("ok") && DataMap.Equivalent(memoryBefore, failing.Snapshot()) && !failing.HasClaimed("disk-run", "aircraft-a"), "failed reward write registers no partial receipts");
        Directory.Delete(path + ".tmp");
        Check(failing.ClaimAlienChips("disk-run", new[] { "aircraft-a", "aircraft-b" }).L("alien_chips") == 2 && failing.Purchase("a_kinetic_core"), "storage recovery can retry whole drop batch and purchase");
        var reloaded = new FactoryPerks();
        Check(reloaded.LoadProfile(path) && reloaded.AlienChips == 19 && reloaded.GetLevel("a_kinetic_core") == 1 && reloaded.ClaimAlienChip("disk-run", "aircraft-b").B("duplicate"), "reload preserves purchased level, exact balance, and permanent death receipt");
        var saturated = new FactoryPerks();
        Check(saturated.CreditAlienChips(FactoryPerks.MaxCurrency), "bounded currency fixture reaches limit");
        var full = saturated.Snapshot();
        Check(!saturated.CreditAlienChips(1) && !saturated.ClaimAlienChip("full", "enemy").B("ok") && DataMap.Equivalent(full, saturated.Snapshot()) && !saturated.HasClaimed("full", "enemy"), "overflow refuses currency and does not burn pending enemy claim");
        var maxed = perks.Snapshot();
        maxed.Map("levels")["a_kinetic_core"] = PerkCatalog.Definition("a_kinetic_core").L("max_level");
        Check(perks.ImportSnapshot(maxed) && perks.UpgradeCost("a_kinetic_core") == 0 && !perks.Upgrade("a_kinetic_core"), "max-level perk cannot consume chips");
    }
}
