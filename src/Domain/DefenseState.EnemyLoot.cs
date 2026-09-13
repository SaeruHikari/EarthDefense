using System;
using System.Collections.Generic;
using System.Linq;

namespace Earthward.Domain;

public sealed partial class DefenseState
{
    private readonly Dictionary<string, DataMap> _pendingEnemyLoot = new(StringComparer.Ordinal);
    private readonly HashSet<string> _settledEnemyLoot = new(StringComparer.Ordinal);
    private bool _settlingLoot;
    private static readonly string[] LootCurrencies = { "minerals", "energy", "science", "resource_cores", "alien_points", "alien_chips" };

    /// <summary>Detached descriptors; visual positions and flight progress belong to the battlefield.</summary>
    public IReadOnlyList<DataMap> PendingEnemyLoot => _pendingEnemyLoot.Values.Select(row => row.DeepClone()).ToList().AsReadOnly();
    public bool HasPendingLoot(string id) => _pendingEnemyLoot.ContainsKey(id);
    public bool IsLootSettled(string id) => _settledEnemyLoot.Contains(id);

    private static string LootIdentity(string run, string enemyId, string currency) =>
        "loot:" + FactoryPerks.Hash(run + "\u001fenemy-loot\u001f" + enemyId + "\u001f" + currency);

    /// <summary>
    /// Records a death once, immediately applying kill count and score while
    /// keeping every currency in escrow until its pickup reaches the HUD.
    /// </summary>
    public IReadOnlyList<DataMap> RegisterEnemyLoot(DataMap enemy)
    {
        double hp = enemy.N("hp", double.NaN);
        if (!double.IsFinite(hp) || hp > 0) return Array.Empty<DataMap>();
        string enemyId = EnemyRewardIdentity(enemy, Wave);
        string kind = enemy.S("kind", "scout");
        var reward = DomainBalance.KillReward(kind);
        if (reward.Count == 0 || !ValidEnemyLootToken(enemyId) || !_rewardedEnemies.Add(enemyId))
            return Array.Empty<DataMap>();

        var amounts = new DataMap
        {
            ["minerals"] = reward.N("minerals") * KillRewardMultiplier() * (1 + TechEffects().N("mineral_kill_bonus")),
            ["energy"] = reward.N("energy") * KillRewardMultiplier(),
            ["science"] = reward.N("science") * KillRewardMultiplier(),
            ["resource_cores"] = EnemyDropsRareLoot(enemy, "resource_cores", DomainBalance.Value("resource_core_drop_chance")) ? 1L : 0L,
            ["alien_points"] = EnemyDropsRareLoot(enemy, "alien_points", DomainBalance.Value("alien_point_drop_chance")) ? 1L : 0L,
            ["alien_chips"] = EnemyDropsAlienChip(enemy) ? 1L : 0L
        };
        var created = new List<DataMap>();
        foreach (string currency in LootCurrencies)
        {
            double amount = Math.Min(currency is "resource_cores" or "alien_points" or "alien_chips" ? MaxExactInteger : ResourceLimit, amounts.N(currency));
            if (!double.IsFinite(amount) || amount <= 0) continue;
            string id = LootIdentity(RunId, enemyId, currency);
            var descriptor = new DataMap { ["id"] = id, ["enemy_id"] = enemyId, ["currency"] = currency, ["amount"] = amount };
            _pendingEnemyLoot.Add(id, descriptor);
            created.Add(descriptor.DeepClone());
        }
        Kills = Math.Min(MaxExactInteger, Kills + 1);
        Score = Math.Min(MaxExactInteger, Score + reward.L("score"));
        Changed?.Invoke();
        return created.AsReadOnly();
    }

    /// <summary>
    /// Called only at pickup arrival. A permanent-chip write must succeed before
    /// its pending entry is consumed; failed writes remain safe to retry.
    /// </summary>
    public bool CollectLoot(string id) => CollectLootBatch(new[] { id });

