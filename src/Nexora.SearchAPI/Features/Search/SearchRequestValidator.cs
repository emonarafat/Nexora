using System.Text.RegularExpressions;
using Nexora.SearchAPI.Pipeline;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Features.Search;

public sealed class SearchRequestValidator(
    QuerySanitizer sanitizer,
    SearchFilterExpressionValidator filterExpressionValidator)
{
    public SearchRequestValidationResult Validate(SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return SearchRequestValidationResult.Failure(400, "Query 'q' is required.");

        if (request.Query.Length > SearchConstants.MaxQueryLength)
            return SearchRequestValidationResult.Failure(400, $"Query must be <= {SearchConstants.MaxQueryLength} chars.");

        if (request.Page < 1)
            return SearchRequestValidationResult.Failure(400, "page must be >= 1.");

        if (request.PerPage is < 1 or > SearchConstants.MaxPageSize)
            return SearchRequestValidationResult.Failure(400, $"per_page must be 1-{SearchConstants.MaxPageSize}.");

        if (request.Page > SearchConstants.MaxDeepPaginationPage)
            return SearchRequestValidationResult.Failure(429, $"Pagination beyond page {SearchConstants.MaxDeepPaginationPage} not allowed.");

        var sanitizedQuery = sanitizer.Sanitize(request.Query);
        if (string.IsNullOrWhiteSpace(sanitizedQuery))
            return SearchRequestValidationResult.Failure(400, "Query contains invalid content.");

        var normalizedSort = string.IsNullOrWhiteSpace(request.Sort)
            ? SearchConstants.SortModes.Relevance
            : request.Sort.Trim().ToLowerInvariant();

        if (!IsAllowedSort(normalizedSort))
            return SearchRequestValidationResult.Failure(400, "sort must be one of relevance, price_asc, price_desc, rating, newest.");

        if (!filterExpressionValidator.TryValidate(request.FilterBy, out var normalizedFilterBy, out var filterError))
            return SearchRequestValidationResult.Failure(400, filterError!);

        if (!filterExpressionValidator.TryNormalizeFacets(request.FacetBy, out var normalizedFacetBy, out var facetError))
            return SearchRequestValidationResult.Failure(400, facetError!);

        return SearchRequestValidationResult.Success(request with
        {
            Query = sanitizedQuery,
            Sort = normalizedSort,
            FilterBy = normalizedFilterBy,
            FacetBy = normalizedFacetBy
        });
    }

    private static bool IsAllowedSort(string sort) => sort is
        SearchConstants.SortModes.Relevance or
        SearchConstants.SortModes.PriceAsc or
        SearchConstants.SortModes.PriceDesc or
        SearchConstants.SortModes.Rating or
        SearchConstants.SortModes.Newest;
}

public sealed record SearchRequestValidationResult(SearchRequest? Request, int? StatusCode = null, string? Error = null)
{
    public bool IsValid => StatusCode is null;

    public static SearchRequestValidationResult Success(SearchRequest request) => new(request);

    public static SearchRequestValidationResult Failure(int statusCode, string error) => new(null, statusCode, error);
}

public sealed partial class SearchFilterExpressionValidator
{
    private const int MaxFilterLength = 500;

    private static readonly HashSet<string> AllowedFilterFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "price",
        "brand",
        "category",
        "color",
        "size",
        "rating",
        "stock_status"
    };

    private static readonly HashSet<string> AllowedFacetFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "brand",
        "category",
        "price",
        "color",
        "size",
        "rating",
        "stock_status"
    };

    [GeneratedRegex(@"\bmerchant_id\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MerchantIdPattern();

    [GeneratedRegex(@"(?<field>[a-z_]+)\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FilterFieldPattern();

    [GeneratedRegex(@"(;|--|/\*|\*/|<script|javascript:)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SuspiciousFilterPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultiWhitespacePattern();

    public bool TryValidate(string? filterBy, out string? normalizedFilterBy, out string? error)
    {
        normalizedFilterBy = null;
        error = null;

        if (string.IsNullOrWhiteSpace(filterBy))
            return true;

        var trimmed = filterBy.Trim();
        if (trimmed.Length > MaxFilterLength)
        {
            error = $"filter_by must be <= {MaxFilterLength} chars.";
            return false;
        }

        if (MerchantIdPattern().IsMatch(trimmed))
        {
            error = "filter_by cannot reference merchant_id.";
            return false;
        }

        if (SuspiciousFilterPattern().IsMatch(trimmed) || !HasOnlyAllowedCharacters(trimmed))
        {
            error = "filter_by contains unsupported characters or patterns.";
            return false;
        }

        var fields = FilterFieldPattern().Matches(trimmed)
            .Select(match => match.Groups["field"].Value)
            .ToList();

        if (fields.Count == 0 || fields.Any(field => !AllowedFilterFields.Contains(field)))
        {
            error = "filter_by contains an unsupported field.";
            return false;
        }

        normalizedFilterBy = MultiWhitespacePattern().Replace(trimmed, " ");
        return true;
    }

    public bool TryNormalizeFacets(string? facetBy, out string? normalizedFacetBy, out string? error)
    {
        normalizedFacetBy = null;
        error = null;

        if (string.IsNullOrWhiteSpace(facetBy))
            return true;

        var facets = facetBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(facet => facet.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (facets.Count == 0)
            return true;

        if (facets.Any(facet => facet == "merchant_id" || !AllowedFacetFields.Contains(facet)))
        {
            error = "facet_by contains an unsupported field.";
            return false;
        }

        normalizedFacetBy = string.Join(",", facets);
        return true;
    }

    private static bool HasOnlyAllowedCharacters(string filterBy)
        => filterBy.All(ch => char.IsLetterOrDigit(ch)
            || ch is '_' or ':' or '=' or '[' or ']' or '.' or ',' or ' ' or '-' or '(' or ')' or '&' or '|' or '>' or '<');
}
