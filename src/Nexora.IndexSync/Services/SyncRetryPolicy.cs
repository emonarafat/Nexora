using Microsoft.Extensions.Options;
using Nexora.IndexSync.Options;

namespace Nexora.IndexSync.Services;

public sealed class SyncRetryPolicy(IOptions<IndexSyncOptions> options, ILogger<SyncRetryPolicy> logger)
{
    private readonly int _maxRetryAttempts = Math.Max(1, options.Value.MaxRetryAttempts);

    public async Task ExecuteAsync(
        string operationName,
        Func<CancellationToken, Task> action,
        CancellationToken ct)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                await action(ct);
                return;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && attempt < _maxRetryAttempts)
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                logger.LogWarning(ex,
                    "{OperationName} failed on attempt {Attempt}/{MaxAttempts}; retrying in {DelaySeconds}s",
                    operationName,
                    attempt,
                    _maxRetryAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }

        throw lastException ?? new InvalidOperationException($"{operationName} failed after {_maxRetryAttempts} attempts.");
    }
}
