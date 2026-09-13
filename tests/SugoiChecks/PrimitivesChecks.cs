using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sugoi.Data;

internal static unsafe class PrimitivesChecks
{
    private struct A { public int Value; }
    private struct B { public long Value; public B(long value) => Value = value; }
    private struct C { public long Value; public C(long value) => Value = value; }
    private struct RefValue { public Entity Target; public int Value; }
    private struct Owner { public nint Pointer; }
    private struct Marker { }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Primitives: " + message);
    }
    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Primitives: expected " + typeof(T).Name + ": " + message);
    }
    public static void Run()
    {
        Layouts(); Masks(); Registration(); BuffersAndLifetimes(); Pools();
        Console.WriteLine("PASS primitives: layouts, native lifecycle/buffer, SIMD, registry, pools");
    }

    private static void Layouts()
    {
        var types = new TypeRegistry();
        var a = types.Register(new ComponentRegistration<A> { Id = new("00000001-0000-0000-0000-000000000001"), Alignment = 16 });
        var b = types.Register(new ComponentRegistration<B> { Id = new("00000002-0000-0000-0000-000000000001"), Alignment = 8 });
        var c = types.Register(new ComponentRegistration<C> { Id = new("00000003-0000-0000-0000-000000000001"), Alignment = 8, Kind = ComponentKind.Chunk });
        var tag = types.Register(new ComponentRegistration<Marker> { Id = new("00000004-0000-0000-0000-000000000001"), Kind = ComponentKind.Tag });
        var layout = ArchetypeLayout.Create([types.Get(c), types.Get(tag), types.Get(b), types.Get(a)]);
        Check(Unsafe.SizeOf<Entity>() == 8, "Entity64");
        Check(layout.Columns.Count == 3 && layout.FirstChunkComponent == 2, "tag excluded; chunk singleton partition");
        Check(types.Get(a).Layout.Stride == 4, "16-byte column alignment must not inflate 4-byte row stride");
        int[] capacities = [3271, 26208, 52423];
        for (int i = 0; i < 3; i++)
        {
            var pool = layout.For((PoolKind)i);
            Check(pool.TotalBytes == (i == 0 ? 65536 : i == 1 ? 524288 : 1048576), "three native bin sizes");
            Check(pool.PayloadOffset == 64 && pool.PayloadBytes == pool.TotalBytes - 64, "native chunk header");
            Check(pool.Capacity == capacities[i], "fixed golden capacity " + i);
            Check(pool.VersionOffset == pool.PayloadBytes - 12, "4-byte column versions with no locks");
            Check(pool.Offset(0) % 16 == 0 && pool.Offset(1) % 8 == 0 && pool.Offset(2) % 8 == 0, "column alignment");
            Check(pool.Offset(1) + pool.Capacity * 8 <= pool.Offset(2), "ordinary columns do not overlap singleton");
            Check(pool.Offset(2) + 8 <= pool.VersionOffset, "chunk column stored once");
        }
        var buffer = new BufferLayout(sizeof(int), sizeof(int), 4);
        Check(buffer.HeaderSize == 24 && buffer.HeaderAlignment == 8, "pointer/u64/u64 buffer ABI");
        Check(buffer.InlineOffset == 24 && buffer.StorageSize == 40, "inline buffer packed geometry");
        Check(BufferLayout.GrowthCapacity(1, 0) == 4 && BufferLayout.GrowthCapacity(3, 2) == 20, "SkrBase buffer growth policy");
        Check(LayoutMath.NaturalAlignment<A>() == 4 && LayoutMath.NaturalAlignment<B>() == 8, "CLR native alignment facts");
        Throws<ArgumentOutOfRangeException>(() => LayoutMath.AlignUp(int.MaxValue, 3), "invalid alignment");
        Throws<OverflowException>(() => LayoutMath.AlignUp(int.MaxValue, 8), "alignment overflow");
    }

    private static void Masks()
    {
        var random = new Random(74031);
        foreach (MaskScanMode mode in Enum.GetValues<MaskScanMode>())
        {
            foreach (int length in new[] { 0, 1, 3, 4, 5, 7, 8, 9, 15, 16, 17, 33, 129, 1025 })
            {
                var masks = new uint[length];
                for (int i = 0; i < masks.Length; i++) masks[i] = (uint)random.Next(16);
                for (int pattern = 0; pattern < 3; pattern++)
                {
                    if (pattern == 1) Array.Fill(masks, 3u);
                    if (pattern == 2) Array.Fill(masks, 8u);
                    List<MaskRange> expected = [];
                    int open = -1;
                    for (int i = 0; i <= masks.Length; i++)
                    {
                        bool hit = i < masks.Length && (masks[i] & 3u) == 3u && (masks[i] & 8u) == 0;
                        if (hit && open < 0) open = i;
                        if (!hit && open >= 0) { expected.Add(new(open, i - open)); open = -1; }
                    }
                    List<MaskRange> actual = [];
                    try { foreach (var range in MaskScanner.Scan(masks, 3, 8, mode)) actual.Add(range); }
                    catch (PlatformNotSupportedException) { break; }
                    Check(expected.SequenceEqual(actual), $"SIMD {mode} exact continuous ranges length={length} pattern={pattern}");
                }
            }
        }
        uint bits = 0;
        Parallel.For(0, 32, i => ComponentMask.Enable(ref bits, 1u << i));
        Check(bits == uint.MaxValue, "concurrent mask OR does not lose bits");
        Parallel.For(0, 32, i => ComponentMask.Disable(ref bits, 1u << i));
        Check(bits == 0, "concurrent mask AND does not lose bits");
    }

    private static void Registration()
    {
        var registry = new TypeRegistry();
        var id = new Guid("ee000001-0000-0000-0000-000000000001");
        var first = registry.Register(new ComponentRegistration<A> { Id = id });
        var descriptor = registry.Get(first);
        var second = registry.Register(new ComponentRegistration<A> { Id = id, Name = "renamed-A", Construct = static (in ComponentContext _, ref A value) => value.Value = 7 });
        Check(first == second && ReferenceEquals(descriptor, registry.Get(second)) && descriptor.Revision == 2, "compatible callback replacement preserves descriptor identity");
        var b = registry.Register<B>(new Guid("ee000002-0000-0000-0000-000000000001"));
        Check(b.Index > first.Index && registry.Count == 2, "registration stays appendable");
        Throws<InvalidOperationException>(() => registry.Register(new ComponentRegistration<A> { Id = id, Alignment = 16 }), "incompatible layout replacement");
        Throws<ArgumentException>(() => registry.Register(new ComponentRegistration<A> { Id = Guid.Empty }), "stable GUID required");
        Throws<ArgumentException>(() => registry.Register(new ComponentRegistration<C> { Id = Guid.NewGuid(), Kind = ComponentKind.Chunk | ComponentKind.Pinned }), "pinned chunk invalid");
    }

    private static void BuffersAndLifetimes()
    {
        using var runtime = new EcsRuntime();
        using var world = runtime.CreateWorld();
        var entity = world.Create(new EntityType());
        var context = new ComponentContext(world, entity);
        using var scratch = new NativeBumpArena();
        var bufferType = runtime.Types.RegisterBuffer(new ComponentRegistration<RefValue>
        {
            Id = new("ed000001-0000-0000-0000-000000000001"),
            Remap = static (ref RefValue value, EntityRemapper map) => value.Target = map(value.Target)
        }, 2);
        var descriptor = runtime.Types.Get(bufferType);
        var ops = descriptor.Ops;
        var bufferOps = ops.Buffer!;
        nint source = scratch.Allocate(descriptor.Layout.Size, descriptor.Layout.Alignment);
        nint target = scratch.Allocate(descriptor.Layout.Size, descriptor.Layout.Alignment);
        nint copy = scratch.Allocate(descriptor.Layout.Size, descriptor.Layout.Alignment);
        ops.Construct(context, source);
        var borrowed = new BufferColumn<RefValue>(source, bufferOps, context);
        borrowed.Add(new() { Target = entity, Value = 11 });
        ops.Move(context, source, context, target);
        var moved = new BufferColumn<RefValue>(target, bufferOps, context);
        Check(moved.IsInline && moved[0].Value == 11 && borrowed.Count == 0, "inline move rebinds pointer and ends source value");
        Check(bufferOps.Data(target) == target + descriptor.Layout.Buffer!.InlineOffset, "inline pointer belongs to destination");
        moved.Add(new() { Value = 12 }); moved.Add(new() { Value = 13 });
        Check(!moved.IsInline && moved.Count == 3, "spill to heap");
        nint heap = bufferOps.Data(target);
        ops.Move(context, target, context, source);
        Check(bufferOps.Data(source) == heap && bufferOps.Count(target) == 0, "heap ownership transfer without copy");
        ops.Copy(context, source, context, copy);
        Check(bufferOps.Data(copy) != heap && bufferOps.Count(copy) == 3, "buffer clone allocates independent elements");
        ops.Remap(copy, _ => Entity.Null);
        Check(new BufferColumn<RefValue>(copy, bufferOps, context)[0].Target.IsNull, "buffer entity references remapped");
        borrowed.RemoveAt(1);
        Check(borrowed.Count == 2 && borrowed[1].Value == 13, "ordered buffer erase");
        borrowed.Set(borrowed.Span.Slice(1));
        Check(borrowed.Count == 1 && borrowed[0].Value == 13, "Set accepts aliasing slices without reading freed storage");
        borrowed.Resize(1);
        Check(borrowed.Count == 1, "shrink count");
        borrowed.Reserve(70); borrowed.ShrinkToFit();
        Check(borrowed.IsInline && borrowed[0].Value == 13, "heap to inline shrink preserves data and rebinds storage");
        ops.Destroy(context, source); ops.Destroy(context, target); ops.Destroy(context, copy);

        int constructs = 0, copies = 0, moves = 0, destroys = 0, alive = 0;
        var ownerType = runtime.Types.Register(new ComponentRegistration<Owner>
        {
            Id = new("ed000002-0000-0000-0000-000000000001"),
            Construct = (in ComponentContext ctx, ref Owner value) =>
            {
                Check(ctx.World == world && ctx.Entity == entity, "constructor world/entity context");
                value.Pointer = (nint)NativeMemory.Alloc(sizeof(int)); *(int*)value.Pointer = 5; constructs++; alive++;
            },
            Copy = (in ComponentContext sc, in Owner from, in ComponentContext dc, ref Owner to) =>
            {
                to.Pointer = (nint)NativeMemory.Alloc(sizeof(int)); *(int*)to.Pointer = *(int*)from.Pointer; copies++; alive++;
            },
            Move = (in ComponentContext sc, ref Owner from, in ComponentContext dc, ref Owner to) => { to = from; from = default; moves++; },
            Destroy = (in ComponentContext ctx, ref Owner value) => { if (value.Pointer != 0) { NativeMemory.Free((void*)value.Pointer); destroys++; alive--; } }
        });
        var ownerOps = runtime.Types.Get(ownerType).Ops;
        nint o1 = scratch.Allocate(sizeof(Owner)), o2 = scratch.Allocate(sizeof(Owner)), o3 = scratch.Allocate(sizeof(Owner));
        ownerOps.Construct(context, o1); ownerOps.Copy(context, o1, context, o2); ownerOps.Move(context, o2, context, o3);
        ownerOps.Destroy(context, o1); ownerOps.Destroy(context, o3);
        Check(constructs == 1 && copies == 1 && moves == 1 && destroys == 2 && alive == 0, "nontrivial lifecycle balance; no moved-from double destructor");
    }

    private static void Pools()
    {
        using (var chunks = new NativeChunkPool())
        {
            foreach (PoolKind kind in Enum.GetValues<PoolKind>())
            {
                nint p = chunks.Rent(kind);
                Check((p & 63) == 0, "native aligned bins");
                Throws<InvalidOperationException>(() => chunks.Return((PoolKind)(((int)kind + 1) % 3), p), "wrong pool return rejected");
                chunks.Return(kind, p);
                Throws<InvalidOperationException>(() => chunks.Return(kind, p), "double return rejected");
                nint again = chunks.Rent(kind);
                Check(again == p, "returned bin is reused");
                chunks.Return(kind, again);
            }
            chunks.Trim(); Check(chunks.AllocatedBytes == 0, "pool trim releases native memory");
        }
        using (var fixedPool = new NativeFixedBlockPool())
        {
            var blocks = new nint[NativeFixedBlockPool.BlocksPerSlab];
            for (int i = 0; i < blocks.Length; i++) blocks[i] = fixedPool.Rent();
            Check(fixedPool.AllocatedBytes == 1024 * 1024 && blocks.Distinct().Count() == 1024, "fixed pool has exactly one slab and distinct slots");
            Check(!fixedPool.TryRent(out var exhausted) && exhausted == 0, "fixed pool reports exhaustion without growth");
            Throws<InvalidOperationException>(() => fixedPool.Rent(), "fixed pool Rent rejects exhaustion");
            Throws<InvalidOperationException>(() => fixedPool.Reset(), "cannot reset while metadata is leased");
            fixedPool.Return(blocks[0]);
            Check(fixedPool.TryRent(out var reused) && reused == blocks[0], "one returned block becomes available after exhaustion");
            Check(!fixedPool.TryRent(out _), "reuse does not increase fixed capacity");
            for (int i = 0; i < blocks.Length; i++) fixedPool.Return(blocks[i]);
            fixedPool.Reset();
            fixedPool.Reset();
            for (int i = 0; i < blocks.Length; i++) blocks[i] = fixedPool.Rent();
            Check(blocks.Distinct().Count() == 1024 && !fixedPool.TryRent(out _), "repeated reset cannot double-enqueue free blocks");
            for (int i = 0; i < blocks.Length; i++) fixedPool.Return(blocks[i]);
        }
        using (var arena = new NativeBumpArena(64))
        {
            nint a = arena.Allocate(13, 16), b = arena.Allocate(128, 32);
            Check((a & 15) == 0 && (b & 31) == 0, "arena aligned growth");
            long allocated = arena.AllocatedBytes;
            arena.Reset();
            Check(arena.Allocate(13, 16) == a && arena.AllocatedBytes == allocated, "arena reset retains and reuses storage");
            var types = new ComponentType[] { new(1), new(3) };
            Entity[] meta = [Entity.Null];
            nint packed = arena.Allocate(MetadataPacking.Size(types.Length, meta.Length), 8);
            var layout = MetadataPacking.Write(packed, types, meta);
            Check(layout.Size == 16 && layout.MetaOffset == 8, "metadata size/padding authority");
            Check(MetadataPacking.Types(packed, layout).SequenceEqual(types) && MetadataPacking.Meta(packed, layout).SequenceEqual(meta), "metadata pack roundtrip");
        }
    }
}
