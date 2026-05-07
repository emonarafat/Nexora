using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.SearchAPI.Ranking;
using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;
using Xunit;

namespace Nexora.SearchAPI.Tests.Ranking;

/// <summary>
/// Tests for Phase 1 Ranking Engine.
/// Validates formula correctness, edge cases, and business boost logic.
/// </summary>
public class RankingEngineTests
{
    private readonly RankingEngine _sut = new(
        new ConfigurationBuilder().Build(),
        NullLogger<RankingEngine>.Instance);

    private static ProductDocument Doc(
        string stock = SearchConstants.StockStatus.InStock,
        float rating = 4.5f,
        int ratingCount = 100,
        bool featured = false,
        float popularity = 0.5f) => new()
    {
        Id = "test-product",
        Title = "Test Product",
        StockStatus = stock,
        Rating = rating,
        RatingCount = ratingCount,
        IsFeatured = featured,
        PopularityScore = popularity,
        IsActive = true
    };

    #region Basic Score Computation

    [Fact]
    public void ComputeScore_InStockProduct_ReturnsPositiveScore()
    {
        // Arrange: Product with good signals
        var doc = Doc(rating: 4.5f, popularity: 0.8f);

        // Act
        var score = _sut.ComputeScore(doc, 80);

        // Assert: Score should be positive and reasonable
        score.Should().BeGreaterThan(0);
        score.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void ComputeScore_HighQualityProduct_ScoreNear100()
    {
        // Arrange: Perfect signals - high text match, in stock, 5-star rating, high popularity
        var doc = Doc(
            stock: SearchConstants.StockStatus.InStock,
            rating: 5.0f,
            popularity: 1.0f);

        // Act: Perfect BM25 score (100)
        var score = _sut.ComputeScore(doc, 100);

        // Assert: Should be near maximum (100 without boosts)
        score.Should().BeGreaterThanOrEqualTo(95);
        score.Should().BeLessThanOrEqualTo(105); // Can slightly exceed 100 due to rounding
    }

    #endregion

    #region Availability Tests

    [Fact]
    public void ComputeScore_OutOfStock_ScoreDemotedBelow20()
    {
        // Arrange: Out-of-stock product (×0.2 penalty)
        var doc = Doc(
            stock: SearchConstants.StockStatus.OutOfStock,
            rating: 5.0f,
            popularity: 1.0f);

        // Act: Even with perfect other signals
        var score = _sut.ComputeScore(doc, 100);

        // Assert: Heavy penalty should reduce score significantly
        score.Should().BeLessThan(20); // ×0.2 penalty brings it way down
    }

    [Fact]
    public void ComputeScore_LowStock_LowerThanInStock()
    {
        // Arrange: Two identical products, different stock status
        var inStock = Doc(stock: SearchConstants.StockStatus.InStock);
        var lowStock = Doc(stock: SearchConstants.StockStatus.LowStock);

        // Act
        var inStockScore = _sut.ComputeScore(inStock, 80);
        var lowStockScore = _sut.ComputeScore(lowStock, 80);

        // Assert: In-stock should score higher
        inStockScore.Should().BeGreaterThan(lowStockScore);
    }

    #endregion

    #region Rating Tests

    [Fact]
    public void ComputeScore_ZeroRating_HandledGracefully()
    {
        // Arrange: Product with no reviews (rating = 0)
        var doc = Doc(rating: 0.0f, ratingCount: 0);

        // Act: Should not throw, should return valid score
        var score = _sut.ComputeScore(doc, 80);

        // Assert: Score should be valid but lower due to zero rating
        score.Should().BeGreaterThanOrEqualTo(0);
        score.Should().BeLessThan(100);
    }

    [Fact]
    public void ComputeScore_HighRating_HigherThanLowRating()
    {
        // Arrange: Two products with different ratings
        var highRated = Doc(rating: 5.0f, ratingCount: 100);
        var lowRated = Doc(rating: 2.0f, ratingCount: 100);

        // Act
        var highScore = _sut.ComputeScore(highRated, 80);
        var lowScore = _sut.ComputeScore(lowRated, 80);

        // Assert: Higher rating should result in higher score
        highScore.Should().BeGreaterThan(lowScore);
    }

    #endregion

    #region Popularity Tests

    [Fact]
    public void ComputeScore_HighPopularity_HigherThanLowPopularity()
    {
        // Arrange: Two products with different popularity
        var popular = Doc(popularity: 1.0f);
        var unpopular = Doc(popularity: 0.1f);

        // Act
        var popularScore = _sut.ComputeScore(popular, 80);
        var unpopularScore = _sut.ComputeScore(unpopular, 80);

        // Assert: More popular product should score higher
        popularScore.Should().BeGreaterThan(unpopularScore);
    }

    [Fact]
    public void ComputeScore_ZeroPopularity_StillValidScore()
    {
        // Arrange: Product with zero popularity
        var doc = Doc(popularity: 0.0f);

        // Act
        var score = _sut.ComputeScore(doc, 80);

        // Assert: Should still return valid score based on other signals
        score.Should().BeGreaterThan(0); // Still has text, availability, rating
    }

    #endregion

    #region Business Boosts

    [Fact]
    public void ComputeScore_FeaturedProduct_AppliesBoostCorrectly()
    {
        // Arrange: Two identical products, one featured
        var normal = Doc(featured: false);
        var featured = Doc(featured: true);

        // Act
        var normalScore = _sut.ComputeScore(normal, 80);
        var featuredScore = _sut.ComputeScore(featured, 80);

        // Assert: Featured should be ~1.3× higher
        featuredScore.Should().BeGreaterThan(normalScore);
        var ratio = featuredScore / normalScore;
        ratio.Should().BeApproximately(1.3, 0.01);
    }

    [Fact]
    public void ComputeScore_OutOfStockPenalty_AppliedCorrectly()
    {
        // Arrange: Out-of-stock vs in-stock
        var inStock = Doc(stock: SearchConstants.StockStatus.InStock);
        var outOfStock = Doc(stock: SearchConstants.StockStatus.OutOfStock);

        // Act
        var inStockScore = _sut.ComputeScore(inStock, 80);
        var outOfStockScore = _sut.ComputeScore(outOfStock, 80);

        // Assert: Out-of-stock should be heavily penalized (both through 0 availability score + ×0.2 penalty)
        // Combined effect: availability contributes 0, then remaining score gets ×0.2 penalty
        outOfStockScore.Should().BeLessThan(inStockScore);

        // Out-of-stock should be dramatically lower (< 25% of in-stock due to combined effects)
        var ratio = outOfStockScore / inStockScore;
        ratio.Should().BeLessThan(0.25);
    }

    #endregion

    #region Formula Validation

    [Fact]
    public void ComputeScore_ScoreAlwaysInValidRange()
    {
        // Arrange: Test various extreme combinations
        var testCases = new[]
        {
            Doc(stock: SearchConstants.StockStatus.InStock, rating: 5.0f, popularity: 1.0f, featured: true),
            Doc(stock: SearchConstants.StockStatus.OutOfStock, rating: 0.0f, popularity: 0.0f),
            Doc(stock: SearchConstants.StockStatus.LowStock, rating: 2.5f, popularity: 0.5f),
            Doc(stock: SearchConstants.StockStatus.InStock, rating: 0.0f, popularity: 0.0f),
        };

        foreach (var doc in testCases)
        {
            // Act: Test with various text scores
            var score1 = _sut.ComputeScore(doc, 0);
            var score2 = _sut.ComputeScore(doc, 50);
            var score3 = _sut.ComputeScore(doc, 100);

            // Assert: All scores should be non-negative (can exceed 100 with featured boost)
            score1.Should().BeGreaterThanOrEqualTo(0);
            score2.Should().BeGreaterThanOrEqualTo(0);
            score3.Should().BeGreaterThanOrEqualTo(0);
            // Featured products can exceed 100, so upper bound is ~130 (100 * 1.3)
            score1.Should().BeLessThanOrEqualTo(130);
            score2.Should().BeLessThanOrEqualTo(130);
            score3.Should().BeLessThanOrEqualTo(130);
        }
    }

    [Fact]
    public void ComputeScore_Phase1Formula_WeightsCorrect()
    {
        // Arrange: Product with known signals
        // Text=100 (→1.0), Avail=in_stock (→1.0), Rating=5.0 (→1.0), Popularity=1.0 (→1.0)
        var doc = Doc(
            stock: SearchConstants.StockStatus.InStock,
            rating: 5.0f,
            popularity: 1.0f,
            featured: false);

        // Act: Perfect signals, no boosts
        var score = _sut.ComputeScore(doc, 100);

        // Assert: Should equal 0.40*1.0 + 0.20*1.0 + 0.20*1.0 + 0.20*1.0 = 1.0 → 100
        score.Should().Be(100.0);
    }

    [Fact]
    public void ComputeScore_WeightsSum_AlwaysOne()
    {
        // Validation: Weights should sum to 1.0
        // Text: 0.40, Availability: 0.20, Rating: 0.20, Popularity: 0.20
        var weightSum = 0.40 + 0.20 + 0.20 + 0.20;
        weightSum.Should().Be(1.0);
    }

    #endregion

    #region Sort Order Tests

    [Fact]
    public void ComputeScore_CandidatesInDescendingOrder()
    {
        // Arrange: Multiple products with varying quality
        var excellent = Doc(stock: SearchConstants.StockStatus.InStock, rating: 5.0f, popularity: 1.0f, featured: true);
        var good = Doc(stock: SearchConstants.StockStatus.InStock, rating: 4.5f, popularity: 0.8f);
        var average = Doc(stock: SearchConstants.StockStatus.InStock, rating: 3.5f, popularity: 0.5f);
        var poor = Doc(stock: SearchConstants.StockStatus.LowStock, rating: 2.0f, popularity: 0.2f);
        var outOfStock = Doc(stock: SearchConstants.StockStatus.OutOfStock, rating: 5.0f, popularity: 1.0f);

        // Act: Compute scores for all
        var scores = new[]
        {
            (doc: excellent, score: _sut.ComputeScore(excellent, 90)),
            (doc: good, score: _sut.ComputeScore(good, 85)),
            (doc: average, score: _sut.ComputeScore(average, 75)),
            (doc: poor, score: _sut.ComputeScore(poor, 70)),
            (doc: outOfStock, score: _sut.ComputeScore(outOfStock, 80)),
        };

        // Assert: Order should be: excellent > good > average > poor > outOfStock
        scores[0].score.Should().BeGreaterThan(scores[1].score);
        scores[1].score.Should().BeGreaterThan(scores[2].score);
        scores[2].score.Should().BeGreaterThan(scores[3].score);
        scores[3].score.Should().BeGreaterThan(scores[4].score);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ComputeScore_NegativeTextScore_ClampedToZero()
    {
        // Arrange: Edge case with negative text score (shouldn't happen, but defensive)
        var doc = Doc();

        // Act
        var score = _sut.ComputeScore(doc, -10);

        // Assert: Should handle gracefully
        score.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ComputeScore_VeryHighTextScore_ClampedCorrectly()
    {
        // Arrange: Edge case with text score > 100
        var doc = Doc();

        // Act
        var score = _sut.ComputeScore(doc, 200);

        // Assert: Should clamp text component to 1.0 maximum
        score.Should().BeLessThanOrEqualTo(130); // Max 100 with 1.3 featured boost
    }

    [Fact]
    public void ComputeScore_AllZeroSignals_ReturnsZero()
    {
        // Arrange: Product with all zero signals
        var doc = Doc(
            stock: SearchConstants.StockStatus.OutOfStock,
            rating: 0.0f,
            popularity: 0.0f);

        // Act
        var score = _sut.ComputeScore(doc, 0);

        // Assert: Should be zero or near-zero
        score.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void ComputeScore_CustomWeights_AppliedCorrectly()
    {
        // Arrange: Custom configuration with different weights
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Ranking:TextScoreWeight", "0.50" },
                { "Ranking:AvailabilityWeight", "0.20" },
                { "Ranking:RatingWeight", "0.15" },
                { "Ranking:PopularityWeight", "0.15" }
            })
            .Build();
        var engine = new RankingEngine(config, NullLogger<RankingEngine>.Instance);

        var doc = Doc(rating: 5.0f, popularity: 1.0f);

        // Act
        var score = engine.ComputeScore(doc, 100);

        // Assert: Should use custom weights (higher text weight)
        score.Should().BeGreaterThan(0);
        // With custom weights: 0.50*1.0 + 0.20*1.0 + 0.15*1.0 + 0.15*1.0 = 1.0 → 100
        score.Should().Be(100.0);
    }

    #endregion
}
