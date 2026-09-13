using System;
using System.Buffers.Binary;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public event Action<DataMap>? AlienChipDropped;
    public int PendingAlienChipCount => _pendingEnemyLoot.Values.Count(row => row.S("currency") == "alien_chips");

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
    public bool EnemyDropsAlienChip(DataMap enemy) => EnemyDropsRareLoot(enemy, "alien_chips", PerkCatalog.AlienChipDropChance);

    private bool EnemyDropsRareLoot(DataMap enemy, string currency, double chance)
    {
        double hp = enemy.N("hp", double.NaN);
        if (enemy.S("kind", "scout") != "scout" || enemy.B("post_carrier") || !double.IsFinite(hp) || hp > 0)
            return false;
        string id = EnemyRewardIdentity(enemy, Wave);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(RunId + "\u001fdrop:" + currency + "\u001f" + id));
        double roll = (BinaryPrimitives.ReadUInt64LittleEndian(digest) >> 11) * (1d / 9007199254740992d);
        return roll < chance;
    }

}
