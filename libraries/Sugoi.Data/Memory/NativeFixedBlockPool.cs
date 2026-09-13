using System.Runtime.InteropServices;

namespace Sugoi.Data;

/// <summary>One fixed 1024-slot slab of 1024-byte blocks. Exhaustion never silently adds another slab.</summary>
public sealed unsafe class NativeFixedBlockPool : IDisposable
{
    public const int BlockBytes = 1024;
    public const int BlocksPerSlab = 1024;
    private nint _slab;
    private readonly Stack<nint> _free = [];
    private readonly HashSet<nint> _leased = [];
    private readonly object _gate = new();
    private bool _disposed;
    public int LeasedCount { get { lock (_gate) return _leased.Count; } }
    public long AllocatedBytes { get { lock (_gate) return _slab == 0 ? 0 : BlockBytes * BlocksPerSlab; } }

    public nint Rent()
    {
        if (TryRent(out nint result)) return result;
        throw new InvalidOperationException("The fixed metadata pool has exhausted its 1024 blocks.");
    }

    public bool TryRent(out nint block)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_slab == 0)
            {
                _slab = (nint)NativeMemory.AlignedAlloc(BlockBytes * BlocksPerSlab, ArchetypeLayout.ChunkAlignment);
                if (_slab == 0) throw new OutOfMemoryException();
                Refill();
            }
            if (_free.Count == 0) { block = 0; return false; }
            block = _free.Pop();
            _leased.Add(block);
            return true;
        }
    }

    /// <summary>Rebuilds the free stack only when no blocks remain leased.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_leased.Count != 0) throw new InvalidOperationException("Return every metadata block before resetting the pool.");
            Refill();
        }
    }

    private void Refill()
    {
        _free.Clear();
        if (_slab == 0) return;
        for (int i = BlocksPerSlab - 1; i >= 0; i--) _free.Push(_slab + i * BlockBytes);
    }

    public void Return(nint block)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_leased.Remove(block)) throw new InvalidOperationException("Block is not leased from this pool.");
            _free.Push(block);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_leased.Count != 0) throw new InvalidOperationException("Metadata blocks must be returned before disposal.");
            if (_slab != 0) NativeMemory.AlignedFree((void*)_slab);
            _slab = 0; _free.Clear(); _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
