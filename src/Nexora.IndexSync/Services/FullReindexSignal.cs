namespace Nexora.IndexSync.Services;

public sealed class FullReindexSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly object _sync = new();

    public void Trigger()
    {
        lock (_sync)
        {
            if (_signal.CurrentCount == 0)
                _signal.Release();
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
        => _signal.WaitAsync(timeout, ct);
}
