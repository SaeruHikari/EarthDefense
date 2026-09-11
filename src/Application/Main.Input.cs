using Godot;
using System;
using System.Linq;
using Earthward.Domain;
namespace Earthward.Application;

public partial class Main
{

    public bool IsOverUi(Vector2 point)
    {
        if (TacticalAlertContains(point) || NativeUiContains(point))
            return true;
        return UiRects.Any(rect => rect.HasPoint(point)) || Buttons.Any(button => button.Get<Rect2>("rect").HasPoint(point));
    }

    private bool NativeUiContains(Vector2 point) => IsInstanceValid(ResearchGraph) && ResearchGraph.IsVisibleInTree() && _researchClip.GetGlobalRect().HasPoint(point) && ResearchGraph.GetGlobalRect().HasPoint(point) || IsInstanceValid(FactoryPerkSidebar) && FactoryPerkSidebar.IsVisibleInTree() && FactoryPerkSidebar.GetGlobalRect().HasPoint(point) || Modal == "combat" && (CombatFields.Values.Any(f => f.Visible && f.GetGlobalRect().HasPoint(point)) || IsInstanceValid(CheatAmountField) && CheatAmountField.Visible && CheatAmountField.GetGlobalRect().HasPoint(point));

    public override void _Input(InputEvent e)
    {
        if (Planet == null)
            return;
        if (e is InputEventMouse m)
            Mouse = m.Position;
        if (TacticalAlertInput(e)) return;
        if (e is InputEventMouseButton dismiss && dismiss.Pressed && dismiss.ButtonIndex == MouseButton.Right)
            ClearFactoryCoverage();
        if (e is InputEventMouseButton ending && ending.ButtonIndex == MouseButton.Left && !ending.Pressed && (_buildPainting || _upgradePointerDown))
        {
            if (_buildPainting)
                FinishBuildStroke();
            _upgradePointerDown = false;
            Dragging = false;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventMouse && (NativeUiContains(Mouse) || ResearchGraph.IsPointerActive || GetViewport().GuiIsDragging()))
        {
            Dragging = false;
            return;
        }
        if (WorldTargetInput(e))
            return;
        if (e is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (Defeated && key.Keycode != Key.F11)
                return;
            if (Modal == "combat" && key.Keycode != Key.F11)
            {
                if (key.Keycode == Key.Escape)
                {
                    CloseCombatSettings();
                    GetViewport().SetInputAsHandled();
                }
                else if (key.Keycode is Key.Enter or Key.KpEnter)
                {
                    if (_combatPage == "cheat") ApplyResourceCheat();
                    else ApplyCombatSettings();
                    GetViewport().SetInputAsHandled();
                }
                return;
            }
            switch (key.Keycode)
            {
                case Key.Escape:
                    if (Modal != "")
                        Modal = "";
                    else if (SelectedCoverageSiteId >= 0)
                    {
                        ClearFactoryCoverage();
                        GetViewport().SetInputAsHandled();
                    }
                    else if (ResearchSidebarOpen())
                    {
                        DockOpen = false;
                    }
                    else if (IsSpectating())
                        ExitSpectator();
                    else if (SolarNavOpen)
                        SolarNavOpen = false;
                    else if (ResourceUpgradeMode)
                        CancelResourceUpgrade();
                    else if (SelectedBuild != "")
                        CancelBuildSelection();
                    else if (DockOpen)
                        DockOpen = false;
                    else if (IsObserving())
                        FocusBody("earth");
                    else
                        UserPaused = !UserPaused;
                    break;
                case Key.Space:
                    if (Modal == "" && !Defeated)
                    {
                        if (!Started && !Campaign.OwnsEarthSchedule())
                            StartWave();
                        else
                            UserPaused = !UserPaused;
                    }
                    break;
                case Key.V:
                    if (Modal == "" && !Defeated)
                    {
                        NextSpectator();
                        GetViewport().SetInputAsHandled();
                    }
                    break;
                case Key.Tab:
                    if (Modal == "")
                    {
                        if (ResearchSidebarOpen())
                            DockOpen = false;
                        else
                            OpenResearchSidebar();
                    }
                    GetViewport().SetInputAsHandled();
                    break;
                case Key.F1:
                    Modal = Modal == "help" ? "" : "help";
                    break;
                case Key.C:
                    OpenCombatSettings();
                    break;
                case Key.R:
                    if (Modal == "")
                        Planet.ResetCamera();
                    break;
                case Key.F11:
                    DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
                    break;
                case Key.Key1:
                    Speed = 1;
                    break;
                case Key.Key2:
                    Speed = 2;
                    break;
            }
        }
        if (IsSpectating() && Modal == "" && e is InputEventMouse && !IsOverUi(Mouse))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventMouseButton wheel && wheel.Pressed && Modal == "" && !IsOverUi(Mouse) && new Rect2(Vector2.Zero, WorldSize).HasPoint(Mouse) && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            Planet.ZoomCamera(wheel.ButtonIndex == MouseButton.WheelUp ? -.75f : .75f);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventMouseButton control && Modal == "")
        {
            if (control.ButtonIndex == MouseButton.Right && control.Pressed && ResourceUpgradeMode)
            {
                CancelResourceUpgrade();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (control.ButtonIndex == MouseButton.Right && control.Pressed && SelectedBuild != "")
            {
                CancelBuildSelection();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (control.ButtonIndex == MouseButton.Middle)
            {
                _middleDragging = control.Pressed && !IsOverUi(Mouse);
                Dragging = _middleDragging;
                _dragDistance = _middleDragging ? 8 : 0;
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        if (e is InputEventMouseMotion painting && _buildPainting)
        {
            float distance = _paintPrevious.DistanceTo(Mouse);
            int count = Math.Clamp((int)Math.Ceiling(distance / 6), 1, 256);
            for (int sample = 1; sample <= count; sample++)
                PaintBuildAt(_paintPrevious.Lerp(Mouse, (float)sample / count));
            _paintPrevious = Mouse;
            Planet.SetBuildHover(Mouse);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventMouseButton click && click.ButtonIndex == MouseButton.Left)
        {
            if (click.Pressed)
            {
                for (int i = Buttons.Count - 1; i >= 0; i--)
                {
                    var button = Buttons[i];
                    if (Modal != "" && !button.S("action").StartsWith("modal:"))
                        continue;
                    if (button.Get<Rect2>("rect").HasPoint(Mouse))
                    {
                        Action(button.S("action"));
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
                if (IsOverUi(Mouse))
                {
                    Dragging = false;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (Modal == "" && !Defeated && new Rect2(Vector2.Zero, WorldSize).HasPoint(Mouse))
                {
                    bool surface = Planet.ScreenToSurface(Mouse).LengthSquared() > .5f;
                    if (ResourceUpgradeMode && surface)
                    {
                        if (!_upgradePointerDown)
                        {
                            _upgradePointerDown = true;
                            UpgradeResourceAt(Mouse);
                        }
                        Dragging = false;
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    if (SelectedBuild != "" && surface)
                    {
                        _buildPainting = true;
                        _paintedCells.Clear();
                        _paintShortageNotified = false;
                        _paintPrevious = Mouse;
                        Dragging = false;
                        PaintBuildAt(Mouse);
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                    Dragging = true;
                    _dragDistance = 0;
                    _dragStart = Mouse;
                }
            }
            else
            {
                if (_upgradePointerDown)
                {
                    _upgradePointerDown = false;
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (_buildPainting)
                {
                    FinishBuildStroke();
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (Dragging && _dragDistance < 7 && !ResourceUpgradeMode && !IsOverUi(Mouse) && !IsObserving())
                {
                    Planet.PlacingBuilding = SelectedBuild != "";
                    Planet.PlacingKind = SelectedBuild;
                    if (Planet.PickExistingSite(Mouse) < 0)
                        ClearFactoryCoverage();
                    Planet.HandleClick(Mouse);
                }
                Dragging = false;
            }
        }
        if (e is InputEventMouseMotion motion && Dragging)
        {
            _dragDistance += motion.Relative.Length();
            if (_dragDistance >= 7 && !IsOverUi(Mouse))
                Planet.OrbitCamera(-motion.Relative.X * .007f, motion.Relative.Y * .007f);
        }
        if (e is InputEventMouseMotion)
        {
            bool hovering = SelectedBuild != "" && Modal == "" && !Defeated && !IsObserving() && !Dragging && !IsOverUi(Mouse) && new Rect2(Vector2.Zero, WorldSize).HasPoint(Mouse);
            Planet.SetBuildHover(hovering ? Mouse : new Vector2(-10000, -10000));
            UpdateResourceUpgradeHover();
            UpdateNavigationHover();
        }
    }

    public void Action(string action)
    {
        InvalidateHudData();
        if (action == "research:frontier")
        {
            OpenResearchSidebar();
            ResearchGraph.FocusNode(new[] { "C_G1", "C_A2", "C_A3", "C_G2" }[Math.Clamp(Game.DefenseReachStage, 0, 3)]);
            return;
        }
        if (action.StartsWith("body:"))
        {
            FocusBody(action[5..]);
            return;
        }
        if (action.StartsWith("build:"))
        {
            ChooseBuild(action[6..]);
            return;
        }
        if (action.StartsWith("modal:combat:resource:"))
        {
            SelectCheatResource(action[22..]);
            return;
        }
        if (action.StartsWith("modal:combat:page:"))
        {
            SetCombatPage(action[18..]);
            return;
        }
        switch (action)
        {
            case "coverage:close":
                ClearFactoryCoverage();
                break;
            case "spectator:toggle":
                if (IsSpectating())
                    ExitSpectator();
                else
                    EnterSpectator();
                break;
            case "spectator:next":
                NextSpectator();
                break;
            case "spectator:exit":
                ExitSpectator();
                break;
            case "tool:resource_upgrade":
                SetResourceUpgradeMode(!ResourceUpgradeMode);
                break;
            case "system:overview":
                FocusSystem();
                break;
            case "tab:build":
                ExitSpectator();
                if (IsObserving())
                    FocusBody("earth");
                SolarNavOpen = false;
                Tab = "build";
                DockOpen = true;
                break;
            case "tab:tech":
            case "tab:research":
            case "research":
                OpenResearchSidebar();
                break;
            case "tab:perks":
            case "modal:perks":
                OpenFactoryPerks();
                break;
            case "dock:close":
                DockOpen = false;
                break;
            case "solar:toggle":
                SolarNavOpen = !SolarNavOpen;
                if (SolarNavOpen)
                {
                    DockOpen = false;
                    RefreshCelestialCatalog();
                }
                break;
            case "solar:close":
                SolarNavOpen = false;
                break;
            case "start":
                if (!Defeated)
                    StartWave();
                break;
            case "pause":
                UserPaused = !UserPaused;
                break;
            case "speed":
                Speed = Speed == 1 ? 2 : 1;
                break;
            case "repair":
                if (Game.Repair())
                {
                    Sounds.PlaySound("build");
                    ShowNotice("修复完成 · 行星耐久 +30，轨道护盾 +40");
                    SaveIfSafe();
                }
                else
                    ShowNotice("无需修复，或修复资源不足 · " + CostText(Game.RepairCost()));
                break;
            case "help":
                Modal = "help";
                break;
            case "victory":
                Modal = "victory";
                break;
            case "combat":
                OpenCombatSettings();
                break;
            case "modal:combat:unlock_all":
                ApplyUnlockAllTechnologyCheat();
                break;
            case "modal:combat:cheat":
                ApplyResourceCheat();
                break;
            case "modal:combat:apply":
                ApplyCombatSettings();
                break;
            case "modal:combat:defaults":
                foreach (string key in CombatKeys)
                    CombatFields[key].Text = FormatSetting(DefenseState.DefaultCombatSettings.N(key));
                _settingsNotice = "已填入默认值 · 点击应用使其生效";
                _settingsError = false;
                break;
            case "audio":
                Sounds.Muted = !Sounds.Muted;
                break;
            case "modal:retry_wave":
                RetryWaveStart();
                break;
            case "load":
            case "modal:load":
                LoadCheckpoint();
                break;
            case "modal:close":
                CloseCombatSettings();
                break;
            case "modal:restart":
                Restart();
                break;
        }
        if (Modal != "")
            ClearResourceUpgradeHover();
        QueueRedraw();
    }

    public void ChooseBuild(string id)
    {
        if (!Game.BuildingUnlocked(id))
        {
            ShowNotice(Game.BuildingLockReason(id));
            return;
        }
        ClearFactoryCoverage();
        ExitSpectator();
        CancelResourceUpgrade();
        if (IsObserving())
        {
            FocusBody("earth");
            Planet.SelectedSlot = -1;
        }
        SolarNavOpen = false;
        SelectedBuild = id;
        Planet.PlacingKind = id;
        DockOpen = true;
        Planet.PlacingBuilding = true;
        var slots = Planet.GetSlots();
        int slot = Planet.SelectedSlot;
        if (slot >= 0 && slot < slots.Count && (string?)slots[slot] == "")
            PlaceBuilding(slot);
        else
            ShowNotice("已选择 " + BuildingName(id) + " · 按住左键划过空地连续建造，右键退出");
    }

    private void SlotSelected(int index)
    {
        if (IsObserving() || IsSpectating() || ResourceUpgradeMode)
            return;
        var slots = Planet.GetSlots();
        if (index < 0 || index >= slots.Count)
        {
            ClearFactoryCoverage();
            return;
        }
        string kind = (string?)slots[index] ?? "";
        if (kind is "interceptor" or "laser" or "missile" && SelectedBuild == "")
        {
            SelectFactoryCoverage(index);
            if (Tab == "perks")
                FactoryPerkSidebar.SelectFactory(kind, index);
            return;
        }
        ClearFactoryCoverage();
        if (kind == "")
        {
            if (SelectedBuild != "")
                PlaceBuilding(index);
            else
                ShowNotice($"已选择六边形地块 {index + 1:00} · 点击右上「建设」选择设施");
        }
        else
            ShowNotice($"六边形地块 {index + 1:00} · {BuildingName(kind)} 已占用此格");
    }

    public void PlaceBuilding(int slot)
    {
        if (IsSpectating())
            return;
        var slots = Planet.GetSlots();
        if (IsObserving() || SelectedBuild == "" || slot < 0 || slot >= slots.Count)
            return;
        string existing = (string?)slots[slot] ?? "";
        if (existing != "")
        {
            ShowNotice("该六边形地块已有 " + BuildingName(existing) + " · 请选择空闲地块");
            return;
        }
        if (!Planet.CanPlaceStructure(slot, SelectedBuild))
        {
            ShowNotice(Planet.GetStructurePlacementReason(slot, SelectedBuild));
            return;
        }
        if (Game.Build(SelectedBuild, slot))
        {
            Planet.SetSlot(slot, SelectedBuild);
            RefreshCampaignUi();
            Sounds.PlaySound("build");
            ShowNotice(BuildingName(SelectedBuild) + " 建造完成 · 生产已接入行星网络");
            Planet.PlacingBuilding = true;
            if (_buildPainting)
                _paintSavePending = true;
            else
                SaveIfSafe();
        }
        else
        {
            ShowNotice("资源不足或科技尚未解锁 · " + CostText(Game.BuildingCost(SelectedBuild)));
            Sounds.PlaySound("error");
        }
    }

    private void PaintBuildAt(Vector2 point)
    {
        if (_buildPainting && _paintShortageNotified || SelectedBuild == "" || Modal != "" || Defeated || IsObserving() || IsOverUi(point) || !new Rect2(Vector2.Zero, WorldSize).HasPoint(point))
            return;
        if (Planet.ScreenToSurface(point).LengthSquared() < .5f)
            return;
        int cell = Planet.PickBuildCell(point);
        if (cell < 0 || !_paintedCells.Add(cell))
            return;
        int site = Planet.GetSiteAtCell(cell);
        var slots = Planet.GetSlots();
        if (site >= 0 && site < slots.Count && (string?)slots[site] != "")
            return;
        if (!Game.CanBuild(SelectedBuild))
        {
            if (!_paintShortageNotified)
            {
                ShowNotice("资源不足或科技尚未解锁，已保留建造选择 · " + CostText(Game.BuildingCost(SelectedBuild)));
                _paintShortageNotified = true;
            }
            return;
        }
        Planet.PlacingBuilding = true;
        Planet.HandleClick(point);
    }

    private void FinishBuildStroke()
    {
        _buildPainting = false;
        _paintedCells.Clear();
        if (_paintSavePending)
        {
            _paintSavePending = false;
            SaveIfSafe();
        }
    }

    private void CancelBuildSelection()
    {
        FinishBuildStroke();
        SelectedBuild = "";
        if (IsInstanceValid(Planet))
            Planet.PlacingBuilding = false;
        Dragging = false;
        ShowNotice("已退出建造 · 左键拖动环绕地球");
    }

    private void SetResourceUpgradeMode(bool enabled)
    {
        if (!enabled)
        {
            CancelResourceUpgrade();
            return;
        }
        if (Defeated || Modal != "")
            return;
        ExitSpectator();
        if (IsObserving())
            FocusBody("earth");
        FinishBuildStroke();
        SelectedBuild = "";
        Planet.PlacingBuilding = false;
        Planet.SetBuildHover(new(-10000, -10000));
        ClearFactoryCoverage();
        ResourceUpgradeMode = true;
        _upgradePointerDown = false;
        Dragging = false;
        _middleDragging = false;
        SolarNavOpen = false;
        UpdateResourceUpgradeHover();
        ShowNotice("核心强化 · 点击资源设施消耗 1 核心提升一层 · 右键 / Esc 退出");
        QueueRedraw();
    }

    private void CancelResourceUpgrade()
    {
        ResourceUpgradeMode = false;
        _upgradePointerDown = false;
        ClearResourceUpgradeHover();
        Dragging = false;
        _middleDragging = false;
        QueueRedraw();
    }

    private void ClearResourceUpgradeHover()
    {
        _upgradeHoverSite = -1;
        if (IsInstanceValid(Planet))
            Planet.ClearResourceUpgradeHover();
    }

    private void UpdateResourceUpgradeHover()
    {
        if (!ResourceUpgradeMode || Modal != "" || Defeated || IsObserving() || IsSpectating() || Dragging || IsOverUi(Mouse) || !new Rect2(Vector2.Zero, WorldSize).HasPoint(Mouse))
        {
            ClearResourceUpgradeHover();
            return;
        }
        _upgradeHoverSite = Planet.SetResourceUpgradeHover(Mouse);
    }

    public bool UpgradeResourceAt(Vector2 point)
    {
        if (!ResourceUpgradeMode || Modal != "" || Defeated || IsObserving() || IsSpectating() || IsOverUi(point) || !new Rect2(Vector2.Zero, WorldSize).HasPoint(point))
            return false;
        int site = Planet.PickExistingSite(point);
        var slots = Planet.GetSlots();
        if (site < 0 || site >= slots.Count || !DefenseState.ResourceFacilityKinds.Contains((string?)slots[site] ?? ""))
        {
            ShowNotice("核心只能强化采矿站、太阳能阵列或研究所 · 请选择已有资源设施");
            return false;
        }
        string kind = (string)slots[site]!;
        if (!Game.UpgradeResourceFacility(site, kind))
        {
            ShowNotice("需要 1 个资源核心 · 已保留强化工具");
            Sounds.PlaySound("error");
            return false;
        }
        Planet.SelectedSlot = site;
        _upgradeHoverSite = Planet.SetResourceUpgradeHover(point);
        ShowNotice($"{BuildingName(kind)} 已强化至 L{Game.GetResourceFacilityLevel(site)} · 单座产能 {FormatSetting(Game.ResourceFacilityOutput(kind, site))} / 秒");
        Sounds.PlaySound("research");
        SaveIfSafe();
        QueueRedraw();
        return true;
    }

    private bool NavigationAllowed(Vector2 point) => Modal == "" && !Defeated && !IsSpectating() && new Rect2(Vector2.Zero, WorldSize).HasPoint(point) && !IsOverUi(point);

    private bool AircraftPerkSelectionEnabled() => FactoryPerkSidebar != null && DockOpen && Tab == "perks" && FactoryPerkSidebar.SelectedLayer == "aircraft" && SelectedBuild == "" && !ResourceUpgradeMode && !IsSpectating();

    public DataMap PickAircraftPerkTarget(Vector2 point, bool precise = false)
    {
        if (!AircraftPerkSelectionEnabled())
        {
            _aircraftHover = new();
            return new();
        }
        long now = (long)Time.GetTicksMsec();
        if (!precise && now - _aircraftPickTime < 100 && point.DistanceSquaredTo(_aircraftPickPoint) < 16)
            return _aircraftPickCache;
        _aircraftPickTime = now;
        _aircraftPickPoint = point;
        _aircraftPickCache = new();
        _aircraftHover = new();
        var origin = Planet.Camera.GlobalPosition;
        var ray = Planet.ProjectViewRay(point);
        float best = float.PositiveInfinity, pixels = 2 * Mathf.Tan(Mathf.DegToRad(Planet.Camera.Fov) * .5f) / Math.Max(1, WorldSize.Y);
        foreach (var drone in Battle.Drones)
        {
            if (drone.N("hp") <= 0)
                continue;
            var p = Battle.GetDroneWorldPosition(drone);
            float depth = (p - origin).Dot(ray);
            if (depth <= 0 || depth >= best)
                continue;
            float radius = Math.Max((float)drone.N("hit_radius", .055), depth * pixels * 5);
            if (p.DistanceSquaredTo(origin + ray * depth) > radius * radius || !Planet.IsSpaceVisible(p))
                continue;
            best = depth;
            _aircraftHover = drone;
            _aircraftPickCache = new()
            {
                ["kind"] = "aircraft",
                ["uid"] = drone.L("uid"),
                ["aircraft_kind"] = drone.S("kind"),
                ["site_id"] = drone.L("factory_site_id"),
                ["berth"] = drone.I("patrol_slot"),
                ["distance"] = depth
            };
        }
        return _aircraftPickCache;
    }

    private DataMap PickWorldTarget(Vector2 point, bool precise = false)
    {
        var target = Planet.PickNavigationTarget(point);
        if (target.S("kind") == "body" && target.S("id") == FocusId)
            target = new DataMap();
        var aircraft = PickAircraftPerkTarget(point, precise);
        if (aircraft.Count > 0 && (target.Count == 0 || aircraft.N("distance") < target.N("distance", double.PositiveInfinity)))
            return aircraft;
        return target;
    }

    public void SelectAircraftPerkTarget(DataMap target)
    {
        if (!Battle.IsLiveDrone(target.L("uid", -1)))
        {
            ShowNotice("该战机已离场，可在编制列表配置补充战机");
            return;
        }
        OpenFactoryPerks();
        FactoryPerkSidebar.SelectAircraft(target.S("aircraft_kind"), target.L("site_id"), target.I("berth"));
        ShowNotice($"已选择工厂 #{target.L("site_id") + 1} · 战机编制 {target.I("berth") + 1:00}，补造后沿用此配置");
    }

    private void ClearNavigationFeedback()
    {
        if (IsInstanceValid(Planet))
            Planet.ClearNavigationHover();
        if (_navigationCursorActive)
        {
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            _navigationCursorActive = false;
        }
        _aircraftHover = new();
    }

    private void CancelNavigationPress()
    {
        if (_navigationPressedTarget.Count > 0)
            Dragging = false;
        _navigationPressedTarget.Clear();
        _navigationPressDistance = 0;
    }

    private void UpdateNavigationHover()
    {
        if (!IsInstanceValid(Planet))
            return;
        if (!NavigationAllowed(Mouse) || Dragging || _buildPainting || _middleDragging || _upgradePointerDown || ResearchGraph.IsPointerActive || _navigationPressedTarget.Count > 0)
        {
            ClearNavigationFeedback();
            return;
        }
        var target = PickWorldTarget(Mouse);
        if (target.S("kind") == "aircraft")
            Planet.ClearNavigationHover();
        else
            Planet.SetNavigationHover(target);
        bool active = target.Count > 0;
        if (active != _navigationCursorActive)
        {
            _navigationCursorActive = active;
            Input.SetDefaultCursorShape(active ? Input.CursorShape.PointingHand : Input.CursorShape.Arrow);
        }
    }

    private bool WorldTargetInput(InputEvent e)
    {
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode is Key.Escape or Key.Tab or Key.R or Key.C or Key.F1 or Key.V)
        {
            CancelNavigationPress();
            ClearNavigationFeedback();
        }
        if (e is not InputEventMouse)
            return false;
        if (e is InputEventMouseMotion motion)
        {
            if (_navigationPressedTarget.Count > 0)
            {
                _navigationPressDistance += motion.Relative.Length();
                _navigationPressDistance = Math.Max(_navigationPressDistance, Mouse.DistanceTo(_navigationPressOrigin));
            }
            UpdateNavigationHover();
        }
        if (e is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Right && button.Pressed)
            {
                CancelNavigationPress();
                ClearNavigationFeedback();
            }
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    CancelNavigationPress();
                    if (!NavigationAllowed(Mouse))
                    {
                        ClearNavigationFeedback();
                        return false;
                    }
                    _navigationPressedTarget = PickWorldTarget(Mouse, true).DeepClone();
                    if (_navigationPressedTarget.Count > 0)
                    {
                        _navigationPressOrigin = Mouse;
                        _navigationPressDistance = 0;
                        Dragging = true;
                        _dragDistance = 0;
                        _dragStart = Mouse;
                        GetViewport().SetInputAsHandled();
                        return true;
                    }
                }
                else if (_navigationPressedTarget.Count > 0)
                {
                    var target = _navigationPressedTarget.DeepClone();
                    bool click = Math.Max(_navigationPressDistance, Mouse.DistanceTo(_navigationPressOrigin)) < 7 && _dragDistance < 7 && NavigationAllowed(Mouse);
                    CancelNavigationPress();
                    Dragging = false;
                    if (click)
                    {
                        ClearNavigationFeedback();
                        if (target.S("kind") == "aircraft")
                            SelectAircraftPerkTarget(target);
                        else if (target.S("kind") == "body")
                            FocusBody(target.S("id"));
                    }
                    else
                        ClearNavigationFeedback();
                    GetViewport().SetInputAsHandled();
                    return true;
                }
            }
        }
        if (!NavigationAllowed(Mouse))
            ClearNavigationFeedback();
        return false;
    }
}

