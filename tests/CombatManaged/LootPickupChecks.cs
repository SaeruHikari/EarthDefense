using Earthward.Combat;
using Earthward.Domain;
using Godot;

internal static class LootPickupChecks
{
    private static readonly Vector3 DropPosition = Vector3.Back * (float)(CombatScale.EarthRadius + 3);

    public static void Run(Action<bool, string> check)
    {
        void Check(bool value, string label) => check(value, "loot pickup: " + label);
        ArrivalBoundary(Check);
        NearbyClusters(Check);
        PairedSaveAndValidation(Check);
        PermanentReceiptReplay(Check);
        FailedProfileWrite(Check);
    }

    private static (DefenseState Game, Battlefield Battle) Scene(string name)
    {
        var game = new DefenseState();
        var state = game.Serialize(); state["run_id"] = "loot-combat-contract-" + name;
        if (!game.Restore(state)) throw new InvalidOperationException("Cannot establish isolated loot test run.");
        return (game, new Battlefield(game));
    }

    private static DataMap Kill(Battlefield battle, string id)
    {
        var enemy = battle.SpawnEnemy("scout", new() { ["wave"] = 1L, ["role"] = "claw" }, DropPosition)!;
        enemy["reward_event_id"] = id;
        if (battle.ApplyEnemyDamage(enemy, 1e9) <= 0 || battle.Enemies.Contains(enemy))
            throw new InvalidOperationException("Fixture failed to destroy the actual ordinary aircraft.");
        return enemy;
    }

    private static DataMap Pickup(Battlefield battle, string currency, string phase = "world") =>
        battle.LootPickups.First(row => row.S("currency") == currency && row.S("phase") == phase);

    private static string ChipEnemyId(DefenseState game, string prefix)
    {
        for (int i = 0; i < 10000; i++)
        {
            string id = prefix + ":" + i;
            if (game.EnemyDropsAlienChip(new() { ["kind"] = "scout", ["hp"] = 0d, ["reward_event_id"] = id })) return id;
        }
        throw new InvalidOperationException("Deterministic chip test could not find a winning ordinary aircraft.");
    }

    private static void ArrivalBoundary(Action<bool, string> check)
    {
        var (game, battle) = Scene("arrival");
        double openingMinerals = game.Minerals, openingEnergy = game.Energy;
        var enemy = Kill(battle, "arrival-aircraft");
        var mineral = Pickup(battle, "minerals");
        long uid = mineral.L("uid"); double amount = mineral.N("amount");
        check(game.Kills == 1 && game.Minerals == openingMinerals && game.Energy == openingEnergy,
            "a real kill records combat progress but leaves every wallet unchanged");
        check(battle.LootPickups.All(row => row.S("phase") == "world" && row.Vector3("space_position").Length() > CombatScale.EarthRadius),
            "death creates physical pickups outside the planet surface");
        int count = battle.LootPickups.Count, receipts = game.PendingEnemyLoot.Count;
        battle.ApplyEnemyDamage(enemy, 1e9);
        check(battle.LootPickups.Count == count && game.PendingEnemyLoot.Count == receipts && game.Kills == 1,
            "repeated destruction cannot duplicate a pickup or a kill");
        check(battle.AdvanceLootFlights(100).Count == 0 && game.Minerals == openingMinerals,
            "world items never auto-collect through elapsed presentation time");
        check(!battle.BeginLootFlight(uid, new(-.1f, .5f)) && !battle.BeginLootFlight(uid, new(.5f, 1.1f))
            && !battle.BeginLootFlight(uid, new(float.NaN, .5f)) && !battle.BeginLootFlight(-1, new(.5f, .5f)),
            "invalid pointer coordinates and missing pickup IDs cannot reserve loot");
        check(battle.BeginLootFlight(uid, new(.6f, .4f), .9) && !battle.BeginLootFlight(uid, new(.6f, .4f)),
            "a click reserves once and duplicate clicks cannot start a second flight");
        check(game.Minerals == openingMinerals && mineral.S("phase") == "flying", "clicking still pays nothing");
        battle.Paused = true;
        check(battle.AdvanceLootFlights(.89).Count == 0 && game.Minerals == openingMinerals,
            "paused combat permits presentation flight without paying before its arrival boundary");
        var arrivals = battle.AdvanceLootFlights(.02);
        check(arrivals.Count == 1 && arrivals[0].B("ok") && arrivals[0].N("amount") == amount
            && game.Minerals == openingMinerals + amount && battle.FindLootPickup(uid) == null,
            "arrival credits exactly the carried quantity and consumes the object");
        check(battle.AdvanceLootFlights(10).Count == 0 && !battle.BeginLootFlight(uid, new(.5f, .5f))
            && game.Minerals == openingMinerals + amount && game.Energy == openingEnergy,
            "a consumed pickup cannot pay again and unclicked currencies stay in the world");
        check(Battlefield.ValidateLootConsistency(battle.SerializeCombatSnapshot(), game), "arrival leaves the world roster and escrow consistent");
    }

