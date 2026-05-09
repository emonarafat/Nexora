using FluentAssertions;
using Nexora.Shared.Constants;

namespace Nexora.SearchAPI.Tests.Infrastructure;

public class SearchConstantsTests
{
    [Fact]
    public void CacheKeys_Search_ProducesConsistentKey()
    {
        var key1 = SearchConstants.CacheKeys.Search("abc", "def", 1);
        var key2 = SearchConstants.CacheKeys.Search("abc", "def", 1);

        key1.Should().Be(key2);
        key1.Should().StartWith("search::");
    }

    [Fact]
    public void CacheKeys_Search_DifferentPageProducesDifferentKey()
    {
        var key1 = SearchConstants.CacheKeys.Search("abc", "def", 1);
        var key2 = SearchConstants.CacheKeys.Search("abc", "def", 2);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void CacheKeys_Suggest_ProducesConsistentKey()
    {
        var key1 = SearchConstants.CacheKeys.Suggest("prefix");
        var key2 = SearchConstants.CacheKeys.Suggest("prefix");

        key1.Should().Be(key2);
        key1.Should().StartWith("suggest::");
    }

    [Fact]
    public void CacheKeys_Suggest_DifferentPrefixProducesDifferentKey()
    {
        var key1 = SearchConstants.CacheKeys.Suggest("abc");
        var key2 = SearchConstants.CacheKeys.Suggest("xyz");

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void CacheKeys_SuggestVersionConstant_IsNonEmpty()
    {
        SearchConstants.CacheKeys.SuggestVersion.Should().NotBeNullOrWhiteSpace();
    }
}
