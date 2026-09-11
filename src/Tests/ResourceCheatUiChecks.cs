using Godot;
using Earthward.Application;
using Earthward.Domain;
using Earthward.Presentation;

namespace Earthward.Tests;

public partial class ResourceCheatUiChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool value, string label)
    {
        _checks++;
        if (!value) { _failures++; GD.PrintErr("RESOURCE_CHEAT_UI_FAIL: " + label); }
    }
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Click(Vector2 point)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }); await Frames();
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Frames();
    }
    private async Task Action(string action)
    {
        var button = _app.Buttons.First(row => row.S("action") == action).Get<Rect2>("rect");
        await Click(button.GetCenter());
    }
    private async Task Type(string value)
    {
        _app.CheatAmountField.Clear();
        await Click(_app.CheatAmountField.GetGlobalRect().GetCenter());
        foreach (char c in value)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = (Key)c, Unicode = c, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = (Key)c, Unicode = c, Pressed = false });
        }
        await Frames();
        Check(_app.CheatAmountField.Text == value, "native amount field accepts typed increment");
    }
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Resource cheat UI requires isolated profile");
            Check(UiTheme.Number(0) == "0" && UiTheme.Number(-0.0) == "0", "zero number formatting stays stable");
            Check(UiTheme.Number(1e4) == "10.0k" && UiTheme.Number(1e6) == "1.0M" && UiTheme.Number(1e9) == "1.0B" && UiTheme.Number(1e12) == "1.0T", "existing compact number units remain unchanged");
            Check(!UiTheme.Number(1e15 - 1).Contains('e') && UiTheme.Number(1e15) == "1e+15", "scientific notation starts exactly at one quadrillion");
            Check(UiTheme.Number(-1.25e25) == "-1.25e+25" && UiTheme.Number(1e300) == "1e+300", "huge resource balances preserve sign and remain short");
            GetWindow().Size = new Vector2I(1440, 900);
            _app = new Main(); AddChild(_app); _app.UserPaused = true; _app.PreserveCheckpoint = true;
            await Frames(5);
            string camera = _app.Planet.CaptureCameraState().ToJson();
            _app.OpenCombatSettings(); await Frames();
            await Action("modal:combat:page:cheat");
            Check(_app.CheatAmountField.Visible && _app.CombatFields.Values.All(field => !field.Visible), "cheat page reuses modal with only its own amount control");
            Check(_app.Buttons.Count(row => row.S("action").StartsWith("modal:combat:resource:")) == 6, "six resource choices include permanent energy cores");
            foreach (string resource in Main.CheatResourceIds)
            {
                await Action("modal:combat:resource:" + resource);
                Check(_app.SelectedCheatResource == resource, "native resource card selects exact currency");
                double before = _app.Game.CheatResourceBalance(resource);
                await Type(resource is "minerals" or "energy" or "science" ? "125.5" : "7");
                await Action("modal:combat:cheat");
                double expected = resource is "minerals" or "energy" or "science" ? 125.5 : 7;
                Check(_app.Game.CheatResourceBalance(resource) == before + expected, "native submit adds increment instead of replacing balance");
                Check(_app.CheatFeedback.Contains("当前"), "submit reports actual new balance");
            }
            Check(Godot.FileAccess.FileExists(Main.SavePath), "ordinary resources immediately save atomic checkpoint");
            Check(Godot.FileAccess.FileExists("user://earthward_factory_perks.json"), "permanent energy cores save existing profile");
            await Action("modal:combat:resource:resource_cores");
            double cores = _app.Game.CheatResourceBalance("resource_cores");
            foreach (string bad in new[] { "0", "-3", "1.5", "NaN", "Infinity", "1e309", "100000000000000000000", "oops" })
            {
                _app.CheatAmountField.Text = bad;
                await Action("modal:combat:cheat");
                Check(_app.Game.CheatResourceBalance("resource_cores") == cores, "invalid negative fractional or overflow core input changes nothing");
            }
            await Action("modal:combat:resource:minerals");
            double mineral = _app.Game.CheatResourceBalance("minerals");
            await Type("10");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = true }); await Frames();
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, Pressed = false }); await Frames();
            Check(_app.Game.CheatResourceBalance("minerals") == mineral + 10, "Enter on cheat page adds exactly once and does not apply engineering settings");
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(90, 90), GlobalPosition = new Vector2(90, 90) });
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using (var image = GetViewport().GetTexture().GetImage()) Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/resource-cheat-ui.png")) == Error.Ok, "capture resource cheat UI");
            Check(_app.Planet.CaptureCameraState().ToJson() == camera, "cheat UI never changes strategic camera");
            Check(DeepTechnology.ChainBranches.All(branch => _app.Game.SuccessorCount(branch) == 0), "fresh run starts with no advanced research");
            var balances = Main.CheatResourceIds.ToDictionary(id => id, id => _app.Game.CheatResourceBalance(id));
            await Action("modal:combat:unlock_all");
            Check(DeepTechnology.Nodes.All(node => _app.Game.HasResearch(node.S("id"))), "native unlock all completes every regular technology");
            Check(DeepTechnology.ChainBranches.All(branch => _app.Game.SuccessorCount(branch) == 0), "unlock all leaves fresh advanced research at zero");
            Check(balances.All(pair => _app.Game.CheatResourceBalance(pair.Key) == pair.Value), "unlock all spends no resources");
            await Action("modal:combat:unlock_all");
            Check(balances.All(pair => _app.Game.CheatResourceBalance(pair.Key) == pair.Value), "second unlock all remains idempotent and free");
            Check(DeepTechnology.ChainBranches.All(branch => _app.Game.SuccessorCount(branch) == 0), "repeated unlock all never buys advanced research");
            _app.CloseCombatSettings();
            Check(!_app.CheatAmountField.Visible && !_app.CheatAmountField.HasFocus(), "closing parameters releases cheat input");
            Check(_app.LoadCheckpoint(), "cheated checkpoint restores through production loader");
            Check(_app.Game.CheatResourceBalance("minerals") == mineral + 10, "ordinary cheat survives checkpoint reload");
            Check(_app.Game.CheatResourceBalance("energy_cores") >= 7, "permanent energy credit survives checkpoint reload");
            Check(DeepTechnology.Nodes.All(node => _app.Game.HasResearch(node.S("id"))), "regular technology cheat survives actual checkpoint reload");
            Check(DeepTechnology.ChainBranches.All(branch => _app.Game.SuccessorCount(branch) == 0), "checkpoint reload preserves unpurchased advanced research");

            // A later click must also preserve advances the player explicitly researched.
            Check(_app.Game.AddResourcesCheat("science", 2000) && _app.Game.AddResourcesCheat("alien_points", 20), "fund one manual advanced research in isolated fixture");
            Check(_app.Game.PurchaseGroup("K_R00001"), "player can research a successor after the regular technology cheat");
            var advanced = DeepTechnology.ChainBranches.ToDictionary(branch => branch, branch => _app.Game.SuccessorCount(branch));
            balances = Main.CheatResourceIds.ToDictionary(id => id, id => _app.Game.CheatResourceBalance(id));
            _app.OpenCombatSettings(); await Frames();
            await Action("modal:combat:page:cheat");
            await Action("modal:combat:unlock_all");
            Check(advanced.All(pair => _app.Game.SuccessorCount(pair.Key) == pair.Value), "unlock all neither adds nor erases manually researched advances");
            Check(balances.All(pair => _app.Game.CheatResourceBalance(pair.Key) == pair.Value), "preserving manual advances remains free");
            _app.CloseCombatSettings();
            Check(_app.LoadCheckpoint() && _app.Game.HasResearch("K_R00001") && !_app.Game.HasResearch("K_R00002"), "saved regular unlock preserves exactly the manually researched successor");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout); }
        GD.Print($"RESOURCE_CHEAT_UI_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
