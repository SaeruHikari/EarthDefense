using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace Earthward.Domain;

public static partial class DeepTechnology
{
    public const int SuccessorMaxRank = 10000;
    public static readonly string[] ChainBranches = { "K", "M", "L", "I", "D", "C" };
    private static readonly string[] ChainAttributes = { "\u52a8\u80fd\u4f24\u5bb3", "\u5bfc\u5f39\u4f24\u5bb3", "\u6fc0\u5149\u4f24\u5bb3", "\u79d1\u7814\u4ea7\u91cf", "\u6218\u673a\u8010\u4e45", "\u5de1\u822a\u901f\u5ea6" };
    public static bool TryParseSuccessor(string id, out string branch, out int rank)
    {
        branch = ""; rank = 0;
        if (id.Length != 8 || id[1] != '_' || id[2] != 'R' || !ChainBranches.Contains(id[..1]) || !int.TryParse(id.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out rank) || rank < 1 || rank > SuccessorMaxRank)
            return false;
        branch = id[..1];
        return id == SuccessorId(branch, rank);
    }
    public static string SuccessorId(string branch, int rank) => ChainBranches.Contains(branch) && rank >= 1 && rank <= SuccessorMaxRank ? branch + "_R" + rank.ToString("D5", CultureInfo.InvariantCulture) : "";
    public static DataMap SuccessorCost(string branch, int rank)
    {
        if (!ChainBranches.Contains(branch) || rank < 1 || rank > SuccessorMaxRank) return new();
        return new()
        {
            ["science"] = Math.Min(DefenseState.ResourceLimit, Math.Ceiling(1200 * Math.Pow(1.22, rank - 1))),
            ["alien_points"] = (long)Math.Min(DefenseState.MaxExactInteger, Math.Ceiling(12 * Math.Pow(1.38, rank - 1)))
        };
    }
    private static DataMap SuccessorDefinition(string branch, int rank)
    {
        int index = Array.IndexOf(ChainBranches, branch);
        string parent = rank == 1 ? branch + "_G2" : SuccessorId(branch, rank - 1);
        return new()
        {
            ["id"] = SuccessorId(branch, rank), ["name"] = ChainAttributes[index] + " \u8fdb\u9636 " + rank,
            ["branch"] = branch, ["size"] = "medium", ["importance"] = 2L, ["alien"] = true,
            ["max"] = 1L, ["ranks"] = 1L, ["tier"] = 6L,
            ["effects"] = new List<object?> { ChainAttributes[index] + " +3%" },
            ["values"] = new DataMap { [SuccessorEffectKeys[index]] = .03 },
            ["cost"] = SuccessorCost(branch, rank), ["requires"] = new List<object?> { parent },
            ["unlock"] = new DataMap(), ["is_successor"] = true,
            ["chain_rank"] = rank, ["chain_branch"] = branch,
            ["chain_max_rank"] = SuccessorMaxRank
        };
    }
}

public sealed partial class DefenseState
{
    public long PurchasedResearchCount => DeepResearch.Count + _successorLevels.Values.Sum(value => DataMap.Integer(value));
    public long SuccessorCount(string branch) => _successorLevels.L(branch);
    private IEnumerable<DataMap> SuccessorGraphNodes(string focusId)
    {
        bool focused = DeepTechnology.TryParseSuccessor(focusId, out string focusBranch, out int focusRank);
        foreach (string branch in DeepTechnology.ChainBranches)
        {
            if (!HasResearch(branch + "_G2")) continue;
            int completed = (int)_successorLevels.L(branch);
            int latest = Math.Min(DeepTechnology.SuccessorMaxRank, completed + 1);
            int limit = Math.Min(DeepTechnology.SuccessorMaxRank, completed + 3);
            int center = focused && branch == focusBranch ? Math.Clamp(focusRank, 1, limit) : latest;
            int start = Math.Max(1, completed - 2), end = limit;
            if (focused && branch == focusBranch && center != latest)
            {
                start = Math.Clamp(center - 3, 1, Math.Max(1, limit - 5));
                end = Math.Min(limit, start + 5);
            }
            for (int rank = start; rank <= end; rank++)
            {
                var row = DeepTechnology.Definition(DeepTechnology.SuccessorId(branch, rank));
                int index = Array.IndexOf(DeepTechnology.ChainBranches, branch);
                double angle = Math.PI / 3 * index, radial = 1260 + (rank - start) * 92;
                row["draw_position"] = new List<object?> { Math.Sin(angle) * radial, -Math.Cos(angle) * radial };
                row["visible"] = true;
                row["completed_chain_count"] = completed;
                row["chain_latest_id"] = DeepTechnology.SuccessorId(branch, latest);
                row["chain_window_start"] = start; row["chain_window_end"] = end;
                row["prev_page"] = start > 1 ? DeepTechnology.SuccessorId(branch, Math.Max(1, center - 3)) : "";
                row["next_page"] = end < limit ? DeepTechnology.SuccessorId(branch, Math.Min(limit, center + 3)) : "";
                yield return row;
            }
        }
    }
}
