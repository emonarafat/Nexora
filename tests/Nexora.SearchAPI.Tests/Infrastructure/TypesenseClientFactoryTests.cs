using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Nexora.SearchAPI.Infrastructure;

namespace Nexora.SearchAPI.Tests.Infrastructure;

public class TypesenseClientFactoryTests
{
    [Fact]
    public void CreateClient_DefaultConfig_ReturnsNonNullClient()
    {
        // Arrange: minimal config (factory should not connect until a method is called)
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Typesense:Host", "localhost" },
                { "Typesense:Port", "8108" },
                { "Typesense:Protocol", "http" },
                { "Typesense:ApiKey", "test-key" }
            })
            .Build();

        var factory = new TypesenseClientFactory(config);

        // Act
        var client = factory.CreateClient();

        // Assert: client is created (no connection established yet)
        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateClient_MissingConfig_FallsBackToDefaults()
    {
        // Arrange: empty config – all values use defaults (localhost:8108 http, empty key)
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var factory = new TypesenseClientFactory(config);

        // Act + Assert: no exception, returns non-null client
        var act = () => factory.CreateClient();
        act.Should().NotThrow();
        act().Should().NotBeNull();
    }
}
