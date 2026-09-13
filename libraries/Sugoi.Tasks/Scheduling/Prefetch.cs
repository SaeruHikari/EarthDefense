using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Sugoi.Data;

namespace Sugoi.Tasks;

internal readonly record struct PrefetchPlan(PrefetchMode Mode, ComponentType[] Streams);

internal static class Prefetch
{
    internal static PrefetchPlan BuildPlan(ComponentAccess[] accesses, TaskOptions options, bool explicitEntities)
    {
        PrefetchMode mode = options.Prefetch ?? PrefetchMode.Off;
        if (!options.Prefetch.HasValue)
            foreach (var access in accesses) mode = (PrefetchMode)Math.Max((int)mode, (int)access.Prefetch);
        if (mode == PrefetchMode.Off || mode == PrefetchMode.Auto &&
            (explicitEntities || options.BatchSize is > 0 and < 128))
            return new(PrefetchMode.Off, []);
        var streams = new List<ComponentType>();
        foreach (var access in accesses)
            if (access.Mode == AccessMode.Sequential && (access.Read || access.Write) &&
                (options.Prefetch.HasValue || access.Prefetch != PrefetchMode.Off) && !streams.Contains(access.Type))
                streams.Add(access.Type);
        if (mode == PrefetchMode.Auto && streams.Count > 4) streams.Clear();
        return new(mode, streams.ToArray());
    }

    internal static unsafe void Apply(QueryRange range, PrefetchPlan plan)
    {
        if (!Sse.IsSupported || plan.Mode == PrefetchMode.Off || plan.Streams.Length == 0 ||
            range.Count == 0 || plan.Mode == PrefetchMode.Auto && range.Count < 128) return;
        var view = range.View;
        bool secondLine = plan.Mode == PrefetchMode.Force;
        if (secondLine) Hint(MemoryMarshal.AsBytes(view.Entities), true);
        foreach (var type in plan.Streams)
        {
            if (type.IsTag || !view.HasOwned(type)) continue;
            Hint(view.ReadOwnedBytes(type), secondLine);
        }
    }

    private static unsafe void Hint(ReadOnlySpan<byte> bytes, bool secondLine)
    {
        if (bytes.IsEmpty) return;
        fixed (byte* pointer = bytes)
        {
            Sse.Prefetch0(pointer);
            if (secondLine && bytes.Length > 64) Sse.Prefetch0(pointer + 64);
        }
    }
}
