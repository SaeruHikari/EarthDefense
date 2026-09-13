using System.Numerics;
using System.Runtime.CompilerServices;
using Sugoi.Data;

namespace Sugoi.DefenseSimulation;

internal readonly record struct SpawnSpec(int Count, FleetSide Side, Entity ExternalFactory, int FirstSerial);
internal readonly record struct PendingAssociation(Entity Source, Entity SourceWingman, Entity ExternalFactory, FleetSide Side, int Ticket);
internal readonly record struct ImportResult(int PublishedHuman, int PublishedAlien, int Canceled);

/// <summary>A business-owned spawn batch using real per-type staging rows and the Apply protocol.</summary>
internal sealed class GenerationBatch : IDisposable
{
    private readonly StagingWorld _staging;
    private readonly ComponentType _ticketType;
    private readonly List<PendingAssociation> _associations = new();
    private int _owned;

    internal GenerationBatch(EcsRuntime runtime)
    {
        _staging = new(runtime, 128, 128);
        _ticketType = runtime.Types.Get<BirthTicket>();
    }

    /// <summary>Stages values and transient relationships without accessing main-world components.</summary>
    internal void Build(SpawnSpec[] specs)
    {
        if (Interlocked.CompareExchange(ref _owned, 1, 0) != 0) throw new InvalidOperationException("A generation batch must finish before reuse.");
        if (_staging.RecordCount != 0 || _associations.Count != 0) throw new InvalidOperationException("Staging epoch is not empty.");
        try
        {
            foreach (var spec in specs)
            {
                var entities = new Entity[spec.Count];
                for (int i = 0; i < entities.Length; i++) entities[i] = _staging.NewEntity();
                for (int i = 0; i < entities.Length; i++)
                {
                    int serial = checked(spec.FirstSerial + i);
                    float angle = serial * 2.39996323f;
                    float latitude = ((serial * 37) % 2001 - 1000) / 1000f;
                    float horizontal = MathF.Sqrt(MathF.Max(0, 1 - latitude * latitude));
                    Vector3 normal = new(MathF.Cos(angle) * horizontal, latitude, MathF.Sin(angle) * horizontal);
                    float radius = spec.Side == FleetSide.Human ? 100 : 108;
                    Entity wingman = entities[(i + 1) % entities.Length];
                    _staging.Add(entities[i], new Position { Value = normal * radius });
                    _staging.Add(entities[i], new Velocity { Value = Vector3.Cross(normal, Vector3.UnitY) * 0.25f });
                    _staging.Add(entities[i], new Hull { Value = spec.Side == FleetSide.Human ? 12 : 16 });
                    _staging.Add(entities[i], new Weapon { Damage = 3, RangeSquared = 150 * 150, FireEverySteps = spec.Side == FleetSide.Human ? 6 : 4 });
                    _staging.Add(entities[i], new Aircraft { Serial = serial, Side = spec.Side, Wingman = wingman, Factory = Entity.Null });
                    _staging.Add(entities[i], new BirthTicket { Value = serial });
                    if (spec.Side == FleetSide.Human) _staging.Add(entities[i], new Human());
                    else _staging.Add(entities[i], new Alien());
                    _associations.Add(new(entities[i], wingman, spec.ExternalFactory, spec.Side, serial));
                }
            }
            if (_staging.RecordCount != _associations.Count) throw new InvalidOperationException("Staging lost spawn records.");
        }
        catch
        {
            _staging.Clear(); _associations.Clear(); Volatile.Write(ref _owned, 0);
            throw;
        }
    }

    private sealed unsafe class BirthSink(ComponentType ticketType, IReadOnlyList<PendingAssociation> associations) : IStructuralChangeSink
    {
        private readonly Dictionary<int, PendingAssociation> _tickets = associations.ToDictionary(a => a.Ticket);
        internal readonly Dictionary<Entity, Entity> Targets = new(associations.Count);
        public void ComponentAdded(World world, Entity entity, ComponentType type, nint payload)
        {
            if (type != ticketType) return;
            int ticket = Unsafe.Read<BirthTicket>((void*)payload).Value;
            if (entity.IsTransient || !world.Exists(entity) || !_tickets.TryGetValue(ticket, out var association) || !Targets.TryAdd(association.Source, entity))
                throw new InvalidOperationException("Birth sink received an invalid or duplicate business receipt.");
        }
    }

    /// <summary>Runs after producer and main-world jobs finish, before the next combat dispatch.</summary>
    internal ImportResult ImportInto(World main)
    {
        if (Volatile.Read(ref _owned) != 1) throw new InvalidOperationException("No owned generation batch to apply.");
        var sink = new BirthSink(_ticketType, _associations);
        _staging.Apply(main, sink);
        if (sink.Targets.Count != _associations.Count || _staging.RecordCount != 0 || _staging.TransientCount != 0)
            throw new InvalidOperationException("Apply did not publish every birth receipt and clear its epoch.");
        var rejected = new List<Entity>();
        int human = 0, alien = 0;
        foreach (var association in _associations)
        {
            Entity target = sink.Targets[association.Source];
            var aircraft = main.Read<Aircraft>(target);
            if (!aircraft.Factory.IsNull || aircraft.Wingman != sink.Targets[association.SourceWingman])
                throw new InvalidOperationException("Staged wingman references or pre-publication factory state are invalid.");
            if (association.Side == FleetSide.Human)
            {
                if (!main.Exists(association.ExternalFactory) || !main.Has<Factory>(association.ExternalFactory))
                {
                    rejected.Add(target); continue;
                }
                aircraft.Factory = association.ExternalFactory; main.Set(target, in aircraft);
                main.Get<Factory>(association.ExternalFactory).ActiveAircraft++; human++;
            }
            else alien++;
        }
        // A lost factory rejects aircraft before any birth count or capacity is published.
        if (rejected.Count != 0) main.Destroy(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(rejected));
        _associations.Clear(); Volatile.Write(ref _owned, 0);
        return new(human, alien, rejected.Count);
    }

    public void Dispose() => _staging.Dispose();
}
