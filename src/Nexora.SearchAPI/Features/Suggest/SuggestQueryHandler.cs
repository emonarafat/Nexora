using System.Diagnostics;
using Nexora.SearchAPI.Infrastructure;
using Nexora.SearchAPI.Pipeline;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Typesense;

namespace Nexora.SearchAPI.Features.Suggest;

public sealed class SuggestQueryHandler(
    TypesenseClientFactory clientFactory,
    QuerySanitizer sanitizer,
    IValkeyCache cache,
    ILogger<SuggestQueryHandler> logger)
{
    public async Task<SuggestResponse> HandleAsync(SuggestRequest req, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var sanitized = sanitizer.Sanitize(req.Query);

        var client = clientFactory.CreateClient();

        var filterBy = req.Category is not null ? $"category:={req.Category}" : null;

        var searchParams = new SearchParameters(sanitized, "title,brand")
        {
            FilterBy = filterBy,
            PerPage = req.Limit,
            NumberOfTypos = "1",
            Prefix = true
        };

        List<SuggestionItem> suggestions;
        try
        {
            var result = await client.Search<ProductDocument>(SearchConstants.ProductsCollection, searchParams, ct);
            suggestions = result.Hits?.Select(hit => new SuggestionItem
            {
                Text = hit.Document.Title,
                Category = hit.Document.Category,
                PopularityScore = hit.Document.PopularityScore
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Typesense suggest failed");
            suggestions = [];
        }

        sw.Stop();
        return new SuggestResponse
        {
            Suggestions = suggestions,
            LatencyMs = sw.Elapsed.TotalMilliseconds
        };
    }
}
