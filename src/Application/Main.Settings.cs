using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using Earthward.Domain;
using Earthward.Presentation;
namespace Earthward.Application;

public partial class Main
{
    public static readonly string[] CombatKeys = { "enemy_health", "drone_damage", "laser_damage", "missile_damage", "factory_spawn_interval", "aircraft_scale", "factory_capacity_multiplier", "interceptor_factory_capacity", "laser_factory_capacity", "missile_factory_capacity", "enemy_count_multiplier", "enemy_aircraft_scale", "enemy_bullet_base_count", "enemy_bullet_growth_interval", "enemy_bullet_growth_step", "enemy_bullet_max_count", "enemy_bullet_speed", "enemy_bombard_speed", "enemy_speed_multiplier", "resource_core_drop_count", "resource_output_multiplier", "enemy_wave_duration", "enemy_wave_base_count", "enemy_wave_growth", "first_wave_enemy_count_multiplier", "first_wave_enemy_health_multiplier", "first_wave_enemy_damage_multiplier", "atmosphere_hit_full_percent", "atmosphere_hit_max_strength", "atmosphere_hit_lerp_speed", "resource_core_upgrade_percent", "factory_starting_capacity_factor", "drone_base_damage_multiplier", "drone_base_health_multiplier", "frontier_radius_1", "frontier_radius_2", "frontier_radius_3", "medium_boss_first_wave", "medium_boss_interval", "enemy_spawn_duration", "enemy_health_growth", "enemy_damage_growth", "patrol_coverage_multiplier" };

