using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class CampaignReviewService : ICampaignReviewService
{
    public CampaignReviewData CalculateMetrics(TradePlan plan, IEnumerable<Trade> linkedTrades)
    {
        var trades = linkedTrades.ToList();

        var buys = trades.Where(t => t.TradeType == TradeType.BUY).ToList();
        var sells = trades.Where(t => t.TradeType == TradeType.SELL).ToList();

        var totalInvested = buys.Sum(t => t.Quantity * t.Price + t.Fee + t.Tax);
        var totalReturned = sells.Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);
        var totalFees = trades.Sum(t => t.Fee + t.Tax);

        var pnlAmount = totalReturned - totalInvested;
        var pnlPercent = totalInvested > 0 ? pnlAmount / totalInvested * 100m : 0m;

        var dates = trades.Select(t => t.TradeDate).OrderBy(d => d).ToList();
        var holdingDays = dates.Count >= 2 ? Math.Max(1, (dates.Last() - dates.First()).Days) : 1;

        var pnlPerDay = pnlAmount / holdingDays;

        var annualizedReturn = CalculateAnnualizedReturn(pnlPercent, holdingDays);

        var plannedTargetPercent = plan.EntryPrice > 0
            ? (plan.Target - plan.EntryPrice) / plan.EntryPrice * 100m
            : 0m;
        var targetAchievement = plannedTargetPercent != 0
            ? pnlPercent / plannedTargetPercent * 100m
            : 0m;

        return new CampaignReviewData
        {
            PnLAmount = pnlAmount,
            PnLPercent = Math.Round(pnlPercent, 2),
            HoldingDays = holdingDays,
            PnLPerDay = Math.Round(pnlPerDay, 0),
            AnnualizedReturnPercent = Math.Round(annualizedReturn, 2),
            TargetAchievementPercent = Math.Round(targetAchievement, 2),
            TotalInvested = totalInvested,
            TotalReturned = totalReturned,
            TotalFees = totalFees,
            ReviewedAt = DateTime.UtcNow
        };
    }

    private static decimal CalculateAnnualizedReturn(decimal pnlPercent, int holdingDays)
    {
        if (holdingDays < 7)
            return 0m; // Too short to annualize meaningfully

        var rate = 1m + pnlPercent / 100m;
        if (rate <= 0)
            return -100m; // Total loss cap

        var exponent = 365.0 / holdingDays;
        var annualized = (decimal)Math.Pow((double)rate, exponent) - 1m;
        return annualized * 100m;
    }
}
