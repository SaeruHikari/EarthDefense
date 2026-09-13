using System.Runtime.CompilerServices;
using Sugoi.Data;

namespace Sugoi.Tasks;

public delegate void MessageCopy<T>(in T source, ref T destination) where T : unmanaged;
public delegate void MessageMove<T>(ref T source, ref T destination) where T : unmanaged;
public delegate void MessageDestroy<T>(ref T value) where T : unmanaged;

/// <summary>Statically registered native message representation. Lifecycle hooks must not throw.</summary>
public sealed class MessageRegistration<T> where T : unmanaged
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public int Alignment { get; init; }
    public bool IsCopyable { get; init; } = true;
    public MessageCopy<T>? Copy { get; init; }
    public MessageMove<T>? Move { get; init; }
    public MessageDestroy<T>? Destroy { get; init; }
}

/// <summary>All message byte/stride calculations live in this layout, shared by ring, overflow and owned batches.</summary>
public readonly record struct MessageLayout(int Size, int Alignment)
{
    public int PayloadBytes(int capacity) => LayoutMath.AlignUp(checked(Size * capacity), Alignment);
    public static int EntityBytes(int capacity) => checked(Unsafe.SizeOf<Entity>() * capacity);
}

public sealed class MessageDescriptor
{
    internal MessageDescriptor(Guid id, string name, Type type, MessageLayout layout, MessageOperations operations)
    { Id = id; Name = name; RuntimeType = type; Layout = layout; Operations = operations; }
    public Guid Id { get; }
    public string Name { get; }
    public Type RuntimeType { get; }
    public MessageLayout Layout { get; }
    public MessageOperations Operations { get; }
    public bool IsCopyable => Operations.IsCopyable;
}

public abstract class MessageOperations
{
    public abstract bool IsCopyable { get; }
    public abstract bool IsTrivial { get; }
    internal abstract void Copy(nint source, nint destination);
    internal abstract void Move(nint source, nint destination);
    internal abstract void Destroy(nint value);
    internal abstract MessageSubscription CreateSubscription(MessageBus bus, Query query, MessageDescriptor descriptor, int capacity);
}

public sealed unsafe class MessageOperations<T> : MessageOperations where T : unmanaged
{
    private readonly MessageRegistration<T> _registration;
    internal MessageOperations(MessageRegistration<T> registration) => _registration = registration;
    public override bool IsCopyable => _registration.IsCopyable;
    public override bool IsTrivial => _registration.Copy is null && _registration.Move is null && _registration.Destroy is null;
    internal override void Copy(nint source, nint destination)
    {
        if (!IsCopyable) throw new InvalidOperationException($"Message {typeof(T).Name} is move-only.");
        if (_registration.Copy is { } copy) copy(in Unsafe.AsRef<T>((void*)source), ref Unsafe.AsRef<T>((void*)destination));
        else Unsafe.CopyBlockUnaligned((void*)destination, (void*)source, (uint)sizeof(T));
    }
    internal override void Move(nint source, nint destination)
    {
        if (_registration.Move is { } move) move(ref Unsafe.AsRef<T>((void*)source), ref Unsafe.AsRef<T>((void*)destination));
        else if (_registration.Copy is { } copy) copy(in Unsafe.AsRef<T>((void*)source), ref Unsafe.AsRef<T>((void*)destination));
        else Unsafe.CopyBlockUnaligned((void*)destination, (void*)source, (uint)sizeof(T));
    }
    internal override void Destroy(nint value) => _registration.Destroy?.Invoke(ref Unsafe.AsRef<T>((void*)value));
    internal override MessageSubscription CreateSubscription(MessageBus bus, Query query, MessageDescriptor descriptor, int capacity) => new MessageSubscription<T>(bus, query, descriptor, capacity);
}

/// <summary>GUID identity and typed operations are explicit roots for AOT; no runtime discovery or synthesized GUIDs.</summary>
public sealed class MessageRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, MessageDescriptor> _byId = new();
    private readonly Dictionary<Type, MessageDescriptor> _byType = new();

    public MessageDescriptor Register<T>(MessageRegistration<T> registration) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (registration.Id == Guid.Empty) throw new ArgumentException("A stable nonempty message GUID is required.", nameof(registration));
        int natural = LayoutMath.NaturalAlignment<T>();
        int alignment = registration.Alignment == 0 ? natural : registration.Alignment;
        LayoutMath.ValidateAlignment(alignment);
        if (alignment < natural) throw new ArgumentException("Message alignment is below the CLR representation's natural alignment.", nameof(registration));
        if (!registration.IsCopyable && registration.Copy is not null) throw new ArgumentException("Move-only messages cannot register a copy hook.", nameof(registration));
        if (!registration.IsCopyable && registration.Destroy is not null && registration.Move is null)
            throw new ArgumentException("An owning move-only message must provide its move hook.", nameof(registration));
        var layout = new MessageLayout(Unsafe.SizeOf<T>(), alignment);
        lock (_gate)
        {
            if (_byType.TryGetValue(typeof(T), out var existingType) && existingType.Id != registration.Id)
                throw new InvalidOperationException("A message CLR type cannot have two identities.");
            if (_byId.TryGetValue(registration.Id, out var existing))
            {
                if (existing.RuntimeType != typeof(T) || existing.Layout != layout || existing.IsCopyable != registration.IsCopyable)
                    throw new InvalidOperationException("The message GUID is already registered with another representation.");
                return existing;
            }
            var descriptor = new MessageDescriptor(registration.Id, registration.Name ?? typeof(T).FullName ?? typeof(T).Name,
                typeof(T), layout, new MessageOperations<T>(registration));
            _byId.Add(descriptor.Id, descriptor); _byType.Add(typeof(T), descriptor);
            return descriptor;
        }
    }
    public MessageDescriptor Register<T>(Guid id, string? name = null) where T : unmanaged => Register(new MessageRegistration<T> { Id = id, Name = name });
    public MessageDescriptor Get<T>() where T : unmanaged
    {
        lock (_gate) return _byType.TryGetValue(typeof(T), out var descriptor) ? descriptor : throw new InvalidOperationException($"Message {typeof(T).FullName} is not registered.");
    }
    public MessageDescriptor Get(Guid id)
    { lock (_gate) return _byId.TryGetValue(id, out var descriptor) ? descriptor : throw new KeyNotFoundException($"Message {id} is not registered."); }
    public bool TryGet(Guid id, out MessageDescriptor? descriptor) { lock (_gate) return _byId.TryGetValue(id, out descriptor); }
    public bool TryGet<T>(out MessageDescriptor? descriptor) where T : unmanaged { lock (_gate) return _byType.TryGetValue(typeof(T), out descriptor); }
}
