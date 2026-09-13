using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SkrGui;

// Source: SkrGuiCore/batch/batch_cmd.hpp and src/batch/batch_cmd.cpp, 611561f8.
public enum EBatchRegionKind : byte { Mesh, Text, Atlas, ClipRect, ClipMesh, Backdrop }

public abstract class BatchRule
{
    public abstract bool Matches(BatchCmd command);
    public abstract bool CanBatch(BatchCmd lhs, BatchCmd rhs);
    public virtual bool CanCrossWhenOverlap(BatchCmd moving, BatchCmd blocker) => false;
    public abstract ulong SortKey(BatchCmd command);
}

public class BatchCmd { public Rectf Bound; public uint BatchDepth; }
public sealed class BatchCmdDrawMesh : BatchCmd
{
    public Mesh? Mesh;
    public Texture? Texture;
    public Shader? Shader;
    public Matrix4x4 Transform;
}
public sealed class BatchCmdText : BatchCmd
{
    public Mesh? Mesh;
    public Matrix4x4 Transform;
    public uint AtlasIndex = uint.MaxValue;
    public ETextPixelMode PixelMode = ETextPixelMode.Sdf;
    public object? Source;
}
public sealed class BatchCmdAtlas : BatchCmd
{
    public Mesh? Mesh;
    public Matrix4x4 Transform;
    public uint AtlasIndex = uint.MaxValue;
}
public class BatchCmdClip : BatchCmd { public BatchRoot? SubRoot; }
public sealed class BatchCmdClipRect : BatchCmdClip { }
public sealed class BatchCmdClipMesh : BatchCmdClip { public Mesh? Mesh; public Matrix4x4 Transform; }
public class BatchCmdBackdrop : BatchCmd { public Mesh? Mesh; public Matrix4x4 Transform; }

public struct SortedBatchCmd
{
    public ulong CommandIndex, BatchIndex, DrawIndexStart, DrawIndexEnd;
}
public struct BatchRegion
{
    public EBatchRegionKind Kind;
    public ulong SortedCmdStart, SortedCmdCount, DrawIndexStart, DrawIndexEnd;
}

public sealed class BatchRoot
{
    public readonly List<BatchCmd> Cmds = [];
    public readonly List<SortedBatchCmd> SortedCmds = [];
    public readonly List<BatchRegion> Batches = [];

    public void NeighbourBatch(IReadOnlyList<BatchRule>? rules = null)
    {
        var context = new BatchBuildContext(this, rules);
        context.InitSortedCmds(); context.RebuildBatches(); context.RefreshBatchIndices();
    }
    public void GroupBatch(ulong lookaheadCount, IReadOnlyList<BatchRule>? rules = null)
    {
        var context = new BatchBuildContext(this, rules);
        context.InitSortedCmds(); context.RebuildBatches();
        while (context.GroupBatchPass(lookaheadCount)) { }
        context.RefreshBatchIndices();
    }
    public void ResortBatch(IReadOnlyList<BatchRule>? rules = null)
    {
        var context = new BatchBuildContext(this, rules);
        context.InitSortedCmds(); context.ComputeBatchDepth();
        // The original final comparison is the original command index, hence all ties are stable.
        SortedCmds.Sort((lhs, rhs) =>
        {
            var a = Cmds[(int)lhs.CommandIndex]; var b = Cmds[(int)rhs.CommandIndex];
            int c = a.BatchDepth.CompareTo(b.BatchDepth); if (c != 0) return c;
            c = BatchBuildContext.CommandKind(a).CompareTo(BatchBuildContext.CommandKind(b)); if (c != 0) return c;
            c = context.SortKeyPrimary(a).CompareTo(context.SortKeyPrimary(b)); if (c != 0) return c;
            c = context.SortKeySecondary(a).CompareTo(context.SortKeySecondary(b));
            return c != 0 ? c : lhs.CommandIndex.CompareTo(rhs.CommandIndex);
        });
        context.RebuildBatches(); context.RefreshBatchIndices();
    }
}

