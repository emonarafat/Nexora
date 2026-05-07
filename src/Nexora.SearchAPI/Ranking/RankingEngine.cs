using Nexora.Shared.Constants;
using Nexora.Shared.DTOs;

namespace Nexora.SearchAPI.Ranking;

public sealed class RankingEngine(IConfiguration config, ILogger<RankingEngine> logger)
{
    private double TextW => config.GetValue("Ranking:TextScoreWeight", 0.40);
    private double CtrW => config.GetValue("Ranking:CtrWeight", 0.20);
    private double ConvW => config.GetValue("Ranking:ConversionWeight", 0.15);
    private double AvailW => config.GetValue("Ranking:AvailabilityWeight", 0.10);
    private double RatingW => config.GetValue("Ranking:RatingWeight", 0.10);
    private double PersonW => config.GetValue("Ranking:PersonalizationWeight", 0.05);

    public double ComputeScore(ProductDocument doc, double rawTextScore)
    {
        var text = Math.Min(1.0, rawTextScore / 100.0);
        var ctr = Math.Min(1.0, (double)doc.Ctr30d);
        var conv = Math.Min(1.0, (double)doc.ConversionRate30d);
        var avail = doc.StockStatus switch
        {
            SearchConstants.StockStatus.InStock => 1.0,
            SearchConstants.StockStatus.LowStock => 0.5,
            _ => 0.0
        };
        var rating = doc.Rating / 5.0;

        var score = (TextW * text) + (CtrW * ctr) + (ConvW * conv)
                  + (AvailW * avail) + (RatingW * rating) + (PersonW * 0.0);

        if (doc.IsFeatured) score *= 1.3;
        if (doc.StockStatus == SearchConstants.StockStatus.LowStock) score *= 0.9;
        if (doc.StockStatus == SearchConstants.StockStatus.OutOfStock) score = 0;
        if (doc.Rating < 2.5f && doc.RatingCount > 20) score *= 0.7;

        return Math.Round(score * 100, 4);
    }
}
