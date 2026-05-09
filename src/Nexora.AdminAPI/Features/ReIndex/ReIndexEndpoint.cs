namespace Nexora.AdminAPI.Features.ReIndex;

public static class ReIndexEndpoints
{
    public static IEndpointRouteBuilder MapReIndexEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/reindex", Trigger)
            .WithSummary("Trigger full re-index")
            .WithTags("Admin")
            .RequireRateLimiting("AdminLimit");
        return app;
    }

    private static async Task<IResult> Trigger(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var baseUrl = configuration["IndexSync:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Results.Problem("IndexSync:BaseUrl is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/internal/reindex");
        using var response = await httpClientFactory.CreateClient("IndexSync").SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("IndexSync re-index trigger failed with status code {StatusCode}", response.StatusCode);
            return Results.StatusCode((int)response.StatusCode);
        }

        logger.LogInformation("Full re-index triggered via Admin API");
        return Results.Accepted("/api/v1/admin/reindex/status",
            new { Status = "Accepted", Message = "Full re-index queued." });
    }
}
