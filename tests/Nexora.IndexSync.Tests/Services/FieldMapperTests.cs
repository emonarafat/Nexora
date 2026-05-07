using FluentAssertions;
using Nexora.IndexSync.Models;
using Nexora.IndexSync.Services;
using Nexora.Shared.Constants;
using Xunit;

namespace Nexora.IndexSync.Tests.Services;

public class FieldMapperTests
{
    private readonly FieldMapper _mapper = new();

    private static CdcChange Base() => new()
    {
        ProductId = 42, ProductName = "Nike Air Max 90", BrandName = "Nike",
        ProductSku = "NIKE-AM90", ProductDescription = "<p>Great shoe</p>",
        CategoryName = "Footwear", CategoryHierarchy = "Clothing > Footwear > Running",
        UnitPrice = 120f, CurrencyCode = "USD",
        ColorVariants = "Black,White", SizeVariants = "9,10,11",
        AvgRating = 4.5f, RatingCount = 200,
        IsFeaturedFlag = true, IsActiveFlag = true, MerchantId = "m1",
        CreatedDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ModifiedDate = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
        StockStatus = "IN_STOCK", StockQuantity = 50
    };

    [Fact]
    public void Map_BasicFields_Correct()
    {
        var doc = _mapper.MapToDocument(Base());
        doc.Id.Should().Be("42");
        doc.Title.Should().Be("Nike Air Max 90");
        doc.Brand.Should().Be("Nike");
        doc.Price.Should().Be(120f);
        doc.Rating.Should().Be(4.5f);
        doc.IsFeatured.Should().BeTrue();
        doc.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Map_HtmlDescription_Stripped()
    {
        var doc = _mapper.MapToDocument(Base());
        doc.Description.Should().NotContain("<p>");
        doc.Description.Should().Contain("Great shoe");
    }

    [Fact]
    public void Map_CsvColor_SplitToArray()
    {
        var doc = _mapper.MapToDocument(Base());
        doc.Color.Should().Contain("Black").And.Contain("White");
    }

    [Fact]
    public void Map_CategoryHierarchy_ParsedToArray()
    {
        var doc = _mapper.MapToDocument(Base());
        doc.CategoryPath.Should().HaveCount(3).And.Contain("Clothing").And.Contain("Running");
    }

    [Fact]
    public void Map_InStock_MapsCorrectly()
        => _mapper.MapToDocument(Base() with { StockStatus = "IN_STOCK" })
            .StockStatus.Should().Be(SearchConstants.StockStatus.InStock);

    [Fact]
    public void Map_OutOfStock_MapsCorrectly()
        => _mapper.MapToDocument(Base() with { StockStatus = "OUT_OF_STOCK" })
            .StockStatus.Should().Be(SearchConstants.StockStatus.OutOfStock);

    [Fact]
    public void Map_NullStockWithPositiveQty_IsInStock()
        => _mapper.MapToDocument(Base() with { StockStatus = null, StockQuantity = 10 })
            .StockStatus.Should().NotBe(SearchConstants.StockStatus.OutOfStock);

    [Fact]
    public void Map_NullStockWithZeroQty_IsOutOfStock()
        => _mapper.MapToDocument(Base() with { StockStatus = null, StockQuantity = 0 })
            .StockStatus.Should().Be(SearchConstants.StockStatus.OutOfStock);

    [Fact]
    public void Map_CreatedDate_ConvertedToEpoch()
    {
        var doc = _mapper.MapToDocument(Base());
        doc.CreatedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
    }
}
