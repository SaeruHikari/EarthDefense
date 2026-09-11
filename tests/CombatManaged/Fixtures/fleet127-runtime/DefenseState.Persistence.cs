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
        ["buildings"] = Buildings.DeepClone(),
        ["tech"] = Tech.DeepClone(),
        ["combat_settings"] = CombatSettings.DeepClone(),
        ["resource_core_upgrades"] = _resourceUpgrades.DeepClone(),
        ["research_state"] = DeepSave(),
        ["research_runtime"] = RuntimeSave(),
        ["airframe_selection"] = _airframes.DeepClone(),
        ["research_flags"] = _flags.DeepClone(),
        ["expedition"] = Expedition.Serialize()
    };
    private DataMap DeepSave() => new() { ["version"] = 2L, ["nodes"] = DeepResearch.DeepClone(), ["credited"] = _credited.Cast<object?>().ToList(), ["successor_levels"] = _successorLevels.DeepClone() };
    private DataMap RuntimeSave() => new() { ["science_refunds"] = _refunds.Select(row => (object?)row.DeepClone()).ToList(), ["resource_boosts"] = _boosts.Select(row => (object?)row.DeepClone()).ToList(), ["zero_shield_until"] = _zeroShieldUntil, ["zero_shield_ready"] = _zeroShieldReady, ["sacrifice_bucket"] = _sacrificeBucket, ["sacrifice_used"] = _sacrificeUsed, ["rewarded_enemies"] = _rewardedEnemies.Cast<object?>().ToList() };
    private static DataMap BlankRuntime() => new() { ["science_refunds"] = new List<object?>(), ["resource_boosts"] = new List<object?>(), ["zero_shield_until"] = 0d, ["zero_shield_ready"] = 0d, ["sacrifice_bucket"] = -1L, ["sacrifice_used"] = 0d, ["rewarded_enemies"] = new List<object?>() };
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
        if (!DataMap.ValidNumber(data.Value("version"), 1, 1, true) || !DataMap.ValidNumber(data.Value("world_scale_version", 1), 1, WorldScaleVersion, true) || !DataMap.ValidNumber(data.Value("defense_reach_stage", 0), 0, 3, true))
            return false;
        if (data.ContainsKey("run_id") && (data.Value("run_id") is not string id || id.Length == 0 || id.Length > 96))
            return false;
        if (new[] { "minerals", "energy", "science" }.Any(k => !DataMap.ValidNumber(data.Value(k), 0, ResourceLimit)) || !DataMap.ValidNumber(data.Value("defense_time", 0), 0, DefenseTimeLimit) || !DataMap.ValidNumber(data.Value("earth_hp"), 0, 100))
            return false;
        if (new[] { "wave", "kills", "score" }.Any(k => !DataMap.ValidNumber(data.Value(k), 0, MaxExactInteger, true)) || new[] { "alien_points", "resource_cores" }.Any(k => !DataMap.ValidNumber(data.Value(k, 0), 0, MaxExactInteger, true)) || !DataMap.ValidNumber(data.Value("completed_waves", data.Value("wave")), 0, data.N("wave"), true))
            return false;
        if (data.Value("buildings") is not DataMap savedBuildings || data.Value("tech") is not DataMap savedTech || savedTech.Keys.Any(key => !CatalogData.Load("legacy-technology.json").Map("initial_levels").ContainsKey(key)))
            return false;
        DataMap? settings = data.ContainsKey("combat_settings") ? data.Value("combat_settings") is DataMap values ? MigrateCombatSettings(values, data.I("world_scale_version", 1)) : null : DefaultCombatSettings;
        if (settings == null)
            return false;
        var buildings = new DataMap();
        foreach (var definition in BuildingDefinitions)
        {
            string key = definition.S("id");
            if (key is "starship_silo" or "shield" && !savedBuildings.ContainsKey(key))
            {
                buildings[key] = 0L;
                continue;
            }
            if (!DataMap.ValidNumber(savedBuildings.Value(key), 0, MaxExactInteger, true))
                return false;
            buildings[key] = savedBuildings.L(key);
        }
        var upgrades = ValidateResourceUpgrades(data.Value("resource_core_upgrades", new DataMap()), buildings);
        if (upgrades == null)
            return false;
        var tech = new DataMap();
        foreach (var definition in LegacyTechnologies)
        {
            string key = definition.S("id");
            if (!savedTech.ContainsKey(key) && !LegacyTechIds.Contains(key))
            {
                tech[key] = 0L;
                continue;
            }
            if (!DataMap.ValidNumber(savedTech.Value(key), 0, definition.N("legacy_max", definition.N("max")), true))
                return false;
            tech[key] = Math.Min(definition.L("max"), savedTech.L(key));
        }
        var payload = ValidateResearchPayload(data, tech);
        if (payload == null)
            return false;
        var state = payload.Map("state");
        var effects = DeepTechnology.Effects(state.Map("nodes"), state.List("credited").Cast<string>().ToHashSet(), state.Map("successor_levels"));
        if (!DataMap.ValidNumber(data.Value("shield"), 0, ResourceLimit))
            return false;
        foreach (string kind in new[] { "laser", "missile" })
            if (buildings.L(kind) > 0 && tech.L(kind) == 0 && !state.Map("nodes").ContainsKey(kind == "missile" ? "M_N1" : "L_N1"))
                return false;
        foreach (var definition in LegacyTechnologies)
            if (tech.L(definition.S("id")) > 0 && !RequirementsMet(tech, definition.Map("requires")) && (!definition.ContainsKey("legacy_requires") || !RequirementsMet(tech, definition.Map("legacy_requires"))))
                return false;
        if (buildings.L("shield") > 0 && !state.Map("nodes").ContainsKey("D_N4"))
            return false;
        if (data.ContainsKey("expedition") && (data.Value("expedition") is not DataMap nested || nested.Count == 0))
            return false;
        var expedition = ExpeditionData.Validate(data.Value("expedition", new DataMap()));
        if (expedition == null || buildings.L("starship_silo") > 0 && !expedition.Map("research").B("telescope"))
            return false;
        string runId = data.S("run_id", "legacy-" + FactoryPerks.Hash(LegacyJson.Stringify(data)));
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
        Buildings = buildings;
        Tech = tech;
        CombatSettings = settings;
        if (!data.ContainsKey("research_state") && Math.Abs(CombatSettings.N("enemy_wave_duration") - 30) < .00001)
            CombatSettings["enemy_wave_duration"] = 45d;
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
        Minerals = 320;
        Energy = 180;
        Science = 80;
        EarthHp = 100;
        Shield = 0;
        Wave = 0;
        Kills = 0;
        Score = 0;
        DefenseTime = 0;
        AlienPoints = 0;
        ResourceCores = 0;
        CompletedWaves = 0;
        Buildings = CatalogData.Load("economy.json").Map("initial_buildings").DeepClone();
        Tech = CatalogData.Load("legacy-technology.json").Map("initial_levels").DeepClone();
        CombatSettings = DefaultCombatSettings;
        DeepResearch = new();
        _credited.Clear();
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
    private static DataMap? ValidateResearchPayload(DataMap data, DataMap legacy)
    {
        string[] fields = { "research_state", "research_runtime", "airframe_selection", "research_flags" };
        int present = fields.Count(data.ContainsKey);
        if (present == 0)
            return new()
            {
                ["state"] = MigrateLegacyResearch(legacy),
                ["runtime"] = BlankRuntime(),
                ["airframes"] = AirframeCatalog.EmptySelection(),
                ["flags"] = new DataMap { ["first_medium_boss_defeated"] = data.L("completed_waves") >= 3 || data.L("alien_points") > 0, ["missile_intel"] = legacy.L("missile") > 0, ["laser_intel"] = legacy.L("laser") > 0 || data.L("completed_waves") >= 10, ["unrestricted_research"] = false }
            };
        if (present != 4)
            return null;
        var state = DeepTechnology.Validate(data.Value("research_state"));
        var frames = AirframeCatalog.Validate(data.Value("airframe_selection"));
        if (state == null || frames == null || data.Value("research_flags") is not DataMap flags || (flags.Count != 3 && flags.Count != 4) || flags.Keys.Any(k => k is not ("first_medium_boss_defeated" or "missile_intel" or "laser_intel" or "unrestricted_research")) || new[] { "first_medium_boss_defeated", "missile_intel", "laser_intel" }.Any(k => flags.Value(k) is not bool) || (flags.ContainsKey("unrestricted_research") && flags.Value("unrestricted_research") is not bool))
            return null;
        var legacyRights = MigrateLegacyResearch(legacy).Map("nodes");
        if (state.List("credited").Cast<string>().Any(id => !legacyRights.ContainsKey(id)))
            return null;
        foreach (string id in state.Map("nodes").Keys)
        {
            if (state.List("credited").Contains(id) || flags.B("unrestricted_research"))
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
                if (node.Length == 0 || id is "M1" or "L1" || id == AirframeCatalog.BaseFor(def.S("kind")) && legacy.L(def.S("kind")) > 0)
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
        _credited = state.List("credited").Cast<string>().ToHashSet();
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
