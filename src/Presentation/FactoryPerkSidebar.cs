using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
namespace Earthward.Presentation;

public partial class FactoryPerkSidebar : Control
{
    public event Action<string, bool>? FeedbackRequested;
    public static readonly string[] Kinds = { "interceptor", "laser", "missile" };

    public static readonly Dictionary<string, string> KindNames = new() { { "interceptor", "拦截工厂" }, { "laser", "激光工厂" }, { "missile", "导弹工厂" } };
    public string SelectedKind { get; private set; } = "interceptor";
    public string SelectedLayer = "factory";
    public string SelectedAirframe = "K1";
    public string SelectedPerk = "expanded_hangar";
    public long SelectedSiteId { get; private set; } = -1;
    public int SelectedBerth { get; private set; } = -1;
    public int SelectedSlot;

    public Dictionary<string, Button> KindButtons { get; } = new();

    public Dictionary<string, Button> LayerButtons = new();

    public List<EquipmentSocket> SlotButtons { get; } = new();

    public Dictionary<string, InventoryTile> InventoryTiles { get; } = new();

    public List<string> VisibleIds { get; } = new();
    public OptionButton SourceSelector { get; private set; } = null!;
    public OptionButton BerthSelector = null!;
    public OptionButton AirframeSelector = null!;
    public ScrollContainer Scroll { get; private set; } = null!;
    public ScrollContainer InventoryScroll = null!;
    public GridContainer InventoryGrid { get; private set; } = null!;
    public Button EquipButton { get; private set; } = null!;
    public Button UpgradeButton = null!;
    public Button InheritButton = null!;
    public Label DetailName { get; private set; } = null!;
    public Label DetailEffect = null!;
    public Label DetailNext = null!;
    public Label CoreLabel = null!;
    private Label _inventoryCount = null!;
    private DefenseState? _game;
    private FactoryPerks? _perks;
    private bool _readyComplete;
    private bool _refreshPending;

    private List<DataMap> _sites = new();

    private List<long> _sourceIds = new() { -1 };

    private List<int> _berthIds = new() { -1 };

    private List<string> _airframeIds = new();

    private readonly Dictionary<string, DataMap> _definitions = new();

