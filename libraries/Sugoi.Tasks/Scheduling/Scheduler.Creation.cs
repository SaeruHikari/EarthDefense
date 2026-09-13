using Sugoi.Data;

namespace Sugoi.Tasks;

public sealed partial class Scheduler
{
    private sealed class CreationOperation(World world)
    {
        internal readonly World World = world;
        internal readonly TaskCompletionSource Completion = JobEvent.NewSignal();
    }
    private readonly HashSet<CreationOperation> _creations = new();

    private CreationOperation BeginCreation(World world)
    {
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_closing, this);
            if (_closingWorlds.Contains(world)) throw new InvalidOperationException("The scheduled World is being released.");
            var operation = new CreationOperation(world);
            _creations.Add(operation);
            _preparing.Add();
            return operation;
        }
    }
    private void EndCreation(CreationOperation operation)
    {
        lock (_lifetime) _creations.Remove(operation);
        _preparing.Decrement();
        operation.Completion.TrySetResult();
    }

    /// <summary>Creates and initializes each native range immediately. User code runs outside scheduler metadata locks.</summary>
    public void CreateEntities<TJob>(World world, Span<Entity> destination, ref TJob creation, MessageBus? messages = null)
        where TJob : ICreationJob
    {
        ArgumentNullException.ThrowIfNull(world);
        if (creation is null) throw new ArgumentNullException(nameof(creation));
        var bus = messages ?? GetMessageBus(world);
        if (!ReferenceEquals(bus.World, world)) throw new ArgumentException("Creation messages must belong to the target World.", nameof(messages));
        var operation = BeginCreation(world);
        try
        {
            var builder = new CreationBuilder(world);
            creation.Build(builder);
            var initializer = new CreationInitializer<TJob>(creation, bus);
            try { world.Create(builder.Complete(), destination, ref initializer); }
            finally { creation = initializer.Job; }
        }
        finally { EndCreation(operation); }
    }

    public void CreateReservedEntities<TJob>(World world, ReadOnlySpan<Entity> reserved, ref TJob creation, MessageBus? messages = null)
        where TJob : ICreationJob
    {
        ArgumentNullException.ThrowIfNull(world);
        if (creation is null) throw new ArgumentNullException(nameof(creation));
        var bus = messages ?? GetMessageBus(world);
        if (!ReferenceEquals(bus.World, world)) throw new ArgumentException("Creation messages must belong to the target World.", nameof(messages));
        var operation = BeginCreation(world);
        try
        {
            var builder = new CreationBuilder(world);
            creation.Build(builder);
            var initializer = new CreationInitializer<TJob>(creation, bus);
            try { world.CreateReserved(builder.Complete(), reserved, ref initializer); }
            finally { creation = initializer.Job; }
        }
        finally { EndCreation(operation); }
    }
}
