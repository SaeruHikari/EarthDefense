using System.Runtime.InteropServices;

namespace Sugoi.Data;

internal sealed class StagingChange
{
    internal readonly List<ComponentType> Added = [], Removed = [];
    internal readonly List<Entity> AddedMeta = [], RemovedMeta = [];
    internal bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && AddedMeta.Count == 0 && RemovedMeta.Count == 0;
    internal bool Add(ComponentType type) { Removed.Remove(type); return !Insert(Added, type); }
    internal bool Remove(ComponentType type) { bool had = Added.Remove(type); Insert(Removed, type); return had; }
    internal void AddMeta(Entity entity) { RemovedMeta.Remove(entity); Insert(AddedMeta, entity); }
    internal void RemoveMeta(Entity entity) { AddedMeta.Remove(entity); Insert(RemovedMeta, entity); }
    internal bool SameAdded(StagingChange other) => Added.SequenceEqual(other.Added) && AddedMeta.SequenceEqual(other.AddedMeta);
    internal EntityType EntityType() => new(Added.ToArray(), AddedMeta.ToArray());
    internal TypeDelta Delta() => new(new EntityType(Added.ToArray(), AddedMeta.ToArray()), new EntityType(Removed.ToArray(), RemovedMeta.ToArray()));
    internal void Clear() { Added.Clear(); Removed.Clear(); AddedMeta.Clear(); RemovedMeta.Clear(); }
    private static bool Insert<T>(List<T> items, T value) where T : IComparable<T>
    {
        int index = items.BinarySearch(value);
        if (index >= 0) return false;
        items.Insert(~index, value); return true;
    }
}

internal sealed class StagingRecord
{
    internal readonly object Gate = new();
    internal readonly StagingChange Change = new();
    internal Entity Entity;
    internal int Index;
    internal bool Destroy;
    internal void Reset(Entity entity, int index) { Change.Clear(); Entity = entity; Index = index; Destroy = false; }
}

internal sealed unsafe class StagingRow : IDisposable
{
    internal readonly ComponentDescriptor Descriptor;
    internal readonly ComponentOps Ops;
    internal readonly StagingRowLayout Layout;
    private readonly object _gate = new();
    private readonly List<nint> _blocks = [];
    private byte[] _constructed = [];
    internal StagingRow(ComponentDescriptor descriptor) { Descriptor = descriptor; Ops = descriptor.Ops; Layout = new(descriptor.Layout); }
    internal long AllocatedBytes { get { lock (_gate) return (long)_blocks.Count(p => p != 0) * Layout.BlockBytes; } }

    internal nint Ensure(int record)
    {
        lock (_gate)
        {
            int block = Layout.BlockIndex(record);
            while (_blocks.Count <= block) _blocks.Add(0);
            if (_blocks[block] == 0)
            {
                nint pointer = (nint)NativeMemory.AlignedAlloc((nuint)Layout.BlockBytes, (nuint)Layout.Alignment);
                if (pointer == 0) throw new OutOfMemoryException();
                _blocks[block] = pointer;
            }
            if (_constructed.Length <= record) Array.Resize(ref _constructed, checked((block + 1) * Layout.RecordsPerBlock));
            return _blocks[block] + Layout.Offset(record);
        }
    }
    internal nint Pointer(int record) { lock (_gate) return _blocks[Layout.BlockIndex(record)] + Layout.Offset(record); }
    internal nint PointerExclusive(int record) => _blocks[Layout.BlockIndex(record)] + Layout.Offset(record);
    internal bool Constructed(int record) { lock (_gate) return record < _constructed.Length && _constructed[record] != 0; }
    internal bool ConstructedExclusive(int record) => record < _constructed.Length && _constructed[record] != 0;
    internal void Mark(int record, bool value) { lock (_gate) _constructed[record] = value ? (byte)1 : (byte)0; }
    internal void MarkExclusive(int record, bool value) => _constructed[record] = value ? (byte)1 : (byte)0;
    internal void Destroy(StagingRecord record)
    {
        if (!Constructed(record.Index)) return;
        Ops.Destroy(new(null, record.Entity), Pointer(record.Index));
        Mark(record.Index, false);
    }
    internal void DestroyExclusive(StagingRecord record)
    {
        if (!ConstructedExclusive(record.Index)) return;
        Ops.Destroy(new(null, record.Entity), PointerExclusive(record.Index));
        MarkExclusive(record.Index, false);
    }
    public void Dispose()
    {
        foreach (nint block in _blocks) if (block != 0) NativeMemory.AlignedFree((void*)block);
        _blocks.Clear(); _constructed = [];
    }
}

/// <summary>Synchronous borrowed notifications. Removed payloads and destroyed entities are still readable during the callback.</summary>
public interface IStructuralChangeSink
{
    void ComponentAdded(World world, Entity entity, ComponentType type, nint payload) { }
    void ComponentRemoved(World world, Entity entity, ComponentType type, nint payload) { }
    void EntityDestroyed(World world, Entity entity) { }
}
