using Microsoft.AspNetCore.Mvc;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Features.Suggest;

public static class SuggestEndpoints
{
    public static IEndpointRouteBuilder MapSuggestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/suggest", HandleAsync)
            .WithName("Suggest")
            .WithSummary("Typeahead suggestions")
            .WithTags("Suggest")
            .Produces<SuggestResponse>()
            .ProducesProblem(400);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromQuery] string q,
        [FromQuery] int limit = SearchConstants.DefaultSuggestResults,
        [FromQuery] string? category = null,
        [FromServices] SuggestQueryHandler handler = null!,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Results.Problem("Query must be >= 2 characters.", statusCode: 400);
        if (limit is < 1 or > SearchConstants.MaxSuggestResults)
            return Results.Problem($"limit must be 1-{SearchConstants.MaxSuggestResults}.", statusCode: 400);

        return Results.Ok(await handler.HandleAsync(
            new SuggestRequest { Query = q, Limit = limit, Category = category }, ct));
    }
}
