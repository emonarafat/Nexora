namespace Nexora.AdminAPI.Features.ReIndex;

public static class ReIndexEndpoints
{
    public static IEndpointRouteBuilder MapReIndexEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/admin/reindex", Trigger)
            .WithSummary("Trigger full re-index")
            .WithTags("Admin");
        return app;
    }

    private static IResult Trigger(ILogger<Program> logger)
    {
        logger.LogInformation("Full re-index triggered via Admin API");
        return Results.Accepted("/api/v1/admin/reindex/status",
            new { Status = "Accepted", Message = "Full re-index queued." });
    }
}
