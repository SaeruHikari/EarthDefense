using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Sugoi.Data;

/// <summary>Three reusable aligned native bins. Lifecycle cleanup belongs to the owning World.</summary>
public sealed unsafe class NativeChunkPool : IDisposable
{
    private readonly ConcurrentStack<nint>[] _free = [new(), new(), new()];
    private readonly object _lifetime = new();
    private readonly Dictionary<nint, PoolKind> _leasedBlocks = [];
    private bool _disposed;
    private int _leased;
    public int LeasedCount => Volatile.Read(ref _leased);
    public long AllocatedBytes { get; private set; }

    public nint Rent(PoolKind kind)
    {
        int bytes = ArchetypeLayout.PoolBytes(kind);
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_free[(int)kind].TryPop(out nint block))
            {
                block = (nint)NativeMemory.AlignedAlloc((nuint)bytes, ArchetypeLayout.ChunkAlignment);
                if (block == 0) throw new OutOfMemoryException();
                AllocatedBytes += bytes;
            }
            _leased++;
            _leasedBlocks.Add(block, kind);
            // Only metadata is initialized by the pool. World constructs actual live ranges.
            NativeMemory.Clear((void*)block, ArchetypeLayout.ChunkHeaderBytes);
            return block;
        }
    }

    public void Return(PoolKind kind, nint block)
    {
        if (block == 0) throw new ArgumentNullException(nameof(block));
        ArchetypeLayout.PoolBytes(kind);
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_leasedBlocks.TryGetValue(block, out var actual) || actual != kind)
                throw new InvalidOperationException("Block is not leased from the specified pool.");
            _leasedBlocks.Remove(block);
            _free[(int)kind].Push(block);
            _leased--;
        }
    }

    public void Trim()
    {
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            FreeCached();
        }
    }

    private void FreeCached()
    {
        for (int i = 0; i < 3; i++)
            while (_free[i].TryPop(out nint block))
            {
                NativeMemory.AlignedFree((void*)block);
                AllocatedBytes -= ArchetypeLayout.PoolBytes((PoolKind)i);
            }
    }

    public void Dispose()
    {
        lock (_lifetime)
        {
            if (_disposed) return;
            if (_leased != 0) throw new InvalidOperationException("Dispose the owning Worlds before their chunk pool.");
            FreeCached();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
