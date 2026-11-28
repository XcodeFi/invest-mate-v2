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
    /// <summary>Mọi chiến dịch đã review, gồm cả review còn mở/bán vượt (tiền đã chốt của chúng vẫn nằm trong tổng tích lũy và đường lũy kế).</summary>
    public int TotalCampaigns { get; set; }
    public int WinningCampaigns { get; set; }
    public int LosingCampaigns { get; set; }

    /// <summary>
    /// Số review loại khỏi tỉ lệ thắng, lãi/lỗ trung bình và tốt nhất/tệ nhất: còn cổ phiếu chưa bán (OpenQuantity &gt; 0) hoặc bán vượt số mua
    /// đã gắn (OversoldQuantity &gt; 0), vì P&amp;L của chúng chỉ là phần đã chốt (có thể bằng 0) chứ không phải kết quả cả chiến dịch.
    /// </summary>
    public int ExcludedOpenReviews { get; set; }

    // Chỉ tính trên review ĐÃ ĐÓNG. Null (không phải 0) khi không có review nào đã đóng.
    public decimal? WinRate { get; set; }
    public decimal? AveragePnLPercent { get; set; }
    public decimal? AveragePnLPerDay { get; set; }
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

        // Review ĐÃ ĐÓNG: không còn cổ phiếu chưa bán và không bán vượt số mua đã gắn. Review cũ (hai trường null) tính là đã đóng.
        var closed = reviewed.Where(p => !(p.ReviewData!.OpenQuantity is > 0m) && !(p.ReviewData.OversoldQuantity is > 0m)).ToList();
        var winning = closed.Count(p => p.ReviewData!.PnLAmount > 0);
        var losing = closed.Count - winning;

        var best = closed.OrderByDescending(p => p.ReviewData!.PnLPerDay).FirstOrDefault();
        var worst = closed.OrderBy(p => p.ReviewData!.PnLPerDay).FirstOrDefault();

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
            ExcludedOpenReviews = reviewed.Count - closed.Count,
            WinRate = closed.Count > 0 ? Math.Round((decimal)winning / closed.Count * 100m, 1) : null,
            AveragePnLPercent = closed.Count > 0 ? Math.Round(closed.Average(p => p.ReviewData!.PnLPercent), 2) : null,
            AveragePnLPerDay = closed.Count > 0 ? Math.Round(closed.Average(p => p.ReviewData!.PnLPerDay), 0) : null,
            TotalAccumulatedPnL = reviewed.Sum(p => p.ReviewData!.PnLAmount),
            AverageHoldingDays = Math.Round(reviewed.Average(p => (decimal)p.ReviewData!.HoldingDays), 1),
            BestCampaign = best == null ? null : MapToSummary(best),
            WorstCampaign = worst == null ? null : MapToSummary(worst),
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
