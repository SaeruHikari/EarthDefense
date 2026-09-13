using System.Runtime.CompilerServices;

namespace Sugoi.Data;

/// <summary>A stored range descriptor, not a memory lease. Obtain View only while the world is stable.</summary>
public readonly struct QueryRange
{
    internal readonly Chunk Chunk;
    private readonly ulong _structureVersion;
    public World World { get; }
    public long ChunkId => Chunk.Id;
    public long GroupId => Chunk.Group.Id;
    public int Start { get; }
    public int Count { get; }
    public ChunkView View { get { Validate(); return new(this); } }

    internal QueryRange(World world, Chunk chunk, int start, int count)
    {
        World = world; Chunk = chunk; Start = start; Count = count; _structureVersion = world.StructureVersion;
    }

    internal void Validate()
    {
        if (World is null || Chunk is null) throw new InvalidOperationException("Uninitialized view.");
        World.EnsureAlive();
        if (Chunk.Memory == 0 || Chunk.Group.World != World || _structureVersion != World.StructureVersion || Start < 0 || Count < 0 || Start + Count > Chunk.Count)
            throw new InvalidOperationException("A borrowed view outlived a structural change.");
    }
    public QueryRange Slice(int start, int count)
    {
        Validate();
        if (start < 0 || count < 0 || start > Count - count) throw new ArgumentOutOfRangeException(nameof(start));
        return new(World, Chunk, Start + start, count);
    }
}

/// <summary>Synchronous, zero-copy access. Access declarations and world ownership provide exclusion; no slice locks.</summary>
public readonly unsafe ref struct ChunkView
{
    private readonly QueryRange _range;
    internal ChunkView(QueryRange range) => _range = range;
    public World World => _range.World;
    public int Count => _range.Count;
    public long ChunkId => _range.ChunkId;
    public long GroupId => _range.GroupId;
    public ReadOnlySpan<Entity> Entities { get { _range.Validate(); return _range.Chunk.Entities.Slice(_range.Start, Count); } }
    public EntityType EntityType => _range.Chunk.Group.Type;
    public bool HasOwned(ComponentType type) { _range.Validate(); return _range.Chunk.Group.Owns(type); }
    public bool HasOwned<T>() where T : unmanaged => HasOwned(World.Types.Get<T>());
    public bool Has(ComponentType type) { _range.Validate(); return _range.Chunk.Group.Has(type); }

    private int RequireColumn(ComponentType type, bool buffer, bool chunk)
    {
        _range.Validate();
        int slot = _range.Chunk.Group.Archetype.Layout.IndexOf(type);
        if (slot < 0 || type.IsBuffer != buffer || type.IsChunk != chunk || type.IsTag)
            throw new InvalidOperationException($"Expected an owned {(buffer ? "buffer" : chunk ? "chunk singleton" : "per-entity")} column of {type}.");
        return slot;
    }

    public ReadOnlySpan<T> ReadOwned<T>() where T : unmanaged
    {
        int slot = RequireColumn(World.Types.Get<T>(), false, false);
        return new((void*)_range.Chunk.Address(slot, _range.Start), Count);
    }
    public Span<T> WriteOwned<T>() where T : unmanaged
    {
        int slot = RequireColumn(World.Types.Get<T>(), false, false);
        _range.Chunk.MarkChanged(slot);
        return new((void*)_range.Chunk.Address(slot, _range.Start), Count);
    }
    public bool TryReadOwned<T>(out ReadOnlySpan<T> values) where T : unmanaged
    {
        if (!World.Types.TryGet<T>(out var type) || !HasOwned(type)) { values = default; return false; }
        values = ReadOwned<T>(); return true;
    }
    public bool TryWriteOwned<T>(out Span<T> values) where T : unmanaged
    {
        if (!World.Types.TryGet<T>(out var type) || !HasOwned(type)) { values = default; return false; }
        values = WriteOwned<T>(); return true;
    }
    public ReadOnlySpan<byte> ReadOwnedBytes(ComponentType type)
    {
        _range.Validate();
        int slot = _range.Chunk.Group.Archetype.Layout.IndexOf(type);
        if (slot < 0) return default;
        var layout = _range.Chunk.Group.Archetype.Columns[slot].Layout;
        return new((void*)_range.Chunk.Address(slot, _range.Start), checked(layout.Size * (type.IsChunk ? 1 : Count)));
    }
    public ref readonly T ReadChunk<T>() where T : unmanaged
    {
        int slot = RequireColumn(World.Types.Get<T>(), false, true);
        return ref Unsafe.AsRef<T>((void*)_range.Chunk.Address(slot, 0));
    }
    public ref T WriteChunk<T>() where T : unmanaged
    {
        int slot = RequireColumn(World.Types.Get<T>(), false, true);
        _range.Chunk.MarkChanged(slot);
        return ref Unsafe.AsRef<T>((void*)_range.Chunk.Address(slot, 0));
    }
    public ref readonly T ReadShared<T>() where T : unmanaged
    {
        _range.Validate();
        var type = World.Types.Get<T>();
        var owner = _range.Chunk.Group.SharedOwner(type);
        if (owner.IsNull) throw new InvalidOperationException("Shared component does not exist.");
        ref var entry = ref World.Registry.ValidEntry(owner);
        int slot = entry.Chunk!.Group.Archetype.Layout.IndexOf(type);
        if (type.IsTag || type.IsBuffer || slot < 0) throw new InvalidOperationException("Shared component is not a scalar value.");
        return ref Unsafe.AsRef<T>((void*)entry.Chunk.Address(slot, entry.Row));
    }
    public ReadOnlySpan<T> ReadBuffer<T>(int row) where T : unmanaged
    {
        if ((uint)row >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(row));
        var type = World.Types.GetBuffer<T>();
        int slot = RequireColumn(type, true, type.IsChunk);
        var ops = _range.Chunk.Group.Archetype.Columns[slot].Ops.Buffer!;
        nint address = _range.Chunk.Address(slot, _range.Start + row);
        return new((void*)ops.Data(address), ops.Count(address));
    }
    public BufferColumn<T> WriteBuffer<T>(int row) where T : unmanaged
    {
        if ((uint)row >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(row));
        var type = World.Types.GetBuffer<T>();
        int slot = RequireColumn(type, true, type.IsChunk);
        var ops = _range.Chunk.Group.Archetype.Columns[slot].Ops.Buffer!;
        _range.Chunk.MarkChanged(slot);
        return new(_range.Chunk.Address(slot, _range.Start + row), ops, new ComponentContext(World, Entities[row]));
    }
}

