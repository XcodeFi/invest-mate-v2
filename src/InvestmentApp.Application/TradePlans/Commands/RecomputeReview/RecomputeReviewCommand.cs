using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.RecomputeReview;

/// <summary>
/// Người dùng bấm "Tính lại review" trên kế hoạch ĐÃ review (sau khi sửa công thức hoặc gắn thêm lệnh còn thiếu).
/// Tính lại số liệu bằng service hiện tại, giữ bài học, số cũ vào <c>ReviewData.Legacy</c> (chỉ lần đầu). Không bao giờ chạy hàng loạt.
/// </summary>
public class RecomputeReviewCommand : IRequest<CampaignReviewDto>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class RecomputeReviewCommandHandler : IRequestHandler<RecomputeReviewCommand, CampaignReviewDto>
{
    private readonly ITradePlanRepository _plans;
    private readonly ITradeRepository _trades;
    private readonly IPortfolioRepository _portfolios;
    private readonly ICorporateActionRepository _actions;
    private readonly ICampaignReviewService _reviewService;

    public RecomputeReviewCommandHandler(
        ITradePlanRepository plans, ITradeRepository trades, IPortfolioRepository portfolios,
        ICorporateActionRepository actions, ICampaignReviewService reviewService)
    {
        _plans = plans;
        _trades = trades;
        _portfolios = portfolios;
        _actions = actions;
        _reviewService = reviewService;
    }

    public async Task<CampaignReviewDto> Handle(RecomputeReviewCommand request, CancellationToken cancellationToken)
    {
        // Không có và của người khác cho cùng một phản hồi (KeyNotFound → 404), giống các handler khác (#186)
        var plan = await _plans.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.UserId != request.UserId)
            throw new KeyNotFoundException($"Trade plan {request.PlanId} not found");

        if (plan.Status != TradePlanStatus.Reviewed || plan.ReviewData == null)
            throw new ArgumentException("Chỉ kế hoạch đã review mới tính lại được", nameof(request));

        var fresh = await ReviewMetricsLoader.ComputeAsync(plan, request.UserId, _trades, _portfolios, _actions, _reviewService, cancellationToken);
        plan.RecomputeReview(fresh);
        await _plans.UpdateAsync(plan, cancellationToken);

        return ReviewTradePlanCommandHandler.MapToDto(plan.ReviewData!);
    }
}
