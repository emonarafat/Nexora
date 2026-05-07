using FluentAssertions;
using Nexora.SearchAPI.Pipeline;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

public class QueryStructurerTests
{
    private readonly QueryStructurer _sut = new();

    [Fact]
    public void BuildSearchParameters_BasicQuery_ReturnsValidParameters()
    {
        var processed = new ProcessedQuery(
            OriginalQuery: "running shoes",
            NormalizedQuery: "running shoes",
            CorrectedQuery: null,
            ExpandedTerms: ["running shoes"],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new SearchRequest
        {
            Query = "running shoes",
            Page = 1,
            PerPage = 20,
            Sort = SearchConstants.SortModes.Relevance
        };

        var result = _sut.BuildSearchParameters(processed, request);

        result.Should().NotBeNull();
        result.Page.Should().Be(1);
        result.PerPage.Should().Be(20);
        result.SortBy.Should().Be("_text_match:desc,popularity_score:desc");
        result.NumberOfTypos.Should().Be("2");
    }

    [Theory]
    [InlineData("short", "1")]
    [InlineData("12345678", "1")]
    [InlineData("123456789", "2")]
    [InlineData("longer query string", "2")]
    public void BuildSearchParameters_TypoTolerance_BasedOnQueryLength(string query, string expectedTypos)
    {
        var processed = new ProcessedQuery(
            OriginalQuery: query,
            NormalizedQuery: query,
            CorrectedQuery: null,
            ExpandedTerms: [query],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new SearchRequest { Query = query };

        var result = _sut.BuildSearchParameters(processed, request);

        result.NumberOfTypos.Should().Be(expectedTypos);
    }

    [Theory]
    [InlineData("relevance", "_text_match:desc,popularity_score:desc")]
    [InlineData("price_asc", "price:asc")]
    [InlineData("price_desc", "price:desc")]
    [InlineData("rating", "rating:desc,rating_count:desc")]
    [InlineData("newest", "created_at:desc")]
    public void BuildSearchParameters_SortMode_MapsCorrectly(string sortMode, string expectedSort)
    {
        var processed = new ProcessedQuery(
            OriginalQuery: "test",
            NormalizedQuery: "test",
            CorrectedQuery: null,
            ExpandedTerms: ["test"],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new SearchRequest { Query = "test", Sort = sortMode };

        var result = _sut.BuildSearchParameters(processed, request);

        result.SortBy.Should().Be(expectedSort);
    }

    [Fact]
    public void BuildSearchParameters_WithClientFilter_IncludesFilter()
    {
        var processed = new ProcessedQuery(
            OriginalQuery: "laptop",
            NormalizedQuery: "laptop",
            CorrectedQuery: null,
            ExpandedTerms: ["laptop"],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new SearchRequest
        {
            Query = "laptop",
            FilterBy = "price:>500"
        };

        var result = _sut.BuildSearchParameters(processed, request);

        result.FilterBy.Should().Be("price:>500");
    }

    [Fact]
    public void BuildSearchParameters_WithIntentFilters_CombinesFilters()
    {
        var processed = new ProcessedQuery(
            OriginalQuery: "electronics",
            NormalizedQuery: "electronics",
            CorrectedQuery: null,
            ExpandedTerms: ["electronics"],
            Intent: SearchIntent.CategoryFiltered,
            IntentFilters: new Dictionary<string, string> { { "category", "electronics" } });

        var request = new SearchRequest
        {
            Query = "electronics",
            FilterBy = "price:>100"
        };

        var result = _sut.BuildSearchParameters(processed, request);

        result.FilterBy.Should().Be("price:>100 && category:=electronics");
    }

    [Fact]
    public void BuildSearchParameters_IntentFiltersOnly_UsesIntentFilters()
    {
        var processed = new ProcessedQuery(
            OriginalQuery: "shoes",
            NormalizedQuery: "shoes",
            CorrectedQuery: null,
            ExpandedTerms: ["shoes"],
            Intent: SearchIntent.CategoryFiltered,
            IntentFilters: new Dictionary<string, string> { { "category", "footwear" } });

        var request = new SearchRequest { Query = "shoes" };

        var result = _sut.BuildSearchParameters(processed, request);

        result.FilterBy.Should().Be("category:=footwear");
    }

    [Fact]
    public void BuildSearchParameters_WithFacets_IncludesFacets()
    {
        var processed = new ProcessedQuery(
            OriginalQuery: "shirt",
            NormalizedQuery: "shirt",
            CorrectedQuery: null,
            ExpandedTerms: ["shirt"],
            Intent: SearchIntent.Transactional,
            IntentFilters: null);

        var request = new SearchRequest
        {
            Query = "shirt",
            FacetBy = "brand,color,size"
        };

        var result = _sut.BuildSearchParameters(processed, request);

        result.FacetBy.Should().Be("brand,color,size");
    }

    [Fact]
    public void BuildSearchParameters_NavigationalIntent_PreservesIntent()
    {
        var processed = new ProcessedQuery(
            OriginalQuery: "SKU-12345",
            NormalizedQuery: "sku-12345",
            CorrectedQuery: null,
            ExpandedTerms: ["sku-12345"],
            Intent: SearchIntent.Navigational,
            IntentFilters: null);

        var request = new SearchRequest { Query = "SKU-12345" };

        var result = _sut.BuildSearchParameters(processed, request);

        result.Should().NotBeNull();
        result.NumberOfTypos.Should().Be("2"); // 9 chars > 8, so 2 typos
    }
}
