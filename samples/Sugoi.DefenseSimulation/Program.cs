using System.Diagnostics;
using System.Runtime.InteropServices;
using Sugoi.Data;
using Sugoi.Tasks;

namespace Sugoi.DefenseSimulation;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            int entities = ReadOption(args, "--entities", 5000, 128, 1_000_000);
            int frames = ReadOption(args, "--frames", 120, 1, 100_000);
            int workers = ReadOption(args, "--workers", Math.Min(4, Environment.ProcessorCount), 1, 256);
            if (args.Contains("--help"))
            {
                Console.WriteLine("Sugoi.DefenseSimulation [--entities 5000] [--frames 120] [--workers 4]");
                Console.WriteLine("Standalone ECS business sample. No Godot, rendering, persistence, or all-pairs target search.");
                return 0;
            }
            await using var simulation = new DefenseSimulation(entities, workers);
            await simulation.InitializeAsync();
            await simulation.RunAsync(frames);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int ReadOption(string[] args, string name, int fallback, int minimum, int maximum)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0) return fallback;
        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int value) || value < minimum || value > maximum)
            throw new ArgumentException($"{name} requires an integer in [{minimum}, {maximum}].");
        return value;
    }
}

internal sealed class DefenseSimulation : IAsyncDisposable
{
    private readonly EcsRuntime _runtime = new();
    private readonly Scheduler _scheduler;
    private readonly World _main;
    private readonly GenerationBatch _generation;
    private readonly int _desiredHumans, _desiredAliens;
    private readonly List<Entity> _factories = new();
    private readonly HashSet<Entity> _rewardedDeaths = new();
    private readonly Query _movement, _humanSnapshots, _alienSnapshots, _humanAttacks, _alienAttacks;
    private readonly Dictionary<Entity, float> _damage = new();
    private readonly HashSet<int> _shots = new();
    private readonly List<Entity> _deaths = new();
    private int _nextSerial, _activeAliens;
    private long _humanDeaths, _alienDeaths, _credits, _births, _replacements, _canceledBirths, _duplicateHits, _duplicateRewards, _appliedHits;
    private HitResult _previousHit;

    internal DefenseSimulation(int entities, int workers)
    {
        Sugoi.Generated.Sugoi_DefenseSimulationModule.Register(_runtime.Types);
        _scheduler = new(workers);
        _main = _runtime.CreateWorld();
        _generation = new(_runtime);
        _desiredAliens = Math.Max(32, entities / 5);
        _desiredHumans = entities - _desiredAliens;
        var movement = new MovementJob { DeltaTime = 1f / 60 };
        var humanSnapshot = new SnapshotJob { FleetTag = _runtime.Types.Get<Human>(), Output = Array.Empty<TargetSnapshot>() };
        var alienSnapshot = new SnapshotJob { FleetTag = _runtime.Types.Get<Alien>(), Output = Array.Empty<TargetSnapshot>() };
        var humanAttack = new AttackJob { FleetTag = _runtime.Types.Get<Human>(), Targets = Array.Empty<TargetSnapshot>(), Results = Array.Empty<HitResult>() };
        var alienAttack = new AttackJob { FleetTag = _runtime.Types.Get<Alien>(), Targets = Array.Empty<TargetSnapshot>(), Results = Array.Empty<HitResult>() };
        _movement = _scheduler.CreateQuery(_main, in movement);
        _humanSnapshots = _scheduler.CreateQuery(_main, in humanSnapshot);
        _alienSnapshots = _scheduler.CreateQuery(_main, in alienSnapshot);
        _humanAttacks = _scheduler.CreateQuery(_main, in humanAttack);
        _alienAttacks = _scheduler.CreateQuery(_main, in alienAttack);
    }

