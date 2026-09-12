using Godot;
using System.Globalization;
using Earthward.Presentation;

namespace Earthward.Application;

public partial class Main
{
    public static readonly string[] CheatResourceIds = ["minerals", "energy", "science", "resource_cores", "alien_points", "energy_cores"];
    private static readonly string[] CheatResourceNames = ["矿物", "能量", "科研", "资源核心", "外星科技点", "永久能源核心"];
    private static readonly string[] CheatResourceIcons = ["mineral", "energy", "science", "core", "alien", "core"];
    public LineEdit CheatAmountField { get; private set; } = null!;
    public string SelectedCheatResource { get; private set; } = "minerals";
    public string CheatFeedback { get; private set; } = "";
    private bool _cheatError;

    private void CreateCheatControl()
    {
        CheatAmountField = new LineEdit
        {
            Name = "ResourceCheatIncrement", Position = new Vector2(748, 593), Size = new Vector2(310, 44),
            Alignment = HorizontalAlignment.Right, MaxLength = 64, SelectAllOnFocus = true,
            Text = "1000", PlaceholderText = "输入要增加的数量", TooltipText = "增加到当前余额；资源核心、外星科技点和能源核心必须是正整数。"
        };
        CheatAmountField.AddThemeFontOverride("font", UiTheme.Font);
        CheatAmountField.AddThemeFontSizeOverride("font_size", 20);
        CheatAmountField.AddThemeColorOverride("font_color", UiTheme.Ink);
        CheatAmountField.AddThemeStyleboxOverride("normal", UiTheme.Panel(new Color("0b1923"), new Color("496875"), 6));
        _combatControls.AddChild(CheatAmountField);
        CheatAmountField.Hide();
    }

    public void SelectCheatResource(string resource)
    {
        if (!CheatResourceIds.Contains(resource)) return;
        SelectedCheatResource = resource;
        CheatFeedback = "";
        _cheatError = false;
        QueueRedraw();
    }

    public bool ApplyResourceCheat()
    {
        if (!double.TryParse(CheatAmountField.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)
            || !double.IsFinite(amount) || amount <= 0)
        {
            CheatFeedback = "请输入有限的正数增量，当前余额未改变";
            _cheatError = true;
            CheatAmountField.GrabFocus();
            QueueRedraw();
            return false;
        }
        if (!Game.AddResourcesCheat(SelectedCheatResource, amount, SaveCheckpoint))
        {
            CheatFeedback = Game.LastCheatError;
            _cheatError = true;
            QueueRedraw();
            return false;
        }
        string name = CheatResourceNames[Array.IndexOf(CheatResourceIds, SelectedCheatResource)];
        CheatFeedback = $"{name} +{UiTheme.Number(amount)} · 当前 {UiTheme.Number(Game.CheatResourceBalance(SelectedCheatResource))}";
        _cheatError = false;
        _graphDirty = true;
        Sounds.PlaySound("research");
        ShowNotice(CheatFeedback);
        QueueRedraw();
        return true;
    }

    public bool ApplyUnlockAllTechnologyCheat()
    {
        if (!Game.UnlockAllTechnologyCheat(SaveCheckpoint))
        {
            CheatFeedback = Game.LastCheatError;
            _cheatError = true;
            QueueRedraw();
            return false;
        }
        _researchWindowFocus = "";
        RefreshGraph();
        if (SelectedCoverageSiteId >= 0) SelectFactoryCoverage((int)SelectedCoverageSiteId);
        else if (SelectedShieldCoverageSiteId >= 0) SelectShieldCoverage((int)SelectedShieldCoverageSiteId);
        CheatFeedback = "常规科技已免费完成 · 进阶科技需自行研究，已有进度保留";
        _cheatError = false;
        Sounds.PlaySound("research");
        ShowNotice(CheatFeedback);
        QueueRedraw();
        return true;
    }

    private void DrawResourceCheats()
    {
        Text("选择资源，再输入增加的数量 · 已有余额保留", new Vector2(184, 269), 13, UiTheme.Muted);
        for (int i = 0; i < CheatResourceIds.Length; i++)
        {
            string resource = CheatResourceIds[i];
            bool selected = resource == SelectedCheatResource;
            Color accent = i is 3 or 5 ? UiTheme.Amber : i == 4 ? UiTheme.Alien : i == 1 ? UiTheme.Amber : UiTheme.Mint;
            var rect = new Rect2(182 + i % 2 * 550, 291 + i / 2 * 90, 524, 76);
            Box(rect, new Color(selected ? "203a3b" : "142935"), selected ? accent : UiTheme.Line, 8);
            HudIcon(CheatResourceIcons[i], rect.Position + new Vector2(30, 35), 13, accent);
            Text(CheatResourceNames[i], rect.Position + new Vector2(58, 28), 16, selected ? UiTheme.Ink : UiTheme.Muted);
            Text(UiTheme.Number(Game.CheatResourceBalance(resource)), rect.Position + new Vector2(58, 57), 20, accent);
            if (selected) Text("已选择", rect.Position + new Vector2(450, 43), 12, accent);
            if (i == 5) Text("跨局保留", rect.Position + new Vector2(332, 43), 11, UiTheme.Amber);
            RegisterButton(rect, "modal:combat:resource:" + resource);
        }
        int index = Array.IndexOf(CheatResourceIds, SelectedCheatResource);
        Box(new Rect2(182, 578, 1074, 72), new Color("11242e"), UiTheme.Line, 8);
        Text("增加 " + CheatResourceNames[index], new Vector2(202, 609), 18, UiTheme.Ink);
        Text(index >= 3 ? "输入正整数增量" : "输入正数增量，支持小数与科学计数法", new Vector2(202, 634), 11, UiTheme.Muted);
        Text("+", new Vector2(722, 623), 26, UiTheme.Mint);
        Text(UiTheme.Fit(CheatFeedback, 1060, 13), new Vector2(184, 686), 13, _cheatError ? UiTheme.Coral : UiTheme.Mint);
        Text("每次提交累加到所选资源；永久能源核心按原特性档案保存。", new Vector2(184, 712), 12, UiTheme.Muted);
        Button(new Rect2(182, 736, 215, 39), "解锁全部科技", "modal:combat:unlock_all");
        Text("免费完成常规科技；进阶需自行研究，永久特性不变", new Vector2(416, 761), 11, UiTheme.Amber);
        Button(new Rect2(1044, 736, 212, 39), "增加所选资源 +", "modal:combat:cheat", true, true, "primary");
    }
}
