using Nexora.IndexSync.Services;

namespace Nexora.IndexSync.Workers;

public sealed class FullReindexWorker(
    CdcChangeReader reader,
    FieldMapper mapper,
    TypesenseUpsertClient upsertClient,
    SearchApiSuggestCacheInvalidator cacheInvalidator,
    ILogger<FullReindexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var next = NextSundayAt2AM();
                var delay = next - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
                if (!ct.IsCancellationRequested) await ReindexAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Full re-index failed"); await Task.Delay(TimeSpan.FromHours(1), ct); }
        }
    }

    private async Task ReindexAsync(CancellationToken ct)
    {
        int page = 0; int total = 0;
        while (!ct.IsCancellationRequested)
        {
            var rows = await reader.GetFullPageAsync(page, 1000, ct);
            if (rows.Count == 0) break;
            await upsertClient.UpsertBatchAsync(rows.Select(mapper.MapToDocument).ToList(), ct);
            total += rows.Count; page++;
            logger.LogInformation("Re-indexed {Total} so far", total);
        }
        if (total > 0)
            await cacheInvalidator.InvalidateAsync(ct);
        logger.LogInformation("Full re-index done: {Total}", total);
    }

    private static DateTimeOffset NextSundayAt2AM()
    {
        var now = DateTimeOffset.UtcNow;
        int days = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
        if (days == 0 && now.TimeOfDay >= TimeSpan.FromHours(2)) days = 7;
        return new DateTimeOffset(now.Date.AddDays(days).Add(TimeSpan.FromHours(2)), TimeSpan.Zero);
    }
}
