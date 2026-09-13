# Sugoi.Tasks

This .NET 8 / NativeAOT-compatible runtime library depends only on Sugoi.Data and the BCL. It ports the original ECS online scheduler and message mechanisms. StagingWorld lives in Data; there is no CommandBuffer or serialization layer.

## Dispatch and dependencies

Dispatch immediately performs Build/Prepare on the submitting thread, then waits explicit admission gates, analyzes dependencies online, and runs ready work. There is no prebuilt frame graph or later launch switch. FlushDispatchAsync fences queued admissions; SyncAllAsync waits registered running/gated work. SyncWorldAsync includes pending preparation and synchronous Creation operations for its World.

Sequential RAW/WAR/WAW hazards use physical chunk fences. Any pair involving Random uses a whole-task fence. Writer/readers history is retained for each chunk so disjoint writers cannot hide an earlier dependency. Random writers serialize their own units; chunk-component writers serialize batches within a chunk. These two safety rules prevent source race cases without component locks.

A query-based dispatch updates meta conditions from the current Build even when a Query is reused. Query metadata is published immutably so concurrent matching never enumerates a changing list. Explicit-entity dispatch preserves input order and duplicates, skips invalid identities, and follows the original path without applying the job Query filter. Its overload does not call Prepare. Query-based Prepare receives entity count; message Prepare also receives Query entity count.

## Async execution and lifetime

IQueryJob / IMessageJob<T> implement synchronous bodies. IAsyncQueryJob / IAsyncMessageJob<T> can await inside the ECS execution body. Borrow component/payload spans only in synchronous scopes using AsyncJobContext.Borrow() or MessageTaskBatch<T>.Bind().

The scheduler awaits the actual body before releasing dependencies, payloads, Query and World uses. A suspended body yields its worker to other jobs; its continuation returns to the same worker unless the application explicitly opts out with ConfigureAwait(false).

The executor holds strong roots for suspended operations, just as the source scheduler owns suspended fibers. Merely counting pending work is insufficient: an otherwise unrooted awaiter/event cycle can be collected by the GC. Async For admits every logical body, so sixteen batches can all reach a barrier on two workers. The OS thread budget remains fixed. Synchronous For uses worker partitions for CPU kernels; partial host admission always drains accepted children before returning an error.

Do not synchronously block a worker on unfinished work. Do not await a job whose dependency requires the current job to finish: this is still a dependency cycle. Disposal is coordinated from the host and drains accepted work.

## Job body ownership

IJobCloneable<TSelf>.CloneForBatch() creates an independent submission or batch body. IDisposable ends that copy's lifetime. The original caller-owned object is neither consumed nor disposed by Dispatch.

The scheduler cleans every batch in finally, then the submission snapshot during final cleanup, including failures. Trivial structs use value copying. Reference bodies use typed cloning or the existing query/message clone contract. Source Generator can emit private JobClone / JobDispose hooks and diagnoses owning bodies without a valid copy policy.

An owning async body must be a class with explicit clone/cleanup. A C# async struct method copies this into its state machine, so mutable native ownership fields cannot reliably be observed by cleanup on the original value. The generator and runtime reject that unsafe combination. Immutable-parameter async structs remain supported.

Creation uses the same caller initializer by ref, without cloning or disposing it. Scheduler.CreateEntities and CreateReservedEntities build the final signature and invoke it after each new native range is constructed, before allocating the next range. Callbacks run outside scheduler metadata locks; shutdown and World synchronization still track the operation. The Data World enforces an exclusive structure phase during creation.

## Events, counters and completion

TaskOptions.AfterEvents / AfterCounters / OnFinishEvents / OnFinishCounters contain WeakJobEvent / WeakJobCounter values. Collection expressions such as AfterEvents = [gate] implicitly convert strong handles. Snapshotting copies weak handles, not strong target ownership. Expired targets are skipped, matching the original weak-option protocol.

Weak handles follow .NET GC lifetime rather than C++ scope destruction. Keep a strong producer handle while intending to signal it; dropping a reference is not a cancellation API. A waiter keeps the state it has successfully acquired alive until completion or cancellation, and the executor retains the suspended operation itself.

