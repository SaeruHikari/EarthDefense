using Sugoi.Data;

internal static class TransferChecks
{
    private struct Value { public int Number; }
    private struct References { public Entity Next; public Entity External; public int Serial; }
    private struct Pinned { public int Value; public Pinned(int value) => Value = value; }
    private struct MoveOnly { public int Value; }
    private struct Singleton { public int Value; }
    private struct OwnedSingleton { public int Token; public int Payload; }
    private struct TagA { }
    private struct TagB { }
    private struct Extra { public int Number; }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Transfer: " + message);
    }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Transfer expected " + typeof(T).Name + ": " + message);
    }
    private static (ComponentType Value, ComponentType Ref, ComponentType Buffer, ComponentType Singleton) Register(EcsRuntime runtime)
    {
        var value = runtime.Types.Register<Value>(new Guid("fa000001-0000-0000-0000-000000000001"));
        var references = runtime.Types.Register(new ComponentRegistration<References>
        {
            Id = new("fa000002-0000-0000-0000-000000000001"),
            Remap = static (ref References value, EntityRemapper map) => { value.Next = map(value.Next); value.External = map(value.External); }
        });
        var buffer = runtime.Types.RegisterBuffer(new ComponentRegistration<References>
        {
            Id = new("fa000003-0000-0000-0000-000000000001"),
            Remap = static (ref References value, EntityRemapper map) => { value.Next = map(value.Next); value.External = map(value.External); }
        }, 2);
        var singleton = runtime.Types.Register(new ComponentRegistration<Singleton>
        {
            Id = new("fa000004-0000-0000-0000-000000000001"), Kind = ComponentKind.Chunk
        });
        return (value, references, buffer, singleton);
    }
    public static void Run()
    {
        Merge(); Instantiate(); SingletonMigration(); Defragment(); MetadataReuse(); Failures();
        Console.WriteLine("PASS transfer: whole-World import, prefab sets, references, cross-pool defragmentation");
    }

    private static void Merge()
    {
        using var runtime = new EcsRuntime();
        var types = Register(runtime);
        int destroyed = 0;
        var pinned = runtime.Types.Register(new ComponentRegistration<Pinned>
        {
            Id = new("fa000005-0000-0000-0000-000000000001"), Kind = ComponentKind.Pinned,
            Destroy = (in ComponentContext context, ref Pinned value) => destroyed++
        });
        using var source = runtime.CreateWorld();
        using var target = runtime.CreateWorld();
        Entity targetExisting = target.Create(new EntityType(types.Value));
        target.Set(targetExisting, new Value { Number = 999 });
        Entity meta = source.Create(new EntityType(types.Value));
        Entity staleMeta = source.Create(new EntityType(types.Value));
        source.Set(meta, new Value { Number = 27 });
        Entity a = source.Create(new EntityType([types.Ref, types.Buffer], [meta, staleMeta]));
        Entity b = source.Create(new EntityType(types.Ref, runtime.DisabledType));
        Entity dead = source.Create(new EntityType(types.Ref, pinned));
        source.Destroy(dead);
        Entity stale = source.Create(new EntityType(types.Ref));
        source.Destroy(stale);
        Entity replacement = source.Create(new EntityType(types.Ref));
        Check(stale.Index == replacement.Index && stale.Generation != replacement.Generation, "test generation reuse setup");
        source.Set(a, new References { Next = b, External = stale, Serial = 10 });
        source.Set(b, new References { Next = a, Serial = 20 });
        var buffer = source.GetBuffer<References>(a);
        buffer.Add(new() { Next = b }); buffer.Add(new() { Next = replacement }); buffer.Add(new() { Next = stale });
        source.Destroy(staleMeta);
        using var query = source.CreateQuery(QueryDescription.Create().WithAll(types.Ref).IncludeDisabled());
        Check(query.Count == 3, "cached source query excludes Dead but includes Disabled");
        Entity[] oldEntities = source.Registry.Snapshot();
        var chunkIds = oldEntities.ToDictionary(e => e, e => source.Registry.ValidEntry(e).Chunk!.Id);
        var mappings = new EntityMapping[oldEntities.Length + 1];
        mappings[^1] = new(targetExisting, targetExisting);
        int count = target.MergeFrom(source, mappings);
        Check(count == oldEntities.Length && source.EntityCount == 0 && target.EntityCount == count + 1, "all constructed entities transferred");
        Check(mappings.Take(count).Select(m => m.Source).SequenceEqual(oldEntities), "mapping output follows source index order");
        Check(mappings[^1] == new EntityMapping(targetExisting, targetExisting), "mapping tail remains untouched");
        var map = mappings.Take(count).ToDictionary(m => m.Source, m => m.Destination);
        foreach (Entity old in oldEntities)
        {
            Check(!source.Exists(old) && target.Exists(map[old]), "source identity invalidated and target attached");
            Check(target.Registry.ValidEntry(map[old]).Chunk!.Id == chunkIds[old], "native chunk adopted rather than recreated");
        }
        Check(target.Read<References>(map[a]).Next == map[b] && target.Read<References>(map[b]).Next == map[a], "cyclic references patched once");
        Check(target.Read<References>(map[a]).External.IsNull, "stale full generation does not map to reused source index");
        var movedBuffer = target.GetBuffer<References>(map[a]);
        Check(movedBuffer[0].Next == map[b] && movedBuffer[1].Next == map[replacement] && movedBuffer[2].Next.IsNull, "inline/heap buffer references mapped");
        Check(target.GetEntityType(map[a]).MetaEntities.SequenceEqual(new[] { map[meta] }), "stale meta removed and valid meta remapped");
        Check(target.Read<Value>(map[a]).Number == 27, "shared component resolves in target World");
        Check(target.Has(map[b], runtime.DisabledType) && target.Exists(map[dead]) && !target.IsAlive(map[dead]), "disabled and PIN cleanup entities are included");
        Check(destroyed == 0 && target.Read<Value>(targetExisting).Number == 999, "no import destructor or accidental destination overwrite");
        Check(query.Count == 0, "retained source query observes empty storage");
        Entity reused = source.Create(new EntityType(types.Ref));
        Check(query.Count == 1 && oldEntities.All(old => old != reused), "source query and World reusable; old generations remain invalid");
        target.Remove(map[dead], pinned);
        Check(destroyed == 1 && !target.Exists(map[dead]), "cleanup lifetime now belongs to destination");
    }

    private static void Instantiate()
    {
        using var runtime = new EcsRuntime();
        var types = Register(runtime);
        int destroyed = 0;
        var pinned = runtime.Types.Register(new ComponentRegistration<Pinned>
        {
            Id = new("fb000005-0000-0000-0000-000000000001"), Kind = ComponentKind.Pinned,
            Destroy = (in ComponentContext context, ref Pinned value) => destroyed++
        });
        using var world = runtime.CreateWorld();
        using var remote = runtime.CreateWorld();
        Entity external = world.Create(new EntityType(types.Value));
        Entity b = world.Create(new EntityType(types.Ref, types.Value));
        Entity a = world.Create(new EntityType([types.Ref, types.Buffer, pinned], [b]));
        world.Set(a, new References { Next = b, External = external, Serial = 1 });
        world.Set(b, new References { Next = a, External = external, Serial = 2 });
        world.Set(b, new Value { Number = 77 });
        world.GetBuffer<References>(a).Add(new() { Next = b });
        Entity single = world.Instantiate(a);
        Check(!world.Has(single, pinned) && world.Has(a, pinned) && destroyed == 0, "PIN is excluded without touching source lifetime");
        Check(world.Read<References>(single).Next == b && world.Read<References>(single).External == external, "single-prefab copy retains ordinary reference semantics");
        Entity stale = world.Create(new EntityType(types.Value));
        world.Destroy(stale);
        Entity stalePrefab = world.Create(new EntityType(types.Ref));
        world.Set(stalePrefab, new References { Next = stale });
        Check(world.Read<References>(world.Instantiate(stalePrefab)).Next == stale, "same-World single copy does not silently reinterpret a stale reference");
        Entity[] replicas = new Entity[4];
        world.InstantiateSet([a, b], 2, replicas);
        for (int i = 0; i < 2; i++)
        {
            Entity nextA = replicas[2 * i], nextB = replicas[2 * i + 1];
            Check(world.Read<References>(nextA).Next == nextB && world.Read<References>(nextB).Next == nextA, "per-replica cyclic mapping");
            Check(world.Read<References>(nextA).External == external, "same-World external references retained");
            Check(world.GetBuffer<References>(nextA)[0].Next == nextB, "copied buffer internally remapped");
            Check(world.GetEntityType(nextA).MetaEntities.SequenceEqual(new[] { nextB }), "set meta references map before group construction");
        }
        Entity[] cross = new Entity[2];
        remote.InstantiateSet(world, [a, b], cross);
        Check(remote.Read<References>(cross[0]).Next == cross[1] && remote.Read<References>(cross[1]).Next == cross[0], "cross-World set mapping");
        Check(remote.Read<References>(cross[0]).External.IsNull && world.Read<References>(a).Next == b, "cross-World external dropped; source unmodified");
        Entity crossSingle = remote.Instantiate(world, a);
        Check(remote.Read<References>(crossSingle).Next.IsNull && remote.GetEntityType(crossSingle).MetaEntities.IsEmpty, "cross-World singleton copy has no implicit external association");

        Entity singleton = world.Create(new EntityType(types.Ref, types.Singleton));
        world.Set(singleton, new Singleton { Value = 31 });
        Entity[] singletonCopies = new Entity[2];
        world.Instantiate(singleton, singletonCopies);
        world.Set(singletonCopies[0], new Singleton { Value = 80 });
        Check(world.Read<Singleton>(singleton).Value == 31 && world.Read<Singleton>(singletonCopies[1]).Value == 31, "cloning singleton columns cannot overwrite existing chunk state");
        long[] singletonChunks = singletonCopies.Append(singleton).Select(e => world.Registry.ValidEntry(e).Chunk!.Id).ToArray();
        world.Defragment();
        Check(singletonCopies.Append(singleton).Select(e => world.Registry.ValidEntry(e).Chunk!.Id).SequenceEqual(singletonChunks), "defrag preserves distinct singleton boundaries");

        world.RedirectReferences(e => e == external ? b : e);
        Check(world.Read<References>(a).External == b && world.Read<References>(replicas[0]).External == b, "redirect applies to complete World references");
        var repeated = new Entity[4097];
        world.Instantiate(b, repeated);
        Check(repeated.All(e => world.Read<Value>(e).Number == 77), "bulk duplicate doubling and remainder path");
    }

    private static void Defragment()
    {
        using var runtime = new EcsRuntime();
        var types = Register(runtime);
        using var world = runtime.CreateWorld();
        var signature = new EntityType(types.Value, types.Ref, types.Buffer);
        Entity first = world.Create(signature);
        int smallCapacity = world.Registry.ValidEntry(first).Chunk!.Capacity;
        var batch = new Entity[smallCapacity + 2500];
        world.Create(signature, batch);
        Entity[] all = new[] { first }.Concat(batch).ToArray();
        for (int i = 0; i < all.Length; i++)
        {
            world.Set(all[i], new Value { Number = i + 1 });
            world.Set(all[i], new References { Next = all[(i + 1) % all.Length], Serial = i + 1 });
            if (i % 97 == 0) { var buffer = world.GetBuffer<References>(all[i]); buffer.Add(new() { Next = first, Serial = i + 1 }); }
        }
        PoolKind[] beforeKinds = world.Groups.SelectMany(g => g.Chunks).Select(c => c.PoolKind).Distinct().ToArray();
        Check(beforeKinds.Contains(PoolKind.Small) && beforeKinds.Contains(PoolKind.Normal), "cross-pool defrag setup");
        Entity[] removed = all.Where((_, index) => index % 3 == 0).ToArray();
        world.Destroy(removed);
        var survivors = all.Where(world.Exists).ToArray();
        int count = world.EntityCount;
        int moved = world.Defragment();
        Check(moved > 0 && world.EntityCount == count, "defrag relocates ranges without changing identities");
        foreach (Entity entity in survivors)
        {
            int serial = Array.IndexOf(all, entity) + 1;
            Check(world.Read<Value>(entity).Number == serial && world.Read<References>(entity).Serial == serial, "cross-pool offsets preserve ordinary column values");
            if ((serial - 1) % 97 == 0) Check(world.GetBuffer<References>(entity)[0].Serial == serial, "buffer ownership preserved during defrag");
        }
    }

    private static void Failures()
    {
        using var runtime = new EcsRuntime();
        var types = Register(runtime);
        using var source = runtime.CreateWorld();
        using var target = runtime.CreateWorld();
        Entity entity = source.Create(new EntityType(types.Value));
        Throws<ArgumentException>(() => target.MergeFrom(source, Span<EntityMapping>.Empty), "mapping output capacity");
        Check(source.EntityCount == 1 && target.EntityCount == 0 && !source.IsFaulted && !target.IsFaulted, "merge precheck has no ownership side effects");
        using (var active = source.AcquireUsage())
            Throws<InvalidOperationException>(() => target.MergeFrom(source, new EntityMapping[1]), "source users must finish");
        var moveOnly = runtime.Types.Register(new ComponentRegistration<MoveOnly>
        {
            Id = new("fc000005-0000-0000-0000-000000000001"), Destroy = static (in ComponentContext context, ref MoveOnly value) => value.Value = 0
        });
        Entity owner = source.Create(new EntityType(types.Value, moveOnly));
        Throws<InvalidOperationException>(() => target.Instantiate(source, owner), "Copy capability checked before reservation");
        Check(target.EntityCount == 0 && !target.IsFaulted, "uncopyable preflight does not fault target");
        Entity[] result = new Entity[1];
        target.InstantiateWithDelta(source, owner, new TypeDelta(EntityType.Empty, new EntityType(moveOnly)), result);
        Check(target.Exists(result[0]) && !target.Has(result[0], moveOnly), "removed components need no copy operation");
        Throws<ArgumentException>(() => target.InstantiateSet(source, [entity, entity], new Entity[2]), "duplicate set identities rejected");
    }

    private static void SingletonMigration()
    {
        using var runtime = new EcsRuntime();
        var types = Register(runtime);
        int constructed = 0, copied = 0, moved = 0, destructed = 0, nextToken = 0;
        var alive = new HashSet<int>();
        var owned = runtime.Types.Register(new ComponentRegistration<OwnedSingleton>
        {
            Id = new("fd000001-0000-0000-0000-000000000001"), Kind = ComponentKind.Chunk,
            Construct = (in ComponentContext context, ref OwnedSingleton value) =>
            {
                Check(context.Entity.IsNull, "chunk constructor context is a singleton");
                value.Token = ++nextToken; value.Payload = 17; alive.Add(value.Token); constructed++;
            },
            Copy = (in ComponentContext sc, in OwnedSingleton from, in ComponentContext dc, ref OwnedSingleton to) =>
            {
                Check(sc.Entity.IsNull && dc.Entity.IsNull, "chunk copy uses singleton contexts");
                to = new() { Token = ++nextToken, Payload = from.Payload }; alive.Add(to.Token); copied++;
            },
            Move = (in ComponentContext sc, ref OwnedSingleton from, in ComponentContext dc, ref OwnedSingleton to) =>
            {
                Check(sc.Entity.IsNull && dc.Entity.IsNull, "chunk move uses singleton contexts");
                to = from; from = default; moved++;
            },
            Destroy = (in ComponentContext context, ref OwnedSingleton value) =>
            {
                Check(value.Token != 0 && alive.Remove(value.Token), "moved singleton cannot be destructed twice");
                destructed++;
            }
        });
        var tagA = runtime.Types.Register<TagA>(new Guid("fd000002-0000-0000-0000-000000000001"), kind: ComponentKind.Tag);
        var tagB = runtime.Types.Register<TagB>(new Guid("fd000003-0000-0000-0000-000000000001"), kind: ComponentKind.Tag);
        _ = runtime.Types.Register<Extra>(new Guid("fd000004-0000-0000-0000-000000000001"));
        using var world = runtime.CreateWorld();
        var baseType = new EntityType(types.Value, owned);
        Entity a = world.Create(baseType), b = world.Create(baseType);
        world.Get<OwnedSingleton>(a).Payload = 31;
        Entity existing = world.Create(baseType.With(tagA));
        world.Get<OwnedSingleton>(existing).Payload = 99;
        world.Add(a, tagA);
        Check(copied == 1 && moved == 0, "splitting a multirow chunk copies its singleton exactly once");
        Check(world.Read<OwnedSingleton>(a).Payload == 31 && world.Read<OwnedSingleton>(b).Payload == 31 && world.Read<OwnedSingleton>(existing).Payload == 99,
            "migration cannot merge incompatible singleton values");
        world.Add(b, new Extra { Number = 7 });
        Check(moved == 1 && copied == 1 && world.Read<OwnedSingleton>(b).Payload == 31,
            "last-row physical migration transfers singleton ownership");
        long oldChunk = world.Registry.ValidEntry(b).Chunk!.Id;
        world.Add(b, tagB);
        Check(world.Registry.ValidEntry(b).Chunk!.Id == oldChunk && moved == 1,
            "single-row tag-only change reattaches the native chunk without lifetime work");
        world.Reset();
        Check(alive.Count == 0 && destructed == constructed + copied, "singleton ownership is balanced across split, move, and reset");
    }

    private static void MetadataReuse()
    {
        using var runtime = new EcsRuntime();
        var types = Register(runtime);
        using var world = runtime.CreateWorld();
        using var query = world.CreateQuery(QueryDescription.Create().WithAll(types.Ref));
        for (int i = 0; i < 1100; i++)
        {
            Entity meta = world.Create(new EntityType(types.Value));
            Entity child = world.Create(new EntityType([types.Ref], [meta]));
            Check(query.Count == 1, "query rebuild after previous metadata trim");
            world.Destroy(child); world.Destroy(meta);
            Check(world.TrimEmptyGroups() == 2 && world.GroupPool.LeasedCount == 0,
                "empty signatures return fixed metadata slots");
            Check(query.Count == 0, "query remains usable after its cached groups are trimmed");
        }
        Check(world.GroupPool.AllocatedBytes == 1024 * 1024, "repeated batches reuse the single metadata slab");
        using (world.AcquireUsage()) Throws<InvalidOperationException>(() => world.TrimEmptyGroups(), "trim requires structural boundary");
    }
}
