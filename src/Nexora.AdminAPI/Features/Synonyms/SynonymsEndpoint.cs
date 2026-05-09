using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace Nexora.AdminAPI.Features.Synonyms;

[ExcludeFromCodeCoverage]
public static class SynonymEndpoints
{
    public static IEndpointRouteBuilder MapSynonymEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/admin/synonyms").WithTags("Synonyms");
        g.MapGet("/", GetAsync).WithSummary("List synonyms");
        g.MapPost("/", CreateAsync).WithSummary("Create synonym mapping");
        g.MapDelete("/{term}", DeleteAsync).WithSummary("Delete synonym mapping");
        return app;
    }

    private static async Task<IResult> GetAsync([FromServices] NpgsqlDataSource db, CancellationToken ct)
    {
        var list = new List<SynonymEntry>();
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT term, synonyms, is_active, created_at FROM search_synonyms ORDER BY term";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new(r.GetString(0), r.GetFieldValue<string[]>(1), r.GetBoolean(2), r.GetDateTime(3)));
        return Results.Ok(list);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateSynonymRequest req, [FromServices] NpgsqlDataSource db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Term)) return Results.Problem("Term required.", statusCode: 400);
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO search_synonyms (term, synonyms, is_active, created_at)
            VALUES (@term, @synonyms, true, NOW())
            ON CONFLICT (term) DO UPDATE SET synonyms = @synonyms, is_active = true
            """;
        cmd.Parameters.AddWithValue("term", req.Term.ToLowerInvariant());
        cmd.Parameters.AddWithValue("synonyms", req.Synonyms);
        await cmd.ExecuteNonQueryAsync(ct);
        return Results.Created($"/api/v1/admin/synonyms/{req.Term}", null);
    }

    private static async Task<IResult> DeleteAsync(
        string term, [FromServices] NpgsqlDataSource db, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE search_synonyms SET is_active = false WHERE term = @term";
        cmd.Parameters.AddWithValue("term", term.ToLowerInvariant());
        await cmd.ExecuteNonQueryAsync(ct);
        return Results.NoContent();
    }
}

[ExcludeFromCodeCoverage]
public record SynonymEntry(string Term, string[] Synonyms, bool IsActive, DateTime CreatedAt);
[ExcludeFromCodeCoverage]
public record CreateSynonymRequest(string Term, string[] Synonyms);
