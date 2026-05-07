using FluentAssertions;
using Nexora.SearchAPI.Features.Search;
using Nexora.SearchAPI.Pipeline;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Tests.Features.Search;

public class SearchRequestValidatorTests
{
    private readonly SearchRequestValidator _sut = new(
        new QuerySanitizer(),
        new SearchFilterExpressionValidator());

    [Fact]
    public void Validate_ValidRequest_NormalizesRequest()
    {
        var result = _sut.Validate(new SearchRequest
        {
            Query = "  Running Shoes  ",
            Page = 2,
            PerPage = 24,
            Sort = "RATING",
            FilterBy = "brand:=[Nike,Adidas] && price:[10..250] && rating:>=4",
            FacetBy = "brand, category,brand"
        });

        result.IsValid.Should().BeTrue();
        result.Request.Should().BeEquivalentTo(new
        {
            Query = "Running Shoes",
            Page = 2,
            PerPage = 24,
            Sort = SearchConstants.SortModes.Rating,
            FilterBy = "brand:=[Nike,Adidas] && price:[10..250] && rating:>=4",
            FacetBy = "brand,category"
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SELECT * FROM products")]
    public void Validate_InvalidQuery_ReturnsBadRequest(string query)
    {
        var result = _sut.Validate(new SearchRequest { Query = query });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Validate_QueryTooLong_ReturnsBadRequest()
    {
        var result = _sut.Validate(new SearchRequest { Query = new string('a', SearchConstants.MaxQueryLength + 1) });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Validate_PageBelowOne_ReturnsBadRequest()
    {
        var result = _sut.Validate(new SearchRequest { Query = "shoes", Page = 0 });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error.Should().Be("page must be >= 1.");
    }

    [Fact]
    public void Validate_DeepPagination_ReturnsRateLimit()
    {
        var result = _sut.Validate(new SearchRequest
        {
            Query = "shoes",
            Page = SearchConstants.MaxDeepPaginationPage + 1
        });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(429);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SearchConstants.MaxPageSize + 1)]
    public void Validate_InvalidPerPage_ReturnsBadRequest(int perPage)
    {
        var result = _sut.Validate(new SearchRequest { Query = "shoes", PerPage = perPage });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Validate_UnknownSort_ReturnsBadRequest()
    {
        var result = _sut.Validate(new SearchRequest { Query = "shoes", Sort = "oldest" });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Validate_FilterByMerchantId_ReturnsBadRequest()
    {
        var result = _sut.Validate(new SearchRequest
        {
            Query = "shoes",
            FilterBy = "merchant_id:=tenant-1"
        });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error.Should().Be("filter_by cannot reference merchant_id.");
    }

    [Fact]
    public void Validate_FilterByUnsupportedField_ReturnsBadRequest()
    {
        var result = _sut.Validate(new SearchRequest
        {
            Query = "shoes",
            FilterBy = "created_at:>123"
        });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error.Should().Be("filter_by contains an unsupported field.");
    }

    [Fact]
    public void Validate_FacetByUnsupportedField_ReturnsBadRequest()
    {
        var result = _sut.Validate(new SearchRequest
        {
            Query = "shoes",
            FacetBy = "brand,merchant_id"
        });

        result.IsValid.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Error.Should().Be("facet_by contains an unsupported field.");
    }
}
