using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sugoi.Data;
using Sugoi.Tasks;

namespace SugoiFixtures;

[Component("edbe6db2-45c2-4986-9a93-636355a2d101")]
public partial struct Position
{
    public float X, Y, Z;
}

[Component("edbe6db2-45c2-4986-9a93-636355a2d102")]
public partial struct Velocity
{
    public float X, Y, Z;
}

public struct NestedReferences
{
    public Entity Target;
    public Entity Wingman;
}

[InlineArray(3)]
public struct EntityTriple
{
    private Entity _element;
}

[Component("edbe6db2-45c2-4986-9a93-636355a2d103")]
public partial struct References
{
    private Entity _owner;
    public NestedReferences Nested;
    public EntityTriple Targets;
    public Entity Owner { readonly get => _owner; set => _owner = value; }
}

[BufferComponent("edbe6db2-45c2-4986-9a93-636355a2d104", 4)]
public partial struct Link
{
    public Entity Target;
    public int Weight;
}

[Component("edbe6db2-45c2-4986-9a93-636355a2d105", Kind = ComponentKind.Tag)]
public partial struct Selected { }

[Component("edbe6db2-45c2-4986-9a93-636355a2d106", Kind = ComponentKind.Chunk)]
public partial struct ChunkStatistics
{
    public long Ticks;
    public Entity Leader;
}

/// <summary>Owns one unmanaged integer, exercising copy/move/destroy rather than treating unmanaged as trivial.</summary>
[Component("edbe6db2-45c2-4986-9a93-636355a2d107", Kind = ComponentKind.Pinned)]
[ComponentOwnership]
public unsafe partial struct OwnedResource
{
    public nint Pointer;
    public static int Constructs, Copies, Moves, Destroys;

    public static void ResetCounts() => Constructs = Copies = Moves = Destroys = 0;

    [ComponentConstruct]
    private static void Construct(in ComponentContext context, ref OwnedResource value)
    {
        value.Pointer = (nint)NativeMemory.AllocZeroed((nuint)sizeof(int));
        Interlocked.Increment(ref Constructs);
    }

    [ComponentCopy]
    private static void Copy(in ComponentContext sourceContext, in OwnedResource source, in ComponentContext destinationContext, ref OwnedResource destination)
    {
        destination.Pointer = source.Pointer == 0 ? 0 : (nint)NativeMemory.Alloc((nuint)sizeof(int));
        if (source.Pointer != 0) *(int*)destination.Pointer = *(int*)source.Pointer;
        Interlocked.Increment(ref Copies);
    }

    [ComponentMove]
    private static void Move(in ComponentContext sourceContext, ref OwnedResource source, in ComponentContext destinationContext, ref OwnedResource destination)
    {
        destination.Pointer = source.Pointer;
        source.Pointer = 0;
        Interlocked.Increment(ref Moves);
    }

    [ComponentDestroy]
    private static void Destroy(in ComponentContext context, ref OwnedResource value)
    {
        NativeMemory.Free((void*)value.Pointer);
        value.Pointer = 0;
        Interlocked.Increment(ref Destroys);
    }
}

[QueryJob]
public partial struct MoveJob
{
    public float Delta;
    private void Execute(Span<Position> positions, ReadOnlySpan<Velocity> velocities)
    {
        for (var i = 0; i < positions.Length; ++i)
        {
            positions[i].X += velocities[i].X * Delta;
            positions[i].Y += velocities[i].Y * Delta;
            positions[i].Z += velocities[i].Z * Delta;
        }
    }
}

[QueryJob]
public partial class ClassMoveJob
{
    public float Delta;
    private void Execute(Span<Position> positions, ReadOnlySpan<Velocity> velocities)
    {
        for (var i = 0; i < positions.Length; ++i) positions[i].X += velocities[i].X * Delta;
    }
}
