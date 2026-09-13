using Sugoi.Data;

internal static class PackingChecks
{
    private struct Reference { public Entity Target; public int Payload; }
    private struct Header { public Entity Target; public int Stamp; }

    internal static void Run()
    {
        IdentityAndReferences();
        ReservationsAndGenerationWrap();
        PreflightFailure();
        RemapperFailure();
        QueryLeaseDisposal();
        Console.WriteLine("PASS packing: generation history, dense identities, component/buffer/chunk/meta remap, failure ownership and query disposal");
    }

    private static ComponentType RegisterReferences(EcsRuntime runtime) => runtime.Types.Register(new ComponentRegistration<Reference>
    {
        Id = new("a9844767-8e67-41e7-906d-86daef918481"),
        Remap = static (ref Reference value, EntityRemapper map) => value.Target = map(value.Target)
    });

    private static void IdentityAndReferences()
    {
        using var runtime = new EcsRuntime();
        var reference = RegisterReferences(runtime);
        var header = runtime.Types.Register(new ComponentRegistration<Header>
        {
            Id = new("a9844767-8e67-41e7-906d-86daef918482"), Kind = ComponentKind.Chunk,
            Remap = static (ref Header value, EntityRemapper map) => value.Target = map(value.Target)
        });
        using var world = runtime.CreateWorld();
        var original = new Entity[19];
        world.Create(new EntityType(reference, header, runtime.LinkType), original);
        for (int i = 0; i < original.Length; i++) world.Get<Reference>(original[i]).Payload = i + 100;
        foreach (int hole in new[] { 0, 3, 8, 12 }) world.Destroy(original[hole]);
        var stale = original[3];
        var live = original.Where(world.Exists).ToArray();
        for (int i = 0; i < live.Length; i++)
        {
            world.Get<Reference>(live[i]).Target = live[(i + 1) % live.Length];
            var links = world.GetBuffer<EntityLink>(live[i]);
            // Both inline and spilled arrays must traverse every Entity64 element exactly once.
            int count = i % 2 == 0 ? 3 : 21;
            for (int j = 0; j < count; j++) links.Add(new EntityLink { Value = j == 0 ? stale : live[(i + j) % live.Length] });
        }
        world.Get<Header>(live[0]) = new Header { Target = live[^1], Stamp = 901 };
        var dependent = world.Create(new EntityType([reference], [live[0]]));
        world.Get<Reference>(dependent) = new Reference { Target = live[0], Payload = 777 };
        using var query = world.CreateQuery(QueryDescription.Create().WithAll(reference));
        int expectedCount = world.EntityCount;
        var mapping = new EntityMapping[expectedCount];
        Check(world.CompactEntityIds(mapping) == expectedCount, "complete identity mapping");
        var map = mapping.ToDictionary(pair => pair.Source, pair => pair.Destination);
        for (int i = 0; i < mapping.Length; i++)
        {
            Check(mapping[i].Destination.Index == (uint)i && world.Exists(mapping[i].Destination), "dense current index and valid generation");
            Check(!world.Exists(mapping[i].Source), "every old live identity invalidated");
        }
        for (int i = 0; i < live.Length; i++)
        {
            var entity = map[live[i]];
            ref readonly var value = ref world.Read<Reference>(entity);
            Check(value.Payload == Array.IndexOf(original, live[i]) + 100 && value.Target == map[live[(i + 1) % live.Length]], "payload and cyclic ordinary references preserved");
            var links = world.GetBuffer<EntityLink>(entity);
            Check(links.Count == (i % 2 == 0 ? 3 : 21), "buffer length preserved");
            Check(links[0].Value.IsNull, "stale generation reference becomes null");
            for (int j = 1; j < links.Count; j++) Check(links[j].Value == map[live[(i + j) % live.Length]], "buffer identity remapped");
        }
        Check(world.Read<Header>(map[live[0]]).Target == map[live[^1]] && world.Read<Header>(map[live[0]]).Stamp == 901, "chunk singleton remapped once and value retained");
        var childType = world.GetEntityType(map[dependent]);
        Check(childType.MetaEntities.Length == 1 && childType.MetaEntities[0] == map[live[0]], "group meta points to new owner identity");
        Check(world.Read<Reference>(map[dependent]).Target == map[live[0]], "dependent component points to new owner");
        Check(query.Count == expectedCount, "pre-existing query cache refreshes after group relabeling");
        Check(world.Groups.All(group => group.Count != 0), "obsolete empty meta groups reclaimed");
        Check(world.GroupPool.LeasedCount == world.Groups.Count(), "native metadata pool has one lease per retained group");

        var priorIdentities = mapping.Select(pair => pair.Source).ToHashSet();
        for (int cycle = 0; cycle < 5; cycle++)
        {
            var extra = new Entity[23];
            world.Create(new EntityType(reference), extra);
            Check(extra.All(entity => !priorIdentities.Contains(entity)), "growing after compaction cannot resurrect old high-index generations");
            foreach (var entity in extra.Where((_, index) => index % 2 == 0)) world.Destroy(entity);
            var next = new EntityMapping[world.EntityCount];
            world.CompactEntityIds(next);
            foreach (var pair in next) { Check(!world.Exists(pair.Source), "repeated compaction invalidates previous identity"); priorIdentities.Add(pair.Source); }
        }
    }

