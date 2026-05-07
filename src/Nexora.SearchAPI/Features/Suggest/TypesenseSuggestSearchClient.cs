using Nexora.SearchAPI.Infrastructure;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Typesense;

namespace Nexora.SearchAPI.Features.Suggest;

public sealed class TypesenseSuggestSearchClient(TypesenseClientFactory clientFactory) : ISuggestSearchClient
{
    public async Task<IReadOnlyList<SuggestionItem>> SearchAsync(string prefix, int limit, string? category, CancellationToken ct)
    {
        var searchParams = BuildPrefixSearchQuery(prefix, limit, category);
        var result = await clientFactory.CreateClient()
            .Search<ProductDocument>(SearchConstants.ProductsCollection, searchParams, ct);

        return result.Hits?.Select(hit => new SuggestionItem
        {
            Text = hit.Document.Title,
            Category = hit.Document.Category,
            PopularityScore = hit.Document.PopularityScore
        }).ToList() ?? [];
    }

    private static SearchParameters BuildPrefixSearchQuery(string prefix, int limit, string? category)
        => new(prefix, "title,brand")
        {
            FilterBy = string.IsNullOrWhiteSpace(category) ? null : $"category:={category}",
            PerPage = limit,
            NumberOfTypos = "1",
            Prefix = true,
            SortBy = "popularity_score:desc,_text_match:desc"
        };
}
