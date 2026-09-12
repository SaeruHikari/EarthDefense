using Godot;
using Earthward.Domain;
using Earthward.Presentation;

namespace Earthward.Application;

public partial class Main
{
    public long SelectedSatelliteLauncherSiteId { get; private set; } = -1;
    public Rect2 SatelliteLauncherHudRect { get; private set; }
    public bool SatelliteGuidePending => Game != null && !Game.ResearchSatelliteDeployed;

    private string _satelliteGuideRunId = "";
    private bool _satelliteGuideActive;

    public void InitializeSatelliteGuide()
    {
        if (Game == null)
            return;
        if (_satelliteGuideRunId != Game.RunId)
        {
            _satelliteGuideRunId = Game.RunId;
            _satelliteGuideActive = !Game.ResearchSatelliteDeployed;
        }
        if (!_satelliteGuideActive || Game.ResearchSatelliteDeployed)
            return;
        if (!Game.HasSatelliteLauncher)
            ShowNotice("开局引导 · 在右侧「建设」选择卫星发射中心，建造一次发射设施");
        else if (Game.ResearchSatelliteLaunchInProgress)
            ShowNotice("科研卫星正在发射 · 等待火箭完成分级上升并入轨");
        else
            ShowNotice("开局引导 · 点击地表的卫星发射中心，发射免费的第一颗科研卫星");
    }

    public void NotifySatelliteLauncherBuilt(int site)
    {
        if (Game == null)
            return;
        _satelliteGuideActive = true;
        SelectSatelliteLauncher(site);
        ShowNotice("开局引导 · 卫星发射中心已就绪，点击设施卡片中的「发射科研卫星」");
    }

    public void SelectSatelliteLauncher(int site)
    {
        if (Game == null || Planet == null || site < 0 || site >= Planet.GetSlots().Count || !Planet.HasSatelliteLauncher(site) || SelectedBuild != "" || ResourceUpgradeMode || IsObserving() || IsSpectating() || Defeated)
        {
            ClearSatelliteLauncher();
            return;
        }
        ClearFactoryCoverageOnly();
        ClearShieldCoverageOnly();
        SelectedSatelliteLauncherSiteId = site;
        if (!Game.ResearchSatelliteDeployed && !Game.ResearchSatelliteLaunchInProgress)
            ShowNotice("卫星发射中心 · 首颗科研卫星发射免费，火箭将分级上升并自动入轨");
        QueueRedraw();
    }

    public void ClearSatelliteLauncher()
    {
        if (SelectedSatelliteLauncherSiteId < 0 && SatelliteLauncherHudRect == default)
            return;
        SelectedSatelliteLauncherSiteId = -1;
        SatelliteLauncherHudRect = default;
        QueueRedraw();
    }

    public void LaunchResearchSatellite()
    {
        if (Game == null || Planet == null || SelectedSatelliteLauncherSiteId < 0)
            return;
        if (Game.ResearchSatelliteDeployed)
        {
            ShowNotice("科研卫星已经在轨运行");
            return;
        }
        if (Game.ResearchSatelliteLaunchInProgress)
        {
            ShowNotice("科研卫星正在发射 · 请等待入轨动画完成");
            return;
        }
        if (!Game.BeginResearchSatelliteLaunch())
        {
            ShowNotice("请先建造卫星发射中心，并确认它仍在地表运行");
            return;
        }
        if (!Planet.StartResearchSatelliteLaunch((int)SelectedSatelliteLauncherSiteId))
        {
            Game.CancelResearchSatelliteLaunch();
            ShowNotice("发射序列无法启动 · 发射器位置不可用");
            return;
        }
        ShowNotice("发射序列已启动 · 一级助推器点火，目标为近地轨道");
        SaveIfSafe();
        QueueRedraw();
    }

    private void OnResearchSatelliteLaunchCompleted()
    {
        if (Game == null)
            return;
        if (Game.CompleteResearchSatelliteLaunch())
        {
            _satelliteGuideActive = false;
            ClearSatelliteLauncher();
            ShowNotice("科研卫星已成功入轨 · 科研收入上线，轨道环与卫星开始运行");
            SaveIfSafe();
        }
    }

