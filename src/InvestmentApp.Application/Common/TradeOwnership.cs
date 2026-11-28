using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Trade không có UserId — quyền sở hữu đi qua danh mục chứa nó. Mọi chỗ nhận một TradeId từ
/// request rồi liên kết/đổi trade đó phải qua đây TRƯỚC khi ghi bất cứ thứ gì.
/// </summary>
internal static class TradeOwnership
{
    /// <summary>
    /// Nạp trade theo id; trả null nếu không có id hoặc không tìm thấy (giữ nguyên hành vi cũ của
    /// từng handler). Ném <see cref="UnauthorizedAccessException"/> nếu trade nằm trong danh mục
    /// không thuộc <paramref name="userId"/>.
    /// </summary>
    public static async Task<Trade?> LoadOwnedAsync(
        string? tradeId, string userId,
        ITradeRepository tradeRepository, IPortfolioRepository portfolioRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tradeId)) return null;

        var trade = await tradeRepository.GetByIdAsync(tradeId, cancellationToken);
        if (trade == null) return null;

        var portfolio = await portfolioRepository.GetByIdAsync(trade.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != userId)
            throw new UnauthorizedAccessException("Not authorized to link this trade");

        return trade;
    }

    /// <summary>
    /// Các trade gắn với plan (qua TradeId/TradeIds và qua TradePlanId), chỉ giữ những trade
    /// thuộc danh mục của <paramref name="userId"/>. Dữ liệu cũ có thể đã trỏ sang trade của
    /// người khác; đường đọc không được trả số liệu của chúng.
    /// </summary>
    public static async Task<List<Trade>> CollectOwnedLinkedTradesAsync(
        TradePlan plan, string userId,
        ITradeRepository tradeRepository, IPortfolioRepository portfolioRepository,
        CancellationToken cancellationToken)
    {
        var ownedPortfolioIds = (await portfolioRepository.GetByUserIdAsync(userId, cancellationToken))
            .Select(p => p.Id).ToHashSet();

        var tradeIds = new HashSet<string>();
        if (plan.TradeId != null) tradeIds.Add(plan.TradeId);
        if (plan.TradeIds != null) tradeIds.UnionWith(plan.TradeIds);

        var trades = new List<Trade>();
        foreach (var tradeId in tradeIds)
        {
            var trade = await tradeRepository.GetByIdAsync(tradeId, cancellationToken);
            if (trade != null && ownedPortfolioIds.Contains(trade.PortfolioId)) trades.Add(trade);
        }

        var byPlanId = await tradeRepository.GetByTradePlanIdAsync(plan.Id, cancellationToken);
        foreach (var trade in byPlanId)
        {
            if (ownedPortfolioIds.Contains(trade.PortfolioId) && !trades.Any(t => t.Id == trade.Id))
                trades.Add(trade);
        }

        return trades;
    }

    /// <summary>
    /// Sự kiện quyền của đúng các (danh mục, mã) có trong <paramref name="trades"/>. Trade đã qua
    /// <see cref="CollectOwnedLinkedTradesAsync"/> nên danh mục đều của người gọi; không đọc thêm danh mục nào khác.
    /// </summary>
    public static async Task<List<CorporateAction>> LoadCorporateActionsAsync(
        IEnumerable<Trade> trades, ICorporateActionRepository corporateActionRepository,
        CancellationToken cancellationToken)
    {
        var actions = new List<CorporateAction>();
        var keys = trades
            .Select(t => (t.PortfolioId, Symbol: t.Symbol.ToUpperInvariant()))
            .Distinct();
        foreach (var (portfolioId, symbol) in keys)
            actions.AddRange(await corporateActionRepository.GetByPortfolioIdAndSymbolAsync(portfolioId, symbol, cancellationToken));
        return actions;
    }
}
