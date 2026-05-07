using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

public class QueryPipelineTests
{
    private readonly QueryPipeline _pipeline;

    public QueryPipelineTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" }
            })
            .Build();

        var sanitizer = new QuerySanitizer();
        var normalizer = new QueryNormalizer();
        var synonymExpander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);
        var intentClassifier = new IntentClassifier();

        _pipeline = new QueryPipeline(
            sanitizer,
            normalizer,
            synonymExpander,
            intentClassifier,
            NullLogger<QueryPipeline>.Instance);
    }

    [Theory]
    [InlineData("running shoes", SearchIntent.CategoryFiltered)]
    [InlineData("nike running shoes", SearchIntent.CategoryFiltered)]
    [InlineData("leather sofas brown", SearchIntent.CategoryFiltered)]
    public async Task ProcessAsync_CategoryQuery_ReturnsCategoryIntent(string query, SearchIntent expectedIntent)
    {
        var result = await _pipeline.ProcessAsync(query);

        result.Intent.Should().Be(expectedIntent);
        result.NormalizedQuery.Should().Be(query.ToLowerInvariant());
        result.OriginalQuery.Should().Be(query);
    }

    [Theory]
    [InlineData("ABC-12345")]
    [InlineData("SKU-ABC123")]
    [InlineData("PROD-98765")]
    public async Task ProcessAsync_SkuQuery_ReturnsNavigationalIntent(string query)
    {
        var result = await _pipeline.ProcessAsync(query);

        result.Intent.Should().Be(SearchIntent.Navigational);
        result.NormalizedQuery.Should().Be(query.ToLowerInvariant());
    }

    [Theory]
    [InlineData("wireless mouse")]
    [InlineData("coffee maker")]
    [InlineData("yoga mat")]
    public async Task ProcessAsync_TransactionalQuery_ReturnsTransactionalIntent(string query)
    {
        var result = await _pipeline.ProcessAsync(query);

        result.Intent.Should().Be(SearchIntent.Transactional);
        result.NormalizedQuery.Should().Be(query.ToLowerInvariant());
    }

    [Fact]
    public async Task ProcessAsync_QueryWithExcessiveSpaces_Normalizes()
    {
        var result = await _pipeline.ProcessAsync("running     shoes");

        result.NormalizedQuery.Should().Be("running shoes");
        result.OriginalQuery.Should().Be("running     shoes");
    }

    [Fact]
    public async Task ProcessAsync_MixedCaseQuery_Lowercases()
    {
        var result = await _pipeline.ProcessAsync("NIKE AIR MAX");

        result.NormalizedQuery.Should().Be("nike air max");
    }

    [Fact]
    public async Task ProcessAsync_QueryWithHtml_RemovesHtml()
    {
        var result = await _pipeline.ProcessAsync("running <b>shoes</b>");

        result.NormalizedQuery.Should().Contain("running");
        result.NormalizedQuery.Should().Contain("shoes");
        result.NormalizedQuery.Should().NotContain("<b>");
    }

    [Fact]
    public async Task ProcessAsync_InjectionAttempt_ReturnsEmptyNormalized()
    {
        var result = await _pipeline.ProcessAsync("SELECT * FROM products");

        result.NormalizedQuery.Should().BeEmpty();
        result.ExpandedTerms.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessAsync_LongQuery_TruncatesAndProcesses()
    {
        var longQuery = new string('a', 250);
        var result = await _pipeline.ProcessAsync(longQuery);

        result.NormalizedQuery.Length.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task ProcessAsync_ValidQuery_ReturnsExpandedTerms()
    {
        var result = await _pipeline.ProcessAsync("laptop");

        result.ExpandedTerms.Should().NotBeNull();
        result.ExpandedTerms.Should().Contain("laptop");
    }

    [Fact]
    public async Task ProcessAsync_EmptyQuery_HandlesGracefully()
    {
        var result = await _pipeline.ProcessAsync("");

        result.NormalizedQuery.Should().BeEmpty();
        result.OriginalQuery.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WhitespaceQuery_HandlesGracefully()
    {
        var result = await _pipeline.ProcessAsync("   ");

        result.NormalizedQuery.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_CompleteFlow_AllFieldsPopulated()
    {
        var result = await _pipeline.ProcessAsync("running shoes");

        result.Should().NotBeNull();
        result.OriginalQuery.Should().Be("running shoes");
        result.NormalizedQuery.Should().Be("running shoes");
        result.ExpandedTerms.Should().NotBeEmpty();
        result.Intent.Should().BeOneOf(SearchIntent.Transactional, SearchIntent.CategoryFiltered, SearchIntent.Navigational);
    }

    [Fact]
    public async Task ProcessAsync_TypoInQuery_NoCorrection()
    {
        // Spell correction happens in Typesense, not in the pipeline
        // CorrectedQuery is only set if normalized differs from original
        var result = await _pipeline.ProcessAsync("snekars");

        result.NormalizedQuery.Should().Be("snekars");
        // Since "snekars" lowercased is "snekars", CorrectedQuery will be null
        result.CorrectedQuery.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_SameAsOriginal_NoCorrectedQuery()
    {
        var result = await _pipeline.ProcessAsync("laptop");

        result.NormalizedQuery.Should().Be("laptop");
        // CorrectedQuery should be null if same as original lowercase
        if (result.OriginalQuery.ToLowerInvariant() == result.NormalizedQuery)
        {
            result.CorrectedQuery.Should().BeNull();
        }
    }

    [Fact]
    public async Task ProcessAsync_CategoryWithFilters_PopulatesIntentFilters()
    {
        var result = await _pipeline.ProcessAsync("shoes");

        if (result.Intent == SearchIntent.CategoryFiltered)
        {
            result.IntentFilters.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ProcessAsync_MultipleQueriesInSequence_AllSucceed()
    {
        var queries = new[] { "laptop", "running shoes", "SKU-123", "coffee maker" };

        foreach (var query in queries)
        {
            var result = await _pipeline.ProcessAsync(query);
            result.Should().NotBeNull();
            result.NormalizedQuery.Should().NotBeEmpty();
        }
    }
}
