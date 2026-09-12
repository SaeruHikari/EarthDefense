using Godot;
using Earthward.Domain;
using Earthward.Presentation;

namespace Earthward.Application;

public partial class Main
{
    public const string AchievementProfilePath = "user://earthward_achievements.json";
    private string _achievementReturnModal = "";
    private string _achievementSaveError = "";
    private bool _achievementJustUnlocked, _pendingDefeatAchievement;
    private double _achievementOpenedAt;

    private void LoadAchievements()
    {
        if (!Game.Achievements.LoadProfile(ProjectSettings.GlobalizePath(AchievementProfilePath)))
        {
            _achievementSaveError = "成就档案读取失败，原文件已保留。";
            ShowNotice(_achievementSaveError);
        }
    }

    private void RecordDefeatAchievement()
    {
        ApplyAchievementResult(Game.RecordDefeat());
    }

    private void ApplyAchievementResult(DataMap result)
    {
        _pendingDefeatAchievement = !result.B("ok");
        _achievementSaveError = _pendingDefeatAchievement ? "成就尚未保存，请检查存档目录后重试。" : "";
        if (result.B("unlocked"))
        {
            _achievementJustUnlocked = true;
            var definition = AchievementCatalog.Definition(AchievementCatalog.FirstDefeatId);
            ShowNotice($"成就达成 · {definition.S("name")} · 护盾容量科技永久额外 +{definition.I("reward_per_research")}");
            if (IsInstanceValid(Sounds)) Sounds.PlaySound("research");
        }
        InvalidateHudData();
        _graphDirty = true;
        QueueRedraw();
    }

    private void RetryAchievementSave()
    {
        if (!Game.Achievements.LoadProfile(ProjectSettings.GlobalizePath(AchievementProfilePath)))
        {
            _achievementSaveError = "成就档案仍无法读取，原文件已保留。";
            return;
        }
        if (_pendingDefeatAchievement)
            // The original defeat was already verified before entering this
            // retry path, even if the player has since restarted the run.
            ApplyAchievementResult(Game.Achievements.RecordDefeat());
        else
            _achievementSaveError = "";
        QueueRedraw();
    }

    public void OpenAchievements()
    {
        if (Modal == "achievements")
        {
            CloseAchievements();
            return;
        }
        if (Modal is not ("" or "defeat")) return;
        _achievementReturnModal = Defeated ? "defeat" : "";
        CancelBuildSelection();
        CancelResourceUpgrade();
        Dragging = false;
        _middleDragging = false;
        _achievementOpenedAt = Elapsed;
        _achievementJustUnlocked = false;
        Modal = "achievements";
        ResearchGraph.Hide();
        _researchClip.Hide();
        FactoryPerkSidebar.Hide();
        SyncAchievementPause();
        QueueRedraw();
    }

    public void CloseAchievements()
    {
        if (Modal != "achievements") return;
        Modal = Defeated ? "defeat" : _achievementReturnModal;
        _achievementReturnModal = "";
        SyncAchievementPause();
        QueueRedraw();
    }

    private void SyncAchievementPause()
    {
        // Only modal ownership changes. Keep the user's existing pause and
        // camera state intact when returning to play or to the defeat screen.
        bool paused = WorldIsPaused();
        Battle.Paused = paused;
        Campaign.Paused = paused;
        Planet.Paused = paused;
    }

    private void DrawAchievementHeaderButton()
    {
        Rect2 rect = new(WorldSize.X - 170, 14, 34, 34);
        Button(rect, "", "achievements", Modal == "achievements");
        DrawAchievementMedal(rect.GetCenter(), 9, Modal == "achievements" ? UiTheme.Mint : UiTheme.Ink);
        Hint(rect, "成就 · 查看跨局保留的永久加成");
        if (_achievementJustUnlocked || _achievementSaveError.Length > 0)
            DrawCircle(rect.Position + new Vector2(28, 6), 3,
                _achievementSaveError.Length > 0 ? UiTheme.Coral : UiTheme.Mint);
    }

    private void DrawAchievementMedal(Vector2 p, float radius, Color color)
    {
        DrawPolyline(new[]
        {
            p + new Vector2(-.6f, .34f) * radius, p + new Vector2(-.78f, 1.05f) * radius,
            p + new Vector2(-.28f, .84f) * radius, p + new Vector2(0, 1.12f) * radius,
            p + new Vector2(.28f, .84f) * radius, p + new Vector2(.78f, 1.05f) * radius,
            p + new Vector2(.6f, .34f) * radius
        }, UiTheme.Alpha(color, .85f), 1.25f, true);
        Vector2[] hex = new Vector2[7];
        for (int i = 0; i < hex.Length; i++)
            hex[i] = p + Vector2.FromAngle(Mathf.Pi / 6 + i * Mathf.Tau / 6) * radius;
        DrawColoredPolygon(hex[..6], new Color("10232d"));
        DrawPolyline(hex, color, 1.5f, true);
        HudIcon("shield", p, radius * .47f, color);
    }

    private void DrawDefeatAchievement()
    {
        bool owned = Game.Achievements.HasUnlocked(AchievementCatalog.FirstDefeatId);
        if (!owned && _achievementSaveError.Length == 0) return;
        var definition = AchievementCatalog.Definition(AchievementCatalog.FirstDefeatId);
        Rect2 rect = new(481, 440, 478, 29);
        Box(rect, new("122b2c"), UiTheme.Alpha(owned ? UiTheme.Mint : UiTheme.Coral, .38f), 6);
        DrawAchievementMedal(new(498, 454), 7, owned ? UiTheme.Mint : UiTheme.Coral);
        Text(owned
            ? (_achievementJustUnlocked ? "成就达成" : "永久成就") + $" · {definition.S("name")} · 护盾容量科技额外 +{definition.I("reward_per_research")}"
            : "成就尚未保存 · 点击查看并重试", new(514, 459), 11, owned ? UiTheme.Mint : UiTheme.Coral);
        RegisterButton(rect, "modal:achievements:open");
    }