    private static void ReservationsAndGenerationWrap()
    {
        using var runtime = new EcsRuntime();
        var reference = RegisterReferences(runtime);
        using var world = runtime.CreateWorld();
        var entity = world.Create(new EntityType(reference));
        var reserved = world.Registry.Allocate();
        Throws<InvalidOperationException>(() => world.CompactEntityIds(new EntityMapping[world.EntityCount]), "unresolved reservations rejected before mutation");
        Check(world.Exists(entity) && !world.IsFaulted, "reservation rejection preserves world");
        world.Registry.Release(reserved);

        ref var entry = ref world.Registry.ValidEntry(entity);
        entry.Generation = uint.MaxValue - 1;
        var high = Entity.FromRawValue(((ulong)(uint.MaxValue - 1) << 32) | entity.Index);
        entry.Chunk!.EntityCapacity[entry.Row] = high;
        world.Get<Reference>(high).Target = high;
        var mapping = new EntityMapping[1];
        world.CompactEntityIds(mapping);
        Check(mapping[0].Source == high && mapping[0].Destination.Generation == 1, "generation wraps past reserved zero/max values");
        Check(!world.Exists(high) && world.Read<Reference>(mapping[0].Destination).Target == mapping[0].Destination, "full 64-bit identity remapped across generation wrap");
    }

    private static void PreflightFailure()
    {
        using var runtime = new EcsRuntime();
        var reference = RegisterReferences(runtime);
        using var world = runtime.CreateWorld();
        var owner = world.Create(EntityType.Empty);
        var child = world.Create(new EntityType([reference], [owner]));
        world.Get<Reference>(child) = new Reference { Target = owner, Payload = 44 };
        Throws<ArgumentException>(() => world.CompactEntityIds(new EntityMapping[1]), "mapping capacity validated before identity changes");
        Check(world.Exists(owner) && world.Exists(child), "small output does not mutate identities");

        var leases = new List<nint>();
        try
        {
            while (world.GroupPool.TryRent(out var block)) leases.Add(block);
            Throws<InvalidOperationException>(() => world.CompactEntityIds(new EntityMapping[2]), "future meta signature allocation fails before identity commit");
            Check(!world.IsFaulted && world.Exists(owner) && world.Exists(child), "metadata exhaustion leaves old identities valid");
            Check(world.Read<Reference>(child).Target == owner && world.Read<Reference>(child).Payload == 44, "metadata exhaustion leaves payload unchanged");
        }
        finally { foreach (var block in leases) world.GroupPool.Return(block); }
        var mappings = new EntityMapping[2];
        world.CompactEntityIds(mappings);
        Check(mappings.All(pair => world.Exists(pair.Destination)), "packing can be retried after preflight failure");
    }

    private static void RemapperFailure()
    {
        using var runtime = new EcsRuntime();
        int destroyed = 0;
        var reference = runtime.Types.Register(new ComponentRegistration<Reference>
        {
            Id = new("44b0a697-6edc-4f9b-ae4a-d36fba733080"),
            Copy = static (in ComponentContext _, in Reference source, in ComponentContext __, ref Reference target) => target = source,
            Move = static (in ComponentContext _, ref Reference source, in ComponentContext __, ref Reference target) => { target = source; source = default; },
            Destroy = (in ComponentContext _, ref Reference value) => { destroyed++; value = default; },
            Remap = static (ref Reference value, EntityRemapper _) => throw new InvalidOperationException("Deliberately broken no-throw remapper")
        });
        var world = runtime.CreateWorld();
        var entities = new Entity[3];
        world.Create(new EntityType(reference), entities);
        Throws<InvalidOperationException>(() => world.CompactEntityIds(new EntityMapping[3]), "broken callback reported");
        Check(world.IsFaulted, "destructive remap failure faults world instead of continuing with partial references");
        world.Dispose();
        Check(destroyed == 3 && world.IsDisposed, "identity columns remain coherent for exactly-once disposal after callback failure");
    }

    private static void QueryLeaseDisposal()
    {
        using var runtime = new EcsRuntime();
        var reference = RegisterReferences(runtime);
        var world = runtime.CreateWorld();
        var entity = world.Create(new EntityType(reference));
        world.Get<Reference>(entity).Payload = 812;
        var query = world.CreateQuery(QueryDescription.Create().WithAll(reference));
        using (query.AcquireUsage())
        {
            Throws<InvalidOperationException>(world.Dispose, "active query definitions prevent disposal before reset");
            Check(!world.IsDisposed && world.Exists(entity) && world.Read<Reference>(entity).Payload == 812, "failed disposal cannot destroy data");
            // Definition leases used by subscriptions deliberately do not prevent ordinary safe structure changes.
            var another = world.Create(new EntityType(reference));
            Check(world.Exists(another), "query definition lease does not act as a permanent storage lease");
        }
        world.Dispose();
        Check(world.IsDisposed && query.IsDisposed, "disposal closes queries after leases drain");
        Throws<ObjectDisposedException>(() => query.AcquireUsage(), "closed query cannot admit another lease");
        Throws<ObjectDisposedException>(() => world.CreateQuery(new QueryDescription()), "disposed world cannot create a query");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Packing: " + message);
    }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Packing: expected " + typeof(T).Name + ": " + message);
    }
}
