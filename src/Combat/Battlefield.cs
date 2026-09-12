using System;
using System.Collections.Generic;
using System.Linq;
using Earthward.Domain;
using Godot;

namespace Earthward.Combat;

/// <summary>Managed authoritative battle. Rendering, UI and engine nodes are consumers only.</summary>
public sealed partial class Battlefield
{
    public DefenseState Game { get; set; } = null!;
    public ICombatSurface? Surface
    {
        get; set;
    }
    public CombatPerformanceCounters? Performance { get; set; }
    public bool Active
    {
        get; set;
    }
    public bool Paused
    {
        get; set;
    }
    public double SpeedScale { get; set; } = 1;
    public bool WaveRunning
    {
        get; private set;
    }
    public bool Dead
    {
        get; private set;
    }
    public bool InvasionWon
    {
        get; private set;
    }
    public double Clock
    {
        get; private set;
    }
    public long DestroyedDrones
    {
        get; private set;
    }
    public long NumberSequence
    {
        get; private set;
    }
    public long WaveRemaining
    {
        get; private set;
    }
    public long WaveTotal
    {
        get; private set;
    }
    public CombatRandom Random { get; } = new(BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)));
    public List<DataMap> Drones { get; } = new();
    public List<DataMap> Enemies { get; } = new();
    public Dictionary<string, DataMap> Motherships { get; } = new(StringComparer.Ordinal);
    public Dictionary<long, DataMap> Factories { get; } = new();
    public List<DataMap> Shots { get; } = new();
    public List<DataMap> HostileShots { get; } = new();
    public List<DataMap> Beams { get; } = new();
    public List<DataMap> Bursts { get; } = new();
    public List<DataMap> DamageNumbers { get; } = new();
    public List<DataMap> FactoryActivity { get; } = new();
    public Dictionary<long, Vector3> WorldCache { get; } = new();
    public event Action<long, DataMap>? WaveStarted;
    public event Action? WaveCompleted, EarthDestroyed, InvasionDefeated;
    public event Action<string>? EventNotice, PostDefenseWaveCompleted;
    public event Action<double, Vector3>? EarthDamaged;
    public event Action? RenderDirty;

    private InvasionDirector _invasion = new();
    private long _nextUid = 1, _revision = -1, _motherWave = -1;
    private int _epoch;
    // Reset/restore starts a new actor timeline; ephemeral HUD notifications
    // must not retain identities from a previous use of the same actor UIDs.
    public int TimelineEpoch => _epoch;
    private double _chipSettlementClock;
    private bool _layoutDirty = true, _postDefense, _fixedRunning, _cohortComplete;
    private int _postSpawned;
    private DataMap _postPlan = new(), _wavePlan = new(), _globalWeapons = new();
    private double _cycleElapsed, _spawnDuration, _spawnElapsed, _segmentStart, _spawnClock, _assignmentClock;
    private long _spawned, _initialCount, _segmentCount, _segmentSpawned;
    private bool _spawnCancelled, _bossSpawned, _smallBossSpawned;
    private readonly Dictionary<long, DataMap> _enemyById = new(), _droneById = new();
    private readonly Dictionary<long, Profile> _factoryProfiles = new();
    private readonly Dictionary<long, Profile> _actorProfiles = new();
    private readonly Dictionary<(long, int), List<(int Berth, string Frame, int Cost)>> _productionPlans = new();
    private readonly Dictionary<(long, string), double> _productionIntervals = new();
    private readonly Dictionary<(long, int, string), Profile> _aircraftProfiles = new();
    private readonly Dictionary<string, DataMap> _frames = new();
    private readonly List<DataMap> _targets = new();
    private readonly Dictionary<Vector3I, List<DataMap>> _enemySectors = new(), _projectileSectors = new();
    private readonly List<Vector3I> _sectorKeys = new();
    private bool _sectorsDirty = true;
    private long _layoutHash;
    private sealed class Profile
    {
        public DataMap Weapons = new(), Patrol = new();
        public double Range, Radial, Cosine, Angle;
        public string Frame = "K1", Kind = "interceptor";
        public DataMap? DerivedWeapons;
        public ArmorMatchSignature ArmorSignature;
        public double AssignedPower;
    }
    public Battlefield()
    {
    }
    public Battlefield(DefenseState game, ICombatSurface? surface = null)
    {
        Game = game;
        Surface = surface;
        RefreshConfiguration();
        SyncFleet();
    }
    private long NewUid() => _nextUid++;
    private static string BaseFrame(string kind) => kind == "laser" ? "L1" : kind == "missile" ? "M1" : "K1";
    private DataMap Frame(string id)
    {
        if (!_frames.TryGetValue(id, out var frame))
        {
            frame = AirframeCatalog.Definition(id);
            _frames[id] = frame;
        }
        return frame;
    }
    public void Step(double delta)
    {
        if (Game == null) return;
        _chipSettlementClock += double.IsFinite(delta) ? Math.Max(0, delta) : 0;
        if (_chipSettlementClock >= 1)
        {
            _chipSettlementClock = 0;
            if (!Game.FlushAlienChipDrops())
            {
                EventNotice?.Invoke("外星芯片暂未保存，正在重试 · " + Game.FactoryPerks.LastError);
                _chipSettlementClock = -4;
            }
        }
        if (Paused) return;
        var timing = Performance;
        long mark = timing?.BeginFrame() ?? 0;
        RefreshConfiguration();
        SyncFleet();
        if (timing != null) mark = timing.Record(CombatStage.Configuration, mark);
        RefreshWorldSites();
        RebuildActorIndex();
        if (timing != null) mark = timing.Record(CombatStage.WorldSynchronization, mark);
        double dt = Math.Min(delta, .06) * SpeedScale;
        Clock += dt;
        UpdateEffects(dt);
        UpdateLocalShields(dt);
        if (timing != null) mark = timing.Record(CombatStage.EffectsAndShields, mark);
        if (!Dead)
        {
            if (Active)
                UpdateFactories(dt);
            if (timing != null) mark = timing.Record(CombatStage.Factories, mark);
            UpdateDrones(dt);
            if (timing != null) mark = timing.Record(CombatStage.Drones, mark);
        }
        if (Active && !Dead)
        {
            if (_fixedRunning)
                AdvanceFixedSchedule(Math.Max(delta, 0) * SpeedScale);
            if (Enemies.Count > 0)
                UpdateEnemies(dt);
            if (timing != null) mark = timing.Record(CombatStage.EnemyAI, mark);
            if (!InvasionWon && (WaveRunning || Motherships.Count > 0 || Shots.Count > 0 || HostileShots.Count > 0))
            {
                RebuildTargetSectors();
                if (timing != null) mark = timing.Record(CombatStage.SpatialIndex, mark);
                UpdateFiring(dt);
                if (timing != null) mark = timing.Record(CombatStage.Weapons, mark);
                UpdateShots(dt);
                if (timing != null) mark = timing.Record(CombatStage.Projectiles, mark);
                if (!Paused) CheckWaveEnd();
            }
        }
        RenderDirty?.Invoke();
    }
    public void ResetBattle()
    {
        _epoch++;
        Active = false;
        Paused = false;
        SpeedScale = 1;
        WaveRunning = false;
        Dead = false;
        InvasionWon = false;
        Clock = 0;
        DestroyedDrones = 0;
        NumberSequence = 0;
        _nextUid = 1;
        Drones.Clear();
        Enemies.Clear();
        Shots.Clear();
        HostileShots.Clear();
        Beams.Clear();
        Bursts.Clear();
        DamageNumbers.Clear();
        _localShields.Clear();
        _localShieldView.Clear();
        _localShieldRevision = -1;
        Factories.Clear();
        FactoryActivity.Clear();
        Motherships.Clear();
        WorldCache.Clear();
        _enemyById.Clear();
        _droneById.Clear();
        _invasion = new();
        _postDefense = false;
        _postPlan = new();
        _postSpawned = 0;
        _fixedRunning = false;
        _cohortComplete = false;
        _cycleElapsed = 0;
        _wavePlan = new();
        WaveRemaining = WaveTotal = _spawned = _initialCount = _segmentCount = _segmentSpawned = 0;
        _spawnElapsed = _spawnDuration = _segmentStart = _spawnClock = _assignmentClock = 0;
        _spawnCancelled = false;
        _smallBossSpawned = _bossSpawned = false;
        _revision = -1;
        _motherWave = -1;
        _layoutDirty = true;
        RefreshConfiguration();
        SyncFleet();
    }
    public void InvalidateFactoryLayout()
    {
        _layoutDirty = true;
        _revision = -1;
    }
    public IReadOnlyList<DataMap> GetFactorySites()
    {
        if (Surface != null)
            return Surface.GetFactorySites();
        var result = new List<DataMap>();
        int global = 0;
        foreach (var kind in new[] { "interceptor", "laser", "missile" })
        {
            int count = (int)Math.Min(int.MaxValue, Game.Buildings.L(kind));
            for (int i = 0; i < count; i++)
            {
                double angle = global * 2.39996323 + .45;
                var normal = C.Vec(Math.Sin(angle), Math.Sin(angle * .71) * .45, Math.Cos(angle)).Normalized();
                var tangent = CombatGeometry.AxisBetween(normal, Vector3.Up).Cross(normal).Normalized();
                long id = -(1L + i + (kind == "interceptor" ? 0 : kind == "laser" ? 1000000 : 2000000));
                result.Add(new()
                {
                    ["site_id"] = id,
                    ["kind"] = kind,
                    ["normal"] = normal,
                    ["launch_position"] = C.Scale(normal, CombatScale.EarthRadius + .108),
                    ["launch_direction"] = tangent
                });
                global++;
            }
        }
        return result;
    }
    private void RefreshConfiguration()
    {
        if (Game == null || _revision == Game.FactoryStatsRevision)
            return;
        _revision = Game.FactoryStatsRevision;
        _globalWeapons = Game.DroneStats();
        _coverageSnapshots.Clear();
        _factoryProfiles.Clear();
        _aircraftProfiles.Clear();
        _actorProfiles.Clear();
        _assignmentGroups.Clear();
        _armorMatchGroups.Clear();
        _productionPlans.Clear();
        _productionIntervals.Clear();
        _layoutDirty = true;
        foreach (var site in GetFactorySites())
        {
            long id = C.L(site, "site_id");
            string kind = C.S(site, "kind");
            _factoryProfiles[id] = BuildProfile(kind, id, -1, Game.FactoryAirframe(kind, id));
        }
        foreach (var d in Drones)
        {
            var p = GetProfile(d);
            double fraction = C.N(d, "hp") / Math.Max(.001, C.N(d, "max_hp"));
            d["max_hp"] = C.N(p.Patrol, "health", 45);
            d["hp"] = C.N(d, "max_hp") * fraction;
            d["hit_radius"] = .11 * GetDroneScale() * C.N(p.Weapons, "scale_multiplier", 1);
        }
    }
    private Profile BuildProfile(string kind, long site, int berth, string physical)
    {
        var weapons = Game.AircraftDroneStats(kind, site, berth, physical);
        var patrol = Game.AircraftPatrolStats(kind, site, berth, physical);
        weapons["_combat_effects"] = Capture(weapons, PerkEffectKeys);
        weapons["_combat_tech_values"] = Capture(weapons, TechPacketKeys);
        double angle = Math.Min(Math.PI, (C.N(patrol, "patrol_radius") + C.N(patrol, "patrol_outer_range")) / CombatScale.EarthRadius);
        return new()
        {
            Kind = kind,
            Frame = physical,
            Weapons = weapons,
            Patrol = patrol,
            Range = WeaponRangeForStats(kind, weapons) * C.N(weapons, "frame_range_multiplier", 1),
            Radial = Math.Max(CombatScale.EarthRadius + CombatScale.DroneAltitude + C.N(patrol, "patrol_outer_range"), C.N(patrol, "action_radius")),
            Angle = angle,
            Cosine = Math.Cos(angle)
        };
    }
    private Profile GetProfile(DataMap d)
    {
        if (_flightContext is { } flight && ReferenceEquals(flight.Owner, this) && ReferenceEquals(flight.Actor, d)) return flight.Profile;
        long uid = C.L(d, "uid");
        if (_actorProfiles.TryGetValue(uid, out var cached))
            return cached;
        long site = C.L(d, "factory_site_id");
        int berth = C.I(d, "patrol_slot");
        string kind = C.S(d, "kind"), frame = C.S(d, "airframe_id", BaseFrame(kind));
        var profile = GetLoadoutProfile(kind, site, berth, frame);
        _actorProfiles[uid] = profile;
        return profile;
    }
    private Profile GetLoadoutProfile(string kind, long site, int berth, string frame)
    {
        if (_factoryProfiles.TryGetValue(site, out var common) && frame == common.Frame && !Game.AircraftHasOverride(kind, site, berth))
            return common;
        var key = (site, berth, frame);
        if (!_aircraftProfiles.TryGetValue(key, out var profile))
        {
            profile = BuildProfile(kind, site, berth, frame);
            _aircraftProfiles[key] = profile;
        }
        return profile;
    }
    public DataMap DroneWeaponStats(DataMap d) => GetProfile(d).Weapons;
    public DataMap DronePatrolStats(DataMap d) => GetProfile(d).Patrol;
    public void SyncFleet()
    {
        if (Game == null)
            return;
        var sites = GetFactorySites();
        long hash = sites.Count;
        foreach (var site in sites)
            hash = unchecked(hash * 397 + C.L(site, "site_id") * 31 + C.S(site, "kind").GetHashCode(StringComparison.Ordinal));
        if (hash != _layoutHash)
        {
            _layoutHash = hash;
            _layoutDirty = true;
        }
        if (!_layoutDirty)
            return;
        var valid = new HashSet<long>();
        foreach (var site in sites)
        {
            long id = C.L(site, "site_id");
            string kind = C.S(site, "kind");
            valid.Add(id);
            if (!Factories.TryGetValue(id, out var f) || C.S(f, "kind") != kind)
            {
                f = new()
                {
                    ["site_id"] = id,
                    ["kind"] = kind,
                    ["site"] = site,
                    ["timer"] = 0d,
                    ["door"] = 0d,
                    ["capacity"] = Game.FactoryCapacityForSite(kind, id),
                    ["active"] = 0
                };
                Factories[id] = f;
            }
            else
            {
                f["site"] = site;
                f["capacity"] = Game.FactoryCapacityForSite(kind, id);
            }
            if (!_factoryProfiles.ContainsKey(id))
                _factoryProfiles[id] = BuildProfile(kind, id, -1, Game.FactoryAirframe(kind, id));
        }
        foreach (long id in Factories.Keys.ToArray())
            if (!valid.Contains(id))
                Factories.Remove(id);
        Drones.RemoveAll(d => !Factories.TryGetValue(C.L(d, "factory_site_id"), out var f) || C.S(f, "kind") != C.S(d, "kind"));
        _layoutDirty = false;
        EnsureInvasionAnchor();
        RefreshWorldSites();
    }
    private void RefreshWorldSites()
    {
        _defendersValid = false;
        if (Surface != null)
            foreach (var site in Surface.GetFactorySites())
                if (Factories.TryGetValue(C.L(site, "site_id"), out var f))
                    f["site"] = site;
        foreach (var f in Factories.Values)
        {
            var normal = SurfaceToSpace(C.V(C.M(f, "site"), "normal"), 0).Normalized();
            var east = CombatGeometry.AxisBetween(normal, Vector3.Up);
            f["_world_normal"] = normal;
            f["_patrol_east"] = east;
            f["_patrol_north"] = normal.Cross(east).Normalized();
        }
        WorldCache.Clear();
        foreach (var d in Drones)
            WorldCache[C.L(d, "uid")] = RawDronePosition(d);
    }
    private void RebuildActorIndex()
    {
        _droneById.Clear();
        for (int i = 0; i < Drones.Count; i++)
        {
            var d = Drones[i];
            d["_actor_order"] = i;
            _droneById[C.L(d, "uid")] = d;
        }
        RebuildTargets();
    }
    private Vector3 RawDronePosition(DataMap d) => d.ContainsKey("space_position") ? C.V(d, "space_position") : SurfaceToSpace(C.V(d, "normal"), C.N(d, "altitude", CombatScale.DroneAltitude));
    public Vector3 GetDroneWorldPosition(DataMap d)
    {
        if (_flightContext is { } flight && ReferenceEquals(flight.Owner, this) && ReferenceEquals(flight.Actor, d)) return flight.Position;
        return WorldCache.TryGetValue(C.L(d, "uid"), out var position) ? position : RawDronePosition(d);
    }
    private void CachePosition(DataMap d)
    {
        var position = RawDronePosition(d);
        if (_flightContext is { } flight && ReferenceEquals(flight.Owner, this) && ReferenceEquals(flight.Actor, d)) flight.Position = position;
        else WorldCache[C.L(d, "uid")] = position;
    }
    public DataMap? GetDroneByUid(long uid)
    {
        if (uid < 0) return null;
        if (_droneById.TryGetValue(uid, out var drone)) return C.N(drone, "hp") > 0 ? drone : null;
        // Public fixture/editor callers may append actors directly before the next index rebuild.
        return _droneById.Count == Drones.Count ? null : Drones.Find(x => C.L(x, "uid") == uid && C.N(x, "hp") > 0);
    }
    public bool IsLiveDrone(long uid) => GetDroneByUid(uid) != null;
    public int GetActiveDroneCount() => Drones.Count;
    public double GetDroneScale() => C.N(Game.CombatSettings, "aircraft_scale", .5);
    public double GetEnemyScale() => C.N(Game.CombatSettings, "enemy_aircraft_scale", .5);
    public Vector3 SurfaceToSpace(Vector3 normal, double altitude = CombatScale.DroneAltitude)
    {
        if (_flightContext is { } flight && ReferenceEquals(flight.Owner, this)) return flight.Frame * C.Scale(normal.Normalized(), CombatScale.EarthRadius + altitude);
        return Surface?.SurfaceToSpace(normal, altitude) ?? C.Scale(normal.Normalized(), CombatScale.EarthRadius + altitude);
    }
    public Vector3 SpaceToSurface(Vector3 position)
    {
        if (_flightContext is { } flight && ReferenceEquals(flight.Owner, this)) return (flight.Inverse * position).Normalized();
        return Surface?.SpaceToSurface(position) ?? position.Normalized();
    }
    private Vector3 Home(DataMap d)
    {
        if (_flightContext is { } flight && ReferenceEquals(flight.Owner, this) && ReferenceEquals(flight.Actor, d)) return flight.Home;
        if (!Factories.TryGetValue(C.L(d, "factory_site_id"), out var factory))
            return GetDroneWorldPosition(d).Normalized();
        if (factory.TryGetValue("_world_normal", out var cached) && cached is Vector3 normal)
            return normal;
        return SurfaceToSpace(C.V(C.M(factory, "site"), "normal"), 0).Normalized();
    }
    private bool HasTech(string id) => C.B(C.M(_globalWeapons, "tech_abilities"), id);
    public double GetDroneWeaponRange(string kind) => WeaponRangeForStats(kind, _globalWeapons);
    private static double WeaponRangeForStats(string kind, DataMap stats) => ((kind == "interceptor" ? CombatScale.KineticRange * C.N(stats, "interceptor_range_multiplier", 1) : CombatScale.WeaponRange * C.N(stats, kind == "laser" ? "laser_range_multiplier" : "missile_range_multiplier", 1)) + C.N(stats, "weapon_range_bonus")) * C.N(stats, "all_target_range_multiplier", 1);
    private double TargetRangeForProfile(Profile profile, bool largeTarget) => profile.Range * (largeTarget && HasTech("C_N1") ? C.N(_globalWeapons, "large_target_range_multiplier", 1) : 1);
    private double MaximumTargetRadius(Profile profile, bool largeTarget) => profile.Radial + TargetRangeForProfile(profile, largeTarget);
    private double TargetRange(DataMap d, DataMap? target = null) => TargetRangeForProfile(GetProfile(d), target != null && C.Large(target));
    public bool CanDroneEngagePosition(DataMap d, Vector3 p) => CanEngage(d, p);
    private bool CanEngage(DataMap d, Vector3 position, DataMap? target = null)
    {
        if (C.N(d, "hp") <= 0 || C.N(d, "launch_age") < CombatScale.LaunchDuration || C.S(d, "state") is "launching" or "landing" or "repairing")
            return false;
        var origin = GetDroneWorldPosition(d);
        double range = TargetRange(d, target);
        return origin.DistanceSquaredTo(position) <= range * range && Home(d).Dot(position.Normalized()) >= GetProfile(d).Cosine && CombatGeometry.HasLineOfSight(origin, position);
    }
}
