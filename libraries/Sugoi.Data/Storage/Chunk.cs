using System.Runtime.InteropServices;

namespace Sugoi.Data;

internal sealed unsafe class Chunk
{
    private static long _nextId;
    internal readonly long Id = Interlocked.Increment(ref _nextId);
    internal nint Memory;
    internal nint Payload => Memory + ArchetypeLayout.ChunkHeaderBytes;
    internal Group Group;
    internal readonly PoolKind PoolKind;
    internal PoolLayout Pool;
    internal int Count;
    internal int GroupIndex;
    private bool[]? _endedSingletons;
    internal int Capacity => Pool.Capacity;
    internal Span<Entity> Entities => new((void*)(Payload + Pool.EntityOffset), Count);
    internal Span<Entity> EntityCapacity => new((void*)(Payload + Pool.EntityOffset), Capacity);
    internal uint* Versions => (uint*)(Payload + Pool.VersionOffset);

    internal Chunk(Group group, PoolKind kind)
    {
        Group = group; PoolKind = kind; Pool = group.Archetype.Layout.For(kind);
        if (Pool.Capacity == 0) throw new InvalidOperationException("Archetype does not fit selected pool.");
        Memory = group.World.Runtime.ChunkPool.Rent(kind);
        NativeMemory.Clear((void*)Memory, (nuint)ArchetypeLayout.ChunkHeaderBytes);
        for (int i = 0; i < group.Archetype.Columns.Length; i++) Versions[i] = group.World.ChangeVersion;
        int constructed = group.Archetype.Layout.FirstChunkComponent;
        try
        {
            for (; constructed < group.Archetype.Columns.Length; constructed++)
                group.Archetype.Columns[constructed].Ops.Construct(new ComponentContext(group.World, Entity.Null), Address(constructed, 0));
        }
        catch
        {
            for (int i = group.Archetype.Layout.FirstChunkComponent; i < constructed; i++)
                group.Archetype.Columns[i].Ops.Destroy(new ComponentContext(group.World, Entity.Null), Address(i, 0));
            group.World.Runtime.ChunkPool.Return(kind, Memory); Memory = 0;
            throw;
        }
    }

    internal nint Address(int slot, int row)
    {
        var descriptor = Group.Archetype.Columns[slot];
        return Payload + Pool.Offset(slot) + (descriptor.Type.IsChunk ? 0 : checked(row * descriptor.Layout.Stride));
    }

    internal void MarkChanged(int slot) => Versions[slot] = Group.World.ChangeVersion;
    internal void PrepareSingletonTransfers() => _endedSingletons ??= new bool[Group.Archetype.Columns.Length];
    internal void EndSingletonLifetime(int slot)
    {
        if (_endedSingletons is null) throw new InvalidOperationException("Prepare singleton transfers before moving ownership.");
        _endedSingletons[slot] = true;
    }
    internal void Release()
    {
        if (Memory == 0) return;
        var columns = Group.Archetype.Columns;
        for (int slot = Group.Archetype.Layout.FirstChunkComponent; slot < columns.Length; slot++)
            if (_endedSingletons is null || !_endedSingletons[slot])
                columns[slot].Ops.Destroy(new ComponentContext(Group.World, Entity.Null), Address(slot, 0));
        Group.World.Runtime.ChunkPool.Return(PoolKind, Memory);
        Memory = 0;
    }
}

internal sealed class Archetype
{
    internal readonly ArchetypeLayout Layout;
    internal readonly ComponentDescriptor[] Columns;
    internal readonly EntityType PhysicalType;
    internal Archetype(EntityType physicalType, TypeRegistry types)
    {
        PhysicalType = physicalType;
        var descriptors = new ComponentDescriptor[physicalType.Components.Length];
        for (int i = 0; i < descriptors.Length; i++) descriptors[i] = types.Get(physicalType.Components[i]);
        Layout = ArchetypeLayout.Create(descriptors);
        Columns = Layout.Columns.ToArray();
    }
}
