namespace Sugoi.Data;

public sealed partial class World
{
    /// <summary>Creates a query from the active DSL. Parse failure never registers a partial query.</summary>
    public bool TryCreateQuery(string? text, out Query? query, out QueryParseError error)
    {
        query = null;
        if (!QueryParser.TryParse(this, text, out var parsed, out error)) return false;
        query = CreateQuery(parsed!.Description);
        query.SetParsedTerms(parsed.Terms);
        return true;
    }

    public Query CreateQuery(string text)
    {
        if (!TryCreateQuery(text, out var query, out var error)) throw new FormatException(error.ToString());
        return query!;
    }
}
