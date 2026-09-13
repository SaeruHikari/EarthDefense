using System;

namespace Sugoi.Tasks;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class MessageAttribute(string id) : Attribute
{
    public string Id { get; } = id;
    public string? Name { get; set; }
    public int Alignment { get; set; }
    public bool IsCopyable { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class MessageCopyAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class MessageMoveAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public sealed class MessageDestroyAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedMessageModuleAttribute(Type moduleType) : Attribute
{
    public Type ModuleType { get; } = moduleType;
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedMessageIdentityAttribute(string id, string typeName) : Attribute
{
    public string Id { get; } = id;
    public string TypeName { get; } = typeName;
}
