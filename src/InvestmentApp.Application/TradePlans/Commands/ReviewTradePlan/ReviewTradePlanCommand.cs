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
    /// <summary>Số CP còn giữ lúc review. Lớn hơn 0: PnL/R chỉ tính phần đã bán và bị loại khỏi thống kê R.</summary>
    public decimal? OpenQuantity { get; set; }
    /// <summary>Cổ tức tiền mặt ròng theo quyền hưởng, đã cộng vào PnL.</summary>
    public decimal? DividendIncome { get; set; }
    /// <summary>Giá vốn của phần đã bán (mẫu số của PnL%).</summary>
    public decimal? RealizedCost { get; set; }
    /// <summary>Số CP bán vượt số mua đã gắn. Lớn hơn 0: PnL chỉ phần khớp, R = null, loại khỏi thống kê.</summary>
    public decimal? OversoldQuantity { get; set; }
    /// <summary>"thieu-lenh-mua-lien-ket" | "lenh-ban-o-ke-hoach-khac".</summary>
    public List<string>? Flags { get; set; }
    /// <summary>Số liệu trước lần tính lại đầu tiên (null nếu chưa tính lại).</summary>
    public LegacyReviewDto? Legacy { get; set; }
}

public class LegacyReviewDto
{
    public decimal PnLAmount { get; set; }
    public decimal PnLPercent { get; set; }
    public decimal? RealizedR { get; set; }
    public DateTime RecomputedAt { get; set; }
}

public class ReviewTradePlanCommandHandler : IRequestHandler<ReviewTradePlanCommand, CampaignReviewDto>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ICampaignReviewService _reviewService;
    private readonly ICorporateActionRepository _corporateActionRepository;

    public ReviewTradePlanCommandHandler(
        ITradePlanRepository tradePlanRepository,
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        ICampaignReviewService reviewService,
        ICorporateActionRepository corporateActionRepository)
    {
        _corporateActionRepository = corporateActionRepository;
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

        // Trade gắn vào kế hoạch + sự kiện quyền → số liệu tự tính (cùng một đường với xem trước và tính lại)
        var reviewData = await ReviewMetricsLoader.ComputeAsync(plan, request.UserId, _tradeRepository, _portfolioRepository,
            _corporateActionRepository, _reviewService, cancellationToken);
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
        RiskBasis = data.RiskBasis,
        OpenQuantity = data.OpenQuantity,
        DividendIncome = data.DividendIncome,
        RealizedCost = data.RealizedCost,
        OversoldQuantity = data.OversoldQuantity,
        Flags = data.Flags,
        Legacy = data.Legacy == null ? null : new LegacyReviewDto
        {
            PnLAmount = data.Legacy.PnLAmount, PnLPercent = data.Legacy.PnLPercent,
            RealizedR = data.Legacy.RealizedR, RecomputedAt = data.Legacy.RecomputedAt
        }
    };
}
