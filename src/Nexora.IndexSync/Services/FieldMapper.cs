using System.Text.RegularExpressions;
using Nexora.IndexSync.Models;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.IndexSync.Services;

public sealed partial class FieldMapper
{
    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTags();

    public ProductDocument MapToDocument(CdcChange row) => new()
    {
        Id = row.ProductId.ToString(),
        Title = row.ProductName ?? string.Empty,
        Brand = row.BrandName ?? string.Empty,
        Sku = row.ProductSku ?? string.Empty,
        Description = StripHtml(row.ProductDescription),
        Category = row.CategoryName ?? string.Empty,
        CategoryPath = SplitBy(row.CategoryHierarchy, '>'),
        Price = row.UnitPrice,
        Currency = row.CurrencyCode ?? "USD",
        Color = SplitBy(row.ColorVariants, ','),
        Size = SplitBy(row.SizeVariants, ','),
        Rating = row.AvgRating,
        RatingCount = row.RatingCount,
        StockStatus = MapStock(row.StockStatus, row.StockQuantity),
        StockQuantity = row.StockQuantity,
        IsFeatured = row.IsFeaturedFlag,
        IsActive = row.IsActiveFlag,
        MerchantId = row.MerchantId ?? string.Empty,
        CreatedAt = row.CreatedDate.ToUnixTimeSeconds(),
        UpdatedAt = row.ModifiedDate.ToUnixTimeSeconds()
    };

    private string StripHtml(string? html) =>
        string.IsNullOrEmpty(html) ? string.Empty : HtmlTags().Replace(html, string.Empty).Trim();

    private static string[] SplitBy(string? value, char separator) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string MapStock(string? mssqlStatus, int qty)
    {
        if (!string.IsNullOrEmpty(mssqlStatus))
            return mssqlStatus.ToUpperInvariant() switch
            {
                "IN_STOCK" or "INSTOCK" or "AVAILABLE" => SearchConstants.StockStatus.InStock,
                "LOW_STOCK" or "LOWSTOCK" or "LIMITED" => SearchConstants.StockStatus.LowStock,
                "OUT_OF_STOCK" or "OUTOFSTOCK" or "UNAVAILABLE" => SearchConstants.StockStatus.OutOfStock,
                _ => qty > 0 ? SearchConstants.StockStatus.InStock : SearchConstants.StockStatus.OutOfStock
            };
        return qty > 0 ? SearchConstants.StockStatus.InStock : SearchConstants.StockStatus.OutOfStock;
    }
}
