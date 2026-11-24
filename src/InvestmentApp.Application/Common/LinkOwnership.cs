using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Các id liên kết (danh mục, chiến lược, kế hoạch) nhận từ request chỉ được lưu khi thuộc
/// tài khoản đang gọi. Id rỗng (null) vẫn hợp lệ — liên kết là tùy chọn. Id không tồn tại giữ
/// nguyên hành vi cũ (được lưu, không có dữ liệu của ai để lộ); chỉ chặn thực thể có chủ khác.
/// Giao dịch đi qua <see cref="TradeOwnership"/> vì Trade không có UserId.
/// </summary>
internal static class LinkOwnership
{
    public static async Task EnsurePortfolioAsync(
        string? portfolioId, string userId, IPortfolioRepository portfolios, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(portfolioId)) return;
        var portfolio = await portfolios.GetByIdAsync(portfolioId, ct);
        if (portfolio != null && portfolio.UserId != userId)
            throw new UnauthorizedAccessException("Not authorized to link this portfolio");
    }

    public static async Task EnsureStrategyAsync(
        string? strategyId, string userId, IStrategyRepository strategies, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(strategyId)) return;
        var strategy = await strategies.GetByIdAsync(strategyId, ct);
        if (strategy != null && strategy.UserId != userId)
            throw new UnauthorizedAccessException("Not authorized to link this strategy");
    }

    public static async Task EnsureTradePlanAsync(
        string? tradePlanId, string userId, ITradePlanRepository plans, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tradePlanId)) return;
        var plan = await plans.GetByIdAsync(tradePlanId, ct);
        if (plan != null && plan.UserId != userId)
            throw new UnauthorizedAccessException("Not authorized to link this trade plan");
    }
}
