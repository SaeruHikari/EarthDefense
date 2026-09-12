using Godot;
using Earthward.Domain;
using Earthward.Presentation;

namespace Earthward.Application;

public partial class Main
{
    private const double HudDataInterval = 1d / 15;
    private List<DataMap> _headerSnapshot = new();
    private double _headerSnapshotAt = double.NegativeInfinity;
    private string _headerRunId = "";
    private long _headerRevision = -1, _headerWave = -1;
    private bool _headerStarted, _headerPaused, _headerDefeated;
    private double _headerSpeed;
    private float[] _headerWidths = [], _headerValueWidths = [];
    private float _headerTotalWidth;
    private readonly Dictionary<(string Text, int Size), float> _hudTextWidths = new();
    private readonly Dictionary<(string Text, float Width, int Size), string> _hudFittedText = new();
    public long HeaderSnapshotRefreshCount { get; private set; }
    public long HudTextMeasureCount { get; private set; }
    public string HudDisplayedValue(string id) => _headerSnapshot.FirstOrDefault(row => row.S("id") == id)?.S("value") ?? "";

    /// <summary>Data refresh is capped at 15 Hz; actual hover, hitboxes, draw calls and animations remain per frame.</summary>
    public void InvalidateHudData() => _headerSnapshotAt = double.NegativeInfinity;

    private List<DataMap> HeaderData()
    {
        bool paused = WorldIsPaused();
        if (_headerSnapshot.Count > 0 && Elapsed >= _headerSnapshotAt && Elapsed - _headerSnapshotAt < HudDataInterval
            && _headerRunId == Game.RunId && _headerRevision == Game.FactoryStatsRevision && _headerWave == Game.Wave
            && _headerStarted == Started && _headerPaused == paused && _headerSpeed == Speed && _headerDefeated == Defeated)
            return _headerSnapshot;
        _headerSnapshot = BuildHeaderData();
        _headerSnapshotAt = Elapsed;
        _headerRunId = Game.RunId;
        _headerRevision = Game.FactoryStatsRevision;
        _headerWave = Game.Wave;
        _headerStarted = Started;
        _headerPaused = paused;
        _headerSpeed = Speed;
        _headerDefeated = Defeated;
        HeaderSnapshotRefreshCount++;
        if (_headerWidths.Length != _headerSnapshot.Count)
        {
            _headerWidths = new float[_headerSnapshot.Count];
            _headerValueWidths = new float[_headerSnapshot.Count];
        }
        _headerTotalWidth = 4 * (_headerSnapshot.Count - 1);
        for (int i = 0; i < _headerSnapshot.Count; i++)
        {
            var item = _headerSnapshot[i];
            float valueWidth = HudTextWidth(item.S("value"), 15);
            _headerValueWidths[i] = valueWidth;
            _headerWidths[i] = item.S("id") == "forecast" ? 91 : Math.Max(64, 41 + valueWidth + HudTextWidth(item.S("gain"), 10));
            _headerTotalWidth += _headerWidths[i];
        }
        return _headerSnapshot;
    }

    private sealed record BuildingHudData(string Id, string RunId, long Revision, long Wave, long Count, DataMap Cost, string CostText, string LockReason);
    private readonly Dictionary<string, BuildingHudData> _buildingHudData = new();
    private BuildingHudData BuildingHud(string id)
    {
        long count = Game.Buildings.L(id);
        if (_buildingHudData.TryGetValue(id, out var entry) && entry.RunId == Game.RunId && entry.Revision == Game.FactoryStatsRevision && entry.Wave == Game.Wave && entry.Count == count)
            return entry;
        var cost = Game.BuildingCost(id);
        entry = new BuildingHudData(id, Game.RunId, Game.FactoryStatsRevision, Game.Wave, count, cost, CostText(cost), Game.BuildingLockReason(id));
        _buildingHudData[id] = entry;
        return entry;
    }

    private bool CanBuildFromHud(BuildingHudData entry) => entry.LockReason.Length == 0 && Game.CanBuild(entry.Id);
    private string _hudTooltipText = "";
    private float _hudTooltipAvailableWidth;
    private string[] _hudTooltipLines = [];
    private float _hudTooltipWidth;
    private void MeasureHudTooltip(string text, float availableWidth)
    {
        if (_hudTooltipText == text && _hudTooltipAvailableWidth == availableWidth && _hudTooltipLines.Length > 0) return;
        _hudTooltipText = text;
        _hudTooltipAvailableWidth = availableWidth;
        _hudTooltipLines = UiTheme.Wrap(text, availableWidth, 12).Split('\n');
        _hudTooltipWidth = Math.Min(644, _hudTooltipLines.Max(line => HudTextWidth(line, 12)) + 24);
    }

    private float HudTextWidth(string text, int size)
    {
        var key = (text, size);
        if (_hudTextWidths.TryGetValue(key, out float width)) return width;
        width = UiTheme.Font.GetStringSize(text, fontSize: size).X;
        HudTextMeasureCount++;
        if (_hudTextWidths.Count >= 2048) _hudTextWidths.Clear();
        _hudTextWidths[key] = width;
        return width;
    }

    private string HudFitText(string text, float width, int size)
    {
        var key = (text, width, size);
        if (_hudFittedText.TryGetValue(key, out string? fitted)) return fitted;
        fitted = text;
        if (HudTextWidth(fitted, size) > width)
        {
            while (fitted.Length > 1 && HudTextWidth(fitted + "\u2026", size) > width) fitted = fitted[..^1];
            fitted += "\u2026";
        }
        if (_hudFittedText.Count >= 1024) _hudFittedText.Clear();
        _hudFittedText[key] = fitted;
        return fitted;
    }
}
