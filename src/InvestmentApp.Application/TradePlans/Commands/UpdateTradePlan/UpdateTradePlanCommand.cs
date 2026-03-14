using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;

public class UpdateTradePlanCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string? Symbol { get; set; }
    public string? Direction { get; set; }
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? Target { get; set; }
    public int? Quantity { get; set; }
    public string? StrategyId { get; set; }
    public string? MarketCondition { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public decimal? RiskPercent { get; set; }
    public decimal? AccountBalance { get; set; }
    public decimal? RiskRewardRatio { get; set; }
    public int? ConfidenceLevel { get; set; }
    public List<ChecklistItemDto>? Checklist { get; set; }
    public string? EntryMode { get; set; }
    public List<PlanLotDto>? Lots { get; set; }
    public List<ExitTargetDto>? ExitTargets { get; set; }
}

public class UpdateTradePlanCommandHandler : IRequestHandler<UpdateTradePlanCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public UpdateTradePlanCommandHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<Unit> Handle(UpdateTradePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Trade plan {request.Id} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        var checklist = request.Checklist?.Select(c => new ChecklistItem
        {
            Label = c.Label,
            Category = c.Category,
            Checked = c.Checked,
            Critical = c.Critical,
            Hint = c.Hint
        }).ToList();

        plan.Update(
            request.Symbol, request.Direction, request.EntryPrice,
            request.StopLoss, request.Target, request.Quantity,
            request.PortfolioId, request.StrategyId, request.MarketCondition,
            request.Reason, request.Notes, request.RiskPercent,
            request.AccountBalance, request.RiskRewardRatio,
            request.ConfidenceLevel, checklist
        );

        // Multi-lot support
        if (request.EntryMode != null && request.Lots != null)
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
        if (request.ExitTargets != null)
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

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
