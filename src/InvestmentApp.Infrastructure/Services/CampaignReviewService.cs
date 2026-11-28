using InvestmentApp.Application.Common;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Infrastructure.Services;

public class CampaignReviewService : ICampaignReviewService
{
    /// <summary>Không có sự kiện quyền (tiện cho test và nơi không có dữ liệu sự kiện).</summary>
    public CampaignReviewData CalculateMetrics(TradePlan plan, IEnumerable<Trade> linkedTrades)
        => CalculateMetrics(plan, linkedTrades, Array.Empty<CorporateAction>());

    /// <summary>
    /// Chỉ tính phần ĐÃ CHỐT. Giá vốn = bình quân (gồm phí, thuế mua, đã chia theo số lượng SAU điều chỉnh sự kiện quyền)
    /// × số đã bán; cổ tức tiền mặt cộng theo quyền hưởng của riêng kế hoạch tại ngày GDKHQ. Vị thế do
    /// <see cref="PositionBuilder"/> dựng từ giao dịch của kế hoạch nên hai kế hoạch cùng mã không cộng trùng.
    /// R = PnL đã chốt (gồm cổ tức) ÷ (1R × số đã bán ÷ tổng số lượng đã điều chỉnh).
    /// Phần còn giữ không phải lỗ cũng không phải lãi: nó chỉ hiện ở <c>OpenQuantity</c>.
    /// </summary>
    public CampaignReviewData CalculateMetrics(
        TradePlan plan, IEnumerable<Trade> linkedTrades, IEnumerable<CorporateAction> corporateActions)
    {
        var trades = linkedTrades.ToList();

        var buys = trades.Where(t => t.TradeType == TradeType.BUY).ToList();
        var sells = trades.Where(t => t.TradeType == TradeType.SELL).ToList();

        var totalInvested = buys.Sum(t => t.Quantity * t.Price + t.Fee + t.Tax);
        var totalReturned = sells.Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);
        var totalFees = trades.Sum(t => t.Fee + t.Tax);

        var lastTradeDate = trades.Count > 0 ? trades.Max(t => t.TradeDate.Date) : DateTime.MinValue;
        var asOf = lastTradeDate > DateTime.UtcNow.Date ? lastTradeDate : DateTime.UtcNow.Date;
        var position = PositionBuilder
            .Build(trades, corporateActions, asOf)
            .FirstOrDefault(p => string.Equals(p.Symbol, plan.Symbol, StringComparison.OrdinalIgnoreCase));

        var openQuantity = position?.TotalQuantity ?? 0m;
        var dividendIncome = (position?.DividendNet ?? 0m) + (position?.PendingDividend ?? 0m);
        var realizedCost = totalInvested - (position?.TotalCost ?? 0m);
        var pnlAmount = Math.Round((position?.RealizedPnL ?? 0m) + dividendIncome, 2);
        var pnlPercent = realizedCost > 0 ? pnlAmount / realizedCost * 100m : 0m;

        var soldQuantity = sells.Sum(t => t.Quantity);
        var totalAdjusted = openQuantity + soldQuantity;
        var riskFraction = totalAdjusted > 0 ? soldQuantity / totalAdjusted : 1m;

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

        var realized = RMath.ResolveRealizedR(plan, pnlAmount, riskFraction);

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
            ReviewedAt = DateTime.UtcNow,
            RealizedR = realized.R,
            RiskBasis = realized.Basis,
            OpenQuantity = openQuantity,
            DividendIncome = Math.Round(dividendIncome, 2),
            RealizedCost = Math.Round(realizedCost, 2)
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
