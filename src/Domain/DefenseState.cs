using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public const bool ExpeditionEnabled = false;
    public const long MaxExactInteger = 9007199254740991;
    public const double ResourceLimit = 1e300, DefenseTimeLimit = 1e12;
    public static double DeathBlastBaseDamage => DomainBalance.Value("death_blast_damage");
    public static double DeathBlastRadius => DomainBalance.Value("death_blast_radius");
    public static IReadOnlyList<DataMap> BuildingDefinitions => CatalogData.Rows("economy.json", "buildings");
    public static IReadOnlyList<DataMap> LegacyTechnologies => CatalogData.Rows("legacy-technology.json", "definitions");
    public static DataMap DefaultCombatSettings => CatalogData.Load("economy.json").Map("settings").DeepClone();
    public static IReadOnlyList<string> IntegerCombatSettings => CatalogData.Load("economy.json").List("integer_settings").Cast<string>().ToList();
    public static IReadOnlyList<string> ResourceFacilityKinds => new[] { "mine", "solar", "lab" };
    public event Action? Changed;
    public event Action<DataMap>? FactoryPerkRewarded;
    public FactoryPerks FactoryPerks { get; } = new();
    public ExpeditionData Expedition { get; } = new();
    public string RunId { get; private set; } = Guid.NewGuid().ToString("N");
    public int DefenseReachStage
    {
        get; private set;
    }
    public double Minerals { get; set; } = DomainBalance.Value("initial_minerals"); public double Energy { get; set; } = DomainBalance.Value("initial_energy"); public double Science { get; set; } = DomainBalance.Value("initial_science"); public double EarthHp { get; set; } = DomainBalance.Value("earth_max_health"); public double Shield { get; set; } = 0; public double DefenseTime
    {
        get; set;
    }
    public long Wave
    {
        get; set;
    }
    public long Kills
    {
        get; set;
    }
    public long Score
    {
        get; set;
    }
    public long AlienPoints
    {
        get; set;
    }
    public long ResourceCores
    {
        get; set;
    }
    public long CompletedWaves
    {
        get; set;
    }
    public DataMap Buildings { get; private set; } = CatalogData.Load("economy.json").Map("initial_buildings").DeepClone();
    public DataMap Tech { get; private set; } = CatalogData.Load("legacy-technology.json").Map("initial_levels").DeepClone();
    public DataMap CombatSettings { get; private set; } = DefaultCombatSettings;
    public DataMap DeepResearch { get; private set; } = new();
    private HashSet<string> _credited = new();
    private DataMap _researchMigration = new(), _researchRefundApplied = new();
    private DataMap _successorLevels = new(), _flags = new() { ["first_medium_boss_defeated"] = false, ["missile_intel"] = false, ["laser_intel"] = false, ["unrestricted_research"] = false };
    private DataMap _airframes = AirframeCatalog.EmptySelection(), _resourceUpgrades = new();
    private List<DataMap> _refunds = new(), _boosts = new();
    private DataMap _boostUnits = new(), _boostSites = new(), _resourceLevelTotals = new();
    private DataMap? _primedWeapons, _primedPatrol;
    private double _zeroShieldUntil, _zeroShieldReady, _sacrificeUsed;
    private long _sacrificeBucket = -1;
    private HashSet<string> _rewardedEnemies = new();
    private DataMap? _techEffects;
    public long FactoryStatsRevision
    {
        get; private set;
    }
    private readonly Dictionary<string, DataMap> _weaponCache = new(), _patrolCache = new();
    public DefenseState()
    {
        CatalogData.ValidateRuntime();
        FactoryPerks.Changed += () => { InvalidateFactoryStats(); Changed?.Invoke(); };
        Expedition.Changed += () => Changed?.Invoke();
    }
    private double Level(string id) => (id is "missile" or "laser") && WeaponUnlocked(id) ? Math.Max(1, Tech.N(id)) : Tech.N(id);
    private static DataMap Find(IEnumerable<DataMap> rows, string id) => rows.FirstOrDefault(row => row.S("id") == id)?.DeepClone() ?? new();
    private static bool RequirementsMet(DataMap levels, DataMap requirements) => requirements.All(pair => levels.L(pair.Key) >= DataMap.Integer(pair.Value));
    private static DataMap ScaledCost(DataMap source, double multiplier)
    {
        var r = new DataMap();
        foreach (var (k, v) in source)
            r[k] = Math.Min(ResourceLimit, Math.Ceiling(DataMap.Number(v) * multiplier));
        return r;
    }
    public bool CanAfford(DataMap cost) => Minerals >= cost.N("minerals") && Energy >= cost.N("energy") && Science >= cost.N("science") && AlienPoints >= cost.L("alien_points") && ResourceCores >= cost.L("resource_cores");
    public void Pay(DataMap cost)
    {
        Minerals -= cost.N("minerals");
        Energy -= cost.N("energy");
        Science -= cost.N("science");
        AlienPoints -= cost.L("alien_points");
        ResourceCores -= cost.L("resource_cores");
    }
    public DataMap ResourceFacilityBaseOutputs()
    {
        double multiplier = CombatSettings.N("resource_output_multiplier", .1), efficiency = (1 + Level("mining") * DomainBalance.Legacy("mining_1")) * (1 + Level("industrial_synergy") * DomainBalance.Legacy("industrial_synergy_1")) * Math.Pow(DomainBalance.Value("legacy_industry_growth"), Level("industrial_mastery"));
        var fx = TechEffects();
        return new()
        {
            ["mine"] = DomainBalance.Value("mineral_output_base") * multiplier * efficiency * (1 + Level("mineral_processing") * DomainBalance.Legacy("mineral_processing_1")) * (1 + fx.N("mineral_output_bonus")),
            ["solar"] = DomainBalance.Value("energy_output_base") * multiplier * efficiency * (1 + Level("energy_grid") * DomainBalance.Legacy("energy_grid_1")) * (1 + Level("laser_capacitors") * DomainBalance.Legacy("laser_capacitors_1") + Level("photonic_mastery") * DomainBalance.Legacy("photonic_mastery_1")) * (1 + fx.N("energy_output_bonus")),
            ["lab"] = DomainBalance.Value("science_output_base") * multiplier * efficiency * (1 + Level("research_methods") * DomainBalance.Legacy("research_methods_1")) * (1 + fx.N("science_output_bonus")) * fx.N("science_output_multiplier", 1)
        };
    }
    public DataMap Rates()
    {
        var basis = ResourceFacilityBaseOutputs();
        var result = new DataMap();
        foreach (var (kind, currency) in new[] { ("mine", "minerals"), ("solar", "energy"), ("lab", "science") })
        {
            double levels = _resourceLevelTotals.N(kind);
            result[currency] = Math.Min(ResourceLimit, basis.N(kind) * (Buildings.N(kind) + levels * CoreUpgradeFraction() + _boostUnits.N(kind)));
        }
        return result;
    }
    public double CoreUpgradeFraction() => CombatSettings.N("resource_core_upgrade_percent", 5) * .01 + (HasResearch("I_N1") ? Math.Round(TechEffects().N("resource_core_fraction") - DomainBalance.Value("resource_core_reference_fraction"), 12) : 0);
    public double EffectiveResourceCorePercent() => CoreUpgradeFraction() * 100;
    public long GetResourceFacilityLevel(long site) => _resourceUpgrades.Map(site.ToString()).L("level");
    public bool CanUpgradeResourceFacility(long site, string kind) => site >= 0 && site <= MaxExactInteger && ResourceFacilityKinds.Contains(kind) && EarthHp > 0 && ResourceCores >= 1 && Buildings.L(kind) > 0 && (!_resourceUpgrades.ContainsKey(site.ToString()) || _resourceUpgrades.Map(site.ToString()).S("kind") == kind) && (_resourceUpgrades.ContainsKey(site.ToString()) || _resourceUpgrades.Values.OfType<DataMap>().Count(row => row.S("kind") == kind) < Buildings.L(kind)) && GetResourceFacilityLevel(site) < MaxExactInteger;
    public bool UpgradeResourceFacility(long site, string kind)
    {
        if (!CanUpgradeResourceFacility(site, kind))
            return false;
        ResourceCores--;
        _resourceUpgrades[site.ToString()] = new DataMap { ["kind"] = kind, ["level"] = (long)GetResourceFacilityLevel(site) + 1 };
        RefreshResourceBoosts();
        Changed?.Invoke();
        return true;
    }
    public double ResourceFacilityOutput(string kind, long site, int additionalLevels = 0)
    {
        if (site < 0 || !ResourceFacilityKinds.Contains(kind) || Buildings.L(kind) <= 0 || additionalLevels < 0 || (_resourceUpgrades.ContainsKey(site.ToString()) && _resourceUpgrades.Map(site.ToString()).S("kind") != kind))
            return 0;
        double level = Math.Min(MaxExactInteger, (double)GetResourceFacilityLevel(site) + additionalLevels);
        return Math.Min(ResourceLimit, ResourceFacilityBaseOutputs().N(kind) * (1 + level * CoreUpgradeFraction()) * (_boostSites.N(site.ToString()) > DefenseTime ? TechEffects().N("new_facility_boost_multiplier") : 1));
    }
    private void RefreshResourceBoosts()
    {
        _boostUnits = new();
        _boostSites = new();
        _resourceLevelTotals = new();
        foreach (var row in _resourceUpgrades.Values.OfType<DataMap>())
        {
            string kind = row.S("kind");
            _resourceLevelTotals[kind] = _resourceLevelTotals.N(kind) + row.N("level");
        }
        foreach (var boost in _boosts)
        {
            if (boost.N("expires") <= DefenseTime)
                continue;
            long site = boost.L("site");
            double units = 1;
            if (site >= 0)
            {
                units += GetResourceFacilityLevel(site) * CoreUpgradeFraction();
                _boostSites[site.ToString()] = boost.N("expires");
            }
            string kind = boost.S("kind");
            _boostUnits[kind] = _boostUnits.N(kind) + units * (TechEffects().N("new_facility_boost_multiplier") - 1);
        }
    }
    public void Tick(double delta)
    {
        if (!double.IsFinite(delta) || delta <= 0 || EarthHp <= 0)
            return;
        DefenseTime = Math.Min(DefenseTimeLimit, DefenseTime + delta);
        while (_refunds.Count > 0 && _refunds[0].N("due") <= DefenseTime)
        {
            Science = Math.Min(ResourceLimit, Science + _refunds[0].N("amount"));
            _refunds.RemoveAt(0);
        }
        bool expired = false;
        while (_boosts.Count > 0 && _boosts[0].N("expires") <= DefenseTime)
        {
            _boosts.RemoveAt(0);
            expired = true;
        }
        if (expired)
            RefreshResourceBoosts();
        var income = Rates();
        Minerals = Math.Min(ResourceLimit, Minerals + income.N("minerals") * delta);
        Energy = Math.Min(ResourceLimit, Energy + income.N("energy") * delta);
        Science = Math.Min(ResourceLimit, Science + income.N("science") * delta);
        Changed?.Invoke();
    }
    private static double ShieldCapacity(DataMap levels) => DomainBalance.Value("shield_capacity_base") + levels.N("shield") * DomainBalance.Legacy("shield_1") + levels.N("defense_network") * DomainBalance.Legacy("defense_network_1");
    public double ShieldMax() => ShieldCapacity(Tech) * (1 + TechEffects().N("earth_shield_bonus")) * TechEffects().N("earth_shield_multiplier", 1);
    public double ShieldRegeneration() => (DomainBalance.Value("shield_regeneration_base") + Level("shield") * DomainBalance.Legacy("shield_2") + Level("shield_regen") * DomainBalance.Legacy("shield_regen_1") + Level("defense_network") * DomainBalance.Legacy("defense_network_2")) * (1 + TechEffects().N("shield_regeneration_bonus"));
    public DataMap BuildingCost(string id)
    {
        if (id == "starship_silo")
            return Expedition.SiloCost();
        var def = Find(BuildingDefinitions, id);
        if (def.Count == 0)
            return new();
        double count = Buildings.N(id);
        var result = ScaledCost(def.Map("cost"), (1 + DomainBalance.Value("building_cost_per_existing") * count + DomainBalance.Value("building_cost_late_factor") * Math.Pow(Math.Max(0, count - DomainBalance.Value("building_cost_late_start")), DomainBalance.Value("building_cost_late_power"))) / (1 + Level("industrial_synergy") * DomainBalance.Legacy("industrial_synergy_2")));
        if (ResourceFacilityKinds.Contains(id))
            result["resource_cores"] = 1L;
        return result;
    }
    public bool CanBuild(string id) => BuildingUnlocked(id) && EarthHp > 0 && Buildings.L(id) < MaxExactInteger && CanAfford(BuildingCost(id));
    public bool Build(string id, long siteId = -1)
    {
        if (!CanBuild(id))
            return false;
        Pay(BuildingCost(id));
        Buildings[id] = Buildings.L(id) + 1;
        if (ResourceFacilityKinds.Contains(id) && HasResearch("I_A1"))
            _boosts.Add(new()
            {
                ["kind"] = id,
                ["site"] = siteId,
                ["expires"] = DefenseTime + TechEffects().N("new_facility_boost_duration")
            });
        InvalidateFactoryStats();
        Changed?.Invoke();
        return true;
    }
    public long DroneCount() => PerkCatalog.Kinds.Sum(kind => Buildings.L(kind) * FactoryCapacity(kind));
    public long FacilityCount() => Buildings.Values.Sum(value => DataMap.Integer(value));
    public int FacilityCapacity() => -1;
    public Vector2 CombatSettingBounds(string id)
    {
        var values = CatalogData.Load("economy.json").Map("ranges").List(id);
        return values.Count == 2 ? new((float)DataMap.Number(values[0]), (float)DataMap.Number(values[1])) : Vector2.Zero;
    }
    public bool CombatSettingInRange(string id, double value)
    {
        var values = CatalogData.Load("economy.json").Map("ranges").List(id);
        return values.Count == 2 && DataMap.ValidNumber(value, DataMap.Number(values[0]), DataMap.Number(values[1]));
    }
    public DataMap? ValidatedCombatSettings(DataMap values)
    {
        var defaults = DefaultCombatSettings;
        if (values.Keys.Any(k => !defaults.ContainsKey(k)))
            return null;
        foreach (string key in defaults.Keys.ToList())
        {
            if (!values.ContainsKey(key) && key is not ("enemy_health" or "drone_damage" or "laser_damage" or "missile_damage"))
                continue;
            var range = CatalogData.Load("economy.json").Map("ranges").List(key);
            if (!DataMap.ValidNumber(values.Value(key), DataMap.Number(range[0]), DataMap.Number(range[1]), IntegerCombatSettings.Contains(key)))
                return null;
            defaults[key] = values.N(key);
        }
        if (defaults.N("enemy_bullet_max_count") < defaults.N("enemy_bullet_base_count") || defaults.N("frontier_radius_2") <= defaults.N("frontier_radius_1") || defaults.N("frontier_radius_3") <= defaults.N("frontier_radius_2"))
            return null;
        return defaults;
    }
    public bool ApplyCombatSettings(DataMap values)
    {
        var clean = ValidatedCombatSettings(values);
        if (clean == null)
            return false;
        CombatSettings = clean;
        InvalidateFactoryStats();
        Changed?.Invoke();
        return true;
    }
    public bool SetCombatSetting(string id, double value)
    {
        if (!CombatSettingInRange(id, value) || (IntegerCombatSettings.Contains(id) && Math.Floor(value) != value))
            return false;
        var copy = CombatSettings.DeepClone();
        copy[id] = value;
        return ApplyCombatSettings(copy);
    }
    public int EnemyBulletCount()
    {
        int basis = CombatSettings.I("enemy_bullet_base_count"), maximum = CombatSettings.I("enemy_bullet_max_count"), step = CombatSettings.I("enemy_bullet_growth_step");
        if (step <= 0 || basis >= maximum)
            return Math.Min(basis, maximum);
        double interval = CombatSettings.N("enemy_bullet_growth_interval");
        return (int)Math.Min(maximum, basis + Math.Floor(DefenseTime / interval) * step);
    }
    public void TakeDamage(double amount)
    {
        if (amount <= 0 || !double.IsFinite(amount) || EarthHp <= 0)
            return;
        EarthHp = Math.Max(0, EarthHp - amount);
        Changed?.Invoke();
    }
    public DataMap RepairCost() => ScaledCost(new() { ["minerals"] = DomainBalance.Value("repair_minerals"), ["energy"] = DomainBalance.Value("repair_energy") }, 1 / (1 + Level("repair_protocol") * DomainBalance.Legacy("repair_protocol_1")));
    public bool CanRepair() => EarthHp > 0 && (EarthHp < DomainBalance.Value("earth_max_health") || (HasDamagedLocalShields?.Invoke() ?? false)) && CanAfford(RepairCost());
    public bool Repair()
    {
        if (!CanRepair())
            return false;
        Pay(RepairCost());
        EarthHp = Math.Min(DomainBalance.Value("earth_max_health"), EarthHp + DomainBalance.Value("repair_earth_health"));
        RequestLocalShieldRecharge(DomainBalance.Value("repair_shield_budget"));
        Changed?.Invoke();
        return true;
    }
    public double ApplySacrificeRecovery(double damage)
    {
        if (!HasResearch("D_A3") || damage <= 0 || !double.IsFinite(damage) || EarthHp <= 0)
            return 0;
        long bucket = (long)Math.Floor(DefenseTime);
        if (bucket != _sacrificeBucket)
        {
            _sacrificeBucket = bucket;
            _sacrificeUsed = 0;
        }
        double amount = Math.Min(damage * TechEffects().N("sacrifice_shield_fraction"), Math.Max(0, ShieldMax() * TechEffects().N("sacrifice_shield_cap_per_second") - _sacrificeUsed));
        if (amount <= 0)
            return 0;
        amount = RequestLocalShieldRecharge(amount);
        if (amount <= 0) return 0;
        _sacrificeUsed += amount;
        Changed?.Invoke();
        return amount;
    }
    public double KillRewardMultiplier() => 1 + Level("salvage") * DomainBalance.Legacy("salvage_1");
    public double WaveRewardMultiplier() => 1 + Level("salvage") * DomainBalance.Legacy("salvage_2") + Level("industrial_mastery") * DomainBalance.Legacy("industrial_mastery_1");
    public long AlienRewardForWave(long wave, int stage = 0) => (long)Math.Ceiling((DomainBalance.Value("alien_reward_base") + Math.Floor(Math.Max(0, wave) / DomainBalance.Value("alien_reward_wave_interval"))) * (1 + DomainBalance.Value("alien_reward_stage_increment") * Math.Clamp(stage, 0, 3)));
    public void RewardKill(string kind, long waveValue = -1, int stage = 0)
    {
        var reward = DomainBalance.KillReward(kind);
        if(reward.Count==0)return;
        double m=reward.N("minerals"),e=reward.N("energy"),s=reward.N("science");long score=reward.L("score"),ap=reward.B("wave_alien_reward")?AlienRewardForWave(waveValue<0?Wave:waveValue,stage):reward.L("alien_points");
        long cores=reward.S("resource_core_setting").Length>0?CombatSettings.L(reward.S("resource_core_setting")):0;
        if(reward.B("first_medium_boss"))_flags["first_medium_boss_defeated"]=true;
        Minerals = Math.Min(ResourceLimit, Minerals + m * KillRewardMultiplier() * (1 + TechEffects().N("mineral_kill_bonus")));
        Energy = Math.Min(ResourceLimit, Energy + e * KillRewardMultiplier());
        Science = Math.Min(ResourceLimit, Science + s * KillRewardMultiplier());
        AlienPoints = Math.Min(MaxExactInteger, AlienPoints + ap);
        ResourceCores = Math.Min(MaxExactInteger, ResourceCores + cores);
        Kills = Math.Min(MaxExactInteger, Kills + 1);
        Score = Math.Min(MaxExactInteger, Score + score);
        Changed?.Invoke();
    }
    public bool RewardEnemy(DataMap enemy)
    {
        string id = enemy.S("reward_event_id");
        if (id.Length == 0)
            id = $"{enemy.L("wave", Wave)}:{enemy.L("uid", -1)}:{enemy.S("kind", "scout")}";
        if (!_rewardedEnemies.Add(id))
            return false;
        string kind = enemy.B("resource_core_carrier") ? "small_boss" : enemy.S("reward_kind", enemy.S("kind", "scout"));
        RewardKill(kind, enemy.L("spawn_wave", enemy.L("wave", Wave)), enemy.I("defense_stage", DefenseReachStage));
        return true;
    }
    public void RewardWave(long waveValue = -1)
    {
        long ended = waveValue < 0 ? Wave : waveValue;
        if (ended <= CompletedWaves)
            return;
        CompletedWaves = Math.Max(CompletedWaves, ended);
        Minerals = Math.Min(ResourceLimit, Minerals + (DomainBalance.Value("wave_minerals_base") + ended * DomainBalance.Value("wave_minerals_increment")) * WaveRewardMultiplier());
        Energy = Math.Min(ResourceLimit, Energy + (DomainBalance.Value("wave_energy_base") + ended * DomainBalance.Value("wave_energy_increment")) * WaveRewardMultiplier());
        Science = Math.Min(ResourceLimit, Science + (DomainBalance.Value("wave_science_base") + ended * DomainBalance.Value("wave_science_increment")) * WaveRewardMultiplier());
        RequestLocalShieldRecharge(DomainBalance.Value("wave_shield_budget"));
        Score = Math.Min(MaxExactInteger, Score + (long)DomainBalance.Value("wave_score_base") + ended * (long)DomainBalance.Value("wave_score_increment"));
        if (CompletedWaves >= DomainBalance.Value("laser_intel_wave"))
        {
            _flags["laser_intel"] = true;
            FactoryPerks.RememberIntel("L1");
        }
        Changed?.Invoke();
    }
    public void InvalidateFactoryStats()
    {
        FactoryStatsRevision++;
        _techEffects = null;
        _primedWeapons = null;
        _primedPatrol = null;
        _weaponCache.Clear();
        _patrolCache.Clear();
        RefreshResourceBoosts();
    }
    public int GetDefenseReachStage() => DefenseReachStage;
    public void SetDefenseReachStage(int value)
    {
        int stage = Math.Clamp(value, 0, 3);
        if (stage == DefenseReachStage)
            return;
        DefenseReachStage = stage;
        FactoryPerks.MarkDefenseStage(stage);
        InvalidateFactoryStats();
        Changed?.Invoke();
    }
}
