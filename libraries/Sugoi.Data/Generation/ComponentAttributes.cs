using System;

namespace Sugoi.Data;

/// <summary>Generates explicit, statically reachable component registration for a partial unmanaged struct.</summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class ComponentAttribute(string id) : Attribute
{
    public string Id { get; } = id;
    public string? Name { get; set; }
    public int Alignment { get; set; }
    public ComponentKind Kind { get; set; }
    public bool GenerateEntityRemap { get; set; } = true;
}

/// <summary>The annotated unmanaged struct is the element type; the registry owns the buffer header and inline layout.</summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class BufferComponentAttribute(string id, int inlineCapacity) : Attribute
{
    public string Id { get; } = id;
    public int InlineCapacity { get; } = inlineCapacity;
    public string? Name { get; set; }
    public int Alignment { get; set; }
    public ComponentKind Kind { get; set; }
    public bool GenerateEntityRemap { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ComponentConstructAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComponentCopyAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComponentMoveAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComponentDestroyAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class ComponentRemapAttribute : Attribute { }

/// <summary>Declares an explicit copy/move/destroy ownership policy. All three callbacks are required.</summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class ComponentOwnershipAttribute : Attribute { }

/// <summary>Generated assembly metadata used by the compiler to discover statically callable manifests.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedComponentModuleAttribute(Type moduleType) : Attribute
{
    public Type ModuleType { get; } = moduleType;
}

/// <summary>Allows GUID conflicts across component assemblies to be diagnosed without runtime reflection.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedComponentIdentityAttribute(string id, string typeName) : Attribute
{
    public string Id { get; } = id;
    public string TypeName { get; } = typeName;
}
