using Godot;
using System.Globalization;
using Earthward.Combat;
using Earthward.Presentation;
namespace Earthward.Application;

public partial class Main
{
    public long SelectedCoverageSiteId { get; private set; } = -1;
    public FactoryCoverageSnapshot? CurrentFactoryCoverage { get; private set; }
    public long FactoryCoverageQueryCount { get; private set; }
    public Rect2 FactoryCoverageHudRect { get; private set; }
    private double _factoryCoverageDelay;
    private string[] _coverageValues = ["—", "—", "—", "—"];
    private string _coverageSource = "";
    private string _coverageDetail = "";
    private string _coverageLargeHeight = "";
    private bool _coverageGlobalProjection;

    public void SelectFactoryCoverage(int site)
    {
        if (site < 0 || SelectedBuild != "" || ResourceUpgradeMode || IsObserving() || IsSpectating() || Defeated)
        {
            ClearFactoryCoverage();
            return;
        }
        ClearShieldCoverageOnly();
        SelectedCoverageSiteId = site;
        RefreshFactoryCoverage();
        QueueRedraw();
    }

    private void ClearFactoryCoverageOnly()
    {
        SelectedCoverageSiteId = -1;
        CurrentFactoryCoverage = null;
        FactoryCoverageHudRect = default;
        _factoryCoverageDelay = 0;
        if (IsInstanceValid(Planet))
            Planet.SetFactoryCoverage(null);
    }

    public void ClearFactoryCoverage()
    {
        bool had = SelectedCoverageSiteId >= 0 || CurrentFactoryCoverage != null
            || SelectedShieldCoverageSiteId >= 0 || CurrentShieldCoverage != null;
        ClearFactoryCoverageOnly();
        ClearShieldCoverageOnly();
        if (!had)
            return;
        QueueRedraw();
    }

    private void UpdateFactoryCoverage(double delta)
    {
        if (SelectedCoverageSiteId < 0)
            return;
        if (SelectedBuild != "" || ResourceUpgradeMode || IsObserving() || IsSpectating() || Defeated)
        {
            ClearFactoryCoverage();
            return;
        }
        _factoryCoverageDelay -= delta;
        if (_factoryCoverageDelay <= 0)
            RefreshFactoryCoverage();
    }

    private void RefreshFactoryCoverage()
    {
        _factoryCoverageDelay = .25;
        FactoryCoverageQueryCount++;
        var coverage = Battle.GetFactoryCoverage(SelectedCoverageSiteId);
        if (coverage == null)
        {
            ClearFactoryCoverage();
            return;
        }
        if (!ReferenceEquals(CurrentFactoryCoverage, coverage))
        {
            CurrentFactoryCoverage = coverage;
            CacheFactoryCoverageLabels(coverage);
        }
        Planet.SetFactoryCoverage(coverage);
    }

    private static string CoverageRange(IEnumerable<double> source)
    {
        var values = source.Where(value => double.IsFinite(value) && value >= 0).ToArray();
        if (values.Length == 0)
            return "—";
        double low = values.Min(), high = values.Max();
        string Format(double value) => value < 1000 ? value.ToString("0.##", CultureInfo.InvariantCulture) : UiTheme.Number(value);
        return Math.Abs(high - low) < .000001 ? Format(low) : Format(low) + "–" + Format(high);
    }

    private void CacheFactoryCoverageLabels(FactoryCoverageSnapshot coverage)
    {
        var bands = coverage.Bands;
        _coverageSource = coverage.Preview ? "计划预览 · 尚无在役战机" : $"在役 {coverage.AircraftCount} 架";
        if (bands.Count > 1)
            _coverageSource += $" · {bands.Count} 组配置";
        _coverageValues =
        [
            CoverageRange(bands.Select(band => band.PatrolRadius)),
            CoverageRange(bands.Count == 0 ? Array.Empty<double>() : bands.Select(band => band.MaximumPursuitAltitude).Append(WorldScale.DroneAltitude)),
            CoverageRange(bands.Select(band => band.WeaponRange)),
            CoverageRange(bands.Select(band => band.MaximumCombatAltitude))
        ];
        _coverageGlobalProjection = bands.Any(band => band.AngleRadians >= Math.PI - .000001);
        bool largeTargetBonus = bands.Any(band => band.MaximumLargeTargetCombatAltitude > band.MaximumCombatAltitude + .000001);
        _coverageLargeHeight = largeTargetBonus ? "大目标高度 " + CoverageRange(bands.Select(band => band.MaximumLargeTargetCombatAltitude)) : "";
        _coverageDetail = $"{BuildingName(coverage.Kind)} #{coverage.SiteId + 1} · {_coverageSource}\n"
            + $"巡逻半径 {_coverageValues[0]} · 远端范围 {CoverageRange(bands.Select(band => band.OuterRange))}\n"
            + $"巡航高度 {_coverageValues[1]} · 武器射程 {_coverageValues[2]}\n"
            + $"普通目标最高作战高度 {_coverageValues[3]}\n"
            + $"大目标射程 {CoverageRange(bands.Select(band => band.LargeTargetWeaponRange))} · 最高高度 {CoverageRange(bands.Select(band => band.MaximumLargeTargetCombatAltitude))}\n"
            + "高度均从地表计算；巡航高度显示全厂空域包络，混编战机各自遵循本机上限。圆环是地表投影，混合配置显示最小–最大值；不同战机能力不会拼成一架。实际命中仍需满足高度、射程、瞄准与遮挡条件。\n动能无法穿透能量层，激光破盾后可接力。";
        if (bands.Count == 0)
            _coverageDetail = "当前工厂没有可生产编制，增加工厂容量后可以查看计划覆盖。";
    }

