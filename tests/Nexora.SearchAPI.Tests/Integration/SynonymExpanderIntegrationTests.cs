using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Integration;

/// <summary>
/// Integration tests for SynonymExpander with PostgreSQL Testcontainer.
/// Validates synonym expansion with real database queries.
/// </summary>
[Trait("Category", "Integration")]
public class SynonymExpanderIntegrationTests : PostgreSqlIntegrationTestBase
{
    protected override async Task InitializeDatabaseSchemaAsync()
    {
        await CreateSynonymsTableAsync();
        await SeedTestSynonyms();
    }

    private async Task SeedTestSynonyms()
    {
        await SeedSynonymsAsync(
            ("couch", new[] { "sofa", "settee", "divan" }),
            ("tv", new[] { "television", "telly" }),
            ("laptop", new[] { "notebook", "portable computer" }),
            ("sneakers", new[] { "trainers", "kicks", "tennis shoes" }),
            ("phone", new[] { "smartphone", "mobile", "cell phone" })
        );
    }

    private IConfiguration GetConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", ConnectionString }
            })
            .Build();
    }

    [Fact]
    public async Task ExpandAsync_WithDatabaseSynonyms_ExpandsTerm()
    {
        // Arrange
        var config = GetConfiguration();
        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Act
        var result = await expander.ExpandAsync("couch");

        // Assert
        result.Should().Contain("sofa");
        result.Should().Contain("settee");
        result.Should().Contain("couch"); // Original term included
    }

    [Fact]
    public async Task ExpandAsync_MultipleTermsInSequence_AllExpanded()
    {
        // Arrange
        var config = GetConfiguration();
        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Act
        var result1 = await expander.ExpandAsync("tv");
        var result2 = await expander.ExpandAsync("laptop");
        var result3 = await expander.ExpandAsync("sneakers");

        // Assert
        result1.Should().Contain("television");
        result2.Should().Contain("notebook");
        result3.Should().Contain("trainers");
    }

    [Fact]
    public async Task ExpandAsync_CachingBehavior_SecondCallFaster()
    {
        // Arrange
        var config = GetConfiguration();
        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Act: First call (cache miss)
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var result1 = await expander.ExpandAsync("phone");
        sw1.Stop();

        // Act: Second call (cache hit)
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var result2 = await expander.ExpandAsync("phone");
        sw2.Stop();

        // Assert: Results should be identical
        result1.Should().BeEquivalentTo(result2);

        // Assert: Second call should be faster (cache hit)
        // Note: This is a soft assertion as timing can vary in CI
        if (sw1.ElapsedMilliseconds > 5)
        {
            sw2.ElapsedMilliseconds.Should().BeLessThan(sw1.ElapsedMilliseconds,
                "cached lookup should be faster than database query");
        }
    }

    [Fact]
    public async Task ExpandAsync_NoSynonymsFound_ReturnsOriginalTerm()
    {
        // Arrange
        var config = GetConfiguration();
        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Act
        var result = await expander.ExpandAsync("unknownterm");

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("unknownterm");
    }

    [Fact]
    public async Task ExpandAsync_CaseInsensitive_FindsSynonyms()
    {
        // Arrange
        var config = GetConfiguration();
        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Act
        var resultLower = await expander.ExpandAsync("couch");
        var resultUpper = await expander.ExpandAsync("COUCH");
        var resultMixed = await expander.ExpandAsync("Couch");

        // Assert: All should expand to same synonyms
        resultLower.Should().Contain("sofa");
        resultUpper.Should().Contain("sofa");
        resultMixed.Should().Contain("sofa");
    }

    [Fact]
    public async Task ExpandAsync_ConcurrentRequests_AllSucceed()
    {
        // Arrange
        var config = GetConfiguration();
        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);
        var queries = new[] { "couch", "tv", "laptop", "sneakers", "phone" };

        // Act: Execute concurrent expansions
        var tasks = queries.Select(q => expander.ExpandAsync(q)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert: All results should be valid
        results[0].Should().Contain("sofa"); // couch
        results[1].Should().Contain("television"); // tv
        results[2].Should().Contain("notebook"); // laptop
        results[3].Should().Contain("trainers"); // sneakers
        results[4].Should().Contain("smartphone"); // phone
    }
}
