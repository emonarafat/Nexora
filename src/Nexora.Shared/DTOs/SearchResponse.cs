namespace Nexora.Shared.DTOs;

public record SearchResponse
{
    public IReadOnlyList<ProductResult> Results { get; init; } = [];
    public long TotalCount { get; init; }
    public int Page { get; init; }
    public int PerPage { get; init; }
    public int TotalPages { get; init; }
    public Dictionary<string, List<FacetValue>> Facets { get; init; } = [];
    public string? CorrectedQuery { get; init; }
    public double LatencyMs { get; init; }
    public bool CacheHit { get; init; }
}

public record ProductResult
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "USD";
    public float Rating { get; init; }
    public int RatingCount { get; init; }
    public string StockStatus { get; init; } = string.Empty;
    public bool IsFeatured { get; init; }
    public double FinalScore { get; init; }
}

public record FacetValue
{
    public string Value { get; init; } = string.Empty;
    public long Count { get; init; }
}