    private static void NearbyClusters(Action<bool, string> check)
    {
        var (game, battle) = Scene("clusters");
        for (int i = 0; i < 3; i++) Kill(battle, "nearby:" + i);
        var mineral = Pickup(battle, "minerals");
        check(battle.LootPickups.Count(row => row.S("currency") == "minerals") == 1 && mineral.List("receipts").Count == 3
            && mineral.N("amount") == 3 * DomainBalance.KillReward("scout").N("minerals"),
            "nearby same-currency deaths merge their quantities and retain three unique receipts");
        double flyingAmount = mineral.N("amount");
        battle.BeginLootFlight(mineral.L("uid"), new(.5f, .5f));
        Kill(battle, "nearby:after-click");
        check(mineral.N("amount") == flyingAmount && mineral.List("receipts").Count == 3
            && battle.LootPickups.Count(row => row.S("currency") == "minerals") == 2,
            "new drops cannot join an already-clicked flight or receive unrequested collection");
        for (int i = 4; i < 140; i++) Kill(battle, "nearby:" + i);
        check(battle.LootPickups.All(row => row.List("receipts").Count <= 128)
            && battle.LootPickups.Where(row => row.S("currency") == "minerals").Sum(row => row.List("receipts").Count) == 140,
            "dense battles split bounded clusters without losing any reward receipt");
        check(Battlefield.ValidateLootConsistency(battle.SerializeCombatSnapshot(), game), "dense world and flying clusters keep complete escrow coverage");
    }

    private static void PairedSaveAndValidation(Action<bool, string> check)
    {
        var (game, battle) = Scene("save");
        Kill(battle, "saved-aircraft");
        var mineral = Pickup(battle, "minerals");
        battle.BeginLootFlight(mineral.L("uid"), new(.72f, .31f), 1);
        battle.AdvanceLootFlights(.4);
        var gameSave = DataMap.Parse(game.Serialize().ToJson());
        var combatSave = DataMap.Parse(battle.SerializeCombatSnapshot().ToJson());
        check(combatSave.I("version") == 5 && Battlefield.ValidateCombatSnapshot(combatSave)
            && Battlefield.ValidateLootConsistency(combatSave, game), "world and in-flight items form a valid strict version-five checkpoint");
        var copyGame = new DefenseState();
        check(copyGame.Restore(gameSave), "companion run escrow restores from JSON");
        var copy = new Battlefield(copyGame);
        check(copy.RestoreCombatSnapshot(combatSave), "paired combat and run checkpoint restores atomically");
        var flight = Pickup(copy, "minerals", "flying");
        check(flight.N("flight_elapsed") == .4 && Math.Abs(flight.N("screen_x") - .72) < .000001
            && Math.Abs(flight.N("screen_y") - .31) < .000001 && copyGame.Minerals == game.Minerals,
            "reload preserves unpaid normalized screen origin and remaining flight time");
        copy.AdvanceLootFlights(.59);
        check(copyGame.Minerals == game.Minerals, "restored flight still waits for its original arrival");
        copy.AdvanceLootFlights(.02);
        check(copyGame.Minerals == game.Minerals + mineral.N("amount") && copy.LootPickups.Any(row => row.S("phase") == "world"),
            "restored arrival pays once while world items remain collectible");

        var original = new Battlefield(game);
        check(original.RestoreCombatSnapshot(combatSave), "validation fixture starts with the paired saved state");
        string before = original.SerializeCombatSnapshot().ToJson();
        DataMap Mutate(Action<DataMap> edit)
        {
            var changed = combatSave.DeepClone();
            CombatSnapshotCodec.TryDecode(changed.Value("payload"), out var decoded);
            edit((DataMap)decoded!); changed["payload"] = CombatSnapshotCodec.Encode(decoded); return changed;
        }
        var amountMismatch = Mutate(payload => payload.List("_loot").OfType<DataMap>().First()["amount"] = 99999d);
        check(!Battlefield.ValidateLootConsistency(amountMismatch, game) && !original.RestoreCombatSnapshot(amountMismatch)
            && original.SerializeCombatSnapshot().ToJson() == before, "tampered cluster amounts are rejected without changing the current battle");
        var missing = Mutate(payload => payload.List("_loot").RemoveAt(0));
        check(!original.RestoreCombatSnapshot(missing), "orphaned run receipts cannot disappear through a partial combat save");
        var duplicate = Mutate(payload => payload.List("_loot").Add(payload.List("_loot")[0]));
        check(!Battlefield.ValidateCombatSnapshot(duplicate), "duplicate physical pickup IDs and receipts are rejected");
        var invalidOrigin = Mutate(payload => payload.List("_loot").OfType<DataMap>().First()["screen_x"] = -1d);
        check(!Battlefield.ValidateCombatSnapshot(invalidOrigin), "out-of-bounds flight origins are rejected");
        var old = combatSave.DeepClone(); old["version"] = 4;
        check(!original.RestoreCombatSnapshot(old), "removed snapshot versions are rejected without migration");
        check(game.Reset(), "new-run fixture resets its domain state"); original.ResetBattle();
        check(game.PendingEnemyLoot.Count == 0 && original.LootPickups.Count == 0 && original.AdvanceLootFlights(10).Count == 0,
            "a new run clears old world objects, flights, and unpaid receipts together");
    }

