namespace Sugoi.Data;

public readonly record struct MetadataLayout(int TypeOffset, int TypeCount, int MetaOffset, int MetaCount, int Size)
{
    public static MetadataLayout Create(int typeCount, int metaCount)
    {
        if (typeCount < 0 || metaCount < 0) throw new ArgumentOutOfRangeException();
        int metaOffset = LayoutMath.AlignUp(checked(typeCount * sizeof(uint)), sizeof(ulong));
        return new(0, typeCount, metaOffset, metaCount, checked(metaOffset + metaCount * sizeof(ulong)));
    }
}

/// <summary>One packing plan drives allocation, write, and read of runtime type/meta signatures.</summary>
public static unsafe class MetadataPacking
{
    public static int Size(int typeCount, int metaCount) => MetadataLayout.Create(typeCount, metaCount).Size;
    public static MetadataLayout Write(nint destination, int destinationBytes, ReadOnlySpan<ComponentType> types, ReadOnlySpan<Entity> meta)
    {
        var layout = MetadataLayout.Create(types.Length, meta.Length);
        if (destinationBytes < layout.Size) throw new ArgumentException("Metadata destination is too small.", nameof(destinationBytes));
        types.CopyTo(new Span<ComponentType>((void*)(destination + layout.TypeOffset), types.Length));
        meta.CopyTo(new Span<Entity>((void*)(destination + layout.MetaOffset), meta.Length));
        return layout;
    }
    public static MetadataLayout Write(nint destination, ReadOnlySpan<ComponentType> types, ReadOnlySpan<Entity> meta) =>
        Write(destination, Size(types.Length, meta.Length), types, meta);
    public static ReadOnlySpan<ComponentType> Types(nint source, in MetadataLayout layout) => new((void*)(source + layout.TypeOffset), layout.TypeCount);
    public static ReadOnlySpan<Entity> Meta(nint source, in MetadataLayout layout) => new((void*)(source + layout.MetaOffset), layout.MetaCount);
}
