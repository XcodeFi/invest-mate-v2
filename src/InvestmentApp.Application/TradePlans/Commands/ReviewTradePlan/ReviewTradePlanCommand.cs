using System.Text.Json.Serialization;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;

public class ReviewTradePlanCommand : IRequest<CampaignReviewDto>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? LessonsLearned { get; set; }
}

public class CampaignReviewDto
{
    public decimal PnLAmount { get; set; }
    public decimal PnLPercent { get; set; }
    public int HoldingDays { get; set; }
    public decimal PnLPerDay { get; set; }
    public decimal AnnualizedReturnPercent { get; set; }
    public decimal TargetAchievementPercent { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalReturned { get; set; }
    public decimal TotalFees { get; set; }
    public string? LessonsLearned { get; set; }
    public DateTime ReviewedAt { get; set; }
}

public class ReviewTradePlanCommandHandler : IRequestHandler<ReviewTradePlanCommand, CampaignReviewDto>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly ICampaignReviewService _reviewService;

    public ReviewTradePlanCommandHandler(
        ITradePlanRepository tradePlanRepository,
        ITradeRepository tradeRepository,
        ICampaignReviewService reviewService)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
        _reviewService = reviewService;
    }

    public async Task<CampaignReviewDto> Handle(ReviewTradePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new Exception($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to review this trade plan");

        // Collect all linked trades: from plan's TradeIds + query by TradePlanId
        var linkedTrades = await CollectLinkedTrades(plan, cancellationToken);

        // Auto-calculate metrics
        var reviewData = _reviewService.CalculateMetrics(plan, linkedTrades);
        reviewData.LessonsLearned = request.LessonsLearned;

        // Mark plan as reviewed with data
        plan.MarkReviewed(reviewData);
        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);

        return MapToDto(reviewData);
    }

    private async Task<List<Trade>> CollectLinkedTrades(TradePlan plan, CancellationToken ct)
    {
        var tradeIds = new HashSet<string>();
        if (plan.TradeId != null) tradeIds.Add(plan.TradeId);
        if (plan.TradeIds != null) tradeIds.UnionWith(plan.TradeIds);

        var trades = new List<Trade>();

        // Get trades by their IDs (from plan's linked list)
        foreach (var tradeId in tradeIds)
        {
            var trade = await _tradeRepository.GetByIdAsync(tradeId, ct);
            if (trade != null) trades.Add(trade);
        }

        // Also query trades that link back to this plan via TradePlanId
        var byPlanId = await _tradeRepository.GetByTradePlanIdAsync(plan.Id, ct);
        foreach (var trade in byPlanId)
        {
            if (!trades.Any(t => t.Id == trade.Id))
                trades.Add(trade);
        }

        return trades;
    }

    internal static CampaignReviewDto MapToDto(CampaignReviewData data) => new()
    {
        PnLAmount = data.PnLAmount,
        PnLPercent = data.PnLPercent,
        HoldingDays = data.HoldingDays,
        PnLPerDay = data.PnLPerDay,
        AnnualizedReturnPercent = data.AnnualizedReturnPercent,
        TargetAchievementPercent = data.TargetAchievementPercent,
        TotalInvested = data.TotalInvested,
        TotalReturned = data.TotalReturned,
        TotalFees = data.TotalFees,
        LessonsLearned = data.LessonsLearned,
        ReviewedAt = data.ReviewedAt
    };
}
