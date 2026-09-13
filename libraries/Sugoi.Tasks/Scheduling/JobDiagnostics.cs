using System.Diagnostics;
using Sugoi.Data;

namespace Sugoi.Tasks;

public interface IJobDebugInfo { string DebugName { get; } }

/// <summary>Optional BCL tracing. No listener means no Activity allocation in the batch path.</summary>
public static class JobDiagnostics
{
    public const string SourceName = "Sugoi.Tasks";
    private static readonly ActivitySource Source = new(SourceName);
    internal static Activity? StartBatch(string? name, QueryRange range, int index, int taskIndex)
    {
        if (!Source.HasListeners()) return null;
        var activity = Source.StartActivity(name ?? "ECS Job", ActivityKind.Internal);
        activity?.SetTag("ecs.chunk", range.ChunkId);
        activity?.SetTag("ecs.entity_start", index);
        activity?.SetTag("ecs.entity_count", range.Count);
        activity?.SetTag("ecs.task_index", taskIndex);
        return activity;
    }
}
