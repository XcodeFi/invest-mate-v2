using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
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

    public GetTradePlansQueryHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<IEnumerable<TradePlanDto>> Handle(GetTradePlansQuery request, CancellationToken cancellationToken)
    {
        var plans = request.ActiveOnly
            ? await _tradePlanRepository.GetActiveByUserIdAsync(request.UserId, cancellationToken)
            : await _tradePlanRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        return plans.Select(MapToDto);
    }

    internal static TradePlanDto MapToDto(Domain.Entities.TradePlan p) => new()
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
        Reason = p.Reason,
        Notes = p.Notes,
        RiskPercent = p.RiskPercent,
        AccountBalance = p.AccountBalance,
        RiskRewardRatio = p.RiskRewardRatio,
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
            Status = l.Status.ToString(),
            ActualPrice = l.ActualPrice,
            ExecutedAt = l.ExecutedAt,
            TradeId = l.TradeId
        }).ToList(),
        ExitTargets = p.ExitTargets?.Select(e => new ExitTargetDto
        {
            Level = e.Level,
            ActionType = e.ActionType.ToString(),
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

    public GetTradePlanByIdQueryHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<TradePlanDto?> Handle(GetTradePlanByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.Id, cancellationToken);
        if (plan == null || plan.UserId != request.UserId) return null;
        return GetTradePlansQueryHandler.MapToDto(plan);
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
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public decimal? RiskPercent { get; set; }
    public decimal? AccountBalance { get; set; }
    public decimal? RiskRewardRatio { get; set; }
    public int ConfidenceLevel { get; set; }
    public List<ChecklistItemDto> Checklist { get; set; } = new();
    public string? EntryMode { get; set; }
    public List<PlanLotDto>? Lots { get; set; }
    public List<ExitTargetDto>? ExitTargets { get; set; }
    public List<StopLossHistoryDto>? StopLossHistory { get; set; }
    public string Status { get; set; } = "Draft";
    public string? TradeId { get; set; }
    public List<string>? TradeIds { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PlanLotDto
{
    public int LotNumber { get; set; }
    public decimal PlannedPrice { get; set; }
    public int PlannedQuantity { get; set; }
    public decimal? AllocationPercent { get; set; }
    public string? Label { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal? ActualPrice { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? TradeId { get; set; }
}

public class ExitTargetDto
{
    public int Level { get; set; }
    public string ActionType { get; set; } = "TakeProfit";
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
