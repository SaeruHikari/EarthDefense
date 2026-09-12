using Earthward.Domain;
using Earthward.Presentation;
using Godot;

namespace Earthward.Application;

public partial class Main
{
    public double PlayTimeSeconds { get; private set; }
    public bool PlayTimeEstimated { get; private set; }
    private string _playTimeRunId = "";
    public string PlayTimeText
    {
        get
        {
            long seconds = (long)Math.Floor(Math.Max(0, PlayTimeSeconds));
            return $"{(PlayTimeEstimated ? "≈" : "")}{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        }
    }
    private void AdvancePlayTime(double delta)
    {
        if (_playTimeRunId != Game.RunId) ResetPlayTime();
        if (Started && !WorldIsPaused() && double.IsFinite(delta) && delta > 0)
            PlayTimeSeconds = Math.Min(DefenseState.MaxExactInteger, PlayTimeSeconds + delta);
    }
    private void ResetPlayTime()
    {
        PlayTimeSeconds = 0;
        PlayTimeEstimated = false;
        _playTimeRunId = Game.RunId;
    }
    private void RestorePlayTime(DataMap checkpoint, bool retry, string previousRun, double previousTime, bool previousEstimated)
    {
        double saved = checkpoint.N("play_time_seconds");
        bool estimated = checkpoint.B("play_time_estimated");
        if (retry && previousRun == Game.RunId && previousTime > saved)
        {
            saved = previousTime;
            estimated |= previousEstimated;
        }
        PlayTimeSeconds = Math.Max(0, saved);
        PlayTimeEstimated = estimated;
        _playTimeRunId = Game.RunId;
    }
    private void DrawPlayTime()
    {
        var rect = new Rect2(20, WorldSize.Y - 106, 226, 35);
        FloatingPanel(rect, .88f);
        var center = rect.Position + new Vector2(17, 17);
        DrawArc(center, 6, 0, Mathf.Tau, 24, UiTheme.Alpha(UiTheme.Mint, .85f), 1.2f, true);
        DrawLine(center, center + new Vector2(0, -3.5f), UiTheme.Mint, 1.2f, true);
        DrawLine(center, center + new Vector2(3, 1.5f), UiTheme.Mint, 1.2f, true);
        Text("本局游玩", rect.Position + new Vector2(32, 22), 11, UiTheme.Muted);
        Text(PlayTimeText, rect.Position + new Vector2(99, 23), 15, UiTheme.Mint);
        Hint(rect, "实际游玩时间 · 暂停时停表 · 倍速不影响计时\n读档继续累计，重试波次保留已经花掉的时间。");
    }
}
