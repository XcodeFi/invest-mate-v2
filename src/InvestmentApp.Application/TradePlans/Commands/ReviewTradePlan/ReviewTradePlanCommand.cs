using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
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
    /// <summary>PnL ròng / 1R ban đầu (null nếu không tính được).</summary>
    public decimal? RealizedR { get; set; }
    public string? RiskBasis { get; set; }
}

public class ReviewTradePlanCommandHandler : IRequestHandler<ReviewTradePlanCommand, CampaignReviewDto>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICampaignReviewService _reviewService;

    public ReviewTradePlanCommandHandler(
        ITradePlanRepository tradePlanRepository,
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        ICampaignReviewService reviewService)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _reviewService = reviewService;
    }

    public async Task<CampaignReviewDto> Handle(ReviewTradePlanCommand request, CancellationToken cancellationToken)
    {
        // Kế hoạch không có và kế hoạch của người khác cho cùng một phản hồi (KeyNotFound → 404)
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.UserId != request.UserId)
            throw new KeyNotFoundException($"Trade plan {request.PlanId} not found");

        // Collect all linked trades: from plan's TradeIds + query by TradePlanId
        var linkedTrades = await TradeOwnership.CollectOwnedLinkedTradesAsync(plan, request.UserId, _tradeRepository, _portfolioRepository, cancellationToken);

        // Auto-calculate metrics
        var reviewData = _reviewService.CalculateMetrics(plan, linkedTrades);
        reviewData.LessonsLearned = request.LessonsLearned;

        // Mark plan as reviewed with data
        plan.MarkReviewed(reviewData);
        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);

        return MapToDto(reviewData);
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
        ReviewedAt = data.ReviewedAt,
        RealizedR = data.RealizedR,
        RiskBasis = data.RiskBasis
    };
}
