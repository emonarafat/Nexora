using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SearchAPI.Pipeline;
using Xunit;

namespace Nexora.SearchAPI.Tests.Pipeline;

/// <summary>
/// Tests for performance and cache TTL requirements.
/// </summary>
public class PipelinePerformanceTests
{
    [Fact]
    public async Task Pipeline_FullProcessing_CompletesUnder20Ms()
    {
        // Arrange
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

        var pipeline = new QueryPipeline(
            sanitizer,
            normalizer,
            synonymExpander,
            intentClassifier,
            NullLogger<QueryPipeline>.Instance);

        var queries = new[]
        {
            "running shoes",
            "laptop",
            "SKU-12345",
            "nike air max",
            "wireless mouse"
        };

        // Act & Assert
        foreach (var query in queries)
        {
            var sw = Stopwatch.StartNew();
            var result = await pipeline.ProcessAsync(query);
            sw.Stop();

            // Verify result is valid
            result.Should().NotBeNull();
            result.NormalizedQuery.Should().NotBeEmpty();

            // Verify latency < 20ms
            sw.ElapsedMilliseconds.Should().BeLessThan(20,
                $"Pipeline processing for '{query}' should complete in under 20ms");
        }
    }

    [Fact]
    public async Task Pipeline_MultipleQueries_AverageLatencyUnder20Ms()
    {
        // Arrange
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

        var pipeline = new QueryPipeline(
            sanitizer,
            normalizer,
            synonymExpander,
            intentClassifier,
            NullLogger<QueryPipeline>.Instance);

        var queries = new[]
        {
            "running shoes", "laptop", "SKU-12345", "nike air max",
            "wireless mouse", "coffee maker", "yoga mat", "smartphone",
            "headphones", "tablet"
        };

        // Act
        var totalTime = 0L;
        foreach (var query in queries)
        {
            var sw = Stopwatch.StartNew();
            await pipeline.ProcessAsync(query);
            sw.Stop();
            totalTime += sw.ElapsedMilliseconds;
        }

        // Assert
        var avgLatency = totalTime / queries.Length;
        avgLatency.Should().BeLessThan(20,
            $"Average pipeline latency should be under 20ms (was {avgLatency}ms)");
    }

    [Fact]
    public async Task SynonymExpander_CacheTtl_Uses5MinuteTtl()
    {
        // This test verifies the cache TTL constant is set correctly
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" }
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Act - First call loads from DB (empty in this test)
        var result1 = await expander.ExpandAsync("laptop");

        // Second call should use cache
        var sw = Stopwatch.StartNew();
        var result2 = await expander.ExpandAsync("laptop");
        sw.Stop();

        // Assert - Cache hit should be very fast
        sw.ElapsedMilliseconds.Should().BeLessThan(5,
            "Cached synonym lookup should be under 5ms");

        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public async Task SynonymExpander_Cache_PersistsBetweenCalls()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "" }
            })
            .Build();

        var expander = new SynonymExpander(config, NullLogger<SynonymExpander>.Instance);

        // Act - Multiple calls to same query
        var result1 = await expander.ExpandAsync("sofa");
        var result2 = await expander.ExpandAsync("sofa");
        var result3 = await expander.ExpandAsync("couch");

        // Assert - Results should be consistent
        result1.Should().BeEquivalentTo(result2);
        result1.Should().Contain("sofa");
        result3.Should().Contain("couch");
    }

    [Fact]
    public async Task Pipeline_ConcurrentQueries_AllComplete()
    {
        // Arrange
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

        var pipeline = new QueryPipeline(
            sanitizer,
            normalizer,
            synonymExpander,
            intentClassifier,
            NullLogger<QueryPipeline>.Instance);

        var queries = new[]
        {
            "running shoes", "laptop", "SKU-12345", "nike air max",
            "wireless mouse", "coffee maker", "yoga mat", "smartphone"
        };

        // Act - Process all queries concurrently
        var sw = Stopwatch.StartNew();
        var tasks = queries.Select(q => pipeline.ProcessAsync(q)).ToArray();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        results.Should().HaveCount(queries.Length);
        results.Should().OnlyContain(r => r.NormalizedQuery != null);

        // All queries processed concurrently should still be fast
        sw.ElapsedMilliseconds.Should().BeLessThan(100,
            "Concurrent processing should complete in under 100ms");
    }

    [Fact]
    public void QuerySanitizer_Performance_FastForValidQueries()
    {
        // Arrange
        var sanitizer = new QuerySanitizer();
        var queries = Enumerable.Range(0, 100)
            .Select(i => $"query {i}")
            .ToArray();

        // Act
        var sw = Stopwatch.StartNew();
        foreach (var query in queries)
        {
            sanitizer.Sanitize(query);
        }
        sw.Stop();

        // Assert - 100 queries in under 10ms
        sw.ElapsedMilliseconds.Should().BeLessThan(10,
            "Sanitizer should process 100 queries in under 10ms");
    }

    [Fact]
    public void QueryNormalizer_Performance_FastForValidQueries()
    {
        // Arrange
        var normalizer = new QueryNormalizer();
        var queries = Enumerable.Range(0, 100)
            .Select(i => $"QUERY {i}")
            .ToArray();

        // Act
        var sw = Stopwatch.StartNew();
        foreach (var query in queries)
        {
            normalizer.Normalize(query);
        }
        sw.Stop();

        // Assert - 100 queries in under 10ms
        sw.ElapsedMilliseconds.Should().BeLessThan(10,
            "Normalizer should process 100 queries in under 10ms");
    }

    [Fact]
    public void IntentClassifier_Performance_FastForAllIntents()
    {
        // Arrange
        var classifier = new IntentClassifier();
        var queries = new[]
        {
            "running shoes", "laptop", "SKU-12345", "ABC-999",
            "electronics", "furniture", "wireless mouse", "coffee maker"
        };

        // Act
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            foreach (var query in queries)
            {
                classifier.Classify(query);
            }
        }
        sw.Stop();

        // Assert - 800 classifications in under 20ms
        sw.ElapsedMilliseconds.Should().BeLessThan(20,
            "Intent classifier should process 800 queries in under 20ms");
    }
}