    private static readonly Dictionary<string, int[]> CombatPages = new() { ["base"] = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 42 }, ["barrage"] = new[] { 12, 13, 14, 15, 16, 17 }, ["strategy"] = new[] { 18, 19, 20, 30 }, ["wave"] = new[] { 21, 39, 22, 23, 10, 24, 25, 26 }, ["feedback"] = new[] { 27, 28, 29 }, ["incremental"] = new[] { 31, 32, 33, 34, 35, 36, 37, 38 }, ["curve"] = new[] { 40, 41 }, ["cheat"] = Array.Empty<int>() };
    private static readonly string[] CombatLabels = { "敌方基础血量", "拦截机伤害基数", "激光机伤害基数", "导弹机伤害基数", "工厂补造间隔（秒）", "我方飞机尺寸倍率", "工厂容量倍率", "拦截工厂容量基数", "激光工厂容量基数", "导弹工厂容量基数", "敌方波次数量倍率", "敌方单位尺寸倍率", "初始每轮弹数", "每次增长间隔（秒）", "每次增加弹数", "每轮弹数上限", "防空子弹速度", "对地轰炸弹速度", "敌方移动速度倍率", "小 Boss 核心掉落数量", "资源建筑产量系数", "每波固定周期（秒）", "敌军数量基数", "每波增加敌军数", "\u9996\u6ce2\u654c\u673a\u6570\u91cf\u500d\u7387", "\u9996\u6ce2\u654c\u673a\u751f\u547d\u500d\u7387", "\u9996\u6ce2\u654c\u673a\u4f24\u5bb3\u500d\u7387", "受击满幅阈值（%）", "大气最大红色强度", "受击强度过渡速度", "核心强化每级产能（%）", "开局编制系数", "战机基础伤害系数", "战机基础耐久系数", "首次外推半径", "第二次外推半径", "第三次外推半径", "首个中 Boss 波次", "中 Boss 周期波数", "每波生成窗口（秒）", "敌军耐久每波增幅（小数）", "敌军伤害每波增幅（小数）", "巡逻覆盖倍率" };

    public static string FormatSetting(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    private string CombatPageFor(int index) => CombatPages.First(pair => pair.Value.Contains(index)).Key;

    private Vector2 CombatFieldPosition(int index)
    {
        string page = CombatPageFor(index);
        int local = Array.IndexOf(CombatPages[page], index), rows = (CombatPages[page].Length + 1) / 2;
        float gap = page == "base" ? 62 : page is "wave" or "incremental" ? 64 : 86;
        return new(514 + (local / rows) * 550, 291 + (local % rows) * gap);
    }

    private void CreateCombatControls()
    {
        _combatControls = new Control { Name = "CombatParameterInputs", MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_combatControls);
        for (int i = 0; i < CombatKeys.Length; i++)
        {
            var field = new LineEdit { Name = CombatKeys[i], Position = CombatFieldPosition(i), Size = new(160, 38), Alignment = HorizontalAlignment.Right, MaxLength = 40, SelectAllOnFocus = true };
            if (CombatKeys[i] == "patrol_coverage_multiplier")
                field.TooltipText = "同时扩大基础巡逻半径与远端范围，默认 2；科技与特性继续叠加。不会改变武器射程、速度或固定作战航程。";
            field.AddThemeFontOverride("font", UiTheme.Font);
            field.AddThemeFontSizeOverride("font_size", 18);
            field.AddThemeColorOverride("font_color", UiTheme.Ink);
            field.AddThemeStyleboxOverride("normal", UiTheme.Panel(new Color("0b1923"), new Color("496875"), 5));
            _combatControls.AddChild(field);
            CombatFields[CombatKeys[i]] = field;
        }
        CreateCheatControl();
        _combatControls.Hide();
    }

    public void OpenCombatSettings()
    {
        if (_combatControls == null)
            return;
        CancelBuildSelection();
        CancelResourceUpgrade();
        Modal = "combat";
        _settingsNotice = "全部字段验证通过后一次应用；当前输入尚未生效";
        _settingsError = false;
        foreach (string key in CombatKeys)
            CombatFields[key].Text = FormatSetting(Game.CombatSettings.N(key));
        SetCombatPage(_combatPage);
        _combatControls.Show();
    }

    public void CloseCombatSettings(bool clearModal = true)
    {
        if (IsInstanceValid(CheatAmountField))
        {
            CheatAmountField.ReleaseFocus();
            CheatAmountField.Hide();
        }
        foreach (var field in CombatFields.Values)
        {
            field.ReleaseFocus();
            field.Hide();
        }
        if (_combatControls != null)
            _combatControls.Hide();
        if (clearModal)
            Modal = "";
    }

    private void SetCombatPage(string page)
    {
        if (!CombatPages.ContainsKey(page))
            return;
        _combatPage = page;
        CheatAmountField.Visible = page == "cheat";
        if (page != "cheat") CheatAmountField.ReleaseFocus();
        for (int i = 0; i < CombatKeys.Length; i++)
        {
            var field = CombatFields[CombatKeys[i]];
            field.Visible = CombatPageFor(i) == page;
            field.Position = CombatFieldPosition(i);
        }
        QueueRedraw();
    }

    public bool ApplyCombatSettings()
    {
        var candidate = new DataMap();
        for (int i = 0; i < CombatKeys.Length; i++)
        {
            string key = CombatKeys[i];
            if (!double.TryParse(CombatFields[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value) || !Game.CombatSettingInRange(key, value))
            {
                SetCombatPage(CombatPageFor(i));
                CombatFields[key].GrabFocus();
                _settingsNotice = CombatLabels[i] + "：请输入范围内的有效数值";
                _settingsError = true;
                return false;
            }
            candidate[key] = value;
        }
        if (Game.ValidatedCombatSettings(candidate) == null)
        {
            _settingsNotice = "参数组合无效：周期不能小于生成窗口，整数参数必须是整数";
            _settingsError = true;
            return false;
        }
        Game.ApplyCombatSettings(candidate);
        Battle.RefreshEnemyHealth();
        SaveCombatPreferences();
        SaveIfSafe();
        Sounds.PlaySound("research");
        _settingsNotice = "配置已应用 · 已开始的波次继续原计划，下一波采用新配置";
        _settingsError = false;
        return true;
    }

    private void LoadCombatPreferences()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(SettingsPath);
            if (!System.IO.File.Exists(path))
                return;
            var raw = DataMap.Parse(System.IO.File.ReadAllText(path));
            var validated = Game.ReadCombatPreferences(raw);
            if (validated != null)
                Game.ApplyCombatSettings(validated);
        }
        catch (Exception e) { GD.PushWarning("Combat preferences: " + e.Message); }
    }

    private void SaveCombatPreferences()
    {
        try
        {
            AtomicWrite(ProjectSettings.GlobalizePath(SettingsPath), Game.SerializeCombatPreferences().ToJson(true), raw => Game.ReadCombatPreferences(DataMap.Parse(raw)) != null);
        }
        catch (Exception e) { ShowNotice("参数保存失败：" + e.Message); }
    }

    private void DrawCombatSettings()
    {
        Box(new(150, 116, 1140, 680), new("10212e"), new("3a5662"), 16);
        Text("防御工程参数", new(182, 160), 28, UiTheme.Ink);
        Text("编辑时暂停战斗 · 基数、倍率与成长节奏独立调节", new(184, 188), 13, UiTheme.Muted);
        Button(new(1210, 139, 45, 37), "×", "modal:close");
        string[] names = { "基础战斗", "敌军弹幕", "推进战利品", "波次增援", "受击反馈", "增量成长", "敌军曲线", "资源作弊" };
        float width = (1074 - 6 * (CombatPages.Count - 1)) / (float)CombatPages.Count;
        int t = 0;
        foreach (string page in CombatPages.Keys)
        {
            Button(new(182 + t * (width + 6), 207, width, 35), names[t], "modal:combat:page:" + page, _combatPage == page, true, "tab");
            t++;
        }
        if (_combatPage == "cheat")
        {
            DrawResourceCheats();
            return;
        }
        string help = _combatPage switch
        {
            "wave" => "每波开始时锁定时长与敌数 · 新参数在下一波生效",
            "barrage" => "按有效防御时间成长 · 暂停不累计 · 下一轮射击使用新参数",
            "feedback" => "按护盾与耐久的实际损失反馈 · 三项参数实时生效",
            "incremental" => "开局基数与永久成长分开 · 外推距离在下一批母舰开始时锁定",
            "curve" => "0.035 表示每波耐久增长 3.5% · 数量、耐久和伤害分别调节",
            _ => "基数和系数独立配置 · 修改后点击应用"
        };
        Text(help, new(184, 267), 12, UiTheme.Muted);
        for (int i = 0; i < CombatKeys.Length; i++)
        {
            if (CombatPageFor(i) != _combatPage)
                continue;
            var p = CombatFieldPosition(i) - new Vector2(332, 9);
            Box(new(p, new(510, _combatPage == "base" ? 57 : 65)), new("142935"), UiTheme.Line, 8);
            Text(CombatLabels[i], p + new Vector2(15, 25), 14, UiTheme.Ink);
            var bounds = Game.CombatSettingBounds(CombatKeys[i]);
            Text($"范围 {FormatSetting(bounds.X)} — {FormatSetting(bounds.Y)}", p + new Vector2(15, 46), 10, UiTheme.Dim);
        }
        if (_combatPage == "wave")
        {
            var plan = Battle.GetWaveSpawnPlan();
            var next = Battle.PreviewNextWaveSpawnPlan();
            SettingsInfo(498, new[] { "固定时间增援 · 波次开始后保持当前计划", $"当前第 {Game.Wave} 波：已派遣 {UiTheme.Number(plan.N("spawned"))} / {UiTheme.Number(plan.N("planned_count"))} · {plan.N("elapsed"):0.0} / {plan.N("duration"):0.0} 秒", $"下一波：{next.L("wave")} 波 · {UiTheme.Number(next.N("planned_count"))} 敌军 · {FormatSetting(next.N("duration"))} 秒", "Boss 与孵化子机计入总配额；周期不能小于生成窗口。" });
        }
        else if (_combatPage == "strategy")
            SettingsInfo(455, new[] { "资源建设与高级科技", "每波小 Boss 掉资源核心；资源设施每座消耗 1 核心。", "资源核心强化设施；外星科技点用于研究外星科技。", $"资源核心 {Game.ResourceCores} · 外星点 {Game.AlienPoints} · 外星芯片 {Game.FactoryPerks.AlienChips}" });
        else if (_combatPage == "feedback")
            SettingsInfo(455, new[] { "伤害反馈使用实际受伤比例", "受伤比例 = 实际耐久与护盾损失 / (最大耐久 + 护盾上限)", "目标强度 = 最大红色强度 × clamp(受伤比例 / 满幅阈值, 0, 1)", "过渡速度控制接近目标的快慢；无新伤害时恢复正常大气。" });
        else if (_combatPage == "barrage")
            SettingsInfo(543, new[] { $"当前生效：每轮 {Game.EnemyBulletCount()} 发 · 已防御 {Game.DefenseTime:0} 秒", "每轮弹数 = 初始弹数 + 已过增长周期 × 每次增量，受上限约束。", "存档保留成长时间，调整不会改变已经在飞行的子弹。" });
        else if (_combatPage == "incremental")
            SettingsInfo(555, new[] { "三次拉远 → 三项边界航程研究 → 外层持续防御", "普通敌机低概率回收外星芯片，用于购买与永久升级特性。" });
        Text(UiTheme.Fit(_settingsNotice, 1060, 12), new(184, 677), 12, _settingsError ? UiTheme.Coral : UiTheme.Mint);
        Text($"当前单厂编制点：动能 {Game.FactoryCapacity("interceptor")} / 激光 {Game.FactoryCapacity("laser")} / 导弹 {Game.FactoryCapacity("missile")}", new(184, 705), 12, UiTheme.Cyan);
        Button(new(182, 736, 180, 39), "恢复默认数值", "modal:combat:defaults");
        Button(new(1044, 736, 212, 39), "应用配置 →", "modal:combat:apply", true, true, "primary");
    }

    private void SettingsInfo(float y, string[] lines)
    {
        Box(new(182, y, 1074, Math.Min(649 - y, lines.Length * 30 + 23)), new("11242e"), UiTheme.Line, 8);
        for (int i = 0; i < lines.Length; i++)
            Text(lines[i], new(200, y + 28 + i * 29), i == 0 ? 16 : 12, i == 0 ? UiTheme.Mint : UiTheme.Muted);
    }
}