    private static void PermanentReceiptReplay(Action<bool, string> check)
    {
        var (game, battle) = Scene("replay");
        Kill(battle, ChipEnemyId(game, "permanent"));
        var chip = Pickup(battle, "alien_chips");
        battle.BeginLootFlight(chip.L("uid"), new(.4f, .4f)); battle.AdvanceLootFlights(.3);
        var gameSave = game.Serialize(); var battleSave = battle.SerializeCombatSnapshot();
        battle.AdvanceLootFlights(1);
        check(game.FactoryPerks.AlienChips == 1, "the first completed chip flight pays one permanent chip");
        check(game.Restore(gameSave) && battle.RestoreCombatSnapshot(battleSave), "retry restores the pre-arrival run and physical flight together");
        var replay = battle.AdvanceLootFlights(1);
        check(game.FactoryPerks.AlienChips == 1 && replay.Count == 1 && replay[0].B("ok") && replay[0].N("amount") == 0
            && !battle.LootPickups.Any(row => row.S("currency") == "alien_chips"),
            "replaying a collected chip consumes the restored object without duplicating permanent currency");
    }

    private static void FailedProfileWrite(Action<bool, string> check)
    {
        var (game, battle) = Scene("profile-failure");
        string folder = Path.GetFullPath(Path.Combine(".runtime-tests", "loot-profile-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "chips.json");
        check(game.FactoryPerks.LoadProfile(path), "chip collection failure uses an isolated profile file");
        Kill(battle, ChipEnemyId(game, "failed-profile"));
        var chip = Pickup(battle, "alien_chips");
        string receipt = (string)chip.List("receipts")[0]!;
        Directory.CreateDirectory(path + ".tmp");
        battle.BeginLootFlight(chip.L("uid"), new(.5f, .5f));
        var failed = battle.AdvanceLootFlights(1);
        check(failed.Count == 1 && !failed[0].B("ok") && game.FactoryPerks.AlienChips == 0
            && game.HasPendingLoot(receipt) && chip.S("phase") == "world", "failed permanent write returns the unpaid chip to the world for retry");
        check(Battlefield.ValidateLootConsistency(battle.SerializeCombatSnapshot(), game), "failed arrival preserves a fully saveable receipt and physical item");
        Directory.Delete(path + ".tmp");
        check(battle.BeginLootFlight(chip.L("uid"), new(.5f, .5f)), "recovered profile allows another collection flight");
        var success = battle.AdvanceLootFlights(1);
        check(success.Count == 1 && success[0].B("ok") && game.FactoryPerks.AlienChips == 1 && !game.HasPendingLoot(receipt),
            "retry after storage recovery commits one chip and consumes its receipt");
    }
}