    private Rect2 PlaceCoverageHud(Vector2 anchor, Vector2 size)
    {
        var alternatives = new[]
        {
            anchor + new Vector2(34, -size.Y * .5f),
            anchor - new Vector2(size.X + 34, size.Y * .5f),
            anchor + new Vector2(34, 28),
            anchor - new Vector2(size.X + 34, size.Y + 28)
        };
        Rect2 chosen = default;
        double score = double.PositiveInfinity;
        foreach (var candidate in alternatives)
        {
            var position = new Vector2(
                Math.Clamp(candidate.X, 12, Math.Max(12, WorldSize.X - size.X - 12)),
                Math.Clamp(candidate.Y, 58, Math.Max(58, WorldSize.Y - size.Y - 112)));
            var rect = new Rect2(position, size);
            double overlap = 0;
            foreach (var obstacle in UiRects)
                if (rect.Intersects(obstacle))
                {
                    var intersection = rect.Intersection(obstacle);
                    overlap += intersection.Size.X * intersection.Size.Y;
                }
            double value = overlap * 100 + position.DistanceSquaredTo(candidate);
            if (value >= score)
                continue;
            chosen = rect;
            score = value;
        }
        return chosen;
    }

    private void DrawFactoryCoverageHud()
    {
        FactoryCoverageHudRect = default;
        var coverage = CurrentFactoryCoverage;
        if (coverage == null || SelectedCoverageSiteId < 0 || IsObserving() || IsSpectating() || !Planet.IsSlotVisible((int)SelectedCoverageSiteId))
            return;
        Vector2 anchor = Planet.GetSlotScreenPosition((int)SelectedCoverageSiteId);
        if (!new Rect2(Vector2.Zero, WorldSize).HasPoint(anchor))
            return;
        bool empty = coverage.Bands.Count == 0;
        var rect = PlaceCoverageHud(anchor, new Vector2(258, empty ? 82 : _coverageLargeHeight.Length > 0 ? 162 : 145));
        FactoryCoverageHudRect = rect;
        Vector2 p = rect.Position;
        var attachment = new Vector2(Math.Clamp(anchor.X, rect.Position.X, rect.End.X), Math.Clamp(anchor.Y, rect.Position.Y + 12, rect.End.Y - 12));
        DrawLine(anchor, attachment, UiTheme.Alpha(UiTheme.Mint, .55f), 1.1f, true);
        DrawArc(anchor, 9, 0, Mathf.Tau, 28, UiTheme.Alpha(UiTheme.Mint, .9f), 1.25f, true);
        FloatingPanel(rect, .91f);
        HudIcon(coverage.Kind, p + new Vector2(16, 18), 7, UiTheme.Mint);
        Text(UiTheme.Fit(BuildingName(coverage.Kind) + $" #{coverage.SiteId + 1}", 205, 12), p + new Vector2(30, 22), 12, UiTheme.Ink);
        Button(new(rect.End.X - 25, p.Y + 7, 18, 20), "×", "coverage:close");
        Text(UiTheme.Fit(_coverageSource, 230, 10), p + new Vector2(13, 38), 10, coverage.Preview ? UiTheme.Amber : UiTheme.Muted);
        if (empty)
        {
            Text("无可生产编制 · 请先增加容量", p + new Vector2(13, 65), 11, UiTheme.Muted);
            Hint(rect, _coverageDetail);
            return;
        }
        string[] labels = ["巡逻半径", "巡航高度", "武器射程", "最高作战高度"];
        Color[] colors = [UiTheme.Mint, UiTheme.Cyan, UiTheme.Cyan, UiTheme.Ink];
        for (int i = 0; i < 4; i++)
        {
            var position = p + new Vector2(13 + i % 2 * 124, 56 + i / 2 * 35);
            Text(labels[i], position, 10, UiTheme.Muted);
            Text(UiTheme.Fit(_coverageValues[i], 110, 15), position + new Vector2(0, 18), 15, colors[i]);
        }
        DrawLine(p + new Vector2(12, 117), p + new Vector2(246, 117), UiTheme.Alpha(UiTheme.Muted, .18f), 1);
        DrawCircle(p + new Vector2(16, 131), 2.5f, UiTheme.Mint);
        Text("巡逻", p + new Vector2(23, 135), 10, UiTheme.Mint);
        DrawCircle(p + new Vector2(94, 131), 2.5f, UiTheme.Amber);
        Text(_coverageGlobalProjection ? "追击·全球" : "追击", p + new Vector2(101, 135), 10, UiTheme.Amber);
        DrawCircle(p + new Vector2(178, 131), 2.5f, UiTheme.Cyan);
        Text("高度", p + new Vector2(185, 135), 10, UiTheme.Cyan);
        if (_coverageLargeHeight.Length > 0)
            Text(UiTheme.Fit(_coverageLargeHeight, 230, 10), p + new Vector2(13, 153), 10, UiTheme.Muted);
        Hint(rect, _coverageDetail);
    }
}
