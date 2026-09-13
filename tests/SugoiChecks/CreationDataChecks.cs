using System.Runtime.InteropServices;
using Sugoi.Data;

internal static class CreationDataChecks
{
    [StructLayout(LayoutKind.Sequential, Size = 512)]
    private struct Record { public int Ordinal; }
    private struct Owner { public int Token; }
    private sealed class InitializationFailure : Exception { }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Creation data: " + message);
    }
    private struct Initializer : IEntityInitializer
    {
        internal World World;
        internal int Baseline, Calls, Initialized, ThrowOnCall;
        internal List<int> Starts;
        public void Initialize(in ChunkView view, int entityStartIndex)
        {
            Check(entityStartIndex == Initialized, "batch offsets are contiguous across chunks");
            Check(World.EntityCount == Baseline + Initialized + view.Count, "later batches are not materialized before this callback");
            Starts.Add(entityStartIndex); Calls++;
            var records = view.WriteOwned<Record>();
            var owners = view.ReadOwned<Owner>();
            for (int i = 0; i < view.Count; i++)
            {
                Check(owners[i].Token != 0 && World.Exists(view.Entities[i]), "all columns constructed and identities visible before initialization");
                records[i].Ordinal = 42 + entityStartIndex + i;
            }
            Initialized += view.Count;
            bool rejected = false;
            try { World.Create(EntityType.Empty); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "initializer cannot invalidate its view by structural reentry");
            if (Calls == ThrowOnCall) throw new InitializationFailure();
        }
    }

    public static void Run()
    {
        using var runtime = new EcsRuntime();
        var record = runtime.Types.Register<Record>(new Guid("e9000001-0000-0000-0000-000000000001"));
        int next = 0, destroyed = 0;
        var live = new HashSet<int>();
        var owner = runtime.Types.Register(new ComponentRegistration<Owner>
        {
            Id = new("e9000002-0000-0000-0000-000000000001"),
            Construct = (in ComponentContext context, ref Owner value) =>
            {
                Check(context.World is not null && context.World.Exists(context.Entity), "constructor sees its registered owner");
                value.Token = ++next; live.Add(value.Token);
            },
            Destroy = (in ComponentContext context, ref Owner value) =>
            {
                Check(value.Token != 0 && live.Remove(value.Token), "each constructed resource has exactly one destructor");
                destroyed++;
            }
        });
        var type = new EntityType(record, owner);
        using (var world = runtime.CreateWorld())
        {
            var entities = new Entity[2500];
            var initializer = new Initializer { World = world, Starts = [] };
            world.Create(type, entities, ref initializer);
            Check(initializer.Calls >= 3 && initializer.Initialized == entities.Length, "caller retains mutable initializer state without boxing");
            for (int i = 0; i < entities.Length; i++) Check(world.Read<Record>(entities[i]).Ordinal == 42 + i, "created values initialized once");
            var reserved = new Entity[1250]; world.ReserveEntities(reserved);
            var reservedInitializer = new Initializer { World = world, Baseline = world.EntityCount, Starts = [] };
            world.CreateReserved(type, reserved, ref reservedInitializer);
            Check(reservedInitializer.Initialized == reserved.Length && reservedInitializer.Starts[0] == 0, "reserved initializer starts at its own batch index zero");
            for (int i = 0; i < reserved.Length; i++) Check(world.Read<Record>(reserved[i]).Ordinal == 42 + i, "reserved identities receive matching values");
            Check(world.Read<Record>(entities[0]).Ordinal == 42 && live.Count == 3750, "existing values survive initialization into a partially filled chunk");
        }
        Check(live.Count == 0 && destroyed == 3750, "ordinary disposal releases all initialized batches");
        using (var failing = runtime.CreateWorld())
        {
            var output = new Entity[2500];
            var initializer = new Initializer { World = failing, Starts = [], ThrowOnCall = 2 };
            bool failed = false;
            try { failing.Create(type, output, ref initializer); } catch (InitializationFailure) { failed = true; }
            Check(failed && failing.IsFaulted && initializer.Calls == 2 && initializer.Initialized == failing.EntityCount,
                "failed initializer keeps completed views owned and exposes its updated caller state");
            Check(failing.EntityCount < output.Length && live.Count == failing.EntityCount, "later views were never constructed after failure");
        }
        Check(live.Count == 0 && destroyed == next, "faulted-world disposal never double-destroys initialized owners");
        Console.WriteLine("PASS synchronous creation bridge: per-view callbacks, reserved IDs, ref state, fault ownership");
    }
}
