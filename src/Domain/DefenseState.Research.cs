using System;
using System.Collections.Generic;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public bool HasResearch(string id) => DeepTechnology.TryParseSuccessor(id, out string branch, out int rank) ? _successorLevels.L(branch) >= rank : DeepResearch.L(id) > 0;
    public int ResearchLevel(string id) => HasResearch(id) ? 1 : 0;
    public int ResearchCapacityBonus => (int)Math.Clamp(TechEffects().L("capacity_add"), 0, 3);
    private bool WeaponUnlocked(string kind) => kind == "interceptor" || HasResearch(kind == "missile" ? "M_N1" : "L_N1");
    public int FrontierTechnologyLevel() => HasResearch("C_G2") ? 3 : HasResearch("C_A3") ? 2 : HasResearch("C_A2") ? 1 : 0;
    public DataMap TechEffects()
    {
        if (_techEffects != null)
            return _techEffects;
        _techEffects = DeepTechnology.Effects(DeepResearch, _successorLevels);
        ApplyAchievementResearchEffects(_techEffects);
        return _techEffects;
    }
    public List<DataMap> GraphNodes(string focusId = "")
    {
        var result = new List<DataMap>();
        foreach (var source in DeepTechnology.Nodes)
        {
            var row = source.DeepClone();
            row["visible"] = true;
            result.Add(row);
        }
        result.AddRange(SuccessorGraphNodes(focusId));
        return result;
    }
    public DataMap GetGroupStatus(string id)
    {
        var def = DeepTechnology.Definition(id);
        if (def.Count == 0)
            return new();
        bool owned = HasResearch(id);
        var status = new DataMap { ["id"] = id, ["name"] = def.S("name"), ["branch"] = def.S("branch"), ["size"] = def.S("size"), ["alien"] = def.B("alien"), ["tier"] = def.I("tier"), ["level"] = owned ? 1L : 0L, ["max"] = 1L, ["cost"] = def.Map("cost").DeepClone(), ["lock_reason"] = "", ["can_purchase"] = false, ["preview"] = string.Join("\n", def.List("effects").Cast<string>()), ["requires"] = new DataMap(), ["requirement_details"] = new List<object?>() };
        status["preview"] = status.S("preview") + AchievementResearchPreview(def);
        foreach (string parent in def.List("requires").Cast<string>())
        {
            status.Map("requires")[parent] = 1L;
            status.List("requirement_details").Add(new DataMap { ["id"] = parent, ["name"] = DeepTechnology.Definition(parent).S("name", parent), ["required_level"] = 1L, ["current_level"] = ResearchLevel(parent), ["met"] = HasResearch(parent) });
        }
        if (owned)
        {
            status["lock_reason"] = "已研究";
            return status;
        }
        string reason = "";
        var gate = def.Map("unlock");
        if (DefenseReachStage < gate.I("defense_stage"))
            reason = $"需要完成上一层母舰并进入第{gate.I("defense_stage")}次外推";
        else if (CompletedWaves < gate.L("completed_wave"))
            reason = $"需要完成第{gate.L("completed_wave")}波情报";
        else if (gate.B("first_medium_boss_defeated") && !_flags.B("first_medium_boss_defeated"))
            reason = "先击败一只中型Boss";
        else
            foreach (string parent in def.List("requires").Cast<string>())
                if (!HasResearch(parent))
                {
                    reason = "需要研究「" + DeepTechnology.Definition(parent).S("name") + "」";
                    break;
                }
        if (reason.Length == 0 && EarthHp <= 0)
            reason = "本局防线已失守";
        if (reason.Length == 0 && !CanAfford(status.Map("cost")))
            reason = "科研或外星科技点不足";
        status["lock_reason"] = reason;
        status["can_purchase"] = reason.Length == 0;
        return status;
    }
    public string GroupEffectPreview(string id) => GetGroupStatus(id).S("preview");
    public bool PurchaseGroup(string id)
    {
        var status = GetGroupStatus(id);
        if (!status.B("can_purchase"))
            return false;
        string intel = id == "M_N1" ? "M1" : id == "L_N1" ? "L1" : "";
        if (intel.Length > 0 && !FactoryPerks.RememberIntel(intel))
            return false;
        bool refund = HasResearch("I_A2");
        Pay(status.Map("cost"));
        if (DeepTechnology.TryParseSuccessor(id, out string chain, out int rank))
            _successorLevels[chain] = (long)rank;
        else
            DeepResearch[id] = 1L;
        if (id == "M_N1")
            _flags["missile_intel"] = true;
        if (id == "L_N1")
            _flags["laser_intel"] = true;
        if (refund)
            QueueScienceRefund(status.Map("cost").N("science"));
        InvalidateFactoryStats();
        Changed?.Invoke();
        return true;
    }
    private void QueueScienceRefund(double paid)
    {
        if (paid > 0)
            _refunds.Add(new()
            {
                ["amount"] = paid * TechEffects().N("science_refund_fraction"),
                ["due"] = DefenseTime + TechEffects().N("science_refund_delay")
            });
    }
    public bool CanResearch(string id) => DeepTechnology.Has(id) && GetGroupStatus(id).B("can_purchase");
    public bool Research(string id) => DeepTechnology.Has(id) && PurchaseGroup(id);
    public List<DataMap> AvailableAirframes(string kind)
    {
        var list = new List<DataMap>();
        foreach (var source in AirframeCatalog.Definitions.Where(row => row.S("kind") == kind))
        {
            var row = source.DeepClone();
            bool unlocked = AirframeUnlocked(row.S("id"));
            row["unlocked"] = unlocked;
            row["known"] = FactoryPerks.KnowsIntel(row.S("id")) || unlocked;
            row["lock_reason"] = unlocked ? "" : "需要研究「" + DeepTechnology.Definition(row.S("unlock_node")).S("name", row.S("unlock_node")) + "」";
            list.Add(row);
        }
        return list;
    }
    private bool AirframeUnlocked(string id)
    {
        var def = AirframeCatalog.Definition(id);
        if (def.Count == 0)
            return false;
        if (def.S("unlock_node").Length == 0)
            return true;
        return id is "M1" or "L1" ? WeaponUnlocked(def.S("kind")) : HasResearch(def.S("unlock_node"));
    }
    public string FactoryAirframe(string kind, long site = -1, int berth = -1)
    {
        string id = _airframes.Map("templates").S(kind, AirframeCatalog.BaseFor(kind));
        var row = _airframes.Map("sites").Map(site.ToString());
        if (site >= 0 && row.S("kind") == kind)
            id = row.S("airframe");
        row = _airframes.Map("berths").Map(site + ":" + berth);
        if (site >= 0 && berth >= 0 && row.S("kind") == kind)
            id = row.S("airframe");
        return AirframeUnlocked(id) ? id : AirframeCatalog.BaseFor(kind);
    }
    public string AirframeFor(string kind, long site = -1, int berth = -1) => FactoryAirframe(kind, site, berth);
    public int AirframeCapacityCost(string id) => AirframeCatalog.Definition(id).I("capacity_cost", 1);
    public string GetAirframeLockReason(string kind, long site, int berth, string id)
    {
        if (!AirframeCatalog.Compatible(id, kind))
            return "机型不属于当前工厂";
        if (site < -1 || berth < -1 || (site < 0 && berth >= 0) || berth > 32767)
            return "无效的工厂或编制编号";
        if (!AirframeUnlocked(id))
            return "请先研究该机型";
        if (site >= 0 && berth >= 0)
        {
            if (berth + AirframeCapacityCost(id) > FactoryCapacityForSite(kind, site))
                return "该编制位置剩余点数不足以容纳此机型";
            foreach (var row in FactoryAirframePlan(kind, site))
                if (row.I("berth") < berth && berth < row.I("berth") + row.I("capacity_cost"))
                    return "该点位已由前一架重型战机占用";
        }
        return "";
    }
    public bool SetAirframe(string kind, long site, int berth, string id)
    {
        if (GetAirframeLockReason(kind, site, berth, id).Length > 0 || !FactoryPerks.ReconcileAirframe(kind, site, berth, id))
            return false;
        if (site < 0)
            _airframes.Map("templates")[kind] = id;
        else
            _airframes.Map(berth < 0 ? "sites" : "berths")[berth < 0 ? site.ToString() : site + ":" + berth] = new DataMap { ["kind"] = kind, ["airframe"] = id };
        InvalidateFactoryStats();
        Changed?.Invoke();
        return true;
    }
    public List<DataMap> FactoryAirframePlan(string kind, long site = -1)
    {
        var list = new List<DataMap>();
        int available = Math.Min(32768, FactoryCapacityForSite(kind, site));
        for (int berth = 0; berth < available;)
        {
            string id = FactoryAirframe(kind, site, berth);
            int cost = AirframeCapacityCost(id);
            list.Add(new()
            {
                ["berth"] = berth,
                ["airframe_id"] = id,
                ["capacity_cost"] = cost,
                ["enabled"] = berth + cost <= available,
                ["point_offset"] = berth
            });
            berth += cost;
        }
        return list;
    }
    public int FactoryPlannedAircraftCount(string kind, long site = -1) => FactoryAirframePlan(kind, site).Count(row => row.B("enabled"));
    public int FactoryProductionLanes(string kind, long site = -1) => TechEffects().I("production_lanes", 1);
}
