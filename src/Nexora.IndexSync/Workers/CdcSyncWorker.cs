using System.Diagnostics.CodeAnalysis;
using Nexora.IndexSync.Services;
using Nexora.IndexSync.Options;
using Microsoft.Extensions.Options;

namespace Nexora.IndexSync.Workers;

[ExcludeFromCodeCoverage]
public sealed class CdcSyncWorker(
    CdcChangeReader reader,
    BatchCollector batchCollector,
    SyncBatchProcessor batchProcessor,
    IOptions<IndexSyncOptions> options,
    ILogger<CdcSyncWorker> logger) : BackgroundService
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

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
                    foreach (var batch in batchCollector.Chunk(changes))
                        await batchProcessor.ProcessAsync(batch, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "CDC sync iteration failed"); }
            await Task.Delay(_pollInterval, ct);
        }
    }
}
