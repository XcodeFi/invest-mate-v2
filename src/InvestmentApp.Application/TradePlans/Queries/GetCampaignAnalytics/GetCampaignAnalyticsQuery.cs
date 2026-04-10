using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetCampaignAnalytics;

public class GetCampaignAnalyticsQuery : IRequest<CampaignAnalyticsDto>
{
    public string UserId { get; set; } = null!;
    public string? TimeHorizon { get; set; }
}

public class CampaignAnalyticsDto
{
    public int TotalCampaigns { get; set; }
    public int WinningCampaigns { get; set; }
    public int LosingCampaigns { get; set; }
    public decimal WinRate { get; set; }
    public decimal AveragePnLPercent { get; set; }
    public decimal AveragePnLPerDay { get; set; }
    public decimal TotalAccumulatedPnL { get; set; }
    public decimal AverageHoldingDays { get; set; }
    public CampaignSummaryDto? BestCampaign { get; set; }
    public CampaignSummaryDto? WorstCampaign { get; set; }
    public List<CampaignTrendDto> Trend { get; set; } = new();
}

public class CampaignSummaryDto
{
    public string PlanId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public decimal PnLAmount { get; set; }
    public decimal PnLPercent { get; set; }
    public decimal PnLPerDay { get; set; }
    public int HoldingDays { get; set; }
    public string? TimeHorizon { get; set; }
    public DateTime ReviewedAt { get; set; }
}

public class CampaignTrendDto
{
    public string PlanId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public DateTime ReviewedAt { get; set; }
    public decimal PnLPercent { get; set; }
    public decimal CumulativePnL { get; set; }
}

public class GetCampaignAnalyticsQueryHandler : IRequestHandler<GetCampaignAnalyticsQuery, CampaignAnalyticsDto>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public GetCampaignAnalyticsQueryHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<CampaignAnalyticsDto> Handle(GetCampaignAnalyticsQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<TradePlan> plans;

        if (request.TimeHorizon != null && Enum.TryParse<TimeHorizon>(request.TimeHorizon, ignoreCase: true, out var horizon))
            plans = await _tradePlanRepository.GetReviewedByUserIdAndTimeHorizonAsync(request.UserId, horizon, cancellationToken);
        else
            plans = await _tradePlanRepository.GetReviewedByUserIdAsync(request.UserId, cancellationToken);

        var reviewed = plans.Where(p => p.ReviewData != null).ToList();

        if (reviewed.Count == 0)
            return new CampaignAnalyticsDto();

        var winning = reviewed.Count(p => p.ReviewData!.PnLAmount > 0);
        var losing = reviewed.Count - winning;

        var best = reviewed.OrderByDescending(p => p.ReviewData!.PnLPerDay).First();
        var worst = reviewed.OrderBy(p => p.ReviewData!.PnLPerDay).First();

        // Build cumulative trend
        var ordered = reviewed.OrderBy(p => p.ReviewData!.ReviewedAt).ToList();
        var cumPnL = 0m;
        var trend = ordered.Select(p =>
        {
            cumPnL += p.ReviewData!.PnLAmount;
            return new CampaignTrendDto
            {
                PlanId = p.Id,
                Symbol = p.Symbol,
                ReviewedAt = p.ReviewData.ReviewedAt,
                PnLPercent = p.ReviewData.PnLPercent,
                CumulativePnL = cumPnL
            };
        }).ToList();

        return new CampaignAnalyticsDto
        {
            TotalCampaigns = reviewed.Count,
            WinningCampaigns = winning,
            LosingCampaigns = losing,
            WinRate = Math.Round((decimal)winning / reviewed.Count * 100m, 1),
            AveragePnLPercent = Math.Round(reviewed.Average(p => p.ReviewData!.PnLPercent), 2),
            AveragePnLPerDay = Math.Round(reviewed.Average(p => p.ReviewData!.PnLPerDay), 0),
            TotalAccumulatedPnL = reviewed.Sum(p => p.ReviewData!.PnLAmount),
            AverageHoldingDays = Math.Round(reviewed.Average(p => (decimal)p.ReviewData!.HoldingDays), 1),
            BestCampaign = MapToSummary(best),
            WorstCampaign = MapToSummary(worst),
            Trend = trend
        };
    }

    private static CampaignSummaryDto MapToSummary(TradePlan plan) => new()
    {
        PlanId = plan.Id,
        Symbol = plan.Symbol,
        PnLAmount = plan.ReviewData!.PnLAmount,
        PnLPercent = plan.ReviewData.PnLPercent,
        PnLPerDay = plan.ReviewData.PnLPerDay,
        HoldingDays = plan.ReviewData.HoldingDays,
        TimeHorizon = plan.TimeHorizon?.ToString(),
        ReviewedAt = plan.ReviewData.ReviewedAt
    };
}
