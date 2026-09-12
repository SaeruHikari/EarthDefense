using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using Earthward.Domain;
using Earthward.Presentation;
namespace Earthward.Application;

public partial class Main
{

    public override void _Draw()
    {
        if (Game == null || Planet == null || Battle == null)
            return;
        long drawStarted = CaptureFrameTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        Buttons.Clear();
        UiRects.Clear();
        _hudItems.Clear();
        _tooltips.Clear();
        SetUiOffset(Vector2.Zero);
        DrawHeader();
        DrawOrbitLabels();
        SetUiOffset(new(WorldSize.X - DesignSize.X, 0));
        DrawRight();
        SetUiOffset(new((WorldSize.X - DesignSize.X) * .5f, WorldSize.Y - DesignSize.Y));
        DrawCommands();
        SetUiOffset(WorldSize - DesignSize);
        if (!IsSpectating())
            DrawSolarNavigation();
        if (Modal != "")
        {
            SetUiOffset(Vector2.Zero);
            DrawRect(new(Vector2.Zero, WorldSize), new(.02f, .04f, .07f, .78f));
            SetUiOffset((WorldSize - DesignSize) * .5f);
            DrawModal();
            if (Modal is "defeat" or "achievements")
            {
                SetUiOffset(Vector2.Zero);
                DrawAchievementHeaderButton();
            }
        }
        else if (UserPaused)
        {
            SetUiOffset(new((WorldSize.X - DesignSize.X) * .5f, 0));
            FloatingPanel(new(595, 52, 250, 30));
            Center("II  已暂停 · 空格继续", new(595, 52, 250, 30), 14, UiTheme.Mint);
        }
        SetUiOffset(Vector2.Zero);
        if (Modal == "")
        {
            DrawFrontierWarning();
            DrawFactoryCoverageHud();
            DrawShieldCoverageHud();
            DrawSatelliteLauncherHud();
            DrawHudTooltip();
            DrawResourceUpgradeCursor();
            DrawAircraftHover();
            DrawPlayTime();
        }
        if (CaptureFrameTimings) LastHudDrawMs = FrameElapsed(drawStarted);
    }

    private void SetUiOffset(Vector2 offset)
    {
        _uiOffset = offset;
        DrawSetTransform(offset);
    }

    private void Text(string text, Vector2 at, int size, Color color, bool display = false) => DrawString(display ? UiTheme.Display : UiTheme.Font, at, text, HorizontalAlignment.Left, -1, size, color);

    private void Center(string text, Rect2 rect, int size, Color color)
    {
        string fitted = HudFitText(text, Math.Max(1, rect.Size.X - 8), size);
        Vector2 at = rect.Position + new Vector2((rect.Size.X - HudTextWidth(fitted, size)) * .5f, (rect.Size.Y + size * .72f) * .5f);
        DrawString(UiTheme.Font, at, fitted, HorizontalAlignment.Left, -1, size, color);
    }

    private void Box(Rect2 rect, Color fill, Color border = default, int radius = 8)
    {
        string key = fill.ToHtml() + border.ToHtml() + radius;
        if (!_boxStyles.TryGetValue(key, out var style))
        {
            style = UiTheme.Panel(fill, border, radius);
            style.SetBorderWidthAll(border.A > 0 ? 1 : 0);
            if (_boxStyles.Count > 2048)
                _boxStyles.Clear();
            _boxStyles[key] = style;
        }
        DrawStyleBox(style, rect);
    }

    private void FloatingPanel(Rect2 rect, float opacity = .78f)
    {
        Box(new(rect.Position + new Vector2(0, 3), rect.Size), new(0, 0, 0, opacity * .18f), Colors.Transparent, 12);
        Box(rect, new(.035f, .075f, .105f, opacity), new(.35f, .53f, .59f, .27f), 11);
        UiRects.Add(new(rect.Position + _uiOffset, rect.Size));
    }

    private void RegisterButton(Rect2 rect, string action) => Buttons.Add(new() { ["rect"] = new Rect2(rect.Position + _uiOffset, rect.Size), ["action"] = action });

    private void Button(Rect2 rect, string label, string action, bool selected = false, bool enabled = true, string style = "quiet")
    {
        bool hover = rect.HasPoint(Mouse - _uiOffset);
        Color fill = new(selected ? "172c37" : "162632"), border = selected ? new("51736f") : UiTheme.Line, color = selected ? UiTheme.Mint : UiTheme.Ink;
        if (style == "primary")
        {
            fill = hover ? new("bcf2df") : UiTheme.Mint;
            border = fill;
            color = new("143128");
        }
        else if (style == "tab")
        {
            fill = new(selected ? "234139" : "101d2b");
            border = new(selected ? "345b4d" : "101d2b");
            color = selected ? UiTheme.Mint : UiTheme.Muted;
        }
        else if (hover)
        {
            fill = new("203743");
            border = new("4c666f");
        }
        if (!enabled)
            color = UiTheme.Dim;
        if (style == "quiet")
        {
            fill.A = hover || selected ? .86f : .72f;
            border.A = .62f;
        }
        Box(rect, fill, border, 7);
        Center(label, rect, style == "tab" ? 13 : 14, color);
        RegisterButton(rect, action);
    }

