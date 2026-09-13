namespace Sugoi.Data;

/// <summary>An immutable logical signature: owned component types and shared/meta entities.</summary>
public sealed class EntityType : IEquatable<EntityType>
{
    internal readonly ComponentType[] ComponentArray;
    internal readonly Entity[] MetaArray;
    private readonly int _hash;

    public EntityType(params ComponentType[] components) : this(components.AsSpan(), ReadOnlySpan<Entity>.Empty) { }
    public EntityType(ReadOnlySpan<ComponentType> components, ReadOnlySpan<Entity> meta)
    {
        ComponentArray = SortedDistinct(components);
        MetaArray = SortedDistinct(meta);
        foreach (var entity in MetaArray)
            if (entity.IsNull) throw new ArgumentException("Null cannot be a meta entity.", nameof(meta));
        var hash = new HashCode();
        foreach (var type in ComponentArray) hash.Add(type);
        hash.Add(-1);
        foreach (var entity in MetaArray) hash.Add(entity);
        _hash = hash.ToHashCode();
    }

    public static EntityType Empty { get; } = new();
    public ReadOnlySpan<ComponentType> Components => ComponentArray;
    public ReadOnlySpan<Entity> MetaEntities => MetaArray;
    public bool Contains(ComponentType type) => Array.BinarySearch(ComponentArray, type) >= 0;
    public EntityType With(ComponentType type) => new(TypeSetOps.Union<ComponentType>(ComponentArray, [type]), MetaArray);
    public EntityType Without(ComponentType type) => new(TypeSetOps.Subtract<ComponentType>(ComponentArray, [type]), MetaArray);
    public EntityType WithMeta(Entity entity) => new(ComponentArray, TypeSetOps.Union<Entity>(MetaArray, [entity]));
    public EntityType WithoutMeta(Entity entity) => new(ComponentArray, TypeSetOps.Subtract<Entity>(MetaArray, [entity]));
    public bool Equals(EntityType? other) => ReferenceEquals(this, other) || other is not null &&
        _hash == other._hash && ComponentArray.AsSpan().SequenceEqual(other.ComponentArray) && MetaArray.AsSpan().SequenceEqual(other.MetaArray);
    public override bool Equals(object? obj) => obj is EntityType other && Equals(other);
    public override int GetHashCode() => _hash;

    internal static T[] SortedDistinct<T>(ReadOnlySpan<T> values) where T : IComparable<T>, IEquatable<T>
    {
        if (values.IsEmpty) return [];
        T[] data = values.ToArray();
        Array.Sort(data);
        int length = 1;
        for (int i = 1; i < data.Length; i++)
            if (!data[i].Equals(data[length - 1])) data[length++] = data[i];
        if (length != data.Length) Array.Resize(ref data, length);
        return data;
    }
}

public readonly record struct TypeDelta(EntityType Added, EntityType Removed)
{
    internal EntityType Apply(EntityType source) => new(
        TypeSetOps.Union<ComponentType>(TypeSetOps.Subtract<ComponentType>(source.ComponentArray, Removed.ComponentArray), Added.ComponentArray),
        TypeSetOps.Union<Entity>(TypeSetOps.Subtract<Entity>(source.MetaArray, Removed.MetaArray), Added.MetaArray));
}
