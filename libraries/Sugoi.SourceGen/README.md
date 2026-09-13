# Sugoi Source Generator

The generator is a build-time Roslyn analyzer. `Sugoi.Data` and `Sugoi.Tasks` never reference Roslyn or the generator assembly at runtime. Consumers reference it as an analyzer:

```xml
<ProjectReference Include="../Sugoi.SourceGen/Sugoi.SourceGen.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Components and explicit modules

```csharp
[Component("08b0584c-40bc-48fe-9bae-e2b04c1386ee")]
public partial struct Target
{
    public Entity Enemy;
}

[BufferComponent("a61c9a85-6220-4ca8-a807-4e8b82c0b573", 8)]
public partial struct Wingman
{
    public Entity Ally;
}

Target.Register(registry);
Wingman.Register(registry);
// Or register the entire consumer assembly, here named MyGame.Components:
Sugoi.Generated.MyGame_ComponentsModule.Register(registry);
```

`Register` registers this assembly only. `RegisterDependencies` statically calls referenced component assembly manifests, and `RegisterWithDependencies` performs both. There is no assembly scanning, runtime code generation, or module initialization side effect. Cross-module conflicts are diagnosed when visible at compile time and remain validated by the runtime registry. Type registration can be appended and compatible callback tables updated.

GUIDs, component flags, and alignment facts are supplied to `TypeRegistry`; no column offsets, buffer headers, strides, or capacities are calculated by generated code. `ComponentKind.Tag`, `Chunk`, and `Pinned` retain their distinct runtime semantics. For buffer elements, specify `BufferComponent` rather than the Buffer flag.

Nested writable Entity fields, private fields belonging directly to the partial component, and C# inline arrays are remapped through a generated strongly typed method. Buffer elements use the same callback through `BufferOps`. Auto-property backing fields, readonly Entity paths, and inaccessible nested fields require a custom `[ComponentRemap]` method. `GenerateEntityRemap=false` deliberately disables the automatic visitor; it does not make external-world Entity values safe to import. Merge still follows its source-local reference contract.

## Owned resources

Unmanaged representation does not mean trivial ownership. Attribute static component methods with `ComponentConstruct`, `ComponentCopy`, `ComponentMove`, `ComponentDestroy`, and `ComponentRemap`; their signatures must match the Data delegates exactly. The generated `Register` binds these methods directly, including private hooks.

```csharp
[ComponentOwnership]
[Component("2f1f489c-7a3a-45ce-8099-4df15ab2fbcf")]
public partial struct Resource
{
    public nint Handle;

    [ComponentMove]
    private static void Transfer(in ComponentContext sourceContext, ref Resource source,
        in ComponentContext destinationContext, ref Resource destination)
    {
        destination.Handle = source.Handle;
        source.Handle = 0;
    }
    // Supply Copy and Destroy as well. A destructor without these is diagnosed.
}
```

Construct/copy/move/destroy hooks must follow the runtime no-throw contract and must not perform structure changes. Managed objects are stored through an explicit owning handle representation with suitable hooks, never copied into native columns. Runtime resource traversal has its own typed callback and remains separate from serialization.

## Typed jobs

```csharp
[QueryJob]
public partial struct MoveJob
{
    public float Delta;
    private void Execute(Span<Position> positions, ReadOnlySpan<Velocity> velocities,
        ReadOnlySpan<Entity> identities, in JobContext context)
    {
        for (int i = 0; i < positions.Length; i++)
            positions[i].X += velocities[i].X * Delta;
    }
}
```

The generator implements `IQueryJob.Build` and `IQueryJob.Execute` in the partial job. `Span<T>` declares write access, `ReadOnlySpan<T>` declares read access, and `ReadOnlySpan<Entity>` binds the immutable identity column. `JobContext` is optional. Captured values remain ordinary job fields; borrowed spans remain synchronous parameters and cannot cross an await.

The following parameters generate both dependency declarations and automatic execution bindings:

| Execute parameter | Access declaration | Bound view |
|---|---|---|
| `OptionalRead<T>` / `OptionalWrite<T>` | Optional component read/write | `HasValue`, `Span`, indexed values |
| `SharedRead<T>` | Shared read with whole-task owner dependency | Read-only `Value` |
| `ChunkRead<T>` / `ChunkWrite<T>` | Chunk component read/write | Singleton `Value` |
| `BufferRead<T>` / `BufferWrite<T>` | Buffer element type read/write | Per-row buffer span or mutable buffer |
| `RandomReader<T>` / `RandomWriter<T>` / `RandomReadWrite<T>` | Corresponding random access | Cached World accessor |
| `MessageConsumer<T>` | Message job payload contract | The current owned payload range |
| `MessageSender<T>` | World-local typed sender | The task's bound message bus |

`[Read(typeof(T), Random = true)]`, `[Write(typeof(T), Random = true)]`, and `[Without(typeof(T))]` additionally describe accesses hidden inside helper methods. Different parameters may alias a component; the builder combines their access modes. Required and excluded declarations for the same type are diagnosed. Existing exact manual Build/Execute implementations remain supported for custom algorithms, but the bindings in this table require no manual binding code.

Non-owning struct bodies are copied by value. Non-owning class bodies receive an `IJobCloneable<TSelf>` shallow-clone implementation unless an explicit clone policy exists; captured reference fields remain shared. A public `Prepare(int entityCount)` participates before materialization.

Owning bodies use `[JobOwnership]`, `[JobClone]` on an instance method returning a new independently owned body, and `[JobDispose]` on an instance void cleanup method. These generate `IJobCloneable<TSelf>` and `IDisposable`. The scheduler disposes the submission snapshot and every batch copy, including failures. Existing typed clone interfaces and IDisposable implementations are also accepted. An owning job without explicit cloning is SG008, including disposable structs; component ownership hooks are not substitutes for task-body ownership.

## Async ECS jobs

```csharp
[QueryJob, Write(typeof(Position))]
public partial struct DelayedMove
{
    public JobCounter Gate;
    private async ValueTask ExecuteAsync(AsyncJobContext context)
    {
        await Gate.WaitAsync();
        Apply(context);
    }
    private static void Apply(AsyncJobContext context)
    {
        var positions = context.Borrow().WriteOwned<Position>();
        // Work with the span entirely inside this synchronous helper.
    }
}
```

This generates `IAsyncQueryJob`; the scheduler retains dependencies and storage ownership until the returned Task/ValueTask completes. `Index` is the entity offset and `TaskIndex` is the independent batch invocation number. Async methods may bind a stable context, random accessors, and message senders; borrowed span/optional/shared/chunk/buffer/consumer parameters are diagnosed. Reacquire those views from the context inside a synchronous scope between awaits.

An async struct copies `this` into its state machine. **Owning async jobs must therefore use a class with explicit clone and cleanup**; otherwise post-await ownership changes would not reliably reach scheduler cleanup. Both the generator and runtime enforce this. Non-owning async structs remain supported.

## Messages

```csharp
[Message("c3f3ad3a-9e87-47e3-af9b-2375f3f33ec5")]
public partial struct Damage { public float Amount; }

