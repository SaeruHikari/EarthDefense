using Earthward.Domain;

namespace Earthward.Combat;

public sealed partial class Battlefield
{
    /// <summary>One-time recovery for old checkpoints that never recorded a wave
    /// opening. New saves restore the exact opening snapshot instead. Ownership,
    /// resources, permanent reward claims, clock and UID sequence remain intact.</summary>
    public bool RestartCurrentWaveForLegacyRetry()
    {
        if (Game == null || !_postDefense && _invasion.FrontsForWave(Math.Max(1, Game.Wave)).Count == 0)
            return false;
        long wave = Math.Max(1, Game.Wave);
        _epoch++;
        Dead = false;
        InvasionWon = false;
        Game.EarthHp = 100;
        Enemies.Clear();
        Shots.Clear();
        HostileShots.Clear();
        Beams.Clear();
        Bursts.Clear();
        DamageNumbers.Clear();
        _projectileSectors.Clear();
        _enemySectors.Clear();
        _sectorKeys.Clear();
        _defendersValid = false;
        _assignmentClock = 0;
        _cohortComplete = false;
        _postSpawned = 0;
        if (_postDefense)
        {
            _postPlan["completion_handled"] = false;
            _postPlan["sortie_elapsed"] = 0d;
            _postPlan["sortie_started"] = true;
        }
        foreach (var drone in Drones)
        {
            drone["target_uid"] = -1L;
            drone["aim_target_uid"] = -1L;
            if (C.S(drone, "state") is "engaging" or "suiciding") drone["state"] = "patrol";
        }
        RefreshConfiguration();
        RebuildTargets();
        // OpenWave emits WaveStarted synchronously; the application writes its new
        // baseline after these resets, before a single hostile unit is deployed.
        if (_postDefense && C.S(_postPlan, "id").Length == 0) return false;
        OpenWave(wave);
        return Game.Wave == wave && WaveRunning && _spawnElapsed == 0 && _spawned == 0;
    }
}
