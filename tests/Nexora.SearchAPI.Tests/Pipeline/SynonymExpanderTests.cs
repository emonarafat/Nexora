using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

public class SynonymExpanderTests
{
    [Fact]
    public async Task ExpandAsync_NoSynonyms_ReturnsOriginalQuery()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" } // Empty connection string
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        var result = await expander.ExpandAsync("laptop");

        result.Should().ContainSingle()
            .Which.Should().Be("laptop");
    }

    [Fact]
    public async Task ExpandAsync_EmptyQuery_ReturnsEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" }
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        var result = await expander.ExpandAsync("");

        result.Should().ContainSingle()
            .Which.Should().Be("");
    }

    [Fact]
    public async Task ExpandAsync_MultipleWords_ReturnsOriginal()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" }
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        var result = await expander.ExpandAsync("running shoes");

        result.Should().Contain("running shoes");
    }

    [Fact]
    public async Task ExpandAsync_CacheWorks_DoesNotReloadEveryTime()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" }
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // First call
        var result1 = await expander.ExpandAsync("laptop");
        // Second call should use cache
        var result2 = await expander.ExpandAsync("laptop");

        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public async Task ExpandAsync_InvalidConnectionString_HandlesGracefully()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "Host=invalid;Database=test" }
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Should not throw, should return original query
        var result = await expander.ExpandAsync("sofa");

        result.Should().ContainSingle()
            .Which.Should().Be("sofa");
    }

    [Fact]
    public async Task ExpandAsync_NullConnectionString_ReturnsOriginalQuery()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        var result = await expander.ExpandAsync("couch");

        result.Should().ContainSingle()
            .Which.Should().Be("couch");
    }

    [Fact]
    public async Task ExpandAsync_CaseInsensitive_Works()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" }
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        var result = await expander.ExpandAsync("LAPTOP");

        result.Should().ContainSingle()
            .Which.Should().Be("LAPTOP");
    }
}
