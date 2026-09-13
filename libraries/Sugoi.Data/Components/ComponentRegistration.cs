namespace Sugoi.Data;

/// <summary>Identity and world owning the value whose lifetime is being operated on.</summary>
public readonly record struct ComponentContext(World? World, Entity Entity)
{
    public bool IsStaged => World is null;
}

public delegate Entity EntityRemapper(Entity entity);
public delegate void ComponentConstruct<T>(in ComponentContext context, ref T value) where T : unmanaged;
public delegate void ComponentCopy<T>(in ComponentContext sourceContext, in T source, in ComponentContext destinationContext, ref T destination) where T : unmanaged;
public delegate void ComponentMove<T>(in ComponentContext sourceContext, ref T source, in ComponentContext destinationContext, ref T destination) where T : unmanaged;
public delegate void ComponentDestroy<T>(in ComponentContext context, ref T value) where T : unmanaged;
public delegate void ComponentRemap<T>(ref T value, EntityRemapper remapper) where T : unmanaged;

/// <summary>Native representation facts and typed hooks. Hooks must not throw or perform structure changes.</summary>
public sealed class ComponentRegistration<T> where T : unmanaged
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public ComponentKind Kind { get; init; }
    public int Alignment { get; init; }
    public ComponentConstruct<T>? Construct { get; init; }
    public ComponentCopy<T>? Copy { get; init; }
    public ComponentMove<T>? Move { get; init; }
    public ComponentDestroy<T>? Destroy { get; init; }
    public ComponentRemap<T>? Remap { get; init; }
    public ComponentResourceScan<T>? ScanResources { get; init; }
}

public sealed class ComponentDescriptor
{
    public ComponentType Type { get; }
    public Guid Id { get; }
    public Guid Guid => Id;
    public string Name { get; internal set; }
    public ComponentLayout Layout { get; }
    public ComponentOps Ops { get; internal set; }
    public int Revision { get; internal set; }

    internal ComponentDescriptor(ComponentType type, Guid id, string name, ComponentLayout layout, ComponentOps ops)
    {
        Type = type; Id = id; Name = name; Layout = layout; Ops = ops; Revision = 1;
    }
}