    /// <summary>One arriving cluster settles atomically, including a single permanent profile write for all its chips.</summary>
    public bool CollectLootBatch(IReadOnlyList<string> ids)
    {
        if (ids == null || _settlingLoot) return false;
        var pending = new List<DataMap>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!unique.Add(id) || _settledEnemyLoot.Contains(id)) continue;
            if (!_pendingEnemyLoot.TryGetValue(id, out var row)) return false;
            pending.Add(row);
        }
        if (pending.Count == 0) return true;
        DataMap? chipReceipt = null;
        string[] chips = pending.Where(row => row.S("currency") == "alien_chips").Select(row => row.S("id")).ToArray();
        _settlingLoot = true;
        try
        {
            if (chips.Length > 0)
            {
                chipReceipt = FactoryPerks.ClaimAlienChips(RunId, chips);
                if (!chipReceipt.B("ok")) return false;
            }

            foreach (var loot in pending)
            {
                string id = loot.S("id");
                double amount = loot.N("amount");
                switch (loot.S("currency"))
                {
                    case "minerals": Minerals = Math.Min(ResourceLimit, Minerals + amount); break;
                    case "energy": Energy = Math.Min(ResourceLimit, Energy + amount); break;
                    case "science": Science = Math.Min(ResourceLimit, Science + amount); break;
                    case "resource_cores": ResourceCores = Math.Min(MaxExactInteger, ResourceCores + (long)amount); break;
                    case "alien_points": AlienPoints = Math.Min(MaxExactInteger, AlienPoints + (long)amount); break;
                }
                _pendingEnemyLoot.Remove(id);
                _settledEnemyLoot.Add(id);
            }
        }
        finally { _settlingLoot = false; }
        if (chipReceipt != null && chipReceipt.L("alien_chips") > 0) AlienChipDropped?.Invoke(chipReceipt);
        Changed?.Invoke();
        return true;
    }

    private DataMap EnemyLootSave() => new()
    {
        ["version"] = 1L,
        ["pending"] = _pendingEnemyLoot.Values.Select(row => (object?)row.DeepClone()).ToList(),
        ["settled"] = _settledEnemyLoot.Order(StringComparer.Ordinal).Cast<object?>().ToList()
    };

    private static bool ValidEnemyLootToken(string value) => value.Length > 0 && value.Length <= 512 && value.All(c => c >= 32);
    private static bool ValidLootIdentity(string value) => value.Length == 69 && value.StartsWith("loot:", StringComparison.Ordinal)
        && value.Skip(5).All(c => "0123456789abcdef".Contains(c));

    private static DataMap? ValidateEnemyLoot(object? value, string run, DataMap runtime)
    {
        if (value is not DataMap data || data.Count != 3 || !DataMap.ValidNumber(data.Value("version"), 1, 1, true)
            || data.Value("pending") is not List<object?> pending || pending.Count > 1200000
            || data.Value("settled") is not List<object?> settled || settled.Count > 1200000)
            return null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (object? item in settled)
            if (item is not string id || !ValidLootIdentity(id) || !seen.Add(id)) return null;
        var rewarded = runtime.List("rewarded_enemies").OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (object? item in pending)
        {
            if (item is not DataMap row || row.Count != 4) return null;
            string id = row.S("id"), enemyId = row.S("enemy_id"), currency = row.S("currency");
            bool whole = currency is "resource_cores" or "alien_points" or "alien_chips";
            if (!ValidEnemyLootToken(enemyId) || !rewarded.Contains(enemyId) || !LootCurrencies.Contains(currency)
                || id != LootIdentity(run, enemyId, currency) || !seen.Add(id)
                || !DataMap.ValidNumber(row.Value("amount"), double.Epsilon, whole ? MaxExactInteger : ResourceLimit, whole)
                || currency == "alien_chips" && row.N("amount") != 1)
                return null;
        }
        return data.DeepClone();
    }

    private void RestoreEnemyLoot(DataMap data)
    {
        ClearEnemyLoot();
        foreach (var row in data.List("pending").OfType<DataMap>()) _pendingEnemyLoot.Add(row.S("id"), row.DeepClone());
        foreach (string id in data.List("settled").Cast<string>()) _settledEnemyLoot.Add(id);
    }

    private void ClearEnemyLoot()
    {
        _pendingEnemyLoot.Clear();
        _settledEnemyLoot.Clear();
    }
}
