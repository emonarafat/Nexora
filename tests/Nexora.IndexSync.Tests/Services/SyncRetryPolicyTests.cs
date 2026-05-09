using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.IndexSync.Options;
using Nexora.IndexSync.Services;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Nexora.IndexSync.Tests.Services;

public class SyncRetryPolicyTests
{
    private static SyncRetryPolicy CreatePolicy(int maxRetryAttempts)
    {
        var opts = OptionsFactory.Create(new IndexSyncOptions { MaxRetryAttempts = maxRetryAttempts });
        return new SyncRetryPolicy(opts, NullLogger<SyncRetryPolicy>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ActionSucceedsOnFirstAttempt_CompletesNormally()
    {
        var policy = CreatePolicy(3);
        var callCount = 0;

        await policy.ExecuteAsync("test-op", ct =>
        {
            callCount++;
            return Task.CompletedTask;
        }, CancellationToken.None);

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ActionFailsOnceThenSucceeds_RetriesAndCompletes()
    {
        // MaxRetryAttempts = 2: attempt 1 fails (delay 1s), attempt 2 succeeds
        var policy = CreatePolicy(2);
        var callCount = 0;

        await policy.ExecuteAsync("test-op", ct =>
        {
            callCount++;
            if (callCount == 1)
                throw new InvalidOperationException("Transient error");
            return Task.CompletedTask;
        }, CancellationToken.None);

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ActionAlwaysFails_MaxRetryAttempts1_ThrowsImmediately()
    {
        // With MaxRetryAttempts=1, the when-filter (attempt < _maxRetryAttempts = 1<1=false)
        // is false on the first attempt, so the exception propagates immediately.
        var policy = CreatePolicy(1);
        var callCount = 0;

        var act = async () => await policy.ExecuteAsync("test-op", ct =>
        {
            callCount++;
            throw new InvalidOperationException("Always fails");
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Always fails");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ActionAlwaysFails_MaxRetryAttempts2_ThrowsAfterRetry()
    {
        // MaxRetryAttempts=2: attempt 1 fails (delay 1s), attempt 2 fails → exception propagates.
        var policy = CreatePolicy(2);
        var callCount = 0;
        var expectedException = new InvalidOperationException("Always fails");

        var act = async () => await policy.ExecuteAsync("test-op", ct =>
        {
            callCount++;
            throw expectedException;
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Always fails");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroMaxRetryAttempts_ClampedToOne_ActionCalledOnce()
    {
        // MaxRetryAttempts is clamped to Math.Max(1, value), so 0 → 1
        var policy = CreatePolicy(0);
        var callCount = 0;

        var act = async () => await policy.ExecuteAsync("test-op", ct =>
        {
            callCount++;
            throw new InvalidOperationException("fail");
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_PropagatesException()
    {
        var policy = CreatePolicy(3);
        using var cts = new CancellationTokenSource();

        var act = async () => await policy.ExecuteAsync("test-op", ct =>
        {
            cts.Cancel();
            throw new OperationCanceledException(ct);
        }, cts.Token);

        // When cancelled, the when-filter (!ct.IsCancellationRequested) is false → exception propagates
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
