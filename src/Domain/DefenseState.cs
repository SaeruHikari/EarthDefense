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
    public static DataMap DefaultCombatSettings => CatalogData.Load("economy.json").Map("settings").DeepClone();
    public static IReadOnlyList<string> IntegerCombatSettings => CatalogData.Load("economy.json").List("integer_settings").Cast<string>().ToList();
    // Ground construction covers extraction, power, defense and the one-time
    // satellite launch facility. Science starts when the first satellite is
    // inserted into orbit.
    public static IReadOnlyList<string> ResourceFacilityKinds => new[] { "mine", "solar" };
    public event Action? Changed;
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
    public DataMap CombatSettings { get; private set; } = DefaultCombatSettings;
    public DataMap DeepResearch { get; private set; } = new();
    private DataMap _successorLevels = new(), _flags = new() { ["missile_intel"] = false, ["laser_intel"] = false, ["unrestricted_research"] = false };
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
        Achievements.Changed += () => { InvalidateFactoryStats(); Changed?.Invoke(); };
        Expedition.Changed += () => Changed?.Invoke();
    }
    private static DataMap Find(IEnumerable<DataMap> rows, string id) => rows.FirstOrDefault(row => row.S("id") == id)?.DeepClone() ?? new();
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
        double multiplier = CombatSettings.N("resource_output_multiplier", .1);
        var fx = TechEffects();
        return new()
        {
            ["mine"] = DomainBalance.Value("mineral_output_base") * multiplier * (1 + fx.N("mineral_output_bonus")),
            ["solar"] = DomainBalance.Value("energy_output_base") * multiplier * (1 + fx.N("energy_output_bonus")),
            ["science"] = DomainBalance.Value("science_output_base") * multiplier * (1 + fx.N("science_output_bonus")) * fx.N("science_output_multiplier", 1)
        };
    }
    public DataMap Rates()
    {
        var basis = ResourceFacilityBaseOutputs();
        var result = new DataMap();
        foreach (var (kind, currency) in new[] { ("mine", "minerals"), ("solar", "energy") })
        {
            double levels = _resourceLevelTotals.N(kind);
            result[currency] = Math.Min(ResourceLimit, basis.N(kind) * (Buildings.N(kind) + levels * CoreUpgradeFraction() + _boostUnits.N(kind)));
        }
        // Science is supplied by the run-owned research satellite. During the
        // launch sequence it remains offline, so the first launch is a clear
        // incremental milestone instead of a hidden starting income source.
        result["science"] = ResearchSatelliteDeployed ? Math.Min(ResourceLimit, basis.N("science")) : 0;
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
        double earthRepair = LocalShieldEarthRepairRate();
        if (earthRepair > 0 && EarthHp < DomainBalance.Value("earth_max_health"))
            EarthHp = Math.Min(DomainBalance.Value("earth_max_health"), EarthHp + earthRepair * delta);
        Changed?.Invoke();
    }
    public double ShieldMax() => DomainBalance.Value("shield_capacity_base") * (1 + TechEffects().N("earth_shield_bonus")) * TechEffects().N("earth_shield_multiplier", 1);
    public double ShieldRegeneration() => DomainBalance.Value("shield_regeneration_base") * (1 + TechEffects().N("shield_regeneration_bonus"));
    public DataMap BuildingCost(string id)
    {
        var def = Find(BuildingDefinitions, id);
        if (def.Count == 0)
            return new();
        double count = Buildings.N(id);
        var result = ScaledCost(def.Map("cost"), 1 + DomainBalance.Value("building_cost_per_existing") * count + DomainBalance.Value("building_cost_late_factor") * Math.Pow(Math.Max(0, count - DomainBalance.Value("building_cost_late_start")), DomainBalance.Value("building_cost_late_power")));
        if (ResourceFacilityKinds.Contains(id))
            result["resource_cores"] = 1L;
        return result;
    }
    public bool CanBuild(string id)
    {
        if (!BuildingUnlocked(id) || EarthHp <= 0 || Buildings.L(id) >= BuildingMaxCount(id))
            return false;
        if (id == "shield" && ShieldBuildLockReason().Length > 0)
            return false;
        return CanAfford(BuildingCost(id));
    }
    public bool Build(string id, long siteId = -1)
    {
        if (!CanBuild(id))
            return false;
        Pay(BuildingCost(id));
        Buildings[id] = Buildings.L(id) + 1;
        if (id == "shield")
        {
            long cooldown = LocalShieldBuildCooldownWaves;
            _shieldBuildReadyWave = Wave > MaxExactInteger - cooldown ? MaxExactInteger : Wave + cooldown;
        }
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
    public double KillRewardMultiplier() => 1;
    public double WaveRewardMultiplier() => 1;
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
