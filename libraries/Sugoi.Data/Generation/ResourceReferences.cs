namespace Sugoi.Data;

/// <summary>A native resource identity. This does not load, own, or serialize the referenced resource.</summary>
public readonly record struct ResourceReference(Guid Id)
{
    public bool IsNull => Id == Guid.Empty;
}

public delegate void ResourceVisitor(Guid resourceId);
public delegate void ComponentResourceScan<T>(in T value, ResourceVisitor visitor) where T : unmanaged;

/// <summary>Marks a Guid field or readable property as a runtime resource reference.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ResourceFieldAttribute : Attribute { }

/// <summary>Overrides automatic resource scanning with a static void(in T, ResourceVisitor) hook.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComponentResourceScanAttribute : Attribute { }
