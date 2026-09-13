# Standalone defense simulation

This executable demonstrates an actual business loop on `Sugoi.Data` and `Sugoi.Tasks`. It targets .NET 8, has no Godot dependency, and references `Sugoi.SourceGen` as a build-time analyzer. Component registration, Entity field remapping and the movement job's typed span binding are generated statically.

Run from the repository root:

```powershell
dotnet run --project samples/Sugoi.DefenseSimulation/Sugoi.DefenseSimulation.csproj -c Release -- --entities 5000 --frames 120 --workers 4
```

`--entities` is the initial total number of combat aircraft: 80% human and 20% alien. Factories are additional entities. Defaults are 5,000 aircraft, 120 simulation steps and up to four logical workers. `--help` prints the command options. One step is a deterministic simulation tick, not a measured render frame.

## The complete loop

1. The coordinator creates human factories in the main World and produces value-only `SpawnSpec` input. Factory references in those specifications are external identity tokens, not borrowed components.
2. A generation task owns a **StagingWorld epoch**. It assigns transient identities and stores components in native per-type payload rows, with cyclic transient wingman references. It never reads main-world components. The same staging rows and payload blocks are reused for replacement batches.
3. After the producer and main-world jobs finish, `StagingWorld.Apply` reserves all real identities, patches references, and creates the entities in the main World. A structural sink reads the business `BirthTicket` component to correlate each created identity with its batch. This sample does not use `MergeFrom` or a second ordinary World as a staging substitute.
4. External factory associations are validated against the main World **after Apply and before birth publication**. A deliberately destroyed factory cancels four unpublished births without incrementing capacity. This exercises the case where a factory disappears while its staging job is running.
5. Movement runs as a generated task. Two snapshot tasks read current positions and fill frame-owned target arrays. Human and alien attack jobs gate admission on the opposing snapshot's completion; component declarations retain the movement-to-position-read hazards.
6. Every attacker checks at most eight candidates and writes at most one `HitResult` to its own output slot. A `CombatFrame` owns all target and hit arrays until **all five submitted jobs and their batches finish**. The sample does not perform an all-pairs target search, use a global append lock, or race writers against target health.
7. The coordinator enters the structural phase. It rejects duplicate or stale hit IDs, validates complete Entity64 targets, sums damage once per target, collects deaths, updates factory capacity, and grants each dead alien's reward once. Repeated hit and reward deliveries are intentionally injected to check deduplication.
8. `World.Destroy` runs immediately at that safe phase. Factories rebuild missing aircraft after a three-step delay. Alien replacements are staged every twelve steps. Apply clears records and transient counters while retaining the staging payload blocks. Transient identities never escape their owning batch into later epochs. There is no serialization or universal command buffer.
9. Only after structure changes and birth association/publication finish does the next step call `Dispatch`.

## Output and validation

Successful runs print initial/alive counts, births, replacements, canceled births, unique hits, deaths, credits and rejected duplicates. Final checks compare each factory's reported population with the actual aircraft storage and assert `credits == unique alien deaths * 10`.

The timing output separates:

- **Wall-step median/p95:** staging, Apply, scheduling, combat and structural commit latency.
- **Process CPU milliseconds:** accumulated CPU time across the executable's threads, both total and per step. This can exceed wall time because workers execute concurrently.
- **Managed bytes per step:** total managed allocation across threads, including snapshots, result buffers and scheduling metadata. Native chunk storage is separate.

Initialization is outside the timed loop. The first timed steps include cold task/query effects; there is no hidden discarded gameplay period. The sample uses deterministic target hashing and exact integer-valued damage, but does not claim that general floating-point reductions or arbitrary job completion ordering are deterministic.

These numbers describe this standalone ECS scenario. They do not establish Godot render FPS, reproduce EarthDefense's full targeting/weapon logic, or claim the game's existing combat system has been migrated.

## Files

- `Components.cs`: generated registrations and Entity-reference-bearing aircraft data.
- `GenerationBatch.cs`: StagingWorld production, Apply and structural-sink birth receipts, reference validation and factory binding.
- `CombatJobs.cs`: generated movement, snapshots, bounded targeting and frame-owned results.
- `Program.cs`: coordinator, immediate structural phase, damage/reward reduction, replacement planning, checks and metrics.

Generation/Apply failures are terminal for the sample and return a nonzero exit code. Apply is not a transaction rollback protocol. Output buffers, staging storage and the World remain owned until work drains during disposal.