    internal async Task InitializeAsync()
    {
        SpawnSpec[] specs = CreateInitialSpecs(out Entity canceledFactory);
        var generation = _scheduler.Executor.RunAsync(() => _generation.Build(specs));
        // The worker reads only SpawnSpec. The main-world owner can independently destroy this factory.
        _main.Destroy(canceledFactory);
        await generation;
        CommitBirths(initial: true);
        Validate();
        if (_canceledBirths != 4) throw new InvalidOperationException("The destroyed-factory birth cancellation scenario failed.");
    }

    private SpawnSpec[] CreateInitialSpecs(out Entity canceledFactory)
    {
        var factoryType = new EntityType(_runtime.Types.Get<Factory>());
        int count = Math.Min(32, Math.Max(1, _desiredHumans / 128));
        var specs = new List<SpawnSpec>(count + 2);
        for (int i = 0; i < count; i++)
        {
            int capacity = _desiredHumans / count + (i < _desiredHumans % count ? 1 : 0);
            var factory = _main.Create(factoryType);
            _main.Set(factory, new Factory { Capacity = capacity });
            _factories.Add(factory);
            specs.Add(PlanSpawn(capacity, FleetSide.Human, factory));
        }
        specs.Add(PlanSpawn(_desiredAliens, FleetSide.Alien, Entity.Null));
        canceledFactory = _main.Create(factoryType);
        _main.Set(canceledFactory, new Factory { Capacity = 4 });
        specs.Add(PlanSpawn(4, FleetSide.Human, canceledFactory));
        return specs.ToArray();
    }

