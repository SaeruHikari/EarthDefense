# StagingWorld

This implementation follows `engine/modules/engine/runtime/include/SkrRuntime/ecs/staging.hpp` and `src/ecs/staging.cpp` in ExtremeEngine. The unrelated `src/sugoi/staging_world.cpp` is empty and is not the reference implementation.

Storage uses an entity lookup table, coalesced records, and one native payload row per component type. `StagingRowLayout` owns the original 256 KiB target block calculation, clamped to 256–4096 records per block. Payload stride remains the component storage size. A normal World is not used as staging storage.

```csharp
using var staging = new StagingWorld(runtime, entryCapacity: 16, maxStagingCount: 16);
Entity spawned = staging.NewEntity();
staging.Add(spawned, new Position { X = 12 });
staging.Add(existing, new Link { Target = spawned });

// Complete all producers and participating World jobs before this synchronous call.
staging.Apply(world, sink);
```

Capacity arguments are initial hints, not hard limits. Records, lookup arrays and payload blocks grow beyond them. Apply/Clear retain blocks for reuse; Dispose frees them.

## Operations and identity

- `Add<T>` replaces a previously staged ordinary value. If `T` is registered only as a buffer element, it appends an element. `Append<T>` is the explicit buffer form when the same native type has both registrations.
- `AddMove<T>` and `AppendMove<T>` transfer owning native values and end the caller's value lifetime. Copying requires the registered Copy hook when the value owns resources.
- `SetBuffer<T>` replaces the staged array. Add/Remove and meta changes cancel their opposite pending change rather than replaying a command list.
- `Destroy` targets existing identities and dominates that record's pending component values. `DestroyOwned` recursively targets meta-owned descendants.
- Query-scoped `AddTag`/`Remove` are evaluated in the query's World at Apply time. They act on matching groups before this epoch's spawns and do not emit per-record sink events.
- `UpdateFrom` copies source ordinary columns and retains target extras. `ReplaceFrom` additionally removes missing target ordinary columns. Both preserve caller-selected types and support an explicit complete-identity mapping. Tags, chunk singletons and meta are not copied by these helpers, matching the reference helper's `firstChunkComponent` range.
- A transient Entity64 uses generation `0xFFFFFFFF`; ordinary World registries cannot accept it. Null remains zero. `NewEntity` alone does not create a record; referencing an issued transient that has no record is an error, not an implicit Null.
- Apply resets transient indices. Transient values belong to one staging instance and epoch and must not be kept into later epochs. Destroying a transient is rejected, as in the reference API.

## Apply order and notifications

1. Apply query-scoped structural deltas.
2. Reserve real identities for all transient records.
3. Patch transient references in payloads and meta.
4. Create consecutive equal-signature spawn batches and move their payloads.
5. Apply existing-entity deltas and replacement payloads.
6. Collect recursive meta targets plus explicit destroys, deduplicate, notify, and destroy in chunk/descending-row batches.
7. Destroy unconsumed staged payloads, clear records, and reset epoch counters.

`IStructuralChangeSink.ComponentAdded` receives the installed non-tag payload and real entity. `ComponentRemoved` runs before migration, with the old payload still readable. `EntityDestroyed` runs before removal. Tags added through records and query-scoped operations do not emit ComponentAdded, matching the original callback points. Payload addresses are borrowed for that synchronous callback only. Sink callbacks cannot reenter structure or staging operations.

Staging lifecycle contexts have `ComponentContext.World == null` and `IsStaged == true`; no fake World is created to supply a context. Apply transfers ownership to a context containing the actual World. Pending resources can be visited with `ScanResourceReferences` without serialization.

## Concurrency and deliberate corrections

Producers use a BCL read/write phase gate plus record/row-granularity synchronization. Different records can be written concurrently; appends to one record are serialized. Apply/Clear/Dispose reject active producers instead of blocking an executor worker. Their exclusive phase reads and marks row storage without the producer locks. Callers must complete their jobs first; Apply does not implicitly synchronize a scheduler.

Bad lifecycle callbacks or a failure after Apply starts mutating storage fault the affected staging/World objects. This is not rollback. Busy-owner and stale-target preflight failures leave the queued epoch available for a safe retry or Clear. Hooks remain nonthrowing ownership operations by contract.

The port makes the following explicit corrections rather than reproducing unsafe source details:

- Existing-entity lookup checks complete Entity64 generation; two generations of one index in an epoch are rejected.
- Update/Replace validates an explicit root mapping before recording removals.
- Transient meta mappings are sorted/deduplicated after remapping recycled target indices.
- Existing/default owning target payloads are ended before transferring staged ownership. Buffer transfer follows actual heap ownership even when its size was shrunk below inline capacity, avoiding the source's size/capacity mismatch leak.
- Pending queries are held alive by a query lease until Apply/Clear releases it.

## Source behavior checks

`tests/SugoiChecks/StagingChecks.cs` covers all 16 staging cases from `runtime/tests/ecs/ecs_staging_tests.cpp`:

| Source case | C# check area |
|---|---|
| non_contiguous_existing_entities_do_not_expand_batch | ExistingAndSinks |
| transient_spawn_preserves_component_array_and_tag_data | ConcurrentAndGrowth |
| concurrent_component_staging_initializes_rows_once | ConcurrentAndGrowth |
| transient_reference_in_existing_component_is_patched | SpawnsAndReferences |
| transient_reference_between_spawned_entities_is_patched | SpawnsAndReferences |
| null_entity_reference_is_kept_empty | SpawnsAndReferences |
| transient_reference_in_array_component_is_patched | SpawnsAndReferences |
| transient_reference_in_meta_is_patched | SpawnsAndReferences |
| copy_entity_components_remaps_batch_references | CopyUpdateReplace |
| update_entity_components_preserves_runtime_extras | CopyUpdateReplace |
| replace_entity_components_removes_only_unpreserved_types | CopyUpdateReplace |
| destroy_entities_with_meta_recurses_through_ownership | SpawnsAndReferences |
| grows_beyond_initialize_hints | ConcurrentAndGrowth |
| structural_sink_reports_spawned_component_with_real_entity | SpawnsAndReferences / Sink |
| structural_sink_reports_removed_component_payload | ExistingAndSinks / Sink |
| structural_sink_reports_destroy_before_storage_removal | ExistingAndSinks / Sink |

Additional checks cover coalescing, query phase order, copy versus move-only values, inline/heap ownership, producer/Apply rejection, stale targets, resource visitation, missing transient records, and block reuse. The standalone DefenseSimulation uses this StagingWorld and a business BirthTicket sink for birth publication.
