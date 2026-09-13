using System.Runtime.InteropServices;

namespace Sugoi.Data;

/// <summary>Reusable native arena. Individual allocations have no free; Reset invalidates every borrow.</summary>
public sealed unsafe class NativeBumpArena : IDisposable
{
    private readonly int _defaultBlockBytes;
    private readonly List<Block> _blocks = [];
    private int _current;
    private bool _disposed;
    private sealed class Block(nint pointer, int bytes)
    {
        public readonly nint Pointer = pointer;
        public readonly int Bytes = bytes;
        public int Cursor;
    }

    public NativeBumpArena(int blockBytes = 64 * 1024)
    {
        if (blockBytes < 64) throw new ArgumentOutOfRangeException(nameof(blockBytes));
        _defaultBlockBytes = LayoutMath.AlignUp(blockBytes, ArchetypeLayout.ChunkAlignment);
    }

    public long AllocatedBytes => _blocks.Sum(b => (long)b.Bytes);

    public nint Allocate(int bytes, int alignment = 16)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        LayoutMath.ValidateAlignment(alignment);
        if (alignment > ArchetypeLayout.ChunkAlignment) throw new ArgumentOutOfRangeException(nameof(alignment));
        if (bytes == 0) bytes = 1;
        while (_current < _blocks.Count)
        {
            var block = _blocks[_current];
            int start = LayoutMath.AlignUp(block.Cursor, alignment);
            if (bytes <= block.Bytes - start)
            {
                block.Cursor = checked(start + bytes);
                return block.Pointer + start;
            }
            _current++;
        }
        int size = LayoutMath.AlignUp(Math.Max(_defaultBlockBytes, bytes), ArchetypeLayout.ChunkAlignment);
        nint pointer = (nint)NativeMemory.AlignedAlloc((nuint)size, ArchetypeLayout.ChunkAlignment);
        if (pointer == 0) throw new OutOfMemoryException();
        _blocks.Add(new(pointer, size) { Cursor = bytes });
        return pointer;
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var block in _blocks) block.Cursor = 0;
        _current = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var block in _blocks) NativeMemory.AlignedFree((void*)block.Pointer);
        _blocks.Clear(); _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Synchronous thread-owned scratch. Never carry allocations across an await or Reset.</summary>
public sealed class NativeScratchArena : IDisposable
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly NativeBumpArena _arena;
    public NativeScratchArena(int blockBytes = 32 * 1024) => _arena = new(blockBytes);
    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Scratch arena used by another thread.");
    }
    public nint Allocate(int bytes, int alignment = 16) { CheckThread(); return _arena.Allocate(bytes, alignment); }
    public void Reset() { CheckThread(); _arena.Reset(); }
    public void Dispose() { CheckThread(); _arena.Dispose(); }
}
