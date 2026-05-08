using System.Diagnostics.Metrics;

namespace Nexora.IndexSync.Services;

public sealed class IndexSyncMetrics
{
    private const string MeterName = "Nexora.IndexSync";
    private readonly Meter _meter;
    private readonly Dictionary<string, double> _lagBySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly Counter<long> _processedCounter;
    private readonly Counter<long> _errorCounter;

    public IndexSyncMetrics()
    {
        _meter = new Meter(MeterName);
        _processedCounter = _meter.CreateCounter<long>("search_index_sync_processed_total");
        _errorCounter = _meter.CreateCounter<long>("search_index_sync_errors_total");
        _meter.CreateObservableGauge("search_index_sync_lag_seconds", ObserveLagSeconds, unit: "s");
        _lagBySource["product"] = 0;
        _lagBySource["stock_price"] = 0;
        _lagBySource["full_reindex"] = 0;
    }

    public static string MetricsSourceName => MeterName;

    public void RecordProcessed(string source, int count)
    {
        if (count > 0)
            _processedCounter.Add(count, KeyValuePair.Create<string, object?>("source", source));
    }

    public void RecordError(string source)
        => _errorCounter.Add(1, KeyValuePair.Create<string, object?>("source", source));

    public void SetLag(string source, TimeSpan lag)
    {
        lock (_sync)
        {
            _lagBySource[source] = Math.Max(0, lag.TotalSeconds);
        }
    }

    private IEnumerable<Measurement<double>> ObserveLagSeconds()
    {
        lock (_sync)
        {
            return _lagBySource
                .Select(entry => new Measurement<double>(
                    entry.Value,
                    KeyValuePair.Create<string, object?>("source", entry.Key)))
                .ToArray();
        }
    }
}
