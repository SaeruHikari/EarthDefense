using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>A generated optional owned read. HasValue distinguishes absence from an empty column.</summary>
public readonly ref struct OptionalRead<T> where T : unmanaged
{
    private readonly ReadOnlySpan<T> _values;
    public OptionalRead(ChunkView view) { HasValue = view.TryReadOwned<T>(out var values); _values = values; }
    public bool HasValue { get; }
    public ReadOnlySpan<T> Span => _values;
    public int Count => _values.Length;
    public ref readonly T this[int index] => ref _values[index];
}

public readonly ref struct OptionalWrite<T> where T : unmanaged
{
    private readonly Span<T> _values;
    public OptionalWrite(ChunkView view) { HasValue = view.TryWriteOwned<T>(out var values); _values = values; }
    public bool HasValue { get; }
    public Span<T> Span => _values;
    public int Count => _values.Length;
    public ref T this[int index] => ref _values[index];
}

/// <summary>A shared scalar resolved through group meta. The builder declares its owner as random read access.</summary>
public readonly ref struct SharedRead<T> where T : unmanaged
{
    private readonly ReadOnlySpan<T> _value;
    public SharedRead(ChunkView view)
    {
        ref readonly var value = ref view.ReadShared<T>();
        _value = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in value), 1);
    }
    public ref readonly T Value => ref _value[0];
}

public readonly ref struct ChunkRead<T> where T : unmanaged
{
    private readonly ReadOnlySpan<T> _value;
    public ChunkRead(ChunkView view)
    {
        ref readonly var value = ref view.ReadChunk<T>();
        _value = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in value), 1);
    }
    public ref readonly T Value => ref _value[0];
}

public readonly ref struct ChunkWrite<T> where T : unmanaged
{
    private readonly Span<T> _value;
    public ChunkWrite(ChunkView view) => _value = MemoryMarshal.CreateSpan(ref view.WriteChunk<T>(), 1);
    public ref T Value => ref _value[0];
}

public readonly ref struct BufferRead<T> where T : unmanaged
{
    private readonly ChunkView _view;
    public BufferRead(ChunkView view) => _view = view;
    public int Count => _view.Count;
    public ReadOnlySpan<T> this[int row] => _view.ReadBuffer<T>(row);
}

public readonly ref struct BufferWrite<T> where T : unmanaged
{
    private readonly ChunkView _view;
    public BufferWrite(ChunkView view) => _view = view;
    public int Count => _view.Count;
    public BufferColumn<T> this[int row] => _view.WriteBuffer<T>(row);
}

/// <summary>A separately declared random read/write capability with the same physical-column cache as the scalar accessors.</summary>
public struct RandomReadWrite<T> where T : unmanaged
{
    private readonly World _world;
    private ComponentAccessor<T> _accessor;
    public RandomReadWrite(World world) { _world = world; _accessor = world.GetAccessor<T>(); }
    public ref readonly T Read(Entity entity) => ref _accessor.Read(entity);
    public ref T Write(Entity entity) => ref _accessor.Write(entity);
    public bool Exists(Entity entity) => _world.Exists(entity);
}
