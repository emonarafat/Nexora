using FluentAssertions;
using Nexora.SearchAPI.Features.Search;

namespace Nexora.SearchAPI.Tests.Features.Search;

/// <summary>
/// Direct unit tests for SearchFilterExpressionValidator covering all branches
/// of TryValidate and TryNormalizeFacets.
/// </summary>
public class SearchFilterExpressionValidatorTests
{
    private readonly SearchFilterExpressionValidator _sut = new();

    #region TryValidate – null / empty

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryValidate_NullOrEmptyFilter_ReturnsTrueWithNullOutput(string? filterBy)
    {
        var result = _sut.TryValidate(filterBy, out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().BeNull();
        error.Should().BeNull();
    }

    #endregion

    #region TryValidate – too long

    [Fact]
    public void TryValidate_FilterTooLong_ReturnsFalse()
    {
        var longFilter = "brand:" + new string('a', 500);

        var result = _sut.TryValidate(longFilter, out var normalized, out var error);

        result.Should().BeFalse();
        normalized.Should().BeNull();
        error.Should().Contain("<= 500 chars");
    }

    #endregion

    #region TryValidate – merchant_id

    [Fact]
    public void TryValidate_ContainsMerchantId_ReturnsFalse()
    {
        var result = _sut.TryValidate("merchant_id:=tenant-1", out var normalized, out var error);

        result.Should().BeFalse();
        error.Should().Be("filter_by cannot reference merchant_id.");
    }

    [Fact]
    public void TryValidate_ContainsMerchantIdUpperCase_ReturnsFalse()
    {
        var result = _sut.TryValidate("MERCHANT_ID:=X", out var normalized, out var error);

        result.Should().BeFalse();
        error.Should().Be("filter_by cannot reference merchant_id.");
    }

    #endregion

    #region TryValidate – suspicious patterns

    [Theory]
    [InlineData("price:>10; DROP TABLE products")]
    [InlineData("brand:=Nike -- comment")]
    [InlineData("brand:=Nike /* comment */")]
    [InlineData("brand:=<script>alert(1)</script>")]
    [InlineData("brand:=javascript:void(0)")]
    public void TryValidate_SuspiciousPattern_ReturnsFalse(string filterBy)
    {
        var result = _sut.TryValidate(filterBy, out var normalized, out var error);

        result.Should().BeFalse();
        error.Should().Contain("unsupported");
    }

    #endregion

    #region TryValidate – non-allowed characters

    [Fact]
    public void TryValidate_NonAllowedCharacter_AtSign_ReturnsFalse()
    {
        var result = _sut.TryValidate("brand:=Nike@Adidas", out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("unsupported");
    }

    [Fact]
    public void TryValidate_NonAllowedCharacter_Hash_ReturnsFalse()
    {
        // '#' is not in the allowed character set
        var result = _sut.TryValidate("brand#Nike", out _, out var error);

        result.Should().BeFalse();
    }

    #endregion

    #region TryValidate – no recognized fields

    [Fact]
    public void TryValidate_FilterWithNoColonSeparator_ReturnsFalse()
    {
        // No `:` means the field pattern regex matches nothing → fields.Count == 0
        var result = _sut.TryValidate("brandNike", out var normalized, out var error);

        result.Should().BeFalse();
        error.Should().Contain("unsupported field");
    }

    [Fact]
    public void TryValidate_FilterWithUnknownField_ReturnsFalse()
    {
        var result = _sut.TryValidate("created_at:>123456", out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("unsupported field");
    }

    #endregion

    #region TryValidate – valid filters

    [Theory]
    [InlineData("brand:=Nike")]
    [InlineData("price:[10..500]")]
    [InlineData("category:=Footwear")]
    [InlineData("stock_status:=in_stock")]
    [InlineData("color:=[Red,Blue]")]
    [InlineData("size:=[S,M,L]")]
    [InlineData("rating:>=4")]
    public void TryValidate_ValidSingleFieldFilter_ReturnsTrue(string filterBy)
    {
        var result = _sut.TryValidate(filterBy, out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_ValidMultiFieldFilter_ReturnsTrue()
    {
        var result = _sut.TryValidate(
            "brand:=[Nike,Adidas] && price:[10..250] && rating:>=4",
            out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_FilterWithExtraWhitespace_NormalizesWhitespace()
    {
        var result = _sut.TryValidate("brand:=Nike  &&  price:>50", out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().Be("brand:=Nike && price:>50");
    }

    #endregion

    #region TryNormalizeFacets – null / empty

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalizeFacets_NullOrEmptyInput_ReturnsTrueWithNullOutput(string? facetBy)
    {
        var result = _sut.TryNormalizeFacets(facetBy, out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void TryNormalizeFacets_OnlyCommas_ReturnsTrueWithNullOutput()
    {
        // All entries are empty after splitting with RemoveEmptyEntries → facets.Count == 0
        var result = _sut.TryNormalizeFacets(",,,", out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().BeNull();
    }

    #endregion

    #region TryNormalizeFacets – invalid fields

    [Fact]
    public void TryNormalizeFacets_ContainsMerchantId_ReturnsFalse()
    {
        var result = _sut.TryNormalizeFacets("brand,merchant_id", out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("unsupported field");
    }

    [Fact]
    public void TryNormalizeFacets_UnknownField_ReturnsFalse()
    {
        var result = _sut.TryNormalizeFacets("brand,created_at", out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("unsupported field");
    }

    #endregion

    #region TryNormalizeFacets – valid

    [Theory]
    [InlineData("brand", "brand")]
    [InlineData("BRAND", "brand")]
    [InlineData("brand,category", "brand,category")]
    public void TryNormalizeFacets_ValidSingleOrMultiple_ReturnsTrue(string facetBy, string expectedNormalized)
    {
        var result = _sut.TryNormalizeFacets(facetBy, out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().Be(expectedNormalized);
        error.Should().BeNull();
    }

    [Fact]
    public void TryNormalizeFacets_DuplicateFacets_Deduplicated()
    {
        var result = _sut.TryNormalizeFacets("brand,category,brand", out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().Be("brand,category");
    }

    [Fact]
    public void TryNormalizeFacets_AllAllowedFields_ReturnsTrue()
    {
        var result = _sut.TryNormalizeFacets(
            "brand,category,price,color,size,rating,stock_status",
            out var normalized, out var error);

        result.Should().BeTrue();
        normalized.Should().NotBeNull();
    }

    #endregion
}