[MessageJob(typeof(Damage))]
public partial struct ApplyDamage
{
    private void Execute(MessageConsumer<Damage> messages, Span<Health> health,
        MessageSender<Acknowledgment> replies, in MessageJobContext<Damage> context)
    {
        for (int i = 0; i < messages.Count; i++) health[i].Value -= messages[i].Amount;
    }
}
```

Each message generates `RegisterMessage(MessageRegistry)` and an assembly `Sugoi.Generated.<AssemblyName>Messages.Register` manifest, with statically composed dependency manifests and GUID conflict diagnostics. Message registration has Copy/Move/Destroy hooks with exact strongly typed signatures; payload construction belongs to the sender. Set `IsCopyable=false` for move-only messages. Native owners require explicit lifetime operations, and generated callbacks remain statically reachable in NativeAOT.

Message jobs generate `IMessageJob<T>` or `IAsyncMessageJob<T>`. An async message method receives `MessageTaskBatch<T>` and calls `Bind()` only in a synchronous scope; it does not keep a payload span across await. The bound message bus is available through `MessagesBus`. ScheduledWorld injects its bus automatically; direct scheduler callers configure a bus through TaskOptions or the scheduler's world binding. Message GUID registration must precede typed sends/subscriptions.

## Runtime resource references

`ResourceReference(Guid)` fields, nested values, and inline arrays generate a read-only resource visitor. `[ResourceField]` marks an ordinary Guid field/property as a resource identity; unrelated GUID fields are not guessed. Use `[ComponentResourceScan]` on a static `void(in T value, ResourceVisitor visitor)` method for custom native containers. Empty resource identities are skipped, matching the original ResourceHandle scanner.

The callback is registered in `ComponentRegistration<T>.ScanResources`. `World.ScanResourceReferences` uses the current component operation table, visits native buffer elements and each chunk singleton once, includes disabled entities, and excludes dead entities. Finish conflicting writers before scanning. This API neither loads resources nor serializes them.

## Diagnostics and incremental work

| Code | Meaning |
|---|---|
| SG001 | Invalid partial component declaration, conflicting generated member, or inaccessible module target |
| SG002 | Invalid, empty, or duplicate stable GUID, including visible dependency modules |
| SG003 | Managed native payload or invalid kind/alignment/buffer representation |
| SG004 | Incorrect or incomplete lifetime ownership hooks |
| SG005 | Entity path requires explicit remapping |
| SG006 | Unsupported, ambiguous, or unsafe job binding |
| SG007 | Invalid message GUID, native representation, lifetime hook, or visible message identity conflict |
| SG008 | Job resource ownership lacks safe clone/cleanup, or an owning async job uses a struct |
| SG009 | Resource path/hook cannot be generated safely |

Component types are non-generic partial structs; nested non-generic partial containers are supported. Generic job types may be used with unmanaged constraints. The runtime registration API remains available for manual closed generic component adapters.

Roslyn semantic symbols are converted to equatable immutable values before output; they are not retained in caches. Attribute entry points, individual component/job outputs, and a separate dependency/manifest pipeline avoid whole-project rescanning. Generated source names and registration order are stable. `tests/SugoiGeneratorChecks` exercises semantic compilation, diagnostics, cross-assembly manifests, and unchanged-input caching. `tests/SugoiFixtures` is an independent consumer assembly used by runtime/AOT checks.