    internal async Task RunAsync(int frames)
    {
        var times = new double[frames];
        using var process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        for (int frame = 0; frame < frames; frame++)
        {
            long start = Stopwatch.GetTimestamp();
            SpawnSpec[] replacements = PlanReplacements(frame);
            if (replacements.Length != 0)
            {
                await _scheduler.Executor.RunAsync(() => _generation.Build(replacements));
                CommitBirths(initial: false);
            }

            var batch = new CombatFrame(_humanSnapshots.Count, _alienSnapshots.Count);
            await DispatchCombatAsync(frame, batch);
            // All uses have ended: this coordinator alone owns the immediate main-world structural stage.
            CommitCombat(frame, batch);
            times[frame] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        await _scheduler.SyncAllAsync();
        double cpuMilliseconds = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
        long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        Validate();
        Array.Sort(times);
        Console.WriteLine($"Validated {frames} simulation steps; initial aircraft {_desiredHumans + _desiredAliens:N0}; workers {_scheduler.Executor.WorkerCount}.");
        Console.WriteLine($"Alive: human {_humanSnapshots.Count:N0}, alien {_activeAliens:N0}; factories {_factories.Count}.");
        Console.WriteLine($"Published births {_births:N0}; replacements {_replacements:N0}; canceled unpublished births {_canceledBirths:N0}.");
        Console.WriteLine($"Applied unique hits {_appliedHits:N0}; human deaths {_humanDeaths:N0}; alien deaths {_alienDeaths:N0}; credits {_credits:N0}.");
        Console.WriteLine($"Rejected duplicate/stale hits {_duplicateHits:N0}; duplicate reward claims {_duplicateRewards:N0}.");
        Console.WriteLine($"Wall step: median {times[frames / 2]:F3} ms, p95 {times[Math.Min(frames - 1, (int)(frames * 0.95))]:F3} ms.");
        Console.WriteLine($"Process CPU: {cpuMilliseconds:F1} ms total, {cpuMilliseconds / frames:F3} ms/step; managed allocation {allocated / (double)frames:F0} B/step.");
        Console.WriteLine("Checks passed: generation/import identities, cyclic references, external factory association, capacity, generation-safe hits, reward deduplication, and frame ownership.");
        Console.WriteLine("This measures the standalone ECS scenario; it is not Godot gameplay, rendering performance, or a game FPS claim.");
    }

    private async Task DispatchCombatAsync(int frame, CombatFrame batch)
    {
        var options = new TaskOptions { BatchSize = 512 };
        var move = new MovementJob { DeltaTime = 1f / 60 };
        var movement = _scheduler.Dispatch(_movement, in move, options);
        var humanSnapshot = new SnapshotJob { FleetTag = _runtime.Types.Get<Human>(), Output = batch.HumanTargets };
        var alienSnapshot = new SnapshotJob { FleetTag = _runtime.Types.Get<Alien>(), Output = batch.AlienTargets };
        var snapshotHumans = _scheduler.Dispatch(_humanSnapshots, in humanSnapshot, options);
        var snapshotAliens = _scheduler.Dispatch(_alienSnapshots, in alienSnapshot, options);
        var humanAttack = new AttackJob { FleetTag = _runtime.Types.Get<Human>(), Frame = frame, Targets = batch.AlienTargets, Results = batch.HumanHits };
        var alienAttack = new AttackJob { FleetTag = _runtime.Types.Get<Alien>(), Frame = frame, Targets = batch.HumanTargets, Results = batch.AlienHits };
        // Snapshot arrays are business-owned outputs, so their completion is an explicit admission dependency.
        var humanFire = _scheduler.Dispatch(_humanAttacks, in humanAttack, options with { After = new[] { snapshotAliens } });
        var alienFire = _scheduler.Dispatch(_alienAttacks, in alienAttack, options with { After = new[] { snapshotHumans } });
        await Task.WhenAll(movement.AsTask(), snapshotHumans.AsTask(), snapshotAliens.AsTask(), humanFire.AsTask(), alienFire.AsTask());
    }

    private SpawnSpec[] PlanReplacements(int frame)
    {
        var specs = new List<SpawnSpec>();
        foreach (var entity in _factories)
        {
            ref var factory = ref _main.Get<Factory>(entity);
            if (factory.RebuildSteps > 0) { factory.RebuildSteps--; continue; }
            int missing = factory.Capacity - factory.ActiveAircraft;
            if (missing > 0) specs.Add(PlanSpawn(missing, FleetSide.Human, entity));
        }
        if (frame % 12 == 0 && _activeAliens < _desiredAliens)
            specs.Add(PlanSpawn(_desiredAliens - _activeAliens, FleetSide.Alien, Entity.Null));
        return specs.ToArray();
    }

    private SpawnSpec PlanSpawn(int count, FleetSide side, Entity factory)
    {
        var spec = new SpawnSpec(count, side, factory, _nextSerial);
        _nextSerial = checked(_nextSerial + count);
        return spec;
    }

    private void CommitBirths(bool initial)
    {
        ImportResult result = _generation.ImportInto(_main);
        _activeAliens += result.PublishedAlien;
        _births += result.PublishedHuman + result.PublishedAlien;
        if (!initial) _replacements += result.PublishedHuman + result.PublishedAlien;
        _canceledBirths += result.Canceled;
    }

    private void CommitCombat(int frame, CombatFrame batch)
    {
        _damage.Clear(); _shots.Clear(); _deaths.Clear();
        CollectHits(frame, batch.HumanHits);
        CollectHits(frame, batch.AlienHits);
        // Inject a repeated delivery and a delayed delivery to exercise business deduplication every step.
        var duplicate = FirstHit(batch.HumanHits);
        if (duplicate.Target.IsNull) duplicate = FirstHit(batch.AlienHits);
        AddHit(frame, in duplicate);
        AddHit(frame, in _previousHit);
        _previousHit = duplicate;

        foreach (var pair in _damage)
        {
            if (!_main.Exists(pair.Key) || !_main.Has<Hull>(pair.Key)) continue;
            ref var hull = ref _main.Get<Hull>(pair.Key);
            hull.Value -= pair.Value;
            if (hull.Value <= 0) _deaths.Add(pair.Key);
        }

        foreach (var entity in _deaths)
        {
            var aircraft = _main.Read<Aircraft>(entity);
            RewardOnce(entity, aircraft.Side);
            RewardOnce(entity, aircraft.Side); // The second claim must never grant a second reward.
            if (aircraft.Side == FleetSide.Human)
            {
                _humanDeaths++;
                if (_main.Exists(aircraft.Factory))
                {
                    ref var factory = ref _main.Get<Factory>(aircraft.Factory);
                    factory.ActiveAircraft--;
                    if (factory.RebuildSteps == 0) factory.RebuildSteps = 3;
                }
            }
            else { _alienDeaths++; _activeAliens--; }
        }
        if (_deaths.Count != 0) _main.Destroy(CollectionsMarshal.AsSpan(_deaths));
    }

    private void CollectHits(int frame, HitResult[] results)
    { foreach (ref readonly var hit in results.AsSpan()) AddHit(frame, in hit); }

    private void AddHit(int frame, in HitResult hit)
    {
        if (hit.Target.IsNull) return;
        if (hit.Frame != frame || !_shots.Add(hit.ShotSerial)) { _duplicateHits++; return; }
        // Exists uses the entire Entity64, so late hits cannot affect another entity reusing the same index.
        if (!_main.Exists(hit.Target) || !_main.Exists(hit.Attacker)) return;
        _damage.TryGetValue(hit.Target, out float accumulated);
        _damage[hit.Target] = accumulated + hit.Damage;
        _appliedHits++;
    }

    private void RewardOnce(Entity entity, FleetSide side)
    {
        if (!_rewardedDeaths.Add(entity)) { _duplicateRewards++; return; }
        if (side == FleetSide.Alien) _credits += 10;
    }

    private static HitResult FirstHit(HitResult[] results)
    { foreach (ref readonly var result in results.AsSpan()) if (!result.Target.IsNull) return result; return default; }

    private void Validate()
    {
        if (_main.ActiveUsers != 0) throw new InvalidOperationException("A frame released its output buffers before its jobs ended.");
        var counts = new Dictionary<Entity, int>();
        foreach (var factory in _factories) counts.Add(factory, 0);
        int human = 0, alien = 0;
        foreach (var view in _movement)
        {
            var aircraft = view.ReadOwned<Aircraft>();
            var hull = view.ReadOwned<Hull>();
            for (int i = 0; i < view.Count; i++)
            {
                if (hull[i].Value <= 0) throw new InvalidOperationException("A destroyed aircraft remained in combat storage.");
                if (aircraft[i].Side == FleetSide.Human)
                {
                    if (!counts.ContainsKey(aircraft[i].Factory) || !_main.Exists(aircraft[i].Factory))
                        throw new InvalidOperationException("Published aircraft has an invalid factory owner.");
                    counts[aircraft[i].Factory]++; human++;
                }
                else alien++;
            }
        }
        foreach (var entity in _factories)
        {
            var factory = _main.Read<Factory>(entity);
            if (counts[entity] != factory.ActiveAircraft || factory.ActiveAircraft < 0 || factory.ActiveAircraft > factory.Capacity)
                throw new InvalidOperationException("Factory capacity accounting diverged from the actual aircraft.");
        }
        if (human > _desiredHumans || alien != _activeAliens || alien > _desiredAliens || _credits != _alienDeaths * 10)
            throw new InvalidOperationException("Fleet or reward accounting is inconsistent.");
        if (_rewardedDeaths.Count != _humanDeaths + _alienDeaths || _duplicateRewards != _rewardedDeaths.Count)
            throw new InvalidOperationException("Death/reward deduplication failed.");
    }

    public async ValueTask DisposeAsync()
    {
        try { await _scheduler.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            _generation.Dispose();
            _movement.Dispose(); _humanSnapshots.Dispose(); _alienSnapshots.Dispose(); _humanAttacks.Dispose(); _alienAttacks.Dispose();
            _main.Dispose(); _runtime.Dispose();
        }
    }
}
