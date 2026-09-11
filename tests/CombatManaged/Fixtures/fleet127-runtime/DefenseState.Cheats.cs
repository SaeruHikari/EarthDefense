namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public string LastCheatError { get; private set; } = "";
    public double CheatResourceBalance(string resource) => resource switch
    {
        "minerals" => Minerals, "energy" => Energy, "science" => Science,
        "resource_cores" => ResourceCores, "alien_points" => AlienPoints,
        "energy_cores" => FactoryPerks.EnergyCores, _ => double.NaN
    };

    /// <summary>Explicit development grant; adds an amount without replacing earned balances.</summary>
    public bool AddResourcesCheat(string resource, double amount, Func<bool>? persistCurrentRun = null)
    {
        LastCheatError = "";
        double current = CheatResourceBalance(resource);
        bool integral = resource is "resource_cores" or "alien_points" or "energy_cores";
        double limit = integral ? MaxExactInteger : ResourceLimit;
        if (!double.IsFinite(current)) return CheatFailed("请选择有效的资源");
        if (!double.IsFinite(amount) || amount <= 0) return CheatFailed("请输入大于 0 的有限数量");
        if (integral && (amount != Math.Floor(amount) || amount > MaxExactInteger))
            return CheatFailed("核心与外星科技点需要输入有效的整数");
        if (current < 0 || current > limit || amount > limit - current)
            return CheatFailed("增加后会超出资源上限，请减少数量");
        double next = current + amount;
        if (!double.IsFinite(next) || next <= current || next > limit)
            return CheatFailed("该增量无法准确加入当前余额，请调整数量");
        if (resource == "energy_cores")
        {
            if (!FactoryPerks.CreditEnergyCores((long)amount))
                return CheatFailed(FactoryPerks.LastError);
            return true; // Permanent currency is atomically saved in its separate profile.
        }

        SetCheatBalance(resource, next);
        try
        {
            if (persistCurrentRun != null && !persistCurrentRun())
            {
                SetCheatBalance(resource, current);
                return CheatFailed("保存未完成，资源余额已恢复");
            }
        }
        catch (Exception)
        {
            SetCheatBalance(resource, current);
            return CheatFailed("保存未完成，资源余额已恢复");
        }
        Changed?.Invoke();
        return true;
    }
    private void SetCheatBalance(string resource, double value)
    {
        switch (resource)
        {
            case "minerals": Minerals = value; break;
            case "energy": Energy = value; break;
            case "science": Science = value; break;
            case "resource_cores": ResourceCores = (long)value; break;
            case "alien_points": AlienPoints = (long)value; break;
        }
    }
    private bool CheatFailed(string error) { LastCheatError = error; return false; }
}