internal sealed class BatchBuildContext(BatchRoot root, IReadOnlyList<BatchRule>? rules)
{
    private sealed class Identity { public readonly ulong Value = (ulong)Interlocked.Increment(ref _nextIdentity); }
    private static long _nextIdentity;
    private static readonly ConditionalWeakTable<object, Identity> Identities = new();
    private static ulong ResourceIdentity(object? value) => value is null ? 0 : Identities.GetValue(value, _ => new Identity()).Value;
    public static EBatchRegionKind CommandKind(BatchCmd command) => command switch
    {
        BatchCmdDrawMesh => EBatchRegionKind.Mesh, BatchCmdText => EBatchRegionKind.Text,
        BatchCmdAtlas => EBatchRegionKind.Atlas, BatchCmdClipRect => EBatchRegionKind.ClipRect,
        BatchCmdClipMesh => EBatchRegionKind.ClipMesh, BatchCmdBackdrop => EBatchRegionKind.Backdrop,
        _ => throw new InvalidOperationException("Unknown batch command type")
    };
    private BatchRule? MatchingRule(BatchCmd command)
    {
        var kind = CommandKind(command);
        if (kind is not (EBatchRegionKind.Mesh or EBatchRegionKind.Text) || rules is null) return null;
        foreach (var rule in rules) if (rule is not null && rule.Matches(command)) return rule;
        return null;
    }
    private static bool BuiltInCanBatch(BatchCmd lhs, BatchCmd rhs) => lhs switch
    {
        BatchCmdDrawMesh a => rhs is BatchCmdDrawMesh b && ReferenceEquals(a.Texture, b.Texture) && ReferenceEquals(a.Shader, b.Shader),
        BatchCmdText a => rhs is BatchCmdText b && a.AtlasIndex == b.AtlasIndex && a.PixelMode == b.PixelMode,
        BatchCmdAtlas a => rhs is BatchCmdAtlas b && a.AtlasIndex == b.AtlasIndex,
        _ => false
    };
    private bool CanBatch(BatchCmd lhs, BatchCmd rhs)
    {
        var kind = CommandKind(lhs); if (kind != CommandKind(rhs)) return false;
        if (BuiltInCanBatch(lhs, rhs)) return true;
        if (kind is not (EBatchRegionKind.Mesh or EBatchRegionKind.Text)) return false;
        var rule = MatchingRule(lhs);
        return rule is not null && ReferenceEquals(rule, MatchingRule(rhs)) && rule.CanBatch(lhs, rhs);
    }
    private bool CanCrossWhenOverlap(BatchCmd moving, BatchCmd blocker)
    {
        if (moving is BatchCmdText a && blocker is BatchCmdText b && a.Source is not null && ReferenceEquals(a.Source, b.Source)) return true;
        if (CommandKind(moving) is not (EBatchRegionKind.Mesh or EBatchRegionKind.Text) || CommandKind(blocker) is not (EBatchRegionKind.Mesh or EBatchRegionKind.Text)) return false;
        var rule = MatchingRule(moving);
        return rule is not null && ReferenceEquals(rule, MatchingRule(blocker)) && rule.CanCrossWhenOverlap(moving, blocker);
    }
    public ulong SortKeyPrimary(BatchCmd command)
    {
        var rule = MatchingRule(command); if (rule is not null) return ResourceIdentity(rule);
        return command switch { BatchCmdDrawMesh m => ResourceIdentity(m.Texture), BatchCmdText t => t.AtlasIndex, BatchCmdAtlas a => a.AtlasIndex, _ => 0 };
    }
    public ulong SortKeySecondary(BatchCmd command)
    {
        var rule = MatchingRule(command); if (rule is not null) return rule.SortKey(command);
        return command switch { BatchCmdDrawMesh m => ResourceIdentity(m.Shader), BatchCmdText t => (ulong)t.PixelMode, _ => 0 };
    }
    private bool IsEffectiveOverlap(BatchCmd moving, BatchCmd blocker) => moving.Bound.Overlaps(blocker.Bound) && !CanCrossWhenOverlap(moving, blocker);
    private bool CanMoveRegionTo(int movingIndex, int targetIndex)
    {
        if (targetIndex + 1 >= movingIndex) return true;
        var moving = root.Batches[movingIndex];
        for (int i = targetIndex + 1; i < movingIndex; ++i)
        {
            var blocker = root.Batches[i];
            for (ulong m = moving.SortedCmdStart; m < moving.SortedCmdStart + moving.SortedCmdCount; ++m)
                for (ulong b = blocker.SortedCmdStart; b < blocker.SortedCmdStart + blocker.SortedCmdCount; ++b)
                    if (IsEffectiveOverlap(root.Cmds[(int)root.SortedCmds[(int)m].CommandIndex], root.Cmds[(int)root.SortedCmds[(int)b].CommandIndex])) return false;
        }
        return true;
    }
    public void InitSortedCmds()
    {
        root.SortedCmds.Clear(); root.SortedCmds.EnsureCapacity(root.Cmds.Count);
        for (int i = 0; i < root.Cmds.Count; ++i) root.SortedCmds.Add(new SortedBatchCmd { CommandIndex = (ulong)i });
        // Native address order becomes stable object identity order in managed memory.
        foreach (var command in root.Cmds) { _ = SortKeyPrimary(command); _ = SortKeySecondary(command); }
    }
    public void RebuildBatches()
    {
        root.Batches.Clear(); root.Batches.EnsureCapacity(root.SortedCmds.Count);
        int i = 0;
        while (i < root.SortedCmds.Count)
        {
            var first = root.Cmds[(int)root.SortedCmds[i].CommandIndex];
            var region = new BatchRegion { Kind = CommandKind(first), SortedCmdStart = (ulong)i, SortedCmdCount = 1 };
            ++i;
            while (i < root.SortedCmds.Count && CanBatch(first, root.Cmds[(int)root.SortedCmds[i].CommandIndex])) { ++region.SortedCmdCount; ++i; }
            root.Batches.Add(region);
        }
    }
    private void MoveRegionAfter(int movingIndex, int targetIndex)
    {
        var moving = root.Batches[movingIndex]; var target = root.Batches[targetIndex];
        int end = (int)(target.SortedCmdStart + target.SortedCmdCount), begin = (int)moving.SortedCmdStart, count = (int)moving.SortedCmdCount;
        var slice = root.SortedCmds.GetRange(begin, count);
        root.SortedCmds.RemoveRange(begin, count); root.SortedCmds.InsertRange(end, slice);
        RebuildBatches();
    }
    public bool GroupBatchPass(ulong lookaheadCount)
    {
        if (lookaheadCount == 0 || root.Batches.Count < 2) return false;
        for (int targetIndex = 0; targetIndex + 1 < root.Batches.Count; ++targetIndex)
        {
            var target = root.Batches[targetIndex]; var targetCmd = root.Cmds[(int)root.SortedCmds[(int)target.SortedCmdStart].CommandIndex];
            ulong lookaheadEnd = System.Math.Min((ulong)root.Batches.Count - 1, (ulong)targetIndex + lookaheadCount);
            for (int movingIndex = targetIndex + 1; (ulong)movingIndex <= lookaheadEnd; ++movingIndex)
            {
                var moving = root.Batches[movingIndex]; var movingCmd = root.Cmds[(int)root.SortedCmds[(int)moving.SortedCmdStart].CommandIndex];
                if (!CanBatch(targetCmd, movingCmd) || !CanMoveRegionTo(movingIndex, targetIndex)) continue;
                MoveRegionAfter(movingIndex, targetIndex); return true;
            }
        }
        return false;
    }
    public void RefreshBatchIndices()
    {
        for (int i = 0; i < root.Batches.Count; ++i)
        {
            var region = root.Batches[i];
            for (ulong j = region.SortedCmdStart; j < region.SortedCmdStart + region.SortedCmdCount; ++j) CollectionsMarshal.AsSpan(root.SortedCmds)[(int)j].BatchIndex = (ulong)i;
        }
    }
    public void ComputeBatchDepth()
    {
        foreach (var command in root.Cmds) command.BatchDepth = 0;
        for (int i = 0; i < root.Cmds.Count; ++i)
        {
            var command = root.Cmds[i]; uint depth = 0;
            for (int j = 0; j < i; ++j)
            {
                var previous = root.Cmds[j]; if (!IsEffectiveOverlap(command, previous)) continue;
                uint overlapDepth = CanBatch(command, previous) ? previous.BatchDepth : previous.BatchDepth + 1;
                depth = System.Math.Max(depth, overlapDepth);
            }
            command.BatchDepth = depth;
        }
    }
}