JobHandle can be awaited by multiple callers. Computation is the component dependency fence; normal awaiting includes cleanup and finish notifications. Cleanup releases payload/Query/World ownership before publishing external finish signals. On-finish counters must be incremented by the caller before submission. Failures/cancellation release all fences, clean owned state and remain observable through handles and SyncAllAsync.

## Messages

MessageRegistry stores stable GUID descriptors and static Copy/Move/Destroy callbacks. Register with MessageRegistration<T>, or use generated Message.Register and assembly MessageModule methods. The bus accepts typed and GUID/raw sends; no RTTR reflection or runtime code generation is required.

Each (message GUID, Query) owns one native MPSC ticket ring, default capacity 8192. Re-registering the same pair returns the original queue. A full ring uses native overflow blocks; only allocation failure drops a valid incoming message. Count/HasPending include published ring entries and overflow data. Ring visibility follows the contiguous published ticket prefix, so a slow earlier producer cannot be overtaken in that ring.

Send<T>(in value) copies. SendMove<T>(ref value) transfers into a single receiver. For multiple matching receivers, a copyable payload is stabilized then copied per receiver; a move-only payload is delivered only to the first matched receiver and increments MoveOnlyFanoutCount. SendRaw copies directly per receiver and reports failure for an incompatible move-only raw copy. No subscriber means no ownership is taken.

An unwrapped ring-only Visit can expose native storage directly. Wrapped or overflow consumption linearizes through Move/Copy. Owned MessageBatch retains its payload until Dispose; its Values span is writable, allowing consumers to move out resources and clear their source value. Invalid/skipped work retains the original payload offset and is still cleaned exactly once.

MessageSender<T> is automatically bound in Query and Message jobs. It is a borrowed job capability: use it inside that job's lifetime. Host code uses MessageBus.Send/SendMove/SendRaw, which acquire a World use scope.

## Message dispatch timing

DispatchMessages(subscription, job) consumes the existing queue at admission, before component dependency waits. DispatchMessages<TMessage,TJob>(bus, job, reusedQuery) implements the source lazy path: if no queue exists at admission it registers the queue and runs no message work that time. JobHandle.Query returns the retained definition for reuse/release.

A producer-to-consumer component hazard does not defer message materialization. Use an explicit After gate to consume messages produced by that job. ValidateMessages defaults to false; true rechecks target group membership at materialization. Invalid Entity64 identities are never used to address storage. Host Consume defaults to full validation.

ScheduledWorld combines World, bus, subscriptions and job shutdown. ReleaseQueryAsync drains its jobs, removes subscriptions, then disposes the Query. Staging producers must be awaited before StagingWorld.Apply; Apply also rejects target Worlds with active or suspended jobs.

## Binding, diagnostics and prefetch

The generator supports owned Span, Optional, Shared, Chunk, Buffer, Random, Entity, MessageConsumer/MessageSender, async contexts and synchronous Creation. See the generator README for exact parameter contracts.

Index is the source entity/message starting offset; TaskIndex is a separate execution ordinal. Optional BCL ActivitySource tracing uses the name Sugoi.Tasks and records task name, chunk, count and both indices. TaskOptions.DebugName overrides a generated QueryJob/MessageJob Name.

Prefetch is planned once at admission, preserving the source conditions: default Off; Auto requires at least 128 entities, no more than four sequential streams, no explicit-entity path and no configured batch smaller than 128. Per-access modes merge into a task mode. A task override can include otherwise-Off sequential streams. Force hints two lines and the identity column; Auto hints one. Missing/tag/random columns are skipped and unsupported architectures do not emit an unavailable instruction.

## Evidence

The unified runner executes TasksChecks, ThreadRaceChecks, JobParityChecks, MessagesParityChecks, WeakSignalChecks and GeneratedAdvancedChecks. They cover online hazards, body-level async waits, more suspended batches than workers, body resource cleanup, Creation callback joins, reused queries, message ordering/overflow/fanout, weak-state collection and shutdown races. Both runtime libraries are exercised by the cross-assembly NativeAOT consumer.
