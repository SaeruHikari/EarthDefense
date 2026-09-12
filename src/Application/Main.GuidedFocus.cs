using Godot;

namespace Earthward.Application;

public partial class Main
{
    private long _guidedFocusRevision;

    /// <summary>
    /// Reusable onboarding entry point. Freeze the world immediately, then open
    /// a UI and focus its target after the triggering simulation event completes.
    /// Camera animation and UI highlight remain presentation callbacks so this
    /// can target research, construction, satellite controls or later features.
    /// The player always resumes explicitly; this never schedules an auto-resume.
    /// </summary>
    public bool PauseAndFocusUi(string message, Action revealUi, Action focusUi,
        Action? animateCamera = null, Func<bool>? isRelevant = null)
    {
        ArgumentNullException.ThrowIfNull(revealUi);
        ArgumentNullException.ThrowIfNull(focusUi);
        if (Game == null || Defeated || Game.EarthHp <= 0 || isRelevant?.Invoke() == false)
            return false;

        UserPaused = true;
        Battle.Paused = true;
        Campaign.Paused = true;
        Planet.Paused = true;
        ShowNotice(message);
        string run = Game.RunId;
        long request = ++_guidedFocusRevision;

        Callable.From(() =>
        {
            // A newer guide, restart, defeat or completed objective invalidates
            // queued presentation work; none may steal focus from the new state.
            if (!IsInstanceValid(this) || !IsInsideTree() || Game.RunId != run
                || request != _guidedFocusRevision || Defeated || Game.EarthHp <= 0
                || isRelevant?.Invoke() == false)
                return;
            Dragging = false;
            _middleDragging = false;
            CancelNavigationPress();
            _alertPress = "";
            ClearFactoryCoverage();
            CancelBuildSelection();
            CancelResourceUpgrade();
            ExitSpectator();
            SolarNavOpen = false;
            animateCamera?.Invoke();
            // World navigation may close the previous dock. Reveal the target
            // UI afterwards so navigation cannot immediately hide the guide.
            revealUi();
            focusUi();
            ShowNotice(message);
        }).CallDeferred();
        return true;
    }
}
