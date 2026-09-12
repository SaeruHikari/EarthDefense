using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Earthward.Application;
using Earthward.Domain;
namespace Earthward.Tests;

public partial class PresentationChecks : Node
{
    private Main _app = null!; private int _checks; private List<string> _failures = new();
    private void Check(bool value, string text)
    {
        _checks++;
        if (!value)
        {
            _failures.Add(text);
            GD.PrintErr("UI_FAIL: " + text);
        }
    }
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Move(Vector2 point, Vector2 relative = default, bool held = false)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = relative, ButtonMask = held ? MouseButtonMask.Left : 0 });
        await Frames();
    }
    private async Task Press(Vector2 point, MouseButton button = MouseButton.Left, bool pressed = true)
    {
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = pressed, ButtonMask = pressed && button == MouseButton.Left ? MouseButtonMask.Left : 0 });
        await Frames();
    }
    private async Task Click(Vector2 point, MouseButton button = MouseButton.Left)
    {
        await Move(point);
        await Press(point, button);
        await Press(point, button, false);
    }
    private async Task Capture(string name)
    {
        await Frames(4);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = ProjectSettings.GlobalizePath("res://artifacts/managed-" + name + ".png");
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
    public override async void _Ready()
    {
        try
        {
            string profile = ProjectSettings.GlobalizePath("user://");
            if (!profile.Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("UI tests require an isolated runtime-tests profile");
            _app = new Main();
            AddChild(_app);
            _app.UserPaused = true;
            await Frames(6);
            var camera = _app.Planet.CaptureCameraState();
            Vector2I view = _app.Planet.GetViewSize();
            Check(_app.Buttons.Count > 15, "custom HUD is populated");
            var tech = _app.Buttons.First(b => b.S("action") == "tab:tech").Get<Rect2>("rect");
            await Click(tech.GetCenter());
            await ToSignal(GetTree().CreateTimer(.45), SceneTreeTimer.SignalName.Timeout);
            Check(_app.Tab == "tech" && _app.ResearchGraph.Visible, "native technology tab opens graph");
            Check(_app.ResearchGraph.Nodes.Count(n => !n.B("is_successor")) == 300, "300 real base research nodes");
            Check(_app.ResearchGraph.Nodes.Count(n => n.S("size") == "small") == 247, "247 small independent nodes");
            Check(_app.Planet.GetViewSize() == view, "technology keeps render viewport");
            Check(_app.Planet.CaptureCameraState().ToJson() == camera.ToJson(), "technology leaves camera state unchanged");
            var graph = _app.ResearchGraph;
            graph.FitView();
            await Frames();
            Check(graph.Zoom < .5, "fit includes full deep graph");
            Vector2 origin = graph.Canvas.GlobalPosition + graph.Canvas.Size * .5f, oldPan = graph.Pan;
            await Move(origin);
            await Press(origin);
            await Move(origin + new Vector2(31, 18), new Vector2(31, 18), true);
            await Press(origin + new Vector2(31, 18), MouseButton.Left, false);
            Check(graph.Pan.DistanceTo(oldPan) > 20, "native left drag pans research canvas");
            Check(_app.Planet.CaptureCameraState().ToJson() == camera.ToJson(), "research drag does not rotate camera");
            _app.Game.Science = 1000000000;
            _app.PurchaseGraph("K_S01");
            Check(_app.Game.HasResearch("K_S01"), "single real research purchase");
            graph.Zoom = .8f;
            graph.Pan = -graph.WorldPosition(graph.Node("K_S02")) * .8f;
            graph.PositionButtons();
            await Frames();
            Vector2 small = graph.Canvas.GlobalPosition + graph.Point(graph.Node("K_S02"));
            long before = _app.Game.DeepResearch.Values.Count(v => v is bool b && b);
            double science = _app.Game.Science;
            await Move(small);
            Vector2 clickPan = graph.Pan;
            bool clickAvailable = graph.Node("K_S02").B("available");
            string hoverBefore = GetViewport().GuiGetHoveredControl()?.GetPath().ToString() ?? "none";
            await Press(small);
            bool clickCaptured = graph.IsPointerActive;
            await Press(small, MouseButton.Left, false);
            if (!_app.Game.HasResearch("K_S02"))
                GD.PrintErr($"UI_SMALL_CLICK_TRACE: available={clickAvailable} captured={clickCaptured} hover={hoverBefore} point={small} canvas={graph.Canvas.GetGlobalRect()} target={graph.Canvas.GlobalPosition + graph.Point(graph.Node("K_S02"))} pan={clickPan}->{graph.Pan} selected={graph.SelectedId}");
            Check(_app.Game.HasResearch("K_S02"), "native small click purchases in one click");
            Check(_app.Game.Science < science, "small research spends science");
            Check(graph.FindChildren("ResearchPurchaseCard", "", true, false).Count == 0, "research has no secondary confirmation panel");
            graph.FitView();
            await Capture("research-ui");
            _app.Action("tab:perks");
            await ToSignal(GetTree().CreateTimer(.45), SceneTreeTimer.SignalName.Timeout);
            var ui = _app.FactoryPerkSidebar;
            Check(ui.Visible && ui.Size.X == 276 && ui.Size.Y == 480, "native perk sidebar fits 276x480");
            Check(ui.VisibleIds.Count == 9, "factory includes six legacy and three exclusive perks");
            Check(ui.SlotButtons.Count == 2, "factory two physical equipment slots");
            var permanent = _app.Game.FactoryPerks.Snapshot();
            foreach (string id in permanent.Map("levels").Keys.ToArray())
                permanent.Map("levels")[id] = 1L;
            permanent["energy_cores"] = 500L;
            permanent["advanced_unlocked"] = true;
            Check(_app.Game.FactoryPerks.ImportSnapshot(permanent), "isolated permanent fixture");
            ui.Refresh();
            await Frames();
            string perk = ui.VisibleIds[0];
            var tile = ui.InventoryTiles[perk];
            var target = ui.SlotButtons[0];
            long cores = _app.Game.FactoryPerks.EnergyCores;
            await Move(tile.GetGlobalRect().GetCenter());
            await Press(tile.GetGlobalRect().GetCenter());
            await Move(tile.GetGlobalRect().GetCenter() + new Vector2(14, -14), new Vector2(14, -14), true);
            await Move(target.GetGlobalRect().GetCenter(), new Vector2(0, -70), true);
            Check(GetViewport().GuiIsDragging(), "native inventory drag enters Godot drag state");
            await Press(target.GetGlobalRect().GetCenter(), MouseButton.Left, false);
            Check(_app.Game.FactoryPerks.SlotsFor("interceptor")[0] == perk, "native drop equips selected factory scope");
            Check(_app.Game.FactoryPerks.EnergyCores == cores, "equipment does not spend upgrade cores");
            await Capture("factory-ui");
            await Click(target.GetGlobalRect().GetCenter(), MouseButton.Right);
            Check(_app.Game.FactoryPerks.SlotsFor("interceptor")[0] == "", "native right click unequips");
            ui.SelectPerk(perk);
            await Frames();
            int level = _app.Game.FactoryPerks.GetLevel(perk);
            long price = _app.Game.FactoryPerks.UpgradeCost(perk);
            await Click(ui.UpgradeButton.GetGlobalRect().GetCenter());
            Check(_app.Game.FactoryPerks.GetLevel(perk) == level + 1, "native upgrade commits permanent level");
            Check(_app.Game.FactoryPerks.EnergyCores == cores - price, "native upgrade spends exact core price");
            await Click(ui.LayerButtons["aircraft"].GetGlobalRect().GetCenter());
            Check(ui.SelectedLayer == "aircraft" && ui.VisibleIds.Count == 4, "native aircraft layer has four compatible perks");
            Check(ui.AirframeSelector.Visible, "aircraft model selector visible");
            Check(_app.Game.FactoryPerks.SlotsFor("interceptor", -1, "aircraft").All(s => s == ""), "factory does not leak into aircraft layer");
            await Capture("aircraft-ui");
            await Move(ui.InventoryTiles[ui.VisibleIds[0]].GetGlobalRect().GetCenter());
            await ToSignal(GetTree().CreateTimer(.75), SceneTreeTimer.SignalName.Timeout);
            await Capture("aircraft-hover-ui");
            _app.OpenCombatSettings();
            await Frames();
            Check(_app.CombatFields.Count == Main.CombatKeys.Length && _app.CombatFields.ContainsKey("patrol_coverage_multiplier"), "all engineering settings include editable patrol coverage");
            double originalDamage = _app.Game.CombatSettings.N("drone_damage");
            _app.CombatFields["drone_damage"].Text = "invalid";
            Check(!_app.ApplyCombatSettings(), "invalid settings rejected atomically");
            Check(_app.Game.CombatSettings.N("drone_damage") == originalDamage, "invalid settings preserve active model");
            _app.CombatFields["drone_damage"].Text = (originalDamage + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Check(_app.ApplyCombatSettings(), "valid settings commit");
            await Capture("settings-ui");
            _app.CloseCombatSettings();
            _app.Action("tab:build");
            await Frames();
            _app.ChooseBuild("mine");
            Vector2 outer = new(80, 350);
            var beforeOrbit = _app.Planet.CaptureCameraState().ToJson();
            await Move(outer);
            await Press(outer);
            await Move(outer + new Vector2(40, 15), new Vector2(40, 15), true);
            await Press(outer + new Vector2(40, 15), MouseButton.Left, false);
            Check(beforeOrbit != _app.Planet.CaptureCameraState().ToJson(), "building mode permits outer space left orbit");
            Check(_app.SelectedBuild == "mine", "orbit retains continuous build tool");
            await Press(outer, MouseButton.Right);
            Check(_app.SelectedBuild == "", "right click exits continuous build");
            _app.Planet.ResetCamera();
            _app.Game.Minerals = 1e7;
            _app.Game.Energy = 1e7;
            _app.Game.ResourceCores = 100;
            _app.ChooseBuild("mine");
            await Frames();
            var emptyPoints = new List<Vector2>();
            for (int y = 230; y < 700 && emptyPoints.Count < 3; y += 30)
                for (int x = 350; x < 1050 && emptyPoints.Count < 3; x += 35)
                {
                    var at = new Vector2(x, y);
                    int cell = _app.Planet.PickBuildCell(at);
                    if (cell < 0)
                        continue;
                    int site = _app.Planet.GetSiteAtCell(cell);
                    if (site >= 0 && (string?)_app.Planet.GetSlots()[site] != "")
                        continue;
                    if (emptyPoints.All(v => v.DistanceTo(at) > 85))
                        emptyPoints.Add(at);
                }
            Check(emptyPoints.Count >= 2, "projected empty ground exists");
            long facilities = _app.Game.FacilityCount(), resourceCores = _app.Game.ResourceCores;
            await Click(emptyPoints[0]);
            await Click(emptyPoints[1]);
            Check(_app.Game.FacilityCount() == facilities + 2, "two native surface clicks continuously build two facilities");
            Check(_app.Game.ResourceCores == resourceCores - 2, "continuous resource builds each spend one core");
            Check(_app.SelectedBuild == "mine", "build selection persists after completion");
            await Press(emptyPoints[1], MouseButton.Right);
            _app.Action("tool:resource_upgrade");
            int upgradeSite = _app.Planet.PickExistingSite(emptyPoints[0]);
            long upgrade = _app.Game.GetResourceFacilityLevel(upgradeSite);
            resourceCores = _app.Game.ResourceCores;
            await Click(emptyPoints[0]);
            Check(_app.Game.GetResourceFacilityLevel(upgradeSite) == upgrade + 1, "native core tool upgrades clicked facility once");
            Check(_app.Game.ResourceCores == resourceCores - 1, "native core tool spends exactly one core");
            _app.Action("tool:resource_upgrade");
            _app.Started = true;
            _app.Battle.Active = true;
            _app.Battle.Paused = false;
            for (int i = 0; i < 600; i++)
                _app.Battle.Step(1d / 60);
            _app.Battle.Paused = true;
            _app.SyncRender();
            await Frames();
            Check(_app.Battle.Drones.Count > 0, "real factory creates spectator candidates");
            _app.Action("tab:perks");
            ui.SelectAircraft("interceptor");
            await Frames();
            var drone = _app.Battle.Drones.FirstOrDefault(d => d.S("state") == "patrol");
            if (drone != null)
            {
                var cameraDirection = _app.Planet.Camera.GlobalPosition.Normalized();
                drone["space_position"] = cameraDirection * (WorldScale.EarthRadius + WorldScale.DroneAltitude);
                drone["normal"] = _app.Planet.SpaceToSurface((Vector3)drone["space_position"]!);
                drone["aim_direction"] = _app.Planet.Camera.GlobalBasis.X.Normalized();
                drone["aim_up"] = cameraDirection;
                drone["launch_age"] = 10d;
                _app.Battle.WorldCache.Clear();
                await Frames();
                Vector2 plane = _app.Planet.GetSpaceScreenPosition(_app.Battle.GetDroneWorldPosition(drone));
                await Click(plane);
                Check(ui.SelectedSiteId == drone.L("factory_site_id") && ui.SelectedBerth == drone.I("patrol_slot"), "native aircraft world click selects stable factory berth");
                var savedCamera = _app.Planet.CaptureCameraState().ToJson();
                _app.Action("spectator:toggle");
                await Frames();
                Check(_app.IsSpectating(), "native spectator enters an airborne factory aircraft");
                Check(Math.Abs(_app.Planet.Camera.Fov - 72) < .01, "spectator applies first-person field of view");
                _app.ExitSpectator();
                await Frames();
                Check(!_app.IsSpectating() && _app.Planet.CaptureCameraState().ToJson() == savedCamera, "spectator exit restores exact strategic camera");
            }
            else
                Check(false, "a real patrol aircraft is available");
        }
        catch (Exception e) { _failures.Add(e.ToString()); GD.PrintErr(e); }
        // Native audio releases are consumed after the scene exits. Drain them
        // before terminating the test process, including newly triggered short UI sounds.
        if (IsInstanceValid(_app))
        {
            _app.QueueFree();
            await Frames(4);
            await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
        }
        GD.Print($"MANAGED_PRESENTATION: {_checks} checks / {_failures.Count} failures");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}

