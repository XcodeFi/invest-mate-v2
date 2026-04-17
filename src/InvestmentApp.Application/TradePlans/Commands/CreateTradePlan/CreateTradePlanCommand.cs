using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;

public class CreateTradePlanCommand : IRequest<string>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string Symbol { get; set; } = null!;
    public string Direction { get; set; } = "Buy";
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }
    public int Quantity { get; set; }
    public string? StrategyId { get; set; }
    public string MarketCondition { get; set; } = "Trending";
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public decimal? RiskPercent { get; set; }
    public decimal? AccountBalance { get; set; }
    public decimal? RiskRewardRatio { get; set; }
    public int ConfidenceLevel { get; set; } = 5;
    public List<ChecklistItemDto>? Checklist { get; set; }
    public string? EntryMode { get; set; }
    public List<PlanLotDto>? Lots { get; set; }
    public List<ExitTargetDto>? ExitTargets { get; set; }
    public string? ExitStrategyMode { get; set; }
    public List<ScenarioNodeDto>? ScenarioNodes { get; set; }
    public string? TimeHorizon { get; set; }
    public string? Status { get; set; }
    public string? TradeId { get; set; }
}

public class ChecklistItemDto
{
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Checked { get; set; }
    public bool Critical { get; set; }
    public string Hint { get; set; } = string.Empty;
}

public class CreateTradePlanCommandHandler : IRequestHandler<CreateTradePlanCommand, string>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;

    public CreateTradePlanCommandHandler(ITradePlanRepository tradePlanRepository, ITradeRepository tradeRepository)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<string> Handle(CreateTradePlanCommand request, CancellationToken cancellationToken)
    {
        var checklist = request.Checklist?.Select(c => new ChecklistItem
        {
            Label = c.Label,
            Category = c.Category,
            Checked = c.Checked,
            Critical = c.Critical,
            Hint = c.Hint
        }).ToList();

        var plan = new TradePlan(
            request.UserId, request.Symbol, request.Direction,
            request.EntryPrice, request.StopLoss, request.Target, request.Quantity,
            request.PortfolioId, request.StrategyId,
            request.MarketCondition, request.Reason, request.Notes,
            request.RiskPercent, request.AccountBalance, request.RiskRewardRatio,
            request.ConfidenceLevel, checklist
        );

        // Multi-lot support
        if (request.EntryMode != null && request.Lots != null && request.Lots.Count > 0)
        {
            var entryMode = Enum.Parse<EntryMode>(request.EntryMode, ignoreCase: true);
            var lots = request.Lots.Select(l => new PlanLot
            {
                LotNumber = l.LotNumber,
                PlannedPrice = l.PlannedPrice,
                PlannedQuantity = l.PlannedQuantity,
                AllocationPercent = l.AllocationPercent,
                Label = l.Label
            }).ToList();
            plan.SetLots(entryMode, lots);
        }

        // Exit targets
        if (request.ExitTargets != null && request.ExitTargets.Count > 0)
        {
            var targets = request.ExitTargets.Select(e => new ExitTarget
            {
                Level = e.Level,
                ActionType = Enum.Parse<ExitActionType>(e.ActionType, ignoreCase: true),
                Price = e.Price,
                Quantity = e.Quantity,
                PercentOfPosition = e.PercentOfPosition,
                Label = e.Label
            }).ToList();
            plan.SetExitTargets(targets);
        }

        // Time horizon
        if (request.TimeHorizon != null && Enum.TryParse<TimeHorizon>(request.TimeHorizon, ignoreCase: true, out var horizon))
            plan.SetTimeHorizon(horizon);

        // Scenario Playbook
        if (request.ExitStrategyMode?.Equals("Advanced", StringComparison.OrdinalIgnoreCase) == true)
        {
            plan.SetExitStrategyMode(ExitStrategyMode.Advanced);
            if (request.ScenarioNodes != null && request.ScenarioNodes.Count > 0)
            {
                var nodes = request.ScenarioNodes.Select(MapToScenarioNode).ToList();
                plan.SetScenarioNodes(nodes);
            }
        }

        // Handle initial status if provided (e.g., "Ready" or "Executed" from wizard)
        // Must follow sequential state machine: Draft → Ready → InProgress → Executed
        if (request.Status == "Ready")
            plan.MarkReady();
        else if (request.Status == "Executed" && request.TradeId != null)
        {
            plan.MarkReady();
            plan.MarkInProgress();
            plan.Execute(request.TradeId);
        }

        await _tradePlanRepository.AddAsync(plan, cancellationToken);

        // Link trade to plan if executing
        if (request.TradeId != null)
        {
            var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
            if (trade != null)
            {
                trade.LinkTradePlan(plan.Id);
                await _tradeRepository.UpdateAsync(trade, cancellationToken);
            }
        }

        return plan.Id;
    }

    internal static ScenarioNode MapToScenarioNode(ScenarioNodeDto dto) => new()
    {
        NodeId = dto.NodeId,
        ParentId = dto.ParentId,
        Order = dto.Order,
        Label = dto.Label,
        ConditionType = Enum.Parse<ScenarioConditionType>(dto.ConditionType, ignoreCase: true),
        ConditionValue = dto.ConditionValue,
        ConditionNote = dto.ConditionNote,
        ActionType = Enum.Parse<ScenarioActionType>(dto.ActionType, ignoreCase: true),
        ActionValue = dto.ActionValue,
        TrailingStopConfig = dto.TrailingStopConfig != null ? new TrailingStopConfig
        {
            Method = Enum.Parse<TrailingStopMethod>(dto.TrailingStopConfig.Method, ignoreCase: true),
            TrailValue = dto.TrailingStopConfig.TrailValue,
            ActivationPrice = dto.TrailingStopConfig.ActivationPrice,
            StepSize = dto.TrailingStopConfig.StepSize
        } : null
    };
}
