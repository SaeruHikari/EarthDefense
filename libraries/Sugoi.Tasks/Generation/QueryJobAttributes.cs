using System;

namespace Sugoi.Tasks;

/// <summary>Generates IQueryJob binding for a partial type with one synchronous, typed Execute method.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class QueryJobAttribute : Attribute
{
    public string? Name { get; set; }
}

/// <summary>Generates the message job contract and typed component/message binding for one subscribed payload type.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class MessageJobAttribute(Type messageType) : Attribute
{
    public Type MessageType { get; } = messageType;
    public string? Name { get; set; }
}

/// <summary>Generates synchronous, by-reference initialization for native creation ranges.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class CreationJobAttribute : Attribute
{
    public string? Name { get; set; }
}

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public sealed class CreationMetaAttribute(string memberName) : Attribute
{
    public string MemberName { get; } = memberName;
}

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public sealed class CreationComponentAttribute(Type componentType) : Attribute
{
    public Type ComponentType { get; } = componentType;
}

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public sealed class CreationBufferAttribute(Type elementType) : Attribute
{
    public Type ElementType { get; } = elementType;
}

/// <summary>Binds an instance TSelf Clone() method to IJobCloneable&lt;TSelf&gt;.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class JobCloneAttribute : Attribute { }

/// <summary>Binds an instance void cleanup method to IDisposable for both submission and batch copies.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class JobDisposeAttribute : Attribute { }

/// <summary>Marks owning job state. An explicit clone and deterministic cleanup are required.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class JobOwnershipAttribute : Attribute { }

/// <summary>Declares additional access not visible in the typed Execute spans, including explicit random reads.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ReadAttribute(Type componentType) : Attribute
{
    public Type ComponentType { get; } = componentType;
    public bool Random { get; set; }
}

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public sealed class WriteAttribute(Type componentType) : Attribute
{
    public Type ComponentType { get; } = componentType;
    public bool Random { get; set; }
}

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public sealed class WithoutAttribute(Type componentType) : Attribute
{
    public Type ComponentType { get; } = componentType;
}
