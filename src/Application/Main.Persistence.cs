using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Earthward.Domain;
using Earthward.Combat;
using Earthward.Rendering;
namespace Earthward.Application;

public partial class Main
{
    private readonly CheckpointWriteQueue _checkpointWriter = new();
    public bool QueueCheckpointSave()
    {
        _checkpointPending = false;
        try
        {
            var snapshot=CaptureCheckpointData();
            if(!ValidateCheckpoint(snapshot)){ShowNotice("\u81ea\u52a8\u5b58\u6863\u6821\u9a8c\u5931\u8d25\uff0c\u4e0a\u4e00\u4efd\u6709\u6548\u8bb0\u5f55\u4ecd\u4fdd\u7559");return false;}
            _checkpointWriter.EnqueueOwned(Game.RunId,Game.Wave,ProjectSettings.GlobalizePath(SavePath),snapshot);
            _campaignSaveElapsed=0;
            return true;
        }
        catch(Exception error){ShowNotice("\u81ea\u52a8\u5b58\u6863\u672a\u5b8c\u6210\uff1a"+error.Message);return false;}
    }
    public void PumpCheckpointWrites(bool showNotices=true)
    {
        foreach(var result in _checkpointWriter.TakeResults())
        {
            if(Game==null||result.RunId!=Game.RunId)continue;
            if(result.MainSaved)SaveExists=true;
            if(showNotices&&(!result.MainSaved||result.IsWaveStart&&!result.WaveSaved))
                ShowNotice("\u540e\u53f0\u5b58\u6863\u672a\u5b8c\u6210\uff0c\u5df2\u6709\u6709\u6548\u8bb0\u5f55\u4ecd\u4fdd\u7559\uff1a"+result.Error);
        }
    }
    public void FlushCheckpointWrites(bool showNotices=true)
    {
        _checkpointWriter.Flush();
        PumpCheckpointWrites(showNotices);
    }


    private void SaveIfSafe()
    {
        if (!Defeated && !PreserveCheckpoint)
        {
            _checkpointPending = true;
            _checkpointDebounce = .6;
        }
    }

    public bool SaveCheckpoint()
    {
        FlushCheckpointWrites();
        _checkpointPending = false;
        try
        {
            var payload = CaptureCheckpointData();
            if (!ValidateCheckpoint(payload) || !AtomicWrite(ProjectSettings.GlobalizePath(SavePath), CheckpointFile.Serialize(payload)))
            {
                ShowNotice("保存未完成，上一份有效记录仍保留");
                return false;
            }
            SaveExists = true;
            _campaignSaveElapsed = 0;
            return true;
        }
        catch (Exception e) { GD.PushWarning("Checkpoint write: " + e.Message); ShowNotice("保存未完成，上一份有效记录仍保留"); return false; }
    }

    private static bool AtomicWrite(string path, byte[] validatedContents) => CheckpointFile.WriteVerified(path, validatedContents);
    private static bool AtomicWrite(string path, string contents, Func<string,bool> validate) => validate(contents) && CheckpointFile.WriteVerified(path, System.Text.Encoding.UTF8.GetBytes(contents));

