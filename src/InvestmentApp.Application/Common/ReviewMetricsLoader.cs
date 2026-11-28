using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Một đường duy nhất tính số liệu review (review, xem trước, tính lại) để ba nơi không lệch nhau:
/// trade gắn vào kế hoạch (chỉ danh mục của người gọi) + sự kiện quyền → <see cref="ICampaignReviewService"/>,
/// rồi kiểm chéo giữa các kế hoạch để gắn cờ <see cref="ReviewFlags.SellsInOtherPlan"/>.
/// </summary>
internal static class ReviewMetricsLoader
{
    public static async Task<CampaignReviewData> ComputeAsync(
        TradePlan plan, string userId,
        ITradeRepository tradeRepository, IPortfolioRepository portfolioRepository,
        ICorporateActionRepository corporateActionRepository, ICampaignReviewService reviewService,
        CancellationToken cancellationToken)
    {
        var linked = await TradeOwnership.CollectOwnedLinkedTradesAsync(plan, userId, tradeRepository, portfolioRepository, cancellationToken);
        var actions = await TradeOwnership.LoadCorporateActionsAsync(linked, corporateActionRepository, cancellationToken);
        var data = reviewService.CalculateMetrics(plan, linked, actions);

        if (await HasSellsInOtherPlanAsync(plan, linked, data, tradeRepository, cancellationToken))
        {
            data.Flags ??= new List<string>();
            if (!data.Flags.Contains(ReviewFlags.SellsInOtherPlan)) data.Flags.Add(ReviewFlags.SellsInOtherPlan);
        }
        return data;
    }

    /// <summary>
    /// Kế hoạch chỉ có lệnh MUA và còn cổ phiếu mở, trong khi cùng danh mục + cùng mã có lệnh BÁN gắn vào một kế hoạch khác:
    /// số "còn mở" chỉ đúng theo lệnh mua đã gắn, lệnh bán có thể đang nằm ở kế hoạch kia.
    /// </summary>
    private static async Task<bool> HasSellsInOtherPlanAsync(
        TradePlan plan, IReadOnlyList<Trade> linked, CampaignReviewData data,
        ITradeRepository tradeRepository, CancellationToken cancellationToken)
    {
        if (data.OpenQuantity is not > 0m) return false;
        if (linked.Any(t => t.TradeType == TradeType.SELL)) return false;

        foreach (var (portfolioId, symbol) in linked.Select(t => (t.PortfolioId, Symbol: t.Symbol.ToUpperInvariant())).Distinct())
        {
            var sameSymbol = await tradeRepository.GetByPortfolioIdAndSymbolAsync(portfolioId, symbol, cancellationToken);
            if (sameSymbol.Any(t => t.TradeType == TradeType.SELL && t.TradePlanId != null && t.TradePlanId != plan.Id))
                return true;
        }
        return false;
    }
}
