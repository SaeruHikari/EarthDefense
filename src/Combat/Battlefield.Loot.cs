using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

public sealed partial class Battlefield
{
    private const float LootMergeDistance = .9f;
    private readonly List<DataMap> _lootPickups = new();
    private readonly Dictionary<long, DataMap> _lootByUid = new();
    private readonly Dictionary<(string Currency, Vector3I Cell), List<DataMap>> _lootCells = new();
    private static readonly HashSet<string> PickupCurrencies = new(StringComparer.Ordinal)
        { "minerals", "energy", "science", "resource_cores", "alien_points", "alien_chips" };
    public IReadOnlyList<DataMap> LootPickups => _lootPickups;
    public DataMap? FindLootPickup(long uid) => _lootByUid.GetValueOrDefault(uid);
    public static bool RareLoot(string currency) => currency is "resource_cores" or "alien_points" or "alien_chips";

    private static Vector3I LootCell(Vector3 at) => new((int)Math.Floor(at.X / LootMergeDistance),
        (int)Math.Floor(at.Y / LootMergeDistance), (int)Math.Floor(at.Z / LootMergeDistance));

    /// <summary>A death creates escrow-backed world items; no resource is paid here.</summary>
    private void SpawnEnemyLoot(DataMap enemy)
    {
        var drops = Game.RegisterEnemyLoot(enemy);
        Vector3 at = C.V(enemy, "space_position");
        if (!at.IsFinite() || at.LengthSquared() < .01f) at = GetInvasionAnchor() * (float)(CombatScale.EarthRadius + 2);
        Vector3 normal = at.Normalized();
        at = normal * Math.Max(at.Length(), (float)CombatScale.EarthRadius + 1.8f);
        Vector3 tangent = normal.Cross(Math.Abs(normal.Y) > .9 ? Vector3.Right : Vector3.Up).Normalized();
        Vector3 across = normal.Cross(tangent).Normalized();
        for (int i = 0; i < drops.Count; i++)
        {
            var receipt = drops[i];
            string currency = receipt.S("currency");
            // Fixed currency sectors keep both click targets and nearby-cluster
            // positions stable when a rare item joins the normal two drops.
            int sector = currency switch { "minerals" => 0, "energy" => 1, "science" => 2,
                "resource_cores" => 3, "alien_points" => 4, "alien_chips" => 5, _ => 0 };
            float angle = Mathf.Tau * sector / 6;
            Vector3 position = at + (tangent * Mathf.Cos(angle) + across * Mathf.Sin(angle));
            var cell = LootCell(position);
            DataMap? cluster = null;
            float nearest = LootMergeDistance * LootMergeDistance;
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
            {
                if (!_lootCells.TryGetValue((currency, cell + new Vector3I(x, y, z)), out var candidates)) continue;
                foreach (var candidate in candidates)
                {
                    if (candidate.List("receipts").Count >= 128) continue;
                    float distance = candidate.Vector3("space_position").DistanceSquaredTo(position);
                    if (distance < nearest) { nearest = distance; cluster = candidate; }
                }
            }
            if (cluster != null)
            {
                cluster.List("receipts").Add(receipt.S("id"));
                cluster["amount"] = Math.Min(LootAmountLimit(currency), cluster.N("amount") + receipt.N("amount"));
                continue;
            }
            cluster = new DataMap {
                ["uid"] = NewUid(), ["currency"] = currency, ["amount"] = receipt.N("amount"),
                ["space_position"] = position, ["receipts"] = new List<object?> { receipt.S("id") },
                ["phase"] = "world", ["flight_elapsed"] = 0d, ["flight_duration"] = .9d,
                ["screen_x"] = 0d, ["screen_y"] = 0d, ["spawn_clock"] = Clock
            };
            _lootPickups.Add(cluster); _lootByUid.Add(cluster.L("uid"), cluster); IndexLoot(cluster);
        }
    }

    private static double LootAmountLimit(string currency) => RareLoot(currency) ? DefenseState.MaxExactInteger : DefenseState.ResourceLimit;
    private void IndexLoot(DataMap pickup)
    {
        if (pickup.S("phase") != "world") return;
        var key = (pickup.S("currency"), LootCell(pickup.Vector3("space_position")));
        if (!_lootCells.TryGetValue(key, out var list)) _lootCells[key] = list = new();
        list.Add(pickup);
    }
    private void UnindexLoot(DataMap pickup)
    {
        var key = (pickup.S("currency"), LootCell(pickup.Vector3("space_position")));
        if (!_lootCells.TryGetValue(key, out var list)) return;
        list.Remove(pickup);
        if (list.Count == 0) _lootCells.Remove(key);
    }
    private void ClearLoot()
    {
        _lootPickups.Clear(); _lootByUid.Clear(); _lootCells.Clear();
    }
    private void RestoreLoot(IEnumerable<DataMap> pickups)
    {
        ClearLoot();
        foreach (var pickup in pickups)
        {
            _lootPickups.Add(pickup); _lootByUid.Add(pickup.L("uid"), pickup); IndexLoot(pickup);
        }
    }

