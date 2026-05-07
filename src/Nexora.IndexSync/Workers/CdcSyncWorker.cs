using Nexora.IndexSync.Services;

namespace Nexora.IndexSync.Workers;

public sealed class CdcSyncWorker(
    CdcChangeReader reader,
    FieldMapper mapper,
    TypesenseUpsertClient upsertClient,
    SearchApiSuggestCacheInvalidator cacheInvalidator,
    ILogger<CdcSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("CdcSyncWorker started");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var changes = await reader.GetChangesAsync(ct);
                if (changes.Count > 0)
                {
                    var upserts = changes.Where(c => c.Operation != "DELETE")
                        .Select(mapper.MapToDocument).ToList();
                    var deletes = changes.Where(c => c.Operation == "DELETE")
                        .Select(c => c.ProductId.ToString()).ToList();
                    if (upserts.Count > 0) await upsertClient.UpsertBatchAsync(upserts, ct);
                    if (deletes.Count > 0) await upsertClient.DeleteBatchAsync(deletes, ct);
                    await cacheInvalidator.InvalidateAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "CDC sync iteration failed"); }
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }
}
