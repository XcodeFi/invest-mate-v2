using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using System.ComponentModel;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetTradePlans;

// --- List Query ---

public class GetTradePlansQuery : IRequest<IEnumerable<TradePlanDto>>
{
    public string UserId { get; set; } = null!;
    public bool ActiveOnly { get; set; }
}

public class GetTradePlansQueryHandler : IRequestHandler<GetTradePlansQuery, IEnumerable<TradePlanDto>>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly IFeeCalculationService? _fees;

    public GetTradePlansQueryHandler(ITradePlanRepository tradePlanRepository, IFeeCalculationService? fees = null)
    {
        _tradePlanRepository = tradePlanRepository;
        _fees = fees;
    }

    public async Task<IEnumerable<TradePlanDto>> Handle(GetTradePlansQuery request, CancellationToken cancellationToken)
    {
        var plans = request.ActiveOnly
            ? await _tradePlanRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken)
            : await _tradePlanRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        var fee = PlanFees.Create(_fees);
        return plans.Select(p => MapToDto(p, fee));
    }

    private static PlanExpectationDto ToExpectationDto(PlanExpectation e)
        => new() { EvR = e.EvR, ImpliedWinPercent = e.ImpliedWinPercent, EvAmount = e.EvAmount };

    private static List<OutcomeScenarioDto> ToDtos(IEnumerable<OutcomeScenario> scenarios)
        => scenarios.Select(x => new OutcomeScenarioDto
            { Label = x.Label, ProbabilityPercent = x.ProbabilityPercent, ExitPrice = x.ExitPrice }).ToList();

    internal static TradePlanDto MapToDto(Domain.Entities.TradePlan p, RoundTripFee? fee = null) => new()
    {
        Id = p.Id,
        PortfolioId = p.PortfolioId,
        Symbol = p.Symbol,
        Direction = p.Direction,
        EntryPrice = p.EntryPrice,
        StopLoss = p.StopLoss,
        Target = p.Target,
        Quantity = p.Quantity,
        StrategyId = p.StrategyId,
        MarketCondition = p.MarketCondition,
        Thesis = p.Thesis,
        Notes = p.Notes,
        InvalidationCriteria = p.InvalidationCriteria?.Select(r => new InvalidationRuleDto
        {
            Trigger = r.Trigger,
            Detail = r.Detail,
            CheckDate = r.CheckDate,
            IsTriggered = r.IsTriggered,
            TriggeredAt = r.TriggeredAt
        }).ToList(),
        ExpectedReviewDate = p.ExpectedReviewDate,
        LegacyExempt = p.LegacyExempt,
        RiskPercent = p.RiskPercent,
        AccountBalance = p.AccountBalance,
        RiskRewardRatio = p.RiskRewardRatio,
        Scenarios = p.OutcomeScenarios == null ? null : ToDtos(p.OutcomeScenarios),
        Estimate = p.Estimate == null ? null : new EstimateDto
        {
            Scenarios = ToDtos(p.Estimate.Scenarios),
            EvR = p.Estimate.EvR,
            ImpliedWinProbability = p.Estimate.ImpliedWinProbability,
            FeesIncluded = p.Estimate.FeesIncluded,
            CapturedAt = p.Estimate.CapturedAt,
            Locked = p.Estimate.LockedAt != null,
            LockedAt = p.Estimate.LockedAt
        },
        Expectation = ToExpectationDto(ExpectedValue.ForPlan(p, fee)),
        EvGateReason = p.EvGateReason,
        EvGateSkipped = p.EvGateSkipped,
        EffectiveRiskReward = RMath.EffectiveRiskReward(p),
        RiskRewardComputed = RMath.ComputeRiskReward(p) != null,
        ConfidenceLevel = p.ConfidenceLevel,
        Checklist = p.Checklist?.Select(c => new ChecklistItemDto
        {
            Label = c.Label,
            Category = c.Category,
            Checked = c.Checked,
            Critical = c.Critical,
            Hint = c.Hint
        }).ToList() ?? new(),
        EntryMode = p.EntryMode?.ToString(),
        Lots = p.Lots?.Select(l => new PlanLotDto
        {
            LotNumber = l.LotNumber,
            PlannedPrice = l.PlannedPrice,
            PlannedQuantity = l.PlannedQuantity,
            AllocationPercent = l.AllocationPercent,
            Label = l.Label,
            Status = l.Status,
            ActualPrice = l.ActualPrice,
            ExecutedAt = l.ExecutedAt,
            TradeId = l.TradeId
        }).ToList(),
        ExitTargets = p.ExitTargets?.Select(e => new ExitTargetDto
        {
            Level = e.Level,
            ActionType = e.ActionType,
            Price = e.Price,
            Quantity = e.Quantity,
            PercentOfPosition = e.PercentOfPosition,
            Label = e.Label,
            IsTriggered = e.IsTriggered,
            TriggeredAt = e.TriggeredAt,
            TradeId = e.TradeId
        }).ToList(),
        StopLossHistory = p.StopLossHistory?.Select(s => new StopLossHistoryDto
        {
            OldPrice = s.OldPrice,
            NewPrice = s.NewPrice,
            Reason = s.Reason,
            ChangedAt = s.ChangedAt
        }).ToList(),
        ExitStrategyMode = p.ExitStrategyMode.ToString(),
        ScenarioNodes = p.ScenarioNodes?.Select(n => new ScenarioNodeDto
        {
            NodeId = n.NodeId,
            ParentId = n.ParentId,
            Order = n.Order,
            Label = n.Label,
            ConditionType = n.ConditionType,
            ConditionValue = n.ConditionValue,
            ConditionNote = n.ConditionNote,
            ActionType = n.ActionType,
            ActionValue = n.ActionValue,
            TrailingStopConfig = n.TrailingStopConfig != null ? new TrailingStopConfigDto
            {
                Method = n.TrailingStopConfig.Method,
                TrailValue = n.TrailingStopConfig.TrailValue,
                ActivationPrice = n.TrailingStopConfig.ActivationPrice,
                StepSize = n.TrailingStopConfig.StepSize,
                CurrentTrailingStop = n.TrailingStopConfig.CurrentTrailingStop,
                HighestPrice = n.TrailingStopConfig.HighestPrice
            } : null,
            Status = n.Status,
            TriggeredAt = n.TriggeredAt,
            TradeId = n.TradeId
        }).ToList(),
        TimeHorizon = p.TimeHorizon?.ToString(),
        ReviewData = p.ReviewData != null ? new CampaignReviewDataDto
        {
            PnLAmount = p.ReviewData.PnLAmount,
            PnLPercent = p.ReviewData.PnLPercent,
            HoldingDays = p.ReviewData.HoldingDays,
            PnLPerDay = p.ReviewData.PnLPerDay,
            AnnualizedReturnPercent = p.ReviewData.AnnualizedReturnPercent,
            TargetAchievementPercent = p.ReviewData.TargetAchievementPercent,
            TotalInvested = p.ReviewData.TotalInvested,
            TotalReturned = p.ReviewData.TotalReturned,
            TotalFees = p.ReviewData.TotalFees,
            LessonsLearned = p.ReviewData.LessonsLearned,
            ReviewedAt = p.ReviewData.ReviewedAt,
            RealizedR = RMath.ResolveRealizedR(p).R,
            RiskBasis = RMath.ResolveRealizedR(p).Basis,
            OpenQuantity = p.ReviewData.OpenQuantity,
            DividendIncome = p.ReviewData.DividendIncome,
            RealizedCost = p.ReviewData.RealizedCost,
            OversoldQuantity = p.ReviewData.OversoldQuantity,
            Flags = p.ReviewData.Flags,
            Legacy = p.ReviewData.Legacy == null ? null : new LegacyReviewDto
            {
                PnLAmount = p.ReviewData.Legacy.PnLAmount, PnLPercent = p.ReviewData.Legacy.PnLPercent,
                RealizedR = p.ReviewData.Legacy.RealizedR, RecomputedAt = p.ReviewData.Legacy.RecomputedAt
            }
        } : null,
        Status = p.Status.ToString(),
        TradeId = p.TradeId,
        TradeIds = p.TradeIds,
        ExecutedAt = p.ExecutedAt,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}