    private void DrawAchievements()
    {
        // A short sheet reveal continues on UI time while combat is paused.
        float progress = (float)Math.Clamp((Elapsed - _achievementOpenedAt) / .22, 0, 1);
        float offset = 12 * MathF.Pow(1 - progress, 3);
        SetUiOffset(_uiOffset + new Vector2(0, offset));
        bool owned = Game.Achievements.HasUnlocked(AchievementCatalog.FirstDefeatId);
        Color accent = owned ? UiTheme.Mint : UiTheme.Cyan;
        var definition = AchievementCatalog.Definition(AchievementCatalog.FirstDefeatId);
        int bonus = definition.I("reward_per_research");

        Rect2 panel = new(294, 211, 852, 472);
        Box(new(panel.Position + new Vector2(0, 8), panel.Size), new(0, 0, 0, .28f), Colors.Transparent, 14);
        Box(panel, new("10212b"), new("3a5662"), 14);
        DrawLine(new(321, 212), new(1119, 212), UiTheme.Alpha(UiTheme.Cyan, .45f), 1.2f, true);
        DrawAchievementMedal(new(338, 254), 17, UiTheme.Mint);
        Text("成就", new(373, 257), 28, UiTheme.Ink);
        Text("每一次守望，都留下成长。", new(373, 284), 13, UiTheme.Muted);
        Box(new(925, 242, 126, 32), new("172d35"), new("314951"), 6);
        Center(owned ? "已达成  1 / 1" : "已达成  0 / 1", new(925, 242, 126, 32), 12, accent);
        Button(new(1074, 237, 42, 36), "×", "modal:achievements:close");
        DrawLine(new(322, 308), new(1118, 308), UiTheme.Line, 1, true);

        Rect2 row = new(322, 331, 796, 231);
        Box(row, new(owned ? "142b30" : "13252f"), new(owned ? "426b61" : "304750"), 10);
        DrawLine(new(323, 350), new(323, 541), UiTheme.Alpha(accent, .65f), 2, true);
        Box(new(342, 352, 112, 189), new("0d1d28"), new("2d4650"), 8);
        DrawArc(new(398, 408), 37, 0, Mathf.Tau, 64, UiTheme.Alpha(accent, .16f), 1, true);
        DrawAchievementMedal(new(398, 405), 24, accent);
        Center("01", new(360, 458, 76, 24), 16, accent);
        Center(owned ? "跨局保留" : "未达成", new(350, 492, 96, 24), 11, UiTheme.Muted);

        Text(definition.S("name"), new(475, 370), 22, UiTheme.Ink);
        Box(new(997, 349, 97, 25), new(owned ? "23453b" : "20323c"), Colors.Transparent, 5);
        Center(owned ? "已自动生效" : _pendingDefeatAchievement ? "待保存" : "等待达成", new(997, 349, 97, 25), 10, owned ? UiTheme.Mint : UiTheme.Muted);
        Text("达成条件 · " + definition.S("condition"), new(475, 399), 12, UiTheme.Muted);
        Box(new(475, 417, 619, 119), new("0d202b"), new("2c444e"), 7);
        Text("永久增益", new(491, 440), 11, accent);
        Text($"每项护盾容量科技，额外增加 {bonus} 座建造上限", new(491, 465), 16, UiTheme.Ink);
        Text("首次解锁", new(491, 512), 12, UiTheme.Muted);
        Text("1", new(571, 515), 24, UiTheme.Ink, true);
        Text("+", new(600, 512), 17, UiTheme.Dim);
        Box(new(625, 488, 97, 35), new(owned ? "224c3f" : "1b3640"), Colors.Transparent, 5);
        Center($"{bonus}  成就加成", new(625, 488, 97, 35), 12, accent);
        Text("=", new(739, 512), 17, UiTheme.Dim);
        Text($"{1 + bonus} 座", new(770, 515), 23, accent);
        Text($"后续容量科技同样 +{bonus}", new(893, 510), 11, UiTheme.Muted);

        string description = owned
            ? "永久加成已生效 · 重新部署与重试本波均保留，无需领取或装备。"
            : "达成后自动生效 · 奖励跨局保留，无需领取或装备。";
        Text(description, new(324, 592), 12, UiTheme.Muted);
        if (_achievementSaveError.Length > 0)
        {
            Text(_achievementSaveError, new(324, 616), 11, UiTheme.Coral);
            Button(new(324, 632, 131, 32), "重试保存", "modal:achievements:retry");
        }
        else
        {
            Text("护盾的三波建造冷却保持不变；强度与修复科技不增加建造数量。", new(324, 615), 11, UiTheme.Dim);
            HudIcon("shield", new(332, 646), 7, accent);
            string capacity = !owned ? "局部护盾工程仍需在科技树研究解锁"
                : !Game.HasResearch("D_N4") ? $"研究「区域护盾工程」后可建 {1 + bonus} 座"
                : $"本局护盾建造上限 {Game.LocalShieldBuildLimit} 座 · 其中成就贡献 +{Game.LocalShieldAchievementBonus}";
            Text(capacity, new(348, 650), 11, accent);
        }
        Button(new(937, 632, 157, 32), Defeated ? "返回战败结算" : "返回游戏", "modal:achievements:close", true);
    }
}
