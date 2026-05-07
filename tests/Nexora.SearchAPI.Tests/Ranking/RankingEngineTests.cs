using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SearchAPI.Ranking;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Xunit;

namespace Nexora.SearchAPI.Tests.Ranking;

public class RankingEngineTests
{
    private readonly RankingEngine _sut = new(
        new ConfigurationBuilder().Build(),
        NullLogger<RankingEngine>.Instance);

    private static ProductDocument Doc(
        string stock = SearchConstants.StockStatus.InStock,
        float rating = 4.5f, int ratingCount = 100,
        bool featured = false, float ctr = 0.05f, float conv = 0.02f) => new()
    {
        Id = "x", Title = "T", StockStatus = stock,
        Rating = rating, RatingCount = ratingCount,
        IsFeatured = featured, Ctr30d = ctr, ConversionRate30d = conv, IsActive = true
    };

    [Fact]
    public void Score_InStock_Positive() => _sut.ComputeScore(Doc(), 80).Should().BeGreaterThan(0);

    [Fact]
    public void Score_OutOfStock_IsZero()
        => _sut.ComputeScore(Doc(stock: SearchConstants.StockStatus.OutOfStock), 80).Should().Be(0);

    [Fact]
    public void Score_Featured_HigherThanNonFeatured()
    {
        var f = _sut.ComputeScore(Doc(featured: true), 80);
        var n = _sut.ComputeScore(Doc(featured: false), 80);
        f.Should().BeGreaterThan(n);
    }

    [Fact]
    public void Score_LowStock_LowerThanInStock()
    {
        var i = _sut.ComputeScore(Doc(stock: SearchConstants.StockStatus.InStock), 80);
        var l = _sut.ComputeScore(Doc(stock: SearchConstants.StockStatus.LowStock), 80);
        i.Should().BeGreaterThan(l);
    }

    [Fact]
    public void Score_BadRating_Lower()
    {
        var good = _sut.ComputeScore(Doc(rating: 4.5f, ratingCount: 100), 80);
        var bad = _sut.ComputeScore(Doc(rating: 2.0f, ratingCount: 100), 80);
        good.Should().BeGreaterThan(bad);
    }

    [Fact]
    public void Score_HighCtr_Higher()
    {
        var h = _sut.ComputeScore(Doc(ctr: 0.20f), 80);
        var l = _sut.ComputeScore(Doc(ctr: 0.01f), 80);
        h.Should().BeGreaterThan(l);
    }
}