/// <summary>Batch-local random accessor with a cached physical column for consecutive entities in one chunk.</summary>
public unsafe struct ComponentAccessor<T> where T : unmanaged
{
    private readonly World _world;
    private readonly ComponentType _type;
    private Chunk? _cachedChunk;
    private ulong _version;
    private nint _base;
    private int _slot;
    internal ComponentAccessor(World world) { _world = world; _type = world.Types.Get<T>(); _cachedChunk = null; _version = 0; _base = 0; _slot = -1; }
    private nint Resolve(Entity entity, bool write)
    {
        _world.EnsureAlive();
        ref var entry = ref _world.Registry.ValidEntry(entity);
        var chunk = entry.Chunk!;
        if (_cachedChunk != chunk || _version != _world.StructureVersion)
        {
            _slot = chunk.Group.Archetype.Layout.IndexOf(_type);
            if (_slot < 0 || _type.IsBuffer || _type.IsTag) throw new InvalidOperationException("Entity does not own the accessor component.");
            _cachedChunk = chunk; _version = _world.StructureVersion; _base = chunk.Address(_slot, 0);
        }
        if (write) chunk.MarkChanged(_slot);
        return _base + (_type.IsChunk ? 0 : entry.Row * sizeof(T));
    }
    public ref readonly T Read(Entity entity) => ref Unsafe.AsRef<T>((void*)Resolve(entity, false));
    public ref T Write(Entity entity) => ref Unsafe.AsRef<T>((void*)Resolve(entity, true));
}
