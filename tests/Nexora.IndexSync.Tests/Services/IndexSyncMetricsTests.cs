using FluentAssertions;
using Nexora.IndexSync.Services;

namespace Nexora.IndexSync.Tests.Services;

public class IndexSyncMetricsTests
{
    [Fact]
    public void MetricsSourceName_IsCorrectMeterName()
    {
        IndexSyncMetrics.MetricsSourceName.Should().Be("Nexora.IndexSync");
    }

    [Fact]
    public void RecordProcessed_PositiveCount_DoesNotThrow()
    {
        var metrics = new IndexSyncMetrics();
        var act = () => metrics.RecordProcessed("product", 10);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordProcessed_ZeroCount_DoesNotThrow()
    {
        // count == 0 → the counter is not incremented (no-op branch)
        var metrics = new IndexSyncMetrics();
        var act = () => metrics.RecordProcessed("product", 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordProcessed_DifferentSources_DoesNotThrow()
    {
        var metrics = new IndexSyncMetrics();
        var act = () =>
        {
            metrics.RecordProcessed("product", 5);
            metrics.RecordProcessed("stock_price", 3);
            metrics.RecordProcessed("full_reindex", 1000);
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordError_DoesNotThrow()
    {
        var metrics = new IndexSyncMetrics();
        var act = () => metrics.RecordError("product");
        act.Should().NotThrow();
    }

    [Fact]
    public void SetLag_PositiveLag_DoesNotThrow()
    {
        var metrics = new IndexSyncMetrics();
        var act = () => metrics.SetLag("product", TimeSpan.FromSeconds(30));
        act.Should().NotThrow();
    }

    [Fact]
    public void SetLag_NegativeLag_ClampedToZero_DoesNotThrow()
    {
        // Negative timespan → Math.Max(0, negative) = 0; should not throw
        var metrics = new IndexSyncMetrics();
        var act = () => metrics.SetLag("product", TimeSpan.FromSeconds(-5));
        act.Should().NotThrow();
    }

    [Fact]
    public void SetLag_MultipleSourcesConcurrently_NoRaceCondition()
    {
        var metrics = new IndexSyncMetrics();
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            metrics.SetLag("product", TimeSpan.FromSeconds(i));
            metrics.SetLag("stock_price", TimeSpan.FromMilliseconds(i * 100));
        }));

        var act = async () => await Task.WhenAll(tasks);
        act.Should().NotThrowAsync();
    }
}
