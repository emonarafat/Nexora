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
            .WithDescription("Search products with full-text query, filter expressions, facets, sorting, and pagination.")
            .WithTags("Search")
            .Produces<SearchResponse>()
            .ProducesProblem(400)
            .ProducesProblem(429)
            .RequireRateLimiting("SearchLimit");
        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] string q,
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "per_page")] int perPage = SearchConstants.DefaultPageSize,
        [FromQuery] string sort = SearchConstants.SortModes.Relevance,
        [FromQuery(Name = "filter_by")] string? filterBy = null,
        [FromQuery(Name = "facet_by")] string? facetBy = null,
        [FromServices] SearchRequestValidator validator = null!,
        [FromServices] SearchQueryHandler handler = null!,
        CancellationToken ct = default)
    {
        var validation = validator.Validate(new SearchRequest
        {
            Query = q,
            Page = page,
            PerPage = perPage,
            Sort = sort,
            FilterBy = filterBy,
            FacetBy = facetBy
        });

        if (!validation.IsValid)
            return Results.Problem(validation.Error, statusCode: validation.StatusCode);

        var response = await handler.HandleAsync(validation.Request!, ct);
        return Results.Ok(response);
    }
}
