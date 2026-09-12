using Godot;
using System;
using System.Linq;
using Earthward.Combat;
using Earthward.Presentation;

namespace Earthward.Application;

public partial class Main
{
    public long SelectedShieldCoverageSiteId { get; private set; } = -1;
    public LocalShieldCoverageSnapshot? CurrentShieldCoverage { get; private set; }
    public long ShieldCoverageQueryCount { get; private set; }
    public Rect2 ShieldCoverageHudRect { get; private set; }
    private double _shieldCoverageDelay;

    public void SelectShieldCoverage(int site)
    {
        if (site < 0 || SelectedBuild != "" || ResourceUpgradeMode || IsObserving() || IsSpectating() || Defeated)
        {
            ClearFactoryCoverage();
            return;
        }
        ClearFactoryCoverageOnly();
        ClearSatelliteLauncher();
        SelectedShieldCoverageSiteId = site;
        RefreshShieldCoverage();
        QueueRedraw();
    }

    private void ClearShieldCoverageOnly()
    {
        bool had = SelectedShieldCoverageSiteId >= 0 || CurrentShieldCoverage != null;
        SelectedShieldCoverageSiteId = -1;
        CurrentShieldCoverage = null;
        ShieldCoverageHudRect = default;
        _shieldCoverageDelay = 0;
        if (IsInstanceValid(Planet))
            Planet.SetShieldCoverage(null);
        if (had)
            QueueRedraw();
    }

    private void UpdateShieldCoverage(double delta)
    {
        if (SelectedShieldCoverageSiteId < 0)
            return;
        if (SelectedBuild != "" || ResourceUpgradeMode || IsObserving() || IsSpectating() || Defeated)
        {
            ClearShieldCoverageOnly();
            return;
        }
        _shieldCoverageDelay -= delta;
        if (_shieldCoverageDelay <= 0)
            RefreshShieldCoverage();
    }

    private void RefreshShieldCoverage()
    {
        _shieldCoverageDelay = .25;
        ShieldCoverageQueryCount++;
        var row = Battle.GetLocalShieldState().FirstOrDefault(state => state.L("site_id") == SelectedShieldCoverageSiteId);
        if (row == null)
        {
            ClearShieldCoverageOnly();
            return;
        }
        var stats = Game.ShieldFacilityStats();
        var snapshot = new LocalShieldCoverageSnapshot(
            SelectedShieldCoverageSiteId,
            row.N("hp"),
            row.N("max_hp", stats.N("capacity", 100)),
            row.N("surface_radius", stats.N("surface_radius", 5)),
            row.N("altitude", stats.N("altitude", WorldScale.ShieldAltitude)),
            row.N("regeneration", stats.N("regeneration")),
            stats.N("earth_repair_rate_per_generator"));
        CurrentShieldCoverage = snapshot;
        Planet.SetShieldCoverage(snapshot);
    }

    private void DrawShieldCoverageHud()
    {
        ShieldCoverageHudRect = default;
        var coverage = CurrentShieldCoverage;
        if (coverage == null || SelectedShieldCoverageSiteId < 0 || IsObserving() || IsSpectating()
            || !Planet.IsSlotVisible((int)SelectedShieldCoverageSiteId))
            return;
        Vector2 anchor = Planet.GetSlotScreenPosition((int)SelectedShieldCoverageSiteId);
        if (!new Rect2(Vector2.Zero, WorldSize).HasPoint(anchor))
            return;

        var rect = PlaceCoverageHud(anchor, new Vector2(270, 190));
        ShieldCoverageHudRect = rect;
        Vector2 p = rect.Position;
        var attachment = new Vector2(Math.Clamp(anchor.X, rect.Position.X, rect.End.X), Math.Clamp(anchor.Y, rect.Position.Y + 12, rect.End.Y - 12));
        DrawLine(anchor, attachment, UiTheme.Alpha(UiTheme.Cyan, .58f), 1.15f, true);
        DrawArc(anchor, 10, 0, Mathf.Tau, 30, UiTheme.Alpha(UiTheme.Cyan, .95f), 1.35f, true);
        FloatingPanel(rect, .92f);
        HudIcon("shield", p + new Vector2(16, 18), 7, UiTheme.Cyan);
        Text(UiTheme.Fit(BuildingName("shield") + $" #{coverage.SiteId + 1}", 212, 12), p + new Vector2(30, 22), 12, UiTheme.Ink);
        Button(new(rect.End.X - 25, p.Y + 7, 18, 20), "×", "shield_coverage:close");
        Text("局部覆盖 · 运行中", p + new Vector2(13, 40), 10, UiTheme.Cyan);

        string[] labels = ["覆盖半径", "覆盖角度", "护盾强度", "自身恢复"];
        string[] values =
        [
            $"{coverage.SurfaceRadius:0.##}",
            $"{coverage.AngleRadians * 180 / Math.PI:0.##}°",
            $"{UiTheme.Number(coverage.Hp)} / {UiTheme.Number(coverage.Capacity)}",
            $"{coverage.Regeneration:0.##}/s"
        ];
        Color[] colors = [UiTheme.Mint, UiTheme.Cyan, coverage.Integrity > .25 ? UiTheme.Cyan : UiTheme.Coral, UiTheme.Cyan];
        for (int i = 0; i < labels.Length; i++)
        {
            var position = p + new Vector2(13 + i % 2 * 132, 59 + i / 2 * 39);
            Text(labels[i], position, 10, UiTheme.Muted);
            Text(UiTheme.Fit(values[i], 122, 15), position + new Vector2(0, 18), 15, colors[i]);
        }

        DrawLine(p + new Vector2(12, 137), p + new Vector2(258, 137), UiTheme.Alpha(UiTheme.Muted, .2f), 1);
        string cooldown = Game.LocalShieldBuildCooldownRemaining > 0
            ? $"建造冷却 {Game.LocalShieldBuildCooldownRemaining} 波"
            : "建造可用";
        string repair = coverage.EarthRepairRatePerGenerator > 0
            ? $"地球修复 +{coverage.EarthRepairRatePerGenerator:0.##}/s"
            : "地球修复未研究";
        Text($"离地 {coverage.Altitude:0.##} · {repair}", p + new Vector2(13, 156), 10, UiTheme.Mint);
        Text(UiTheme.Fit($"上限 {Game.LocalShieldBuildLimit} 座 · {cooldown}", 244, 10), p + new Vector2(13, 174), 10, UiTheme.Amber);

        Hint(rect,
            $"{BuildingName("shield")} #{coverage.SiteId + 1}\n"
            + $"覆盖半径 {coverage.SurfaceRadius:0.##} · 地表覆盖角度 {coverage.AngleRadians * 180 / Math.PI:0.##}°\n"
            + $"护盾强度 {UiTheme.Number(coverage.Hp)} / {UiTheme.Number(coverage.Capacity)} · 自身恢复 {coverage.Regeneration:0.##}/s\n"
            + $"离地高度 {coverage.Altitude:0.##} · 单座地球修复 {coverage.EarthRepairRatePerGenerator:0.##}/s\n"
            + $"场上 {Game.Buildings.L("shield")} / {Game.LocalShieldBuildLimit} 座 · {cooldown}"
            + (Game.LocalShieldAchievementBonus > 0 ? $"\n永久成就贡献：建造上限 +{Game.LocalShieldAchievementBonus} 座" : ""));
    }
}
