using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>A caller-owned creation body. Build and Execute run synchronously on the same logical ref instance.</summary>
public interface ICreationJob
{
    void Build(CreationBuilder builder);
    void Execute(in JobContext context);
}

/// <summary>Owns the final creation signature and binding declarations; this does not submit scheduler hazards.</summary>
public sealed class CreationBuilder
{
    private readonly HashSet<ComponentType> _components = [];
    private readonly HashSet<Entity> _meta = [];
    internal CreationBuilder(World world) => World = world;
    public World World { get; }
    public CreationBuilder Add<T>() where T : unmanaged { _components.Add(World.Types.Get<T>()); return this; }
    public CreationBuilder Add(ComponentType type) { _ = World.Types.Get(type); _components.Add(type); return this; }
    public CreationBuilder AddBuffer<T>() where T : unmanaged { _components.Add(World.Types.GetBuffer<T>()); return this; }
    public CreationBuilder WithMeta(Entity entity) { if (entity.IsNull) throw new ArgumentException("A creation meta entity cannot be null."); _meta.Add(entity); return this; }
    public CreationBuilder WithMeta(ReadOnlySpan<Entity> entities) { foreach (var entity in entities) WithMeta(entity); return this; }
    public CreationBuilder WithMeta(IEnumerable<Entity> entities) { foreach (var entity in entities) WithMeta(entity); return this; }
    public CreationBuilder WithMeta(Entity[] entities) => WithMeta(entities.AsSpan());
    public CreationBuilder Has<T>() where T : unmanaged => Add<T>();
    public CreationBuilder Without<T>() where T : unmanaged { _components.Remove(World.Types.Get<T>()); return this; }
    public CreationBuilder Read<T>() where T : unmanaged => Add<T>();
    public CreationBuilder Write<T>() where T : unmanaged => Add<T>();
    public CreationBuilder ReadBuffer<T>() where T : unmanaged => AddBuffer<T>();
    public CreationBuilder WriteBuffer<T>() where T : unmanaged => AddBuffer<T>();
    public CreationBuilder ReadChunk<T>() where T : unmanaged => AddChunk<T>();
    public CreationBuilder WriteChunk<T>() where T : unmanaged => AddChunk<T>();
    private CreationBuilder AddChunk<T>() where T : unmanaged
    {
        var type = World.Types.Get<T>();
        if (!type.IsChunk) throw new ArgumentException("A chunk binding requires a chunk component.");
        return Add(type);
    }
    public CreationBuilder OptionalRead<T>() where T : unmanaged { _ = World.Types.Get<T>(); return this; }
    public CreationBuilder OptionalWrite<T>() where T : unmanaged { _ = World.Types.Get<T>(); return this; }
    public CreationBuilder ReadShared<T>() where T : unmanaged { _ = World.Types.Get<T>(); return this; }
    public CreationBuilder RandomRead<T>() where T : unmanaged { _ = World.Types.Get<T>(); return this; }
    public CreationBuilder RandomWrite<T>() where T : unmanaged { _ = World.Types.Get<T>(); return this; }
    public CreationBuilder RandomReadWrite<T>() where T : unmanaged { _ = World.Types.Get<T>(); return this; }
    internal EntityType Complete() => new(_components.ToArray(), _meta.ToArray());
}

internal struct CreationInitializer<TJob>(TJob job, MessageBus messages) : IEntityInitializer where TJob : ICreationJob
{
    internal TJob Job = job;
    public void Initialize(in ChunkView view, int entityStartIndex)
    {
        // The reference creation API always uses task_index 0; the caller's mutable body is reused across ranges.
        var context = new JobContext(in view, entityStartIndex, 0, messages);
        Job.Execute(in context);
    }
}
