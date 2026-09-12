using Godot;
using System.Reflection;
using Earthward.Application;
using Earthward.Combat;
using Earthward.Domain;

namespace Earthward.Tests;

/// <summary>First-defeat experience through the real combat, persistent profile, and HUD paths.</summary>
public partial class AchievementUiChecks : Node
{
    private const string AchievementId = "first_defeat";
    private Main _app = null!;
    private int _checks, _failures;

    private void Check(bool value, string label)
    {
        _checks++;
        if (value) return;
        _failures++;
        GD.PrintErr("ACHIEVEMENT_UI_FAIL: " + label);
    }

    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Click(Vector2 point)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left, Pressed = true
        });
        await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false
        });
        await Frames(3);
    }

    private Rect2 ButtonRect(string action) => _app.Buttons.First(row => row.S("action") == action).Get<Rect2>("rect");

    private async Task ClickAction(string action) => await Click(ButtonRect(action).GetCenter());

    private async Task KeyPress(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
        await Frames(3);
    }

    private async Task Capture(string name)
    {
        await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://artifacts/" + name + ".png")) == Error.Ok,
            "capture " + name);
    }

    private async Task NewMain()
    {
        if (IsInstanceValid(_app))
        {
            _app.PreserveCheckpoint = true;
            _app.QueueFree();
            await Frames(5);
        }
        _app = new Main();
        AddChild(_app);
        _app.PreserveCheckpoint = true;
        _app.Sounds.Muted = true;
        await Frames(7);
    }

    private void DestroyEarth()
    {
        var method = typeof(Battlefield).GetMethod("DamageEarth", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(Battlefield), "DamageEarth");
        Vector3 impact = _app.Planet.Camera.GlobalPosition.Normalized() * WorldScale.EarthRadius;
        method.Invoke(_app.Battle, [_app.Game.EarthHp + 100, impact, true]);
    }

    private async Task ClickResearch(string id)
    {
        _app.OpenResearchSidebar();
        _app.FocusResearchWindow(id);
        await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
        await Frames(5);
        var graph = _app.ResearchGraph;
        graph.FocusNode(id, center: true);
        await Frames(3);
        await Click(graph.Canvas.GlobalPosition + graph.Point(graph.Node(id)));
    }

    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Achievement UI requires an isolated runtime-tests profile.");
            GetWindow().Size = new Vector2I(1440, 900);
            await NewMain();
            Check(_app.Restart(), "fixture starts a clean deployment through actual application reset");
            _app.PreserveCheckpoint = true;
            _app.Action("tab:build");
            _app.UserPaused = false;
            await Frames(5);
            Check(!_app.Game.Achievements.HasUnlocked(AchievementId), "fresh profile has not earned first-defeat achievement");
            Rect2 achievementButton = ButtonRect("achievements");
            Rect2 settingsButton = ButtonRect("combat");
            Check(achievementButton.Size == settingsButton.Size
                && Math.Abs(achievementButton.Position.Y - settingsButton.Position.Y) < 1
                && Math.Abs(achievementButton.GetCenter().X - settingsButton.GetCenter().X) <= settingsButton.Size.X + 18,
                "achievement entry uses the same top-right button size and sits beside settings");
            await ClickAction("achievements");
            Check(_app.Modal == "achievements" && !_app.UserPaused && _app.Battle.Paused && _app.Planet.Paused,
                "real medal click opens the list and pauses simulation without changing user pause state");
            Check(!_app.Buttons.Any(row => row.S("action").Contains("claim") || row.S("action").Contains("achievement:upgrade")),
                "achievement list has no manual claim or upgrade transaction");
            await Capture("achievement-locked-ui");
            await KeyPress(Key.Escape);
            Check(_app.Modal == "" && !_app.UserPaused, "Escape closes achievements and restores unpaused command mode");

            _app.UserPaused = true;
            await Frames();
            await ClickAction("achievements");
            await ClickAction("modal:achievements:close");
            Check(_app.Modal == "" && _app.UserPaused && _app.Battle.Paused && _app.Planet.Paused,
                "close button preserves a pause that existed before opening the list");
            _app.UserPaused = false;
            _app.StartWave();
            _app.PreserveCheckpoint = true;
            await Frames(3);
            Check(_app.Started && _app.HasWaveStartRetry && !_app.UserPaused, "real wave start provides a pre-achievement retry checkpoint");
            await ClickAction("achievements");
            double clock = _app.Battle.Clock;
            await Frames(6);
            Check(_app.Battle.Clock == clock && _app.Battle.Paused && _app.Campaign.Paused,
                "combat and wave scheduling stay frozen while browsing achievements");
            await KeyPress(Key.Space);
            Check(_app.Modal == "achievements" && !_app.UserPaused && _app.Battle.Clock == clock,
                "Space inside achievement modal cannot alter underlying pause choice or advance combat");
            await KeyPress(Key.Escape);
            Check(_app.Modal == "" && !_app.UserPaused && !_app.Battle.Paused,
                "closing achievements resumes a previously running battle");

            DestroyEarth();
            await Frames(5);
            Check(_app.Game.EarthHp <= 0 && _app.Defeated && _app.Modal == "defeat",
                "real combat damage reaches the application's defeat handler");
            Check(_app.Game.Achievements.HasUnlocked(AchievementId)
                && System.IO.File.Exists(ProjectSettings.GlobalizePath("user://earthward_achievements.json")),
                "real EarthDestroyed event grants and immediately persists first-defeat experience");
            await ClickAction("achievements");
            Check(_app.Modal == "achievements" && _app.Defeated,
                "top-right achievement entry remains actually clickable on defeat screen");
            string defeatedRun = _app.Game.RunId;
            await ClickAction("modal:restart");
            Check(_app.Modal == "achievements" && _app.Defeated && _app.Game.RunId == defeatedRun,
                "achievement modal blocks clicks through to the underlying redeployment button");
            await Capture("achievement-unlocked-ui");
            await KeyPress(Key.Escape);
            Check(_app.Modal == "defeat" && _app.Defeated && _app.Battle.Paused,
                "Escape from earned list returns to defeat screen without bypassing defeat");
            await ClickAction("achievements");
            await ClickAction("modal:achievements:close");
            Check(_app.Modal == "defeat" && _app.Defeated,
                "list close button also restores the underlying defeat screen");

            await ClickAction("modal:retry_wave");
            Check(!_app.Defeated && _app.Modal == "" && _app.UserPaused && _app.Game.EarthHp > 0,
                "real retry button restores a living paused wave opening");
            Check(_app.Game.Achievements.HasUnlocked(AchievementId) && !_app.Game.HasResearch("D_N4")
                && _app.Game.LocalShieldBuildLimit == 0,
                "wave-start restore keeps permanent experience but still requires local-shield research");
            await ClickResearch("D_N4");
            Check(_app.Game.HasResearch("D_N4") && _app.Game.LocalShieldBuildLimit == 2,
                "native first shield-research click grants the promised one plus one capacity");
            _app.Game.Science = 10000;
            foreach (string prerequisite in new[] { "D_S01", "D_S21", "D_S02", "D_S22" })
                Check(_app.Game.PurchaseGroup(prerequisite), "actual defense prerequisite " + prerequisite);
            await ClickResearch("D_N1");
            Check(_app.Game.HasResearch("D_N1") && _app.Game.LocalShieldBuildLimit == 4,
                "native next capacity research again contributes its base one plus achievement one");

            string priorRun = _app.Game.RunId;
            Check(_app.Restart() && _app.Game.RunId != priorRun, "real redeployment creates a new run");
            _app.PreserveCheckpoint = true;
            _app.UserPaused = true;
            await Frames(5);
            Check(_app.Game.Achievements.HasUnlocked(AchievementId) && !_app.Game.HasResearch("D_N4")
                && _app.Game.LocalShieldBuildLimit == 0, "redeployment resets technology without erasing permanent experience");
            await ClickResearch("D_N4");
            Check(_app.Game.LocalShieldBuildLimit == 2, "next run native research still grants exactly two slots");
            await NewMain();
            Check(_app.Game.Achievements.HasUnlocked(AchievementId), "new application instance reloads permanent achievement from disk");
        }
        catch (Exception error)
        {
            Check(false, error.ToString());
        }
        if (IsInstanceValid(_app))
        {
            _app.PreserveCheckpoint = true;
            _app.QueueFree();
            await Frames(5);
        }
        GD.Print($"ACHIEVEMENT_UI_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
