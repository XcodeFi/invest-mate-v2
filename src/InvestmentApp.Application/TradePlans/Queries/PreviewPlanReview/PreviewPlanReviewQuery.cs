using InvestmentApp.Application.Common;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.PreviewPlanReview;

public class PreviewPlanReviewQuery : IRequest<CampaignReviewDto>
{
    public string PlanId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class PreviewPlanReviewQueryHandler : IRequestHandler<PreviewPlanReviewQuery, CampaignReviewDto>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICampaignReviewService _reviewService;

    public PreviewPlanReviewQueryHandler(
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

    public async Task<CampaignReviewDto> Handle(PreviewPlanReviewQuery request, CancellationToken cancellationToken)
    {
        // Kế hoạch không có và kế hoạch của người khác cho cùng một phản hồi (KeyNotFound → 404), giống GetScenarioHistory
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.UserId != request.UserId)
            throw new KeyNotFoundException($"Trade plan {request.PlanId} not found");

        // Chỉ tính giao dịch thuộc danh mục của người gọi (cùng logic với ReviewTradePlanCommand)
        var trades = await TradeOwnership.CollectOwnedLinkedTradesAsync(
            plan, request.UserId, _tradeRepository, _portfolioRepository, cancellationToken);

        var metrics = _reviewService.CalculateMetrics(plan, trades);
        return ReviewTradePlanCommandHandler.MapToDto(metrics);
    }
}