    private List<string> _equipped = new() { "", "" };
    private string _scope = "";
    private string _berthCache = "";
    public string LastFeedback { get; private set; } = "";
    public partial class InventoryTile : Button
    {
        public FactoryPerkSidebar Sidebar = null!;
        public string PerkId = "";
        public override void _Draw() => Sidebar.DrawInventory(this, PerkId);
        public override Variant _GetDragData(Vector2 atPosition) => Sidebar.InventoryDrag(PerkId, this);
        public override GodotObject _MakeCustomTooltip(string text) => Sidebar.PerkTooltip(PerkId);
        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton b && b.Pressed && b.ButtonIndex == MouseButton.Left && b.DoubleClick)
            {
                Sidebar.SelectPerk(PerkId);
                Sidebar.EquipTo(Sidebar.SelectedSlot, PerkId);
                AcceptEvent();
            }
        }
    }
    public partial class EquipmentSocket : Button
    {
        public FactoryPerkSidebar Sidebar = null!;
        public int SlotIndex;
        public Button Clear = null!;
        public override void _Draw() => Sidebar.DrawSocket(this, SlotIndex);
        public override bool _CanDropData(Vector2 atPosition, Variant data) => Sidebar.CanEquipDrop(SlotIndex, data);
        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (Sidebar.CanEquipDrop(SlotIndex, data))
                Sidebar.EquipTo(SlotIndex, data.AsString().Split('\t').Last());
        }
        public override GodotObject _MakeCustomTooltip(string text) => Sidebar.PerkTooltip(Sidebar._equipped[SlotIndex]);
        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton b && b.Pressed && b.ButtonIndex == MouseButton.Right)
            {
                Sidebar.EquipTo(SlotIndex, "");
                AcceptEvent();
            }
        }
    }
    public partial class Glyph : Control
    {
        public string Icon = "";
        public Color Tint = UiTheme.Mint;
        public override void _Draw() => VectorIcons.Draw(this, Icon, Size * .5f, Math.Min(Size.X, Size.Y) * .4f, Tint);
    }

    public override void _Ready()
    {
        Name = "FactoryPerks";
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        AddThemeFontOverride("font", UiTheme.Font);
        Scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(Scroll);
        Scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 4);
        Scroll.AddChild(body);
        var currency = new HBoxContainer { CustomMinimumSize = new Vector2(0, 22) };
        body.AddChild(currency);
        currency.AddChild(NewGlyph("core", UiTheme.Amber, new Vector2(22, 22)));
        CoreLabel = UiTheme.Label("能源核心  0", 14, UiTheme.Amber);
        CoreLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        currency.AddChild(CoreLabel);
        var badge = UiTheme.Label("跨局保留", 10, UiTheme.Mint);
        badge.CustomMinimumSize = new Vector2(48, 0);
        badge.TooltipText = "已解锁特性、等级、能源核心与类型默认配装跨局保留；本局单厂覆盖随新局重置。";
        currency.AddChild(badge);
        var layers = new HBoxContainer();
        layers.AddThemeConstantOverride("separation", 5);
        body.AddChild(layers);
        foreach (string layer in new[] { "factory", "aircraft" })
        {
            var button = UiTheme.Button(layer == "factory" ? "工厂装备" : "战机装备", 11);
            button.CustomMinimumSize = new Vector2(0, 24);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Pressed += () => SelectLayer(layer);
            layers.AddChild(button);
            LayerButtons[layer] = button;
        }
        var kinds = new HBoxContainer();
        kinds.AddThemeConstantOverride("separation", 5);
        body.AddChild(kinds);
        foreach (string kind in Kinds)
        {
            var button = UiTheme.Button("    " + KindNames[kind].Replace("工厂", ""), 11);
            button.CustomMinimumSize = new Vector2(0, 25);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.TooltipText = KindNames[kind];
            var icon = NewGlyph(kind, UiTheme.Mint, new Vector2(16, 16));
            icon.Position = new Vector2(5, 5);
            button.AddChild(icon);
            button.Pressed += () => SelectKind(kind);
            kinds.AddChild(button);
            KindButtons[kind] = button;
        }
        AirframeSelector = NewSelector(24, 11);
        AirframeSelector.ItemSelected += index => AirframeSelected((int)index);
        body.AddChild(AirframeSelector);
        var sources = new HBoxContainer();
        body.AddChild(sources);
        SourceSelector = NewSelector(25, 11);
        SourceSelector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SourceSelector.ItemSelected += index => SourceSelected((int)index);
        sources.AddChild(SourceSelector);
        BerthSelector = NewSelector(25, 10);
        BerthSelector.CustomMinimumSize = new Vector2(78, 25);
        BerthSelector.ItemSelected += index => BerthSelected((int)index);
        sources.AddChild(BerthSelector);
        InheritButton = UiTheme.Button("↺", 15);
        InheritButton.CustomMinimumSize = new Vector2(27, 25);
        BindTargetAction(InheritButton, InheritDefault);
        sources.AddChild(InheritButton);
        var sockets = new HBoxContainer();
        sockets.AddThemeConstantOverride("separation", 8);
        body.AddChild(sockets);
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            var socket = new EquipmentSocket { Sidebar = this, SlotIndex = index, CustomMinimumSize = new Vector2(80, 68), SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "装备槽" };
            UiTheme.PaintButton(socket);
            socket.Pressed += () => SelectSlot(index);
            sockets.AddChild(socket);
            SlotButtons.Add(socket);
            socket.Clear = UiTheme.Button("×", 12);
            socket.Clear.CustomMinimumSize = new Vector2(18, 18);
            socket.Clear.Size = new Vector2(18, 18);
            socket.Clear.TooltipText = "卸下此槽；也可右键点击装备槽。";
            socket.AddChild(socket.Clear);
            socket.Resized += () => socket.Clear.Position = new Vector2(socket.Size.X - 22, 4);
            BindTargetAction(socket.Clear, () => EquipTo(index, ""));
        }
        var heading = new HBoxContainer { CustomMinimumSize = new Vector2(0, 18) };
        body.AddChild(heading);
        var title = UiTheme.Label("特性背包", 11, UiTheme.Muted);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(title);
        _inventoryCount = UiTheme.Label("已解锁 0/6", 10, UiTheme.Mint, false);
        _inventoryCount.CustomMinimumSize = new Vector2(66, 0);
        _inventoryCount.HorizontalAlignment = HorizontalAlignment.Right;
        heading.AddChild(_inventoryCount);
        InventoryScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 118), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(InventoryScroll);
        InventoryGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        InventoryGrid.AddThemeConstantOverride("h_separation", 6);
        InventoryGrid.AddThemeConstantOverride("v_separation", 6);
        InventoryScroll.AddChild(InventoryGrid);
        var detail = new PanelContainer();
        detail.AddThemeStyleboxOverride("panel", UiTheme.Panel());
        body.AddChild(detail);
        var details = new VBoxContainer();
        details.AddThemeConstantOverride("separation", 4);
        detail.AddChild(details);
        DetailName = UiTheme.Label("选择背包特性", 13);
        details.AddChild(DetailName);
        DetailEffect = UiTheme.Label("", 11, UiTheme.Muted);
        DetailEffect.CustomMinimumSize = new Vector2(0, 17);
        DetailEffect.MaxLinesVisible = 2;
        DetailEffect.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        details.AddChild(DetailEffect);
        DetailNext = UiTheme.Label("", 10, UiTheme.Amber);
        DetailNext.MaxLinesVisible = 1;
        DetailNext.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        details.AddChild(DetailNext);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        details.AddChild(actions);
        EquipButton = UiTheme.Button("装入槽 1", 11);
        EquipButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BindTargetAction(EquipButton, () => EquipTo(SelectedSlot, SelectedPerk));
        actions.AddChild(EquipButton);
        UpgradeButton = UiTheme.Button("升级", 11);
        UpgradeButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        UpgradeButton.Pressed += () => Upgrade(SelectedPerk);
        actions.AddChild(UpgradeButton);
        _readyComplete = true;
        RebuildSources();
        Refresh();
    }

    private static OptionButton NewSelector(float height, int fontSize)
    {
        var button = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(0, height), TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        button.AddThemeFontOverride("font", UiTheme.Font);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        return button;
    }

    private static Glyph NewGlyph(string id, Color tint, Vector2 size) => new() { Icon = id, Tint = tint, CustomMinimumSize = size, Size = size, MouseFilter = MouseFilterEnum.Ignore };

    public override void _ExitTree()
    {
        if (_perks != null)
            _perks.Changed -= QueueRefresh;
    }

    public void Bind(DefenseState game)
    {
        if (_perks != null)
            _perks.Changed -= QueueRefresh;
        _game = game;
        _perks = game.FactoryPerks;
        _perks.Changed += QueueRefresh;
        Refresh();
    }

    public void SetSites(IEnumerable<DataMap> entries)
    {
        var next = entries.Where(e => Kinds.Contains(e.S("kind")) && e.L("site_id", e.L("id", -1)) >= 0).GroupBy(e => e.L("site_id", e.L("id", -1))).Select(g => new DataMap { { "site_id", g.Key }, { "kind", g.First().S("kind") } }).OrderBy(e => e.L("site_id")).ToList();
        if (_sites.Count == next.Count && _sites.Zip(next).All(p => p.First.L("site_id") == p.Second.L("site_id") && p.First.S("kind") == p.Second.S("kind")))
            return;
        _sites = next;
        if (SelectedSiteId >= 0 && !SiteExists(SelectedKind, SelectedSiteId))
        {
            SelectedSiteId = -1;
            SelectedBerth = -1;
            Feedback("原工厂已移除，已切换为默认配装。", false);
        }
        RebuildSources();
        Refresh();
    }

    public void SelectFactory(string kind, long siteId = -1) => SelectTarget(kind, siteId, "factory", -1);

    public void SelectAircraft(string kind, long siteId = -1, int berth = -1) => SelectTarget(kind, siteId, "aircraft", berth);

    public void SelectLayer(string layer)
    {
        if (layer is "factory" or "aircraft")
            SelectTarget(SelectedKind, SelectedSiteId, layer, -1);
    }

    public void SelectKind(string kind) => SelectTarget(kind, -1, SelectedLayer, -1);

    private void SelectTarget(string kind, long site, string layer, int berth)
    {
        if (!Kinds.Contains(kind))
            return;
        SelectedKind = kind;
        SelectedLayer = layer;
        SelectedSiteId = site >= 0 && SiteExists(kind, site) ? site : -1;
        SelectedBerth = layer == "aircraft" && SelectedSiteId >= 0 && berth >= 0 && berth < FactoryCapacity() ? berth : -1;
        SelectedPerk = "";
        _berthCache = "";
        RebuildSources();
        Refresh();
        Callable.From(RevealSelected).CallDeferred();
    }

    public void SelectSlot(int slot)
    {
        if (slot < 0 || slot > 1)
            return;
        SelectedSlot = slot;
        if (_perks != null)
        {
            var equipped = _perks.SlotsFor(SelectedKind, SelectedSiteId, SelectedLayer, SelectedBerth);
            if (equipped[slot] != "")
                SelectedPerk = equipped[slot];
        }
        Refresh();
        Callable.From(RevealSelected).CallDeferred();
    }

    public void SelectPerk(string id)
    {
        if (!VisibleIds.Contains(id))
            return;
        SelectedPerk = id;
        Refresh();
    }

    private void RevealSelected()
    {
        if (InventoryTiles.TryGetValue(SelectedPerk, out var tile) && tile.Visible)
            InventoryScroll.EnsureControlVisible(tile);
    }

    private int FactoryCapacity() => _game == null || SelectedSiteId < 0 ? 0 : (int)_game.FactoryCapacityForSite(SelectedKind, SelectedSiteId);

    private bool SiteExists(string kind, long site) => _sites.Any(s => s.L("site_id") == site && s.S("kind") == kind);

    private void SourceSelected(int index)
    {
        if (index < 0 || index >= _sourceIds.Count)
            return;
        SelectedSiteId = _sourceIds[index];
        SelectedBerth = -1;
        SelectedPerk = "";
        _berthCache = "";
        Refresh();
    }

    private void BerthSelected(int index)
    {
        if (index < 0 || index >= _berthIds.Count)
            return;
        SelectedBerth = _berthIds[index];
        SelectedPerk = "";
        Refresh();
    }

    private void RebuildSources()
    {
        if (!_readyComplete)
            return;
        _sourceIds = new() { -1 };
        SourceSelector.Clear();
        SourceSelector.AddItem(SelectedLayer == "factory" ? "默认工厂配装" : "默认战机配装");
        foreach (var site in _sites.Where(s => s.S("kind") == SelectedKind))
        {
            _sourceIds.Add(site.L("site_id"));
            SourceSelector.AddItem($"工厂 #{site.L("site_id") + 1}");
        }
        SourceSelector.Select(Math.Max(0, _sourceIds.IndexOf(SelectedSiteId)));
    }

    private void QueueRefresh()
    {
        if (_refreshPending || !IsInsideTree())
            return;
        _refreshPending = true;
        Callable.From(() => { _refreshPending = false; Refresh(); }).CallDeferred();
    }

    public void Refresh()
    {
        if (!_readyComplete)
            return;
        if (_perks == null || _game == null)
        {
            CoreLabel.Text = "能源核心  —";
            return;
        }
        _definitions.Clear();
        foreach (var definition in _perks.Definitions())
            _definitions[definition.S("id")] = definition;
        RefreshBerths();
        RefreshAirframes();
        var definitions = _perks.Definitions(SelectedLayer, SelectedKind, SelectedLayer == "aircraft" ? SelectedAirframe : "").OrderByDescending(d => d.List("kinds").Count == 1).ThenBy(d => d.I("stage")).ThenBy(d => d.I("priority", 999)).ToList();
        VisibleIds.Clear();
        int unlocked = 0;
        foreach (var definition in definitions)
        {
            string id = definition.S("id");
            VisibleIds.Add(id);
            if (definition.B("unlocked"))
                unlocked++;
            if (!InventoryTiles.ContainsKey(id))
                CreateTile(id);
            InventoryGrid.MoveChild(InventoryTiles[id], VisibleIds.Count - 1);
            InventoryTiles[id].Visible = true;
            InventoryTiles[id].QueueRedraw();
        }
        foreach (var (id, tile) in InventoryTiles)
            if (!VisibleIds.Contains(id))
                tile.Visible = false;
        CoreLabel.Text = "能源核心  " + UiTheme.Number(_perks.EnergyCores);
        CoreLabel.TooltipText = "中型 Boss 掉落的永久升级货币。工厂与战机分开装备，等级跨局共享保留。";
        _inventoryCount.Text = $"已解锁 {unlocked}/{VisibleIds.Count}";
        foreach (var (kind, button) in KindButtons)
            UiTheme.Mark(button, kind == SelectedKind);
        foreach (var (layer, button) in LayerButtons)
            UiTheme.Mark(button, layer == SelectedLayer);
        _equipped = _perks.SlotsFor(SelectedKind, SelectedSiteId, SelectedLayer, SelectedBerth);
        if (SelectedPerk == "" || (!VisibleIds.Contains(SelectedPerk) && !_equipped.Contains(SelectedPerk)))
            SelectedPerk = VisibleIds.Contains(_equipped[SelectedSlot]) ? _equipped[SelectedSlot] : VisibleIds.FirstOrDefault() ?? "";
        bool hasOverride = SelectedSiteId >= 0 && _perks.HasSiteOverride(SelectedKind, SelectedSiteId, SelectedLayer, SelectedBerth);
        _scope = SelectedLayer == "factory" ? (SelectedSiteId < 0 ? "工厂类型默认" : $"工厂 #{SelectedSiteId + 1}") : (SelectedSiteId < 0 ? "战机类型默认" : SelectedBerth < 0 ? $"工厂 #{SelectedSiteId + 1} · 整队" : $"工厂 #{SelectedSiteId + 1} · 编制 {SelectedBerth + 1}");
        SourceSelector.TooltipText = _scope + "\n" + (SelectedSiteId < 0 ? "跨局默认配装，已有单独配置者不受影响。" : hasOverride ? "当前独立配置，重开局清除该覆盖。" : "当前继承上一级默认配装。");
        InheritButton.Visible = SelectedSiteId >= 0;
        InheritButton.Disabled = !hasOverride;
        InheritButton.TooltipText = SelectedLayer == "aircraft" && SelectedBerth >= 0 ? "恢复所属整队默认" : "恢复该类型默认";
        for (int i = 0; i < 2; i++)
        {
            SlotButtons[i].Clear.Visible = _equipped[i] != "";
            SlotButtons[i].QueueRedraw();
        }
        RefreshDetail();
    }

    private void RefreshBerths()
    {
        bool active = SelectedLayer == "aircraft" && SelectedSiteId >= 0;
        BerthSelector.Visible = active;
        if (!active)
        {
            SelectedBerth = -1;
            return;
        }
        int capacity = FactoryCapacity();
        if (SelectedBerth >= capacity)
            SelectedBerth = -1;
        var plan = _game!.FactoryAirframePlan(SelectedKind, SelectedSiteId);
        string signature = $"{SelectedKind}:{SelectedSiteId}:{capacity}:" + string.Join(';', plan.Select(p => $"{p.I("berth")},{p.S("airframe_id")},{p.B("enabled")},{p.I("capacity_cost")}"));
        if (signature != _berthCache)
        {
            _berthCache = signature;
            BerthSelector.Clear();
            BerthSelector.AddItem("整队默认");
            _berthIds = new() { -1 };
            foreach (var row in plan)
            {
                int berth = row.I("berth");
                _berthIds.Add(berth);
                BerthSelector.AddItem($"编制 {berth + 1}" + (row.B("enabled") ? "" : " · 预备"));
                BerthSelector.GetPopup().SetItemTooltip(_berthIds.Count - 1, $"占用 {row.I("capacity_cost")} 编制点。" + (row.B("enabled") ? "独立机型与特性，阵亡补造继承。" : "编制点不足，配置保留；扩容或换轻型机后补造。"));
            }
        }
        if (SelectedBerth >= 0 && !_berthIds.Contains(SelectedBerth))
        {
            _berthIds.Add(SelectedBerth);
            BerthSelector.AddItem($"在役编制 {SelectedBerth + 1}");
        }
        BerthSelector.Select(Math.Max(0, _berthIds.IndexOf(SelectedBerth)));
        BerthSelector.TooltipText = "整队默认影响该厂未单独配置的战机；指定编制只影响该架与其阵亡补造。也可点击场上战机选择编制。";
    }

    private void RefreshAirframes()
    {
        AirframeSelector.Visible = SelectedLayer == "aircraft";
        SelectedAirframe = _game!.FactoryAirframe(SelectedKind, SelectedSiteId, SelectedBerth);
        if (SelectedLayer != "aircraft")
            return;
        AirframeSelector.Clear();
        _airframeIds.Clear();
        foreach (var d in _game.AvailableAirframes(SelectedKind))
        {
            string id = d.S("id");
            _airframeIds.Add(id);
            int index = _airframeIds.Count - 1;
            AirframeSelector.AddItem($"{id} {d.S("name")}  ·  {d.I("capacity_cost")} 编制点");
            string reason = _game.GetAirframeLockReason(SelectedKind, SelectedSiteId, SelectedBerth, id);
            AirframeSelector.SetItemDisabled(index, reason != "");
            AirframeSelector.GetPopup().SetItemTooltip(index, reason != "" ? d.S("lock_reason", reason) : "下次补造生效。在役飞机保持原机型；不兼容专属特性会卸下，解锁与永久等级保留。");
        }
        AirframeSelector.Select(Math.Max(0, _airframeIds.IndexOf(SelectedAirframe)));
        AirframeSelector.TooltipText = "当前编制机型 · 基础型 1 点，重型 2 点。\n下次补造生效，在役飞机保持原机型。机型只保留于本局。未编入现役的预备位不会额外生成战机。";
    }

    public void AirframeSelected(int index)
    {
        if (_game == null || index < 0 || index >= _airframeIds.Count)
            return;
        string id = _airframeIds[index], reason = _game.GetAirframeLockReason(SelectedKind, SelectedSiteId, SelectedBerth, id);
        if (reason != "")
        {
            Feedback(reason, false);
            Refresh();
            return;
        }
        bool ok = _game.SetAirframe(SelectedKind, SelectedSiteId, SelectedBerth, id);
        if (ok)
        {
            SelectedPerk = "";
            _berthCache = "";
        }
        Feedback(ok ? $"{id} 已排产，下次补造生效；永久特性等级保留。" : "机型切换失败，原配置已保留。", ok);
        Refresh();
    }

    private void CreateTile(string id)
    {
        var tile = new InventoryTile { Sidebar = this, PerkId = id, CustomMinimumSize = new Vector2(70, 56), SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = id };
        UiTheme.PaintButton(tile);
        tile.Pressed += () => SelectPerk(id);
        InventoryGrid.AddChild(tile);
        InventoryTiles[id] = tile;
    }

    private void RefreshDetail()
    {
        if (!_definitions.TryGetValue(SelectedPerk, out var d))
            return;
        string id = SelectedPerk;
        bool unlocked = d.B("unlocked"), current = _equipped[SelectedSlot] == id, other = _equipped.Contains(id) && !current;
        int level = d.I("level"), maximum = d.I("max_level");
        bool incompatible = SelectedLayer == "aircraft" && !VisibleIds.Contains(id);
        string equipReason = _perks!.EquipLockReason(SelectedKind, SelectedSiteId, SelectedSlot, id, SelectedLayer, SelectedBerth, SelectedAirframe), upgradeReason = _perks.UpgradeLockReason(id);
        DetailName.Text = $"{d.S("name")}  ·  " + (unlocked ? $"Lv.{level}" : "未解锁");
        DetailEffect.Text = incompatible ? "当前机型不适用 · 永久等级保留" : d.S("effect_text");
        DetailEffect.TooltipText = d.S("description");
        DetailNext.Text = !unlocked ? (d.I("stage") > 0 ? "高级 · 清空首轮母舰后掉落" : "基础 · 中型 Boss 掉落解锁") : level >= maximum ? "永久强化 · 已满级" : "下级 " + d.S("next_effect_text");
        DetailNext.AddThemeColorOverride("font_color", unlocked ? UiTheme.Amber : UiTheme.Muted);
        EquipButton.Text = current ? $"已装备 · 槽 {SelectedSlot + 1}" : other ? "另一槽已装备" : $"装入槽 {SelectedSlot + 1}";
        EquipButton.Disabled = current || equipReason != "";
        EquipButton.TooltipText = equipReason != "" ? equipReason : $"免费装备至{_scope}；可直接拖入槽位。";
        UpgradeButton.Text = !unlocked ? "尚未解锁" : level >= maximum ? "已满级" : "升级 · ◇ " + UiTheme.Number(d.L("upgrade_cost"));
        UpgradeButton.Disabled = upgradeReason != "";
        UpgradeButton.TooltipText = upgradeReason != "" ? upgradeReason : $"消耗 {UiTheme.Number(d.L("upgrade_cost"))} 能源核心，所有装备此特性的单位共享永久等级。";
    }

    private void DrawInventory(Button tile, string id)
    {
        if (!_definitions.TryGetValue(id, out var d))
            return;
        bool unlocked = d.B("unlocked"), selected = id == SelectedPerk;
        Color tint = unlocked ? new Color(d.S("color", "9be4cc")) : new Color("5c727e");
        DrawEquipmentFrame(tile, new Rect2(Vector2.One, tile.Size - new Vector2(2, 2)), selected, tile.IsHovered(), false);
        VectorIcons.Draw(tile, d.S("icon", id), new Vector2(tile.Size.X * .5f, 25), 13, tint);
        UiTheme.Center(tile, d.S("name"), new Vector2(0, tile.Size.Y - 5), tile.Size.X, 10, unlocked ? UiTheme.Ink : UiTheme.Muted);
        if (unlocked)
            SmallBadge(tile, $"L{d.I("level")}", new Vector2(tile.Size.X - 27, 5), UiTheme.Amber);
        else
            DrawLock(tile, new Vector2(tile.Size.X - 14, 13), new Color("77909a"));
        if (d.I("stage") > 0)
            tile.DrawColoredPolygon(new[] { new Vector2(10, 5), new Vector2(14, 9), new Vector2(10, 13), new Vector2(6, 9) }, new Color("c6acec"));
        if (_equipped.Contains(id))
        {
            tile.DrawCircle(new Vector2(9, 10), 3, UiTheme.Mint);
            tile.DrawLine(new Vector2(5, 39), new Vector2(5, 49), UiTheme.Mint, 2, true);
        }
        if (selected)
            tile.DrawLine(new Vector2(12, tile.Size.Y - 2), new Vector2(tile.Size.X - 12, tile.Size.Y - 2), UiTheme.Mint, 2, true);
    }

    private void DrawSocket(Button socket, int index)
    {
        string id = _equipped[index];
        DrawEquipmentFrame(socket, new Rect2(Vector2.One, socket.Size - new Vector2(2, 2)), index == SelectedSlot, socket.IsHovered(), id == "");
        socket.DrawString(UiTheme.Font, new Vector2(8, 14), $"0{index + 1}", fontSize: 9, modulate: index == SelectedSlot ? UiTheme.Mint : UiTheme.Muted);
        var c = new Vector2(socket.Size.X * .5f, 34);
        if (id == "")
        {
            socket.DrawCircle(c, 17, new Color("162c32"));
            socket.DrawLine(c - new Vector2(7, 0), c + new Vector2(7, 0), new Color("789c9a"), 1.5f, true);
            socket.DrawLine(c - new Vector2(0, 7), c + new Vector2(0, 7), new Color("789c9a"), 1.5f, true);
            UiTheme.Center(socket, "拖入特性", new Vector2(0, 64), socket.Size.X, 10, UiTheme.Muted);
        }
        else if (_definitions.TryGetValue(id, out var d))
        {
            bool incompatible = SelectedLayer == "aircraft" && !VisibleIds.Contains(id);
            VectorIcons.Draw(socket, d.S("icon", id), c, 18, new Color(d.S("color", "9be4cc")));
            UiTheme.Center(socket, (incompatible ? "未适配 · " : "") + d.S("name", id), new Vector2(0, 64), socket.Size.X, 10, incompatible ? UiTheme.Coral : UiTheme.Ink);
            SmallBadge(socket, $"Lv.{d.I("level")}", new Vector2(7, 46), UiTheme.Amber);
        }
    }

    private static void DrawEquipmentFrame(CanvasItem target, Rect2 rect, bool selected, bool hovered, bool dashed)
    {
        target.DrawRect(rect, new Color(selected ? "19332f" : "101e28"));
        target.DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, rect.Size.Y * .48f)), new Color(selected ? "243a3e" : "192b35"));
        Color border = selected ? UiTheme.Mint : new Color(hovered ? "66848b" : "3c535d");
        if (dashed)
        {
            foreach (float y in new[] { rect.Position.Y, rect.End.Y })
                target.DrawDashedLine(new Vector2(rect.Position.X, y), new Vector2(rect.End.X, y), UiTheme.Alpha(border, .7f), 1, 4, true);
            foreach (float x in new[] { rect.Position.X, rect.End.X })
                target.DrawDashedLine(new Vector2(x, rect.Position.Y), new Vector2(x, rect.End.Y), UiTheme.Alpha(border, .7f), 1, 4, true);
        }
        else
            target.DrawRect(rect, border, false, 1);
        foreach (var anchor in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), new Vector2(rect.Position.X, rect.End.Y), rect.End })
        {
            var direction = new Vector2(anchor.X == rect.Position.X ? 1 : -1, anchor.Y == rect.Position.Y ? 1 : -1);
            target.DrawLine(anchor, anchor + new Vector2(direction.X * 5, 0), border, 1.5f, true);
            target.DrawLine(anchor, anchor + new Vector2(0, direction.Y * 5), border, 1.5f, true);
        }
    }

    private static void SmallBadge(CanvasItem target, string text, Vector2 at, Color color)
    {
        float width = UiTheme.Font.GetStringSize(text, fontSize: 9).X + 7;
        target.DrawRect(new Rect2(at, new Vector2(width, 13)), new Color("0a1921"));
        target.DrawString(UiTheme.Font, at + new Vector2(3, 10), text, fontSize: 9, modulate: color);
    }

    private static void DrawLock(CanvasItem target, Vector2 c, Color color)
    {
        target.DrawArc(c - new Vector2(0, 1), 3.5f, Mathf.Pi, Mathf.Tau, 10, color, 1.2f, true);
        target.DrawRect(new Rect2(c - new Vector2(4, 1), new Vector2(8, 7)), new Color("172730"));
        target.DrawRect(new Rect2(c - new Vector2(4, 1), new Vector2(8, 7)), color, false, 1);
        target.DrawCircle(c + new Vector2(0, 2), 1, color);
    }
    public string ScopeContext => $"{SelectedLayer}:{SelectedKind}:{SelectedSiteId}:{SelectedBerth}:{SelectedAirframe}";
    private string TargetContext => $"{ScopeContext}:{SelectedSlot}:{SelectedPerk}";

    private Variant InventoryDrag(string id, Control source)
    {
        if (_perks == null || !_perks.IsUnlocked(id))
            return default;
        SelectPerk(id);
        var preview = new InventoryTile { Sidebar = this, PerkId = id, Size = new Vector2(76, 60), Position = new Vector2(-38, -30), MouseFilter = MouseFilterEnum.Ignore };
        UiTheme.PaintButton(preview);
        source.SetDragPreview(preview);
        return $"{GetInstanceId()}\t{ScopeContext}\t{id}";
    }

    public bool CanEquipDrop(int index, Variant data)
    {
        if (_perks == null || data.VariantType != Variant.Type.String)
            return false;
        string[] fields = data.AsString().Split('\t');
        return fields.Length == 3 && fields[0] == GetInstanceId().ToString() && fields[1] == ScopeContext && _perks.EquipLockReason(SelectedKind, SelectedSiteId, index, fields[2], SelectedLayer, SelectedBerth, SelectedAirframe) == "";
    }

    private void BindTargetAction(Button button, Action action)
    {
        string pressed = "";
        button.ButtonDown += () => pressed = TargetContext;
        button.Pressed += () => { if (pressed != "" && pressed != TargetContext) { Feedback("配置目标已改变，请重新选择。", false); return; } action(); };
    }

    public void EquipTo(int index, string id)
    {
        if (_perks == null)
            return;
        bool ok = _perks.Equip(SelectedKind, SelectedSiteId, index, id, SelectedLayer, SelectedBerth, SelectedAirframe);
        if (ok)
        {
            SelectedSlot = index;
            if (id != "")
                SelectedPerk = id;
        }
        Feedback(ok ? (id == "" ? $"已卸下{_scope}的槽 {index + 1}" : $"{_scope} · 已装备{_definitions.GetValueOrDefault(id)?.S("name", id) ?? id}") : _perks.LastError, ok);
        Refresh();
    }

    public void Upgrade(string id)
    {
        if (_perks == null)
            return;
        bool ok = _perks.Upgrade(id);
        Feedback(ok ? $"{_definitions.GetValueOrDefault(id)?.S("name", id) ?? id} Lv.{_perks.GetLevel(id)} · 永久强化" : _perks.LastError, ok);
        Refresh();
    }

    private void InheritDefault()
    {
        if (_perks == null || SelectedSiteId < 0)
            return;
        bool ok = _perks.ResetSiteToTemplate(SelectedKind, SelectedSiteId, SelectedLayer, SelectedBerth);
        Feedback(ok ? "已恢复上一级默认配装。" : _perks.LastError, ok);
        Refresh();
    }

    private void Feedback(string text, bool ok)
    {
        LastFeedback = text;
        FeedbackRequested?.Invoke(text, ok);
    }

    private Control PerkTooltip(string id)
    {
        if (!_definitions.TryGetValue(id, out var d))
            return UiTheme.Tooltip("空装备槽", "拖入已解锁特性进行装备。");
        var kinds = UiTheme.Strings(d.List("kinds"));
        string kind = kinds.Length == 1 ? KindNames.GetValueOrDefault(kinds[0], kinds[0]).Replace("工厂", "") : "通用";
        var frames = UiTheme.Strings(d.List("airframes"));
        if (frames.Length > 0)
            kind += " · " + string.Join('/', frames);
        string text = $"{(d.S("layer", "factory") == "factory" ? "工厂特性" : "战机特性")} · {kind} · {(d.I("stage") > 0 ? "高级" : "基础")}\n{d.S("description")}\n\n{d.S("effect_text")}";
        if (SelectedLayer == "aircraft" && _equipped.Contains(id) && !VisibleIds.Contains(id))
            text += "\n\n当前机型不适用，此特性暂不生效；等级保留，可卸下或更换。";
        if (d.B("unlocked"))
        {
            text += "\n下级：" + d.S("next_effect_text");
            text += d.I("level") < d.I("max_level") ? $"\n升级消耗 {UiTheme.Number(d.L("upgrade_cost"))} 能源核心 · 等级跨局保留" : "\n已达到永久等级上限";
            text += "\n拖入装备槽 / 双击装入所选槽 · 装备免费";
        }
        else
            text += "\n\n" + d.S("unlock_reason", "击败中型 Boss 后解锁。");
        return UiTheme.Tooltip(d.S("name") + (d.B("unlocked") ? $" · Lv.{d.I("level")}" : " · 未解锁"), text);
    }
}

