using System.Diagnostics.CodeAnalysis;

namespace Nexora.Shared.DTOs;

[ExcludeFromCodeCoverage]
public record SuggestResponse
{
    public IReadOnlyList<SuggestionItem> Suggestions { get; init; } = [];
    public double LatencyMs { get; init; }
    public bool CacheHit { get; init; }
}

[ExcludeFromCodeCoverage]
public record SuggestionItem
{
    public string Text { get; init; } = string.Empty;
    public string? Category { get; init; }
    public float PopularityScore { get; init; }
}
