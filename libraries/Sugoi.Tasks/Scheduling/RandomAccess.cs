using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>A declared random read capability. Its lifetime must remain within the task owning its dependency.</summary>
public struct RandomReader<T> where T : unmanaged
{
    private readonly World _world;
    private ComponentAccessor<T> _accessor;
    public RandomReader(World world) { _world = world; _accessor = world.GetAccessor<T>(); }
    public ref readonly T Read(Entity entity) => ref _accessor.Read(entity);
    public bool Exists(Entity entity) => _world.Exists(entity);
}

/// <summary>Random writes serialize a task's own units because target overlap cannot be inferred from its query.</summary>
public struct RandomWriter<T> where T : unmanaged
{
    private readonly World _world;
    private ComponentAccessor<T> _accessor;
    public RandomWriter(World world) { _world = world; _accessor = world.GetAccessor<T>(); }
    public ref T Write(Entity entity) => ref _accessor.Write(entity);
    public bool Exists(Entity entity) => _world.Exists(entity);
}
