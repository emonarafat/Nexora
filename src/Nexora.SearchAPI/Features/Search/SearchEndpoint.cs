using Microsoft.AspNetCore.Mvc;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Features.Search;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/search", HandleAsync)
            .WithName("Search")
            .WithSummary("Full-text product search")
            .WithTags("Search")
            .Produces<SearchResponse>()
            .ProducesProblem(400)
            .ProducesProblem(429);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] string q,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = SearchConstants.DefaultPageSize,
        [FromQuery] string sort = SearchConstants.SortModes.Relevance,
        [FromQuery(Name = "filter_by")] string? filterBy = null,
        [FromQuery(Name = "facet_by")] string? facetBy = null,
        [FromServices] SearchQueryHandler handler = null!,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Results.Problem("Query 'q' is required.", statusCode: 400);
        if (q.Length > SearchConstants.MaxQueryLength)
            return Results.Problem($"Query must be <= {SearchConstants.MaxQueryLength} chars.", statusCode: 400);
        if (perPage is < 1 or > SearchConstants.MaxPageSize)
            return Results.Problem($"per_page must be 1-{SearchConstants.MaxPageSize}.", statusCode: 400);
        if (page > SearchConstants.MaxDeepPaginationPage)
            return Results.Problem($"Pagination beyond page {SearchConstants.MaxDeepPaginationPage} not allowed.", statusCode: 429);

        var response = await handler.HandleAsync(
            new SearchRequest { Query = q, Page = page, PerPage = perPage, Sort = sort, FilterBy = filterBy, FacetBy = facetBy },
            ct);
        return Results.Ok(response);
    }
}