    private void Hint(Rect2 rect, string text) => _tooltips.Add(new() { ["rect"] = new Rect2(rect.Position + _uiOffset, rect.Size), ["text"] = text });

    private void HeaderButton(Rect2 rect, string icon, string action, string hint, bool selected = false)
    {
        Button(rect, "", action, selected);
        HudIcon(icon, rect.GetCenter(), 9, selected ? UiTheme.Mint : UiTheme.Ink);
        Hint(rect, hint);
    }

    public static string BuildingName(string kind) => kind switch { "mine" => "采矿站", "solar" => "太阳能阵列", "interceptor" => "动能战机工厂", "laser" => "激光战机工厂", "missile" => "导弹战机工厂", "satellite_launcher" => "卫星发射中心", "shield" => "局部护盾发生器", _ => kind };

    public static string CostText(DataMap cost)
    {
        var words = new List<string>();
        foreach (var pair in new[] { ("minerals", "矿"), ("energy", "能"), ("science", "科研"), ("resource_cores", "核心"), ("alien_points", "外星") })
            if (cost.N(pair.Item1) > 0)
                words.Add(pair.Item2 + " " + UiTheme.Number(cost.N(pair.Item1)));
        return words.Count > 0 ? string.Join(" · ", words) : "免费";
    }

    private static string Rate(double n) => n > 0 ? "+" + (n < 100 ? n.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : UiTheme.Number(n)) + "/s" : "—";

    private List<DataMap> BuildHeaderData()
    {
        var result = new List<DataMap>();
        var rates = Game.Rates();
        double collecting = Started && !WorldIsPaused() ? Speed : 0;
        void Add(string id, string icon, string value, string gain, Color color, string tip) => result.Add(new()
        {
            ["id"] = id,
            ["icon"] = icon,
            ["value"] = value,
            ["gain"] = gain,
            ["color"] = color,
            ["tooltip"] = tip
        });
        foreach (var row in new[] { ("minerals", "矿物", Game.Minerals, UiTheme.Mint), ("energy", "能量", Game.Energy, UiTheme.Amber), ("science", "科研", Game.Science, UiTheme.Cyan) })
        {
            double rate = rates.N(row.Item1) * collecting;
            string source = row.Item1 == "science" ? Game.ResearchSatelliteDeployed ? "科研卫星" : "科研卫星（待发射）" : row.Item2;
            Add(row.Item1, row.Item1, UiTheme.Number(row.Item3), Rate(rate), row.Item4, $"{source} · 当前 {UiTheme.Number(row.Item3)} · 实际收入 {FormatSetting(rate)} / 秒");
        }
        Add("resource_cores", "core", UiTheme.Number(Game.ResourceCores), "—", UiTheme.Amber, "资源核心 · 击败小型 Boss 获得，用于建设及强化资源设施");
        Add("alien_points", "alien", UiTheme.Number(Game.AlienPoints), "—", UiTheme.Alien, "外星科技点 · 研究外星分化中科技、大科技与边界航程");
        var fleet = Battle.FleetCapacityState();
        int active = Battle.GetActiveDroneCount();
        Add("fleet", "interceptor", UiTheme.Number(active), fleet.N("pending") > 0 ? "+" + UiTheme.Number(fleet.N("pending")) : "—", UiTheme.Mint, $"地球防御机群 · 在役 {active} 架 · 编制点 {fleet.L("used_points")} / {fleet.L("capacity_points")} · 待补造 {fleet.L("pending")} 架；重型机占 2 点");
        string waveGain = "—", waveTip = "波次 · 尚未开始防御";
        if (Campaign.OwnsEarthSchedule())
        {
            waveGain = _campaignStatus.S("earth_phase") == "ceasefire" ? "休" + _campaignStatus.L("ceasefire_rounds_remaining") : "外" + Game.DefenseReachStage;
            waveTip = $"边界第 {Game.DefenseReachStage} 层 · 休整剩余 {_campaignStatus.N("ceasefire_remaining"):0} 秒 · 下一批 {_campaignStatus.L("earth_next_carriers")} 艘母舰";
        }
        else if (Battle.WaveRunning)
        {
            var plan = Battle.GetWaveSpawnPlan();
            double remain = Math.Max(0, plan.N("cycle_duration", Game.CombatSettings.N("enemy_wave_duration")) - plan.N("cycle_elapsed", plan.N("elapsed")));
            waveGain = $"{Math.Ceiling(remain)}s";
            waveTip = $"第 {Game.Wave} 波 · 下波剩余 {Math.Ceiling(remain)} 秒 · 已派遣 {plan.L("spawned")} / {plan.L("planned_count")} · 残敌不延长周期";
        }
        Add("wave", "wave", Game.Wave.ToString("00"), waveGain, Battle.WaveRunning ? UiTheme.Coral : UiTheme.Amber, waveTip);
        if (Started && !Defeated)
        {
            if (_forecast.Count == 0 || Elapsed - _forecastClock >= .25)
            {
                _forecast = DescribeForecast(Battle.PreviewNextWaveSpawnPlan());
                _forecastClock = Elapsed;
            }
            if (_forecast.L("wave") > 0)
            {
                Add("forecast", "eye", "", "", UiTheme.Muted, _forecast.S("tooltip"));
                result[^1]["armor"] = _forecast.Map("armor");
            }
        }
        double earthRepair = Started && !WorldIsPaused() ? Game.LocalShieldEarthRepairRate() * Speed : 0;
        Add("earth_hp", "earth", $"{(int)Game.EarthHp}%", Rate(earthRepair), Game.EarthHp > 45 ? UiTheme.Mint : UiTheme.Coral, "地球耐久 · 局部护盾研究可提供极慢修复，也可在建筑卡片查看覆盖范围");
        var shields = Battle.GetLocalShieldSummary();
        Add("shield", "shield", UiTheme.Number(shields.N("hp")), "/" + UiTheme.Number(shields.N("capacity")), UiTheme.Cyan, $"局部护盾 · {shields.I("active")} / {shields.I("count")} 座在线 · 科技上限 {shields.I("build_limit")} · 当前 {UiTheme.Number(shields.N("hp"))} / {UiTheme.Number(shields.N("capacity"))}\n{(shields.L("build_cooldown_remaining") > 0 ? $"建造冷却还需 {shields.L("build_cooldown_remaining")} 波 · " : "")}只吸收护盾建筑覆盖范围内的攻击，范围外由地球承受伤害。");
        return result;
    }

