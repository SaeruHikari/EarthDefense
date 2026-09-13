# Messages: source-equivalent ownership and dispatch

Messages use an explicitly registered native representation, a ticket-published ring and segmented overflow. The runtime is `Sugoi.Tasks` plus `Sugoi.Data` and the BCL. There is no serialization, runtime assembly scan, generated-at-runtime code, component lock or custom atomic reader/writer lock.

## Registration and identity

`MessageRegistry.Register<T>(MessageRegistration<T>)` assigns a stable GUID to an unmanaged CLR representation. `MessageRegistration<T>` contains `Id`, `Name`, `Alignment`, `IsCopyable`, and optional typed Copy/Move/Destroy hooks. No GUID is silently derived from a CLR name. Repeated compatible registration of the same GUID/type is idempotent; a conflicting identity or physical representation is rejected.

`[Message("guid")]` plus the Source Generator produces the static `T.RegisterMessage(registry)` entry and an assembly message manifest. The generated hooks call typed application methods directly. The registry stores a statically instantiated operation object for typed and raw-GUID dispatch, so native AOT does not need reflection-based generic construction.

`MessageLayout` is the common source of payload size/stride, base alignment and checked block byte counts. Ring, overflow, linearization and owned task batches consume this layout. Entity targets are a separate contiguous 8-byte Entity64 column. A CLR value's stride remains its actual unmanaged size; allocation may round the backing byte count to its alignment.

## Queue and publication rules

`MessageBus.Subscribe<T>(query, capacity: 8192)` and `Subscribe(guid, query, capacity)` resolve the **same queue for the same GUID and Query object**. Repeating registration does not create another recipient. Different Query objects remain different subscriptions. The initial registration determines ring capacity.

Each producer reserves an increasing ring ticket with compare/exchange, constructs the payload in its native slot, then publishes the ticket with release ordering. Consumers see only the contiguous published prefix from the current head. If an earlier producer is still constructing its payload, a later ring ticket is not made visible merely because its own construction finished.

When all ring slots are reserved, the queue appends to native overflow blocks under a short BCL metadata lock. Each overflow block has the ring's capacity. **A full ring does not drop a message.** `DroppedCount` reports failed overflow allocation or a violated lifecycle construction contract; `OverflowMessageCount` reports current overflow occupancy, and `OverflowBlockCount` records allocated overflow blocks. `PublishedCount` is the published ring prefix; `Count`/`PendingCount` include overflow. `HasPending` supports both a particular Query and any subscriber of a typed/GUID message.

Consumption snapshots the published ring prefix and detaches the current overflow blocks. It presents ring entries first, then detached overflow blocks, as in the reference. This is not a claim of global wall-clock ordering across producers or between partially unpublished ring tickets and overflow.

## Copy, move and fan-out

`Send(target, in value)` copies. `SendMove(target, ref value)` invokes the registered move hook and can transfer native ownership from the caller. Typed inputs are pinned for the complete dispatch, including callbacks and fan-out, so a managed array/field passed by reference cannot move underneath a raw address during GC.

- One matching receiver constructs directly from the caller using the requested copy/move operation.
- Multiple receivers of a copyable typed message first construct one stable value, copy it to every matching queue, then destroy the stable value.
- A move-only typed message goes to only the first matching queue when several queries match. `MoveOnlyFanoutCount` exposes that event; it is not silently shallow-copied to all receivers.
- `SendRaw(target, guid, pointer)` follows the raw reference path: copy directly to each matching receiver. An unknown GUID or null pointer fails; a registered type with no subscribers succeeds without consuming the caller's value.

Hooks are construction operations, not assignment into a live value. They must fully initialize the destination, leave moved-from values destructible, and must not throw. Native-owning move-only messages require a move hook. Queue/task cleanup calls destructors on moved-from objects too; a correct destructor tolerates an emptied owner. Constructor/destructor callback counts therefore differ from the count of actual resource frees.

## Borrowed consumption and owned task payload

`subscription.Visit(visitor)` exposes a writable payload and Entity target inside a synchronous callback. An unwrapped, ring-only batch borrows the native ring directly. Wrapping or overflow is linearized with move-preferred/copy-fallback operations; source values are subsequently destroyed. Queue disposal destroys queued values directly and does not perform unnecessary move/copy linearization.

`subscription.Consume()` returns an owned `MessageBatch<T>` with `Targets` and writable `Values`/`Messages` spans. A scheduled consumer similarly moves/copies the materialized payload into its own native owner before releasing queue slots. Its owner remains live until every batch, including awaited asynchronous execution, finishes. Writable payload access permits explicit ownership transfer out of a consumed message before its final destructor runs.

Direct host `Consume`/typed `Visit` validate targets against the full Query by default, and accept an explicit `false` to skip that filter. Task validation is a separate option described below. Borrowed spans must not outlive their visit/bind scope; `MessageTaskBatch<T>.Bind()` creates fresh borrowed views after an await. Raw pointer dispatch and raw views require the caller to obey the registered representation and pointer lifetime.

## Task integration

Both explicit subscription dispatch and lazy dispatch are supported:

