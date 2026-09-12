using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public event Action<DataMap>? AlienChipDropped;
    private readonly Dictionary<string, HashSet<string>> _pendingAlienChipEvents = new(StringComparer.Ordinal);
    public int PendingAlienChipCount => _pendingAlienChipEvents.Values.Sum(events => events.Count);

    private static string EnemyRewardIdentity(DataMap enemy, long currentWave)
    {
        string explicitId = enemy.S("reward_event_id");
        if (explicitId.Length > 0) return explicitId;
        string plannedId = enemy.S("planned_uid");
        if (plannedId.Length > 0) return plannedId;
        return $"{enemy.L("spawn_wave", enemy.L("wave", currentWave))}:{enemy.L("uid", -1)}:{enemy.S("kind", "scout")}";
    }

    /// <summary>
    /// A wave-plan identity has a stable roll within the run. Changing combat
    /// RNG consumption or replaying a wave cannot reroll a defeated aircraft.
    /// </summary>
    public bool EnemyDropsAlienChip(DataMap enemy)
    {
        double hp = enemy.N("hp", double.NaN);
        if (enemy.S("kind") is not ("scout" or "cruiser") || enemy.B("resource_core_carrier")
            || enemy.B("post_carrier") || !double.IsFinite(hp) || hp > 0)
            return false;
        string id = EnemyRewardIdentity(enemy, Wave);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(RunId + "\u001fchip:\u001f" + id));
        double roll = (BinaryPrimitives.ReadUInt64LittleEndian(digest) >> 11) * (1d / 9007199254740992d);
        return roll < PerkCatalog.AlienChipDropChance;
    }

    private void QueueAlienChipDrop(DataMap enemy, string rewardId)
    {
        if (!EnemyDropsAlienChip(enemy)) return;
        if (!_pendingAlienChipEvents.TryGetValue(RunId, out var events))
            _pendingAlienChipEvents[RunId] = events = new(StringComparer.Ordinal);
        events.Add("chip:" + rewardId);
    }

    /// <summary>
    /// Settle one burst of kills with one permanent-profile transaction, rather
    /// than synchronously rewriting the profile inside every projectile hit.
    /// Failed transactions remain pending for a later retry.
    /// </summary>
    public bool FlushAlienChipDrops()
    {
        if (_pendingAlienChipEvents.Count == 0) return true;
        foreach (string run in _pendingAlienChipEvents.Keys.ToArray())
        {
            var result = FactoryPerks.ClaimAlienChips(run, _pendingAlienChipEvents[run].ToArray());
            if (!result.B("ok")) return false;
            _pendingAlienChipEvents.Remove(run);
            if (result.L("alien_chips") > 0) AlienChipDropped?.Invoke(result);
        }
        return true;
    }
}
