using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Features.Suggest;

[ExcludeFromCodeCoverage]
public static class SuggestEndpoints
{
    public static IEndpointRouteBuilder MapSuggestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/suggest", HandleAsync)
            .WithName("Suggest")
            .WithSummary("Typeahead suggestions")
            .WithDescription("Returns autocomplete suggestions for query prefixes. Frontend callers should debounce requests to 300ms.")
            .WithTags("Suggest")
            .Produces<IReadOnlyList<SuggestionItem>>()
            .ProducesProblem(400);

        app.MapPost("/api/v1/suggest/cache/invalidate", InvalidateAsync)
            .WithName("InvalidateSuggestCache")
            .WithSummary("Invalidate suggest cache")
            .WithDescription("Invalidates suggest cache after product index updates.")
            .WithTags("Suggest")
            .Produces(StatusCodes.Status204NoContent);

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

        var response = await handler.HandleAsync(new SuggestRequest { Query = q, Limit = limit, Category = category }, ct);
        return Results.Ok(response.Suggestions);
    }

    private static async Task<IResult> InvalidateAsync(
        [FromServices] SuggestQueryHandler handler = null!,
        CancellationToken ct = default)
    {
        await handler.InvalidateCacheAsync(ct);
        return Results.NoContent();
    }
}
