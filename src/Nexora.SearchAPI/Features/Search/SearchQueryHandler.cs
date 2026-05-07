using System.Diagnostics;
using Nexora.SearchAPI.Infrastructure;
using Nexora.SearchAPI.Pipeline;
using Nexora.SearchAPI.Ranking;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Typesense;

namespace Nexora.SearchAPI.Features.Search;

public sealed class SearchQueryHandler(
    TypesenseClientFactory clientFactory,
    QueryPipeline pipeline,
    RankingEngine ranking,
    IRabbitMqPublisher publisher,
    ILogger<SearchQueryHandler> logger)
{
    public async Task<SearchResponse> HandleAsync(SearchRequest req, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "Executing search query {Query} page {Page} perPage {PerPage} sort {Sort}",
            req.Query,
            req.Page,
            req.PerPage,
            req.Sort);

        var processed = await pipeline.ProcessAsync(req.Query, ct);

        var sortBy = req.Sort switch
        {
            SearchConstants.SortModes.PriceAsc => "price:asc",
            SearchConstants.SortModes.PriceDesc => "price:desc",
            SearchConstants.SortModes.Rating => "rating:desc,rating_count:desc",
            SearchConstants.SortModes.Newest => "created_at:desc",
            _ => "_text_match:desc,popularity_score:desc"
        };

        var client = clientFactory.CreateClient();

        var searchParams = new SearchParameters(
            processed.NormalizedQuery,
            "title,brand,description,category,sku")
        {
            FilterBy = req.FilterBy,
            FacetBy = req.FacetBy,
            SortBy = sortBy,
            Page = req.Page,
            PerPage = req.PerPage,
            NumberOfTypos = "2"
        };

        SearchResult<ProductDocument> result;
        try
        {
            result = await client.Search<ProductDocument>(SearchConstants.ProductsCollection, searchParams, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Typesense search failed");
            return new SearchResponse { Page = req.Page, PerPage = req.PerPage, LatencyMs = sw.Elapsed.TotalMilliseconds };
        }

        var products = result.Hits?.Select(hit =>
        {
            var doc = hit.Document;
            var rawScore = double.TryParse(hit.TextMatchInfo?.Score, out var s) ? s : 0.0;
            var score = ranking.ComputeScore(doc, rawScore);
            return new ProductResult
            {
                Id = doc.Id,
                Title = doc.Title,
                Brand = doc.Brand,
                Sku = doc.Sku,
                Description = doc.Description,
                Category = doc.Category,
                Price = (decimal)doc.Price,
                Currency = doc.Currency,
                Rating = doc.Rating,
                RatingCount = doc.RatingCount,
                StockStatus = doc.StockStatus,
                IsFeatured = doc.IsFeatured,
                FinalScore = score
            };
        }).ToList() ?? [];

        var facets = new Dictionary<string, List<FacetValue>>();
        if (result.FacetCounts != null)
        {
            foreach (var fc in result.FacetCounts)
            {
                var values = fc.Counts?
                    .Where(c => c.Count > 0 && !string.IsNullOrWhiteSpace(c.Value))
                    .Select(c => new FacetValue
                    {
                        Value = c.Value,
                        Count = c.Count
                    })
                    .ToList() ?? [];

                if (!string.IsNullOrWhiteSpace(fc.FieldName) && values.Count > 0)
                {
                    facets[fc.FieldName] = values;
                }
            }
        }

        sw.Stop();

        _ = publisher.PublishAsync("search.executed", new
        {
            Query = req.Query,
            Found = result.Found,
            LatencyMs = sw.Elapsed.TotalMilliseconds
        });

        return new SearchResponse
        {
            Results = products,
            TotalCount = result.Found,
            Page = req.Page,
            PerPage = req.PerPage,
            TotalPages = req.PerPage > 0 ? (int)Math.Ceiling((double)result.Found / req.PerPage) : 0,
            Facets = facets,
            CorrectedQuery = processed.CorrectedQuery,
            LatencyMs = sw.Elapsed.TotalMilliseconds
        };
    }
}
