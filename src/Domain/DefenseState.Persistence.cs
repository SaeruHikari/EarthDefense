using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public DataMap Serialize() => new()
    {
        ["version"] = 1L,
        ["incremental_version"] = 1L,
        ["world_scale_version"] = WorldScaleVersion,
        ["run_id"] = RunId,
        ["defense_reach_stage"] = DefenseReachStage,
        ["minerals"] = Minerals,
        ["energy"] = Energy,
        ["science"] = Science,
        ["earth_hp"] = EarthHp,
        ["shield"] = Shield,
        ["wave"] = Wave,
        ["kills"] = Kills,
        ["score"] = Score,
        ["defense_time"] = DefenseTime,
        ["alien_points"] = AlienPoints,
        ["resource_cores"] = ResourceCores,
        ["completed_waves"] = CompletedWaves,
        ["shield_build_ready_wave"] = _shieldBuildReadyWave,
        ["buildings"] = Buildings.DeepClone(),
        ["combat_settings"] = CombatSettings.DeepClone(),
        ["resource_core_upgrades"] = _resourceUpgrades.DeepClone(),
        ["research_state"] = DeepSave(),
        ["research_runtime"] = RuntimeSave(),
        ["airframe_selection"] = _airframes.DeepClone(),
        ["research_flags"] = _flags.DeepClone(),
        ["expedition"] = Expedition.Serialize()
    };
    private DataMap DeepSave() => new() { ["version"] = 3L, ["nodes"] = DeepResearch.DeepClone(), ["successor_levels"] = _successorLevels.DeepClone() };
    private DataMap RuntimeSave() => new() { ["science_refunds"] = _refunds.Select(row => (object?)row.DeepClone()).ToList(), ["resource_boosts"] = _boosts.Select(row => (object?)row.DeepClone()).ToList(), ["zero_shield_until"] = _zeroShieldUntil, ["zero_shield_ready"] = _zeroShieldReady, ["sacrifice_bucket"] = _sacrificeBucket, ["sacrifice_used"] = _sacrificeUsed, ["rewarded_enemies"] = _rewardedEnemies.Cast<object?>().ToList() };
    private static bool SiteId(string key) => long.TryParse(key, out long id) && id >= 0 && id <= MaxExactInteger && id.ToString(CultureInfo.InvariantCulture) == key;
    private static DataMap? ValidateResourceUpgrades(object? value, DataMap counts)
    {
        if (value is not DataMap map)
            return null;
        var sites = new DataMap();
        foreach (var (key, entry) in map)
        {
            if (!SiteId(key) || entry is not DataMap row || row.Count != 2 || !ResourceFacilityKinds.Contains(row.S("kind")) || !DataMap.ValidNumber(row.Value("level"), 1, MaxExactInteger, true))
                return null;
            string kind = row.S("kind");
            sites[kind] = sites.L(kind) + 1;
            if (sites.L(kind) > counts.L(kind))
                return null;
        }
        return map.DeepClone();
    }
    public bool Restore(DataMap data)
    {
        if (!DataMap.ValidNumber(data.Value("version"), 1, 1, true) || !DataMap.ValidNumber(data.Value("world_scale_version"), WorldScaleVersion, WorldScaleVersion, true) || !DataMap.ValidNumber(data.Value("defense_reach_stage", 0), 0, 3, true))
            return false;
        if (data.Value("run_id") is not string suppliedRunId || suppliedRunId.Length == 0 || suppliedRunId.Length > 96)
            return false;
        if (new[] { "minerals", "energy", "science" }.Any(k => !DataMap.ValidNumber(data.Value(k), 0, ResourceLimit)) || !DataMap.ValidNumber(data.Value("defense_time", 0), 0, DefenseTimeLimit) || !DataMap.ValidNumber(data.Value("earth_hp"), 0, DomainBalance.Value("earth_max_health")))
            return false;
        if (new[] { "wave", "kills", "score" }.Any(k => !DataMap.ValidNumber(data.Value(k), 0, MaxExactInteger, true)) || new[] { "alien_points", "resource_cores" }.Any(k => !DataMap.ValidNumber(data.Value(k, 0), 0, MaxExactInteger, true)) || !DataMap.ValidNumber(data.Value("completed_waves", data.Value("wave")), 0, data.N("wave"), true) || !DataMap.ValidNumber(data.Value("shield_build_ready_wave", 0L), 0, MaxExactInteger, true))
            return false;
        if (data.Value("buildings") is not DataMap savedBuildings)
            return false;
        DataMap? settings = data.Value("combat_settings") is DataMap values ? MigrateCombatSettings(values, WorldScaleVersion) : null;
        if (settings == null)
            return false;
        var buildings = new DataMap();
        foreach (var definition in BuildingDefinitions)
        {
            string key = definition.S("id");
            if (!DataMap.ValidNumber(savedBuildings.Value(key), 0, MaxExactInteger, true))
                return false;
            buildings[key] = savedBuildings.L(key);
        }
        var upgrades = ValidateResourceUpgrades(data.Value("resource_core_upgrades", new DataMap()), buildings);
        if (upgrades == null)
            return false;
        var payload = ValidateResearchPayload(data);
        if (payload == null)
            return false;
        var state = payload.Map("state");
        if (!DataMap.ValidNumber(data.Value("shield"), 0, ResourceLimit))
            return false;
        foreach (string kind in new[] { "laser", "missile" })
            if (buildings.L(kind) > 0 && !state.Map("nodes").ContainsKey(kind == "missile" ? "M_N1" : "L_N1"))
                return false;
        if (buildings.L("shield") > 0 && !state.Map("nodes").ContainsKey("D_N4"))
            return false;
        if (data.Value("expedition") is not DataMap nested || nested.Count == 0)
            return false;
        var expedition = ExpeditionData.Validate(nested);
        if (expedition == null || buildings.L("starship_silo") > 0 && !expedition.Map("research").B("telescope"))
            return false;
        string runId = suppliedRunId;
        if (!FactoryPerks.ResetRunSites(runId))
            return false;
        Minerals = data.N("minerals");
        Energy = data.N("energy");
        Science = data.N("science");
        EarthHp = data.N("earth_hp");
        Shield = data.N("shield");
        Wave = data.L("wave");
        Kills = data.L("kills");
        Score = data.L("score");
        DefenseTime = data.N("defense_time");
        AlienPoints = data.L("alien_points");
        ResourceCores = data.L("resource_cores");
        CompletedWaves = data.L("completed_waves", Wave);
        _shieldBuildReadyWave = data.L("shield_build_ready_wave", 0);
        Buildings = buildings;
        CombatSettings = settings;
        _resourceUpgrades = upgrades;
        Expedition.Restore(expedition);
        DefenseReachStage = data.I("defense_reach_stage");
        RunId = runId;
        ApplyResearchPayload(payload);
        InvalidateFactoryStats();
        Changed?.Invoke();
        return true;
    }
    public bool Reset()
    {
        string id = Guid.NewGuid().ToString("N");
        if (!FactoryPerks.ResetRunSites(id))
            return false;
        RunId = id;
        DefenseReachStage = 0;
        Minerals = DomainBalance.Value("initial_minerals");
        Energy = DomainBalance.Value("initial_energy");
        Science = DomainBalance.Value("initial_science");
        EarthHp = DomainBalance.Value("earth_max_health");
        Shield = 0;
        Wave = 0;
        Kills = 0;
        Score = 0;
        DefenseTime = 0;
        AlienPoints = 0;
        ResourceCores = 0;
        CompletedWaves = 0;
        _shieldBuildReadyWave = 0;
        Buildings = CatalogData.Load("economy.json").Map("initial_buildings").DeepClone();
        CombatSettings = DefaultCombatSettings;
        DeepResearch = new();
        _successorLevels = new();
        _flags = new()
        {
            ["first_medium_boss_defeated"] = false,
            ["missile_intel"] = FactoryPerks.KnowsIntel("M1"),
            ["laser_intel"] = FactoryPerks.KnowsIntel("L1"),
            ["unrestricted_research"] = false
        };
        _airframes = AirframeCatalog.EmptySelection();
        _refunds.Clear();
        _boosts.Clear();
        _resourceUpgrades = new();
        _boostUnits = new();
        _boostSites = new();
        _zeroShieldUntil = 0;
        _zeroShieldReady = 0;
        _sacrificeBucket = -1;
        _sacrificeUsed = 0;
        _rewardedEnemies.Clear();
        Expedition.Reset(false);
        InvalidateFactoryStats();
        Changed?.Invoke();
        return true;
    }
    private static DataMap? ValidateResearchPayload(DataMap data)
    {
        var state = DeepTechnology.Validate(data.Value("research_state"));
        var frames = AirframeCatalog.Validate(data.Value("airframe_selection"));
        if (state == null || frames == null || data.Value("research_flags") is not DataMap flags || flags.Count != 4 || flags.Keys.Any(k => k is not ("first_medium_boss_defeated" or "missile_intel" or "laser_intel" or "unrestricted_research")) || new[] { "first_medium_boss_defeated", "missile_intel", "laser_intel", "unrestricted_research" }.Any(k => flags.Value(k) is not bool))
            return null;
        foreach (string id in state.Map("nodes").Keys)
        {
            if (flags.B("unrestricted_research"))
                continue;
            var gate = DeepTechnology.Definition(id).Map("unlock");
            if (data.I("defense_reach_stage") < gate.I("defense_stage") || data.L("completed_waves") < gate.L("completed_wave") || gate.B("first_medium_boss_defeated") && !flags.B("first_medium_boss_defeated"))
                return null;
        }
        foreach (string field in new[] { "templates", "sites", "berths" })
            foreach (var (key, value) in frames.Map(field))
            {
                string id = field == "templates" ? value as string ?? "" : (value as DataMap)?.S("airframe") ?? "";
                var def = AirframeCatalog.Definition(id);
                string node = def.S("unlock_node");
                if (node.Length == 0 || id == AirframeCatalog.BaseFor(def.S("kind")))
                    continue;
                if (!state.Map("nodes").ContainsKey(node))
                    return null;
            }
        if (data.Value("research_runtime") is not DataMap runtime || runtime.Count != 7 || new[] { "science_refunds", "resource_boosts", "zero_shield_until", "zero_shield_ready", "sacrifice_bucket", "sacrifice_used", "rewarded_enemies" }.Any(k => !runtime.ContainsKey(k)))
            return null;
        if (runtime.Value("science_refunds") is not List<object?> refunds || refunds.Count > 4096 || runtime.Value("resource_boosts") is not List<object?> boosts || boosts.Count > FactoryPerks.MaxSites || runtime.Value("rewarded_enemies") is not List<object?> claimed || claimed.Count > 200000)
            return null;
        if (!DataMap.ValidNumber(runtime.Value("zero_shield_until"), 0, DefenseTimeLimit + 60) || !DataMap.ValidNumber(runtime.Value("zero_shield_ready"), 0, DefenseTimeLimit + 60) || !DataMap.ValidNumber(runtime.Value("sacrifice_bucket"), -1, DefenseTimeLimit, true) || !DataMap.ValidNumber(runtime.Value("sacrifice_used"), 0, ResourceLimit))
            return null;
        double last = -1;
        foreach (var item in refunds)
        {
            if (item is not DataMap row || row.Count != 2 || !DataMap.ValidNumber(row.Value("amount"), 0, ResourceLimit) || !DataMap.ValidNumber(row.Value("due"), 0, DefenseTimeLimit + 60) || row.N("due") < last)
                return null;
            last = row.N("due");
        }
        last = -1;
        var seen = new HashSet<long>();
        foreach (var item in boosts)
        {
            if (item is not DataMap row || row.Count != 3 || !ResourceFacilityKinds.Contains(row.S("kind")) || !DataMap.ValidNumber(row.Value("site"), -1, MaxExactInteger, true) || !DataMap.ValidNumber(row.Value("expires"), 0, DefenseTimeLimit + 60) || row.N("expires") < last)
                return null;
            last = row.N("expires");
            if (row.L("site") >= 0 && !seen.Add(row.L("site")))
                return null;
        }
        if (claimed.Distinct().Count() != claimed.Count || claimed.Any(v => v is not string key || key.Length == 0 || key.Length > 512))
            return null;
        return new()
        {
            ["state"] = state,
            ["runtime"] = runtime.DeepClone(),
            ["airframes"] = frames,
            ["flags"] = flags.DeepClone()
        };
    }
    private void ApplyResearchPayload(DataMap payload)
    {
        var state = payload.Map("state");
        DeepResearch = state.Map("nodes").DeepClone();
        _successorLevels = state.Map("successor_levels").DeepClone();
        _flags = payload.Map("flags").DeepClone();
        _flags["unrestricted_research"] = _flags.B("unrestricted_research");
        _airframes = payload.Map("airframes").DeepClone();
        var runtime = payload.Map("runtime");
        _refunds = runtime.List("science_refunds").OfType<DataMap>().Select(row => row.DeepClone()).ToList();
        _boosts = runtime.List("resource_boosts").OfType<DataMap>().Select(row => row.DeepClone()).ToList();
        _zeroShieldUntil = runtime.N("zero_shield_until");
        _zeroShieldReady = runtime.N("zero_shield_ready");
        _sacrificeBucket = runtime.L("sacrifice_bucket");
        _sacrificeUsed = runtime.N("sacrifice_used");
        _rewardedEnemies = runtime.List("rewarded_enemies").Cast<string>().ToHashSet();
        _techEffects = null;
        RefreshResourceBoosts();
    }
}
