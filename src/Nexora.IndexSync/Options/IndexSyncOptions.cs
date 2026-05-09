using System.Diagnostics.CodeAnalysis;

namespace Nexora.IndexSync.Options;

[ExcludeFromCodeCoverage]
public sealed class IndexSyncOptions
{
    public const string SectionName = "IndexSync";

    public int PollIntervalSeconds { get; init; } = 10;
    public int BatchSize { get; init; } = 250;
    public int FullReindexPageSize { get; init; } = 1000;
    public int MaxRetryAttempts { get; init; } = 5;
}
