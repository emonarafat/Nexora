using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Typesense;

namespace Nexora.SearchAPI.Pipeline;

/// <summary>
/// Produces final Typesense search parameters from processed query and request options.
/// Combines query processing results with filters, facets, and sort parameters.
/// </summary>
public sealed class QueryStructurer
{
    private const string SearchFields = "title,brand,description,category,sku";

    /// <summary>
    /// Builds Typesense search parameters from processed query and search request.
    /// </summary>
    /// <param name="processed">The processed query from the pipeline</param>
    /// <param name="request">The original search request with pagination and filtering</param>
    /// <returns>Configured SearchParameters for Typesense</returns>
    public SearchParameters BuildSearchParameters(ProcessedQuery processed, SearchRequest request)
    {
        var sortBy = MapSortMode(request.Sort);

        var searchParams = new SearchParameters(
            processed.NormalizedQuery,
            SearchFields)
        {
            FilterBy = BuildFilterBy(request.FilterBy, processed.IntentFilters),
            FacetBy = request.FacetBy,
            SortBy = sortBy,
            Page = request.Page,
            PerPage = request.PerPage,
            NumberOfTypos = DetermineTypoTolerance(processed.NormalizedQuery)
        };

        return searchParams;
    }

    /// <summary>
    /// Determines typo tolerance based on query length.
    /// Uses 1 typo for queries ≤8 chars, 2 for longer queries.
    /// </summary>
    private static string DetermineTypoTolerance(string query)
    {
        return query.Length <= 8 ? "1" : "2";
    }

    /// <summary>
    /// Maps sort mode from request to Typesense sort parameter.
    /// </summary>
    private static string MapSortMode(string sortMode) => sortMode switch
    {
        SearchConstants.SortModes.PriceAsc => "price:asc",
        SearchConstants.SortModes.PriceDesc => "price:desc",
        SearchConstants.SortModes.Rating => "rating:desc,rating_count:desc",
        SearchConstants.SortModes.Newest => "created_at:desc",
        _ => "_text_match:desc,popularity_score:desc"
    };

    /// <summary>
    /// Combines client-provided filters with intent-based filters from classification.
    /// </summary>
    private static string? BuildFilterBy(string? clientFilter, Dictionary<string, string>? intentFilters)
    {
        if (intentFilters == null || intentFilters.Count == 0)
            return clientFilter;

        var intentFilterStr = string.Join(" && ",
            intentFilters.Select(kvp => $"{kvp.Key}:={kvp.Value}"));

        if (string.IsNullOrWhiteSpace(clientFilter))
            return intentFilterStr;

        return $"{clientFilter} && {intentFilterStr}";
    }
}
