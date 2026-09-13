using System.Collections.ObjectModel;

namespace Sugoi.Data;

public sealed partial class Query
{
    private ReadOnlyCollection<QueryTerm> _terms = Array.AsReadOnly(Array.Empty<QueryTerm>());
    /// <summary>DSL access terms, including filter-only entries. Empty for queries built directly from a description.</summary>
    public ReadOnlyCollection<QueryTerm> Terms => _terms;
    internal void SetParsedTerms(IEnumerable<QueryTerm> terms) => _terms = Array.AsReadOnly(terms.ToArray());
}