    public bool ValidateCheckpoint(DataMap data)
    {
        if (data.ContainsKey("play_time_seconds") && !DataMap.ValidNumber(data.Value("play_time_seconds"), 0, DefenseState.MaxExactInteger) || data.ContainsKey("play_time_estimated") && data.Value("play_time_estimated") is not bool) return false;
        if (!DataMap.ValidNumber(data.Value("version"), 1, 3) || data.N("version") != data.I("version") || data.Value("started") is not bool || data.Value("game") is not DataMap state || data.Value("slots") is not List<object?> slots)
            return false;
        if (data.ContainsKey("celestial") && data.Value("celestial") is not DataMap)
            return false;
        var celestial = data.Map("celestial");
        if (!DataMap.ValidNumber(celestial.Value("earth_rotation_y", Mathf.DegToRad(12)), -double.MaxValue, double.MaxValue) || !DataMap.ValidNumber(celestial.Value("elapsed", 0), 0, double.MaxValue))
            return false;
        if (data.I("version") == 3 && (!data.ContainsKey("destroyed_fronts") || !state.ContainsKey("expedition") || data.Value("expedition_battle") is not DataMap))
            return false;
        if (data.ContainsKey("destroyed_fronts") && data.Value("destroyed_fronts") is not List<object?>)
            return false;
        if (!InvasionDirector.ValidateDestroyedFronts(data.List("destroyed_fronts")))
            return false;
        if (data.ContainsKey("site_directions") && data.Value("site_directions") is not List<object?>)
            return false;
        if (data.ContainsKey("expedition_battle") && (data.Value("expedition_battle") is not DataMap campaignSnapshot || !DefenseCampaignDirector.ValidateSnapshot(campaignSnapshot)))
            return false;
        if (data.ContainsKey("combat_snapshot"))
        {
            if (data.Value("combat_snapshot") is not DataMap combatSnapshot) return false;
            if (data.ContainsKey("expedition_battle"))
            { if (!DataMap.Equivalent(combatSnapshot, data.Map("expedition_battle").Map("earth"))) return false; }
            else if (!Battlefield.ValidateCombatSnapshot(combatSnapshot)) return false;
        }
        if (data.ContainsKey("invasion_anchor"))
        {
            var anchor = data.List("invasion_anchor");
            if (anchor.Count != 3 || anchor.Any(v => !DataMap.ValidNumber(v, -1.01, 1.01)) || Math.Abs(Math.Sqrt(anchor.Sum(v => Math.Pow(DataMap.Number(v), 2))) - 1) > .01)
                return false;
        }
        if (state.Value("buildings") is not DataMap buildings)
            return false;
        if (data.I("version") == 1)
        {
            if (slots.Count != 16)
                return false;
        }
        else if (data.Value("site_directions") is not List<object?> || !PlanetView.ValidateStructureLayout(slots, data.List("site_directions")))
            return false;
        var counts = new Dictionary<string, long> { { "mine", 0 }, { "solar", 0 }, { "lab", 0 }, { "interceptor", 0 }, { "laser", 0 }, { "missile", 0 }, { "shield", 0 }, { "starship_silo", 0 } };
        foreach (var value in slots)
        {
            if (value is not string kind || kind != "" && !counts.ContainsKey(kind))
                return false;
            if (kind != "")
                counts[kind]++;
        }
        if (counts.Any(pair => buildings.L(pair.Key) != pair.Value))
            return false;
        var verifier = new DefenseState();
        if (!verifier.Restore(state))
            return false;
        var fleet = verifier.Expedition.Serialize().Map("fleet");
        foreach (var entry in fleet.List("orders").Concat(fleet.List("ships")))
        {
            if (entry is not DataMap ship)
                return false;
            int site = ship.I("silo_id", -1);
            if (site < 0 || site >= slots.Count || (string?)slots[site] != "starship_silo")
                return false;
        }
        foreach (var pair in state.Map("resource_core_upgrades"))
        {
            if (!int.TryParse(pair.Key, out int site) || site < 0 || site >= slots.Count || pair.Value is not DataMap record || (string?)slots[site] != record.S("kind"))
                return false;
        }
        return true;
    }

    public bool LoadCheckpoint()
    {
        FlushCheckpointWrites();
        string path = ProjectSettings.GlobalizePath(SavePath);
        DataMap data;
        try
        {
            if (!File.Exists(path))
            {
                ShowNotice("还没有保存的防御记录");
                return false;
            }
            if(new FileInfo(path).Length>CheckpointFile.MaximumBytes)throw new InvalidDataException("Checkpoint exceeds the safe size limit.");
            data = DataMap.Parse(File.ReadAllText(path));
            if (!ValidateCheckpoint(data))
            {
                ShowNotice("防御记录无法读取，当前进度未改变");
                return false;
            }
        }
        catch (Exception e) { GD.PushWarning("Checkpoint read: " + e.Message); ShowNotice("防御记录无法读取"); return false; }
        return RestoreCheckpointData(data, false);
    }

    private DataMap CaptureCheckpointData(DataMap? campaign = null)
    {
        var anchor = Battle.GetInvasionAnchor();
        return new DataMap
        {
            ["version"] = 3, ["game"] = Game.Serialize(), ["slots"] = Planet.GetSlots(),
            ["site_directions"] = Planet.GetSiteDirections(), ["started"] = Started,
            ["play_time_seconds"] = PlayTimeSeconds, ["play_time_estimated"] = PlayTimeEstimated,
            ["invasion_anchor"] = new List<object?> { anchor.X, anchor.Y, anchor.Z },
            ["destroyed_fronts"] = Battle.GetDestroyedFronts().Cast<object?>().ToList(),
            ["expedition_battle"] = campaign ?? Campaign.Serialize(), ["celestial"] = Planet.CaptureCelestialState()
        };
    }

