using System.Runtime.CompilerServices;
using Sugoi.Data;

internal static class WorldChecks
{
    private struct Value { public int Number; }
    private struct Extra { public long Number; }
    private struct TagA { }
    private struct TagB { }
    private struct Element { public Entity Link; public int Number; }
    private struct Cleanup { public int Number; public Cleanup(int number) => Number = number; }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("World: " + message); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    public static void Run()
    {
        Check(Unsafe.SizeOf<Entity>() == 8 && default(Entity).IsNull, "Entity64 default ABI");
        Check(Entity.FromRawValue(0x0000_0001_0000_0003UL) != Entity.FromRawValue(0x0000_0002_0000_0003UL), "generation participates in equality");
        Check(!Entity.TryFromRawValue(3, out _) && !Entity.TryFromRawValue(ulong.MaxValue, out _), "reserved identity rejected");
        Check(Entity.NextGeneration(0xffff_fffe) == 1, "generation skips reserved values");

        using var runtime = new EcsRuntime();
        var value = runtime.Types.Register<Value>(new Guid("437e2149-20dd-43d4-8aef-488628cb9201"));
        var extra = runtime.Types.Register<Extra>(new Guid("437e2149-20dd-43d4-8aef-488628cb9202"));
        var tagA = runtime.Types.Register<TagA>(new Guid("437e2149-20dd-43d4-8aef-488628cb9203"), kind: ComponentKind.Tag);
        var tagB = runtime.Types.Register<TagB>(new Guid("437e2149-20dd-43d4-8aef-488628cb9204"), kind: ComponentKind.Tag);
        var buffer = runtime.Types.RegisterBuffer(new ComponentRegistration<Element>
        {
            Id = new Guid("437e2149-20dd-43d4-8aef-488628cb9205"),
            Remap = static (ref Element e, EntityRemapper map) => e.Link = map(e.Link)
        }, 3);
        int destructs = 0;
        var cleanup = runtime.Types.Register(new ComponentRegistration<Cleanup>
        {
            Id = new Guid("437e2149-20dd-43d4-8aef-488628cb9206"), Kind = ComponentKind.Pinned,
            Destroy = (in ComponentContext _, ref Cleanup c) => destructs++
        });
        using var world = runtime.CreateWorld();
        var entities = new Entity[20];
        world.Create(new EntityType(value, runtime.EnabledMaskType, runtime.DirtyMaskType, buffer, tagA), entities);
        for (int i = 0; i < entities.Length; i++) world.Get<Value>(entities[i]).Number = i;
        using var all = world.CreateQuery(new QueryDescription().WithAll(value));
        Check(all.Count == 20, "batch create/query count");
        foreach (var view in all)
            for (int row = 0; row < view.Count; row++) Check(view.ReadOwned<Value>()[row].Number == (int)view.Entities[row].Index, "SoA column binding");

        world.Disable(entities[3], value); world.Disable(entities[4], value);
        Check(all.Count == 18, "enabled SIMD filtering");
        using var notValue = world.CreateQuery(new QueryDescription().Without(value));
        Check(notValue.Count == 2, "none filter against disabled owned values");
        world.Add(entities[3], new Extra { Number = 900 });
        Check(!world.ComponentsEnabled(entities[3], [value]) && world.Read<Extra>(entities[3]).Number == 900, "cast remaps mask by local slot");
        world.Enable(entities[3], value);
        Check(all.Count == 19, "enable after type migration");
        Check(world.GetEntityType(entities[3]).Contains(tagA), "cast retains tags");

        var oldRange = world.GetView(entities[2]);
        world.Add(entities[2], tagB);
        bool stale = false;
        try { _ = oldRange.Entities.Length; } catch (InvalidOperationException) { stale = true; }
        Check(stale, "view invalidated by structure");
        Check(world.Read<Value>(entities[2]).Number == 2, "tag-only cast keeps data");
        using var queryLease = all.AcquireUsage();
        Throws<InvalidOperationException>(all.Dispose);
        int beforeFailedDispose = world.EntityCount;
        Throws<InvalidOperationException>(world.Dispose);
        Check(!world.IsDisposed && world.EntityCount == beforeFailedDispose && world.Exists(entities[0]), "query lease rejects world disposal before any data is destroyed");
        queryLease.Dispose();
        using (world.AcquireUsage()) Throws<InvalidOperationException>(() => world.Destroy(entities[1]));

        uint since = world.ChangeVersion;
        world.AdvanceChangeVersion();
        world.Get<Value>(entities[0]).Number = 123;
        using var changed = world.CreateQuery(new QueryDescription().WithAll(value).ChangedSince(since, value));
        Check(changed.Count > 0, "changed columns survive removal of locks");

