namespace Nexora.IndexSync.Services;

public sealed class FullReindexSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Trigger()
    {
        if (_signal.CurrentCount == 0)
            _signal.Release();
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
        => _signal.WaitAsync(timeout, ct);
}
