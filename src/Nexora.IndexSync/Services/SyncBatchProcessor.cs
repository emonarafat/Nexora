using System.Diagnostics.CodeAnalysis;
using Nexora.IndexSync.Models;

namespace Nexora.IndexSync.Services;

[ExcludeFromCodeCoverage]
public sealed class SyncBatchProcessor(
    FieldMapper mapper,
    TypesenseUpsertClient upsertClient,
    SearchApiSuggestCacheInvalidator cacheInvalidator,
    SyncRetryPolicy retryPolicy,
    SyncDeadLetterWriter deadLetterWriter,
    IndexSyncMetrics metrics,
    ILogger<SyncBatchProcessor> logger)
{
    public async Task ProcessAsync(IReadOnlyList<CdcChange> changes, CancellationToken ct)
    {
        if (changes.Count == 0)
            return;

        var upserts = changes
            .Where(change => !string.Equals(change.Operation, "DELETE", StringComparison.OrdinalIgnoreCase))
            .Select(mapper.MapToDocument)
            .ToList();

        var deletes = changes
            .Where(change => string.Equals(change.Operation, "DELETE", StringComparison.OrdinalIgnoreCase))
            .Select(change => change.ProductId.ToString())
            .ToList();

        var source = ResolveSource(changes);
        var oldestChangeTimestamp = changes.Min(change => change.ChangeTimestamp);

        try
        {
            if (upserts.Count > 0)
                await retryPolicy.ExecuteAsync("Typesense upsert batch", innerCt => upsertClient.UpsertBatchAsync(upserts, innerCt), ct);

            if (deletes.Count > 0)
                await retryPolicy.ExecuteAsync("Typesense delete batch", innerCt => upsertClient.DeleteBatchAsync(deletes, innerCt), ct);

            if (upserts.Count > 0 || deletes.Count > 0)
                await cacheInvalidator.InvalidateAsync(ct);

            metrics.RecordProcessed(source, changes.Count);
            metrics.SetLag(source, DateTimeOffset.UtcNow - oldestChangeTimestamp);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            metrics.RecordError(source);
            await deadLetterWriter.WriteAsync(changes, ex.ToString(), ct);
            logger.LogError(ex, "Failed to process sync batch with {Count} change(s)", changes.Count);
            throw;
        }
    }

    private static string ResolveSource(IReadOnlyList<CdcChange> changes)
        => changes.Any(change => string.Equals(change.ChangeSource, "product", StringComparison.OrdinalIgnoreCase))
            ? "product"
            : "stock_price";
}
