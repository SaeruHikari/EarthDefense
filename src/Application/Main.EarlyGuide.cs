namespace Earthward.Application;

public partial class Main
{
    private string _localShieldGuideRunId = "";
    private bool _localShieldGuideActive;

    /// <summary>
    /// The first impact starts an unobtrusive onboarding cue. It stays available
    /// until the player researches the free local-shield node, then disappears.
    /// </summary>
    public bool LocalShieldGuidePending => Game != null
        && _localShieldGuideRunId == Game.RunId
        && _localShieldGuideActive
        && !Game.HasResearch("D_N4");

    private void NotifyFirstEarthAttackGuide()
    {
        if (Game == null)
            return;
        if (_localShieldGuideRunId != Game.RunId)
        {
            _localShieldGuideRunId = Game.RunId;
            _localShieldGuideActive = false;
        }
        if (_localShieldGuideActive || Game.HasResearch("D_N4"))
            return;

        _localShieldGuideActive = true;
        ShowNotice("首次受击引导 · 打开右侧「科技」，研究免费中科技「区域护盾工程」；随后在「建设」建造局部护盾发生器（需要 3 个资源核心）");
    }
}
