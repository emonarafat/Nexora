namespace Nexora.IndexSync.Models;

public record CdcChange
{
    public string Operation { get; init; } = "UPDATE";
    public int ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? BrandName { get; init; }
    public string? ProductSku { get; init; }
    public string? ProductDescription { get; init; }
    public string? CategoryName { get; init; }
    public string? CategoryHierarchy { get; init; }
    public float UnitPrice { get; init; }
    public string? CurrencyCode { get; init; }
    public string? ColorVariants { get; init; }
    public string? SizeVariants { get; init; }
    public float AvgRating { get; init; }
    public int RatingCount { get; init; }
    public bool IsFeaturedFlag { get; init; }
    public bool IsActiveFlag { get; init; } = true;
    public string? MerchantId { get; init; }
    public DateTimeOffset CreatedDate { get; init; }
    public DateTimeOffset ModifiedDate { get; init; }
    public string? StockStatus { get; init; }
    public int StockQuantity { get; init; }
}