```csharp
Damage.RegisterMessage(bus.Registry);
using var query = scheduler.CreateMessageQuery<Damage, ApplyDamage>(bus, in job);
using var subscription = bus.Subscribe<Damage>(query);
bus.Send(target, in damage);
await scheduler.DispatchMessages<Damage, ApplyDamage>(subscription, in job);
```

For the lazy overload `DispatchMessages(bus, in job, reused, options)`, Build/Query/Prepare run synchronously on the submitting thread. **Prepare receives the Query's entity count**, not the current message count. Explicit gates are then awaited. At admission, an absent `(GUID, Query)` queue is registered and that first task executes no message work. `JobHandle.Query` exposes a created Query so subsequent calls can reuse/release it. The scheduler does not subscribe before a gate to make the API look easier.

Message consumption/materialization occurs at admission, before component hazard waits. To receive messages produced by an earlier task in the same phase, gate admission on that producer or await it before Dispatch. A read/write dependency alone does not move message materialization later.

`TaskOptions.ValidateMessages` defaults to **false**, matching the original task signature. Existing targets may therefore be consumed after their group no longer matches the subscription. When enabled, generation also tests the target's current Group. Dead/invalid Entity64 targets never produce a work range. Payload offsets always refer to the original materialized array; skipping an invalid/nonmatching prefix cannot shift a later target onto another message.

`IMessageJob<T>` binds synchronous work through `MessageJobContext<T>`. `IAsyncMessageJob<T>` receives the stable `MessageTaskBatch<T>` descriptor. `MessagesBus`, `MessageConsumer<T>`, `MessageSender<T>`, component views and `TaskIndex` are available to the generated bindings. `Index` is the payload-range offset; `TaskIndex` is the submission's per-execution sequence number, so they are deliberately different.

Task submission snapshots and individual batch copies each own their resources. `IJobCloneable<T>` defines copying; each applicable `IDisposable` body is disposed exactly once. Owning asynchronous jobs use a class, because a C# async struct copies `this` into a state machine and cannot safely publish its final ownership fields back to a separately disposed struct. The generator and runtime reject disposable async structs. Optional `JobDiagnostics` activities span each actual synchronous/asynchronous batch, using the same `TaskIndex` supplied to its context.

## Source correspondence and deliberate corrections

Reference root: `D:/Code/ExtremeEngine/engine/modules/engine/runtime/`.

| Reference behavior | Source | C# owner |
|---|---|---|
| Ring reservation and contiguous ticket publication | `src/ecs/messages.cpp:177–225`, `:250–290` | `MessageSubscription.TryReserve`, `PublishedWindow`, `SendNative` |
| Segmented overflow and counters | `src/ecs/messages.cpp:11–59`, `:228–247` | `MessageSubscription.OverflowBlock` and overflow metadata |
| Ring fast view / wrap+overflow linearization | `src/ecs/messages.cpp:293–377` | `VisitUnderUsage`, `ConsumeCore`, `MessageBatch` |
| Idempotent registration and pending lookup | `src/ecs/messages.cpp:461–526` | `MessageBus.Subscribe`, `GetSubscription`, `HasPending` |
| Typed move/fan-out and move-only fallback | `include/SkrRuntime/ecs/message.hpp:148–225` | `SendTyped`, `SendMove`, descriptor operation table |
| Raw GUID send | `src/ecs/messages.cpp:529–563` | `MessageRegistry`, `MessageBus.SendRaw` |
| Prepare count and sender/consumer binding | `include/SkrRuntime/ecs/world.hpp:512–515`, `:555–569` | Scheduler preparation, generated bindings and message contexts |
| Lazy admission and validation default | `src/ecs/scheduler.cpp:232–314`; `include/SkrRuntime/ecs/scheduler.hpp:155` | `MessageSubmission.Materialize`, `TaskOptions.ValidateMessages` |
| Async wait and payload/body cleanup | `src/ecs/scheduler.cpp:560–567`, `:607–649` | awaited submission batches, message owner and job lifecycle |

The Entity column deliberately uses Entity64. BCL synchronization replaces original custom atomic mutex types; no component/slice lock is restored. Invalid-prefix payload mapping is explicitly corrected: the original generator increments its offset only for accepted views while keeping the unfiltered payload array, which can pair a later target with an earlier rejected message. The C# generator preserves each range's original source index. `MessagesParityChecks` names and verifies this correction rather than treating the defective offset as a golden result.

No coalescing API is claimed merely because the original header comments mention it; the audited active implementation did not expose that documented callback overload.

## Validation

`MessagesParityChecks.RunAsync()` checks ring overflow without drops, wrapping, delayed constructor publication, GUID/typed idempotency, pending lookup, raw dispatch, move-only fan-out, stable typed fan-out, actual native resource transfer/free counts, validation defaults, invalid-prefix offsets, query-count Prepare, admission-time lazy subscription and awaited task-body cleanup. `ThreadRaceChecks` exercises simultaneous producers and consumers. Generated message declarations/binders are checked by the source-generator fixtures and NativeAOT consumer.

These are correctness/ownership checks; they do not claim zero GC, universal throughput, or original-implementation equivalence for untested exception misuse outside the no-throw lifecycle contract.
