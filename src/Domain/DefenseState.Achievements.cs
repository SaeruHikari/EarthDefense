using System;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public AchievementProfile Achievements { get; } = new();

    /// <summary>Gameplay boundary: only a defeated Earth can earn the first-defeat reward.</summary>
    public DataMap RecordDefeat()
    {
        if (!double.IsFinite(EarthHp) || EarthHp > 0)
            return new() { ["ok"] = false, ["unlocked"] = false, ["duplicate"] = false, ["id"] = AchievementCatalog.FirstDefeatId, ["reason"] = "地球尚未战败" };
        return Achievements.RecordDefeat();
    }

    public int ResearchAchievementBonus(string id, string attribute)
        => DeepTechnology.Definition(id).Map("values").N(attribute) > 0 ? Achievements.PerResearchBonus(attribute) : 0;

    public int LocalShieldAchievementBonus => (int)TechEffects().N("achievement_shield_building_limit_add");

    private void ApplyAchievementResearchEffects(DataMap effects)
    {
        const string attribute = AchievementCatalog.ShieldCapacityAttribute;
        int perNode = Achievements.PerResearchBonus(attribute);
        if (perNode <= 0) return;
        int researchedNodes = DeepTechnology.Nodes.Count(row => HasResearch(row.S("id")) && row.Map("values").N(attribute) > 0);
        int additional = researchedNodes * perNode;
        effects[attribute] = effects.N(attribute) + additional;
        effects["achievement_shield_building_limit_add"] = additional;
    }

    private string AchievementResearchPreview(DataMap definition)
    {
        const string attribute = AchievementCatalog.ShieldCapacityAttribute;
        double baseline = definition.Map("values").N(attribute);
        int bonus = baseline > 0 ? Achievements.PerResearchBonus(attribute) : 0;
        if (bonus <= 0) return "";
        return $"\n永久成就「{AchievementCatalog.Definition(AchievementCatalog.FirstDefeatId).S("name")}」：本项建造上限 +{baseline:0} +{bonus} = +{baseline + bonus:0}";
    }
}
