using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Ranking;

/// <summary>
/// Phase 1 Ranking Engine: Computes FinalScore for product candidates using a weighted formula.
/// Formula: 0.40×TextScore + 0.20×Availability + 0.20×Rating + 0.20×Popularity
/// Business boosts: ×1.3 if featured, ×0.2 if out_of_stock
/// </summary>
public sealed class RankingEngine(IConfiguration config, ILogger<RankingEngine> logger)
{
    // Phase 1 weights: Text (40%), Availability (20%), Rating (20%), Popularity (20%)
    // Weights sum to 1.0 and are configurable for future Phase 2 tuning
    private double TextW => config.GetValue("Ranking:TextScoreWeight", 0.40);
    private double AvailW => config.GetValue("Ranking:AvailabilityWeight", 0.20);
    private double RatingW => config.GetValue("Ranking:RatingWeight", 0.20);
    private double PopularityW => config.GetValue("Ranking:PopularityWeight", 0.20);

    // Business boost multipliers
    private const double FeaturedBoost = 1.3;
    private const double OutOfStockPenalty = 0.2;

    /// <summary>
    /// Computes the final ranking score for a product document.
    /// </summary>
    /// <param name="doc">Product document with ranking signals</param>
    /// <param name="rawTextScore">Raw BM25 text score from Typesense (0-100 scale)</param>
    /// <returns>Final score in [0, 130] range after normalization and boosts (can exceed 100 with featured boost)</returns>
    public double ComputeScore(ProductDocument doc, double rawTextScore)
    {
        // Normalize all signals to [0, 1] range
        var text = NormalizeTextScore(rawTextScore);
        var availability = ComputeAvailabilityScore(doc.StockStatus);
        var rating = NormalizeRating(doc.Rating);
        var popularity = NormalizePopularity(doc.PopularityScore);

        // Phase 1 weighted formula: sum to 1.0
        var score = (TextW * text) + (AvailW * availability)
                  + (RatingW * rating) + (PopularityW * popularity);

        // Apply business boosts (can push score above 1.0 for featured products)
        score = ApplyBusinessBoosts(score, doc);

        // Ensure score is non-negative (can exceed 100 with featured boost)
        score = Math.Max(0.0, score);

        // Scale to [0, 100+] for consistent API output
        return Math.Round(score * 100, 4);
    }

    /// <summary>
    /// Normalizes BM25 text score from Typesense (0-100) to [0, 1] range.
    /// </summary>
    private static double NormalizeTextScore(double rawScore)
        => Math.Min(1.0, rawScore / 100.0);

    /// <summary>
    /// Computes availability score from stock status.
    /// Phase 1: 1.0 = in_stock, 0.0 = out_of_stock
    /// </summary>
    private static double ComputeAvailabilityScore(string stockStatus)
        => stockStatus switch
        {
            SearchConstants.StockStatus.InStock => 1.0,
            SearchConstants.StockStatus.LowStock => 0.5, // Partial availability
            _ => 0.0 // out_of_stock
        };

    /// <summary>
    /// Normalizes 5-point rating scale to [0, 1] range.
    /// Handles zero rating (no reviews) gracefully.
    /// </summary>
    private static double NormalizeRating(float rating)
        => Math.Clamp(rating / 5.0, 0.0, 1.0);

    /// <summary>
    /// Normalizes popularity score using min-max normalization.
    /// Phase 1: Simple clamping to [0, 1] (assumes popularity already normalized).
    /// Phase 2+: Can use global min/max from PostgreSQL for true min-max normalization.
    /// </summary>
    private static double NormalizePopularity(float popularityScore)
        => Math.Clamp(popularityScore, 0.0f, 1.0f);

    /// <summary>
    /// Applies business boosts/penalties based on product attributes.
    /// Phase 1: Featured products (×1.3), Out-of-stock products (×0.2)
    /// </summary>
    private static double ApplyBusinessBoosts(double score, ProductDocument doc)
    {
        // Featured products get a significant boost
        if (doc.IsFeatured)
            score *= FeaturedBoost;

        // Out-of-stock products get heavy penalty (almost zero score)
        if (doc.StockStatus == SearchConstants.StockStatus.OutOfStock)
            score *= OutOfStockPenalty;

        return score;
    }
}
