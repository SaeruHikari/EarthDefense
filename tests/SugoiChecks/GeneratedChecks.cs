using Sugoi.Data;
using Sugoi.Tasks;
using SugoiFixtures;

internal static class GeneratedChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Generated: " + message); }

    public static async Task RunAsync()
    {
        using var runtime = new EcsRuntime();
        Sugoi.Generated.SugoiFixturesModule.Register(runtime.Types);
        Sugoi.Generated.SugoiFixturesModule.Register(runtime.Types);
        using var source = runtime.CreateWorld();
        using var destination = runtime.CreateWorld();
        Entity[] entities = BuildSource(runtime, source);
        var mapping = new EntityMapping[entities.Length];
        destination.MergeFrom(source, mapping);
        Check(!source.Exists(entities[0]), "import invalidates source identity");
        VerifyReferences(destination, mapping);
        await using var scheduler = new Scheduler(4);
        await scheduler.Dispatch(destination, new MoveJob { Delta = 0.5f }, new TaskOptions { BatchSize = 17 });
        await scheduler.Dispatch(destination, new ClassMoveJob { Delta = 0.5f }, new TaskOptions { BatchSize = 19 });
        foreach (var pair in mapping) Check(destination.Read<Position>(pair.Destination).X == 2, "generated struct/class jobs execute");
        Check(destination.CreateQuery(new QueryDescription().WithAll(runtime.Types.Get<Selected>())).Count == 0, "tag registered without payload");
        TestPinnedLifecycle(runtime);
    }

    private static Entity[] BuildSource(EcsRuntime runtime, World world)
    {
        var entities = new Entity[70];
        world.Create(new EntityType(runtime.Types.Get<Position>(), runtime.Types.Get<Velocity>(), runtime.Types.Get<References>(), runtime.Types.GetBuffer<Link>()), entities);
        foreach (var entity in entities)
        {
            world.Get<Velocity>(entity).X = 2;
            ref var refs = ref world.Get<References>(entity);
            refs.Owner = entity; refs.Nested.Target = entities[1]; refs.Nested.Wingman = entities[2];
            refs.Targets[0] = entities[3]; refs.Targets[1] = entities[4]; refs.Targets[2] = entities[5];
            world.GetBuffer<Link>(entity).Add(new Link { Target = entities[6], Weight = 42 });
        }
        return entities;
    }

    private static void VerifyReferences(World world, EntityMapping[] mapping)
    {
        foreach (var pair in mapping)
        {
            ref readonly var refs = ref world.Read<References>(pair.Destination);
            Check(refs.Owner == pair.Destination && refs.Nested.Target == mapping[1].Destination && refs.Nested.Wingman == mapping[2].Destination, "private and nested entity remap");
            Check(refs.Targets[0] == mapping[3].Destination && refs.Targets[1] == mapping[4].Destination && refs.Targets[2] == mapping[5].Destination, "InlineArray entity remap");
            Check(world.GetBuffer<Link>(pair.Destination)[0].Target == mapping[6].Destination, "generated buffer element visitor");
        }
    }

    private static void TestPinnedLifecycle(EcsRuntime runtime)
    {
        OwnedResource.ResetCounts();
        using var world = runtime.CreateWorld();
        var entity = world.Create(new EntityType(runtime.Types.Get<Position>(), runtime.Types.Get<OwnedResource>()));
        var clone = world.Instantiate(entity);
        Check(!world.Has<OwnedResource>(clone), "PIN not duplicated by ordinary instantiate");
        world.Destroy(entity);
        Check(world.Exists(entity) && !world.IsAlive(entity), "generated PIN cleanup remains");
        world.Remove<OwnedResource>(entity);
        Check(OwnedResource.Constructs == 1 && OwnedResource.Destroys == 1, "generated native ownership released once");
    }
}
