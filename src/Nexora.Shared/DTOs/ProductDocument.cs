using System.Diagnostics.CodeAnalysis;

namespace Nexora.Shared.DTOs;

[ExcludeFromCodeCoverage]
public record ProductDocument
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string[] CategoryPath { get; init; } = [];
    public float Price { get; init; }
    public string Currency { get; init; } = "USD";
    public string[] Color { get; init; } = [];
    public string[] Size { get; init; } = [];
    public string[] Attributes { get; init; } = [];
    public float Rating { get; init; }
    public int RatingCount { get; init; }
    public string StockStatus { get; init; } = "in_stock";
    public int StockQuantity { get; init; }
    public float PopularityScore { get; init; }
    public float Ctr30d { get; init; }
    public float ConversionRate30d { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsActive { get; init; } = true;
    public string MerchantId { get; init; } = string.Empty;
    public long CreatedAt { get; init; }
    public long UpdatedAt { get; init; }
}
