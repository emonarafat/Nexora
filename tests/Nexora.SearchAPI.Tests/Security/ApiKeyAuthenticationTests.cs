using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexora.SearchAPI.Security;
using Xunit;

namespace Nexora.SearchAPI.Tests.Security;

/// <summary>
/// Phase 1.10: API Key Authentication middleware tests
/// </summary>
public class ApiKeyAuthenticationTests
{
    [Fact]
    public async Task Middleware_WithoutApiKey_Returns401()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "test_valid_key_123"
        });
        var middleware = CreateMiddleware(config);
        var context = CreateHttpContext("/api/v1/search");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Middleware_WithInvalidApiKey_Returns401()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "test_valid_key_123"
        });
        var middleware = CreateMiddleware(config);
        var context = CreateHttpContext("/api/v1/search");
        context.Request.Headers["X-API-Key"] = "invalid_key";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Middleware_WithValidApiKey_CallsNext()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "test_valid_key_123"
        });
        var nextCalled = false;
        var middleware = CreateMiddleware(config, () => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/api/v1/search");
        context.Request.Headers["X-API-Key"] = "test_valid_key_123";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200); // Default status
    }

    [Fact]
    public async Task Middleware_HealthEndpoint_SkipsAuthentication()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "test_valid_key_123"
        });
        var nextCalled = false;
        var middleware = CreateMiddleware(config, () => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/health");
        // No API key provided

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_MetricsEndpoint_SkipsAuthentication()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "test_valid_key_123"
        });
        var nextCalled = false;
        var middleware = CreateMiddleware(config, () => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext("/metrics");
        // No API key provided

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_EmptyApiKey_Returns401()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "test_valid_key_123"
        });
        var middleware = CreateMiddleware(config);
        var context = CreateHttpContext("/api/v1/search");
        context.Request.Headers["X-API-Key"] = "";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Middleware_WhitespaceApiKey_Returns401()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "test_valid_key_123"
        });
        var middleware = CreateMiddleware(config);
        var context = CreateHttpContext("/api/v1/search");
        context.Request.Headers["X-API-Key"] = "   ";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Middleware_MultipleValidKeys_AcceptsAny()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "key1",
            ["ApiKeys:ValidKeys:1"] = "key2"
        });
        var nextCalled1 = false;
        var nextCalled2 = false;
        var middleware1 = CreateMiddleware(config, () => { nextCalled1 = true; return Task.CompletedTask; });
        var middleware2 = CreateMiddleware(config, () => { nextCalled2 = true; return Task.CompletedTask; });
        var context1 = CreateHttpContext("/api/v1/search");
        context1.Request.Headers["X-API-Key"] = "key1";
        var context2 = CreateHttpContext("/api/v1/search");
        context2.Request.Headers["X-API-Key"] = "key2";

        // Act
        await middleware1.InvokeAsync(context1);
        await middleware2.InvokeAsync(context2);

        // Assert
        nextCalled1.Should().BeTrue();
        nextCalled2.Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_CaseSensitiveKey_RejectsWrongCase()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ApiKeys:ValidKeys:0"] = "TestKey123"
        });
        var middleware = CreateMiddleware(config);
        var context = CreateHttpContext("/api/v1/search");
        context.Request.Headers["X-API-Key"] = "testkey123"; // wrong case

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ApiKeyAuthenticationMiddleware CreateMiddleware(
        IConfiguration config,
        Func<Task>? nextDelegate = null)
    {
        var next = new RequestDelegate(_ => nextDelegate?.Invoke() ?? Task.CompletedTask);
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ApiKeyAuthenticationMiddleware>();
        return new ApiKeyAuthenticationMiddleware(next, config, logger);
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
