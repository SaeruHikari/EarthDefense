using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sugoi.Data;

/// <summary>Per-column native operations. Trivial ranges use bulk copies; callbacks execute immediately.</summary>
public abstract unsafe class ComponentOps
{
    public ComponentLayout Layout { get; }
    public abstract bool IsTrivial { get; }
    public abstract bool HasReferences { get; }
    public virtual bool HasResources => false;
    public abstract bool CanCopy { get; }
    public virtual bool CanBulkConstruct => IsTrivial;
    public virtual bool CanBulkCopy => IsTrivial;
    public virtual bool CanBulkMove => IsTrivial;
    public virtual bool CanBulkDestroy => IsTrivial;
    public virtual BufferOps? Buffer => null;

    protected ComponentOps(ComponentLayout layout) => Layout = layout;
    public abstract void Construct(in ComponentContext context, nint destination);
    public abstract void Copy(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination);
    public abstract void Move(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination);
    public abstract void Destroy(in ComponentContext context, nint value);
    public abstract void Remap(nint value, EntityRemapper remapper);
    public virtual void ScanResources(nint value, ResourceVisitor visitor) { }

    public void ScanResources(nint values, int count, ResourceVisitor visitor)
    {
        if (!HasResources) return;
        ArgumentNullException.ThrowIfNull(visitor);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        for (int i = 0; i < count; i++) ScanResources(values + checked(i * Layout.Stride), visitor);
    }

    public void Construct(World world, ReadOnlySpan<Entity> entities, nint destination)
    {
        if (CanBulkConstruct)
        {
            NativeMemory.Clear((void*)destination, checked((nuint)entities.Length * (nuint)Layout.Stride));
            return;
        }
        for (int i = 0; i < entities.Length; i++) Construct(new(world, entities[i]), destination + checked(i * Layout.Stride));
    }

    public void Destroy(World world, ReadOnlySpan<Entity> entities, nint values)
    {
        if (CanBulkDestroy) return;
        for (int i = 0; i < entities.Length; i++) Destroy(new(world, entities[i]), values + checked(i * Layout.Stride));
    }

    public void Copy(World sourceWorld, ReadOnlySpan<Entity> sourceEntities, nint source,
        World destinationWorld, ReadOnlySpan<Entity> destinationEntities, nint destination)
    {
        if (sourceEntities.Length != destinationEntities.Length) throw new ArgumentException("Entity range lengths differ.");
        if (!CanCopy) throw new InvalidOperationException("An owning component requires a Copy hook for duplication.");
        if (CanBulkCopy)
        {
            NativeMemory.Copy((void*)source, (void*)destination, checked((nuint)sourceEntities.Length * (nuint)Layout.Stride));
            return;
        }
        for (int i = 0; i < sourceEntities.Length; i++)
            Copy(new(sourceWorld, sourceEntities[i]), source + checked(i * Layout.Stride),
                new(destinationWorld, destinationEntities[i]), destination + checked(i * Layout.Stride));
    }

    public void Move(World sourceWorld, ReadOnlySpan<Entity> sourceEntities, nint source,
        World destinationWorld, ReadOnlySpan<Entity> destinationEntities, nint destination)
    {
        if (sourceEntities.Length != destinationEntities.Length) throw new ArgumentException("Entity range lengths differ.");
        if (source == destination) return;
        if (CanBulkMove)
        {
            // Span.CopyTo explicitly supports overlap (swap-back and in-column compaction).
            int bytes = checked(sourceEntities.Length * Layout.Stride);
            new ReadOnlySpan<byte>((void*)source, bytes).CopyTo(new Span<byte>((void*)destination, bytes));
            return;
        }
        if (destination > source && destination < source + checked(sourceEntities.Length * Layout.Stride))
        {
            for (int i = sourceEntities.Length - 1; i >= 0; i--)
                Move(new(sourceWorld, sourceEntities[i]), source + checked(i * Layout.Stride),
                    new(destinationWorld, destinationEntities[i]), destination + checked(i * Layout.Stride));
        }
        else
        {
            for (int i = 0; i < sourceEntities.Length; i++)
                Move(new(sourceWorld, sourceEntities[i]), source + checked(i * Layout.Stride),
                    new(destinationWorld, destinationEntities[i]), destination + checked(i * Layout.Stride));
        }
    }

