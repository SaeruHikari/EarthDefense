using Godot;
using Earthward.Application;
using Earthward.Domain;
namespace Earthward.Tests;

/// <summary>Real Main timer/persistence/retry integration. Every file is confined to an isolated test profile.</summary>
public partial class PlayTimeChecks : Node
{
    private Main _app = null!;
    private int _checks, _failures;
    private void Check(bool okay, string label)
    {
        _checks++;
        if (!okay) { _failures++; GD.PrintErr("PLAY_TIME_FAIL: " + label); }
    }
    private static bool Near(double a, double b) => Math.Abs(a - b) < .000001;
    private static string SaveFile => ProjectSettings.GlobalizePath(Main.SavePath);
    private async Task Frames(int count = 3)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task NewMain()
    {
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); }
        _app = new Main(); AddChild(_app);
        _app.SetProcess(false); _app.Planet.SetProcess(false);
        _app.UserPaused = true; _app.Sounds.Muted = true;
        await Frames(4);
    }
    private void Step(double realDelta) => _app._Process(realDelta);
    public override async void _Ready()
    {
        try
        {
            if (!ProjectSettings.GlobalizePath("user://").Contains("runtime-tests", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Play-time tests require an isolated runtime-tests profile.");
            GetWindow().Size = new(1440, 900);
            await NewMain();
            Check(Near(_app.PlayTimeSeconds, 0) && !_app.PlayTimeEstimated && _app.PlayTimeText == "00:00:00", "new clock starts exact and empty");
            _app.UserPaused = false; Step(.5);
            Check(Near(_app.PlayTimeSeconds, 0), "preparation before first start does not accumulate play time");
            _app.StartWave();
            Check(_app.Started && _app.HasWaveStartRetry, "real first-wave hook captures retry before advancing time");
            _app.Speed = 1; Step(1);
            Check(Near(_app.PlayTimeSeconds, 1), "one real second at normal speed counts once");
            double simulation = _app.Game.DefenseTime;
            _app.Speed = 2; Step(1);
            Check(Near(_app.PlayTimeSeconds, 2), "double speed still adds one real second");
            Check(Near(_app.Game.DefenseTime - simulation, 2), "simulation actually advances twice as fast in same timer test");
            _app.UserPaused = true; Step(2);
            Check(Near(_app.PlayTimeSeconds, 2), "user pause stops the timer");
            _app.UserPaused = false; _app.Modal = "help"; Step(.75);
            Check(Near(_app.PlayTimeSeconds, 2), "pausing help modal stops the timer");
            _app.Modal = ""; _app.Defeated = true; Step(.5);
            Check(Near(_app.PlayTimeSeconds, 2), "defeat screen does not inflate active play time");
            _app.Defeated = false; _app.Speed = 1; Step(.25);
            Check(Near(_app.PlayTimeSeconds, 2.25), "resuming continues fractional real time");
            string run = _app.Game.RunId;
            Check(_app.SaveCheckpoint(), "real synchronous checkpoint saves timer");
            var recorded = DataMap.Parse(System.IO.File.ReadAllText(SaveFile));
            Check(Near(recorded.N("play_time_seconds"), 2.25) && !recorded.B("play_time_estimated"), "checkpoint stores exact wall-play seconds and provenance");
            await NewMain();
            Check(_app.LoadCheckpoint(), "fresh Main reads the actual saved checkpoint");
            Check(_app.Game.RunId == run && Near(_app.PlayTimeSeconds, 2.25) && !_app.PlayTimeEstimated, "same run and exact clock survive scene recreation");
            _app.UserPaused = false; _app.Speed = 1; Step(.75);
            Check(Near(_app.PlayTimeSeconds, 3), "loaded timer continues from saved duration");
            double beforeRetry = _app.PlayTimeSeconds;
            Check(_app.RetryWaveStart(), "real retry restores first-wave combat state");
            Check(_app.Game.RunId == run && Near(_app.PlayTimeSeconds, beforeRetry), "same-run retry keeps all time spent on failed attempt");
            Check(_app.UserPaused && Near(_app.Battle.GetWaveSpawnPlan().N("cycle_elapsed"), 0), "retry still restores exact paused wave opening");
            Step(1);
            Check(Near(_app.PlayTimeSeconds, beforeRetry), "redeployment pause after retry does not count");
            _app.UserPaused = false; Step(.5);
            Check(Near(_app.PlayTimeSeconds, beforeRetry + .5), "second attempt extends existing real time");
            Check(_app.RetryWaveStart() && Near(_app.PlayTimeSeconds, beforeRetry + .5), "repeated retries cannot roll the timer back");
            Check(_app.SaveCheckpoint(), "post-retry timer persists in ordinary save");
            var invalid = DataMap.Parse(System.IO.File.ReadAllText(SaveFile)); invalid["play_time_seconds"] = -1d;
            Check(!_app.ValidateCheckpoint(invalid), "negative timer is rejected at checkpoint boundary");
            invalid["play_time_seconds"] = double.NaN;
            Check(!_app.ValidateCheckpoint(invalid), "non-finite timer is rejected at checkpoint boundary");
            invalid["play_time_seconds"] = 5d; invalid["play_time_estimated"] = "true";
            Check(!_app.ValidateCheckpoint(invalid), "invalid provenance type is rejected");
            string previousRun = _app.Game.RunId;
            Check(_app.Restart(), "real new-run action succeeds");
            Check(_app.Game.RunId != previousRun && Near(_app.PlayTimeSeconds, 0) && !_app.PlayTimeEstimated && _app.PlayTimeText == "00:00:00", "new run clears accumulated time and estimate flag");
            Step(.5);
            Check(Near(_app.PlayTimeSeconds, 0), "new run remains zero before its first wave");
        }
        catch (Exception error) { Check(false, error.ToString()); }
        if (IsInstanceValid(_app)) { _app.QueueFree(); await Frames(4); }
        GD.Print($"PLAY_TIME_CHECKS: {_checks} checks / {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