    private static DataMap DescribeForecast(DataMap plan)
    {
        var composition = plan.Map("composition");
        long light = 0, heavy = 0, energy = 0;
        var entries = new List<string>();
        var names = new Dictionary<string, string> { { "claw", "裂爪" }, { "needle", "针翼" }, { "rock", "岩甲" }, { "siege", "攻城" }, { "prism", "棱镜" }, { "weaver", "织幕" }, { "hatcher", "孵化" }, { "jammer", "干扰" } };
        foreach (var pair in composition)
        {
            long n = composition.L(pair.Key);
            if (n == 0)
                continue;
            if (pair.Key is "rock" or "siege" or "hatcher")
                heavy += n;
            else if (pair.Key is "prism" or "weaver")
                energy += n;
            else
                light += n;
            entries.Add(names.GetValueOrDefault(pair.Key, pair.Key) + " " + n);
        }
        return new()
        {
            ["wave"] = plan.L("wave"),
            ["armor"] = new DataMap { ["light"] = light, ["heavy"] = heavy, ["energy"] = energy },
            ["tooltip"] = $"下波预告 · 第 {plan.L("wave")} 波 · 共 {plan.L("planned_count")} 架\n" + string.Join(" · ", entries) + "\n动能克轻甲，对能量层无效；导弹克重甲；激光优先拆能量层。"
        };
    }

