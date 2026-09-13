using System.Collections.ObjectModel;

namespace Sugoi.Data;

public enum QueryAccess : byte { Read, ReadWrite, Write, AtomicWrite, Presence }
public enum QuerySequence : byte { Sequential, Parallel, Unsequenced }
public enum QuerySelection : byte { Required, Optional, Excluded }

/// <summary>A DSL term retains access intent separately from the query's matching filter.</summary>
public readonly record struct QueryTerm(ComponentType Type, string Name, QueryAccess Access,
    QuerySequence Sequence, QuerySelection Selection, bool Shared, int Phase)
{
    public bool IsFilterOnly => Access == QueryAccess.Presence || Selection == QuerySelection.Excluded;
    public bool Reads => !IsFilterOnly && (Access == QueryAccess.Read || Access == QueryAccess.ReadWrite);
    public bool Writes => !IsFilterOnly && (Access == QueryAccess.ReadWrite || Access == QueryAccess.Write || Access == QueryAccess.AtomicWrite);
    public bool Atomic => Access == QueryAccess.AtomicWrite;
}

public readonly record struct QueryParseError(int Position, string Message)
{
    public override string ToString() => $"{Message} (character {Position}).";
}

public sealed class QueryParseResult
{
    internal QueryParseResult(QueryDescription description, QueryTerm[] terms)
    {
        Description = description;
        Terms = Array.AsReadOnly(terms);
    }
    public QueryDescription Description { get; }
    public ReadOnlyCollection<QueryTerm> Terms { get; }
}

/// <summary>
/// Parses the active sugoi DSL: [in/inout/out/atomic/has], &lt;seq/par/unseq&gt;, $shared, ?optional, !excluded, type or alias.
/// Names support C# namespaces and digit zero. Obsolete rand, | selectors, and quote-based phases are not accepted.
/// </summary>
public static class QueryParser
{
    public static bool TryParse(World world, string? text, out QueryParseResult? result, out QueryParseError error)
    {
        ArgumentNullException.ThrowIfNull(world);
        using var usage = world.AcquireUsage();
        result = null;
        error = default;
        if (text is null) { error = new(0, "Query text cannot be null"); return false; }

        // The reference removes whitespace before parsing, even between a modifier and its type.
        // Retain an original-text map so diagnostics still point into the caller's string.
        var characters = new char[text.Length];
        var positions = new int[text.Length];
        var length = 0;
        for (var i = 0; i < text.Length; i++)
            if (!char.IsWhiteSpace(text[i])) { characters[length] = text[i]; positions[length++] = i; }
        var compact = new string(characters, 0, length);
        var description = new QueryDescription();
        var terms = new List<QueryTerm>();
        if (length == 0) { result = new(description, []); return true; }
        var begin = 0;
        while (begin < length)
        {
            var end = compact.IndexOf(',', begin);
            if (end < 0) end = length;
            if (!TryReadTerm(world, compact.AsSpan(begin, end - begin), out var term, out var localPosition, out var message))
            {
                var index = begin + localPosition;
                error = new(index < length ? positions[index] : text.Length, message);
                return false;
            }
            terms.Add(term);
            if (term.Shared)
            {
                if (term.Selection == QuerySelection.Required) description.WithShared(term.Type);
                else if (term.Selection == QuerySelection.Excluded) description.WithoutShared(term.Type);
            }
            else
            {
                if (term.Selection == QuerySelection.Required) description.WithAll(term.Type);
                else if (term.Selection == QuerySelection.Excluded) description.Without(term.Type);
            }
            if (term.Writes && term.Phase >= 0) description.WritePhase(term.Type, term.Phase);
            if (end == length) break;
            begin = end + 1;
            if (begin == length) { error = new(text.Length, "Expected a query term after ','"); return false; }
        }
        result = new(description, terms.ToArray());
        return true;
    }

    private static bool TryReadTerm(World world, ReadOnlySpan<char> text, out QueryTerm term, out int position, out string message)
    {
        term = default;
        position = 0;
        message = "";
        if (text.IsEmpty) { message = "Empty query term"; return false; }
        var access = QueryAccess.Read;
        var sequence = QuerySequence.Parallel;
        var selection = QuerySelection.Required;
        var phase = -1;
        var shared = false;
        var index = 0;

        if (text[index] == '[')
        {
            var close = text.IndexOf(']');
            if (close < 0) { message = "Access modifier '[' needs a closing ']'"; return false; }
            switch (text.Slice(1, close - 1))
            {
                case "in": access = QueryAccess.Read; break;
                case "inout": access = QueryAccess.ReadWrite; break;
                case "out": access = QueryAccess.Write; phase = 0; break;
                case "atomic": access = QueryAccess.AtomicWrite; break;
                case "has": access = QueryAccess.Presence; break;
                default: position = 1; message = "Unknown access modifier; expected in, inout, out, atomic, or has"; return false;
            }
            index = close + 1;
        }
        if (index < text.Length && text[index] == '<')
        {
            var relativeClose = text[index..].IndexOf('>');
            if (relativeClose < 0) { position = index; message = "Sequence modifier '<' needs a closing '>'"; return false; }
            var close = index + relativeClose;
            switch (text.Slice(index + 1, close - index - 1))
            {
                case "seq": sequence = QuerySequence.Sequential; break;
                case "par": sequence = QuerySequence.Parallel; break;
                case "unseq": sequence = QuerySequence.Unsequenced; selection = QuerySelection.Optional; break;
                default: position = index + 1; message = "Unknown sequence modifier; expected seq, par, or unseq"; return false;
            }
            index = close + 1;
        }
        if (index < text.Length && text[index] == '$')
        {
            if (access != QueryAccess.Read && access != QueryAccess.Presence)
            { position = index; message = "Shared components are read-only"; return false; }
            shared = true;
            sequence = QuerySequence.Sequential;
            index++;
        }
        if (index < text.Length && (text[index] == '?' || text[index] == '!'))
        {
            if (sequence == QuerySequence.Unsequenced && text[index] != '?')
            { position = index; message = "An unsequenced component must be optional"; return false; }
            selection = text[index] == '?' ? QuerySelection.Optional : QuerySelection.Excluded;
            index++;
        }
        if (index >= text.Length || !(char.IsLetter(text[index]) || text[index] == '_'))
        { position = index; message = "Expected a component type or alias name"; return false; }
        var nameBegin = index;
        while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '_' or ':' or '.')) index++;
        if (index != text.Length)
        { position = index; message = "Unexpected character after component name"; return false; }
        var name = text[nameBegin..].ToString();
        ComponentType type;
        if (world.TryAlias(name, out var alias))
        {
            if (access == QueryAccess.Write)
            { position = nameBegin; message = "[out] always uses phase zero and cannot use a phase alias"; return false; }
            type = alias.Type;
            phase = alias.Phase;
        }
        else
        {
            try { type = world.Types.Get(name).Type; }
            catch (KeyNotFoundException)
            { position = nameBegin; message = $"Unknown component type or alias '{name}'"; return false; }
        }
        term = new(type, name, access, sequence, selection, shared, phase);
        return true;
    }
}