    /// <summary>Screen origin is normalized for window resizing and save/restore. The click only reserves an item.</summary>
    public bool BeginLootFlight(long uid, Vector2 normalizedOrigin, double duration = .9)
    {
        if (!_lootByUid.TryGetValue(uid, out var pickup) || pickup.S("phase") != "world"
            || !normalizedOrigin.IsFinite() || normalizedOrigin.X < 0 || normalizedOrigin.X > 1
            || normalizedOrigin.Y < 0 || normalizedOrigin.Y > 1 || !double.IsFinite(duration)) return false;
        if (pickup.List("receipts").Cast<string>().Any(id => !Game.HasPendingLoot(id))) return false;
        UnindexLoot(pickup);
        pickup["phase"] = "flying"; pickup["flight_elapsed"] = 0d;
        pickup["flight_duration"] = Math.Clamp(duration, .55, 1.2);
        pickup["screen_x"] = (double)normalizedOrigin.X; pickup["screen_y"] = (double)normalizedOrigin.Y;
        return true;
    }

    /// <summary>Advance presentation time even while combat is paused. Credit occurs only at the arrival boundary.</summary>
    public IReadOnlyList<DataMap> AdvanceLootFlights(double delta)
    {
        if (!double.IsFinite(delta) || delta <= 0) return Array.Empty<DataMap>();
        List<DataMap>? arrivals = null;
        for (int i = _lootPickups.Count - 1; i >= 0; i--)
        {
            var pickup = _lootPickups[i];
            if (pickup.S("phase") != "flying") continue;
            double duration = pickup.N("flight_duration");
            pickup["flight_elapsed"] = Math.Min(duration, pickup.N("flight_elapsed") + delta);
            if (pickup.N("flight_elapsed") < duration) continue;
            string currency = pickup.S("currency");
            double before = LootBalance(currency);
            bool collected = Game.CollectLootBatch(pickup.List("receipts").Cast<string>().ToArray());
            arrivals ??= new();
            arrivals.Add(new DataMap { ["uid"] = pickup.L("uid"), ["currency"] = currency,
                ["amount"] = collected ? Math.Max(0, LootBalance(currency) - before) : 0d, ["ok"] = collected });
            if (!collected)
            {
                pickup["phase"] = "world"; pickup["flight_elapsed"] = 0d; IndexLoot(pickup);
                continue;
            }
            _lootByUid.Remove(pickup.L("uid")); _lootPickups.RemoveAt(i);
        }
        return arrivals is null ? Array.Empty<DataMap>() : arrivals;
    }
    private double LootBalance(string currency) => currency switch {
        "minerals" => Game.Minerals, "energy" => Game.Energy, "science" => Game.Science,
        "resource_cores" => Game.ResourceCores, "alien_points" => Game.AlienPoints,
        "alien_chips" => Game.FactoryPerks.AlienChips, _ => 0
    };

    private static bool ValidateLootRecords(object? value)
    {
        if (value is not List<object?> list || list.Count > 200000) return false;
        var receipts = new HashSet<string>(StringComparer.Ordinal);
        var uids = new HashSet<long>();
        foreach (var entry in list)
        {
            if (entry is not DataMap row || row.Count != 11 || !CombatSnapshotCodec.HasFields(row,
                "uid:int currency:text amount:number space_position:v3 receipts:array phase:text flight_elapsed:number flight_duration:number screen_x:number screen_y:number spawn_clock:number")) return false;
            if (row.L("uid") < 1 || !uids.Add(row.L("uid")) || !PickupCurrencies.Contains(row.S("currency"))
                || !DataMap.ValidNumber(row.Value("amount"), double.Epsilon, LootAmountLimit(row.S("currency")))
                || !row.Vector3("space_position").IsFinite() || row.Vector3("space_position").Length() < CombatScale.EarthRadius
                || row.S("phase") is not ("world" or "flying") || row.N("flight_duration") < .55 || row.N("flight_duration") > 1.2
                || !DataMap.ValidNumber(row.Value("flight_elapsed"), 0, row.N("flight_duration"))
                || !DataMap.ValidNumber(row.Value("screen_x"), 0, 1) || !DataMap.ValidNumber(row.Value("screen_y"), 0, 1)
                || !DataMap.ValidNumber(row.Value("spawn_clock"), 0, double.MaxValue)) return false;
            if (row.List("receipts").Count is < 1 or > 128) return false;
            foreach (var receipt in row.List("receipts"))
                if (receipt is not string id || !id.StartsWith("loot:", StringComparison.Ordinal) || id.Length != 69 || !receipts.Add(id)) return false;
        }
        return true;
    }

    /// <summary>Reject split snapshots that would lose a reward, duplicate it, or change its currency/amount.</summary>
    public static bool ValidateLootConsistency(DataMap combatSnapshot, DefenseState game)
    {
        if (!CombatSnapshotCodec.TryDecode(combatSnapshot.Value("payload"), out var decoded) || decoded is not DataMap payload
            || !ValidateLootRecords(payload.Value("_loot"))) return false;
        var pending = game.PendingEnemyLoot.ToDictionary(r => r.S("id"), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pickup in payload.List("_loot").Cast<DataMap>())
        {
            double sum = 0;
            foreach (string id in pickup.List("receipts").Cast<string>())
            {
                if (!pending.TryGetValue(id, out var receipt) || !seen.Add(id) || receipt.S("currency") != pickup.S("currency")) return false;
                sum = Math.Min(LootAmountLimit(pickup.S("currency")), sum + receipt.N("amount"));
            }
            if (Math.Abs(sum - pickup.N("amount")) > Math.Max(1e-8, sum * 1e-10)) return false;
        }
        return seen.Count == pending.Count;
    }
}
