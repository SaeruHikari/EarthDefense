namespace Sugoi.Data;

public enum PoolKind { Small = 0, Normal = 1, Large = 2 }

public sealed class PoolLayout
{
    public PoolKind Kind { get; }
    public int TotalBytes { get; }
    public int PayloadOffset { get; }
    public int PayloadBytes { get; }
    public int Capacity { get; }
    public int EntityOffset => 0;
    public int VersionOffset { get; }
    public IReadOnlyList<int> ColumnOffsets { get; }
    private readonly int[] _offsets;

    internal PoolLayout(PoolKind kind, BlockLayout block, int capacity, int versions, int[] offsets)
    {
        Kind = kind; TotalBytes = block.TotalBytes; PayloadOffset = block.PayloadOffset;
        PayloadBytes = block.PayloadBytes; Capacity = capacity; VersionOffset = versions;
        _offsets = offsets; ColumnOffsets = Array.AsReadOnly(offsets);
    }

    public int Offset(int slot) => _offsets[slot];
    public int ColumnVersionOffset(int slot) => checked(VersionOffset + slot * sizeof(uint));
}

/// <summary>One immutable authority for all three sugoi pool layouts. Offsets are payload-relative.</summary>
public sealed class ArchetypeLayout
{
    public const int ChunkHeaderBytes = 64;
    public const int ChunkAlignment = 64;
    public const int EntityStride = sizeof(ulong);
    public IReadOnlyList<ComponentDescriptor> Columns { get; }
    public IReadOnlyList<int> StableOrder { get; }
    public int FirstChunkComponent { get; }
    public PoolLayout Small => _pools[0];
    public PoolLayout Normal => _pools[1];
    public PoolLayout Large => _pools[2];
    private readonly PoolLayout[] _pools;
    private readonly ComponentDescriptor[] _columns;

    private ArchetypeLayout(ComponentDescriptor[] columns, int[] order, PoolLayout[] pools)
    {
        _columns = columns; Columns = Array.AsReadOnly(columns); StableOrder = Array.AsReadOnly(order); _pools = pools;
        FirstChunkComponent = Array.FindIndex(columns, d => (d.Type.Kind & ComponentKind.Chunk) != 0);
        if (FirstChunkComponent < 0) FirstChunkComponent = columns.Length;
    }

    public PoolLayout For(PoolKind kind) => _pools[(int)kind];

    public int IndexOf(ComponentType type)
    {
        int low = 0, high = _columns.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            uint value = _columns[mid].Type.Value;
            if (value == type.Value) return mid;
            if (value < type.Value) low = mid + 1; else high = mid - 1;
        }
        return -1;
    }

    public static ArchetypeLayout Create(IEnumerable<ComponentDescriptor> descriptors)
    {
        var columns = descriptors.Where(d => (d.Type.Kind & ComponentKind.Tag) == 0).OrderBy(d => d.Type.Value).ToArray();
        for (int i = 1; i < columns.Length; i++)
            if (columns[i - 1].Type == columns[i].Type) throw new ArgumentException("Duplicate physical component.", nameof(descriptors));
        var order = Enumerable.Range(0, columns.Length).ToArray();
        // The Windows C++ source compares std::array<char,16>: preserve signed-byte memory order,
        // not System.Guid.CompareTo's field order.
        Array.Sort(order, (a, b) => CompareGuidBytes(columns[a].Id, columns[b].Id));
        var pools = new PoolLayout[3];
        for (int p = 0; p < 3; p++) pools[p] = BuildPool((PoolKind)p, columns, order);
        if (pools[2].Capacity == 0) throw new ArgumentException("Archetype cannot fit one entity in the largest pool.");
        return new(columns, order, pools);
    }

    public static int PoolBytes(PoolKind kind) => kind switch
    {
        PoolKind.Small => 64 * 1024, PoolKind.Normal => 512 * 1024, PoolKind.Large => 1024 * 1024,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static PoolLayout BuildPool(PoolKind kind, ComponentDescriptor[] columns, int[] order)
    {
        var block = BlockLayout.Create(PoolBytes(kind), ChunkHeaderBytes, ChunkAlignment);
        int versions = LayoutMath.AlignDown(checked(block.PayloadBytes - columns.Length * sizeof(uint)), sizeof(uint));
        int top = versions;
        var offsets = new int[columns.Length];
        int perEntity = EntityStride, padding = 0;
        foreach (int slot in order)
        {
            var descriptor = columns[slot];
            if ((descriptor.Type.Kind & ComponentKind.Chunk) != 0)
            {
                // Chunk components are singletons. The reference reads uninitialized capacity
                // here; reserve exactly one instance before calculating entity capacity.
                top = LayoutMath.AlignDown(checked(top - descriptor.Layout.Size), descriptor.Layout.Alignment);
                offsets[slot] = top;
            }
            else
            {
                perEntity = checked(perEntity + descriptor.Layout.Size);
                padding = checked(padding + descriptor.Layout.Alignment);
            }
        }
        int capacity = top <= padding ? 0 : (top - padding) / perEntity;
        int cursor = checked(capacity * EntityStride);
        foreach (int slot in order)
        {
            var d = columns[slot];
            if ((d.Type.Kind & ComponentKind.Chunk) != 0) continue;
            cursor = LayoutMath.AlignUp(cursor, d.Layout.Alignment);
            offsets[slot] = cursor;
            cursor = checked(cursor + capacity * d.Layout.Size);
        }
        if (capacity > 0 && cursor > top) throw new InvalidOperationException("Column layout exceeds reserved capacity.");
        return new(kind, block, capacity, versions, offsets);
    }

    private static int CompareGuidBytes(Guid left, Guid right)
    {
        Span<byte> a = stackalloc byte[16], b = stackalloc byte[16];
        left.TryWriteBytes(a); right.TryWriteBytes(b);
        for (int i = 0; i < 16; i++)
        {
            int comparison = ((sbyte)a[i]).CompareTo((sbyte)b[i]);
            if (comparison != 0) return comparison;
        }
        return 0;
    }
}
