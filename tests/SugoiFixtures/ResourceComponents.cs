using System.Runtime.CompilerServices;
using Sugoi.Data;

namespace SugoiFixtures;

[InlineArray(2)]
public struct ResourcePair { private ResourceReference _element; }

public readonly struct ResourceNested(ResourceReference reference)
{
    public readonly ResourceReference Asset = reference;
}

[Component("8f513ee4-c30d-49fb-972c-9fc3b4637601")]
public partial struct ResourceSet
{
    public ResourceReference Primary;
    public ResourceNested Nested;
    public ResourcePair Extras;
    [ResourceField] private Guid _effect;
    public void SetEffect(Guid value) => _effect = value;
}

[BufferComponent("8f513ee4-c30d-49fb-972c-9fc3b4637602", 2)]
public partial struct ResourceElement { public ResourceReference Asset; }

[Component("8f513ee4-c30d-49fb-972c-9fc3b4637603", Kind = ComponentKind.Chunk)]
public partial struct ResourceHeader { public ResourceReference Asset; }

[Component("8f513ee4-c30d-49fb-972c-9fc3b4637604")]
public readonly partial struct CustomResource(Guid id)
{
    private readonly Guid _id = id;
    [ComponentResourceScan]
    private static void Scan(in CustomResource value, ResourceVisitor visitor)
    {
        if (value._id != Guid.Empty) visitor(value._id);
    }
}
