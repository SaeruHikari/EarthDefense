using Earthward.Domain;

internal static class EnemyLootChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string label) => check(value, "Enemy loot: " + label);
        var ordinary = new DefenseState();
        var initial = ordinary.Serialize();
        initial["run_id"] = "enemy-loot-deterministic-contract";
        Check(ordinary.Restore(initial), "fixed run identity restores before its first enemy");
        Check(DomainBalance.Value("resource_core_drop_chance") == .005 && DomainBalance.Value("alien_point_drop_chance") == .0025
            && PerkCatalog.AlienChipDropChance == .03, "default core, technology point and chip chances are 0.5%, 0.25% and 3%");
        Check(!ordinary.CombatSettings.ContainsKey("medium_boss_first_wave") && !ordinary.CombatSettings.ContainsKey("medium_boss_interval")
            && !ordinary.CombatSettings.ContainsKey("resource_core_drop_count"), "removed enemy variants leave no boss settings");
        Check(DeepTechnology.Nodes.All(node => !node.Map("unlock").ContainsKey("first_medium_boss_defeated"))
            && !ordinary.Serialize().Map("research_flags").ContainsKey("first_medium_boss_defeated"), "research no longer depends on a removed boss");
        var enemy = new DataMap { ["kind"] = "scout", ["hp"] = 0d, ["planned_uid"] = "ordinary:1", ["wave"] = 1L };
        var first = ordinary.RegisterEnemyLoot(enemy);
        Check(first.Count >= 2 && first.All(row => row.Count == 4) && ordinary.Kills == 1 && ordinary.Score == 30,
            "death creates currency descriptors and immediately updates only kill count and score");
        Check(ordinary.Minerals == initial.N("minerals") && ordinary.Energy == initial.N("energy") && ordinary.ResourceCores == 0
            && ordinary.AlienPoints == 0 && ordinary.FactoryPerks.AlienChips == 0, "unattended loot changes no currency");
        string mineralId = first.Single(row => row.S("currency") == "minerals").S("id");
        first.Single(row => row.S("currency") == "minerals")["amount"] = 9000d;
        Check(ordinary.PendingEnemyLoot.Single(row => row.S("id") == mineralId).N("amount") == 9, "descriptor callers cannot mutate escrow amounts");
        Check(ordinary.RegisterEnemyLoot(enemy).Count == 0 && ordinary.Kills == 1,
            "repeated death registration uses the same defeated-enemy ledger");
        var beforeUnknown = ordinary.Serialize();
        Check(!ordinary.CollectLootBatch(new[] { mineralId, "unknown" }) && DataMap.Equivalent(beforeUnknown, ordinary.Serialize()),
            "unknown receipt rejects the entire arrival batch before any credit");
        Check(ordinary.CollectLootBatch(new[] { mineralId, mineralId }) && ordinary.Minerals == initial.N("minerals") + 9
            && ordinary.IsLootSettled(mineralId) && !ordinary.HasPendingLoot(mineralId), "one arrival credits a duplicated receipt once");
        Check(ordinary.CollectLoot(mineralId) && ordinary.Minerals == initial.N("minerals") + 9, "settled receipt replay is idempotent");
        var saved = DataMap.Parse(ordinary.Serialize().ToJson());
        var reopened = new DefenseState();
        Check(reopened.Restore(saved) && reopened.IsLootSettled(mineralId) && reopened.PendingEnemyLoot.Count == ordinary.PendingEnemyLoot.Count
            && reopened.RegisterEnemyLoot(enemy).Count == 0, "JSON checkpoint preserves pending rewards and both settlement and death ledgers");
        Check(reopened.CollectLoot(mineralId) && reopened.Minerals == ordinary.Minerals, "restored settled receipt cannot pay again");
        foreach (string mutation in new[] { "old_version", "missing_loot", "duplicate_pending", "negative_amount", "foreign_id", "unknown_currency", "settled_overlap", "duplicate_settled" })
        {
            var invalid = saved.DeepClone();
            var pending = invalid.Map("enemy_loot").List("pending");
            var row = pending.OfType<DataMap>().First();
            switch (mutation)
            {
                case "old_version": invalid["version"] = 1L; break;
                case "missing_loot": invalid.Remove("enemy_loot"); break;
                case "duplicate_pending": pending.Add(row.DeepClone()); break;
                case "negative_amount": row["amount"] = -1d; break;
                case "foreign_id": row["id"] = "loot:" + new string('a', 64); break;
                case "unknown_currency": row["currency"] = "credits"; break;
                case "settled_overlap": invalid.Map("enemy_loot").List("settled").Add(row.S("id")); break;
                case "duplicate_settled": invalid.Map("enemy_loot").List("settled").Add(mineralId); break;
            }
            var before = reopened.Serialize();
            Check(!reopened.Restore(invalid) && DataMap.Equivalent(before, reopened.Serialize()), "malformed checkpoint refuses atomically: " + mutation);
        }

        double mineralsBefore = ordinary.Minerals;
        var secondEnemy = new DataMap { ["kind"] = "scout", ["hp"] = 0d, ["planned_uid"] = "ordinary:2", ["wave"] = 2L };
        ordinary.RegisterEnemyLoot(secondEnemy);
        Check(ordinary.Minerals == mineralsBefore && ordinary.Kills == 2, "a second stable enemy identity escrows its own reward");
        var nextCopy = new DefenseState();
        Check(nextCopy.Restore(ordinary.Serialize()) && nextCopy.RegisterEnemyLoot(secondEnemy).Count == 0, "stable enemy identity persists across restore");
        int pendingBefore = nextCopy.PendingEnemyLoot.Count;
        nextCopy.RegisterEnemyLoot(new() { ["kind"] = "scout", ["hp"] = 0d, ["planned_uid"] = "ordinary:3", ["wave"] = 2L });
        Check(nextCopy.PendingEnemyLoot.Count >= pendingBefore + 2 && nextCopy.Kills == 3, "distinct real enemy identities cannot collide after restore");
        var beforeLiving = nextCopy.Serialize();
        foreach (object? hp in new object?[] { null, 1d, double.NaN, double.PositiveInfinity })
        {
            var living = new DataMap { ["kind"] = "scout", ["reward_event_id"] = "invalid-health" };
            if (hp != null) living["hp"] = hp;
            Check(nextCopy.RegisterEnemyLoot(living).Count == 0 && DataMap.Equivalent(beforeLiving, nextCopy.Serialize()),
                "missing, living or nonfinite health cannot register a death");
        }
        var onlyMother = new DefenseState();
        onlyMother.RegisterEnemyLoot(new() { ["kind"] = "mothership", ["hp"] = 0d, ["reward_event_id"] = "mothership:1" });
        Check(onlyMother.PendingEnemyLoot.Count == 2 && onlyMother.PendingEnemyLoot.All(row => row.S("currency") is "minerals" or "energy")
            && onlyMother.ResourceCores == 0 && onlyMother.AlienPoints == 0, "mothership destruction has no guaranteed rare currencies");
        long scoreBefore = ordinary.Score;
        int countBefore = ordinary.PendingEnemyLoot.Count;
        mineralsBefore = ordinary.Minerals;
        ordinary.Wave = 1;
        ordinary.RewardWave(1);
        Check(ordinary.Minerals > mineralsBefore && ordinary.Score > scoreBefore && ordinary.PendingEnemyLoot.Count == countBefore,
            "wave rewards remain immediate without creating enemy loot");
        mineralsBefore = ordinary.Minerals;
        ordinary.Tick(1);
        Check(ordinary.Minerals > mineralsBefore && ordinary.PendingEnemyLoot.Count == countBefore, "passive income remains immediate");

        // Known identities make this distribution regression deterministic.
        for (int i = 0; i < 10000; i++)
            ordinary.RegisterEnemyLoot(new() { ["kind"] = "scout", ["hp"] = 0d, ["reward_event_id"] = "probability:" + i });
        var rare = ordinary.PendingEnemyLoot;
        int cores = rare.Count(row => row.S("currency") == "resource_cores"), points = rare.Count(row => row.S("currency") == "alien_points"), chips = rare.Count(row => row.S("currency") == "alien_chips");
        Check(cores is > 15 and < 90 && points is > 5 and < 60 && chips is > 200 and < 410,
            "ordinary aircraft generate independently rare physical currencies at the configured rates");
        Check(rare.Where(row => row.S("currency") is "resource_cores" or "alien_points" or "alien_chips").All(row => row.N("amount") == 1),
            "every rare enemy drop contains one unit");

        string catalogRoot = Path.GetFullPath("data/domain");
        try
        {
            CatalogData.Configure(file =>
            {
                string content = File.ReadAllText(Path.Combine(catalogRoot, file));
                return file switch
                {
                    "domain_balance.csv" => content.Replace("resource_core_drop_chance,0.005,", "resource_core_drop_chance,1,").Replace("alien_point_drop_chance,0.0025,", "alien_point_drop_chance,1,"),
                    "perk_economy.csv" => content.Replace("alien_chip_drop_chance,0.03", "alien_chip_drop_chance,1"),
                    "kill_rewards.csv" => content.Replace("scout,9,3,0,30", "scout,9,3,7,30"),
                    _ => content
                };
            });
            var transaction = new DefenseState();
            string folder = Path.GetFullPath(Path.Combine(".runtime-tests", "enemy-loot-write-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            string profile = Path.Combine(folder, "profile.json");
            Check(transaction.FactoryPerks.LoadProfile(profile), "arrival transaction binds an isolated real profile");
            transaction.RegisterEnemyLoot(new() { ["kind"] = "scout", ["hp"] = 0d, ["reward_event_id"] = "all-currencies:1" });
            transaction.RegisterEnemyLoot(new() { ["kind"] = "scout", ["hp"] = 0d, ["reward_event_id"] = "all-currencies:2" });
            var sixCurrencies = transaction.PendingEnemyLoot;
            string[] ids = sixCurrencies.Select(row => row.S("id")).ToArray();
            Check(sixCurrencies.Count == 12 && sixCurrencies.Select(row => row.S("currency")).Distinct().Count() == 6,
                "nonzero science and all configurable rare rolls become distinct physical receipts");
            int currencyWrites = 0, notifications = 0;
            transaction.FactoryPerks.CurrencyChanged += () => currencyWrites++;
            transaction.AlienChipDropped += _ => notifications++;
            transaction.Tick(1);
            var pendingSave = transaction.Serialize();
            var profileBefore = transaction.FactoryPerks.Snapshot();
            bool? reentrantCollection = null, reentrantReset = null, reentrantRestore = null;
            transaction.FactoryPerks.CurrencyChanged += () =>
            {
                reentrantCollection = transaction.CollectLootBatch(ids);
                reentrantReset = transaction.Reset();
                reentrantRestore = transaction.Restore(pendingSave);
            };
            Check(transaction.FactoryPerks.AlienChips == 0 && transaction.ResourceCores == 0 && transaction.AlienPoints == 0
                && transaction.PendingEnemyLoot.Count == 12 && notifications == 0, "time passing leaves even guaranteed rare currencies in physical pickups");
            Directory.CreateDirectory(profile + ".tmp");
            Check(!transaction.CollectLootBatch(ids) && DataMap.Equivalent(pendingSave, transaction.Serialize())
                && DataMap.Equivalent(profileBefore, transaction.FactoryPerks.Snapshot()) && currencyWrites == 0,
                "failed permanent write leaves every currency and every receipt pending without partial local credit");
            Directory.Delete(profile + ".tmp");
            Check(transaction.CollectLootBatch(ids) && transaction.PendingEnemyLoot.Count == 0 && currencyWrites == 1 && notifications == 1,
                "one retry commits both chips in one permanent transaction and settles the entire cluster");
            Check(reentrantCollection == false && reentrantReset == false && reentrantRestore == false,
                "synchronous permanent-currency callbacks cannot reenter collection or replace the run during settlement");
            Check(transaction.Minerals == pendingSave.N("minerals") + 18 && transaction.Energy == pendingSave.N("energy") + 6
                && transaction.Science == pendingSave.N("science") + 14 && transaction.ResourceCores == 2 && transaction.AlienPoints == 2
                && transaction.FactoryPerks.AlienChips == 2, "successful arrival credits all six exact amounts");
            var arrived = transaction.Serialize();
            Check(transaction.CollectLootBatch(ids) && DataMap.Equivalent(arrived, transaction.Serialize()) && currencyWrites == 1,
                "repeat arrival produces no local credit or permanent write");
            Check(transaction.Restore(pendingSave) && transaction.CollectLootBatch(ids) && transaction.FactoryPerks.AlienChips == 2
                && currencyWrites == 1 && notifications == 1, "replayed pre-arrival checkpoint reuses permanent chip receipts");
            transaction.RegisterEnemyLoot(new() { ["kind"] = "scout", ["hp"] = 0d, ["reward_event_id"] = "abandoned" });
            string abandoned = transaction.PendingEnemyLoot.First().S("id");
            Check(transaction.Reset() && transaction.PendingEnemyLoot.Count == 0 && transaction.FactoryPerks.AlienChips == 2
                && !transaction.CollectLoot(abandoned) && !transaction.IsLootSettled(ids[0]), "new run discards all unattended loot and clears local receipts while preserving earned chips");
        }
        finally { CatalogData.Configure(catalogRoot); }
    }
}
