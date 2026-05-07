using Microsoft.Data.SqlClient;
using Nexora.IndexSync.Models;

namespace Nexora.IndexSync.Services;

public sealed class CdcChangeReader(IConfiguration config, ILogger<CdcChangeReader> logger)
{
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

            const string sql = """
                SELECT p.__$operation, p.product_id, p.product_name, p.brand_name, p.product_sku,
                       p.product_description, p.category_name, p.category_hierarchy,
                       p.unit_price, p.currency_code, p.color_variants, p.size_variants,
                       p.avg_rating, p.rating_count, p.is_featured_flag, p.is_active_flag,
                       p.merchant_id, p.created_date, p.modified_date,
                       COALESCE(s.stock_status_code,'OUT_OF_STOCK') AS stock_status,
                       COALESCE(s.qty_on_hand,0) AS stock_quantity
                FROM cdc.fn_cdc_get_all_changes_dbo_products(
                    sys.fn_cdc_map_time_to_lsn('smallest greater than or equal', @from),
                    sys.fn_cdc_map_time_to_lsn('largest less than or equal', @to),
                    'all') p
                LEFT JOIN stock s ON s.product_id = p.product_id
                WHERE p.__$operation IN (1,2,4)
                ORDER BY p.__$seqval
                """;

            await using var cmd = new SqlCommand(sql, conn);
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
            const string sql = """
                SELECT 4 AS __$operation, product_id, product_name, brand_name, product_sku,
                       product_description, category_name, category_hierarchy,
                       unit_price, currency_code, color_variants, size_variants,
                       avg_rating, rating_count, is_featured_flag, is_active_flag,
                       merchant_id, created_date, modified_date,
                       stock_status, stock_quantity
                FROM vw_search_product_flat
                ORDER BY product_id
                OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY
                """;
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@offset", page * size);
            cmd.Parameters.AddWithValue("@size", size);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) changes.Add(Map(r));
        }
        catch (Exception ex) { logger.LogError(ex, "Full page read failed (page {Page})", page); }
        return changes;
    }

    private static CdcChange Map(SqlDataReader r) => new()
    {
        Operation = r.GetInt32(0) switch { 1 => "DELETE", 2 => "INSERT", _ => "UPDATE" },
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
