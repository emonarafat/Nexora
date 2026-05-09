using System.Diagnostics.CodeAnalysis;
using Nexora.IndexSync.Services;
using Nexora.IndexSync.Options;
using Microsoft.Extensions.Options;

namespace Nexora.IndexSync.Workers;

[ExcludeFromCodeCoverage]
public sealed class FullReindexWorker(
    CdcChangeReader reader,
    BatchCollector batchCollector,
    SyncBatchProcessor batchProcessor,
    FullReindexSignal signal,
    IOptions<IndexSyncOptions> options,
    IndexSyncMetrics metrics,
    ILogger<FullReindexWorker> logger) : BackgroundService
{
    private readonly int _fullReindexPageSize = Math.Max(1, options.Value.FullReindexPageSize);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var next = NextSundayAt2AM();
                var delay = next - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    var triggered = await signal.WaitAsync(delay, ct);
                    logger.LogInformation("Full re-index starting via {Trigger}", triggered ? "manual trigger" : "scheduled trigger");
                }
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
            var rows = await reader.GetFullPageAsync(page, _fullReindexPageSize, ct);
            if (rows.Count == 0) break;
            foreach (var batch in batchCollector.Chunk(rows))
                await batchProcessor.ProcessAsync(batch, ct);
            total += rows.Count; page++;
            logger.LogInformation("Re-indexed {Total} so far", total);
        }
        metrics.SetLag("full_reindex", TimeSpan.Zero);
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
