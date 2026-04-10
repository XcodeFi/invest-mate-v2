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
    private readonly ICampaignReviewService _reviewService;

    public PreviewPlanReviewQueryHandler(
        ITradePlanRepository tradePlanRepository,
        ITradeRepository tradeRepository,
        ICampaignReviewService reviewService)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
        _reviewService = reviewService;
    }

    public async Task<CampaignReviewDto> Handle(PreviewPlanReviewQuery request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new Exception($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized");

        // Collect linked trades (same logic as ReviewTradePlanCommand)
        var tradeIds = new HashSet<string>();
        if (plan.TradeId != null) tradeIds.Add(plan.TradeId);
        if (plan.TradeIds != null) tradeIds.UnionWith(plan.TradeIds);

        var trades = new List<Trade>();
        foreach (var tradeId in tradeIds)
        {
            var trade = await _tradeRepository.GetByIdAsync(tradeId, cancellationToken);
            if (trade != null) trades.Add(trade);
        }
        var byPlanId = await _tradeRepository.GetByTradePlanIdAsync(plan.Id, cancellationToken);
        foreach (var trade in byPlanId)
        {
            if (!trades.Any(t => t.Id == trade.Id))
                trades.Add(trade);
        }

        var metrics = _reviewService.CalculateMetrics(plan, trades);
        return ReviewTradePlanCommandHandler.MapToDto(metrics);
    }
}
