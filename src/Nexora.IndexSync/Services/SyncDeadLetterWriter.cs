using System.Text.Json;
using Microsoft.Data.SqlClient;
using Nexora.IndexSync.Models;

namespace Nexora.IndexSync.Services;

public sealed class SyncDeadLetterWriter(IConfiguration config, ILogger<SyncDeadLetterWriter> logger)
{
    public async Task WriteAsync(IReadOnlyList<CdcChange> changes, string errorMessage, CancellationToken ct)
    {
        if (changes.Count == 0)
            return;

        try
        {
            await using var conn = new SqlConnection(
                config.GetConnectionString("MSSQL")
                ?? throw new InvalidOperationException("MSSQL connection string not configured"));
            await conn.OpenAsync(ct);

            const string sql = """
                IF OBJECT_ID('dbo.sync_dead_letter', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.sync_dead_letter (
                        id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        product_id INT NOT NULL,
                        operation NVARCHAR(32) NOT NULL,
                        change_source NVARCHAR(32) NOT NULL,
                        payload_json NVARCHAR(MAX) NOT NULL,
                        error_message NVARCHAR(4000) NOT NULL,
                        created_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END;

                INSERT INTO dbo.sync_dead_letter (
                    product_id,
                    operation,
                    change_source,
                    payload_json,
                    error_message
                )
                VALUES (
                    @productId,
                    @operation,
                    @changeSource,
                    @payloadJson,
                    @errorMessage
                );
                """;

            foreach (var change in changes)
            {
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@productId", change.ProductId);
                cmd.Parameters.AddWithValue("@operation", change.Operation);
                cmd.Parameters.AddWithValue("@changeSource", change.ChangeSource);
                cmd.Parameters.AddWithValue("@payloadJson", JsonSerializer.Serialize(change));
                cmd.Parameters.AddWithValue("@errorMessage", errorMessage[..Math.Min(errorMessage.Length, 4000)]);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to write {Count} change(s) to sync_dead_letter", changes.Count);
        }
    }
}
