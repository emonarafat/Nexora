using Microsoft.Data.SqlClient;
using Nexora.IndexSync.Models;
using Nexora.IndexSync.Options;
using Microsoft.Extensions.Options;

namespace Nexora.IndexSync.Services;

public sealed class CdcChangeReader(
    IConfiguration config,
    CdcQueryBuilder queryBuilder,
    IOptions<IndexSyncOptions> options,
    ILogger<CdcChangeReader> logger)
{
    private readonly int _fullReindexPageSize = Math.Max(1, options.Value.FullReindexPageSize);
    private DateTimeOffset _lastPoll = DateTimeOffset.UtcNow.AddSeconds(-30);

    public async Task<IReadOnlyList<CdcChange>> GetChangesAsync(CancellationToken ct)
    {
        var from = _lastPoll;
        var to = DateTimeOffset.UtcNow;
        var changes = new List<CdcChange>();

        try
        {
            await using var conn = new SqlConnection(
                config.GetConnectionString("MSSQL")
                ?? throw new InvalidOperationException("MSSQL connection string not configured"));
            await conn.OpenAsync(ct);

            await using var cmd = new SqlCommand(queryBuilder.BuildChangesQuery(), conn);
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) changes.Add(Map(r));
            _lastPoll = to;
        }
        catch (Exception ex) { logger.LogError(ex, "CDC read failed"); }

        return changes;
    }

    public async Task<IReadOnlyList<CdcChange>> GetFullPageAsync(int page, int size, CancellationToken ct)
    {
        var changes = new List<CdcChange>();
        try
        {
            await using var conn = new SqlConnection(
                config.GetConnectionString("MSSQL")
                ?? throw new InvalidOperationException("MSSQL connection string not configured"));
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(queryBuilder.BuildFullReindexQuery(), conn);
            var pageSize = size > 0 ? size : _fullReindexPageSize;
            cmd.Parameters.AddWithValue("@offset", page * pageSize);
            cmd.Parameters.AddWithValue("@size", pageSize);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) changes.Add(Map(r));
        }
        catch (Exception ex) { logger.LogError(ex, "Full page read failed (page {Page})", page); }
        return changes;
    }

    private static CdcChange Map(SqlDataReader r) => new()
    {
        Operation = r.GetInt32(0) switch { 1 => "DELETE", 2 => "INSERT", _ => "UPDATE" },
        ChangeSource = S(r, 21) ?? "product",
        ChangeTimestamp = r.IsDBNull(22) ? DateTimeOffset.UtcNow : r.GetDateTimeOffset(22),
        ProductId = r.GetInt32(1),
        ProductName = S(r, 2), BrandName = S(r, 3), ProductSku = S(r, 4),
        ProductDescription = S(r, 5), CategoryName = S(r, 6), CategoryHierarchy = S(r, 7),
        UnitPrice = r.IsDBNull(8) ? 0f : (float)r.GetDecimal(8),
        CurrencyCode = S(r, 9) ?? "USD",
        ColorVariants = S(r, 10), SizeVariants = S(r, 11),
        AvgRating = r.IsDBNull(12) ? 0f : (float)r.GetDecimal(12),
        RatingCount = r.IsDBNull(13) ? 0 : r.GetInt32(13),
        IsFeaturedFlag = !r.IsDBNull(14) && r.GetBoolean(14),
        IsActiveFlag = r.IsDBNull(15) || r.GetBoolean(15),
        MerchantId = S(r, 16) ?? string.Empty,
        CreatedDate = r.IsDBNull(17) ? DateTimeOffset.UtcNow : r.GetDateTimeOffset(17),
        ModifiedDate = r.IsDBNull(18) ? DateTimeOffset.UtcNow : r.GetDateTimeOffset(18),
        StockStatus = S(r, 19), StockQuantity = r.IsDBNull(20) ? 0 : r.GetInt32(20)
    };

    private static string? S(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
