using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace Earthward.Domain;

public sealed partial class DefenseState
{
    public const bool ExpeditionEnabled = false;
    public const long MaxExactInteger = 9007199254740991;
    public const double ResourceLimit = 1e300, DefenseTimeLimit = 1e12, DeathBlastBaseDamage = 35, DeathBlastRadius = .55;
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
    public double Minerals { get; set; } = 320; public double Energy { get; set; } = 180; public double Science { get; set; } = 80; public double EarthHp { get; set; } = 100; public double Shield { get; set; } = 0; public double DefenseTime
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
        double multiplier = CombatSettings.N("resource_output_multiplier", .1), efficiency = (1 + Level("mining") * .25) * (1 + Level("industrial_synergy") * .12) * Math.Pow(1.12, Level("industrial_mastery"));
        var fx = TechEffects();
        return new()
        {
            ["mine"] = 5 * multiplier * efficiency * (1 + Level("mineral_processing") * .18) * (1 + fx.N("mineral_output_bonus")),
            ["solar"] = 4 * multiplier * efficiency * (1 + Level("energy_grid") * .18) * (1 + Level("laser_capacitors") * .04 + Level("photonic_mastery") * .05) * (1 + fx.N("energy_output_bonus")),
            ["lab"] = 1.5 * multiplier * efficiency * (1 + Level("research_methods") * .2) * (1 + fx.N("science_output_bonus")) * fx.N("science_output_multiplier", 1)
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
    public double CoreUpgradeFraction() => CombatSettings.N("resource_core_upgrade_percent", 5) * .01 + (HasResearch("I_N1") ? .02 : 0);
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
        return Math.Min(ResourceLimit, ResourceFacilityBaseOutputs().N(kind) * (1 + level * CoreUpgradeFraction()) * (_boostSites.N(site.ToString()) > DefenseTime ? 2 : 1));
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
            _boostUnits[kind] = _boostUnits.N(kind) + units;
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
    private static double ShieldCapacity(DataMap levels) => 100 + levels.N("shield") * 35 + levels.N("defense_network") * 25;
    public double ShieldMax() => ShieldCapacity(Tech) * (1 + TechEffects().N("earth_shield_bonus")) * TechEffects().N("earth_shield_multiplier", 1);
    public double ShieldRegeneration() => (.8 + Level("shield") * .6 + Level("shield_regen") * .9 + Level("defense_network") * .3) * (1 + TechEffects().N("shield_regeneration_bonus"));
    public DataMap BuildingCost(string id)
    {
        if (id == "starship_silo")
            return Expedition.SiloCost();
        var def = Find(BuildingDefinitions, id);
        if (def.Count == 0)
            return new();
        double count = Buildings.N(id);
        var result = ScaledCost(def.Map("cost"), (1 + .3 * count + .08 * Math.Pow(Math.Max(0, count - 12), 1.18)) / (1 + Level("industrial_synergy") * .06));
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
                ["expires"] = DefenseTime + 20
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
    public DataMap RepairCost() => ScaledCost(new() { ["minerals"] = 65d, ["energy"] = 55d }, 1 / (1 + Level("repair_protocol") * .05));
    public bool CanRepair() => EarthHp > 0 && (EarthHp < 100 || (HasDamagedLocalShields?.Invoke() ?? false)) && CanAfford(RepairCost());
    public bool Repair()
    {
        if (!CanRepair())
            return false;
        Pay(RepairCost());
        EarthHp = Math.Min(100, EarthHp + 30);
        RequestLocalShieldRecharge(40);
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
        double amount = Math.Min(damage * .1, Math.Max(0, ShieldMax() * .02 - _sacrificeUsed));
        if (amount <= 0)
            return 0;
        amount = RequestLocalShieldRecharge(amount);
        if (amount <= 0) return 0;
        _sacrificeUsed += amount;
        Changed?.Invoke();
        return amount;
    }
    public double KillRewardMultiplier() => 1 + Level("salvage") * .18;
    public double WaveRewardMultiplier() => 1 + Level("salvage") * .15 + Level("industrial_mastery") * .1;
    public long AlienRewardForWave(long wave, int stage = 0) => (long)Math.Ceiling((3 + Math.Floor(Math.Max(0, wave) / 10d)) * (1 + .5 * Math.Clamp(stage, 0, 3)));
    public void RewardKill(string kind, long waveValue = -1, int stage = 0)
    {
        double m, e, s;
        long score, ap = 0, cores = 0;
        switch (kind)
        {
            case "meteor":
                m = 5;
                e = 0;
                s = 0;
                score = 10;
                break;
            case "scout":
                m = 9;
                e = 3;
                s = 2;
                score = 30;
                break;
            case "cruiser":
            case "carrier":
                m = 22;
                e = 9;
                s = 6;
                score = 90;
                break;
            case "small_boss":
                m = 35;
                e = 15;
                s = 10;
                score = 150;
                cores = CombatSettings.L("resource_core_drop_count");
                break;
            case "boss":
                _flags["first_medium_boss_defeated"] = true;
                m = 120;
                e = 60;
                s = 45;
                score = 500;
                ap = AlienRewardForWave(waveValue < 0 ? Wave : waveValue, stage);
                break;
            case "mothership":
                m = 1800;
                e = 1200;
                s = 900;
                score = 5000;
                ap = 3;
                break;
            default:
                return;
        }
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
        Minerals = Math.Min(ResourceLimit, Minerals + (45 + ended * 8d) * WaveRewardMultiplier());
        Energy = Math.Min(ResourceLimit, Energy + (25 + ended * 4d) * WaveRewardMultiplier());
        Science = Math.Min(ResourceLimit, Science + (12 + ended * 2d) * WaveRewardMultiplier());
        RequestLocalShieldRecharge(20);
        Score = Math.Min(MaxExactInteger, Score + 100 + ended * 20);
        if (CompletedWaves >= 10)
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
