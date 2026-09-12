using Godot;

namespace Earthward.Application;

public partial class Main
{
    private string _localShieldGuideRunId = "";
    private bool _localShieldGuideActive;
    private const string LocalShieldGuideTechnology = "D_N4";

    /// <summary>
    /// The first real hull impact pauses combat and reveals the free shield
    /// research. The player resumes explicitly after choosing their defenses.
    /// </summary>
    public bool LocalShieldGuidePending => Game != null
        && _localShieldGuideRunId == Game.RunId
        && _localShieldGuideActive
        && !Game.HasResearch(LocalShieldGuideTechnology);

    private void NotifyFirstEarthAttackGuide(Vector3 worldImpact)
    {
        if (Game == null || Game.EarthHp <= 0 || Defeated || !worldImpact.IsFinite() || worldImpact.LengthSquared() < .001f)
            return;
        if (_localShieldGuideRunId != Game.RunId)
        {
            _localShieldGuideRunId = Game.RunId;
            _localShieldGuideActive = false;
        }
        if (_localShieldGuideActive || Game.HasResearch(LocalShieldGuideTechnology))
            return;

        _localShieldGuideActive = true;
        Vector3 localImpact = Planet.Globe.ToLocal(worldImpact).Normalized();
        PauseAndFocusUi(
            "地球首次受击 · 已暂停 · 点击红色节点，免费研究「区域护盾工程」",
            revealUi: () => { _researchWindowFocus = ""; OpenResearchSidebar(); },
            focusUi: () => ResearchGraph.FocusNode(LocalShieldGuideTechnology, center: true),
            animateCamera: () =>
            {
                // Keep the impact in the unobscured part of the full viewport.
                float visibleRight = ResearchTargetRect().Position.X - 20;
                float screenFraction = Mathf.Clamp(visibleRight * .5f / WorldSize.X, .15f, .5f);
                Planet.RotateEarthToDirection(localImpact, screenFraction, 1.4);
            },
            isRelevant: () => LocalShieldGuidePending);
    }

    private void UpdateLocalShieldGuide()
    {
        if (IsInstanceValid(ResearchGraph))
            ResearchGraph.SetTutorialHighlight(LocalShieldGuidePending && !Defeated ? LocalShieldGuideTechnology : "");
    }
}