    public void Remap(nint values, int count, EntityRemapper remapper)
    {
        if (!HasReferences) return;
        for (int i = 0; i < count; i++) Remap(values + checked(i * Layout.Stride), remapper);
    }

    /// <summary>Repeat a source value. Trivial columns use the reference's doubling memcpy strategy.</summary>
    public void Duplicate(in ComponentContext sourceContext, nint source, World destinationWorld,
        ReadOnlySpan<Entity> destinationEntities, nint destination)
    {
        if (destinationEntities.IsEmpty) return;
        if (!CanCopy) throw new InvalidOperationException("An owning component requires a Copy hook for duplication.");
        if (CanBulkCopy)
        {
            NativeMemory.Copy((void*)source, (void*)destination, (nuint)Layout.Stride);
            int written = 1;
            while (written < destinationEntities.Length)
            {
                int amount = Math.Min(written, destinationEntities.Length - written);
                NativeMemory.Copy((void*)destination, (void*)(destination + checked(written * Layout.Stride)), checked((nuint)amount * (nuint)Layout.Stride));
                written += amount;
            }
            return;
        }
        for (int i = 0; i < destinationEntities.Length; i++)
            Copy(sourceContext, source, new(destinationWorld, destinationEntities[i]), destination + checked(i * Layout.Stride));
    }
}

internal sealed unsafe class TypedComponentOps<T> : ComponentOps where T : unmanaged
{
    private readonly ComponentRegistration<T> _registration;
    public override bool IsTrivial => _registration.Construct is null && _registration.Copy is null && _registration.Move is null && _registration.Destroy is null;
    public override bool HasReferences => _registration.Remap is not null;
    public override bool HasResources => _registration.ScanResources is not null;
    public override bool CanCopy => _registration.Destroy is null || _registration.Copy is not null;
    public override bool CanBulkConstruct => _registration.Construct is null;
    public override bool CanBulkCopy => _registration.Copy is null && _registration.Destroy is null;
    public override bool CanBulkMove => _registration.Move is null;
    public override bool CanBulkDestroy => _registration.Destroy is null;
    internal TypedComponentOps(ComponentLayout layout, ComponentRegistration<T> registration) : base(layout) => _registration = registration;

    public override void Construct(in ComponentContext context, nint destination)
    {
        if (Layout.Size == 0) return;
        ref var value = ref Unsafe.AsRef<T>((void*)destination);
        value = default;
        _registration.Construct?.Invoke(context, ref value);
    }

    public override void Copy(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination)
    {
        if (Layout.Size == 0 || source == destination) return;
        ref var from = ref Unsafe.AsRef<T>((void*)source);
        ref var to = ref Unsafe.AsRef<T>((void*)destination);
        to = default;
        if (_registration.Copy is not null) _registration.Copy(sourceContext, in from, destinationContext, ref to);
        else
        {
            if (_registration.Destroy is not null) throw new InvalidOperationException("An owning component requires a Copy hook for duplication.");
            to = from;
        }
    }

    public override void Move(in ComponentContext sourceContext, nint source, in ComponentContext destinationContext, nint destination)
    {
        if (Layout.Size == 0 || source == destination) return;
        ref var from = ref Unsafe.AsRef<T>((void*)source);
        ref var to = ref Unsafe.AsRef<T>((void*)destination);
        if (_registration.Move is not null)
        {
            to = default;
            _registration.Move(sourceContext, ref from, destinationContext, ref to);
        }
        else to = from;
        // Move ends the source lifetime; World must not destruct it again.
        from = default;
    }

    public override void Destroy(in ComponentContext context, nint value)
    {
        if (Layout.Size == 0) return;
        ref var item = ref Unsafe.AsRef<T>((void*)value);
        _registration.Destroy?.Invoke(context, ref item);
        item = default;
    }

    public override void Remap(nint value, EntityRemapper remapper)
    {
        if (_registration.Remap is not null) _registration.Remap(ref Unsafe.AsRef<T>((void*)value), remapper);
    }

    public override void ScanResources(nint value, ResourceVisitor visitor)
    {
        if (_registration.ScanResources is not null) _registration.ScanResources(in Unsafe.AsRef<T>((void*)value), visitor);
    }
}