// --- Get By Id Query ---

public class GetTradePlanByIdQuery : IRequest<TradePlanDto?>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class GetTradePlanByIdQueryHandler : IRequestHandler<GetTradePlanByIdQuery, TradePlanDto?>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly IFeeCalculationService? _fees;

    public GetTradePlanByIdQueryHandler(ITradePlanRepository tradePlanRepository, IFeeCalculationService? fees = null)
    {
        _tradePlanRepository = tradePlanRepository;
        _fees = fees;
    }

    public async Task<TradePlanDto?> Handle(GetTradePlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.Id, cancellationToken);
        if (plan == null || plan.UserId != request.UserId) return null;
        return GetTradePlansQueryHandler.MapToDto(plan, PlanFees.Create(_fees));
    }
}

// --- DTO ---

public class TradePlanDto
{
    public string Id { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string Symbol { get; set; } = null!;
    public string Direction { get; set; } = "Buy";
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }
    public int Quantity { get; set; }
    public string? StrategyId { get; set; }
    public string MarketCondition { get; set; } = string.Empty;
    public string? Thesis { get; set; }
    public string? Notes { get; set; }
    public List<InvalidationRuleDto>? InvalidationCriteria { get; set; }
    public DateTime? ExpectedReviewDate { get; set; }
    public bool LegacyExempt { get; set; }
    public decimal? RiskPercent { get; set; }
    public decimal? AccountBalance { get; set; }
    /// <summary>Giá trị người dùng đã gõ (giữ nguyên, không bị ghi đè).</summary>
    public decimal? RiskRewardRatio { get; set; }
    /// <summary>R:R hiệu lực khi đọc: số hệ thống tính khi đủ dữ liệu, không thì giá trị đã gõ.</summary>
    public decimal? EffectiveRiskReward { get; set; }
    /// <summary>True nếu <see cref="EffectiveRiskReward"/> là số hệ thống tính, false nếu là giá trị người dùng gõ.</summary>
    public bool RiskRewardComputed { get; set; }
    /// <summary>Bảng kịch bản kết quả hiện hành (null với kế hoạch cũ hoặc lệnh bán).</summary>
    public List<OutcomeScenarioDto>? Scenarios { get; set; }
    /// <summary>Ảnh chụp ước lượng lúc chốt kế hoạch (khoá ở lần khớp đầu).</summary>
    public EstimateDto? Estimate { get; set; }
    /// <summary>Kỳ vọng tính khi đọc từ bảng hiện hành (đã trừ phí ước tính nếu có dịch vụ phí). Các trường null khi chưa tính được.</summary>
    public PlanExpectationDto? Expectation { get; set; }
    public string? EvGateReason { get; set; }
    /// <summary>Vào lệnh mà không qua cổng kỳ vọng (EV ≤ 0 hoặc chưa ước lượng, không có lý do).</summary>
    public bool EvGateSkipped { get; set; }
    public int ConfidenceLevel { get; set; }
    public List<ChecklistItemDto> Checklist { get; set; } = new();
    public string? EntryMode { get; set; }
    public List<PlanLotDto>? Lots { get; set; }
    public List<ExitTargetDto>? ExitTargets { get; set; }
    public List<StopLossHistoryDto>? StopLossHistory { get; set; }
    public string ExitStrategyMode { get; set; } = "Simple";
    public List<ScenarioNodeDto>? ScenarioNodes { get; set; }
    public string? TimeHorizon { get; set; }
    public CampaignReviewDataDto? ReviewData { get; set; }
    public string Status { get; set; } = "Draft";
    public string? TradeId { get; set; }
    public List<string>? TradeIds { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EstimateDto
{
    public List<OutcomeScenarioDto> Scenarios { get; set; } = new();
    public decimal? EvR { get; set; }
    public decimal? ImpliedWinProbability { get; set; }
    public bool FeesIncluded { get; set; }
    public DateTime CapturedAt { get; set; }
    public bool Locked { get; set; }
    public DateTime? LockedAt { get; set; }
}

public class PlanExpectationDto
{
    public decimal? EvR { get; set; }
    public decimal? ImpliedWinPercent { get; set; }
    /// <summary>EV theo VND = EV theo R × 1R.</summary>
    public decimal? EvAmount { get; set; }
}

public class PlanLotDto
{
    public int LotNumber { get; set; }
    public decimal PlannedPrice { get; set; }
    public int PlannedQuantity { get; set; }
    public decimal? AllocationPercent { get; set; }
    public string? Label { get; set; }

    [Description("Trạng thái lô (bỏ trống = Pending).")]
    public PlanLotStatus Status { get; set; } = PlanLotStatus.Pending;

    public decimal? ActualPrice { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? TradeId { get; set; }
}

public class ExitTargetDto
{
    public int Level { get; set; }

    [Description("Hành động tại mốc. TakeProfit = chốt lời; CutLoss = cắt lỗ; " +
                 "TrailingStop = chuyển sang trailing; PartialExit = thoát một phần. Bắt buộc. " +
                 "Lưu ý: tập giá trị này KHÁC với actionType của scenarioNodes.")]
    public ExitActionType? ActionType { get; set; }

    public decimal Price { get; set; }
    public int? Quantity { get; set; }
    public decimal? PercentOfPosition { get; set; }
    public string? Label { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public string? TradeId { get; set; }
}

public class StopLossHistoryDto
{
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>
/// Kiểu enum thật, không phải chuỗi: SDK sinh ra mảng "enum" trong inputSchema nên agent —
/// vốn chỉ thấy MCP và không đọc tài liệu — biết được tập giá trị hợp lệ ngay ở tools/list.
/// </summary>
public class ScenarioNodeDto
{
    public string NodeId { get; set; } = null!;
    public string? ParentId { get; set; }
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;

    [Description("Điều kiện kích hoạt nhánh. PriceAbove/PriceBelow so với conditionValue (VND); " +
                 "PricePercentChange so với giá vào (%); TimeElapsed tính theo số ngày. Bắt buộc.")]
    public ScenarioConditionType? ConditionType { get; set; }

    public decimal? ConditionValue { get; set; }
    public string? ConditionNote { get; set; }

    [Description("Hành động khi điều kiện chạm. SellPercent = bán actionValue% vị thế; " +
                 "SellAll = bán toàn bộ; MoveStopLoss = dời cắt lỗ tới actionValue (VND); " +
                 "MoveStopToBreakeven = dời cắt lỗ về giá hòa vốn; " +
                 "ActivateTrailingStop = bật trailing theo trailingStopConfig; " +
                 "AddPosition = mua thêm actionValue% vị thế; SendNotification = chỉ báo, không lệnh. " +
                 "Bắt buộc — không có giá trị mặc định.")]
    public ScenarioActionType? ActionType { get; set; }

    public decimal? ActionValue { get; set; }
    public TrailingStopConfigDto? TrailingStopConfig { get; set; }
    public ScenarioNodeStatus Status { get; set; } = ScenarioNodeStatus.Pending;
    public DateTime? TriggeredAt { get; set; }
    public string? TradeId { get; set; }
}

public class TrailingStopConfigDto
{
    [Description("Cách tính khoảng trailing (bỏ trống = Percentage).")]
    public TrailingStopMethod? Method { get; set; }

    [Description("Độ rộng trailing: % nếu Method=Percentage, số lần ATR nếu ATR, VND nếu FixedAmount.")]
    public decimal TrailValue { get; set; }

    public decimal? ActivationPrice { get; set; }
    public decimal? StepSize { get; set; }
    public decimal? CurrentTrailingStop { get; set; }
    public decimal? HighestPrice { get; set; }
}

public class CampaignReviewDataDto
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
    /// <summary>PnL ròng / 1R ban đầu. Review cũ thiếu giá trị thì tính khi đọc (chỉ trong DTO).</summary>
    public decimal? RealizedR { get; set; }
    public string? RiskBasis { get; set; }
    /// <summary>Số CP còn giữ lúc review (null = review cũ). Lớn hơn 0: kết quả chỉ tính phần đã bán.</summary>
    public decimal? OpenQuantity { get; set; }
    public decimal? DividendIncome { get; set; }
    public decimal? RealizedCost { get; set; }
    public decimal? OversoldQuantity { get; set; }
    public List<string>? Flags { get; set; }
    public LegacyReviewDto? Legacy { get; set; }
}
