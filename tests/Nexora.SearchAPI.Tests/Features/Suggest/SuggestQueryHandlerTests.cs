using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SearchAPI.Features.Suggest;
using Nexora.SearchAPI.Infrastructure;
using Nexora.SearchAPI.Pipeline;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Tests.Features.Suggest;

public class SuggestQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_CacheMissThenHit_ComputesAndCachesSuggestions()
    {
        var cache = new FakeValkeyCache();
        var searchClient = new FakeSuggestSearchClient([
            new SuggestionItem { Text = "B", Category = "Shoes", PopularityScore = 10 },
            new SuggestionItem { Text = "A", Category = "Shoes", PopularityScore = 2 }
        ]);
        var sut = CreateSut(cache, searchClient);

        var first = await sut.HandleAsync(new SuggestRequest { Query = "ru", Limit = 8 }, CancellationToken.None);
        var second = await sut.HandleAsync(new SuggestRequest { Query = "ru", Limit = 8 }, CancellationToken.None);

        first.CacheHit.Should().BeFalse();
        first.Suggestions.Select(x => x.Text).Should().ContainInOrder("B", "A");
        second.CacheHit.Should().BeTrue();
        second.Suggestions.Should().BeEquivalentTo(first.Suggestions);
        searchClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_DifferentCategory_UsesDifferentCacheEntries()
    {
        var cache = new FakeValkeyCache();
        var searchClient = new FakeSuggestSearchClient([new SuggestionItem { Text = "Item", PopularityScore = 1 }]);
        var sut = CreateSut(cache, searchClient);

        await sut.HandleAsync(new SuggestRequest { Query = "ru", Category = "Shoes" }, CancellationToken.None);
        await sut.HandleAsync(new SuggestRequest { Query = "ru", Category = "Electronics" }, CancellationToken.None);

        searchClient.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateCacheAsync_BumpsVersion_AndForcesRecompute()
    {
        var cache = new FakeValkeyCache();
        var searchClient = new FakeSuggestSearchClient([new SuggestionItem { Text = "Item", PopularityScore = 1 }]);
        var sut = CreateSut(cache, searchClient);

        await sut.HandleAsync(new SuggestRequest { Query = "ru" }, CancellationToken.None);
        await sut.InvalidateCacheAsync(CancellationToken.None);
        var response = await sut.HandleAsync(new SuggestRequest { Query = "ru" }, CancellationToken.None);

        response.CacheHit.Should().BeFalse();
        searchClient.CallCount.Should().Be(2);
    }

    private static SuggestQueryHandler CreateSut(FakeValkeyCache cache, FakeSuggestSearchClient searchClient)
        => new(
            searchClient,
            cache,
            new QuerySanitizer(),
            new QueryNormalizer(),
            NullLogger<SuggestQueryHandler>.Instance);

    private sealed class FakeSuggestSearchClient(IReadOnlyList<SuggestionItem> results) : ISuggestSearchClient
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<SuggestionItem>> SearchAsync(string prefix, int limit, string? category, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(results);
        }
    }

    private sealed class FakeValkeyCache : IValkeyCache
    {
        private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult(_store.TryGetValue(key, out var value) ? value as T : null);

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }
}