    private bool RestoreCheckpointData(DataMap data, bool retry)
    {
        FlushCheckpointWrites();
        ClearFactoryCoverage();
        _researchWindowFocus = "";
        ExitSpectator();
        CancelResourceUpgrade();
        FinishBuildStroke();
        string previousRun = Game.RunId; double previousTime = PlayTimeSeconds; bool previousEstimated = PlayTimeEstimated;
        if (!Game.Restore(data.Map("game")))
        {
            ShowNotice("防御记录版本不兼容");
            return false;
        }
        RestorePlayTime(data, retry, previousRun, previousTime, previousEstimated);
        PreserveCheckpoint = false;
        _checkpointPending = false;
        Planet.ResetCamera();
        FocusId = "earth";
        SolarNavOpen = false;
        Planet.RestoreSites(data.List("slots"), data.List("site_directions"));
        Planet.RestoreCelestialState(data.Map("celestial"));
        Battle.ResetBattle();
        if (data.ContainsKey("invasion_anchor"))
            Battle.RestoreInvasionAnchor(data.Vector3("invasion_anchor"));
        Battle.RestoreDestroyedFronts(data.List("destroyed_fronts").Cast<string>());
        Campaign.ResetRuntime();
        if (data.ContainsKey("expedition_battle"))
        {
            if (!Campaign.Restore(data.Map("expedition_battle")))
                throw new InvalidOperationException("Validated campaign failed to restore");
        }
        else if (data.ContainsKey("combat_snapshot"))
        {
            if (!Battle.RestoreCombatSnapshot(data.Map("combat_snapshot")))
                throw new InvalidOperationException("Validated combat failed to restore");
        }
        Campaign.SyncCampaign();
        CampaignWon = Battle.GetDestroyedFronts().Count == 8;
        Started = data.B("started");
        Battle.Active = Started;
        Defeated = false;
        bool legacyShieldLayout = !data.Map("game").Map("buildings").ContainsKey("shield");
        UserPaused = retry || Started && legacyShieldLayout;
        Battle.Paused = UserPaused;
        Campaign.Paused = UserPaused;
        Planet.Paused = UserPaused;
        Modal = "";
        DockOpen = true;
        CloseCombatSettings();
        SelectedBuild = "";
        Planet.PlacingBuilding = false;
        SaveCombatPreferences();
        RefreshCampaignUi();
        _graphDirty = true;
        NextWave = -1;
        Game.FactoryPerks.MarkDefenseStage(Game.DefenseReachStage);
        if (!retry && Started && !CampaignWon && !Battle.WaveRunning && !Campaign.OwnsEarthSchedule())
            Callable.From(StartWave).CallDeferred();
        RefreshWaveRetryForCurrentRun();
        ShowNotice(retry
            ? $"已回到第 {Game.Wave:00} 波开头 · 已暂停布防，按空格继续"
            : UserPaused && legacyShieldLayout
            ? $"已恢复第 {Game.Wave:00} 波 · 已暂停，研究并布置局部护盾后按空格继续"
            : $"已恢复第 {Game.Wave:00} 波记录 · 地球科技、资源与舰队进度已恢复");
        SyncRender();
        return true;
    }

    public bool Restart()
    {
        FlushCheckpointWrites();
        var preferences = Game.CombatSettings.DeepClone();
        if (!Game.Reset())
        {
            ShowNotice("永久特性档案保存失败，未重置当前防御");
            return false;
        }
        ResetPlayTime();
        _forecast.Clear();
        _checkpointPending = false;
        _waveStartRecord = null;
        ClearFactoryCoverage();
        _researchWindowFocus = "";
        ExitSpectator();
        CancelResourceUpgrade();
        Game.ApplyCombatSettings(preferences);
        Campaign.ResetRuntime();
        _graphDirty = true;
        PreserveCheckpoint = false;
        Planet.ResetPlanet();
        Planet.ResetCamera();
        FocusId = "earth";
        SolarNavOpen = false;
        Battle.ResetBattle();
        Battle.Active = false;
        Started = false;
        CampaignWon = false;
        Defeated = false;
        UserPaused = false;
        Modal = "";
        DockOpen = true;
        CloseCombatSettings();
        SelectedBuild = "";
        Planet.PlacingBuilding = false;
        NextWave = -1;
        Speed = 1;
        RefreshCampaignUi();
        ShowNotice("新的守望开始 · 已继承永久特性、等级和能源核心");
        UpdateFactoryPerkSites();
        return true;
    }
}