    private void DrawHeader()
    {
        var items = HeaderData();
        var widths = _headerWidths;
        float factor = Math.Min(1, Math.Max(1, WorldSize.X - 204) / _headerTotalWidth);
        Vector2 origin = new(16, 14);
        DrawSetTransform(origin, 0, Vector2.One * factor);
        float x = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            Rect2 rect = new(x, 0, widths[i], 34);
            Color color = item.Get<Color>("color");
            Box(rect, new(.035f, .075f, .105f, .72f), new(.35f, .53f, .59f, .21f), 6);
            HudIcon(item.S("icon"), new(x + 15, 17), 8, color);
            if (item.S("id") == "forecast")
            {
                string[] ids = { "light", "heavy", "energy" };
                Color[] colors = { UiTheme.Cyan, UiTheme.Amber, UiTheme.Alien };
                for (int a = 0; a < 3; a++)
                    HudIcon("armor_" + ids[a], new(x + 37 + a * 20, 17), 6, item.Map("armor").L(ids[a]) > 0 ? colors[a] : UiTheme.Dim);
            }
            Text(item.S("value"), new(x + 29, 22), 15, UiTheme.Ink);
            float w = _headerValueWidths[i];
            Text(item.S("gain"), new(x + 34 + w, 21), 10, item.S("gain") == "—" ? UiTheme.Dim : color);
            var screen = new Rect2(origin + rect.Position * factor, rect.Size * factor);
            item["rect"] = screen;
            _hudItems.Add(item);
            UiRects.Add(screen);
            _tooltips.Add(new()
            {
                ["rect"] = screen,
                ["text"] = item.S("tooltip")
            });
            x += widths[i] + 4;
        }
        SetUiOffset(Vector2.Zero);
        DrawAchievementHeaderButton();
        float left = WorldSize.X - 130;
        HeaderButton(new(left, 14, 34, 34), "settings", "combat", "战斗参数 · C");
        HeaderButton(new(left + 40, 14, 34, 34), Sounds.Muted ? "muted" : "audio", "audio", Sounds.Muted ? "开启音效" : "关闭音效");
        HeaderButton(new(left + 80, 14, 34, 34), UserPaused ? "play" : "pause", "pause", "全局暂停 / 继续 · 空格", UserPaused);
    }

    private void DrawOrbitLabels()
    {
        if (IsSpectating())
            return;
        SetUiOffset(new(0, WorldSize.Y - 900));
        FloatingPanel(new(18, 820, 244, 47));
        Text("拖动环绕 · 滚轮缩放 · R 复位", new(32, 840), 11, UiTheme.Muted);
        var heading = Planet.GetCameraHeading();
        Text($"视角 {Mathf.PosMod(Mathf.RadToDeg(heading.X), 360):000}° · {Planet.DefaultCameraDistance / Planet.GetCameraDistance():0.0}× · {Engine.GetFramesPerSecond()} FPS", new(32, 857), 10, UiTheme.Dim);
        if (NoticeTime > 0 && Modal == "" && !IsObserving())
        {
            SetUiOffset(new((WorldSize.X - 1440) * .5f, WorldSize.Y - 900));
            FloatingPanel(new(390, 785, 660, 29), (float)Math.Min(NoticeTime, 1) * .78f);
            Center(HudFitText(Notice, 640, 11), new(400, 785, 640, 29), 11, UiTheme.Mint);
        }
    }

    private void DrawRight()
    {
        if (ResearchSidebarPresent())
        {
            SetUiOffset(Vector2.Zero);
            var r = ResearchCurrentRect();
            Box(r, new("13232d"), new("62777e"), 5);
            UiRects.Add(r);
            Box(new(r.Position + new Vector2(1, 4), new(7, r.Size.Y - 8)), new("30464e"), new("657b7e"), 3);
            Text("防御指挥 · 科技", r.Position + new Vector2(18, 29), 16, UiTheme.Ink);
            Button(new(r.End.X - 44, r.Position.Y + 7, 30, 28), "−", "dock:close");
            DrawCommandTabs(new(r.Position + new Vector2(12, 48), new(r.Size.X - 24, 35)));
            return;
        }
        var rail = new Rect2(1122, 87, 300, DockOpen && !IsSpectating() ? (Tab == "perks" ? 600 : 693) : 42);
        Box(rail, new("1b2529"), new("667477"), 3);
        UiRects.Add(new(rail.Position + _uiOffset, rail.Size));
        DrawLine(new(1127, 91), new(1417, 91), new("889493"));
        DrawLine(new(1127, 125), new(1417, 125), new("080f14"), 2);
        DrawCircle(new(1129, 94), 2, new("a3ada7"));
        DrawCircle(new(1415, 94), 2, new("a3ada7"));
        Text("防御指挥", new(1141, 113), 16, UiTheme.Ink);
        HeaderButton(new(1308, 94, 30, 26), "core_upgrade", "tool:resource_upgrade", "核心强化 · 每次消耗 1 核心提升一座资源设施产能", ResourceUpgradeMode);
        HeaderButton(new(1346, 94, 30, 26), "eye", "spectator:toggle", "战机观战 · V 进入 / 切换 · Esc 退出", IsSpectating());
        Button(new(1384, 94, 27, 26), DockOpen ? "−" : "+", DockOpen ? "dock:close" : "tab:build");
        if (!DockOpen || IsSpectating())
            return;
        DrawCommandTabs(new(1134, 139, 276, 36));
        if (Tab == "build")
            DrawBuildings();
    }

    private void DrawCommandTabs(Rect2 rect)
    {
        string[] ids = { "build", "tech", "perks" }, names = { "建设", "科技", "特性" };
        float width = (rect.Size.X - 16) / 3;
        for (int i = 0; i < 3; i++)
        {
            var tab = new Rect2(rect.Position + new Vector2(i * (width + 8), 0), new(width, rect.Size.Y));
            Button(tab, names[i], "tab:" + ids[i], Tab == ids[i], true, "tab");
            if (ids[i] == "tech" && LocalShieldGuidePending && Tab != "tech")
            {
                float pulse = .55f + .35f * MathF.Sin((float)Elapsed * 4.5f);
                DrawStyleBox(UiTheme.Panel(new Color(0, 0, 0, 0), UiTheme.Alpha(UiTheme.Amber, pulse), 7, 0), tab.Grow(2));
                Text("建议", tab.Position + new Vector2(tab.Size.X - 36, -4), 10, UiTheme.Amber);
            }
        }
    }

    private void DrawBuildings()
    {
        Text("生产建设", new(1137, 200), 13, UiTheme.Amber);
        Text("设施 " + Game.FacilityCount(), new(1332, 200), 11, UiTheme.Muted);
        string[] ids = { "satellite_launcher", "mine", "solar", "interceptor", "missile", "laser" };
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            var rect = new Rect2(1134 + i % 2 * 143, 215 + i / 2 * 126, 132, 118);
            var entry = BuildingHud(id);
            bool locked = entry.LockReason.Length > 0, selected = SelectedBuild == id;
            bool guide = id == "satellite_launcher" && !Game.HasSatelliteLauncher && !Game.ResearchSatelliteDeployed;
            Color border = selected ? UiTheme.Amber : guide ? UiTheme.Alien : new("3b4b4e");
            Box(rect, new("0d171d"), border, 2);
            if (_buildIcons.TryGetValue(id, out var icon))
                DrawTextureRect(icon, new(rect.Position + new Vector2(14, 0), new(104, 78)), false, new Color(1, 1, 1, locked ? .35f : 1));
            else
                HudIcon(id, rect.Position + new Vector2(66, 36), 24, UiTheme.Mint);
            Text(Game.Buildings.L(id).ToString("00"), rect.Position + new Vector2(8, 76), 10, UiTheme.Mint);
            if (selected)
            {
                DrawRect(new(rect.Position + new Vector2(2, 2), new(128, 3)), UiTheme.Amber);
                Text(id == "satellite_launcher" ? "七格" : "连建", rect.Position + new Vector2(96, 76), 10, UiTheme.Amber);
            }
            else if (guide)
            {
                float pulse = .62f + .25f * MathF.Sin((float)Elapsed * 4.2f);
                DrawRect(new(rect.Position + new Vector2(2, 2), new(128, 3)), UiTheme.Alpha(UiTheme.Alien, pulse));
                Text("开局引导", rect.Position + new Vector2(72, 76), 10, UiTheme.Alien);
            }
            Text(HudFitText(BuildingName(id), 120, 12), rect.Position + new Vector2(8, 94), 12, locked ? UiTheme.Muted : UiTheme.Ink);
            string costLabel = id == "satellite_launcher" ? (Game.HasSatelliteLauncher ? "已建造 · 点击设施发射" : "免费 · 七格蜂窝 · 限一座") : locked ? "研究解锁" : entry.CostText;
            bool affordable = CanBuildFromHud(entry) || id == "satellite_launcher" && Game.HasSatelliteLauncher;
            Text(HudFitText(costLabel, 118, 10), rect.Position + new Vector2(8, 112), 10, locked ? UiTheme.Dim : affordable ? UiTheme.Mint : UiTheme.Amber);
            RegisterButton(rect, "build:" + id);
            if (locked) Hint(rect, entry.LockReason);
            else if (guide) Hint(rect, "开局引导 · 建造一次卫星发射中心，随后点击地表设施发射免费的科研卫星");
        }
        DrawShieldBuildingCard(new Rect2(1134, 599, 276, 63));
        Box(new(1134, 669, 276, 54), new("122128"), new("45665c"), 3);
        Text("工厂 / 战机特性", new(1147, 691), 14, UiTheme.Mint);
        Text("两类双槽 · 能源核心升级 · 跨局继承", new(1147, 712), 10, UiTheme.Muted);
        RegisterButton(new(1134, 669, 276, 54), "tab:perks");
        if (Game.DefenseReachStage > 0)
        {
            var frontier = _campaignStatus.Map("frontier");
            Box(new(1134, 731, 276, 46), new("152a34"), new("486d80"), 3);
            Text($"边界 {Game.DefenseReachStage} / 3 · 距离 {Game.CombatSettings.N("frontier_radius_" + Game.DefenseReachStage):0}", new(1147, 750), 12, UiTheme.Cyan);
            Text($"母舰 {frontier.L("alive") + frontier.L("pending")} · 配套航程研究 →", new(1147, 767), 10, UiTheme.Mint);
            RegisterButton(new(1134, 731, 276, 46), "research:frontier");
        }
        else
            Text("左键划过地表连建 · 右键退出", new(1147, 751), 11, UiTheme.Muted);
    }

    private void DrawShieldBuildingCard(Rect2 rect)
    {
        const string id = "shield";
        var entry = BuildingHud(id);
        bool locked = entry.LockReason.Length > 0, selected = SelectedBuild == id;
        Color accent = locked ? UiTheme.Dim : UiTheme.Cyan;
        Box(rect, new("0d171d"), selected ? UiTheme.Amber : new("3b4b4e"), 2);
        if (_buildIcons.TryGetValue(id, out var icon))
            DrawTextureRect(icon, new Rect2(rect.Position + new Vector2(4, 1), new Vector2(69, 57)), false, new Color(1, 1, 1, locked ? .35f : 1));
        else
        {
            Vector2 center = rect.Position + new Vector2(34, 28);
            DrawArc(center, 23, Mathf.Pi, Mathf.Tau, 30, UiTheme.Alpha(accent, .35f), 1.2f, true);
            HudIcon(id, center, 16, accent);
        }
        Text(BuildingName(id), rect.Position + new Vector2(77, 22), 13, locked ? UiTheme.Muted : UiTheme.Ink);
        bool canBuild = CanBuildFromHud(entry);
        string status = locked ? entry.LockReason : canBuild ? entry.CostText : Game.ShieldBuildLockReason();
        Text(HudFitText(status, 187, 10), rect.Position + new Vector2(77, 43), 10, locked ? UiTheme.Dim : canBuild ? UiTheme.Mint : UiTheme.Amber);
        Text(Game.Buildings.L(id).ToString("00"), rect.Position + new Vector2(8, 56), 10, accent);
        if (selected) Text("连建", rect.Position + new Vector2(242, 55), 10, UiTheme.Amber);
        RegisterButton(rect, "build:" + id);
        Hint(rect, locked ? entry.LockReason : "局部护盾发生器 · 保护覆盖范围内的地球表面，建造免费，受科技上限与三波冷却限制。研究「局部修复」后，每座还会以极慢速率修复地球耐久，点击可查看覆盖范围与护盾强度");
    }

    private void DrawCommands()
    {
        if (IsSpectating())
        {
            FloatingPanel(new(480, 828, 480, 56), .82f);
            Button(new(490, 836, 130, 40), "下一架 · V", "spectator:next");
            Button(new(628, 836, 132, 40), "返回指挥视角", "spectator:exit", true, true, "primary");
            Button(new(768, 836, 182, 40), UserPaused ? "继续 · 空格" : "暂停 · 空格", "pause");
            return;
        }
        if (IsObserving())
        {
            var body = _celestialBodies.FirstOrDefault(b => b.S("id") == FocusId) ?? new DataMap();
            FloatingPanel(new(388, 793, 664, 91), .83f);
            Text(body.S("name", "星域"), new(407, 820), 19, UiTheme.Ink);
            Text("自由视角 · 地球战斗持续推进", new(501, 819), 12, UiTheme.Cyan);
            Text(HudFitText(body.S("description", "拖动环绕，滚轮查看表面细节"), 408, 11), new(407, 844), 11, UiTheme.Muted);
            Text("拖动环绕 · 滚轮缩放 · R 返回地球 · 空格暂停", new(407, 867), 10, UiTheme.Dim);
            Button(new(855, 832, 178, 40), "返回地球 →", "body:earth", true, true, "primary");
            return;
        }
        bool resume = SaveExists && !Started;
        float width = resume ? 582 : 422, x = 720 - width * .5f;
        FloatingPanel(new(x - 8, 828, width + 16, 56), .72f);
        string label = Defeated ? "重新部署 →" : CampaignWon ? UserPaused ? "继续防御" : _campaignStatus.S("earth_phase") == "ceasefire" ? "休整中" : "地球防御中" : Started ? Battle.WaveRunning ? UserPaused ? "继续防御" : "防御进行中" : "下一波 →" : "开始防御 →";
        Button(new(x, 836, 144, 40), label, Defeated ? "modal:restart" : CampaignWon || Battle.WaveRunning ? "pause" : "start", true, true, "primary");
        Button(new(x + 152, 836, 64, 40), $"{Speed:0} ×", "speed");
        Button(new(x + 224, 836, 92, 40), "说明 F1", "help");
        if (resume)
            Button(new(x + 430, 836, 152, 40), "继续上次防御 →", "load");
    }

    private void DrawSolarNavigation()
    {
        Button(new(1280, 824, 132, 42), "星域 · 导航", "solar:toggle", SolarNavOpen);
        if (!SolarNavOpen)
            return;
        FloatingPanel(new(1184, 383, 228, 427), .9f);
        Text("星域导航", new(1202, 410), 17, UiTheme.Ink);
        Text("切换天体，环绕观测", new(1203, 431), 11, UiTheme.Muted);
        Button(new(1370, 393, 28, 26), "×", "solar:close");
        string[] ids = { "earth", "moon", "mercury", "venus", "mars", "europa", "titan", "sun" }, names = { "地球", "月球", "水星", "金星", "火星", "木卫二", "土卫六", "太阳" };
        for (int i = 0; i < ids.Length; i++)
            Button(new(1197, 445 + i * 36, 202, 31), names[i] + (i == 0 ? " · 防御基地" : " · 查看天体"), "body:" + ids[i], FocusId == ids[i]);
        Button(new(1197, 739, 202, 34), "星域全览 →", "system:overview", FocusId == "system");
        Text("切换视角不会暂停地球战斗", new(1203, 796), 10, UiTheme.Dim);
    }

    private void DrawModal()
    {
        if (Modal == "achievements")
        {
            DrawAchievements();
            return;
        }
        if (Modal == "combat")
        {
            DrawCombatSettings();
            return;
        }
        if (Modal == "help")
        {
            Box(new(365, 178, 710, 556), new("10212e"), new("3a5662"), 16);
            Text("欢迎来到地球守望", new(405, 232), 30, UiTheme.Ink);
            Text("单击星球切换视角，悬停轮廓提示可选目标。", new(407, 269), 15, UiTheme.Muted);
            Button(new(994, 201, 44, 36), "×", "modal:close");
            string[] names = { "连续建设", "工厂机群", "边界推进", "永久特性" }, descriptions = { "选中后左键划过空格连续建造，右键退出，中键拖动视角。", "编制选择九种机型；重型占2点，阵亡由所属工厂补造。", "母舰清空后分三次拉远，继续研究航程，存活母舰持续派兵。", "小科技只需科研；外星点研究中大科技，特性升级跨局保留。" };
            for (int i = 0; i < 4; i++)
            {
                float y = 320 + i * 72;
                Text((i + 1).ToString("00"), new(407, y + 19), 25, UiTheme.Mint, true);
                Text(names[i], new(458, y + 6), 17, UiTheme.Ink);
                Text(descriptions[i], new(458, y + 32), 13, UiTheme.Muted);
            }
            Text("空格 开始 / 暂停   TAB 科技树   C 战斗参数   V 观战", new(407, 639), 13, UiTheme.Cyan);
            Text("切换天体不会暂停战斗；空格与右上按钮全局暂停。", new(407, 668), 13, UiTheme.Muted);
            Button(new(407, 683, 180, 35), "开始新的防御", "modal:restart");
            Button(new(837, 683, 197, 35), "继续守望 →", "modal:close", true, true, "primary");
            return;
        }
        bool defeat = Modal == "defeat";
        Box(new(436, 249, 568, 381), new("18252e"), defeat ? UiTheme.Coral : UiTheme.Mint, 16);
        Center(defeat ? "这一次，星光暂时熄灭。" : "近地威胁已清空，守望仍将继续。", new(456, 283, 528, 54), 26, UiTheme.Ink);
        Center(defeat ? "永久特性与能源核心已保留，强化后再次部署。" : "休整之后，母舰阵地将逐层拉远。", new(456, 343, 528, 36), 14, UiTheme.Muted);
        Center($"坚持 {Game.Wave:00} 波     拦截 {Game.Kills} 个目标     积分 {UiTheme.Number(Game.Score)}", new(456, 400, 528, 36), 17, UiTheme.Mint);
        if (defeat)
            DrawDefeatAchievement();
        Button(new(481, 481, 230, 46), defeat ? "强化工厂特性" : "继续整备", defeat ? "modal:perks" : "modal:close");
        Button(new(723, 481, 236, 46), "重新部署 →", "modal:restart", true, true, "primary");
        if (defeat && CanRetryWave)
            Button(new(481, 542, 478, 40), "重试本波 · 从开头继续", "modal:retry_wave");
        else if (SaveExists)
            Button(new(481, 542, 478, 40), "恢复保存的防御记录", "modal:load");
    }

    private void DrawHudTooltip()
    {
        foreach (var hint in _tooltips)
        {
            Rect2 r = hint.Get<Rect2>("rect");
            if (!r.HasPoint(Mouse))
                continue;
            MeasureHudTooltip(hint.S("text"), Math.Min(620, WorldSize.X - 56));
            var lines = _hudTooltipLines;
            float width = _hudTooltipWidth, height = 12 + 19 * lines.Length;
            Vector2 at = new(Math.Clamp(r.Position.X, 16, WorldSize.X - width - 16), Math.Clamp(r.End.Y + 8, 52, WorldSize.Y - height - 12));
            FloatingPanel(new(at, new(width, height)), .96f);
            for (int i = 0; i < lines.Length; i++)
                Text(lines[i], at + new Vector2(12, 21 + i * 19), 12, i == 0 ? UiTheme.Ink : UiTheme.Muted);
            return;
        }
    }

    private void DrawResourceUpgradeCursor()
    {
        if (!ResourceUpgradeMode || IsOverUi(Mouse))
            return;
        HudIcon("core_upgrade", Mouse + new Vector2(20, 18), 10, UiTheme.Amber);
        if (_upgradeHoverSite < 0)
            return;
        var slots = Planet.GetSlots();
        if (_upgradeHoverSite >= slots.Count)
            return;
        string kind = (string)slots[_upgradeHoverSite]!;
        long level = Game.GetResourceFacilityLevel(_upgradeHoverSite);
        Vector2 at = (Mouse + new Vector2(28, 32)).Min(WorldSize - new Vector2(306, 116));
        FloatingPanel(new(at, new(290, 95)), .94f);
        Text(BuildingName(kind) + $" · L{level} → L{level + 1}", at + new Vector2(12, 25), 14, UiTheme.Ink);
        Text($"{FormatSetting(Game.ResourceFacilityOutput(kind, _upgradeHoverSite))} → {FormatSetting(Game.ResourceFacilityOutput(kind, _upgradeHoverSite, 1))} / 秒", at + new Vector2(12, 49), 13, UiTheme.Mint);
        Text($"消耗 1 核心 · 当前 {Game.ResourceCores}", at + new Vector2(12, 73), 12, Game.ResourceCores > 0 ? UiTheme.Amber : UiTheme.Coral);
    }

    private void DrawAircraftHover()
    {
        if (!AircraftPerkSelectionEnabled() || _aircraftHover.Count == 0 || IsOverUi(Mouse))
            return;
        var p = Planet.GetSpaceScreenPosition(Battle.GetDroneWorldPosition(_aircraftHover));
        float radius = 13;
        foreach (int sx in new[] { -1, 1 })
            foreach (int sy in new[] { -1, 1 })
            {
                var c = p + new Vector2(sx, sy) * radius;
                DrawLine(c, c - new Vector2(sx * 6, 0), UiTheme.Mint, 1.2f, true);
                DrawLine(c, c - new Vector2(0, sy * 6), UiTheme.Mint, 1.2f, true);
            }
        Text("编制 " + (_aircraftHover.I("patrol_slot") + 1), p + new Vector2(18, -14), 10, UiTheme.Mint);
    }

    private void HudIcon(string id, Vector2 p, float r, Color c)
    {
        if (id == "settings")
        {
            for (int i = -1; i <= 1; i++)
            {
                DrawLine(p + new Vector2(-r, i * r * .7f), p + new Vector2(r, i * r * .7f), c, 1.3f, true);
                DrawCircle(p + new Vector2((i == 0 ? .4f : -.4f) * r, i * r * .7f), 2.2f, c);
            }
            return;
        }
        if (id is "pause" or "play")
        {
            if (id == "pause")
            {
                DrawLine(p + new Vector2(-r * .35f, -r * .7f), p + new Vector2(-r * .35f, r * .7f), c, 2.4f, true);
                DrawLine(p + new Vector2(r * .35f, -r * .7f), p + new Vector2(r * .35f, r * .7f), c, 2.4f, true);
            }
            else
                DrawColoredPolygon(new[] { p + new Vector2(-r * .45f, -r * .75f), p + new Vector2(r * .7f, 0), p + new Vector2(-r * .45f, r * .75f) }, c);
            return;
        }
        if (id is "audio" or "muted")
        {
            DrawPolyline(new[] { p + new Vector2(-r, -r * .3f), p + new Vector2(-r * .55f, -r * .3f), p + new Vector2(0, -r * .8f), p + new Vector2(0, r * .8f), p + new Vector2(-r * .55f, r * .3f), p + new Vector2(-r, r * .3f), p + new Vector2(-r, -r * .3f) }, c, 1.3f, true);
            if (id == "audio")
                DrawArc(p, r, -.65f, .65f, 12, c, 1.2f, true);
            else
            {
                DrawLine(p + new Vector2(r * .3f, -r * .35f), p + new Vector2(r, r * .35f), c, 1.4f, true);
                DrawLine(p + new Vector2(r, -r * .35f), p + new Vector2(r * .3f, r * .35f), c, 1.4f, true);
            }
            return;
        }
        if (id == "eye")
        {
            DrawPolyline(Enumerable.Range(0, 33).Select(i => p + new Vector2(Mathf.Cos(i * Mathf.Tau / 32) * r, Mathf.Sin(i * Mathf.Tau / 32) * r * .58f)).ToArray(), c, 1.3f, true);
            DrawCircle(p, r * .24f, c);
            return;
        }
        if (id == "earth")
        {
            DrawArc(p, r, 0, Mathf.Tau, 24, c, 1.2f, true);
            DrawLine(p - new Vector2(r * .9f, 0), p + new Vector2(r * .9f, 0), c, 1, true);
            DrawPolyline(Enumerable.Range(0, 25).Select(i => p + new Vector2(Mathf.Cos(i * Mathf.Tau / 24) * r * .43f, Mathf.Sin(i * Mathf.Tau / 24) * r)).ToArray(), c, 1, true);
            return;
        }
        if (id == "wave")
        {
            for (int i = 0; i < 2; i++)
            {
                float y = i * r * .8f - r * .4f;
                DrawPolyline(new[] { p + new Vector2(-r * .75f, y + r * .3f), p + new Vector2(0, y - r * .3f), p + new Vector2(r * .75f, y + r * .3f) }, c, 1.4f, true);
            }
            return;
        }
        if (id == "alien")
        {
            DrawArc(p, r * .83f, 0, Mathf.Tau, 24, c, 1.3f, true);
            DrawLine(p + new Vector2(-r * .4f, -r * .05f), p + new Vector2(-r * .15f, r * .25f), c, 1.8f, true);
            DrawLine(p + new Vector2(r * .4f, -r * .05f), p + new Vector2(r * .15f, r * .25f), c, 1.8f, true);
            return;
        }
        if (id == "core_upgrade")
        {
            VectorIcons.Draw(this, "core", p - new Vector2(r * .22f, 0), r * .7f, c);
            DrawLine(p + new Vector2(r * .72f, r * .65f), p + new Vector2(r * .72f, -r * .65f), c, 1.6f, true);
            DrawPolyline(new[] { p + new Vector2(r * .35f, -r * .2f), p + new Vector2(r * .72f, -r * .65f), p + new Vector2(r * 1.09f, -r * .2f) }, c, 1.4f, true);
            return;
        }
        if (id == "armor_light")
        {
            DrawPolyline(new[] { p + new Vector2(-r, r * .65f), p + new Vector2(0, -r), p + new Vector2(r, r * .65f) }, c, 1.4f, true);
            return;
        }
        if (id == "armor_energy")
        {
            VectorIcons.Draw(this, "core", p, r, c);
            DrawArc(p, r * 1.28f, 0, Mathf.Tau, 20, UiTheme.Alpha(c, .5f), 1, true);
            return;
        }
        VectorIcons.Draw(this, id switch
        {
            "minerals" => "mineral",
            "energy" => "power",
            "armor_heavy" => "armor",
            "mine" => "mineral",
            "solar" => "power",
            "satellite_launcher" => "orbit",
            _ => id
        }, p, r, c);
    }
}
