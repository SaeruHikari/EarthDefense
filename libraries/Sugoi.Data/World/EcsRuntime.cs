namespace Sugoi.Data;

/// <summary>Owns the registry and the three shared native chunk pools.</summary>
public sealed class EcsRuntime : IDisposable
{
    private readonly HashSet<World> _worlds = [];
    private bool _closing;
    internal NativeChunkPool ChunkPool { get; } = new();
    public TypeRegistry Types { get; } = new();
    public bool IsDisposed { get; private set; }
    public ComponentType DisabledType { get; }
    public ComponentType DeadType { get; }
    public ComponentType EnabledMaskType { get; }
    public ComponentType DirtyMaskType { get; }
    public ComponentType LinkType { get; }

    public EcsRuntime()
    {
        DisabledType = Types.Register(new ComponentRegistration<DisabledTag> { Id = new Guid("b68b1cab-98ff-4298-a22e-68b404034b1b"), Name = "disabled", Kind = ComponentKind.Tag });
        DeadType = Types.Register(new ComponentRegistration<DeadTag> { Id = new Guid("c0471b12-5462-48bb-b8c4-9983036ecc6c"), Name = "dead", Kind = ComponentKind.Tag });
        EnabledMaskType = Types.Register(new ComponentRegistration<EnabledMask>
        {
            Id = new Guid("27a134c7-2252-4575-a531-f99c630ad728"), Name = "enabled-mask",
            Alignment = 16,
            Construct = static (in ComponentContext _, ref EnabledMask value) => value.Bits = uint.MaxValue
        });
        DirtyMaskType = Types.Register(new ComponentRegistration<DirtyMask>
        {
            Id = new Guid("a55d73d3-d41c-4683-89e1-8b211c115303"), Name = "dirty-mask",
            Alignment = 16,
            Construct = static (in ComponentContext _, ref DirtyMask value) => value.Bits = uint.MaxValue
        });
        LinkType = Types.RegisterBuffer(new ComponentRegistration<EntityLink>
        {
            Id = new Guid("54bd68d5-fd66-4dbe-85cf-70f535c27389"), Name = "entity-links",
            Remap = static (ref EntityLink link, EntityRemapper map) => link.Value = map(link.Value)
        }, 8);
    }

    public World CreateWorld(int maximumEntities = 16_777_216)
    {
        lock (_worlds)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (_closing) throw new InvalidOperationException("Runtime is closing its worlds.");
            var world = new World(this, maximumEntities);
            _worlds.Add(world);
            return world;
        }
    }

    internal void Released(World world) { lock (_worlds) _worlds.Remove(world); }
    public void Dispose()
    {
        lock (_worlds)
        {
            if (IsDisposed) return;
            if (_closing) throw new InvalidOperationException("Runtime disposal cannot reenter lifecycle hooks.");
            _closing = true;
            try
            {
                foreach (var world in _worlds.ToArray()) world.Dispose();
                ChunkPool.Dispose();
                IsDisposed = true;
            }
            finally { _closing = false; }
        }
    }
}

public struct DisabledTag { }
public struct DeadTag { }
public struct EnabledMask { public uint Bits; }
public struct DirtyMask { public uint Bits; }
public struct EntityLink { public Entity Value; }
