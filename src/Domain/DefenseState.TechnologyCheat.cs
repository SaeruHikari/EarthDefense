namespace Earthward.Domain;

public sealed partial class DefenseState
{
    /// <summary>Completes the fixed technology tree only. Existing continuation research is never modified.</summary>
    public bool UnlockAllTechnologyCheat(Func<bool>? persistCurrentRun = null)
    {
        LastCheatError = "";
        var previousNodes = DeepResearch.DeepClone();
        var previousFlags = _flags.DeepClone();
        foreach (var node in DeepTechnology.Nodes) DeepResearch[node.S("id")] = 1L;
        _flags["missile_intel"] = true;
        _flags["laser_intel"] = true;
        _flags["unrestricted_research"] = true;
        InvalidateFactoryStats();
        try
        {
            if (persistCurrentRun != null && !persistCurrentRun())
            {
                DeepResearch = previousNodes; _flags = previousFlags;
                InvalidateFactoryStats();
                return CheatFailed("保存未完成，科技状态已恢复");
            }
        }
        catch (Exception)
        {
            DeepResearch = previousNodes; _flags = previousFlags;
            InvalidateFactoryStats();
            return CheatFailed("保存未完成，科技状态已恢复");
        }
        Changed?.Invoke();
        return true;
    }
}