    private void DrawSatelliteLauncherHud()
    {
        SatelliteLauncherHudRect = default;
        if (Game == null || Planet == null || SelectedSatelliteLauncherSiteId < 0 || IsObserving() || IsSpectating() || !Planet.IsSlotVisible((int)SelectedSatelliteLauncherSiteId))
            return;
        Vector2 anchor = Planet.GetSlotScreenPosition((int)SelectedSatelliteLauncherSiteId);
        if (!new Rect2(Vector2.Zero, WorldSize).HasPoint(anchor))
            return;

        var rect = PlaceCoverageHud(anchor, new Vector2(304, Game.ResearchSatelliteLaunchInProgress ? 190 : 174));
        SatelliteLauncherHudRect = rect;
        Vector2 p = rect.Position;
        Vector2 attachment = new(Mathf.Clamp(anchor.X, rect.Position.X, rect.End.X), Mathf.Clamp(anchor.Y, rect.Position.Y + 12, rect.End.Y - 12));
        DrawLine(anchor, attachment, UiTheme.Alpha(UiTheme.Alien, .62f), 1.2f, true);
        DrawArc(anchor, 10, 0, Mathf.Tau, 30, UiTheme.Alpha(UiTheme.Alien, .94f), 1.4f, true);
        FloatingPanel(rect, .94f);
        HudIcon("satellite_launcher", p + new Vector2(18, 19), 8, UiTheme.Alien);
        Text("卫星发射中心", p + new Vector2(34, 23), 13, UiTheme.Ink);
        Text($"地表设施 #{SelectedSatelliteLauncherSiteId + 1:00} · 七格蜂窝 · 限一座", p + new Vector2(13, 43), 10, UiTheme.Muted);
        Button(new(rect.End.X - 27, p.Y + 7, 19, 20), "×", "satellite:close");

        if (Game.ResearchSatelliteLaunchInProgress)
        {
            float progress = Planet.SatelliteLaunchProgress;
            Text("科研卫星发射中", p + new Vector2(13, 72), 14, UiTheme.Cyan);
            Text(progress < .34f ? "一级助推器上升" : progress < .62f ? "一级分离 · 二级推进" : progress < .72f ? "二级分离 · 载荷滑行" : "整流罩分离 · 卫星入轨", p + new Vector2(13, 94), 11, UiTheme.Muted);
            DrawRect(new Rect2(p + new Vector2(13, 111), new Vector2(278, 6)), new("20313a"));
            DrawRect(new Rect2(p + new Vector2(13, 111), new Vector2(278 * progress, 6)), UiTheme.Cyan);
            Text($"发射进度 {progress * 100:0}%", p + new Vector2(13, 139), 11, UiTheme.Cyan);
            Text("目标轨道已对齐 · 入轨后自动开始科研", p + new Vector2(13, 162), 10, UiTheme.Dim);
            return;
        }

        if (Game.ResearchSatelliteDeployed)
        {
            Text("科研卫星已在轨", p + new Vector2(13, 72), 14, UiTheme.Mint);
            Text("轨道环与卫星持续运行 · 科研收入已接入", p + new Vector2(13, 96), 11, UiTheme.Muted);
            Text("科研基数 " + FormatSetting(Game.ResourceFacilityBaseOutputs().N("science")) + " / 秒", p + new Vector2(13, 120), 12, UiTheme.Cyan);
            return;
        }

        Text("发射窗口已就绪", p + new Vector2(13, 72), 14, UiTheme.Alien);
        Text("第一颗科研卫星 · 免费发射", p + new Vector2(13, 96), 12, UiTheme.Ink);
        Text("火箭会逐级上升、分离级段，并把卫星送入近地轨道", p + new Vector2(13, 119), 10, UiTheme.Muted);
        Button(new(p + new Vector2(13, 134), new(278, 30)), "发射科研卫星 · 免费", "satellite:launch:research", true, true, "primary");
        Hint(rect, "卫星发射中心 · 当前仅开放科研卫星。发射无资源消耗，完成后科研收入开始运行。");
    }
}
