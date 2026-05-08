using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Nexora.IndexSync.Models;

namespace Nexora.IndexSync.Services;

public sealed class SyncDeadLetterWriter(IConfiguration config, ILogger<SyncDeadLetterWriter> logger)
{
    private const int MaxErrorMessageLength = 4000;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _tableInitialized;

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
            await EnsureTableAsync(conn, ct);

            var sql = new StringBuilder("""
                INSERT INTO dbo.sync_dead_letter (
                    product_id,
                    operation,
                    change_source,
                    payload_json,
                    error_message
                )
                VALUES
                """);

            await using var cmd = new SqlCommand();
            cmd.Connection = conn;

            for (var index = 0; index < changes.Count; index++)
            {
                if (index > 0)
                    sql.AppendLine(",");

                sql.Append($"(@productId{index}, @operation{index}, @changeSource{index}, @payloadJson{index}, @errorMessage{index})");

                var change = changes[index];
                cmd.Parameters.AddWithValue($"@productId{index}", change.ProductId);
                cmd.Parameters.AddWithValue($"@operation{index}", change.Operation);
                cmd.Parameters.AddWithValue($"@changeSource{index}", change.ChangeSource);
                cmd.Parameters.AddWithValue($"@payloadJson{index}", JsonSerializer.Serialize(change));
                cmd.Parameters.AddWithValue($"@errorMessage{index}", errorMessage[..Math.Min(errorMessage.Length, MaxErrorMessageLength)]);
            }

            cmd.CommandText = sql.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to write {Count} change(s) to sync_dead_letter", changes.Count);
        }
    }

    private async Task EnsureTableAsync(SqlConnection conn, CancellationToken ct)
    {
        if (_tableInitialized)
            return;

        await _initializationLock.WaitAsync(ct);
        try
        {
            if (_tableInitialized)
                return;

            var sql = $"""
                IF OBJECT_ID('dbo.sync_dead_letter', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.sync_dead_letter (
                        id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        product_id INT NOT NULL,
                        operation NVARCHAR(32) NOT NULL,
                        change_source NVARCHAR(32) NOT NULL,
                        payload_json NVARCHAR(MAX) NOT NULL,
                        error_message NVARCHAR({MaxErrorMessageLength}) NOT NULL,
                        created_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                END;
                """;

            await using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
            _tableInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
