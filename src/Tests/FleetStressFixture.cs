using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Earthward.Combat;
using Earthward.Domain;
using Godot;

namespace Earthward.Tests;

public enum FleetStressMode { Patrol, Combat }
public enum FleetStressLayout { Cluster, World }

/// <summary>Synthetic isolated-profile benchmark. Never use this setup in a player's active save.</summary>
public sealed class FleetStressFixture
{
    private sealed class StableSurface : ICombatSurface
    {
        public List<DataMap> Records = new();
        public long TransformCalls;
        public IReadOnlyList<DataMap> GetFactorySites() => Records;
        public Vector3 SurfaceToSpace(Vector3 normal, double altitude) { TransformCalls++; return normal.Normalized() * (float)(WorldScale.EarthRadius + altitude); }
        public Vector3 SpaceToSurface(Vector3 position) => position.Normalized();
        public IReadOnlyList<Vector3> GetOccupiedSurfaceNormals() => Array.Empty<Vector3>();
        public long SpatialRevision => 0;
        public bool TryGetCoordinateFrame(out Transform3D localToWorld) { localToWorld = Transform3D.Identity; return true; }
    }
    private readonly DefenseState _game;
    private readonly Battlefield _battle;
    private readonly StableSurface _surface = new();
    private readonly Dictionary<long, int> _targetBySite = new();
    private readonly Dictionary<long, int> _capacityBySite = new();
    private readonly Dictionary<long, int> _counts = new();
    private int _generation;
    private long _totalCreated, _totalEnemyCreated;
    public const ulong Seed = 127;
    public int FriendlyCount { get; }
    public int EnemyCount { get; }
    public FleetStressLayout Layout { get; }
    public int AircraftWithTargets { get; private set; }
    public int PeakAircraftWithTargets { get; private set; }
    private readonly HashSet<long> _observedFiringLives = new(), _observedEngagedFactories = new();
    public int ObservedFiringLives => _observedFiringLives.Count;
    public int ObservedEngagedFactories => _observedEngagedFactories.Count;
    public FleetStressMode Mode { get; private set; }
    public IReadOnlyList<DataMap> Sites => _surface.Records;
    public string StatsHash { get; private set; } = "";
    public long SurfaceTransformCalls => _surface.TransformCalls;
    public long FriendlyCreated => _totalCreated;
    public long EnemyCreated => _totalEnemyCreated;
    public double PlanetDamage { get; private set; }
    public int PeakProjectiles { get; private set; }
    public int PeakFriendlyCount { get; private set; }
    public int PeakEnemyCount { get; private set; }
    public long ShotsFired { get; private set; }
    private static readonly string[] Frames = { "K1", "K2", "K3", "M1", "M2", "M3", "L1", "L2", "L3" };
    private static readonly string[][] Perks = {
        new[]{"a_kinetic_pierce","a_kinetic_ricochet"}, new[]{"a_k2_dense_fire","a_k2_recovery"},new[]{"a_k3_marked_shot","a_k3_mobile_gun"},
        new[]{"a_missile_cluster","a_missile_slow"},new[]{"a_m2_dual_zone","a_m2_shock_charge"},new[]{"a_m3_drill_charge","a_m3_escort_guidance"},
        new[]{"a_laser_refraction","a_laser_erosion"},new[]{"a_l2_charge_memory","a_l2_resonance"},new[]{"a_l3_wide_pulse","a_l3_afterpulse"} };
    private static string _fixtureProjectRoot = Directory.GetCurrentDirectory();
    public static string ConfigureCatalog(string? projectRoot = null)
    {
        _fixtureProjectRoot = Path.GetFullPath(projectRoot ?? Directory.GetCurrentDirectory());
        string path = Path.GetFullPath(Path.Combine(_fixtureProjectRoot, "tests", "CombatManaged", "Fixtures", "fleet127-data"));
        CatalogData.Configure(path);
        return path;
    }
    public FleetStressFixture(DefenseState game, Battlefield battle, int friendlyCount = 5000, int enemyCount = 1000, FleetStressLayout layout = FleetStressLayout.Cluster)
    {
        _game = game; _battle = battle; FriendlyCount = friendlyCount; EnemyCount = enemyCount; Layout = layout;
    }
    public void Setup(FleetStressMode mode)
    {
        if (!_game.Reset()) throw new InvalidOperationException("Could not reset isolated benchmark state");
        _game.Wave = 28; _game.CompletedWaves = 27;
        foreach (var frame in AirframeCatalog.Definitions) { string unlock = frame.S("unlock_node"); if (unlock.Length > 0) _game.DeepResearch[unlock] = 1L; }
        foreach (string kind in new[] { "interceptor", "missile", "laser" })
        {
            _game.SetCombatSetting(kind + "_factory_capacity", 4);
            _game.Buildings[kind] = 0L;
        }
        _game.SetCombatSetting("factory_capacity_multiplier", 50);
        _game.SetCombatSetting("factory_starting_capacity_factor", .5);
        var owned = new FactoryPerks().Snapshot();
        foreach (string id in owned.Map("levels").Keys.ToArray()) owned.Map("levels")[id] = 1L;
        owned["advanced_unlocked"] = true; owned["energy_cores"] = 1000000L;
        if (!_game.FactoryPerks.ImportSnapshot(owned) || !_game.FactoryPerks.ResetRunSites(_game.RunId)) throw new InvalidOperationException("Cannot configure fixed benchmark perks");
        _surface.Records.Clear(); _targetBySite.Clear(); _capacityBySite.Clear();
        int siteCount = 0;
        for (int todo = FriendlyCount; todo > 0; siteCount++) todo -= Math.Min(100 / AirframeCatalog.Definition(Frames[siteCount % Frames.Length]).I("capacity_cost", 1), todo);
        int remaining = FriendlyCount;
        for (int i = 0; remaining > 0; i++)
        {
            int index = i % Frames.Length; string frame = Frames[index]; var def = AirframeCatalog.Definition(frame); string kind = def.S("kind");
            int cost = def.I("capacity_cost", 1), count = Math.Min(100 / cost, remaining); long site = i + 1;
            double angle = i * 2.39996323, spread = .13 + (i % 3) * .10;
            var normal = new Vector3((float)(spread * Math.Cos(angle)), (float)(spread * Math.Sin(angle)), 1).Normalized();
            if (Layout == FleetStressLayout.World)
            {
                double y = 1 - 2 * (i + .5) / siteCount, horizontal = Math.Sqrt(Math.Max(0, 1 - y * y));
                normal = new Vector3((float)(horizontal * Math.Cos(angle)), (float)y, (float)(horizontal * Math.Sin(angle))).Normalized();
            }
            _surface.Records.Add(new() { ["site_id"] = site, ["kind"] = kind, ["normal"] = normal, ["launch_position"] = normal * (WorldScale.EarthRadius + .108f), ["launch_direction"] = Vector3.Right });
            _game.Buildings[kind] = _game.Buildings.L(kind) + 1;
            _targetBySite[site] = count; _capacityBySite[site] = count * cost; remaining -= count;
            if (!_game.SetAirframe(kind, site, -1, frame)) throw new InvalidOperationException("Frame selection failed: " + frame);
            for (int slot = 0; slot < 2; slot++) if (!_game.FactoryPerks.Equip(kind, site, slot, Perks[index][slot], "aircraft", -1, frame)) throw new InvalidOperationException("Perk selection failed: " + frame);
        }
        _game.InvalidateFactoryStats();
        _battle.Surface = _surface; _battle.ResetBattle(); _battle.Random.Seed = Seed;
        _totalCreated = _totalEnemyCreated = 0; _generation = 0; PlanetDamage = 0; PeakProjectiles = PeakFriendlyCount = PeakEnemyCount = 0; ShotsFired = 0;
        AircraftWithTargets = PeakAircraftWithTargets = 0; _observedFiringLives.Clear(); _observedEngagedFactories.Clear();
        _battle.EarthDamaged -= RecordPlanetDamage; _battle.EarthDamaged += RecordPlanetDamage;
        var profiles = new DataMap();
        foreach (var site in _surface.Records.Take(9))
        {
            string kind = site.S("kind"), frame = _game.FactoryAirframe(kind, site.L("site_id"));
            profiles[frame] = new DataMap { ["weapon"] = _game.FactoryDroneStats(kind, site.L("site_id")), ["patrol"] = _game.FactoryPatrolStats(kind, site.L("site_id")) };
        }
        StatsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profiles.ToJson()))).ToLowerInvariant();
        LoadFrozenProfiles();
        SetMode(mode); MaintainPopulation();
    }
    private static string CompiledFile => Path.Combine(_fixtureProjectRoot, "tests", "CombatManaged", "Fixtures", "fleet127-compiled-profiles.json");
    public bool FrozenProfilesLoaded { get; private set; }
    public bool FreezeCompiledProfilesEnabled { get; set; } = true;
    private DataMap? _frozenProfileData;
    public void ExportCompiledProfiles()
    {
        var profiles = new DataMap();
        var field = typeof(Battlefield).GetField("_factoryProfiles", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var configured = (IDictionary)field.GetValue(_battle)!;
        foreach (var site in _surface.Records.Take(9))
        {
            object profile = configured[site.L("site_id")]!; var type = profile.GetType(); var row = new DataMap();
            foreach (string name in new[] { "Weapons", "Patrol", "Range", "Radial", "Cosine", "Angle", "Frame", "Kind" }) row[name] = type.GetField(name)!.GetValue(profile);
            profiles[(string)row["Frame"]!] = row;
        }
        var global = (DataMap)typeof(Battlefield).GetField("_globalWeapons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_battle)!;
        File.WriteAllText(CompiledFile, new DataMap { ["source"] = "Pre-optimization Domain source and JSON; exact compiled battle profiles exported before the technology redesign", ["stats_hash"] = StatsHash, ["global_weapons"] = global, ["profiles"] = profiles }.ToJson());
    }
    private void LoadFrozenProfiles()
    {
        if (!FreezeCompiledProfilesEnabled || !File.Exists(CompiledFile)) return;
        var frozen = _frozenProfileData ??= DataMap.Parse(File.ReadAllText(CompiledFile));
        var field = typeof(Battlefield).GetField("_factoryProfiles", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var configured = (IDictionary)field.GetValue(_battle)!;
        foreach (DictionaryEntry entry in configured)
        {
            object profile = entry.Value!; var type = profile.GetType(); string frame = (string)type.GetField("Frame")!.GetValue(profile)!;
            var row = frozen.Map("profiles").Map(frame);
            if (row.Count == 0) throw new InvalidDataException("Frozen benchmark profile missing: " + frame);
            foreach (string name in new[] { "Weapons", "Patrol" }) type.GetField(name)!.SetValue(profile, row.Map(name).DeepClone());
            foreach (string name in new[] { "Range", "Radial", "Cosine", "Angle" }) type.GetField(name)!.SetValue(profile, row.N(name));
        }
        ((IDictionary)typeof(Battlefield).GetField("_actorProfiles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_battle)!).Clear();
        ((IDictionary)typeof(Battlefield).GetField("_aircraftProfiles", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_battle)!).Clear();
        typeof(Battlefield).GetField("_globalWeapons", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_battle, frozen.Map("global_weapons").DeepClone());
        StatsHash = frozen.S("stats_hash"); FrozenProfilesLoaded = true;
    }
    private void RecordPlanetDamage(double damage, Vector3 position) => PlanetDamage += damage;
    public void SetMode(FleetStressMode mode)
    {
        Mode = mode; _battle.Active = true; _battle.Paused = false;
        typeof(Battlefield).GetProperty(nameof(Battlefield.WaveRunning))!.SetValue(_battle, mode == FleetStressMode.Combat);
        if (mode == FleetStressMode.Patrol)
        {
            _battle.Enemies.Clear(); _battle.Shots.Clear(); _battle.HostileShots.Clear();
            foreach (var d in _battle.Drones) { d["target_uid"] = -1L; d["aim_target_uid"] = -1L; d["state"] = "patrol"; }
        }
    }
    private void SpawnFriendly(DataMap factory)
    {
        var d = _battle.SpawnFactoryDrone(factory); long site = factory.L("site_id"); var normal = _battle.SurfaceToSpace(factory.Map("site").Vector3("normal"), 0).Normalized();
        var axis = normal.Cross(Vector3.Up).Normalized(); int berth = d.I("patrol_slot");
        double phase = berth * 2.39996323, scatter = .0035 * Math.Sqrt(berth + 1);
        var n = (normal + axis * (float)(Math.Cos(phase) * scatter) + normal.Cross(axis) * (float)(Math.Sin(phase) * scatter)).Normalized();
        d["state"] = Mode == FleetStressMode.Combat ? "engaging" : "patrol"; d["launch_age"] = 1.3; d["space_position"] = n * (float)(WorldScale.EarthRadius + (Mode == FleetStressMode.Combat ? 1.1 : .7));
        d["normal"] = _battle.SpaceToSurface(d.Vector3("space_position")); d["aim_direction"] = n; d["aim_up"] = Vector3.Up; d["velocity"] = Vector3.Zero; d["fire"] = (d.L("uid") % 31) / 31d;
        _battle.WorldCache[d.L("uid")] = d.Vector3("space_position"); _totalCreated++;
    }
    public void MaintainPopulation()
    {
        var revisionField = typeof(Battlefield).GetField("_revision", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (FrozenProfilesLoaded && (long)revisionField.GetValue(_battle)! != _game.FactoryStatsRevision)
        {
            typeof(Battlefield).GetMethod("RefreshConfiguration", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_battle, null);
            LoadFrozenProfiles();
        }
        _battle.SyncFleet(); _counts.Clear(); AircraftWithTargets = 0;
        foreach (var d in _battle.Drones)
        {
            _counts[d.L("factory_site_id")] = _counts.GetValueOrDefault(d.L("factory_site_id")) + 1;
            ObserveParticipation(d);
        }
        PeakAircraftWithTargets = Math.Max(PeakAircraftWithTargets, AircraftWithTargets);
        foreach (var f in _battle.Factories.Values)
        {
            long site = f.L("site_id"); if (!_targetBySite.TryGetValue(site, out int target)) continue;
            f["capacity"] = _capacityBySite[site];
            for (int i = _counts.GetValueOrDefault(site); i < target; i++) SpawnFriendly(f);
        }
        if (Mode == FleetStressMode.Combat)
            while (_battle.Enemies.Count < EnemyCount)
            {
                int index = _generation++; double angle = index * 2.39996323, radius = .10 + .035 * (index % 6);
                var n = new Vector3((float)(radius * Math.Cos(angle)), (float)(radius * Math.Sin(angle)), 1).Normalized(); string role = DefenseWavePlan.RoleOrder[index % 8];
                long sourceSite = -1;
                if (Layout == FleetStressLayout.World)
                {
                    sourceSite = _surface.Records[index % _surface.Records.Count].L("site_id");
                    var factory = _battle.Factories[sourceSite];
                    var home = _battle.SurfaceToSpace(factory.Map("site").Vector3("normal"), 0).Normalized();
                    var east = CombatGeometry.AxisBetween(home, Vector3.Up); var north = home.Cross(east).Normalized();
                    double sideways = (.35 + .08 * (index % 6)) / (WorldScale.EarthRadius + 1.75);
                    n = (home + east * (float)(Math.Cos(angle) * sideways) + north * (float)(Math.Sin(angle) * sideways)).Normalized();
                }
                var e = _battle.SpawnEnemy(role is "claw" or "needle" or "prism" or "jammer" ? "scout" : "cruiser", new() { ["wave"] = 28L, ["stage"] = 0, ["role"] = role, ["index"] = index }, n * (float)(WorldScale.EarthRadius + 1.75 + .025 * (index % 8)))!;
                if (sourceSite >= 0) e["benchmark_factory_site_id"] = sourceSite;
                double shieldFraction = e.N("energy_max_hp") / e.N("max_hp"); e["hp"] = 4000d; e["max_hp"] = 4000d; e["energy_hp"] = 4000 * shieldFraction; e["energy_max_hp"] = e["energy_hp"]; e["fire"] = .05 * (index % 12); _totalEnemyCreated++;
            }
        // Synthetic load maintenance, outside the measured simulation: planet damage is
        // still processed and counted, but the benchmark must not end at game-over.
        _game.EarthHp = 1e9;
        PeakFriendlyCount = Math.Max(PeakFriendlyCount, _battle.Drones.Count); PeakEnemyCount = Math.Max(PeakEnemyCount, _battle.Enemies.Count);
    }
    public void CaptureCounters()
    {
        PeakProjectiles = Math.Max(PeakProjectiles, _battle.Shots.Count + _battle.HostileShots.Count);
        long current = 0; AircraftWithTargets = 0;
        foreach (var d in _battle.Drones) { current += d.L("total_weapon_shots"); ObserveParticipation(d); }
        ShotsFired = Math.Max(ShotsFired, current); PeakAircraftWithTargets = Math.Max(PeakAircraftWithTargets, AircraftWithTargets);
    }
    private void ObserveParticipation(DataMap drone)
    {
        bool targeting = drone.L("target_uid", -1) >= 0 || drone.L("aim_target_uid", -1) >= 0;
        if (targeting) { AircraftWithTargets++; _observedEngagedFactories.Add(drone.L("factory_site_id")); }
        if (drone.L("total_weapon_shots") > 0) { _observedFiringLives.Add(drone.L("uid")); _observedEngagedFactories.Add(drone.L("factory_site_id")); }
    }
}
