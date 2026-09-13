using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Sugoi.Data;

/// <summary>Appendable process/runtime type catalog. Compatible descriptor replacement requires quiescent Worlds.</summary>
public sealed class TypeRegistry
{
    private readonly object _registration = new();
    private readonly Dictionary<Guid, ComponentDescriptor> _byGuid = [];
    private readonly Dictionary<string, ComponentDescriptor> _byName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Type, ComponentType> _ordinaryTypes = new();
    private readonly ConcurrentDictionary<Type, ComponentType> _bufferTypes = new();
    private ComponentDescriptor[] _descriptors = [];
    public int Count => Volatile.Read(ref _descriptors).Length;
    public int Revision { get; private set; }

    public ComponentType Register<T>(ComponentRegistration<T> registration) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(registration);
        if ((registration.Kind & ComponentKind.Buffer) != 0)
            throw new ArgumentException("Use RegisterBuffer to supply element and inline layout facts.", nameof(registration));
        var layout = BuildLayout(registration);
        return RegisterCore(typeof(T), false, registration.Id, registration.Name ?? typeof(T).FullName ?? typeof(T).Name,
            layout, new TypedComponentOps<T>(layout, registration));
    }

    public ComponentType Register<T>(Guid id, string? name = null, ComponentKind kind = ComponentKind.None) where T : unmanaged =>
        Register(new ComponentRegistration<T> { Id = id, Name = name, Kind = kind });

    public ComponentType RegisterBuffer<T>(ComponentRegistration<T> registration, int inlineCapacity) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(registration);
        if ((registration.Kind & ComponentKind.Tag) != 0) throw new ArgumentException("A buffer cannot be a tag.");
        if ((registration.Kind & (ComponentKind.Chunk | ComponentKind.Pinned)) == (ComponentKind.Chunk | ComponentKind.Pinned))
            throw new ArgumentException("A chunk component cannot be pinned.");
        int natural = LayoutMath.NaturalAlignment<T>();
        int alignment = registration.Alignment == 0 ? Math.Max(IntPtr.Size, natural) : registration.Alignment;
        ValidateColumnAlignment(alignment);
        var buffer = new BufferLayout(Unsafe.SizeOf<T>(), natural, inlineCapacity, alignment);
        var elementLayout = new ComponentLayout(Unsafe.SizeOf<T>(), natural, ComponentKind.None);
        var elementOps = new TypedComponentOps<T>(elementLayout, registration);
        var layout = new ComponentLayout(buffer.StorageSize, buffer.StorageAlignment, registration.Kind | ComponentKind.Buffer, buffer);
        var ops = new BufferComponentOps(layout, new BufferOps(buffer, elementOps));
        return RegisterCore(typeof(T), true, registration.Id, registration.Name ?? $"Buffer<{typeof(T).FullName}>", layout, ops);
    }

    public ComponentType RegisterBuffer<T>(Guid id, int inlineCapacity, string? name = null) where T : unmanaged =>
        RegisterBuffer(new ComponentRegistration<T> { Id = id, Name = name }, inlineCapacity);

    public ComponentType Get<T>() where T : unmanaged => _ordinaryTypes.TryGetValue(typeof(T), out var type) ? type :
        throw new InvalidOperationException($"Component {typeof(T).FullName} has not been registered.");
    public ComponentType GetBuffer<T>() where T : unmanaged => _bufferTypes.TryGetValue(typeof(T), out var type) ? type :
        throw new InvalidOperationException($"Buffer element {typeof(T).FullName} has not been registered.");
    public bool TryGet<T>(out ComponentType type) where T : unmanaged => _ordinaryTypes.TryGetValue(typeof(T), out type);
    public bool TryGetBuffer<T>(out ComponentType type) where T : unmanaged => _bufferTypes.TryGetValue(typeof(T), out type);
    public ComponentDescriptor Describe<T>() where T : unmanaged => Get(Get<T>());
    public ComponentDescriptor Get(ComponentType type)
    {
        var snapshot = Volatile.Read(ref _descriptors);
        if (type.Index >= snapshot.Length || snapshot[type.Index].Type != type) throw new ArgumentException("Unknown component type.", nameof(type));
        return snapshot[type.Index];
    }
    public ComponentDescriptor Get(Guid id)
    {
        lock (_registration) return _byGuid.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException($"Unknown component GUID {id}.");
    }
    public ComponentDescriptor Get(string name)
    {
        lock (_registration) return _byName.TryGetValue(name, out var value) ? value : throw new KeyNotFoundException($"Unknown component {name}.");
    }

    private ComponentType RegisterCore(Type managedType, bool buffer, Guid id, string name, ComponentLayout layout, ComponentOps ops)
    {
        if (id == Guid.Empty) throw new ArgumentException("Components require a nonempty stable GUID.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Components require a name.", nameof(name));
        lock (_registration)
        {
            var typedMap = buffer ? _bufferTypes : _ordinaryTypes;
            if (typedMap.TryGetValue(managedType, out var prior) && Get(prior).Id != id)
                throw new InvalidOperationException($"{managedType} is already registered with another identity.");
            if (_byName.TryGetValue(name, out var sameName) && sameName.Id != id)
                throw new InvalidOperationException($"Component name {name} belongs to another identity.");
            if (_byGuid.TryGetValue(id, out var existing))
            {
                if (!existing.Layout.Equals(layout))
                    throw new InvalidOperationException("Descriptor replacement cannot change size, alignment, kind, or buffer geometry.");
                existing.Name = name;
                existing.Ops = ops;
                existing.Revision++;
                _byName[name] = existing;
                typedMap[managedType] = existing.Type;
                Revision++;
                return existing.Type;
            }
            if (_descriptors.Length >= 0x0fff_ffff) throw new InvalidOperationException("Type index capacity exhausted.");
            var type = new ComponentType((uint)_descriptors.Length | (uint)layout.Kind);
            var descriptor = new ComponentDescriptor(type, id, name, layout, ops);
            var next = new ComponentDescriptor[_descriptors.Length + 1];
            _descriptors.CopyTo(next, 0); next[^1] = descriptor;
            _byGuid.Add(id, descriptor); _byName.Add(name, descriptor);
            Volatile.Write(ref _descriptors, next);
            typedMap[managedType] = type;
            Revision++;
            return type;
        }
    }

    private static ComponentLayout BuildLayout<T>(ComponentRegistration<T> registration) where T : unmanaged
    {
        if ((registration.Kind & (ComponentKind.Chunk | ComponentKind.Pinned)) == (ComponentKind.Chunk | ComponentKind.Pinned))
            throw new ArgumentException("A chunk component cannot be pinned.");
        if ((registration.Kind & (ComponentKind.Chunk | ComponentKind.Tag)) == (ComponentKind.Chunk | ComponentKind.Tag))
            throw new ArgumentException("A chunk component must have storage.");
        int alignment = registration.Alignment == 0 ? LayoutMath.NaturalAlignment<T>() : registration.Alignment;
        ValidateColumnAlignment(alignment);
        bool tag = (registration.Kind & ComponentKind.Tag) != 0;
        if (tag && (registration.Construct is not null || registration.Destroy is not null || registration.Copy is not null || registration.Move is not null || registration.Remap is not null))
            throw new ArgumentException("Tags have no native value and cannot have value lifecycle callbacks.");
        return new(tag ? 0 : Unsafe.SizeOf<T>(), alignment, registration.Kind);
    }

    private static void ValidateColumnAlignment(int alignment)
    {
        LayoutMath.ValidateAlignment(alignment);
        if (alignment > ArchetypeLayout.ChunkAlignment)
            throw new ArgumentOutOfRangeException(nameof(alignment), "Column alignment must not exceed the 64-byte native chunk payload alignment.");
    }
}
