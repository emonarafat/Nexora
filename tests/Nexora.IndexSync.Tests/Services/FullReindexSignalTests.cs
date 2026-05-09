using FluentAssertions;
using Nexora.IndexSync.Services;

namespace Nexora.IndexSync.Tests.Services;

public class FullReindexSignalTests
{
    [Fact]
    public async Task WaitAsync_AfterTrigger_ReturnsTrue()
    {
        var signal = new FullReindexSignal();
        signal.Trigger();

        var result = await signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WaitAsync_WithoutTrigger_ReturnsFalseOnTimeout()
    {
        var signal = new FullReindexSignal();

        var result = await signal.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Trigger_CalledMultipleTimes_DoesNotBlock()
    {
        var signal = new FullReindexSignal();

        // Multiple triggers should not throw or block (semaphore capped at 1)
        signal.Trigger();
        signal.Trigger();
        signal.Trigger();

        var result = await signal.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        result.Should().BeTrue();

        // Second wait should time out (only one release should have been added)
        var second = await signal.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        second.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAsync_CancelledToken_ThrowsOrReturnsFalse()
    {
        var signal = new FullReindexSignal();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => signal.WaitAsync(TimeSpan.FromSeconds(10), cts.Token);

        // SemaphoreSlim.WaitAsync with a cancelled token throws OperationCanceledException
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
