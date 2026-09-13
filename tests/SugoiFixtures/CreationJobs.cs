using Sugoi.Data;
using Sugoi.Tasks;

namespace SugoiFixtures;

[CreationJob(Name = "Initialize fleet")]
[CreationMeta(nameof(Owner))]
[CreationComponent(typeof(Selected))]
public partial struct FleetCreation
{
    public Entity Owner;
    public int Initialized;
    public int Calls;

    private void Execute(Span<Position> positions, Span<Velocity> velocities, BufferWrite<Link> links,
        ChunkWrite<ChunkStatistics> statistics, SharedRead<SharedScale> scale,
        RandomReader<SharedScale> parent, in JobContext context)
    {
        if (context.Index != Initialized || context.TaskIndex != 0)
            throw new InvalidOperationException("Creation body state or native range offset was not preserved.");
        if (parent.Read(Owner).Value != scale.Value.Value)
            throw new InvalidOperationException("Creation random and shared bindings disagree.");
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i].X = context.Index + i;
            velocities[i].X = scale.Value.Value;
            links[i].Add(new Link { Target = context.Entities[i], Weight = 23 });
        }
        statistics.Value.Ticks += positions.Length;
        Initialized += positions.Length;
        Calls++;
    }
}