        var values = world.GetBuffer<Element>(entities[0]);
        for (int i = 0; i < 12; i++) values.Add(new Element { Link = entities[1], Number = i });
        Check(!values.IsInline && values.Count == 12, "world buffer heap growth");
        world.Add(entities[0], extra);
        Check(world.GetBuffer<Element>(entities[0])[11].Number == 11, "heap ownership after migration");
        world.Remove<Extra>(entities[0]);
        Check(world.GetBuffer<Element>(entities[0])[2].Link == entities[1], "buffer data after second migration");

        var meta = world.Create(new EntityType(extra)); world.Get<Extra>(meta).Number = 404;
        var shared = world.Create(new EntityType([value], [meta]));
        Check(world.Read<Extra>(shared).Number == 404, "shared value resolves meta owner");
        using var sharedQuery = world.CreateQuery(new QueryDescription().WithShared(extra));
        Check(sharedQuery.Matches(shared), "shared query");
        var originalSharedType = world.GetEntityType(shared);
        world.Destroy(meta);
        Throws<ArgumentException>(() => world.Create(originalSharedType));
        world.ValidateMeta();
        Check(world.GetEntityType(shared).MetaEntities.Length == 0, "invalid meta removed");

        var pinned = world.Create(new EntityType(value, cleanup));
        world.Get<Value>(pinned).Number = 614;
        world.Destroy(pinned);
        // archetype.cpp:265-302 and storage.cpp:152: dead retains all columns until final PIN removal.
        Check(world.Exists(pinned) && !world.IsAlive(pinned) && world.Has<Value>(pinned) && world.Read<Value>(pinned).Number == 614,
            "PIN logical death retains ordinary values for cleanup");
        Throws<InvalidOperationException>(() => world.Instantiate(pinned));
        world.Remove<Cleanup>(pinned);
        Check(!world.Exists(pinned) && destructs == 1, "last PIN removal releases identity");

        var before = entities[10]; world.Destroy(before);
        var recycled = world.Create(new EntityType(value));
        Check(recycled.Index == before.Index && recycled.Generation != before.Generation && !world.Exists(before), "recycle cannot revive stale identity");
        var reserved = new Entity[2]; world.ReserveEntities(reserved);
        Check(!world.Exists(reserved[0]), "reserved is not stored");
        world.CreateReserved(new EntityType(value), reserved);
        Check(world.Exists(reserved[0]), "create reserved identities");
        var reservedUnique = new Entity[1]; world.ReserveEntities(reservedUnique);
        Throws<ArgumentException>(() => world.CreateReserved(new EntityType(value), [reservedUnique[0], reservedUnique[0]]));
        Throws<ArgumentException>(() => world.CancelReservation([reservedUnique[0], reservedUnique[0]]));
        world.CreateReserved(new EntityType(value), reservedUnique);
        Check(world.Exists(reservedUnique[0]) && !world.IsFaulted, "duplicate reservation input rejected before attachment or release");

        var batch = world.GetEntityRanges([entities[0], entities[7], entities[8]]);
        Check(batch.Sum(static r => r.Count) == 3, "explicit range coalescing preserves selection");
        var visits = new List<Entity>();
        world.VisitEntities([entities[5], entities[9], entities[6]], (in ChunkView view) =>
        {
            var current = view.Entities.ToArray();
            visits.AddRange(current);
            world.Destroy(current);
        });
        Check(visits.SequenceEqual(new[] { entities[5], entities[9], entities[6] }), "callback mutations relocate later input identities");

        // Incomparable writer overloads both exclude their overlap, matching the source pairwise excludes.
        var onlyA = world.Create(new EntityType(value, tagA));
        var onlyB = world.Create(new EntityType(value, tagB));
        var both = world.Create(new EntityType(value, tagA, tagB));
        using var qa = world.CreateQuery(new QueryDescription().WithAll(value, tagA).WritePhase(value, 0));
        using var qb = world.CreateQuery(new QueryDescription().WithAll(value, tagB).WritePhase(value, 0));
        Check(qa.Matches(onlyA) && !qa.Matches(both) && qb.Matches(onlyB) && !qb.Matches(both), "pairwise phase overload excludes");

        using var limited = runtime.CreateWorld(2);
        limited.Create(EntityType.Empty); limited.Create(EntityType.Empty);
        Throws<InvalidOperationException>(() => limited.Create(EntityType.Empty));
        Check(!limited.IsFaulted, "capacity validation precedes mutation payload work");
    }
}
