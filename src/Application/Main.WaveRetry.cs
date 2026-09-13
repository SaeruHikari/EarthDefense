using Godot;
using Earthward.Domain;
using Earthward.Combat;

namespace Earthward.Application;

public partial class Main
{
    public const string WaveStartPath = "user://earthward_wave_start.json";
    private DataMap? _waveStartRecord;
    public bool HasWaveStartRetry => _waveStartRecord != null && MatchesCurrentWave(_waveStartRecord);
    public bool CanRetryWave => HasWaveStartRetry;
    public long RetryWaveNumber => HasWaveStartRetry ? _waveStartRecord!.L("wave") : 0;
    public DataMap? CurrentWaveStartCheckpoint => HasWaveStartRetry ? _waveStartRecord!.Map("checkpoint").DeepClone() : null;

    private bool MatchesCurrentWave(DataMap record) => record.S("run_id") == Game.RunId && record.L("wave") == Game.Wave;

    public bool ValidateWaveStartRecord(DataMap record)
    {
        if (record.Count != 5 || !DataMap.ValidNumber(record.Value("version"), 1, 1, true)
            || !DataMap.ValidNumber(record.Value("wave"), 1, DefenseState.MaxExactInteger, true)
            || record.S("origin") != "wave_start"
            || record.Value("checkpoint") is not DataMap data || !ValidateCheckpoint(data)) return false;
        var game = data.Map("game");
        if (record.S("run_id") == "" || record.S("run_id") != game.S("run_id") || record.L("wave") != game.L("wave")
            || !data.B("started") || game.N("earth_hp") <= 0) return false;
        var battle = data.Map("expedition_battle").Map("earth");
        // ValidateCheckpoint has already fully decoded/validated this battle. Read only these scalar guards.
        object? Field(object? encoded, string key)
        {
            if(encoded is not DataMap map||map.S("@")!="map")return null;
            foreach(var pair in map.List("v"))if(pair is List<object?> values&&values.Count==2&&values[0] as string==key)return values[1];
            return null;
        }
        object? payload=battle.Value("payload");
        object? Scalar(string key){var value=Field(payload,key);return CombatSnapshotCodec.TryDecode(value,out var decoded)?decoded:null;}
        var waveValue=Field(Field(payload,"_wave_spawn_snapshot"),"wave");
        return Scalar("wave_running") is true && Scalar("_fixed_cycle_running") is true && Scalar("_dead") is false
            && DataMap.Number(Scalar("_fixed_cycle_elapsed"),double.NaN)==0 && DataMap.Number(Scalar("_wave_spawn_elapsed"),double.NaN)==0 && DataMap.Integer(Scalar("_wave_spawned"),-1)==0
            && CombatSnapshotCodec.TryDecode(waveValue,out var spawnWave) && DataMap.Integer(spawnWave,-1)==record.L("wave");
    }

    private DataMap? ReadWaveStartRecord(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            if(new System.IO.FileInfo(path).Length>CheckpointFile.MaximumBytes)return null;
            var record = DataMap.Parse(System.IO.File.ReadAllText(path));
            return ValidateWaveStartRecord(record) ? record : null;
        }
        catch (Exception) { return null; }
    }

    private void InitializeWaveRetry()
    {
        _waveStartRecord = ReadWaveStartRecord(ProjectSettings.GlobalizePath(WaveStartPath));
    }

    private void RefreshWaveRetryForCurrentRun()
    {
        if (HasWaveStartRetry) return;
        string path = ProjectSettings.GlobalizePath(WaveStartPath);
        foreach (string candidate in new[] { path, path + ".previous" })
        {
            var record = ReadWaveStartRecord(candidate);
            if (record != null && MatchesCurrentWave(record)) { _waveStartRecord = record; return; }
        }
    }

    private void CaptureWaveStartCheckpoint(long wave)
    {
        try
        {
            var data = CaptureCheckpointData(Campaign.SerializeWaveStart());
            data["started"] = true;
            var record = new DataMap
            {
                ["version"] = 1L, ["run_id"] = Game.RunId, ["wave"] = wave,
                ["origin"] = "wave_start", ["checkpoint"] = data
            };
            if (!ValidateWaveStartRecord(record))
            {
                ShowNotice("\u672c\u6ce2\u5f00\u5934\u8bb0\u5f55\u672a\u901a\u8fc7\u6821\u9a8c\uff0c\u4e0a\u4e00\u4efd\u6709\u6548\u8bb0\u5f55\u4ecd\u4fdd\u7559");
                return;
            }
            // Keep one immutable captured state. Both files embed the same serialized checkpoint bytes.
            _waveStartRecord = record;
            _checkpointWriter.EnqueueOwned(Game.RunId,wave,ProjectSettings.GlobalizePath(SavePath),data,ProjectSettings.GlobalizePath(WaveStartPath),record);
            _checkpointPending = false;
            _campaignSaveElapsed = 0;
        }
        catch (Exception error)
        {
            ShowNotice("\u6ce2\u9996\u4fdd\u5b58\u672a\u5b8c\u6210\uff0c\u5df2\u6709\u6709\u6548\u8bb0\u5f55\u4ecd\u4fdd\u7559\uff1a" + error.Message);
        }
    }

    public bool RetryWaveStart()
    {
        FlushCheckpointWrites();
        RefreshWaveRetryForCurrentRun();
        if (!HasWaveStartRetry)
        {
            ShowNotice("还没有可以重试的波次记录");
            return false;
        }
        var data = _waveStartRecord!.Map("checkpoint").DeepClone();
        if (!ValidateCheckpoint(data) || !RestoreCheckpointData(data, true)) return false;
        if (!SaveCheckpoint()) ShowNotice($"已回到第 {Game.Wave:00} 波开头并暂停，磁盘记录暂未更新");
        return true;
    }
}
