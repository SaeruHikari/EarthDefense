using Sugoi.Data;

namespace Sugoi.Tasks;

/// <summary>Online component hazards. Sequential writer frontiers are retained independently for every chunk.</summary>
internal sealed class DependencyAnalyzer
{
    private readonly Dictionary<(World, ComponentType), TypeHistory> _types = new();
    private readonly List<(World, ComponentType)> _retiredTypes = new();

    internal void Analyze(Submission submission)
    {
        foreach (var access in submission.Accesses)
        {
            if (!access.Read && !access.Write) continue;
            var key = (submission.World, access.Type);
            if (!_types.TryGetValue(key, out var history)) _types.Add(key, history = new());
            history.Prune();

            if (access.Mode == AccessMode.Random)
            {
                foreach (var prior in history.All)
                    if (access.Write || prior.Access.Write) submission.TaskDependencies.Add(prior.Submission.Completion.Computed.Task);
            }
            else
            {
                // Random accesses cover the entire storage, even when their query runs on a disjoint group.
                foreach (var prior in history.All)
                    if (prior.Access.Mode == AccessMode.Random && (access.Write || prior.Access.Write))
                        submission.TaskDependencies.Add(prior.Submission.Completion.Computed.Task);

                foreach (var unit in submission.Units)
                {
                    if (!history.Chunks.TryGetValue(unit.ChunkId, out var frontier)) history.Chunks.Add(unit.ChunkId, frontier = new());
                    if (frontier.Writer is { } writer && !writer.IsCompleted) unit.Dependencies.Add(writer);
                    if (access.Write)
                    {
                        foreach (var reader in frontier.Readers) if (!reader.IsCompleted) unit.Dependencies.Add(reader);
                        frontier.Readers.Clear();
                        frontier.Writer = unit.Finish.Task;
                    }
                    else if (access.Read) frontier.Readers.Add(unit.Finish.Task);
                }
            }
            history.All.Add(new(submission, access));
        }
    }

    internal void PruneCompleted()
    {
        _retiredTypes.Clear();
        foreach (var pair in _types)
        {
            pair.Value.Prune();
            if (pair.Value.All.Count == 0 && pair.Value.Chunks.Count == 0) _retiredTypes.Add(pair.Key);
        }
        foreach (var key in _retiredTypes) _types.Remove(key);
        _retiredTypes.Clear();
    }

    private readonly record struct AccessRecord(Submission Submission, ComponentAccess Access);
    private sealed class ChunkFrontier
    {
        internal Task? Writer;
        internal readonly HashSet<Task> Readers = new();
    }
    private sealed class TypeHistory
    {
        internal readonly Dictionary<long, ChunkFrontier> Chunks = new();
        internal readonly List<AccessRecord> All = new();
        private readonly List<long> _retiredChunks = new();
        internal void Prune()
        {
            All.RemoveAll(static record => record.Submission.Completion.Computed.Task.IsCompleted);
            _retiredChunks.Clear();
            foreach (var pair in Chunks)
            {
                if (pair.Value.Writer?.IsCompleted == true) pair.Value.Writer = null;
                pair.Value.Readers.RemoveWhere(static task => task.IsCompleted);
                if (pair.Value.Writer is null && pair.Value.Readers.Count == 0) _retiredChunks.Add(pair.Key);
            }
            foreach (var key in _retiredChunks) Chunks.Remove(key);
            _retiredChunks.Clear();
        }
    }
}
