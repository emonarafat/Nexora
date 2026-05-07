using FluentAssertions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

public class IntentClassifierTests
{
    private readonly IntentClassifier _sut = new();

    [Theory]
    [InlineData("ABC-12345")]
    [InlineData("SKU-ABC123")]
    [InlineData("PROD-98765")]
    public void Classify_SkuPattern_ReturnsNavigational(string query)
        => _sut.Classify(query).Intent.Should().Be(SearchIntent.Navigational);

    [Fact]
    public void Classify_CategoryTerm_ReturnsCategoryFiltered()
        => _sut.Classify("running shoes").Intent.Should().Be(SearchIntent.CategoryFiltered);

    [Fact]
    public void Classify_GenericQuery_ReturnsTransactional()
        => _sut.Classify("something random xyz").Intent.Should().Be(SearchIntent.Transactional);
}
